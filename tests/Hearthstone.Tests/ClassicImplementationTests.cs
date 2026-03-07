using System.Linq;
using HearthstoneServer;
using HearthstoneServer.Dsl;
using HearthstoneServer.Entities;
using HearthstoneServer.Events;
using CardData = HearthstoneServer.LoadCardIntoDbf.Card;

namespace Hearthstone.Tests;

public class ClassicImplementationTests
{
	private static Game CreateGame()
	{
		CardScriptRegistry.Instance.Initialize();

		var p1 = new Player("Player1") { Hero = new Hero { MaxHealth = 30 } };
		var p2 = new Player("Player2") { Hero = new Hero { MaxHealth = 30 } };
		var game = new Game(p1, p2, seed: 42)
		{
			CardDb = new Dictionary<string, CardData>(StringComparer.Ordinal)
			{
				["EX1_398t"] = new() { CardId = "EX1_398t", CardType = CardType.Weapon, Attack = 2, Durability = 2, Cost = 2 },
				["EX1_554t"] = new() { CardId = "EX1_554t", CardType = CardType.Minion, Attack = 1, Health = 1, Cost = 1 },
				["TEST_COPY_A"] = new() { CardId = "TEST_COPY_A", CardType = CardType.Minion, Attack = 2, Health = 2, Cost = 2 },
				["TEST_COPY_B"] = new() { CardId = "TEST_COPY_B", CardType = CardType.Minion, Attack = 4, Health = 4, Cost = 4 },
				["TEST_DRAGON"] = new() { CardId = "TEST_DRAGON", CardType = CardType.Minion, Attack = 3, Health = 3, Cost = 3, Races = new[] { Race.Dragon } },
			}
		};
		game.Setup();
		return game;
	}

	private static void RunActions(BaseEntity source, IEnumerable<HearthstoneServer.Dsl.Action> actions, BaseEntity? target = null)
	{
		source.CurrentTarget = target;
		try
		{
			foreach (var action in actions)
			{
				if (action is TargetedAction ta) ta.Trigger(source);
				else if (action is GameAction ga) ga.Trigger(source);
			}
		}
		finally
		{
			source.CurrentTarget = null;
		}
	}

	private static void ActivateScript(Game game, BuffableEntity entity)
	{
		CardScriptRegistry.Instance.GetScript(entity.CardId ?? string.Empty)?.OnActivate?.Invoke(entity, game);
	}

	private static void ReindexField(Player player)
	{
		int pos = 1;
		foreach (var minion in player.Field.OfType<Minion>())
			minion.ZonePosition = pos++;
	}

	private static Minion CreateMinion(Game game, Player owner, string cardId, int atk = 1, int health = 1, Zone zone = Zone.Hand, Race[]? races = null, int cost = 1)
	{
		var minion = new Minion
		{
			Controller = owner,
			CardData = new CardData
			{
				CardId = cardId,
				CardType = CardType.Minion,
				Attack = atk,
				Health = health,
				Cost = cost,
				Races = races ?? Array.Empty<Race>(),
			},
			Data = new CardData
			{
				CardId = cardId,
				CardType = CardType.Minion,
				Attack = atk,
				Health = health,
				Cost = cost,
				Races = races ?? Array.Empty<Race>(),
			},
			MaxHealth = health,
			Atk = atk,
		};
		minion.SetIntProperty("cost", cost);
		game.RegisterEntity(minion);
		minion.SetZone(zone, owner);
		ReindexField(owner);
		return minion;
	}

	private static Spell CreateSpell(Game game, Player owner, string cardId, int cost = 1, Zone zone = Zone.Hand)
	{
		var spell = new Spell
		{
			Controller = owner,
			CardData = new CardData
			{
				CardId = cardId,
				CardType = CardType.Spell,
				Cost = cost,
			},
			Data = new CardData
			{
				CardId = cardId,
				CardType = CardType.Spell,
				Cost = cost,
			},
			Cost = cost,
		};
		game.RegisterEntity(spell);
		spell.SetZone(zone, owner);
		return spell;
	}

	private static Weapon CreateWeapon(Game game, Player owner, string cardId, int atk = 1, int durability = 2, Zone zone = Zone.Invalid)
	{
		var weapon = new Weapon
		{
			Controller = owner,
			CardData = new CardData
			{
				CardId = cardId,
				CardType = CardType.Weapon,
				Attack = atk,
				Durability = durability,
				Cost = 1,
			},
			Data = new CardData
			{
				CardId = cardId,
				CardType = CardType.Weapon,
				Attack = atk,
				Durability = durability,
				Cost = 1,
			}
		};
		weapon.Atk = atk;
		weapon.MaxDurability = durability;
		game.RegisterEntity(weapon);
		weapon.SetZone(zone, owner);
		return weapon;
	}

	[Fact]
	public void ClassicWarrior_RegistersMissingWeapons()
	{
		CardScriptRegistry.Instance.Initialize();

		Assert.NotNull(CardScriptRegistry.Instance.GetScript("CS2_106"));
		Assert.NotNull(CardScriptRegistry.Instance.GetScript("CS2_112"));
	}

	[Fact]
	public void CS1_129_SetsAttackEqualToCurrentHealth()
	{
		var game = CreateGame();
		var target = CreateMinion(game, game.Player1, "TEST_TARGET", atk: 1, health: 5, zone: Zone.Play);
		target.DamageTaken = 2;
		var spell = CreateSpell(game, game.Player1, "CS1_129");

		RunActions(spell, CardScriptRegistry.Instance.GetScript("CS1_129")!.Play, target);

		Assert.Equal(3, target.Atk);
	}

	[Fact]
	public void EX1_339_CopiesTwoRandomCardsFromEnemyDeckWithoutRemovingThem()
	{
		var game = CreateGame();
		CreateMinion(game, game.Player2, "TEST_COPY_A", zone: Zone.Deck, cost: 2);
		CreateMinion(game, game.Player2, "TEST_COPY_B", zone: Zone.Deck, cost: 4);
		var spell = CreateSpell(game, game.Player1, "EX1_339");
		int handBefore = game.Player1.Hand.Count;
		int deckBefore = game.Player2.Deck.Count;

		RunActions(spell, CardScriptRegistry.Instance.GetScript("EX1_339")!.Play);

		Assert.Equal(deckBefore, game.Player2.Deck.Count);
		Assert.Equal(handBefore + 2, game.Player1.Hand.Count);
		Assert.Contains(game.Player1.Hand, c => (c as BuffableEntity)?.CardId == "TEST_COPY_A");
		Assert.Contains(game.Player1.Hand, c => (c as BuffableEntity)?.CardId == "TEST_COPY_B");
	}

	[Fact]
	public void EX1_304_ConsumesAdjacentMinionsAndGainsTheirStats()
	{
		var game = CreateGame();
		var left = CreateMinion(game, game.Player1, "LEFT", atk: 2, health: 3, zone: Zone.Play);
		var center = CreateMinion(game, game.Player1, "EX1_304", atk: 3, health: 3, zone: Zone.Play);
		var right = CreateMinion(game, game.Player1, "RIGHT", atk: 1, health: 4, zone: Zone.Play);
		ReindexField(game.Player1);

		RunActions(center, CardScriptRegistry.Instance.GetScript("EX1_304")!.Battlecry);
		game.ProcessDeaths();

		Assert.Equal(6, center.Atk);
		Assert.Equal(10, center.Health);
		Assert.DoesNotContain(left, game.Player1.Field);
		Assert.DoesNotContain(right, game.Player1.Field);
	}

	[Fact]
	public void EX1_315_ReducesFriendlyMinionCostsByTwoButNotBelowOne()
	{
		var game = CreateGame();
		CreateMinion(game, game.Player1, "EX1_315", atk: 0, health: 4, zone: Zone.Play, cost: 4);
		var minionA = CreateMinion(game, game.Player1, "HAND_MINION_A", cost: 5);
		var minionB = CreateMinion(game, game.Player1, "HAND_MINION_B", cost: 1);
		var spell = CreateSpell(game, game.Player1, "HAND_SPELL", cost: 5);

		game.RefreshAuras();

		Assert.Equal(3, minionA.Cost);
		Assert.Equal(1, minionB.Cost);
		Assert.Equal(5, spell.Cost);
	}

	[Fact]
	public void EX1_398_EquipsBattleAxe()
	{
		var game = CreateGame();
		var minion = CreateMinion(game, game.Player1, "EX1_398", atk: 3, health: 3, zone: Zone.Play);

		RunActions(minion, CardScriptRegistry.Instance.GetScript("EX1_398")!.Battlecry);

		Assert.NotNull(game.Player1.Hero!.EquippedWeapon);
		Assert.Equal("EX1_398t", game.Player1.Hero.EquippedWeapon!.CardId);
		Assert.Equal(2, game.Player1.Hero.EquippedWeapon.Atk);
	}

	[Fact]
	public void EX1_554_SummonsThreeSnakesWhenFriendlyMinionAttacked()
	{
		var game = CreateGame();
		var secret = CreateSpell(game, game.Player1, "EX1_554", zone: Zone.Secret);
		ActivateScript(game, secret);
		var defender = CreateMinion(game, game.Player1, "FRIENDLY_DEFENDER", zone: Zone.Play);
		var attacker = CreateMinion(game, game.Player2, "ENEMY_ATTACKER", zone: Zone.Play);
		int fieldBefore = game.Player1.Field.Count;

		game.EventBus.Publish(new AttackEvent(attacker, attacker, defender), CombatPhase.Preparation);

		Assert.Equal(fieldBefore + 3, game.Player1.Field.Count);
		Assert.Equal(3, game.Player1.Field.OfType<Minion>().Count(m => m.CardId == "EX1_554t"));
	}

	[Fact]
	public void EX1_533_RedirectsAttackFromHeroToRandomOtherCharacter()
	{
		var game = CreateGame();
		var secret = CreateSpell(game, game.Player1, "EX1_533", zone: Zone.Secret);
		ActivateScript(game, secret);
		var attacker = CreateMinion(game, game.Player2, "ENEMY_ATTACKER", zone: Zone.Play);
		CreateMinion(game, game.Player1, "FRIENDLY_OTHER", zone: Zone.Play);
		CreateMinion(game, game.Player2, "ENEMY_OTHER", zone: Zone.Play);
		game.ProposedAttacker = attacker;
		game.ProposedDefender = game.Player1.Hero!;

		game.EventBus.Publish(new ProposedAttackEvent(attacker, attacker, game.Player1.Hero!), CombatPhase.Preparation);

		Assert.NotNull(game.ProposedDefender);
		Assert.NotEqual(game.Player1.Hero, game.ProposedDefender);
		Assert.NotEqual(attacker, game.ProposedDefender);
	}

	[Fact]
	public void EX1_128_StealthExpiresAtStartOfNextFriendlyTurn()
	{
		var game = CreateGame();
		var first = CreateMinion(game, game.Player1, "FIRST", zone: Zone.Play);
		var second = CreateMinion(game, game.Player1, "SECOND", zone: Zone.Play);
		var spell = CreateSpell(game, game.Player1, "EX1_128");

		RunActions(spell, CardScriptRegistry.Instance.GetScript("EX1_128")!.Play);

		Assert.True(first.Stealth);
		Assert.True(second.Stealth);

		game.EventBus.Publish(new TurnStartEvent(game.Player1.Hero!, game.Player1), TurnPhase.Start);

		Assert.False(first.Stealth);
		Assert.False(second.Stealth);
	}

	[Fact]
	public void GVG_123_DealsTwoWhenHoldingDragon()
	{
		var game = CreateGame();
		CreateMinion(game, game.Player1, "TEST_DRAGON", zone: Zone.Hand, races: new[] { Race.Dragon }, cost: 3);
		var minion = CreateMinion(game, game.Player1, "GVG_123", atk: 3, health: 3, zone: Zone.Play);
		var enemyHero = game.Player2.Hero!;

		RunActions(minion, CardScriptRegistry.Instance.GetScript("GVG_123")!.Battlecry, enemyHero);

		Assert.Equal(28, enemyHero.Health);
	}

	[Fact]
	public void EX1_295_SetsFatalPreDamageToZero()
	{
		var game = CreateGame();
		var secret = CreateSpell(game, game.Player1, "EX1_295", zone: Zone.Secret);
		ActivateScript(game, secret);
		var source = CreateMinion(game, game.Player2, "ENEMY_SOURCE", atk: 4, health: 4, zone: Zone.Play);

		new DealDamage(game.Player1.Hero!, 40).Trigger(source);

		Assert.Equal(30, game.Player1.Hero!.Health);
		Assert.True(game.Player1.Hero.Immune);

		new DealDamage(game.Player1.Hero!, 5).Trigger(source);

		Assert.Equal(30, game.Player1.Hero.Health);

		game.EventBus.Publish(new TurnEndEvent(game.Player2.Hero!, game.Player2), TurnPhase.End);

		Assert.False(game.Player1.Hero.Immune);
	}

	[Fact]
	public void EX1_349_DrawsUntilHandSizeMatchesOpponent()
	{
		var game = CreateGame();
		CreateMinion(game, game.Player1, "DRAW_A", zone: Zone.Deck);
		CreateMinion(game, game.Player1, "DRAW_B", zone: Zone.Deck);
		CreateMinion(game, game.Player1, "DRAW_C", zone: Zone.Deck);
		CreateMinion(game, game.Player1, "DRAW_D", zone: Zone.Deck);
		CreateMinion(game, game.Player2, "OPP_1");
		CreateMinion(game, game.Player2, "OPP_2");
		CreateMinion(game, game.Player2, "OPP_3");
		CreateMinion(game, game.Player2, "OPP_4");
		CreateMinion(game, game.Player1, "MY_EXISTING");
		var spell = CreateSpell(game, game.Player1, "EX1_349", zone: Zone.Invalid);

		RunActions(spell, CardScriptRegistry.Instance.GetScript("EX1_349")!.Play);

		Assert.Equal(game.Player2.Hand.Count, game.Player1.Hand.Count);
	}

	[Fact]
	public void EX1_365_DealsDamageEqualToDrawnCardCost()
	{
		var game = CreateGame();
		CreateMinion(game, game.Player1, "DRAWN_COST_7", atk: 7, health: 7, zone: Zone.Deck, cost: 7);
		var spell = CreateSpell(game, game.Player1, "EX1_365", zone: Zone.Invalid);
		var target = game.Player2.Hero!;

		RunActions(spell, CardScriptRegistry.Instance.GetScript("EX1_365")!.Play, target);

		Assert.Equal(23, target.Health);
	}

	[Fact]
	public void EX1_132_ReflectsExactHeroDamageToEnemyHero()
	{
		var game = CreateGame();
		var secret = CreateSpell(game, game.Player1, "EX1_132", zone: Zone.Secret);
		ActivateScript(game, secret);
		var source = CreateMinion(game, game.Player2, "ENEMY_SOURCE", atk: 3, health: 3, zone: Zone.Play);

		new DealDamage(game.Player1.Hero!, 4).Trigger(source);

		Assert.Equal(26, game.Player1.Hero!.Health);
		Assert.Equal(26, game.Player2.Hero!.Health);
	}

	[Fact]
	public void NEW1_036_UsesHeroAuraToKeepFriendlyMinionsAtOne()
	{
		var game = CreateGame();
		CreateMinion(game, game.Player1, "DRAW_ONE", zone: Zone.Deck);
		var target = CreateMinion(game, game.Player1, "FRIENDLY_MINION", atk: 2, health: 3, zone: Zone.Play);
		var source = CreateMinion(game, game.Player2, "ENEMY_SOURCE", atk: 6, health: 6, zone: Zone.Play);
		var spell = CreateSpell(game, game.Player1, "NEW1_036", zone: Zone.Invalid);

		RunActions(spell, CardScriptRegistry.Instance.GetScript("NEW1_036")!.Play);
		new DealDamage(target, 10).Trigger(source);

		Assert.Equal(1, target.Health);

		new DealDamage(game.Player1.Hero!, 5).Trigger(source);

		Assert.Equal(25, game.Player1.Hero!.Health);

		game.EventBus.Publish(new TurnEndEvent(game.Player1.Hero!, game.Player1), TurnPhase.End);
		new DealDamage(target, 2).Trigger(source);

		Assert.True(target.Dead || target.Health <= 0);
	}

	[Fact]
	public void EX1_411_ReplacesDurabilityLossWithAttackLossWhenAttackingMinion()
	{
		var game = CreateGame();
		var weapon = CreateWeapon(game, game.Player1, "EX1_411", atk: 3, durability: 2, zone: Zone.Play);
		game.Player1.Hero!.EquippedWeapon = weapon;
		ActivateScript(game, weapon);
		var target = CreateMinion(game, game.Player2, "DEFENDER", atk: 1, health: 2, zone: Zone.Play);

		game.Sequences.Combat(game.Player1.Hero!, target);

		Assert.Equal(2, weapon.Atk);
		Assert.Equal(2, weapon.Durability);
	}

	[Fact]
	public void EX1_411_StillLosesDurabilityWhenAttackingHero()
	{
		var game = CreateGame();
		var weapon = CreateWeapon(game, game.Player1, "EX1_411", atk: 3, durability: 2, zone: Zone.Play);
		game.Player1.Hero!.EquippedWeapon = weapon;
		ActivateScript(game, weapon);

		game.Sequences.Combat(game.Player1.Hero!, game.Player2.Hero!);

		Assert.Equal(3, weapon.Atk);
		Assert.Equal(1, weapon.Durability);
	}

	[Fact]
	public void PART_004_RemovesStealthAtOwnersTurnStart()
	{
		var game = CreateGame();
		var target = CreateMinion(game, game.Player1, "TARGET", atk: 2, health: 2, zone: Zone.Play);
		var spell = CreateSpell(game, game.Player1, "PART_004", zone: Zone.Invalid);

		RunActions(spell, CardScriptRegistry.Instance.GetScript("PART_004")!.Play, target);

		Assert.True(target.Stealth);

		game.EventBus.Publish(new TurnStartEvent(game.Player1.Hero!, game.Player1), TurnPhase.Start);

		Assert.False(target.Stealth);
	}
}
