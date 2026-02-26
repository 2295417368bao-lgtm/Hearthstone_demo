namespace HearthstoneServer.Entities;

/// <summary>
/// A player's deck (card list with deck-size constraints).
/// Note: MAX_CARDS is a property (not const) to support future cards
/// that change deck size (e.g. Prince Renathal → 40 cards).
/// </summary>
public class Deck : CardList<BuffableEntity>
{
	/// <summary>Default maximum cards in a deck.</summary>
	public const int DEFAULT_MAX_CARDS = 30;

	/// <summary>Max copies of a single non-legendary card.</summary>
	public const int MAX_UNIQUE_CARDS = 2;

	/// <summary>Max copies of a single legendary card.</summary>
	public const int MAX_UNIQUE_LEGENDARIES = 1;

	/// <summary>
	/// Maximum cards allowed in this deck. Can be overridden per-deck
	/// (e.g. Prince Renathal sets this to 40).
	/// </summary>
	public int MaxCards { get; set; } = DEFAULT_MAX_CARDS;

	/// <summary>The hero card for this deck.</summary>
	public BuffableEntity? Hero { get; set; }

	public Deck() { }
	public Deck(IEnumerable<BuffableEntity> cards) : base(cards) { }

	public override string ToString() => $"<Deck ({Count} cards)>";
}
