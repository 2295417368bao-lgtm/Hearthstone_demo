using Serilog;
using HearthstoneServer.Entities;
using HearthstoneServer.Events;

namespace HearthstoneServer.Dsl;

// ─── P1: 战斗 / 变身 / Buff 扩展 ──────────

/// <summary>
/// 额外攻击机会。
/// </summary>
public class GrantWindfury : TargetedAction
{
	private static readonly ILogger _log = LogConfig.ForType<GrantWindfury>();

	public GrantWindfury(object target) : base(target) { }

	public override object? Do(BaseEntity source, BaseEntity target, params object[] args)
	{
		if (target is Character ch)
		{
			_log.Information("{Target} gets an extra attack chance", target);
			ch.NumAttacks = Math.Max(0, ch.NumAttacks - 1);
		}
		return null;
	}
}

/// <summary>
/// 溢出伤害。
/// </summary>
public class DealOverflowDamage : TargetedAction
{
	public DealOverflowDamage(object target, object amount) : base(target, amount) { }

	public override object? Do(BaseEntity source, BaseEntity target, params object[] args)
	{
		int amount = ActionHelper.ResolveInt(args, 0, source);
		if (amount <= 0) return 0;

		if (target is Character ch && source.Game != null)
		{
			if (ch.Health >= amount)
			{
				source.Game.QueueActions(source,
					new List<Action> { new PredamageAction(target, amount) });
				return 0;
			}
			else
			{
				int excess = amount - ch.Health;
				source.Game.QueueActions(source,
					new List<Action> { new PredamageAction(target, amount) });
				return excess;
			}
		}
		return 0;
	}
}

/// <summary>
/// 多重 Buff。
/// </summary>
public class MultiEnchant : TargetedAction
{
	public MultiEnchant(object target, object buffs) : base(target, buffs) { }

	public override object? Do(BaseEntity source, BaseEntity target, params object[] args)
	{
		if (args.Length == 0 || target is not BuffableEntity be) return target;

		// args[0] is a list of buff IDs or Enchantment objects
		var buffs = args[0] as IEnumerable<object>;
		if (buffs == null && args[0] is string singleBuff)
			buffs = new[] { (object)singleBuff };
		if (buffs == null) return target;

		var controller = source is BuffableEntity sb ? sb.Controller : null;

		foreach (var buff in buffs)
		{
			if (buff is string buffId && controller != null)
			{
				var enchantment = controller.CreateCard(buffId) as Enchantment;
				if (enchantment != null)
				{
					source.Game?.RegisterEntity(enchantment);
					enchantment.Apply(be);
				}
			}
			else if (buff is Enchantment enc)
			{
				enc.Apply(be);
			}
		}
		Log.Debug("MultiBuff applied to {Target}", target);
		return target;
	}
}

/// <summary>
/// 存储 Buff。
/// </summary>
public class StoreEnchant : TargetedAction
{
	public StoreEnchant(object target, object buff, object card) : base(target, buff, card) { }

	public override object? Do(BaseEntity source, BaseEntity target, params object[] args)
	{
		if (target is not BuffableEntity be) return null;
		if (args.Length < 2) return null;

		var controller = source is BuffableEntity sb ? sb.Controller : null;
		if (controller == null) return null;

		// args[0] = buff ID, args[1] = card to store
		var buffId = args[0] as string;
		var storeCard = args[1] as BaseEntity;

		if (buffId != null)
		{
			var enchantment = controller.CreateCard(buffId) as Enchantment;
			if (enchantment != null)
			{
				source.Game?.RegisterEntity(enchantment);
				if (storeCard != null)
					enchantment.StoreCard = storeCard;
				Log.Debug("StoringBuff {Buff} stores {Card}, applied to {Target}", buffId, storeCard, target);
				enchantment.Apply(be);
				return enchantment;
			}
		}
		return null;
	}
}

/// <summary>
/// 复制亡语 Buff。
/// </summary>
public class CopyDeathrattle : TargetedAction
{
	public CopyDeathrattle(object target, object buff) : base(target, buff) { }

	public override object? Do(BaseEntity source, BaseEntity target, params object[] args)
	{
		if (target is not BuffableEntity be) return null;
		if (!be.HasDeathrattle) return null;

		// 复制亡语到 source
		if (source is BuffableEntity sourceBe)
		{
			foreach (var deathrattle in be.Deathrattles)
			{
				sourceBe.Deathrattles.Add(deathrattle);
			}
		}
		return null;
	}
}

/// <summary>
/// 放到牌库顶。
/// </summary>
public class PlaceOnDeckTop : TargetedAction
{
	private static readonly ILogger _log = LogConfig.ForType<PlaceOnDeckTop>();

	public PlaceOnDeckTop(object target, object card) : base(target, card) { }

	public override object? Do(BaseEntity source, BaseEntity target, params object[] args)
	{
		if (target is not Player player) return null;
		if (args.Length == 0) return null;

		var cardArg = args[0];
		var cards = cardArg is List<BaseEntity> list ? list : new List<BaseEntity> { cardArg as BaseEntity ?? target };

		if (cards.Count > 0)
			player.ShuffleDeck();

		foreach (var card in cards)
		{
			if (card is not BuffableEntity be) continue;

			if (be.Controller != player)
			{
				be.SetZone(Zone.SetAside);
				be.Controller = player;
			}

			if (be.Zone != Zone.Deck && player.Deck.Count >= player.MaxDeckSize)
			{
				_log.Information("PutOnTop({Card}) fails because {Player}'s deck is full", card, player);
				continue;
			}

			be.SetZone(Zone.Deck);
			// 交换到顶部
			if (player.Deck.Count > 1)
			{
				int lastIdx = player.Deck.Count - 1;
				(player.Deck[0], player.Deck[lastIdx]) = (player.Deck[lastIdx], player.Deck[0]);
			}
		}
		return null;
	}
}

/// <summary>
/// 改变目标。
/// </summary>
public class RedirectTarget : TargetedAction
{
	private static readonly ILogger _log = LogConfig.ForType<RedirectTarget>();

	public RedirectTarget(object target, object newTarget) : base(target, newTarget) { }

	public override object? Do(BaseEntity source, BaseEntity target, params object[] args)
	{
		if (args.Length == 0) return null;
		var newTarget = args[0] as BaseEntity;
		if (newTarget == null) return null;

		_log.Information("Retargeting {Target} to {NewTarget}", target, newTarget);
		// Target 属性存储在实体的瞬态字段中
		if (target is BuffableEntity be)
		{
			be.SetIntProperty("_retarget", newTarget.EntityId);
		}
		return newTarget;
	}
}

/// <summary>
/// 设置标签。
/// </summary>
public class SetTags : TargetedAction
{
	public SetTags(object target, object tags) : base(target, tags) { }

	public override object? Do(BaseEntity source, BaseEntity target, params object[] args)
	{
		if (args.Length == 0) return null;

		if (args[0] is Dictionary<GameTag, int> tagDict)
		{
			target.Tags.Update(tagDict);
		}
		else if (args[0] is IEnumerable<GameTag> tagList)
		{
			foreach (var tag in tagList)
			{
				target.Tags[tag] = 1;
			}
		}
		return null;
	}
}

/// <summary>
/// 取消标签。
/// </summary>
public class UnsetTags : TargetedAction
{
	public UnsetTags(object target, object tags) : base(target, tags) { }

	public override object? Do(BaseEntity source, BaseEntity target, params object[] args)
	{
		if (args.Length == 0) return null;

		if (args[0] is IEnumerable<GameTag> tagList)
		{
			foreach (var tag in tagList)
			{
				if (target.Tags.Contains(tag))
					target.Tags[tag] = 0;
			}
		}
		return null;
	}
}

/// <summary>
/// 获取标签值。
/// </summary>
public class GetTag : TargetedAction
{
	public GetTag(object target, object tag) : base(target, tag) { }

	public override object? Do(BaseEntity source, BaseEntity target, params object[] args)
	{
		if (args.Length == 0) return null;
		if (args[0] is GameTag tag)
		{
			return target.Tags.Get(tag, 0);
		}
		return null;
	}
}

/// <summary>
/// 进化。
/// </summary>
public class Evolve : TargetedAction
{
	public Evolve(object target, object amount) : base(target, amount) { }

	public override object? Do(BaseEntity source, BaseEntity target, params object[] args)
	{
		if (target is not BuffableEntity be) return null;
		if (source.Game == null) return null;
		int amount = ActionHelper.ResolveInt(args, 0, source);

		int cost = be.Cost + amount;
		var picker = RandomCards.RandomMinion().Cost(cost);
		var cardSet = picker.FindCards(source);
		if (cardSet.Count > 0)
		{
			var cardId = cardSet[source.Game.Random.Next(cardSet.Count)];
			Log.Information("Evolve {Target} by {Amount} → {Card}", target, amount, cardId);
			source.Game.QueueActions(source,
				new List<Action> { new Transform(target, cardId) });
		}
		else
		{
			Log.Debug("Evolve {Target} by {Amount}: no minions at cost {Cost}", target, amount, cost);
		}
		return null;
	}
}


/// <summary>
/// 交换属性 Buff。
/// </summary>
public class SwapStats : TargetedAction
{
	public SwapStats(object target, object other, object buff) : base(target, other, buff) { }

	public override object? Do(BaseEntity source, BaseEntity target, params object[] args)
	{
		if (args.Length < 2) return null;

		// args[0] = other entity (or list), args[1] = buff ID
		BaseEntity? other = args[0] is IList<BaseEntity> list && list.Count > 0 ? list[0] : args[0] as BaseEntity;
		var buffId = args[1] as string;
		if (other == null || buffId == null) return null;
		if (target is not BuffableEntity beTarget) return null;
		if (other is not BuffableEntity beOther) return null;

		var controller = source is BuffableEntity sb ? sb.Controller : null;
		if (controller == null) return null;

		Log.Information("SwapStateBuff: swapping stats between {Target} and {Other}", target, other);

		// buff1: apply other's stats to target
		var buff1 = controller.CreateCard(buffId) as Enchantment;
		if (buff1 != null)
		{
			source.Game?.RegisterEntity(buff1);
			if (beOther is Minion mo) { buff1.SetIntProperty("_xatk", mo.Atk); buff1.SetIntProperty("_xhealth", mo.Health); }
			buff1.SetIntProperty("_xcost", beOther.Cost);
			buff1.Apply(beTarget);
		}

		// buff2: apply target's stats to other
		var buff2 = controller.CreateCard(buffId) as Enchantment;
		if (buff2 != null)
		{
			source.Game?.RegisterEntity(buff2);
			if (beTarget is Minion mt) { buff2.SetIntProperty("_xatk", mt.Atk); buff2.SetIntProperty("_xhealth", mt.Health); }
			buff2.SetIntProperty("_xcost", beTarget.Cost);
			buff2.Apply(beOther);
		}
		return null;
	}
}

/// <summary>
/// 复制属性 Buff。
/// </summary>
public class CopyStats : TargetedAction
{
	public CopyStats(object target, object other, object buff) : base(target, other, buff) { }

	public override object? Do(BaseEntity source, BaseEntity target, params object[] args)
	{
		// Copy target's stats onto source via buff
		if (args.Length < 1) return null;
		var buffId = args[0] as string;
		if (buffId == null) return null;
		if (source is not BuffableEntity beSource) return null;
		if (target is not BuffableEntity beTarget) return null;

		var controller = beSource.Controller;
		if (controller == null) return null;

		var buff = controller.CreateCard(buffId) as Enchantment;
		if (buff != null)
		{
			source.Game?.RegisterEntity(buff);
			if (beTarget is Minion m) { buff.SetIntProperty("_xatk", m.Atk); buff.SetIntProperty("_xhealth", m.Health); }
			Log.Information("CopyStateBuff: copying {Target} stats to {Source}", target, source);
			buff.Apply(beSource);
		}
		return null;
	}
}

/// <summary>
/// 设置属性 Buff。
/// </summary>
public class SetStats : TargetedAction
{
	public SetStats(object target, object other, object buff) : base(target, other, buff) { }

	public override object? Do(BaseEntity source, BaseEntity target, params object[] args)
	{
		// Set source's stats onto target via buff
		if (args.Length < 1) return null;
		var buffId = args[0] as string;
		if (buffId == null) return null;
		if (source is not BuffableEntity beSource) return null;
		if (target is not BuffableEntity beTarget) return null;

		var controller = beSource.Controller;
		if (controller == null) return null;

		var buff = controller.CreateCard(buffId) as Enchantment;
		if (buff != null)
		{
			source.Game?.RegisterEntity(buff);
			if (beSource is Minion m) { buff.SetIntProperty("_xatk", m.Atk); buff.SetIntProperty("_xhealth", m.Health); }
			Log.Information("SetStateBuff: setting {Source} stats on {Target}", source, target);
			buff.Apply(beTarget);
		}
		return null;
	}
}

/// <summary>
/// 玩家断线。
/// </summary>
public class Disconnect : GameAction
{
	public Disconnect(object player) : base(player) { }

	public override void Do(BaseEntity source, params object[] args)
	{
		if (args.Length > 0 && args[0] is Player player)
		{
			player.PlayState = PlayState.Disconnected;
		}
	}
}

/// <summary>
/// 失去圣盾。
/// </summary>
public class RemoveDivineShield : TargetedAction
{
	public RemoveDivineShield(object target) : base(target) { }

	public override object? Do(BaseEntity source, BaseEntity target, params object[] args)
	{
		if (target is Character ch)
		{
			ch.DivineShield = false;
		}
		return null;
	}
}
