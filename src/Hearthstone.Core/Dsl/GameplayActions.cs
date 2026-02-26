using Serilog;
using HearthstoneServer.Entities;

namespace HearthstoneServer.Dsl;

// ═══════════════════════════════════════════════════
// Phase 10b — P0 核心游戏流程 action 类
// ═══════════════════════════════════════════════════

// ─── MulliganChoice ──────────

/// <summary>
/// 调度阶段选牌。
/// </summary>
public class MulliganChoice : GameAction
{
	private static readonly ILogger _log = LogConfig.ForType<MulliganChoice>();

	private readonly System.Action? _callback;
	public Player? TargetPlayer { get; private set; }
	public List<BaseEntity> Cards { get; private set; } = new();
	public int MinCount { get; set; }
	public int MaxCount { get; set; }

	public MulliganChoice(System.Action? callback = null) : base()
	{
		_callback = callback;
	}

	public override void Do(BaseEntity source, params object[] args)
	{
		if (args.Length == 0 || args[0] is not Player player) return;

		player.MulliganState = Mulligan.Input;
		player.Choice = new Choice(player, new List<BaseEntity>());

		// 排除硬币（GAME_005）
		var cards = new List<BaseEntity>();
		foreach (var c in player.Hand)
		{
			if (c is BuffableEntity be && be.CardId != "GAME_005")
				cards.Add(c);
		}

		Cards = cards;
		TargetPlayer = player;
		MinCount = 0;
		MaxCount = player.Hand.Count;
	}

	/// <summary>
	/// Choose：替换选中的牌（从手牌换到牌库，从牌库抽新牌）。
	/// </summary>
	public void Choose(params BaseEntity[] cards)
	{
		if (TargetPlayer == null) return;

		foreach (var card in cards)
		{
			if (!Cards.Contains(card))
				throw new InvalidActionException($"Card {card} is not in the mulligan choice set");
		}

		TargetPlayer.Choice = null;

		foreach (var card in cards)
		{
			if (card is BuffableEntity be)
			{
				be.SummonIndex = 0;
				if (TargetPlayer.Deck.Count > 0)
				{
					var newCard = TargetPlayer.Deck[^1];
					if (newCard is BuffableEntity newBe)
						newBe.SummonIndex = be.SummonIndex;
					be.SetZone(Zone.Deck);
					if (newCard is BuffableEntity newBe2)
						newBe2.SetZone(Zone.Hand);
				}
			}
		}

		TargetPlayer.ShuffleDeck();
		TargetPlayer.MulliganState = Mulligan.Done;

		if (TargetPlayer.Opponent?.MulliganState == Mulligan.Done)
		{
			_callback?.Invoke();
		}
	}
}

// ─── Battlecry ──────────

/// <summary>
/// 战吼触发。
/// </summary>
public class BattlecryAction : TargetedAction
{
	private static readonly ILogger _log = LogConfig.ForType<BattlecryAction>();

	public BattlecryAction(object card, object? target = null)
		: base(card, target ?? new object()) { }

	public bool HasExtraBattlecries(Player player, BuffableEntity card)
	{
		// 布莱恩·铜须
		if (player.ExtraBattlecries && card.HasBattlecry)
			return true;

		if (card.Type == CardType.Minion)
		{
			if (player.MinionExtraCombos && card.HasCombo && player.Combo)
				return true;
			if (player.MinionExtraBattlecries && card.HasBattlecry)
				return true;
		}

		return false;
	}

	public override object? Do(BaseEntity source, BaseEntity target, params object[] args)
	{
		if (target is not BuffableEntity card) return null;
		var player = (source as BuffableEntity)?.Controller;
		if (player == null) return null;

		BaseEntity? battlecryTarget = args.Length > 0 ? args[0] as BaseEntity : null;

		List<Action>? actions;
		if (card.HasCombo && player.Combo)
		{
			_log.Information("Activating {Card} combo targeting {Target}", card, battlecryTarget);
			actions = card.GetActions("combo");
		}
		else
		{
			_log.Information("Activating {Card} battlecry targeting {Target}", card, battlecryTarget);
			actions = card.GetActions("battlecry");
		}

		if (card.BattlecryRequiresTarget() && battlecryTarget == null)
		{
			_log.Information("{Card} requires a target for its battlecry. Will not trigger.", card);
			return null;
		}

		if (actions != null && source.Game != null)
		{
			source.Game.TriggerBlock(source, actions);

			if (HasExtraBattlecries(player, card))
			{
				source.Game.TriggerBlock(source, actions);
			}

			if (card.Overload > 0)
			{
				source.Game.QueueActions(card,
					new List<Action> { new OverloadAction(player, card.Overload) });
			}
		}

		return null;
	}
}

// ─── ExtraBattlecry ──────────

/// <summary>
/// 额外战吼（无条件额外触发一次）。
/// </summary>
public class ExtraBattlecryAction : TargetedAction
{
	public ExtraBattlecryAction(object card, object? target = null)
		: base(card, target ?? new object()) { }

	public override object? Do(BaseEntity source, BaseEntity target, params object[] args)
	{
		if (target is not BuffableEntity card) return null;
		var player = (source as BuffableEntity)?.Controller;
		if (player == null) return null;

		List<Action>? actions;
		if (card.HasCombo && player.Combo)
			actions = card.GetActions("combo");
		else
			actions = card.GetActions("play");

		if (actions != null && source.Game != null)
		{
			source.Game.TriggerBlock(source, actions);
		}

		return null;
	}
}

// ─── Deathrattle ──────────

/// <summary>
/// 亡语触发。
/// </summary>
public class DeathrattleAction : TargetedAction
{
	private static readonly ILogger _log = LogConfig.ForType<DeathrattleAction>();

	public DeathrattleAction(object target) : base(target) { }

	public override object? Do(BaseEntity source, BaseEntity target, params object[] args)
	{
		if (target is not BuffableEntity be) return null;
		if (!be.HasDeathrattle) return null;

		foreach (var entity in be.Entities)
		{
			foreach (var deathrattle in entity.Deathrattles)
			{
				List<Action>? actions = null;
				if (deathrattle is List<Action> actionList)
					actions = actionList;
				else if (deathrattle is Func<BuffableEntity, List<Action>> func)
					actions = func(entity);

				if (actions != null && source.Game != null)
				{
					source.Game.QueueActions(entity, actions);

					if (be.Controller?.ExtraDeathrattles == true)
					{
						_log.Information("Triggering deathrattles for {Target} again", target);
						source.Game.QueueActions(entity, actions);
					}
				}
			}
		}

		return null;
	}
}

// ─── Overload ──────────

/// <summary>
/// 过载。
/// </summary>
public class OverloadAction : GameAction
{
	private static readonly ILogger _log = LogConfig.ForType<OverloadAction>();

	public OverloadAction(object player, object amount) : base(player, amount) { }

	public override void Do(BaseEntity source, params object[] args)
	{
		if (args.Length < 2) return;
		if (args[0] is not Player player) return;
		int amount = ActionHelper.ResolveInt(args, 1, source);

		if (player.CantOverload)
		{
			_log.Information("{Source} cannot overload {Player}", source, player);
			return;
		}

		_log.Information("{Source} overloads {Player} for {Amount}", source, player, amount);
		player.Overloaded += amount;
		player.OverloadedThisGame += amount;
	}
}

// ─── UnlockOverload ──────────

/// <summary>
/// 解锁过载。
/// </summary>
public class UnlockOverloadAction : TargetedAction
{
	private static readonly ILogger _log = LogConfig.ForType<UnlockOverloadAction>();

	public UnlockOverloadAction(object target) : base(target) { }

	public override object? Do(BaseEntity source, BaseEntity target, params object[] args)
	{
		if (target is not Player player) return null;

		_log.Information("{Player} overload gets cleared", player);
		player.Overloaded = 0;
		player.OverloadLocked = 0;
		return null;
	}
}

// ─── Fatigue ──────────

/// <summary>
/// 疲劳伤害。
/// </summary>
public class FatigueAction : TargetedAction
{
	private static readonly ILogger _log = LogConfig.ForType<FatigueAction>();

	public FatigueAction(object target) : base(target) { }

	public override object? Do(BaseEntity source, BaseEntity target, params object[] args)
	{
		if (target is not Player player) return null;

		if (player.CantFatigue)
		{
			_log.Information("{Player} can't fatigue and does not take damage", player);
			return null;
		}

		player.FatigueCounter++;
		_log.Information("{Player} takes {Amount} fatigue damage", player, player.FatigueCounter);

		if (player.Hero != null && source.Game != null)
		{
			source.Game.QueueActions(source,
				new List<Action> { new DealDamage(player.Hero, player.FatigueCounter) });
		}

		return null;
	}
}

// ─── Predamage ──────────

/// <summary>
/// 预伤害（伤害修正前）。
/// </summary>
public class PredamageAction : TargetedAction
{
	public PredamageAction(object target, object amount) : base(target, amount) { }

	public override object? Do(BaseEntity source, BaseEntity target, params object[] args)
	{
		int amount = ActionHelper.ResolveInt(args, 0, source);

		if (target is Character ch)
		{
			ch.SetIntProperty("predamage", amount);
		}

		if (amount > 0 && source.Game != null)
		{
			return source.Game.TriggerActions(source,
				new List<Action> { new RawDamage(target) });
		}

		return 0;
	}
}

// ─── ForceDraw ──────────

/// <summary>
/// 强制抽牌（把指定实体抽到手牌）。
/// </summary>
public class ForceDrawAction : TargetedAction
{
	public ForceDrawAction(object target) : base(target) { }

	public override object? Do(BaseEntity source, BaseEntity target, params object[] args)
	{
		if (target is BuffableEntity be)
		{
			be.SetZone(Zone.Hand);
		}
		return target;
	}
}

// ─── DrawUntil ──────────

/// <summary>
/// 抽牌至指定数量。
/// </summary>
public class DrawUntilAction : TargetedAction
{
	public DrawUntilAction(object target, object amount) : base(target, amount) { }

	public override object? Do(BaseEntity source, BaseEntity target, params object[] args)
	{
		if (target is not Player player) return null;
		int amount = ActionHelper.ResolveInt(args, 0, source);

		int diff = Math.Max(0, amount - player.Hand.Count);
		if (diff > 0 && source.Game != null)
		{
			var drawActions = new List<Action>();
			for (int i = 0; i < diff; i++)
				drawActions.Add(new DrawCard(target));
			source.Game.QueueActions(source, drawActions);
		}
		return null;
	}
}

// ─── Mill ──────────

/// <summary>
/// 磨牌（从牌库顶弃牌）。
/// </summary>
public class MillAction : TargetedAction
{
	public MillAction(object target) : base(target) { }

	public override object? Do(BaseEntity source, BaseEntity target, params object[] args)
	{
		if (target is not Player player) return null;

		if (player.Deck.Count == 0)
			return null;

		var card = player.Deck[^1];
		if (card is BuffableEntity be)
		{
			be.SetZone(Zone.Graveyard);
		}

		return card;
	}
}

// ─── Activate ──────────

/// <summary>
/// 英雄技能激活。
/// </summary>
public class ActivateAction : GameAction
{
	private static readonly ILogger _log = LogConfig.ForType<ActivateAction>();

	public ActivateAction(params object[] args) : base(args) { }

	public override void Do(BaseEntity source, params object[] args)
	{
		if (args.Length == 0 || args[0] is not Player player) return;

		var heropower = player.Hero?.Power;
		if (heropower == null) return;

		// 支付费用
		player.PayCost(heropower.Cost);

		// 执行英雄技能
		if (source.Game != null)
		{
			source.Game.QueueActions(source,
				new List<Action> { new PlayHeroPowerAction(heropower) });
		}

		heropower.ActivationsThisTurn++;
		player.TimesHeroPowerUsedThisGame++;
	}
}

// ─── PlayHeroPower ──────────

/// <summary>
/// 使用英雄技能（执行 activate 动作）。
/// </summary>
public class PlayHeroPowerAction : TargetedAction
{
	public PlayHeroPowerAction(object heropower, object? target = null)
		: base(heropower, target ?? new object()) { }

	public override object? Do(BaseEntity source, BaseEntity target, params object[] args)
	{
		if (target is not BuffableEntity heropower) return null;
		var actions = heropower.GetActions("activate");
		if (actions != null && source.Game != null)
		{
			source.Game.TriggerBlock(heropower, actions);
		}
		return null;
	}
}

// ─── RefreshHeroPower ──────────

/// <summary>
/// 刷新英雄技能。
/// </summary>
public class RefreshHeroPowerAction : TargetedAction
{
	private static readonly ILogger _log = LogConfig.ForType<RefreshHeroPowerAction>();

	public RefreshHeroPowerAction(object heropower) : base(heropower) { }

	public override object? Do(BaseEntity source, BaseEntity target, params object[] args)
	{
		if (target is not HeroPower hp) return null;

		_log.Information("Refresh Hero Power {HeroPower}", hp);
		if (hp.Exhausted)
		{
			hp.AdditionalActivationsThisTurn++;
		}
		return null;
	}
}

// ─── SpendMana ──────────

/// <summary>
/// 花费法力值。
/// </summary>
public class SpendManaAction : TargetedAction
{
	private static readonly ILogger _log = LogConfig.ForType<SpendManaAction>();

	public SpendManaAction(object target, object amount) : base(target, amount) { }

	public override object? Do(BaseEntity source, BaseEntity target, params object[] args)
	{
		if (target is not Player player) return null;
		int amount = ActionHelper.ResolveInt(args, 0, source);

		_log.Information("{Player} pays {Amount} mana", player, amount);
		player.PayCost(amount);
		return null;
	}
}

// ─── FillMana (backward compat alias) ──────────

/// <summary>
/// Backward-compatible alias for FillMana in Actions.cs.
/// </summary>
public class FillManaAction : TargetedAction
{
	public FillManaAction(object target, object amount) : base(target, amount) { }

	public override object? Do(BaseEntity source, BaseEntity target, params object[] args)
	{
		if (target is not Player player) return null;
		int amount = ActionHelper.ResolveInt(args, 0, source);
		player.UsedMana = Math.Max(0, player.UsedMana - amount);
		return null;
	}
}

// ─── Concede ──────────

/// <summary>
/// 投降。
/// Sets the player's PlayState to Conceded and triggers end-game check.
/// </summary>
public class ConcedeAction : GameAction
{
	private static readonly ILogger _log = LogConfig.ForType<ConcedeAction>();

	public ConcedeAction(object player) : base(player) { }

	public override void Do(BaseEntity source, params object[] args)
	{
		if (args.Length > 0 && args[0] is Player player)
		{
			player.PlayState = PlayState.Conceded;
			_log.Information("{Player} concedes", player.Name);
			source.Game?.CheckForEndGame();
		}
	}
}

// ─── ForcePlay ──────────
// Python: card.play(force=True) — play a card bypassing mana cost and board checks.
// Used by effects like Varian Wrynn, Y'Shaarj.

/// <summary>
/// Force-play a card from anywhere, bypassing mana cost.
/// The card is played as if it cost 0 mana, and board-full checks
/// are handled by creating space if possible.
/// </summary>
public class ForcePlayAction : TargetedAction
{
	private static readonly ILogger _log = LogConfig.ForType<ForcePlayAction>();

	public ForcePlayAction(object target) : base(target) { }

	public override object? Do(BaseEntity source, BaseEntity target, params object[] args)
	{
		if (target is not BuffableEntity card) return null;

		var player = card.Controller ?? (source as BuffableEntity)?.Controller;
		if (player?.Game == null) return null;

		_log.Information("{Source} force-plays {Card}", source, card.CardId);

		// Save and zero the cost so PlayCard doesn't charge mana
		int originalCost = card.Cost;
		card.Cost = 0;

		try
		{
			player.Game.PlayCard(card);
		}
		catch (Exception ex)
		{
			_log.Warning("ForcePlay of {Card} failed: {Error}", card.CardId, ex.Message);
		}
		finally
		{
			// Restore cost (the entity may have moved zones, but this is safe)
			card.Cost = originalCost;
		}

		return null;
	}
}

// ─── SetTag ──────────

/// <summary>
/// 直接设置目标实体的 GameTag。
/// 用法：new SetTag(S.TARGET, GameTag.Charge, 1)
/// </summary>
public class SetTag : TargetedAction
{
	public SetTag(object target, GameTag tag, int value) : base(target, tag, value) { }

	public override object? Do(BaseEntity source, BaseEntity target, params object[] args)
	{
		if (target is not BuffableEntity be) return null;
		if (args.Length < 2) return null;

		var tag = (GameTag)args[0];
		var value = (int)args[1];

		be.Tags[tag] = value;
		Log.Information("{Target} tag {Tag} set to {Value}", target, tag, value);
		return null;
	}
}

// ─── ConditionalAction ──────────

/// <summary>
/// 根据 Evaluator 条件选择不同的 Action 执行。
/// 用法：new ConditionalAction(new Find(S.DAMAGED), trueActions, falseActions)
/// </summary>
public class ConditionalAction : TargetedAction
{
	private readonly Evaluator _condition;
	private readonly Action[] _trueActions;
	private readonly Action[] _falseActions;

	public ConditionalAction(Evaluator condition, Action[] trueActions,
		Action[]? falseActions = null)
		: base()
	{
		_condition = condition;
		_trueActions = trueActions;
		_falseActions = falseActions ?? Array.Empty<Action>();
	}

	public override object? Do(BaseEntity source, BaseEntity target, params object[] args)
	{
		var actions = _condition.Check(source)
			? _trueActions
			: _falseActions;

		object? result = null;
		foreach (var action in actions)
		{
			if (action is TargetedAction ta)
			{
				var targets = ta.GetTargets(source);
				foreach (var t in targets)
				{
					var tArgs = ta.GetTargetArgs(source, t);
					result = ta.Do(source, t, tArgs);
				}
			}
		}
		return result;
	}
}

// ─── TriggerDeathrattle ──────────

/// <summary>
/// 触发选中实体的亡语效果（不销毁它们）。
/// 用法：new TriggerDeathrattle(S.FRIENDLY_MINIONS)
/// </summary>
public class TriggerDeathrattle : TargetedAction
{
	public TriggerDeathrattle(object target) : base(target) { }

	public override object? Do(BaseEntity source, BaseEntity target, params object[] args)
	{
		if (target is not BuffableEntity be) return null;

		var script = CardScriptRegistry.Instance.GetScript(be.CardId ?? "");
		if (script == null) return null;

		foreach (var action in script.Deathrattle)
		{
			if (action is TargetedAction ta)
			{
				var targets = ta.GetTargets(be);
				foreach (var t in targets)
				{
					var tArgs = ta.GetTargetArgs(be, t);
					ta.Do(be, t, tArgs);
				}
			}
		}

		Log.Information("{Source} triggers deathrattle of {Target}", source, target);
		return null;
	}
}

// ─── ExtraAttack ──────────

/// <summary>
/// 额外攻击（减少攻击计数，使角色可以再次攻击）。
/// </summary>
public class ExtraAttack : TargetedAction
{
	public ExtraAttack(object target) : base(target) { }

	public override object? Do(BaseEntity source, BaseEntity target, params object[] args)
	{
		if (target is not Character ch) return null;
		if (ch.NumAttacks > 0)
			ch.NumAttacks--;
		return null;
	}
}

// ─── LosesDivineShield ──────────

/// <summary>
/// 移除圣盾。
/// </summary>
public class LosesDivineShield : TargetedAction
{
	public LosesDivineShield(object target) : base(target) { }

	public override object? Do(BaseEntity source, BaseEntity target, params object[] args)
	{
		if (target is Character ch)
			ch.DivineShield = false;
		return null;
	}
}

// ─── DormantAction ──────────

/// <summary>
/// 使随从进入休眠状态，持续指定回合数。
/// </summary>
public class DormantAction : TargetedAction
{
	public DormantAction(object target, object turns) : base(target, turns) { }

	public override object? Do(BaseEntity source, BaseEntity target, params object[] args)
	{
		if (target is not Minion minion) return null;
		int turns = ActionHelper.ResolveInt(args, 0, source);

		minion.Dormant = true;
		minion.DormantTurns += turns;
		return null;
	}
}

// ─── EquipWeapon ──────────

/// <summary>
/// 装备武器（从 cardId 创建武器并装备）。
/// 用法：new EquipWeapon(S.CONTROLLER, "EX1_323w")
/// </summary>
public class EquipWeapon : TargetedAction
{
	private static readonly ILogger _log = LogConfig.ForType<EquipWeapon>();

	public EquipWeapon(object target, object cardId) : base(target, cardId) { }

	public override object? Do(BaseEntity source, BaseEntity target, params object[] args)
	{
		if (target is not Player player) return null;
		if (args.Length == 0) return null;

		string? cardId = args[0] as string;
		if (string.IsNullOrEmpty(cardId)) return null;

		var entity = player.CreateCard(cardId, Zone.Invalid);
		if (entity is not Weapon weapon) return null;

		_log.Information("{Player} equips weapon {Weapon}", player.Name, cardId);
		weapon.Equip();

		// Register OnActivate for weapon script
		var script = CardScriptRegistry.Instance.GetScript(cardId);
		script?.OnActivate?.Invoke(weapon, source.Game!);

		return weapon;
	}
}


