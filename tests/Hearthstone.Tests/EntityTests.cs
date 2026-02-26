using HearthstoneServer;
using HearthstoneServer.Entities;
using HearthstoneServer.Managers;

namespace Hearthstone.Tests;

/// <summary>
/// BaseEntity + BuffableEntity + Entity 测试�?
/// 覆盖：属性聚合（int/bool/slot）、Buff/AuraBuff 叠加、伤�?治疗判断�?
/// </summary>
public class EntityTests
{
	// ── 基本属性读�?──

	[Fact]
	public void SetIntProperty_And_GetIntProperty_RoundTrips()
	{
		var entity = new Entity();
		entity.SetIntProperty("atk", 5);
		Assert.Equal(5, entity.GetIntProperty("atk"));
	}

	[Fact]
	public void GetIntProperty_DefaultsToZero_WhenNotSet()
	{
		var entity = new Entity();
		Assert.Equal(0, entity.GetIntProperty("atk"));
	}

	[Fact]
	public void GetIntProperty_NeverReturnsNegative()
	{
		// Python: max(0, result)
		var entity = new Entity();
		entity.SetIntProperty("atk", -3);
		Assert.Equal(0, entity.GetIntProperty("atk"));
	}

	[Fact]
	public void SetBoolProperty_And_GetBoolProperty_RoundTrips()
	{
		var entity = new Entity();
		entity.SetBoolProperty("taunt", true);
		Assert.True(entity.GetBoolProperty("taunt"));
	}

	[Fact]
	public void GetBoolProperty_DefaultsToFalse_WhenNotSet()
	{
		var entity = new Entity();
		Assert.False(entity.GetBoolProperty("taunt"));
	}

	// ── Buff 聚合 ──

	[Fact]
	public void IntProperty_AggregatesBuffValues()
	{
		var entity = new Entity();
		entity.SetIntProperty("atk", 3);

		// 添加一�?+2 atk �?buff
		var buff = new BuffableEntity();
		buff.SetIntProperty("atk", 2);
		entity.Buffs.Add(buff);

		Assert.Equal(5, entity.GetIntProperty("atk"));
	}

	[Fact]
	public void IntProperty_AggregatesMultipleBuffs()
	{
		var entity = new Entity();
		entity.SetIntProperty("atk", 1);

		var buff1 = new BuffableEntity();
		buff1.SetIntProperty("atk", 3);
		entity.Buffs.Add(buff1);

		var buff2 = new BuffableEntity();
		buff2.SetIntProperty("atk", 2);
		entity.Buffs.Add(buff2);

		// 1 + 3 + 2 = 6
		Assert.Equal(6, entity.GetIntProperty("atk"));
	}

	[Fact]
	public void BoolProperty_TrueIfAnyBuffIsTrue()
	{
		var entity = new Entity();
		Assert.False(entity.GetBoolProperty("taunt"));

		// buff 上设�?taunt = true，即使实体自身没有设
		var buff = new BuffableEntity();
		buff.SetBoolProperty("taunt", true);
		entity.Buffs.Add(buff);

		Assert.True(entity.GetBoolProperty("taunt"));
	}

	// ── AuraBuff (Slot) 聚合 ──

	[Fact]
	public void IntProperty_AggregatesSlotValues()
	{
		var entity = new Entity();
		entity.SetIntProperty("atk", 2);

		var source = new Entity();
		var slot = new HearthstoneServer.Auras.AuraBuff(source, entity);
		slot.UpdateTags(new Dictionary<GameTag, int> { [GameTag.Atk] = 3 });
		entity.Slots.Add(slot);

		Assert.Equal(5, entity.GetIntProperty("atk"));
	}

	[Fact]
	public void GetSlotProperty_ReturnsTrueWhenSlotHasAttr()
	{
		var entity = new Entity();
		var source = new Entity();
		var slot = new HearthstoneServer.Auras.AuraBuff(source, entity);
		// Taunt tag �?"taunt" 属性名
		slot.UpdateTags(new Dictionary<GameTag, int> { [GameTag.Taunt] = 1 });
		entity.Slots.Add(slot);

		Assert.True(entity.GetSlotProperty("taunt"));
	}

	[Fact]
	public void GetSlotProperty_ReturnsFalseWhenNoSlots()
	{
		var entity = new Entity();
		Assert.False(entity.GetSlotProperty("taunt"));
	}

	// ── ClearBuffs ──

	[Fact]
	public void ClearBuffs_RemovesAllBuffs()
	{
		var entity = new Entity();
		entity.SetIntProperty("atk", 1);

		var buff = new BuffableEntity();
		buff.SetIntProperty("atk", 5);
		entity.Buffs.Add(buff);

		Assert.Equal(6, entity.GetIntProperty("atk"));

		entity.ClearBuffs();
		Assert.Empty(entity.Buffs);
		Assert.Equal(1, entity.GetIntProperty("atk"));
	}

	// ── BaseEntity 伤害/治疗 ──

	[Fact]
	public void GetDamage_ReturnsZero_WhenTargetIsDormant()
	{
		var attacker = new Entity();
		var target = new Entity();
		target.SetBoolProperty("dormant", true);

		// Dormant 目标: 需要通过 GameTag 查找，先用字符串形式
		// GetDamage 内部检�?GetBoolProperty(GameTag.Dormant) �?"dormant"
		Assert.Equal(0, attacker.GetDamage(5, target));
	}

	[Fact]
	public void GetDamage_ReturnsZero_WhenTargetIsImmune()
	{
		var attacker = new Entity();
		var target = new Entity();
		// GameTag.Immune �?"immune" 已在 CardManager 中映�?
		target.SetBoolProperty("immune", true);

		Assert.Equal(0, attacker.GetDamage(5, target));
	}

	[Fact]
	public void GetDamage_ReturnsAmount_WhenTargetHasNoProtection()
	{
		var attacker = new Entity();
		var target = new Entity();

		Assert.Equal(5, attacker.GetDamage(5, target));
	}

	[Fact]
	public void GetHeal_ReturnsAmount()
	{
		var entity = new Entity();
		var target = new Entity();
		Assert.Equal(3, entity.GetHeal(3, target));
	}

	// ── EntityId / UUID ──

	[Fact]
	public void Entity_HasUniqueUuid()
	{
		var e1 = new Entity();
		var e2 = new Entity();
		Assert.NotEqual(e1.Uuid, e2.Uuid);
	}

	[Fact]
	public void IsCard_TrueForCardTypes()
	{
		var entity = new Entity();
		entity.Type = CardType.Minion;
		Assert.True(entity.IsCard);
	}

	[Fact]
	public void IsCard_FalseForPlayerType()
	{
		var entity = new Entity();
		entity.Type = CardType.Player;
		Assert.False(entity.IsCard);
	}

	// ── Dump ──

	[Fact]
	public void Dump_ContainsEntityIdAndType()
	{
		var entity = new Entity();
		entity.EntityId = 42;
		entity.Type = CardType.Minion;

		var dump = entity.Dump();
		Assert.Equal(42, dump["entity_id"]);
		Assert.Equal((int)CardType.Minion, dump["type"]);
	}
}
