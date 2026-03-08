using HearthstoneServer.Auras;
using HearthstoneServer.Entities;
using HearthstoneServer.Events;
using static HearthstoneServer.Dsl.BusEvent;
using static HearthstoneServer.Cards.CardScriptUtils;
using static HearthstoneServer.Dsl.EventFilters;
using HearthstoneServer.Targeting;

namespace HearthstoneServer.Dsl.Cards;

// ══════════════════════════════════════════════════�?
// 经典猎人卡牌脚本
// ══════════════════════════════════════════════════�?

public class ClassicHunter : CardPack
{
	public override void Register(CardBuilder b)
	{
		// ── 附魔 ──
		b.Buff("DS1_070o", atk: 2, health: 2, extraTags: (GameTag.Taunt, true)); // 驯兽�?
		b.Buff("DS1_175o", atk: 1);                                              // 森林�?
		b.Buff("DS1_178e", extraTags: (GameTag.Charge, true));                    // 冻原犀�?
		b.Buff("EX1_531e", atk: 2, health: 1);                                   // 腐肉食尸�?
		b.Buff("NEW1_033o", atk: 1);                                              // 雷欧�?
		b.Buff("CS2_084e", maxHealthMod: SetTo(1));                            // 猎人印记
		b.Buff("EX1_549o", atk: 2, extraTags: (GameTag.Immune, true),
			on: When<TurnEndEvent>(OWN, ON, new Destroy(S.SELF)));  // 野兽之怒（本回合）            // 野兽之�?
		b.Buff("EX1_611e", cost: 2);                                             // 冰冻陷阱
		b.Buff("EX1_536e", durability: 1);                                       // 鹰角长弓

		// ── 随从 ──

		// 饥饿的秃�?�?每当你召唤一个野兽，抽一张牌
		b.Card("CS2_237", on: new[]
		{
			When<MinionSummonedEvent>(TriggerOwner.Own, TriggerTiming.On, new DrawCard(S.CONTROLLER))
		});

		// 驯兽�?�?战吼：给一个友方野�?+2/+2 和嘲�?
		b.Card("DS1_070",
			battlecry: new Enchant(S.TARGET, "DS1_070o"),
			target: Target.IfAvailable().Friendly().Race((Race)20));

		// 森林�?�?你的其他野兽获得 +1 攻击�?
		b.Card("DS1_175", update: new[]
		{
			new Refresh(S.FRIENDLY_MINIONS + S.BEAST - S.SELF, buff: "DS1_175o")
		});

		// 冻原犀�?�?你的野兽获得冲锋
		b.Card("DS1_178", update: new[]
		{
			new Refresh(S.FRIENDLY_MINIONS + S.BEAST, buff: "DS1_178e")
		});

		// 腐肉食尸�?�?每当一个友方野兽死亡，获得 +2/+1
		b.Card("EX1_531", on: new[]
		{
			When<DeathEvent>(TriggerOwner.Own, TriggerTiming.On, new Enchant(S.SELF, "EX1_531e"),
				when: EventTargetIn<DeathEvent>(S.MINION + S.Race(Race.Beast)))
		});

		// 萨凡纳雄�?�?亡语：召唤两�?2/2 鬣狗
		b.Card("EX1_534", deathrattle: new Summon(S.CONTROLLER, "EX1_534t") * 2);

		// 雷欧�?�?你的其他随从获得 +1 攻击�?
		b.Card("NEW1_033", update: new[]
		{
			new Refresh(S.FRIENDLY_MINIONS - S.SELF, buff: "NEW1_033o")
		});

		// ── 法术 ──

		// 猎人印记 �?将一个随从的生命值变�?1
		b.Card("CS2_084",
			play: new Enchant(S.TARGET, "CS2_084e"),
			target: Target.Required().Minion());

		// 多重射击 �?对两个随机敌方随从造成 3 点伤�?
		b.Card("DS1_183",
			play: new DealDamage(new RandomSelector(S.ENEMY_MINIONS, 2), 3),
			target: Target.Condition().NeedsEnemyMinions(2));

		// 追踪�?�?查看牌库顶部 3 张牌，抽一�?
		b.Card("DS1_184", play: new GenericChoice(S.CONTROLLER, S.FRIENDLY_DECK));

		// 奥术射击 �?造成 2 点伤�?
		b.Card("DS1_185",
			play: new DealDamage(S.TARGET, 2),
			target: Target.Required());

		// 爆炸射击 �?对一个随从造成 5 点伤害，对其相邻随从造成 2 点伤�?
		b.Card("EX1_537",
			play: Acts(new DealDamage(S.TARGET, 5), new DealDamage(S.TARGET_ADJACENT, 2)),
			target: Target.Required().Minion());

		// 放狗 — 为每个敌方随从召唤一只 1/1 猎犬
		b.Card("EX1_538",
			play: new Summon(S.CONTROLLER, "EX1_538t") * new Count(S.ENEMY_MINIONS),
			target: Target.Condition().NeedsEnemyMinions(1));

		// 杀戮命�?�?造成 3 点伤害，若你有野兽则改为 5 �?
		b.Card("EX1_539",
			play: IfElse(new Find(S.FRIENDLY_MINIONS + S.BEAST),
				new DealDamage(S.TARGET, 5), new DealDamage(S.TARGET, 3)),
			target: Target.Required());

		// 照明�?�?消灭所有敌方奥秘。抽一张牌
		b.Card("EX1_544", play: Acts(
			Unstealth(S.ALL_MINIONS),
			new Destroy(S.ENEMY_SECRETS),
			new DrawCard(S.CONTROLLER)));

		// 野兽之�?�?+2 攻击力和免疫（本回合�?
		b.Card("EX1_549",
			play: new Enchant(S.TARGET, "EX1_549o"),
			target: Target.Required().Friendly().Race((Race)20));

		// 致命射击 �?消灭一个随机敌方随�?
		b.Card("EX1_617",
			play: new Destroy(S.RANDOM_ENEMY_MINION),
			target: Target.Condition().NeedsEnemyMinions(1));

		// 动物伙伴 �?召唤一个随机动物伙�?
		b.Card("NEW1_031",
			play: new Summon(S.CONTROLLER, new RandomCardPicker()),
			entourage: new[] { "NEW1_032", "NEW1_033", "NEW1_034" },
			target: TargetRequirement.None);

		// ── 奥秘 ──

		// 狙击 — 当对手打出一个随从时，对其造成 4 点伤害
		b.Card("EX1_609",
			secret: When<MinionPlayedEvent>(ENEMY, AFTER,
				new DealDamage(S.EVT_SOURCE, 4)));

		// 爆炸陷阱 — 当你的英雄被攻击时，对所有敌方角色造成 2 点伤害
		b.Card("EX1_610",
			secret: When<AttackEvent>(ENEMY, ON,
				new DealDamage(S.ENEMY_CHARACTERS, 2),
				when: EventTargetIn<AttackEvent>(S.FRIENDLY_HERO)));

		// 冰冻陷阱 — 当敌方随从攻击时，将其移回手牌，法力值消耗增加(2) 点
		b.Card("EX1_611",
			secret: When<AttackEvent>(ENEMY, ON,
				new TargetedAction[] {
					new ReturnToHand(S.EVT_SOURCE),
					new Enchant(S.EVT_SOURCE, "EX1_611e")
				},
				when: EventSourceIn<AttackEvent>(S.ALL_MINIONS)));

		// ── 武器 ──

		// 角斗士长弓 — 攻击时你的英雄免疫
		b.Card("DS1_188", update: new[]
		{
			new Refresh(S.FRIENDLY_HERO, new Dictionary<GameTag, object>
			{
				[GameTag.ImmuneWhileAttacking] = true
			})
		});

		// 鹰角长弓 — 每当一个友方奥秘被揭示，获得 +1 耐久
		b.Card("EX1_536",
			on: When<RevealSecretEvent>(OWN, ON, new Enchant(S.SELF, "EX1_536e")));
		// 误导：当敌方角色攻击你的英雄时，随机改为攻击另一个合法角色
		b.Card("EX1_533",
			secret: When<ProposedAttackEvent>(ENEMY, ON,
				new RedirectAttackToRandomOtherCharacter(),
				when: EventTargetIn<ProposedAttackEvent>(S.FRIENDLY_HERO)));
		b.Card("EX1_543"); // 暴龙王克鲁什
		// 毒蛇陷阱：当你的一个随从被攻击时，召唤三条 1/1 的蛇
		b.Card("EX1_554",
			secret: When<AttackEvent>(ENEMY, ON,
				new Summon(S.CONTROLLER, "EX1_554t") * 3,
				when: EventTargetIn<AttackEvent>(S.FRIENDLY_MINIONS)));
		b.Card("GIFT_03"); // 雷克萨的礼物
	}
}
