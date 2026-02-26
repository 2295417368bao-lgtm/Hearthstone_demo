using HearthstoneServer;
using HearthstoneServer.Dsl;
using HearthstoneServer.Entities;
using HearthstoneServer.Events;
using CardData = HearthstoneServer.LoadCardIntoDbf.Card;
using Action = HearthstoneServer.Dsl.Action;

namespace Hearthstone.Tests;

/// <summary>
/// Integration tests for the new EventBus + SequenceRunner phase pipeline.
/// Validates:
/// - EventBus subscribe/publish/unsubscribe
/// - Phase ordering (OnPlay before SpellText before AfterSpell)
/// - PlayCounter-based trigger ordering
/// - SequenceRunner spell/minion flows
/// - Counterspell-style cancel via "countered" flag
/// - One-shot (secret) subscriptions
/// </summary>
public class EventBusTests
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

	private static Minion CreateMinion(Game game, Player owner, int atk = 1, int health = 1,
		string? cardId = null, int cost = 0)
	{
		var m = new Minion
		{
			Controller = owner,
			MaxHealth = health,
			Atk = atk,
			CardData = cardId != null ? new CardData { CardId = cardId } : null,
		};
		m.SetIntProperty("cost", cost);
		game.RegisterEntity(m);
		m.SetZone(Zone.Hand);
		return m;
	}

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

	// ══════════════════════════════════════════�?
	// 1. Basic Subscribe + Publish
	// ══════════════════════════════════════════�?

	[Fact]
	public void EventBus_Subscribe_ReceivesPublishedEvent()
	{
		var game = CreateTestGame();
		game.Setup();
		game.Start();

		var listener = CreateMinion(game, game.Player1, cardId: "listener");
		listener.SetZone(Zone.Play);

		bool triggered = false;
		game.EventBus.Subscribe<SpellCastEvent>(
			listener, SpellPhase.OnPlay,
			evt => true,
			evt => { triggered = true; });

		var spell = CreateSpell(game, game.Player1, cardId: "test_spell");
		game.EventBus.Publish(new SpellCastEvent(spell, spell, null), SpellPhase.OnPlay);

		Assert.True(triggered, "Subscriber should have been triggered");
	}

	[Fact]
	public void EventBus_Subscribe_DoesNotTriggerOnWrongPhase()
	{
		var game = CreateTestGame();
		game.Setup();
		game.Start();

		var listener = CreateMinion(game, game.Player1, cardId: "listener");
		listener.SetZone(Zone.Play);

		bool triggered = false;
		game.EventBus.Subscribe<SpellCastEvent>(
			listener, SpellPhase.AfterSpell,
			evt => true,
			evt => { triggered = true; });

		var spell = CreateSpell(game, game.Player1, cardId: "test_spell");
		game.EventBus.Publish(new SpellCastEvent(spell, spell, null), SpellPhase.OnPlay);

		Assert.False(triggered, "Subscriber at AfterSpell should NOT trigger for OnPlay");
	}

	// ══════════════════════════════════════════�?
	// 2. Phase Ordering
	// ══════════════════════════════════════════�?

	[Fact]
	public void SpellPhases_FireInCorrectOrder()
	{
		var game = CreateTestGame();
		game.Setup();
		game.Start();

		var listener = CreateMinion(game, game.Player1, cardId: "phase_tracker");
		listener.SetZone(Zone.Play);

		var phases = new List<string>();

		game.EventBus.Subscribe<SpellCastEvent>(
			listener, SpellPhase.OnPlay,
			evt => true,
			evt => { phases.Add("OnPlay"); });

		game.EventBus.Subscribe<SpellCastEvent>(
			listener, SpellPhase.AfterSpell,
			evt => true,
			evt => { phases.Add("AfterSpell"); });

		// Manually publish in correct phase order (simulates SequenceRunner)
		var spell = CreateSpell(game, game.Player1, cardId: "test_spell");
		game.EventBus.Publish(new SpellCastEvent(spell, spell, null), SpellPhase.OnPlay);
		game.EventBus.Publish(new SpellCastEvent(spell, spell, null), SpellPhase.AfterSpell);

		Assert.Equal(new[] { "OnPlay", "AfterSpell" }, phases);
	}

	// ══════════════════════════════════════════�?
	// 3. PlayCounter Ordering
	// ══════════════════════════════════════════�?

	[Fact]
	public void EventBus_TriggersInPlayCounterOrder()
	{
		var game = CreateTestGame();
		game.Setup();
		game.Start();

		// Create two minions with different PlayCounters
		var first = CreateMinion(game, game.Player1, cardId: "first");
		first.SetZone(Zone.Play);   // gets PlayCounter = N
		var second = CreateMinion(game, game.Player1, cardId: "second");
		second.SetZone(Zone.Play);  // gets PlayCounter = N+1

		var order = new List<string>();

		// Subscribe second first, then first �?EventBus should reorder by PlayCounter
		game.EventBus.Subscribe<SpellCastEvent>(
			second, SpellPhase.OnPlay,
			evt => true,
			evt => { order.Add("second"); });

		game.EventBus.Subscribe<SpellCastEvent>(
			first, SpellPhase.OnPlay,
			evt => true,
			evt => { order.Add("first"); });

		var spell = CreateSpell(game, game.Player1, cardId: "test_spell");
		game.EventBus.Publish(new SpellCastEvent(spell, spell, null), SpellPhase.OnPlay);

		Assert.Equal(new[] { "first", "second" }, order);
	}

	// ══════════════════════════════════════════�?
	// 4. Unsubscribe
	// ══════════════════════════════════════════�?

	[Fact]
	public void EventBus_Dispose_StopsTrigger()
	{
		var game = CreateTestGame();
		game.Setup();
		game.Start();

		var listener = CreateMinion(game, game.Player1, cardId: "listener");
		listener.SetZone(Zone.Play);

		int count = 0;
		var sub = game.EventBus.Subscribe<SpellCastEvent>(
			listener, SpellPhase.OnPlay,
			evt => true,
			evt => { count++; });

		var spell = CreateSpell(game, game.Player1, cardId: "test_spell");

		game.EventBus.Publish(new SpellCastEvent(spell, spell, null), SpellPhase.OnPlay);
		Assert.Equal(1, count);

		sub.Dispose(); // unsubscribe

		game.EventBus.Publish(new SpellCastEvent(spell, spell, null), SpellPhase.OnPlay);
		Assert.Equal(1, count); // should NOT increment
	}

	[Fact]
	public void EventBus_UnsubscribeAll_RemovesAllForEntity()
	{
		var game = CreateTestGame();
		game.Setup();
		game.Start();

		var listener = CreateMinion(game, game.Player1, cardId: "listener");
		listener.SetZone(Zone.Play);

		int count = 0;
		game.EventBus.Subscribe<SpellCastEvent>(
			listener, SpellPhase.OnPlay,
			evt => true,
			evt => { count++; });
		game.EventBus.Subscribe<SpellCastEvent>(
			listener, SpellPhase.AfterSpell,
			evt => true,
			evt => { count++; });

		game.EventBus.UnsubscribeAll(listener);

		var spell = CreateSpell(game, game.Player1, cardId: "test_spell");
		game.EventBus.Publish(new SpellCastEvent(spell, spell, null), SpellPhase.OnPlay);
		game.EventBus.Publish(new SpellCastEvent(spell, spell, null), SpellPhase.AfterSpell);

		Assert.Equal(0, count);
	}

	// ══════════════════════════════════════════�?
	// 5. Once (Secret) Subscription
	// ══════════════════════════════════════════�?

	[Fact]
	public void EventBus_Once_TriggersOnlyOnce()
	{
		var game = CreateTestGame();
		game.Setup();
		game.Start();

		var secret = CreateSpell(game, game.Player1, cardId: "secret_counterspell");
		secret.SetZone(Zone.Secret);

		int triggerCount = 0;
		game.EventBus.Subscribe<SpellCastEvent>(
			secret, SpellPhase.OnPlay,
			evt => true,
			evt => { triggerCount++; },
			once: true);

		var spell1 = CreateSpell(game, game.Player1, cardId: "spell1");
		var spell2 = CreateSpell(game, game.Player1, cardId: "spell2");

		game.EventBus.Publish(new SpellCastEvent(spell1, spell1, null), SpellPhase.OnPlay);
		game.EventBus.Publish(new SpellCastEvent(spell2, spell2, null), SpellPhase.OnPlay);

		Assert.Equal(1, triggerCount);
	}

	// ══════════════════════════════════════════�?
	// 6. Condition Filtering
	// ══════════════════════════════════════════�?

	[Fact]
	public void EventBus_Condition_FiltersEvents()
	{
		var game = CreateTestGame();
		game.Setup();
		game.Start();

		var listener = CreateMinion(game, game.Player1, cardId: "listener");
		listener.SetZone(Zone.Play);

		int count = 0;
		// Only trigger for opponent's spells
		game.EventBus.Subscribe<SpellCastEvent>(
			listener, SpellPhase.OnPlay,
			evt => evt.Spell.Controller != listener.Controller,
			evt => { count++; });

		var ownSpell = CreateSpell(game, game.Player1, cardId: "own_spell");
		var oppSpell = CreateSpell(game, game.Player2, cardId: "opp_spell");

		game.EventBus.Publish(new SpellCastEvent(ownSpell, ownSpell, null), SpellPhase.OnPlay);
		Assert.Equal(0, count); // own spell filtered out

		game.EventBus.Publish(new SpellCastEvent(oppSpell, oppSpell, null), SpellPhase.OnPlay);
		Assert.Equal(1, count); // opponent's spell triggers
	}

	// ══════════════════════════════════════════�?
	// 7. MinionPhase Ordering
	// ══════════════════════════════════════════�?

	[Fact]
	public void MinionPhases_FireInCorrectOrder()
	{
		var game = CreateTestGame();
		game.Setup();
		game.Start();

		var watcher = CreateMinion(game, game.Player1, cardId: "watcher");
		watcher.SetZone(Zone.Play);

		var phases = new List<string>();

		game.EventBus.Subscribe<MinionPlayedEvent>(
			watcher, MinionPhase.OnPlay,
			evt => true,
			evt => { phases.Add("OnPlay"); });

		game.EventBus.Subscribe<MinionSummonedEvent>(
			watcher, MinionPhase.OnSummon,
			evt => true,
			evt => { phases.Add("OnSummon"); });

		game.EventBus.Subscribe<MinionPlayedEvent>(
			watcher, MinionPhase.AfterPlay,
			evt => true,
			evt => { phases.Add("AfterPlay"); });

		game.EventBus.Subscribe<MinionSummonedEvent>(
			watcher, MinionPhase.AfterSummon,
			evt => true,
			evt => { phases.Add("AfterSummon"); });

		// Manually fire in correct order simulating SequenceRunner
		var newMinion = CreateMinion(game, game.Player1, cardId: "new_minion");
		game.EventBus.Publish(new MinionPlayedEvent(newMinion, newMinion, null), MinionPhase.OnPlay);
		game.EventBus.Publish(new MinionSummonedEvent(newMinion, newMinion), MinionPhase.OnSummon);
		game.EventBus.Publish(new MinionPlayedEvent(newMinion, newMinion, null), MinionPhase.AfterPlay);
		game.EventBus.Publish(new MinionSummonedEvent(newMinion, newMinion), MinionPhase.AfterSummon);

		Assert.Equal(new[] { "OnPlay", "OnSummon", "AfterPlay", "AfterSummon" }, phases);
	}

	// ══════════════════════════════════════════�?
	// 8. Counterspell-style cancel via SequenceRunner
	// ══════════════════════════════════════════�?

	[Fact]
	public void SequenceRunner_PlaySpell_CounteredSpellSkipsText()
	{
		var game = CreateTestGame();
		game.Setup();
		game.Start();
		game.CurrentPlayer = game.Player1;

		// Register a "Counterspell" secret on P2 �?it sets "countered" flag on OnPlay
		var counterspell = CreateSpell(game, game.Player2, cardId: "counter_secret");
		counterspell.SetZone(Zone.Secret);

		game.EventBus.Subscribe<SpellCastEvent>(
			counterspell, SpellPhase.OnPlay,
			evt => evt.Spell.Controller != counterspell.Controller, // opponent's spell only
			evt =>
			{
				// Counter the spell
				evt.Spell.SetBoolProperty("countered", true);
			},
			once: true);

		// Register a card script that tracks execution
		bool spellTextExecuted = false;

		// Create a spell with a registered script
		var testSpellId = "TEST_COUNTERABLE_SPELL";
		CardScriptRegistry.Instance.Register(testSpellId, new InlineCardScript
		{
			Play = new Action[] { new GenericAction(() => { spellTextExecuted = true; }) }
		});

		var spell = CreateSpell(game, game.Player1, cardId: testSpellId);
		game.Player1.MaxMana = 10;

		// Play the spell via SequenceRunner
		game.Sequences.PlaySpell(spell, null);

		// Spell text should NOT have executed (countered)
		Assert.False(spellTextExecuted, "Countered spell should not execute its text");

		// Spell should be in graveyard
		Assert.Equal(Zone.Graveyard, spell.Zone);
	}

	// ══════════════════════════════════════════�?
	// 9. SequenceRunner PlaySpell (uncountered)
	// ══════════════════════════════════════════�?

	[Fact]
	public void SequenceRunner_PlaySpell_ExecutesAllPhases()
	{
		var game = CreateTestGame();
		game.Setup();
		game.Start();
		game.CurrentPlayer = game.Player1;

		var watcher = CreateMinion(game, game.Player1, cardId: "watcher");
		watcher.SetZone(Zone.Play);

		var phases = new List<string>();

		game.EventBus.Subscribe<SpellCastEvent>(
			watcher, SpellPhase.OnPlay,
			evt => true,
			evt => { phases.Add("OnPlay"); });

		game.EventBus.Subscribe<SpellCastEvent>(
			watcher, SpellPhase.AfterSpell,
			evt => true,
			evt => { phases.Add("AfterSpell"); });

		bool spellTextExecuted = false;
		var testId = "TEST_TRACKED_SPELL";
		CardScriptRegistry.Instance.Register(testId, new InlineCardScript
		{
			Play = new Action[] { new GenericAction(() => { spellTextExecuted = true; phases.Add("SpellText"); }) }
		});

		var spell = CreateSpell(game, game.Player1, cardId: testId);
		game.Player1.MaxMana = 10;

		game.Sequences.PlaySpell(spell, null);

		Assert.True(spellTextExecuted, "Uncountered spell should execute its text");
		Assert.Equal(new[] { "OnPlay", "SpellText", "AfterSpell" }, phases);
	}

	// ══════════════════════════════════════════�?
	// 10. SequenceRunner PlayMinion phases
	// ══════════════════════════════════════════�?

	[Fact]
	public void SequenceRunner_PlayMinion_ExecutesAllPhases()
	{
		var game = CreateTestGame();
		game.Setup();
		game.Start();
		game.CurrentPlayer = game.Player1;

		var watcher = CreateMinion(game, game.Player1, cardId: "phase_watcher");
		watcher.SetZone(Zone.Play);

		var phases = new List<string>();

		game.EventBus.Subscribe<MinionPlayedEvent>(
			watcher, MinionPhase.OnPlay,
			evt => evt.Minion.CardId != "phase_watcher",
			evt => { phases.Add("OnPlay"); });

		game.EventBus.Subscribe<MinionSummonedEvent>(
			watcher, MinionPhase.OnSummon,
			evt => evt.Minion.CardId != "phase_watcher",
			evt => { phases.Add("OnSummon"); });

		game.EventBus.Subscribe<MinionPlayedEvent>(
			watcher, MinionPhase.AfterPlay,
			evt => evt.Minion.CardId != "phase_watcher",
			evt => { phases.Add("AfterPlay"); });

		game.EventBus.Subscribe<MinionSummonedEvent>(
			watcher, MinionPhase.AfterSummon,
			evt => evt.Minion.CardId != "phase_watcher",
			evt => { phases.Add("AfterSummon"); });

		var minion = CreateMinion(game, game.Player1, atk: 2, health: 3, cardId: "test_minion");
		game.Player1.MaxMana = 10;

		game.Sequences.PlayMinion(minion, null);

		Assert.Equal(new[] { "OnPlay", "OnSummon", "AfterPlay", "AfterSummon" }, phases);
		Assert.Equal(Zone.Play, minion.Zone);
	}
}
