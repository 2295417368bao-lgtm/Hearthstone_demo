using HearthstoneServer.Entities;
using HearthstoneServer.Dsl;
using HearthstoneServer.Events;
using static HearthstoneServer.Dsl.S;

namespace HearthstoneServer.Cards;

/// <summary>
/// 卡牌脚本工具常量和辅助函数。
/// 使用方式：using static HearthstoneServer.Cards.CardScriptUtils;
/// </summary>
public static class CardScriptUtils
{
	/// <summary>空动作数组 — 用于 IfElse 的空分支。</summary>
	public static readonly Dsl.Action[] NOTHING = Array.Empty<Dsl.Action>();

	// ═══════════════════════════════════════
	// 标签快捷方式 — SetTag / UnsetTag
	// ═══════════════════════════════════════

	/// <summary>为目标设置单个 GameTag（封装 SetTags）。</summary>
	public static SetTags SetTag(object target, GameTag tag) => new(target, new[] { tag });
	/// <summary>为目标移除单个 GameTag（封装 UnsetTags）。</summary>
	public static UnsetTags UnsetTag(object target, GameTag tag) => new(target, new[] { tag });

	// ── 常用标签快捷方式 ──

	public static SetTags Freeze(object target) => SetTag(target, GameTag.Frozen);
	public static SetTags Stealth(object target) => SetTag(target, GameTag.Stealth);
	public static UnsetTags Unstealth(object target) => UnsetTag(target, GameTag.Stealth);
	public static SetTags Taunt(object target) => SetTag(target, GameTag.Taunt);
	public static SetTags GiveCharge(object target) => SetTag(target, GameTag.Charge);
	public static SetTags GiveDivineShield(object target) => SetTag(target, GameTag.DivineShield);
	public static SetTags GiveWindfury(object target) => SetTag(target, GameTag.Windfury);
	public static SetTags GivePoisonous(object target) => SetTag(target, GameTag.Poisonous);
	public static SetTags GiveLifesteal(object target) => SetTag(target, GameTag.Lifesteal);
	public static SetTags GiveRush(object target) => SetTag(target, GameTag.Rush);
	public static SetTags GiveReborn(object target) => SetTag(target, GameTag.Reborn);

	// ═══════════════════════════════════════
	// LazyNum 快捷方式
	// ═══════════════════════════════════════

	/// <summary>ARMOR(selector) — 实体的护甲值。</summary>
	public static Attr ARMOR(Selector sel) => new(sel, GameTag.Armor);

	/// <summary>ATK(selector) — 匹配选择器的实体总攻击力。</summary>
	public static Attr ATK(Selector sel) => new(sel, GameTag.Atk);

	/// <summary>CURRENT_HEALTH(selector) — 实体的当前生命值总和。</summary>
	public static Attr CURRENT_HEALTH(Selector sel) => new(sel, GameTag.Health);

	/// <summary>MANA(selector) — 玩家的法力水晶数。</summary>
	public static Attr MANA(Selector sel) => new(sel, GameTag.Resources);

	/// <summary>MAX_MANA(selector) — 玩家的最大法力值。</summary>
	public static Attr MAX_MANA(Selector sel) => new(sel, GameTag.Maxresources);

	/// <summary>OVERLOAD_LOCKED(selector) — 已锁定的过载法力。</summary>
	public static Attr OVERLOAD_LOCKED(Selector sel) => new(sel, GameTag.OverloadLocked);

	/// <summary>OVERLOAD_OWED(selector) — 待过载的法力。</summary>
	public static Attr OVERLOAD_OWED(Selector sel) => new(sel, GameTag.OverloadOwed);

	// ═══════════════════════════════════════
	// 复合选择器
	// ═══════════════════════════════════════

	/// <summary>HIGHEST_ATK(selector) — 选择器中攻击力最高的实体。</summary>
	public static Selector HIGHEST_ATK(Selector sel) => new FuncSelector(
		(entities, source) =>
		{
			var candidates = sel.Eval(entities, source);
			if (candidates.Count == 0) return new List<BaseEntity>();
			var maxAtk = candidates.Max(e => e is BuffableEntity be ? be.GetIntProperty("Atk") : 0);
			return candidates.Where(e => e is BuffableEntity be && be.GetIntProperty("Atk") == maxAtk).ToList();
		});

	// ═══════════════════════════════════════
	// 常用条件
	// ═══════════════════════════════════════

	/// <summary>CLEAVE — 对攻击目标相邻随从造成等于攻击力的伤害。</summary>
	public static readonly DealDamage CLEAVE = new(TARGET_ADJACENT, ATK(SELF));

	/// <summary>场上没有友方随从。</summary>
	public static readonly Evaluator EMPTY_BOARD = new ComparisonEvaluator(new Count(FRIENDLY_MINIONS), 0);

	/// <summary>手牌为空。</summary>
	public static readonly Evaluator EMPTY_HAND = new ComparisonEvaluator(new Count(FRIENDLY_HAND), 0);

	/// <summary>场上有 7 个友方随从（满场）。</summary>
	public static readonly Evaluator FULL_BOARD = new ComparisonEvaluator(new Count(FRIENDLY_MINIONS), 7);

	/// <summary>手牌已满（10 张）。</summary>
	public static readonly Evaluator FULL_HAND = new ComparisonEvaluator(
		new Count(FRIENDLY_HAND), new Attr(CONTROLLER, GameTag.Maxhandsize));

	/// <summary>法力水晶已满（≥10）。</summary>
	public static readonly Evaluator MANA_CAPPED =
		new LazyNumEvaluator(MAX_MANA(S.CONTROLLER), 10, (a, b) => a >= b);

	/// <summary>手中持有龙牌。</summary>
	public static readonly Evaluator HOLDING_DRAGON = new Find(FRIENDLY_HAND + DRAGON - SELF);

	/// <summary>当前操作目标被击杀（health ≤ 0）。用于 .IfThen(KILLED, ...)。</summary>
	public static readonly Evaluator KILLED = new Dead(CURRENT_TARGET);

	/// <summary>当前操作目标存活（health > 0）。用于 .IfThen(SURVIVED, ...)。</summary>
	public static readonly Evaluator SURVIVED = -new Dead(CURRENT_TARGET);

	/// <summary>永真条件。用于 .IfThen(ALWAYS, ...) 表示"无条件执行一次"。</summary>
	public static readonly Evaluator ALWAYS = new AlwaysTrue();

	// ═══════════════════════════════════════
	// 卡牌 ID 集合
	// ═══════════════════════════════════════

	public static readonly string[] BASIC_HERO_POWERS =
	{
		"HERO_01bp", "HERO_02bp", "HERO_03bp", "HERO_04bp", "HERO_05bp",
		"HERO_06bp", "HERO_07bp", "HERO_08bp", "HERO_09bp", "HERO_10bp",
	};

	public static readonly string[] UPGRADED_HERO_POWERS =
	{
		"HERO_01bp", "HERO_02bp", "HERO_03bp", "HERO_04bp", "HERO_05bp",
		"HERO_06bp", "HERO_07bp", "HERO_08bp", "HERO_09bp", "HERO_10bp2",
	};

	public static readonly string[] BASIC_TOTEMS =
	{
		"CS2_050", "CS2_051", "CS2_052", "NEW1_009",
	};

	public static readonly string[] POTIONS =
	{
		"CFM_021", "CFM_065", "CFM_620", "CFM_603",
		"CFM_604", "CFM_661", "CFM_662", "CFM_094",
		"CFM_608", "CFM_611",
	};

	public static readonly string[] LICH_KING_CARDS =
	{
		"ICC_314t1", "ICC_314t2", "ICC_314t3", "ICC_314t4",
		"ICC_314t5", "ICC_314t6", "ICC_314t7", "ICC_314t8",
	};

	public static readonly string THE_COIN = "GAME_005";

	public static readonly string[] LACKEY_CARDS =
	{
		"DAL_613", "DAL_614", "DAL_615", "DAL_739",
		"DAL_741", "ULD_616", "DRG_052",
	};

	// ═══════════════════════════════════════
	// 复合行动工厂
	// ═══════════════════════════════════════

	/// <summary>
	/// 发现 — 发现一张牌并加入手牌。
	/// 传入 Selector（旧写法）或 DiscoverFilter（新的声明式过滤）。
	/// </summary>
	public static Discover MakeDiscover(params object[] args)
		=> new(CONTROLLER, args);

	/// <summary>
	/// 发现 — 使用 DiscoverFilter 声明式过滤。
	/// 用法：MakeDiscover(DiscoverFilter.Minion().Cost(1))
	///       MakeDiscover(DiscoverFilter.Spell().FromDeck())
	///       MakeDiscover(DiscoverFilter.All().FromEnemyHand())
	/// </summary>
	public static Discover MakeDiscover(object target, DiscoverFilter filter)
	{
		var discover = filter.HasSource
			? new Discover(target, filter.Source!)
			: new Discover(target, filter.ToPicker());
		discover.Mode = filter.Mode;
		return discover;
	}

	/// <summary>
	/// 招募 — 从你的牌库中召唤一个随机随从。
	/// Summon(CONTROLLER, RANDOM(FRIENDLY_DECK + MINION))
	/// </summary>
	public static readonly Summon RECRUIT = new(CONTROLLER, Random(FRIENDLY_DECK + MINION));

	/// <summary>
	/// 带子选择器的招募。
	/// Summon(CONTROLLER, RANDOM(FRIENDLY_DECK + MINION + selector))
	/// </summary>
	public static Summon Recruit(Selector selector)
		=> new(CONTROLLER, Random(FRIENDLY_DECK + MINION + selector));

	// ═══════════════════════════════════════
	// 增益工厂
	// ═══════════════════════════════════════

	/// <summary>
	/// 根据攻击力/生命值和可选标签创建增益定义。
	/// 返回 Dictionary&lt;GameTag, int&gt; 表示增益标签。
	/// </summary>
	public static Dictionary<GameTag, int> MakeBuff(int atk = 0, int health = 0, params (GameTag tag, int value)[] extraTags)
	{
		var tags = new Dictionary<GameTag, int>();
		if (atk != 0) tags[GameTag.Atk] = atk;
		if (health != 0) tags[GameTag.Health] = health;
		foreach (var (tag, value) in extraTags)
			tags[tag] = value;
		return tags;
	}

	/// <summary>
	/// 创建"免疫"增益（不能被伤害 + 不能被对手指定为目标）。
	/// </summary>
	public static Dictionary<GameTag, int> ImmuneBuff(bool value = true)
	{
		int v = value ? 1 : 0;
		return new Dictionary<GameTag, int>
		{
			[GameTag.CantBeDamaged] = v,
			[GameTag.CantBeTargetedByOpponents] = v,
		};
	}

	// ═══════════════════════════════════════
	// 辅助函数
	// ═══════════════════════════════════════

	/// <summary>
	/// 获得空法力水晶 — 获得法力后立即消耗。
	/// 用于获得空法力水晶的辅助方法。
	/// </summary>
	public static Dsl.Action[] GainEmptyMana(object selector, int amount)
		=> new Dsl.Action[]
		{
			new GainMana(selector, amount),
			new SpendManaAction(selector, amount),
		};



	/// <summary>
	/// SetToIf(condition, value) — 条件满足时设为指定值，否则保持原值。
	/// </summary>
	public static Func<BaseEntity, int, int> SetToIf(Evaluator condition, int value)
		=> (self, cur) => condition.Check(self) ? value : cur;

	/// <summary>
	/// AT_MAX_MANA — 检查玩家是否已达最大法力值。
	/// </summary>
	public static Evaluator AT_MAX_MANA(Selector sel)
		=> new ComparisonEvaluator(MANA(sel), MAX_MANA(sel));

	/// <summary>
	/// IsOverloaded — 检查玩家是否有过载法力（锁定或待过载）。
	/// </summary>
	public static bool IsOverloaded(BaseEntity source, Selector sel)
	{
		var entities = source.Game?.Entities ?? new List<BaseEntity>();
		var locked = OVERLOAD_LOCKED(sel).Value(source);
		var owed = OVERLOAD_OWED(sel).Value(source);
		return locked > 0 || owed > 0;
	}

	/// <summary>
	/// CastWhenDrawn — 抽到时施放：销毁自身、抽一张牌、触发战吼。
	/// </summary>
	public static Dsl.Action[] CastWhenDrawn()
		=> new Dsl.Action[]
		{
			new Destroy(SELF),
			new DrawCard(CONTROLLER),
			new BattlecryAction(SELF),
		};

	// ═══════════════════════════════════════
	// 卡牌脚本简化辅助
	// ═══════════════════════════════════════

	/// <summary>
	/// 一行注册 Enchantment 脚本。
	/// 用法：Buff("BT_142e", atk: 1) 或 Buff("CS2_053e", cost: -3)。
	/// </summary>
	public static void Buff(string cardId, int atk = 0, int health = 0, int cost = 0,
		Func<Entities.BaseEntity, int, int>? atkMod = null,
		Func<Entities.BaseEntity, int, int>? maxHealthMod = null,
		Func<Entities.BaseEntity, int, int>? costModFunc = null,
		params (GameTag tag, object value)[] extraTags)
	{
		// cost 简写：自动生成 costModFunc
		if (cost != 0 && costModFunc == null)
			costModFunc = (_, c) => c + cost;

		CardScriptRegistry.Instance.Register(cardId,
			new InlineBuff(atk, health, atkMod, maxHealthMod, costModFunc, extraTags));
	}

	/// <summary>
	/// 简化 Action[] 创建：Acts(Hit(...), Draw(...)) 代替 new Action[] { ... }。
	/// </summary>
	public static Dsl.Action[] Acts(params Dsl.Action[] actions) => actions;

	/// <summary>
	/// 声明式条件分支：IfElse(condition, trueAction, falseAction)。
	/// </summary>
	public static ConditionalAction IfElse(Evaluator cond, Dsl.Action[] ifTrue, Dsl.Action[] ifFalse)
		=> new ConditionalAction(cond, ifTrue, ifFalse);
	public static ConditionalAction IfElse(Evaluator cond, Dsl.Action ifTrue, Dsl.Action[] ifFalse)
		=> new ConditionalAction(cond, new[] { ifTrue }, ifFalse);
	public static ConditionalAction IfElse(Evaluator cond, Dsl.Action[] ifTrue, Dsl.Action ifFalse)
		=> new ConditionalAction(cond, ifTrue, new[] { ifFalse });
	public static ConditionalAction IfElse(Evaluator cond, Dsl.Action ifTrue, Dsl.Action ifFalse)
		=> new ConditionalAction(cond, new[] { ifTrue }, new[] { ifFalse });

	/// <summary>
	/// 取反 Evaluator：Not(new Find(S.FRIENDLY_MINIONS - S.SELF))。
	/// </summary>
	public static Evaluator Not(Evaluator eval) => new NotEvaluator(eval);

	// ═══════════════════════════════════════
	// Buff Mod 声明式辅助
	// ═══════════════════════════════════════

	/// <summary>设为固定值：maxHealthMod: SetTo(1)</summary>
	public static Func<BaseEntity, int, int> SetTo(int value) => (_, _) => value;

	/// <summary>乘以倍数：atkMod: MultiplyBy(2)</summary>
	public static Func<BaseEntity, int, int> MultiplyBy(int factor) => (_, v) => v * factor;

	/// <summary>减半（向上取整）：costModFunc: HalveUp()</summary>
	public static Func<BaseEntity, int, int> HalveUp() => (_, v) => (v + 1) / 2;

	// ═══════════════════════════════════════
	// REMOVED_IN_PLAY — 附魔在卡牌打出时自毁
	// ═══════════════════════════════════════

	/// <summary>
	/// REMOVED_IN_PLAY — 当附魔的拥有者被打出时，移除该附魔。
	/// 用于费用修改增益（如"你的下一个法术费用减少 (2) 点"）。
	/// Python: Summon(ALL_PLAYERS, OWNER).after(Destroy(SELF))
	/// </summary>
	public static readonly BusEvent REMOVED_IN_PLAY =
		BusEvent.WhenDo<CardPlayedEvent>(
			BusEvent.ANY, BusEvent.ON,
			(evt, self) =>
			{
				if (self is Enchantment ench && ench.Owner != null && evt.Card == ench.Owner)
					ench.Remove();
			});
}

/// <summary>
/// 基于比较的简单 Evaluator：检查 LazyNum 是否等于某值。
/// 用于 EMPTY_BOARD、FULL_BOARD 等。
/// </summary>
public class ComparisonEvaluator : Evaluator
{
	private readonly LazyNum _left;
	private readonly object _right; // int 或 LazyNum

	public ComparisonEvaluator(LazyNum left, int right)
	{
		_left = left;
		_right = right;
	}

	public ComparisonEvaluator(LazyNum left, LazyNum right)
	{
		_left = left;
		_right = right;
	}

	public override bool Check(BaseEntity source)
	{
		int leftVal = _left.Value(source);
		int rightVal = _right switch
		{
			int i => i,
			LazyNum ln => ln.Value(source),
			_ => 0
		};
		return leftVal == rightVal;
	}
}
