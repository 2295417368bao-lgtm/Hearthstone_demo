using HearthstoneServer;
using HearthstoneServer.Auras;
using HearthstoneServer.Entities;

namespace Hearthstone.Tests;

/// <summary>
/// Aura 系统测试�?
/// 覆盖：AuraBuff 创建/更新/移除/聚合，ITargetableByAuras.RefreshTags，Refresh 结构�?
/// </summary>
public class AuraTests
{
	// ── AuraBuff 基本 ──

	[Fact]
	public void AuraBuff_UpdateTags_StoresValues()
	{
		var source = new Entity();
		var target = new Entity();
		var buff = new AuraBuff(source, target);

		buff.UpdateTags(new Dictionary<GameTag, int>
		{
			[GameTag.Atk] = 2,
			[GameTag.Health] = 1,  // Health �?"health" in CardManager
		});

		Assert.Equal(2, buff.AggregateAttr("atk", 0));
		Assert.Equal(1, buff.AggregateAttr("health", 0));
	}

	[Fact]
	public void AuraBuff_AggregateAttr_AddsToCurrentValue()
	{
		var source = new Entity();
		var target = new Entity();
		var buff = new AuraBuff(source, target);

		buff.UpdateTags(new Dictionary<GameTag, int> { [GameTag.Atk] = 3 });

		// 当前�?5 + buff �?3 = 8
		Assert.Equal(8, buff.AggregateAttr("atk", 5));
	}

	[Fact]
	public void AuraBuff_AggregateAttr_ReturnsCurrentWhenNoMatch()
	{
		var source = new Entity();
		var target = new Entity();
		var buff = new AuraBuff(source, target);

		// buff 没有 "atk" 属�?
		Assert.Equal(5, buff.AggregateAttr("atk", 5));
	}

	[Fact]
	public void AuraBuff_HasBoolAttr_TrueForNonZero()
	{
		var source = new Entity();
		var target = new Entity();
		var buff = new AuraBuff(source, target);

		buff.UpdateTags(new Dictionary<GameTag, int> { [GameTag.Taunt] = 1 });

		Assert.True(buff.HasBoolAttr("taunt"));
	}

	[Fact]
	public void AuraBuff_HasBoolAttr_FalseForZero()
	{
		var source = new Entity();
		var target = new Entity();
		var buff = new AuraBuff(source, target);

		buff.UpdateTags(new Dictionary<GameTag, int> { [GameTag.Taunt] = 0 });

		Assert.False(buff.HasBoolAttr("taunt"));
	}

	[Fact]
	public void AuraBuff_HasBoolAttr_FalseWhenNotPresent()
	{
		var source = new Entity();
		var target = new Entity();
		var buff = new AuraBuff(source, target);

		Assert.False(buff.HasBoolAttr("taunt"));
	}

	// ── AuraBuff.Remove ──

	[Fact]
	public void AuraBuff_Remove_RemovesFromTargetSlots()
	{
		var source = new Entity();
		var target = new Entity();
		var buff = new AuraBuff(source, target);
		target.Slots.Add(buff);
		Assert.Single(target.Slots);

		buff.Remove();
		Assert.Empty(target.Slots);
	}

	// ── AuraBuff Tick 追踪 ──

	[Fact]
	public void AuraBuff_Tick_TrackingWorks()
	{
		var source = new Entity();
		var target = new Entity();
		var buff = new AuraBuff(source, target);

		buff.Tick = 42;
		Assert.Equal(42, buff.Tick);
	}

	// ── AuraBuff Source/Entity 引用 ──

	[Fact]
	public void AuraBuff_HoldsCorrectSourceAndEntity()
	{
		var source = new Entity();
		var target = new Entity();
		var buff = new AuraBuff(source, target);

		Assert.Same(source, buff.Source);
		Assert.Same(target, buff.Entity);
	}

	// ── ITargetableByAuras.RefreshTags ──

	[Fact]
	public void RefreshTags_CreatesNewSlot_WhenNoneExists()
	{
		var source = new Entity();
		var target = new TestTargetable();

		var tags = new Dictionary<GameTag, int> { [GameTag.Atk] = 2 };
		((ITargetableByAuras)target).RefreshTags(source, tags, gameTick: 10);

		Assert.Single(target.Slots);
		Assert.Equal(10, target.Slots[0].Tick);
		Assert.Same(source, target.Slots[0].Source);
	}

	[Fact]
	public void RefreshTags_UpdatesExistingSlot_WhenSameSource()
	{
		var source = new Entity();
		var target = new TestTargetable();

		var tags1 = new Dictionary<GameTag, int> { [GameTag.Atk] = 2 };
		((ITargetableByAuras)target).RefreshTags(source, tags1, gameTick: 10);

		var tags2 = new Dictionary<GameTag, int> { [GameTag.Atk] = 5 };
		((ITargetableByAuras)target).RefreshTags(source, tags2, gameTick: 20);

		// 应该还是只有一�?slot，不是两�?
		Assert.Single(target.Slots);
		Assert.Equal(20, target.Slots[0].Tick);
		// atk 应被更新�?5
		Assert.Equal(5, target.Slots[0].AggregateAttr("atk", 0));
	}

	[Fact]
	public void RefreshTags_CreatesMultipleSlots_ForDifferentSources()
	{
		var source1 = new Entity();
		var source2 = new Entity();
		var target = new TestTargetable();

		((ITargetableByAuras)target).RefreshTags(source1, new Dictionary<GameTag, int> { [GameTag.Atk] = 1 }, 10);
		((ITargetableByAuras)target).RefreshTags(source2, new Dictionary<GameTag, int> { [GameTag.Atk] = 2 }, 10);

		Assert.Equal(2, target.Slots.Count);
	}

	// ── Refresh 结构 ──

	[Fact]
	public void Refresh_StoresConstructorArgs()
	{
		var tags = new Dictionary<GameTag, object> { [GameTag.Atk] = 1 };
		var selector = HearthstoneServer.Dsl.S.SELF;
		var refresh = new Refresh(selector: selector, tags: tags, buff: "test_buff", priority: 75);

		Assert.Same(selector, refresh.Selector);
		Assert.Same(tags, refresh.Tags);
		Assert.Equal("test_buff", refresh.Buff);
		Assert.Equal(75, refresh.Priority);
	}

	[Fact]
	public void Refresh_DefaultPriorityIs50()
	{
		var refresh = new Refresh(selector: HearthstoneServer.Dsl.S.SELF);
		Assert.Equal(50, refresh.Priority);
	}

	// ── 完整光环流程：Slot 影响 Entity 属�?──

	[Fact]
	public void FullAuraFlow_SlotModifiesEntityProperty()
	{
		var source = new Entity();
		var target = new Entity();
		target.SetIntProperty("atk", 3);

		// 模拟光环：对 target 施加 +2 atk
		var auraBuff = new AuraBuff(source, target);
		auraBuff.UpdateTags(new Dictionary<GameTag, int> { [GameTag.Atk] = 2 });
		target.Slots.Add(auraBuff);

		Assert.Equal(5, target.GetIntProperty("atk"));

		// 移除光环
		auraBuff.Remove();
		Assert.Equal(3, target.GetIntProperty("atk"));
	}

	[Fact]
	public void FullAuraFlow_SlotGrantsBoolProperty()
	{
		var source = new Entity();
		var target = new Entity();
		Assert.False(target.GetSlotProperty("taunt"));

		var auraBuff = new AuraBuff(source, target);
		auraBuff.UpdateTags(new Dictionary<GameTag, int> { [GameTag.Taunt] = 1 });
		target.Slots.Add(auraBuff);

		Assert.True(target.GetSlotProperty("taunt"));

		auraBuff.Remove();
		Assert.False(target.GetSlotProperty("taunt"));
	}

	// ── Helper: 实现 ITargetableByAuras 的测试实�?──

	private class TestTargetable : Entity, ITargetableByAuras
	{
		// Entity 已有 Buffs �?Slots，直接满足接�?

		// 显式声明以匹配接口中�?List<BuffableEntity> Buffs
		List<BuffableEntity> ITargetableByAuras.Buffs => Buffs;
		List<AuraBuff> ITargetableByAuras.Slots => Slots;
	}
}
