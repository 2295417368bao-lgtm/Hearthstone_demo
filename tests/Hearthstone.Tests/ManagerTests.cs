using HearthstoneServer;
using HearthstoneServer.Entities;
using HearthstoneServer.Managers;

namespace Hearthstone.Tests;

/// <summary>
/// Manager 系统测试�?
/// 覆盖：索引器、Tag→属性映射、批量更新、Observer 通知�?
/// </summary>
public class ManagerTests
{
	/// <summary>
	/// 辅助实体：暴�?Tags �?setter（BaseEntity.Tags �?protected set）�?
	/// </summary>
	private class TestableEntity : Entity
	{
		public void SetManager(Manager manager) => Tags = manager;
	}

	// ── GameManager ──

	[Fact]
	public void GameManager_AssignsEntityId_OnConstruction()
	{
		var game = new Entity();
		var gm = new GameManager(game);
		Assert.Equal(1, game.EntityId);
	}

	[Fact]
	public void NewEntity_IncrementsCounter()
	{
		var game = new Entity();
		var gm = new GameManager(game);

		var e1 = new Entity();
		gm.NewEntity(e1);
		Assert.Equal(2, e1.EntityId);

		var e2 = new Entity();
		gm.NewEntity(e2);
		Assert.Equal(3, e2.EntityId);
	}

	[Fact]
	public void NewEntity_NotifiesObservers()
	{
		var game = new Entity();
		var gm = new GameManager(game);
		var spy = new SpyObserver();
		gm.Register(spy);

		var e = new Entity();
		gm.NewEntity(e);

		Assert.Equal(1, spy.NewEntityCount);
		Assert.Same(e, spy.LastNewEntity);
	}

	[Fact]
	public void SetStep_NotifiesObservers()
	{
		var game = new TestableEntity();
		var gm = new GameManager(game);
		game.SetManager(gm);
		var spy = new SpyObserver();
		gm.Register(spy);

		gm.SetStep(Step.BeginMulligan);

		Assert.Equal(1, spy.GameStepCount);
	}

	[Fact]
	public void ActionStart_NotifiesObservers()
	{
		var game = new Entity();
		var gm = new GameManager(game);
		var spy = new SpyObserver();
		gm.Register(spy);

		gm.ActionStart(BlockType.Attack, game, 0, null);

		Assert.Equal(1, spy.ActionStartCount);
	}

	// ── Manager 索引�?──

	[Fact]
	public void CardManager_Indexer_GetSet()
	{
		var entity = new TestableEntity();
		var cm = new CardManager(entity);
		entity.SetManager(cm);

		cm[GameTag.Atk] = 5;
		Assert.Equal(5, cm[GameTag.Atk]);
	}

	[Fact]
	public void CardManager_Indexer_ReturnsZero_ForUnmappedTag()
	{
		var entity = new TestableEntity();
		var cm = new CardManager(entity);
		entity.SetManager(cm);

		Assert.Equal(0, cm[GameTag.TagNotSet]);
	}

	[Fact]
	public void PlayerManager_Indexer_GetSet()
	{
		var entity = new TestableEntity();
		var pm = new PlayerManager(entity);
		entity.SetManager(pm);

		pm[GameTag.Resources] = 10;
		Assert.Equal(10, pm[GameTag.Resources]);
	}

	// ── Manager.Get with default ──

	[Fact]
	public void Manager_Get_ReturnsDefault_ForUnmappedTag()
	{
		var entity = new TestableEntity();
		var cm = new CardManager(entity);
		entity.SetManager(cm);

		Assert.Equal(42, cm.Get(GameTag.TagNotSet, 42));
	}

	[Fact]
	public void Manager_Get_ReturnsValue_ForMappedTag()
	{
		var entity = new TestableEntity();
		var cm = new CardManager(entity);
		entity.SetManager(cm);
		cm[GameTag.Atk] = 7;

		Assert.Equal(7, cm.Get(GameTag.Atk, 0));
	}

	// ── Manager.Contains ──

	[Fact]
	public void Manager_Contains_TrueForMappedTag()
	{
		var entity = new TestableEntity();
		var cm = new CardManager(entity);
		entity.SetManager(cm);

		Assert.True(cm.Contains(GameTag.Atk));
		Assert.True(cm.Contains(GameTag.Health));
	}

	// ── Manager.Update (批量更新) ──

	[Fact]
	public void Manager_Update_SetsMultipleTags()
	{
		var entity = new TestableEntity();
		var cm = new CardManager(entity);
		entity.SetManager(cm);

		cm.Update(new Dictionary<GameTag, int>
		{
			[GameTag.Atk] = 4,
			[GameTag.Health] = 6,
			[GameTag.Cost] = 3,
		});

		Assert.Equal(4, cm[GameTag.Atk]);
		Assert.Equal(6, cm[GameTag.Health]);
		Assert.Equal(3, cm[GameTag.Cost]);
	}

	[Fact]
	public void Manager_Update_IgnoresUnmappedTags()
	{
		var entity = new TestableEntity();
		var cm = new CardManager(entity);
		entity.SetManager(cm);

		cm.Update(new Dictionary<GameTag, int>
		{
			[GameTag.Atk] = 4,
			[GameTag.TagNotSet] = 99,
		});

		Assert.Equal(4, cm[GameTag.Atk]);
		Assert.Equal(0, cm[GameTag.TagNotSet]);
	}

	// ── Manager.Items ──

	[Fact]
	public void Manager_Items_ReturnsAllMappedTagValues()
	{
		var entity = new TestableEntity();
		var cm = new CardManager(entity);
		entity.SetManager(cm);
		cm[GameTag.Atk] = 3;
		cm[GameTag.Health] = 5;

		var items = cm.Items().ToList();
		Assert.Contains(items, x => x.Tag == GameTag.Atk && x.Value == 3);
		Assert.Contains(items, x => x.Tag == GameTag.Health && x.Value == 5);
	}

	// ── Observer Register/Unregister ──

	[Fact]
	public void Observer_Unregister_StopsNotifications()
	{
		var game = new Entity();
		var gm = new GameManager(game);
		var spy = new SpyObserver();
		gm.Register(spy);

		gm.NewEntity(new Entity());
		Assert.Equal(1, spy.NewEntityCount);

		gm.Unregister(spy);
		gm.NewEntity(new Entity());
		Assert.Equal(1, spy.NewEntityCount); // 不再递增
	}

	// ── CardManager.GetPropertyName ──

	[Fact]
	public void CardManager_GetPropertyName_ReturnsName()
	{
		Assert.Equal("atk", CardManager.GetPropertyName(GameTag.Atk));
		Assert.Equal("health", CardManager.GetPropertyName(GameTag.Health));
		Assert.Equal("taunt", CardManager.GetPropertyName(GameTag.Taunt));
		Assert.Equal("cost", CardManager.GetPropertyName(GameTag.Cost));
		Assert.Equal("stealth", CardManager.GetPropertyName(GameTag.Stealth));
	}

	[Fact]
	public void CardManager_GetPropertyName_ReturnsNull_ForUnmapped()
	{
		Assert.Null(CardManager.GetPropertyName(GameTag.TagNotSet));
	}

	// ── BuffableEntity.GetBoolProperty(GameTag) 重载 ──

	[Fact]
	public void GetBoolProperty_ByGameTag_UsesCardManagerMapping()
	{
		var entity = new Entity();
		entity.SetBoolProperty("taunt", true);

		Assert.True(entity.GetBoolProperty(GameTag.Taunt));
		Assert.False(entity.GetBoolProperty(GameTag.Stealth));
	}

	// ── Helper: SpyObserver ──

	private class SpyObserver : IObserver
	{
		public int NewEntityCount { get; private set; }
		public int GameStepCount { get; private set; }
		public int ActionStartCount { get; private set; }
		public BaseEntity? LastNewEntity { get; private set; }

		public void NewEntity(BaseEntity entity)
		{
			NewEntityCount++;
			LastNewEntity = entity;
		}

		public void GameStep(Step step, Step? nextStep)
		{
			GameStepCount++;
		}

		public void ActionStart(BlockType type, BaseEntity source, int index, BaseEntity? target)
		{
			ActionStartCount++;
		}
	}
}
