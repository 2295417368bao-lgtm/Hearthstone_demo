using Serilog;

namespace HearthstoneServer.Entities;

// ═══════════════════════════════════════════════════
// Hero — the player's hero character
// ═══════════════════════════════════════════════════

/// <summary>
/// A hero entity. Heroes absorb damage through Armor first, then Health.
/// </summary>
public class Hero : Character
{
	private static readonly ILogger _log = LogConfig.ForType<Hero>();

	public Hero()
	{
		Type = CardType.Hero;
	}

	// ── Armor ──

	/// <summary>Current armor amount.</summary>
	public int Armor
	{
		get
		{
			int armor = GetField("armor");
			foreach (var buff in Buffs)
			{
				armor = buff.AggregateAttr("armor", armor);
				if (buff is Enchantment ench && ench.ArmorMod != null)
					armor = ench.ArmorMod(this, armor);
			}
			foreach (var slot in Slots)
				armor = slot.AggregateAttr("armor", armor);
			return Math.Max(0, armor);
		}
		set => SetIntProperty("armor", value);
	}

	// ── Equipment ──

	/// <summary>The hero's active hero power.</summary>
	public HeroPower? Power { get; set; }

	/// <summary>The hero's active weapon.</summary>
	public Weapon? EquippedWeapon { get; set; }

	// ── Hero-specific properties ──

	public bool Galakrond
	{
		get => GetBoolProperty("galakrond");
		set => SetBoolProperty("galakrond", value);
	}

	// ── Weapon attribute forwarding ──

	/// <summary>
	/// Hero ATK includes the equipped weapon's ATK.
	/// Weapon not exhausted = weapon's owner is the current player.
	/// </summary>
	public new int Atk
	{
		get
		{
			int baseAtk = GetIntProperty("atk");
			var w = EquippedWeapon;
			if (w != null && !IsWeaponExhausted(w))
				baseAtk += w.Atk;
			return baseAtk;
		}
		set => SetIntProperty("atk", value);
	}

	/// <summary>
	/// Windfury: inherited from weapon even when exhausted (Hearthstone patch 9786).
	/// </summary>
	public new bool Windfury
	{
		get
		{
			bool baseVal = GetBoolProperty("windfury");
			var w = EquippedWeapon;
			if (w != null) return w.Windfury || baseVal;
			return baseVal;
		}
		set => SetBoolProperty("windfury", value);
	}

	/// <summary>Lifesteal: inherited from weapon when not exhausted.</summary>
	public new bool Lifesteal
	{
		get
		{
			bool baseVal = GetBoolProperty("lifesteal");
			var w = EquippedWeapon;
			if (w != null && !IsWeaponExhausted(w)) return w.Lifesteal || baseVal;
			return baseVal;
		}
		set => SetBoolProperty("lifesteal", value);
	}

	/// <summary>Poisonous: inherited from weapon when not exhausted.</summary>
	public new bool Poisonous
	{
		get
		{
			bool baseVal = GetBoolProperty("poisonous");
			var w = EquippedWeapon;
			if (w != null && !IsWeaponExhausted(w)) return w.Poisonous || baseVal;
			return baseVal;
		}
		set => SetBoolProperty("poisonous", value);
	}

	/// <summary>
	/// A weapon is "exhausted" when it's in play but it's NOT the owner's turn.
	/// </summary>
	private bool IsWeaponExhausted(Weapon w)
	{
		return w.Zone == Zone.Play && Controller?.IsCurrentPlayer != true;
	}

	// ── Damage (armor absorbs first) ──

	/// <summary>
	/// Heroes absorb damage through armor first, then health.
	/// </summary>
	public override int Hit(int amount)
	{
		if (amount <= 0) return 0;
		if (CantBeDamaged || Immune) return 0;

		// Armor absorbs damage first
		int armorAbsorb = Math.Min(Armor, amount);
		Armor -= armorAbsorb;
		int remaining = amount - armorAbsorb;

		if (remaining > 0)
		{
			DamageTaken += remaining;
			DamagedThisTurn += remaining;
		}

		_log.Debug("Hero {EntityId} takes {Amount} damage ({ArmorAbsorbed} absorbed by armor, health now {Health})",
			EntityId, amount, armorAbsorb, Health);
		return amount;
	}

	// ── Health includes armor for display ──

	/// <summary>Effective health = HP + Armor.</summary>
	public int EffectiveHealth => Health + Armor;

	// ── Entity enumeration ──

	/// <summary>All sub-entities (hero power, weapon, buffs).</summary>
	public IEnumerable<BaseEntity> SubEntities
	{
		get
		{
			if (Power != null) yield return Power;
			if (EquippedWeapon != null) yield return EquippedWeapon;
			foreach (var buff in Buffs) yield return buff;
		}
	}

	// NOTE: Hero card play logic is handled by SequenceRunner.PlayHeroCard()
	// Hero.Play() is not used directly — DispatchToSequence routes to PlayHeroCard.


	// ── Dump ──

	public override Dictionary<string, object> Dump()
	{
		var data = base.Dump();
		data["armor"] = Armor;
		if (Power != null) data["hero_power"] = Power.Dump();
		if (EquippedWeapon != null) data["weapon"] = EquippedWeapon.Dump();
		return data;
	}
}
