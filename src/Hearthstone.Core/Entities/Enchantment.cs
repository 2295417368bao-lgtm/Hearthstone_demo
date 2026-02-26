using Serilog;

namespace HearthstoneServer.Entities;

// ═══════════════════════════════════════════════════
// Enchantment — a buff/debuff attached to an entity
// ═══════════════════════════════════════════════════

/// <summary>
/// An enchantment (buff/debuff) that is applied to an entity and modifies its attributes.
/// When applied, the enchantment adds itself to the owner's Buffs list.
/// When removed, it removes itself and adjusts damage to prevent health overflow.
/// 附魔效果实体。
/// </summary>
public class Enchantment : BuffableEntity
{
	private static readonly ILogger _log = LogConfig.ForType<Enchantment>();

	public Enchantment()
	{
		Type = CardType.Enchantment;
	}

	/// <summary>The entity this enchantment is applied to (the buff target).</summary>
	public BuffableEntity? Owner { get; set; }

	/// <summary>The entity that created this enchantment (the buff source).</summary>
	public BuffableEntity? Source { get; set; }

	/// <summary>光环 buff 的来源实体（用于 refresh_buff 匹配）。</summary>
	public BuffableEntity? SourceCard { get; set; }

	/// <summary>光环 buff 的标识 ID（用于 refresh_buff 匹配）。</summary>
	public string? BuffId { get; set; }

	/// <summary>光环刷新 tick（用于检测过时的 aura buff）。</summary>
	public int AuraTick { get; set; }

	/// <summary>Whether this is a one-turn effect (removed at end of turn).</summary>
	public bool OneTurnEffect
	{
		get => Duration == 1;
		set { if (value) Duration = 1; }
	}

	/// <summary>
	/// 持续回合数（0 = 永久）。从卡牌数据的 QuestProgressTotal tag 自动读取。
	/// 每回合结束时 TurnsActive++，达到 Duration 时自动移除。
	/// </summary>
	public int Duration { get; set; }

	/// <summary>已存活的回合数。</summary>
	public int TurnsActive { get; set; }

	/// <summary>Card stored by this enchantment (e.g. StoringBuff).
	public BaseEntity? StoreCard { get; set; }

	/// <summary>Cost modifier function from enchantment script (e.g. SET(1)).</summary>
	public Func<BaseEntity, int, int>? CostModFunc { get; set; }

	/// <summary>Attack modifier function from enchantment script (e.g. SET(1) for Humility).</summary>
	public Func<BaseEntity, int, int>? AtkMod { get; set; }

	/// <summary>Max health modifier function from enchantment script (e.g. SetTo(1) for Equality).</summary>
	public Func<BaseEntity, int, int>? MaxHealthMod { get; set; }

	/// <summary>Durability modifier function.</summary>
	public Func<BaseEntity, int, int>? DurabilityMod { get; set; }

	/// <summary>Armor modifier function.</summary>
	public Func<BaseEntity, int, int>? ArmorMod { get; set; }

	/// <summary>Whether this enchantment has a deathrattle.</summary>
	public override bool HasDeathrattle
	{
		get => GetBoolProperty("has_deathrattle");
		set => SetBoolProperty("has_deathrattle", value);
	}

	// ── Apply / Remove ──

	/// <summary>
	/// Apply this enchantment to a target entity.
	/// Adds this buff to the target's Buffs list and sets Zone to Play.
	/// </summary>
	public void Apply(BuffableEntity target)
	{
		_log.Debug("Applying {Enchantment} to {Target}", EntityId, target.EntityId);
		Owner = target;
		target.Buffs.Add(this);
		Zone = Zone.Play;
	}

	/// <summary>
	/// Remove this enchantment from its owner.
	/// Adjusts damage to prevent health overflow when a health buff is removed.
	/// </summary>
	public void Remove()
	{
		if (Owner == null) return;

		_log.Debug("Removing {Enchantment} from {Owner}", EntityId, Owner.EntityId);

		// If the owner is a Character, handle health adjustment
		if (Owner is Character character)
		{
			int oldHealth = character.Health;
			Owner.Buffs.Remove(this);
			// If removing a health buff would lower health below current,
			// adjust damage to prevent overflow (.
			if (character.Health < oldHealth)
			{
				int excess = oldHealth - character.Health;
				character.DamageTaken = Math.Max(character.DamageTaken - excess, 0);
			}
		}
		else
		{
			Owner.Buffs.Remove(this);
		}

		Zone = Zone.RemovedFromGame;
	}

	/// <summary>Dump enchantment state for serialization.</summary>
	public override Dictionary<string, object> Dump()
	{
		var data = base.Dump();
		data["owner"] = Owner?.EntityId ?? 0;
		data["source"] = Source?.EntityId ?? 0;
		data["one_turn_effect"] = OneTurnEffect;
		return data;
	}
}
