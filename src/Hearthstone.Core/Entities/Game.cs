using System.Collections;
using Serilog;
using HearthstoneServer.Dsl;
using HearthstoneServer.Events;

namespace HearthstoneServer.Entities;

// ═══════════════════════════════════════════════════
// Game — the game state machine
// ═══════════════════════════════════════════════════

/// <summary>
/// Game state enum.
/// </summary>
public enum GameState
{
	Invalid = 0,
	Loading = 1,
	Running = 2,
	Complete = 3,
}

/// <summary>
/// The root game entity that manages turn flow, action queuing,
/// death processing, and game-over detection.
/// </summary>
public class Game : BuffableEntity
{
	private static readonly ILogger _log = LogConfig.ForType<Game>();

	// ── Constants ──

	public const int MAX_MINIONS_ON_FIELD = 7;
	public const int MAX_SECRETS_ON_PLAY = 5;

	// ── Players ──

	public Player Player1 { get; private set; } = null!;
	public Player Player2 { get; private set; } = null!;
	public Player[] Players { get; private set; } = Array.Empty<Player>();
	public Player? CurrentPlayer { get; set; }

	// ── State ──

	public GameState State { get; set; } = GameState.Invalid;
	public Step Step { get; set; } = Step.BeginFirst;
	public Step NextStep { get; set; } = Step.BeginShuffle;
	public int Turn { get; set; }
	public int Tick { get; set; }
	public new Zone Zone { get; set; } = Zone.Invalid;

	/// <summary>Whether this game uses Standard format (restricts card pool for Discover).</summary>
	public bool IsStandard { get; set; } = true;

	// ── Random ──

	public Random Random { get; private set; }

	// ── Global entity list ──

	/// <summary>All entities ever created in this game.</summary>
	public new List<BaseEntity> Entities { get; } = new();

	/// <summary>Card database for card lookups by CardId. Used by RandomCardPicker/Discover.</summary>
	private Dictionary<string, LoadCardIntoDbf.Card>? _cardDb;
	public Dictionary<string, LoadCardIntoDbf.Card>? CardDb
	{
		get => _cardDb;
		set
		{
			_cardDb = value;
			if (value != null)
			{
				var byDbf = new Dictionary<int, LoadCardIntoDbf.Card>();
				foreach (var c in value.Values)
				{
					if (c.DbfId != 0)
						byDbf[c.DbfId] = c;
				}
				CardDbByDbfId = byDbf;
			}
			else
			{
				CardDbByDbfId = null;
			}
		}
	}

	/// <summary>Card database indexed by DbfId, for O(1) lookups from tag references. Auto-built from CardDb.</summary>
	public Dictionary<int, LoadCardIntoDbf.Card>? CardDbByDbfId { get; private set; }

	/// <summary>Next available entity ID.</summary>
	private int _nextEntityId = 1;

	// ── Action / Play counter ──

	private const int MaxActionDepth = 10;
	private int _actionStack;
	private bool _processingDeaths;

	// ── Event System (Phase Pipeline) ──

	/// <summary>Central event bus for phase-based triggers.</summary>
	public EventBus EventBus { get; }

	/// <summary>Sequence executor for phased play/attack/heropower flows.</summary>
	public SequenceRunner Sequences { get; }

	/// <summary>回合管理器：BeginTurn / EndTurn 逻辑委托。</summary>
	public TurnManager Turns { get; }

	/// <summary>卡牌脚本注册表（可注入，默认为全局单例）。</summary>
	public CardScriptRegistry ScriptRegistry { get; }

	/// <summary>下一个可用的 PlayCounter 值。</summary>
	public int PlayCounterNext { get; set; }

	// ── PowerHistory (frontend communication) ──

	/// <summary>
	/// Callback invoked for each PowerHistory entry (ActionStart, ActionEnd, TagChange, etc.).
	/// The WebApi layer hooks this to stream events to the frontend.
	/// </summary>
	public Action<PowerHistory.PowerHistoryEntry>? OnPowerHistory { get; set; }

	/// <summary>Emit a PowerHistory entry to the observer.</summary>
	public void EmitPowerHistory(PowerHistory.PowerHistoryEntry entry)
		=> OnPowerHistory?.Invoke(entry);

	// ── Constructor ──

	public Game(Player player1, Player player2, int? seed = null, CardScriptRegistry? registry = null)
	{
		Type = CardType.Game;
		base.Game = this;
		Random = seed.HasValue ? new Random(seed.Value) : new Random();
		ScriptRegistry = registry ?? CardScriptRegistry.Instance;

		EventBus = new EventBus(this);
		Sequences = new SequenceRunner(this, EventBus);
		Turns = new TurnManager(this);

		Player1 = player1;
		Player2 = player2;
		Players = new[] { player1, player2 };

		foreach (var player in Players)
		{
			player.Game = this;
		}

		// Set opponent references
		Player1.Opponent = Player2;
		Player2.Opponent = Player1;
	}

	/// <summary>Parameterless constructor for test scenarios.</summary>
	public Game()
	{
		Type = CardType.Game;
		base.Game = this;
		Random = new Random();
		ScriptRegistry = CardScriptRegistry.Instance;
		EventBus = new EventBus(this);
		Sequences = new SequenceRunner(this, EventBus);
		Turns = new TurnManager(this);
	}

	// ── Entity management ──

	/// <summary>Register a new entity in the game.</summary>
	public int RegisterEntity(BaseEntity entity)
	{
		entity.EntityId = _nextEntityId++;
		entity.Game = this;
		Entities.Add(entity);

		// Auto-initialize Tags if not already set
		entity.EnsureTags();

		// Sync Controller tag for selector compatibility
		if (entity is BuffableEntity be && be.Controller != null)
			entity.Tags![GameTag.Controller] = be.Controller.EntityId;

		return entity.EntityId;
	}

	// ── Queries ──

	/// <summary>True if the game has ended.</summary>
	public bool Ended => State == GameState.Complete;

	/// <summary>All minions on the board (both players).</summary>
	public List<BaseEntity> Board
	{
		get
		{
			var list = new List<BaseEntity>();
			list.AddRange(Player1.Field);
			list.AddRange(Player2.Field);
			list.Sort((a, b) => a.PlayCounter.CompareTo(b.PlayCounter));
			return list;
		}
	}

	/// <summary>All characters (heroes + minions, both players).</summary>
	public List<BaseEntity> Characters
	{
		get
		{
			var list = new List<BaseEntity>();
			list.AddRange(Player1.Characters);
			list.AddRange(Player2.Characters);
			list.Sort((a, b) => a.PlayCounter.CompareTo(b.PlayCounter));
			return list;
		}
	}

	/// <summary>All live entities (field + heroes + weapons, both players).</summary>
	public List<BaseEntity> LiveEntities
	{
		get
		{
			var list = new List<BaseEntity>();
			foreach (var e in Player1.LiveEntities) list.Add(e);
			foreach (var e in Player2.LiveEntities) list.Add(e);
			list.Sort((a, b) => a.PlayCounter.CompareTo(b.PlayCounter));
			return list;
		}
	}

	/// <summary>Total minions killed this turn by both players.</summary>
	public int MinionsKilledThisTurn =>
		Player1.MinionsKilledThisTurn + Player2.MinionsKilledThisTurn;

	/// <summary>All hand entities (both players).</summary>
	public IEnumerable<BaseEntity> Hands
	{
		get
		{
			foreach (var e in Player1.Hand) yield return e;
			foreach (var e in Player2.Hand) yield return e;
		}
	}

	/// <summary>All deck entities (both players).</summary>
	public IEnumerable<BaseEntity> Decks
	{
		get
		{
			foreach (var e in Player1.Deck) yield return e;
			foreach (var e in Player2.Deck) yield return e;
		}
	}

	/// <summary>All active aura buffs in the game (for tick-based cleanup).</summary>
	public List<Auras.AuraBuff> ActiveAuraBuffs { get; } = new();

	// ── ActionBlock 引擎 ──

	/// <summary>
	/// Execute an action block: ActionStart → QueueActions → ActionEnd.
	/// When the action stack reaches 0, auto-calls RefreshAuras + ProcessDeaths.
	/// </summary>
	public List<object?> ActionBlock(BaseEntity source, List<Dsl.Action> actions,
		BlockType blockType = BlockType.Power, int index = -1,
		BaseEntity? target = null, object[]? eventArgs = null)
	{
		// 递归深度保护：防止事件触发链导致 StackOverflow
		if (_actionStack > MaxActionDepth)
		{
			_log.Warning("ActionBlock 递归深度超限 ({Depth})，跳过", _actionStack);
			return new List<object?>();
		}

		var oldTargets = new List<BaseEntity>(source.CurrentTargets);
		source.CurrentTarget = target;

		ActionStart(blockType, source, index, target);
		var ret = actions.Count > 0
			? QueueActions(source, actions, eventArgs)
			: new List<object?>();
		ActionEnd(blockType, source);

		source.CurrentTargets = oldTargets;
		return ret;
	}

	/// <summary>
	/// Start an action block. Increments the action stack (except for PLAY blocks).
	/// Emits PowerBlockStart to the frontend.
	/// </summary>
	public void ActionStart(BlockType type, BaseEntity source, int index, BaseEntity? target)
	{
		if (type != BlockType.Play)
			_actionStack++;
		var cardId = (source as BuffableEntity)?.CardId;
		EmitPowerHistory(new PowerHistory.PowerBlockStart(
			type, source.EntityId, index, target?.EntityId, cardId));
	}

	/// <summary>
	/// End an action block. When action stack reaches 0, auto-refreshes auras and processes deaths.
	/// </summary>
	public void ActionEnd(BlockType type, BaseEntity source)
	{
		if (Ended) return;

		if (type != BlockType.Play)
			_actionStack--;

		EmitPowerHistory(new PowerHistory.PowerBlockEnd(type, source.EntityId));

		if (_actionStack == 0)
		{
			_log.Debug("Empty action stack, refreshing auras and processing deaths");
			RefreshAuras();
			ProcessDeaths();
		}
	}

	/// <summary>
	/// Queue a list of actions for processing. Sets event_args on source during execution.
	/// </summary>
	public List<object?> QueueActions(BaseEntity source, List<Dsl.Action> actions, object[]? eventArgs = null)
	{
		var oldEventArgs = source.EventArgs;
		source.EventArgs = eventArgs;
		var ret = TriggerActions(source, actions);
		source.EventArgs = oldEventArgs;
		return ret;
	}

	/// <summary>
	/// Execute a list of actions from source.
	/// </summary>
	public List<object?> TriggerActions(BaseEntity source, List<Dsl.Action> actions)
	{
		var ret = new List<object?>();
		foreach (var action in actions)
		{
			if (action is Dsl.TargetedAction ta)
			{
				var result = ta.Trigger(source);
				ret.AddRange(result);
			}
			else if (action is Dsl.GameAction ga)
			{
				ga.Trigger(source);
				ret.Add(null);
			}
		}
		return ret;
	}

	/// <summary>
	/// Perform actions as a TRIGGER block (used by entity.trigger_event).
	/// </summary>
	public void TriggerBlock(BaseEntity source, List<Dsl.Action> actions, object[]? eventArgs = null)
	{
		ActionBlock(source, actions, BlockType.Trigger, eventArgs: eventArgs);
	}

	/// <summary>
	/// Perform a joust between a challenger and a defender.
	/// </summary>
	public void Joust(BaseEntity source, BaseEntity? challenger, BaseEntity? defender, Dsl.Action? callback)
	{
		var actions = callback != null ? new List<Dsl.Action> { callback } : new List<Dsl.Action>();
		ActionBlock(source, actions, BlockType.Joust, eventArgs: new object[] { challenger!, defender! });
	}



	/// <summary>
	/// Refresh all auras in the game.
	/// Iterates all entities' update_scripts, sorts by priority, executes, then cleans stale buffs.
	/// </summary>
	public void RefreshAuras()
	{
		// 收集所有 entity 的 update_scripts
		var refreshQueue = new List<(BuffableEntity entity, Auras.Refresh script)>();

		foreach (var entity in Entities)
		{
			if (entity is BuffableEntity be)
			{
				foreach (var script in be.UpdateScripts)
					refreshQueue.Add((be, script));
			}
		}

		// 手牌中的 entity 也可能有 aura 脚本（如手上的卡效果）
		foreach (var hand in Hands)
		{
			if (hand is BuffableEntity be)
			{
				foreach (var script in be.UpdateScripts)
					refreshQueue.Add((be, script));
			}
		}

		// 牌库中的 entity 可能有 DeckUpdate 光环
		foreach (var deckCard in Decks)
		{
			if (deckCard is BuffableEntity be)
			{
				foreach (var script in be.UpdateScripts)
					refreshQueue.Add((be, script));
			}
		}

		// 按 priority 排序（数字越小越先执行）
		refreshQueue.Sort((a, b) => a.script.Priority.CompareTo(b.script.Priority));

		// 执行所有 refresh 脚本
		foreach (var (entity, script) in refreshQueue)
			script.Trigger(entity);

		// 清除过时的 aura buff（tick < 当前 tick）
		var toRemove = ActiveAuraBuffs.Where(b => b.Tick < Tick).ToList();
		foreach (var buff in toRemove)
			buff.Remove();

		Tick++;
	}



	/// <summary>
	/// Trade a card: shuffle a Tradeable card from hand into the deck and draw a new card.
	/// Costs 1 mana. The card must have the Tradeable mechanic.
	/// </summary>
	public void TradeCard(BuffableEntity card)
	{
		var player = card.Controller;
		if (player == null) return;

		// Validate Tradeable tag
		if (!card.GetBoolProperty(GameTag.Tradeable))
			throw new InvalidActionException($"{card.CardId} is not Tradeable");

		// Validate mana (costs 1)
		if (!player.CanPayCost(1))
			throw new InvalidActionException("Not enough mana to trade (costs 1)");

		_log.Information("Player {Player} trades {Card}", player.Name, card.CardId);

		// Pay 1 mana
		player.PayCost(1);

		// Remove from hand
		player.Hand.Remove(card);

		// Shuffle into deck at random position
		if (player.Deck.Count > 0)
		{
			int pos = Random.Next(player.Deck.Count + 1);
			player.Deck.Insert(pos, card);
		}
		else
		{
			player.Deck.Add(card);
		}
		card.SetZone(Zone.Deck);

		// Draw a new card
		player.DrawCard();
	}

	/// <summary>
	/// Play a card from a player's hand (backward-compatible single-target overload).
	/// </summary>
	public void PlayCard(BuffableEntity card, Character? target = null, int index = -1)
	{
		PlayCard(card, target != null ? new[] { target } : null, index);
	}

	/// <summary>
	/// Play a card from a player's hand. Supports multiple targets for future cards
	/// (e.g. Chillin' Vol'jin: choose two minions, swap their stats).
	/// Delegates to the entity's Play() method which handles mana payment,
	/// zone transition, and script triggering.
	/// </summary>
	public void PlayCard(BuffableEntity card, Character[]? targets, int index = -1)
	{
		var player = card.Controller;
		if (player == null) return;

		var target = targets != null && targets.Length > 0 ? targets[0] : null;

		_log.Information("Player {Player} plays {Card}", player.Name, card.CardId ?? card.EntityId.ToString());

		// ── Target validation ──
		var script = ScriptRegistry.GetScript(card.CardId ?? "");

		// Validate target requirement
		if (card.RequiresTarget && target == null)
		{
			throw new InvalidActionException(
				$"{card.CardId} requires a target but none was provided");
		}

		// Validate target legality
		if (target != null && card.TargetReq != null)
		{
			if (!Targeting.TargetingRules.IsValidTarget(card, target, card.TargetReq))
			{
				throw new InvalidActionException(
					$"{target} is not a valid target for {card.CardId}");
			}
		}

		// Validate board-state play conditions (e.g. MinEnemyMinions for Multi-Shot)
		if (card.TargetReq != null)
		{
			var req = card.TargetReq;
			var opponent = player.Opponent;
			if (req.MinEnemyMinions > 0 && opponent != null)
			{
				int enemyMinionCount = opponent.Field.Count;
				if (enemyMinionCount < req.MinEnemyMinions)
				{
					throw new InvalidActionException(
						$"{card.CardId} requires at least {req.MinEnemyMinions} enemy minion(s) but found {enemyMinionCount}");
				}
			}
			if (req.MinTotalMinions > 0)
			{
				int totalMinions = player.Field.Count + (player.Opponent?.Field.Count ?? 0);
				if (totalMinions < req.MinTotalMinions)
				{
					throw new InvalidActionException(
						$"{card.CardId} requires at least {req.MinTotalMinions} minion(s) on the board but found {totalMinions}");
				}
			}
			if (req.NeedsWeapon && player.Hero?.EquippedWeapon == null)
			{
				throw new InvalidActionException(
					$"{card.CardId} requires a weapon equipped but none was found");
			}
		}

		// ── 统计更新（在 SequenceRunner 执行前记录） ──
		player.CardsPlayedThisTurn++;
		if (card is Minion) player.MinionsPlayedThisTurn++;
		player.LastCardPlayed = card;
		player.CardsPlayedThisGame.Add(card);
		if (card.Type == CardType.Spell)
			player.SpentManaOnSpellsThisGame += card.Cost;

		// ── Combo 检测 ──
		bool useCombo = player.Combo
			&& script != null
			&& script.Combo.Length > 0;

		// ── Outcast 检测（打出前卡牌仍在手牌中） ──
		bool isOutcast = false;
		if (script != null && script.Outcast.Length > 0)
		{
			int handIdx = player.Hand.IndexOf(card);
			if (handIdx == 0 || handIdx == player.Hand.Count - 1)
				isOutcast = true;
		}

		// Combo flag: 打出第一张卡牌后激活
		if (player.CardsPlayedThisTurn >= 1)
			player.Combo = true;

		// ── Store all targets on the card entity for S.TARGET / S.TARGET_2 access ──
		if (targets != null && targets.Length > 0)
		{
			card.CurrentTargets = targets.Cast<BaseEntity>().ToList();
		}

		// ── 分派到 SequenceRunner ──
		var chooseCards = script?.ChooseCards ?? Array.Empty<string>();

		if (chooseCards.Length > 0)
		{
			// Choose One 分支
			bool chooseBoth = player.GetBoolProperty("choose_both");
			if (chooseBoth)
			{
				_log.Information("{Card} plays both choose-one options (ChooseBoth)", card.CardId);
				DispatchToSequence(card, script, target, index, false, isOutcast);
			}
			else
			{
				_log.Information("{Card} has choose-one options: {Options}", card.CardId, chooseCards);
				player.PayCost(card.Cost);
				card.SetZone(Zone.Play);
				card.TurnPlayed = Turn;

				var choiceEntities = new List<BaseEntity>();
				foreach (var choiceId in chooseCards)
				{
					var choiceCard = player.CreateCard(choiceId);
					if (choiceCard != null)
						choiceEntities.Add(choiceCard);
				}
				if (choiceEntities.Count > 0)
					player.Choice = new Dsl.ChooseOneChoice(card, target, choiceEntities);
			}
		}
		else
		{
			// 正常打出（或 Combo）
			DispatchToSequence(card, script, target, index, useCombo, isOutcast);
		}
	}

	/// <summary>
	/// 分派到 SequenceRunner 执行正确的阶段流水线。
	/// </summary>
	private void DispatchToSequence(BuffableEntity card, Dsl.ICardScript? script, Character? target, int index, bool useCombo, bool isOutcast = false)
	{
		if (card is Minion minion)
		{
			Sequences.PlayMinion(minion, target, index, script, useCombo, isOutcast);
		}
		else if (card is Secret || card is Quest)
		{
			// Secret/Quest 继承 Spell，但需要走 PlaySecret 路径
			Sequences.PlaySecret(card, script);
		}
		else if (card.Type == CardType.Spell)
		{
			Sequences.PlaySpell(card, target, script, useCombo, isOutcast);
		}
		else if (card is Hero hero)
		{
			Sequences.PlayHeroCard(hero, target, script, useCombo);
		}
		else if (card is Weapon weapon)
		{
			Sequences.PlayWeapon(weapon, target, script);
		}
		else if (card is Location location)
		{
			Sequences.PlayLocation(location, script);
		}
		else
		{
			// Fallback: 未知卡牌类型，走通用逻辑
			var player = card.Controller;
			if (player == null) return;
			player.PayCost(card.Cost);
			card.TurnPlayed = Turn;
			if (script != null && script.Play.Length > 0)
				ActionBlock(card, script.Play.ToList(), BlockType.Power, target: target);
		}
	}

	/// <summary>
	/// Use a player's hero power. Validates playability, pays mana, and triggers the power's script.
	/// </summary>
	public void UseHeroPower(Player player, Character? target = null)
	{
		var power = player.Hero?.Power;
		if (power == null)
			throw new InvalidActionException("Player has no hero power");

		if (!power.IsUsable)
			throw new InvalidActionException($"Hero power is not usable (exhausted={power.Exhausted}, mana={player.Mana}/{power.Cost})");

		_log.Information("Player {Player} uses hero power {Power}", player.Name, power.CardId ?? power.EntityId.ToString());

		// Pay mana cost
		player.PayCost(power.Cost);

		// Increment activation counter
		power.ActivationsThisTurn++;

		// Trigger hero power script (Play actions)
		var cardId = power.CardId ?? "";
		var script = ScriptRegistry.GetScript(cardId);
		_log.Information("UseHeroPower: CardId={CardId}, CardData={CardData}, ScriptFound={Found}, TotalScripts={Total}",
			cardId, power.CardData?.CardId ?? "null", script != null, ScriptRegistry.Count);
		if (script != null && script.Play.Length > 0)
		{
			ActionBlock(power, script.Play.ToList(), BlockType.Power, target: target);
		}
	}

	/// <summary>
	/// Use a location card. Validates usability and triggers the location's effect.
	/// </summary>
	public void UseLocation(Player player, Location location, Character? target = null)
	{
		if (!location.IsUsable)
			throw new InvalidActionException(
				$"Location {location.CardId} is not usable (durability={location.Durability}, cooldown={location.Cooldown})");

		_log.Information("Player {Player} uses location {Location}",
			player.Name, location.CardId ?? location.EntityId.ToString());

		Sequences.UseLocation(player, location, target);
	}

	/// <summary>
	/// Use one of a Titan minion's abilities. Validates the titan and ability index.
	/// </summary>
	public void UseTitanAbility(Player player, Minion titan, int abilityIndex, Character? target = null)
	{
		if (!titan.IsTitan)
			throw new InvalidActionException($"{titan.CardId} is not a Titan");

		if (titan.Silenced)
			throw new InvalidActionException($"Titan {titan.CardId} is silenced");

		if (abilityIndex < 0 || abilityIndex > 2)
			throw new InvalidActionException($"Invalid ability index {abilityIndex} (must be 0-2)");

		bool used = abilityIndex switch
		{
			0 => titan.TitanAbility1Used,
			1 => titan.TitanAbility2Used,
			2 => titan.TitanAbility3Used,
			_ => true
		};
		if (used)
			throw new InvalidActionException($"Titan {titan.CardId} ability {abilityIndex + 1} already used");

		var abilityIds = titan.TitanAbilityCardIds;
		if (abilityIndex >= abilityIds.Length)
			throw new InvalidActionException($"Titan {titan.CardId} has no ability at index {abilityIndex}");

		_log.Information("Player {Player} uses Titan {Titan} ability {Index} ({AbilityId})",
			player.Name, titan.CardId, abilityIndex + 1, abilityIds[abilityIndex]);

		Sequences.UseTitanAbility(player, titan, abilityIndex, target);
	}

	// ── Combat ──

	/// <summary>The proposed attacker (for event interception/redirection).</summary>
	public Character? ProposedAttacker { get; set; }

	/// <summary>The proposed defender (for event interception/redirection).</summary>
	public Character? ProposedDefender { get; set; }

	/// <summary>
	/// Execute an attack action block: attacker trades blows with defender.
	/// 通过 ActionBlock(ATTACK) 包装，确保 _actionStack 正确管理。
	/// Validates attack legality including taunt enforcement.
	/// </summary>
	public void Attack(Character attacker, Character defender)
	{
		// Validate attack legality (includes taunt check via AttackTargets)
		if (!attacker.CanAttack(defender))
		{
			_log.Warning("{Attacker} cannot attack {Defender} (taunt or other restriction)",
				attacker.CardId ?? attacker.EntityId.ToString(),
				defender.CardId ?? defender.EntityId.ToString());
			return;
		}

		_log.Information("{Attacker} attacks {Defender}",
			attacker.CardId ?? attacker.EntityId.ToString(),
			defender.CardId ?? defender.EntityId.ToString());

		// Delegate to SequenceRunner for correct phase pipeline
		Sequences.Combat(attacker, defender);
	}


	// ── Setup & Start ──

	/// <summary>
	/// Initialize the game: set player opponents, zones, prepare for game.
	/// </summary>
	public void Setup()
	{
		_log.Information("Setting up game");
		State = GameState.Running;
		Step = Step.BeginDraw;
		Zone = Zone.Play;

		Player1.Opponent = Player2;
		Player2.Opponent = Player1;

		foreach (var player in Players)
		{
			RegisterEntity(player);
			player.Zone = Zone.Play;

			// Register hero so selectors can find it (skip if already registered by PrepareForGame)
			if (player.Hero != null)
			{
				player.Hero.Controller = player;
				if (!Entities.Contains(player.Hero))
					RegisterEntity(player.Hero);
				player.Hero.Zone = Zone.Play;
			}
		}

		// Pick first player (default: Player1)
		var (first, second) = PickFirstPlayer();
		Player1 = first;
		Player1.FirstPlayer = true;
		Player2 = second;
		Player2.FirstPlayer = false;
		Players = new[] { Player1, Player2 };

		// ── EventBus 历史追踪订阅 ──
		SubscribeHistoryTracking();
	}

	/// <summary>
	/// 订阅 EventBus 事件，自动填充 Player 实体历史列表。
	/// </summary>
	private void SubscribeHistoryTracking()
	{
		EventBus.Subscribe<DeathEvent>(null, MiscPhase.On,
			_ => true,
			evt =>
			{
				if (evt.Dead is BuffableEntity be && be.Controller != null)
				{
					be.Controller.DeathsThisTurn.Add(be);
					be.Controller.DeathsThisGame.Add(be);
				}
			});

		EventBus.Subscribe<SpellCastEvent>(null, MiscPhase.On,
			_ => true,
			evt =>
			{
				if (evt.Spell.Controller != null)
				{
					evt.Spell.Controller.SpellsCastThisTurn.Add(evt.Spell);
					evt.Spell.Controller.SpellsCastThisGame.Add(evt.Spell);
				}
			});

		EventBus.Subscribe<CardPlayedEvent>(null, MiscPhase.On,
			_ => true,
			evt =>
			{
				if (evt.Card.Controller != null)
				{
					evt.Card.Controller.CardsPlayedThisTurnEntities.Add(evt.Card);
				}
			});

		EventBus.Subscribe<DrawCardEvent>(null, MiscPhase.On,
			_ => true,
			evt =>
			{
				if (evt.Card != null)
				{
					evt.Player.CardsDrawnThisTurnEntities.Add(evt.Card);
				}
			});
	}

	/// <summary>
	/// Start the game: setup, begin first turn.
	/// </summary>
	public void Start()
	{
		Setup();

		// Initial draw: first player draws 3, second player draws 4
		InitialDraw();

		// Give The Coin to the second player
		GiveCoin(Player2);

		BeginTurn(Player1);
	}

	/// <summary>
	/// Draw initial hands: first player draws 3 cards, second player draws 4.
	/// </summary>
	public void InitialDraw()
	{
		_log.Information("Initial draw: {P1} draws 3, {P2} draws 4",
			Player1.Name, Player2.Name);

		for (int i = 0; i < 3; i++)
			Player1.DrawCard();
		for (int i = 0; i < 4; i++)
			Player2.DrawCard();
	}

	/// <summary>
	/// Mulligan: replace selected cards from hand, redraw the same number, shuffle deck.
	/// </summary>
	public void Mulligan(Player player, List<BaseEntity> toReplace)
	{
		if (toReplace.Count == 0) return;

		int count = toReplace.Count;
		_log.Information("{Player} mulligans {Count} cards", player.Name, count);

		// Put cards back into deck (use manual list ops like DrawCard)
		foreach (var card in toReplace)
		{
			player.Hand.Remove(card);
			player.Deck.Add(card);
		}

		// Shuffle deck
		player.ShuffleDeck();

		// Draw replacement cards
		for (int i = 0; i < count; i++)
			player.DrawCard();
	}

	/// <summary>
	/// Give The Coin to the second player (going second).
	/// Creates a Coin entity (CS2_009t) and adds it to the player's hand.
	/// </summary>
	public void GiveCoin(Player player)
	{
		var coinData = new HearthstoneServer.LoadCardIntoDbf.Card
		{
			CardId = "GAME_005",
			CardType = CardType.Spell,
			Cost = 0
		};
		var coin = player.CreateCard(coinData, Zone.Hand);
		if (coin != null)
			_log.Information("{Player} receives The Coin", player.Name);
	}

	// ── Pick first player (CoinRules override) ──

	/// <summary>Randomly determine which player goes first.</summary>
	public virtual (Player first, Player second) PickFirstPlayer()
	{
		// CoinRules: random choice
		if (Random.Next(2) == 0)
			return (Player1, Player2);
		return (Player2, Player1);
	}

	// ── Turn flow ──

	/// <summary>
	/// Begin a new turn for the given player.
	/// 实际逻辑委托给 TurnManager。
	/// </summary>
	public virtual void BeginTurn(Player player) => Turns.BeginTurn(player);

	/// <summary>
	/// End the current turn and begin the opponent's turn.
	/// 实际逻辑委托给 TurnManager。
	/// </summary>
	public virtual void EndTurn() => Turns.EndTurn();

	// ── Death processing ──

	/// <summary>
	/// Check for and process dead entities.
	/// 用 ActionStart/ActionEnd(DEATHS) 包装，防止 ActionEnd→ProcessDeaths 递归。
	/// </summary>
	public void ProcessDeaths()
	{
		if (_processingDeaths) return; // 防止重入
		_processingDeaths = true;

		// 循环：亡语可能导致新死亡，需要反复检查直到无新死亡
		bool hasNewDeaths;
		do
		{
			var deadEntities = new List<BaseEntity>();

			foreach (var entity in LiveEntities)
			{
				if (entity is Minion m && m.Dead)
					deadEntities.Add(entity);
				else if (entity is Weapon w && w.Dead)
					deadEntities.Add(entity);
				else if (entity is Location loc && loc.Dead)
					deadEntities.Add(entity);
			}

			hasNewDeaths = deadEntities.Count > 0;
			if (!hasNewDeaths) break;

			_log.Debug("Processing {Count} deaths", deadEntities.Count);

			// 用 DEATHS 类型包装
			ActionStart(BlockType.Deaths, this, 0, null);

			foreach (var entity in deadEntities)
			{
				if (entity is Minion m)
				{
					// Save field position for deathrattle summon positioning
					int fieldIdx = m.Controller?.Field.IndexOf(m) ?? -1;
					m.SetIntProperty("death_field_index", fieldIdx);
					m.SummonIndex = 0; // Reset for deathrattle multi-summon

					// 移入墓地
					m.Controller?.Field.Remove(m);
					m.Controller?.Graveyard.Add(m);
					if (m.Controller != null)
						m.Controller.MinionsKilledThisTurn++;
					m.Zone = Zone.Graveyard;
					_log.Debug("Minion {EntityId} dies", m.EntityId);

					// 残骸系统：友方随从死亡时获得 1 份残骸
					m.Controller?.GainCorpses(1);

					// Publish DeathEvent（亡语触发前，让 "在...死亡时" 订阅者参与队列）
					EventBus.Publish(new DeathEvent(m, m), MiscPhase.On);

					// 触发亡语脚本
					var script = ScriptRegistry.GetScript(m.CardId ?? "");
					if (script?.Deathrattle.Length > 0)
					{
						_log.Debug("Triggering deathrattle for {EntityId}", m.EntityId);
						ActionBlock(m, script.Deathrattle.ToList(), BlockType.Trigger);
					}

					// 秘密抉择亡语：根据 secret_deathrattle tag 选择索引对应的亡语变体
					int secretIdx = m.GetIntProperty("secret_deathrattle");
					if (secretIdx > 0 && script?.SecretDeathrattles.Length >= secretIdx)
					{
						_log.Debug("Triggering secret deathrattle index {Idx} for {EntityId}",
							secretIdx, m.EntityId);
						ActionBlock(m, script.SecretDeathrattles[secretIdx - 1].ToList(), BlockType.Trigger);
					}

					// 死亡后清除 EventBus 订阅
					EventBus.UnsubscribeAll(m);
				}
				else if (entity is Weapon w)
				{
					if (w.Controller?.Hero != null)
						w.Controller.Hero.EquippedWeapon = null;
					_log.Debug("Weapon {EntityId} breaks", w.EntityId);

					// Publish DeathEvent for weapon
					EventBus.Publish(new DeathEvent(w, w), MiscPhase.On);

					// 武器亡语
					var wScript = ScriptRegistry.GetScript(w.CardId ?? "");
					if (wScript?.Deathrattle.Length > 0)
					{
						ActionBlock(w, wScript.Deathrattle.ToList(), BlockType.Trigger);
					}

					// 清除 EventBus 订阅
					EventBus.UnsubscribeAll(w);
				}
				else if (entity is Location loc)
				{
					loc.Controller?.Field.Remove(loc);
					loc.Controller?.Graveyard.Add(loc);
					loc.Zone = Zone.Graveyard;
					_log.Debug("Location {EntityId} destroyed", loc.EntityId);

					EventBus.Publish(new DeathEvent(loc, loc), MiscPhase.On);
					EventBus.UnsubscribeAll(loc);
				}
			}

			ActionEnd(BlockType.Deaths, this);

		} while (hasNewDeaths);

		_processingDeaths = false;

		// Check for game end after processing deaths
		CheckForEndGame();
	}

	// ── Game-over detection ──

	/// <summary>
	/// Check if one or more players are losing and end the game if so.
	/// </summary>
	public void CheckForEndGame()
	{
		bool gameover = false;

		foreach (var player in Players)
		{
			if (player.PlayState is PlayState.Conceded or PlayState.Disconnected)
				player.PlayState = PlayState.Losing;
			if (player.Hero != null && player.Hero.Dead)
				player.PlayState = PlayState.Losing;
			if (player.PlayState == PlayState.Losing)
				gameover = true;
		}

		if (!gameover) return;

		if (Player1.PlayState == Player2.PlayState)
		{
			// Both losing = tie
			foreach (var player in Players)
				player.PlayState = PlayState.Tied;
		}
		else
		{
			foreach (var player in Players)
			{
				if (player.PlayState == PlayState.Losing)
					player.PlayState = PlayState.Lost;
				else
					player.PlayState = PlayState.Won;
			}
		}

		State = GameState.Complete;
		_log.Information("Game over! P1={P1State}, P2={P2State}",
			Player1.PlayState, Player2.PlayState);
	}

	// ── Skip turn (testing helper) ──

	/// <summary>Skip two turns (current + opponent), returning to current player.</summary>
	public void SkipTurn()
	{
		EndTurn();
		EndTurn();
	}

	// ── CheatAction (testing helper) ──

	/// <summary>
	/// Perform actions as if a card had just triggered them.
	/// Useful for testing and debugging card effects outside normal play flow.
	/// </summary>
	public void CheatAction(BaseEntity source, List<Dsl.Action> actions)
	{
		_log.Debug("CheatAction: triggering {Count} actions from {Source}", actions.Count, source);
		TriggerActions(source, actions);
	}

	// ── Dump ──

	public override Dictionary<string, object> Dump()
	{
		var data = base.Dump();
		data["state"] = (int)State;
		data["step"] = (int)Step;
		data["turn"] = Turn;
		data["current_player"] = CurrentPlayer?.Name ?? "none";
		data["player1"] = Player1.Dump();
		data["player2"] = Player2.Dump();
		return data;
	}
}
