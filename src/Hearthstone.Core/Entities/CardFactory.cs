namespace HearthstoneServer.Entities;

// ═══════════════════════════════════════════════════
// CardFactory — create game entities from CardData
// ═══════════════════════════════════════════════════

/// <summary>
/// Static factory for creating game entity instances from card data.
/// </summary>
public static class CardFactory
{
	/// <summary>
	/// Create a BuffableEntity of the correct subtype based on CardType.
	/// Sets initial stats and applies ALL tags from the card data.
	/// Does NOT register the entity in a game or assign a controller.
	/// </summary>
	public static BuffableEntity Create(LoadCardIntoDbf.Card cardData)
	{
		BuffableEntity entity = cardData.CardType switch
		{
			CardType.Minion => new Minion { MaxHealth = cardData.Health, Atk = cardData.Attack },
			CardType.Spell => CreateSpellOrSecret(cardData),
			CardType.Weapon => new Weapon { MaxDurability = cardData.Health, Atk = cardData.Attack },
			CardType.Location => new Location { MaxDurability = cardData.Health },
			CardType.Hero => new Hero { MaxHealth = cardData.Health },
			CardType.HeroPower => new HeroPower(),
			_ => new BuffableEntity(),
		};

		entity.CardData = cardData;
		entity.Cost = cardData.Cost;

		// Apply ALL tags from card data via the Manager system.
		// Manager maps GameTag → property name, then calls SetIntProperty.
		// GetBoolProperty falls back to GetIntProperty > 0, so boolean
		// tags (Taunt, Stealth, DivineShield, etc.) work automatically.
		entity.EnsureTags();
		entity.Tags.Update(cardData.AllTagsInt);

		return entity;
	}

	/// <summary>
	/// Create a BuffableEntity from a card ID, looking up the card database.
	/// Returns null if the card ID is not found.
	/// </summary>
	public static BuffableEntity? Create(string cardId, Dictionary<string, LoadCardIntoDbf.Card> cardDb)
	{
		if (cardDb.TryGetValue(cardId, out var cardData))
			return Create(cardData);
		return null;
	}
	/// <summary>
	/// Determine if a spell is actually a Secret or Quest based on card tags.
	/// </summary>
	private static BuffableEntity CreateSpellOrSecret(LoadCardIntoDbf.Card cardData)
	{
		if (cardData.HasTag(GameTag.Secret))
			return new Secret();
		if (cardData.HasTag(GameTag.SideQuest))
			return new SideQuest();
		if (cardData.HasTag(GameTag.Questline) || cardData.HasTag(GameTag.Quest))
			return new Quest();
		return new Spell();
	}
}
