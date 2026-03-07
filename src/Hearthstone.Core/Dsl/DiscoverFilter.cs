namespace HearthstoneServer.Dsl;

// ═══════════════════════════════════════════════════
// DiscoverMode — 发现后的行为模式
// ═══════════════════════════════════════════════════

/// <summary>
/// 发现选择后的处理模式。
/// </summary>
public enum DiscoverMode
{
	/// <summary>从全卡池生成新牌加入手牌（默认）。</summary>
	Create,

	/// <summary>从区域复制一张牌加入手牌，原牌不动。</summary>
	Copy,

	/// <summary>从牌库中移出选中的牌，加入手牌（真正抽牌）。</summary>
	Draw,

	/// <summary>只记录选择结果（存入 source.PickedCards），不移动卡牌。用于多步链式发现。</summary>
	Pick,
}

// ═══════════════════════════════════════════════════
// DiscoverFilter — 声明式 Discover 过滤器
// ═══════════════════════════════════════════════════

/// <summary>
/// 流式构建 Discover 过滤条件，最终转为 RandomCardPicker。
/// <para>用法：</para>
/// <code>
/// DiscoverFilter.Minion().Cost(1)          // 1 费随从
/// DiscoverFilter.Race(Race.Demon)          // 恶魔牌（任意类型）
/// DiscoverFilter.Spell().School(SpellSchool.Fire) // 火焰法术
/// DiscoverFilter.All().Set(CardSet.Core)   // 核心系列所有牌
/// DiscoverFilter.All().Standard()          // 标准模式所有牌
/// </code>
/// </summary>
public class DiscoverFilter
{
	private readonly Dictionary<string, object> _filters = new() { ["collectible"] = true };

	// ── 静态工厂 ──

	/// <summary>所有可收集卡牌。</summary>
	public static DiscoverFilter All() => new();

	/// <summary>随从。</summary>
	public static DiscoverFilter Minion() => new DiscoverFilter().Type(CardType.Minion);

	/// <summary>法术。</summary>
	public static DiscoverFilter Spell() => new DiscoverFilter().Type(CardType.Spell);

	/// <summary>武器。</summary>
	public static DiscoverFilter Weapon() => new DiscoverFilter().Type(CardType.Weapon);

	/// <summary>指定种族的牌。</summary>
	public static DiscoverFilter OfRace(Race race) => new DiscoverFilter().Race(race);

	// ── 流式过滤方法 ──

	/// <summary>按卡牌类型过滤。</summary>
	public DiscoverFilter Type(CardType t) { _filters["type"] = t; return this; }

	/// <summary>按费用过滤。支持 int 或 LazyNum。</summary>
	public DiscoverFilter Cost(object c) { _filters["cost"] = c; return this; }

	/// <summary>按种族过滤。</summary>
	public DiscoverFilter Race(Race r) { _filters["race"] = r; return this; }

	/// <summary>按法术派系过滤。</summary>
	public DiscoverFilter School(SpellSchool s) { _filters["spell_school"] = s; return this; }

	/// <summary>按拓展包过滤。</summary>
	public DiscoverFilter Set(CardSet s) { _filters["card_set"] = s; return this; }

	/// <summary>按职业过滤。</summary>
	public DiscoverFilter Class(CardClass c) { _filters["card_class"] = c; return this; }

	/// <summary>运行时按指定玩家的职业过滤（如 S.OPPONENT、S.CONTROLLER）。</summary>
	public DiscoverFilter ClassOf(Selector s) { _filters["card_class"] = s; return this; }

	/// <summary>按稀有度过滤。</summary>
	public DiscoverFilter OfRarity(Rarity r) { _filters["rarity"] = r; return this; }

	/// <summary>限制为标准模式合法卡牌。</summary>
	public DiscoverFilter Standard() { _filters["is_standard"] = true; return this; }

	/// <summary>强制从狂野卡池发现（标准模式中也可以发现狂野卡）。</summary>
	public DiscoverFilter Wild() { _filters["is_standard"] = false; return this; }

	/// <summary>按机制过滤（战吼、亡语、嘲讽等）。</summary>
	public DiscoverFilter HasMechanic(Mechanic m) { _filters["mechanic"] = m; return this; }

	// ── 来源指定 ──

	private Selector? _source;

	/// <summary>从己方牌库发现。</summary>
	public DiscoverFilter FromDeck() { _source = S.FRIENDLY_DECK; return this; }

	/// <summary>从己方手牌发现。</summary>
	public DiscoverFilter FromHand() { _source = S.FRIENDLY_HAND; return this; }

	/// <summary>从敌方牌库发现。</summary>
	public DiscoverFilter FromEnemyDeck() { _source = S.ENEMY_DECK; return this; }

	/// <summary>从敌方手牌发现。</summary>
	public DiscoverFilter FromEnemyHand() { _source = S.ENEMY_HAND; return this; }

	/// <summary>从指定 Selector 来源发现。</summary>
	public DiscoverFilter From(Selector source) { _source = source; return this; }

	// ── 模式 ──

	private DiscoverMode _mode = DiscoverMode.Create;

	/// <summary>复制模式：原牌不动，复制品加入手牌。</summary>
	public DiscoverFilter AsCopy() { _mode = DiscoverMode.Copy; return this; }

	/// <summary>抽牌模式：从牌库移到手牌。</summary>
	public DiscoverFilter AsDraw() { _mode = DiscoverMode.Draw; return this; }

	/// <summary>选择模式：只记录不移动，用于 .Then() 链式。</summary>
	public DiscoverFilter AsPick() { _mode = DiscoverMode.Pick; return this; }

	// ── 转换 ──

	/// <summary>是否使用区域来源（而非全卡池）。</summary>
	public bool HasSource => _source != null;

	/// <summary>获取来源 Selector。</summary>
	public Selector? Source => _source;

	/// <summary>获取发现模式。</summary>
	public DiscoverMode Mode => _mode;

	/// <summary>生成 RandomCardPicker（3 张候选卡用）。</summary>
	public RandomCardPicker ToPicker() => new(new Dictionary<string, object>(_filters));
}
