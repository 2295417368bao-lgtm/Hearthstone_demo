namespace HearthstoneServer.Targeting;

/// <summary>
/// 卡牌目标选择与打出条件的完整描述。
/// 由 <see cref="Target"/> 的流式 Builder 创建。
/// </summary>
public sealed class TargetRequirement
{
	// ── 目标选择模式 ──

	/// <summary>必须选择一个目标才能打出。</summary>
	public bool MustTarget { get; init; }

	/// <summary>有合法目标时可选目标（无合法目标时仍可打出）。</summary>
	public bool TargetIfAvailable { get; init; }

	/// <summary>连击时才需要选择目标。</summary>
	public bool TargetForCombo { get; init; }

	// ── 目标类型过滤 ──

	/// <summary>只能选择随从。</summary>
	public bool MinionOnly { get; init; }

	/// <summary>只能选择英雄。</summary>
	public bool HeroOnly { get; init; }

	/// <summary>只能选择地标。</summary>
	public bool LocationOnly { get; init; }

	/// <summary>只能选择随从或敌方英雄。</summary>
	public bool MinionOrEnemyHero { get; init; }

	// ── 目标阵营过滤 ──

	/// <summary>只能选择己方目标。</summary>
	public bool FriendlyOnly { get; init; }

	/// <summary>只能选择敌方目标。</summary>
	public bool EnemyOnly { get; init; }

	/// <summary>不能选中自身。</summary>
	public bool NonSelf { get; init; }

	// ── 目标属性过滤 ──

	/// <summary>目标最大攻击力限制。-1 表示无限制。</summary>
	public int MaxAttack { get; init; } = -1;

	/// <summary>目标最小攻击力限制。-1 表示无限制。</summary>
	public int MinAttack { get; init; } = -1;

	/// <summary>目标最大生命值限制。-1 表示无限制。</summary>
	public int MaxHealth { get; init; } = -1;

	/// <summary>目标最小生命值限制。-1 表示无限制。</summary>
	public int MinHealth { get; init; } = -1;

	/// <summary>目标最大费用限制。-1 表示无限制。</summary>
	public int MaxCost { get; init; } = -1;

	/// <summary>目标最小费用限制。-1 表示无限制。</summary>
	public int MinCost { get; init; } = -1;

	/// <summary>只能选择已受伤目标。</summary>
	public bool DamagedOnly { get; init; }

	/// <summary>只能选择未受伤目标。</summary>
	public bool UndamagedOnly { get; init; }

	/// <summary>只能选择被冻结的目标。</summary>
	public bool FrozenOnly { get; init; }

	/// <summary>只能选择嘲讽目标。</summary>
	public bool TauntOnly { get; init; }

	/// <summary>只能选择传说品质目标。</summary>
	public bool LegendaryOnly { get; init; }

	/// <summary>目标必须具有指定机制（战吼、亡语、潜行等）。null 表示无限制。</summary>
	public Mechanic? WithMechanic { get; init; }

	/// <summary>目标必须具有指定种族。-1 表示无限制。</summary>
	public int WithRace { get; init; } = -1;

	/// <summary>敌方目标不能是免疫的。</summary>
	public bool EnemyNotImmune { get; init; }

	/// <summary>目标必须是友方走狗（Lackey）。</summary>
	public bool FriendlyLackey { get; init; }

	/// <summary>随从攻击力必须 ≤ 当前法力值。</summary>
	public bool AttackLessOrEqualMana { get; init; }

	// ── 打出条件（不影响目标选择，影响能否打出） ──

	/// <summary>场上至少需要 N 个敌方随从才能打出。</summary>
	public int MinEnemyMinions { get; init; }

	/// <summary>场上至少需要 N 个总随从（双方合计）才能打出。</summary>
	public int MinTotalMinions { get; init; }

	/// <summary>需要装备武器才能打出。</summary>
	public bool NeedsWeapon { get; init; }

	// ── 条件目标（满足条件时才出现目标选择） ──

	/// <summary>手牌中有龙时可选目标。</summary>
	public bool IfDragonInHand { get; init; }

	/// <summary>至少有 N 个友方随从时可选目标。</summary>
	public int IfMinFriendlyMinions { get; init; }

	/// <summary>至少有 N 个友方奥秘时可选目标。</summary>
	public int IfMinFriendlySecrets { get; init; }

	/// <summary>上回合打出元素时可选目标。</summary>
	public bool IfElementalPlayedLastTurn { get; init; }

	/// <summary>牌组中没有 3 费牌时可选目标。</summary>
	public bool IfNo3CostInDeck { get; init; }

	/// <summary>英雄有攻击力时可选目标。</summary>
	public bool IfHeroHasAttack { get; init; }

	/// <summary>本回合已打出至少 N 个法术时可选目标。</summary>
	public int IfMinSpellsThisTurn { get; init; }

	/// <summary>有过载法力值时可选目标。</summary>
	public bool IfHasOverloadedMana { get; init; }

	/// <summary>英雄本回合已攻击过时可选目标。</summary>
	public bool IfHeroAttackedThisTurn { get; init; }

	/// <summary>本回合抽到的牌时可选目标。</summary>
	public bool IfDrawnThisTurn { get; init; }

	/// <summary>非本回合抽到的牌时可选目标。</summary>
	public bool IfNotDrawnThisTurn { get; init; }

	/// <summary>牌组中只有偶数费牌时可选目标。</summary>
	public bool IfOnlyEvenCostInDeck { get; init; }

	/// <summary>牌组中只有奇数费牌时可选目标。</summary>
	public bool IfOnlyOddCostInDeck { get; init; }

	/// <summary>手牌中有 5 费或以上法术时可选目标。</summary>
	public bool IfCost5OrMoreSpellInHand { get; init; }

	/// <summary>至少有 N 个法力水晶时可选目标。</summary>
	public int IfMinManaCrystals { get; init; }

	/// <summary>拥有友方走狗时可选目标。</summary>
	public bool IfFriendlyLackey { get; init; }

	/// <summary>本回合玩家生命值变化过时可选目标。</summary>
	public bool IfPlayerHealthChangedThisTurn { get; init; }

	/// <summary>牌组中有灵魂碎片时可选目标。</summary>
	public bool IfSoulFragmentInDeck { get; init; }

	// ── 辅助方法 ──

	/// <summary>是否有目标选择能力（任一目标模式为 true）。</summary>
	public bool HasTargeting => MustTarget || TargetIfAvailable || TargetForCombo
		|| IfDragonInHand || IfMinFriendlyMinions > 0 || IfMinFriendlySecrets > 0
		|| IfHeroAttackedThisTurn || IfElementalPlayedLastTurn
		|| IfNo3CostInDeck || IfHeroHasAttack || IfMinSpellsThisTurn > 0
		|| IfHasOverloadedMana || IfDrawnThisTurn || IfNotDrawnThisTurn
		|| IfOnlyEvenCostInDeck || IfOnlyOddCostInDeck
		|| IfCost5OrMoreSpellInHand || IfMinManaCrystals > 0
		|| IfFriendlyLackey;

	/// <summary>空实例（无任何要求）。</summary>
	public static readonly TargetRequirement None = new();
}

/// <summary>
/// 流式 Builder，用于创建 <see cref="TargetRequirement"/>。
/// 用法示例：<c>Target.Required().Minion().Enemy()</c>
/// </summary>
public static class Target
{
	/// <summary>必须选择一个目标才能打出。</summary>
	public static TargetBuilder Required() => new() { _mustTarget = true };

	/// <summary>有合法目标时可选目标。</summary>
	public static TargetBuilder IfAvailable() => new() { _ifAvailable = true };

	/// <summary>连击时才需要目标。</summary>
	public static TargetBuilder ForCombo() => new() { _forCombo = true };

	/// <summary>无需目标（仅设置打出条件）。</summary>
	public static TargetBuilder Condition() => new();
}

/// <summary>
/// <see cref="TargetRequirement"/> 的 Builder。
/// 通过链式调用设置各种约束，最终通过隐式转换生成 <see cref="TargetRequirement"/>。
/// </summary>
public class TargetBuilder
{
	// 目标模式
	internal bool _mustTarget;
	internal bool _ifAvailable;
	internal bool _forCombo;

	// 目标类型
	private bool _minionOnly;
	private bool _heroOnly;
	private bool _locationOnly;
	private bool _minionOrEnemyHero;

	// 阵营
	private bool _friendlyOnly;
	private bool _enemyOnly;
	private bool _nonSelf;

	// 属性过滤
	private int _maxAttack = -1;
	private int _minAttack = -1;
	private int _maxHealth = -1;
	private int _minHealth = -1;
	private int _maxCost = -1;
	private int _minCost = -1;
	private bool _damagedOnly;
	private bool _undamagedOnly;
	private bool _frozenOnly;
	private bool _tauntOnly;
	private bool _legendaryOnly;
	private Mechanic? _withMechanic;
	private int _withRace = -1;
	private bool _enemyNotImmune;
	private bool _friendlyLackey;
	private bool _attackLessOrEqualMana;

	// 打出条件
	private int _minEnemyMinions;
	private int _minTotalMinions;
	private bool _needsWeapon;

	// 条件目标
	private bool _ifDragonInHand;
	private int _ifMinFriendlyMinions;
	private int _ifMinFriendlySecrets;
	private bool _ifElementalPlayedLastTurn;
	private bool _ifNo3CostInDeck;
	private bool _ifHeroHasAttack;
	private int _ifMinSpellsThisTurn;
	private bool _ifHasOverloadedMana;
	private bool _ifHeroAttackedThisTurn;
	private bool _ifDrawnThisTurn;
	private bool _ifNotDrawnThisTurn;
	private bool _ifOnlyEvenCostInDeck;
	private bool _ifOnlyOddCostInDeck;
	private bool _ifCost5OrMoreSpellInHand;
	private int _ifMinManaCrystals;
	private bool _ifFriendlyLackey;
	private bool _ifPlayerHealthChangedThisTurn;
	private bool _ifSoulFragmentInDeck;

	// ── 目标类型 ──
	public TargetBuilder Minion() { _minionOnly = true; return this; }
	public TargetBuilder Hero() { _heroOnly = true; return this; }
	public TargetBuilder Location() { _locationOnly = true; return this; }
	public TargetBuilder MinionOrEnemyHero() { _minionOrEnemyHero = true; return this; }

	// ── 阵营 ──
	public TargetBuilder Friendly() { _friendlyOnly = true; return this; }
	public TargetBuilder Enemy() { _enemyOnly = true; return this; }
	public TargetBuilder NonSelf() { _nonSelf = true; return this; }

	// ── 属性过滤 ──
	public TargetBuilder MaxAttack(int max) { _maxAttack = max; return this; }
	public TargetBuilder MinAttack(int min) { _minAttack = min; return this; }
	public TargetBuilder MaxHealth(int max) { _maxHealth = max; return this; }
	public TargetBuilder MinHealth(int min) { _minHealth = min; return this; }
	public TargetBuilder MaxCost(int max) { _maxCost = max; return this; }
	public TargetBuilder MinCost(int min) { _minCost = min; return this; }
	public TargetBuilder Damaged() { _damagedOnly = true; return this; }
	public TargetBuilder Undamaged() { _undamagedOnly = true; return this; }
	public TargetBuilder Frozen() { _frozenOnly = true; return this; }
	public TargetBuilder Taunt() { _tauntOnly = true; return this; }
	public TargetBuilder Legendary() { _legendaryOnly = true; return this; }
	public TargetBuilder Mechanic(Mechanic m) { _withMechanic = m; return this; }
	public TargetBuilder Race(Race race) { _withRace = (int)race; return this; }
	public TargetBuilder EnemyNotImmune() { _enemyNotImmune = true; return this; }
	public TargetBuilder Lackey() { _friendlyLackey = true; return this; }
	public TargetBuilder AttackLessOrEqualMana() { _attackLessOrEqualMana = true; return this; }

	// ── 打出条件 ──
	public TargetBuilder NeedsWeapon() { _needsWeapon = true; return this; }
	public TargetBuilder NeedsEnemyMinions(int n) { _minEnemyMinions = n; return this; }
	public TargetBuilder NeedsTotalMinions(int n) { _minTotalMinions = n; return this; }

	// ── 条件目标 ──
	public TargetBuilder WithDragonInHand() { _ifDragonInHand = true; return this; }
	public TargetBuilder WithMinFriendlyMinions(int n) { _ifMinFriendlyMinions = n; return this; }
	public TargetBuilder WithMinFriendlySecrets(int n) { _ifMinFriendlySecrets = n; return this; }
	public TargetBuilder WithElementalPlayedLastTurn() { _ifElementalPlayedLastTurn = true; return this; }
	public TargetBuilder WithNo3CostInDeck() { _ifNo3CostInDeck = true; return this; }
	public TargetBuilder WithHeroAttack() { _ifHeroHasAttack = true; return this; }
	public TargetBuilder WithMinSpellsThisTurn(int n) { _ifMinSpellsThisTurn = n; return this; }
	public TargetBuilder WithOverloadedMana() { _ifHasOverloadedMana = true; return this; }
	public TargetBuilder WithHeroAttackedThisTurn() { _ifHeroAttackedThisTurn = true; return this; }
	public TargetBuilder WithDrawnThisTurn() { _ifDrawnThisTurn = true; return this; }
	public TargetBuilder WithNotDrawnThisTurn() { _ifNotDrawnThisTurn = true; return this; }
	public TargetBuilder WithOnlyEvenCostInDeck() { _ifOnlyEvenCostInDeck = true; return this; }
	public TargetBuilder WithOnlyOddCostInDeck() { _ifOnlyOddCostInDeck = true; return this; }
	public TargetBuilder WithCost5OrMoreSpellInHand() { _ifCost5OrMoreSpellInHand = true; return this; }
	public TargetBuilder WithMinManaCrystals(int n) { _ifMinManaCrystals = n; return this; }
	public TargetBuilder WithFriendlyLackey() { _ifFriendlyLackey = true; return this; }
	public TargetBuilder WithPlayerHealthChangedThisTurn() { _ifPlayerHealthChangedThisTurn = true; return this; }
	public TargetBuilder WithSoulFragmentInDeck() { _ifSoulFragmentInDeck = true; return this; }

	// ── 构建 ──
	public TargetRequirement Build() => new()
	{
		MustTarget = _mustTarget,
		TargetIfAvailable = _ifAvailable,
		TargetForCombo = _forCombo,
		MinionOnly = _minionOnly,
		HeroOnly = _heroOnly,
		LocationOnly = _locationOnly,
		MinionOrEnemyHero = _minionOrEnemyHero,
		FriendlyOnly = _friendlyOnly,
		EnemyOnly = _enemyOnly,
		NonSelf = _nonSelf,
		MaxAttack = _maxAttack,
		MinAttack = _minAttack,
		MaxHealth = _maxHealth,
		MinHealth = _minHealth,
		MaxCost = _maxCost,
		MinCost = _minCost,
		DamagedOnly = _damagedOnly,
		UndamagedOnly = _undamagedOnly,
		FrozenOnly = _frozenOnly,
		TauntOnly = _tauntOnly,
		LegendaryOnly = _legendaryOnly,
		WithMechanic = _withMechanic,
		WithRace = _withRace,
		EnemyNotImmune = _enemyNotImmune,
		FriendlyLackey = _friendlyLackey,
		AttackLessOrEqualMana = _attackLessOrEqualMana,
		MinEnemyMinions = _minEnemyMinions,
		MinTotalMinions = _minTotalMinions,
		NeedsWeapon = _needsWeapon,
		IfDragonInHand = _ifDragonInHand,
		IfMinFriendlyMinions = _ifMinFriendlyMinions,
		IfMinFriendlySecrets = _ifMinFriendlySecrets,
		IfElementalPlayedLastTurn = _ifElementalPlayedLastTurn,
		IfNo3CostInDeck = _ifNo3CostInDeck,
		IfHeroHasAttack = _ifHeroHasAttack,
		IfMinSpellsThisTurn = _ifMinSpellsThisTurn,
		IfHasOverloadedMana = _ifHasOverloadedMana,
		IfHeroAttackedThisTurn = _ifHeroAttackedThisTurn,
		IfDrawnThisTurn = _ifDrawnThisTurn,
		IfNotDrawnThisTurn = _ifNotDrawnThisTurn,
		IfOnlyEvenCostInDeck = _ifOnlyEvenCostInDeck,
		IfOnlyOddCostInDeck = _ifOnlyOddCostInDeck,
		IfCost5OrMoreSpellInHand = _ifCost5OrMoreSpellInHand,
		IfMinManaCrystals = _ifMinManaCrystals,
		IfFriendlyLackey = _ifFriendlyLackey,
		IfPlayerHealthChangedThisTurn = _ifPlayerHealthChangedThisTurn,
		IfSoulFragmentInDeck = _ifSoulFragmentInDeck,
	};

	/// <summary>隐式转换为 <see cref="TargetRequirement"/>，方便直接赋值。</summary>
	public static implicit operator TargetRequirement(TargetBuilder b) => b.Build();
}
