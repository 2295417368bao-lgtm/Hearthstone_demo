using HearthstoneServer.Auras;
using HearthstoneServer.Events;
using static HearthstoneServer.Dsl.BusEvent;
using static HearthstoneServer.Cards.CardScriptUtils;
using static HearthstoneServer.Dsl.EventFilters;
using HearthstoneServer.Targeting;

namespace HearthstoneServer.Dsl.Cards;

// ══════════════════════════════════════════════════════
// 核心战士卡牌脚本 (Core Set)
// ══════════════════════════════════════════════════════

public class CoreWarrior : CardPack
{
	public override void Register(CardBuilder b)
	{
		// ── 附魔 ──
		b.Buff("CORE_EX1_604e", atk: 1); // 暴乱狂战士

		// ── 随从 ──

		// 暴乱狂战士 CORE_EX1_604 — 每当一个随从受到伤害，获得 +1 攻击力
		b.Card("CORE_EX1_604",
			on: When<DamageEvent>(ANY, ON, new Enchant(S.SELF, "CORE_EX1_604e"),
				when: TargetIsMinion()));

		// 货物保镖 CORE_SW_030 — 回合结束时获得 3 点护甲
		b.Card("CORE_SW_030",
			on: When<TurnEndEvent>(OWN, ON, new GainArmor(S.FRIENDLY_HERO, 3)));

		// 暴虐食尸鬼 CORE_OG_149 — 战吼：对所有其他随从造成 1 点伤害
		b.Card("CORE_OG_149",
			battlecry: new DealDamage(S.ALL_MINIONS - S.SELF, 1));

		// 钩拳-3000型 CORE_NX2_028 — 英雄攻击后，获得 4 护甲并抽牌
		b.Card("CORE_NX2_028",
			on: When<AfterAttackEvent>(OWN, AFTER,
				new TargetedAction[] { new GainArmor(S.FRIENDLY_HERO, 4), new DrawCard(S.CONTROLLER) },
				when: AttackerIsHero()));

		// 血蹄勇士 CORE_OG_218 — 嘲讽，受伤时 +3 攻击力
		b.Card("CORE_OG_218",
			enrage: new[] { new Refresh(S.SELF, buff: "CORE_OG_218e") });
		b.Buff("CORE_OG_218e", atk: 3);

		// 格罗玛什·地狱咆哮 CORE_EX1_414 — 冲锋，受伤时 +6 攻击力
		b.Card("CORE_EX1_414",
			enrage: new[] { new Refresh(S.SELF, buff: "CORE_EX1_414e") });
		b.Buff("CORE_EX1_414e", atk: 6);

		// ── 法术 ──

		// 斩杀 CORE_CS2_108 — 消灭一个受伤的敌方随从
		b.Card("CORE_CS2_108",
			play: new Destroy(S.TARGET),
			target: Target.Required().Enemy().Minion().Damaged());

		// 猛击 CORE_EX1_391 — 对一个随从造成 2 点伤害，若存活则抽牌
		b.Card("CORE_EX1_391",
			play: Acts(new DealDamage(S.TARGET, 2),
				IfElse(new Find(S.TARGET), new DrawCard(S.CONTROLLER), NOTHING)),
			target: Target.Required().Minion());

		// 盾牌猛击 CORE_EX1_410 — 你每有 1 点护甲值，对一个随从造成 1 点伤害
		b.Card("CORE_EX1_410",
			play: new DealDamage(S.TARGET, ARMOR(S.FRIENDLY_HERO)),
			target: Target.Required().Minion());

		// 盛气凌人 CORE_WON_350 — 发现一张嘲讽随从牌，使其获得 +1/+2
		b.Card("CORE_WON_350",
			play: MakeDiscover(S.CONTROLLER, DiscoverFilter.Minion().HasMechanic(Mechanic.TAUNT))
				.Then(new Enchant(S.TARGET, "CORE_WON_350e")));
		b.Buff("CORE_WON_350e", atk: 1, health: 2);

		// 怒袭 CORE_AT_064 — 造成 3 点伤害，获得 3 护甲
		b.Card("CORE_AT_064",
			play: Acts(new DealDamage(S.TARGET, 3), new GainArmor(S.FRIENDLY_HERO, 3)),
			target: Target.Required());

		// 盾牌格挡 CORE_EX1_606 — 获得 5 点护甲，抽一张牌
		b.Card("CORE_EX1_606",
			play: Acts(new GainArmor(S.FRIENDLY_HERO, 5), new DrawCard(S.CONTROLLER)));

		// 厚重板甲 CORE_SW_094 — 获得 8 点护甲
		b.Card("CORE_SW_094",
			play: new GainArmor(S.FRIENDLY_HERO, 8));

		// 绝命乱斗 CORE_EX1_407 — 随机保留一个随从，消灭其余
		b.Card("CORE_EX1_407",
			play: new Destroy(S.ALL_MINIONS - S.Random(S.ALL_MINIONS)));

		// ── 地标 ──

		// 赤红深渊 CORE_REV_990 — 对一个随从造成 1 点伤害并使其获得 +2 攻击力
		b.Card("CORE_REV_990",
			location: Acts(new DealDamage(S.TARGET, 1), new Enchant(S.TARGET, "CORE_REV_990e")),
			target: Target.Required().Minion());
		b.Buff("CORE_REV_990e", atk: 2);

		// ── 武器 ──

		// 炽炎战斧 CORE_CS2_106 — 无特效
		b.Card("CORE_CS2_106");

		// 埃辛诺斯壁垒 CORE_BT_781 — 英雄受伤时改为武器失去 1 耐久
		b.Card("CORE_BT_781",
			on: When<PreDamageEvent>(OWN, ON, new RedirectDamageToWeapon(),
				when: PreDamageTargetIsFriendlyHero()));
	}
}
