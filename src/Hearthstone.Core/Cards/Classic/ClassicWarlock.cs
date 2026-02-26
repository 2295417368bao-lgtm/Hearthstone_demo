using HearthstoneServer.Auras;

using static HearthstoneServer.Cards.CardScriptUtils;
using HearthstoneServer.Targeting;

namespace HearthstoneServer.Dsl.Cards;

// ═════════════════════════════════════════
// 经典术士卡牌脚本
// ═════════════════════════════════════════

public class ClassicWarlock : CardPack
{
	public override void Register(CardBuilder b)
	{
		// ── 附魔 ──
		b.Buff("EX1_596e", atk: 3, health: 3);   // 恶魔之火
		b.Buff("CS2_064e", atk: 4, health: 4);   // 恶魔之心（增益）
		b.Buff("EX1_309e", atk: 2, health: 2);   // 灵魂虹吸（简化版）

		// ── 随从 ──

		// 烈焰小鬼：对你的英雄造成 3 点伤害
		b.Card("EX1_319", battlecry: new DealDamage(S.FRIENDLY_HERO, 3));

		// 深渊领主：对你的英雄造成 5 点伤害
		b.Card("EX1_313", battlecry: new DealDamage(S.FRIENDLY_HERO, 5));

		// 末日守卫：弃两张牌
		b.Card("EX1_310", battlecry: new Discard(S.CONTROLLER) * 2);

		// 魅魔：弃一张牌
		b.Card("EX1_306", battlecry: new Discard(S.CONTROLLER));

		// 虚空恐魔：消灭相邻随从，获得其属性
		b.Card("AT_021",
			battlecry: Acts(new Destroy(S.SELF_ADJACENT), new Enchant(S.SELF, "AT_021e")));
		b.Buff("AT_021e", atk: 0); // 运行时获得属性

		// 恶魔卫士：失去一个法力水晶
		b.Card("EX1_301", battlecry: new GainMana(S.CONTROLLER, -1));

		// ── 法术 ──

		// 灵魂之火：造成 4 点伤害，弃一张牌
		b.Card("EX1_308",
			play: Acts(new DealDamage(S.TARGET, 4), new Discard(S.CONTROLLER)),
			target: Target.Required());

		// 死亡缠绕：造成 1 点伤害，若目标死亡则抽一张牌
		b.Card("EX1_302",
			play: new DealDamage(S.TARGET, 1),
			target: Target.Required());

		// 暗影箭：造成 4 点伤害
		b.Card("CS2_057",
			play: new DealDamage(S.TARGET, 4),
			target: Target.Required().Minion());

		// 吸取生命：造成 2 点伤害，恢复英雄 2 点生命值
		b.Card("CS2_061",
			play: Acts(new DealDamage(S.TARGET, 2), new RestoreHealth(S.FRIENDLY_HERO, 2)),
			target: Target.Required());

		// 地狱烈焰：对所有角色造成 3 点伤害
		b.Card("CS2_062", play: new DealDamage(S.ALL_CHARACTERS, 3));

		// 腐蚀术：在你的回合开始时消灭该随从
		b.Card("CS2_063",
			play: new Enchant(S.TARGET, "CS2_063e"),
			target: Target.Required().Minion().Enemy());

		// 恶魔之火：对随从造成 2 点伤害，或使友方恶魔获得 +2/+2
		b.Card("EX1_596",
			play: Acts(new DealDamage(S.TARGET, 2), new Enchant(S.TARGET, "EX1_596e")),
			target: Target.Required().Minion());

		// 灵魂虹吸：消灭一个随从，恢复英雄 3 点生命值
		b.Card("EX1_309",
			play: Acts(new Destroy(S.TARGET), new RestoreHealth(S.FRIENDLY_HERO, 3)),
			target: Target.Required().Minion());

		// 扭曲虚空：消灭所有随从
		b.Card("EX1_312", play: new Destroy(S.ALL_MINIONS));

		// 末日灾祸：造成 2 点伤害；若目标死亡，召唤一个随机恶魔
		b.Card("EX1_320",
			play: new DealDamage(S.TARGET, 2),
			target: Target.Required());

		// 暗影烈焰：消灭一个友方随从，对所有敌方随从造成等同于其攻击力的伤害
		b.Card("EX1_303",
			play: Acts(new Destroy(S.TARGET), new DealDamage(S.ENEMY_MINIONS, ATK(S.TARGET))),
			target: Target.Required().Minion().Friendly());

		// 力量压倒：+4/+4，回合结束时死亡
		b.Card("EX1_316",
			play: new Enchant(S.TARGET, "EX1_316e"),
			target: Target.Required().Minion().Friendly());
		b.Buff("EX1_316e", atk: 4, health: 4);

		// 感知恶魔：从牌库中抽两个恶魔
		b.Card("EX1_317", play: new DrawCard(S.CONTROLLER) * 2);

		// 恐惧地狱火：对所有其他角色造成 1 点伤害
		b.Card("CS2_064", play: new DealDamage(S.ALL_CHARACTERS - S.SELF, 1));
	}
}
