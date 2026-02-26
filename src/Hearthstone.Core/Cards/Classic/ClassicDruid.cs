using HearthstoneServer.Auras;
using HearthstoneServer.Events;
using static HearthstoneServer.Dsl.BusEvent;
using static HearthstoneServer.Cards.CardScriptUtils;
using HearthstoneServer.Targeting;

namespace HearthstoneServer.Dsl.Cards;

// ══════════════════════════════════════════════════════
// 经典德鲁伊卡牌脚本
// ══════════════════════════════════════════════════════

public class ClassicDruid : CardPack
{
	public override void Register(CardBuilder b)
	{
		// ── 附魔 ──
		b.Buff("CS2_005o", atk: 2,
			on: When<TurnEndEvent>(OWN, ON, new Destroy(S.SELF)));  // 爪击（本回合）
		b.Buff("CS2_009e", atk: 2, health: 2, extraTags: (GameTag.Taunt, true)); // 野性印记
		b.Buff("CS2_011o", atk: 2,
			on: When<TurnEndEvent>(OWN, ON, new Destroy(S.SELF)));  // 野蛮咆哮（本回合）
		b.Buff("EX1_155ae", atk: 4);                                    // 自然印记·攻击
		b.Buff("EX1_155be", health: 4, extraTags: (GameTag.Taunt, true)); // 自然印记·生命
		b.Buff("EX1_160be", atk: 1, health: 1);                         // 丛林之力
		b.Buff("EX1_178ae", health: 5, extraTags: (GameTag.Taunt, true)); // 战争古树·扎根
		b.Buff("EX1_178be", atk: 5);                                    // 战争古树·拔根
		b.Buff("EX1_183e", atk: 2, health: 2, extraTags: (GameTag.Taunt, true)); // 野性祝福
		b.Buff("EX1_570e", atk: 4,
			on: When<TurnEndEvent>(OWN, ON, new Destroy(S.SELF)));  // 撕咬（本回合）
		b.Buff("EX1_573ae", atk: 2, health: 2);                         // 塞纳留斯

		// ── 随从 ──

		// 利爪德鲁伊 — 抉择：冲锋；或 +2 生命并获得嘲讽
		b.Card("EX1_165",
			chooseCards: new[] { "EX1_165a", "EX1_165b" },
			play: new Transform(S.SELF, "OG_044a"));
		b.Card("EX1_165a", play: new Transform(S.SELF, "EX1_165t1"));
		b.Card("EX1_165b", play: new Transform(S.SELF, "EX1_165t2"));

		// 丛林守护者 — 抉择：造成 2 点伤害；或沉默一个随从
		b.Card("EX1_166",
			chooseCards: new[] { "EX1_166a", "EX1_166b" },
			play: Acts(new DealDamage(S.TARGET, 2), new Silence(S.TARGET)),
			target: Target.IfAvailable());
		b.Card("EX1_166a",
			play: new DealDamage(S.TARGET, 2),
			target: Target.Required());
		b.Card("EX1_166b",
			play: new Silence(S.TARGET),
			target: Target.Required().Minion());

		// 战争古树 — 抉择：+5 攻击力；或 +5 生命值并获得嘲讽
		b.Card("EX1_178",
			chooseCards: new[] { "EX1_178b", "EX1_178a" },
			play: Acts(new Enchant(S.SELF, "EX1_178ae"), new Enchant(S.SELF, "EX1_178be")));
		b.Card("EX1_178a", play: new Enchant(S.SELF, "EX1_178ae"));
		b.Card("EX1_178b", play: new Enchant(S.SELF, "EX1_178be"));

		// 塞纳留斯 — 抉择：+2/+2；或召唤两个 2/2 嘲讽树人
		b.Card("EX1_573",
			chooseCards: new[] { "EX1_573a", "EX1_573b" },
			play: Acts(new Enchant(S.FRIENDLY_MINIONS - S.SELF, "EX1_573ae"),
				new Summon(S.CONTROLLER, "EX1_573t"), new Summon(S.CONTROLLER, "EX1_573t")));
		b.Card("EX1_573a", play: new Enchant(S.FRIENDLY_MINIONS - S.SELF, "EX1_573ae"));
		b.Card("EX1_573b",
			play: new Summon(S.CONTROLLER, "EX1_573t") * 2,
			target: TargetRequirement.None);

		// 知识古树 — 抉择：抽一张牌；或恢复 5 点生命值
		b.Card("NEW1_008",
			chooseCards: new[] { "NEW1_008a", "NEW1_008b" },
			play: Acts(new DrawCard(S.CONTROLLER), new RestoreHealth(S.TARGET, 5)),
			target: Target.IfAvailable());
		b.Card("NEW1_008a", play: new DrawCard(S.CONTROLLER));
		b.Card("NEW1_008b",
			play: new RestoreHealth(S.TARGET, 5),
			target: Target.Required());

		// ── 法术 ──

		// 爪击 — +2 攻击力（本回合），+2 护甲
		b.Card("CS2_005", play: Acts(new Enchant(S.FRIENDLY_HERO, "CS2_005o"), new GainArmor(S.FRIENDLY_HERO, 2)));

		// 治疗之触 — 恢复 8 点生命值
		b.Card("CS2_007",
			play: new RestoreHealth(S.TARGET, 8),
			target: Target.Required());

		// 月火术 — 造成 1 点伤害
		b.Card("CS2_008",
			play: new DealDamage(S.TARGET, 1),
			target: Target.Required());

		// 野性印记 — +2/+2 并获得嘲讽
		b.Card("CS2_009",
			play: new Enchant(S.TARGET, "CS2_009e"),
			target: Target.Required().Minion());

		// 野蛮咆哮 — 所有友方角色 +2 攻击力（本回合）
		b.Card("CS2_011", play: new Enchant(S.FRIENDLY_CHARACTERS, "CS2_011o"));

		// 横扫 — 对目标造成 4 点伤害，对其余敌方角色造成 1 点伤害
		b.Card("CS2_012",
			play: Acts(new DealDamage(S.TARGET, 4), new DealDamage(S.ENEMY_CHARACTERS - S.TARGET, 1)),
			target: Target.Required().Enemy());

		// 野性成长 — 获得一个空的法力水晶（满则给过量法力）
		b.Card("CS2_013", play: IfElse(MANA_CAPPED,
			new AddToHand(S.CONTROLLER, "CS2_013t"),
			GainEmptyMana(S.CONTROLLER, 1)));

		// 过量法力 — 抽一张牌
		b.Card("CS2_013t", play: new DrawCard(S.CONTROLLER));

		// 愤怒 — 抉择：3 点伤害；或 1 点伤害并抽一张牌
		b.Card("EX1_154",
			chooseCards: new[] { "EX1_154a", "EX1_154b" },
			play: Acts(new DealDamage(S.TARGET, 3), new DealDamage(S.TARGET, 1), new DrawCard(S.CONTROLLER)),
			target: Target.Required().Minion());
		b.Card("EX1_154a",
			play: new DealDamage(S.TARGET, 3),
			target: Target.Required().Minion());
		b.Card("EX1_154b",
			play: Acts(new DealDamage(S.TARGET, 1), new DrawCard(S.CONTROLLER)),
			target: Target.Required().Minion());

		// 自然印记 — 抉择：+4 攻击力；或 +4 生命值并获得嘲讽
		b.Card("EX1_155",
			chooseCards: new[] { "EX1_155a", "EX1_155b" },
			play: Acts(new Enchant(S.TARGET, "EX1_155ae"), new Enchant(S.TARGET, "EX1_155be")),
			target: Target.Required().Minion());
		b.Card("EX1_155a",
			play: new Enchant(S.TARGET, "EX1_155ae"),
			target: Target.Required().Minion());
		b.Card("EX1_155b",
			play: new Enchant(S.TARGET, "EX1_155be"),
			target: Target.Required().Minion());

		// 从林之魂 — 给所有友方随从"亡语：召唤一个 2/2 树人"
		b.Card("EX1_158", play: new Enchant(S.FRIENDLY_MINIONS, "EX1_158e"));
		b.Card("EX1_158e",
			deathrattle: new Summon(S.CONTROLLER, "EX1_158t"));

		// 野性之力 — 抉择：+1/+1；或召唤一个 3/2 黑豹
		b.Card("EX1_160",
			chooseCards: new[] { "EX1_160a", "EX1_160b" },
			play: Acts(new Enchant(S.FRIENDLY_MINIONS, "EX1_160be"), new Summon(S.CONTROLLER, "EX1_160t")));
		b.Card("EX1_160a",
			play: new Summon(S.CONTROLLER, "EX1_160t"),
			target: TargetRequirement.None);
		b.Card("EX1_160b", play: new Enchant(S.FRIENDLY_MINIONS, "EX1_160be"));

		// 自然平衡 — 消灭一个随从，对手抽两张牌
		b.Card("EX1_161",
			play: Acts(new Destroy(S.TARGET), new DrawCard(S.OPPONENT), new DrawCard(S.OPPONENT)),
			target: Target.Required().Minion());

		// 滋养 — 抉择：获得 2 个法力水晶；或抽 3 张牌
		b.Card("EX1_164",
			chooseCards: new[] { "EX1_164a", "EX1_164b" },
			play: Acts(new GainMana(S.CONTROLLER, 2),
				new DrawCard(S.CONTROLLER), new DrawCard(S.CONTROLLER), new DrawCard(S.CONTROLLER)));
		b.Card("EX1_164a", play: new GainMana(S.CONTROLLER, 2));
		b.Card("EX1_164b", play: new DrawCard(S.CONTROLLER) * 3);

		// 激活 — 本回合获得 1 点法力水晶
		b.Card("EX1_169", play: new GainTemporaryMana(S.CONTROLLER, 1));

		// 星火术 — 造成 5 点伤害，抽一张牌
		b.Card("EX1_173",
			play: Acts(new DealDamage(S.TARGET, 5), new DrawCard(S.CONTROLLER)),
			target: Target.Required());

		// 撕咬 — +4 攻击力（本回合），+4 护甲
		b.Card("EX1_570", play: Acts(new Enchant(S.FRIENDLY_HERO, "EX1_570e"), new GainArmor(S.FRIENDLY_HERO, 4)));

		// 自然之力 — 召唤三个 2/2 树人
		b.Card("EX1_571",
			play: new Summon(S.CONTROLLER, "EX1_tk9") * 3,
			target: TargetRequirement.None);

		// 野性之击 — 对目标造成等于英雄攻击力的伤害
		b.Card("EX1_578",
			play: new DealDamage(S.TARGET, ATK(S.FRIENDLY_HERO)),
			target: Target.Required().Minion());

		// 星辰陨落 — 抉择：对一个随从造成 5 点伤害；或对所有敌方随从造成 2 点伤害
		b.Card("NEW1_007",
			chooseCards: new[] { "NEW1_007a", "NEW1_007b" },
			play: Acts(new DealDamage(S.TARGET, 5), new DealDamage(S.ENEMY_MINIONS, 2)),
			target: Target.IfAvailable().Minion());
		b.Card("NEW1_007a", play: new DealDamage(S.ENEMY_MINIONS, 2));
		b.Card("NEW1_007b",
			play: new DealDamage(S.TARGET, 5),
			target: Target.Required().Minion());

		// 野性赐福 — 给所有友方随从 +2/+2 和嘲讽
		b.Card("EX1_183", play: new Enchant(S.FRIENDLY_MINIONS, "EX1_183e"));
	}
}
