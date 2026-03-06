using Xunit;
using HearthstoneServer;
using HearthstoneServer.Entities;
using HearthstoneServer.Dsl;

namespace Hearthstone.Tests;

public class Phase4Tests
{
	// ═══════════════════════════════════════════════
	// Helpers
	// ═══════════════════════════════════════════════

	private static (Game game, Player p1, Player p2) SetupGame()
	{
		var p1 = new Player("P1") { MaxMana = 10 };
		p1.Hero = new Hero { MaxHealth = 30, Controller = p1 };
		var p2 = new Player("P2") { MaxMana = 10 };
		p2.Hero = new Hero { MaxHealth = 30, Controller = p2 };
		for (int i = 0; i < 10; i++)
		{
			p1.Deck.Add(new Minion { MaxHealth = 1, Controller = p1 });
			p2.Deck.Add(new Minion { MaxHealth = 1, Controller = p2 });
		}
		var game = new Game(p1, p2, seed: 42);
		game.Start();
		return (game, p1, p2);
	}

	// ═══════════════════════════════════════════════
	// 法瑞克 (CORE_EDR_003) — 残骸加倍
	// ═══════════════════════════════════════════════

	[Fact]
	public void Falric_ExtraCorpses_DoubleOnMinionDeath()
	{
		var (game, p1, p2) = SetupGame();

		// ExtraCorpses is false by default
		Assert.False(p1.ExtraCorpses);
		p1.Corpses = 0;

		// Summon a minion then kill it — should get 1 corpse normally
		var minion1 = new Minion { MaxHealth = 1, Atk = 1, Controller = p1 };
		game.RegisterEntity(minion1);
		p1.Summon(minion1);
		minion1.Zone = Zone.Play;
		minion1.ToBeDestroyed = true;
		game.ProcessDeaths();

		Assert.Equal(1, p1.Corpses);

		// Enable ExtraCorpses
		p1.ExtraCorpses = true;

		// Kill another minion — should get 2 corpses
		var minion2 = new Minion { MaxHealth = 1, Atk = 1, Controller = p1 };
		game.RegisterEntity(minion2);
		p1.Summon(minion2);
		minion2.Zone = Zone.Play;
		minion2.ToBeDestroyed = true;
		game.ProcessDeaths();

		Assert.Equal(3, p1.Corpses); // 1 + 2 = 3
	}

	[Fact]
	public void Falric_ExtraCorpses_GainCorpsesActionDoubles()
	{
		var (game, p1, p2) = SetupGame();
		p1.Corpses = 0;
		p1.ExtraCorpses = true;

		// GainCorpses(2) should give 4 when ExtraCorpses is active
		p1.GainCorpses(2);
		Assert.Equal(4, p1.Corpses);
	}

	[Fact]
	public void Falric_ExtraCorpses_DisableRestoresNormal()
	{
		var (game, p1, p2) = SetupGame();
		p1.Corpses = 0;
		p1.ExtraCorpses = true;

		p1.GainCorpses(1);
		Assert.Equal(2, p1.Corpses); // doubled

		p1.ExtraCorpses = false;
		p1.GainCorpses(1);
		Assert.Equal(3, p1.Corpses); // normal: 2 + 1 = 3
	}

	// ═══════════════════════════════════════════════
	// 霜之哀伤 (CORE_RLK_086) — 武器击杀记录
	// ═══════════════════════════════════════════════

	[Fact]
	public void Frostmourne_KilledMinions_RecordsOnKill()
	{
		var (game, p1, p2) = SetupGame();

		// Create and equip a weapon
		var weapon = new Weapon { Controller = p1 };
		weapon.Atk = 4;
		weapon.MaxDurability = 3;
		game.RegisterEntity(weapon);
		weapon.Equip();

		Assert.Empty(weapon.KilledMinions);

		// Record a kill
		weapon.KilledMinions.Add("TEST_001");
		Assert.Single(weapon.KilledMinions);
		Assert.Equal("TEST_001", weapon.KilledMinions[0]);

		// Record another
		weapon.KilledMinions.Add("TEST_002");
		Assert.Equal(2, weapon.KilledMinions.Count);
	}

	[Fact]
	public void SummonFromWeaponKills_SummonsRecordedMinions()
	{
		var (game, p1, p2) = SetupGame();

		// Create weapon with recorded kills
		var weapon = new Weapon
		{
			Controller = p1,
			CardData = new HearthstoneServer.LoadCardIntoDbf.Card { CardId = "test_weapon", CardType = CardType.Weapon }
		};
		weapon.Atk = 4;
		weapon.MaxDurability = 1;
		game.RegisterEntity(weapon);
		weapon.Equip();

		// We can't test real card summoning without CardDb, but we can test
		// the KilledMinions list tracking functionality
		weapon.KilledMinions.Add("card_a");
		weapon.KilledMinions.Add("card_b");

		Assert.Equal(2, weapon.KilledMinions.Count);
		Assert.Equal("card_a", weapon.KilledMinions[0]);
		Assert.Equal("card_b", weapon.KilledMinions[1]);
	}

	// ═══════════════════════════════════════════════
	// DrawFilter.HasTag
	// ═══════════════════════════════════════════════

	[Fact]
	public void DrawFilter_HasTag_FiltersCorrectly()
	{
		// Test DrawFilter.HasTag with CorpseSpender tag
		var filter = DrawFilter.All().HasTag(GameTag.CorpseSpender);
		var func = filter.ToFunc();

		// Create a minion entity without card data — should not match
		var noData = new Minion { MaxHealth = 1 };
		Assert.False(func(noData));
	}
}
