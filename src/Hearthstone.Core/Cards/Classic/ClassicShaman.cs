using HearthstoneServer.Auras;
using HearthstoneServer.Events;
using static HearthstoneServer.Dsl.BusEvent;
using static HearthstoneServer.Cards.CardScriptUtils;
using HearthstoneServer.Targeting;

namespace HearthstoneServer.Dsl.Cards;

// ═════════════════════════════════════════
// 经典萨满卡牌脚本
// ═════════════════════════════════════════

public class ClassicShaman : CardPack
{
	public override void Register(CardBuilder b)
	{
		// ── 附魔 ──
		b.Buff("CS2_045e", atk: 1,
			on: When<TurnEndEvent>(OWN, ON, new Destroy(S.SELF)));  // 石化武器（本回合）
		b.Buff("CS2_041e", atk: 3);                           // 先祖治疗（简化版）
		b.Buff("EX1_565o", atk: 2);                           // 火舌图腾
		b.Buff("EX1_587e", atk: 3, health: 3);                // 风语者（简化版）
		b.Buff("CS2_038e", atk: 2);                           // 先祖之灵（简化版）

		// ── 随从 ──

		// 火舌图腾：相邻随从获得 +2 攻击力
		b.Card("EX1_565", update: new[]
		{
			new Refresh(S.SELF_ADJACENT, buff: "EX1_565o")
		});

		// 法力之潮图腾：回合结束时抽一张牌
		b.Card("EX1_575",
			on: When<TurnEndEvent>(OWN, ON, new DrawCard(S.CONTROLLER)));

		// 无羁元素：每当你过载时获得 +1/+1
		b.Card("EX1_258",
			on: When<OverloadEvent>(OWN, ON, new Enchant(S.SELF, "EX1_258e")));
		b.Buff("EX1_258e", atk: 1, health: 1);

		// 火元素：战吼：造成 3 点伤害
		b.Card("CS2_042",
			battlecry: new DealDamage(S.TARGET, 3),
			target: Target.IfAvailable());

		// 奥拉基尔：冲锋、圣盾、嘲讽、风怒（仅属性）

		// 土元素：嘲讽，过载 (3)（仅属性）

		// ── 法术 ──

		// 石化武器：本回合 +3 攻击力
		b.Card("CS2_045",
			play: new Enchant(S.TARGET, "CS2_045e"),
			target: Target.Required().Friendly());

		// 先祖治疗：恢复满血并获得嘲讽
		b.Card("CS2_041",
			play: Acts(new FullRestore(S.TARGET), new Enchant(S.TARGET, "CS2_041e")),
			target: Target.Required().Minion());
		b.Buff("CS2_041e_taunt", extraTags: (GameTag.Taunt, true));

		// 闪电箭：造成 3 点伤害
		b.Card("EX1_238",
			play: new DealDamage(S.TARGET, 3),
			target: Target.Required());

		// 冰霜震击：造成 1 点伤害，冻结目标
		b.Card("CS2_037",
			play: Acts(new DealDamage(S.TARGET, 1), Freeze(S.TARGET)),
			target: Target.Required().Enemy());

		// 风怒：使一个随从获得风怒
		b.Card("CS2_039",
			play: GiveWindfury(S.TARGET),
			target: Target.Required().Minion());

		// 妖术：将一个随从变形为 0/1 的青蛙（嘲讽）
		b.Card("EX1_246",
			play: new Transform(S.TARGET, "hexfrog"),
			target: Target.Required().Minion());

		// 闪电风暴：对所有敌方随从造成 2-3 点伤害
		b.Card("EX1_259", play: new DealDamage(S.ENEMY_MINIONS, 2));

		// 熔岩爆裂：造成 5 点伤害
		b.Card("EX1_241",
			play: new DealDamage(S.TARGET, 5),
			target: Target.Required());

		// 野性狼魂：召唤两个 2/3 的具有嘲讽的精灵狼
		b.Card("EX1_248", play: new Summon(S.CONTROLLER, "EX1_tk11") * 2);

		// 嗜血：使所有友方随从获得 +3 攻击力
		b.Card("EX1_233", play: new Enchant(S.FRIENDLY_MINIONS, "EX1_233e"));
		b.Buff("EX1_233e", atk: 3);

		// 先知之识：抽一张牌，法力值消耗减少 (3) 点
		b.Card("CS2_053", play: new DrawCard(S.CONTROLLER));

		// 先祖之灵：亡语：重新召唤该随从
		b.Card("CS2_038",
			play: new Enchant(S.TARGET, "CS2_038e"),
			target: Target.Required().Minion());

		// 图腾之力：使所有图腾获得 +2 生命值
		b.Card("EX1_244", play: new Enchant(S.FRIENDLY_MINIONS + S.TOTEM, "EX1_244e"));
		b.Buff("EX1_244e", health: 2);

		// 叉状闪电：对 2 个随机敌方随从造成 2 点伤害
		b.Card("EX1_251",
			play: new DealDamage(new RandomSelector(S.ENEMY_MINIONS, 2), 2),
			target: Target.Condition().NeedsEnemyMinions(1));

		// ── 武器 ──

		// 风暴锻锤：2/3，过载 (1)（仅属性）

		// 毁灭之锤：2/8 风怒，过载 (2)（仅属性）
	}
}
