namespace HearthstoneServer.LoadCardIntoDbf;

public sealed class Card
{
	public int DbfId { get; init; }
	public string CardId { get; init; } = "";
	public int Version { get; init; }

	public int Cost { get; init; }
	public int Attack { get; init; }
	public int Health { get; init; }
	public int Durability { get; init; }
	public int QuestProgressTotal { get; init; }

	public Mechanic[] Mechanics { get; init; } = Array.Empty<Mechanic>();
	public Race[] Races { get; init; } = Array.Empty<Race>();
	public CardClass[] Classes { get; init; } = Array.Empty<CardClass>();

	// 你常用的枚举字段（从 tagsInt 中读出来缓存到这里）
	public CardClass CardClass { get; init; } = CardClass.Invalid;
	public CardSet CardSet { get; init; } = CardSet.Invalid;
	public CardType CardType { get; init; } = CardType.Invalid;
	public Faction Faction { get; init; } = Faction.Invalid;
	public Race Race { get; init; } = Race.Invalid;
	public Rarity Rarity { get; init; } = Rarity.Invalid;
	public SpellSchool SpellSchool { get; init; } = SpellSchool.None;
	public FactionType FactionType { get; init; } = FactionType.Generic;

	/// <summary>Whether this card is collectible (can appear in decks/Discover).</summary>
	public bool Collectible { get; init; }

	// ✅ 大多数 tag：int（0/1/枚举/数值）
	public Dictionary<GameTag, int> TagsInt { get; init; } = new();
	public Dictionary<GameTag, int> ReferencedTagsInt { get; init; } = new();

	/// <summary>
	/// Merged view of TagsInt + ReferencedTagsInt.
	/// TagsInt takes priority on conflict. Cached after first access.
	/// 注意：ReferencedTagsInt 是衍生牌的标签，不是卡牌自身的。
	/// </summary>
	private Dictionary<GameTag, int>? _allTagsInt;
	public Dictionary<GameTag, int> AllTagsInt
	{
		get
		{
			if (_allTagsInt != null) return _allTagsInt;
			// ReferencedTagsInt first, then TagsInt overwrites
			var merged = new Dictionary<GameTag, int>(ReferencedTagsInt);
			foreach (var kv in TagsInt)
				merged[kv.Key] = kv.Value;
			_allTagsInt = merged;
			return merged;
		}
	}

	/// <summary>Check if a tag exists and is non-zero in TagsInt (卡牌自身标签).</summary>
	public bool HasTag(GameTag tag)
		=> TagsInt.TryGetValue(tag, out var v) && v != 0;

	/// <summary>Get a tag value from TagsInt (卡牌自身标签), or default 0.</summary>
	public int GetTag(GameTag tag)
		=> TagsInt.TryGetValue(tag, out var v) ? v : 0;

	/// <summary>Check if a tag exists in ReferencedTagsInt (衍生牌标签).</summary>
	public bool HasReferencedTag(GameTag tag)
		=> ReferencedTagsInt.TryGetValue(tag, out var v) && v != 0;

	// ✅ LocString：完整多语言 dict（用于多语言 UI）
	public Dictionary<GameTag, Dictionary<string, string>> TagsLoc { get; init; } = new();

	// ✅ 少量普通 string（比如 ARTISTNAME）
	public Dictionary<GameTag, string> TagsStr { get; init; } = new();

	// ✅ unknown：全 int（key 是 enumID）
	public Dictionary<int, int> UnknownTagsInt { get; init; } = new();
	public Dictionary<int, int> UnknownReferencedTagsInt { get; init; } = new();

	public bool Has(Mechanic m) => Array.IndexOf(Mechanics, m) >= 0;

	/// <summary>Whether this card is a ColossalLimb (附肢).</summary>
	public bool IsColossalLimb => HasTag(GameTag.ColossalLimb);
}