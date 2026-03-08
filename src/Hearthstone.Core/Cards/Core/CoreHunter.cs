using HearthstoneServer.Auras;
using HearthstoneServer.Events;
using static HearthstoneServer.Dsl.BusEvent;
using static HearthstoneServer.Cards.CardScriptUtils;
using static HearthstoneServer.Dsl.EventFilters;
using static HearthstoneServer.Dsl.RandomCards;
using HearthstoneServer.Targeting;

namespace HearthstoneServer.Dsl.Cards;

// ══════════════════════════════════════════════════════
// 核心猎人卡牌脚本 (Core Set)
// ══════════════════════════════════════════════════════

public class CoreHunter : CardPack
{
	public override void Register(CardBuilder b)
	{
		// ── 附魔 ──
		b.Buff("CORE_EX1_611e", cost: 2); // 冰冻陷阱额外费用
		b.Buff("CORE_AV_226e", cost: 1); // 冰霜陷阱额外费用

		// ── 随从 ──

		// 宝石鹦鹉 CORE_UNG_912 — 战吼：随机将一张野兽牌置入手牌
		b.Card("CORE_UNG_912",
			battlecry: new AddToHand(S.CONTROLLER, RandomCard().Minion().Race(Race.Beast)));

		// 恐鳞追猎者 CORE_UNG_800 — 战吼：触发一个友方随从的亡语
		b.Card("CORE_UNG_800",
			battlecry: new DeathrattleAction(S.TARGET),
			target: Target.IfAvailable().Friendly().Minion().Mechanic(Mechanic.DEATHRATTLE));

		// 巴拉克·科多班恩 CORE_BAR_551 — 战吼：抽取费用 1/2/3 的法术各一张
		b.Card("CORE_BAR_551",
			battlecry: Acts(
				new DrawCard(S.CONTROLLER, DrawFilter.Spell().Cost(1)),
				new DrawCard(S.CONTROLLER, DrawFilter.Spell().Cost(2)),
				new DrawCard(S.CONTROLLER, DrawFilter.Spell().Cost(3))));

		// 维拉努斯 CORE_DRG_095 — 战吼：将所有敌方随从生命值变为 1
		b.Card("CORE_DRG_095",
			battlecry: new Enchant(S.ENEMY_MINIONS, "CORE_DRG_095e"));
		b.Buff("CORE_DRG_095e", maxHealthMod: SetTo(1));

		// 山岭野熊 CORE_AV_337 — 嘲讽，亡语：召唤两只 2/4 嘲讽山熊宝宝
		b.Card("CORE_AV_337",
			deathrattle: new Summon(S.CONTROLLER, "AV_337t") * 2);

		// ── 法术 ──

		// 击伤猎物 CORE_BAR_801 — 造成 1 点伤害，召唤 1/1 突袭土狼
		b.Card("CORE_BAR_801",
			play: Acts(new DealDamage(S.TARGET, 1), new Summon(S.CONTROLLER, "BAR_801t")),
			target: Target.Required());

		// 追踪术 CORE_DS1_184 — 从牌库顶发现一张牌并抽取
		b.Card("CORE_DS1_184",
			play: MakeDiscover(S.CONTROLLER, DiscoverFilter.All().FromDeck().AsDraw()));

		// 奥术射击 CORE_DS1_185 — 造成 2 点伤害
		b.Card("CORE_DS1_185",
			play: new DealDamage(S.TARGET, 2),
			target: Target.Required());

		// 快速射击 CORE_BRM_013 — 造成 3 点伤害，手牌为空则抽牌
		b.Card("CORE_BRM_013",
			play: Acts(new DealDamage(S.TARGET, 3),
				IfElse(EMPTY_HAND, new DrawCard(S.CONTROLLER), NOTHING)),
			target: Target.Required());

		// 天降蛛群 CORE_AT_062 — 召唤三只 1/1 结网蛛（亡语：随机获取野兽牌）
		b.Card("CORE_AT_062",
			play: new Summon(S.CONTROLLER, "FP1_011") * 3);

		// 致命射击 CORE_EX1_617 — 随机消灭一个敌方随从
		b.Card("CORE_EX1_617",
			play: new Destroy(S.RANDOM_ENEMY_MINION),
			target: Target.Condition().NeedsEnemyMinions(1));

		// 动物伙伴 CORE_NEW1_031 — 随机召唤一个野兽伙伴
		b.Card("CORE_NEW1_031",
			play: new Summon(S.CONTROLLER, new RandomCardPicker()),
			entourage: new[] { "NEW1_032", "NEW1_033", "NEW1_034" });

		// 主人的召唤 CORE_TRL_339 — 发现一张随从牌
		b.Card("CORE_TRL_339",
			play: MakeDiscover(S.CONTROLLER, DiscoverFilter.Minion()));

		// ── 奥秘 ──

		// 冰霜陷阱 CORE_AV_226 — 对手施放法术时，将其移回手牌，费用+1
		b.Card("CORE_AV_226",
			secret: When<SpellCastEvent>(ENEMY, ON,
				new ReturnToHand(S.EVT_SOURCE),
				new Enchant(S.EVT_SOURCE, "CORE_AV_226e")));

		// 爆炸陷阱 CORE_EX1_610 — 英雄被攻击时，对所有敌人造成 2 点伤害
		b.Card("CORE_EX1_610",
			secret: When<AttackEvent>(ENEMY, ON,
				new DealDamage(S.ENEMY_CHARACTERS, 2),
				when: EventTargetIn<AttackEvent>(S.FRIENDLY_HERO)));

		// 冰冻陷阱 CORE_EX1_611 — 敌方随从攻击时将其移回手牌，费用+2
		b.Card("CORE_EX1_611",
			secret: When<AttackEvent>(ENEMY, ON,
				new TargetedAction[] {
					new ReturnToHand(S.EVT_SOURCE),
					new Enchant(S.EVT_SOURCE, "CORE_EX1_611e")
				},
				when: EventSourceIn<AttackEvent>(S.ALL_MINIONS)));

		// 捕鼠陷阱 CORE_GIL_577 — 对手一回合打出三张牌后，召唤 6/6 老鼠
		b.Card("CORE_GIL_577",
			secret: When<CardPlayedEvent>(ENEMY, AFTER,
				new Summon(S.CONTROLLER, "GIL_577t"),
				when: new Find(S.ENEMY_CARDS_PLAYED_THIS_TURN, 3)));
	}
}
