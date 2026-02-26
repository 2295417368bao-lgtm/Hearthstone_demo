using HearthstoneServer.Entities;

namespace HearthstoneServer.Events;

// ═══════════════════════════════════════════════════
// GameEvent — 不可变事件数据包
// 描述"发生了什么"，由 EventBus 分发
// ═══════════════════════════════════════════════════

/// <summary>
/// Base class for all game events. Immutable data describing what happened.
/// </summary>
public abstract record GameEvent(BaseEntity Source);

// ── 卡牌打出 ──

/// <summary>Fires whenever any card is played from hand.</summary>
public record CardPlayedEvent(BaseEntity Source, BuffableEntity Card, Character? Target) : GameEvent(Source);

/// <summary>Fires whenever a spell is cast.</summary>
public record SpellCastEvent(BaseEntity Source, BuffableEntity Spell, Character? Target) : GameEvent(Source);

/// <summary>Fires whenever a minion is played from hand.</summary>
public record MinionPlayedEvent(BaseEntity Source, Minion Minion, Character? Target) : GameEvent(Source);

// ── 召唤 ──

/// <summary>Fires whenever a minion enters the battlefield (played or summoned).</summary>
public record MinionSummonedEvent(BaseEntity Source, Minion Minion) : GameEvent(Source);

// ── 战斗 ──

/// <summary>Fires when an attack is proposed (can redirect defender).</summary>
public record ProposedAttackEvent(BaseEntity Source, Character Attacker, Character Defender) : GameEvent(Source);

/// <summary>Fires when an attack begins (after redirects).</summary>
public record AttackEvent(BaseEntity Source, Character Attacker, Character Defender) : GameEvent(Source);

/// <summary>Fires after attack damage is dealt.</summary>
public record AfterAttackEvent(BaseEntity Source, Character Attacker, Character Defender) : GameEvent(Source);

// ── 伤害/治疗 ──

/// <summary>
/// Fires BEFORE damage is applied to a character. Mutable — handlers can set
/// <see cref="Redirected"/> to prevent the damage from being applied.
/// Used for damage redirection effects (e.g. Bulwark of Azzinoth).
/// </summary>
public record PreDamageEvent(BaseEntity Source, Character Target, int Amount) : GameEvent(Source)
{
	/// <summary>If set to true by a handler, the damage is NOT applied to the original target.</summary>
	public bool Redirected { get; set; }
}

/// <summary>Fires when damage is dealt to a character.</summary>
public record DamageEvent(BaseEntity Source, Character Target, int Amount) : GameEvent(Source);

/// <summary>Fires when a character is healed.</summary>
public record HealEvent(BaseEntity Source, Character Target, int Amount) : GameEvent(Source);

// ── 死亡 ──

/// <summary>Fires when an entity dies.</summary>
public record DeathEvent(BaseEntity Source, BaseEntity Dead) : GameEvent(Source);

// ── 回合 ──

/// <summary>Fires at the start of a player's turn.</summary>
public record TurnStartEvent(BaseEntity Source, Player Player) : GameEvent(Source);

/// <summary>Fires at the end of a player's turn.</summary>
public record TurnEndEvent(BaseEntity Source, Player Player) : GameEvent(Source);

// ── 其他 ──

/// <summary>Fires when a secret is played.</summary>
public record SecretPlayedEvent(BaseEntity Source, BuffableEntity Secret) : GameEvent(Source);

/// <summary>Fires when a card is drawn.</summary>
public record DrawCardEvent(BaseEntity Source, Player Player, BaseEntity? Card) : GameEvent(Source);

/// <summary>Fires when a hero power is used.</summary>
public record HeroPowerUsedEvent(BaseEntity Source, Player Player) : GameEvent(Source);

/// <summary>Fires when overload is applied.</summary>
public record OverloadEvent(BaseEntity Source, Player Player, int Amount) : GameEvent(Source);

/// <summary>Fires when a secret is revealed.</summary>
public record RevealSecretEvent(BaseEntity Source, BuffableEntity Secret) : GameEvent(Source);

/// <summary>Fires when a hero gains armor.</summary>
public record GainArmorEvent(BaseEntity Source, Player Player, int Amount) : GameEvent(Source);

/// <summary>
/// Fires when a drawn card is burned because the hand is full.
/// The card goes to the graveyard without entering the hand.
/// </summary>
public record MillEvent(BaseEntity Source, Player Player, BaseEntity Card) : GameEvent(Source);

/// <summary>
/// Fires when a card is discarded from hand (NOT from deck).
/// Mill from deck = destruction, not discard.
/// Death on battlefield = death, not discard.
/// Discarded cards are tracked for discard-synergy effects.
/// </summary>
public record DiscardEvent(BaseEntity Source, Player Player, BuffableEntity Card) : GameEvent(Source);
