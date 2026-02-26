namespace HearthstoneServer.Managers;

/// <summary>
/// 管理器基类。
/// 提供 GameTag → 属性名的映射、索引器访问和 Observer 通知。
/// </summary>
public abstract class Manager
{
    protected readonly Entities.BaseEntity Obj;
    private readonly List<IObserver> _observers = new();

    /// <summary>
    /// GameTag → 属性名映射表。子类必须提供。
    /// 值为 null 表示该 Tag 被识别但不映射到任何属性。
    /// </summary>
    protected abstract Dictionary<GameTag, string?> Map { get; }

    protected Manager(Entities.BaseEntity obj)
    {
        Obj = obj;
    }

    // ── 索引器 ──

    /// <summary>
    /// 通过 GameTag 获取对应属性值。
    /// 如果映射存在且为 BuffableEntity，则使用 GetIntProperty。
    /// 否则返回 0。
    /// </summary>
    public int this[GameTag tag]
    {
        get
        {
            if (Map.TryGetValue(tag, out var attr) && attr != null && Obj is Entities.BuffableEntity be)
                return be.GetIntProperty(attr);
            return 0;
        }
        set
        {
            if (Map.TryGetValue(tag, out var attr) && attr != null && Obj is Entities.BuffableEntity be)
                be.SetIntProperty(attr, value);
        }
    }

    /// <summary>
    /// 尝试获取 Tag 值，如不在 Map 中返回 default。
    /// </summary>
    public int Get(GameTag tag, int defaultValue = 0)
    {
        return Map.ContainsKey(tag) ? this[tag] : defaultValue;
    }

    /// <summary>
    /// 检查 Tag 是否在映射表中。
    /// </summary>
    public bool Contains(GameTag tag) => Map.ContainsKey(tag);

    /// <summary>
    /// 批量更新 Tags。
    /// </summary>
    public void Update(Dictionary<GameTag, int> tags)
    {
        foreach (var (k, v) in tags)
        {
            if (Map.TryGetValue(k, out var attr) && attr != null)
                this[k] = v;
        }
    }

    /// <summary>
    /// 枚举所有已映射的 Tag 及其值。
    /// </summary>
    public IEnumerable<(GameTag Tag, int Value)> Items()
    {
        foreach (var (k, v) in Map)
        {
            if (v != null)
                yield return (k, this[k]);
        }
    }

    // ── Observer ──

    public void Register(IObserver observer) => _observers.Add(observer);
    public void Unregister(IObserver observer) => _observers.Remove(observer);
    protected IReadOnlyList<IObserver> Observers => _observers;
}
