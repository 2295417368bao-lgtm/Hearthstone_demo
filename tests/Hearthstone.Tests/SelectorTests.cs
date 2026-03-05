using HearthstoneServer.Dsl;
using HearthstoneServer.Entities;
using HearthstoneServer.LoadCardIntoDbf;
using HearthstoneServer.Managers;
using HearthstoneServer;
using Xunit;

namespace Hearthstone.Tests;

public class SelectorTests
{
	// Shared mock players to act as controllers
	private readonly Player _player1 = new("P1") { EntityId = 100 };
	private readonly Player _player2 = new("P2") { EntityId = 200 };

	private class MockEntity : Minion
	{
		public MockEntity(int id, Player controller)
		{
			EntityId = id;
			Controller = controller;
			Tags = new CardManager(this);
			Tags[GameTag.Controller] = controller.EntityId;
			Zone = Zone.Play;
		}

		public void SetTag(GameTag tag, int value) => Tags[tag] = value;
	}

	[Fact]
	public void EnumSelector_FiltersByCardType()
	{
		var e1 = new MockEntity(1, _player1) { Type = CardType.Minion };
		var e2 = new MockEntity(2, _player1) { Type = CardType.Spell };
		var list = new List<BaseEntity> { e1, e2 };

		var result = S.MINION.Eval(list, e1);
		Assert.Single(result);
		Assert.Equal(e1, result[0]);
	}

	[Fact]
	public void SetOpSelector_Union_Works()
	{
		var e1 = new MockEntity(1, _player1) { Type = CardType.Minion };
		var e2 = new MockEntity(2, _player1) { Type = CardType.Hero };
		var list = new List<BaseEntity> { e1, e2 };

		// MINION | HERO
		var selector = S.MINION | S.HERO;
		var result = selector.Eval(list, e1);

		Assert.Equal(2, result.Count);
	}

	[Fact]
	public void SetOpSelector_Intersection_Works()
	{
		var e1 = new MockEntity(1, _player1) { Type = CardType.Minion };
		e1.SetTag(GameTag.Taunt, 1);
		var e2 = new MockEntity(2, _player1) { Type = CardType.Minion };
		var list = new List<BaseEntity> { e1, e2 };

		// MINION + TAUNT
		var selector = S.MINION + S.TAUNT;
		var result = selector.Eval(list, e1);

		Assert.Single(result);
		Assert.Equal(e1, result[0]);
	}

	[Fact]
	public void SetOpSelector_Difference_Works()
	{
		var e1 = new MockEntity(1, _player1) { Type = CardType.Minion };
		e1.SetTag(GameTag.Taunt, 1);
		var e2 = new MockEntity(2, _player1) { Type = CardType.Minion };
		var list = new List<BaseEntity> { e1, e2 };

		// MINION - TAUNT
		var selector = S.MINION - S.TAUNT;
		var result = selector.Eval(list, e1);

		Assert.Single(result);
		Assert.Equal(e2, result[0]);
	}

	[Fact]
	public void ComparisonSelector_AttrValue_Works()
	{
		var e1 = new MockEntity(1, _player1);
		e1.SetTag(GameTag.Atk, 5);
		var e2 = new MockEntity(2, _player1);
		e2.SetTag(GameTag.Atk, 2);

		var list = new List<BaseEntity> { e1, e2 };

		// ATK > 3
		var selector = new FilterSelector((e, _) => true) + (S.ATK > 3);
		// Note: ComparisonSelector needs to be combined with something or used directly.
		// But ComparisonSelector is a Selector, so it filters the input list.
		var result = (S.ATK > 3).Eval(list, e1);

		Assert.Single(result);
		Assert.Equal(e1, result[0]);
	}

	[Fact]
	public void Constants_Friendly_Enemy_Work()
	{
		var source = new MockEntity(1, _player1);
		var friendly = new MockEntity(2, _player1);
		var enemy = new MockEntity(3, _player2);

		var list = new List<BaseEntity> { source, friendly, enemy };

		var friendlies = S.FRIENDLY.Eval(list, source);
		Assert.Contains(source, friendlies);
		Assert.Contains(friendly, friendlies);
		Assert.DoesNotContain(enemy, friendlies);

		var enemies = S.ENEMY.Eval(list, source);
		Assert.Single(enemies);
		Assert.Equal(enemy, enemies[0]);
	}

	[Fact]
	public void ComplexSelector_FriendlyMinions()
	{
		var source = new MockEntity(1, _player1);
		var m1 = new MockEntity(2, _player1) { Type = CardType.Minion };
		var m2 = new MockEntity(3, _player1) { Type = CardType.Hero };
		var m3 = new MockEntity(4, _player2) { Type = CardType.Minion }; // Enemy

		var list = new List<BaseEntity> { source, m1, m2, m3 };

		// FRIENDLY_MINIONS = ALL_MINIONS + FRIENDLY
		// ALL_MINIONS = IN_PLAY + MINION - DORMANT
		// Assuming MockEntity sets Zone=Play by default

		var result = S.FRIENDLY_MINIONS.Eval(list, source);

		Assert.Equal(2, result.Count); // source (Minion) + m1 (Minion), both friendly
		Assert.Contains(m1, result);
	}

	[Fact]
	public void LeftmostHand_SelectsFirstCard()
	{
		var e1 = new MockEntity(1, _player1);
		var e2 = new MockEntity(2, _player1);
		var e3 = new MockEntity(3, _player1);
		_player1.Hand.Clear();
		_player1.Hand.AddRange(new[] { e1, e2, e3 });

		var result = S.LEFTMOST_HAND.Eval(new List<BaseEntity>(), e1);

		Assert.Single(result);
		Assert.Equal(e1, result[0]);
	}

	[Fact]
	public void RightmostHand_SelectsLastCard()
	{
		var e1 = new MockEntity(1, _player1);
		var e2 = new MockEntity(2, _player1);
		var e3 = new MockEntity(3, _player1);
		_player1.Hand.Clear();
		_player1.Hand.AddRange(new[] { e1, e2, e3 });

		var result = S.RIGHTMOST_HAND.Eval(new List<BaseEntity>(), e1);

		Assert.Single(result);
		Assert.Equal(e3, result[0]);
	}

	[Fact]
	public void SingleCardHand_BothSelectSame()
	{
		var e1 = new MockEntity(1, _player1);
		_player1.Hand.Clear();
		_player1.Hand.Add(e1);

		var leftResult = S.LEFTMOST_HAND.Eval(new List<BaseEntity>(), e1);
		var rightResult = S.RIGHTMOST_HAND.Eval(new List<BaseEntity>(), e1);

		Assert.Single(leftResult);
		Assert.Single(rightResult);
		Assert.Equal(e1, leftResult[0]);
		Assert.Equal(e1, rightResult[0]);
	}
}
