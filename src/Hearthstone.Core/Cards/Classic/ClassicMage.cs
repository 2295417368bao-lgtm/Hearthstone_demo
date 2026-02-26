using HearthstoneServer.Auras;
using HearthstoneServer.Events;
using static HearthstoneServer.Dsl.BusEvent;
using static HearthstoneServer.Cards.CardScriptUtils;
using static HearthstoneServer.Dsl.EventFilters;
using HearthstoneServer.Targeting;

namespace HearthstoneServer.Dsl.Cards;

// ═════════════════════════════════════════
// 经典法师卡牌脚本
// ═════════════════════════════════════════

public class ClassicMage : CardPack
{
	public override void Register(CardBuilder b)
	{
		// ── 附魔 ──
		b.Buff("CS2_028e", atk: 1); // 暴风雪（简化版）

		// ── 随从 ──

		// 法力浮龙：每当你施放一个法术，获得 +1 攻击力
		b.Card("NEW1_012",
			on: When<SpellCastEvent>(OWN, AFTER, new Enchant(S.SELF, "NEW1_012o")));
		b.Buff("NEW1_012o", atk: 1);

		// 巫师学徒：你的法术消耗减少 (1) 点
		b.Card("EX1_608", update: new[]
		{
			new Refresh(S.FRIENDLY_HAND + S.SPELL - S.SELF, buff: "EX1_608e")
		});
		b.Buff("EX1_608e", extraTags: (GameTag.Cost, -1));

		// 虚空行者：在回合结束时若你有奥秘，获得 +2/+2
		b.Card("EX1_274",
			on: When<TurnEndEvent>(OWN, ON,
				new TargetedAction[] { new Enchant(S.SELF, "EX1_274e") },
				when: new Find(S.FRIENDLY_SECRETS)));
		b.Buff("EX1_274e", atk: 2, health: 2);

		// 肯瑞托法师：战吼：你的下一个奥秘消耗为 (0)
		b.Card("EX1_612",
			battlecry: new Enchant(S.Random(S.FRIENDLY_HAND + S.SECRET), "EX1_612o"));
		b.Buff("EX1_612o", extraTags: (GameTag.Cost, -100));

		// 水元素：对该随从造成伤害的角色被冻结（仅属性）

		// 大法师安东尼达斯：每当你施放一个法术，将一张"火球术"置入你的手牌
		b.Card("EX1_559",
			on: When<SpellCastEvent>(OWN, AFTER, new AddToHand(S.CONTROLLER, "CS2_029")));

		// ── 法术 ──

		// 奥术飞弹：随机造成 3 点伤害（每次 1 点）
		b.Card("EX1_277", play: new DealDamage(S.RANDOM_ENEMY_CHARACTER, 1) * 3);

		// 镜像：召唤两个 0/2 的具有嘲讽的随从
		b.Card("CS2_027", play: new Summon(S.CONTROLLER, "CS2_mirror") * 2);

		// 魔爆术：对所有敌方随从造成 1 点伤害
		b.Card("CS2_025", play: new DealDamage(S.ENEMY_MINIONS, 1));

		// 寒冰箭：造成 3 点伤害，冻结目标
		b.Card("CS2_024",
			play: Acts(new DealDamage(S.TARGET, 3), Freeze(S.TARGET)),
			target: Target.Required());

		// 奥术智慧：抽两张牌
		b.Card("CS2_023", play: new DrawCard(S.CONTROLLER) * 2);

		// 冰霜新星：冻结所有敌方随从
		b.Card("CS2_026", play: Freeze(S.ENEMY_MINIONS));

		// 火球术：造成 6 点伤害
		b.Card("CS2_029",
			play: new DealDamage(S.TARGET, 6),
			target: Target.Required());

		// 变形术：将一个随从变形为 1/1 的绵羊
		b.Card("CS2_022",
			play: new Transform(S.TARGET, "CS2_tk1"),
			target: Target.Required().Minion());

		// 暴风雪：对所有敌方随从造成 2 点伤害并冻结
		b.Card("CS2_028", play: Acts(new DealDamage(S.ENEMY_MINIONS, 2), Freeze(S.ENEMY_MINIONS)));

		// 烈焰风暴：对所有敌方随从造成 4 点伤害
		b.Card("CS2_032", play: new DealDamage(S.ENEMY_MINIONS, 4));

		// 炎爆术：造成 10 点伤害
		b.Card("EX1_279",
			play: new DealDamage(S.TARGET, 10),
			target: Target.Required());

		// 冰锥术：冻结目标及相邻随从，造成 1 点伤害
		b.Card("EX1_275",
			play: Acts(Freeze(S.TARGET), Freeze(S.TARGET_ADJACENT),
				new DealDamage(S.TARGET, 1), new DealDamage(S.TARGET_ADJACENT, 1)),
			target: Target.Required().Minion());

		// ── 奥秘 ──

		// 镜像实体：对手打出随从后，召唤一个它的复制
		b.Card("EX1_294",
			secret: When<MinionPlayedEvent>(ENEMY, AFTER,
				new SummonCopy(S.CONTROLLER, S.EVT_SOURCE)));

		// 法术反制：反制敌方下一个法术
		b.Card("EX1_287",
			secret: When<SpellCastEvent>(ENEMY, ON,
				new Counter(S.EVT_SOURCE)));

		// 蒸发：消灭攻击你英雄的敌方随从
		b.Card("EX1_594",
			secret: When<AttackEvent>(ENEMY, ON,
				new Destroy(S.EVT_SOURCE),
				when: And(AttackerIsMinion(), DefenderIsHero())));

		// 寒冰屏障：英雄被攻击时获得 8 点护甲
		b.Card("EX1_289",
			secret: When<AttackEvent>(ENEMY, ON,
				new GainArmor(S.FRIENDLY_HERO, 8),
				when: DefenderIsHero()));

		// 寒冰屏障：英雄受到致命伤害时免疫（简化版 — 仅揭示）
		b.Card("EX1_295",
			secret: When<DamageEvent>(ANY, ON,
				new TargetedAction[] { },
				when: TargetIsHero()));

		// 法术弯曲者：敌方施放指向法术时，召唤 1/3 作为新目标
		b.Card("tt_010",
			secret: When<SpellCastEvent>(ENEMY, ON,
				new Summon(S.CONTROLLER, "tt_010a"),
				when: SpellHasTarget()));
	}
}
