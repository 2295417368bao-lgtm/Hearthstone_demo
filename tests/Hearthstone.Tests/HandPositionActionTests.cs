using HearthstoneServer;
using HearthstoneServer.Dsl;
using HearthstoneServer.Entities;
using HearthstoneServer.Managers;
using Xunit;

namespace Hearthstone.Tests;

public class HandPositionActionTests
{
	private class MockGame : Game
	{
		public MockGame()
		{
			CardDb = new Dictionary<string, HearthstoneServer.LoadCardIntoDbf.Card>();
		}
	}

	private readonly Player _player;
	private readonly Game _game;

	public HandPositionActionTests()
	{
		_game = new MockGame();

		// Add mock card data for "TEST_CARD"
		_game.CardDb!["TEST_CARD"] = new HearthstoneServer.LoadCardIntoDbf.Card 
		{ 
			CardId = "TEST_CARD", 
			CardType = CardType.Minion, 
			Cost = 3, 
			Attack = 2, 
			Health = 3 
		};

		_player = new Player("P1") { EntityId = 100 };
		_game.RegisterEntity(_player);
		_player.Game = _game;
	}

	[Fact]
	public void CopyToHand_DefaultNegativeOne_AppendsToRightmost()
	{
		var handCard1 = _player.CreateCard("TEST_CARD", Zone.Hand);
		var handCard2 = _player.CreateCard("TEST_CARD", Zone.Hand);

		// Source card to copy
		var sourceCard = _player.CreateCard("TEST_CARD", Zone.Play);
		Assert.NotNull(sourceCard);
		Assert.Equal("TEST_CARD", sourceCard.CardId);

		var targets = S.SELF.Eval(new List<BaseEntity>(), sourceCard);
		Assert.Single(targets);
		Assert.Same(sourceCard, targets[0]);

		var action = new CopyToHand(S.CONTROLLER, S.SELF, keepBuffs: false, handIndex: -1);
		
		// Capture Do output manually if Trigger fails to pass args
		action.Do(sourceCard, _player, targets);

		Assert.Equal(3, _player.Hand.Count);
		Assert.NotSame(sourceCard, _player.Hand[^1]); // new card
		Assert.Equal("TEST_CARD", (_player.Hand[^1] as BuffableEntity)?.CardId);
	}

	[Fact]
	public void CopyToHand_IndexZero_InsertsAtLeftmost()
	{
		var handCard1 = _player.CreateCard("TEST_CARD", Zone.Hand);
		var handCard2 = _player.CreateCard("TEST_CARD", Zone.Hand);

		var sourceCard = _player.CreateCard("TEST_CARD", Zone.Play);
		
		var targets = S.SELF.Eval(new List<BaseEntity>(), sourceCard);
		var action = new CopyToHand(S.CONTROLLER, S.SELF, keepBuffs: false, handIndex: 0);
		action.Do(sourceCard, _player, targets);

		Assert.Equal(3, _player.Hand.Count);
		Assert.NotSame(sourceCard, _player.Hand[0]); // new card inserted at leftmost
		Assert.Equal("TEST_CARD", (_player.Hand[0] as BuffableEntity)?.CardId);
		
		// handCard1 should now be at index 1
		Assert.Same(handCard1, _player.Hand[1]);
	}

	[Fact]
	public void CopyToHand_KeepBuffsTrue_CopiesCurrentStats()
	{
		var sourceCard = _player.CreateCard("TEST_CARD", Zone.Play);
		if (sourceCard != null)
		{
			sourceCard.SetIntProperty("atk", 9);
			sourceCard.SetIntProperty("health", 9);
			sourceCard.SetIntProperty("cost", 0);
		}

		var targets = S.SELF.Eval(new List<BaseEntity>(), sourceCard);
		var action = new CopyToHand(S.CONTROLLER, S.SELF, keepBuffs: true, handIndex: 0);
		action.Do(sourceCard, _player, targets);

		Assert.Single(_player.Hand);
		var copy = _player.Hand[0] as BuffableEntity;
		Assert.NotNull(copy);
		Assert.Equal(9, copy.GetIntProperty("atk"));
		Assert.Equal(9, copy.GetIntProperty("health"));
		Assert.Equal(0, copy.GetIntProperty("cost"));
	}
}
