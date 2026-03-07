using HearthstoneServer.Entities;

namespace HearthstoneServer.Dsl;

// ─────────────────────────────────────────────
// LazyValue — abstract base for deferred evaluation
// ─────────────────────────────────────────────

public abstract class LazyValue
{
	public abstract object? Evaluate(BaseEntity source);
}

// ─────────────────────────────────────────────
// LazyNum — abstract base for numeric deferred evaluation
// ─────────────────────────────────────────────

public abstract class LazyNum : LazyValue
{
	public int Base { get; set; } = 1;

	public abstract int Value(BaseEntity source);

	public override object? Evaluate(BaseEntity source) => Value(source);

	// ── Operators ──

	public static LazyNum operator +(LazyNum a, LazyNum b) => new BinOpLazyNum(a, b, (x, y) => x + y);
	public static LazyNum operator -(LazyNum a, LazyNum b) => new BinOpLazyNum(a, b, (x, y) => x - y);
	public static LazyNum operator *(LazyNum a, LazyNum b) => new BinOpLazyNum(a, b, (x, y) => x * y);
	public static LazyNum operator /(LazyNum a, LazyNum b) => new BinOpLazyNum(a, b, (x, y) => x / y);
	public static LazyNum operator %(LazyNum a, LazyNum b) => new BinOpLazyNum(a, b, (x, y) => x % y);

	public static LazyNum operator +(LazyNum a, int b) => new BinOpLazyNum(a, new IntLazyNum(b), (x, y) => x + y);
	public static LazyNum operator -(LazyNum a, int b) => new BinOpLazyNum(a, new IntLazyNum(b), (x, y) => x - y);
	public static LazyNum operator *(LazyNum a, int b) => new BinOpLazyNum(a, new IntLazyNum(b), (x, y) => x * y);
	public static LazyNum operator /(LazyNum a, int b) => new BinOpLazyNum(a, new IntLazyNum(b), (x, y) => x / y);
	public static LazyNum operator %(LazyNum a, int b) => new BinOpLazyNum(a, new IntLazyNum(b), (x, y) => x % y);

	public static LazyNum operator +(int a, LazyNum b) => new BinOpLazyNum(new IntLazyNum(a), b, (x, y) => x + y);
	public static LazyNum operator -(int a, LazyNum b) => new BinOpLazyNum(new IntLazyNum(a), b, (x, y) => x - y);
	public static LazyNum operator *(int a, LazyNum b) => new BinOpLazyNum(new IntLazyNum(a), b, (x, y) => x * y);
	public static LazyNum operator /(int a, LazyNum b) => new BinOpLazyNum(new IntLazyNum(a), b, (x, y) => x / y);
	public static LazyNum operator %(int a, LazyNum b) => new BinOpLazyNum(new IntLazyNum(a), b, (x, y) => x % y);

	// ── 预定义常量 ──

	/// <summary>控制者手牌数。</summary>
	public static readonly LazyNum HAND_COUNT = Of(s => (s as BuffableEntity)?.Controller?.Hand.Count ?? 0);

	/// <summary>控制者牌库剩余牌数。</summary>
	public static readonly LazyNum DECK_COUNT = Of(s => (s as BuffableEntity)?.Controller?.Deck.Count ?? 0);

	/// <summary>控制者场上随从数。</summary>
	public static readonly LazyNum FIELD_COUNT = Of(s => (s as BuffableEntity)?.Controller?.Field.Count ?? 0);

	/// <summary>控制者装备武器的攻击力（无武器为 0）。</summary>
	public static readonly LazyNum WEAPON_ATK = Of(s => (s as BuffableEntity)?.Controller?.Hero?.EquippedWeapon?.Atk ?? 0);

	/// <summary>当前目标的攻击力（配合 ForEach 使用）。</summary>
	public static readonly LazyNum ATK = Of(s => s.CurrentTarget is Character ch ? ch.Atk : 0);

	/// <summary>当前目标的生命值（配合 ForEach 使用）。</summary>
	public static readonly LazyNum HEALTH = Of(s => s.CurrentTarget is Character ch ? ch.Health : 0);

	/// <summary>当前目标的最大生命值。</summary>
	public static readonly LazyNum MAX_HEALTH = Of(s => s.CurrentTarget is Character ch ? ch.MaxHealth : 0);

	/// <summary>当前目标的费用。</summary>
	public static readonly LazyNum COST = Of(s => s.CurrentTarget is BuffableEntity be ? be.Cost : 0);

	/// <summary>当前目标的护甲值。</summary>
	public static readonly LazyNum ARMOR = Of(s => s.CurrentTarget is Hero h ? h.Armor : 0);

	/// <summary>当前目标已受伤害（最大生命 - 当前生命）。</summary>
	public static readonly LazyNum DAMAGE_TAKEN = Of(s => s.CurrentTarget is Character ch ? ch.MaxHealth - ch.Health : 0);

	/// <summary>事件源的费用（配合 When 触发器使用，如法术触发时取法术费用）。</summary>
	public static readonly LazyNum EVT_COST = Of(s => s.EventSource is BuffableEntity be ? be.Cost : 0);

	/// <summary>事件源的生命值（配合 When 触发器使用，如随从打出时取随从生命值）。</summary>
	public static readonly LazyNum EVT_HEALTH = Of(s => s.EventSource is Character ch ? ch.Health : 0);

	/// <summary>当前事件的主数值（如伤害、治疗、护甲或过载值）。</summary>
	public static readonly LazyNum EVT_AMOUNT = Of(s => s.EventAmount ?? 0);

	// ── Factory ──

	/// <summary>从 lambda 创建 LazyNum。</summary>
	public static LazyNum Of(Func<BaseEntity, int> func) => new FuncLazyNum(func);

	// Implicit conversion from int
	public static implicit operator LazyNum(int value) => new IntLazyNum(value);

	// Helper to apply Base multiplier
	protected int ApplyBase(int value) => (int)Math.Ceiling((double)value * Base);
}

// ─────────────────────────────────────────────
// IntLazyNum — Constant integer
// ─────────────────────────────────────────────

public class IntLazyNum : LazyNum
{
	private readonly int _value;
	public IntLazyNum(int value) => _value = value;
	public override int Value(BaseEntity source) => ApplyBase(_value);
	public override string ToString() => _value.ToString();
}

// ─────────────────────────────────────────────
// FuncLazyNum — Lambda-based deferred evaluation
// ─────────────────────────────────────────────

public class FuncLazyNum : LazyNum
{
	private readonly Func<BaseEntity, int> _func;
	public FuncLazyNum(Func<BaseEntity, int> func) => _func = func;
	public override int Value(BaseEntity source) => ApplyBase(_func(source));
}

// ─────────────────────────────────────────────
// BinOpLazyNum — Binary operation
// ─────────────────────────────────────────────

public class BinOpLazyNum : LazyNum
{
	private readonly LazyNum _left;
	private readonly LazyNum _right;
	private readonly Func<int, int, int> _op;

	public BinOpLazyNum(LazyNum left, LazyNum right, Func<int, int, int> op)
	{
		_left = left;
		_right = right;
		_op = op;
	}

	public override int Value(BaseEntity source)
	{
		return ApplyBase(_op(_left.Value(source), _right.Value(source)));
	}
}

// ─────────────────────────────────────────────
// Count — count entities from a selector
// ─────────────────────────────────────────────

public class Count : LazyNum
{
	private readonly Selector _selector;

	public Count(Selector selector) => _selector = selector;

	public override int Value(BaseEntity source)
	{
		var entities = _selector.Eval(source.GetGame()?.Entities ?? new List<BaseEntity>(), source);
		return ApplyBase(entities.Count);
	}
}

public class SummonedThisGameCount : LazyNum
{
	private readonly Race? _race;

	public SummonedThisGameCount(Race? race = null)
	{
		_race = race;
	}

	public override int Value(BaseEntity source)
	{
		var controller = (source as BuffableEntity)?.Controller;
		if (controller == null) return 0;

		var count = controller.SummonedMinionsThisGame.Count(entity =>
			_race == null || entity.CardData?.Races.Contains(_race.Value) == true);
		return ApplyBase(count);
	}
}

/// <summary>
/// 计算“为了让当前事件目标保留指定生命值，还应造成多少伤害”。
/// 仅用于 PreDamageEvent 处理中改写当前伤害量。
/// </summary>
public class DamageToLeaveTargetAtHealthValue : LazyNum
{
	private readonly int _health;

	public DamageToLeaveTargetAtHealthValue(int health)
	{
		_health = health;
	}

	public override int Value(BaseEntity source)
	{
		if (source.EventTarget is not Character target) return 0;
		return ApplyBase(Math.Max(0, target.Health - _health));
	}
}

// ─────────────────────────────────────────────
// Attr — Sum an attribute over a selector
// ─────────────────────────────────────────────

public class Attr : LazyNum
{
	private readonly Selector _selector;
	private readonly AttrValue _attr;

	public Attr(Selector selector, AttrValue attr)
	{
		_selector = selector;
		_attr = attr;
	}

	// Convenience constructor for GameTag
	public Attr(Selector selector, GameTag tag) : this(selector, new AttrValue(tag)) { }

	public override int Value(BaseEntity source)
	{
		var entities = _selector.Eval(source.GetGame()?.Entities ?? new List<BaseEntity>(), source);
		int sum = 0;
		foreach (var e in entities)
		{
			sum += _attr.Value(e, source);
		}
		return ApplyBase(sum);
	}
}

// ─────────────────────────────────────────────
// OpAttr — Aggregate attribute with custom function (Min, Max, etc.)
// ─────────────────────────────────────────────

public class OpAttr : LazyNum
{
	private readonly Selector _selector;
	private readonly AttrValue _attr;
	private readonly Func<IEnumerable<int>, int> _aggregator;

	public OpAttr(Selector selector, AttrValue attr, Func<IEnumerable<int>, int> aggregator)
	{
		_selector = selector;
		_attr = attr;
		_aggregator = aggregator;
	}

	public override int Value(BaseEntity source)
	{
		var entities = _selector.Eval(source.GetGame()?.Entities ?? new List<BaseEntity>(), source);
		if (entities.Count == 0) return 0; // Default to 0 if empty

		var values = entities.Select(e => _attr.Value(e, source));
		return ApplyBase(_aggregator(values));
	}
}

// ─────────────────────────────────────────────
// Min / Max — Aggregate multiple LazyNums
// ─────────────────────────────────────────────

public class Max : LazyNum
{
	private readonly LazyNum[] _nums;
	public Max(params LazyNum[] nums) => _nums = nums;

	public override int Value(BaseEntity source)
	{
		if (_nums.Length == 0) return 0;
		return ApplyBase(_nums.Max(n => n.Value(source)));
	}
}

public class Min : LazyNum
{
	private readonly LazyNum[] _nums;
	public Min(params LazyNum[] nums) => _nums = nums;

	public override int Value(BaseEntity source)
	{
		if (_nums.Length == 0) return 0;
		return ApplyBase(_nums.Min(n => n.Value(source)));
	}
}

// ─────────────────────────────────────────────
// RandomAmount — Random number
// ─────────────────────────────────────────────

public class RandomAmount : LazyNum
{
	private readonly int _min;
	private readonly int _max;
	private static readonly Random _rng = new();

	public RandomAmount(int min, int max)
	{
		_min = min;
		_max = max;
	}

	public override int Value(BaseEntity source)
	{
		return ApplyBase(_rng.Next(_min, _max + 1));
	}
}

// ─────────────────────────────────────────────
// Extensions
// ─────────────────────────────────────────────

internal static class EntityExtensions
{
	public static Game? GetGame(this BaseEntity entity)
	{
		return entity.Game;
	}
}
