using System.Linq;
using HearthstoneServer;
using HearthstoneServer.Dsl;
using HearthstoneServer.Entities;
using HearthstoneServer.Events;
using HearthstoneServer.Targeting;
using CardData = HearthstoneServer.LoadCardIntoDbf.Card;
using static HearthstoneServer.Cards.CardScriptUtils;
using static HearthstoneServer.Dsl.BusEvent;
using static HearthstoneServer.Dsl.S;

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
		return new Game(p1, p2, seed: 42)
		{
			CardDb = new Dictionary<string, CardData>(StringComparer.Ordinal)
			{
				["CS2_082"] = new() { CardId = "CS2_082", CardType = CardType.Weapon, Attack = 1, Durability = 2, Cost = 2 },
				["AT_132_ROGUEt_H1"] = new() { CardId = "AT_132_ROGUEt_H1", CardType = CardType.Weapon, Attack = 2, Durability = 2, Cost = 2 },
				["HERO_10bp2"] = new() { DbfId = 60483, CardId = "HERO_10bp2", CardType = CardType.HeroPower, Cost = 2 },
				["HERO_11bp2"] = new() { DbfId = 79943, CardId = "HERO_11bp2", CardType = CardType.HeroPower, Cost = 2 },
				["HERO_11bpt"] = new()
				{
					DbfId = 78164,
					CardId = "HERO_11bpt",
					CardType = CardType.Minion,
					Attack = 1,
					Health = 1,
					Cost = 1,
					TagsInt = new Dictionary<GameTag, int> { [GameTag.Charge] = 1 },
				},
				["HERO_11bp2t"] = new()
				{
					DbfId = 87918,
					CardId = "HERO_11bp2t",
					CardType = CardType.Minion,
					Attack = 2,
					Health = 1,
					Cost = 1,
					TagsInt = new Dictionary<GameTag, int> { [GameTag.Charge] = 1 },
				},
				["AT_132_SHAMANa"] = new() { CardId = "AT_132_SHAMANa", CardType = CardType.Minion, Attack = 0, Health = 2, Cost = 1 },
				["AT_132_SHAMANb"] = new() { CardId = "AT_132_SHAMANb", CardType = CardType.Minion, Attack = 1, Health = 1, Cost = 1 },
				["AT_132_SHAMANc"] = new() { CardId = "AT_132_SHAMANc", CardType = CardType.Minion, Attack = 0, Health = 2, Cost = 1 },
				["AT_132_SHAMANd"] = new() { CardId = "AT_132_SHAMANd", CardType = CardType.Minion, Attack = 0, Health = 2, Cost = 1 },
				["AT_132_SHAMANe"] = new() { CardId = "AT_132_SHAMANe", CardType = CardType.Minion, Attack = 0, Health = 2, Cost = 1 },
				["TEST_COPY_TARGET"] = new() { CardId = "TEST_COPY_TARGET", CardType = CardType.Minion, Attack = 4, Health = 4, Cost = 4 },
				["TEST_AT002_MATCH"] = new() { CardId = "TEST_AT002_MATCH", CardType = CardType.Minion, Attack = 3, Health = 3, Cost = 4, CardSet = CardSet.Core, Collectible = true },
				["TEST_AT002_OTHER"] = new() { CardId = "TEST_AT002_OTHER", CardType = CardType.Minion, Attack = 5, Health = 5, Cost = 6, CardSet = CardSet.Core, Collectible = true },
				["TEST_RANDOM_SPELL_A"] = new() { CardId = "TEST_RANDOM_SPELL_A", CardType = CardType.Spell, Cost = 4, SpellSchool = SpellSchool.Arcane, CardSet = CardSet.Core },
				["TEST_RANDOM_SPELL_B"] = new() { CardId = "TEST_RANDOM_SPELL_B", CardType = CardType.Spell, Cost = 6, SpellSchool = SpellSchool.Fire, CardSet = CardSet.Core },
			}
		};
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
		minion.Data = minion.CardData;
		minion.SetIntProperty("cost", 0);
		game.RegisterEntity(minion);
		minion.SetZone(Zone.Hand);
		return minion;
	}

	private static Minion CreateMinion(Game game, Player owner, string cardId, Race[]? races, int atk = 1, int health = 1, int cost = 1)
	{
		var minion = new Minion
		{
			Controller = owner,
			MaxHealth = health,
			Atk = atk,
			CardData = new CardData
			{
				CardId = cardId,
				CardType = CardType.Minion,
				Attack = atk,
				Health = health,
				Cost = cost,
				Races = races ?? Array.Empty<Race>(),
			},
		};
		minion.Data = minion.CardData;
		minion.SetIntProperty("cost", cost);
		game.RegisterEntity(minion);
		minion.SetZone(Zone.Hand);
		return minion;
	}

	private static Weapon CreateWeapon(Game game, Player owner, string cardId, int atk = 1, int durability = 2)
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
		};
		weapon.Data = weapon.CardData;
		weapon.Atk = atk;
		weapon.MaxDurability = durability;
		game.RegisterEntity(weapon);
		return weapon;
	}

	private static HeroPower CreateHeroPower(Game game, Player owner, string cardId, int cost = 2, int upgradedHeroPowerDbfId = 0)
	{
		var tags = new Dictionary<GameTag, int>();
		if (upgradedHeroPowerDbfId > 0)
			tags[GameTag.UpgradedHeroPower] = upgradedHeroPowerDbfId;

		var power = new HeroPower
		{
			Controller = owner,
			CardData = new CardData
			{
				CardId = cardId,
				CardType = CardType.HeroPower,
				Cost = cost,
				TagsInt = tags,
			},
			Cost = cost,
		};
		power.Data = power.CardData;
		game.RegisterEntity(power);
		power.SetZone(Zone.Play, owner);
		owner.Hero!.Power = power;
		return power;
	}

	private static Spell CreateSpell(Game game, Player owner, string cardId, int cost = 0)
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
			Cost = cost,
		};
		spell.Data = spell.CardData;
		game.RegisterEntity(spell);
		spell.SetZone(Zone.Hand, owner);
		return spell;
	}

	private static void ActivateScript(Game game, BuffableEntity entity)
	{
		CardScriptRegistry.Instance.GetScript(entity.CardId ?? string.Empty)?.OnActivate?.Invoke(entity, game);
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

	[Fact]
	public void When_ActionArray_RunsInOrder()
	{
		var game = CreateTestGame();
		var builder = new CardBuilder();
		game.Setup();
		game.BeginTurn(game.Player1);
		CardScriptRegistry.Instance.Initialize();

		builder.Card("TEST_WHEN_ACTIONS",
			on: When<TurnEndEvent>(OWN, ON, Acts(
				new GainArmor(S.FRIENDLY_HERO, 1),
				new GainArmor(S.FRIENDLY_HERO, 2))));

		var triggerMinion = CreateMinion(game, game.Player1, atk: 1, health: 1, cardId: "TEST_WHEN_ACTIONS");
		game.PlayCard(triggerMinion);

		game.EndTurn();

		Assert.Equal(3, game.Player1.Hero!.Armor);
	}

	[Fact]
	public void Tgt_WhenWithEventTarget_ReplacesAt014Correctly()
	{
		var game = CreateTestGame();
		game.Setup();
		game.BeginTurn(game.Player1);
		CardScriptRegistry.Instance.Initialize();

		game.Player1.Deck.Clear();
		game.Player1.CreateCard(new CardData
		{
			CardId = "TEST_DRAW_TARGET",
			CardType = CardType.Minion,
			Attack = 3,
			Health = 3,
			Cost = 5,
		}, Zone.Deck);

		var shadowfiend = CreateMinion(game, game.Player1, atk: 3, health: 3, cardId: "AT_014");
		game.PlayCard(shadowfiend);

		var drawn = Assert.IsType<Minion>(game.Player1.DrawCard());

		Assert.Equal("TEST_DRAW_TARGET", drawn.CardId);
		Assert.Equal(4, drawn.Cost);
	}

	[Fact]
	public void DrawCardEvent_SourceIsActualCause_ForHeroPower()
	{
		var game = CreateTestGame();
		var builder = new CardBuilder();
		game.Setup();
		game.BeginTurn(game.Player1);
		CardScriptRegistry.Instance.Initialize();

		builder.Card("TEST_HERO_POWER_DRAW", play: new DrawCard(S.CONTROLLER));

		var power = new HeroPower
		{
			Controller = game.Player1,
			CardData = new CardData
			{
				CardId = "TEST_HERO_POWER_DRAW",
				CardType = CardType.HeroPower,
				Cost = 2,
			},
			Cost = 2,
		};
		game.RegisterEntity(power);
		power.SetZone(Zone.Play, game.Player1);
		game.Player1.Hero!.Power = power;
		game.Player1.MaxMana = 10;
		game.CurrentPlayer = game.Player1;

		game.Player1.Deck.Clear();
		game.Player1.CreateCard(new CardData
		{
			CardId = "TEST_DRAWN_BY_HERO_POWER",
			CardType = CardType.Minion,
			Attack = 1,
			Health = 1,
			Cost = 3,
		}, Zone.Deck);

		BaseEntity? observedSource = null;
		BaseEntity? observedTarget = null;
		game.EventBus.Subscribe<DrawCardEvent>(power, MiscPhase.On,
			_ => true,
			evt =>
			{
				observedSource = evt.Source;
				observedTarget = evt.Card;
			});

		game.UseHeroPower(game.Player1);

		Assert.Same(power, observedSource);
		Assert.NotNull(observedTarget);
		Assert.Equal("TEST_DRAWN_BY_HERO_POWER", Assert.IsType<Minion>(observedTarget).CardId);
	}

	[Fact]
	public void Tgt_At027_MakesHeroPowerDrawnCardCostZero()
	{
		var game = CreateTestGame();
		var builder = new CardBuilder();
		game.Setup();
		game.BeginTurn(game.Player1);
		CardScriptRegistry.Instance.Initialize();

		builder.Card("TEST_HERO_POWER_DRAW_FOR_WILFRED", play: new DrawCard(S.CONTROLLER));

		var power = new HeroPower
		{
			Controller = game.Player1,
			CardData = new CardData
			{
				CardId = "TEST_HERO_POWER_DRAW_FOR_WILFRED",
				CardType = CardType.HeroPower,
				Cost = 2,
			},
			Cost = 2,
		};
		game.RegisterEntity(power);
		power.SetZone(Zone.Play, game.Player1);
		game.Player1.Hero!.Power = power;
		game.Player1.MaxMana = 10;
		game.CurrentPlayer = game.Player1;

		game.Player1.Deck.Clear();
		game.Player1.CreateCard(new CardData
		{
			CardId = "TEST_WILFRED_DRAW",
			CardType = CardType.Minion,
			Attack = 5,
			Health = 5,
			Cost = 6,
		}, Zone.Deck);

		var wilfred = CreateMinion(game, game.Player1, atk: 4, health: 4, cardId: "AT_027");
		game.PlayCard(wilfred);

		game.UseHeroPower(game.Player1);

		var drawn = Assert.Single(game.Player1.Hand.OfType<Minion>(), card => card.CardId == "TEST_WILFRED_DRAW");
		Assert.Equal(0, drawn.Cost);
	}

	[Fact]
	public void AT_060_TriggersOnlyWhenFriendlyHeroIsAttacked()
	{
		var game = CreateTestGame();
		game.CardDb["CS2_125"] = new CardData { CardId = "CS2_125", CardType = CardType.Minion, Attack = 3, Health = 3, Cost = 3 };
		game.Setup();
		CardScriptRegistry.Instance.Initialize();

		game.Player1.CreateCard(new CardData
		{
			CardId = "AT_060",
			CardType = CardType.Spell,
			TagsInt = new Dictionary<GameTag, int> { [GameTag.Secret] = 1 },
		}, Zone.Secret);

		var attacker = CreateMinion(game, game.Player2, "TEST_ATTACKER", null, atk: 3, health: 3, cost: 3);
		attacker.SetZone(Zone.Play, game.Player2);
		var friendlyMinion = CreateMinion(game, game.Player1, "TEST_FRIENDLY", null, atk: 2, health: 2, cost: 2);
		friendlyMinion.SetZone(Zone.Play, game.Player1);

		game.EventBus.Publish(new AfterAttackEvent(attacker, attacker, friendlyMinion), CombatPhase.Combat);
		Assert.DoesNotContain(game.Player1.Field, entity => (entity as BuffableEntity)?.CardId == "CS2_125");

		game.EventBus.Publish(new AfterAttackEvent(attacker, attacker, game.Player1.Hero!), CombatPhase.Combat);
		Assert.Contains(game.Player1.Field, entity => (entity as BuffableEntity)?.CardId == "CS2_125");
	}

	[Fact]
	public void AT_063_DestroysOnlyDamagedEnemyMinions()
	{
		var game = CreateTestGame();
		game.Setup();
		game.BeginTurn(game.Player1);
		CardScriptRegistry.Instance.Initialize();
		game.Player1.MaxMana = 10;
		game.CurrentPlayer = game.Player1;

		var acidmaw = CreateMinion(game, game.Player1, "AT_063", null, atk: 2, health: 4, cost: 7);
		var friendly = CreateMinion(game, game.Player1, "TEST_FRIENDLY_TARGET", null, atk: 2, health: 3, cost: 2);
		var enemy = CreateMinion(game, game.Player2, "TEST_ENEMY_TARGET", null, atk: 2, health: 3, cost: 2);
		game.PlayCard(acidmaw);
		friendly.SetZone(Zone.Play, game.Player1);
		enemy.SetZone(Zone.Play, game.Player2);

		game.EventBus.Publish(new DamageEvent(game.Player2.Hero!, friendly, 1), MiscPhase.After);
		Assert.Equal(Zone.Play, friendly.Zone);

		game.EventBus.Publish(new DamageEvent(game.Player2.Hero!, enemy, 1), MiscPhase.After);
		game.ProcessDeaths();
		Assert.True(enemy.Dead || enemy.Zone != Zone.Play);
	}

	[Fact]
	public void AT_031_AddsCoinOnlyAfterAttackingHero()
	{
		var game = CreateTestGame();
		game.CardDb[THE_COIN] = new CardData { CardId = THE_COIN, CardType = CardType.Spell, Cost = 0 };
		game.Setup();
		game.BeginTurn(game.Player1);
		CardScriptRegistry.Instance.Initialize();
		game.Player1.MaxMana = 10;
		game.CurrentPlayer = game.Player1;

		var burglar = CreateMinion(game, game.Player1, "AT_031", null, atk: 3, health: 1, cost: 3);
		var enemyMinion = CreateMinion(game, game.Player2, "TEST_ENEMY_TARGET", null, atk: 2, health: 2, cost: 2);
		game.PlayCard(burglar);
		enemyMinion.SetZone(Zone.Play, game.Player2);

		game.EventBus.Publish(new AfterAttackEvent(burglar, burglar, enemyMinion), CombatPhase.Combat);
		Assert.DoesNotContain(game.Player1.Hand, entity => (entity as BuffableEntity)?.CardId == THE_COIN);

		game.EventBus.Publish(new AfterAttackEvent(burglar, burglar, game.Player2.Hero!), CombatPhase.Combat);
		Assert.Contains(game.Player1.Hand, entity => (entity as BuffableEntity)?.CardId == THE_COIN);
	}

	[Fact]
	public void Tgt_WhenWithEventAmount_ReplacesAt026Correctly()
	{
		var game = CreateTestGame();
		game.Setup();
		game.BeginTurn(game.Player1);
		CardScriptRegistry.Instance.Initialize();

		var wrathguard = CreateMinion(game, game.Player1, atk: 4, health: 3, cardId: "AT_026");
		game.PlayCard(wrathguard);
		int heroHealthBefore = game.Player1.Hero!.Health;

		new DealDamage(wrathguard, 2).Trigger(game.Player2.Hero!);

		Assert.Equal(1, wrathguard.Health);
		Assert.Equal(heroHealthBefore - 2, game.Player1.Hero.Health);
	}

	[Fact]
	public void AT_129_TriggersOnlyWhenSpellTargetsSelf()
	{
		var game = CreateTestGame();
		var builder = new CardBuilder();
		game.Setup();
		game.BeginTurn(game.Player1);
		CardScriptRegistry.Instance.Initialize();

		builder.Buff("TEST_SELF_TARGET_BUFF", atk: 1);
		builder.Card("TEST_SELF_TARGET_SPELL",
			play: new Enchant(S.TARGET, "TEST_SELF_TARGET_BUFF"),
			target: Target.Required().Friendly().Minion());

		var velen = CreateMinion(game, game.Player1, "AT_129", null, atk: 1, health: 4, cost: 3);
		var ally = CreateMinion(game, game.Player1, "TEST_ALLY", null, atk: 1, health: 2, cost: 1);
		ally.SetZone(Zone.Play, game.Player1);
		game.Player1.MaxMana = 10;
		game.CurrentPlayer = game.Player1;
		game.PlayCard(velen);

		var firstSpell = CreateSpell(game, game.Player1, "TEST_SELF_TARGET_SPELL");
		game.PlayCard(firstSpell, ally);
		Assert.False(velen.GetBoolProperty(GameTag.DivineShield));

		var secondSpell = CreateSpell(game, game.Player1, "TEST_SELF_TARGET_SPELL");
		game.PlayCard(secondSpell, velen);
		Assert.True(velen.GetBoolProperty(GameTag.DivineShield));
	}

	[Fact]
	public void AT_131_TriggersOnlyWhenSpellTargetsSelf()
	{
		var game = CreateTestGame();
		var builder = new CardBuilder();
		game.Setup();
		game.BeginTurn(game.Player1);
		CardScriptRegistry.Instance.Initialize();

		builder.Buff("TEST_SELF_DAMAGE_BUFF", atk: 1);
		builder.Card("TEST_SELF_DAMAGE_SPELL",
			play: new Enchant(S.TARGET, "TEST_SELF_DAMAGE_BUFF"),
			target: Target.Required().Friendly().Minion());

		var auchenai = CreateMinion(game, game.Player1, "AT_131", null, atk: 3, health: 5, cost: 4);
		var ally = CreateMinion(game, game.Player1, "TEST_ALLY_DAMAGE", null, atk: 1, health: 2, cost: 1);
		ally.SetZone(Zone.Play, game.Player1);
		game.Player1.MaxMana = 10;
		game.CurrentPlayer = game.Player1;
		game.PlayCard(auchenai);

		int healthBefore = game.Player2.Hero!.Health;
		var firstSpell = CreateSpell(game, game.Player1, "TEST_SELF_DAMAGE_SPELL");
		game.PlayCard(firstSpell, ally);
		Assert.Equal(healthBefore, game.Player2.Hero.Health);

		var secondSpell = CreateSpell(game, game.Player1, "TEST_SELF_DAMAGE_SPELL");
		game.PlayCard(secondSpell, auchenai);
		Assert.Equal(healthBefore - 3, game.Player2.Hero.Health);
	}

	[Fact]
	public void DeathEvent_SourceTracksActualCause()
	{
		var game = CreateTestGame();
		game.Setup();
		game.BeginTurn(game.Player1);

		var killer = CreateMinion(game, game.Player1, atk: 3, health: 3, cardId: "KILLER");
		var victim = CreateMinion(game, game.Player2, atk: 1, health: 2, cardId: "VICTIM");
		game.PlayCard(killer);
		game.CurrentPlayer = game.Player2;
		game.PlayCard(victim);
		game.CurrentPlayer = game.Player1;

		BaseEntity? observedSource = null;
		BaseEntity? observedDead = null;
		game.EventBus.Subscribe<DeathEvent>(killer, MiscPhase.On,
			_ => true,
			evt =>
			{
				observedSource = evt.Source;
				observedDead = evt.Dead;
			});

		new DealDamage(victim, 2).Trigger(killer);
		game.ProcessDeaths();

		Assert.Same(killer, observedSource);
		Assert.Same(victim, observedDead);
	}

	[Fact]
	public void DeathEventTargetIn_MatchesOnlyDeathrattleMinions()
	{
		CardScriptRegistry.Instance.Initialize();
		var builder = new CardBuilder(CardScriptRegistry.Instance);
		builder.Buff("TEST_DEATH_EVENT_BRIDGE_BUFF", atk: 1);
		builder.Card("TEST_DEATH_EVENT_LISTENER",
			on: When<DeathEvent>(ANY, AFTER,
				new Enchant(S.SELF, "TEST_DEATH_EVENT_BRIDGE_BUFF"),
				when: EventFilters.EventTargetIn<DeathEvent>(S.MINION + S.DEATHRATTLE)));

		var game = CreateTestGame();
		game.Setup();
		game.BeginTurn(game.Player1);
		game.Player1.MaxMana = 10;

		var listener = CreateMinion(game, game.Player1, "TEST_DEATH_EVENT_LISTENER", null, atk: 1, health: 1, cost: 1);
		game.PlayCard(listener);

		var deathrattleMinion = CreateMinion(game, game.Player2, "TEST_DEATHRATTLE_MINION", null, atk: 2, health: 2, cost: 2);
		deathrattleMinion.SetBoolProperty("has_deathrattle", true);
		deathrattleMinion.SetZone(Zone.Graveyard, game.Player2);

		var deathrattleWeapon = CreateWeapon(game, game.Player2, "TEST_DEATHRATTLE_WEAPON");
		deathrattleWeapon.SetBoolProperty("has_deathrattle", true);
		deathrattleWeapon.SetZone(Zone.Graveyard, game.Player2);

		game.EventBus.Publish(new DeathEvent(deathrattleMinion, deathrattleMinion), MiscPhase.After);
		game.EventBus.Publish(new DeathEvent(deathrattleWeapon, deathrattleWeapon), MiscPhase.After);

		Assert.Equal(2, listener.Atk);
	}

	[Fact]
	public void MinionSummonedEvent_BindsSourceToSummonerAndTargetToSummonedMinion()
	{
		CardScriptRegistry.Instance.Initialize();
		var builder = new CardBuilder(CardScriptRegistry.Instance);
		builder.Buff("TEST_SUMMON_TARGET_BUFF", atk: 1);
		builder.Card("TEST_SUMMON_LISTENER",
			on: When<MinionSummonedEvent>(OWN, AFTER,
				new Enchant(S.EVT_TARGET, "TEST_SUMMON_TARGET_BUFF"),
				when: EventFilters.EventSourceIn<MinionSummonedEvent>(S.SELF)));

		var game = CreateTestGame();
		game.Setup();

		var listener = CreateMinion(game, game.Player1, "TEST_SUMMON_LISTENER", null, atk: 1, health: 3, cost: 1);
		listener.SetZone(Zone.Play, game.Player1);
		ActivateScript(game, listener);

		var summoned = CreateMinion(game, game.Player1, "TEST_SUMMONED_MINION", null, atk: 1, health: 1, cost: 1);
		summoned.SetZone(Zone.Play, game.Player1);

		game.EventBus.Publish(new MinionSummonedEvent(listener, summoned), MinionPhase.AfterSummon);

		Assert.Equal(1, listener.Atk);
		Assert.Equal(2, summoned.Atk);
	}

	[Fact]
	public void GVG_040_DrawsOnlyWhenFriendlyMurlocDies()
	{
		var game = CreateTestGame();
		game.Setup();
		game.BeginTurn(game.Player1);
		CardScriptRegistry.Instance.Initialize();
		game.Player1.MaxMana = 10;
		game.CurrentPlayer = game.Player1;

		var oracle = CreateMinion(game, game.Player1, "GVG_040", null, atk: 2, health: 3, cost: 2);
		game.PlayCard(oracle);

		var murloc = CreateMinion(game, game.Player1, "TEST_MURLOC", new[] { global::HearthstoneServer.Race.Murloc }, atk: 1, health: 1, cost: 1);
		murloc.SetZone(Zone.Graveyard, game.Player1);
		var mech = CreateMinion(game, game.Player1, "TEST_MECH", new[] { global::HearthstoneServer.Race.Mechanical }, atk: 1, health: 1, cost: 1);
		mech.SetZone(Zone.Graveyard, game.Player1);

		int before = game.Player1.Hand.Count;
		game.EventBus.Publish(new DeathEvent(murloc, murloc), MiscPhase.After);
		Assert.Equal(before + 1, game.Player1.Hand.Count);

		game.EventBus.Publish(new DeathEvent(mech, mech), MiscPhase.After);
		Assert.Equal(before + 1, game.Player1.Hand.Count);
	}

	[Fact]
	public void EventTargetIn_MinionSummonedEvent_MatchesOnlyFriendlyPirates()
	{
		CardScriptRegistry.Instance.Initialize();
		var builder = new CardBuilder(CardScriptRegistry.Instance);
		builder.Buff("TEST_PIRATE_SUMMON_BUFF", atk: 1);
		builder.Card("TEST_PIRATE_SUMMON_LISTENER",
			on: When<MinionSummonedEvent>(OWN, AFTER,
				new Enchant(S.SELF, "TEST_PIRATE_SUMMON_BUFF"),
				when: EventFilters.EventTargetIn<MinionSummonedEvent>(S.MINION + S.PIRATE)));

		var game = CreateTestGame();
		game.Setup();

		var listener = CreateMinion(game, game.Player1, "TEST_PIRATE_SUMMON_LISTENER", null, atk: 1, health: 3, cost: 1);
		listener.SetZone(Zone.Play, game.Player1);
		ActivateScript(game, listener);

		var mech = CreateMinion(game, game.Player1, "TEST_MECH", new[] { global::HearthstoneServer.Race.Mechanical }, atk: 1, health: 1, cost: 1);
		mech.SetZone(Zone.Play, game.Player1);
		var pirate = CreateMinion(game, game.Player1, "TEST_PIRATE", new[] { global::HearthstoneServer.Race.Pirate }, atk: 1, health: 1, cost: 1);
		pirate.SetZone(Zone.Play, game.Player1);

		game.EventBus.Publish(new MinionSummonedEvent(mech, mech), MinionPhase.AfterSummon);
		Assert.Equal(1, listener.Atk);

		game.EventBus.Publish(new MinionSummonedEvent(pirate, pirate), MinionPhase.AfterSummon);
		Assert.Equal(2, listener.Atk);
	}

	[Fact]
	public void GVG_086_TriggersOnlyWhenFriendlyPlayerGainsArmor()
	{
		var game = CreateTestGame();
		game.Setup();
		game.BeginTurn(game.Player1);
		CardScriptRegistry.Instance.Initialize();
		game.Player1.MaxMana = 10;
		game.CurrentPlayer = game.Player1;

		var engine = CreateMinion(game, game.Player1, "GVG_086", null, atk: 3, health: 5, cost: 5);
		game.PlayCard(engine);

		game.EventBus.Publish(new GainArmorEvent(game.Player2.Hero!, game.Player2, 2), MiscPhase.On);
		Assert.Equal(3, engine.Atk);

		game.EventBus.Publish(new GainArmorEvent(game.Player1.Hero!, game.Player1, 2), MiscPhase.On);
		Assert.Equal(4, engine.Atk);
	}

	[Fact]
	public void GVG_113_TriggersOnlyWhenSelfAttacks()
	{
		var game = CreateTestGame();
		game.Setup();
		game.BeginTurn(game.Player1);
		CardScriptRegistry.Instance.Initialize();
		game.Player1.MaxMana = 10;
		game.CurrentPlayer = game.Player1;

		var reaper = CreateMinion(game, game.Player1, "GVG_113", null, atk: 3, health: 6, cost: 6);
		var otherAttacker = CreateMinion(game, game.Player1, "TEST_OTHER_ATTACKER", null, atk: 2, health: 2, cost: 2);
		var left = CreateMinion(game, game.Player2, "TEST_LEFT", null, atk: 1, health: 5, cost: 1);
		var defender = CreateMinion(game, game.Player2, "TEST_DEFENDER", null, atk: 1, health: 5, cost: 1);
		var right = CreateMinion(game, game.Player2, "TEST_RIGHT", null, atk: 1, health: 5, cost: 1);
		game.PlayCard(reaper);
		otherAttacker.SetZone(Zone.Play, game.Player1);
		left.SetZone(Zone.Play, game.Player2);
		defender.SetZone(Zone.Play, game.Player2);
		right.SetZone(Zone.Play, game.Player2);

		reaper.CurrentTarget = defender;
		game.EventBus.Publish(new AfterAttackEvent(otherAttacker, otherAttacker, defender), CombatPhase.Combat);
		Assert.Equal(5, left.Health);
		Assert.Equal(5, right.Health);

		reaper.CurrentTarget = defender;
		game.EventBus.Publish(new AfterAttackEvent(reaper, reaper, defender), CombatPhase.Combat);
		Assert.Equal(2, left.Health);
		Assert.Equal(2, right.Health);
	}

	[Fact]
	public void GVG_116_TriggersOnlyWhenEnemyMinionDies()
	{
		var game = CreateTestGame();
		game.CardDb["EX1_029"] = new CardData { CardId = "EX1_029", CardType = CardType.Minion, Attack = 1, Health = 1, Cost = 1 };
		game.Setup();
		CardScriptRegistry.Instance.Initialize();

		var engineer = CreateMinion(game, game.Player1, "GVG_116", null, atk: 9, health: 7, cost: 9);
		engineer.SetZone(Zone.Play, game.Player1);
		ActivateScript(game, engineer);

		var enemyMinion = CreateMinion(game, game.Player2, "TEST_ENEMY_MINION", null, atk: 2, health: 2, cost: 2);
		enemyMinion.SetZone(Zone.Graveyard, game.Player2);
		var enemyWeapon = CreateWeapon(game, game.Player2, "TEST_ENEMY_WEAPON");
		enemyWeapon.SetZone(Zone.Graveyard, game.Player2);

		game.EventBus.Publish(new DeathEvent(enemyMinion, enemyMinion), MiscPhase.After);
		Assert.Single(game.Player1.Field.OfType<BuffableEntity>(), entity => entity.CardId == "EX1_029");

		game.EventBus.Publish(new DeathEvent(enemyWeapon, enemyWeapon), MiscPhase.After);
		Assert.Single(game.Player1.Field.OfType<BuffableEntity>(), entity => entity.CardId == "EX1_029");
	}

	[Fact]
	public void DealDamageRandomOnce_UsesSingleRollForAllTargets()
	{
		var game = CreateTestGame();
		game.Setup();

		var m1 = CreateMinion(game, game.Player1, "TEST_RANDOM_AOE_1", null, health: 6);
		var m2 = CreateMinion(game, game.Player2, "TEST_RANDOM_AOE_2", null, health: 6);
		m1.SetZone(Zone.Play, game.Player1);
		m2.SetZone(Zone.Play, game.Player2);

		new DealDamageRandomOnce(S.ALL_MINIONS, 4, 5).Trigger(game.Player1.Hero!);

		int dealtToM1 = 6 - m1.Health;
		int dealtToM2 = 6 - m2.Health;

		Assert.Equal(dealtToM1, dealtToM2);
		Assert.InRange(dealtToM1, 4, 5);
	}

	[Fact]
	public void CancelWeaponEquip_OnlyWorksInBeforeEquip()
	{
		var game = CreateTestGame();
		game.Setup();

		var source = CreateMinion(game, game.Player1, atk: 1, health: 1, cardId: "TEST_CANCEL_WEAPON");
		var weapon = CreateWeapon(game, game.Player1, "TEST_PENDING_WEAPON");
		var evt = new WeaponEquipEvent(source, game.Player1, weapon);

		source.ActiveWeaponEquipEvent = evt;
		source.ActiveWeaponPhase = WeaponPhase.BeforeEquip;
		new CancelWeaponEquip().Trigger(source);
		Assert.True(evt.Cancelled);

		evt.Cancelled = false;
		source.ActiveWeaponPhase = WeaponPhase.OnEquip;
		Assert.Throws<InvalidOperationException>(() => new CancelWeaponEquip().Trigger(source));
	}

	[Fact]
	public void WeaponEquipEvent_SourceIsHeroPower_WhenHeroPowerEquipsWeapon()
	{
		var game = CreateTestGame();
		var builder = new CardBuilder();
		game.Setup();
		game.BeginTurn(game.Player1);
		CardScriptRegistry.Instance.Initialize();

		builder.Card("TEST_EQUIP_POWER", play: new EquipWeapon(S.CONTROLLER, "CS2_082"));

		var power = CreateHeroPower(game, game.Player1, "TEST_EQUIP_POWER");
		game.Player1.MaxMana = 10;
		game.CurrentPlayer = game.Player1;

		BaseEntity? observedSource = null;
		game.EventBus.Subscribe<WeaponEquipEvent>(power, WeaponPhase.BeforeEquip,
			_ => true,
			evt => observedSource = evt.Source);

		game.UseHeroPower(game.Player1);

		Assert.Same(power, observedSource);
	}

	[Fact]
	public void AT_007_DiscountsOnlyTheSpellAddedToController()
	{
		var game = CreateTestGame();
		var builder = new CardBuilder();
		game.Setup();
		game.BeginTurn(game.Player1);
		CardScriptRegistry.Instance.Initialize();

		builder.Card("TEST_AT_007",
			battlecry: Acts(
				new AddToHand(S.OPPONENT, "TEST_RANDOM_SPELL_B"),
				new AddToHand(S.CONTROLLER, "TEST_RANDOM_SPELL_A").Then(new Enchant(S.CURRENT_TARGET, "AT_007e"))));

		var existingSpell = CreateSpell(game, game.Player1, "TEST_EXISTING_SPELL", cost: 5);
		var curious = CreateMinion(game, game.Player1, atk: 3, health: 4, cardId: "TEST_AT_007");
		game.PlayCard(curious);

		var gainedSpell = Assert.Single(game.Player1.Hand.OfType<Spell>(), spell => !ReferenceEquals(spell, existingSpell));
		Assert.Equal("TEST_RANDOM_SPELL_A", gainedSpell.CardId);
		Assert.Equal(2, gainedSpell.Cost);
		Assert.Equal(5, existingSpell.Cost);
		Assert.Equal("TEST_RANDOM_SPELL_B", Assert.Single(game.Player2.Hand.OfType<Spell>()).CardId);
	}

	[Fact]
	public void AT_034_CancelsHeroPowerWeaponReplacementAndGainsAttack()
	{
		var game = CreateTestGame();
		game.Setup();
		game.BeginTurn(game.Player1);
		CardScriptRegistry.Instance.Initialize();

		var poisonedBlade = CreateWeapon(game, game.Player1, "AT_034");
		poisonedBlade.Equip();
		ActivateScript(game, poisonedBlade);
		CreateHeroPower(game, game.Player1, "CS2_083b");
		game.Player1.MaxMana = 10;
		game.CurrentPlayer = game.Player1;

		game.UseHeroPower(game.Player1);

		Assert.Same(poisonedBlade, game.Player1.Hero!.EquippedWeapon);
		Assert.Equal("AT_034", game.Player1.Hero.EquippedWeapon!.CardId);
		Assert.Equal(2, poisonedBlade.Atk);
	}

	[Fact]
	public void AT_034_DoesNotTrigger_WhenSourceIsNotHeroPower()
	{
		var game = CreateTestGame();
		var builder = new CardBuilder();
		game.Setup();
		game.BeginTurn(game.Player1);
		CardScriptRegistry.Instance.Initialize();

		builder.Card("TEST_EQUIP_MINION", battlecry: new EquipWeapon(S.CONTROLLER, "CS2_082"));

		var poisonedBlade = CreateWeapon(game, game.Player1, "AT_034");
		poisonedBlade.Equip();
		ActivateScript(game, poisonedBlade);
		var minion = CreateMinion(game, game.Player1, atk: 2, health: 2, cardId: "TEST_EQUIP_MINION");

		game.PlayCard(minion);

		Assert.NotSame(poisonedBlade, game.Player1.Hero!.EquippedWeapon);
		Assert.Equal("CS2_082", game.Player1.Hero.EquippedWeapon!.CardId);
	}

	[Fact]
	public void AT_041_ReducesCostBySummonedBeastsThisGame()
	{
		var game = CreateTestGame();
		game.Setup();

		var beast1 = CreateMinion(game, game.Player1, "TEST_BEAST_1", new[] { global::HearthstoneServer.Race.Beast }, cost: 1);
		var beast2 = CreateMinion(game, game.Player1, "TEST_BEAST_2", new[] { global::HearthstoneServer.Race.Beast }, cost: 1);
		var mech = CreateMinion(game, game.Player1, "TEST_MECH", new[] { global::HearthstoneServer.Race.Mechanical }, cost: 1);
		beast1.SetZone(Zone.Play, game.Player1);
		beast2.SetZone(Zone.Play, game.Player1);
		mech.SetZone(Zone.Play, game.Player1);

		var knight = CreateMinion(game, game.Player1, "AT_041", null, atk: 6, health: 6, cost: 7);

		Assert.Equal(2, game.Player1.SummonedMinionsThisGame.Count(m => m.CardData?.Races.Contains(global::HearthstoneServer.Race.Beast) == true));
		Assert.Equal(5, knight.Cost);
	}

	[Fact]
	public void AT_051_DealsSameRandomAmountToAllMinions()
	{
		var game = CreateTestGame();
		game.Setup();
		game.BeginTurn(game.Player1);
		CardScriptRegistry.Instance.Initialize();

		var friendly = CreateMinion(game, game.Player1, "TEST_AT051_FRIENDLY", null, health: 6);
		var enemy = CreateMinion(game, game.Player2, "TEST_AT051_ENEMY", null, health: 6);
		friendly.SetZone(Zone.Play, game.Player1);
		enemy.SetZone(Zone.Play, game.Player2);

		var spell = CreateSpell(game, game.Player1, "AT_051");
		game.PlayCard(spell);

		int friendlyDamage = 6 - friendly.Health;
		int enemyDamage = 6 - enemy.Health;
		Assert.Equal(friendlyDamage, enemyDamage);
		Assert.InRange(friendlyDamage, 4, 5);
	}

	[Fact]
	public void AT_132_RogueHeroPower_EquipsTwoTwoWeapon_WhenNoPoisonedBlade()
	{
		var game = CreateTestGame();
		game.Setup();
		game.BeginTurn(game.Player1);
		CardScriptRegistry.Instance.Initialize();

		CreateHeroPower(game, game.Player1, "AT_132_ROGUE_H1");
		game.Player1.MaxMana = 10;
		game.CurrentPlayer = game.Player1;

		game.UseHeroPower(game.Player1);

		Assert.NotNull(game.Player1.Hero!.EquippedWeapon);
		Assert.Equal("AT_132_ROGUEt_H1", game.Player1.Hero.EquippedWeapon!.CardId);
		Assert.Equal(2, game.Player1.Hero.EquippedWeapon.Atk);
	}

	[Fact]
	public void AT_132_RogueHeroPower_IsInterceptedByPoisonedBlade_WhenEquipped()
	{
		var game = CreateTestGame();
		game.Setup();
		game.BeginTurn(game.Player1);
		CardScriptRegistry.Instance.Initialize();

		var poisonedBlade = CreateWeapon(game, game.Player1, "AT_034");
		poisonedBlade.Equip();
		ActivateScript(game, poisonedBlade);
		CreateHeroPower(game, game.Player1, "AT_132_ROGUE_H1");
		game.Player1.MaxMana = 10;
		game.CurrentPlayer = game.Player1;

		game.UseHeroPower(game.Player1);

		Assert.Same(poisonedBlade, game.Player1.Hero!.EquippedWeapon);
		Assert.Equal(2, poisonedBlade.Atk);
	}

	[Fact]
	public void AT_132_ShamanHeroPower_PresentsFiveTotemChoices()
	{
		var game = CreateTestGame();
		game.Setup();
		game.BeginTurn(game.Player1);
		CardScriptRegistry.Instance.Initialize();

		CreateHeroPower(game, game.Player1, "CS2_049_H1_AT_132");
		game.Player1.MaxMana = 10;
		game.CurrentPlayer = game.Player1;

		game.UseHeroPower(game.Player1);

		var choice = Assert.IsAssignableFrom<Choice>(game.Player1.Choice);
		var ids = choice.Cards.OfType<BuffableEntity>().Select(card => card.CardId).OrderBy(id => id).ToArray();

		Assert.Equal(5, ids.Length);
		Assert.Equal(new[] { "AT_132_SHAMANa", "AT_132_SHAMANb", "AT_132_SHAMANc", "AT_132_SHAMANd", "AT_132_SHAMANe" }, ids);
	}

	[Fact]
	public void AT_132_ShamanHeroPower_SummonsChosenTotem()
	{
		var game = CreateTestGame();
		game.Setup();
		game.BeginTurn(game.Player1);
		CardScriptRegistry.Instance.Initialize();

		CreateHeroPower(game, game.Player1, "CS2_049_H1_AT_132");
		game.Player1.MaxMana = 10;
		game.CurrentPlayer = game.Player1;

		game.UseHeroPower(game.Player1);

		var choice = Assert.IsAssignableFrom<Choice>(game.Player1.Choice);
		var selected = Assert.Single(choice.Cards.OfType<BuffableEntity>(), card => card.CardId == "AT_132_SHAMANd");
		game.Player1.Choose(selected);

		Assert.Null(game.Player1.Choice);
		Assert.Contains(game.Player1.Field, entity => (entity as BuffableEntity)?.CardId == "AT_132_SHAMANd");
	}

	[Fact]
	public void UpgradeStartingHeroPower_UsesCurrentHeroPowerTag_ForDemonHunter()
	{
		var game = CreateTestGame();
		game.Setup();

		CreateHeroPower(game, game.Player1, "HERO_10bp", upgradedHeroPowerDbfId: 60483);

		new UpgradeStartingHeroPower(game.Player1).Do(game, game.Player1);

		Assert.Equal("HERO_10bp2", game.Player1.Hero!.Power!.CardId);
	}

	[Fact]
	public void UpgradeStartingHeroPower_UsesCurrentHeroPowerTag_ForDeathKnight()
	{
		var game = CreateTestGame();
		game.Setup();

		CreateHeroPower(game, game.Player1, "HERO_11bp", upgradedHeroPowerDbfId: 79943);

		new UpgradeStartingHeroPower(game.Player1).Do(game, game.Player1);

		Assert.Equal("HERO_11bp2", game.Player1.Hero!.Power!.CardId);
	}

	[Fact]
	public void HERO_10bp2_GivesTwoAttackThisTurn()
	{
		var game = CreateTestGame();
		game.Setup();
		game.BeginTurn(game.Player1);
		CardScriptRegistry.Instance.Initialize();

		CreateHeroPower(game, game.Player1, "HERO_10bp2");
		game.Player1.MaxMana = 10;
		game.CurrentPlayer = game.Player1;

		game.UseHeroPower(game.Player1);

		Assert.Equal(2, game.Player1.Hero!.Atk);
	}

	[Fact]
	public void HERO_11bp2_SummonsTwoOneGhoul()
	{
		var game = CreateTestGame();
		game.Setup();
		game.BeginTurn(game.Player1);
		CardScriptRegistry.Instance.Initialize();

		CreateHeroPower(game, game.Player1, "HERO_11bp2");
		game.Player1.MaxMana = 10;
		game.CurrentPlayer = game.Player1;

		game.UseHeroPower(game.Player1);

		var ghoul = Assert.Single(game.Player1.Field.OfType<Minion>());
		Assert.Equal("HERO_11bp2t", ghoul.CardId);
		Assert.Equal(2, ghoul.Atk);
		Assert.Equal(1, ghoul.Health);
	}

	[Fact]
	public void AT_015_CopiesEnemyMinionToHandAndSetsCostToOne()
	{
		var game = CreateTestGame();
		game.Setup();
		game.BeginTurn(game.Player1);
		CardScriptRegistry.Instance.Initialize();

		var target = CreateMinion(game, game.Player2, "TEST_COPY_TARGET", null, atk: 4, health: 4, cost: 4);
		target.SetZone(Zone.Play, game.Player2);

		var spell = CreateSpell(game, game.Player1, "AT_015");
		game.PlayCard(spell, target);

		var copied = Assert.Single(game.Player1.Hand.OfType<BuffableEntity>(), card => card.CardId == "TEST_COPY_TARGET");
		Assert.Equal(1, copied.Cost);
	}

	[Fact]
	public void AT_072_SummonsDrawnMinionsAndKeepsSpellInHand()
	{
		var game = CreateTestGame();
		game.Setup();
		game.BeginTurn(game.Player1);
		CardScriptRegistry.Instance.Initialize();

		game.Player1.Deck.Clear();
		game.Player1.CreateCard(new CardData { CardId = "TEST_DRAW_MINION_A", CardType = CardType.Minion, Attack = 2, Health = 2, Cost = 2 }, Zone.Deck);
		game.Player1.CreateCard(new CardData { CardId = "TEST_DRAW_SPELL", CardType = CardType.Spell, Cost = 3 }, Zone.Deck);
		game.Player1.CreateCard(new CardData { CardId = "TEST_DRAW_MINION_B", CardType = CardType.Minion, Attack = 3, Health = 3, Cost = 3 }, Zone.Deck);

		var varian = CreateMinion(game, game.Player1, "AT_072", null, atk: 7, health: 7, cost: 1);
		game.PlayCard(varian);

		Assert.Contains(game.Player1.Field, entity => (entity as BuffableEntity)?.CardId == "TEST_DRAW_MINION_A");
		Assert.Contains(game.Player1.Field, entity => (entity as BuffableEntity)?.CardId == "TEST_DRAW_MINION_B");
		Assert.Contains(game.Player1.Hand, entity => (entity as BuffableEntity)?.CardId == "TEST_DRAW_SPELL");
		Assert.DoesNotContain(game.Player1.Hand, entity => (entity as BuffableEntity)?.CardId == "TEST_DRAW_MINION_A");
		Assert.DoesNotContain(game.Player1.Hand, entity => (entity as BuffableEntity)?.CardId == "TEST_DRAW_MINION_B");
	}

	[Fact]
	public void AT_124_RedirectsHeroDamageToSelf()
	{
		var game = CreateTestGame();
		game.Setup();
		game.BeginTurn(game.Player1);
		CardScriptRegistry.Instance.Initialize();

		var bolf = CreateMinion(game, game.Player1, "AT_124", null, atk: 3, health: 7, cost: 6);
		bolf.SetZone(Zone.Play, game.Player1);
		ActivateScript(game, bolf);
		var attacker = CreateMinion(game, game.Player2, "TEST_ATTACKER", null, atk: 4, health: 4, cost: 4);
		attacker.SetZone(Zone.Play, game.Player2);

		new DealDamage(game.Player1.Hero!, 3).Trigger(attacker);

		Assert.Equal(30, game.Player1.Hero!.Health);
		Assert.Equal(4, bolf.Health);
	}

	[Fact]
	public void AT_002_SummonsRandomMinionWithSameCostAsDead()
	{
		var game = CreateTestGame();
		game.Setup();
		CardScriptRegistry.Instance.Initialize();

		game.Player1.CreateCard(new CardData
		{
			CardId = "AT_002",
			CardType = CardType.Spell,
			TagsInt = new Dictionary<GameTag, int> { [GameTag.Secret] = 1 },
		}, Zone.Secret);

		var dead = new Minion
		{
			Controller = game.Player1,
			CardData = new CardData { CardId = "TEST_DEAD_MINION", CardType = CardType.Minion, Attack = 4, Health = 4, Cost = 4 },
			Atk = 4,
			MaxHealth = 4,
		};
		dead.SetIntProperty("cost", 4);
		game.RegisterEntity(dead);
		dead.SetZone(Zone.Graveyard, game.Player1);

		game.EventBus.Publish(new DeathEvent(dead, dead), MiscPhase.After);

		Assert.Contains(game.Player1.Field, entity => (entity as BuffableEntity)?.CardId == "TEST_AT002_MATCH");
		Assert.DoesNotContain(game.Player1.Field, entity => (entity as BuffableEntity)?.CardId == "TEST_AT002_OTHER");
	}
}
