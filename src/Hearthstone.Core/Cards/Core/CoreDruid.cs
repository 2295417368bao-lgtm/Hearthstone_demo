using HearthstoneServer.Auras;
using HearthstoneServer.Events;
using static HearthstoneServer.Dsl.BusEvent;
using static HearthstoneServer.Cards.CardScriptUtils;
using static HearthstoneServer.Dsl.EventFilters;
using HearthstoneServer.Targeting;

namespace HearthstoneServer.Dsl.Cards;

// ══════════════════════════════════════════════════════
// 核心德鲁伊卡牌脚本 (Core Set)
// ══════════════════════════════════════════════════════

public class CoreDruid : CardPack
{
	public override void Register(CardBuilder b)
	{
		// ── 附魔 ──
		b.Buff("CORE_CS2_009e", atk: 2, health: 3, extraTags: (GameTag.Taunt, true)); // 野性印记
		b.Buff("CORE_EX1_160be", atk: 1, health: 1); // 野性之力

		// ── 随从 ──

		// 活泼的松鼠 CORE_SW_439 — 亡语：将四张橡果洗入牌库
		b.Card("CORE_SW_439",
			deathrattle: new ShuffleIntoDeck(S.CONTROLLER, "SW_439t") * 4);

		// 厚皮科多兽 CORE_BAR_535 — 嘲讽，亡语：获得 5 点护甲
		b.Card("CORE_BAR_535",
			deathrattle: new GainArmor(S.FRIENDLY_HERO, 5));

		// 范达尔·鹿盔 CORE_OG_044 — 抉择牌同时拥有两种效果（光环）
		// TODO: Fandral 光环需要特殊引擎支持
		b.Card("CORE_OG_044");

		// 花园猎豹 CORE_SW_431 — 突袭，每当本随从攻击，英雄获得 +3 攻击力（本回合）
		b.Card("CORE_SW_431",
			on: When<AfterAttackEvent>(OWN, AFTER,
				new Enchant(S.FRIENDLY_HERO, "CORE_SW_431e"),
				when: SelfIsAttacker()));
		b.Buff("CORE_SW_431e", atk: 3,
			on: When<TurnEndEvent>(OWN, ON, new Destroy(S.SELF)));

		// 格雷布 CORE_DMF_734 — 嘲讽，亡语：随机使一个友方随从获得"亡语：召唤格雷布"
		b.Card("CORE_DMF_734",
			deathrattle: new Enchant(S.Random(S.FRIENDLY_MINIONS), "CORE_DMF_734e"));
		b.Buff("CORE_DMF_734e",
			deathrattle: new[] { (Action)new Summon(S.CONTROLLER, "CORE_DMF_734") });

		// ── 法术 ──

		// 激活 CORE_EX1_169 — 本回合获得一个法力水晶
		b.Card("CORE_EX1_169", play: new GainTemporaryMana(S.CONTROLLER, 1));

		// 活体根须 CORE_AT_037 — 抉择：造成 2 点伤害；或召唤两个 1/1 树苗
		b.Card("CORE_AT_037",
			chooseCards: new[] { "CORE_AT_037a", "CORE_AT_037b" });
		b.Card("CORE_AT_037a",
			play: new DealDamage(S.TARGET, 2),
			target: Target.Required());
		b.Card("CORE_AT_037b",
			play: new Summon(S.CONTROLLER, "AT_037t") * 2);

		// 女巫森林苹果 CORE_GIL_663 — 将两张 2/2 树人置入手牌
		b.Card("CORE_GIL_663",
			play: new AddToHand(S.CONTROLLER, "EX1_tk9") * 2);

		// 乌鸦神像 Core_LOE_115 — 抉择：发现一张随从牌；或发现一张法术牌
		b.Card("Core_LOE_115",
			chooseCards: new[] { "Core_LOE_115a", "Core_LOE_115b" });
		b.Card("Core_LOE_115a",
			play: MakeDiscover(S.CONTROLLER, DiscoverFilter.Minion()));
		b.Card("Core_LOE_115b",
			play: MakeDiscover(S.CONTROLLER, DiscoverFilter.Spell()));

		// 野性印记 CORE_CS2_009 — 使一个随从获得嘲讽和 +2/+3
		b.Card("CORE_CS2_009",
			play: new Enchant(S.TARGET, "CORE_CS2_009e"),
			target: Target.Required().Minion());

		// 愤怒 CORE_EX1_154 — 抉择：对一个随从造成 3 点伤害；或造成 1 点伤害并抽牌
		b.Card("CORE_EX1_154",
			chooseCards: new[] { "CORE_EX1_154a", "CORE_EX1_154b" },
			target: Target.Required().Minion());
		b.Card("CORE_EX1_154a",
			play: new DealDamage(S.TARGET, 3),
			target: Target.Required().Minion());
		b.Card("CORE_EX1_154b",
			play: Acts(new DealDamage(S.TARGET, 1), new DrawCard(S.CONTROLLER)),
			target: Target.Required().Minion());

		// 野性之力 CORE_EX1_160 — 抉择：召唤 3/2 猎豹；或 +1/+1 所有随从
		b.Card("CORE_EX1_160",
			chooseCards: new[] { "CORE_EX1_160a", "CORE_EX1_160b" });
		b.Card("CORE_EX1_160a",
			play: new Summon(S.CONTROLLER, "EX1_160t"));
		b.Card("CORE_EX1_160b", play: new Enchant(S.FRIENDLY_MINIONS, "CORE_EX1_160be"));

		// 横扫 CORE_CS2_012 — 对一个敌人造成 4 点伤害，对其余敌人造成 1 点伤害
		b.Card("CORE_CS2_012",
			play: Acts(new DealDamage(S.TARGET, 4), new DealDamage(S.ENEMY_CHARACTERS - S.TARGET, 1)),
			target: Target.Required().Enemy());

		// 野性之怒 CORE_OG_047 — 抉择：英雄 +4 攻击力（本回合）；或获得 8 点护甲
		b.Card("CORE_OG_047",
			chooseCards: new[] { "CORE_OG_047a", "CORE_OG_047b" });
		b.Card("CORE_OG_047a",
			play: new Enchant(S.FRIENDLY_HERO, "CORE_OG_047ae"));
		b.Buff("CORE_OG_047ae", atk: 4,
			on: When<TurnEndEvent>(OWN, ON, new Destroy(S.SELF)));
		b.Card("CORE_OG_047b",
			play: new GainArmor(S.FRIENDLY_HERO, 8));

		// 橡树的召唤 CORE_LOOT_309 — 获得 6 点护甲，从牌库召唤一个法力消耗≤4 的随从
		b.Card("CORE_LOOT_309",
			play: Acts(new GainArmor(S.FRIENDLY_HERO, 6), Recruit(S.MaxCost(4))));

		// 划水好友 CORE_TSC_650 — 抉择：召唤 6/6 虎鲸；或六只 1/1 海獭
		b.Card("CORE_TSC_650",
			chooseCards: new[] { "CORE_TSC_650a", "CORE_TSC_650b" });
		b.Card("CORE_TSC_650a",
			play: new Summon(S.CONTROLLER, "TSC_650t"));
		b.Card("CORE_TSC_650b",
			play: new Summon(S.CONTROLLER, "TSC_650t2") * 6);

		// 紧壳商品 CORE_SW_429 — 召唤两只 2/7 嘲讽龟
		b.Card("CORE_SW_429",
			play: new Summon(S.CONTROLLER, "SW_429t") * 2);
	}
}
