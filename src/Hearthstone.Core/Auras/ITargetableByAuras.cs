namespace HearthstoneServer.Auras;

/// <summary>
/// 可被光环作用的实体接口。
/// 实现 refresh_buff 和 refresh_tags 方法，由 Refresh.Trigger() 调用。
/// </summary>
public interface ITargetableByAuras
{
    /// <summary>Buff 列表</summary>
    List<Entities.BuffableEntity> Buffs { get; }

    /// <summary>Slot 列表</summary>
    List<AuraBuff> Slots { get; }

    /// <summary>
    /// 刷新来自指定来源的 Buff。
    /// 如果已存在来自同一来源且同 id 的 buff，仅更新 tick；
    /// 否则创建新 buff。
    /// </summary>
    void RefreshBuff(Entities.BuffableEntity source, string id, int gameTick)
    {
        foreach (var buff in Buffs)
        {
            // 在完整实现中，buff 应具有 source/id 属性
            // 此处简化为通过 AuraBuff 匹配
        }
        // 完整实现在 Card 层 — 需要 source.Buff(self, id) 方法
    }

    /// <summary>
    /// 刷新来自指定来源的 Tags。
    /// 如果已存在来自同一来源的 AuraBuff slot，更新其 tags；
    /// 否则创建新的 AuraBuff。
    /// </summary>
    void RefreshTags(Entities.BuffableEntity source, Dictionary<GameTag, int> tags, int gameTick)
    {
        // 查找现有 slot
        foreach (var slot in Slots)
        {
            if (slot.Source == source)
            {
                slot.UpdateTags(tags);
                slot.Tick = gameTick;
                return;
            }
        }

        // 创建新 AuraBuff
        if (source is Entities.BuffableEntity be && this is Entities.BuffableEntity self)
        {
            var auraBuff = new AuraBuff(be, self);
            auraBuff.UpdateTags(tags);
            auraBuff.Tick = gameTick;
            Slots.Add(auraBuff);
            // game.active_aura_buffs.Add(auraBuff) — Game 层实现时完成
        }
    }
}
