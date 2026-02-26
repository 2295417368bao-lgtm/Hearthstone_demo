using HearthstoneServer;
using HearthstoneServer.Dsl;
using HearthstoneServer.Entities;
using HearthstoneServer.LoadCardIntoDbf;

namespace Hearthstone.Tests;

/// <summary>
/// Tests for Phase 5: Character, Hero, Minion, Player, and Game entities.
/// </summary>
public class GameFlowTests
{
	// ══════════════════════════════════════════════════�?
	// Character / Hero / Minion
	// ══════════════════════════════════════════════════�?

	[Fact]
	public void Character_Hit_ReducesHealth()
	{
		var m = new Minion();
		m.MaxHealth = 5;
		Assert.Equal(5, m.Health);

		int dealt = m.Hit(3);
		Assert.Equal(3, dealt);
		Assert.Equal(2, m.Health);
		Assert.Equal(3, m.DamageTaken);
	}

	[Fact]
	public void Character_DivineShield_AbsorbsDamage()
	{
		var m = new Minion();
		m.MaxHealth = 3;
		m.DivineShield = true;

		int dealt = m.Hit(5);
		Assert.Equal(0, dealt);
		Assert.Equal(3, m.Health);
		Assert.False(m.DivineShield);

		// Second hit goes through
		dealt = m.Hit(2);
		Assert.Equal(2, dealt);
		Assert.Equal(1, m.Health);
	}

	[Fact]
	public void Character_Immune_BlocksDamage()
	{
		var m = new Minion();
		m.MaxHealth = 5;
		m.Immune = true;

		int dealt = m.Hit(10);
		Assert.Equal(0, dealt);
		Assert.Equal(5, m.Health);
	}

	[Fact]
	public void Character_Heal_RestoresHealth()
	{
		var m = new Minion();
		m.MaxHealth = 10;
		m.Hit(6);
		Assert.Equal(4, m.Health);

		int healed = m.Heal(4);
		Assert.Equal(4, healed);
		Assert.Equal(8, m.Health);
	}

	[Fact]
	public void Character_Heal_DoesntExceedMax()
	{
		var m = new Minion();
		m.MaxHealth = 5;
		m.Hit(2);
		Assert.Equal(3, m.Health);

		int healed = m.Heal(10);
		Assert.Equal(2, healed);  // Only heals the 2 damage taken
		Assert.Equal(5, m.Health);
	}

	[Fact]
	public void Character_Dead_WhenHealthZero()
	{
		var m = new Minion();
		m.MaxHealth = 3;
		Assert.False(m.Dead);

		m.Hit(3);
		Assert.Equal(0, m.Health);
		Assert.True(m.Dead);
	}

	[Fact]
	public void Character_Dead_WhenMarkedForDestruction()
	{
		var m = new Minion();
		m.MaxHealth = 5;
		m.ToBeDestroyed = true;
		Assert.True(m.Dead);
	}

	[Fact]
	public void Character_SetCurrentHealth()
	{
		var m = new Minion();
		m.MaxHealth = 10;
		m.SetCurrentHealth(1);
		Assert.Equal(1, m.Health);
		Assert.Equal(9, m.DamageTaken);
	}

	// ── Hero ──

	[Fact]
	public void Hero_ArmorAbsorbsDamage()
	{
		var hero = new Hero();
		hero.MaxHealth = 30;
		hero.Armor = 5;

		hero.Hit(3);
		Assert.Equal(2, hero.Armor);
		Assert.Equal(30, hero.Health);
	}

	[Fact]
	public void Hero_DamageGoesToHealthAfterArmor()
	{
		var hero = new Hero();
		hero.MaxHealth = 30;
		hero.Armor = 3;

		hero.Hit(8);
		Assert.Equal(0, hero.Armor);
		Assert.Equal(25, hero.Health);  // 30 - 5 = 25
	}

	[Fact]
	public void Hero_EffectiveHealth()
	{
		var hero = new Hero();
		hero.MaxHealth = 30;
		hero.Armor = 10;
		Assert.Equal(40, hero.EffectiveHealth);
	}

	[Fact]
	public void Hero_DeadWhenHealthZero()
	{
		var hero = new Hero();
		hero.MaxHealth = 30;
		hero.Hit(30);
		Assert.Equal(0, hero.Health);
		Assert.True(hero.Dead);
	}

	// ── Minion ──

	[Fact]
	public void Minion_Silence_ClearsKeywords()
	{
		var m = new Minion();
		m.MaxHealth = 5;
		m.Atk = 3;
		m.Taunt = true;
		m.DivineShield = true;
		m.Windfury = true;

		m.Silence();

		Assert.False(m.Taunt);
		Assert.False(m.DivineShield);
		Assert.False(m.Windfury);
		Assert.True(m.Silenced);
	}

	[Fact]
	public void Minion_Asleep_OnFirstTurn()
	{
		var m = new Minion();
		m.MaxHealth = 3;
		Assert.True(m.Asleep);     // TurnsInPlay == 0
		Assert.False(m.CanAttack());

		m.TurnsInPlay = 1;
		Assert.False(m.Asleep);
	}

	[Fact]
	public void Minion_Charge_CanAttackImmediately()
	{
		var p1 = new Player("P1") { MaxMana = 10 };
		var p2 = new Player("P2") { MaxMana = 10 };
		var game = new Game(p1, p2);
		game.CurrentPlayer = p1;

		var hero2 = new Hero { MaxHealth = 30, Controller = p2 };
		hero2.SetZone(Zone.Play);
		p2.Hero = hero2;

		var m = new Minion { MaxHealth = 3, Atk = 2, Controller = p1 };
		m.Charge = true;
		m.SetZone(Zone.Play);

		Assert.False(m.Asleep); // Charge bypasses sleep
		Assert.True(m.CanAttack());
	}

	[Fact]
	public void Minion_Exhausted_AfterMaxAttacks()
	{
		var m = new Minion();
		m.MaxHealth = 3;
		m.Atk = 2;
		m.TurnsInPlay = 1;

		Assert.False(m.Exhausted);
		m.NumAttacks = 1;
		Assert.True(m.Exhausted);
	}

	[Fact]
	public void Minion_Windfury_AllowsTwoAttacks()
	{
		var m = new Minion();
		m.MaxHealth = 3;
		m.Atk = 2;
		m.Windfury = true;
		m.TurnsInPlay = 1;

		Assert.Equal(2, m.MaxAttacks);
		m.NumAttacks = 1;
		Assert.False(m.Exhausted);
		m.NumAttacks = 2;
		Assert.True(m.Exhausted);
	}

	// ══════════════════════════════════════════════════�?
	// Player
	// ══════════════════════════════════════════════════�?

	[Fact]
	public void Player_InitialMana()
	{
		var p = new Player("TestPlayer");
		Assert.Equal(0, p.MaxMana);
		Assert.Equal(0, p.Mana);
	}

	[Fact]
	public void Player_ManaCalculation()
	{
		var p = new Player("TestPlayer");
		p.MaxMana = 5;
		Assert.Equal(5, p.Mana);

		p.UsedMana = 3;
		Assert.Equal(2, p.Mana);

		p.TempMana = 1;
		Assert.Equal(3, p.Mana);

		p.OverloadLocked = 2;
		Assert.Equal(1, p.Mana);
	}

	[Fact]
	public void Player_MaxMana_CapsAt10()
	{
		var p = new Player("TestPlayer");
		p.MaxMana = 15;
		Assert.Equal(10, p.MaxMana);
	}

	[Fact]
	public void Player_PayCost_UsesTemporaryManaFirst()
	{
		var p = new Player("TestPlayer");
		p.MaxMana = 3;
		p.TempMana = 2;
		Assert.Equal(5, p.Mana);

		p.PayCost(3);
		Assert.Equal(0, p.TempMana); // temp mana used first
		Assert.Equal(1, p.UsedMana); // remainder from regular mana
		Assert.Equal(2, p.Mana);
	}

	[Fact]
	public void Player_DrawCard_FromDeck()
	{
		var p = new Player("TestPlayer");
		var card = new Minion { MaxHealth = 1 };
		p.Deck.Add(card);

		var drawn = p.DrawCard();
		Assert.Same(card, drawn);
		Assert.Empty(p.Deck);
		Assert.Single(p.Hand);
		Assert.Equal(1, p.CardsDrawnThisTurn);
	}

	[Fact]
	public void Player_DrawCard_Fatigue_WhenDeckEmpty()
	{
		var p = new Player("TestPlayer");
		p.Hero = new Hero { MaxHealth = 30 };

		var drawn = p.DrawCard();
		Assert.Null(drawn);
		Assert.Equal(1, p.FatigueCounter);
		Assert.Equal(29, p.Hero.Health); // 1 fatigue damage

		drawn = p.DrawCard();
		Assert.Null(drawn);
		Assert.Equal(2, p.FatigueCounter);
		Assert.Equal(27, p.Hero.Health); // 2 more fatigue damage
	}

	[Fact]
	public void Player_DrawCard_Mill_WhenHandFull()
	{
		var p = new Player("TestPlayer");
		for (int i = 0; i < 10; i++)
			p.Hand.Add(new Minion { MaxHealth = 1 });
		p.Deck.Add(new Minion { MaxHealth = 2 });

		var drawn = p.DrawCard();
		Assert.Null(drawn);
		Assert.Equal(10, p.Hand.Count);
		Assert.Empty(p.Deck);
		Assert.Empty(p.Graveyard); // milled cards go to SetAside
	}

	[Fact]
	public void Player_SpellDamage_FromMinions()
	{
		var p = new Player("TestPlayer");
		var m = new Minion { SpellPower = 2 };
		m.MaxHealth = 3;
		p.Field.Add(m);

		Assert.Equal(2, p.SpellPower);
		Assert.Equal(5, p.GetSpellDamage(3)); // 3 + 2 = 5
	}

	[Fact]
	public void Player_Summon_RespectsLimit()
	{
		var p = new Player("TestPlayer");
		for (int i = 0; i < 7; i++)
		{
			var m = new Minion { MaxHealth = 1 };
			Assert.True(p.Summon(m));
		}
		Assert.Equal(7, p.Field.Count);

		var extra = new Minion { MaxHealth = 1 };
		Assert.False(p.Summon(extra));
		Assert.Equal(7, p.Field.Count);
	}

	[Fact]
	public void Player_Concede()
	{
		var p = new Player("TestPlayer");
		p.Concede();
		Assert.Equal(PlayState.Conceded, p.PlayState);
	}

	[Fact]
	public void Player_DiscardHand()
	{
		var p = new Player("TestPlayer");
		for (int i = 0; i < 3; i++)
			p.Hand.Add(new Minion { MaxHealth = 1 });

		p.DiscardHand();
		Assert.Empty(p.Hand);
		Assert.Equal(3, p.Graveyard.Count);
	}

	// ══════════════════════════════════════════════════�?
	// Game
	// ══════════════════════════════════════════════════�?

	private Game CreateTestGame()
	{
		var p1 = new Player("Player1");
		p1.Hero = new Hero();
		p1.Hero.MaxHealth = 30;
		var p2 = new Player("Player2");
		p2.Hero = new Hero();
		p2.Hero.MaxHealth = 30;
		// Add cards to decks so draw doesn't fatigue
		for (int i = 0; i < 10; i++)
		{
			p1.Deck.Add(new Minion { MaxHealth = 1 });
			p2.Deck.Add(new Minion { MaxHealth = 1 });
		}
		return new Game(p1, p2, seed: 42);
	}

	[Fact]
	public void Game_Setup_SetsStateAndOpponents()
	{
		var game = CreateTestGame();
		game.Setup();

		Assert.Equal(GameState.Running, game.State);
		Assert.Same(game.Player1.Opponent, game.Player2);
		Assert.Same(game.Player2.Opponent, game.Player1);
	}

	[Fact]
	public void Game_Start_BeginsTurn1()
	{
		var game = CreateTestGame();
		game.Start();

		Assert.Equal(1, game.Turn);
		Assert.NotNull(game.CurrentPlayer);
		Assert.Equal(1, game.CurrentPlayer.MaxMana);
		Assert.Equal(GameState.Running, game.State);
	}

	[Fact]
	public void Game_BeginTurn_IncrementsMana()
	{
		var game = CreateTestGame();
		game.Start();

		// Turn 1: first player has 1 mana
		Assert.Equal(1, game.CurrentPlayer.MaxMana);

		game.EndTurn(); // Opponent starts turn 2

		Assert.Equal(2, game.Turn);
		Assert.Equal(1, game.CurrentPlayer.MaxMana); // Opponent's first turn

		game.EndTurn(); // Back to first player, turn 3

		Assert.Equal(3, game.Turn);
		Assert.Equal(2, game.Player1.MaxMana);
	}

	[Fact]
	public void Game_BeginTurn_DrawsCard()
	{
		var game = CreateTestGame();
		game.Start();

		// After Start: P1 drew 3 (initial) + 1 (BeginTurn) = hand has cards
		// Now verify that EndTurn→BeginTurn draws exactly 1 more card for P2
		int p2DeckBefore = game.Player2.Deck.Count;
		game.EndTurn(); // triggers BeginTurn for P2
		Assert.Equal(p2DeckBefore - 1, game.Player2.Deck.Count);
	}

	[Fact]
	public void Game_EndTurn_ClearsTempMana()
	{
		var game = CreateTestGame();
		game.Start();
		game.CurrentPlayer.TempMana = 3;

		game.EndTurn();
		Assert.Equal(0, game.Player1.TempMana);
	}

	[Fact]
	public void Game_EndTurn_UnfreezesCharacters()
	{
		var game = CreateTestGame();
		game.Start();

		game.CurrentPlayer.Hero!.Frozen = true;
		game.EndTurn();

		// Hero was frozen, didn't attack, and not exhausted �?unfreezes
		Assert.False(game.Player1.Hero!.Frozen);
	}

	[Fact]
	public void Game_ProcessDeaths_MovesToGraveyard()
	{
		var game = CreateTestGame();
		game.Start();

		var minion = new Minion { MaxHealth = 3, Atk = 2 };
		game.CurrentPlayer.Summon(minion);
		minion.Hit(3); // kill it

		Assert.True(minion.Dead);
		game.ProcessDeaths();

		Assert.DoesNotContain(minion, game.CurrentPlayer.Field);
		Assert.Contains(minion, game.CurrentPlayer.Graveyard);
	}

	[Fact]
	public void Game_CheckForEndGame_Concede()
	{
		var game = CreateTestGame();
		game.Start();

		game.Player1.Concede();
		game.CheckForEndGame();

		Assert.Equal(GameState.Complete, game.State);
		Assert.Equal(PlayState.Lost, game.Player1.PlayState);
		Assert.Equal(PlayState.Won, game.Player2.PlayState);
	}

	[Fact]
	public void Game_CheckForEndGame_HeroDeath()
	{
		var game = CreateTestGame();
		game.Start();

		game.Player2.Hero!.Hit(30);
		Assert.True(game.Player2.Hero.Dead);

		game.CheckForEndGame();

		Assert.Equal(GameState.Complete, game.State);
		Assert.Equal(PlayState.Won, game.Player1.PlayState);
		Assert.Equal(PlayState.Lost, game.Player2.PlayState);
	}

	[Fact]
	public void Game_CheckForEndGame_BothHeroesDie_Tie()
	{
		var game = CreateTestGame();
		game.Start();

		game.Player1.Hero!.Hit(30);
		game.Player2.Hero!.Hit(30);

		game.CheckForEndGame();

		Assert.Equal(GameState.Complete, game.State);
		Assert.Equal(PlayState.Tied, game.Player1.PlayState);
		Assert.Equal(PlayState.Tied, game.Player2.PlayState);
	}

	[Fact]
	public void Game_OverloadCarriesOver()
	{
		var game = CreateTestGame();
		game.Start();
		var p = game.CurrentPlayer;

		p.Overloaded = 2;
		Assert.Equal(0, p.OverloadLocked);

		game.EndTurn(); // opponent's turn
		game.EndTurn(); // back to us

		// On the new turn, overload was locked
		Assert.Equal(2, p.OverloadLocked);
		Assert.Equal(0, p.Overloaded);
		// Mana = max(0, MaxMana - UsedMana - OverloadLocked) + TempMana
		Assert.Equal(Math.Max(0, p.MaxMana - p.OverloadLocked), p.Mana);
	}

	// ── Spell / Weapon / HeroPower ──

	[Fact]
	public void Spell_GetDamage_AppliesSpellPower()
	{
		var p = new Player("TestPlayer");
		var spellDmgMinion = new Minion { SpellPower = 2, MaxHealth = 3 };
		p.Field.Add(spellDmgMinion);

		var spell = new Spell { Controller = p };
		Assert.Equal(5, spell.GetDamage(3)); // 3 + 2 = 5
	}

	[Fact]
	public void Spell_ImmuneToSpellpower_NoBonus()
	{
		var p = new Player("TestPlayer");
		var spellDmgMinion = new Minion { SpellPower = 2, MaxHealth = 3 };
		p.Field.Add(spellDmgMinion);

		var spell = new Spell { Controller = p, ImmuneToSpellpower = true };
		Assert.Equal(3, spell.GetDamage(3)); // No bonus
	}

	[Fact]
	public void Weapon_Durability()
	{
		var w = new Weapon();
		w.Atk = 3;
		w.MaxDurability = 2;
		Assert.Equal(2, w.Durability);
		Assert.False(w.Dead);

		w.LoseDurability();
		Assert.Equal(1, w.Durability);
		Assert.False(w.Dead);

		w.LoseDurability();
		Assert.Equal(0, w.Durability);
		Assert.True(w.Dead);
	}

	[Fact]
	public void HeroPower_Activation()
	{
		var p = new Player("TestPlayer") { MaxMana = 5 };
		var hp = new HeroPower { Controller = p };
		hp.Cost = 2;

		Assert.True(hp.IsUsable);
		hp.ActivationsThisTurn++;
		Assert.False(hp.IsUsable); // Only 1 activation per turn
	}

	[Fact]
	public void HeroPower_NotPlayable_NotEnoughMana()
	{
		var p = new Player("TestPlayer") { MaxMana = 1 };
		var hp = new HeroPower { Controller = p };
		hp.Cost = 2;

		Assert.False(hp.IsUsable);
	}

	// ══════════════════════════════════════════════════�?
	// Phase 6: 实体与卡牌数据绑�?
	// ══════════════════════════════════════════════════�?

	[Fact]
	public void BuffableEntity_CardData_ReadsCardId()
	{
		var card = new HearthstoneServer.LoadCardIntoDbf.Card
		{
			CardId = "CS2_032",
			Rarity = Rarity.Free,
		};
		var minion = new Minion();
		minion.CardData = card;

		Assert.Equal("CS2_032", minion.CardId);
	}

	[Fact]
	public void BuffableEntity_CardData_Null_ReturnsDefaults()
	{
		var minion = new Minion();
		Assert.Null(minion.CardId);
		Assert.Null(minion.NameEnUS);
		Assert.Null(minion.CardData);
	}

	[Fact]
	public void Character_Races_FromCardData()
	{
		var card = new HearthstoneServer.LoadCardIntoDbf.Card
		{
			Races = new[] { Race.Beast, Race.Dragon },
		};
		var minion = new Minion { CardData = card };

		Assert.Equal(2, minion.Races.Length);
		Assert.Contains(Race.Beast, minion.Races);
		Assert.Contains(Race.Dragon, minion.Races);
	}

	[Fact]
	public void Character_Races_NoCardData_ReturnsEmpty()
	{
		var minion = new Minion();
		Assert.Empty(minion.Races);
	}

	[Fact]
	public void Character_Rarity_FromCardData()
	{
		var card = new HearthstoneServer.LoadCardIntoDbf.Card
		{
			Rarity = Rarity.Legendary,
		};
		var minion = new Minion { CardData = card };
		Assert.Equal(Rarity.Legendary, minion.Rarity);
	}

	[Fact]
	public void Minion_AdjacentMinions_FindsNeighbors()
	{
		var p = new Player("TestPlayer");
		var m1 = new Minion { MaxHealth = 1, Controller = p, ZonePosition = 1 };
		var m2 = new Minion { MaxHealth = 2, Controller = p, ZonePosition = 2 };
		var m3 = new Minion { MaxHealth = 3, Controller = p, ZonePosition = 3 };
		p.Field.Add(m1);
		p.Field.Add(m2);
		p.Field.Add(m3);

		// m2 has both neighbors
		Assert.Same(m1, m2.LeftMinion);
		Assert.Same(m3, m2.RightMinion);
		Assert.Equal(2, m2.AdjacentMinions.Count);

		// m1 has only right neighbor
		Assert.Null(m1.LeftMinion);
		Assert.Same(m2, m1.RightMinion);
		Assert.Single(m1.AdjacentMinions);

		// m3 has only left neighbor
		Assert.Same(m2, m3.LeftMinion);
		Assert.Null(m3.RightMinion);
		Assert.Single(m3.AdjacentMinions);
	}

	[Fact]
	public void Minion_NoController_NoAdjacentMinions()
	{
		var m = new Minion { MaxHealth = 1, ZonePosition = 1 };
		Assert.Null(m.LeftMinion);
		Assert.Null(m.RightMinion);
		Assert.Empty(m.AdjacentMinions);
	}

	[Fact]
	public void Spell_SpellSchool_FromCardData()
	{
		var card = new HearthstoneServer.LoadCardIntoDbf.Card
		{
			SpellSchool = SpellSchool.Fire,
		};
		var spell = new Spell { CardData = card };
		Assert.Equal(SpellSchool.Fire, spell.SpellSchool);
	}

	[Fact]
	public void Spell_SpellSchool_NoCardData_ReturnsNone()
	{
		var spell = new Spell();
		Assert.Equal(SpellSchool.None, spell.SpellSchool);
	}

	// ══════════════════════════════════════════════════�?
	// Phase 7: 打牌与区域管�?
	// ══════════════════════════════════════════════════�?

	[Fact]
	public void SetZone_MovesEntityBetweenLists()
	{
		var p = new Player("TestPlayer");
		var minion = new Minion { MaxHealth = 3, Atk = 2, Controller = p };

		// Place in hand
		minion.SetZone(Zone.Hand);
		Assert.Single(p.Hand);
		Assert.Equal(Zone.Hand, minion.Zone);

		// Move to play (field)
		minion.SetZone(Zone.Play);
		Assert.Empty(p.Hand);
		Assert.Single(p.Field);
		Assert.Equal(Zone.Play, minion.Zone);
		Assert.Equal(Zone.Hand, minion.OldZone);

		// Move to graveyard
		minion.SetZone(Zone.Graveyard);
		Assert.Empty(p.Field);
		Assert.Single(p.Graveyard);
		Assert.Equal(Zone.Graveyard, minion.Zone);
	}

	[Fact]
	public void SetZone_SameZone_Ignored()
	{
		var p = new Player("TestPlayer");
		var minion = new Minion { MaxHealth = 1, Controller = p };
		minion.SetZone(Zone.Hand);
		minion.SetZone(Zone.Hand); // same zone, should be ignored
		Assert.Single(p.Hand);
	}

	[Fact]
	public void Enchantment_Apply_AddsToBuff()
	{
		var minion = new Minion { MaxHealth = 5 };
		var buff = new Enchantment();
		buff.Apply(minion);

		Assert.Single(minion.Buffs);
		Assert.Same(minion, buff.Owner);
		Assert.Equal(Zone.Play, buff.Zone);
	}

	[Fact]
	public void Enchantment_Remove_ClearsFromBuffs()
	{
		var minion = new Minion { MaxHealth = 5 };
		var buff = new Enchantment();
		buff.Apply(minion);
		Assert.Single(minion.Buffs);

		buff.Remove();
		Assert.Empty(minion.Buffs);
		Assert.Equal(Zone.RemovedFromGame, buff.Zone);
	}

	[Fact]
	public void Minion_Play_MovesFromHandToField()
	{
		var game = new Game();
		var p = new Player("TestPlayer") { MaxMana = 5 };
		p.Game = game;
		game.CurrentPlayer = p;

		var minion = new Minion { MaxHealth = 3, Atk = 2, Controller = p };
		minion.Cost = 3;
		minion.SetZone(Zone.Hand);

		Assert.Single(p.Hand);
		Assert.True(minion.IsPlayable());

		minion.Play();

		// Should move from hand to field
		Assert.Empty(p.Hand);
		Assert.Single(p.Field);
		Assert.Equal(Zone.Play, minion.Zone);
		// Should spend mana
		Assert.Equal(3, p.UsedMana);
	}

	[Fact]
	public void Spell_Play_MovesFromHandToGraveyard()
	{
		var game = new Game();
		var p = new Player("TestPlayer") { MaxMana = 4 };
		p.Game = game;
		game.CurrentPlayer = p;

		var spell = new Spell { Controller = p };
		spell.Cost = 2;
		spell.SetZone(Zone.Hand);

		spell.Play();

		Assert.Empty(p.Hand);
		Assert.Single(p.Graveyard);
		Assert.Equal(Zone.Graveyard, spell.Zone);
		Assert.Equal(2, p.UsedMana);
	}

	[Fact]
	public void IsPlayable_NotCurrentPlayer_ReturnsFalse()
	{
		var game = new Game();
		var p1 = new Player("P1") { MaxMana = 10 };
		var p2 = new Player("P2") { MaxMana = 10 };
		p1.Game = game;
		p2.Game = game;
		game.CurrentPlayer = p1;

		var minion = new Minion { MaxHealth = 1, Controller = p2 };
		minion.Cost = 1;
		minion.SetZone(Zone.Hand);

		Assert.False(minion.IsPlayable()); // p2 is not current player
	}

	[Fact]
	public void IsPlayable_NotInHand_ReturnsFalse()
	{
		var game = new Game();
		var p = new Player("TestPlayer") { MaxMana = 10 };
		p.Game = game;
		game.CurrentPlayer = p;

		var minion = new Minion { MaxHealth = 1, Controller = p };
		minion.Cost = 1;
		minion.SetZone(Zone.Deck);

		Assert.False(minion.IsPlayable()); // in deck, not hand
	}

	[Fact]
	public void IsPlayable_NotEnoughMana_ReturnsFalse()
	{
		var game = new Game();
		var p = new Player("TestPlayer") { MaxMana = 2 };
		p.Game = game;
		game.CurrentPlayer = p;

		var minion = new Minion { MaxHealth = 1, Controller = p };
		minion.Cost = 5;
		minion.SetZone(Zone.Hand);

		Assert.False(minion.IsPlayable()); // costs too much
	}

	[Fact]
	public void Game_PlayCard_Integration()
	{
		var p1 = new Player("P1") { MaxMana = 10 };
		var p2 = new Player("P2") { MaxMana = 10 };
		var game = new Game(p1, p2);
		game.CurrentPlayer = p1;

		var minion = new Minion { MaxHealth = 4, Atk = 3, Controller = p1 };
		minion.Cost = 3;
		minion.SetZone(Zone.Hand);

		game.PlayCard(minion);

		Assert.Empty(p1.Hand);
		Assert.Single(p1.Field);
		Assert.Equal(3, p1.UsedMana);
		Assert.Equal(1, p1.CardsPlayedThisTurn);
		Assert.Equal(1, p1.MinionsPlayedThisTurn);
	}

	[Fact]
	public void Player_CreateCard_Minion()
	{
		var p = new Player("TestPlayer") { MaxMana = 10 };
		var game = new Game(p, new Player("P2"));

		var cardData = new HearthstoneServer.LoadCardIntoDbf.Card
		{
			CardId = "CS2_213",
			CardType = CardType.Minion,
			Cost = 7,
			Attack = 7,
			Health = 7,
		};

		var entity = p.CreateCard(cardData, Zone.Hand);

		Assert.NotNull(entity);
		Assert.IsType<Minion>(entity);
		Assert.Equal("CS2_213", entity!.CardId);
		Assert.Single(p.Hand);
		Assert.Equal(Zone.Hand, entity.Zone);
	}

	[Fact]
	public void PlayCounter_Assigned_WhenEnteringPlay()
	{
		var p1 = new Player("P1") { MaxMana = 10 };
		var p2 = new Player("P2") { MaxMana = 10 };
		var game = new Game(p1, p2);
		game.CurrentPlayer = p1;

		var m1 = new Minion { MaxHealth = 1, Controller = p1 };
		m1.Cost = 0;
		m1.SetZone(Zone.Hand);
		m1.Play();

		var m2 = new Minion { MaxHealth = 1, Controller = p1 };
		m2.Cost = 0;
		m2.SetZone(Zone.Hand);
		m2.Play();

		Assert.True(m2.PlayCounter > m1.PlayCounter);
	}

	[Fact]
	public void Minion_Play_WithOverload()
	{
		var game = new Game();
		var p = new Player("TestPlayer") { MaxMana = 5 };
		p.Game = game;
		game.CurrentPlayer = p;

		var minion = new Minion { MaxHealth = 2, Atk = 3, Controller = p };
		minion.Cost = 2;
		minion.Overload = 2;
		minion.SetZone(Zone.Hand);

		minion.Play();

		Assert.Equal(2, p.UsedMana);
		Assert.Equal(2, p.Overloaded);
	}

	// ════════════════════════════════════════════�?
	// Phase 8: 战斗与目标系�?
	// ════════════════════════════════════════════�?

	/// <summary>Helper: setup a game with 2 players, each with a Hero on the field.</summary>
	private (Game game, Player p1, Player p2) SetupCombatGame()
	{
		var p1 = new Player("P1") { MaxMana = 10 };
		var p2 = new Player("P2") { MaxMana = 10 };
		var game = new Game(p1, p2);
		game.CurrentPlayer = p1;

		var hero1 = new Hero { MaxHealth = 30, Atk = 0, Controller = p1 };
		hero1.SetZone(Zone.Play);
		p1.Hero = hero1;

		var hero2 = new Hero { MaxHealth = 30, Atk = 0, Controller = p2 };
		hero2.SetZone(Zone.Play);
		p2.Hero = hero2;

		return (game, p1, p2);
	}

	[Fact]
	public void AttackTargets_ReturnsOpponentCharacters()
	{
		var (game, p1, p2) = SetupCombatGame();

		var attacker = new Minion { MaxHealth = 3, Atk = 2, Controller = p1 };
		attacker.SetZone(Zone.Play);
		attacker.TurnsInPlay = 1;

		var defender = new Minion { MaxHealth = 4, Atk = 1, Controller = p2 };
		defender.SetZone(Zone.Play);

		var targets = attacker.AttackTargets;
		Assert.Contains(defender, targets);
		Assert.Contains(p2.Hero!, targets);
		Assert.Equal(2, targets.Count);
	}

	[Fact]
	public void AttackTargets_TauntFilter()
	{
		var (game, p1, p2) = SetupCombatGame();

		var attacker = new Minion { MaxHealth = 3, Atk = 2, Controller = p1 };
		attacker.SetZone(Zone.Play);
		attacker.TurnsInPlay = 1;

		var tauntMinion = new Minion { MaxHealth = 5, Atk = 1, Controller = p2, Taunt = true };
		tauntMinion.SetZone(Zone.Play);

		var normalMinion = new Minion { MaxHealth = 2, Atk = 3, Controller = p2 };
		normalMinion.SetZone(Zone.Play);

		var targets = attacker.AttackTargets;
		Assert.Single(targets);
		Assert.Contains(tauntMinion, targets);
	}

	[Fact]
	public void AttackTargets_RushFirstTurn_CannotHitHero()
	{
		var (game, p1, p2) = SetupCombatGame();

		var rusher = new Minion { MaxHealth = 3, Atk = 2, Controller = p1 };
		rusher.Rush = true;
		rusher.TurnsInPlay = 0; // first turn
		rusher.SetZone(Zone.Play);

		var defender = new Minion { MaxHealth = 4, Atk = 1, Controller = p2 };
		defender.SetZone(Zone.Play);

		var targets = rusher.AttackTargets;
		Assert.Contains(defender, targets);
		// Should NOT contain opponent hero on first turn with Rush
		Assert.DoesNotContain(p2.Hero!, targets);
	}

	[Fact]
	public void AttackTargets_CannotAttackHeroes()
	{
		var (game, p1, p2) = SetupCombatGame();

		var attacker = new Minion { MaxHealth = 3, Atk = 2, Controller = p1 };
		attacker.CannotAttackHeroes = true;
		attacker.TurnsInPlay = 1;
		attacker.SetZone(Zone.Play);

		var defender = new Minion { MaxHealth = 2, Atk = 1, Controller = p2 };
		defender.SetZone(Zone.Play);

		var targets = attacker.AttackTargets;
		Assert.Contains(defender, targets);
		Assert.DoesNotContain(p2.Hero!, targets);
	}

	[Fact]
	public void CanAttack_FrozenMinion_ReturnsFalse()
	{
		var (game, p1, p2) = SetupCombatGame();
		var m = new Minion { MaxHealth = 3, Atk = 2, Controller = p1 };
		m.SetZone(Zone.Play);
		m.TurnsInPlay = 1;
		m.Frozen = true;

		// add a target
		var d = new Minion { MaxHealth = 2, Atk = 1, Controller = p2 };
		d.SetZone(Zone.Play);

		Assert.False(m.CanAttack());
	}

	[Fact]
	public void CanAttack_AsleepMinion_ReturnsFalse()
	{
		var (game, p1, p2) = SetupCombatGame();
		var m = new Minion { MaxHealth = 3, Atk = 2, Controller = p1 };
		m.SetZone(Zone.Play);
		m.TurnsInPlay = 0; // asleep, no rush/charge

		var d = new Minion { MaxHealth = 2, Atk = 1, Controller = p2 };
		d.SetZone(Zone.Play);

		Assert.True(m.Asleep);
		Assert.False(m.CanAttack());
	}

	[Fact]
	public void CanAttack_ZeroAtk_ReturnsFalse()
	{
		var (game, p1, p2) = SetupCombatGame();
		var m = new Minion { MaxHealth = 3, Atk = 0, Controller = p1 };
		m.SetZone(Zone.Play);
		m.TurnsInPlay = 1;

		Assert.False(m.CanAttack());
	}

	[Fact]
	public void Game_Attack_MutualDamage()
	{
		var (game, p1, p2) = SetupCombatGame();

		var a = new Minion { MaxHealth = 5, Atk = 3, Controller = p1 };
		a.SetZone(Zone.Play);
		a.TurnsInPlay = 1;

		var d = new Minion { MaxHealth = 4, Atk = 2, Controller = p2 };
		d.SetZone(Zone.Play);

		game.Attack(a, d);

		Assert.Equal(3, a.Health); // 5 - 2
		Assert.Equal(1, d.Health); // 4 - 3
		Assert.Equal(1, a.NumAttacks);
	}

	[Fact]
	public void Game_Attack_HeroArmorAbsorbs()
	{
		var (game, p1, p2) = SetupCombatGame();

		var a = new Minion { MaxHealth = 3, Atk = 5, Controller = p1 };
		a.SetZone(Zone.Play);
		a.TurnsInPlay = 1;

		p2.Hero!.Armor = 3;

		game.Attack(a, p2.Hero);

		// 5 damage to hero: 3 absorbed by armor, 2 goes to health
		Assert.Equal(0, p2.Hero.Armor);
		Assert.Equal(28, p2.Hero.Health); // 30 - 2
	}

	[Fact]
	public void Game_Attack_WeaponDurabilityLoss()
	{
		var (game, p1, p2) = SetupCombatGame();

		var weapon = new Weapon { Atk = 3, MaxDurability = 2, Controller = p1 };
		p1.Hero!.EquippedWeapon = weapon;
		p1.Hero.Atk = 3; // hero gets weapon ATK

		var target = new Minion { MaxHealth = 10, Atk = 1, Controller = p2 };
		target.SetZone(Zone.Play);

		game.Attack(p1.Hero, target);

		Assert.Equal(1, weapon.Durability); // 2 - 1
		Assert.False(weapon.Dead);
	}

	[Fact]
	public void Game_Attack_WeaponBreaks()
	{
		var (game, p1, p2) = SetupCombatGame();

		var weapon = new Weapon { Atk = 2, MaxDurability = 1, Controller = p1 };
		p1.Hero!.EquippedWeapon = weapon;
		p1.Hero.Atk = 2;

		var target = new Minion { MaxHealth = 10, Atk = 0, Controller = p2 };
		target.SetZone(Zone.Play);

		game.Attack(p1.Hero, target);

		Assert.True(weapon.Dead);
		Assert.Null(p1.Hero.EquippedWeapon);
	}

	[Fact]
	public void Minion_Attackable_StealthedIsNot()
	{
		var m = new Minion { MaxHealth = 3, Atk = 2, Stealth = true };
		Assert.False(m.Attackable);
	}

	[Fact]
	public void Minion_Attackable_DormantIsNot()
	{
		var m = new Minion { MaxHealth = 3, Atk = 2, Dormant = true };
		Assert.False(m.Attackable);
	}

	[Fact]
	public void IsValidTarget_BasicMinionFilter()
	{
		var p1 = new Player("P1");
		var p2 = new Player("P2");
		var game = new Game(p1, p2);

		var source = new Spell { Controller = p1 };
		source.TargetReq = HearthstoneServer.Targeting.Target.Required().Minion();

		var minion = new Minion { MaxHealth = 3, Atk = 2, Controller = p2 };
		var hero = new Hero { MaxHealth = 30, Controller = p2 };

		Assert.True(HearthstoneServer.Targeting.TargetingRules.IsValidTarget(source, minion));
		Assert.False(HearthstoneServer.Targeting.TargetingRules.IsValidTarget(source, hero)); // hero fails MinionTarget
	}

	[Fact]
	public void MegaWindfury_AllowsFourAttacks()
	{
		var (game, p1, p2) = SetupCombatGame();

		var m = new Minion { MaxHealth = 10, Atk = 1, Controller = p1, MegaWindfury = true };
		m.SetZone(Zone.Play);
		m.TurnsInPlay = 1;

		Assert.Equal(4, m.MaxAttacks);
		Assert.False(m.Exhausted);

		m.NumAttacks = 3;
		Assert.False(m.Exhausted);

		m.NumAttacks = 4;
		Assert.True(m.Exhausted);
	}


	// ════════════════════════════════════════════�?
	// Phase 9a: 事件系统运行时（EventBus + When<T>�?
	// ════════════════════════════════════════════�?

	[Fact]
	public void ActionBlock_ExecutesActions_AndIncrementsTick()
	{
		var p1 = new Player("P1") { MaxMana = 10 };
		var p2 = new Player("P2") { MaxMana = 10 };
		var game = new Game(p1, p2);
		game.CurrentPlayer = p1;

		int startTick = game.Tick;

		// ActionBlock with a simple action
		var actions = new List<HearthstoneServer.Dsl.Action>
		{
			new DealDamage(S.SELF, 0)
		};

		game.ActionBlock(game, actions, BlockType.Power);

		// ActionEnd triggers RefreshAuras which increments Tick
		Assert.True(game.Tick > startTick);
	}

	[Fact]
	public void QueueActions_SetsEventArgs()
	{
		var p1 = new Player("P1") { MaxMana = 10 };
		var p2 = new Player("P2") { MaxMana = 10 };
		var game = new Game(p1, p2);
		game.CurrentPlayer = p1;

		var minion = new Minion { MaxHealth = 3, Controller = p1 };
		game.RegisterEntity(minion);

		Assert.Null(minion.EventArgs);

		// The event_args should be set during execution and restored after
		var eventArgs = new object[] { "test_arg", 42 };

		// QueueActions sets event_args during execution
		game.QueueActions(minion, new List<HearthstoneServer.Dsl.Action>(), eventArgs);

		// After execution, event_args should be restored (null)
		Assert.Null(minion.EventArgs);
	}

	[Fact]
	public void RefreshAuras_IncrementsTick()
	{
		var p1 = new Player("P1") { MaxMana = 10 };
		var p2 = new Player("P2") { MaxMana = 10 };
		var game = new Game(p1, p2);

		int tick0 = game.Tick;
		game.RefreshAuras();
		Assert.Equal(tick0 + 1, game.Tick);
		game.RefreshAuras();
		Assert.Equal(tick0 + 2, game.Tick);
	}




	[Fact]
	public void Secret_IsSummonable_UniqueCheck()
	{
		var p1 = new Player("P1") { MaxMana = 10 };
		var p2 = new Player("P2") { MaxMana = 10 };
		var game = new Game(p1, p2);

		var secret1 = new Secret { CardData = new HearthstoneServer.LoadCardIntoDbf.Card { CardId = "SEC_001" } };
		secret1.Controller = p1;
		secret1.SetZone(Zone.Play); // �?Secret zone
		Assert.True(p1.Secrets.Count > 0);

		// �?ID 不能再放
		var secret2 = new Secret { CardData = new HearthstoneServer.LoadCardIntoDbf.Card { CardId = "SEC_001" } };
		secret2.Controller = p1;
		Assert.False(secret2.IsSummonable());

		// 不同 ID 可以
		var secret3 = new Secret { CardData = new HearthstoneServer.LoadCardIntoDbf.Card { CardId = "SEC_002" } };
		secret3.Controller = p1;
		Assert.True(secret3.IsSummonable());
	}

	[Fact]
	public void Quest_SetZone_InsertsAtFront()
	{
		var p1 = new Player("P1") { MaxMana = 10 };
		var p2 = new Player("P2") { MaxMana = 10 };
		var game = new Game(p1, p2);

		// 先放一�?secret
		var secret = new Secret { CardData = new HearthstoneServer.LoadCardIntoDbf.Card { CardId = "SEC_001" } };
		secret.Controller = p1;
		secret.SetZone(Zone.Play); // �?Secret
		Assert.Single(p1.Secrets);

		// �?quest �?应该�?secrets 最前面
		var quest = new Quest { CardData = new HearthstoneServer.LoadCardIntoDbf.Card { CardId = "QUEST_001" }, ProgressTotal = 3 };
		quest.Controller = p1;
		quest.SetZone(Zone.Play); // �?Secret
		Assert.Equal(2, p1.Secrets.Count);
		Assert.Same(quest, p1.Secrets[0]); // Quest 在最前面
	}

	[Fact]
	public void Quest_AddProgress_CompletesAndMovesToGraveyard()
	{
		var p1 = new Player("P1") { MaxMana = 10 };
		var p2 = new Player("P2") { MaxMana = 10 };
		var game = new Game(p1, p2);

		var quest = new Quest { CardData = new HearthstoneServer.LoadCardIntoDbf.Card { CardId = "QUEST_001" }, ProgressTotal = 3 };
		quest.Controller = p1;
		quest.SetZone(Zone.Play); // �?Secret
		Assert.Equal(Zone.Secret, quest.Zone);

		quest.AddProgress(2);
		Assert.Equal(2, quest.Progress);
		Assert.Equal(Zone.Secret, quest.Zone); // 未完�?

		quest.AddProgress(1);
		Assert.Equal(3, quest.Progress);
		Assert.Equal(Zone.Graveyard, quest.Zone); // 完成 �?墓地
	}

	[Fact]
	public void SideQuest_SetZone_RedirectsPlayToSecret()
	{
		var p1 = new Player("P1") { MaxMana = 10 };
		var p2 = new Player("P2") { MaxMana = 10 };
		var game = new Game(p1, p2);

		var sq = new SideQuest { CardData = new HearthstoneServer.LoadCardIntoDbf.Card { CardId = "SQ_001" } };
		sq.Controller = p1;
		sq.SetZone(Zone.Play); // �?Secret
		Assert.Equal(Zone.Secret, sq.Zone);
		Assert.Contains(sq, p1.Secrets);
	}

	[Fact]
	public void AuraBuff_Remove_CleansGameActiveAuraBuffs()
	{
		var p1 = new Player("P1") { MaxMana = 10 };
		var p2 = new Player("P2") { MaxMana = 10 };
		var game = new Game(p1, p2);

		var source = new Minion { MaxHealth = 3 };
		source.Controller = p1;
		source.SetZone(Zone.Play);
		game.RegisterEntity(source);

		var target = new Minion { MaxHealth = 3 };

		var auraBuff = new HearthstoneServer.Auras.AuraBuff(source, target);
		target.Slots.Add(auraBuff);
		game.ActiveAuraBuffs.Add(auraBuff);
		Assert.Single(game.ActiveAuraBuffs);

		auraBuff.Remove();
		Assert.Empty(target.Slots);
		Assert.Empty(game.ActiveAuraBuffs);
	}

	[Fact]
	public void BuffableEntity_RefreshTags_CreatesAndUpdatesSlot()
	{
		var p1 = new Player("P1") { MaxMana = 10 };
		var p2 = new Player("P2") { MaxMana = 10 };
		var game = new Game(p1, p2);

		var source = new Minion { MaxHealth = 3 };
		source.Controller = p1;
		source.SetZone(Zone.Play);
		game.RegisterEntity(source);

		var target = new Minion { MaxHealth = 5 };

		// 首次 RefreshTags �?新建 slot
		var tags = new Dictionary<GameTag, int> { [GameTag.Atk] = 2 };
		target.RefreshTags(source, tags);
		Assert.Single(target.Slots);
		Assert.Single(game.ActiveAuraBuffs);

		// 再次 RefreshTags �?source �?更新已有 slot（不新建�?
		var tags2 = new Dictionary<GameTag, int> { [GameTag.Atk] = 5 };
		target.RefreshTags(source, tags2);
		Assert.Single(target.Slots); // 还是 1 �?
	}

	// ══════════════════════════════════════════════════�?
	// Phase 10a �?Discover + 选择管线
	// ══════════════════════════════════════════════════�?

	[Fact]
	public void Phase10a_InvalidActionException_Throws()
	{
		var ex = new InvalidActionException("test error");
		Assert.Equal("test error", ex.Message);
		Assert.Throws<InvalidActionException>((System.Action)(() => throw ex));
	}

	[Fact]
	public void Phase10a_Choice_SetsPlayerChoice_And_ChooseClearsIt()
	{
		var game = new Game(new Player("P1"), new Player("P2"));
		game.Setup();
		var player = game.Player1;

		// 创建几个可供选择的实�?
		var card1 = new Minion { MaxHealth = 3, Controller = player };
		var card2 = new Minion { MaxHealth = 4, Controller = player };
		game.RegisterEntity(card1);
		game.RegisterEntity(card2);

		var choice = new HearthstoneServer.Dsl.Choice(player, new List<BaseEntity> { card1, card2 });
		// 手动触发 Do
		choice.DoChoice(card1, player, new List<BaseEntity> { card1, card2 });

		Assert.Same(choice, player.Choice);
		Assert.Equal(2, choice.Cards.Count);

		// Choose
		choice.Choose(card1);
		Assert.Null(player.Choice);
	}

	[Fact]
	public void Phase10a_Choice_InvalidChoiceThrows()
	{
		var game = new Game(new Player("P1"), new Player("P2"));
		game.Setup();
		var player = game.Player1;

		var card1 = new Minion { MaxHealth = 3, Controller = player };
		var card2 = new Minion { MaxHealth = 4, Controller = player };
		var notInChoice = new Minion { MaxHealth = 5, Controller = player };
		game.RegisterEntity(card1);
		game.RegisterEntity(card2);

		var choice = new HearthstoneServer.Dsl.Choice(player, new List<BaseEntity> { card1, card2 });
		choice.DoChoice(card1, player, new List<BaseEntity> { card1, card2 });

		Assert.Throws<InvalidActionException>((System.Action)(() => choice.Choose(notInChoice)));
	}

	[Fact]
	public void Phase10a_GenericChoice_ChosenGoesToHand_UnchosenDiscarded()
	{
		var game = new Game(new Player("P1"), new Player("P2"));
		game.Setup();
		var player = game.Player1;

		var card1 = new Minion { MaxHealth = 3, Controller = player };
		var card2 = new Minion { MaxHealth = 4, Controller = player };
		game.RegisterEntity(card1);
		game.RegisterEntity(card2);

		var gc = new GenericChoice(player, new List<BaseEntity> { card1, card2 });
		gc.DoChoice(card1, player, new List<BaseEntity> { card1, card2 });

		Assert.Same(gc, player.Choice);

		gc.Choose(card1);
		Assert.Null(player.Choice);

		// card1 应该�?hand，card2 应该�?graveyard
		Assert.Equal(Zone.Hand, card1.Zone);
		Assert.Equal(Zone.Graveyard, card2.Zone);
	}

	[Fact]
	public void Phase10a_Discover_SetsChoiceWrapper()
	{
		var game = new Game(new Player("P1"), new Player("P2"));
		game.Setup();
		var player = game.Player1;

		var card1 = new Minion { MaxHealth = 1, Controller = player };
		var card2 = new Minion { MaxHealth = 2, Controller = player };
		var card3 = new Minion { MaxHealth = 3, Controller = player };
		game.RegisterEntity(card1);
		game.RegisterEntity(card2);
		game.RegisterEntity(card3);

		var discover = new Discover(player);
		var cards = new List<BaseEntity> { card1, card2, card3 };
		// 手动调用 Do
		discover.Do(player, player, new object[] { cards });

		Assert.NotNull(player.Choice);
		Assert.Equal(3, player.Choice!.Cards.Count);

		// Choose
		player.Choice.Choose(card2);
		Assert.Null(player.Choice);
	}

	[Fact]
	public void Phase10a_RandomCardPicker_FiltersCards()
	{
		var game = new Game(new Player("P1"), new Player("P2"));
		game.Setup();

		// 设置一个小�?CardDb
		game.CardDb = new Dictionary<string, Card>
		{
			["MINION1"] = new Card
			{
				CardId = "MINION1",
				CardType = CardType.Minion,
				Collectible = true,
				Cost = 3,
				CardSet = CardSet.Core,
				Races = Array.Empty<Race>()
			},
			["SPELL1"] = new Card
			{
				CardId = "SPELL1",
				CardType = CardType.Spell,
				Collectible = true,
				Cost = 2,
				CardSet = CardSet.Core,
				Races = Array.Empty<Race>()
			},
			["MINION2"] = new Card
			{
				CardId = "MINION2",
				CardType = CardType.Minion,
				Collectible = false,
				Cost = 1,
				CardSet = CardSet.Core,
				Races = Array.Empty<Race>()
			},
		};

		var source = new Minion { MaxHealth = 1 };
		source.Game = game;

		// Filter: collectible minions
		var picker = new RandomCardPicker(new Dictionary<string, object>
		{
			["collectible"] = true,
			["type"] = CardType.Minion,
		});

		var found = picker.FindCards(source);
		Assert.Single(found); // only MINION1 (MINION2 is non-collectible)
		Assert.Contains("MINION1", found);
	}

	[Fact]
	public void Phase10a_WeightedCardChoice_SamplesWithoutReplacement()
	{
		var game = new Game(new Player("P1"), new Player("P2"), seed: 42);
		game.Setup();
		var source = new Minion { MaxHealth = 1 };
		source.Game = game;

		var pool = new List<string> { "A", "B", "C", "D", "E" };
		var weights = new List<int> { 1 };
		var cardSets = new List<List<string>> { pool };

		var result = RandomCardPicker.WeightedCardChoice(source, weights, cardSets, 3);
		Assert.Equal(3, result.Count);

		// 无放�?�?无重�?
		Assert.Equal(result.Distinct().Count(), result.Count);
	}

	[Fact]
	public void Phase10a_RandomCards_FactoryMethods()
	{
		// ͨ�� ToPicker() תΪ RandomCardPicker ����� Filters
		var minion = RandomCards.RandomMinion().ToPicker();
		Assert.True(minion.Filters.ContainsKey("collectible"));
		Assert.True(minion.Filters.ContainsKey("type"));
		Assert.Equal(CardType.Minion, minion.Filters["type"]);

		var spell = RandomCards.RandomSpell().ToPicker();
		Assert.Equal(CardType.Spell, spell.Filters["type"]);

		var beast = RandomCards.RandomBeast().ToPicker();
		Assert.Equal(Race.Beast, beast.Filters["race"]);

		var legendary = RandomCards.RandomLegendaryMinion().ToPicker();
		Assert.Equal(Rarity.Legendary, legendary.Filters["rarity"]);
	}

	[Fact]
	public void Phase10a_RandomCardPicker_WithCount_And_CopyWithWeighting()
	{
		// RandomCardPicker WithCount / CopyWithWeighting
		var picker = new RandomCardPicker(new Dictionary<string, object>
		{
			["collectible"] = true,
		});
		Assert.Equal(1, picker.Count);

		var tripled = picker.WithCount(3);
		Assert.Equal(3, tripled.Count);
		Assert.Equal(1, picker.Count); // original unchanged

		var weighted = tripled.CopyWithWeighting(2, new Dictionary<string, object>
		{
			["card_class"] = CardClass.Mage,
		});
		Assert.Single(weighted.Weights);
		Assert.Equal(2, weighted.Weights[0]);
		Assert.Single(weighted.WeightedFilters);
		Assert.Equal(3, weighted.Count); // inherited count

		// original not affected
		Assert.Empty(tripled.Weights);
	}

	[Fact]
	public void Phase10a_Player_CreateCardFromString()
	{
		var game = new Game(new Player("P1"), new Player("P2"));
		game.Setup();

		game.CardDb = new Dictionary<string, Card>
		{
			["TEST_001"] = new Card
			{
				CardId = "TEST_001",
				CardType = CardType.Minion,
				Cost = 3,
				Attack = 2,
				Health = 4,
				Races = Array.Empty<Race>(),
			},
		};

		var entity = game.Player1.CreateCard("TEST_001");
		Assert.NotNull(entity);
		Assert.IsType<Minion>(entity);
		Assert.Equal(3, entity!.Cost);

		// 找不到的 ID 返回 null
		var missing = game.Player1.CreateCard("NONEXISTENT");
		Assert.Null(missing);
	}
}

