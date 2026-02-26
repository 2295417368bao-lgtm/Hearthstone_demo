namespace HearthstoneServer.Entities;

// ═══════════════════════════════════════════════════
// Spell — a spell card
// ═══════════════════════════════════════════════════

/// <summary>
/// A spell entity. Spells apply spell damage bonuses and are discarded after use.
/// </summary>
public class Spell : BuffableEntity
{
	public Spell()
	{
		Type = CardType.Spell;
	}

	/// <summary>Whether this spell is immune to spell power bonuses.</summary>
	public bool ImmuneToSpellpower { get; set; }

	/// <summary>Whether this spell receives double spell damage bonus.</summary>
	public bool ReceivesDoubleSpellDamageBonus { get; set; }

	/// <summary>Twinspell — creates a copy when played.</summary>
	public bool Twinspell
	{
		get => GetBoolProperty("twinspell");
		set => SetBoolProperty("twinspell", value);
	}

	/// <summary>法术学派（从 CardData 读取）。</summary>
	public SpellSchool SpellSchool => CardData?.SpellSchool ?? SpellSchool.None;

	/// <summary>The controller of this spell.</summary>
	public override Player? Controller { get; set; }

	/// <summary>Mana cost.</summary>
	public override int Cost
	{
		get => GetIntProperty("cost");
		set => SetIntProperty("cost", value);
	}

	/// <summary>
	/// Calculate spell damage amount, applying spell power bonuses.
	/// </summary>
	public int GetDamage(int amount)
	{
		if (Controller == null || ImmuneToSpellpower) return amount;
		return Controller.GetSpellDamage(amount);
	}

	/// <summary>
	/// Calculate spell heal amount, applying healing bonuses.
	/// </summary>
	public int GetHealAmount(int amount)
	{
		if (Controller == null) return amount;
		return Controller.GetSpellHeal(amount);
	}

	/// <summary>
	/// The card ID of the non-Twinspell copy to give to the player's hand.
	/// Set via CardData (TWINSPELL_COPY tag).
	/// </summary>
	public string? TwinspellCopyId { get; set; }

	// ── Play override ──

	/// <summary>
	/// Play this spell — 轻量钩子。SequenceRunner 处理 Twinspell、费用、效果。
	/// 仅保留 Zone 变换逻辑以供直接调用场景使用。
	/// </summary>
	public override void Play(Character? target = null, int index = -1)
	{
		base.Play(target, index);
		// Spells go to graveyard after playing
		SetZone(Zone.Graveyard);
	}
}
