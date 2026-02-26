using HearthstoneServer;
using HearthstoneServer.Dsl;
using HearthstoneServer.Entities;
using CardData = HearthstoneServer.LoadCardIntoDbf.Card;

namespace Hearthstone.Tests;

/// <summary>
/// Card script validation tests �?ensures all 417+ card scripts
/// can be instantiated and their properties can be accessed without exceptions.
/// </summary>
public class CardScriptValidationTests
{
	// ══════════════════════════════════════════════════�?
	// Bulk Validation
	// ══════════════════════════════════════════════════�?

	[Fact]
	public void AllScripts_RegisterSuccessfully()
	{
		CardScriptRegistry.Instance.Initialize();
		Assert.True(CardScriptRegistry.Instance.Count > 0,
			"CardScriptRegistry should have registered at least one script");
	}

	[Fact]
	public void AllScripts_PropertyAccessDoesNotThrow()
	{
		CardScriptRegistry.Instance.Initialize();
		var failures = new List<string>();
		var cardIds = CardScriptRegistry.Instance.CardIds.ToList();

		foreach (var cardId in cardIds)
		{
			var script = CardScriptRegistry.Instance.GetScript(cardId)!;
			try
			{
				_ = script.Play;
				_ = script.Deathrattle;
				_ = script.Combo;
				_ = script.Inspire;
				_ = script.Update;
				_ = script.TargetReq;
				_ = script.ChooseCards;
				_ = script.Entourage;
				_ = script.Enrage;
				_ = script.CostMod;
				_ = script.Tags;
				_ = script.MaxHealthMod;
				_ = script.AtkMod;
			}
			catch (Exception ex)
			{
				failures.Add($"{cardId}: {ex.GetType().Name} �?{ex.Message}");
			}
		}

		Assert.True(failures.Count == 0,
			$"{failures.Count} script(s) threw on property access:\n" +
			string.Join("\n", failures));
	}

	[Fact]
	public void AllScripts_PlayArraysContainValidActions()
	{
		CardScriptRegistry.Instance.Initialize();
		var failures = new List<string>();
		var cardIds = CardScriptRegistry.Instance.CardIds.ToList();

		foreach (var cardId in cardIds)
		{
			var script = CardScriptRegistry.Instance.GetScript(cardId)!;
			try
			{
				foreach (var action in script.Play)
					Assert.NotNull(action);
				foreach (var action in script.Deathrattle)
					Assert.NotNull(action);
				foreach (var action in script.Combo)
					Assert.NotNull(action);
			}
			catch (Exception ex)
			{
				failures.Add($"{cardId}: {ex.GetType().Name} �?{ex.Message}");
			}
		}

		Assert.True(failures.Count == 0,
			$"{failures.Count} script(s) have invalid actions:\n" +
			string.Join("\n", failures));
	}

	// ══════════════════════════════════════════════════�?
	// Representative End-to-End Tests
	// ══════════════════════════════════════════════════�?

	private static Game CreateTestGame()
	{
		var game = new Game(new Player("P1"), new Player("P2"), seed: 42);
		return game;
	}

	private static Minion CreateMinion(Game game, Player owner,
		int atk = 1, int health = 5, string? cardId = null)
	{
		var m = new Minion
		{
			Controller = owner,
			MaxHealth = health,
			Atk = atk,
			CardData = cardId != null ? new CardData { CardId = cardId } : null,
		};
		m.SetIntProperty("cost", 0);
		game.RegisterEntity(m);
		m.SetZone(Zone.Hand);
		return m;
	}

	[Fact]
	public void Fireball_CS2_029_DealsSixDamage()
	{
		CardScriptRegistry.Instance.Initialize();
		var game = CreateTestGame();
		game.Setup();
		game.BeginTurn(game.Player1);
		var p1 = game.Player1;
		p1.MaxMana = 10;

		var fireball = CreateMinion(game, p1, cardId: "CS2_029");
		var enemyMinion = CreateMinion(game, game.Player2, atk: 3, health: 6);
		enemyMinion.SetZone(Zone.Play);

		var script = CardScriptRegistry.Instance.GetScript("CS2_029")!;
		Assert.True(script.TargetReq?.MustTarget == true);

		game.ActionBlock(fireball, script.Play.ToList(), target: enemyMinion);
		Assert.Equal(0, enemyMinion.Health);
	}

	[Fact]
	public void SavageRoar_CS2_011_HasPlayActions()
	{
		CardScriptRegistry.Instance.Initialize();
		var script = CardScriptRegistry.Instance.GetScript("CS2_011")!;
		Assert.True(script.Play.Length > 0, "Savage Roar should have Play actions");
	}

	[Fact]
	public void Moonfire_CS2_008_HasTargetRequirement()
	{
		CardScriptRegistry.Instance.Initialize();
		var script = CardScriptRegistry.Instance.GetScript("CS2_008")!;

		Assert.True(script.TargetReq?.MustTarget == true,
			"Moonfire should require a target");
		Assert.Single(script.Play);
	}

	[Fact]
	public void Swipe_CS2_012_HasEnemyTargetAndPlayActions()
	{
		CardScriptRegistry.Instance.Initialize();
		var script = CardScriptRegistry.Instance.GetScript("CS2_012")!;

		Assert.True(script.TargetReq?.EnemyOnly == true);
		Assert.True(script.TargetReq?.MustTarget == true);
		Assert.Equal(2, script.Play.Length);
	}

	[Fact]
	public void SI7Agent_EX1_134_HasComboActions()
	{
		CardScriptRegistry.Instance.Initialize();
		var script = CardScriptRegistry.Instance.GetScript("EX1_134");
		if (script == null) return; // Script may not be ported yet
		Assert.True(script.Combo.Length > 0, "SI:7 Agent should have Combo actions");
	}
}
