using Serilog;
using HearthstoneServer.Entities;

namespace HearthstoneServer.Dsl;

// ═══════════════════════════════════════════════════════════
// Phase 11 — Copy DSL
// KeepMagneticCopy, RebornCopy
// ═══════════════════════════════════════════════════════════

// ───────────────────────────────────────
// Copy — lazily return a list of copies of entities selected by a selector
// ───────────────────────────────────────

/// <summary>
/// Lazily return a list of copies of the target entities.
/// The selector picks entities, then each is copied via the controller's CreateCard.
/// </summary>
public class Copy : LazyValue
{
	private static readonly ILogger _log = LogConfig.ForType<Copy>();

	protected readonly Selector _selector;

	public Copy(Selector selector)
	{
		_selector = selector;
	}

	/// <summary>
	/// Create a copy of the given entity, controlled by source's controller.
	/// Subclasses override this to add buffs/tags.
	/// </summary>
	public virtual BuffableEntity? CopyEntity(BaseEntity source, BaseEntity entity)
	{
		_log.Information("Creating a copy of {Entity}", entity);

		var controller = (source as BuffableEntity)?.Controller;
		if (controller == null) return null;

		var entityBe = entity as BuffableEntity;
		var cardId = entityBe?.CardId;
		if (cardId == null) return null;

		var newEntity = controller.CreateCard(cardId);
		return newEntity;
	}

	public override object Evaluate(BaseEntity source)
	{
		var entities = _selector.Eval(
			source.GetGame()?.Entities ?? new List<BaseEntity>(), source);

		var copies = new List<BuffableEntity>();
		foreach (var e in entities)
		{
			var copy = CopyEntity(source, e);
			if (copy != null) copies.Add(copy);
		}
		return copies;
	}

	public override string ToString() => $"Copy({_selector})";
}

// ───────────────────────────────────────
// ExactCopy — copy including buffs and silenceable attributes
// ───────────────────────────────────────

/// <summary>
/// Lazily create an exact copy of the target.
/// An exact copy includes buffs and all tags.
/// </summary>
public class ExactCopy : Copy
{
	private static readonly ILogger _exactLog = LogConfig.ForType<ExactCopy>();

	private readonly string? _overrideId;

	public ExactCopy(Selector selector, string? overrideId = null) : base(selector)
	{
		_overrideId = overrideId;
	}

	public override BuffableEntity? CopyEntity(BaseEntity source, BaseEntity entity)
	{
		var baseCopy = base.CopyEntity(source, entity);
		if (baseCopy == null) return null;

		var controller = (source as BuffableEntity)?.Controller;
		if (controller == null) return baseCopy;

		// If an override ID is specified, create that card instead
		var ret = baseCopy;
		if (_overrideId != null)
		{
			ret = controller.CreateCard(_overrideId);
			if (ret == null) return baseCopy;
		}

		// Copy buffs from the original entity
		if (entity is BuffableEntity entityBe)
		{
			foreach (var buff in entityBe.Buffs)
			{
				if (buff is Enchantment enchBuff)
				{
					var buffCardId = enchBuff.CardId ?? enchBuff.BuffId;
					if (buffCardId == null) continue;

					var newBuff = controller.CreateCard(buffCardId);
					if (newBuff is Enchantment newEnch)
					{
						// Copy buff attributes
						CopyBuffAttributes(enchBuff, newEnch);
						newEnch.Apply(ret);

						// Maintain aura buff tracking
						if (source.Game != null &&
							source.Game.ActiveAuraBuffs.Any(b => b.Entity == enchBuff))
						{
							newEnch.AuraTick = enchBuff.AuraTick;
							source.Game.ActiveAuraBuffs.Add(
								new Auras.AuraBuff(newEnch, ret));
						}
					}
				}
			}

			// For minions: copy silenceable state
			if (entity is Minion minionOriginal && ret is Minion minionCopy)
			{
				CopyMinionState(minionOriginal, minionCopy);
			}
		}

		return ret;
	}

	/// <summary>Copy key attributes from one enchantment to another.</summary>
	private static void CopyBuffAttributes(Enchantment from, Enchantment to)
	{
		// Copy tag-based attributes
		if (from.Tags != null && to.Tags != null)
		{
			foreach (var tag in new[] { GameTag.Atk, GameTag.Health })
			{
				var val = from.Tags[tag];
				if (val != 0) to.Tags[tag] = val;
			}
		}

		// Note: AdditionalDeathrattles not yet in C# model — will be added when needed
	}

	/// <summary>Copy minion-specific silenceable state.</summary>
	private static void CopyMinionState(Minion from, Minion to)
	{
		to.Silenced = from.Silenced;
		// Copy damage via tag
		if (from.Tags != null && to.Tags != null)
		{
			to.Tags[GameTag.Damage] = from.Tags[GameTag.Damage];
		}
		// Note: AdditionalDeathrattles not yet in C# model — will be added when needed
	}
}

// ───────────────────────────────────────
// KeepMagneticCopy — copy keeping Magnetic upgrades
// ───────────────────────────────────────

/// <summary>
/// Copy that keeps Magnetic upgrades from the original entity.
/// Used by Kangor's Endless Army.
/// </summary>
public class KeepMagneticCopy : Copy
{
	private readonly string? _overrideId;

	public KeepMagneticCopy(Selector selector, string? overrideId = null)
		: base(selector)
	{
		_overrideId = overrideId;
	}

	public override BuffableEntity? CopyEntity(BaseEntity source, BaseEntity entity)
	{
		var baseCopy = base.CopyEntity(source, entity);
		if (baseCopy == null) return null;

		var controller = (source as BuffableEntity)?.Controller;
		if (controller == null) return baseCopy;

		var ret = baseCopy;
		if (_overrideId != null)
		{
			ret = controller.CreateCard(_overrideId) ?? baseCopy;
		}

		// Re-apply only Magnetic buffs
		if (entity is BuffableEntity entityBe)
		{
			foreach (var buff in entityBe.Buffs)
			{
				if (buff is Enchantment enchBuff && enchBuff.SourceCard != null)
				{
					// Check if the source card has Magnetic
					var hasMagnetic = enchBuff.SourceCard.GetBoolProperty(GameTag.Magnetic);
					if (hasMagnetic)
					{
						// Re-apply magnetic buff via Enchantment
						var atk = enchBuff.Tags?[GameTag.Atk] ?? 0;
						var health = enchBuff.Tags?[GameTag.Health] ?? 0;
						var buffCardId = enchBuff.CardId ?? enchBuff.BuffId;
						if (buffCardId != null)
						{
							var newBuff = controller.CreateCard(buffCardId);
							if (newBuff is Enchantment magneticEnch)
							{
								if (magneticEnch.Tags != null)
								{
									magneticEnch.Tags[GameTag.Atk] = atk;
									magneticEnch.Tags[GameTag.Health] = health;
								}
								magneticEnch.Apply(ret);
							}
						}
					}
				}
			}
		}

		return ret;
	}
}

// ───────────────────────────────────────
// RebornCopy — copy with Reborn removed and health set to 1
// ───────────────────────────────────────

/// <summary>
/// Copy that removes Reborn and sets current health to 1.
/// Used when a Reborn minion dies and comes back.
/// </summary>
public class RebornCopy : Copy
{
	public RebornCopy(Selector selector) : base(selector) { }

	public override BuffableEntity? CopyEntity(BaseEntity source, BaseEntity entity)
	{
		var ret = base.CopyEntity(source, entity);
		if (ret == null) return null;

		// Remove Reborn and set health to 1
		if (ret is Minion minion)
		{
			minion.Reborn = false;
			// Set current health to 1 (full health minus damage)
			if (minion.Tags != null)
			{
				var maxHealth = minion.Tags[GameTag.Health];
				if (maxHealth > 1)
				{
					minion.Tags[GameTag.Damage] = maxHealth - 1;
				}
			}
		}

		return ret;
	}
}
