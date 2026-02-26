using HearthstoneServer.Entities;
using HearthstoneServer.Events;

namespace HearthstoneServer.Dsl;

// ═══════════════════════════════════════════════════
// EventFilters — 常用事件过滤谓词工厂
// ═══════════════════════════════════════════════════
//
// 用法：using static HearthstoneServer.Dsl.EventFilters;
//
//   When<DeathEvent>(ANY, AFTER, new Summon(...),
//       when: DeadHas(GameTag.Deathrattle))
//

/// <summary>
/// Factory for reusable event filter predicates.
/// Used with BusEvent.When&lt;T&gt;(..., when:) overloads.
/// </summary>
public static class EventFilters
{
	// ─── DeathEvent 过滤 ───

	/// <summary>死亡的实体有指定 GameTag。</summary>
	public static Func<DeathEvent, BuffableEntity, bool> DeadHas(GameTag tag)
		=> (evt, self) => evt.Dead is BuffableEntity be && be.GetBoolProperty(tag);

	/// <summary>死亡的实体是指定种族。</summary>
	public static Func<DeathEvent, BuffableEntity, bool> DeadRace(Race race)
		=> (evt, self) => evt.Dead.Data?.Races?.Contains(race) == true;

	/// <summary>死亡的实体是指定卡牌 ID。</summary>
	public static Func<DeathEvent, BuffableEntity, bool> DeadId(string cardId)
		=> (evt, self) => (evt.Dead as BuffableEntity)?.CardId == cardId;

	/// <summary>死亡的是敌方实体。</summary>
	public static Func<DeathEvent, BuffableEntity, bool> DeadIsEnemy()
		=> (evt, self) => (evt.Dead as BuffableEntity)?.Controller != self.Controller;

	/// <summary>死亡的是友方实体。</summary>
	public static Func<DeathEvent, BuffableEntity, bool> DeadIsFriendly()
		=> (evt, self) => (evt.Dead as BuffableEntity)?.Controller == self.Controller;

	/// <summary>死亡的是随从。</summary>
	public static Func<DeathEvent, BuffableEntity, bool> DeadIsMinion()
		=> (evt, self) => evt.Dead is Minion;

	// ─── CardPlayedEvent / SpellCastEvent 过滤 ───

	/// <summary>法术的目标是 self（用于"当你以法术指向该随从"）。</summary>
	public static Func<SpellCastEvent, BuffableEntity, bool> SpellTargetsSelf()
		=> (evt, self) => evt.Target == self;

	// ─── MinionSummonedEvent 过滤 ───

	/// <summary>召唤的随从是指定种族。</summary>
	public static Func<MinionSummonedEvent, BuffableEntity, bool> SummonedRace(Race race)
		=> (evt, self) => evt.Minion.Data?.Races?.Contains(race) == true;

	/// <summary>召唤的随从攻击力 == value。</summary>
	public static Func<MinionSummonedEvent, BuffableEntity, bool> SummonedAtk(int value)
		=> (evt, self) => evt.Minion.Atk == value;

	/// <summary>召唤的随从有指定 GameTag。</summary>
	public static Func<MinionSummonedEvent, BuffableEntity, bool> SummonedHas(GameTag tag)
		=> (evt, self) => evt.Minion.GetBoolProperty(tag);

	// ─── MinionPlayedEvent 过滤 ───

	/// <summary>打出的随从有指定 GameTag。</summary>
	public static Func<MinionPlayedEvent, BuffableEntity, bool> PlayedHas(GameTag tag)
		=> (evt, self) => evt.Minion.GetBoolProperty(tag);

	// ─── DamageEvent 过滤 ───

	/// <summary>受伤的是友方英雄。</summary>
	public static Func<DamageEvent, BuffableEntity, bool> DamagedIsFriendlyHero()
		=> (evt, self) => evt.Target is Hero h && h.Controller == self.Controller;

	// ─── DrawCardEvent 过滤 ───

	/// <summary>抽牌的是对手。</summary>
	public static Func<DrawCardEvent, BuffableEntity, bool> DrawIsOpponent()
		=> (evt, self) => evt.Player != self.Controller;

	// ─── 通用过滤 ───

	/// <summary>当前是己方回合。</summary>
	public static Func<T, BuffableEntity, bool> IsMyTurn<T>() where T : GameEvent
		=> (evt, self) => self.Game?.CurrentPlayer == self.Controller;

	/// <summary>事件来自对手。</summary>
	public static Func<CardPlayedEvent, BuffableEntity, bool> PlayedByOpponent()
		=> (evt, self) => evt.Card.Controller != self.Controller;

	/// <summary>打出的卡是随从。</summary>
	public static Func<CardPlayedEvent, BuffableEntity, bool> PlayedIsMinion()
		=> (evt, self) => evt.Card is Minion;

	/// <summary>打出的卡是法术。</summary>
	public static Func<CardPlayedEvent, BuffableEntity, bool> PlayedIsSpell()
		=> (evt, self) => evt.Card.CardData?.CardType == CardType.Spell;

	/// <summary>打出的卡拥有指定机制（连击、战吼等）。</summary>
	public static Func<CardPlayedEvent, BuffableEntity, bool> PlayedHasMechanic(Mechanic mechanic)
		=> (evt, self) => evt.Card.CardData?.Has(mechanic) == true;

	/// <summary>打出的随从攻击力 == value。</summary>
	public static Func<CardPlayedEvent, BuffableEntity, bool> PlayedAtk(int value)
		=> (evt, self) => evt.Card is Minion m && m.Atk == value;

	/// <summary>打出的卡是龙族。</summary>
	public static Func<CardPlayedEvent, BuffableEntity, bool> PlayedRace(Race race)
		=> (evt, self) => evt.Card.Data?.Races?.Contains(race) == true;

	// ─── 组合过滤 ───

	/// <summary>AND 组合两个过滤器。</summary>
	public static Func<T, BuffableEntity, bool> And<T>(
		Func<T, BuffableEntity, bool> a,
		Func<T, BuffableEntity, bool> b)
		where T : GameEvent
		=> (evt, self) => a(evt, self) && b(evt, self);

	/// <summary>OR 组合两个过滤器。</summary>
	public static Func<T, BuffableEntity, bool> Or<T>(
		Func<T, BuffableEntity, bool> a,
		Func<T, BuffableEntity, bool> b)
		where T : GameEvent
		=> (evt, self) => a(evt, self) || b(evt, self);

	// ─── SpellCastEvent 过滤 ───

	/// <summary>法术由对手施放。</summary>
	public static Func<SpellCastEvent, BuffableEntity, bool> SpellCastByOpponent()
		=> (evt, self) => evt.Spell.Controller != self.Controller;

	// ─── CardPlayedEvent 高级过滤 ───

	/// <summary>打出的卡是法术且费用 == cost。</summary>
	public static Func<CardPlayedEvent, BuffableEntity, bool> PlayedIsSpellCost(int cost)
		=> (evt, self) => evt.Card is not Minion && evt.Card is BuffableEntity be && be.Cost == cost;

	// ─── GainArmorEvent 过滤 ───

	/// <summary>获得护甲的英雄是友方英雄。</summary>
	public static Func<GainArmorEvent, BuffableEntity, bool> GainArmorIsFriendly()
		=> (evt, self) => evt.Player == self.Controller;

	// ─── 区域过滤 ───

	/// <summary>仅当实体在指定区域时触发（手牌区/战场/牌库等）。</summary>
	public static Func<T, BuffableEntity, bool> InZone<T>(Zone zone) where T : GameEvent
		=> (evt, self) => self.Zone == zone;

	// ─── 属性阈值过滤 ───

	/// <summary>自身攻击力 >= value。</summary>
	public static Func<T, BuffableEntity, bool> SelfAtkAtLeast<T>(int value) where T : GameEvent
		=> (evt, self) => self.GetIntProperty("atk") >= value;

	/// <summary>自身生命值 >= value。</summary>
	public static Func<T, BuffableEntity, bool> SelfHealthAtLeast<T>(int value) where T : GameEvent
		=> (evt, self) => self.GetIntProperty("health") >= value;

	/// <summary>打出/召唤的随从攻击力 >= value。</summary>
	public static Func<MinionPlayedEvent, BuffableEntity, bool> PlayedAtkAtLeast(int value)
		=> (evt, self) => evt.Minion.Atk >= value;

	/// <summary>打出/召唤的随从攻击力 >= value。</summary>
	public static Func<MinionSummonedEvent, BuffableEntity, bool> SummonedAtkAtLeast(int value)
		=> (evt, self) => evt.Minion.Atk >= value;

	// ─── AttackEvent 过滤 ───

	/// <summary>自身就是攻击者。</summary>
	public static Func<AfterAttackEvent, BuffableEntity, bool> SelfIsAttacker()
		=> (evt, self) => evt.Attacker == self;

	/// <summary>攻击者是友方英雄。</summary>
	public static Func<AfterAttackEvent, BuffableEntity, bool> AttackerIsHero()
		=> (evt, self) => evt.Attacker is Hero && evt.Attacker.Controller == self.Controller;

	/// <summary>防御者已死亡（击杀触发）。</summary>
	public static Func<AfterAttackEvent, BuffableEntity, bool> DefenderDied()
		=> (evt, self) => evt.Defender is Character ch && (ch.Dead || ch.ToBeDestroyed);

	/// <summary>攻击者是友方随从。</summary>
	public static Func<AfterAttackEvent, BuffableEntity, bool> AttackerIsFriendlyMinion()
		=> (evt, self) => evt.Attacker is Minion && evt.Attacker.Controller == self.Controller;

	// ─── 声明式事件过滤器 ───

	/// <summary>攻击者是随从。</summary>
	public static Func<AttackEvent, BuffableEntity, bool> AttackerIsMinion()
		=> (evt, self) => evt.Attacker is Minion;

	/// <summary>防御者是己方英雄。</summary>
	public static Func<AttackEvent, BuffableEntity, bool> DefenderIsHero()
		=> (evt, self) => evt.Defender == self.Controller?.Hero;

	/// <summary>防御者是随从。</summary>
	public static Func<AttackEvent, BuffableEntity, bool> DefenderIsMinion()
		=> (evt, self) => evt.Defender is Minion;

	/// <summary>受伤目标是英雄。</summary>
	public static Func<DamageEvent, BuffableEntity, bool> TargetIsHero()
		=> (evt, self) => evt.Target is Hero;

	/// <summary>PreDamage 目标是己方英雄。</summary>
	public static Func<PreDamageEvent, BuffableEntity, bool> PreDamageTargetIsFriendlyHero()
		=> (evt, self) => evt.Target is Hero && evt.Target.Controller == self.Controller;

	/// <summary>受伤目标是随从。</summary>
	public static Func<DamageEvent, BuffableEntity, bool> TargetIsMinion()
		=> (evt, self) => evt.Target is Minion;

	/// <summary>治疗目标是随从。</summary>
	public static Func<HealEvent, BuffableEntity, bool> HealTargetIsMinion()
		=> (evt, self) => evt.Target is Minion;

	/// <summary>死亡实体属于某种族。</summary>
	public static Func<DeathEvent, BuffableEntity, bool> DeadIsRace(Race race)
		=> (evt, self) => evt.Dead is Character c && c.Races.Contains(race);

	/// <summary>法术有目标。</summary>
	public static Func<SpellCastEvent, BuffableEntity, bool> SpellHasTarget()
		=> (evt, self) => evt.Target != null;

	/// <summary>自身存活（生命值 > 0），用于"受伤后若存活"触发。</summary>
	public static Func<DamageEvent, BuffableEntity, bool> SelfIsAlive()
		=> (evt, self) => self is Character c && c.Health > 0;

	/// <summary>大于等于比较器。</summary>
	public static Func<int, int, bool> GE() => (a, b) => a >= b;

	/// <summary>小于等于比较器。</summary>
	public static Func<int, int, bool> LE() => (a, b) => a <= b;

	// ─── 组合器 ───

	/// <summary>And 组合：所有条件均满足。</summary>
	public static Func<T, BuffableEntity, bool> And<T>(
		params Func<T, BuffableEntity, bool>[] filters) where T : GameEvent
		=> (evt, self) => filters.All(f => f(evt, self));

	// ─── Evaluator 桥接 ───

	/// <summary>将 Evaluator 转为 when 过滤器。</summary>
	public static Func<T, BuffableEntity, bool> Condition<T>(Evaluator eval) where T : GameEvent
		=> (evt, self) => eval.Check(self);

	/// <summary>取反任意过滤器。</summary>
	public static Func<T, BuffableEntity, bool> Not<T>(Func<T, BuffableEntity, bool> f) where T : GameEvent
		=> (evt, self) => !f(evt, self);
}
