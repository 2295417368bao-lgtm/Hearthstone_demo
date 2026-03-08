using HearthstoneServer.Auras;
using HearthstoneServer.Entities;
using HearthstoneServer.Events;
using static HearthstoneServer.Dsl.BusEvent;
using static HearthstoneServer.Cards.CardScriptUtils;
using static HearthstoneServer.Dsl.EventFilters;
using HearthstoneServer.Targeting;

namespace HearthstoneServer.Dsl.Cards;

// ══════════════════════════════════════════════════════
// 核心恶魔猎手卡牌脚本 (Core Set)
// ══════════════════════════════════════════════════════

public class CoreDemonHunter : CardPack
{
	public override void Register(CardBuilder b)
	{
		// ── 附魔 ──
		b.Buff("CORE_BT_035e", atk: 2,
			on: When<TurnEndEvent>(OWN, ON, new Destroy(S.SELF))); // 混乱打击（本回合）
		b.Buff("CS3_017e", atk: 3,
			on: When<TurnEndEvent>(OWN, ON, new Destroy(S.SELF))); // 甘尔葛（本回合）

		// ── 随从 ──

		// 火色魔印奔行者 CORE_BT_480 — 流放：抽一张牌
		b.Card("CORE_BT_480",
			outcast: new DrawCard(S.CONTROLLER));

		// 战斗邪犬 CORE_BT_351 — 你的英雄攻击后，获得 +1 攻击力
		b.Card("CORE_BT_351",
			on: When<AfterAttackEvent>(OWN, AFTER,
				new Enchant(S.SELF, "CORE_BT_351e"),
				when: EventSourceIn<AfterAttackEvent>(S.FRIENDLY_HERO)));
		b.Buff("CORE_BT_351e", atk: 1);

		// 游荡贤者 CORE_TSC_217 — 流放：使最左/最右手牌费用减少 (1)
		b.Card("CORE_TSC_217",
			outcast: Acts(
				new Enchant(S.LEFTMOST_HAND, "GBL_003e"),
				new Enchant(S.RIGHTMOST_HAND, "GBL_003e")));

		// 邪能响尾蛇 CORE_WC_701 — 突袭，亡语：对所有敌方随从造成 1 点伤害
		b.Card("CORE_WC_701",
			deathrattle: new DealDamage(S.ENEMY_MINIONS, 1));

		// 甘尔葛战刃铸造师 CS3_017 — 流放：本回合英雄 +3 攻击力
		b.Card("CS3_017",
			outcast: new Enchant(S.FRIENDLY_HERO, "CS3_017e"));

		// 凯恩·日怒 CORE_BT_187 — 冲锋，所有友方攻击无视嘲讽（光环）
		b.Card("CORE_BT_187",
			update: new Refresh(S.FRIENDLY_CHARACTERS, GameTag.IgnoreTaunt, 1));

		// 暴怒邪吼者 CORE_BT_416 — 战吼：下一张恶魔费用减少 (2)
		b.Card("CORE_BT_416",
			battlecry: new Enchant(S.CONTROLLER, "CORE_BT_416e"));
		b.Card("CORE_BT_416e",
			update: new[] { new Refresh(S.FRIENDLY_HAND + S.DEMON, new() { { GameTag.Cost, -2 } }) },
			on: When<CardPlayedEvent>(OWN, AFTER, new Destroy(S.SELF),
				when: EventCardIn<CardPlayedEvent>(S.Race(Race.Demon))));

		// 扎伊 CORE_DMF_231 — 战吼：复制最左和最右手牌
		b.Card("CORE_DMF_231",
			battlecry: Acts(
				new CopyToHand(S.CONTROLLER, S.LEFTMOST_HAND, keepBuffs: true, handIndex: 0),
				new CopyToHand(S.CONTROLLER, S.RIGHTMOST_HAND, keepBuffs: true, handIndex: -1)));

		// 愤怒的女祭司 CORE_BT_493 — 回合结束时造成 6 点伤害随机分配给敌人
		b.Card("CORE_BT_493",
			on: When<TurnEndEvent>(OWN, ON,
				new DealDamage(S.RANDOM_ENEMY_CHARACTER, 1),
				new DealDamage(S.RANDOM_ENEMY_CHARACTER, 1),
				new DealDamage(S.RANDOM_ENEMY_CHARACTER, 1),
				new DealDamage(S.RANDOM_ENEMY_CHARACTER, 1),
				new DealDamage(S.RANDOM_ENEMY_CHARACTER, 1),
				new DealDamage(S.RANDOM_ENEMY_CHARACTER, 1)));

		// 伊利达雷审判官 CS3_020 — 突袭，英雄攻击后本随从也攻击该敌人
		b.Card("CS3_020",
			on: When<AfterAttackEvent>(OWN, AFTER,
				new ForceAttack(S.SELF, S.EVT_TARGET),
				when: EventSourceIn<AfterAttackEvent>(S.FRIENDLY_HERO)));

		// ── 法术 ──

		// 伊利达雷研习 CORE_YOP_001 — 发现一张流放牌
		b.Card("CORE_YOP_001",
			play: MakeDiscover(S.CONTROLLER, DiscoverFilter.All().HasMechanic(Mechanic.OUTCAST)));

		// 混乱打击 CORE_BT_035 — 英雄 +2 攻击力（本回合），抽一张牌
		b.Card("CORE_BT_035",
			play: Acts(new Enchant(S.FRIENDLY_HERO, "CORE_BT_035e"), new DrawCard(S.CONTROLLER)));

		// 幽灵视觉 CORE_BT_491 — 抽一张牌，流放：再抽一张
		b.Card("CORE_BT_491",
			play: new DrawCard(S.CONTROLLER),
			outcast: new DrawCard(S.CONTROLLER));

		// 献祭光环 CORE_BT_514 — 对所有随从造成 1 点伤害两次
		b.Card("CORE_BT_514",
			play: new DealDamage(S.ALL_MINIONS, 1) * 2);

		// 眼棱 CORE_BT_801 — 吸血，造成 3 点伤害，流放：费用变为 (0)
		// 吸血由卡牌属性处理
		b.Card("CORE_BT_801",
			play: new DealDamage(S.TARGET, 3),
			target: Target.Required().Minion(),
			costModFunc: (self, cost) =>
			{
				// 流放减费：在最左或最右手牌位置时费用为 0
				if (self is BuffableEntity be && be.Zone == Zone.Hand && be.Controller != null)
				{
					var hand = be.Controller.Hand;
					if (hand.Count > 0 && (hand[0] == be || hand[^1] == be))
						return 0;
				}
				return cost;
			});

		// ── 武器 ──

		// 獠牙锥刃 CORE_BAR_330 — 亡语：抽一张亡语随从牌
		b.Card("CORE_BAR_330",
			deathrattle: new DrawCard(S.CONTROLLER, DrawFilter.Minion().HasMechanic(Mechanic.DEATHRATTLE)));

		// 奥达奇战刃 CORE_BT_921 — 吸血（纯属性）
		b.Card("CORE_BT_921");
	}
}
