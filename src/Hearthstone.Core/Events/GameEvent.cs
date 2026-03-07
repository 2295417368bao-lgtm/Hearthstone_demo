using HearthstoneServer.Entities;

namespace HearthstoneServer.Events;

/// <summary>所有游戏事件的基类。</summary>
public abstract record GameEvent(BaseEntity Source);

/// <summary>卡牌被打出。</summary>
public record CardPlayedEvent(BaseEntity Source, BuffableEntity Card, Character? Target) : GameEvent(Source);
/// <summary>法术被施放。</summary>
public record SpellCastEvent(BaseEntity Source, BuffableEntity Spell, Character? Target) : GameEvent(Source);
/// <summary>随从被打出。</summary>
public record MinionPlayedEvent(BaseEntity Source, Minion Minion, Character? Target) : GameEvent(Source);
/// <summary>随从进入战场。</summary>
public record MinionSummonedEvent(BaseEntity Source, Minion Minion) : GameEvent(Source);

/// <summary>宣告攻击。</summary>
public record ProposedAttackEvent(BaseEntity Source, Character Attacker, Character Defender) : GameEvent(Source);
/// <summary>攻击开始。</summary>
public record AttackEvent(BaseEntity Source, Character Attacker, Character Defender) : GameEvent(Source);
/// <summary>攻击完成。</summary>
public record AfterAttackEvent(BaseEntity Source, Character Attacker, Character Defender) : GameEvent(Source);

/// <summary>伤害生效前，可重定向、取消或改写伤害值。</summary>
public record PreDamageEvent : GameEvent
{
	public PreDamageEvent(BaseEntity source, Character target, int amount) : base(source)
	{
		Target = target;
		Amount = amount;
	}

	public Character Target { get; init; }
	public int Amount { get; set; }
	public bool Redirected { get; set; }
}

/// <summary>英雄使用武器攻击后，默认失去耐久前后发布的事件。</summary>
public record WeaponDurabilityLossEvent(BaseEntity Source, Hero Hero, Weapon Weapon, Character Defender, int Amount) : GameEvent(Source)
{
	/// <summary>若被替换，则默认耐久损失不会执行。</summary>
	public bool Replaced { get; set; }
}

/// <summary>角色受到伤害。</summary>
public record DamageEvent(BaseEntity Source, Character Target, int Amount) : GameEvent(Source);
/// <summary>角色受到治疗。</summary>
public record HealEvent(BaseEntity Source, Character Target, int Amount) : GameEvent(Source);
/// <summary>发生过量治疗。</summary>
public record OverhealEvent(BaseEntity Source, Character Target, int Amount) : GameEvent(Source);
/// <summary>实体死亡。</summary>
public record DeathEvent(BaseEntity Source, BaseEntity Dead) : GameEvent(Source);

/// <summary>回合开始。</summary>
public record TurnStartEvent(BaseEntity Source, Player Player) : GameEvent(Source);
/// <summary>回合结束。</summary>
public record TurnEndEvent(BaseEntity Source, Player Player) : GameEvent(Source);
/// <summary>对战开始时，按玩家分发的开局事件。</summary>
public record StartOfGameEvent(BaseEntity Source, Player Player) : GameEvent(Source);

/// <summary>奥秘被打出。</summary>
public record SecretPlayedEvent(BaseEntity Source, BuffableEntity Secret) : GameEvent(Source);
/// <summary>抽牌。</summary>
public record DrawCardEvent(BaseEntity Source, Player Player, BaseEntity? Card) : GameEvent(Source);
/// <summary>使用英雄技能。</summary>
public record HeroPowerUsedEvent(BaseEntity Source, Player Player) : GameEvent(Source);
/// <summary>施加过载。</summary>
public record OverloadEvent(BaseEntity Source, Player Player, int Amount) : GameEvent(Source);
/// <summary>奥秘被揭示。</summary>
public record RevealSecretEvent(BaseEntity Source, BuffableEntity Secret) : GameEvent(Source);
/// <summary>获得护甲。</summary>
public record GainArmorEvent(BaseEntity Source, Player Player, int Amount) : GameEvent(Source);
/// <summary>烧牌。</summary>
public record MillEvent(BaseEntity Source, Player Player, BaseEntity Card) : GameEvent(Source);
/// <summary>弃牌。</summary>
public record DiscardEvent(BaseEntity Source, Player Player, BuffableEntity Card) : GameEvent(Source);

/// <summary>
/// 武器装备事件。
/// Source 表示真正 cause；若由英雄技能导致装备，则 Source 为 HeroPower。
/// </summary>
public record WeaponEquipEvent(BaseEntity Source, Player Player, Weapon Weapon) : GameEvent(Source)
{
	/// <summary>在 BeforeEquip 阶段可被置为 true 以取消本次装备。</summary>
	public bool Cancelled { get; set; }
}
