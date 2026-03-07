using HearthstoneServer.Entities;

namespace HearthstoneServer.Dsl;

// ═══════════════════════════════════════════════════
// Action system — base framework
// ═══════════════════════════════════════════════════



// ─────────────────────────────────────────────
// ActionArg — lazy argument resolved at trigger time
// ─────────────────────────────────────────────

/// <summary>
/// A placeholder for an action argument that gets resolved from event_args at trigger time.
/// </summary>
public class ActionArg : LazyValue
{
	public int Index { get; internal set; }
	public string Name { get; internal set; } = "";

	public override object Evaluate(BaseEntity source)
	{
		// Resolve from event_args at trigger time
		var args = source.EventArgs;
		if (args != null && Index >= 0 && Index < args.Length)
			return args[Index];
		return 0;
	}
}

/// <summary>
/// Integer-typed action argument.
/// </summary>
public class IntArg : ActionArg
{
	public int AsInt(object value)
	{
		return value switch
		{
			int i => i,
			LazyNum ln => ln.Value(null!),
			_ => 0
		};
	}
}

/// <summary>
/// Card-typed action argument.
/// </summary>
public class CardArg : ActionArg { }

// ─────────────────────────────────────────────
// Action — base class for all game actions
// ─────────────────────────────────────────────

/// <summary>
/// Base class for all game actions.
/// Actions are the fundamental unit of game state mutation.
/// </summary>
public abstract class Action
{
	protected object[] Args { get; set; } = Array.Empty<object>();
	public Action[] Callback { get; set; } = Array.Empty<Action>();
	public Evaluator? CallbackCondition { get; set; }
	public int Times { get; set; } = 1;

	protected Action() { }

	protected Action(params object[] args)
	{
		Args = args;
	}



	/// <summary>
	/// Create a callback chain: action.Then(followup_actions)
	/// </summary>
	public Action Then(params Action[] actions)
	{
		var clone = (Action)MemberwiseClone();
		clone.Callback = actions;
		clone.CallbackCondition = null;
		return clone;
	}

	/// <summary>
	/// Create a conditional callback chain: action.IfThen(KILLED, followup_actions)
	/// Callbacks only fire when the evaluator returns true.
	/// </summary>
	public Action IfThen(Evaluator when, params Action[] actions)
	{
		var clone = (Action)MemberwiseClone();
		clone.Callback = actions;
		clone.CallbackCondition = when;
		return clone;
	}

	/// <summary>
	/// Get the resolved arguments for this action.
	/// </summary>
	public virtual object[] GetArgs(BaseEntity source) => Args;

	/// <summary>
	/// Check if this action's trigger pattern matches the given args.
	/// Used by EventListener to determine if an event should fire.
	/// </summary>
	public virtual bool Matches(BaseEntity entity, BaseEntity source, object[] args)
	{
		for (int i = 0; i < args.Length && i < Args.Length; i++)
		{
			var match = Args[i];
			if (match == null) continue; // null = wildcard

			var arg = args[i];
			if (arg == null) return false;

			if (match is Selector sel)
			{
				var result = sel.Eval(new List<BaseEntity> { (BaseEntity)arg }, entity);
				if (result.Count == 0 || result[0] != arg)
					return false;
			}
		}
		return true;
	}

	public override string ToString() => $"<Action: {GetType().Name}>";

	/// <summary>
	/// 将 Action 重复 N 次，返回 Action[]。
	/// 用法：new Summon(CONTROLLER, "X") * 3
	/// </summary>
	public static Action[] operator *(Action a, int n)
	{
		var result = new Action[n];
		for (int i = 0; i < n; i++)
			result[i] = a;
		return result;
	}

	/// <summary>
	/// 将 Action 重复 LazyNum 次（动态计数）。
	/// 用法：new Enchant(S.SELF, "e") * Count(S.FRIENDLY_MINIONS)
	/// </summary>
	public static Action[] operator *(Action a, LazyNum count)
	{
		return new Action[] { new RepeatedAction(a, count) };
	}

	/// <summary>隐式转换：单个 Action 自动变为 Action[1]。</summary>
	public static implicit operator Action[](Action a) => new[] { a };
}

/// <summary>
/// Wrapper that repeats an inner action a dynamic number of times (resolved at trigger).
/// </summary>
public class RepeatedAction : TargetedAction
{
	private readonly Action _inner;
	private readonly LazyNum _count;

	public RepeatedAction(Action inner, LazyNum count) : base()
	{
		_inner = inner;
		_count = count;
	}

	/// <summary>
	/// Override Trigger to bypass TargetedAction's GetTargets (which returns empty
	/// since RepeatedAction has no Args). Instead, directly repeat the inner action.
	/// </summary>
	public override List<object?> Trigger(BaseEntity source)
	{
		var ret = new List<object?>();
		int n = _count.Value(source);
		for (int i = 0; i < n; i++)
		{
			if (_inner is TargetedAction ta)
				ret.AddRange(ta.Trigger(source));
			else if (_inner is GameAction ga)
				ga.Trigger(source);
		}
		return ret;
	}

	public override object? Do(BaseEntity source, BaseEntity target, params object[] args)
		=> throw new InvalidOperationException("RepeatedAction.Do should not be called directly; use Trigger instead.");
}

// ─────────────────────────────────────────────
// GameAction — actions that operate on the game level
// ─────────────────────────────────────────────

/// <summary>
/// Actions that operate on the game level (BeginTurn, EndTurn, Attack, etc.).
/// </summary>
public abstract class GameAction : Action
{
	protected GameAction() { }
	protected GameAction(params object[] args) : base(args) { }

	/// <summary>
	/// Trigger the game action from the given source.
	/// </summary>
	public virtual void Trigger(BaseEntity source)
	{
		var args = GetArgs(source);
		Do(source, args);
	}

	/// <summary>
	/// Execute the action. Subclasses must implement this.
	/// </summary>
	public abstract void Do(BaseEntity source, params object[] args);
}

// ─────────────────────────────────────────────
// TargetedAction — actions that operate on specific targets
// ─────────────────────────────────────────────

/// <summary>
/// Actions that operate on specific entity targets (Hit, Heal, Buff, etc.).
/// The first argument is always the target selector.
/// </summary>
public abstract class TargetedAction : Action
{
	protected TargetedAction() { }
	protected TargetedAction(params object[] args) : base(args) { }

	/// <summary>
	/// Resolve the targets from the first argument.
	/// </summary>
	public virtual List<BaseEntity> GetTargets(BaseEntity source)
	{
		if (Args.Length == 0)
			return new List<BaseEntity>();

		var t = Args[0];
		return t switch
		{
			BaseEntity e => new List<BaseEntity> { e },
			Selector sel => sel.Eval(
				source.Game?.Entities ?? new List<BaseEntity>(), source),
			LazyValue lv => EvalLazyToEntityList(lv, source),
			_ => new List<BaseEntity>()
		};
	}

	/// <summary>
	/// Resolve additional arguments (beyond the target) for each target.
	/// </summary>
	public virtual object[] GetTargetArgs(BaseEntity source, BaseEntity target)
	{
		var result = new List<object>();
		for (int i = 1; i < Args.Length; i++)
		{
			var v = Args[i];
			if (v is Selector sel)
				result.Add(sel.Eval(
					source.Game?.Entities ?? new List<BaseEntity>(), source));
			else if (v is LazyValue lv)
				result.Add(lv.Evaluate(source));
			else
				result.Add(v);
		}
		return result.ToArray();
	}

	/// <summary>
	/// Trigger the targeted action from the given source.
	/// Returns a list of results (one per target).
	/// </summary>
	public virtual List<object?> Trigger(BaseEntity source)
	{
		var ret = new List<object?>();
		int times = Times;

		for (int i = 0; i < times; i++)
		{
			var targets = GetTargets(source);
			foreach (var target in targets)
			{
				var targetArgs = GetTargetArgs(source, target);
				var result = Do(source, target, targetArgs);
				ret.Add(result);
				TriggerCallback(source, result);
			}
		}
		return ret;
	}

	/// <summary>
	/// Execute on a single target. Subclasses must implement this.
	/// </summary>
	public abstract object? Do(BaseEntity source, BaseEntity target, params object[] args);

	/// <summary>
	/// Multiply the action: action * N = repeat N times.
	/// </summary>
	public TargetedAction Repeat(int n)
	{
		var clone = (TargetedAction)MemberwiseClone();
		clone.Times = n;
		return clone;
	}

	private static List<BaseEntity> EvalLazyToEntityList(LazyValue lv, BaseEntity source)
	{
		var result = lv.Evaluate(source);
		if (result is BaseEntity e) return new List<BaseEntity> { e };
		if (result is List<BaseEntity> list) return list;
		return new List<BaseEntity>();
	}

	private void TriggerCallback(BaseEntity source, object? result)
	{
		if (Callback.Length == 0) return;

		if (result is BaseEntity entity)
		{
			TriggerCallbackForEntity(source, entity);
			return;
		}

		if (result is IEnumerable<BaseEntity> entities)
		{
			foreach (var item in entities)
				TriggerCallbackForEntity(source, item);
		}
	}

	private void TriggerCallbackForEntity(BaseEntity source, BaseEntity entity)
	{
		source.CurrentTarget = entity;
		try
		{
			if (CallbackCondition != null && !CallbackCondition.Check(source))
				return;

			foreach (var cb in Callback)
			{
				if (cb is TargetedAction ta) ta.Trigger(source);
				else if (cb is GameAction ga) ga.Trigger(source);
			}
		}
		finally
		{
			source.CurrentTarget = null;
		}
	}
}
