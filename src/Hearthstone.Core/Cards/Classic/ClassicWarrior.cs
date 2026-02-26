using HearthstoneServer.Auras;
using HearthstoneServer.Entities;
using HearthstoneServer.Events;
using static HearthstoneServer.Dsl.BusEvent;
using static HearthstoneServer.Cards.CardScriptUtils;
using static HearthstoneServer.Dsl.EventFilters;
using HearthstoneServer.Targeting;

namespace HearthstoneServer.Dsl.Cards;

// ═════════════════════════════════════════
// 经典战士卡牌脚本
// ═════════════════════════════════════════

public class ClassicWarrior : CardPack
{
	public override void Register(CardBuilder b)
	{
		// ── 附魔 ──
		b.Buff("CS2_105e", atk: 2,
			on: When<TurnEndEvent>(OWN, ON, new Destroy(S.SELF)));  // 英勇打击（本回合）
		b.Buff("EX1_409e", atk: 2);                           // 升级！
		b.Buff("NEW1_036e", atk: 1, health: 1);               // 冲锋呐喊（简化版）
		b.Buff("EX1_603e", atk: 2);                           // 残忍的监工
		b.Buff("CS2_104e", atk: 2);                           // 暴怒
		b.Buff("EX1_411e", atk: 2, health: 2);                // 暴乱狂战士（简化版）
		b.Buff("EX1_414e", atk: 1);                           // 格罗玛什（简化版）
		b.Buff("EX1_604o", atk: 1);                           // 恐怖的奴隶主每次受伤

		// ── 随从 ──

		// 铸甲师：友方随从受到伤害时，获得 1 点护甲
		b.Card("EX1_402",
			on: When<DamageEvent>(OWN, ON, new GainArmor(S.FRIENDLY_HERO, 1),
				when: TargetIsMinion()));

		// 恐怖的奴隶主：每当一个随从受到伤害时，获得 +1 攻击力
		b.Card("EX1_604",
			on: When<DamageEvent>(ANY, ON, new Enchant(S.SELF, "EX1_604o"),
				when: TargetIsMinion()));

		// 残忍的监工：战吼：对一个随从造成 1 点伤害，使其获得 +2 攻击力
		b.Card("EX1_603",
			battlecry: Acts(new DealDamage(S.TARGET, 1), new Enchant(S.TARGET, "EX1_603e")),
			target: Target.IfAvailable().Minion());

		// 格罗玛什·地狱咆哮：冲锋。激怒：+6 攻击力
		b.Card("EX1_414", enrage: new[]
		{
			new Refresh(S.SELF, new Dictionary<GameTag, object> { [GameTag.Atk] = 6 })
		});

		// ── 法术 ──

		// 斩杀：消灭一个受伤的敌方随从
		b.Card("CS2_108",
			play: new Destroy(S.TARGET),
			target: Target.Required().Minion().Enemy().Damaged());

		// 英勇打击：本回合 +4 攻击力
		b.Card("CS2_105", play: new Enchant(S.FRIENDLY_HERO, "CS2_105e"));

		// 旋风斩：对所有随从造成 1 点伤害
		b.Card("EX1_400", play: new DealDamage(S.ALL_MINIONS, 1));

		// 顺劈斩：对 2 个随机敌方随从造成 2 点伤害
		b.Card("CS2_114",
			play: new DealDamage(new RandomSelector(S.ENEMY_MINIONS, 2), 2),
			target: Target.Condition().NeedsEnemyMinions(1));

		// 盾牌格挡：获得 5 点护甲，抽一张牌
		b.Card("EX1_606", play: Acts(new GainArmor(S.FRIENDLY_HERO, 5), new DrawCard(S.CONTROLLER)));

		// 盾牌猛击：造成等同于护甲值的伤害
		b.Card("EX1_410",
			play: new DealDamage(S.TARGET, ARMOR(S.FRIENDLY_HERO)),
			target: Target.Required().Minion());

		// 猛击：造成 2 点伤害，若随从存活则抽一张牌
		b.Card("EX1_391",
			play: new DealDamage(S.TARGET, 2),
			target: Target.Required().Minion());

		// 致死打击：造成 4 点伤害（英雄生命值 ≤12 时造成 6 点）
		b.Card("EX1_408",
			play: new DealDamage(S.TARGET, 4),
			target: Target.Required());

		// 冲锋：使一个友方随从获得冲锋和 +2 攻击力
		b.Card("CS2_103",
			play: Acts(GiveCharge(S.TARGET), new Enchant(S.TARGET, "CS2_103e2")),
			target: Target.Required().Minion().Friendly());
		b.Buff("CS2_103e2", atk: 2);

		// 战斗怒火：为每个受伤的友方角色抽一张牌
		b.Card("EX1_392", play: new DrawCard(S.CONTROLLER));

		// 暴怒：使一个受伤的随从获得 +3/+3
		b.Card("CS2_104",
			play: new Enchant(S.TARGET, "CS2_104e"),
			target: Target.Required().Minion());

		// 乱斗：消灭除一个随机随从外的所有随从
		b.Card("EX1_407",
			play: new Destroy(S.ALL_MINIONS),
			target: Target.Condition().NeedsTotalMinions(2));

		// 冲锋呐喊：本回合内你的随从生命值不会降至 1 以下
		b.Card("NEW1_036", play: new Enchant(S.FRIENDLY_MINIONS, "NEW1_036e"));

		// 升级！：若已装备武器则 +1/+1，否则装备 1/3 武器
		b.Card("EX1_409",
			play: Acts(new Enchant(S.FRIENDLY_WEAPON, "EX1_409e"), new Summon(S.CONTROLLER, "EX1_409t")));

		// 内心怒火：对一个随从造成 1 点伤害，使其获得 +2 攻击力
		b.Card("EX1_607",
			play: Acts(new DealDamage(S.TARGET, 1), new Enchant(S.TARGET, "EX1_607e")),
			target: Target.Required().Minion());
		b.Buff("EX1_607e", atk: 2);

		// ── 武器 ──

		// 炽炎战斧：3/2（仅属性）

		// 奥金斧：5/2（仅属性）

		// 血吼：攻击后失去 1 点攻击力而非耐久度（简化版）
		b.Card("EX1_411",
			on: When<AfterAttackEvent>(OWN, AFTER, new Enchant(S.SELF, "EX1_411e")));
	}
}
