using System.Text.RegularExpressions;
using HearthstoneServer.Entities;
using HearthstoneServer.LoadCardIntoDbf;

namespace HearthstoneServer;

/// <summary>
/// Static card data queries, available at game load time (before creating a game).
/// Only depends on CardDbLoader / CardScriptRegistry — no Game or GameController dependency.
/// Used by the frontend for deck-building UI and card browsing.
/// </summary>
public static class CardCatalog
{
	/// <summary>Get all ported cards for a class (class cards + neutral, with scripts only).</summary>
	public static object[] GetAvailableCards(string className)
	{
		var cardDb = GameSetup.GetCardDb();
		if (!Enum.TryParse<CardClass>(className, true, out var cls))
			cls = CardClass.Mage;

		// Ensure scripts are initialized
		Dsl.CardScriptRegistry.Instance.Initialize(typeof(Game).Assembly);
		var registry = Dsl.CardScriptRegistry.Instance;

		var validTypes = new[] { CardType.Minion, CardType.Spell, CardType.Weapon, CardType.Location };
		return cardDb.Values
			.Where(c => c.Collectible
				&& validTypes.Contains(c.CardType)
				&& (c.CardClass == cls || c.CardClass == CardClass.Neutral)
				&& registry.HasScript(c.CardId))  // only ported cards
			.OrderBy(c => c.Cost)
			.ThenBy(c => c.CardId)
			.Select(c => MapCardForBrowser(c))
			.ToArray();
	}

	/// <summary>Get ALL card data (including enchantments/buffs) for frontend lookup.</summary>
	public static object[] GetAllCardData()
	{
		var cardDb = GameSetup.GetCardDb();
		var validTypes = new HashSet<CardType>
		{
			CardType.Minion, CardType.Spell, CardType.Weapon, CardType.Location,
			CardType.Enchantment, CardType.HeroPower, CardType.Hero
		};
		return cardDb.Values
			.Where(c => validTypes.Contains(c.CardType))
			.Select(MapCardForBrowser)
			.ToArray();
	}

	/// <summary>Map a Card to a browser-friendly anonymous object.</summary>
	public static object MapCardForBrowser(Card c)
	{
		string name = c.CardId;
		if (c.TagsLoc.TryGetValue((GameTag)185, out var locDict))
		{
			if (locDict.TryGetValue("zhCN", out var zh)) name = zh;
			else if (locDict.TryGetValue("enUS", out var en)) name = en;
		}
		string? desc = null;
		if (c.TagsLoc.TryGetValue((GameTag)184, out var descDict))
		{
			if (descDict.TryGetValue("zhCN", out var zh)) desc = zh;
			else if (descDict.TryGetValue("enUS", out var en)) desc = en;
		}
		// Resolve $N placeholders (spell damage markers) → just the number
		if (desc != null)
		{
			desc = Regex.Replace(desc, @"\$(\d+)", "$1");
			desc = Regex.Replace(desc, @"<[^>]*>", ""); // strip HTML
		}
		return new
		{
			cardId = c.CardId,
			name,
			cost = c.Cost,
			atk = c.Attack,
			health = c.Health > 0 ? c.Health : (int?)null,
			durability = c.Durability > 0 ? c.Durability : (int?)null,
			type = c.CardType.ToString(),
			rarity = c.Rarity.ToString(),
			className = c.CardClass.ToString(),
			description = desc,
		};
	}
}
