using HearthstoneServer.Auras;
using HearthstoneServer.Entities;
using HearthstoneServer.Events;
using static HearthstoneServer.Dsl.BusEvent;
using static HearthstoneServer.Cards.CardScriptUtils;
using static HearthstoneServer.Dsl.EventFilters;
using static HearthstoneServer.Dsl.LazyNum;
using static HearthstoneServer.Dsl.RandomCards;
using HearthstoneServer.Targeting;

namespace HearthstoneServer.Dsl.Cards;

// ══════════════════════════════════════════════════════
// 核心法师卡牌脚本 (Core Set)
// ══════════════════════════════════════════════════════

public class CoreMage : CardPack
{
	public override void Register(CardBuilder b)
	{
		// ── 附魔 ──
		b.Buff("CORE_UNG_018t_e", atk: 0); // 烈焰喷涌（元素 token 无 buff）
		b.Buff("UNG_941e", cost: -2); // 远古雕文减费

		// ── 随从 ──

		// 奥术工匠 CORE_LOOT_231 — 每施放一个法术，获得等同于其法力值消耗的护甲值
		b.Card("CORE_LOOT_231",
			on: When<SpellCastEvent>(OWN, AFTER, new GainArmor(S.FRIENDLY_HERO, EVT_COST)));

		// 紫罗兰魔翼鸦 CORE_DRG_107 — 亡语：将一张"奥术飞弹"置入手牌
		b.Card("CORE_DRG_107",
			deathrattle: new AddToHand(S.CONTROLLER, "EX1_277"));

		// 呓语魔橱 CORE_EDR_001 — 战吼：随机将 2 张法师法术牌置入手牌
		b.Card("CORE_EDR_001",
			battlecry: new AddToHand(S.CONTROLLER, RandomCard().Spell().Class(CardClass.Mage)) * 2);

		// 大法师安东尼达斯 CORE_EX1_559 — 每施放一个法术，将一张火球术置入手牌
		b.Card("CORE_EX1_559",
			on: When<SpellCastEvent>(OWN, AFTER, new AddToHand(S.CONTROLLER, "CS2_029")));

		// 卡雷苟斯 CORE_DAL_609 — 战吼：发现一张法术牌。每回合你的第一个法术费用为 (0)
		b.Card("CORE_DAL_609",
			battlecry: MakeDiscover(S.CONTROLLER, DiscoverFilter.Spell()),
			update: new Refresh(S.FRIENDLY_HAND + S.SPELL, setTags: new() { { GameTag.Cost, 0 } },
				condition: -new Find(S.FRIENDLY_SPELLS_THIS_TURN)));

		// ── 法术 ──

		// 烈焰喷涌 CORE_UNG_018 — 造成 2 点伤害，将一张 1/2 元素置入手牌
		b.Card("CORE_UNG_018",
			play: Acts(new DealDamage(S.TARGET, 2), new AddToHand(S.CONTROLLER, "UNG_809t1")),
			target: Target.Required());

		// 寒冰箭 CORE_CS2_024 — 造成 3 点伤害并冻结
		b.Card("CORE_CS2_024",
			play: Acts(new DealDamage(S.TARGET, 3), Freeze(S.TARGET)),
			target: Target.Required());

		// 远古雕文 CORE_UNG_941 — 发现一张法术牌，减少 (2) 费
		b.Card("CORE_UNG_941",
			play: MakeDiscover(S.CONTROLLER, DiscoverFilter.Spell())
				.Then(new Enchant(S.CURRENT_TARGET, "UNG_941e")));

		// 奥术智慧 CORE_CS2_023 — 抽两张牌
		b.Card("CORE_CS2_023", play: new DrawCard(S.CONTROLLER) * 2);

		// 火球术 CORE_CS2_029 — 造成 6 点伤害
		b.Card("CORE_CS2_029",
			play: new DealDamage(S.TARGET, 6),
			target: Target.Required());

		// 暴风雪 CORE_CS2_028 — 对所有敌方随从造成 2 点伤害并冻结
		b.Card("CORE_CS2_028", play: Acts(new DealDamage(S.ENEMY_MINIONS, 2), Freeze(S.ENEMY_MINIONS)));

		// 烈焰风暴 CORE_CS2_032 — 对所有敌方随从造成 5 点伤害
		b.Card("CORE_CS2_032", play: new DealDamage(S.ENEMY_MINIONS, 5));

		// 火焰之地传送门 CORE_KAR_076 — 造成 6 点伤害，随机召唤一个 6 费随从
		b.Card("CORE_KAR_076",
			play: Acts(new DealDamage(S.TARGET, 6), new Summon(S.CONTROLLER, RandomCard().Minion().Cost(6))),
			target: Target.Required());

		// ── 奥秘 ──

		// 绿洲盟军 CORE_BAR_812 — 当一个友方随从受到攻击时，召唤一个 3/6 水元素
		b.Card("CORE_BAR_812",
			secret: When<AttackEvent>(ENEMY, ON,
				new Summon(S.CONTROLLER, "CS2_033"),
				when: EventTargetIn<AttackEvent>(S.ALL_MINIONS)));

		// 法术反制 CORE_EX1_287 — 反制敌方下一个法术
		b.Card("CORE_EX1_287",
			secret: When<SpellCastEvent>(ENEMY, ON,
				new Counter(S.EVT_SOURCE)));

		// 寒冰护体 CORE_EX1_289 — 当你的英雄受到攻击时，获得 8 点护甲
		b.Card("CORE_EX1_289",
			secret: When<AttackEvent>(ENEMY, ON,
				new GainArmor(S.FRIENDLY_HERO, 8),
				when: EventTargetIn<AttackEvent>(S.FRIENDLY_HERO)));

		// 爆炸符文 CORE_LOOT_101 — 对手打出随从后，对其造成 6 点伤害，溢出伤害由英雄承受
		b.Card("CORE_LOOT_101",
			secret: When<MinionPlayedEvent>(ENEMY, AFTER,
				new DealDamage(S.EVT_SOURCE, 6),
				new DealDamage(S.ENEMY_HERO, 6 - EVT_HEALTH)));
	}
}
