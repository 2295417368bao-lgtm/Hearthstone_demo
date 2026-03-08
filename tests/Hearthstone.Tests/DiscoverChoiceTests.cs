using HearthstoneServer;
using HearthstoneServer.Entities;
using HearthstoneServer.LoadCardIntoDbf;

namespace Hearthstone.Tests;

public class DiscoverChoiceTests
{
	[Fact]
	public void Discover_SupportsFixedCardIdPool()
	{
		var game = new Game(new Player("P1"), new Player("P2"), seed: 42);
		game.Setup();
		var player = game.Player1;

		game.CardDb = new Dictionary<string, Card>
		{
			["DISCOVER_1"] = MakeMinion("DISCOVER_1", 1),
			["DISCOVER_2"] = MakeMinion("DISCOVER_2", 2),
			["DISCOVER_3"] = MakeMinion("DISCOVER_3", 3),
			["DISCOVER_4"] = MakeMinion("DISCOVER_4", 4),
		};

		var source = new Minion { MaxHealth = 1, Controller = player };
		game.RegisterEntity(source);

		var discover = HearthstoneServer.Cards.CardScriptUtils.MakeDiscover(
			player,
			"DISCOVER_1", "DISCOVER_2", "DISCOVER_3", "DISCOVER_4");

		discover.Trigger(source);

		Assert.NotNull(player.Choice);
		Assert.Equal(3, player.Choice!.Cards.Count);

		var choices = player.Choice.Cards.Cast<BuffableEntity>().ToList();
		Assert.Equal(choices.Count, choices.Select(card => card.CardId).Distinct().Count());
		Assert.All(choices, card =>
		{
			Assert.Contains(card.CardId, game.CardDb.Keys);
			Assert.Equal(Zone.SetAside, card.Zone);
		});

		var chosen = choices[1];
		player.Choice.Choose(chosen);

		Assert.Null(player.Choice);
		Assert.Equal(Zone.Hand, chosen.Zone);
		Assert.Contains(chosen, player.Hand);
	}

	[Fact]
	public void Discover_FixedCardIdPool_UsesAllChoicesWhenPoolSmallerThanThree()
	{
		var game = new Game(new Player("P1"), new Player("P2"), seed: 42);
		game.Setup();
		var player = game.Player1;

		game.CardDb = new Dictionary<string, Card>
		{
			["DISCOVER_A"] = MakeMinion("DISCOVER_A", 1),
			["DISCOVER_B"] = MakeMinion("DISCOVER_B", 2),
		};

		var source = new Minion { MaxHealth = 1, Controller = player };
		game.RegisterEntity(source);

		var discover = HearthstoneServer.Cards.CardScriptUtils.MakeDiscover(
			player,
			"DISCOVER_A", "DISCOVER_B");

		discover.Trigger(source);

		Assert.NotNull(player.Choice);
		Assert.Equal(2, player.Choice!.Cards.Count);
		Assert.All(player.Choice.Cards.Cast<BuffableEntity>(), card => Assert.Equal(Zone.SetAside, card.Zone));
	}

	private static Card MakeMinion(string cardId, int cost) => new()
	{
		CardId = cardId,
		CardType = CardType.Minion,
		Collectible = true,
		Cost = cost,
		CardSet = CardSet.Core,
		Races = Array.Empty<Race>()
	};
}
