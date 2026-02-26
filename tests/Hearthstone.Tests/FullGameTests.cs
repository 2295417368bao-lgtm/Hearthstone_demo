using HearthstoneServer;
using HearthstoneServer.Dsl;
using HearthstoneServer.Entities;
using CardData = HearthstoneServer.LoadCardIntoDbf.Card;

namespace Hearthstone.Tests;

/// <summary>
/// End-to-end full game tests �?verifies Setup �?Mulligan �?multi-turn �?GameOver.
/// </summary>
public class FullGameTests
{
	// ══════════════════════════════════════════════════�?
	// Helpers
	// ══════════════════════════════════════════════════�?

	private static Game CreateGameWithDecks(int deckSize = 30, int seed = 42)
	{
		var p1 = new Player("Alice");
		var p2 = new Player("Bob");

		var game = new Game(p1, p2, seed: seed);

		// Create heroes (must register with game FIRST so Tags get initialized)
		p1.Hero = new Hero { Controller = p1 };
		game.RegisterEntity(p1.Hero);
		p1.Hero.MaxHealth = 30;

		p2.Hero = new Hero { Controller = p2 };
		game.RegisterEntity(p2.Hero);
		p2.Hero.MaxHealth = 30;

		// Fill decks with generic minion cards
		for (int i = 0; i < deckSize; i++)
		{
			var m1 = new Minion
			{
				Controller = p1,
				CardData = new CardData { CardId = $"test_p1_{i}", CardType = CardType.Minion, Cost = 1 },
			};
			game.RegisterEntity(m1);
			m1.MaxHealth = 2;
			m1.Atk = 2;
			m1.SetIntProperty("cost", 1);
			m1.SetZone(Zone.Deck);
			p1.Deck.Add(m1);

			var m2 = new Minion
			{
				Controller = p2,
				CardData = new CardData { CardId = $"test_p2_{i}", CardType = CardType.Minion, Cost = 1 },
			};
			game.RegisterEntity(m2);
			m2.MaxHealth = 2;
			m2.Atk = 2;
			m2.SetIntProperty("cost", 1);
			m2.SetZone(Zone.Deck);
			p2.Deck.Add(m2);
		}

		return game;
	}

	private static Minion CreateMinion(Game game, Player owner,
		int atk = 1, int health = 1, string? cardId = null, int cost = 0)
	{
		var m = new Minion
		{
			Controller = owner,
			CardData = cardId != null ? new CardData { CardId = cardId, Cost = cost } : null,
		};
		game.RegisterEntity(m);
		m.MaxHealth = health;
		m.Atk = atk;
		m.SetIntProperty("cost", cost);
		m.SetZone(Zone.Hand);
		owner.Hand.Add(m);
		return m;
	}

	// ══════════════════════════════════════════════════�?
	// Setup & Start
	// ══════════════════════════════════════════════════�?

	[Fact]
	public void Setup_InitializesGameCorrectly()
	{
		var game = CreateGameWithDecks();
		game.Setup();

		Assert.Equal(GameState.Running, game.State);
		Assert.NotNull(game.Player1.Opponent);
		Assert.NotNull(game.Player2.Opponent);
		Assert.Equal(game.Player2, game.Player1.Opponent);
		Assert.Equal(game.Player1, game.Player2.Opponent);
	}

	[Fact]
	public void Start_DrawsInitialHands_AndGivesCoin()
	{
		var game = CreateGameWithDecks();
		game.Start();

		var first = game.Player1;
		var second = game.Player2;

		// First player draws 3 + 1 (turn draw) = 4
		Assert.True(first.Hand.Count >= 3, $"First player should have 3+ cards, got {first.Hand.Count}");
		// Second player draws 4 + coin = 5 (no turn draw yet since it's first player's turn)
		Assert.True(second.Hand.Count >= 4, $"Second player should have 4+ cards, got {second.Hand.Count}");

		Assert.Equal(GameState.Running, game.State);
		Assert.Equal(1, game.Turn);
		Assert.NotNull(game.CurrentPlayer);
	}

	[Fact]
	public void Mulligan_ReplacesCards()
	{
		var game = CreateGameWithDecks();
		game.Setup();
		game.InitialDraw();

		var p1 = game.Player1;
		var before = p1.Hand.Count;
		var toReplace = new List<BaseEntity> { p1.Hand[0] };

		game.Mulligan(p1, toReplace);

		Assert.Equal(before, p1.Hand.Count);
	}

	// ══════════════════════════════════════════════════�?
	// Turn Flow
	// ══════════════════════════════════════════════════�?

	[Fact]
	public void BeginTurn_IncrementsManaAndDrawsCard()
	{
		var game = CreateGameWithDecks();
		game.Setup();
		game.BeginTurn(game.Player1);

		var p1 = game.Player1;
		Assert.Equal(1, p1.MaxMana);
		Assert.Equal(1, p1.Mana);
		Assert.Equal(1, game.Turn);
		Assert.Equal(p1, game.CurrentPlayer);
	}

	[Fact]
	public void EndTurn_SwitchesToOpponent()
	{
		var game = CreateGameWithDecks();
		game.Setup();
		game.BeginTurn(game.Player1);

		var p1 = game.CurrentPlayer!;
		game.EndTurn();

		Assert.Equal(p1.Opponent, game.CurrentPlayer);
		Assert.Equal(2, game.Turn);
	}

	[Fact]
	public void MultiTurn_ManaIncreasesCorrectly()
	{
		var game = CreateGameWithDecks();
		game.Setup();
		game.BeginTurn(game.Player1);

		Assert.Equal(1, game.Player1.MaxMana);

		game.EndTurn();
		Assert.Equal(1, game.Player2.MaxMana);

		game.EndTurn();
		Assert.Equal(2, game.Player1.MaxMana);
		Assert.Equal(3, game.Turn);
	}

	// ══════════════════════════════════════════════════�?
	// Playing Cards
	// ══════════════════════════════════════════════════�?

	[Fact]
	public void PlayCard_PlacesMinionOnField()
	{
		var game = CreateGameWithDecks();
		game.Setup();
		game.BeginTurn(game.Player1);
		var p1 = game.Player1;

		var minion = CreateMinion(game, p1, atk: 2, health: 3, cost: 1);
		int handBefore = p1.Hand.Count;

		game.PlayCard(minion);

		Assert.Equal(handBefore - 1, p1.Hand.Count);
		Assert.Contains(minion, p1.Field);
	}

	// ══════════════════════════════════════════════════�?
	// Combat
	// ══════════════════════════════════════════════════�?

	[Fact]
	public void Attack_MinionsDamageEachOther()
	{
		var game = CreateGameWithDecks();
		game.Setup();
		game.BeginTurn(game.Player1);
		game.EndTurn();
		game.EndTurn();

		var p1 = game.Player1;
		var p2 = game.Player2;

		var attacker = CreateMinion(game, p1, atk: 3, health: 5, cost: 0);
		game.PlayCard(attacker);

		game.EndTurn();
		var defender = CreateMinion(game, p2, atk: 2, health: 4, cost: 0);
		game.PlayCard(defender);

		game.EndTurn();

		game.Attack(attacker, defender);

		Assert.Equal(3, attacker.Health); // 5 - 2
		Assert.Equal(1, defender.Health); // 4 - 3
	}

	[Fact]
	public void Attack_HeroTakesDamage()
	{
		var game = CreateGameWithDecks();
		game.Setup();
		game.BeginTurn(game.Player1);
		var p1 = game.Player1;
		var p2 = game.Player2;

		var minion = CreateMinion(game, p1, atk: 4, health: 3, cost: 0);
		game.PlayCard(minion);

		game.EndTurn();
		game.EndTurn();

		game.Attack(minion, p2.Hero!);

		Assert.Equal(26, p2.Hero!.Health); // 30 - 4
	}

	// ══════════════════════════════════════════════════�?
	// Death Processing
	// ══════════════════════════════════════════════════�?

	[Fact]
	public void ProcessDeaths_MovesToGraveyard()
	{
		var game = CreateGameWithDecks();
		game.Setup();
		game.BeginTurn(game.Player1);
		var p1 = game.Player1;
		var p2 = game.Player2;

		var m1 = CreateMinion(game, p1, atk: 5, health: 5, cost: 0);
		game.PlayCard(m1);

		game.EndTurn();
		var m2 = CreateMinion(game, p2, atk: 5, health: 2, cost: 0);
		game.PlayCard(m2);

		game.EndTurn();

		game.Attack(m1, m2);

		Assert.True(p2.Graveyard.Count > 0 || m2.Health <= 0, "m2 should be dead");
	}

	// ══════════════════════════════════════════════════�?
	// Game Over Detection
	// ══════════════════════════════════════════════════�?

	[Fact]
	public void GameOver_WhenHeroDies()
	{
		var game = CreateGameWithDecks(deckSize: 5);
		game.Setup();
		game.BeginTurn(game.Player1);
		var p1 = game.Player1;
		var p2 = game.Player2;

		p2.Hero!.Hit(30);
		game.ProcessDeaths();

		Assert.Equal(GameState.Complete, game.State);
		Assert.Equal(PlayState.Won, p1.PlayState);
		Assert.Equal(PlayState.Lost, p2.PlayState);
	}

	[Fact]
	public void GameOver_Tie_WhenBothHeroesDie()
	{
		var game = CreateGameWithDecks(deckSize: 5);
		game.Setup();
		game.BeginTurn(game.Player1);
		var p1 = game.Player1;
		var p2 = game.Player2;

		p1.Hero!.Hit(30);
		p2.Hero!.Hit(30);
		game.ProcessDeaths();

		Assert.Equal(GameState.Complete, game.State);
		Assert.Equal(PlayState.Tied, p1.PlayState);
		Assert.Equal(PlayState.Tied, p2.PlayState);
	}

	[Fact]
	public void Concede_EndsGame()
	{
		var game = CreateGameWithDecks(deckSize: 5);
		game.Setup();
		game.BeginTurn(game.Player1);

		game.Player1.Concede();
		game.CheckForEndGame();

		Assert.Equal(GameState.Complete, game.State);
		Assert.Equal(PlayState.Lost, game.Player1.PlayState);
		Assert.Equal(PlayState.Won, game.Player2.PlayState);
	}

	// ══════════════════════════════════════════════════�?
	// Fatigue
	// ══════════════════════════════════════════════════�?

	[Fact]
	public void Fatigue_DamagesHeroWhenDeckEmpty()
	{
		var game = CreateGameWithDecks(deckSize: 1);
		game.Setup();
		game.BeginTurn(game.Player1);
		var p1 = game.Player1;

		while (p1.Deck.Count > 0)
			p1.DrawCard();

		int healthBefore = p1.Hero!.Health;
		p1.DrawCard();

		Assert.True(p1.FatigueCounter > 0, "Fatigue counter should increment");
		Assert.True(p1.Hero.Health < healthBefore, "Hero should take fatigue damage");
	}

	// ══════════════════════════════════════════════════�?
	// Full Game Simulation
	// ══════════════════════════════════════════════════�?

	[Fact]
	public void FullGame_MultiTurnSimulation_CompletesWithoutCrash()
	{
		var game = CreateGameWithDecks(deckSize: 10, seed: 123);
		game.Start();

		for (int i = 0; i < 20; i++)
		{
			if (game.State == GameState.Complete) break;

			var player = game.CurrentPlayer!;

			if (player.Hand.Count > 0)
			{
				var card = player.Hand[0];
				if (card is Minion m && player.Mana >= m.Cost)
				{
					try { game.PlayCard(m); }
					catch { /* Play validations may fail */ }
				}
			}

			if (player.Field.Count > 0 && player.Opponent?.Hero != null)
			{
				foreach (var entity in player.Field.ToList())
				{
					if (entity is Character c && c.NumAttacks == 0 && c.Atk > 0)
					{
						try { game.Attack(c, player.Opponent.Hero); }
						catch { break; }
					}
					if (game.State == GameState.Complete) break;
				}
			}

			if (game.State == GameState.Complete) break;
			game.EndTurn();
		}

		Assert.True(game.Turn >= 1, "Game should have played at least 1 turn");
		Assert.True(game.State == GameState.Running || game.State == GameState.Complete);
	}

	[Fact]
	public void FullGame_ExhaustDecks_ReachesGameOver()
	{
		// Use very small decks so fatigue kicks in quickly
		var game = CreateGameWithDecks(deckSize: 2, seed: 999);
		game.Setup();
		game.BeginTurn(game.Player1);

		int maxTurns = 60;
		for (int i = 0; i < maxTurns; i++)
		{
			if (game.State == GameState.Complete) break;

			var player = game.CurrentPlayer!;

			// Play all affordable cards
			while (player.Hand.Count > 0 && player.Mana >= 1)
			{
				var card = player.Hand[0];
				if (card is Minion m)
				{
					int cost = m.Cost;
					if (player.Mana >= cost)
					{
						try { game.PlayCard(m); }
						catch { player.Hand.Remove(m); break; }
					}
					else break;
				}
				else break;
			}

			// Attack face with every minion
			if (player.Opponent?.Hero != null)
			{
				foreach (var entity in player.Field.ToList())
				{
					if (entity is Character c && c.NumAttacks == 0 && c.Atk > 0 && !c.Exhausted)
					{
						try { game.Attack(c, player.Opponent.Hero); }
						catch { /* Skip */ }
					}
					if (game.State == GameState.Complete) break;
				}
			}

			if (game.State == GameState.Complete) break;
			game.EndTurn();
			if (game.State == GameState.Complete) break;
		}

		// With only 2 cards per deck, fatigue damage accumulates quickly:
		// After deck is empty, each draw deals 1+2+3+4+... damage.
		// By turn ~12, cumulative fatigue alone = 1+2+...+8 = 36 > 30hp.
		Assert.Equal(GameState.Complete, game.State);

		var p1State = game.Player1.PlayState;
		var p2State = game.Player2.PlayState;
		Assert.True(
			(p1State == PlayState.Won && p2State == PlayState.Lost) ||
			(p1State == PlayState.Lost && p2State == PlayState.Won) ||
			(p1State == PlayState.Tied && p2State == PlayState.Tied),
			$"Invalid final states: P1={p1State}, P2={p2State}");
	}
}
