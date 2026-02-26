namespace HearthstoneServer.Auras;

/// <summary>
/// 光环 Buff。
/// 由光环产生的临时 Slot，附着在目标实体上，影响其属性计算。
/// </summary>
public class AuraBuff
{
	/// <summary>光环的来源实体</summary>
	public Entities.BuffableEntity Source { get; }

	/// <summary>被影响的目标实体</summary>
	public Entities.BuffableEntity Entity { get; }

	/// <summary>上次更新的 Game Tick（用于检测过时的光环）</summary>
	public int Tick { get; set; }

	/// <summary>Buff 的标识 id（用于 refresh_buff 匹配）</summary>
	public string Id { get; set; } = "";

	/// <summary>
	/// 属性存储。key = 属性名（与 CardManager 映射一致），value = 修改值。
	/// </summary>
	private readonly Dictionary<string, int> _attrs = new(StringComparer.Ordinal);

	public AuraBuff(Entities.BuffableEntity source, Entities.BuffableEntity entity)
	{
		Source = source;
		Entity = entity;
	}

	/// <summary>
	/// 更新光环 Tags。
	/// 将 GameTag → int 映射转为属性名 → int 存储。
	/// </summary>
	public void UpdateTags(Dictionary<GameTag, int> tags)
	{
		foreach (var (tag, value) in tags)
		{
			var name = Managers.CardManager.GetPropertyName(tag);
			if (name != null)
				_attrs[name] = value;
		}
	}

	/// <summary>
	/// 移除此光环 Buff。从目标的 slots/buffs 和 game 的 active_aura_buffs 中同时移除。
	/// </summary>
	public void Remove()
	{
		Entity.Slots.Remove(this);

		// Also remove the corresponding Enchantment from Entity.Buffs
		// (created by RefreshBuff — aura buffs live in Buffs, not Slots)
		var toRemove = Entity.Buffs
			.OfType<Entities.Enchantment>()
			.Where(e => e.SourceCard == Source && e.BuffId == Id)
			.ToList();
		foreach (var ench in toRemove)
			Entity.Buffs.Remove(ench);

		Source.Game?.ActiveAuraBuffs.Remove(this);
	}

	/// <summary>
	/// 聚合属性值。
	/// </summary>
	public int AggregateAttr(string attr, int current)
	{
		if (_attrs.TryGetValue(attr, out var value))
			return current + value;
		return current;
	}

	/// <summary>
	/// 检查是否具有指定布尔属性。
	/// </summary>
	public bool HasBoolAttr(string attr)
	{
		return _attrs.TryGetValue(attr, out var v) && v != 0;
	}

	public override string ToString() => $"<AuraBuff {Source} -> {Entity}>";
}

