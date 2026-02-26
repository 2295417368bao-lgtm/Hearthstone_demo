namespace HearthstoneServer.Dsl;

/// <summary>
/// 内联 Buff 脚本：由工厂方法 CardScriptUtils.Buff() 自动创建并注册。
/// 免去为每个 enchantment 写单独 class 的样板代码。
/// <para>
/// 用法：Buff("BT_142e", atk: 1)
/// </para>
/// </summary>
public class InlineBuff : CardScriptBase
{
	private readonly Dictionary<GameTag, object>? _tags;
	private readonly Func<Entities.BaseEntity, int, int>? _atkMod;
	private readonly Func<Entities.BaseEntity, int, int>? _maxHealthMod;
	private readonly Func<Entities.BaseEntity, int, int>? _costModFunc;

	public InlineBuff(
		int atk = 0,
		int health = 0,
		Func<Entities.BaseEntity, int, int>? atkMod = null,
		Func<Entities.BaseEntity, int, int>? maxHealthMod = null,
		Func<Entities.BaseEntity, int, int>? costModFunc = null,
		params (GameTag tag, object value)[] extraTags)
	{
		var tags = new Dictionary<GameTag, object>();
		if (atk != 0) tags[GameTag.Atk] = atk;
		if (health != 0) tags[GameTag.Health] = health;
		foreach (var (tag, value) in extraTags)
			tags[tag] = value;

		_tags = tags.Count > 0 ? tags : null;
		_atkMod = atkMod;
		_maxHealthMod = maxHealthMod;
		_costModFunc = costModFunc;
	}

	public override Dictionary<GameTag, object>? Tags => _tags;
	public override Func<Entities.BaseEntity, int, int>? AtkMod => _atkMod;
	public override Func<Entities.BaseEntity, int, int>? MaxHealthMod => _maxHealthMod;
	public override Func<Entities.BaseEntity, int, int>? CostModFunc => _costModFunc;
}
