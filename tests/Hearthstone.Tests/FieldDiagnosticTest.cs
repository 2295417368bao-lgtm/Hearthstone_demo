using HearthstoneServer;
using HearthstoneServer.Dsl;
using HearthstoneServer.Entities;
using HearthstoneServer.LoadCardIntoDbf;

namespace Hearthstone.Tests;

public class FieldDiagnosticTest
{
	static FieldDiagnosticTest()
	{
		CardScriptRegistry.Instance.Initialize();
	}

	private static (Game game, Player p1, Player p2) CreateDiagGame()
	{
		string cardsPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "data", "cards_dbfid.json");
		cardsPath = Path.GetFullPath(cardsPath);
		var (_, cardDb) = CardDbLoader.Load(cardsPath);

		var p1 = new Player("Human") { StartingHero = "HERO_05", StartingDeck = new List<string> { "CS2_172", "CS2_172" } };
		var p2 = new Player("AI") { StartingHero = "HERO_01", StartingDeck = new List<string> { "CS2_168", "CS2_168", "CS2_168", "CS2_168", "CS2_168" } };

		var game = new Game(p1, p2, 42);
		game.CardDb = cardDb;

		p1.PrepareForGame();
		p2.PrepareForGame();
		game.Setup();

		return (game, p1, p2);
	}

	[Fact]
	public void PlayMultipleMinions_AllAppearOnField()
	{
		var (game, p1, p2) = CreateDiagGame();

		// Draw cards for AI
		for (int i = 0; i < 4; i++) p2.DrawCard();

		Console.WriteLine($"After draw: P2 hand={p2.Hand.Count}, P2 field={p2.Field.Count}");
		Console.WriteLine("P2 Hand:");
		foreach (var c in p2.Hand)
		{
			var be = c as BuffableEntity;
			Console.WriteLine($"  - {be?.CardData?.CardId ?? "?"} type={c.GetType().Name} Zone={be?.Zone} Cost={be?.Cost} EntityId={c.EntityId}");
		}

		// Begin turns to give AI mana
		game.BeginTurn(game.Player1);
		game.EndTurn(); // â†?begins P2's turn

		Console.WriteLine($"\nP2 turn: Mana={p2.Mana}/{p2.MaxMana}");

		// Play first card
		var card1 = p2.Hand.OfType<BuffableEntity>().FirstOrDefault(c => p2.Mana >= c.Cost);
		Assert.NotNull(card1);
		Console.WriteLine($"\nPlaying card 1: {card1!.CardData?.CardId} EntityId={card1.EntityId} Zone={card1.Zone}");

		int handBefore = p2.Hand.Count;
		game.PlayCard(card1);
		Console.WriteLine($"After play 1: hand={p2.Hand.Count} field={p2.Field.Count} card.Zone={card1.Zone}");

		Assert.Equal(handBefore - 1, p2.Hand.Count); // card removed from hand
		Assert.True(p2.Field.Count >= 1, $"Field should have >= 1 minion but has {p2.Field.Count}");

		Console.WriteLine("Field contents after card 1:");
		foreach (var f in p2.Field)
		{
			var m = f as Minion;
			Console.WriteLine($"  [{f.EntityId}] {(f as BuffableEntity)?.CardData?.CardId} Atk={m?.Atk} HP={m?.Health} Dead={m?.Dead}");
		}

		// Give more mana and play second card
		p2.UsedMana = 0;
		var card2 = p2.Hand.OfType<BuffableEntity>().FirstOrDefault(c => p2.Mana >= c.Cost);
		Assert.NotNull(card2);
		Console.WriteLine($"\nPlaying card 2: {card2!.CardData?.CardId} EntityId={card2.EntityId} Zone={card2.Zone}");

		handBefore = p2.Hand.Count;
		game.PlayCard(card2);
		Console.WriteLine($"After play 2: hand={p2.Hand.Count} field={p2.Field.Count} card.Zone={card2.Zone}");

		Assert.Equal(handBefore - 1, p2.Hand.Count);
		Assert.True(p2.Field.Count >= 2, $"Field should have >= 2 minions but has {p2.Field.Count}");

		Console.WriteLine("Field contents after card 2:");
		foreach (var f in p2.Field)
		{
			var m = f as Minion;
			Console.WriteLine($"  [{f.EntityId}] {(f as BuffableEntity)?.CardData?.CardId} Atk={m?.Atk} HP={m?.Health} Dead={m?.Dead}");
		}
	}
}
