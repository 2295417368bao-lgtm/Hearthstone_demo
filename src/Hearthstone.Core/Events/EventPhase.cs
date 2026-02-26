namespace HearthstoneServer.Events;

// ═══════════════════════════════════════════════════
// Phase 枚举 — 每种 Sequence 的精确阶段
// 基于 Advanced Rulebook:
// https://hearthstone.wiki.gg/wiki/Advanced_rulebook
// ═══════════════════════════════════════════════════

/// <summary>
/// Phases for playing a spell.
/// Sequence: OnPlay → SpellText → AfterSpell
/// - Counterspell triggers at OnPlay, cancels SpellText
/// - Spell effects execute at SpellText
/// - "After you cast a spell" triggers at AfterSpell
/// </summary>
public enum SpellPhase
{
	/// <summary>"Whenever you play/cast" triggers + Counterspell check.</summary>
	OnPlay,
	/// <summary>Spell text executes (skipped if countered). Spellbender redirects here.</summary>
	SpellText,
	/// <summary>"After you cast a spell" triggers.</summary>
	AfterSpell,
}

/// <summary>
/// Phases for playing/summoning a minion.
/// Sequence: OnPlay → OnSummon → Battlecry → AfterPlay → AfterSummon
/// </summary>
public enum MinionPhase
{
	/// <summary>"Whenever you play a card/minion" triggers (Questing Adventurer, etc.).</summary>
	OnPlay,
	/// <summary>"Whenever you summon a minion" triggers (Starving Buzzard, Knife Juggler on-summon).</summary>
	OnSummon,
	/// <summary>Battlecry/Combo/Choose One executes here.</summary>
	Battlecry,
	/// <summary>"After you play a minion" triggers (Mirror Entity, Snipe, Repentance).</summary>
	AfterPlay,
	/// <summary>"After you summon a minion" triggers (Knife Juggler, Sword of Justice).</summary>
	AfterSummon,
}

/// <summary>
/// Phases for combat (attacking).
/// Sequence: Preparation → Combat
/// </summary>
public enum CombatPhase
{
	/// <summary>Proposed Attack + Attack events. Secrets like Misdirection trigger here.</summary>
	Preparation,
	/// <summary>Damage dealt simultaneously, weapon durability lost, After Attack triggers.</summary>
	Combat,
}

/// <summary>
/// Phases for using a hero power.
/// Sequence: OnPlay → Effect → AfterPlay
/// </summary>
public enum HeroPowerPhase
{
	OnPlay,
	Effect,
	AfterPlay,
}

/// <summary>
/// Phases for turn start/end.
/// </summary>
public enum TurnPhase
{
	Start,
	End,
}

/// <summary>
/// Generic phases for events without a dedicated sequence
/// (Damage, Heal, Death, Overload, Reveal, etc.).
/// </summary>
public enum MiscPhase
{
	On,
	After,
}
