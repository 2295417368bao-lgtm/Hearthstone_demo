using HearthstoneServer.Dsl;
using HearthstoneServer.Entities;
using HearthstoneServer.Managers;
using HearthstoneServer;
using Xunit;

namespace Hearthstone.Tests;

/// <summary>
/// Phase 10b 单元测试 �?P0 核心游戏流程 + P1 战斗/Buff 扩展 action 测试�?
/// </summary>
public class GameplayActionTests
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

	private class MockCharacter : Character
	{
		public MockCharacter(int id, int controllerId)
		{
			EntityId = id;
			Type = CardType.Minion;
			Tags = new CardManager(this);
			Tags[GameTag.Controller] = controllerId;
			Zone = Zone.Play;
		}
	}

	private class TestHeroPower : HeroPower
	{
		public TestHeroPower(int id)
		{
			EntityId = id;
			Tags = new CardManager(this);
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

	// ══════════════════════════════════════�?
	// P0 �?核心游戏流程 Actions
	// ══════════════════════════════════════�?

	// ─── OverloadAction ──────────

	[Fact]
	public void Overload_IncreasesOverloaded()
	{
		var src = new MockEntity(1, 1);
		var player = new TestPlayer("Test", 2);
		player.Overloaded = 0;
		player.OverloadedThisGame = 0;

		var overloadAction = new OverloadAction(player, 3);
		overloadAction.Trigger(src);

		Assert.Equal(3, player.Overloaded);
		Assert.Equal(3, player.OverloadedThisGame);
	}

	[Fact]
	public void Overload_CantOverload_DoesNothing()
	{
		var src = new MockEntity(1, 1);
		var player = new TestPlayer("Test", 2);
		player.Overloaded = 0;
		player.CantOverload = true;

		var overloadAction = new OverloadAction(player, 5);
		overloadAction.Trigger(src);

		Assert.Equal(0, player.Overloaded);
	}

	// ─── UnlockOverloadAction ──────────

	[Fact]
	public void UnlockOverload_ClearsOverload()
	{
		var src = new MockEntity(1, 1);
		var player = new TestPlayer("Test", 2);
		player.Overloaded = 5;
		player.OverloadLocked = 3;

		var unlock = new UnlockOverloadAction(player);
		unlock.Trigger(src);

		Assert.Equal(0, player.Overloaded);
		Assert.Equal(0, player.OverloadLocked);
	}

	// ─── FatigueAction ──────────

	[Fact]
	public void Fatigue_IncrementsFatigueCounter()
	{
		var src = new MockEntity(1, 1);
		var player = new TestPlayer("Test", 2);
		player.FatigueCounter = 0;

		var fatigue = new FatigueAction(player);
		fatigue.Trigger(src);

		Assert.Equal(1, player.FatigueCounter);
	}

	[Fact]
	public void Fatigue_CantFatigue_NoCounterChange()
	{
		var src = new MockEntity(1, 1);
		var player = new TestPlayer("Test", 2);
		player.FatigueCounter = 0;
		player.CantFatigue = true;

		var fatigue = new FatigueAction(player);
		fatigue.Trigger(src);

		Assert.Equal(0, player.FatigueCounter);
	}

	// ─── ForceDrawAction ──────────

	[Fact]
	public void ForceDraw_SetsZoneToHand()
	{
		var src = new MockEntity(1, 1);
		var card = new MockEntity(2, 1);
		// Set initial zone via the C# property (through SetZone)
		// MockEntity Zone defaults to Zone.Invalid, which is fine for testing

		var forceDraw = new ForceDrawAction(card);
		forceDraw.Trigger(src);

		// SetZone sets the Zone property, not the Tag
		Assert.Equal(Zone.Hand, card.Zone);
	}

	// ─── RefreshHeroPowerAction ──────────

	[Fact]
	public void RefreshHeroPower_IncreasesAdditionalActivations()
	{
		var src = new MockEntity(1, 1);
		var hp = new TestHeroPower(10);
		hp.ActivationsThisTurn = 1; // exhausted (>= MaxActivations=1)

		var refresh = new RefreshHeroPowerAction(hp);
		refresh.Trigger(src);

		Assert.Equal(1, hp.AdditionalActivationsThisTurn);
	}

	[Fact]
	public void RefreshHeroPower_NotExhausted_NoChange()
	{
		var src = new MockEntity(1, 1);
		var hp = new TestHeroPower(10);
		hp.ActivationsThisTurn = 0; // not exhausted

		var refresh = new RefreshHeroPowerAction(hp);
		refresh.Trigger(src);

		Assert.Equal(0, hp.AdditionalActivationsThisTurn);
	}

	// ─── DeathrattleAction ──────────

	[Fact]
	public void Deathrattle_SkipsNonDeathrattle()
	{
		var src = new MockEntity(1, 1);
		var target = new MockEntity(2, 2);

		var dr = new DeathrattleAction(target);
		var results = dr.Trigger(src);

		Assert.Single(results);
		Assert.Null(results[0]);
	}

	// ══════════════════════════════════════�?
	// P1 �?战斗 / 变身 / Buff 扩展 Actions
	// ══════════════════════════════════════�?

	// ─── ExtraAttack ──────────

	[Fact]
	public void ExtraAttack_DecrementsAttackCount()
	{
		var src = new MockEntity(1, 1);
		var ch = new MockCharacter(2, 2);
		ch.NumAttacks = 1;

		new ExtraAttack(ch).Trigger(src);
		Assert.Equal(0, ch.NumAttacks);
	}

	[Fact]
	public void ExtraAttack_DoesNotGoBelowZero()
	{
		var src = new MockEntity(1, 1);
		var ch = new MockCharacter(2, 2);
		ch.NumAttacks = 0;

		new ExtraAttack(ch).Trigger(src);
		Assert.Equal(0, ch.NumAttacks);
	}

	// ─── LosesDivineShield ──────────

	[Fact]
	public void LosesDivineShield_ClearsDivineShield()
	{
		var src = new MockEntity(1, 1);
		var ch = new MockCharacter(2, 2);
		ch.DivineShield = true;

		new LosesDivineShield(ch).Trigger(src);
		Assert.False(ch.DivineShield);
	}

	// ─── Disconnect ──────────

	[Fact]
	public void Disconnect_SetsPlayState()
	{
		var src = new MockEntity(1, 1);
		var player = new TestPlayer("Test", 2);

		new Disconnect(player).Trigger(src);
		Assert.Equal(PlayState.Disconnected, player.PlayState);
	}

	// ─── SetTags ──────────

	[Fact]
	public void SetTags_UpdatesTags()
	{
		var src = new MockEntity(1, 1);
		var target = new MockEntity(2, 2);

		var tagsToSet = new Dictionary<GameTag, int>
		{
			{ GameTag.Taunt, 1 },
			{ GameTag.DivineShield, 1 }
		};

		new SetTags(target, tagsToSet).Trigger(src);

		Assert.Equal(1, target.Tags![GameTag.Taunt]);
		Assert.Equal(1, target.Tags[GameTag.DivineShield]);
	}

	// ─── GetTag ──────────

	[Fact]
	public void GetTag_ReturnsTagValue()
	{
		var src = new MockEntity(1, 1);
		var target = new MockEntity(2, 2);
		target.Tags![GameTag.Taunt] = 1;

		var results = new GetTag(target, GameTag.Taunt).Trigger(src);
		Assert.Single(results);
		Assert.Equal(1, results[0]);
	}

	[Fact]
	public void GetTag_MissingTag_ReturnsZero()
	{
		var src = new MockEntity(1, 1);
		var target = new MockEntity(2, 2);

		var results = new GetTag(target, GameTag.Taunt).Trigger(src);
		Assert.Single(results);
		Assert.Equal(0, results[0]);
	}

	// ─── FillManaAction ──────────

	[Fact]
	public void FillMana_ReducesUsedMana()
	{
		var src = new MockEntity(1, 1);
		var player = new TestPlayer("Test", 2);
		player.MaxMana = 10;
		player.UsedMana = 5;

		new FillManaAction(player, 3).Trigger(src);
		Assert.Equal(2, player.UsedMana);
	}

	[Fact]
	public void FillMana_DoesNotGoBelowZero()
	{
		var src = new MockEntity(1, 1);
		var player = new TestPlayer("Test", 2);
		player.MaxMana = 10;
		player.UsedMana = 2;

		new FillManaAction(player, 10).Trigger(src);
		Assert.Equal(0, player.UsedMana);
	}

	// ─── Evolve ──────────

	[Fact]
	public void Evolve_DoesNotCrash()
	{
		var (game, gm) = CreateGameWithEntities();
		var src = new MockEntity(1, 1);
		gm.NewEntity(src);

		var target = new MockEntity(2, 2);
		target.Cost = 3;
		gm.NewEntity(target);

		// Evolve is a stub �?should not throw
		new Evolve(target, 1).Trigger(src);
	}

	private (Game game, GameManager gm) CreateGameWithEntities()
	{
		var game = new Game();
		var gm = new GameManager(game);
		return (game, gm);
	}
}
