namespace HearthstoneServer.Events;

/// <summary>
/// 法术打出阶段。
/// 顺序：OnPlay -> SpellText -> AfterSpell
/// </summary>
public enum SpellPhase
{
	/// <summary>打出/施放时触发。</summary>
	OnPlay,
	/// <summary>法术正文执行阶段。</summary>
	SpellText,
	/// <summary>法术结算后的阶段。</summary>
	AfterSpell,
}

/// <summary>
/// 随从打出与召唤阶段。
/// 顺序：OnPlay -> OnSummon -> Battlecry -> AfterPlay -> AfterSummon
/// </summary>
public enum MinionPhase
{
	/// <summary>打出随从时触发。</summary>
	OnPlay,
	/// <summary>随从进入战场时触发。</summary>
	OnSummon,
	/// <summary>战吼/连击/抉择执行阶段。</summary>
	Battlecry,
	/// <summary>打出后的阶段。</summary>
	AfterPlay,
	/// <summary>召唤后的阶段。</summary>
	AfterSummon,
}

/// <summary>
/// 战斗阶段。
/// 顺序：Preparation -> Combat
/// </summary>
public enum CombatPhase
{
	/// <summary>宣告攻击与重定向阶段。</summary>
	Preparation,
	/// <summary>伤害结算与攻击后阶段。</summary>
	Combat,
}

/// <summary>
/// 英雄技能阶段。
/// 顺序：OnPlay -> Effect -> AfterPlay
/// </summary>
public enum HeroPowerPhase
{
	OnPlay,
	Effect,
	AfterPlay,
}

/// <summary>
/// 回合开始/结束阶段。
/// </summary>
public enum TurnPhase
{
	Start,
	End,
}

/// <summary>
/// 对战开始时阶段。
/// 顺序：On -> After
/// </summary>
public enum StartOfGamePhase
{
	/// <summary>开局效果执行阶段。</summary>
	On,
	/// <summary>开局效果执行后的阶段。</summary>
	After,
}

/// <summary>
/// 没有专用阶段的通用事件阶段。
/// 例如伤害、治疗、死亡、过载、揭示奥秘等。
/// </summary>
public enum MiscPhase
{
	On,
	After,
}
