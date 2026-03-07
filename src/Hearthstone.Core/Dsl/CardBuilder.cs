using HearthstoneServer.Auras;
using HearthstoneServer.Entities;
using HearthstoneServer.Events;
using HearthstoneServer.Targeting;
using System.Linq;

namespace HearthstoneServer.Dsl;

// ═══════════════════════════════════════════════════
// BuffValue — 支持静态 int 和动态值的 buff 数值
// ═══════════════════════════════════════════════════

/// <summary>
/// Buff 数值：可以是静态 int，也可以是运行时从 source 解析的动态值。
/// <para>支持 implicit 转换，静态和动态写法统一：</para>
/// <code>
/// b.Buff("xx_e", atk: 2);                     // 静态 +2
/// b.Buff("xx_e", atk: BuffValue.SOURCE_ATK);   // 动态：source 攻击力
/// </code>
/// </summary>
public readonly struct BuffValue
{
	private readonly int _static;
	private readonly Func<BuffableEntity, int>? _dynamic;

	private BuffValue(int value) { _static = value; _dynamic = null; }
	private BuffValue(Func<BuffableEntity, int> func) { _static = 0; _dynamic = func; }

	/// <summary>是否为空（默认值，不产生 buff）。</summary>
	public bool IsEmpty => _static == 0 && _dynamic == null;

	/// <summary>解析最终数值。静态直接返回，动态用 source 计算。</summary>
	public int Resolve(BuffableEntity? source)
		=> _dynamic != null && source != null ? _dynamic(source) : _static;

	// ── implicit 转换：int → BuffValue ──
	public static implicit operator BuffValue(int value) => new(value);

	// ── implicit 转换：LazyNum → BuffValue（声明式动态值）──
	public static implicit operator BuffValue(LazyNum lazy) => new(src => lazy.Value(src));

	// ── 声明式动态常量 ──

	/// <summary>source 的攻击力。</summary>
	public static readonly BuffValue SOURCE_ATK =
		new(src => src is Character ch ? ch.Atk : src.GetIntProperty("atk"));

	/// <summary>source 的生命值。</summary>
	public static readonly BuffValue SOURCE_HEALTH =
		new(src => src is Character ch ? ch.Health : src.GetIntProperty("health"));
}

/// <summary>
/// Buff 的事件包装类型。
/// 支持单个事件或事件数组两种写法。
/// </summary>
public readonly struct BuffOn
{
	public BuffOn(BusEvent[] items) => Items = items;

	public BusEvent[] Items { get; }

	public static implicit operator BuffOn(BusEvent single) => new(new[] { single });

	public static implicit operator BuffOn(BusEvent[] many) => new(many);
}

/// <summary>
/// Buff 的额外标签包装类型。
/// 支持单个标签或标签数组两种写法。
/// </summary>
public readonly struct BuffExtraTags
{
	public BuffExtraTags((GameTag tag, object value)[] items) => Items = items;

	public (GameTag tag, object value)[] Items { get; }

	public static implicit operator BuffExtraTags((GameTag tag, object value) single)
		=> new(new[] { single });

	public static implicit operator BuffExtraTags((GameTag tag, object value)[] many)
		=> new(many);
}

// ═══════════════════════════════════════════════════
// CardBuilder — 声明式卡牌脚本注册 API
// ═══════════════════════════════════════════════════

/// <summary>
/// 声明式卡牌脚本注册 API。
/// <para>
/// 用法示例：
/// <code>
/// var b = new CardBuilder();
/// b.Card("BT_142", play: new Enchant(S.CONTROLLER, "BT_142e"));
/// b.Buff("BT_142e", atk: 1);
/// b.Card("BT_036", play: new Summon(S.CONTROLLER, "BT_036t") * 3);
/// </code>
/// </para>
/// </summary>
public class CardBuilder
{
	private readonly CardScriptRegistry _registry;

	public CardBuilder() : this(CardScriptRegistry.Instance) { }
	public CardBuilder(CardScriptRegistry registry) => _registry = registry;

	/// <summary>
	/// 注册一张卡牌的脚本。所有参数都是可选的 —— 只填你需要的。
	/// </summary>
	public void Card(string cardId,
		Action[]? play = null,
		Action[]? battlecry = null,
		BusEvent[]? on = null,
		Action[]? deathrattle = null,
		Action[]? combo = null,
		Action[]? inspire = null,
		Refresh[]? update = null,
		TargetRequirement? target = null,
		string[]? chooseCards = null,
		string[]? entourage = null,
		Refresh[]? enrage = null,

		LazyNum? costMod = null,
		Func<BaseEntity, int, int>? costModFunc = null,
		// ── 扩展钩子 ──
		Action[]? activate = null,
		Action[]? location = null,
		Action[]? outcast = null,
		Action[]? awaken = null,
		Action[]? reward = null,
		Action[]? addProgress = null,
		Action[]? discard = null,
		Action[][]? secretDeathrattles = null,
		Action[]? overkill = null,
		Action[]? draw = null,
		Refresh[]? handUpdate = null,
		Refresh[]? deckUpdate = null,
		int progressTotal = 0,
		int dormantTurns = 0,
		System.Action<BuffableEntity, Game>? onActivate = null,
		BusEvent? secret = null)
	{
		// secret: 参数自动包装 RevealSecret + Once
		var effectiveOn = on;
		if (secret != null)
		{
			var wrapped = secret.WrapSecret();
			effectiveOn = effectiveOn != null
				? effectiveOn.Append(wrapped).ToArray()
				: new[] { wrapped };
		}

		_registry.Register(cardId, new InlineCardScript
		{
			Play = play ?? System.Array.Empty<Action>(),
			Battlecry = battlecry ?? System.Array.Empty<Action>(),
			OnActivate = MergeActivate(onActivate, effectiveOn),
			Deathrattle = deathrattle ?? System.Array.Empty<Action>(),
			Combo = combo ?? System.Array.Empty<Action>(),
			Inspire = inspire ?? System.Array.Empty<Action>(),
			Update = update ?? System.Array.Empty<Refresh>(),
			TargetReq = target ?? TargetRequirement.None,
			ChooseCards = chooseCards ?? System.Array.Empty<string>(),
			Entourage = entourage ?? System.Array.Empty<string>(),
			Enrage = enrage ?? System.Array.Empty<Refresh>(),

			CostMod = costMod,
			CostModFunc = costModFunc,
			// ── 扩展钩子 ──
			Activate = activate ?? System.Array.Empty<Action>(),
			Location = location ?? System.Array.Empty<Action>(),
			Outcast = outcast ?? System.Array.Empty<Action>(),
			Awaken = awaken ?? System.Array.Empty<Action>(),
			Reward = reward ?? System.Array.Empty<Action>(),
			AddProgress = addProgress ?? System.Array.Empty<Action>(),
			Discard = discard ?? System.Array.Empty<Action>(),
			SecretDeathrattles = secretDeathrattles ?? System.Array.Empty<Action[]>(),
			Overkill = overkill ?? System.Array.Empty<Action>(),
			Draw = draw ?? System.Array.Empty<Action>(),
			HandUpdate = handUpdate ?? System.Array.Empty<Refresh>(),
			DeckUpdate = deckUpdate ?? System.Array.Empty<Refresh>(),
			ProgressTotal = progressTotal,
			DormantTurns = dormantTurns,
		});
	}

	/// <summary>
	/// 一行注册 Enchantment（buff）脚本。
	/// </summary>
	public void Buff(string cardId, BuffValue atk = default, BuffValue health = default,
		int cost = 0, int durability = 0, int armor = 0,
		Func<BaseEntity, int, int>? atkMod = null,
		Func<BaseEntity, int, int>? maxHealthMod = null,
		Func<BaseEntity, int, int>? costModFunc = null,
		Func<BaseEntity, int, int>? durabilityMod = null,
		Func<BaseEntity, int, int>? armorMod = null,
		Refresh[]? update = null,
		BuffOn? on = null,
		Action[]? deathrattle = null,
		BuffExtraTags? extraTags = null)
	{
		// cost 简写：自动生成 costModFunc
		if (cost != 0 && costModFunc == null)
			costModFunc = (_, c) => c + cost;
		// durability 简写
		if (durability != 0 && durabilityMod == null)
			durabilityMod = (_, d) => d + durability;
		// armor 简写
		if (armor != 0 && armorMod == null)
			armorMod = (_, a) => a + armor;

		var tags = new Dictionary<GameTag, object>();
		if (!atk.IsEmpty) tags[GameTag.Atk] = atk;
		if (!health.IsEmpty) tags[GameTag.Health] = health;
		foreach (var (tag, value) in extraTags?.Items ?? System.Array.Empty<(GameTag tag, object value)>())
			tags[tag] = value;

		_registry.Register(cardId, new InlineCardScript
		{
			Tags = tags.Count > 0 ? tags : null,
			AtkMod = atkMod,
			MaxHealthMod = maxHealthMod,
			CostModFunc = costModFunc,
			DurabilityMod = durabilityMod,
			ArmorMod = armorMod,
			Update = update ?? System.Array.Empty<Refresh>(),
			Deathrattle = deathrattle ?? System.Array.Empty<Action>(),
			OnActivate = MergeActivate(null, on?.Items),
		});
	}

	/// <summary>
	/// 单个事件版本的 Buff 注册重载，便于脚本层直接写 on: When(...)
	/// </summary>
	/// <summary>
	/// Merge explicit onActivate callback with declarative BusEvent[] triggers.
	/// </summary>
	private static System.Action<BuffableEntity, Game>? MergeActivate(
		System.Action<BuffableEntity, Game>? onActivate, BusEvent[]? on)
	{
		if (on == null || on.Length == 0) return onActivate;

		return (self, game) =>
		{
			onActivate?.Invoke(self, game);
			foreach (var evt in on)
				evt.Activate(self, game);
		};
	}
}
