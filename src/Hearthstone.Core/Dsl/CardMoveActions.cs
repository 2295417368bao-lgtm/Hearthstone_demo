using Serilog;
using HearthstoneServer.Entities;
using HearthstoneServer.Events;

namespace HearthstoneServer.Dsl;

// ─── P1.4 新增 Action: 法力 / 牌库操作 ──────────

/// <summary>
/// Mill cards from the top of a player's deck (discard to graveyard).
/// 磨牌 = 从牌库销毁，不算弃牌（不触发 DiscardEvent，不标记 Discarded）。
/// </summary>
public class MillCards : TargetedAction
{
	public MillCards(object target) : base(target) { }
	public MillCards(object target, object count) : base(target, count) { }

	public override object? Do(BaseEntity source, BaseEntity target, params object[] args)
	{
		if (target is not Player player) return null;
		if (player.Deck.Count == 0) return null;

		var card = player.Deck[^1]; // top of deck is last element
		if (card is BuffableEntity be)
		{
			Log.Information("Mill: {Player} loses {Card} from top of deck", player.Name, be.CardId);
			be.SetZone(Zone.Graveyard);
		}
		return card;
	}
}

/// <summary>
/// Refill mana crystals for player targets.
/// </summary>
public class RefillMana : TargetedAction
{
	public RefillMana(object target, object amount) : base(target, amount) { }

	public override object? Do(BaseEntity source, BaseEntity target, params object[] args)
	{
		if (target is not Player player) return null;
		int amount = ActionHelper.ResolveInt(args, 0, source);
		player.UsedMana = Math.Max(0, player.UsedMana - amount);
		Log.Information("FillMana: {Player} refills {Amount} mana", player.Name, amount);
		return null;
	}
}

/// <summary>
/// Set a player's max mana crystals.
/// </summary>
public class SetMaxMana : TargetedAction
{
	public SetMaxMana(object target, object amount) : base(target, amount) { }

	public override object? Do(BaseEntity source, BaseEntity target, params object[] args)
	{
		if (target is not Player player) return null;
		int amount = ActionHelper.ResolveInt(args, 0, source);
		int oldMana = player.Mana;
		player.MaxMana = amount;
		player.UsedMana = Math.Max(0,
			player.MaxMana - player.OverloadLocked - oldMana + player.TempMana);
		Log.Information("SetMana: {Player} max mana set to {Amount}", player.Name, amount);
		return null;
	}
}

/// <summary>
/// Draw cards until the player has a certain number of cards in hand.
/// </summary>
public class DrawUntilCount : TargetedAction
{
	public DrawUntilCount(object target, object amount) : base(target, amount) { }

	public override object? Do(BaseEntity source, BaseEntity target, params object[] args)
	{
		if (target is not Player player) return null;
		int amount = ActionHelper.ResolveInt(args, 0, source);
		int difference = Math.Max(0, amount - player.Hand.Count);
		if (difference > 0 && source.Game != null)
		{
			for (int i = 0; i < difference; i++)
				player.DrawCard();
		}
		return null;
	}
}

/// <summary>
/// Unlock overloaded mana crystals for player targets.
/// </summary>
public class UnlockOverload : TargetedAction
{
	public UnlockOverload(object target) : base(target) { }

	public override object? Do(BaseEntity source, BaseEntity target, params object[] args)
	{
		if (target is not Player player) return null;
		player.Overloaded = 0;
		player.OverloadLocked = 0;
		Log.Information("UnlockOverload: {Player} overload cleared", player.Name);
		return null;
	}
}

// ─── P8: New composite actions ──────────

/// <summary>
/// Iterate over selector targets and run a per-entity action factory.
/// Used for Lightbomb (damage=ATK), Polarity Shift (swap stats), Poison Seeds, etc.
/// </summary>
public class ForEach : TargetedAction
{
	private readonly Func<BuffableEntity, Action?>? _factory;
	private readonly TargetedAction? _action;

	public ForEach(object selector, Func<BuffableEntity, Action?> factory) : base(selector)
	{
		_factory = factory;
	}

	/// <summary>声明式重载：直接传 TargetedAction，通过 CurrentTarget 上下文传递当前实体。</summary>
	public ForEach(object selector, TargetedAction action) : base(selector)
	{
		_action = action;
	}

	public override object? Do(BaseEntity source, BaseEntity target, params object[] args)
	{
		if (target is not BuffableEntity be) return null;

		if (_action != null)
		{
			var prev = source.CurrentTarget;
			source.CurrentTarget = be;
			try { _action.Trigger(source); }
			finally { source.CurrentTarget = prev; }
		}
		else if (_factory != null)
		{
			var action = _factory(be);
			if (action is TargetedAction ta)
				ta.Trigger(source);
		}
		return null;
	}
}

/// <summary>
/// Copy entities from a selector into a player's hand (clone as new cards).
/// <list type="bullet">
///   <item>keepBuffs=false（默认）：创建原始版本新卡</item>
///   <item>keepBuffs=true：复制当前属性（ATK/Health/Cost）到新卡</item>
/// </list>
/// </summary>
public class CopyToHand : TargetedAction
{
	private readonly bool _keepBuffs;
	private readonly int _handIndex;

	/// <param name="target">Player to receive the copy</param>
	/// <param name="sourceSelector">Selector for entities to copy</param>
	/// <param name="keepBuffs">true = copy current ATK/Health/Cost</param>
	/// <param name="handIndex">-1 = append (rightmost), 0 = insert at leftmost, N = insert at index N</param>
	public CopyToHand(object target, object sourceSelector, bool keepBuffs = false, int handIndex = -1)
		: base(target, sourceSelector)
	{
		_keepBuffs = keepBuffs;
		_handIndex = handIndex;
	}

	public override object? Do(BaseEntity source, BaseEntity target, params object[] args)
	{
		if (target is not Player player) return null;
		var toCopy = args.Length > 0 ? args[0] : null;

		var entities = toCopy switch
		{
			List<BaseEntity> list => list,
			BaseEntity e => new List<BaseEntity> { e },
			_ => new List<BaseEntity>()
		};

		var created = new List<BaseEntity>();
		foreach (var entity in entities)
		{
			if (entity is BuffableEntity be && be.CardId != null)
			{
				BuffableEntity? copy;
				if (_handIndex >= 0)
				{
					// 创建卡牌但不自动加入手牌，手动控制插入位置
					copy = player.CreateCard(be.CardId, Zone.SetAside);
					if (copy != null)
					{
						player.Hand.Insert(Math.Min(_handIndex, player.Hand.Count), copy);
						copy.Zone = Zone.Hand;
					}
				}
				else
				{
					// 默认行为：追加到最右
					copy = player.CreateCard(be.CardId, Zone.Hand);
				}

				if (_keepBuffs && copy is BuffableEntity copyBe)
				{
					// 保留当前属性
					copyBe.SetIntProperty("atk", be.GetIntProperty("atk"));
					copyBe.SetIntProperty("health", be.GetIntProperty("health"));
					copyBe.SetIntProperty("cost", be.GetIntProperty("cost"));
				}
				if (copy != null)
					created.Add(copy);
				Log.Information("{Player} copies {Card} to hand (keepBuffs={Keep}, index={Index})",
					player.Name, copy, _keepBuffs, _handIndex);
			}
		}
		return created.Count switch
		{
			0 => null,
			1 => created[0],
			_ => created,
		};
	}
}

/// <summary>
/// 从墓地或场上召唤实体的副本。
/// 墓地来源的实体使用原始卡牌数据（不带 buff），场上来源保留当前状态。
/// 用于复活术、克尔苏加德、转生、回响软泥怪等。
/// </summary>
public class SummonCopy : TargetedAction
{
	public SummonCopy(object target, object sourceSelector) : base(target, sourceSelector) { }

	public override object? Do(BaseEntity source, BaseEntity target, params object[] args)
	{
		if (target is not Player player) return null;
		var toSummon = args.Length > 0 ? args[0] : null;

		var entities = toSummon switch
		{
			List<BaseEntity> list => list,
			BaseEntity e => new List<BaseEntity> { e },
			_ => new List<BaseEntity>()
		};

		foreach (var entity in entities)
		{
			if (entity is BuffableEntity be && be.CardId != null)
			{
				// 从墓地复活的随从使用原始卡牌数据（无 buff），场上复制保留状态
				bool fromGraveyard = be.Zone == Zone.Graveyard;
				var copy = player.CreateCard(be.CardId, Zone.Play) as Minion;
				if (copy != null)
				{
					// 场上复制：把来源的 buff 复制过去
					if (!fromGraveyard)
					{
						foreach (var (tag, val) in be.Tags.Items())
						{
							if (tag != GameTag.EntityId && tag != GameTag.Zone)
								copy.Tags[tag] = val;
						}
					}
					// 墓地复活：CreateCard 已用原始数据，无需额外操作

					player.Game?.RegisterEntity(copy);
					Log.Information("{Player} summons copy of {Card} (from {Zone})",
						player.Name, copy, fromGraveyard ? "graveyard" : "board");
					player.Game?.EventBus.Publish(
						new MinionSummonedEvent(source, copy), MinionPhase.AfterSummon);
				}
			}
		}
		return null;
	}
}
