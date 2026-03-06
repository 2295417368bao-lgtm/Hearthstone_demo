using HearthstoneServer.Entities;

namespace HearthstoneServer.Events;

/// <summary>
/// 所有游戏事件的基类。
/// 事件对象只描述“发生了什么”，由 EventBus 负责分发。
/// </summary>
public abstract record GameEvent(BaseEntity Source);

/// <summary>任意卡牌从手牌打出时触发。</summary>
public record CardPlayedEvent(BaseEntity Source, BuffableEntity Card, Character? Target) : GameEvent(Source);

/// <summary>法术施放时触发。</summary>
public record SpellCastEvent(BaseEntity Source, BuffableEntity Spell, Character? Target) : GameEvent(Source);

/// <summary>随从从手牌打出时触发。</summary>
public record MinionPlayedEvent(BaseEntity Source, Minion Minion, Character? Target) : GameEvent(Source);

/// <summary>随从进入战场时触发（包括打出和召唤）。</summary>
public record MinionSummonedEvent(BaseEntity Source, Minion Minion) : GameEvent(Source);

/// <summary>宣告攻击时触发，可用于重定向目标。</summary>
public record ProposedAttackEvent(BaseEntity Source, Character Attacker, Character Defender) : GameEvent(Source);

/// <summary>攻击正式开始时触发。</summary>
public record AttackEvent(BaseEntity Source, Character Attacker, Character Defender) : GameEvent(Source);

/// <summary>攻击伤害结算后触发。</summary>
public record AfterAttackEvent(BaseEntity Source, Character Attacker, Character Defender) : GameEvent(Source);

/// <summary>
/// 伤害应用前触发。
/// 处理器可以设置 Redirected 来阻止本次伤害命中原目标。
/// </summary>
public record PreDamageEvent(BaseEntity Source, Character Target, int Amount) : GameEvent(Source)
{
	/// <summary>若为 true，则原始目标不会承受这次伤害。</summary>
	public bool Redirected { get; set; }
}

/// <summary>角色受到伤害时触发。</summary>
public record DamageEvent(BaseEntity Source, Character Target, int Amount) : GameEvent(Source);

/// <summary>角色受到治疗时触发。</summary>
public record HealEvent(BaseEntity Source, Character Target, int Amount) : GameEvent(Source);

/// <summary>实体死亡时触发。</summary>
public record DeathEvent(BaseEntity Source, BaseEntity Dead) : GameEvent(Source);

/// <summary>玩家回合开始时触发。</summary>
public record TurnStartEvent(BaseEntity Source, Player Player) : GameEvent(Source);

/// <summary>玩家回合结束时触发。</summary>
public record TurnEndEvent(BaseEntity Source, Player Player) : GameEvent(Source);

/// <summary>对战开始流程中，针对某个玩家发布的开局事件。</summary>
public record StartOfGameEvent(BaseEntity Source, Player Player) : GameEvent(Source);

/// <summary>奥秘被打出时触发。</summary>
public record SecretPlayedEvent(BaseEntity Source, BuffableEntity Secret) : GameEvent(Source);

/// <summary>抽牌时触发。</summary>
public record DrawCardEvent(BaseEntity Source, Player Player, BaseEntity? Card) : GameEvent(Source);

/// <summary>英雄技能使用时触发。</summary>
public record HeroPowerUsedEvent(BaseEntity Source, Player Player) : GameEvent(Source);

/// <summary>应用过载时触发。</summary>
public record OverloadEvent(BaseEntity Source, Player Player, int Amount) : GameEvent(Source);

/// <summary>奥秘揭示时触发。</summary>
public record RevealSecretEvent(BaseEntity Source, BuffableEntity Secret) : GameEvent(Source);

/// <summary>英雄获得护甲时触发。</summary>
public record GainArmorEvent(BaseEntity Source, Player Player, int Amount) : GameEvent(Source);

/// <summary>
/// 手牌已满导致抽到的牌被烧毁时触发。
/// 这张牌不会进入手牌，而是直接进入墓地。
/// </summary>
public record MillEvent(BaseEntity Source, Player Player, BaseEntity Card) : GameEvent(Source);

/// <summary>
/// 手牌中的牌被弃掉时触发。
/// 牌库被烧牌不算弃牌，战场实体死亡也不算弃牌。
/// </summary>
public record DiscardEvent(BaseEntity Source, Player Player, BuffableEntity Card) : GameEvent(Source);
