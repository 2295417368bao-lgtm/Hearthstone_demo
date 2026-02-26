using HearthstoneServer.Dsl;
using HearthstoneServer.Entities;

namespace HearthstoneServer.Auras;

/// <summary>
/// 光环刷新指令。
/// 遍历 selector 匹配的目标，对每个 entity 执行 RefreshBuff 或 RefreshTags。
/// 在 Game.RefreshAuras() 中被调用（通过 entity.update_scripts）。
/// </summary>
public class Refresh
{
	/// <summary>目标选择器（决定哪些 entity 受光环影响）</summary>
	public Selector Selector { get; }

	/// <summary>要刷新的 tag 增量值（加法修改器，与 Buff/SetTags 互不排斥）</summary>
	public Dictionary<GameTag, object>? Tags { get; }

	/// <summary>要设置的 tag 绝对值（SET 修改器，如 SET cost=1）</summary>
	public Dictionary<GameTag, object>? SetTags { get; }

	/// <summary>要刷新的 buff id（与 Tags 二选一）</summary>
	public string? Buff { get; }

	/// <summary>执行优先级（数字越小越先执行，默认 50）</summary>
	public int Priority { get; }

	/// <summary>可选条件——为 false 时跳过此光环</summary>
	public Evaluator? Condition { get; }

	public Refresh(Selector selector, Dictionary<GameTag, object>? tags = null,
		string? buff = null, int priority = 50, Evaluator? condition = null,
		Dictionary<GameTag, object>? setTags = null)
	{
		Selector = selector;
		Tags = tags;
		Buff = buff;
		Priority = priority;
		Condition = condition;
		SetTags = setTags;
	}

	/// <summary>简写：单个 tag 增量修改。</summary>
	public Refresh(Selector selector, GameTag tag, int value, Evaluator? condition = null)
		: this(selector, new Dictionary<GameTag, object> { { tag, value } }, condition: condition) { }

	/// <summary>隐式转换：单个 Refresh → Refresh[]。</summary>
	public static implicit operator Refresh[](Refresh single) => new[] { single };

	/// <summary>
	/// 触发 aura 刷新。遍历 selector 结果，对每个 entity 调用 RefreshBuff 或 RefreshTags。
	/// </summary>
	public void Trigger(BuffableEntity source)
	{
		var game = source.Game;
		if (game == null) return;

		// 如果有条件且为 false，跳过此光环
		if (Condition != null && !Condition.Check(source))
			return;

		var entities = Selector.Eval(game.Entities, source);
		foreach (var entity in entities)
		{
			if (entity is not BuffableEntity target) continue;

			if (Buff != null)
			{
				target.RefreshBuff(source, Buff);
			}
			else if (Tags != null)
			{
				// 解析 tags 中可能包含的 LazyValue
				var resolvedTags = ResolveTags(Tags, source);
				target.RefreshTags(source, resolvedTags);
			}

			// SET 绝对值模式：直接覆盖 tag 为指定值
			if (SetTags != null)
			{
				var resolved = ResolveTags(SetTags, source);
				foreach (var (tag, value) in resolved)
				{
					target.Tags[tag] = value;
				}
			}
		}
	}

	private static Dictionary<GameTag, int> ResolveTags(Dictionary<GameTag, object> raw, BuffableEntity source)
	{
		var resolved = new Dictionary<GameTag, int>();
		foreach (var (tag, value) in raw)
		{
			if (value is int intVal)
				resolved[tag] = intVal;
			else if (value is LazyValue lv)
			{
				var result = lv.Evaluate(source);
				resolved[tag] = result is int ri ? ri : 0;
			}
			else
				resolved[tag] = 0;
		}
		return resolved;
	}

	public override string ToString() =>
		$"Refresh({Selector}, {(Tags != null ? "tags" : "")}{(SetTags != null ? "set" : "")}{Buff ?? ""})";
}
