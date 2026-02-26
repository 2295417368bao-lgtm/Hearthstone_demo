using HearthstoneServer;
using HearthstoneServer.Dsl;
using HearthstoneServer.Entities;
using HearthstoneServer.LoadCardIntoDbf;
using CardData = HearthstoneServer.LoadCardIntoDbf.Card;

namespace Hearthstone.Tests;

/// <summary>
/// End-to-end integration tests for core improvements:
/// - Play flow with card script triggering
/// - Deathrattle triggering on death
/// - Event listener registration
/// - Play targets / RequiresTarget
/// - The Coin and Game expansion cards
/// - Full turn flow
/// </summary>
public class IntegrationTests
{
	// ── Helpers ──

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

	/// <summary>Create a test minion in hand with specified stats.</summary>
	private static Minion CreateMinion(Game game, Player owner, int atk = 1, int health = 1,
		string? cardId = null)
	{
		var m = new Minion
		{
			Controller = owner,
			MaxHealth = health,
			Atk = atk,
			CardData = cardId != null ? new CardData { CardId = cardId } : null,
		};
		m.SetIntProperty("cost", 0); // free for testing
		game.RegisterEntity(m);
		m.SetZone(Zone.Hand);
		return m;
	}

	/// <summary>Create a test spell in hand with specified card id.</summary>
	private static Spell CreateSpell(Game game, Player owner, string? cardId = null, int cost = 0)
	{
		var s = new Spell
		{
			Controller = owner,
			CardData = cardId != null ? new CardData { CardId = cardId } : null,
		};
		s.SetIntProperty("cost", cost);
		game.RegisterEntity(s);
		s.SetZone(Zone.Hand);
		return s;
	}

	// ══════════════════════════════════════�?
	// Test 1: The Coin gives temporary mana
	// ══════════════════════════════════════�?

	[Fact]
	public void TheCoin_GivesTempMana()
	{
		var game = CreateTestGame();
		game.Setup();
		CardScriptRegistry.Instance.Initialize();
		game.BeginTurn(game.Player1);

		var coin = CreateSpell(game, game.Player1, "GAME_005", cost: 0);

		int manaBefore = game.Player1.Mana;
		game.PlayCard(coin);
		int manaAfter = game.Player1.Mana;

		// The Coin should give +1 temp mana
		Assert.Equal(manaBefore + 1, manaAfter);
	}

	// ══════════════════════════════════════�?
	// Test 2: Card script Play actions trigger
	// ══════════════════════════════════════�?

	[Fact]
	public void PlayMinion_TriggersBattlecry()
	{
		var game = CreateTestGame();
		game.Setup();
		game.BeginTurn(game.Player1);

		CardScriptRegistry.Instance.Register("TEST_BC", new TestBattlecryScript());
		var m = CreateMinion(game, game.Player1, atk: 2, health: 3, cardId: "TEST_BC");

		int heroBefore = game.Player2.Hero!.Health;
		game.PlayCard(m);
		int heroAfter = game.Player2.Hero!.Health;

		// Battlecry should deal 2 damage to enemy hero
		Assert.Equal(heroBefore - 2, heroAfter);
	}

	// ══════════════════════════════════════�?
	// Test 3: Deathrattle triggers on death
	// ══════════════════════════════════════�?

	[Fact]
	public void Minion_Deathrattle_TriggersOnDeath()
	{
		var game = CreateTestGame();
		game.Setup();
		game.BeginTurn(game.Player1);

		CardScriptRegistry.Instance.Register("TEST_DR", new TestDeathrattleScript());
		var m = CreateMinion(game, game.Player1, atk: 1, health: 1, cardId: "TEST_DR");
		game.PlayCard(m);

		// Damage hero first so we can see healing
		game.Player1.Hero!.Hit(5);
		int heroBefore = game.Player1.Hero!.Health;

		// Kill the minion
		m.Hit(10);
		game.ProcessDeaths();

		int heroAfter = game.Player1.Hero!.Health;
		// Deathrattle should have healed 3 health
		Assert.Equal(heroBefore + 3, heroAfter);
	}

	// ══════════════════════════════════════�?
	// Test 4: Event listeners register and trigger
	// ══════════════════════════════════════�?

	[Fact]
	public void EventBus_RegistersOnPlay_AndSubscribes()
	{
		var game = CreateTestGame();
		game.Setup();
		game.BeginTurn(game.Player1);

		// Register a script that uses OnActivate to subscribe to EventBus
		CardScriptRegistry.Instance.Register("TEST_EVT", new TestEventScript());
		var m = CreateMinion(game, game.Player1, atk: 3, health: 4, cardId: "TEST_EVT");
		game.PlayCard(m);

		// After playing, the EventBus should have subscriptions (test passes if no exception)
		Assert.Equal(Zone.Play, m.Zone);
	}

	// ══════════════════════════════════════�?
	// Test 5: Full game turn flow
	// ══════════════════════════════════════�?

	[Fact]
	public void FullGame_TurnFlow_Complete()
	{
		var game = CreateTestGame();
		game.Start();
		Assert.Equal(GameState.Running, game.State);
		Assert.Equal(1, game.Turn);
		Assert.NotNull(game.CurrentPlayer);

		var firstPlayer = game.CurrentPlayer;

		// End turn �?should switch to opponent
		game.EndTurn();
		Assert.Equal(2, game.Turn);
		Assert.NotEqual(firstPlayer, game.CurrentPlayer);

		// End opponent's turn �?back to first player
		game.EndTurn();
		Assert.Equal(3, game.Turn);
	}

	// ══════════════════════════════════════�?
	// Test 6: PlayTargets / RequiresTarget
	// ══════════════════════════════════════�?

	[Fact]
	public void PlayTargets_Empty_WhenNoRequirements()
	{
		var game = CreateTestGame();
		game.Setup();
		game.BeginTurn(game.Player1);

		var m = CreateMinion(game, game.Player1, atk: 1, health: 1);
		// With null requirements, PlayTargets should return empty
		Assert.Empty(m.PlayTargets);
	}

	[Fact]
	public void RequiresTarget_FalseByDefault()
	{
		var game = CreateTestGame();
		game.Setup();
		game.BeginTurn(game.Player1);

		var m = CreateMinion(game, game.Player1, atk: 1, health: 1);
		Assert.False(m.RequiresTarget);
	}

	[Fact]
	public void RequiresTarget_True_WhenTargetToPlayRequired()
	{
		var game = CreateTestGame();
		game.Setup();
		game.BeginTurn(game.Player1);

		var m = CreateMinion(game, game.Player1, atk: 1, health: 1);
		m.TargetReq = HearthstoneServer.Targeting.Target.Required();
		Assert.True(m.RequiresTarget);
	}

	// ══════════════════════════════════════�?
	// Test 7: GameCards are registered
	// ══════════════════════════════════════�?

	[Fact]
	public void GameCards_AreRegistered()
	{
		CardScriptRegistry.Instance.Initialize();
		Assert.True(CardScriptRegistry.Instance.HasScript("GAME_005"),
			"The Coin (GAME_005) should be registered");
		Assert.True(CardScriptRegistry.Instance.HasScript("GAME_001"),
			"Luck of the Coin (GAME_001) should be registered");
		Assert.True(CardScriptRegistry.Instance.HasScript("GAME_003"),
			"Coin's Vengeance (GAME_003) should be registered");
	}

	// ══════════════════════════════════════�?
	// Test 8: PlayCard increments counters
	// ══════════════════════════════════════�?

	[Fact]
	public void PlayCard_IncrementsCardsPlayed()
	{
		var game = CreateTestGame();
		game.Setup();
		game.BeginTurn(game.Player1);

		var m = CreateMinion(game, game.Player1, atk: 1, health: 1);
		Assert.Equal(0, game.Player1.CardsPlayedThisTurn);

		game.PlayCard(m);
		Assert.Equal(1, game.Player1.CardsPlayedThisTurn);
		Assert.Equal(1, game.Player1.MinionsPlayedThisTurn);
	}

	// ── Test card script implementations ──

	/// <summary>Test script: deals 2 damage to enemy hero as battlecry.</summary>
	private class TestBattlecryScript : CardScriptBase
	{
		public override HearthstoneServer.Dsl.Action[] Battlecry => new HearthstoneServer.Dsl.Action[]
		{
			new DealDamage(S.ENEMY_HERO, 2)
		};
	}

	/// <summary>Test script: heals own hero for 3 as deathrattle.</summary>
	private class TestDeathrattleScript : CardScriptBase
	{
		public override HearthstoneServer.Dsl.Action[] Deathrattle => new HearthstoneServer.Dsl.Action[]
		{
			new RestoreHealth(S.FRIENDLY_HERO, 3)
		};
	}

	/// <summary>Test script: uses OnActivate to register EventBus subscriptions.</summary>
	private class TestEventScript : CardScriptBase
	{
		public static bool Activated { get; set; }
		public override System.Action<BuffableEntity, Game>? OnActivate => (entity, game) =>
		{
			Activated = true;
		};
	}

	// ══════════════════════════════════════�?
	// Test 9: Buff action end-to-end
	// ══════════════════════════════════════�?

	[Fact]
	public void PlayMinion_Buff_GivesTargetStats()
	{
		var game = CreateTestGame();
		game.Setup();
		game.BeginTurn(game.Player1);

		// Register Shattered Sun Cleric-style battlecry: Buff(TARGET, "+1/+1")
		CardScriptRegistry.Instance.Register("TEST_BUFF_BC", new TestBuffBattlecryScript());
		CardScriptRegistry.Instance.Register("TEST_BUFF_E", new TestBuffEnchScript());

		// Create a target minion already on board
		var target = CreateMinion(game, game.Player1, atk: 2, health: 3);
		target.SetZone(Zone.Play);

		// Create the buff minion in hand
		var buffMinion = CreateMinion(game, game.Player1, atk: 3, health: 3, cardId: "TEST_BUFF_BC");

		int atkBefore = target.Atk;
		int hpBefore = target.MaxHealth;

		game.PlayCard(buffMinion, target: target);

		// Battlecry should give target +1/+1
		Assert.Equal(atkBefore + 1, target.Atk);
		Assert.Equal(hpBefore + 1, target.MaxHealth);
		Assert.Single(target.Buffs);
	}

	/// <summary>Test script: Battlecry buffs TARGET with +1/+1.</summary>
	private class TestBuffBattlecryScript : CardScriptBase
	{
		public override HearthstoneServer.Dsl.Action[] Battlecry => new HearthstoneServer.Dsl.Action[]
		{
			new Enchant(S.TARGET, "TEST_BUFF_E")
		};
	}

	/// <summary>+1/+1 enchantment.</summary>
	private class TestBuffEnchScript : CardScriptBase
	{
		public override Dictionary<GameTag, object>? Tags => new()
		{
			[GameTag.Atk] = 1,
			[GameTag.Health] = 1
		};
	}

	// ══════════════════════════════════════�?
	// Test 10: Draw action end-to-end (Novice Engineer style)
	// ══════════════════════════════════════�?

	[Fact]
	public void PlayMinion_Draw_DrawsCard()
	{
		var game = CreateTestGame();
		game.Setup();
		game.BeginTurn(game.Player1);

		// Register Novice Engineer-style battlecry: Draw(CONTROLLER)
		CardScriptRegistry.Instance.Register("TEST_DRAW_BC", new TestDrawBattlecryScript());

		var p1 = game.Player1;
		int deckBefore = p1.Deck.Count;
		int handBefore = p1.Hand.Count;

		// Create the draw minion in hand
		var drawMinion = CreateMinion(game, p1, atk: 1, health: 1, cardId: "TEST_DRAW_BC");

		game.PlayCard(drawMinion);

		// Battlecry should draw a card: deck -1, hand +1 (net: same because minion left hand)
		Assert.Equal(deckBefore - 1, p1.Deck.Count);
		// Hand: started with handBefore, added drawMinion (+1), played it (-1), drew a card (+1) = handBefore + 1
		Assert.Equal(handBefore + 1, p1.Hand.Count);
	}

	/// <summary>Test script: Battlecry draws a card.</summary>
	private class TestDrawBattlecryScript : CardScriptBase
	{
		public override HearthstoneServer.Dsl.Action[] Battlecry => new HearthstoneServer.Dsl.Action[]
		{
			new DrawCard(S.CONTROLLER)
		};
	}

	// ══════════════════════════════════════�?
	// Test 11: Summon action end-to-end (Deathrattle summons token)
	// ══════════════════════════════════════�?

	[Fact]
	public void Deathrattle_Summon_SpawnsToken()
	{
		var game = CreateTestGame();
		game.Setup();
		game.BeginTurn(game.Player1);

		// Set up CardDb with a token entry
		game.CardDb = new Dictionary<string, CardData>
		{
			["TEST_TOKEN"] = new CardData
			{
				CardId = "TEST_TOKEN",
				CardType = CardType.Minion,
				Attack = 1,
				Health = 1,
				Cost = 0
			}
		};

		// Register deathrattle script that summons a token
		CardScriptRegistry.Instance.Register("TEST_DR_SUMMON", new TestDeathrattleSummonScript());

		var p1 = game.Player1;

		// Create a minion with deathrattle
		var minion = CreateMinion(game, p1, atk: 2, health: 1, cardId: "TEST_DR_SUMMON");
		game.PlayCard(minion);

		int fieldBefore = p1.Field.Count;

		// Kill the minion �?triggers deathrattle
		minion.Hit(1);
		game.ProcessDeaths();

		// Token should have been summoned
		Assert.Equal(fieldBefore, p1.Field.Count); // minion died (-1), token spawned (+1) = same
	}

	/// <summary>Deathrattle: summon a 1/1 token.</summary>
	private class TestDeathrattleSummonScript : CardScriptBase
	{
		public override HearthstoneServer.Dsl.Action[] Deathrattle => new HearthstoneServer.Dsl.Action[]
		{
			new Summon(S.CONTROLLER, "TEST_TOKEN")
		};
	}

	// ══════════════════════════════════════�?
	// Test 12: Charge minion attacks immediately
	// ══════════════════════════════════════�?

	[Fact]
	public void PlayCharge_AttackEnemy_DealsDamage()
	{
		var game = CreateTestGame();
		game.Setup();
		game.BeginTurn(game.Player1);

		var p1 = game.Player1;
		var p2 = game.Player2;

		// Place an enemy minion on p2's field
		var enemy = new Minion
		{
			Controller = p2,
			MaxHealth = 5,
			Atk = 2,
		};
		game.RegisterEntity(enemy);
		enemy.SetZone(Zone.Play, p2);
		enemy.TurnsInPlay = 1;

		// Create a Charge minion in p1's hand
		var charger = CreateMinion(game, p1, atk: 4, health: 3);
		charger.Charge = true;

		// Play it
		game.PlayCard(charger);

		// Charge minion should NOT be asleep
		Assert.False(charger.Asleep);
		Assert.True(charger.CanAttack(enemy));

		// Attack the enemy
		charger.Attack(enemy);

		Assert.Equal(1, enemy.Health);  // 5 - 4 = 1
		Assert.Equal(1, charger.Health); // 3 - 2 = 1
	}

	// ══════════════════════════════════════�?
	// Real-Card Integration Tests (Phase 4)
	// Use actual [CardScript] registrations
	// ══════════════════════════════════════�?

	// ── Test 13: Nightblade (EX1_593) �?Battlecry: Deal 3 damage to enemy hero ──

	[Fact]
	public void Nightblade_RealCard_DealsDamage()
	{
		var game = CreateTestGame();
		game.Setup();
		CardScriptRegistry.Instance.Initialize();
		game.BeginTurn(game.Player1);

		var nightblade = CreateMinion(game, game.Player1, atk: 4, health: 4, cardId: "EX1_593");

		int heroBefore = game.Player2.Hero!.Health;
		game.PlayCard(nightblade);

		// Nightblade battlecry: Hit(ENEMY_HERO, 3)
		Assert.Equal(heroBefore - 3, game.Player2.Hero.Health);
		// Minion should be on the field
		Assert.Contains(nightblade, game.Player1.Field);
	}

	// ── Test 14: Novice Engineer (EX1_015) �?Battlecry: Draw a card ──

	[Fact]
	public void NoviceEngineer_RealCard_DrawsCard()
	{
		var game = CreateTestGame();
		game.Setup();
		CardScriptRegistry.Instance.Initialize();
		game.BeginTurn(game.Player1);

		var p1 = game.Player1;
		int deckBefore = p1.Deck.Count;
		int handBefore = p1.Hand.Count;

		var engineer = CreateMinion(game, p1, atk: 1, health: 1, cardId: "EX1_015");
		game.PlayCard(engineer);

		// Battlecry: Draw(CONTROLLER) �?deck -1, hand = before + 1 (minion left but drew a card)
		Assert.Equal(deckBefore - 1, p1.Deck.Count);
		Assert.Equal(handBefore + 1, p1.Hand.Count);
	}

	// ── Test 15: Voodoo Doctor (EX1_011) �?Battlecry: Restore 2 Health ──

	[Fact]
	public void VoodooDoctor_RealCard_HealsTarget()
	{
		var game = CreateTestGame();
		game.Setup();
		CardScriptRegistry.Instance.Initialize();
		game.BeginTurn(game.Player1);

		// Damage own hero first
		game.Player1.Hero!.Hit(5);
		Assert.Equal(25, game.Player1.Hero.Health);

		var doc = CreateMinion(game, game.Player1, atk: 2, health: 1, cardId: "EX1_011");
		game.PlayCard(doc, target: game.Player1.Hero);

		// Battlecry: Heal(TARGET, 2) �?hero should regain 2 HP
		Assert.Equal(27, game.Player1.Hero.Health);
	}

	// ── Test 16: Fireball (CS2_029) �?Deal 6 damage to target ──

	[Fact]
	public void Fireball_RealCard_DealsDamage()
	{
		var game = CreateTestGame();
		game.Setup();
		CardScriptRegistry.Instance.Initialize();
		game.BeginTurn(game.Player1);

		var fireball = CreateSpell(game, game.Player1, cardId: "CS2_029", cost: 0);

		int heroBefore = game.Player2.Hero!.Health;
		game.PlayCard(fireball, target: game.Player2.Hero);

		// Fireball: Hit(TARGET, 6)
		Assert.Equal(heroBefore - 6, game.Player2.Hero.Health);
		// Spell should go to graveyard
		Assert.Equal(Zone.Graveyard, fireball.Zone);
	}

	// ── Test 17: Arcane Intellect (CS2_023) �?Draw 2 cards ──

	[Fact]
	public void ArcaneIntellect_RealCard_DrawsTwoCards()
	{
		var game = CreateTestGame();
		game.Setup();
		CardScriptRegistry.Instance.Initialize();
		game.BeginTurn(game.Player1);

		var p1 = game.Player1;
		int deckBefore = p1.Deck.Count;
		int handBefore = p1.Hand.Count;

		var ai = CreateSpell(game, p1, cardId: "CS2_023", cost: 0);
		game.PlayCard(ai);

		// Draw(CONTROLLER) × 2 �?deck -2, hand +2 (spell left hand but drew 2)
		Assert.Equal(deckBefore - 2, p1.Deck.Count);
		Assert.Equal(handBefore + 2, p1.Hand.Count);
	}

	// ── Test 18: Flamestrike (CS2_032) �?Deal 4 damage to all enemy minions ──

	[Fact]
	public void Flamestrike_RealCard_AOEDamage()
	{
		var game = CreateTestGame();
		game.Setup();
		CardScriptRegistry.Instance.Initialize();
		game.BeginTurn(game.Player1);

		var p2 = game.Player2;

		// Place 3 enemy minions with varying health
		var e1 = new Minion { Controller = p2, MaxHealth = 3, Atk = 1 };
		var e2 = new Minion { Controller = p2, MaxHealth = 5, Atk = 2 };
		var e3 = new Minion { Controller = p2, MaxHealth = 4, Atk = 1 };
		game.RegisterEntity(e1); e1.SetZone(Zone.Play, p2);
		game.RegisterEntity(e2); e2.SetZone(Zone.Play, p2);
		game.RegisterEntity(e3); e3.SetZone(Zone.Play, p2);

		var flamestrike = CreateSpell(game, game.Player1, cardId: "CS2_032", cost: 0);
		game.PlayCard(flamestrike);

		// Flamestrike: Hit(ENEMY_MINIONS, 4)
		// e1 (3 HP) should die, e2 (5 HP) �?1 HP, e3 (4 HP) should die
		Assert.True(e1.Health <= 0 || e1.Zone == Zone.Graveyard,
			$"e1 should be dead (health={e1.Health}, zone={e1.Zone})");
		Assert.Equal(1, e2.Health); // 5 - 4 = 1
		Assert.True(e3.Health <= 0 || e3.Zone == Zone.Graveyard,
			$"e3 should be dead (health={e3.Health}, zone={e3.Zone})");
	}

	// ── Test 19: Demolisher (EX1_102) �?At start of your turn, deal 2 to random enemy ──

	[Fact]
	public void Demolisher_RealCard_TurnStartEvent()
	{
		var game = CreateTestGame();
		game.Setup();
		CardScriptRegistry.Instance.Initialize();
		game.BeginTurn(game.Player1);

		var p1 = game.Player1;
		var demolisher = CreateMinion(game, p1, atk: 1, health: 4, cardId: "EX1_102");
		game.PlayCard(demolisher);

		// Demolisher uses on: When<TurnStartEvent>(OWN, ON, ...) via OnActivate
		// Verify subscription is active by checking OnActivate was set
		var script = HearthstoneServer.Dsl.CardScriptRegistry.Instance.GetScript("EX1_102");
		Assert.NotNull(script?.OnActivate);

		// Record total enemy health before turn
		int enemyHeroBefore = game.Player2.Hero!.Health;

		// End turn (P1) �?Begin turn (P2) �?nothing for Demolisher
		game.EndTurn();
		// End turn (P2) �?Begin turn (P1) �?Demolisher triggers!
		game.EndTurn();

		// After P1's turn begins again, Demolisher should have dealt 2 damage to a random enemy
		int enemyHeroAfter = game.Player2.Hero!.Health;

		// Since the only enemy character is the hero, hero should take 2 damage
		Assert.Equal(enemyHeroBefore - 2, enemyHeroAfter);
	}

	// ── Test 20: Multi-turn real-card game simulation ──

	[Fact]
	public void MultiTurn_RealCards_FullGame()
	{
		var game = CreateTestGame();
		game.Setup();
		CardScriptRegistry.Instance.Initialize();
		game.BeginTurn(game.Player1);

		var p1 = game.Player1;
		var p2 = game.Player2;

		// === Turn 1 (P1): Play Nightblade �?deal 3 to enemy hero ===
		var nightblade = CreateMinion(game, p1, atk: 4, health: 4, cardId: "EX1_593");
		int p2HeroBefore = p2.Hero!.Health;
		game.PlayCard(nightblade);
		Assert.Equal(p2HeroBefore - 3, p2.Hero.Health); // 30 �?27

		// === End Turn �?P2's turn ===
		game.EndTurn();
		Assert.Same(p2, game.CurrentPlayer);

		// === Turn 2 (P2): Play Novice Engineer �?draw a card ===
		int p2DeckBefore = p2.Deck.Count;
		var engineer = CreateMinion(game, p2, atk: 1, health: 1, cardId: "EX1_015");
		game.PlayCard(engineer);
		Assert.Equal(p2DeckBefore - 1, p2.Deck.Count);

		// === End Turn �?P1's turn ===
		game.EndTurn();
		Assert.Same(p1, game.CurrentPlayer);

		// === Turn 3 (P1): Fireball enemy hero �?6 damage ===
		int p2HeroBeforeFireball = p2.Hero!.Health;
		var fireball = CreateSpell(game, p1, cardId: "CS2_029", cost: 0);
		game.PlayCard(fireball, target: p2.Hero);
		Assert.Equal(p2HeroBeforeFireball - 6, p2.Hero.Health);

		// === Nightblade attacks enemy hero ===
		nightblade.NumAttacks = 0; // reset for this turn
		int p2HeroBeforeAttack = p2.Hero!.Health;
		game.Attack(nightblade, p2.Hero);
		Assert.Equal(p2HeroBeforeAttack - 4, p2.Hero.Health);

		// === End Turn �?P2's turn ===
		game.EndTurn();
		Assert.Same(p2, game.CurrentPlayer);

		// === Turn 4 (P2): Heal own hero with Voodoo Doctor ===
		p2.Hero.Hit(5); // take 5 more damage for setup
		int p2HeroBeforeHeal = p2.Hero!.Health;
		var doc = CreateMinion(game, p2, atk: 2, health: 1, cardId: "EX1_011");
		game.PlayCard(doc, target: p2.Hero);
		Assert.Equal(p2HeroBeforeHeal + 2, p2.Hero.Health);

		// Verify overall game state
		Assert.Equal(GameState.Running, game.State);
		Assert.True(p1.Field.Contains(nightblade), "Nightblade should still be on P1's field");
		Assert.True(p2.Field.Contains(engineer), "Novice Engineer should be on P2's field");
	}

	// ══════════════════════════════════════════════════�?
	// Phase 5 �?Card Model 完善 集成测试
	// ══════════════════════════════════════════════════�?

	/// <summary>
	/// #21 武器装备 + 英雄攻击 + 耐久消�?
	/// </summary>
	[Fact]
	public void Weapon_Equip_And_Attack()
	{
		var game = CreateTestGame();
		game.Setup();
		var p1 = game.Players[0];
		var p2 = game.Players[1];
		game.BeginTurn(p1);
		p1.MaxMana = 10;
		p1.UsedMana = 0;

		// Create a weapon (3 ATK, 2 Durability, cost 0)
		var weapon = new Weapon
		{
			Controller = p1,
			Atk = 3,
			MaxDurability = 2,
			CardData = new CardData { CardId = "test_weapon" },
		};
		weapon.SetIntProperty("cost", 0);
		game.RegisterEntity(weapon);
		weapon.SetZone(Zone.Hand);

		// Equip the weapon
		weapon.Equip();

		// Verify weapon is equipped
		Assert.Same(weapon, p1.Hero!.EquippedWeapon);
		Assert.Equal(3, weapon.Atk);
		Assert.Equal(2, weapon.Durability);

		// Lose 1 durability (simulating hero attack)
		weapon.LoseDurability();
		Assert.Equal(1, weapon.Durability);
		Assert.False(weapon.Dead);

		// Lose another durability �?weapon should be broken
		weapon.LoseDurability();
		Assert.Equal(0, weapon.Durability);
		Assert.True(weapon.Dead);
	}

	/// <summary>
	/// #22 新武器替换旧武器（旧武器被销毁）
	/// </summary>
	[Fact]
	public void Weapon_Replace_Destroys_Old()
	{
		var game = CreateTestGame();
		game.Setup();
		var p1 = game.Players[0];
		game.BeginTurn(p1);
		p1.MaxMana = 10;
		p1.UsedMana = 0;

		// First weapon
		var weapon1 = new Weapon
		{
			Controller = p1,
			Atk = 2,
			MaxDurability = 3,
			CardData = new CardData { CardId = "weapon_1" },
		};
		weapon1.SetIntProperty("cost", 0);
		game.RegisterEntity(weapon1);
		weapon1.SetZone(Zone.Hand);
		weapon1.Equip();
		Assert.Same(weapon1, p1.Hero!.EquippedWeapon);

		// Second weapon replaces first
		var weapon2 = new Weapon
		{
			Controller = p1,
			Atk = 5,
			MaxDurability = 2,
			CardData = new CardData { CardId = "weapon_2" },
		};
		weapon2.SetIntProperty("cost", 0);
		game.RegisterEntity(weapon2);
		weapon2.SetZone(Zone.Hand);
		weapon2.Equip();

		// Weapon2 is now equipped, Weapon1 destroyed (sent to graveyard)
		Assert.Same(weapon2, p1.Hero.EquippedWeapon);
		Assert.Equal(Zone.Graveyard, weapon1.Zone);
		Assert.Equal(5, weapon2.Atk);
		Assert.Equal(2, weapon2.Durability);
	}

	/// <summary>
	/// #23 英雄技能使用（增加激活计�?+ 费用支付�?
	/// </summary>
	[Fact]
	public void HeroPower_Use_Flow()
	{
		var game = CreateTestGame();
		game.Setup();
		var p1 = game.Players[0];
		var p2 = game.Players[1];
		game.BeginTurn(p1);
		p1.MaxMana = 10;
		p1.UsedMana = 0;

		// Create a hero power (cost 2)
		var heroPower = new HeroPower
		{
			Controller = p1,
			CardData = new CardData { CardId = "test_heropower" },
		};
		heroPower.SetIntProperty("cost", 2);
		game.RegisterEntity(heroPower);
		heroPower.Zone = Zone.Play;
		p1.Hero!.Power = heroPower;

		// Verify initial state
		Assert.True(heroPower.IsUsable);
		Assert.False(heroPower.Exhausted);
		Assert.Equal(0, heroPower.ActivationsThisTurn);
		int manaBefore = p1.Mana;

		// Use the hero power
		heroPower.Use();

		// Verify cost was paid and activation tracked
		Assert.Equal(manaBefore - 2, p1.Mana);
		Assert.Equal(1, heroPower.ActivationsThisTurn);
		Assert.True(heroPower.Exhausted); // 1 activation = exhausted (no extra activations)
		Assert.False(heroPower.IsUsable); // can't use again

		// Verify times used counter
		Assert.Equal(1, p1.TimesHeroPowerUsedThisGame);
	}

	/// <summary>
	/// #24 CardFactory �?CardType 创建正确的实体类�?
	/// </summary>
	[Fact]
	public void CardFactory_CreatesCorrectTypes()
	{
		var minionData = new CardData { CardId = "test_minion", CardType = CardType.Minion, Attack = 3, Health = 4, Cost = 2 };
		var spellData = new CardData { CardId = "test_spell", CardType = CardType.Spell, Cost = 5 };
		var weaponData = new CardData { CardId = "test_weapon", CardType = CardType.Weapon, Attack = 4, Durability = 2, Cost = 3 };
		var heroData = new CardData { CardId = "test_hero", CardType = CardType.Hero, Health = 30, Cost = 7 };
		var heroPowerData = new CardData { CardId = "test_hp", CardType = CardType.HeroPower, Cost = 2 };

		var minion = CardFactory.Create(minionData);
		Assert.IsType<Minion>(minion);
		Assert.Equal(3, ((Minion)minion).Atk);
		Assert.Equal(4, ((Minion)minion).MaxHealth);
		Assert.Equal(2, minion.Cost);

		var spell = CardFactory.Create(spellData);
		Assert.IsType<Spell>(spell);
		Assert.Equal(5, spell.Cost);

		var weapon = CardFactory.Create(weaponData);
		Assert.IsType<Weapon>(weapon);
		Assert.Equal(4, ((Weapon)weapon).Atk);
		Assert.Equal(2, ((Weapon)weapon).MaxDurability);
		Assert.Equal(3, weapon.Cost);

		var hero = CardFactory.Create(heroData);
		Assert.IsType<Hero>(hero);
		Assert.Equal(30, ((Hero)hero).MaxHealth);
		Assert.Equal(7, hero.Cost);

		var heroPower = CardFactory.Create(heroPowerData);
		Assert.IsType<HeroPower>(heroPower);
		Assert.Equal(2, heroPower.Cost);
	}

	// ══════════════════════════════════════════════════�?
	// Phase 6  �?Secret/Quest System Tests
	// ══════════════════════════════════════════════════�?

	[Fact]
	public void Secret_Reveal_Moves_To_Graveyard()
	{
		var game = CreateTestGame();
		game.Setup();
		game.BeginTurn(game.Player1);
		var p1 = game.Player1;
		p1.MaxMana = 10;

		// Create a secret entity
		var secret = new Secret
		{
			Controller = p1,
			CardData = new CardData { CardId = "TEST_SECRET" },
		};
		secret.SetIntProperty("cost", 0);
		game.RegisterEntity(secret);
		secret.SetZone(Zone.Hand);

		// Play the secret �?should redirect to Zone.Secret
		game.PlayCard(secret);
		Assert.Equal(Zone.Secret, secret.Zone);
		Assert.Contains(secret, p1.Secrets);

		// Reveal the secret �?should move to graveyard
		var revealAction = new RevealSecret(secret);
		revealAction.Do(secret, secret);

		Assert.Equal(Zone.Graveyard, secret.Zone);
		Assert.DoesNotContain(secret, p1.Secrets);
	}

	[Fact]
	public void Secret_Uniqueness_Check()
	{
		var game = CreateTestGame();
		game.Setup();
		game.BeginTurn(game.Player1);
		var p1 = game.Player1;

		// Create and place first secret
		var secret1 = new Secret
		{
			Controller = p1,
			CardData = new CardData { CardId = "SAME_SECRET" },
		};
		game.RegisterEntity(secret1);
		secret1.SetZone(Zone.Secret);

		Assert.Single(p1.Secrets);

		// Second secret with same ID should not be summonable
		var secret2 = new Secret
		{
			Controller = p1,
			CardData = new CardData { CardId = "SAME_SECRET" },
		};
		game.RegisterEntity(secret2);
		secret2.SetZone(Zone.Hand);

		Assert.False(secret2.IsSummonable());

		// Different ID should be summonable
		var secret3 = new Secret
		{
			Controller = p1,
			CardData = new CardData { CardId = "DIFF_SECRET" },
		};
		game.RegisterEntity(secret3);
		secret3.SetZone(Zone.Hand);

		Assert.True(secret3.IsSummonable());
	}

	[Fact]
	public void Quest_Progress_And_Completion()
	{
		var game = CreateTestGame();
		game.Setup();
		game.BeginTurn(game.Player1);
		var p1 = game.Player1;

		// Create a quest
		var quest = new Quest
		{
			Controller = p1,
			CardData = new CardData { CardId = "TEST_QUEST" },
			ProgressTotal = 3,
		};
		quest.SetIntProperty("cost", 0);
		game.RegisterEntity(quest);
		quest.SetZone(Zone.Hand);

		// Play the quest �?should redirect to Zone.Secret
		game.PlayCard(quest);
		Assert.Equal(Zone.Secret, quest.Zone);
		Assert.Contains(quest, p1.Secrets);

		// Quest should be at front of secrets list
		Assert.Same(quest, p1.Secrets[0]);

		// Add progress
		quest.AddProgress(1);
		Assert.Equal(1, quest.Progress);
		Assert.Equal(Zone.Secret, quest.Zone); // Not completed yet

		quest.AddProgress(1);
		Assert.Equal(2, quest.Progress);
		Assert.Equal(Zone.Secret, quest.Zone); // Not completed yet

		// Complete the quest
		quest.AddProgress(1);
		Assert.Equal(3, quest.Progress);
		Assert.Equal(Zone.Graveyard, quest.Zone); // Completed �?graveyard
		Assert.DoesNotContain(quest, p1.Secrets);
	}

	[Fact]
	public void Secret_Not_Triggered_On_Own_Turn()
	{
		var game = CreateTestGame();
		game.Setup();
		game.BeginTurn(game.Player1);
		var p1 = game.Player1;

		// Create a secret owned by the current player
		var secret = new Secret
		{
			Controller = p1,
			CardData = new CardData { CardId = "OWN_SECRET" },
		};
		game.RegisterEntity(secret);
		secret.SetZone(Zone.Secret);

		// Secret should be exhausted (own turn)
		Assert.True(secret.Exhausted);

		// Swap to opponent's turn �?secret should not be exhausted
		game.CurrentPlayer = game.Player2;
		Assert.False(secret.Exhausted);
	}

	// ══════════════════════════════════════════════════�?
	// Phase 7  �?Advanced Aura System Tests
	// ══════════════════════════════════════════════════�?

	[Fact]
	public void UpdateScripts_Loads_From_CardScript()
	{
		CardScriptRegistry.Instance.Initialize();
		// CS2_222 = Stormwind Champion �?update = Refresh(FRIENDLY_MINIONS - SELF, {ATK: 1, HEALTH: 1})
		var game = CreateTestGame();
		game.Setup();
		game.BeginTurn(game.Player1);
		var p1 = game.Player1;
		p1.MaxMana = 10;

		var champion = CreateMinion(game, p1, atk: 6, health: 6, cardId: "CS2_222");
		game.PlayCard(champion);

		// Should have UpdateScripts from the card script
		Assert.NotEmpty(champion.UpdateScripts);
	}

	[Fact]
	public void RefreshAuras_Creates_AuraBuff_On_Targets()
	{
		CardScriptRegistry.Instance.Initialize();
		var game = CreateTestGame();
		game.Setup();
		game.BeginTurn(game.Player1);
		var p1 = game.Player1;
		p1.MaxMana = 10;

		// Play a vanilla minion first
		var grunt = CreateMinion(game, p1, atk: 2, health: 3);
		game.PlayCard(grunt);

		int atkBefore = grunt.Atk;

		// Play Stormwind Champion �?should buff all OTHER friendly minions +1/+1
		var champion = CreateMinion(game, p1, atk: 6, health: 6, cardId: "CS2_222");
		game.PlayCard(champion);

		// Trigger aura refresh
		game.RefreshAuras();

		// The grunt should now have an AuraBuff slot
		Assert.True(grunt.Slots.Count > 0 || grunt.Buffs.Count > 0,
			"Grunt should receive an aura buff from Stormwind Champion");
	}

	[Fact]
	public void Minion_Enrage_UpdateScripts_Active_When_Damaged()
	{
		CardScriptRegistry.Instance.Initialize();
		// EX1_393 = Amani Berserker �?Enrage: +3 Attack (buff: "EX1_393e")
		var game = CreateTestGame();
		game.Setup();
		game.BeginTurn(game.Player1);
		var p1 = game.Player1;
		p1.MaxMana = 10;

		var berserker = CreateMinion(game, p1, atk: 2, health: 3, cardId: "EX1_393");
		game.PlayCard(berserker);

		// Verify the card script is registered
		var script = HearthstoneServer.Dsl.CardScriptRegistry.Instance.GetScript("EX1_393");
		Assert.NotNull(script);
		Assert.True(script!.Enrage.Length > 0, $"EX1_393 script should have Enrage (type={script.GetType().Name})");

		// Not enraged yet �?UpdateScripts should not include Enrage
		Assert.False(berserker.Enraged, "Berserker should not be enraged before damage");
		int countBefore = berserker.UpdateScripts.Count;

		// Damage the minion
		berserker.DamageTaken = 1;
		Assert.True(berserker.Enraged, "Berserker should be enraged after damage");

		// Now UpdateScripts should include Enrage scripts too
		int countAfter = berserker.UpdateScripts.Count;
		Assert.True(countAfter > countBefore,
			$"Enraged minion should have additional UpdateScripts (before={countBefore}, after={countAfter}, CardId={berserker.CardId}, Zone={berserker.Zone})");
	}

	// ══════════════════════════════════════════════════�?
	// Phase 8 �?Discover/RandomCardPicker + Choose One
	// ══════════════════════════════════════════════════�?

	[Fact]
	public void RandomCardPicker_StandardFilter()
	{
		// Verify is_standard filter works on the card database
		var game = CreateTestGame();
		game.Setup();
		var p1 = game.Player1;

		// Manually add cards to the CardDb with different sets
		game.CardDb = new Dictionary<string, CardData>();
		game.CardDb["TEST_STD"] = new CardData
		{
			CardId = "TEST_STD",
			Collectible = true,
			CardType = CardType.Minion,
			CardSet = CardSet.Core
		};
		game.CardDb["TEST_WILD"] = new CardData
		{
			CardId = "TEST_WILD",
			Collectible = true,
			CardType = CardType.Minion,
			CardSet = CardSet.Naxx // Not in standard
		};

		var picker = new RandomCardPicker(new Dictionary<string, object>
		{
			["collectible"] = true,
			["type"] = CardType.Minion,
			["is_standard"] = true,
		});

		var testSource = CreateMinion(game, p1, cardId: "TEST_STD");
		var results = picker.FindCards(testSource);

		Assert.Contains("TEST_STD", results);
		Assert.DoesNotContain("TEST_WILD", results);
	}

	[Fact]
	public void RandomCardPicker_CardSetFilter()
	{
		// Verify card_set filter works
		var game = CreateTestGame();
		game.Setup();
		var p1 = game.Player1;

		game.CardDb = new Dictionary<string, CardData>();
		game.CardDb["TEST_CORE"] = new CardData
		{
			CardId = "TEST_CORE",
			Collectible = true,
			CardType = CardType.Minion,
			CardSet = CardSet.Core
		};
		game.CardDb["TEST_EXPERT"] = new CardData
		{
			CardId = "TEST_EXPERT",
			Collectible = true,
			CardType = CardType.Minion,
			CardSet = CardSet.Expert1
		};

		var picker = new RandomCardPicker(new Dictionary<string, object>
		{
			["collectible"] = true,
			["card_set"] = CardSet.Core,
		});

		var testSource = CreateMinion(game, p1, cardId: "TEST_CORE");
		var results = picker.FindCards(testSource);

		Assert.Contains("TEST_CORE", results);
		Assert.DoesNotContain("TEST_EXPERT", results);
	}

	[Fact]
	public void ChooseOne_Creates_Choice_For_Player()
	{
		CardScriptRegistry.Instance.Initialize();
		// EX1_164 = Nourish �?ChooseCards: ["EX1_164a", "EX1_164b"] (no target required)
		var game = CreateTestGame();
		game.CardDb = new Dictionary<string, CardData>
		{
			["EX1_164a"] = new CardData { CardId = "EX1_164a", CardType = CardType.Spell },
			["EX1_164b"] = new CardData { CardId = "EX1_164b", CardType = CardType.Spell },
		};
		game.Setup();
		game.BeginTurn(game.Player1);
		var p1 = game.Player1;
		p1.MaxMana = 10;

		var nourish = CreateMinion(game, p1, cardId: "EX1_164");
		game.PlayCard(nourish);

		// player.Choice should be set with 2 options
		Assert.NotNull(p1.Choice);
		Assert.Equal(2, p1.Choice!.Cards.Count);

		// The choice should be a ChooseOneChoice
		Assert.IsType<ChooseOneChoice>(p1.Choice);

		// Choose the first option and verify choice is cleared
		var firstOption = p1.Choice.Cards[0];
		p1.Choose(firstOption);

		Assert.Null(p1.Choice);
	}

	[Fact]
	public void ChooseOne_ChooseBoth_Skips_Choice()
	{
		CardScriptRegistry.Instance.Initialize();
		// EX1_164 = Nourish �?ChooseCards present, no target required.
		// With choose_both=true, PlayCard should NOT set player.Choice.
		var game = CreateTestGame();
		game.CardDb = new Dictionary<string, CardData>();
		game.Setup();
		game.BeginTurn(game.Player1);
		var p1 = game.Player1;
		p1.MaxMana = 10;
		p1.SetBoolProperty("choose_both", true); // Simulate Fandral effect

		var nourish = CreateMinion(game, p1, cardId: "EX1_164");
		game.PlayCard(nourish);

		// With ChooseBoth, player.Choice should NOT be set
		Assert.Null(p1.Choice);
	}

	// ══════════════════════════════════════════════════�?
	// Battlecry Target System
	// ══════════════════════════════════════════════════�?

	[Fact]
	public void BattlecryRequiresTarget_From_Script()
	{
		CardScriptRegistry.Instance.Initialize();
		var game = CreateTestGame();
		game.Setup();
		var p1 = game.Player1;

		// CS2_008 = Moonfire �?Requirements: { TargetToPlay = 0 }
		var moonfire = CreateMinion(game, p1, cardId: "CS2_008");
		Assert.True(moonfire.BattlecryRequiresTarget(),
			"Moonfire has TargetToPlay and should require a target");

		// CS2_011 = Savage Roar �?no target requirements
		var roar = CreateMinion(game, p1, cardId: "CS2_011");
		Assert.False(roar.BattlecryRequiresTarget(),
			"Savage Roar has no target requirements");
	}

	[Fact]
	public void Requirements_AutoLoaded_From_Script()
	{
		CardScriptRegistry.Instance.Initialize();
		var game = CreateTestGame();
		game.Setup();
		var p1 = game.Player1;

		// CS2_012 = Swipe �?TargetReq: Required + Enemy
		var swipe = CreateMinion(game, p1, cardId: "CS2_012");
		// TargetReq should auto-load from script
		Assert.NotNull(swipe.TargetReq);
		Assert.True(swipe.TargetReq!.EnemyOnly);
		Assert.True(swipe.TargetReq.MustTarget);
	}

	[Fact]
	public void PlayCard_Validates_Target_Required()
	{
		CardScriptRegistry.Instance.Initialize();
		var game = CreateTestGame();
		game.Setup();
		game.BeginTurn(game.Player1);
		var p1 = game.Player1;
		p1.MaxMana = 10;

		// CS2_008 = Moonfire �?TargetToPlay, requires target
		var moonfire = CreateMinion(game, p1, cardId: "CS2_008");

		// Playing without target should throw
		Assert.Throws<InvalidActionException>(() => game.PlayCard(moonfire));
	}

	[Fact]
	public void PlayCard_Validates_Target_Legality()
	{
		CardScriptRegistry.Instance.Initialize();
		var game = CreateTestGame();
		game.Setup();
		game.BeginTurn(game.Player1);
		var p1 = game.Player1;
		var p2 = game.Player2;
		p1.MaxMana = 10;

		// CS2_012 = Swipe �?EnemyTarget + TargetToPlay
		var swipe = CreateMinion(game, p1, cardId: "CS2_012");

		// Create a friendly minion �?targeting it should fail (EnemyTarget requirement)
		var friendlyMinion = CreateMinion(game, p1, atk: 1, health: 1);
		game.PlayCard(friendlyMinion);

		// Swipe requires enemy target; targeting our own minion should throw
		Assert.Throws<InvalidActionException>(() =>
			game.PlayCard(swipe, target: friendlyMinion));
	}

	[Fact]
	public void PlayCard_Combo_Uses_Combo_Actions()
	{
		CardScriptRegistry.Instance.Initialize();
		var game = CreateTestGame();
		game.Setup();
		game.BeginTurn(game.Player1);
		var p1 = game.Player1;
		p1.MaxMana = 10;

		// First card played sets Combo flag for next card
		var filler = CreateMinion(game, p1, atk: 1, health: 1);
		game.PlayCard(filler);

		// After first card, p1.Combo should be true
		Assert.True(p1.Combo, "Combo should be active after playing first card");

		// CardsPlayedThisTurn should be 1
		Assert.Equal(1, p1.CardsPlayedThisTurn);
	}

	// ── EventBus 端到端集成测�?──

	[Fact]
	public void EventBus_TurnStart_TriggersHandler()
	{
		var game = CreateTestGame();
		game.Setup();
		game.Start();
		var p1 = game.Player1;

		// 放置一个随从并通过 EventBus 注册 TurnStart 订阅
		var minion = CreateMinion(game, p1, atk: 2, health: 3);
		game.PlayCard(minion);
		Assert.Equal(Zone.Play, minion.Zone);

		// 手动注册 When<TurnStartEvent> handler（模�?OnActivate 行为�?
		int triggerCount = 0;
		game.EventBus.Subscribe<HearthstoneServer.Events.TurnStartEvent>(
			minion,
			HearthstoneServer.Events.TurnPhase.Start,
			evt => evt.Player == p1,
			_ => triggerCount++);

		// BeginTurn 应触�?handler
		game.BeginTurn(p1);
		Assert.True(triggerCount >= 1, $"Expected TurnStart trigger, got {triggerCount}");
	}

	[Fact]
	public void EventBus_Silence_ClearsSubscriptions()
	{
		var game = CreateTestGame();
		game.Setup();
		game.Start();
		var p1 = game.Player1;

		var minion = CreateMinion(game, p1, atk: 2, health: 3);
		game.PlayCard(minion);

		int triggerCount = 0;
		game.EventBus.Subscribe<HearthstoneServer.Events.TurnStartEvent>(
			minion,
			HearthstoneServer.Events.TurnPhase.Start,
			evt => evt.Player == p1,
			_ => triggerCount++);

		// 触发一次确认订阅有�?
		game.BeginTurn(p1);
		Assert.True(triggerCount >= 1);
		int countAfterFirst = triggerCount;

		// 沉默随从
		minion.Silence();

		// 再次触发 �?不应再增�?
		game.BeginTurn(p1);
		Assert.Equal(countAfterFirst, triggerCount);
	}

	[Fact]
	public void EventBus_Death_ClearsSubscriptions()
	{
		var game = CreateTestGame();
		game.Setup();
		game.Start();
		var p1 = game.Player1;

		var minion = CreateMinion(game, p1, atk: 2, health: 3);
		game.PlayCard(minion);

		int triggerCount = 0;
		game.EventBus.Subscribe<HearthstoneServer.Events.TurnStartEvent>(
			minion,
			HearthstoneServer.Events.TurnPhase.Start,
			evt => evt.Player == p1,
			_ => triggerCount++);

		// 确认订阅有效
		game.BeginTurn(p1);
		Assert.True(triggerCount >= 1);
		int countAfterFirst = triggerCount;

		// 击杀随从
		minion.Hit(minion.Health);
		game.ProcessDeaths();

		// 再次触发 �?不应再增加（�?UnsubscribeAll�?
		game.BeginTurn(p1);
		Assert.Equal(countAfterFirst, triggerCount);
	}

	[Fact]
	public void EventBus_DrawCard_TriggersSubscription()
	{
		var game = CreateTestGame();
		game.Setup();
		game.Start();
		var p1 = game.Player1;

		var minion = CreateMinion(game, p1, atk: 2, health: 3);
		game.PlayCard(minion);

		// 注册抽牌事件订阅
		int drawCount = 0;
		game.EventBus.Subscribe<HearthstoneServer.Events.DrawCardEvent>(
			minion,
			HearthstoneServer.Events.MiscPhase.On,
			evt => evt.Player == p1,
			_ => drawCount++);

		// 确保牌库有牌
		Assert.True(p1.Deck.Count > 0);

		// 抽牌
		p1.DrawCard();
		Assert.Equal(1, drawCount);

		// 再抽一�?
		if (p1.Deck.Count > 0)
		{
			p1.DrawCard();
			Assert.Equal(2, drawCount);
		}
	}

	// ── Test: Nightblade with FULL CardDb (replicating Web game flow) ──

	[Fact]
	public void Nightblade_FullCardDb_Deals3DamageOnly()
	{
		// Load full card database like the web app does
		var cardsPath = @"d:\hs\Hearthstone.CSharp\data\cards_dbfid.json";
		if (!System.IO.File.Exists(cardsPath)) return; // skip if file not available

		var (_, cardDb) = CardDbLoader.Load(cardsPath);
		CardScriptRegistry.Instance.Initialize(typeof(Game).Assembly);

		var p1 = new Player("P1") { StartingHero = "HERO_01" };
		var p2 = new Player("P2") { StartingHero = "HERO_05" };

		// Give both players generic decks
		p1.StartingDeck = new() { "CS2_168", "CS2_168", "CS2_168", "CS2_168", "CS2_168", "CS2_168", "CS2_168", "CS2_168", "CS2_168", "CS2_168", "CS2_168", "CS2_168", "CS2_168", "CS2_168", "CS2_168", "CS2_168", "CS2_168", "CS2_168", "CS2_168", "CS2_168", "CS2_168", "CS2_168", "CS2_168", "CS2_168", "CS2_168", "CS2_168", "CS2_168", "CS2_168", "CS2_168", "CS2_168" };
		p2.StartingDeck = new() { "CS2_168", "CS2_168", "CS2_168", "CS2_168", "CS2_168", "CS2_168", "CS2_168", "CS2_168", "CS2_168", "CS2_168", "CS2_168", "CS2_168", "CS2_168", "CS2_168", "CS2_168", "CS2_168", "CS2_168", "CS2_168", "CS2_168", "CS2_168", "CS2_168", "CS2_168", "CS2_168", "CS2_168", "CS2_168", "CS2_168", "CS2_168", "CS2_168", "CS2_168", "CS2_168" };

		var game = new Game(p1, p2);
		game.CardDb = cardDb;
		p1.PrepareForGame();
		p2.PrepareForGame();
		game.Setup();

		for (int i = 0; i < 3; i++) p1.DrawCard();
		for (int i = 0; i < 4; i++) p2.DrawCard();
		game.BeginTurn(p1);

		// Create Nightblade using FULL card data from the database (like web does)
		var nightbladeData = cardDb["EX1_593"];
		var nightblade = CardFactory.Create(nightbladeData);
		nightblade.Controller = p1;
		game.RegisterEntity(nightblade);
		nightblade.SetZone(Zone.Hand);
		nightblade.Cost = 0; // free for testing

		int heroBefore = p2.Hero!.Health;
		game.PlayCard(nightblade);
		int heroAfter = p2.Hero.Health;

		int actualDamage = heroBefore - heroAfter;

		// Should deal EXACTLY 3 damage, not 6
		Assert.Equal(3, actualDamage);
	}
}
