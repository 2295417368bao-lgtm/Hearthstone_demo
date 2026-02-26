using HearthstoneServer.Dsl;
using HearthstoneServer.Dsl.Cards;
using HearthstoneServer.Entities;
using HearthstoneServer.Managers;
using HearthstoneServer;
using Xunit;
using Action = HearthstoneServer.Dsl.Action;

namespace Hearthstone.Tests;

public class CardScriptTests
{
	// Helper: get a fresh registry with all CardPack scripts registered
	private static CardScriptRegistry CreateRegistry()
	{
		var registry = new CardScriptRegistry();
		// Register attribute-based scripts (legacy)
		registry.Initialize(typeof(CardPack).Assembly);
		// Register declarative CardPack scripts
		var builder = new CardBuilder(registry);
		CardPack.RegisterAll(builder);
		return registry;
	}

	// ─── Registry tests ──────────

	[Fact]
	public void Registry_Initializes_FindsMageAndNeutralScripts()
	{
		var registry = CreateRegistry();
		Assert.True(registry.Count >= 20, $"Expected >= 20 scripts, got {registry.Count}");
	}

	[Fact]
	public void Registry_HasScript_ReturnsTrueForFireball()
	{
		var registry = CreateRegistry();
		Assert.True(registry.HasScript("CS2_029"));
	}

	[Fact]
	public void Registry_HasScript_ReturnsFalseForUnknownCard()
	{
		var registry = CreateRegistry();
		Assert.False(registry.HasScript("FAKE_999"));
	}

	[Fact]
	public void Registry_GetScript_ReturnsInlineScript()
	{
		var registry = CreateRegistry();
		var script = registry.GetScript("CS2_029");
		Assert.NotNull(script);
		Assert.IsType<InlineCardScript>(script);
	}

	[Fact]
	public void Registry_ManualRegister_Works()
	{
		var registry = new CardScriptRegistry();
		var custom = new InlineCardScript
		{
			Play = new Action[] { new DealDamage(S.TARGET, 6) }
		};
		registry.Register("CUSTOM_001", custom);

		Assert.True(registry.HasScript("CUSTOM_001"));
		Assert.Same(custom, registry.GetScript("CUSTOM_001"));
	}

	[Fact]
	public void Registry_Clear_RemovesAll()
	{
		var registry = CreateRegistry();
		Assert.True(registry.Count > 0);

		registry.Clear();
		Assert.Equal(0, registry.Count);
	}

	// ─── ICardScript interface tests (via registry) ──────────

	[Fact]
	public void Fireball_HasOnePlayAction()
	{
		var registry = CreateRegistry();
		var script = registry.GetScript("CS2_029")!;
		Assert.Single(script.Play);
		Assert.IsType<DealDamage>(script.Play[0]);
	}

	[Fact]
	public void Fireball_HasTargetToPlayRequirement()
	{
		var registry = CreateRegistry();
		var script = registry.GetScript("CS2_029")!;
		Assert.True(script.TargetReq?.MustTarget == true);
	}

	[Fact]
	public void ArcaneIntellect_HasTwoDrawActions()
	{
		var registry = CreateRegistry();
		var script = registry.GetScript("CS2_023")!;
		Assert.Equal(2, script.Play.Length);
		Assert.All(script.Play, a => Assert.IsType<DrawCard>(a));
	}

	[Fact]
	public void Frostbolt_HasHitAndFreeze()
	{
		var registry = CreateRegistry();
		var script = registry.GetScript("CS2_024")!;
		Assert.Equal(2, script.Play.Length);
		Assert.IsType<DealDamage>(script.Play[0]);
	}

	[Fact]
	public void NoviceEngineer_HasDrawAction()
	{
		var registry = CreateRegistry();
		var script = registry.GetScript("EX1_015")!;
		Assert.Single(script.Battlecry);
		Assert.IsType<DrawCard>(script.Battlecry[0]);
	}

	[Fact]
	public void EmptyDefaults_ForUnusedHooks()
	{
		var registry = CreateRegistry();
		var script = registry.GetScript("CS2_029")!; // Fireball
		Assert.Empty(script.Deathrattle);
		Assert.Empty(script.Combo);
		Assert.Empty(script.Inspire);
		Assert.Empty(script.Update);
		Assert.Empty(script.ChooseCards);
		Assert.Empty(script.Entourage);
	}

	// ─── BuffHelper tests ──────────

	[Fact]
	public void BuffHelper_Create_BasicStatBuff()
	{
		var b = BuffHelper.Create(atk: 2, health: 1, id: "test_buff");
		Assert.Equal(2, b.Atk);
		Assert.Equal(1, b.Health);
		Assert.Equal("test_buff", b.Id);
	}

	[Fact]
	public void BuffHelper_Create_ZeroStats_NoTags()
	{
		var b = BuffHelper.Create();
		Assert.Equal(0, b.Atk);
		Assert.Equal(0, b.Health);
		Assert.Empty(b.Tags);
	}

	[Fact]
	public void BuffHelper_WithTag_SingleTag()
	{
		var b = BuffHelper.WithTag(GameTag.Taunt, id: "taunt_buff");
		Assert.True(b.Tags.ContainsKey(GameTag.Taunt));
		Assert.Equal(1, b.Tags[GameTag.Taunt]);
	}

	[Fact]
	public void BuffHelper_Immune_SetsCorrectTags()
	{
		var b = BuffHelper.Immune("immune_buff");
		Assert.True(b.Tags.ContainsKey(GameTag.CantBeDamaged));
		Assert.True(b.Tags.ContainsKey(GameTag.CantBeTargetedByOpponents));
	}

	[Fact]
	public void BuffHelper_Create_WithExtras()
	{
		var b = BuffHelper.Create(1, 1, "buff_with_charge",
			(GameTag.Charge, 1));
		Assert.Equal(1, b.Atk);
		Assert.Equal(1, b.Health);
		Assert.True(b.Tags.ContainsKey(GameTag.Charge));
	}

	[Fact]
	public void BuffDef_ToString_ShowsStats()
	{
		var b = BuffHelper.Create(atk: 3, health: 2);
		var str = b.ToString();
		Assert.Contains("atk=+3", str);
		Assert.Contains("health=+2", str);
	}
}
