using System.Linq;
using HearthstoneServer.Entities;

namespace HearthstoneServer.Dsl;

// ─────────────────────────────────────────────
// Selector base class
// ─────────────────────────────────────────────

/// <summary>
/// Selectors take entity lists and return a sub-list.
/// Closed under intersection (+), union (|), and difference (-).
/// </summary>
public class Selector
{
	/// <summary>
	/// Evaluate this selector against a list of entities, from the
	/// perspective of <paramref name="source"/>.
	/// </summary>
	public virtual List<BaseEntity> Eval(List<BaseEntity> entities, BaseEntity source)
		=> entities;

	// ── Set operators ──

	public static Selector operator +(Selector a, Selector b)
		=> new SetOpSelector(SetOp.Intersect, a, b);

	public static Selector operator |(Selector a, Selector b)
		=> new SetOpSelector(SetOp.Union, a, b);

	public static Selector operator -(Selector a, Selector b)
		=> new SetOpSelector(SetOp.Subtract, a, b);

	/// <summary>Complement: -sel  ≡  All - sel</summary>
	public static Selector operator -(Selector a)
		=> new Selector() - a;
}

// ─────────────────────────────────────────────
// SetOp enum
// ─────────────────────────────────────────────

public enum SetOp { Intersect, Union, Subtract }

// ─────────────────────────────────────────────
// SetOpSelector — combines two selectors with a set operation
// ─────────────────────────────────────────────

/// <summary>
/// Combines two selectors using set operations.
/// Preserves ordering from the input entity list.
/// </summary>
public class SetOpSelector : Selector
{
	private readonly SetOp _op;
	private readonly Selector _left;
	private readonly Selector _right;

	public SetOpSelector(SetOp op, Selector left, Selector right)
	{
		_op = op;
		_left = left;
		_right = right;
	}

	public override List<BaseEntity> Eval(List<BaseEntity> entities, BaseEntity source)
	{
		var leftIds = new HashSet<int>(_left.Eval(entities, source).Select(e => e.EntityId));
		var rightIds = new HashSet<int>(_right.Eval(entities, source).Select(e => e.EntityId));

		HashSet<int> resultIds = _op switch
		{
			SetOp.Intersect => new(leftIds.Where(id => rightIds.Contains(id))),
			SetOp.Union => new(leftIds.Union(rightIds)),
			SetOp.Subtract => new(leftIds.Where(id => !rightIds.Contains(id))),
			_ => throw new ArgumentOutOfRangeException()
		};

		// Preserve input ordering and multiplicity
		return entities.Where(e => resultIds.Contains(e.EntityId)).ToList();
	}

	public override string ToString()
	{
		var opStr = _op switch
		{
			SetOp.Intersect => "+",
			SetOp.Union => "|",
			SetOp.Subtract => "-",
			_ => "?"
		};
		return $"<{_left} {opStr} {_right}>";
	}
}

// ─────────────────────────────────────────────
// EnumSelector — filter by GameTag / CardType / Race / Zone / Rarity / CardClass
// ─────────────────────────────────────────────

/// <summary>
/// Tests each entity against an enum value.
/// 
/// </summary>
public class EnumSelector : Selector
{
	private readonly Enum _tag;

	public EnumSelector(Enum tag) => _tag = tag;

	public override List<BaseEntity> Eval(List<BaseEntity> entities, BaseEntity source)
	{
		return entities.Where(e => Test(e)).ToList();
	}

	private bool Test(BaseEntity e)
	{
		return _tag switch
		{
			GameTag gt => e is BuffableEntity be && be.GetBoolProperty(gt),
			CardType ct => e.Type == ct,
			Race r => HasRace(e, r),
			Zone z => GetZone(e) == z,
			Rarity ra => GetRarity(e) == ra,
			CardClass cc => GetCardClass(e) == cc,
			_ => false
		};
	}

	private static bool HasRace(BaseEntity e, Race r)
	{
		if (e is Entities.Character ch)
			return ch.Races.Contains(r);
		if (e is BuffableEntity be)
			return be.CardData?.Races.Contains(r) == true;
		var races = e.Data?.Races;
		return races != null && races.Contains(r);
	}

	private static Zone GetZone(BaseEntity e)
	{
		if (e is BuffableEntity be)
			return be.Zone;
		return Zone.Invalid;
	}

	private static Rarity GetRarity(BaseEntity e)
	{
		return e.Data?.Rarity ?? Rarity.Invalid;
	}

	private static CardClass GetCardClass(BaseEntity e)
	{
		return e.Data?.CardClass ?? CardClass.Invalid;
	}

	public override string ToString() => $"<{_tag}>";
}

// ─────────────────────────────────────────────
// FilterSelector — arbitrary predicate filter
// ─────────────────────────────────────────────

/// <summary>
/// Filters entities using a predicate: (entity, source) → bool.
/// </summary>
public class FilterSelector : Selector
{
	private readonly Func<BaseEntity, BaseEntity, bool> _predicate;

	public FilterSelector(Func<BaseEntity, BaseEntity, bool> predicate)
		=> _predicate = predicate;

	public override List<BaseEntity> Eval(List<BaseEntity> entities, BaseEntity source)
		=> entities.Where(e => _predicate(e, source)).ToList();
}

// ─────────────────────────────────────────────
// FuncSelector — arbitrary transformation
// ─────────────────────────────────────────────

/// <summary>
/// Full control over evaluation: (entities, source) → result list.
/// </summary>
public class FuncSelector : Selector
{
	private readonly Func<List<BaseEntity>, BaseEntity, List<BaseEntity>> _func;

	public FuncSelector(Func<List<BaseEntity>, BaseEntity, List<BaseEntity>> func)
		=> _func = func;

	public override List<BaseEntity> Eval(List<BaseEntity> entities, BaseEntity source)
		=> _func(entities, source);

	/// <summary>Convenience factory for simple lambdas.</summary>
	public static FuncSelector Create(Func<List<BaseEntity>, BaseEntity, List<BaseEntity>> func)
		=> new(func);
}

// ─────────────────────────────────────────────
// SliceSelector — take a sub-range of child results
// ─────────────────────────────────────────────

/// <summary>
/// Takes a slice (skip/take) of the child selector results.
/// </summary>
public class SliceSelector : Selector
{
	private readonly Selector _child;
	private readonly int _skip;
	private readonly int _take;

	public SliceSelector(Selector child, int skip, int take)
	{
		_child = child;
		_skip = skip;
		_take = take;
	}

	/// <summary>Take first N elements.</summary>
	public SliceSelector(Selector child, int take) : this(child, 0, take) { }

	public override List<BaseEntity> Eval(List<BaseEntity> entities, BaseEntity source)
		=> _child.Eval(entities, source).Skip(_skip).Take(_take).ToList();
}

// ─────────────────────────────────────────────
// RandomSelector — random sample from child results
// ─────────────────────────────────────────────

/// <summary>
/// Selects a random sample of <see cref="Times"/> entities from child.
/// </summary>
public class RandomSelector : Selector
{
	private readonly Selector _child;
	public int Times { get; }
	private readonly bool _distinctByType;

	private static readonly Random _rng = new();

	public RandomSelector(Selector child, int times = 1, bool distinctByType = false)
	{
		_child = child;
		Times = times;
		_distinctByType = distinctByType;
	}

	public override List<BaseEntity> Eval(List<BaseEntity> entities, BaseEntity source)
	{
		var pool = _child.Eval(entities, source);

		if (_distinctByType)
			return PickDistinctByType(pool);

		var count = Math.Min(pool.Count, Times);
		// Fisher-Yates partial shuffle
		for (int i = 0; i < count; i++)
		{
			int j = _rng.Next(i, pool.Count);
			(pool[i], pool[j]) = (pool[j], pool[i]);
		}
		return pool.Take(count).ToList();
	}

	private List<BaseEntity> PickDistinctByType(List<BaseEntity> pool)
	{
		// 按种族分组，每组随机选一个，再随机取最多 Times 组
		var groups = pool
			.Where(e => e is Entities.Character ch && ch.Races.Length > 0)
			.GroupBy(e => ((Entities.Character)e).Races[0])
			.ToList();

		// Fisher-Yates shuffle on groups
		for (int i = groups.Count - 1; i > 0; i--)
		{
			int j = _rng.Next(0, i + 1);
			(groups[i], groups[j]) = (groups[j], groups[i]);
		}

		var result = new List<BaseEntity>();
		foreach (var g in groups.Take(Times))
		{
			var list = g.ToList();
			result.Add(list[_rng.Next(list.Count)]);
		}
		return result;
	}

	/// <summary>Create a new RandomSelector with multiplied times.</summary>
	public RandomSelector Multiply(int factor) => new(_child, Times * factor, _distinctByType);
}

// ─────────────────────────────────────────────
// BoardPositionSelector — LEFT_OF / RIGHT_OF
// ─────────────────────────────────────────────

public enum BoardDirection { Left, Right }

/// <summary>
/// Selects minions adjacent to the child selector's results on the board.
/// NOTE: Full board-position logic requires the field/zone list, which will
///       be implemented when integrating with the game state. Current stub
///       returns empty — tests can override via FuncSelector.
/// </summary>
public class BoardPositionSelector : Selector
{
	private readonly BoardDirection _direction;
	private readonly Selector _child;

	public BoardPositionSelector(BoardDirection direction, Selector child)
	{
		_direction = direction;
		_child = child;
	}

	public override List<BaseEntity> Eval(List<BaseEntity> entities, BaseEntity source)
	{
		var childResults = _child.Eval(entities, source);
		var result = new List<BaseEntity>();

		foreach (var entity in childResults)
		{
			if (entity is not BuffableEntity be || be.Controller == null) continue;
			var field = be.Controller.Field;
			int idx = field.IndexOf(be);
			if (idx < 0) continue;

			int neighborIdx = _direction == BoardDirection.Left ? idx - 1 : idx + 1;
			if (neighborIdx >= 0 && neighborIdx < field.Count)
				result.Add(field[neighborIdx]);
		}

		return result;
	}
}

// ─────────────────────────────────────────────
// ComparisonSelector — filter by comparing entity attribute values
// ─────────────────────────────────────────────

/// <summary>
/// Compares an <see cref="AttrValue"/> against a fixed or lazy value.
/// </summary>
public class ComparisonSelector : Selector
{
	private readonly Func<int, int, bool> _op;
	private readonly AttrValue _left;
	private readonly object _right; // int, AttrValue, or (later) LazyValue

	public ComparisonSelector(Func<int, int, bool> op, AttrValue left, object right)
	{
		_op = op;
		_left = left;
		_right = right;
	}

	public override List<BaseEntity> Eval(List<BaseEntity> entities, BaseEntity source)
	{
		int ResolveRight(BaseEntity e)
		{
			return _right switch
			{
				int i => i,
				AttrValue av => av.Value(e, source),
				_ => 0
			};
		}

		return entities.Where(e => _op(_left.Value(e, source), ResolveRight(e))).ToList();
	}
}

// ─────────────────────────────────────────────
// AttrValue — extracts a numeric attribute from an entity
// ─────────────────────────────────────────────

/// <summary>
/// Extracts an attribute value from an entity, allowing comparison
/// operators to produce <see cref="ComparisonSelector"/>s.
/// </summary>
public class AttrValue
{
	private readonly object _tag; // GameTag or string

	public AttrValue(GameTag tag) => _tag = tag;
	public AttrValue(string propertyName) => _tag = propertyName;

	/// <summary>Get the numeric value of this attribute on <paramref name="entity"/>.</summary>
	public int Value(BaseEntity entity, BaseEntity source)
	{
		if (_tag is string prop)
		{
			return entity is BuffableEntity be ? be.GetIntProperty(prop) : 0;
		}
		if (_tag is GameTag gt)
		{
			// First try the mapped property name
			var name = Managers.CardManager.GetPropertyName(gt);
			if (name != null && entity is BuffableEntity be2)
				return be2.GetIntProperty(name);
			// Fallback: raw tag in Tags manager
			if (entity.Tags != null && entity.Tags.Contains(gt))
				return entity.Tags[gt];
		}
		return 0;
	}

	// ── Comparison operators produce ComparisonSelectors ──

	public static ComparisonSelector operator ==(AttrValue left, int right)
		=> new((a, b) => a == b, left, right);
	public static ComparisonSelector operator !=(AttrValue left, int right)
		=> new((a, b) => a != b, left, right);
	public static ComparisonSelector operator >(AttrValue left, int right)
		=> new((a, b) => a > b, left, right);
	public static ComparisonSelector operator <(AttrValue left, int right)
		=> new((a, b) => a < b, left, right);
	public static ComparisonSelector operator >=(AttrValue left, int right)
		=> new((a, b) => a >= b, left, right);
	public static ComparisonSelector operator <=(AttrValue left, int right)
		=> new((a, b) => a <= b, left, right);

	// ── Compare with another AttrValue ──

	public static ComparisonSelector operator ==(AttrValue left, AttrValue right)
		=> new((a, b) => a == b, left, right);
	public static ComparisonSelector operator !=(AttrValue left, AttrValue right)
		=> new((a, b) => a != b, left, right);

	// ── Compare with an object (for Controller, Opponent etc.) ──

	public ComparisonSelector Eq(object right)
		=> new((a, b) => a == b, this, right);

	public override bool Equals(object? obj) => ReferenceEquals(this, obj);
	public override int GetHashCode() => _tag.GetHashCode();

	public override string ToString()
	{
		return _tag switch
		{
			GameTag gt => $"<{gt}>",
			string s => $"<{s}>",
			_ => "<AttrValue>"
		};
	}
}

// ─────────────────────────────────────────────
// DeDuplicate — remove duplicate entities
// ─────────────────────────────────────────────

/// <summary>
/// Removes duplicate entities by EntityId from child results.
/// </summary>
public class DeDuplicate : Selector
{
	private readonly Selector _child;

	public DeDuplicate(Selector child) => _child = child;

	public override List<BaseEntity> Eval(List<BaseEntity> entities, BaseEntity source)
	{
		var seen = new HashSet<int>();
		var result = new List<BaseEntity>();
		foreach (var e in _child.Eval(entities, source))
		{
			if (seen.Add(e.EntityId))
				result.Add(e);
		}
		return result;
	}
}
