using HearthstoneServer;
using HearthstoneServer.Entities;
using CardData = HearthstoneServer.LoadCardIntoDbf.Card;

namespace Hearthstone.Tests;

/// <summary>
/// Tests for Player feature completion (P0.2).
/// </summary>
public class PlayerFeatureTests
{
	// ══════════════════════════════════════════════════�?
	// Helpers
	// ══════════════════════════════════════════════════�?

	private static (Game game, Player p1, Player p2) CreateBasicGame()
	{
		var p1 = new Player("Alice");
		var p2 = new Player("Bob");
		var game = new Game(p1, p2, seed: 42);

		p1.Hero = new Hero { Controller = p1 };
		game.RegisterEntity(p1.Hero);
		p1.Hero.MaxHealth = 30;

		p2.Hero = new Hero { Controller = p2 };
		game.RegisterEntity(p2.Hero);
		p2.Hero.MaxHealth = 30;

		game.Setup();
		return (game, p1, p2);
	}

	private static Minion AddMinion(Game game, Player owner, int atk, int health, string? cardId = null)
	{
		var m = new Minion
		{
			Controller = owner,
			CardData = cardId != null ? new CardData { CardId = cardId, CardType = CardType.Minion } : null,
		};
		game.RegisterEntity(m);
		m.MaxHealth = health;
		m.Atk = atk;
		m.SetIntProperty("cost", 0);
		m.SetZone(Zone.Hand);
		return m;
	}

	// ══════════════════════════════════════════════════�?
	// Give
	// ══════════════════════════════════════════════════�?

	[Fact]
	public void Give_WithoutCardDb_ReturnsNull()
	{
		var (game, p1, _) = CreateBasicGame();
		// No CardDb configured, so Give returns null
		var result = p1.Give("CS2_032");
		Assert.Null(result);
	}

	// ══════════════════════════════════════════════════�?
	// Steal
	// ══════════════════════════════════════════════════�?

	[Fact]
	public void Steal_MovesEntityFromOpponent()
	{
		var (game, p1, p2) = CreateBasicGame();

		// Place a minion for p2
		var minion = AddMinion(game, p2, 3, 4);
		minion.SetZone(Zone.Play);
		p2.Hand.Remove(minion); // AddMinion puts in hand; move to field

		Assert.Contains(minion, p2.Field);
		Assert.DoesNotContain(minion, p1.Field);

		// p1 steals it
		bool result = p1.Steal(minion);

		Assert.True(result);
		Assert.Contains(minion, p1.Field);
		Assert.DoesNotContain(minion, p2.Field);
		Assert.Equal(p1, minion.Controller);
	}

	[Fact]
	public void Steal_FailsWhenBoardFull()
	{
		var (game, p1, p2) = CreateBasicGame();

		// Fill p1's board
		for (int i = 0; i < 7; i++)
		{
			var m = AddMinion(game, p1, 1, 1);
			m.SetZone(Zone.Play);
			p1.Hand.Remove(m);
		}

		var target = AddMinion(game, p2, 3, 3);
		target.SetZone(Zone.Play);
		p2.Hand.Remove(target);

		bool result = p1.Steal(target);
		Assert.False(result);
		Assert.Contains(target, p2.Field); // Still on opponent's board
	}

	// ══════════════════════════════════════════════════�?
	// GetSpellDamage / GetSpellHeal / GetHeroPowerDamage / GetHeroPowerHeal
	// ══════════════════════════════════════════════════�?

	[Fact]
	public void GetSpellDamage_AppliesSpellPower()
	{
		var (game, p1, _) = CreateBasicGame();

		// Add a minion with spell power directly to field (not via AddMinion which adds to hand)
		var spellDmgMinion = new Minion { Controller = p1 };
		game.RegisterEntity(spellDmgMinion);
		spellDmgMinion.MaxHealth = 1;
		spellDmgMinion.Atk = 1;
		spellDmgMinion.SpellPower = 2;
		spellDmgMinion.SetZone(Zone.Play);

		int damage = p1.GetSpellDamage(5);
		Assert.Equal(7, damage); // 5 + 2 spell power
	}

	[Fact]
	public void GetSpellDamage_WithDouble_DoublesResult()
	{
		var (game, p1, _) = CreateBasicGame();
		p1.SpellPowerDouble = 1;

		int damage = p1.GetSpellDamage(4);
		Assert.Equal(8, damage); // 4 << 1 = 8
	}

	[Fact]
	public void GetSpellHeal_WithHealDouble_DoublesResult()
	{
		var (_, p1, _) = CreateBasicGame();
		p1.HealingDouble = 1;

		int heal = p1.GetSpellHeal(3);
		Assert.Equal(6, heal); // 3 << 1 = 6
	}

	[Fact]
	public void GetHeroPowerDamage_AppliesBonuses()
	{
		var (game, p1, _) = CreateBasicGame();

		var hpMinion = new Minion { Controller = p1 };
		game.RegisterEntity(hpMinion);
		hpMinion.MaxHealth = 1;
		hpMinion.Atk = 1;
		hpMinion.HeroPowerDamage = 1;
		hpMinion.SetZone(Zone.Play);

		int damage = p1.GetHeroPowerDamage(2);
		Assert.Equal(3, damage); // 2 + 1
	}

	[Fact]
	public void GetHeroPowerHeal_WithDouble_DoublesResult()
	{
		var (_, p1, _) = CreateBasicGame();
		p1.HeroPowerDouble = 1;

		int heal = p1.GetHeroPowerHeal(2);
		Assert.Equal(4, heal); // 2 << 1 = 4
	}

	// ══════════════════════════════════════════════════�?
	// Entities / ActionableEntities
	// ══════════════════════════════════════════════════�?

	[Fact]
	public void Entities_IncludesAllPlayerOwnedEntities()
	{
		var (game, p1, _) = CreateBasicGame();

		var minion = AddMinion(game, p1, 2, 2);
		minion.SetZone(Zone.Play);
		p1.Hand.Remove(minion);

		var entities = p1.Entities.ToList();

		// Should include: field minion, hero, self (at minimum)
		Assert.Contains(minion, entities);
		Assert.Contains(p1.Hero, entities);
		Assert.Contains(p1, entities);
	}

	[Fact]
	public void ActionableEntities_IncludesCharactersAndHand()
	{
		var (game, p1, _) = CreateBasicGame();

		var fieldMinion = AddMinion(game, p1, 2, 2);
		fieldMinion.SetZone(Zone.Play);
		p1.Hand.Remove(fieldMinion);

		var handCard = AddMinion(game, p1, 1, 1);
		// AddMinion puts in hand via SetZone(Zone.Hand)

		var actionable = p1.ActionableEntities.ToList();

		Assert.Contains(p1.Hero, actionable);    // Hero is a character
		Assert.Contains(fieldMinion, actionable); // Field minion is a character
		Assert.Contains(handCard, actionable);    // Hand card is actionable
	}

	// ══════════════════════════════════════════════════�?
	// CardsPlayedThisGame / LastCardPlayed
	// ══════════════════════════════════════════════════�?

	[Fact]
	public void CardsPlayedThisGame_IsEmptyInitially()
	{
		var (_, p1, _) = CreateBasicGame();
		Assert.Empty(p1.CardsPlayedThisGame);
		Assert.Null(p1.LastCardPlayed);
	}

	[Fact]
	public void CardsPlayedThisGame_TracksManually()
	{
		var (game, p1, _) = CreateBasicGame();
		var minion = AddMinion(game, p1, 1, 1);

		p1.CardsPlayedThisGame.Add(minion);
		p1.LastCardPlayed = minion;

		Assert.Single(p1.CardsPlayedThisGame);
		Assert.Equal(minion, p1.LastCardPlayed);
	}

	// ══════════════════════════════════════════════════�?
	// ShuffleDeck (determinism)
	// ══════════════════════════════════════════════════�?

	[Fact]
	public void ShuffleDeck_IsDeterministicWithSameSeed()
	{
		// Two games with same seed should produce same shuffle
		var p1a = new Player("Alice");
		var p2a = new Player("Bob");
		var gameA = new Game(p1a, p2a, seed: 123);
		p1a.Hero = new Hero { Controller = p1a };
		gameA.RegisterEntity(p1a.Hero);
		p1a.Hero.MaxHealth = 30;
		p2a.Hero = new Hero { Controller = p2a };
		gameA.RegisterEntity(p2a.Hero);
		p2a.Hero.MaxHealth = 30;

		for (int i = 0; i < 5; i++)
		{
			var m = new Minion { Controller = p1a };
			gameA.RegisterEntity(m);
			m.MaxHealth = 1;
			m.Atk = i + 1; // Different stats to distinguish
			m.SetZone(Zone.Deck);
			p1a.Deck.Add(m);
		}

		var p1b = new Player("Alice");
		var p2b = new Player("Bob");
		var gameB = new Game(p1b, p2b, seed: 123);
		p1b.Hero = new Hero { Controller = p1b };
		gameB.RegisterEntity(p1b.Hero);
		p1b.Hero.MaxHealth = 30;
		p2b.Hero = new Hero { Controller = p2b };
		gameB.RegisterEntity(p2b.Hero);
		p2b.Hero.MaxHealth = 30;

		for (int i = 0; i < 5; i++)
		{
			var m = new Minion { Controller = p1b };
			gameB.RegisterEntity(m);
			m.MaxHealth = 1;
			m.Atk = i + 1;
			m.SetZone(Zone.Deck);
			p1b.Deck.Add(m);
		}

		p1a.ShuffleDeck();
		p1b.ShuffleDeck();

		// Same seed + same operation = same order
		for (int i = 0; i < 5; i++)
		{
			Assert.Equal(((Minion)p1a.Deck[i]).Atk, ((Minion)p1b.Deck[i]).Atk);
		}
	}

	// ══════════════════════════════════════════════════�?
	// DiscardHand
	// ══════════════════════════════════════════════════�?

	[Fact]
	public void DiscardHand_MovesAllCardsToGraveyard()
	{
		var (game, p1, _) = CreateBasicGame();

		for (int i = 0; i < 3; i++)
		{
			AddMinion(game, p1, 1, 1);
		}

		Assert.Equal(3, p1.Hand.Count);
		int graveyardBefore = p1.Graveyard.Count;

		p1.DiscardHand();

		Assert.Empty(p1.Hand);
		Assert.Equal(graveyardBefore + 3, p1.Graveyard.Count);
	}

	// ══════════════════════════════════════════════════�?
	// Mana operations
	// ══════════════════════════════════════════════════�?

	[Fact]
	public void CanPayCost_ChecksAvailableMana()
	{
		var (_, p1, _) = CreateBasicGame();
		p1.MaxMana = 5;
		p1.UsedMana = 0;

		Assert.True(p1.CanPayCost(5));
		Assert.True(p1.CanPayCost(3));
		Assert.False(p1.CanPayCost(6));
	}

	[Fact]
	public void PayCost_ConsumesTempManaFirst()
	{
		var (_, p1, _) = CreateBasicGame();
		p1.MaxMana = 5;
		p1.TempMana = 2;

		p1.PayCost(3);

		Assert.Equal(0, p1.TempMana); // 2 temp used
		Assert.Equal(1, p1.UsedMana); // 1 regular used
		Assert.Equal(4, p1.Mana);     // 5 - 1 = 4
	}

	// ══════════════════════════════════════════════════�?
	// MinionSlots
	// ══════════════════════════════════════════════════�?

	[Fact]
	public void MinionSlots_DecreasesWithFieldSize()
	{
		var (game, p1, _) = CreateBasicGame();

		Assert.Equal(7, p1.MinionSlots);

		for (int i = 0; i < 3; i++)
		{
			var m = AddMinion(game, p1, 1, 1);
			m.SetZone(Zone.Play);
			p1.Hand.Remove(m);
		}

		Assert.Equal(4, p1.MinionSlots);
	}

	// ══════════════════════════════════════════════════�?
	// StartingDeck / StartingHero (PrepareForGame setup)
	// ══════════════════════════════════════════════════�?

	[Fact]
	public void StartingDeckAndHero_CanBeSet()
	{
		var p1 = new Player("Alice");
		p1.StartingDeck = new List<string> { "CS2_032", "CS2_033" };
		p1.StartingHero = "HERO_01";

		Assert.Equal(2, p1.StartingDeck.Count);
		Assert.Equal("HERO_01", p1.StartingHero);
	}

	// ══════════════════════════════════════════════════�?
	// Overload properties
	// ══════════════════════════════════════════════════�?

	[Fact]
	public void Mana_AccountsForOverload()
	{
		var (_, p1, _) = CreateBasicGame();
		p1.MaxMana = 5;
		p1.OverloadLocked = 2;

		Assert.Equal(3, p1.Mana); // 5 - 0 (used) - 2 (overload) = 3
	}
}
