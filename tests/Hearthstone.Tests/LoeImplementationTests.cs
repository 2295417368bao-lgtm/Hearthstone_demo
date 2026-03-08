using HearthstoneServer;
using HearthstoneServer.Cards;
using HearthstoneServer.Dsl;
using HearthstoneServer.Entities;
using HearthstoneServer.Events;
using HearthstoneServer.LoadCardIntoDbf;
using HearthstoneServer.Targeting;
using static HearthstoneServer.Dsl.BusEvent;
using static HearthstoneServer.Dsl.EventFilters;

namespace Hearthstone.Tests;

public class LoeImplementationTests
{
	[Fact]
	public void PlayerSpellPower_IncludesPlayerTag()
	{
		var game = CreateGame(new Dictionary<string, Card>());
		var player = game.Player1;

		player.SetIntProperty("spellpower", 2);

		var minion = new Minion { Controller = player, SpellPower = 1, MaxHealth = 1, Atk = 1 };
		game.RegisterEntity(minion);
		minion.SetZone(Zone.Play);

		Assert.Equal(3, player.SpellPower);
		Assert.Equal(7, player.GetSpellDamage(4));
	}

	[Fact]
	public void ModifyPreDamage_CanMultiplyAndClamp()
	{
		var game = CreateGame(new Dictionary<string, Card>());
		var source = new Minion { Controller = game.Player1, MaxHealth = 1, Atk = 1 };
		game.RegisterEntity(source);

		var preDamage = new PreDamageEvent(source, game.Player1.Hero!, 3);
		source.ActivePreDamageEvent = preDamage;
		new ModifyPreDamage(multiply: 2).Trigger(source);
		Assert.Equal(6, preDamage.Amount);

		new ModifyPreDamage(clampMax: 1).Trigger(source);
		Assert.Equal(1, preDamage.Amount);
	}

	[Fact]
	public void ReplaceAllWithLegendaries_ReplacesHandAndDeck()
	{
		var cardDb = new Dictionary<string, Card>
		{
			["HAND_A"] = MakeCard("HAND_A", CardType.Minion, cost: 1),
			["DECK_A"] = MakeCard("DECK_A", CardType.Spell, cost: 2),
			["LEG_1"] = MakeCard("LEG_1", CardType.Minion, cost: 8, rarity: Rarity.Legendary),
			["LEG_2"] = MakeCard("LEG_2", CardType.Minion, cost: 9, rarity: Rarity.Legendary),
		};

		var game = CreateGame(cardDb);
		var player = game.Player1;
		player.CreateCard("HAND_A", Zone.Hand);
		player.CreateCard("HAND_A", Zone.Hand);
		player.CreateCard("DECK_A", Zone.Deck);
		player.CreateCard("DECK_A", Zone.Deck);

		var source = new Minion { Controller = player, MaxHealth = 1, Atk = 1 };
		game.RegisterEntity(source);

		new ReplaceAllWithLegendaries(player).Trigger(source);

		Assert.Equal(2, player.Hand.Count);
		Assert.Equal(2, player.Deck.Count);
		Assert.All(player.Hand.Concat(player.Deck).Cast<BuffableEntity>(),
			card => Assert.Contains(card.CardId, new[] { "LEG_1", "LEG_2" }));
	}

	[Fact]
	public void LOE_076_OffersThreeDistinctHeroPowersAndReplacesCurrentPower()
	{
		CardScriptRegistry.Instance.Initialize();

		var cardDb = CardScriptUtils.BASIC_HERO_POWERS.ToDictionary(
			id => id,
			id => MakeCard(id, CardType.HeroPower, cost: 2));
		cardDb["LOE_076"] = MakeCard("LOE_076", CardType.Minion, cost: 1);

		var game = CreateGame(cardDb);
		AssignHeroPower(game.Player1, "HERO_01bp");
		AssignHeroPower(game.Player2, "HERO_02bp");

		var finley = game.Player1.CreateCard("LOE_076", Zone.Play) as Minion;
		Assert.NotNull(finley);

		var script = CardScriptRegistry.Instance.GetScript("LOE_076");
		Assert.NotNull(script);

		game.TriggerBlock(finley!, script!.Battlecry.ToList());

		Assert.NotNull(game.Player1.Choice);
		Assert.Equal(3, game.Player1.Choice!.Cards.Count);

		var choices = game.Player1.Choice.Cards.Cast<BuffableEntity>().ToList();
		Assert.Equal(3, choices.Select(card => card.CardId).Distinct().Count());
		Assert.DoesNotContain(choices, card => card.CardId == "HERO_01bp");

		var chosen = choices[0];
		game.Player1.Choice.Choose(chosen);

		Assert.Null(game.Player1.Choice);
		Assert.Equal(chosen.CardId, game.Player1.Hero!.Power!.CardId);
	}

	[Fact]
	public void LOE_092_OnlyDiscoversArtifactsFromFixedPool()
	{
		CardScriptRegistry.Instance.Initialize();

		var cardDb = new Dictionary<string, Card>
		{
			["LOE_092"] = MakeCard("LOE_092", CardType.Minion, cost: 9),
			["LOEA16_3"] = MakeCard("LOEA16_3", CardType.Spell, cost: 10),
			["LOEA16_4"] = MakeCard("LOEA16_4", CardType.Spell, cost: 10),
			["LOEA16_5"] = MakeCard("LOEA16_5", CardType.Spell, cost: 10),
		};

		var game = CreateGame(cardDb);
		var rafaam = game.Player1.CreateCard("LOE_092", Zone.Play) as Minion;
		Assert.NotNull(rafaam);

		var script = CardScriptRegistry.Instance.GetScript("LOE_092");
		Assert.NotNull(script);

		game.TriggerBlock(rafaam!, script!.Battlecry.ToList());

		Assert.NotNull(game.Player1.Choice);
		var choices = game.Player1.Choice!.Cards.Cast<BuffableEntity>().ToList();
		Assert.Equal(3, choices.Count);
		Assert.All(choices, card => Assert.Contains(card.CardId, new[] { "LOEA16_3", "LOEA16_4", "LOEA16_5" }));
	}

	[Fact]
	public void Condition_WhenEvaluatorCanReadEventTargetAndCleanupAfterEvaluation()
	{
		CardScriptRegistry.Instance.Initialize();
		RegisterTestSpellCards();

		var builder = new CardBuilder(CardScriptRegistry.Instance);
		builder.Card("TEST_CONDITION_LISTENER",
			on: When<SpellCastEvent>(OWN, AFTER,
				new Enchant(S.SELF, "TEST_COPY_BUFF"),
				when: Condition<SpellCastEvent>(new Find(S.EVT_TARGET))));

		var cardDb = new Dictionary<string, Card>
		{
			["TEST_CONDITION_LISTENER"] = MakeCard("TEST_CONDITION_LISTENER", CardType.Minion, cost: 1, atk: 1, health: 1),
			["TEST_ALLY"] = MakeCard("TEST_ALLY", CardType.Minion, cost: 1, atk: 1, health: 1),
			["TEST_EFFECT_SPELL"] = MakeCard("TEST_EFFECT_SPELL", CardType.Spell, cost: 1),
			["TEST_EFFECT_COPY"] = MakeCard("TEST_EFFECT_COPY", CardType.Spell, cost: 1),
		};

		var game = CreateGame(cardDb);
		var listener = game.Player1.CreateCard("TEST_CONDITION_LISTENER", Zone.Play) as Minion;
		var ally = game.Player1.CreateCard("TEST_ALLY", Zone.Play) as Minion;
		var spell = game.Player1.CreateCard("TEST_EFFECT_SPELL", Zone.SetAside) as Spell;
		var untargeted = game.Player1.CreateCard("TEST_EFFECT_COPY", Zone.SetAside) as Spell;

		Assert.NotNull(listener);
		Assert.NotNull(ally);
		Assert.NotNull(spell);
		Assert.NotNull(untargeted);

		game.Player1.MaxMana = 10;
		game.Sequences.PlaySpell(spell!, ally);

		Assert.Equal(2, listener!.Atk);
		Assert.Null(listener.EventSource);
		Assert.Null(listener.EventTarget);

		game.EventBus.Publish(new SpellCastEvent(untargeted!, untargeted!, null), SpellPhase.AfterSpell);

		Assert.Equal(2, listener.Atk);
		Assert.Null(listener.EventSource);
		Assert.Null(listener.EventTarget);
	}

	[Fact]
	public void SpellTargets_SelectorBridgeMatchesFriendlyOtherMinionsOnly()
	{
		CardScriptRegistry.Instance.Initialize();
		RegisterTestSpellCards();

		var builder = new CardBuilder(CardScriptRegistry.Instance);
		builder.Card("TEST_SELECTOR_LISTENER",
			on: When<SpellCastEvent>(OWN, AFTER,
				new Enchant(S.SELF, "TEST_COPY_BUFF"),
				when: SpellTargets(S.FRIENDLY_MINIONS - S.SELF)));

		var cardDb = new Dictionary<string, Card>
		{
			["TEST_SELECTOR_LISTENER"] = MakeCard("TEST_SELECTOR_LISTENER", CardType.Minion, cost: 1, atk: 1, health: 1),
			["TEST_ALLY"] = MakeCard("TEST_ALLY", CardType.Minion, cost: 1, atk: 1, health: 1),
			["TEST_EFFECT_SPELL"] = MakeCard("TEST_EFFECT_SPELL", CardType.Spell, cost: 1),
		};

		var game = CreateGame(cardDb);
		var listener = game.Player1.CreateCard("TEST_SELECTOR_LISTENER", Zone.Play) as Minion;
		var ally = game.Player1.CreateCard("TEST_ALLY", Zone.Play) as Minion;
		var spell = game.Player1.CreateCard("TEST_EFFECT_SPELL", Zone.SetAside) as Spell;
		var secondSpell = game.Player1.CreateCard("TEST_EFFECT_SPELL", Zone.SetAside) as Spell;

		Assert.NotNull(listener);
		Assert.NotNull(ally);
		Assert.NotNull(spell);
		Assert.NotNull(secondSpell);

		game.Player1.MaxMana = 10;
		game.Sequences.PlaySpell(spell!, ally);
		game.Sequences.PlaySpell(secondSpell!, listener);

		Assert.Equal(3, listener!.Atk);
		Assert.Equal(2, ally!.Atk);
	}

	[Fact]
	public void EventTargetIn_GenericBridgeWorksForDamageEvents()
	{
		CardScriptRegistry.Instance.Initialize();

		var builder = new CardBuilder(CardScriptRegistry.Instance);
		builder.Buff("TEST_DAMAGE_BUFF", atk: 1);
		builder.Card("TEST_DAMAGE_LISTENER",
			on: When<DamageEvent>(OWN, ON,
				new Enchant(S.SELF, "TEST_DAMAGE_BUFF"),
				when: EventTargetIn<DamageEvent>(S.FRIENDLY_MINIONS - S.SELF)));

		var cardDb = new Dictionary<string, Card>
		{
			["TEST_DAMAGE_LISTENER"] = MakeCard("TEST_DAMAGE_LISTENER", CardType.Minion, cost: 1, atk: 1, health: 1),
			["TEST_ALLY"] = MakeCard("TEST_ALLY", CardType.Minion, cost: 1, atk: 1, health: 2),
			["TEST_SOURCE"] = MakeCard("TEST_SOURCE", CardType.Minion, cost: 1, atk: 1, health: 1),
		};

		var game = CreateGame(cardDb);
		var listener = game.Player1.CreateCard("TEST_DAMAGE_LISTENER", Zone.Play) as Minion;
		var ally = game.Player1.CreateCard("TEST_ALLY", Zone.Play) as Minion;
		var source = game.Player1.CreateCard("TEST_SOURCE", Zone.Play) as Minion;

		Assert.NotNull(listener);
		Assert.NotNull(ally);
		Assert.NotNull(source);

		game.EventBus.Publish(new DamageEvent(source!, ally!, 1), MiscPhase.On);
		game.EventBus.Publish(new DamageEvent(source!, game.Player1.Hero!, 1), MiscPhase.On);

		Assert.Equal(2, listener!.Atk);
	}

	[Fact]
	public void EventSourceIn_AndSelectorCountAtLeastCanReadEventSource()
	{
		CardScriptRegistry.Instance.Initialize();

		var builder = new CardBuilder(CardScriptRegistry.Instance);
		builder.Buff("TEST_SOURCE_BUFF", atk: 1);
		builder.Card("TEST_SOURCE_FILTER_LISTENER",
			on: When<MinionPlayedEvent>(ENEMY, AFTER,
				new Enchant(S.SELF, "TEST_SOURCE_BUFF"),
				when: EventSourceIn<MinionPlayedEvent>(S.CardId("TEST_ALLOWED_SOURCE"))));
		builder.Card("TEST_CONDITION_SOURCE_LISTENER",
			on: When<MinionPlayedEvent>(ENEMY, AFTER,
				new Destroy(S.EVT_SOURCE),
				when: Condition<MinionPlayedEvent>(
					SelectorCountAtLeast(S.ENEMY_MINIONS - S.EVT_SOURCE, 3))));

		var cardDb = new Dictionary<string, Card>
		{
			["TEST_SOURCE_FILTER_LISTENER"] = MakeCard("TEST_SOURCE_FILTER_LISTENER", CardType.Minion, cost: 1, atk: 1, health: 1),
			["TEST_CONDITION_SOURCE_LISTENER"] = MakeCard("TEST_CONDITION_SOURCE_LISTENER", CardType.Minion, cost: 1, atk: 1, health: 1),
			["TEST_ALLOWED_SOURCE"] = MakeCard("TEST_ALLOWED_SOURCE", CardType.Minion, cost: 1, atk: 1, health: 1),
			["TEST_OTHER_SOURCE"] = MakeCard("TEST_OTHER_SOURCE", CardType.Minion, cost: 1, atk: 1, health: 1),
			["TEST_BOARD_MINION_A"] = MakeCard("TEST_BOARD_MINION_A", CardType.Minion, cost: 1, atk: 1, health: 1),
			["TEST_BOARD_MINION_B"] = MakeCard("TEST_BOARD_MINION_B", CardType.Minion, cost: 1, atk: 1, health: 1),
			["TEST_BOARD_MINION_C"] = MakeCard("TEST_BOARD_MINION_C", CardType.Minion, cost: 1, atk: 1, health: 1),
		};

		var game = CreateGame(cardDb);
		var filterListener = game.Player1.CreateCard("TEST_SOURCE_FILTER_LISTENER", Zone.Play) as Minion;
		var conditionListener = game.Player1.CreateCard("TEST_CONDITION_SOURCE_LISTENER", Zone.Play) as Minion;

		Assert.NotNull(filterListener);
		Assert.NotNull(conditionListener);

		var allowedSource = game.Player2.CreateCard("TEST_ALLOWED_SOURCE", Zone.Play) as Minion;
		var otherSource = game.Player2.CreateCard("TEST_OTHER_SOURCE", Zone.Play) as Minion;
		game.Player2.CreateCard("TEST_BOARD_MINION_A", Zone.Play);
		game.Player2.CreateCard("TEST_BOARD_MINION_B", Zone.Play);
		game.Player2.CreateCard("TEST_BOARD_MINION_C", Zone.Play);

		Assert.NotNull(allowedSource);
		Assert.NotNull(otherSource);

		game.EventBus.Publish(new MinionPlayedEvent(allowedSource!, allowedSource!, null), MinionPhase.AfterPlay);
		Assert.Equal(2, filterListener!.Atk);

		game.EventBus.Publish(new MinionPlayedEvent(otherSource!, otherSource!, null), MinionPhase.AfterPlay);
		Assert.Equal(2, filterListener.Atk);
		Assert.True(allowedSource!.Dead || allowedSource.Zone != Zone.Play);
		Assert.True(otherSource!.Dead || otherSource.Zone != Zone.Play);
	}

	[Fact]
	public void AfterAttackEvent_BridgesCanComposeWithAnd()
	{
		CardScriptRegistry.Instance.Initialize();

		var builder = new CardBuilder(CardScriptRegistry.Instance);
		builder.Buff("TEST_ATTACK_BRIDGE_BUFF", atk: 1);
		builder.Card("TEST_ATTACK_BRIDGE_LISTENER",
			on: When<AfterAttackEvent>(OWN, AFTER,
				new Enchant(S.SELF, "TEST_ATTACK_BRIDGE_BUFF"),
				when: And(
					EventSourceIn<AfterAttackEvent>(S.SELF),
					EventTargetIn<AfterAttackEvent>(S.ALL_HEROES))));

		var cardDb = new Dictionary<string, Card>
		{
			["TEST_ATTACK_BRIDGE_LISTENER"] = MakeCard("TEST_ATTACK_BRIDGE_LISTENER", CardType.Minion, cost: 1, atk: 1, health: 2),
			["TEST_ENEMY_MINION"] = MakeCard("TEST_ENEMY_MINION", CardType.Minion, cost: 1, atk: 1, health: 2),
		};

		var game = CreateGame(cardDb);
		var listener = game.Player1.CreateCard("TEST_ATTACK_BRIDGE_LISTENER", Zone.Play) as Minion;
		var enemyMinion = game.Player2.CreateCard("TEST_ENEMY_MINION", Zone.Play) as Minion;

		Assert.NotNull(listener);
		Assert.NotNull(enemyMinion);

		game.EventBus.Publish(new AfterAttackEvent(listener!, listener!, game.Player2.Hero!), CombatPhase.Combat);
		game.EventBus.Publish(new AfterAttackEvent(listener!, listener!, enemyMinion!), CombatPhase.Combat);

		Assert.Equal(2, listener.Atk);
	}

	[Fact]
	public void PlayedType_MatchesWeaponCardsOnly()
	{
		CardScriptRegistry.Instance.Initialize();

		var builder = new CardBuilder(CardScriptRegistry.Instance);
		builder.Buff("TEST_PLAYED_TYPE_BUFF", atk: 1);
		builder.Card("TEST_PLAYED_TYPE_LISTENER",
			on: When<CardPlayedEvent>(OWN, AFTER,
				new Enchant(S.SELF, "TEST_PLAYED_TYPE_BUFF"),
				when: PlayedType(CardType.Weapon)));

		var cardDb = new Dictionary<string, Card>
		{
			["TEST_PLAYED_TYPE_LISTENER"] = MakeCard("TEST_PLAYED_TYPE_LISTENER", CardType.Minion, cost: 1, atk: 1, health: 2),
			["TEST_WEAPON"] = MakeCard("TEST_WEAPON", CardType.Weapon, cost: 1),
			["TEST_SPELL"] = MakeCard("TEST_SPELL", CardType.Spell, cost: 1),
		};

		var game = CreateGame(cardDb);
		var listener = game.Player1.CreateCard("TEST_PLAYED_TYPE_LISTENER", Zone.Play) as Minion;
		var weapon = game.Player1.CreateCard("TEST_WEAPON", Zone.SetAside) as BuffableEntity;
		var spell = game.Player1.CreateCard("TEST_SPELL", Zone.SetAside) as BuffableEntity;

		Assert.NotNull(listener);
		Assert.NotNull(weapon);
		Assert.NotNull(spell);

		game.EventBus.Publish(new CardPlayedEvent(weapon!, weapon!, null), SpellPhase.AfterSpell);
		game.EventBus.Publish(new CardPlayedEvent(spell!, spell!, null), SpellPhase.AfterSpell);

		Assert.Equal(2, listener!.Atk);
	}

	[Fact]
	public void Condition_WhenEvaluatorCanReadEventCardAndCleanupAfterEvaluation()
	{
		CardScriptRegistry.Instance.Initialize();

		var builder = new CardBuilder(CardScriptRegistry.Instance);
		builder.Buff("TEST_EVENT_CARD_BUFF", atk: 1);
		builder.Card("TEST_EVENT_CARD_LISTENER",
			on: When<CardPlayedEvent>(OWN, AFTER,
				new Enchant(S.SELF, "TEST_EVENT_CARD_BUFF"),
				when: Condition<CardPlayedEvent>(new Find(S.EVT_CARD + S.SPELL))));

		var cardDb = new Dictionary<string, Card>
		{
			["TEST_EVENT_CARD_LISTENER"] = MakeCard("TEST_EVENT_CARD_LISTENER", CardType.Minion, cost: 1, atk: 1, health: 2),
			["TEST_EVENT_CARD_SPELL"] = MakeCard("TEST_EVENT_CARD_SPELL", CardType.Spell, cost: 1),
			["TEST_EVENT_CARD_MINION"] = MakeCard("TEST_EVENT_CARD_MINION", CardType.Minion, cost: 1, atk: 1, health: 1),
		};

		var game = CreateGame(cardDb);
		var listener = game.Player1.CreateCard("TEST_EVENT_CARD_LISTENER", Zone.Play) as Minion;
		var spell = game.Player1.CreateCard("TEST_EVENT_CARD_SPELL", Zone.SetAside) as BuffableEntity;
		var minion = game.Player1.CreateCard("TEST_EVENT_CARD_MINION", Zone.SetAside) as BuffableEntity;

		Assert.NotNull(listener);
		Assert.NotNull(spell);
		Assert.NotNull(minion);

		game.EventBus.Publish(new CardPlayedEvent(spell!, spell!, null), SpellPhase.AfterSpell);
		Assert.Equal(2, listener!.Atk);
		Assert.Null(listener.EventCard);

		game.EventBus.Publish(new CardPlayedEvent(minion!, minion!, null), SpellPhase.AfterSpell);
		Assert.Equal(2, listener.Atk);
		Assert.Null(listener.EventCard);
	}

	[Fact]
	public void EventCardIn_MatchesRaceAndOpponentCardOwnership()
	{
		CardScriptRegistry.Instance.Initialize();

		var builder = new CardBuilder(CardScriptRegistry.Instance);
		builder.Buff("TEST_EVENT_CARD_RACE_BUFF", atk: 1);
		builder.Buff("TEST_EVENT_CARD_OWNER_BUFF", health: 1);
		builder.Card("TEST_EVENT_CARD_RACE_LISTENER",
			on: When<CardPlayedEvent>(ANY, AFTER,
				new Enchant(S.SELF, "TEST_EVENT_CARD_RACE_BUFF"),
				when: EventCardIn<CardPlayedEvent>(S.Race(Race.Dragon))));
		builder.Card("TEST_EVENT_CARD_OWNER_LISTENER",
			on: When<CardPlayedEvent>(ANY, AFTER,
				new Enchant(S.SELF, "TEST_EVENT_CARD_OWNER_BUFF"),
				when: EventCardIn<CardPlayedEvent>(S.ENEMY)));

		var cardDb = new Dictionary<string, Card>
		{
			["TEST_EVENT_CARD_RACE_LISTENER"] = MakeCard("TEST_EVENT_CARD_RACE_LISTENER", CardType.Minion, cost: 1, atk: 1, health: 2),
			["TEST_EVENT_CARD_OWNER_LISTENER"] = MakeCard("TEST_EVENT_CARD_OWNER_LISTENER", CardType.Minion, cost: 1, atk: 1, health: 2),
			["TEST_DRAGON_CARD"] = MakeCard("TEST_DRAGON_CARD", CardType.Minion, cost: 1, atk: 1, health: 1, races: new[] { Race.Dragon }),
			["TEST_BEAST_CARD"] = MakeCard("TEST_BEAST_CARD", CardType.Minion, cost: 1, atk: 1, health: 1, races: new[] { Race.Beast }),
		};

		var game = CreateGame(cardDb);
		var raceListener = game.Player1.CreateCard("TEST_EVENT_CARD_RACE_LISTENER", Zone.Play) as Minion;
		var ownerListener = game.Player1.CreateCard("TEST_EVENT_CARD_OWNER_LISTENER", Zone.Play) as Minion;
		var friendlyDragon = game.Player1.CreateCard("TEST_DRAGON_CARD", Zone.SetAside) as BuffableEntity;
		var enemyBeast = game.Player2.CreateCard("TEST_BEAST_CARD", Zone.SetAside) as BuffableEntity;

		Assert.NotNull(raceListener);
		Assert.NotNull(ownerListener);
		Assert.NotNull(friendlyDragon);
		Assert.NotNull(enemyBeast);

		game.EventBus.Publish(new CardPlayedEvent(friendlyDragon!, friendlyDragon!, null), SpellPhase.AfterSpell);
		Assert.Equal(2, raceListener!.Atk);
		Assert.Equal(2, ownerListener!.Health);

		game.EventBus.Publish(new CardPlayedEvent(enemyBeast!, enemyBeast!, null), SpellPhase.AfterSpell);
		Assert.Equal(2, raceListener.Atk);
		Assert.Equal(3, ownerListener.Health);
	}

	[Fact]
	public void EventHasTarget_OnlyMatchesTargetedSpellCastEvents()
	{
		CardScriptRegistry.Instance.Initialize();
		RegisterTestSpellCards();

		var builder = new CardBuilder(CardScriptRegistry.Instance);
		builder.Buff("TEST_EVENT_HAS_TARGET_BUFF", atk: 1);
		builder.Card("TEST_EVENT_HAS_TARGET_LISTENER",
			on: When<SpellCastEvent>(OWN, AFTER,
				new Enchant(S.SELF, "TEST_EVENT_HAS_TARGET_BUFF"),
				when: EventHasTarget<SpellCastEvent>()));

		var cardDb = new Dictionary<string, Card>
		{
			["TEST_EVENT_HAS_TARGET_LISTENER"] = MakeCard("TEST_EVENT_HAS_TARGET_LISTENER", CardType.Minion, cost: 1, atk: 1, health: 2),
			["TEST_ALLY"] = MakeCard("TEST_ALLY", CardType.Minion, cost: 1, atk: 1, health: 1),
			["TEST_EFFECT_SPELL"] = MakeCard("TEST_EFFECT_SPELL", CardType.Spell, cost: 1),
			["TEST_EFFECT_COPY"] = MakeCard("TEST_EFFECT_COPY", CardType.Spell, cost: 1),
		};

		var game = CreateGame(cardDb);
		var listener = game.Player1.CreateCard("TEST_EVENT_HAS_TARGET_LISTENER", Zone.Play) as Minion;
		var ally = game.Player1.CreateCard("TEST_ALLY", Zone.Play) as Minion;
		var targeted = game.Player1.CreateCard("TEST_EFFECT_SPELL", Zone.SetAside) as Spell;
		var untargeted = game.Player1.CreateCard("TEST_EFFECT_COPY", Zone.SetAside) as Spell;

		Assert.NotNull(listener);
		Assert.NotNull(ally);
		Assert.NotNull(targeted);
		Assert.NotNull(untargeted);

		game.EventBus.Publish(new SpellCastEvent(targeted!, targeted!, ally), SpellPhase.AfterSpell);
		Assert.Equal(2, listener!.Atk);

		game.EventBus.Publish(new SpellCastEvent(untargeted!, untargeted!, null), SpellPhase.AfterSpell);
		Assert.Equal(2, listener.Atk);
	}

	[Fact]
	public void EventTargetIn_WeaponDurabilityLossEvent_UsesDefenderAsTarget()
	{
		CardScriptRegistry.Instance.Initialize();

		var builder = new CardBuilder(CardScriptRegistry.Instance);
		builder.Buff("TEST_WEAPON_DURABILITY_BRIDGE_BUFF", atk: 1);
		builder.Card("TEST_WEAPON_DURABILITY_BRIDGE_LISTENER",
			on: When<WeaponDurabilityLossEvent>(OWN, BEFORE,
				new Enchant(S.SELF, "TEST_WEAPON_DURABILITY_BRIDGE_BUFF"),
				when: EventTargetIn<WeaponDurabilityLossEvent>(S.ALL_MINIONS)));

		var cardDb = new Dictionary<string, Card>
		{
			["TEST_WEAPON_DURABILITY_BRIDGE_LISTENER"] = MakeCard("TEST_WEAPON_DURABILITY_BRIDGE_LISTENER", CardType.Minion, cost: 1, atk: 1, health: 2),
			["TEST_WEAPON"] = MakeCard("TEST_WEAPON", CardType.Weapon, cost: 2, atk: 3, health: 2),
			["TEST_MINION_DEFENDER"] = MakeCard("TEST_MINION_DEFENDER", CardType.Minion, cost: 1, atk: 1, health: 2),
		};

		var game = CreateGame(cardDb);
		var listener = game.Player1.CreateCard("TEST_WEAPON_DURABILITY_BRIDGE_LISTENER", Zone.Play) as Minion;
		var weapon = game.Player1.CreateCard("TEST_WEAPON", Zone.Play) as Weapon;
		var minionDefender = game.Player2.CreateCard("TEST_MINION_DEFENDER", Zone.Play) as Minion;

		Assert.NotNull(listener);
		Assert.NotNull(weapon);
		Assert.NotNull(minionDefender);

		game.EventBus.Publish(new WeaponDurabilityLossEvent(weapon!, game.Player1.Hero!, weapon!, minionDefender!, 1), WeaponDurabilityLossPhase.BeforeLoseDurability);
		Assert.Equal(2, listener!.Atk);

		game.EventBus.Publish(new WeaponDurabilityLossEvent(weapon!, game.Player1.Hero!, weapon!, game.Player2.Hero!, 1), WeaponDurabilityLossPhase.BeforeLoseDurability);
		Assert.Equal(2, listener.Atk);
	}

	[Fact]
	public void ExactSelectors_CostMatchesAndAgreesWithMinMaxCost()
	{
		var cardDb = new Dictionary<string, Card>
		{
			["TEST_ONE_COST_SPELL"] = MakeCard("TEST_ONE_COST_SPELL", CardType.Spell, cost: 1),
			["TEST_ONE_COST_MINION"] = MakeCard("TEST_ONE_COST_MINION", CardType.Minion, cost: 1, atk: 1, health: 1),
			["TEST_TWO_COST_SPELL"] = MakeCard("TEST_TWO_COST_SPELL", CardType.Spell, cost: 2),
		};

		var game = CreateGame(cardDb);
		var oneCostSpell = game.Player1.CreateCard("TEST_ONE_COST_SPELL", Zone.SetAside) as BuffableEntity;
		var oneCostMinion = game.Player1.CreateCard("TEST_ONE_COST_MINION", Zone.SetAside) as BuffableEntity;
		var twoCostSpell = game.Player1.CreateCard("TEST_TWO_COST_SPELL", Zone.SetAside) as BuffableEntity;

		Assert.NotNull(oneCostSpell);
		Assert.NotNull(oneCostMinion);
		Assert.NotNull(twoCostSpell);

		var exact = S.Cost(1).Eval(game.Entities, oneCostSpell!).Cast<BuffableEntity>().Select(e => e.CardId).OrderBy(id => id).ToList();
		var ranged = (S.MinCost(1) + S.MaxCost(1)).Eval(game.Entities, oneCostSpell!).Cast<BuffableEntity>().Select(e => e.CardId).OrderBy(id => id).ToList();

		Assert.Equal(new[] { "TEST_ONE_COST_MINION", "TEST_ONE_COST_SPELL" }, exact);
		Assert.Equal(ranged, exact);
		Assert.DoesNotContain(twoCostSpell, S.Cost(1).Eval(game.Entities, oneCostSpell!));
	}

	[Fact]
	public void ExactSelectors_AtkMatchesOnlyCurrentAttack()
	{
		var cardDb = new Dictionary<string, Card>
		{
			["TEST_ONE_ATK"] = MakeCard("TEST_ONE_ATK", CardType.Minion, cost: 1, atk: 1, health: 2),
			["TEST_TWO_ATK"] = MakeCard("TEST_TWO_ATK", CardType.Minion, cost: 1, atk: 2, health: 2),
			["TEST_SPELL"] = MakeCard("TEST_SPELL", CardType.Spell, cost: 1),
		};

		var game = CreateGame(cardDb);
		var oneAtk = game.Player1.CreateCard("TEST_ONE_ATK", Zone.Play) as Minion;
		var twoAtk = game.Player1.CreateCard("TEST_TWO_ATK", Zone.Play) as Minion;
		var spell = game.Player1.CreateCard("TEST_SPELL", Zone.SetAside) as BuffableEntity;

		Assert.NotNull(oneAtk);
		Assert.NotNull(twoAtk);
		Assert.NotNull(spell);

		var selected = (S.MINION + S.Atk(1)).Eval(game.Entities, oneAtk!).Cast<BuffableEntity>().Select(e => e.CardId).ToList();

		Assert.Equal(new[] { "TEST_ONE_ATK" }, selected);
		Assert.DoesNotContain(twoAtk, S.Atk(1).Eval(game.Entities, oneAtk!));
		Assert.DoesNotContain(spell, S.Atk(1).Eval(game.Entities, oneAtk!));
	}

	[Fact]
	public void ExactSelectors_HealthAndMaxHealthAreDistinct()
	{
		var cardDb = new Dictionary<string, Card>
		{
			["TEST_DAMAGED_ONE_OF_FOUR"] = MakeCard("TEST_DAMAGED_ONE_OF_FOUR", CardType.Minion, cost: 1, atk: 1, health: 4),
			["TEST_HEALTHY_ONE_OF_ONE"] = MakeCard("TEST_HEALTHY_ONE_OF_ONE", CardType.Minion, cost: 1, atk: 1, health: 1),
			["TEST_HEALTHY_FOUR_OF_FOUR"] = MakeCard("TEST_HEALTHY_FOUR_OF_FOUR", CardType.Minion, cost: 1, atk: 1, health: 4),
		};

		var game = CreateGame(cardDb);
		var damaged = game.Player1.CreateCard("TEST_DAMAGED_ONE_OF_FOUR", Zone.Play) as Minion;
		var healthyOne = game.Player1.CreateCard("TEST_HEALTHY_ONE_OF_ONE", Zone.Play) as Minion;
		var healthyFour = game.Player1.CreateCard("TEST_HEALTHY_FOUR_OF_FOUR", Zone.Play) as Minion;

		Assert.NotNull(damaged);
		Assert.NotNull(healthyOne);
		Assert.NotNull(healthyFour);

		damaged!.Hit(3);

		var healthOne = (S.MINION + S.Health(1)).Eval(game.Entities, damaged).Cast<BuffableEntity>().Select(e => e.CardId).OrderBy(id => id).ToList();
		var maxHealthOne = (S.MINION + S.MaxHealth(1)).Eval(game.Entities, damaged).Cast<BuffableEntity>().Select(e => e.CardId).OrderBy(id => id).ToList();
		var maxHealthFour = (S.MINION + S.MaxHealth(4)).Eval(game.Entities, damaged).Cast<BuffableEntity>().Select(e => e.CardId).OrderBy(id => id).ToList();

		Assert.Equal(new[] { "TEST_DAMAGED_ONE_OF_FOUR", "TEST_HEALTHY_ONE_OF_ONE" }, healthOne);
		Assert.Equal(new[] { "TEST_HEALTHY_ONE_OF_ONE" }, maxHealthOne);
		Assert.Equal(new[] { "TEST_DAMAGED_ONE_OF_FOUR", "TEST_HEALTHY_FOUR_OF_FOUR" }, maxHealthFour);
	}

	[Fact]
	public void RandomCardHasTag_MatchesSparePartsAndAliasUsesSamePool()
	{
		var cardDb = new Dictionary<string, Card>
		{
			["TEST_SPARE_PART_A"] = new()
			{
				CardId = "TEST_SPARE_PART_A",
				CardType = CardType.Spell,
				Cost = 1,
				Collectible = true,
				CardSet = CardSet.Gvg,
				TagsInt = new Dictionary<GameTag, int> { [GameTag.SparePart] = 1 },
			},
			["TEST_SPARE_PART_B"] = new()
			{
				CardId = "TEST_SPARE_PART_B",
				CardType = CardType.Spell,
				Cost = 1,
				Collectible = true,
				CardSet = CardSet.Gvg,
				TagsInt = new Dictionary<GameTag, int> { [GameTag.SparePart] = 1 },
			},
			["TEST_NORMAL_SPELL"] = MakeCard("TEST_NORMAL_SPELL", CardType.Spell, cost: 1),
		};

		var game = CreateGame(cardDb);
		var source = game.Player1.CreateCard("TEST_NORMAL_SPELL", Zone.Play) as BuffableEntity;

		Assert.NotNull(source);

		var fromTag = RandomCards.RandomCard().HasTag(GameTag.SparePart).FindCards(source!).OrderBy(id => id).ToList();
		var fromAlias = RandomCards.RandomSparePart().FindCards(source!).OrderBy(id => id).ToList();

		Assert.Equal(new[] { "TEST_SPARE_PART_A", "TEST_SPARE_PART_B" }, fromTag);
		Assert.Equal(fromTag, fromAlias);
		Assert.DoesNotContain("TEST_NORMAL_SPELL", fromTag);
	}

	[Fact]
	public void And_PlayedTypeAndPlayedAtk_MatchesOnlyOneAttackMinions()
	{
		CardScriptRegistry.Instance.Initialize();

		var builder = new CardBuilder(CardScriptRegistry.Instance);
		builder.Buff("TEST_PLAYED_MINION_ATK_BUFF", atk: 1);
		builder.Card("TEST_PLAYED_MINION_ATK_LISTENER",
			on: When<CardPlayedEvent>(OWN, AFTER,
				new Enchant(S.SELF, "TEST_PLAYED_MINION_ATK_BUFF"),
				when: And(PlayedType(CardType.Minion), PlayedAtk(1))));

		var cardDb = new Dictionary<string, Card>
		{
			["TEST_PLAYED_MINION_ATK_LISTENER"] = MakeCard("TEST_PLAYED_MINION_ATK_LISTENER", CardType.Minion, cost: 1, atk: 1, health: 2),
			["TEST_ONE_ATK_MINION"] = MakeCard("TEST_ONE_ATK_MINION", CardType.Minion, cost: 1, atk: 1, health: 1),
			["TEST_TWO_ATK_MINION"] = MakeCard("TEST_TWO_ATK_MINION", CardType.Minion, cost: 1, atk: 2, health: 1),
			["TEST_ONE_COST_SPELL"] = MakeCard("TEST_ONE_COST_SPELL", CardType.Spell, cost: 1),
		};

		var game = CreateGame(cardDb);
		var listener = game.Player1.CreateCard("TEST_PLAYED_MINION_ATK_LISTENER", Zone.Play) as Minion;
		var oneAtkMinion = game.Player1.CreateCard("TEST_ONE_ATK_MINION", Zone.SetAside) as BuffableEntity;
		var twoAtkMinion = game.Player1.CreateCard("TEST_TWO_ATK_MINION", Zone.SetAside) as BuffableEntity;
		var oneCostSpell = game.Player1.CreateCard("TEST_ONE_COST_SPELL", Zone.SetAside) as BuffableEntity;

		Assert.NotNull(listener);
		Assert.NotNull(oneAtkMinion);
		Assert.NotNull(twoAtkMinion);
		Assert.NotNull(oneCostSpell);

		game.EventBus.Publish(new CardPlayedEvent(oneAtkMinion!, oneAtkMinion!, null), SpellPhase.AfterSpell);
		game.EventBus.Publish(new CardPlayedEvent(twoAtkMinion!, twoAtkMinion!, null), SpellPhase.AfterSpell);
		game.EventBus.Publish(new CardPlayedEvent(oneCostSpell!, oneCostSpell!, null), SpellPhase.AfterSpell);

		Assert.Equal(2, listener!.Atk);
	}

	[Fact]
	public void And_PlayedTypeAndPlayedCost_MatchesOnlyOneCostSpells()
	{
		CardScriptRegistry.Instance.Initialize();

		var builder = new CardBuilder(CardScriptRegistry.Instance);
		builder.Buff("TEST_PLAYED_SPELL_COST_BUFF", atk: 1);
		builder.Card("TEST_PLAYED_SPELL_COST_LISTENER",
			on: When<CardPlayedEvent>(OWN, AFTER,
				new Enchant(S.SELF, "TEST_PLAYED_SPELL_COST_BUFF"),
				when: And(PlayedType(CardType.Spell), PlayedCost(1))));

		var cardDb = new Dictionary<string, Card>
		{
			["TEST_PLAYED_SPELL_COST_LISTENER"] = MakeCard("TEST_PLAYED_SPELL_COST_LISTENER", CardType.Minion, cost: 1, atk: 1, health: 2),
			["TEST_ONE_COST_SPELL"] = MakeCard("TEST_ONE_COST_SPELL", CardType.Spell, cost: 1),
			["TEST_TWO_COST_SPELL"] = MakeCard("TEST_TWO_COST_SPELL", CardType.Spell, cost: 2),
			["TEST_ONE_COST_MINION"] = MakeCard("TEST_ONE_COST_MINION", CardType.Minion, cost: 1, atk: 1, health: 1),
		};

		var game = CreateGame(cardDb);
		var listener = game.Player1.CreateCard("TEST_PLAYED_SPELL_COST_LISTENER", Zone.Play) as Minion;
		var oneCostSpell = game.Player1.CreateCard("TEST_ONE_COST_SPELL", Zone.SetAside) as BuffableEntity;
		var twoCostSpell = game.Player1.CreateCard("TEST_TWO_COST_SPELL", Zone.SetAside) as BuffableEntity;
		var oneCostMinion = game.Player1.CreateCard("TEST_ONE_COST_MINION", Zone.SetAside) as BuffableEntity;

		Assert.NotNull(listener);
		Assert.NotNull(oneCostSpell);
		Assert.NotNull(twoCostSpell);
		Assert.NotNull(oneCostMinion);

		game.EventBus.Publish(new CardPlayedEvent(oneCostSpell!, oneCostSpell!, null), SpellPhase.AfterSpell);
		game.EventBus.Publish(new CardPlayedEvent(twoCostSpell!, twoCostSpell!, null), SpellPhase.AfterSpell);
		game.EventBus.Publish(new CardPlayedEvent(oneCostMinion!, oneCostMinion!, null), SpellPhase.AfterSpell);

		Assert.Equal(2, listener!.Atk);
	}

	[Fact]
	public void LOE_119_UsesEventTargetInForFriendlyHeroOnly()
	{
		CardScriptRegistry.Instance.Initialize();

		var cardDb = new Dictionary<string, Card>
		{
			["LOE_119"] = MakeCard("LOE_119", CardType.Minion, cost: 1, atk: 1, health: 4),
			["TEST_SOURCE"] = MakeCard("TEST_SOURCE", CardType.Minion, cost: 1, atk: 1, health: 1),
			["TEST_ALLY"] = MakeCard("TEST_ALLY", CardType.Minion, cost: 1, atk: 1, health: 2),
		};

		var game = CreateGame(cardDb);
		var source = game.Player2.CreateCard("TEST_SOURCE", Zone.Play) as Minion;
		var ally = game.Player1.CreateCard("TEST_ALLY", Zone.Play) as Minion;
		var watcher = game.Player1.CreateCard("LOE_119", Zone.Play) as Minion;

		Assert.NotNull(source);
		Assert.NotNull(ally);
		Assert.NotNull(watcher);

		var friendlyHeroDamage = new PreDamageEvent(source!, game.Player1.Hero!, 3);
		game.EventBus.Publish(friendlyHeroDamage, MiscPhase.On);
		Assert.Equal(1, friendlyHeroDamage.Amount);

		var friendlyMinionDamage = new PreDamageEvent(source!, ally!, 3);
		game.EventBus.Publish(friendlyMinionDamage, MiscPhase.On);
		Assert.Equal(3, friendlyMinionDamage.Amount);

		var enemyHeroDamage = new PreDamageEvent(source!, game.Player2.Hero!, 3);
		game.EventBus.Publish(enemyHeroDamage, MiscPhase.On);
		Assert.Equal(3, enemyHeroDamage.Amount);
	}

	[Fact]
	public void LOE_027_UsesSelectorCountAtLeastForThreeOtherEnemyMinions()
	{
		CardScriptRegistry.Instance.Initialize();

		var cardDb = new Dictionary<string, Card>
		{
			["LOE_027"] = new Card
			{
				CardId = "LOE_027",
				CardType = CardType.Spell,
				Cost = 1,
				Collectible = true,
				CardSet = CardSet.Core,
				TagsInt = new Dictionary<GameTag, int> { [GameTag.Secret] = 1 },
			},
			["TEST_PLAYED"] = MakeCard("TEST_PLAYED", CardType.Minion, cost: 1, atk: 1, health: 1),
			["TEST_BOARD_MINION_A"] = MakeCard("TEST_BOARD_MINION_A", CardType.Minion, cost: 1, atk: 1, health: 1),
			["TEST_BOARD_MINION_B"] = MakeCard("TEST_BOARD_MINION_B", CardType.Minion, cost: 1, atk: 1, health: 1),
			["TEST_BOARD_MINION_C"] = MakeCard("TEST_BOARD_MINION_C", CardType.Minion, cost: 1, atk: 1, health: 1),
		};

		var game = CreateGame(cardDb);
		var secret = game.Player1.CreateCard("LOE_027", Zone.Secret);
		Assert.NotNull(secret);

		game.Player2.CreateCard("TEST_BOARD_MINION_A", Zone.Play);
		game.Player2.CreateCard("TEST_BOARD_MINION_B", Zone.Play);
		game.Player2.CreateCard("TEST_BOARD_MINION_C", Zone.Play);

		var destroyed = game.Player2.CreateCard("TEST_PLAYED", Zone.Play) as Minion;
		Assert.NotNull(destroyed);

		game.EventBus.Publish(new MinionPlayedEvent(destroyed!, destroyed!, null), MinionPhase.AfterPlay);
		Assert.True(destroyed!.Dead || destroyed.Zone != Zone.Play);

		var secondGame = CreateGame(cardDb);
		var secondSecret = secondGame.Player1.CreateCard("LOE_027", Zone.Secret);
		Assert.NotNull(secondSecret);

		secondGame.Player2.CreateCard("TEST_BOARD_MINION_A", Zone.Play);
		secondGame.Player2.CreateCard("TEST_BOARD_MINION_B", Zone.Play);

		var survives = secondGame.Player2.CreateCard("TEST_PLAYED", Zone.Play) as Minion;
		Assert.NotNull(survives);

		secondGame.EventBus.Publish(new MinionPlayedEvent(survives!, survives!, null), MinionPhase.AfterPlay);
		Assert.Equal(Zone.Play, survives!.Zone);
		Assert.False(survives.Dead);
	}

	[Fact]
	public void SequenceRunner_PlaySpell_ResolveSpellEffectIncludesTwinspellAndOverload()
	{
		CardScriptRegistry.Instance.Initialize();
		RegisterTestSpellCards();

		var cardDb = new Dictionary<string, Card>
		{
			["TEST_ALLY"] = MakeCard("TEST_ALLY", CardType.Minion, cost: 1, atk: 1, health: 1),
			["TEST_EFFECT_SPELL"] = MakeCard("TEST_EFFECT_SPELL", CardType.Spell, cost: 1),
			["TEST_EFFECT_COPY"] = MakeCard("TEST_EFFECT_COPY", CardType.Spell, cost: 1),
		};

		var game = CreateGame(cardDb);
		var ally = game.Player1.CreateCard("TEST_ALLY", Zone.Play) as Minion;
		var spell = game.Player1.CreateCard("TEST_EFFECT_SPELL", Zone.SetAside) as Spell;

		Assert.NotNull(ally);
		Assert.NotNull(spell);

		spell!.Twinspell = true;
		spell.TwinspellCopyId = "TEST_EFFECT_COPY";
		spell.Overload = 2;
		game.Player1.MaxMana = 10;

		int afterSpellCount = 0;
		int overloadCount = 0;
		game.EventBus.Subscribe<SpellCastEvent>(ally!, SpellPhase.AfterSpell, evt => true, evt => afterSpellCount++);
		game.EventBus.Subscribe<OverloadEvent>(ally!, MiscPhase.On, evt => true, evt => overloadCount++);

		game.Sequences.PlaySpell(spell, ally);

		Assert.Equal(2, ally!.Atk);
		Assert.Equal(1, afterSpellCount);
		Assert.Equal(1, overloadCount);
		Assert.Equal(2, game.Player1.Overloaded);
		Assert.Contains(game.Player1.Hand.Cast<BuffableEntity>(), card => card.CardId == "TEST_EFFECT_COPY");
		Assert.Equal(Zone.Graveyard, spell.Zone);
	}

	[Fact]
	public void LOE_053_CopiesSpellEffectWithoutRepublishingSpellCastEvent()
	{
		CardScriptRegistry.Instance.Initialize();
		RegisterTestSpellCards();

		var cardDb = new Dictionary<string, Card>
		{
			["LOE_053"] = MakeCard("LOE_053", CardType.Minion, cost: 5, atk: 4, health: 6),
			["TEST_ALLY"] = MakeCard("TEST_ALLY", CardType.Minion, cost: 1, atk: 1, health: 1),
			["TEST_TRIGGER"] = MakeCard("TEST_TRIGGER", CardType.Minion, cost: 1, atk: 1, health: 1),
			["TEST_EFFECT_SPELL"] = MakeCard("TEST_EFFECT_SPELL", CardType.Spell, cost: 1),
			["TEST_EFFECT_COPY"] = MakeCard("TEST_EFFECT_COPY", CardType.Spell, cost: 1),
		};

		var game = CreateGame(cardDb);
		var djinni = game.Player1.CreateCard("LOE_053", Zone.Play) as Minion;
		var ally = game.Player1.CreateCard("TEST_ALLY", Zone.Play) as Minion;
		var trigger = game.Player1.CreateCard("TEST_TRIGGER", Zone.Play) as Minion;
		var spell = game.Player1.CreateCard("TEST_EFFECT_SPELL", Zone.SetAside) as Spell;

		Assert.NotNull(djinni);
		Assert.NotNull(ally);
		Assert.NotNull(trigger);
		Assert.NotNull(spell);

		spell!.Twinspell = true;
		spell.TwinspellCopyId = "TEST_EFFECT_COPY";
		spell.Overload = 2;
		game.Player1.MaxMana = 10;

		int afterSpellCount = 0;
		int overloadCount = 0;
		game.EventBus.Subscribe<SpellCastEvent>(trigger!, SpellPhase.AfterSpell, evt => true, evt => afterSpellCount++);
		game.EventBus.Subscribe<OverloadEvent>(trigger!, MiscPhase.On, evt => true, evt => overloadCount++);

		game.Sequences.PlaySpell(spell, ally);

		Assert.Equal(2, ally!.Atk);
		Assert.Equal(5, djinni!.Atk);
		Assert.Equal(2, trigger!.Atk);
		Assert.Equal(1, afterSpellCount);
		Assert.Equal(2, overloadCount);
		Assert.Equal(4, game.Player1.Overloaded);
		Assert.Equal(2, game.Player1.Hand.Cast<BuffableEntity>().Count(card => card.CardId == "TEST_EFFECT_COPY"));
	}

	[Fact]
	public void CopySpellEffectOn_DoesNotRunOnActivateOrResolveInvalidTarget()
	{
		CardScriptRegistry.Instance.Initialize();

		int onActivateCount = 0;
		CardScriptRegistry.Instance.Register("TEST_ACTIVATE_SPELL", new InlineCardScript
		{
			Play = new HearthstoneServer.Dsl.Action[] { new Enchant(S.TARGET, "TEST_COPY_BUFF") },
			OnActivate = (_, _) => onActivateCount++,
			TargetReq = Target.Required().Friendly().Minion(),
		});
		CardScriptRegistry.Instance.Register("TEST_EFFECT_COPY", new InlineCardScript());
		CardScriptRegistry.Instance.Register("TEST_COPY_BUFF", new InlineCardScript
		{
			Tags = new Dictionary<GameTag, object> { [GameTag.Atk] = 1 },
		});

		var cardDb = new Dictionary<string, Card>
		{
			["TEST_SOURCE"] = MakeCard("TEST_SOURCE", CardType.Minion, cost: 1, atk: 1, health: 1),
			["TEST_ACTIVATE_SPELL"] = MakeCard("TEST_ACTIVATE_SPELL", CardType.Spell, cost: 1),
			["TEST_EFFECT_COPY"] = MakeCard("TEST_EFFECT_COPY", CardType.Spell, cost: 1),
		};

		var game = CreateGame(cardDb);
		var source = game.Player1.CreateCard("TEST_SOURCE", Zone.Play) as Minion;
		var spell = game.Player1.CreateCard("TEST_ACTIVATE_SPELL", Zone.SetAside) as Spell;

		Assert.NotNull(source);
		Assert.NotNull(spell);

		spell!.Twinspell = true;
		spell.TwinspellCopyId = "TEST_EFFECT_COPY";
		spell.Overload = 2;

		int afterSpellCount = 0;
		int overloadCount = 0;
		game.EventBus.Subscribe<SpellCastEvent>(null, SpellPhase.AfterSpell, evt => true, evt => afterSpellCount++);
		game.EventBus.Subscribe<OverloadEvent>(null, MiscPhase.On, evt => true, evt => overloadCount++);

		new CopySpellEffectOn(spell, S.ENEMY_HERO).Trigger(source!);

		Assert.Equal(1, source!.Atk);
		Assert.Equal(1, onActivateCount);
		Assert.Equal(0, afterSpellCount);
		Assert.Equal(0, overloadCount);
		Assert.Equal(0, game.Player1.Overloaded);
		Assert.DoesNotContain(game.Player1.Hand.Cast<BuffableEntity>(), card => card.CardId == "TEST_EFFECT_COPY");
		Assert.Equal(Zone.SetAside, spell.Zone);
	}

	[Fact]
	public void LOE_089_SummonsThreeDistinctRunts()
	{
		CardScriptRegistry.Instance.Initialize();

		var cardDb = new Dictionary<string, Card>
		{
			["LOE_089"] = MakeCard("LOE_089", CardType.Minion, cost: 6, atk: 6, health: 6),
			["LOE_089t"] = MakeCard("LOE_089t", CardType.Minion, cost: 2, atk: 2, health: 2),
			["LOE_089t2"] = MakeCard("LOE_089t2", CardType.Minion, cost: 2, atk: 2, health: 2),
			["LOE_089t3"] = MakeCard("LOE_089t3", CardType.Minion, cost: 2, atk: 2, health: 2),
		};

		var game = CreateGame(cardDb);
		var wobblingRunts = game.Player1.CreateCard("LOE_089", Zone.Play) as Minion;
		Assert.NotNull(wobblingRunts);

		var script = CardScriptRegistry.Instance.GetScript("LOE_089");
		Assert.NotNull(script);

		game.TriggerBlock(wobblingRunts!, script!.Deathrattle.ToList());

		var summonedIds = game.Player1.Field
			.OfType<Minion>()
			.Where(m => m != wobblingRunts)
			.Select(m => m.CardId)
			.OrderBy(id => id)
			.ToList();

		Assert.Equal(new[] { "LOE_089t", "LOE_089t2", "LOE_089t3" }, summonedIds);
	}

	private static Game CreateGame(Dictionary<string, Card> cardDb)
	{
		var p1 = new Player("P1") { Hero = new Hero { MaxHealth = 30 } };
		var p2 = new Player("P2") { Hero = new Hero { MaxHealth = 30 } };
		var game = new Game(p1, p2, seed: 42)
		{
			CardDb = cardDb,
			IsStandard = false,
		};
		game.Setup();
		return game;
	}

	private static Card MakeCard(
		string id,
		CardType type,
		int cost = 0,
		int atk = 0,
		int health = 0,
		Rarity rarity = Rarity.Common,
		params Race[] races) => new()
		{
			CardId = id,
			CardType = type,
			Cost = cost,
			Attack = atk,
			Health = health,
			Collectible = true,
			Rarity = rarity,
			CardSet = CardSet.Core,
			Races = races,
		};

	private static void AssignHeroPower(Player player, string cardId)
	{
		var power = player.CreateCard(cardId, Zone.Play) as HeroPower;
		Assert.NotNull(power);
		player.Hero!.Power = power;
	}

	private static void RegisterTestSpellCards()
	{
		var builder = new CardBuilder(CardScriptRegistry.Instance);
		builder.Buff("TEST_COPY_BUFF", atk: 1);
		builder.Card("TEST_COPY_SPELL",
			play: new Enchant(S.TARGET, "TEST_COPY_BUFF"),
			target: Target.Required().Friendly().Minion());
		builder.Card("TEST_EFFECT_SPELL",
			play: new Enchant(S.TARGET, "TEST_COPY_BUFF"),
			target: Target.Required().Friendly().Minion());
		builder.Card("TEST_EFFECT_COPY");
		builder.Card("TEST_TRIGGER",
			on: When<SpellCastEvent>(OWN, AFTER, new Enchant(S.SELF, "TEST_COPY_BUFF")));
	}
}
