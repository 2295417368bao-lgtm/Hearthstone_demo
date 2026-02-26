using Xunit;
using HearthstoneServer;
using HearthstoneServer.Entities;
using HearthstoneServer.Dsl;
using HearthstoneServer.Dsl.Cards;
using HearthstoneServer.Cards;
using HearthstoneServer.LoadCardIntoDbf;
using static HearthstoneServer.Dsl.S;

namespace Hearthstone.Tests;

public class Phase12Tests
{
	private class TestMinion : Minion
	{
		public TestMinion(int entityId = 0)
		{
			EntityId = entityId;
			Data = new Card { CardId = $"TEST_{entityId}" };
		}
	}
	// ══════════════════════════════════════�?
	// CardList<T> tests
	// ══════════════════════════════════════�?

	private class FakeItem
	{
		public string Name { get; set; } = "";
		public int Value { get; set; }
	}

	[Fact]
	public void CardList_ContainsUsesReferenceEquality()
	{
		var a = new FakeItem { Name = "A" };
		var b = new FakeItem { Name = "A" }; // same name, different instance
		var list = new CardList<FakeItem> { a };

		Assert.True(list.Contains(a));
		Assert.False(list.Contains(b)); // not the same reference
	}

	[Fact]
	public void CardList_ContainsByValue_MatchesByEquality()
	{
		var a = new FakeItem { Name = "A" };
		var list = new CardList<FakeItem> { a };

		Assert.True(list.ContainsByValue(a));
	}

	[Fact]
	public void CardList_IndexOf_UsesReferenceEquality()
	{
		var a = new FakeItem { Name = "A" };
		var b = new FakeItem { Name = "B" };
		var c = new FakeItem { Name = "A" }; // same name as a
		var list = new CardList<FakeItem> { a, b };

		Assert.Equal(0, list.IndexOf(a));
		Assert.Equal(1, list.IndexOf(b));
		Assert.Equal(-1, list.IndexOf(c)); // different reference
	}

	[Fact]
	public void CardList_Remove_UsesReferenceEquality()
	{
		var a = new FakeItem { Name = "A" };
		var b = new FakeItem { Name = "A" }; // same Name, different ref
		var list = new CardList<FakeItem> { a };

		Assert.False(list.Remove(b)); // b is not the same reference
		Assert.Single(list);          // a still in list
		Assert.True(list.Remove(a));  // remove by reference
		Assert.Empty(list);
	}

	[Fact]
	public void CardList_Exclude_RemovesByReference()
	{
		var a = new FakeItem { Name = "A" };
		var b = new FakeItem { Name = "B" };
		var c = new FakeItem { Name = "C" };
		var list = new CardList<FakeItem> { a, b, c };

		var result = list.Exclude(b);
		Assert.Equal(2, result.Count);
		Assert.True(result.Contains(a));
		Assert.False(result.Contains(b));
		Assert.True(result.Contains(c));
	}

	[Fact]
	public void CardList_Filter_ByProperty()
	{
		var list = new CardList<FakeItem>
		{
			new FakeItem { Name = "X", Value = 1 },
			new FakeItem { Name = "Y", Value = 2 },
			new FakeItem { Name = "Z", Value = 1 },
		};

		var ones = list.Filter("Value", 1);
		Assert.Equal(2, ones.Count);
		Assert.Equal("X", ones[0].Name);
		Assert.Equal("Z", ones[1].Name);
	}

	[Fact]
	public void CardList_ExcludeByProperty()
	{
		var list = new CardList<FakeItem>
		{
			new FakeItem { Name = "X", Value = 1 },
			new FakeItem { Name = "Y", Value = 2 },
			new FakeItem { Name = "Z", Value = 1 },
		};

		var notOnes = list.ExcludeByProperty("Value", 1);
		Assert.Single(notOnes);
		Assert.Equal("Y", notOnes[0].Name);
	}

	[Fact]
	public void CardList_Where_FiltersPredicate()
	{
		var list = new CardList<FakeItem>
		{
			new FakeItem { Name = "X", Value = 5 },
			new FakeItem { Name = "Y", Value = 10 },
		};

		var big = list.Where(i => i.Value > 7);
		Assert.Single(big);
		Assert.Equal("Y", big[0].Name);
	}

	// ══════════════════════════════════════�?
	// Deck tests
	// ══════════════════════════════════════�?

	[Fact]
	public void Deck_DefaultMaxCards_Is30()
	{
		var deck = new Deck();
		Assert.Equal(30, Deck.DEFAULT_MAX_CARDS);
		Assert.Equal(30, deck.MaxCards);
	}

	[Fact]
	public void Deck_MaxCards_CanBeOverridden()
	{
		var deck = new Deck { MaxCards = 40 };
		Assert.Equal(40, deck.MaxCards);
	}

	[Fact]
	public void Deck_Constants()
	{
		Assert.Equal(2, Deck.MAX_UNIQUE_CARDS);
		Assert.Equal(1, Deck.MAX_UNIQUE_LEGENDARIES);
	}

	[Fact]
	public void Deck_HeroProperty()
	{
		var deck = new Deck();
		Assert.Null(deck.Hero);
		deck.Hero = new TestMinion(1);
		Assert.NotNull(deck.Hero);
	}

	[Fact]
	public void Deck_ToString()
	{
		var deck = new Deck();
		Assert.Equal("<Deck (0 cards)>", deck.ToString());
	}

	// ══════════════════════════════════════�?
	// CardScriptUtils tests
	// ══════════════════════════════════════�?

	[Fact]
	public void Freeze_ReturnsSetTagsWithFrozenTag()
	{
		var action = CardScriptUtils.Freeze(SELF);
		Assert.IsType<SetTags>(action);
	}

	[Fact]
	public void Taunt_ReturnsSetTags()
	{
		var action = CardScriptUtils.Taunt(SELF);
		Assert.IsType<SetTags>(action);
	}

	[Fact]
	public void Unstealth_ReturnsUnsetTags()
	{
		var action = CardScriptUtils.Unstealth(SELF);
		Assert.IsType<UnsetTags>(action);
	}

	[Fact]
	public void ATK_ReturnsAttr()
	{
		var attr = CardScriptUtils.ATK(SELF);
		Assert.IsType<Attr>(attr);
	}

	[Fact]
	public void BASIC_HERO_POWERS_HasTenEntries()
	{
		Assert.Equal(10, CardScriptUtils.BASIC_HERO_POWERS.Length);
	}

	[Fact]
	public void BASIC_TOTEMS_HasFourEntries()
	{
		Assert.Equal(4, CardScriptUtils.BASIC_TOTEMS.Length);
	}

	[Fact]
	public void LACKEY_CARDS_HasSevenEntries()
	{
		Assert.Equal(7, CardScriptUtils.LACKEY_CARDS.Length);
	}

	[Fact]
	public void THE_COIN_IsCorrectId()
	{
		Assert.Equal("GAME_005", CardScriptUtils.THE_COIN);
	}

	[Fact]
	public void MakeBuff_CreatesCorrectTags()
	{
		var buff = CardScriptUtils.MakeBuff(atk: 3, health: 2);
		Assert.Equal(3, buff[GameTag.Atk]);
		Assert.Equal(2, buff[GameTag.Health]);
	}

	[Fact]
	public void MakeBuff_WithExtraTags()
	{
		var buff = CardScriptUtils.MakeBuff(atk: 1, extraTags: new[] { (GameTag.Taunt, 1) });
		Assert.Equal(1, buff[GameTag.Atk]);
		Assert.Equal(1, buff[GameTag.Taunt]);
		Assert.False(buff.ContainsKey(GameTag.Health)); // health=0 not added
	}

	[Fact]
	public void ImmuneBuff_SetsCorrectTags()
	{
		var buff = CardScriptUtils.ImmuneBuff();
		Assert.Equal(1, buff[GameTag.CantBeDamaged]);
		Assert.Equal(1, buff[GameTag.CantBeTargetedByOpponents]);
	}

	[Fact]
	public void SetTo_ReturnsConstant()
	{
		var fn = CardScriptUtils.SetTo(5);
		Assert.Equal(5, fn(null!, 0));
		Assert.Equal(5, fn(null!, 999));
	}

	[Fact]
	public void GainEmptyMana_ReturnsTwoActions()
	{
		var actions = CardScriptUtils.GainEmptyMana(CONTROLLER, 1);
		Assert.Equal(2, actions.Length);
		Assert.IsType<GainMana>(actions[0]);
		Assert.IsType<SpendManaAction>(actions[1]);
	}

	[Fact]
	public void CastWhenDrawn_ReturnsThreeActions()
	{
		var actions = CardScriptUtils.CastWhenDrawn();
		Assert.Equal(3, actions.Length);
		Assert.IsType<Destroy>(actions[0]);
		Assert.IsType<DrawCard>(actions[1]);
		Assert.IsType<BattlecryAction>(actions[2]);
	}

	// ══════════════════════════════════════�?
	// ComparisonEvaluator tests
	// ══════════════════════════════════════�?

	[Fact]
	public void ComparisonEvaluator_ConstantEquality()
	{
		var game = new Game(new Player("P1"), new Player("P2"));
		game.Setup();
		var entity = new TestMinion(game.Player1.EntityId);
		game.RegisterEntity(entity);

		var eval = new ComparisonEvaluator(new IntLazyNum(5), 5);
		Assert.True(eval.Check(entity));

		var eval2 = new ComparisonEvaluator(new IntLazyNum(5), 3);
		Assert.False(eval2.Check(entity));
	}

	// ══════════════════════════════════════�?
	// GameRules tests
	// ══════════════════════════════════════�?

	[Fact]
	public void GameRules_CoinRegisteredViaCardPack()
	{
		// The Coin (GAME_005) is registered by GameRulesCards CardPack
		var registry = new CardScriptRegistry();
		var builder = new CardBuilder(registry);
		CardPack.RegisterAll(builder);
		Assert.True(registry.HasScript("GAME_005"));
	}

	// ══════════════════════════════════════�?
	// GameUtils tests
	// ══════════════════════════════════════�?

	[Fact]
	public void RandomClass_ReturnsValidClass()
	{
		var rng = new Random(42);
		var cls = GameUtils.RandomClass(rng);
		Assert.True((int)cls >= 2 && (int)cls <= 10);
	}

	[Fact]
	public void SetupGame_ReturnsGameWithTwoPlayers()
	{
		var game = GameUtils.SetupGame(rng: new Random(42));
		Assert.NotNull(game);
		Assert.NotNull(game.Player1);
		Assert.NotNull(game.Player2);
	}
}
