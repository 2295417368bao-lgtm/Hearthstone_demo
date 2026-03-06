using Serilog;
using HearthstoneServer.Entities;
using HearthstoneServer.Events;

namespace HearthstoneServer.Dsl;


/// <summary>
/// 给目标随机授予风怒/嘲讽/圣盾之一。
/// 用于增强-o-机器人。
/// </summary>
public class GrantRandomAbility : TargetedAction
{
	private static readonly GameTag[] _abilities = { GameTag.Windfury, GameTag.Taunt, GameTag.DivineShield };

	public GrantRandomAbility(object target) : base(target) { }

	public override object? Do(BaseEntity source, BaseEntity target, params object[] args)
	{
		if (target is not BuffableEntity be) return null;
		var rng = source.Game?.Random ?? new Random();
		var tag = _abilities[rng.Next(_abilities.Length)];
		be.Tags[tag] = 1;
		Log.Information("{Target} gains {Tag}", target, tag);
		return tag;
	}
}

/// <summary>翻倍的属性标志。</summary>
[Flags]
public enum DoubleStat
{
	Atk = 1,
	Health = 2,
	Cost = 4,
	AtkAndHealth = Atk | Health,
	All = Atk | Health | Cost,
}

/// <summary>
/// 将目标的指定属性翻倍（攻击力、血量、费用，或任意组合）。
/// 用于戈拉蟾蜍（翻倍 ATK）、内战等。
/// <code>
/// new DoubleStats(S.SELF)                          // 翻倍 ATK（默认）
/// new DoubleStats(S.SELF, DoubleStat.Health)        // 翻倍血量
/// new DoubleStats(S.SELF, DoubleStat.AtkAndHealth)  // 翻倍攻防
/// new DoubleStats(S.SELF, DoubleStat.All)           // 翻倍全部
/// </code>
/// </summary>
public class DoubleStats : TargetedAction
{
	private readonly DoubleStat _stats;

	public DoubleStats(object target, DoubleStat stats = DoubleStat.Atk) : base(target)
	{
		_stats = stats;
	}

	public override object? Do(BaseEntity source, BaseEntity target, params object[] args)
	{
		if (target is not BuffableEntity be) return null;

		if (_stats.HasFlag(DoubleStat.Atk))
		{
			int atk = be.GetIntProperty("atk");
			be.SetIntProperty("atk", atk * 2);
			Log.Information("{Target} ATK doubled: {Old} → {New}", target, atk, atk * 2);
		}
		if (_stats.HasFlag(DoubleStat.Health))
		{
			int hp = be.GetIntProperty("health");
			be.SetIntProperty("health", hp * 2);
			Log.Information("{Target} Health doubled: {Old} → {New}", target, hp, hp * 2);
		}
		if (_stats.HasFlag(DoubleStat.Cost))
		{
			int cost = be.GetIntProperty("cost");
			be.SetIntProperty("cost", cost * 2);
			Log.Information("{Target} Cost doubled: {Old} → {New}", target, cost, cost * 2);
		}
		return null;
	}
}

/// <summary>BouncingBlade 的循环终止条件。</summary>
public enum BounceStop
{
	/// <summary>有随从死亡时停止（经典弹射之刃）。</summary>
	UntilDeath,
	/// <summary>一轮伤害后没有随从死亡则停止（伤害不致死即结束）。</summary>
	UntilNoDeath,
	/// <summary>仅按最大循环次数结束，不检查死亡。</summary>
	MaxOnly,
}

/// <summary>
/// 循环对目标池造成伤害，按指定条件终止。
/// <list type="bullet">
///   <item>随机模式（默认）：每次随机选一个目标</item>
///   <item>全体模式：每次对全部目标各造成伤害</item>
/// </list>
/// <code>
/// new RepeatingDamage(S.ALL_MINIONS)                                       // 随机×1，死亡停
/// new RepeatingDamage(S.ENEMY_MINIONS, 2)                                  // 随机×2，死亡停
/// new RepeatingDamage(S.ALL_MINIONS, 1, true)                              // 全体×1，死亡停
/// new RepeatingDamage(S.ALL_MINIONS, 1, false, BounceStop.UntilNoDeath)    // 随机×1，无死亡停
/// new RepeatingDamage(S.ALL_MINIONS, 1, true, BounceStop.MaxOnly, 3)       // 全体×1，固定3轮
/// </code>
/// </summary>
public class RepeatingDamage : TargetedAction
{
	private readonly int _damage;
	private readonly bool _hitAll;
	private readonly BounceStop _stop;
	private readonly int _maxRounds;
	private readonly int _corpseCostPerRound;

	/// <param name="poolSelector">目标池选择器</param>
	/// <param name="damage">每次伤害量（默认 1）</param>
	/// <param name="hitAll">true = 全体，false = 随机单体（默认）</param>
	/// <param name="stop">终止条件（默认 UntilDeath）</param>
	/// <param name="maxRounds">最大循环次数（默认 80）</param>
	/// <param name="corpseCostPerRound">每轮消耗的残骸数（默认 0 = 不消耗）</param>
	public RepeatingDamage(object poolSelector, int damage = 1, bool hitAll = false,
		BounceStop stop = BounceStop.UntilDeath, int maxRounds = 80,
		int corpseCostPerRound = 0)
		: base(poolSelector)
	{
		_damage = damage;
		_hitAll = hitAll;
		_stop = stop;
		_maxRounds = maxRounds;
		_corpseCostPerRound = corpseCostPerRound;
	}

	public override object? Do(BaseEntity source, BaseEntity target, params object[] args)
	{
		var game = (source as BuffableEntity)?.Controller?.Game;
		if (game == null) return null;
		var rng = game.Random ?? new Random();

		for (int i = 0; i < _maxRounds; i++)
		{
			// 残骸消耗检查：每轮消耗指定数量残骸，不足则终止
			if (_corpseCostPerRound > 0)
			{
				var ctrl = (source as BuffableEntity)?.Controller;
				if (ctrl == null || ctrl.SpendCorpses(_corpseCostPerRound) <= 0) break;
			}

			var pool = GetTargets(source)
				.OfType<Minion>()
				.Where(m => m.Zone == Zone.Play && m.Health > 0)
				.ToList();

			if (pool.Count == 0) break;

			bool died = false;

			if (_hitAll)
			{
				foreach (var m in pool)
				{
					int dealt = m.Hit(_damage);
					if (dealt > 0)
						game.EventBus.Publish(new DamageEvent(source, m, dealt), MiscPhase.On);
					if (m.Health <= 0) died = true;
				}
			}
			else
			{
				var victim = pool[rng.Next(pool.Count)];
				int dealt = victim.Hit(_damage);
				if (dealt > 0)
					game.EventBus.Publish(new DamageEvent(source, victim, dealt), MiscPhase.On);
				if (victim.Health <= 0) died = true;
			}

			// 根据终止条件判断是否结束
			switch (_stop)
			{
				case BounceStop.UntilDeath:
					if (died) return null;
					break;
				case BounceStop.UntilNoDeath:
					if (!died) return null;
					break;
				case BounceStop.MaxOnly:
					break; // 不检查，仅靠 maxRounds 控制
			}
		}
		return null;
	}
}


// ─── Utility ──────────

/// <summary>
/// Resolve int values from action args (supports int, LazyNum, LazyValue).
/// </summary>
public static class ActionHelper
{
	public static int ResolveInt(object[] args, int index, BaseEntity source)
	{
		if (index >= args.Length) return 0;
		return args[index] switch
		{
			int i => i,
			LazyNum ln => ln.Value(source),
			LazyValue lv => lv.Evaluate(source) is int vi ? vi : 0,
			_ => 0
		};
	}
}

/// <summary>
/// 计算选择器匹配的实体数量，返回 LazyNum。
/// 用例：亡语伤害 = 本回合死亡的同名随从数量。
/// </summary>
public class CountSelector : LazyNum
{
	private readonly Selector _selector;

	public CountSelector(Selector selector) => _selector = selector;

	public override int Value(BaseEntity source)
	{
		var all = source.Game?.Entities ?? new List<BaseEntity>();
		return _selector.Eval(all, source).Count;
	}
}

/// <summary>
/// Mimiron's Head: destroy up to 3 friendly Mechs (including self), summon V-07-TR-0N.
/// </summary>
public class MimironTransform : GameAction
{
	public override void Do(BaseEntity source, params object[] args)
	{
		var game = source.Game;
		if (game == null) return;

		// Find friendly Mechs on field (including self)
		var mechs = S.FRIENDLY_MINIONS.Eval(game.Entities, source)
			.OfType<BuffableEntity>()
			.Where(m => m.Data?.Races?.Contains(Race.Mechanical) == true)
			.Take(3)
			.ToList();

		if (mechs.Count < 3) return;

		foreach (var m in mechs)
		{
			if (m is Character ch)
				ch.ToBeDestroyed = true;
		}

		game.ProcessDeaths();

		// Summon V-07-TR-0N
		new Summon(S.CONTROLLER, "GVG_111t").Trigger(source);
	}
}

/// <summary>
/// Swap a minion's Attack and Health values.
/// Reads the current (aggregated) ATK and MaxHealth, then writes the
/// swapped values into _fields. Existing buff/enchantment contributions
/// to ATK/Health are zeroed out so the result accurately reflects the swap.
/// DamageTaken is reset because the health baseline changed.
/// Used by: Crazed Alchemist (EX1_059), Reversing Switch, etc.
/// </summary>
public class SwapAtkHealth : TargetedAction
{
	public SwapAtkHealth(object target) : base(target) { }

	public override object? Do(BaseEntity source, BaseEntity target, params object[] args)
	{
		if (target is not Character ch) return null;

		int currentAtk = ch.Atk;          // aggregated: _fields + buffs + slots
		int currentMaxHp = ch.MaxHealth;   // aggregated

		// Zero out ATK/Health modifiers on existing enchantments
		// so they don't stack on top of the swapped base values
		foreach (var buff in ch.Buffs)
		{
			buff.SetIntProperty("atk", 0);
			buff.SetIntProperty("health", 0);
		}

		// Write swapped values into base _fields
		ch.SetIntProperty("atk", currentMaxHp);
		ch.SetIntProperty("health", currentAtk);

		// Reset damage since health baseline changed
		ch.DamageTaken = 0;

		Log.Information("{Target} ATK/Health swapped: {OldAtk}/{OldHp} → {NewAtk}/{NewHp}",
			target, currentAtk, currentMaxHp, currentMaxHp, currentAtk);

		return null;
	}

	public override string ToString() => $"SwapAtkHealth({string.Join(", ", Args)})";
}

// ═══════════════════════════════════════════════════
// RedirectDamageToWeapon — 伤害重定向到武器
// 用于 PreDamageEvent 处理器中（如埃辛诺斯壁垒）
// ═══════════════════════════════════════════════════

/// <summary>
/// When triggered inside a <see cref="PreDamageEvent"/> handler,
/// redirects the damage: the weapon loses 1 durability
/// and the original damage is cancelled.
/// </summary>
public class RedirectDamageToWeapon : TargetedAction
{
	private readonly int _durabilityLoss;

	public RedirectDamageToWeapon(int durabilityLoss = 1) : base()
	{
		_durabilityLoss = durabilityLoss;
	}

	public override object? Do(BaseEntity source, BaseEntity target, params object[] args)
	{
		var preEvt = source.ActivePreDamageEvent;
		if (preEvt == null)
		{
			Log.Warning("RedirectDamageToWeapon: no ActivePreDamageEvent on {Source}", source);
			return null;
		}

		// Find the hero's equipped weapon
		var hero = (source as BuffableEntity)?.Controller?.Hero;
		var weapon = hero?.EquippedWeapon;
		if (weapon == null)
		{
			Log.Debug("RedirectDamageToWeapon: no weapon equipped, damage not redirected");
			return null;
		}

		// Weapon absorbs the hit (can go negative, e.g. Bulwark at 1 dur + counter-attack)
		weapon.LoseDurability(_durabilityLoss);
		preEvt.Redirected = true;

		Log.Information("RedirectDamageToWeapon: {Weapon} loses {Loss} durability instead of {Amount} damage to hero",
			weapon.CardId, _durabilityLoss, preEvt.Amount);

		return null;
	}

	public override string ToString() => $"RedirectDamageToWeapon({_durabilityLoss})";
}

// ─────────────────────────────────────────────
// SwapCards — 交换两张牌的控制者和区域
// ─────────────────────────────────────────────

/// <summary>
/// 交换两张牌的所有者和区域位置。
/// 用于多步发现链的最后一步。
/// <para>用法：</para>
/// <code>new SwapCards(S.PICKED_1, S.PICKED_2)</code>
/// </summary>
public class SwapCards : TargetedAction
{
	private static readonly ILogger Log = LogConfig.ForType<SwapCards>();

	private readonly Selector _sel2;

	public SwapCards(object selector1, Selector selector2) : base(selector1)
	{
		_sel2 = selector2;
	}

	public override object? Do(BaseEntity source, BaseEntity target, params object[] args)
	{
		var entities = source.Game?.Entities ?? new List<BaseEntity>();
		var list2 = _sel2.Eval(entities, source);
		if (list2.Count == 0) return null;

		var card1 = target as BuffableEntity;
		var card2 = list2[0] as BuffableEntity;
		if (card1 == null || card2 == null) return null;

		// 保存原始状态
		var ctrl1 = card1.Controller;
		var zone1 = card1.Zone;
		var ctrl2 = card2.Controller;
		var zone2 = card2.Zone;

		// 交换
		card1.Controller = ctrl2;
		card1.SetZone(zone2);
		card2.Controller = ctrl1;
		card2.SetZone(zone1);

		Log.Information("SwapCards: {Card1} ↔ {Card2}", card1.CardId, card2.CardId);

		// 清理 PickedCards
		source.PickedCards.Clear();

		return null;
	}
}

// ─────────────────────────────────────────────
// MergeCards — 合并两张牌创建融合实体
// ─────────────────────────────────────────────

/// <summary>
/// 将两张 Pick 的卡牌（如两只野兽）合并为一张新卡。
/// 攻击力、生命值、法力值消耗相加；种族和关键词取并集。
/// 新卡加入手牌，PickedCards 清空。
/// <para>用法：</para>
/// <code>new MergeCards(S.PICKED_1, S.PICKED_2)</code>
/// </summary>
public class MergeCards : TargetedAction
{
	private static readonly ILogger Log = LogConfig.ForType<MergeCards>();

	private readonly Selector _sel2;

	public MergeCards(object selector1, Selector selector2) : base(selector1)
	{
		_sel2 = selector2;
	}

	public override object? Do(BaseEntity source, BaseEntity target, params object[] args)
	{
		var entities = source.Game?.Entities ?? new List<BaseEntity>();
		var list2 = _sel2.Eval(entities, source);
		if (list2.Count == 0) return null;

		var card1 = target as Character;
		var card2 = list2[0] as Character;
		if (card1 == null || card2 == null) return null;

		var player = (source as BuffableEntity)?.Controller;
		if (player == null) return null;

		// 以第一张卡为底板创建新实体
		var baseData = card1.CardData;
		if (baseData == null) return null;

		var merged = player.CreateCard(baseData, Zone.SetAside);
		if (merged is not Character mergedCh) return null;

		// 注册 card2 的脚本为额外脚本（合并效果）
		if (card2.CardId != null)
			mergedCh.ExtraScriptIds.Add(card2.CardId);

		// 合并属性：攻击力、生命值、法力消耗相加
		mergedCh.Atk = card1.Atk + card2.Atk;
		mergedCh.MaxHealth = card1.MaxHealth + card2.MaxHealth;
		mergedCh.Cost = card1.Cost + card2.Cost;

		// 合并种族（取并集）
		var mergedRaces = card1.Races.Union(card2.Races).Distinct().ToArray();
		mergedCh.SetRaces(mergedRaces);

		// 合并关键词（从两张卡复制机制标签）
		var mechanicTags = new[]
		{
			GameTag.Taunt, GameTag.DivineShield, GameTag.Stealth,
			GameTag.Windfury, GameTag.Charge, GameTag.Rush,
			GameTag.Lifesteal, GameTag.Poisonous, GameTag.Reborn,
			GameTag.Deathrattle, GameTag.Battlecry, GameTag.Elusive
		};
		foreach (var tag in mechanicTags)
		{
			if (card1.GetBoolProperty(tag) || card2.GetBoolProperty(tag))
			{
				var propName = Managers.CardManager.GetPropertyName(tag);
				if (propName != null)
					mergedCh.SetBoolProperty(propName, true);
			}
		}

		// 放入手牌
		mergedCh.SetZone(Zone.Hand);

		Log.Information("MergeCards: {Card1} + {Card2} → {Merged} ({Atk}/{Health} Cost {Cost})",
			card1.CardId, card2.CardId, mergedCh.CardId,
			mergedCh.Atk, mergedCh.MaxHealth, mergedCh.Cost);

		// 清理 PickedCards
		source.PickedCards.Clear();

		return mergedCh;
	}
}

// ─── Corpse (残骸) Actions ──────────

/// <summary>
/// 给控制者增加指定数量的残骸。
/// <code>new GainCorpses(S.CONTROLLER, 2)</code>
/// </summary>
public class GainCorpses : TargetedAction
{
	public GainCorpses(object target, object amount) : base(target, amount) { }

	public override object? Do(BaseEntity source, BaseEntity target, params object[] args)
	{
		int amount = ActionHelper.ResolveInt(args, 0, source);
		var player = target is Player p ? p : (target as BuffableEntity)?.Controller;
		if (player != null)
		{
			player.GainCorpses(amount);
			Log.Information("{Player} gains {Amount} corpse(s) (total: {Total})",
				player.Name, amount, player.Corpses);
		}
		return null;
	}
}

/// <summary>
/// 消耗指定数量的残骸并执行效果动作。残骸不足则不消耗也不执行。
/// <code>new SpendCorpses(S.CONTROLLER, 3, new DealDamage(S.ENEMY_CHARACTERS, 2))</code>
/// </summary>
public class SpendCorpses : TargetedAction
{
	private readonly int _count;
	private readonly Action[] _actions;

	public SpendCorpses(object target, int count, params Action[] actions) : base(target)
	{
		_count = count;
		_actions = actions;
	}

	public override object? Do(BaseEntity source, BaseEntity target, params object[] args)
	{
		var player = target is Player p ? p : (target as BuffableEntity)?.Controller;
		if (player == null) return null;

		if (player.SpendCorpses(_count) > 0)
		{
			Log.Information("{Player} spends {Count} corpse(s) (remaining: {Remaining})",
				player.Name, _count, player.Corpses);
			foreach (var action in _actions)
			{
				if (action is TargetedAction ta) ta.Trigger(source);
				else if (action is GameAction ga) ga.Trigger(source);
			}
		}
		return null;
	}
}

/// <summary>
/// 消耗残骸决定效果等级：残骸足够时执行增强效果，否则执行基础效果。
/// <code>new IfCorpses(S.CONTROLLER, 4, enhanced, baseline)</code>
/// </summary>
public class IfCorpses : TargetedAction
{
	private readonly int _count;
	private readonly Action _enhanced;
	private readonly Action? _baseline;

	public IfCorpses(object target, int count, Action enhanced, Action? baseline = null) : base(target)
	{
		_count = count;
		_enhanced = enhanced;
		_baseline = baseline;
	}

	public override object? Do(BaseEntity source, BaseEntity target, params object[] args)
	{
		var player = target is Player p ? p : (target as BuffableEntity)?.Controller;
		if (player == null) return null;

		if (player.SpendCorpses(_count) > 0)
		{
			Log.Information("{Player} spends {Count} corpse(s) for enhanced effect", player.Name, _count);
			if (_enhanced is TargetedAction ta) ta.Trigger(source);
			else if (_enhanced is GameAction ga) ga.Trigger(source);
		}
		else
		{
			if (_baseline != null)
			{
				if (_baseline is TargetedAction ta) ta.Trigger(source);
				else if (_baseline is GameAction ga) ga.Trigger(source);
			}
		}
		return null;
	}
}

/// <summary>
/// 消耗所有残骸，每份残骸召唤一个指定随从（最多受战场上限限制）。
/// <code>new SummonPerCorpse(S.CONTROLLER, "RLK_060t")</code>
/// </summary>
public class SummonPerCorpse : TargetedAction
{
	private readonly string _cardId;

	public SummonPerCorpse(object target, string cardId) : base(target)
	{
		_cardId = cardId;
	}

	public override object? Do(BaseEntity source, BaseEntity target, params object[] args)
	{
		var player = target is Player p ? p : (target as BuffableEntity)?.Controller;
		if (player == null) return null;

		int corpses = player.Corpses;
		if (corpses <= 0) return null;
		player.SpendCorpses(corpses);

		int summoned = 0;
		for (int i = 0; i < corpses && player.Field.Count < 7; i++)
		{
			var next = new Summon(target, _cardId);
			next.Trigger(source);
			summoned++;
		}

		Log.Information("{Player} uses {Corpses} corpse(s) to summon {Count} {Card}(s)",
			player.Name, corpses, summoned, _cardId);
		return summoned;
	}
}

/// <summary>
/// 消耗最多 count 份残骸，每份执行一次子动作。残骸不足时消耗剩余全部。
/// <code>
/// new SpendUpToCorpses(S.CONTROLLER, 5, new Summon(S.CONTROLLER, "RLK_060t")) // 亡者大军
/// </code>
/// </summary>
public class SpendUpToCorpses : TargetedAction
{
	private readonly int _maxCount;
	private readonly Action[] _actions;

	public SpendUpToCorpses(object target, int maxCount, params Action[] actions) : base(target)
	{
		_maxCount = maxCount;
		_actions = actions;
	}

	public override object? Do(BaseEntity source, BaseEntity target, params object[] args)
	{
		var player = target is Player p ? p : (target as BuffableEntity)?.Controller;
		if (player == null) return null;

		int toSpend = Math.Min(player.Corpses, _maxCount);
		if (toSpend <= 0) return null;
		player.SpendCorpses(toSpend);

		Log.Information("{Player} 消耗 {Count} 份残骸（剩余 {Remaining}）",
			player.Name, toSpend, player.Corpses);

		for (int i = 0; i < toSpend; i++)
		{
			foreach (var action in _actions)
			{
				if (action is TargetedAction ta) ta.Trigger(source);
				else if (action is GameAction ga) ga.Trigger(source);
			}
		}
		return toSpend;
	}
}

// ─── Weapon Kill Tracking ──────────

/// <summary>
/// 英雄攻击消灭随从时，将防御者 cardId 记录到英雄武器的 KilledMinions 列表。
/// <code>
/// When&lt;AfterAttackEvent&gt;(OWN, AFTER, new RecordWeaponKill(S.DEFENDER),
///     when: And(AttackerIsHero(), DefenderDied()))
/// </code>
/// </summary>
public class RecordWeaponKill : TargetedAction
{
	public RecordWeaponKill(object defender) : base(defender) { }

	public override object? Do(BaseEntity source, BaseEntity target, params object[] args)
	{
		var weapon = (source as BuffableEntity)?.Controller?.Hero?.EquippedWeapon;
		if (weapon == null || target is not BuffableEntity be || be.CardId == null) return null;
		weapon.KilledMinions.Add(be.CardId);
		Log.Information("Weapon {Weapon} records kill: {CardId}", weapon.CardId, be.CardId);
		return null;
	}
}

/// <summary>
/// 召唤武器记录的所有被消灭的随从（霜之哀伤亡语）。
/// <code>deathrattle: new SummonFromWeaponKills(S.CONTROLLER)</code>
/// </summary>
public class SummonFromWeaponKills : TargetedAction
{
	public SummonFromWeaponKills(object target) : base(target) { }

	public override object? Do(BaseEntity source, BaseEntity target, params object[] args)
	{
		if (source is not Weapon weapon) return null;
		var player = target is Player p ? p : (target as BuffableEntity)?.Controller;
		if (player == null) return null;

		foreach (var cardId in weapon.KilledMinions)
		{
			var summoned = player.CreateCard(cardId, Zone.Invalid);
			if (summoned != null) player.Summon(summoned);
		}
		Log.Information("Weapon {Weapon} deathrattle: summoned {Count} minion(s)",
			weapon.CardId, weapon.KilledMinions.Count);
		return null;
	}
}
