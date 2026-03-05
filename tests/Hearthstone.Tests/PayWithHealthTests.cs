using Xunit;
using HearthstoneServer;
using HearthstoneServer.Entities;

namespace Hearthstone.Tests;

public class CostBloodTests
{
	private static (Game game, Player p1) SetupGame()
	{
		var p1 = new Player("P1") { MaxMana = 10 };
		p1.Hero = new Hero { MaxHealth = 30, Controller = p1 };
		var p2 = new Player("P2") { MaxMana = 10 };
		p2.Hero = new Hero { MaxHealth = 30, Controller = p2 };
		for (int i = 0; i < 5; i++)
		{
			p1.Deck.Add(new Minion { MaxHealth = 1, Controller = p1 });
			p2.Deck.Add(new Minion { MaxHealth = 1, Controller = p2 });
		}
		var game = new Game(p1, p2, seed: 42);
		game.Start();
		return (game, p1);
	}

	private static Minion CreateMinion(Game game, Player owner, int cost, int costBlood = 0)
	{
		var m = new Minion { Controller = owner, MaxHealth = 1, Atk = 1 };
		m.SetIntProperty("cost", cost);
		game.RegisterEntity(m);
		m.EnsureTags();
		if (costBlood > 0)
			m.Tags[GameTag.CostBlood] = costBlood;
		m.SetZone(Zone.Hand);
		return m;
	}

	[Fact]
	public void CostBlood1_AlwaysDeductsHealth()
	{
		var (game, p1) = SetupGame();
		int startHp = p1.Hero!.Health;
		int startMana = p1.Mana;

		var card = CreateMinion(game, p1, cost: 5, costBlood: 1);

		game.PlayCard(card);

		Assert.Equal(startMana, p1.Mana); // Mana untouched
		Assert.Equal(startHp - 5, p1.Hero!.Health); // 5 HP lost
	}

	[Fact]
	public void CostBlood1_FailsIfNotEnoughHealth()
	{
		var (game, p1) = SetupGame();
		p1.Hero!.DamageTaken = p1.Hero.MaxHealth - 2; // 2 HP left

		var card = CreateMinion(game, p1, cost: 5, costBlood: 1);

		Assert.Throws<InvalidActionException>(() => game.PlayCard(card));
	}

	[Fact]
	public void CostBlood2_DeductsMana_EngineIgnoresConditional()
	{
		// CostBlood=2 means "conditional" — engine only checks ==1
		// The card script (not engine) is responsible for changing 2→1
		var (game, p1) = SetupGame();
		int startHp = p1.Hero!.Health;
		int startMana = p1.Mana;

		var card = CreateMinion(game, p1, cost: 3, costBlood: 2);

		game.PlayCard(card);

		Assert.Equal(startMana - 3, p1.Mana); // Mana deducted (engine ignores cb==2)
		Assert.Equal(startHp, p1.Hero!.Health); // Health unchanged
	}

	[Fact]
	public void NormalCard_DeductsManaNotHealth()
	{
		var (game, p1) = SetupGame();
		int startHp = p1.Hero!.Health;
		int startMana = p1.Mana;

		var card = CreateMinion(game, p1, cost: 3);

		game.PlayCard(card);

		Assert.Equal(startMana - 3, p1.Mana); // Mana deducted
		Assert.Equal(startHp, p1.Hero!.Health); // Health unchanged
	}
}
