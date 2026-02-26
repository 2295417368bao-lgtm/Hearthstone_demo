using HearthstoneServer.Dsl;
using HearthstoneServer.Entities;
using HearthstoneServer.Managers;
using HearthstoneServer;
using Xunit;

namespace Hearthstone.Tests;

/// <summary>
/// Phase 10c 单元测试 �?Special / Low-frequency action 测试�?
/// </summary>
public class SpecialActionTests
{
	// ── helpers ──

	private class MockEntity : BuffableEntity
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

	private class TestPlayer : Player
	{
		public TestPlayer(string name, int id) : base(name)
		{
			EntityId = id;
			Tags = new CardManager(this);
		}
	}

	private class TestMinion : Minion
	{
		public TestMinion(int id, int controllerId)
		{
			EntityId = id;
			Tags = new CardManager(this);
			Tags[GameTag.Controller] = controllerId;
			Zone = Zone.Play;
		}
	}

	private class TestQuest : Quest
	{
		public TestQuest(int id)
		{
			EntityId = id;
			Tags = new CardManager(this);
		}
	}

	// ══════════════════════════════════════�?
	// JoustAction
	// ══════════════════════════════════════�?

	[Fact]
	public void JoustAction_DoesNotCrash()
	{
		var src = new MockEntity(1, 1);
		var challenger = new MockEntity(2, 1);
		var defender = new MockEntity(3, 2);

		// Without a Game set, Joust should not crash (Game?.Joust guard)
		var joust = new Joust(challenger, defender);
		joust.Trigger(src);
		// Simply verifying no exception
	}

	// ══════════════════════════════════════�?
	// SummonJadeGolemAction
	// ══════════════════════════════════════�?

	[Fact]
	public void SummonJadeGolem_IncrementsJadeCounter()
	{
		var src = new MockEntity(1, 1);
		var player = new TestPlayer("Test", 2);
		player.JadeGolem = 1;

		new SummonJadeGolem(player).Trigger(src);

		Assert.Equal(2, player.JadeGolem);
	}

	[Fact]
	public void SummonJadeGolem_MaxAt30()
	{
		var src = new MockEntity(1, 1);
		var player = new TestPlayer("Test", 2);
		player.JadeGolem = 30;

		new SummonJadeGolem(player).Trigger(src);

		Assert.Equal(30, player.JadeGolem);
	}

	[Fact]
	public void SummonJadeGolem_IgnoresNonPlayer()
	{
		var src = new MockEntity(1, 1);
		var target = new MockEntity(2, 2);

		// Should not crash when target is a non-Player entity
		new SummonJadeGolem(target).Trigger(src);
	}

	// ══════════════════════════════════════�?
	// CastSpellAction
	// ══════════════════════════════════════�?

	[Fact]
	public void CastSpell_DoesNotCrashWithMinionSource()
	{
		var src = new MockEntity(1, 1);
		var spell = new MockEntity(2, 1, CardType.Spell);

		// Without Game, should complete gracefully
		var cast = new CastSpell(spell);
		cast.Trigger(src);
	}

	// ══════════════════════════════════════�?
	// AddProgressAction
	// ══════════════════════════════════════�?

	[Fact]
	public void AddProgress_IncrementsQuestProgress()
	{
		var src = new MockEntity(1, 1);
		var quest = new TestQuest(2);
		quest.Progress = 0;
		quest.ProgressTotal = 5;

		new AddProgress(quest, 2).Trigger(src);

		Assert.Equal(2, quest.Progress);
	}

	[Fact]
	public void AddProgress_DefaultAmount_IncrementsBy1()
	{
		var src = new MockEntity(1, 1);
		var quest = new TestQuest(2);
		quest.Progress = 3;
		quest.ProgressTotal = 10;

		new AddProgress(quest).Trigger(src);

		Assert.Equal(4, quest.Progress);
	}

	// ══════════════════════════════════════�?
	// ClearProgressAction
	// ══════════════════════════════════════�?

	[Fact]
	public void ClearProgress_ResetsQuestProgress()
	{
		var src = new MockEntity(1, 1);
		var quest = new TestQuest(2);
		quest.Progress = 3;

		new ClearProgress(quest).Trigger(src);

		Assert.Equal(0, quest.Progress);
	}

	// ══════════════════════════════════════�?
	// ReplayAction
	// ══════════════════════════════════════�?

	[Fact]
	public void Replay_DoesNotCrashWithoutGame()
	{
		var src = new MockEntity(1, 1);
		var target = new MockEntity(2, 2, CardType.Spell);

		// Without Game, should complete gracefully (source.Game == null guard)
		new Replay(target).Trigger(src);
	}

	// ══════════════════════════════════════�?
	// InvokeAction
	// ══════════════════════════════════════�?

	[Fact]
	public void Invoke_IncrementsInvokeCounter()
	{
		var player = new TestPlayer("Test", 1);
		player.InvokeCounter = 0;
		var src = new MockEntity(2, 1);
		src.Controller = player;

		var galakrond = new MockEntity(3, 1);

		new Invoke(galakrond).Trigger(src);

		Assert.Equal(1, player.InvokeCounter);
	}

	[Fact]
	public void Invoke_NoController_DoesNotCrash()
	{
		var src = new MockEntity(1, 1);
		var galakrond = new MockEntity(2, 1);

		// MockEntity.Controller is null, should bail gracefully
		new Invoke(galakrond).Trigger(src);
	}

	// ══════════════════════════════════════�?
	// AwakenAction
	// ══════════════════════════════════════�?

	[Fact]
	public void Awaken_SetsDormantFalse()
	{
		var src = new MockEntity(1, 1);
		var minion = new TestMinion(2, 2);
		minion.Dormant = true;
		minion.TurnsInPlay = 3;

		new Awaken(minion).Trigger(src);

		Assert.False(minion.Dormant);
		Assert.Equal(0, minion.TurnsInPlay);
	}

	[Fact]
	public void Awaken_NonDormant_NoEffect()
	{
		var src = new MockEntity(1, 1);
		var minion = new TestMinion(2, 2);
		minion.Dormant = false;
		minion.TurnsInPlay = 5;

		new Awaken(minion).Trigger(src);

		// Should not change turns_in_play for non-dormant
		Assert.Equal(5, minion.TurnsInPlay);
	}

	[Fact]
	public void Awaken_NonMinion_NoEffect()
	{
		var src = new MockEntity(1, 1);
		var target = new MockEntity(2, 2, CardType.Spell);

		// Non-minion should be ignored gracefully
		new Awaken(target).Trigger(src);
	}

	// ══════════════════════════════════════�?
	// DormantAction
	// ══════════════════════════════════════�?

	[Fact]
	public void Dormant_SetsDormantAndTurns()
	{
		var src = new MockEntity(1, 1);
		var minion = new TestMinion(2, 2);
		minion.Dormant = false;
		minion.DormantTurns = 0;

		new DormantAction(minion, 3).Trigger(src);

		Assert.True(minion.Dormant);
		Assert.Equal(3, minion.DormantTurns);
	}

	[Fact]
	public void Dormant_AccumulatesTurns()
	{
		var src = new MockEntity(1, 1);
		var minion = new TestMinion(2, 2);
		minion.Dormant = true;
		minion.DormantTurns = 2;

		new DormantAction(minion, 3).Trigger(src);

		Assert.True(minion.Dormant);
		Assert.Equal(5, minion.DormantTurns);
	}

	[Fact]
	public void Dormant_NonMinion_NoEffect()
	{
		var src = new MockEntity(1, 1);
		var target = new MockEntity(2, 2, CardType.Spell);

		// Non-minion should be ignored
		new DormantAction(target, 3).Trigger(src);
	}
}
