using HearthstoneServer.Auras;
using HearthstoneServer.Entities;
using HearthstoneServer.Events;
using static HearthstoneServer.Dsl.BusEvent;
using static HearthstoneServer.Cards.CardScriptUtils;
using static HearthstoneServer.Dsl.EventFilters;
using HearthstoneServer.Targeting;

namespace HearthstoneServer.Dsl.Cards;

// ══════════════════════════════════════════════════════
// 核心牧师卡牌脚本 (Core Set)
// ══════════════════════════════════════════════════════

public class CorePriest : CardPack
{
	public override void Register(CardBuilder b)
	{
		// ── 附魔 ──
		b.Buff("CORE_CS2_004e", health: 2); // 真言术：盾

		// ── 随从 ──

		// 心灵咒术师 CORE_EX1_193 — 战吼：复制对手牌库中的一张牌
		b.Card("CORE_EX1_193",
			battlecry: MakeDiscover(S.CONTROLLER, DiscoverFilter.All().FromEnemyDeck().AsCopy()));

		// 随船外科医师 CORE_WON_065 — 你召唤随从后，使其获得 +1 生命值
		b.Card("CORE_WON_065",
			on: When<MinionSummonedEvent>(OWN, ON, new Enchant(S.EVT_SOURCE, "CORE_WON_065e")));
		b.Buff("CORE_WON_065e", health: 1);

		// 暗影升腾者 CORE_ICC_210 — 回合结束时，随机使另一个友方随从获得 +1/+1
		b.Card("CORE_ICC_210",
			on: When<TurnEndEvent>(OWN, ON,
				new Enchant(S.Random(S.FRIENDLY_MINIONS - S.SELF), "CORE_ICC_210e")));
		b.Buff("CORE_ICC_210e", atk: 1, health: 1);

		// 拉祖尔女士 CORE_DAL_729 — 战吼：发现一张对手手牌的复制
		b.Card("CORE_DAL_729",
			battlecry: MakeDiscover(S.CONTROLLER, DiscoverFilter.All().FromEnemyHand().AsCopy()));

		// 灵魂向导 CORE_AV_328 — 嘲讽，亡语：抽一张神圣法术和暗影法术
		b.Card("CORE_AV_328",
			deathrattle: Acts(new DrawCard(S.CONTROLLER, SpellSchool.Holy),
				new DrawCard(S.CONTROLLER, SpellSchool.Shadow)));

		// 光沐元素 CORE_BAR_310 — 嘲讽，亡语：为所有友方角色恢复 8 点生命值
		b.Card("CORE_BAR_310",
			deathrattle: new RestoreHealth(S.FRIENDLY_CHARACTERS, 8));

		// 娜塔莉·塞林 CORE_EX1_198 — 战吼：消灭一个随从并获得其生命值
		b.Card("CORE_EX1_198",
			battlecry: new Destroy(S.TARGET)
				.Then(new Enchant(S.SELF, health: LazyNum.HEALTH)),
			target: Target.Required().Minion());

		// 黑曜石雕像 CORE_ICC_214 — 嘲讽、吸血，亡语：随机消灭一个敌方随从
		b.Card("CORE_ICC_214",
			deathrattle: new Destroy(S.RANDOM_ENEMY_MINION));

		// ── 法术 ──

		// 快速治疗 CORE_AT_055 — 恢复 5 点生命值
		b.Card("CORE_AT_055",
			play: new RestoreHealth(S.TARGET, 5),
			target: Target.Required());

		// 神圣惩击 CORE_CS1_130 — 对一个随从造成 3 点伤害
		b.Card("CORE_CS1_130",
			play: new DealDamage(S.TARGET, 3),
			target: Target.Required().Minion());

		// 真言术：盾 CORE_CS2_004 — 使一个随从获得 +2 生命值，抽一张牌
		b.Card("CORE_CS2_004",
			play: Acts(new Enchant(S.TARGET, "CORE_CS2_004e"), new DrawCard(S.CONTROLLER)),
			target: Target.Required().Minion());

		// 暗中生长 CS3_028 — 从牌库发现一张法术牌
		b.Card("CS3_028",
			play: MakeDiscover(S.CONTROLLER, DiscoverFilter.Spell().FromDeck().AsDraw()));

		// 神圣新星 CORE_CS1_112 — 对所有敌方随从造成 2 点伤害，为所有友方角色恢复 2 点
		b.Card("CORE_CS1_112",
			play: Acts(new DealDamage(S.ENEMY_MINIONS, 2), new RestoreHealth(S.FRIENDLY_CHARACTERS, 2)));

		// 强效治疗药水 CORE_CFM_604 — 恢复 12 点生命值，抽一张牌
		b.Card("CORE_CFM_604",
			play: Acts(new RestoreHealth(S.TARGET, 12), new DrawCard(S.CONTROLLER)),
			target: Target.Required().Friendly());

		// 暗言术：毁 CORE_EX1_197 — 消灭所有攻击力≥5 的随从
		b.Card("CORE_EX1_197",
			play: new Destroy(S.ALL_MINIONS + (S.ATK >= 5)));

		// 虚空碎片 CORE_SW_442 — 吸血，造成 4 点伤害
		b.Card("CORE_SW_442",
			play: new DealDamage(S.TARGET, 4).Lifesteal(),
			target: Target.Required());

		// 圣光炸弹 CORE_GVG_008 — 对所有随从造成等同于其攻击力的伤害
		b.Card("CORE_GVG_008",
			play: new ForEach(S.ALL_MINIONS, new DealDamage(S.CURRENT_TARGET, LazyNum.ATK)));
	}
}
