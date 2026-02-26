using HearthstoneServer.Dsl;
using HearthstoneServer.Entities;
using HearthstoneServer.Managers;
using HearthstoneServer;
using HearthstoneServer.LoadCardIntoDbf;
using Xunit;

namespace Hearthstone.Tests;

public class ActionTests
{
	private class MockEntity : Minion
	{
		public MockEntity(int id, int controllerId, CardType type = CardType.Minion)
		{
			EntityId = id;
			Type = type;
			Tags = new CardManager(this);
			Tags[GameTag.Controller] = controllerId;
			Zone = Zone.Play;
		}
	}

	private (Game game, GameManager gm) CreateGame()
	{
		var game = new Game();
		var gm = new GameManager(game);
		return (game, gm);
	}

	// ©¤©¤©¤ DealDamage tests ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤

	[Fact]
	public void Hit_ReducesHealth()
	{
		var (game, gm) = CreateGame();
		var src = new MockEntity(1, 1, CardType.Hero);
		gm.NewEntity(src);

		var m = new MockEntity(2, 2);
		m.MaxHealth = 5;
		gm.NewEntity(m);

		var hit = new DealDamage(m, 3);
		var result = hit.Trigger(src);

		Assert.Single(result);
		Assert.Equal(3, result[0]);
		Assert.Equal(2, m.Health);
	}

	[Fact]
	public void Hit_ZeroDamage_DoesNothing()
	{
		var (game, gm) = CreateGame();
		var src = new MockEntity(1, 1);
		var target = new MockEntity(2, 2);
		target.MaxHealth = 5;

		var hit = new DealDamage(target, 0);
		hit.Trigger(src);
		Assert.Equal(5, target.Health);
	}

	// ©¤©¤©¤ RestoreHealth tests ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤

	[Fact]
	public void Heal_RestoresHealth()
	{
		var (game, gm) = CreateGame();
		var src = new MockEntity(1, 1);
		var target = new MockEntity(2, 2);
		target.MaxHealth = 5;
		target.DamageTaken = 2; // Health = 5 - 2 = 3

		var heal = new RestoreHealth(target, 2);
		var result = heal.Trigger(src);

		Assert.Equal(2, result[0]);
		Assert.Equal(5, target.Health);
	}

	[Fact]
	public void Heal_DoesNotExceedMaxHealth()
	{
		var src = new MockEntity(1, 1);
		var target = new MockEntity(2, 2);
		target.MaxHealth = 5;
		target.DamageTaken = 1; // Health = 5 - 1 = 4

		var heal = new RestoreHealth(target, 10);
		heal.Trigger(src);
		Assert.Equal(5, target.Health);
	}

	// â”€â”€â”€ Destroy tests â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

	[Fact]
	public void Destroy_MarksForDeath()
	{
		var src = new MockEntity(1, 1);
		var target = new MockEntity(2, 2);

		var destroy = new Destroy(target);
		destroy.Trigger(src);

		Assert.True(target.ToBeDestroyed);
	}

	// â”€â”€â”€ Silence tests â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

	[Fact]
	public void Silence_ClearsAttributes()
	{
		var src = new MockEntity(1, 1);
		var target = new MockEntity(2, 2);
		target.SetBoolProperty("taunt", true);
		target.SetBoolProperty("divine_shield", true);
		target.SetBoolProperty("stealth", true);

		var silence = new Silence(target);
		silence.Trigger(src);

		Assert.False(target.GetBoolProperty("taunt"));
		Assert.False(target.GetBoolProperty("divine_shield"));
		Assert.False(target.GetBoolProperty("stealth"));
		Assert.True(target.GetBoolProperty("silenced"));
	}

	[Fact]
	public void Silence_IgnoresNonMinions()
	{
		var src = new MockEntity(1, 1);
		var hero = new MockEntity(2, 2, CardType.Hero);
		hero.SetBoolProperty("taunt", true);

		var silence = new Silence(hero);
		silence.Trigger(src);

		// Hero should not be silenced
		Assert.True(hero.GetBoolProperty("taunt"));
	}

	// â”€â”€â”€ Freeze / Bounce / Discard tests â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

	[Fact]
	public void Freeze_SetsFrozen()
	{
		var src = new MockEntity(1, 1);
		var target = new MockEntity(2, 2);

		new Freeze(target).Trigger(src);
		Assert.True(target.GetBoolProperty("frozen"));
	}

	[Fact]
	public void Bounce_SetsZoneToHand()
	{
		var src = new MockEntity(1, 1);
		var target = new MockEntity(2, 2);
		target.Zone = Zone.Play;

		new ReturnToHand(target).Trigger(src);
		Assert.Equal(Zone.Hand, target.Zone);
	}

	[Fact]
	public void Discard_SetsZoneToGraveyard()
	{
		var (game, gm, p1) = CreateGameWithPlayer();

		var card = new MockEntity(100, 1);
		card.MaxHealth = 3;
		card.Controller = p1;
		gm.NewEntity(card);
		card.SetZone(Zone.Hand, p1);

		new Discard(card).Do(card, card);
		Assert.Contains(card, p1.Graveyard);
	}

	// â”€â”€â”€ GainArmor / GainMana tests â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

	[Fact]
	public void GainArmor_IncreasesArmor()
	{
		var src = new MockEntity(1, 1);
		var hero = new MockEntity(2, 2, CardType.Hero);
		hero.SetIntProperty("armor", 2);

		new GainArmor(hero, 5).Trigger(src);
		Assert.Equal(7, hero.GetIntProperty("armor"));
	}

	[Fact]
	public void GainMana_CapsAt10()
	{
		var src = new MockEntity(1, 1);
		var player = new MockEntity(2, 2);
		player.SetIntProperty("max_mana", 8);

		new GainMana(player, 5).Trigger(src);
		Assert.Equal(10, player.GetIntProperty("max_mana"));
	}


	// â”€â”€â”€ Repeat (multiply) test â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

	[Fact]
	public void Repeat_HitsMultipleTimes()
	{
		var src = new MockEntity(1, 1);
		var target = new MockEntity(2, 2);
		target.MaxHealth = 10;

		var hit = new DealDamage(target, 2).Repeat(3);
		var results = hit.Trigger(src);

		Assert.Equal(3, results.Count);
		Assert.Equal(4, target.Health); // 10 - 2*3 = 4
	}

	// â”€â”€â”€ LazyNum as amount test â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

	[Fact]
	public void Hit_WithLazyNum()
	{
		var src = new MockEntity(1, 1);
		var target = new MockEntity(2, 2);
		target.MaxHealth = 10;

		var lazyAmount = new IntLazyNum(4);
		var hit = new DealDamage(target, lazyAmount);
		hit.Trigger(src);

		Assert.Equal(6, target.Health);
	}

	// ©¤©¤©¤ Enchant tests ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤

	[Fact]
	public void Buff_AddsEnchantment_ModifiesAtk()
	{
		var (game, gm) = CreateGame();
		var src = new MockEntity(1, 1);
		gm.NewEntity(src);

		var target = new MockEntity(2, 1);
		target.MaxHealth = 5;
		gm.NewEntity(target);

		// Register enchant script with +3 ATK
		CardScriptRegistry.Instance.Register("TEST_BUFF_ATK", new TestAtkBuffScript());

		var buff = new Enchant(S.SELF, "TEST_BUFF_ATK");
		buff.Do(src, target);

		Assert.Equal(3, target.Atk); // Base 0 + 3 from buff
		Assert.Single(target.Buffs);
	}

	[Fact]
	public void Buff_AddsEnchantment_ModifiesHealth()
	{
		var (game, gm) = CreateGame();
		var src = new MockEntity(1, 1);
		gm.NewEntity(src);

		var target = new MockEntity(2, 1);
		target.MaxHealth = 5;
		gm.NewEntity(target);

		CardScriptRegistry.Instance.Register("TEST_BUFF_HP", new TestHpBuffScript());

		var buff = new Enchant(S.SELF, "TEST_BUFF_HP");
		buff.Do(src, target);

		// max_health: base 5 + 2 from buff = 7
		Assert.Equal(7, target.MaxHealth);
		Assert.Equal(7, target.Health); // No damage taken
	}

	[Fact]
	public void Buff_MultipleBuff_Stack()
	{
		var (game, gm) = CreateGame();
		var src = new MockEntity(1, 1);
		gm.NewEntity(src);

		var target = new MockEntity(2, 1);
		target.MaxHealth = 5;
		gm.NewEntity(target);

		CardScriptRegistry.Instance.Register("TEST_BUFF_ATK", new TestAtkBuffScript());

		// Apply same buff twice â†?should stack
		new Enchant(S.SELF, "TEST_BUFF_ATK").Do(src, target);
		new Enchant(S.SELF, "TEST_BUFF_ATK").Do(src, target);

		Assert.Equal(6, target.Atk); // 0 + 3 + 3
		Assert.Equal(2, target.Buffs.Count);
	}

	[Fact]
	public void Silence_RemovesAllBuffs()
	{
		var (game, gm) = CreateGame();
		var src = new MockEntity(1, 1);
		gm.NewEntity(src);

		var target = new MockEntity(2, 1);
		target.MaxHealth = 5;
		gm.NewEntity(target);

		CardScriptRegistry.Instance.Register("TEST_BUFF_ATK", new TestAtkBuffScript());
		new Enchant(S.SELF, "TEST_BUFF_ATK").Do(src, target);
		Assert.Equal(3, target.Atk);

		// Silence should clear buffs
		new Silence(S.SELF).Do(src, target);
		Assert.Equal(0, target.Atk); // Back to base
		Assert.Empty(target.Buffs);
	}

	// â”€â”€â”€ Test enchant scripts â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

	private class TestAtkBuffScript : CardScriptBase
	{
		public override Dictionary<GameTag, object>? Tags => new() { [GameTag.Atk] = 3 };
	}

	private class TestHpBuffScript : CardScriptBase
	{
		public override Dictionary<GameTag, object>? Tags => new() { [GameTag.Health] = 2 };
	}

	// ©¤©¤©¤ DrawCard tests ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤

	private (Game game, GameManager gm, Player p1) CreateGameWithPlayer()
	{
		var p1 = new Player("TestPlayer1");
		var p2 = new Player("TestPlayer2");
		var game = new Game(p1, p2, seed: 42);
		var gm = new GameManager(game);
		return (game, gm, p1);
	}

	[Fact]
	public void Draw_DrawsCardFromDeck()
	{
		var (game, gm, p1) = CreateGameWithPlayer();

		// Put a card in player1's deck
		var deckCard = new MockEntity(100, 1);
		deckCard.MaxHealth = 3;
		deckCard.Controller = p1;
		gm.NewEntity(deckCard);
		p1.Deck.Add(deckCard);

		int deckBefore = p1.Deck.Count;
		int handBefore = p1.Hand.Count;

		// Execute Draw action targeting player1
		var draw = new DrawCard(S.CONTROLLER);
		draw.Do(p1, p1);

		Assert.Equal(deckBefore - 1, p1.Deck.Count);
		Assert.Equal(handBefore + 1, p1.Hand.Count);
		Assert.Contains(deckCard, p1.Hand);
	}

	[Fact]
	public void Draw_Fatigue_DamagesHero()
	{
		var (game, gm, p1) = CreateGameWithPlayer();
		p1.Hero = new Hero();
		p1.Hero.MaxHealth = 30;
		p1.Hero.Controller = p1;
		gm.NewEntity(p1.Hero);

		// Empty deck â†?fatigue
		p1.Deck.Clear();
		Assert.Equal(0, p1.FatigueCounter);

		var draw = new DrawCard(S.CONTROLLER);
		draw.Do(p1, p1);

		Assert.Equal(1, p1.FatigueCounter);
		Assert.Equal(29, p1.Hero.Health); // 30 - 1 fatigue

		draw.Do(p1, p1);
		Assert.Equal(2, p1.FatigueCounter);
		Assert.Equal(27, p1.Hero.Health); // 30 - 1 - 2 = 27
	}

	[Fact]
	public void Draw_Mill_FullHand()
	{
		var (game, gm, p1) = CreateGameWithPlayer();

		// Fill hand to max
		for (int i = 0; i < p1.MaxHandSize; i++)
		{
			var filler = new MockEntity(200 + i, 1);
			filler.MaxHealth = 1;
			filler.Controller = p1;
			gm.NewEntity(filler);
			p1.Hand.Add(filler);
		}

		// Put a card in deck
		var deckCard = new MockEntity(300, 1);
		deckCard.MaxHealth = 3;
		deckCard.Controller = p1;
		gm.NewEntity(deckCard);
		p1.Deck.Add(deckCard);

		int handBefore = p1.Hand.Count;

		var draw = new DrawCard(S.CONTROLLER);
		draw.Do(p1, p1);

		// Card should be milled â€?hand unchanged, deck empty, graveyard has the card
		Assert.Equal(handBefore, p1.Hand.Count);
		Assert.Empty(p1.Deck);
		Assert.DoesNotContain(deckCard, p1.Graveyard); // milled cards go to SetAside
	}

	// â”€â”€â”€ Summon tests â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

	[Fact]
	public void Summon_AddsToField()
	{
		var (game, gm, p1) = CreateGameWithPlayer();

		var minion = new MockEntity(100, 1);
		minion.MaxHealth = 3;
		minion.Controller = p1;
		gm.NewEntity(minion);

		int fieldBefore = p1.Field.Count;

		// Summon.Do expects target=Player, args[0]=entity
		var summon = new Summon(S.CONTROLLER, S.SELF);
		summon.Do(p1, p1, minion);

		Assert.Equal(fieldBefore + 1, p1.Field.Count);
		Assert.Contains(minion, p1.Field);
	}

	[Fact]
	public void Summon_BoardFull_DoesNotAdd()
	{
		var (game, gm, p1) = CreateGameWithPlayer();

		// Fill field to max (7)
		for (int i = 0; i < 7; i++)
		{
			var filler = new MockEntity(200 + i, 1);
			filler.MaxHealth = 1;
			filler.Controller = p1;
			gm.NewEntity(filler);
			p1.Field.Add(filler);
		}

		var minion = new MockEntity(300, 1);
		minion.MaxHealth = 3;
		minion.Controller = p1;
		gm.NewEntity(minion);

		var summon = new Summon(S.CONTROLLER, S.SELF);
		summon.Do(p1, p1, minion);

		// Board full â€?should not be added
		Assert.Equal(7, p1.Field.Count);
		Assert.DoesNotContain(minion, p1.Field);
	}

	// â”€â”€â”€ Combat tests â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

	/// <summary>Helper: place a minion on the field ready to attack.</summary>
	private Minion PlaceMinion(GameManager gm, Player owner, int atk, int health, int entityId)
	{
		var m = new Minion { Atk = atk, MaxHealth = health, Controller = owner };
		m.EntityId = entityId;
		m.TurnsInPlay = 1; // not asleep
		gm.NewEntity(m);
		m.SetZone(Zone.Play, owner); // sets Zone property + adds to Field
		return m;
	}

	[Fact]
	public void Attack_MutualDamage()
	{
		var (game, gm, p1) = CreateGameWithPlayer();
		var p2 = game.Player2;
		game.CurrentPlayer = p1;

		var attacker = PlaceMinion(gm, p1, atk: 3, health: 4, entityId: 100);
		var defender = PlaceMinion(gm, p2, atk: 2, health: 5, entityId: 101);

		game.Attack(attacker, defender);

		Assert.Equal(2, attacker.Health); // 4 - 2
		Assert.Equal(2, defender.Health); // 5 - 3
	}

	[Fact]
	public void Attack_KillsMinion()
	{
		var (game, gm, p1) = CreateGameWithPlayer();
		var p2 = game.Player2;
		game.CurrentPlayer = p1;

		var attacker = PlaceMinion(gm, p1, atk: 5, health: 3, entityId: 100);
		var defender = PlaceMinion(gm, p2, atk: 1, health: 3, entityId: 101);

		game.Attack(attacker, defender);

		Assert.True(defender.Dead);            // 3 - 5 = dead
		Assert.Equal(2, attacker.Health);       // 3 - 1
		Assert.False(attacker.Dead);
	}

	[Fact]
	public void Attack_Taunt_MustTarget()
	{
		var (game, gm, p1) = CreateGameWithPlayer();
		var p2 = game.Player2;
		game.CurrentPlayer = p1;

		var attacker = PlaceMinion(gm, p1, atk: 2, health: 3, entityId: 100);
		var tauntMinion = PlaceMinion(gm, p2, atk: 1, health: 4, entityId: 101);
		tauntMinion.Taunt = true;
		var otherMinion = PlaceMinion(gm, p2, atk: 3, health: 2, entityId: 102);

		var targets = attacker.AttackTargets;

		// Should only be able to attack taunt
		Assert.Contains(tauntMinion, targets);
		Assert.DoesNotContain(otherMinion, targets);
	}

	[Fact]
	public void Attack_Windfury_AttacksTwice()
	{
		var (game, gm, p1) = CreateGameWithPlayer();
		var p2 = game.Player2;
		game.CurrentPlayer = p1;

		var attacker = PlaceMinion(gm, p1, atk: 2, health: 10, entityId: 100);
		attacker.Windfury = true;
		var def1 = PlaceMinion(gm, p2, atk: 1, health: 5, entityId: 101);
		var def2 = PlaceMinion(gm, p2, atk: 1, health: 5, entityId: 102);

		// First attack
		Assert.True(attacker.CanAttack(def1));
		game.Attack(attacker, def1);

		// After 1 attack with windfury, can still attack
		Assert.False(attacker.Exhausted);
		Assert.Equal(1, attacker.NumAttacks);

		// Second attack
		Assert.True(attacker.CanAttack(def2));
		game.Attack(attacker, def2);

		// Now exhausted
		Assert.True(attacker.Exhausted);
		Assert.Equal(2, attacker.NumAttacks);
	}

	[Fact]
	public void Attack_Stealth_RemovedAfterAttack()
	{
		var (game, gm, p1) = CreateGameWithPlayer();
		var p2 = game.Player2;
		game.CurrentPlayer = p1;

		var attacker = PlaceMinion(gm, p1, atk: 3, health: 4, entityId: 100);
		attacker.Stealth = true;
		var defender = PlaceMinion(gm, p2, atk: 1, health: 5, entityId: 101);

		Assert.True(attacker.Stealth);

		game.Attack(attacker, defender);

		Assert.False(attacker.Stealth); // stealth removed after attacking
	}

	[Fact]
	public void Attack_Exhausted_CantAttack()
	{
		var (game, gm, p1) = CreateGameWithPlayer();
		var p2 = game.Player2;
		game.CurrentPlayer = p1;

		var attacker = PlaceMinion(gm, p1, atk: 2, health: 10, entityId: 100);
		var defender = PlaceMinion(gm, p2, atk: 1, health: 10, entityId: 101);

		// First attack
		game.Attack(attacker, defender);
		Assert.Equal(1, attacker.NumAttacks);

		// No windfury â€?exhausted after 1 attack
		Assert.True(attacker.Exhausted);
		Assert.False(attacker.CanAttack(defender));
	}

	// â”€â”€â”€ Morph tests â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

	[Fact]
	public void Morph_TransformsMinion()
	{
		var (game, gm, p1) = CreateGameWithPlayer();

		// Setup CardDb with a morph target card
		game.CardDb = new Dictionary<string, Card>
		{
			["CS2_tk1"] = new Card
			{
				CardId = "CS2_tk1",
				CardType = CardType.Minion,
				Health = 1,
				Attack = 1,
				Cost = 0
			}
		};

		// Place a 5/5 on the field
		var target = PlaceMinion(gm, p1, atk: 5, health: 5, entityId: 100);

		var morph = new Transform(target, "CS2_tk1");
		var result = morph.Do(target, target, "CS2_tk1");

		Assert.NotNull(result);
		var replacement = result as Minion;
		Assert.NotNull(replacement);
		Assert.Equal(1, replacement!.Atk);
		Assert.Equal(1, replacement.MaxHealth);
		Assert.Contains(replacement, p1.Field);
		Assert.DoesNotContain(target, p1.Field); // old entity removed
	}

	// â”€â”€â”€ Steal tests â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

	[Fact]
	public void Steal_TransfersControlToSource()
	{
		var (game, gm, p1) = CreateGameWithPlayer();
		var p2 = game.Player2;

		// Attacker on p1's field â€?will be the source
		var source = PlaceMinion(gm, p1, atk: 2, health: 3, entityId: 100);

		// Enemy minion on p2's field
		var enemy = PlaceMinion(gm, p2, atk: 3, health: 4, entityId: 101);
		Assert.Equal(p2, enemy.Controller);

		var steal = new TakeControl(enemy);
		var result = steal.Do(source, enemy);

		Assert.NotNull(result);
		Assert.Equal(p1, enemy.Controller); // controller changed
		Assert.Contains(enemy, p1.Field);    // moved to p1's field
		Assert.DoesNotContain(enemy, p2.Field); // removed from p2's field
	}

	[Fact]
	public void Steal_BoardFull_Fails()
	{
		var (game, gm, p1) = CreateGameWithPlayer();
		var p2 = game.Player2;

		// Fill p1's board (7 minions)
		for (int i = 0; i < 7; i++)
			PlaceMinion(gm, p1, atk: 1, health: 1, entityId: 200 + i);

		var source = p1.Field[0] as Minion;
		var enemy = PlaceMinion(gm, p2, atk: 3, health: 4, entityId: 101);

		var steal = new TakeControl(enemy);
		var result = steal.Do(source!, enemy);

		Assert.Null(result); // can't steal â€?board full
		Assert.Equal(p2, enemy.Controller); // still owned by p2
	}

	// â”€â”€â”€ Give tests â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

	[Fact]
	public void Give_CreatesCardInHand()
	{
		var (game, gm, p1) = CreateGameWithPlayer();

		game.CardDb = new Dictionary<string, Card>
		{
			["EX1_015"] = new Card
			{
				CardId = "EX1_015",
				CardType = CardType.Minion,
				Health = 1,
				Attack = 1,
				Cost = 2
			}
		};

		int handBefore = p1.Hand.Count;

		var give = new AddToHand(p1, "EX1_015");
		var result = give.Do(p1, p1, "EX1_015");

		Assert.NotNull(result);
		Assert.Equal(handBefore + 1, p1.Hand.Count);
	}

	// â”€â”€â”€ Shuffle tests â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

	[Fact]
	public void Shuffle_AddsCardToDeck()
	{
		var (game, gm, p1) = CreateGameWithPlayer();

		game.CardDb = new Dictionary<string, Card>
		{
			["EX1_015"] = new Card
			{
				CardId = "EX1_015",
				CardType = CardType.Minion,
				Health = 1,
				Attack = 1,
				Cost = 2
			}
		};

		int deckBefore = p1.Deck.Count;

		var shuffle = new ShuffleIntoDeck(p1, "EX1_015");
		var result = shuffle.Do(p1, p1, "EX1_015");

		Assert.NotNull(result);
		Assert.Equal(deckBefore + 1, p1.Deck.Count);
	}

	// â”€â”€â”€ Discard tests â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

	[Fact]
	public void Discard_MovesToGraveyard()
	{
		var (game, gm, p1) = CreateGameWithPlayer();

		// Put a card in hand
		var card = new MockEntity(100, 1);
		card.MaxHealth = 3;
		card.Controller = p1;
		gm.NewEntity(card);
		card.SetZone(Zone.Hand, p1);

		Assert.Contains(card, p1.Hand);

		var discard = new Discard(card);
		discard.Do(card, card);

		Assert.DoesNotContain(card, p1.Hand);
		Assert.Contains(card, p1.Graveyard);
	}

	// â”€â”€â”€ Phase 3: Game Flow tests â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

	/// <summary>Helper: create a game with heroes and decks, ready for Start().</summary>
	private (Game game, Player p1, Player p2) CreateGameForFlowTests()
	{
		var p1 = new Player("P1");
		p1.Hero = new Hero { MaxHealth = 30 };
		p1.Hero.Power = new HeroPower { Controller = p1 };
		p1.Hero.Power.Cost = 2;
		var p2 = new Player("P2");
		p2.Hero = new Hero { MaxHealth = 30 };
		p2.Hero.Power = new HeroPower { Controller = p2 };
		p2.Hero.Power.Cost = 2;

		for (int i = 0; i < 15; i++)
		{
			p1.Deck.Add(new Minion { MaxHealth = 1, Atk = 1 });
			p2.Deck.Add(new Minion { MaxHealth = 1, Atk = 1 });
		}

		var game = new Game(p1, p2, seed: 42);
		return (game, p1, p2);
	}

	[Fact]
	public void UseHeroPower_PaysManaAndActivates()
	{
		var (game, _, _) = CreateGameForFlowTests();
		game.Setup();
		var p1 = game.Player1; // may be swapped by PickFirstPlayer
		p1.MaxMana = 3;
		game.CurrentPlayer = p1;

		Assert.Equal(3, p1.Mana);
		Assert.True(p1.Hero!.Power!.IsUsable);

		game.UseHeroPower(p1);

		Assert.Equal(1, p1.Mana);
		Assert.Equal(1, p1.Hero.Power.ActivationsThisTurn);
		Assert.False(p1.Hero.Power.IsUsable);
	}

	[Fact]
	public void UseHeroPower_NotPlayable_Throws()
	{
		var (game, _, _) = CreateGameForFlowTests();
		game.Setup();
		var p1 = game.Player1;
		p1.MaxMana = 1;
		game.CurrentPlayer = p1;

		Assert.False(p1.Hero!.Power!.IsUsable);
		Assert.Throws<InvalidActionException>(() => game.UseHeroPower(p1));
	}

	[Fact]
	public void InitialDraw_CorrectCardCounts()
	{
		var (game, _, _) = CreateGameForFlowTests();
		game.Setup();

		var first = game.Player1;
		var second = game.Player2;

		Assert.Empty(first.Hand);
		Assert.Empty(second.Hand);

		game.InitialDraw();

		// First player draws 3, second draws 4
		Assert.Equal(3, first.Hand.Count);
		Assert.Equal(4, second.Hand.Count);
	}

	[Fact]
	public void Mulligan_ReplacesCards()
	{
		var (game, _, _) = CreateGameForFlowTests();
		game.Setup();
		game.InitialDraw();

		var first = game.Player1;

		Assert.Equal(3, first.Hand.Count);
		var toReplace = new List<BaseEntity> { first.Hand[0], first.Hand[1] };
		int deckSizeBefore = first.Deck.Count;

		game.Mulligan(first, toReplace);

		Assert.Equal(3, first.Hand.Count);
		Assert.Equal(deckSizeBefore, first.Deck.Count);
	}

	[Fact]
	public void MultiTurn_FullCycle()
	{
		var (game, _, _) = CreateGameForFlowTests();
		game.Start();

		var first = game.Player1;
		var second = game.Player2;

		// Turn 1: first player's turn
		Assert.Equal(1, game.Turn);
		Assert.Same(first, game.CurrentPlayer);
		Assert.Equal(1, first.MaxMana);

		// first has 3 (initial) + 1 (BeginTurn) = 4 cards
		Assert.Equal(4, first.Hand.Count);
		// second has 4 (initial) + 1 (Coin) = 5 cards
		Assert.Equal(5, second.Hand.Count);

		// hero power costs 2, only 1 mana â†?fails
		Assert.Throws<InvalidActionException>(() => game.UseHeroPower(first));

		// End turn â†?second's turn
		game.EndTurn();
		Assert.Equal(2, game.Turn);
		Assert.Same(second, game.CurrentPlayer);
		Assert.Equal(1, second.MaxMana);

		// second drew 1 card at BeginTurn
		Assert.Equal(6, second.Hand.Count);

		// End turn â†?first's turn again
		game.EndTurn();
		Assert.Equal(3, game.Turn);
		Assert.Same(first, game.CurrentPlayer);
		Assert.Equal(2, first.MaxMana);

		// Now first has 2 mana, can use hero power
		game.UseHeroPower(first);
		Assert.Equal(0, first.Mana);
		Assert.Equal(1, first.Hero!.Power!.ActivationsThisTurn);
	}

	// â”€â”€â”€ Event System tests â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

	/// <summary>Helper: a TargetedAction that counts how many times it was triggered.</summary>
	private class CounterAction : TargetedAction
	{
		public int Count { get; private set; }
		public CounterAction(object target) : base(target) { }
		public override object? Do(BaseEntity source, BaseEntity target, params object[] args)
		{
			Count++;
			return null;
		}
	}

}

