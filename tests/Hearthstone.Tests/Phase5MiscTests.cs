using System.Linq;
using HearthstoneServer;
using HearthstoneServer.Dsl;
using HearthstoneServer.Entities;
using CardData = HearthstoneServer.LoadCardIntoDbf.Card;
using static HearthstoneServer.Dsl.BusEvent;

namespace Hearthstone.Tests;

public class Phase5MiscTests
{
	private Game CreateTestGame()
	{
		var p1 = new Player("Player1");
		p1.Hero = new Hero();
		p1.Hero.MaxHealth = 30;
		var p2 = new Player("Player2");
		p2.Hero = new Hero();
		p2.Hero.MaxHealth = 30;
		for (int i = 0; i < 10; i++)
		{
			p1.Deck.Add(new Minion { MaxHealth = 1 });
			p2.Deck.Add(new Minion { MaxHealth = 1 });
		}
		return new Game(p1, p2, seed: 42);
	}

	private static Minion CreateMinion(Game game, Player owner, int atk = 1, int health = 1, string? cardId = null)
	{
		var minion = new Minion
		{
			Controller = owner,
			MaxHealth = health,
			Atk = atk,
			CardData = cardId != null ? new CardData { CardId = cardId } : null,
		};
		minion.SetIntProperty("cost", 0);
		game.RegisterEntity(minion);
		minion.SetZone(Zone.Hand);
		return minion;
	}

	[Fact]
	public void StartOfGameEvent_InDeck_Subscription_Fires()
	{
		var game = CreateTestGame();
		var builder = new CardBuilder();
		int triggerCount = 0;

		builder.Card("TEST_SOG_INDECK",
			on: WhenDo<HearthstoneServer.Events.StartOfGameEvent>(OWN, ON, (evt, self) =>
			{
				if (evt.Player == self.Controller)
					triggerCount++;
			}).InDeck());

		game.Player1.CreateCard(new CardData
		{
			CardId = "TEST_SOG_INDECK",
			CardType = CardType.Minion,
			Attack = 1,
			Health = 1,
			Cost = 1,
		}, Zone.Deck);

		game.Initialize();

		Assert.Equal(1, triggerCount);
	}

	[Fact]
	public void StartOfGameEvent_DefaultZone_DoesNotFireFromDeck()
	{
		var game = CreateTestGame();
		var builder = new CardBuilder();
		int triggerCount = 0;

		builder.Card("TEST_SOG_DEFAULT_ZONE",
			on: WhenDo<HearthstoneServer.Events.StartOfGameEvent>(OWN, ON, (_, _) => triggerCount++));

		game.Player1.CreateCard(new CardData
		{
			CardId = "TEST_SOG_DEFAULT_ZONE",
			CardType = CardType.Minion,
			Attack = 1,
			Health = 1,
			Cost = 1,
		}, Zone.Deck);

		game.Initialize();

		Assert.Equal(0, triggerCount);
	}

	[Fact]
	public void StartOfGameEvent_RunsBeforeInitialDraw()
	{
		var game = CreateTestGame();
		var builder = new CardBuilder();
		bool sawEmptyHand = false;

		builder.Card("TEST_SOG_ORDER",
			on: WhenDo<HearthstoneServer.Events.StartOfGameEvent>(OWN, ON, (evt, _) =>
			{
				sawEmptyHand = evt.Player.Hand.Count == 0;
			}).InDeck());

		game.Player1.CreateCard(new CardData
		{
			CardId = "TEST_SOG_ORDER",
			CardType = CardType.Minion,
			Attack = 1,
			Health = 1,
			Cost = 1,
		}, Zone.Deck);

		game.Initialize();

		Assert.True(sawEmptyHand);
		Assert.Equal(3, game.Player1.Hand.Count);
		Assert.Equal(5, game.Player2.Hand.Count);
	}

	[Fact]
	public void Steamcleaner_RemovesGeneratedCardsOnly()
	{
		var game = CreateTestGame();
		game.Setup();
		game.BeginTurn(game.Player1);
		CardScriptRegistry.Instance.Initialize();

		var p1 = game.Player1;
		var p2 = game.Player2;

		p1.Deck.Clear();
		p2.Deck.Clear();
		p1.StartingDeck = new List<string> { "BASE_A", "BASE_A", "BASE_B" };
		p2.StartingDeck = new List<string> { "ENEMY_A" };

		foreach (var cardId in p1.StartingDeck)
			p1.CreateCard(new CardData { CardId = cardId, CardType = CardType.Minion, Attack = 1, Health = 1, Cost = 1 }, Zone.Deck);
		p1.CreateCard(new CardData { CardId = "EXTRA_X", CardType = CardType.Minion, Attack = 1, Health = 1, Cost = 1 }, Zone.Deck);

		foreach (var cardId in p2.StartingDeck)
			p2.CreateCard(new CardData { CardId = cardId, CardType = CardType.Minion, Attack = 1, Health = 1, Cost = 1 }, Zone.Deck);

		var steamcleaner = CreateMinion(game, p1, atk: 5, health: 5, cardId: "CORE_REV_946");
		game.PlayCard(steamcleaner);

		Assert.DoesNotContain(p1.Deck, card => (card as BuffableEntity)?.CardId == "EXTRA_X");
		Assert.Equal(3, p1.Deck.Count);
	}

	[Fact]
	public void Steamcleaner_RespectsDuplicateCounts()
	{
		var game = CreateTestGame();
		game.Setup();
		game.BeginTurn(game.Player1);
		CardScriptRegistry.Instance.Initialize();

		var p1 = game.Player1;
		p1.Deck.Clear();
		p1.StartingDeck = new List<string> { "BASE_A", "BASE_A", "BASE_B" };

		p1.CreateCard(new CardData { CardId = "BASE_A", CardType = CardType.Minion, Attack = 1, Health = 1, Cost = 1 }, Zone.Deck);
		p1.CreateCard(new CardData { CardId = "BASE_A", CardType = CardType.Minion, Attack = 1, Health = 1, Cost = 1 }, Zone.Deck);
		p1.CreateCard(new CardData { CardId = "BASE_A", CardType = CardType.Minion, Attack = 1, Health = 1, Cost = 1 }, Zone.Deck);
		p1.CreateCard(new CardData { CardId = "BASE_B", CardType = CardType.Minion, Attack = 1, Health = 1, Cost = 1 }, Zone.Deck);

		var steamcleaner = CreateMinion(game, p1, atk: 5, health: 5, cardId: "CORE_REV_946");
		game.PlayCard(steamcleaner);

		Assert.Equal(2, p1.Deck.Count(card => (card as BuffableEntity)?.CardId == "BASE_A"));
		Assert.Equal(3, p1.Deck.Count);
	}

	[Fact]
	public void Siamat_FirstChoice_UsesMultiStepChoice()
	{
		var game = CreateTestGame();
		game.Setup();
		game.BeginTurn(game.Player1);
		CardScriptRegistry.Instance.Initialize();

		var siamat = CreateMinion(game, game.Player1, atk: 7, health: 7, cardId: "CORE_ULD_178");
		game.PlayCard(siamat);

		var choice = Assert.IsType<MultiStepChoice>(game.Player1.Choice);
		var optionIds = choice.Cards.OfType<BuffableEntity>().Select(card => card.CardId).OrderBy(id => id).ToArray();

		Assert.Equal(4, choice.Cards.Count);
		Assert.Equal(new[] { "ULD_178a", "ULD_178a2", "ULD_178a3", "ULD_178a4" }, optionIds);
	}

	[Fact]
	public void Siamat_SecondChoice_ExcludesFirstSelection()
	{
		var game = CreateTestGame();
		game.Setup();
		game.BeginTurn(game.Player1);
		CardScriptRegistry.Instance.Initialize();

		var siamat = CreateMinion(game, game.Player1, atk: 7, health: 7, cardId: "CORE_ULD_178");
		game.PlayCard(siamat);

		var firstChoice = Assert.IsType<MultiStepChoice>(game.Player1.Choice);
		var firstPick = Assert.Single(firstChoice.Cards.OfType<BuffableEntity>(), card => card.CardId == "ULD_178a");
		game.Player1.Choose(firstPick);

		var secondChoice = Assert.IsType<MultiStepChoice>(game.Player1.Choice);
		Assert.Equal(3, secondChoice.Cards.Count);
		Assert.DoesNotContain(secondChoice.Cards, card => (card as BuffableEntity)?.CardId == "ULD_178a");
	}

	[Fact]
	public void Siamat_TwoChoices_ApplyTwoKeywords()
	{
		var game = CreateTestGame();
		game.Setup();
		game.BeginTurn(game.Player1);
		CardScriptRegistry.Instance.Initialize();

		var siamat = CreateMinion(game, game.Player1, atk: 7, health: 7, cardId: "CORE_ULD_178");
		game.PlayCard(siamat);

		var firstChoice = Assert.IsType<MultiStepChoice>(game.Player1.Choice);
		game.Player1.Choose(Assert.Single(firstChoice.Cards.OfType<BuffableEntity>(), card => card.CardId == "ULD_178a4"));

		var secondChoice = Assert.IsType<MultiStepChoice>(game.Player1.Choice);
		game.Player1.Choose(Assert.Single(secondChoice.Cards.OfType<BuffableEntity>(), card => card.CardId == "ULD_178a2"));

		Assert.Null(game.Player1.Choice);
		Assert.True(siamat.Rush);
		Assert.True(siamat.DivineShield);
		Assert.False(siamat.Windfury);
		Assert.False(siamat.Taunt);
	}

	[Fact]
	public void Nozdormu_BothStartingDecks_SetTimeoutTo15()
	{
		var game = CreateTestGame();
		CardScriptRegistry.Instance.Initialize();

		game.Player1.CreateCard(new CardData { CardId = "CS3_035", CardType = CardType.Minion, Attack = 8, Health = 8, Cost = 7 }, Zone.Deck);
		game.Player2.CreateCard(new CardData { CardId = "CS3_035", CardType = CardType.Minion, Attack = 8, Health = 8, Cost = 7 }, Zone.Deck);

		game.Initialize();

		Assert.Equal(15, game.Player1.Timeout);
		Assert.Equal(15, game.Player2.Timeout);
	}

	[Fact]
	public void Nozdormu_OnlyOneStartingDeck_NoChange()
	{
		var game = CreateTestGame();
		CardScriptRegistry.Instance.Initialize();

		game.Player1.CreateCard(new CardData { CardId = "CS3_035", CardType = CardType.Minion, Attack = 8, Health = 8, Cost = 7 }, Zone.Deck);

		game.Initialize();

		Assert.Equal(75, game.Player1.Timeout);
		Assert.Equal(75, game.Player2.Timeout);
	}
}
