using Serilog;
using HearthstoneServer.Entities;

namespace HearthstoneServer.Dsl;

// ═══════════════════════════════════════════════════════════
// Phase 10c — Special / Low-frequency Actions
// CastSpellTargetsEnemiesIfPossible, Adapt, AddProgress,
// ClearProgress, Replay, Invoke, Awaken, Dormant
// ═══════════════════════════════════════════════════════════

// ───────────────────────────────────────
// Joust — reveal cards from decks and compare cost
// ───────────────────────────────────────

/// <summary>
/// Performs a joust between a challenger and a defender.
/// The actual evaluation of the joust result is handled by JoustEvaluator.
/// </summary>
public class Joust : GameAction
{
	private static readonly ILogger _log = LogConfig.ForType<Joust>();

	private readonly BaseEntity? _challenger;
	private readonly BaseEntity? _defender;

	public Joust(BaseEntity? challenger, BaseEntity? defender)
	{
		_challenger = challenger;
		_defender = defender;
	}

	public override void Do(BaseEntity source, params object[] args)
	{
		var challenger = args.Length > 0 ? args[0] as BaseEntity ?? _challenger : _challenger;
		var defender = args.Length > 1 ? args[1] as BaseEntity ?? _defender : _defender;

		_log.Information("Jousting {Challenger} vs {Defender}", challenger, defender);
		source.Game?.Joust(source, challenger, defender, null);
	}
}

// ───────────────────────────────────────
// SummonJadeGolem — summon jade golem for a player
// ───────────────────────────────────────

/// <summary>
/// Summons a Jade Golem for the target player.
/// Jade Golem size increments each time (capped at 30).
/// </summary>
public class SummonJadeGolem : TargetedAction
{
	private static readonly ILogger _log = LogConfig.ForType<SummonJadeGolem>();

	public SummonJadeGolem(object target) : base(target) { }

	public override object? Do(BaseEntity source, BaseEntity target, params object[] args)
	{
		if (target is not Player player)
		{
			_log.Warning("SummonJadeGolem target is not a Player: {Target}", target);
			return null;
		}

		_log.Information("{Source} summons a Jade Golem for {Player}", source, player);

		// Build jade golem card id: CFM_712_t01 .. CFM_712_t30
		var jadeId = $"CFM_712_t{player.JadeGolem:D2}";

		// Increment jade counter (max 30)
		player.JadeGolem = Math.Min(30, player.JadeGolem + 1);

		// Create and summon the jade golem card
		var card = player.CreateCard(jadeId);
		if (card is BuffableEntity be && be.IsSummonable())
		{
			source.Game?.QueueActions(source,
				new List<Action> { new Summon(player, jadeId) });
		}

		return null;
	}
}

// ───────────────────────────────────────
// CastSpell — cast a spell with random target selection
// ───────────────────────────────────────

/// <summary>
/// Casts a spell. If the spell requires a target and none is provided,
/// one is chosen randomly from valid targets.
/// </summary>
public class CastSpell : TargetedAction
{
	private static readonly ILogger _log = LogConfig.ForType<CastSpell>();

	private readonly BaseEntity? _spellTarget;

	public CastSpell(object target, BaseEntity? spellTarget = null) : base(target)
	{
		_spellTarget = spellTarget;
	}

	/// <summary>Choose a random target for the spell. Can be overridden.</summary>
	protected virtual BaseEntity? ChooseTarget(BaseEntity source, BuffableEntity card)
	{
		var req = card.TargetReq;
		if (req == null || !req.HasTargeting) return null;

		var targets = Targeting.TargetingRules.GetValidTargets(card, req);
		if (targets.Count == 0) return null;

		return targets[source.Game?.Random.Next(targets.Count) ?? 0];
	}

	public override object? Do(BaseEntity source, BaseEntity target, params object[] args)
	{
		// If source is a dead/silenced/off-field minion, bail
		if (source.Type == CardType.Minion && source is Character ch)
		{
			if (ch.Dead || ch.GetBoolProperty("silenced") || ch.Zone != Zone.Play)
				return null;
		}

		if (target is not BuffableEntity card)
			return null;

		var player = (source as BuffableEntity)?.Controller;
		if (player == null) return null;

		// Save and clear choice
		var oldChoice = player.Choice;
		player.Choice = null;

		// Twinspell handling
		if (card is Spell spell && spell.Twinspell)
		{
			// Give a copy of the twinspell back to hand
			source.Game?.QueueActions(card,
				new List<Action> { new AddToHand(player, card.CardId ?? "") });
		}

		// Cast the spell
		var spellTarget = _spellTarget;
		if (card.BattlecryRequiresTarget() && spellTarget == null)
		{
			spellTarget = ChooseTarget(source, card);
			if (spellTarget == null)
			{
				_log.Information("{Source} cast spell {Card} has no legal target", source, card);
				player.Choice = oldChoice;
				return null;
			}
		}

		card.SetZone(Zone.Play);
		_log.Information("{Source} casts spell {Card} targeting {Target}", source, card, spellTarget);

		// Trigger battlecry actions
		source.Game?.QueueActions(card,
			new List<Action> { new BattlecryAction(card, spellTarget) });

		// Restore choice
		player.Choice = oldChoice;

		return null;
	}
}

// ───────────────────────────────────────
// CastSpellTargetsEnemiesIfPossible
// ───────────────────────────────────────

/// <summary>
/// Like CastSpell but prefers enemy targets when choosing randomly.
/// </summary>
public class CastSpellPreferEnemy : CastSpell
{
	public CastSpellPreferEnemy(object target, BaseEntity? spellTarget = null)
		: base(target, spellTarget) { }

	protected override BaseEntity? ChooseTarget(BaseEntity source, BuffableEntity card)
	{
		var req = card.TargetReq;
		if (req == null || !req.HasTargeting) return base.ChooseTarget(source, card);

		var targets = Targeting.TargetingRules.GetValidTargets(card, req);
		if (targets.Count == 0) return null;

		// 优先选敌方目标
		var enemies = targets.Where(t => t.Controller != (source as BuffableEntity)?.Controller).ToList();
		var pool = enemies.Count > 0 ? enemies : targets;
		return pool[source.Game?.Random.Next(pool.Count) ?? 0];
	}
}

// ───────────────────────────────────────
// CastRandomSpells — Yogg-Saron style: cast N random spells
// ───────────────────────────────────────

/// <summary>
/// 施放随机法术 N 次（N 取决于本场使用法术次数，上限 30）。
/// 每次施放后结算死亡，如果 source 死亡/被沉默/离场则停止。
/// 用法：new CastRandomSpells()  — 默认使用 SpellsCastThisGame.Count
///       new CastRandomSpells(fixedCount) — 固定次数
/// </summary>
public class CastRandomSpells : TargetedAction
{
	private static readonly ILogger _log = LogConfig.ForType<CastRandomSpells>();
	private readonly int _fixedCount;

	/// <param name="fixedCount">固定施放次数，0 表示使用 SpellsCastThisGame.Count。</param>
	public CastRandomSpells(int fixedCount = 0) : base(S.SELF)
	{
		_fixedCount = fixedCount;
	}

	public override object? Do(BaseEntity source, BaseEntity target, params object[] args)
	{
		var player = (source as BuffableEntity)?.Controller;
		var game = player?.Game;
		if (game == null || player == null) return null;

		int times = _fixedCount > 0
			? _fixedCount
			: Math.Min(player.SpellsCastThisGame.Count, 30);

		_log.Information("CastRandomSpells: {Source} will cast {Count} random spells", source, times);

		var spellPicker = RandomCards.RandomSpell().ToPicker();

		for (int i = 0; i < times; i++)
		{
			// 从卡池随机选一张法术
			var pool = spellPicker.FindCards(source);
			if (pool.Count == 0) break;

			var spellId = pool[game.Random.Next(pool.Count)];
			var spellEntity = player.CreateCard(spellId);
			if (spellEntity == null) continue;

			_log.Information("CastRandomSpells round {I}: casting {Spell}", i + 1, spellId);

			// 施放（CastSpell 内部处理随机目标选择）
			var castAction = new CastSpell(spellEntity);
			castAction.Trigger(source);

			// 结算死亡
			game.ProcessDeaths();

			// 中止条件：source 死亡/被沉默/离场
			if (source is Character ch)
			{
				if (ch.Dead || ch.GetBoolProperty("silenced") || ch.Zone != Zone.Play)
				{
					_log.Information("CastRandomSpells: {Source} is dead/silenced/off-field, stopping", source);
					break;
				}
			}
		}

		return null;
	}
}

// ───────────────────────────────────────
// Adapt — choose one of 3 random adaptation buffs
// ───────────────────────────────────────

/// <summary>
/// Adapts a target minion. Presents 3 random adaptation choices from a pool of 10.
/// The player picks one, and its battlecry effect is applied to the target.
/// </summary>
public class Adapt : TargetedAction
{
	private static readonly ILogger _log = LogConfig.ForType<Adapt>();

	/// <summary>The 10 adaptation card IDs (Un'Goro).</summary>
	private static readonly string[] AdaptChoices = new[]
	{
		"UNG_999t10", "UNG_999t2", "UNG_999t3", "UNG_999t4", "UNG_999t5",
		"UNG_999t6", "UNG_999t7", "UNG_999t8", "UNG_999t13", "UNG_999t14"
	};

	public Adapt(object target) : base(target) { }

	public override object? Do(BaseEntity source, BaseEntity target, params object[] args)
	{
		_log.Information("{Source} adapts {Target}", source, target);

		var player = (source as BuffableEntity)?.Controller;
		if (player == null) return null;

		// Pick 3 random adaptation cards
		var rng = source.Game?.Random ?? new Random();
		var shuffled = AdaptChoices.OrderBy(_ => rng.Next()).Take(3).ToList();

		// Create card instances for the choices
		var cards = new List<BaseEntity>();
		foreach (var cardId in shuffled)
		{
			var card = player.CreateCard(cardId);
			if (card != null) cards.Add(card);
		}

		// Set up as a Discover-like choice
		if (source.Game != null && cards.Count > 0)
		{
			source.Game.QueueActions(source,
				new List<Action> { new GenericChoice(player, cards) });
		}

		return null;
	}
}

// ───────────────────────────────────────
// AddProgress — add progress to a quest / upgradeable card
// ───────────────────────────────────────

/// <summary>
/// Adds progress to a target (e.g. Quest, SideQuest, upgradeable card).
/// When progress reaches the threshold, triggers the reward.
/// </summary>
public class AddProgress : TargetedAction
{
	private static readonly ILogger _log = LogConfig.ForType<AddProgress>();

	private readonly int _amount;

	public AddProgress(object target, int amount = 1) : base(target)
	{
		_amount = amount;
	}

	public override object? Do(BaseEntity source, BaseEntity target, params object[] args)
	{
		int amount = args.Length > 0 ? ActionHelper.ResolveInt(args, 0, source) : _amount;
		if (amount == 0) amount = _amount;

		_log.Information("{Target} add progress from {Source} (amount={Amount})", target, source, amount);

		if (target is Quest quest)
		{
			quest.AddProgress(amount);

			// If quest is complete and has reward actions, trigger them
			if (quest.Progress >= quest.ProgressTotal)
			{
				var rewardActions = quest.GetActions("reward") ?? new List<Action>();
				if (rewardActions.Count > 0 && source.Game != null)
				{
					source.Game.TriggerBlock(quest, rewardActions);
				}
			}
		}
		else if (target is BuffableEntity be)
		{
			// Generic progress for SideQuest or upgradeable cards
			var progress = be.GetIntProperty("progress") + amount;
			be.SetIntProperty("progress", progress);

			var total = be.GetIntProperty("progress_total");
			if (progress >= total && total > 0)
			{
				var rewardActions = be.GetActions("reward") ?? new List<Action>();
				if (rewardActions.Count > 0 && source.Game != null)
				{
					source.Game.TriggerBlock(target, rewardActions);
					be.SetZone(Zone.Graveyard);
				}
			}
		}

		return null;
	}
}

// ───────────────────────────────────────
// ClearProgress — clear progress on a quest
// ───────────────────────────────────────

/// <summary>
/// Clears the progress counter on a target quest/upgradeable card.
/// </summary>
public class ClearProgress : TargetedAction
{
	private static readonly ILogger _log = LogConfig.ForType<ClearProgress>();

	public ClearProgress(object target) : base(target) { }

	public override object? Do(BaseEntity source, BaseEntity target, params object[] args)
	{
		_log.Information("{Target} clear progress", target);

		if (target is Quest quest)
		{
			quest.Progress = 0;
		}
		else if (target is BuffableEntity be)
		{
			be.SetIntProperty("progress", 0);
		}

		return null;
	}
}

// ───────────────────────────────────────
// Replay — replay a card (Tess Greymane)
// ───────────────────────────────────────

/// <summary>
/// Replays a card: casts it if it's a spell, otherwise summons it.
/// Used by Tess Greymane (GIL_598).
/// </summary>
public class Replay : TargetedAction
{
	private static readonly ILogger _log = LogConfig.ForType<Replay>();

	public Replay(object target) : base(target) { }

	public override object? Do(BaseEntity source, BaseEntity target, params object[] args)
	{
		_log.Information("{Source} replays {Target}", source, target);

		if (source.Game == null) return null;

		if (target.Type == CardType.Spell)
		{
			// Cast the spell
			source.Game.QueueActions(source,
				new List<Action> { new CastSpell(target) });
		}
		else
		{
			// Summon the minion/weapon/hero
			var controller = (source as BuffableEntity)?.Controller;
			if (controller != null)
			{
				var cardId = (target as BuffableEntity)?.CardId ?? "";
				source.Game.QueueActions(source,
					new List<Action> { new Summon(controller, cardId) });
			}
		}

		return null;
	}
}

// ───────────────────────────────────────
// ReplayOtherClassCards — Tess Greymane
// ───────────────────────────────────────

/// <summary>
/// 重新施放本局中打出的所有非本职业卡牌（随机选取目标）。
/// <para>规则：</para>
/// <list type="bullet">
/// <item>随从 → Summon（不触发战吼/连击/流放/过载）</item>
/// <item>法术 → CastSpell（完整施放，触发连击/过载/法力渴求等）</item>
/// <item>武器 → 直接装备（不触发战吼）</item>
/// <item>排除中立牌和苔丝自身</item>
/// </list>
/// </summary>
public class ReplayOtherClassCards : TargetedAction
{
	private static readonly ILogger _log = LogConfig.ForType<ReplayOtherClassCards>();

	public ReplayOtherClassCards(object target) : base(target) { }

	public override object? Do(BaseEntity source, BaseEntity target, params object[] args)
	{
		var player = target as Player ?? (target as BuffableEntity)?.Controller;
		if (player?.Game == null || player.Hero?.Data == null) return null;

		var heroClass = player.Hero.Data.CardClass;
		var sourceCardId = (source as BuffableEntity)?.CardId;

		// 收集本局打出的所有非本职业卡牌（排除中立牌和苔丝自身）
		var otherClassCards = player.CardsPlayedThisGame
			.OfType<BuffableEntity>()
			.Where(c => c.CardData != null
				&& c.CardData.CardClass != heroClass
				&& c.CardData.CardClass != CardClass.Neutral
				&& c.CardId != sourceCardId)
			.ToList();

		if (otherClassCards.Count == 0)
		{
			_log.Information("{Source} has no other-class cards to replay", source);
			return null;
		}

		_log.Information("{Source} replays {Count} other-class cards", source, otherClassCards.Count);

		// 依次重放：法术 → CastSpell（完整施放），随从/武器 → Summon（无战吼）
		var actions = new List<Action>();
		foreach (var card in otherClassCards)
		{
			actions.Add(new Replay(card));
		}

		source.Game?.QueueActions(source, actions);
		return null;
	}
}

// ───────────────────────────────────────
// Invoke — invoke Galakrond
// ───────────────────────────────────────

/// <summary>
/// Invokes Galakrond: increments invoke counter, reveals the hero card,
/// triggers its hero power, and adds progress.
/// </summary>
public class Invoke : TargetedAction
{
	private static readonly ILogger _log = LogConfig.ForType<Invoke>();

	public Invoke(object target) : base(target) { }

	public override object? Do(BaseEntity source, BaseEntity target, params object[] args)
	{
		_log.Information("{Source} invokes {Target}", source, target);

		var player = (source as BuffableEntity)?.Controller;
		if (player == null) return null;

		player.InvokeCounter++;

		if (source.Game != null)
		{
			// Reveal Galakrond and add progress
			source.Game.QueueActions(source, new List<Action>
			{
				new RevealSecret(target),
				new AddProgress(target, 1)
			});

			// Play Galakrond's hero power if available
			if (target is BuffableEntity be)
			{
				var heroPowerActions = be.GetActions("hero_power") ?? new List<Action>();
				if (heroPowerActions.Count > 0)
				{
					source.Game.TriggerBlock(target, heroPowerActions);
				}
			}
		}

		return null;
	}
}

// ───────────────────────────────────────
// Awaken — wake up a dormant minion
// ───────────────────────────────────────

/// <summary>
/// Awakens a dormant minion: sets dormant=false, resets turns_in_play,
/// and triggers the "awaken" actions if any.
/// </summary>
public class Awaken : TargetedAction
{
	private static readonly ILogger _log = LogConfig.ForType<Awaken>();

	public Awaken(object target) : base(target) { }

	public override object? Do(BaseEntity source, BaseEntity target, params object[] args)
	{
		if (target is not Minion minion || !minion.Dormant)
			return null;

		_log.Information("{Source} awakens {Target}", source, target);

		minion.Dormant = false;
		minion.TurnsInPlay = 0;

		// Trigger awaken actions from script
		var script = CardScriptRegistry.Instance.GetScript(minion.CardId ?? "");
		if (script != null && script.Awaken.Length > 0 && source.Game != null)
		{
			source.Game.ActionBlock(minion, script.Awaken.ToList(), BlockType.Power);
		}

		return null;
	}
}

// ───────────────────────────────────────
// Dormant — set a minion to dormant state
// ───────────────────────────────────────

/// <summary>
/// Makes a target minion dormant for a specified number of turns.
/// </summary>
public class MakeDormant : TargetedAction
{
	private static readonly ILogger _log = LogConfig.ForType<MakeDormant>();

	private readonly int _turns;

	public MakeDormant(object target, int turns) : base(target)
	{
		_turns = turns;
	}

	public override object? Do(BaseEntity source, BaseEntity target, params object[] args)
	{
		int turns = args.Length > 0 ? ActionHelper.ResolveInt(args, 0, source) : _turns;
		if (turns == 0) turns = _turns;

		if (target is not Minion minion) return null;

		_log.Information("{Source} makes {Target} dormant for {Turns} turns", source, target, turns);

		minion.Dormant = true;
		minion.DormantTurns += turns;

		return null;
	}
}

// ───────────────────────────────────────
// Discover — open a 3-card discovery choice
// ───────────────────────────────────────

/// <summary>
/// Opens a Discover choice: presents 3 random collectible cards matching a filter.
/// When the player chooses, the chosen card is added to their hand (or triggers a callback).
/// </summary>
public class DiscoverCards : TargetedAction
{
	private static readonly ILogger _log = LogConfig.ForType<DiscoverCards>();
	private readonly RandomCardPicker _picker;

	/// <param name="target">The player or entity triggering the discover.</param>
	/// <param name="picker">A RandomCardPicker configured with the desired filters.</param>
	public DiscoverCards(object target, RandomCardPicker? picker = null) : base(target)
	{
		_picker = picker ?? RandomCards.RandomCollectible();
	}

	public override object? Do(BaseEntity source, BaseEntity target, params object[] args)
	{
		var player = target as Player ?? (target as BuffableEntity)?.Controller;
		if (player?.Game == null) return null;

		// Generate 3 discover options
		_picker.Count = 3;
		var result = _picker.Evaluate(source);
		var cards = new List<BaseEntity>();
		if (result is IEnumerable<BaseEntity> entities)
		{
			cards.AddRange(entities);
		}

		if (cards.Count == 0)
		{
			_log.Warning("Discover found no cards for {Source}", source);
			return null;
		}

		_log.Information("{Source} discovers {Count} cards for {Player}",
			source, cards.Count, player.Name);

		// Create a GenericChoice — when picked, the card goes to the player's hand
		var choice = new GenericChoiceAction(source, player, cards);
		player.Choice = choice;

		return null;
	}
}

// ───────────────────────────────────────
// GenericChoice — discover/choose result handler
// ───────────────────────────────────────

/// <summary>
/// Handles the result of a Discover or generic card choice.
/// The chosen card goes to the player's hand; unchosen cards are discarded.
/// </summary>
public class GenericChoiceAction : Choice
{
	private static readonly ILogger _log = LogConfig.ForType<GenericChoiceAction>();
	private readonly Player _player;

	public GenericChoiceAction(BaseEntity source, Player player, List<BaseEntity> cards)
	{
		Source = source;
		SourcePlayer = player;
		_player = player;
		Cards = cards;
		MinCount = 1;
		MaxCount = 1;
	}

	public override void Choose(BaseEntity card)
	{
		if (!Cards.Contains(card))
			throw new InvalidActionException(
				$"{card} is not a valid choice (one of {string.Join(", ", Cards)})");

		if (SourcePlayer != null) SourcePlayer.Choice = null;

		_log.Information("{Player} chose {Card} from discover",
			_player.Name, (card as BuffableEntity)?.CardId);

		// Chosen card → hand; others → discard
		foreach (var c in Cards)
		{
			if (c == card)
			{
				if (c is BuffableEntity be)
				{
					if (be.Type == CardType.HeroPower)
					{
						// Hero powers: assign directly to the player's hero
						if (_player.Hero != null)
						{
							_player.Hero.Power = (HeroPower)be;
						}
					}
					else if (_player.Hand.Count < _player.MaxHandSize)
					{
						_player.Hand.Add(be);
					}
					// else: hand full, card is lost
				}
			}
			// Unchosen cards are simply not added anywhere (effectively discarded)
		}
	}

	public override object? Do(BaseEntity source, BaseEntity target, params object[] args) => null;

	public override string ToString() => $"<GenericChoice: {Cards.Count} discover options>";
}

// ───────────────────────────────────────
// SummonChoice — present summon options, summon chosen
// ───────────────────────────────────────

/// <summary>
/// Presents a choice of minions to summon. When the player picks one,
/// it is summoned onto the battlefield.
/// Used by cards that let you choose which minion to summon.
/// </summary>
public class SummonChoiceAction : Choice
{
	private static readonly ILogger _log = LogConfig.ForType<SummonChoiceAction>();
	private readonly Player _player;

	public SummonChoiceAction(BaseEntity source, Player player, List<BaseEntity> choices)
	{
		Source = source;
		SourcePlayer = player;
		_player = player;
		Cards = choices;
		MinCount = 1;
		MaxCount = 1;
	}

	public override void Choose(BaseEntity card)
	{
		if (!Cards.Contains(card))
			throw new InvalidActionException(
				$"{card} is not a valid choice (one of {string.Join(", ", Cards)})");

		if (SourcePlayer != null) SourcePlayer.Choice = null;

		_log.Information("{Player} chose to summon {Card}",
			_player.Name, (card as BuffableEntity)?.CardId);

		// Summon the chosen entity
		if (card is BuffableEntity be)
		{
			_player.Summon(be);
		}
	}

	public override object? Do(BaseEntity source, BaseEntity target, params object[] args) => null;

	public override string ToString() => $"<SummonChoice: {Cards.Count} summon options>";
}

// ───────────────────────────────────────
// QuestTick — increment quest progress
// ───────────────────────────────────────

/// <summary>
/// 增加任务进度（调用 Quest.AddProgress）。
/// 用法：new QuestTick(S.SELF)
/// </summary>
public class QuestTick : TargetedAction
{
	public QuestTick(object target) : base(target) { }

	public override object? Do(BaseEntity source, BaseEntity target, params object[] args)
	{
		if (target is Quest quest)
			quest.AddProgress();
		return null;
	}

	public override string ToString() => "<QuestTick>";
}

// ───────────────────────────────────────
// CreateQuest — create and place a quest directly into Zone.Secret
// ───────────────────────────────────────

/// <summary>
/// 创建任务卡并直接放入 Zone.Secret，不触发打出事件。
/// 用于任务线的多阶段推进。
/// 用法：new CreateQuest(S.CONTROLLER, "SW_450t")
/// </summary>
public class CreateQuest : TargetedAction
{
	private static readonly ILogger _log = LogConfig.ForType<CreateQuest>();
	private readonly string _cardId;

	public CreateQuest(object target, string cardId) : base(target)
	{
		_cardId = cardId;
	}

	public override object? Do(BaseEntity source, BaseEntity target, params object[] args)
	{
		var player = (target as BuffableEntity)?.Controller
			?? (source as BuffableEntity)?.Controller;
		if (player == null) return null;

		var game = player.Game;
		if (game == null) return null;

		// 创建任务卡并直接放入 Zone.Secret
		var entity = player.CreateCard(_cardId, Zone.Secret);
		if (entity is not Quest quest)
		{
			_log.Warning("CreateQuest: {CardId} is not a Quest", _cardId);
			return null;
		}

		_log.Information("[CreateQuest] {Player} receives quest stage {CardId}", player.Name, _cardId);

		// 注册触发器（通过 OnActivate）
		var script = CardScriptRegistry.Instance.GetScript(_cardId);
		script?.OnActivate?.Invoke(quest, game);

		return quest;
	}

	public override string ToString() => $"<CreateQuest: {_cardId}>";
}
