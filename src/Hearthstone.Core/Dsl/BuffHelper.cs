namespace HearthstoneServer.Dsl;

// ═══════════════════════════════════════════════════
// Buff Helper —  → buff()
// Creates lightweight buff definitions from tag dictionaries
// ═══════════════════════════════════════════════════

/// <summary>
/// A buff definition: a named set of tag modifications.
/// </summary>
public class BuffDef
{
	/// <summary>Buff card ID (enchantment identifier).</summary>
	public string Id { get; init; } = "";

	/// <summary>Tag modifications applied by this buff.</summary>
	public Dictionary<GameTag, int> Tags { get; init; } = new();

	/// <summary>ATK bonus (convenience shortcut).</summary>
	public int Atk => Tags.TryGetValue(GameTag.Atk, out var v) ? v : 0;

	/// <summary>Health bonus (convenience shortcut).</summary>
	public int Health => Tags.TryGetValue(GameTag.Health, out var v) ? v : 0;

	public override string ToString()
	{
		var parts = new List<string>();
		if (Atk != 0) parts.Add($"atk={Atk:+#;-#;0}");
		if (Health != 0) parts.Add($"health={Health:+#;-#;0}");
		foreach (var kv in Tags)
		{
			if (kv.Key != GameTag.Atk && kv.Key != GameTag.Health)
				parts.Add($"{kv.Key}={kv.Value}");
		}
		return $"Buff({string.Join(", ", parts)})";
	}
}

/// <summary>
/// Factory for creating buff definitions.
/// Usage: BuffHelper.Create(atk: 2, health: 1) → +2/+1 buff
///        BuffHelper.Create(atk: 1, taunt: true) → +1 atk + taunt
/// </summary>
public static class BuffHelper
{
	/// <summary>
	/// Create a buff definition with ATK and Health bonuses.
	/// </summary>
	public static BuffDef Create(int atk = 0, int health = 0, string id = "")
	{
		var tags = new Dictionary<GameTag, int>();
		if (atk != 0) tags[GameTag.Atk] = atk;
		if (health != 0) tags[GameTag.Health] = health;
		return new BuffDef { Id = id, Tags = tags };
	}

	/// <summary>
	/// Create a buff definition with ATK, Health, and additional tag bonuses.
	/// </summary>
	public static BuffDef Create(int atk, int health, string id,
		params (GameTag tag, int value)[] extras)
	{
		var tags = new Dictionary<GameTag, int>();
		if (atk != 0) tags[GameTag.Atk] = atk;
		if (health != 0) tags[GameTag.Health] = health;
		foreach (var (tag, value) in extras)
			tags[tag] = value;
		return new BuffDef { Id = id, Tags = tags };
	}

	/// <summary>
	/// Create a buff with a single boolean tag (e.g. taunt, charge).
	/// </summary>
	public static BuffDef WithTag(GameTag tag, int value = 1, string id = "")
	{
		return new BuffDef
		{
			Id = id,
			Tags = new Dictionary<GameTag, int> { [tag] = value }
		};
	}

	/// <summary>
	/// Create an immune buff (cant_be_damaged + cant_be_targeted_by_opponents).
	/// </summary>
	public static BuffDef Immune(string id = "")
	{
		return new BuffDef
		{
			Id = id,
			Tags = new Dictionary<GameTag, int>
			{
				[GameTag.CantBeDamaged] = 1,
				[GameTag.CantBeTargetedByOpponents] = 1
			}
		};
	}
}
