using HearthstoneServer.Dsl;
using HearthstoneServer.Entities;
using HearthstoneServer.Managers;
using HearthstoneServer;
using Xunit;

namespace Hearthstone.Tests;

public class EvaluatorTests
{
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

		public void SetTag(GameTag tag, int value) => Tags[tag] = value;
	}

	private (Game game, GameManager gm) CreateGame()
	{
		var game = new Game();
		var gm = new GameManager(game);
		return (game, gm);
	}

	[Fact]
	public void Find_ReturnsTrue_WhenMatchExists()
	{
		var (game, gm) = CreateGame();
		var src = new MockEntity(1, 1);
		gm.NewEntity(src);

		var m1 = new MockEntity(2, 1);
		gm.NewEntity(m1);

		var find = new Find(S.ALL_MINIONS);
		Assert.True(find.Check(src));
	}

	[Fact]
	public void Find_ReturnsFalse_WhenNoMatch()
	{
		var (game, gm) = CreateGame();
		var src = new MockEntity(1, 1, CardType.Hero);
		gm.NewEntity(src);

		// No minions
		var find = new Find(S.ALL_MINIONS);
		Assert.False(find.Check(src));
	}

	[Fact]
	public void Dead_ReturnsTrue_WhenHealthIsZero()
	{
		var (game, gm) = CreateGame();
		var src = new MockEntity(1, 1);
		gm.NewEntity(src);

		var m1 = new MockEntity(2, 1);
		m1.SetIntProperty("health", 0);
		gm.NewEntity(m1);

		var dead = new Dead(S.ALL_MINIONS);
		Assert.True(dead.Check(src));
	}

	[Fact]
	public void Dead_ReturnsFalse_WhenAllAlive()
	{
		var (game, gm) = CreateGame();
		var src = new MockEntity(1, 1, CardType.Hero);
		gm.NewEntity(src);

		var m1 = new MockEntity(2, 1);
		m1.SetIntProperty("health", 5);
		gm.NewEntity(m1);

		var dead = new Dead(S.ALL_MINIONS);
		Assert.False(dead.Check(src));
	}

	[Fact]
	public void Negation_InvertsResult()
	{
		var (game, gm) = CreateGame();
		var src = new MockEntity(1, 1);
		gm.NewEntity(src);

		var find = new Find(S.ALL_MINIONS);
		Assert.True(find.Check(src));

		var negated = -find;
		// Negated Find should return false when entities exist
		Assert.NotNull(negated);
		Assert.True(negated.Negated);
	}

	[Fact]
	public void ThenElse_ReturnsCorrectAction()
	{
		var (game, gm) = CreateGame();
		var src = new MockEntity(1, 1);
		gm.NewEntity(src);

		var find = new Find(S.ALL_MINIONS);
		var cond = find.Then("do_something").Else("do_other");

		// Condition is true -> returns IfAction
		Assert.Equal("do_something", cond.Evaluate(src));

		// Negate
		var negCond = (-find).Then("do_something").Else("do_other");
		Assert.Equal("do_other", negCond.Evaluate(src));
	}

	[Fact]
	public void EvenCost_Works()
	{
		var (game, gm) = CreateGame();
		var src = new MockEntity(1, 1, CardType.Hero);
		gm.NewEntity(src);

		var m1 = new MockEntity(2, 1);
		m1.SetIntProperty("cost", 4);
		gm.NewEntity(m1);

		var m2 = new MockEntity(3, 1);
		m2.SetIntProperty("cost", 2);
		gm.NewEntity(m2);

		Assert.True(new EvenCost(S.ALL_MINIONS).Check(src));

		// Add odd cost minion  
		var m3 = new MockEntity(4, 1);
		m3.SetIntProperty("cost", 3);
		gm.NewEntity(m3);

		Assert.False(new EvenCost(S.ALL_MINIONS).Check(src));
	}

	[Fact]
	public void LazyNumEvaluator_ComparesCorrectly()
	{
		var src = new MockEntity(1, 1);

		var count5 = new IntLazyNum(5);
		var gt3 = new LazyNumEvaluator(count5, 3, (a, b) => a > b);
		Assert.True(gt3.Check(src));

		var lt3 = new LazyNumEvaluator(count5, 3, (a, b) => a < b);
		Assert.False(lt3.Check(src));

		var eq5 = new LazyNumEvaluator(count5, 5, (a, b) => a == b);
		Assert.True(eq5.Check(src));
	}
}
