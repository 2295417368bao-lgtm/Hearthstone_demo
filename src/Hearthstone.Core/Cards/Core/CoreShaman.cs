using HearthstoneServer.Auras;
using HearthstoneServer.Events;
using static HearthstoneServer.Dsl.BusEvent;
using static HearthstoneServer.Cards.CardScriptUtils;
using static HearthstoneServer.Dsl.EventFilters;
using static HearthstoneServer.Dsl.RandomCards;
using HearthstoneServer.Targeting;

namespace HearthstoneServer.Dsl.Cards;

// ══════════════════════════════════════════════════════
// 核心萨满卡牌脚本 (Core Set)
// ══════════════════════════════════════════════════════

public class CoreShaman : CardPack
{
	public override void Register(CardBuilder b)
	{
		// ── 附魔 ──
		b.Buff("CORE_WC_042e", atk: 1); // 哀嚎蒸汽
		b.Buff("CORE_EX1_565o", atk: 2); // 火舌图腾（光环）

		// ── 随从 ──

		// 哀嚎蒸汽 CORE_WC_042 — 使用元素牌后获得 +1 攻击力
		b.Card("CORE_WC_042",
			on: When<CardPlayedEvent>(OWN, AFTER, new Enchant(S.SELF, "CORE_WC_042e"),
				when: EventCardIn<CardPlayedEvent>(S.Race(Race.Elemental))));

		// 电击学徒 CS3_007 — 法术伤害+1，过载：(1)（纯属性）
		b.Card("CS3_007");

		// 火舌图腾 CORE_EX1_565 — 相邻随从拥有 +2 攻击力
		b.Card("CORE_EX1_565", update: new[]
		{
			new Refresh(S.SELF_ADJACENT, buff: "CORE_EX1_565o")
		});

		// 凶恶的雨云 CORE_BOT_533 — 战吼：随机将一张元素牌置入手牌
		b.Card("CORE_BOT_533",
			battlecry: new AddToHand(S.CONTROLLER, RandomElemental()));

		// 沼泽之子 CORE_BT_115 — 战吼：如果你上回合施放过法术，发现一张法术牌
		b.Card("CORE_BT_115",
			battlecry: new ConditionalAction(
				new Find(S.FRIENDLY_SPELLS_LAST_TURN),
				new Action[] { MakeDiscover(S.CONTROLLER, DiscoverFilter.Spell()) }));

		// 锦鱼人水语者 CORE_CFM_061 — 战吼：恢复 6 点生命值，过载：(1)（过载由引擎自动处理）
		b.Card("CORE_CFM_061",
			battlecry: new RestoreHealth(S.TARGET, 6),
			target: Target.Required());

		// 土元素 CORE_EX1_250 — 嘲讽，过载：(2)（纯属性）
		b.Card("CORE_EX1_250");

		// 卡格瓦 CORE_TRL_345 — 战吼：将你上回合使用的法术复制回手牌
		b.Card("CORE_TRL_345",
			battlecry: new ForEach(S.FRIENDLY_SPELLS_LAST_TURN,
				new AddToHand(S.CONTROLLER, S.CURRENT_TARGET)));

		// 火元素 CORE_CS2_042 — 战吼：造成 4 点伤害
		b.Card("CORE_CS2_042",
			battlecry: new DealDamage(S.TARGET, 4),
			target: Target.Required());

		// 风领主奥拉基尔 CORE_NEW1_010 — 冲锋，圣盾，嘲讽，风怒（纯属性）
		b.Card("CORE_NEW1_010");

		// ── 法术 ──

		// 流电爆裂 CORE_BOT_451 — 召唤两个 1/1 突袭火花，过载：(1)
		b.Card("CORE_BOT_451",
			play: new Summon(S.CONTROLLER, "BOT_102t") * 2);

		// 闪电箭 CORE_EX1_238 — 造成 3 点伤害，过载：(1)（过载由引擎自动处理）
		b.Card("CORE_EX1_238",
			play: new DealDamage(S.TARGET, 3),
			target: Target.Required());

		// 炽焰祈咒 CORE_GIL_836 — 发现一张战吼随从牌
		b.Card("CORE_GIL_836",
			play: MakeDiscover(S.CONTROLLER, DiscoverFilter.Minion().HasMechanic(Mechanic.BATTLECRY)));

		// 视界术 CORE_CS2_053 — 抽一张牌，费用减少 (3)
		b.Buff("CS2_053e", cost: -3);
		b.Card("CORE_CS2_053",
			play: new DrawCard(S.CONTROLLER)
				.Then(new Enchant(S.CURRENT_TARGET, "CS2_053e")));

		// 妖术 CORE_EX1_246 — 将一个随从变为 0/1 青蛙
		b.Card("CORE_EX1_246",
			play: new Transform(S.TARGET, "hexfrog"),
			target: Target.Required().Minion());

		// 野性狼魂 CORE_EX1_248 — 召唤两只 2/3 嘲讽幽灵狼，过载：(1)
		b.Card("CORE_EX1_248",
			play: new Summon(S.CONTROLLER, "EX1_tk11") * 2);

		// 闪电风暴 CORE_EX1_259 — 对所有敌方随从造成 3 点伤害，过载：(1)（过载由引擎自动处理）
		b.Card("CORE_EX1_259",
			play: new DealDamage(S.ENEMY_MINIONS, 3));
	}
}
