using Serilog;

namespace HearthstoneServer.Entities;

// ═══════════════════════════════════════════════════
// Weapon — an equipped weapon
// ═══════════════════════════════════════════════════

/// <summary>
/// A weapon entity. Weapons have attack and durability, and lose durability
/// when the hero attacks.
/// </summary>
public class Weapon : BuffableEntity
{
	private static readonly ILogger _log = LogConfig.ForType<Weapon>();

	public Weapon()
	{
		Type = CardType.Weapon;
	}

	/// <summary>Weapon attack power (added to hero's attack).</summary>
	public int Atk
	{
		get
		{
			int atk = GetField("atk");
			foreach (var buff in Buffs)
			{
				atk = buff.AggregateAttr("atk", atk);
				if (buff is Enchantment ench && ench.AtkMod != null)
					atk = ench.AtkMod(this, atk);
			}
			foreach (var slot in Slots)
				atk = slot.AggregateAttr("atk", atk);
			return Math.Max(0, atk);
		}
		set => SetIntProperty("atk", value);
	}

	/// <summary>Maximum durability.</summary>
	public int MaxDurability
	{
		get
		{
			int dur = GetField("durability");
			foreach (var buff in Buffs)
			{
				dur = buff.AggregateAttr("durability", dur);
				if (buff is Enchantment ench && ench.DurabilityMod != null)
					dur = ench.DurabilityMod(this, dur);
			}
			foreach (var slot in Slots)
				dur = slot.AggregateAttr("durability", dur);
			return Math.Max(0, dur);
		}
		set => SetIntProperty("durability", value);
	}

	/// <summary>Damage taken (lost durability).</summary>
	public int DamageTaken
	{
		get => GetIntProperty("damage");
		set => SetIntProperty("damage", value);
	}

	/// <summary>Current durability = MaxDurability - DamageTaken. Can go negative (e.g. Bulwark of Azzinoth).</summary>
	public int Durability => MaxDurability - DamageTaken;

	/// <summary>True if the weapon is broken (durability ≤ 0).</summary>
	public bool Dead => Durability <= 0;

	/// <summary>The controller of this weapon.</summary>
	public override Player? Controller { get; set; }

	/// <summary>Lose one durability (called after hero attacks).</summary>
	public void LoseDurability(int amount = 1)
	{
		DamageTaken += amount;
	}



	public bool Windfury
	{
		get => GetBoolProperty("windfury");
		set => SetBoolProperty("windfury", value);
	}

	public bool Lifesteal
	{
		get => GetBoolProperty("lifesteal");
		set => SetBoolProperty("lifesteal", value);
	}

	public bool Poisonous
	{
		get => GetBoolProperty("poisonous");
		set => SetBoolProperty("poisonous", value);
	}

	// ── Zone management (.

	/// <summary>
	/// 装备武器：先销毁旧武器，再设为英雄的装备。
	/// </summary>
	public void Equip()
	{
		if (Controller?.Hero == null) return;

		// 销毁旧武器
		var oldWeapon = Controller.Hero.EquippedWeapon;
		if (oldWeapon != null)
		{
			_log.Debug("Destroying old weapon {OldWeapon}", oldWeapon.EntityId);
			oldWeapon.Destroy();
		}

		Controller.Hero.EquippedWeapon = this;
		SetZone(Zone.Play);
		_log.Debug("Equipped weapon {Weapon} (ATK={Atk}, Durability={Dur})",
			EntityId, Atk, Durability);
	}

	/// <summary>
	/// 销毁武器：移至墓地，清除英雄武器引用。
	/// </summary>
	public void Destroy()
	{
		if (Controller?.Hero != null && Controller.Hero.EquippedWeapon == this)
		{
			Controller.Hero.EquippedWeapon = null;
		}
		SetZone(Zone.Graveyard);
		_log.Debug("Weapon {Weapon} destroyed", EntityId);
	}

	/// <summary>
	/// 打出武器 — 轻量钩子。SequenceRunner 处理费用支付。
	/// 仅保留装备逻辑以供直接调用场景使用。
	/// </summary>
	public override void Play(Character? target = null, int index = -1)
	{
		base.Play(target, index);
		Equip();
	}

	/// <summary>
	/// Override SetZone: 离开 Play 时清除英雄武器引用。
	/// </summary>
	public override void SetZone(Zone zone, Player? controller = null)
	{
		if (Zone == Zone.Play && zone != Zone.Play)
		{
			// 离开 Play → 清除英雄的武器引用
			if (Controller?.Hero != null && Controller.Hero.EquippedWeapon == this)
			{
				Controller.Hero.EquippedWeapon = null;
			}
		}
		base.SetZone(zone, controller);
	}

	public override Dictionary<string, object> Dump()
	{
		var data = base.Dump();
		data["atk"] = Atk;
		data["durability"] = Durability;
		data["max_durability"] = MaxDurability;
		return data;
	}
}
