using HearthstoneServer.Auras;
using HearthstoneServer.Events;
using static HearthstoneServer.Dsl.BusEvent;
using static HearthstoneServer.Cards.CardScriptUtils;
using HearthstoneServer.Targeting;

namespace HearthstoneServer.Dsl.Cards;

// ═════════════════════════════════════════
// 经典盗贼卡牌脚本
// ═════════════════════════════════════════

public class ClassicRogue : CardPack
{
	public override void Register(CardBuilder b)
	{
		// ── 附魔 ──
		b.Buff("EX1_613e", atk: 2, health: 2);               // 艾德温·范克里夫
		b.Buff("NEW1_014e", extraTags: (GameTag.Stealth, true)); // 伪装大师
		b.Buff("CS2_073e", atk: 2);                           // 冷血
		b.Buff("CS2_073e2", atk: 4);                          // 冷血（连击）
		b.Buff("CS2_074e", atk: 2);                           // 致命药膏
		b.Buff("EX1_145o", atk: 3, health: 3);                // 伺机待发（未使用）
		b.Buff("EX1_128e", atk: 2);                           // 潜行

		// ── 随从 ──

		// 迪菲亚头目：连击：召唤一个 2/1 的强盗
		b.Card("EX1_131", combo: new Summon(S.CONTROLLER, "EX1_131t"));

		// 军情七处特工：连击：造成 2 点伤害
		b.Card("EX1_134",
			combo: new DealDamage(S.TARGET, 2),
			target: Target.ForCombo());

		// 艾德温·范克里夫：连击：每使用过一张牌获得 +2/+2
		b.Card("EX1_613", combo: new Enchant(S.SELF, "EX1_613e"));

		// 绑架者：连击：将一个随从移回其拥有者的手牌
		b.Card("NEW1_005",
			combo: new ReturnToHand(S.TARGET),
			target: Target.ForCombo().Minion());

		// 伪装大师：使一个友方随从获得潜行
		b.Card("NEW1_014",
			battlecry: new Enchant(S.TARGET - S.STEALTH, "NEW1_014e"),
			target: Target.IfAvailable().Minion().Friendly().NonSelf());
		// 伪装大师附魔：获得潜行，攻击后移除
		b.Card("NEW1_014e",
			on: When<AttackEvent>(SELF_TRIGGER, ON, new Destroy(S.SELF)));

		// 耐心的刺客：潜行、剧毒（仅属性）

		// ── 法术 ──

		// 背刺：对一个未受伤的随从造成 2 点伤害
		b.Card("CS2_072",
			play: new DealDamage(S.TARGET, 2),
			target: Target.Required().Minion());

		// 冷血：+2 攻击力，连击：改为 +4
		b.Card("CS2_073",
			play: new Enchant(S.TARGET, "CS2_073e"),
			combo: new Enchant(S.TARGET, "CS2_073e2"),
			target: Target.Required().Minion());

		// 致命药膏：武器 +2 攻击力
		b.Card("CS2_074", play: new Enchant(S.FRIENDLY_WEAPON, "CS2_074e"));

		// 影袭：对敌方英雄造成 3 点伤害
		b.Card("CS2_075", play: new DealDamage(S.ENEMY_HERO, 3));

		// 刺杀：消灭一个敌方随从
		b.Card("CS2_076",
			play: new Destroy(S.TARGET),
			target: Target.Required().Minion());

		// 疾跑：抽四张牌
		b.Card("CS2_077", play: new DrawCard(S.CONTROLLER) * 4);

		// 刀扇：消灭你的武器，对所有敌方随从造成等同于其攻击力的伤害
		b.Card("CS2_233",
			play: Acts(new Destroy(S.FRIENDLY_WEAPON), new DealDamage(S.ENEMY_MINIONS, ATK(S.FRIENDLY_WEAPON))));

		// 刀扇：对所有敌方随从造成 1 点伤害，抽一张牌
		b.Card("EX1_129", play: Acts(new DealDamage(S.ENEMY_MINIONS, 1), new DrawCard(S.CONTROLLER)));

		// 闷棍：将一个敌方随从移回其拥有者的手牌
		b.Card("EX1_581",
			play: new ReturnToHand(S.TARGET),
			target: Target.Required().Minion().Enemy());

		// 刺骨：造成 2 点伤害，连击：改为 4 点
		b.Card("EX1_124",
			play: new DealDamage(S.TARGET, 2),
			combo: new DealDamage(S.TARGET, 4),
			target: Target.Required());

		// 毒刃：造成 1 点伤害，抽一张牌
		b.Card("EX1_278",
			play: Acts(new DealDamage(S.TARGET, 1), new DrawCard(S.CONTROLLER)),
			target: Target.Required());

		// 消失：将所有随从移回其拥有者的手牌
		b.Card("NEW1_004", play: new ReturnToHand(S.ALL_MINIONS));

		// 割喉：对敌方英雄造成 2 点伤害，连击：移回手牌
		b.Card("EX1_137",
			play: new DealDamage(S.ENEMY_HERO, 2));

		// 背叛：一个随从对其相邻随从造成等同于其攻击力的伤害
		b.Card("EX1_126",
			play: new DealDamage(S.TARGET_ADJACENT, ATK(S.TARGET)),
			target: Target.Required().Minion().Enemy());

		// 暗影步：将一个友方随从移回手牌，法力值消耗减少 (2) 点
		b.Card("EX1_144",
			play: new ReturnToHand(S.TARGET),
			target: Target.Required().Minion().Friendly());

		// 伺机待发：你的下一个法术消耗减少 (3) 点
		b.Card("EX1_145", play: new Enchant(S.CONTROLLER, "EX1_145o"));

		// ── 武器 ──

		// 堕落之刃：连击：改为造成 2 点伤害
		b.Card("EX1_133",
			play: new DealDamage(S.TARGET, 1),
			combo: new DealDamage(S.TARGET, 2),
			target: Target.IfAvailable());

		// 刺客之刃：3/4 武器（仅属性）
	}
}
