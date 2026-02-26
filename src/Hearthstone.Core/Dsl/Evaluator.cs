using HearthstoneServer.Entities;

namespace HearthstoneServer.Dsl;

// ─────────────────────────────────────────────
// Evaluator — base class for runtime conditions
// ─────────────────────────────────────────────

/// <summary>
/// Lazily evaluate a condition at runtime.
/// Supports if-then-else via &amp; (then) and | (else), and negation via -.
/// </summary>
public abstract class Evaluator
{
	/// <summary>Action(s) to execute when condition is true.</summary>
	public object? IfAction { get; private set; }

	/// <summary>Action(s) to execute when condition is false.</summary>
	public object? ElseAction { get; private set; }

	/// <summary>Whether this evaluator is negated.</summary>
	public bool Negated { get; private set; }

	/// <summary>Check the condition.</summary>
	public abstract bool Check(BaseEntity source);

	/// <summary>
	/// Evaluate the condition and return the appropriate action.
	/// Returns IfAction when true, ElseAction when false, or null.
	/// </summary>
	public object? Evaluate(BaseEntity source)
	{
		var result = Check(source);
		if (Negated) result = !result;

		if (result)
			return IfAction;
		else
			return ElseAction;
	}

	// ── Operator-like methods (C# can't overload & and | on classes easily) ──

	/// <summary>Set the "then" action: evaluator.Then(action)</summary>
	public Evaluator Then(object action)
	{
		var copy = (Evaluator)MemberwiseClone();
		copy.IfAction = action;
		return copy;
	}

	/// <summary>Set the "else" action: evaluator.Else(action)</summary>
	public Evaluator Else(object action)
	{
		var copy = (Evaluator)MemberwiseClone();
		copy.ElseAction = action;
		return copy;
	}

	/// <summary>Negate: -evaluator</summary>
	public static Evaluator operator -(Evaluator e)
	{
		var copy = (Evaluator)e.MemberwiseClone();
		copy.Negated = !e.Negated;
		return copy;
	}
}

// ─────────────────────────────────────────────
// AlwaysTrue — condition that always passes
// ─────────────────────────────────────────────

/// <summary>
/// 永真条件。用于 .IfThen(ALWAYS, ...) 表示"无条件执行一次"。
/// </summary>
public class AlwaysTrue : Evaluator
{
	public override bool Check(BaseEntity source) => true;
}

// ─────────────────────────────────────────────
// Find — true if selector matches at least N entities (default 1)
// ─────────────────────────────────────────────

public class Find : Evaluator
{
	private readonly Selector _selector;
	private readonly int _count;

	public Find(Selector selector, int count = 1)
	{
		_selector = selector;
		_count = count;
	}

	public override bool Check(BaseEntity source)
	{
		var entities = _selector.Eval(
			source.Game?.Entities ?? new List<BaseEntity>(), source);
		return entities.Count >= _count;
	}

	public override string ToString() => _count == 1
		? $"Find({_selector})"
		: $"Find({_selector}, {_count})";
}

// ─────────────────────────────────────────────
// FindAll — true if ALL selectors have matches
// ─────────────────────────────────────────────

public class FindAll : Evaluator
{
	private readonly Selector[] _selectors;

	public FindAll(params Selector[] selectors) => _selectors = selectors;

	public override bool Check(BaseEntity source)
	{
		var all = source.Game?.Entities ?? new List<BaseEntity>();
		foreach (var sel in _selectors)
		{
			if (sel.Eval(all, source).Count == 0)
				return false;
		}
		return true;
	}
}

// ─────────────────────────────────────────────
// FindDuplicates — true if selector has duplicate card IDs
// ─────────────────────────────────────────────

public class FindDuplicates : Evaluator
{
	private readonly Selector _selector;

	public FindDuplicates(Selector selector) => _selector = selector;

	public override bool Check(BaseEntity source)
	{
		var entities = _selector.Eval(
			source.Game?.Entities ?? new List<BaseEntity>(), source);
		var ids = entities.Select(e => e.Data?.CardId ?? "").ToList();
		return ids.Distinct().Count() < ids.Count;
	}
}

// ─────────────────────────────────────────────
// Dead — true if any target in selector is dead (health <= 0)
// ─────────────────────────────────────────────

public class Dead : Evaluator
{
	private readonly Selector _selector;

	public Dead(Selector selector) => _selector = selector;

	public override bool Check(BaseEntity source)
	{
		var entities = _selector.Eval(
			source.Game?.Entities ?? new List<BaseEntity>(), source);
		foreach (var e in entities)
		{
			if (e is BuffableEntity be && be.GetIntProperty("health") <= 0)
				return true;
		}
		return false;
	}
}

// ─────────────────────────────────────────────
// Attacking — true if selector1 entity is attacking selector2 entity
// ─────────────────────────────────────────────

public class Attacking : Evaluator
{
	private readonly Selector _selector1;
	private readonly Selector _selector2;

	public Attacking(Selector selector1, Selector selector2)
	{
		_selector1 = selector1;
		_selector2 = selector2;
	}

	public override bool Check(BaseEntity source)
	{
		var all = source.Game?.Entities ?? new List<BaseEntity>();
		var t1 = _selector1.Eval(all, source);
		var t2 = _selector2.Eval(all, source);
		foreach (var e in t1)
		{
			if (e is Character ch && ch.Attacking)
			{
				// Check if the attack target is in t2
				if (ch.AttackTarget != null && t2.Contains(ch.AttackTarget))
					return true;
			}
		}
		return false;
	}
}

// ─────────────────────────────────────────────
// CurrentPlayer — true if selector entities are controlled by current player
// ─────────────────────────────────────────────

public class CurrentPlayer : Evaluator
{
	private readonly Selector _selector;

	public CurrentPlayer(Selector selector) => _selector = selector;

	public override bool Check(BaseEntity source)
	{
		var all = source.Game?.Entities ?? new List<BaseEntity>();
		var targets = _selector.Eval(all, source);
		foreach (var e in targets)
		{
			if (e is BuffableEntity be && be.Controller?.IsCurrentPlayer == true)
				return true;
		}
		return false;
	}
}

// ─────────────────────────────────────────────
// ChooseBoth — true if selector evaluates to a player with choose_both
// ─────────────────────────────────────────────

public class ChooseBoth : Evaluator
{
	private readonly Selector _selector;

	public ChooseBoth(Selector selector) => _selector = selector;

	public override bool Check(BaseEntity source)
	{
		var all = source.Game?.Entities ?? new List<BaseEntity>();
		var targets = _selector.Eval(all, source);
		if (targets.Count == 0) return false;
		return targets[0] is BuffableEntity be && be.GetBoolProperty("choose_both");
	}
}

// ─────────────────────────────────────────────
// EvenCost / OddCost — check parity of card costs
// ─────────────────────────────────────────────

public class EvenCost : Evaluator
{
	private readonly Selector _selector;

	public EvenCost(Selector selector) => _selector = selector;

	public override bool Check(BaseEntity source)
	{
		var entities = _selector.Eval(
			source.Game?.Entities ?? new List<BaseEntity>(), source);
		foreach (var e in entities)
		{
			if (e is BuffableEntity be && be.GetIntProperty("cost") % 2 != 0)
				return false;
		}
		return true;
	}
}

public class OddCost : Evaluator
{
	private readonly Selector _selector;

	public OddCost(Selector selector) => _selector = selector;

	public override bool Check(BaseEntity source)
	{
		var entities = _selector.Eval(
			source.Game?.Entities ?? new List<BaseEntity>(), source);
		foreach (var e in entities)
		{
			if (e is BuffableEntity be && be.GetIntProperty("cost") % 2 != 1)
				return false;
		}
		return true;
	}
}

// ─────────────────────────────────────────────
// Lethal — true if amount damage would kill all selector targets
// ─────────────────────────────────────────────

public class Lethal : Evaluator
{
	private readonly Selector _selector;
	private readonly LazyNum _amount;

	public Lethal(Selector selector, LazyNum amount)
	{
		_selector = selector;
		_amount = amount;
	}

	public override bool Check(BaseEntity source)
	{
		var entities = _selector.Eval(
			source.Game?.Entities ?? new List<BaseEntity>(), source);
		int amount = _amount.Value(source);
		foreach (var e in entities)
		{
			if (e is BuffableEntity be)
			{
				int health = be.GetIntProperty("health");
				if (e.Type == CardType.Hero)
					health += be.GetIntProperty("armor");
				if (health > amount)
					return false;
			}
		}
		return true;
	}
}

// ─────────────────────────────────────────────
// JoustEvaluator — compare costs of two selectors
// ─────────────────────────────────────────────

public class JoustEvaluator : Evaluator
{
	private readonly Selector _selector1;
	private readonly Selector _selector2;

	public JoustEvaluator(Selector selector1, Selector selector2)
	{
		_selector1 = selector1;
		_selector2 = selector2;
	}

	public override bool Check(BaseEntity source)
	{
		// Simplified: compare first entity costs
		var all = source.Game?.Entities ?? new List<BaseEntity>();
		var c1 = _selector1.Eval(all, source).FirstOrDefault();
		var c2 = _selector2.Eval(all, source).FirstOrDefault();
		if (c1 == null) return false;
		if (c2 == null) return true;
		int cost1 = c1 is BuffableEntity be1 ? be1.GetIntProperty("cost") : 0;
		int cost2 = c2 is BuffableEntity be2 ? be2.GetIntProperty("cost") : 0;
		return cost1 > cost2;
	}
}

// ─────────────────────────────────────────────
// HasTarget — true if source has a target set
// ─────────────────────────────────────────────

public class HasTarget : Evaluator
{
	public override bool Check(BaseEntity source)
	{
		return source.CurrentTarget != null;
	}
}

// ─────────────────────────────────────────────
// LazyNumEvaluator — bridge LazyNum comparison to Evaluator
// ─────────────────────────────────────────────

/// <summary>
/// Compares a LazyNum's evaluated result against another value.
/// Created by LazyNum comparison operators (==, &gt;, &lt;, etc.).
/// </summary>
public class LazyNumEvaluator : Evaluator
{
	private readonly LazyNum _num;
	private readonly object _other; // int or LazyNum
	private readonly Func<int, int, bool> _cmp;

	public LazyNumEvaluator(LazyNum num, object other, Func<int, int, bool> cmp)
	{
		_num = num;
		_other = other;
		_cmp = cmp;
	}

	public override bool Check(BaseEntity source)
	{
		int numVal = _num.Value(source);
		int otherVal = _other switch
		{
			int i => i,
			LazyNum ln => ln.Value(source),
			_ => 0
		};
		return _cmp(numVal, otherVal);
	}
}

// ─────────────────────────────────────────────
// Predefined evaluator constants
// ─────────────────────────────────────────────

public static class Evaluators
{
	public static readonly Evaluator HAS_TARGET = new HasTarget();
}

// ─────────────────────────────────────────────
// OpponentManaEmpty — true if opponent has 0 remaining mana
// ─────────────────────────────────────────────

/// <summary>对手剩余法力 == 0（已用光全部法力水晶）。</summary>
public class OpponentManaEmpty : Evaluator
{
	public override bool Check(BaseEntity source)
	{
		var controller = (source as BuffableEntity)?.Controller;
		if (controller == null) return false;
		var game = controller.Game;
		if (game == null) return false;
		var opponent = game.Players.FirstOrDefault(p => p != controller);
		return opponent != null &&
			(opponent.MaxMana - opponent.UsedMana + opponent.TempMana - opponent.OverloadLocked) == 0;
	}
}

/// <summary>选择器结果 >= minCount 时为真。</summary>
public class MinCount : Evaluator
{
	private readonly Selector _selector;
	private readonly int _min;

	public MinCount(Selector selector, int min)
	{
		_selector = selector;
		_min = min;
	}

	public override bool Check(BaseEntity source)
	{
		var entities = _selector.Eval(
			source.Game?.Entities ?? new List<BaseEntity>(), source);
		return entities.Count >= _min;
	}

	public override string ToString() => $"MinCount({_selector}, {_min})";
}

/// <summary>取反任意 Evaluator。</summary>
public class NotEvaluator : Evaluator
{
	private readonly Evaluator _inner;
	public NotEvaluator(Evaluator inner) => _inner = inner;
	public override bool Check(BaseEntity source) => !_inner.Check(source);
	public override string ToString() => $"Not({_inner})";

	/// <summary>工厂方法：Not(evaluator)。</summary>
	public static NotEvaluator Not(Evaluator eval) => new(eval);
}
