using HearthstoneServer.Auras;
using HearthstoneServer.Events;
using static HearthstoneServer.Dsl.BusEvent;
using static HearthstoneServer.Cards.CardScriptUtils;
using HearthstoneServer.Targeting;
using static HearthstoneServer.Dsl.EventFilters;

namespace HearthstoneServer.Dsl.Cards;

// ═════════════════════════════════════════
// 经典恶魔猎手卡牌脚本
// ═════════════════════════════════════════

public class ClassicDemonhunter : CardPack
{
	public override void Register(CardBuilder b)
	{
		// ── 附魔 ──
		b.Buff("BT_142e", atk: 1,
			on: When<TurnEndEvent>(OWN, ON, new Destroy(S.SELF)));     // 影蹄屠杀者
		b.Buff("BT_035e", atk: 2,
			on: When<TurnEndEvent>(OWN, ON, new Destroy(S.SELF)));     // 混乱打击
		b.Buff("BT_512e", atk: 8,
			on: When<TurnEndEvent>(OWN, ON, new Destroy(S.SELF)));     // 心魔

		// ── 随从 ──

		// 影蹄屠杀者 — 战吼：本回合你的英雄获得 +1 攻击力
		b.Card("BT_142", battlecry: new Enchant(S.CONTROLLER, "BT_142e"));

		// 盲眼监视者 — 战吼：检视你牌库中的 3 张牌，选择一张置于牌库顶
		b.Card("BT_323", battlecry: new ScryChoice(S.CONTROLLER, 3));

		// 萨特监工 — 你的英雄攻击后，召唤一个 2/2 的萨特
		b.Card("BT_352",
			on: When<AfterAttackEvent>(OWN, AFTER, new Summon(S.CONTROLLER, "BT_352t"),
				when: EventSourceIn<AfterAttackEvent>(S.FRIENDLY_HERO)));

		// 刃缚精锐 — 战吼：若你的英雄本回合已攻击，造成 4 点伤害
		b.Card("BT_495",
			battlecry: new DealDamage(S.TARGET, 4),
			target: Target.IfAvailable().WithHeroAttackedThisTurn());

		// ── 法术 ──

		// 混乱打击 — 本回合你的英雄获得 +2 攻击力，抽一张牌
		b.Card("BT_035", play: Acts(new Enchant(S.FRIENDLY_HERO, "BT_035e"), new DrawCard(S.CONTROLLER)));

		// 协同打击 — 召唤三个 1/1 的具有突袭的伊利达雷
		b.Card("BT_036", play: new Summon(S.CONTROLLER, "BT_036t") * 3);

		// 混乱新星 — 对所有随从造成 4 点伤害
		b.Card("BT_235", play: new DealDamage(S.ALL_MINIONS, 4));

		// 心魔 — 本回合你的英雄获得 +8 攻击力
		b.Card("BT_512", play: new Enchant(S.FRIENDLY_HERO, "BT_512e"));

		// 灵魂裂劈 — 吸血。对两个随机敌方随从造成 2 点伤害
		b.Card("BT_740",
			play: new DealDamage(new RandomSelector(S.ENEMY_MINIONS, 2), 2),
			target: Target.Condition().NeedsEnemyMinions(1));
		b.Card("GIFT_08"); // 伊利丹的礼物
	}
}
