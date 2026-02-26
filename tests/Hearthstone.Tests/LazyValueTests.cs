using HearthstoneServer.Dsl;
using HearthstoneServer.Entities;
using HearthstoneServer.Managers;
using HearthstoneServer;
using Xunit;

namespace Hearthstone.Tests;

public class LazyValueTests
{
	private class MockGame : Game
	{
		public MockGame()
		{
			// Game constructor sets Type=Game
			// We need to attach GameManager to manage entities
			// BaseEntity properties are managed by Manager if needed, 
			// but for simple tests we just need Entities list.

			// Initialize GameManager which sets ID and adds itself?
			// No, GameManager(obj) does not add obj to Entities (it adds NEW entities).
			// But we can add entities manually.
		}
	}

	private static readonly Player _p1 = new("P1") { EntityId = 100 };
	private static readonly Player _p2 = new("P2") { EntityId = 200 };

	private class MockEntity : Minion
	{
		public MockEntity(int id, int controllerId)
		{
			EntityId = id;
			Controller = controllerId == 1 ? _p1 : _p2;
			Tags = new CardManager(this);
			Tags[GameTag.Controller] = controllerId;
			Zone = Zone.Play;
		}

		public void SetTag(GameTag tag, int value) => Tags[tag] = value;
	}

	[Fact]
	public void LazyNum_Arithmetic_Works()
	{
		LazyNum a = 5;
		LazyNum b = 10;

		var src = new MockEntity(1, 1);

		Assert.Equal(15, (a + b).Evaluate(src));
		Assert.Equal(50, (a * b).Evaluate(src));
		Assert.Equal(2, (b / a).Evaluate(src));
		Assert.Equal(15, (a + 10).Evaluate(src));
		Assert.Equal(20, (10 + b).Evaluate(src));
	}

	[Fact]
	public void Count_Selector_Works()
	{
		// Setup Game
		var game = new MockGame();
		var gm = new GameManager(game);
		// Note: GameManager constructor sets game.EntityId. 
		// It does NOT add game to game.Entities logic (that's in NewEntity).

		var src = new MockEntity(1, 1);
		gm.NewEntity(src); // Adds to game.Entities

		var e2 = new MockEntity(2, 1) { Type = CardType.Minion };
		gm.NewEntity(e2);

		var e3 = new MockEntity(3, 2) { Type = CardType.Minion };
		gm.NewEntity(e3);

		// Count(ALL_MINIONS)
		// ALL_MINIONS = IN_PLAY + MINION - DORMANT
		// MockEntity sets Zone=Play by default

		var count = new Count(S.ALL_MINIONS);

		// Eval needs source.Game to be set.
		// NewEntity sets entity.Game = game.

		Assert.Equal(3, count.Evaluate(src)); // src, e2, e3 are all Minions in PLAY
	}

	[Fact]
	public void Attr_Sum_Works()
	{
		var game = new MockGame();
		var gm = new GameManager(game);

		var src = new MockEntity(1, 1);
		gm.NewEntity(src);

		var m1 = new MockEntity(2, 1) { Type = CardType.Minion };
		m1.SetTag(GameTag.Atk, 3);
		gm.NewEntity(m1);

		var m2 = new MockEntity(3, 1) { Type = CardType.Minion };
		m2.SetTag(GameTag.Atk, 4);
		gm.NewEntity(m2);

		// Attr(FRIENDLY_MINIONS, ATK)
		// FRIENDLY_MINIONS = ALL_MINIONS + FRIENDLY
		// FRIENDLY checks controller match

		var attrSum = new Attr(S.FRIENDLY_MINIONS, S.ATK);

		Assert.Equal(7, attrSum.Evaluate(src));
	}

	[Fact]
	public void Max_Works()
	{
		LazyNum n1 = 5;
		LazyNum n2 = 10;
		LazyNum n3 = 3;

		var max = new Max(n1, n2, n3);
		Assert.Equal(10, max.Evaluate(new MockEntity(1, 1)));
	}
}
