namespace HearthstoneServer.Events;

/// <summary>法术打出阶段。</summary>
public enum SpellPhase
{
	/// <summary>打出时。</summary>
	OnPlay,
	/// <summary>法术正文结算。</summary>
	SpellText,
	/// <summary>法术结算后。</summary>
	AfterSpell,
}

/// <summary>随从打出和召唤阶段。</summary>
public enum MinionPhase
{
	/// <summary>打出时。</summary>
	OnPlay,
	/// <summary>进入战场时。</summary>
	OnSummon,
	/// <summary>战吼/连击等结算。</summary>
	Battlecry,
	/// <summary>打出后。</summary>
	AfterPlay,
	/// <summary>召唤后。</summary>
	AfterSummon,
}

/// <summary>战斗阶段。</summary>
public enum CombatPhase
{
	/// <summary>宣告攻击与准备阶段。</summary>
	Preparation,
	/// <summary>战斗伤害与攻击后阶段。</summary>
	Combat,
}

/// <summary>英雄技能阶段。</summary>
public enum HeroPowerPhase
{
	OnPlay,
	Effect,
	AfterPlay,
}

/// <summary>回合起止阶段。</summary>
public enum TurnPhase
{
	Start,
	End,
}

/// <summary>对战开始阶段。</summary>
public enum StartOfGamePhase
{
	On,
	After,
}

/// <summary>武器装备阶段。</summary>
public enum WeaponPhase
{
	/// <summary>装备前，可取消。</summary>
	BeforeEquip,
	/// <summary>装备进行中。</summary>
	OnEquip,
	/// <summary>装备完成后。</summary>
	AfterEquip,
}

/// <summary>武器失去耐久前后的阶段。</summary>
public enum WeaponDurabilityLossPhase
{
	/// <summary>默认掉耐久前，可改写为其他代价。</summary>
	BeforeLoseDurability,
	/// <summary>耐久损失或替代代价结算后。</summary>
	AfterLoseDurability,
}

/// <summary>通用杂项事件阶段。</summary>
public enum MiscPhase
{
	On,
	After,
}
