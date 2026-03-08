using HearthstoneServer.Auras;
using HearthstoneServer.Entities;
using HearthstoneServer.Events;
using static HearthstoneServer.Dsl.BusEvent;
using static HearthstoneServer.Dsl.EventFilters;
using static HearthstoneServer.Dsl.RandomCards;
using static HearthstoneServer.Cards.CardScriptUtils;
using HearthstoneServer.Targeting;

namespace HearthstoneServer.Dsl.Cards;

// ═════════════════════════════════════════
// 纳克萨玛斯 — 可收集卡牌脚本
// ═════════════════════════════════════════

public class NaxxramasCollectible : CardPack
{
	public override void Register(CardBuilder b)
	{
		// ═══════════════════════════════════════
		// 附魔
		// ═══════════════════════════════════════

		b.Buff("FP1_005e", atk: 1, health: 1);   // 纳克萨玛斯之影 +1/+1
		b.Buff("FP1_023e", health: 3);             // 暗色教徒 +3 生命值
		b.Buff("FP1_028e", atk: 1);                // 送葬者 +1 攻击力
		b.Buff("FP1_020e", atk: 3, health: 2);    // 复仇 +3/+2

		// ═══════════════════════════════════════
		// 随从 — 亡语
		// ═══════════════════════════════════════

		// 僵尸：亡语 — 恢复敌方英雄 5 点生命值
		b.Card("FP1_001", deathrattle: new RestoreHealth(S.ENEMY_HERO, 5));

		// 鬼灵爬行者：亡语 — 召唤两个 1/1 的幽灵蜘蛛
		b.Card("FP1_002", deathrattle: new Summon(S.CONTROLLER, "FP1_002t") * 2);

		// 蛛魔之卵：亡语 — 召唤一个 4/4 的蛛魔
		b.Card("FP1_007", deathrattle: new Summon(S.CONTROLLER, "FP1_007t"));

		// 死亡领主：亡语 — 从对手牌库中召唤一个随机随从
		b.Card("FP1_009", deathrattle: new Summon(S.OPPONENT, S.Random(S.ENEMY_DECK + S.MINION)));

		// 蛛网喷射者：亡语 — 将一张随机野兽牌置入你的手牌
		b.Card("FP1_011", deathrattle: new AddToHand(S.CONTROLLER, RandomBeast()));

		// 淤泥充斥者：亡语 — 召唤一个 1/2 的具有嘲讽的史莱姆
		b.Card("FP1_012", deathrattle: new Summon(S.CONTROLLER, "FP1_012t"));

		// 虚空召唤者：亡语 — 从你的手牌中召唤一个随机恶魔
		b.Card("FP1_022", deathrattle: new Summon(S.CONTROLLER, S.Random(S.FRIENDLY_HAND + S.DEMON)));

		// 暗色教徒：亡语 — 使一个随机友方随从获得 +3 生命值
		b.Card("FP1_023", deathrattle: new Enchant(S.RANDOM_OTHER_FRIENDLY_MINION, "FP1_023e"));

		// 不稳定的食尸鬼：亡语 — 对所有随从造成 1 点伤害
		b.Card("FP1_024", deathrattle: new DealDamage(S.ALL_MINIONS, 1));

		// 蛛魔伏击者：亡语 — 将一个随机友方随从移回你的手牌
		b.Card("FP1_026", deathrattle: new ReturnToHand(S.RANDOM_FRIENDLY_MINION));

		// 舞动利刃：亡语 — 对手抽一张牌
		b.Card("FP1_029", deathrattle: new DrawCard(S.OPPONENT));

		// 死亡之咬（武器）：亡语 — 对所有随从造成 1 点伤害
		b.Card("FP1_021", deathrattle: new DealDamage(S.ALL_MINIONS, 1));

		// ═══════════════════════════════════════
		// 随从 — 战吼
		// ═══════════════════════════════════════

		// 哀嚎灵魂：战吼 — 沉默所有其他友方随从
		b.Card("FP1_016", battlecry: new Silence(S.FRIENDLY_MINIONS - S.SELF));

		// 洛欧塞布：战吼 — 对手的法术下回合法力值消耗增加 (5) 点
		b.Card("FP1_030", battlecry: new Enchant(S.CONTROLLER, "FP1_030e"));

		// 斯塔拉格：亡语 — 若费尔根也已死亡，召唤塔迪乌斯
		b.Card("FP1_014", deathrattle: new ConditionalAction(
			new Find(S.FRIENDLY_GRAVEYARD + S.Id("FP1_015")),
			new Action[] { new Summon(S.CONTROLLER, "FP1_014t") }));

		// 费尔根：亡语 — 若斯塔拉格也已死亡，召唤塔迪乌斯
		b.Card("FP1_015", deathrattle: new ConditionalAction(
			new Find(S.FRIENDLY_GRAVEYARD + S.Id("FP1_014")),
			new Action[] { new Summon(S.CONTROLLER, "FP1_014t") }));

		// ═══════════════════════════════════════
		// 随从 — 事件（EventBus）
		// ═══════════════════════════════════════

		// 纳克萨玛斯之影：回合开始时获得 +1/+1
		b.Card("FP1_005",
			on: When<TurnStartEvent>(OWN, ON, new Enchant(S.SELF, "FP1_005e")));

		// 石肤石像鬼：回合开始时恢复满血
		b.Card("FP1_027",
			on: When<TurnStartEvent>(OWN, ON, new FullRestore(S.SELF)));

		// 送葬者：每当你召唤一个具有亡语的随从，获得 +1 攻击力
		b.Card("FP1_028",
			on: When<MinionSummonedEvent>(OWN, AFTER, new Enchant(S.SELF, "FP1_028e"),
				when: EventTargetIn<MinionSummonedEvent>(S.MINION + S.DEATHRATTLE)));

		// 克尔苏加德：每个回合结束时，复活本回合死亡的所有友方随从
		b.Card("FP1_013",
			on: When<TurnEndEvent>(ANY, ON,
				new SummonCopy(S.CONTROLLER, S.KILLED_THIS_TURN + S.FRIENDLY + S.MINION)));

		// ═══════════════════════════════════════
		// 随从 — 光环（刷新）
		// ═══════════════════════════════════════

		// 蛛魔织网者：具有战吼的随从法力值消耗增加 (2) 点
		b.Card("FP1_017", update: new[]
		{
			new Refresh(S.IN_HAND + S.MINION + S.BATTLECRY, new() { { GameTag.Cost, 2 } })
		});

		// 瑞文戴尔男爵：你的随从亡语触发两次
		b.Card("FP1_031", update: new[]
		{
			new Refresh(S.CONTROLLER, new() { { GameTag.Deathrattle, 2 } })
		});

		// ═══════════════════════════════════════
		// 随从 — 回声软泥怪（复杂）
		// ═══════════════════════════════════════

		// 回声软泥怪：战吼 — 回合结束时召唤一个该随从的精确副本
		b.Card("FP1_003",
			on: When<TurnEndEvent>(OWN, ON, new SummonCopy(S.CONTROLLER, S.SELF)));

		// 疯狂的科学家：亡语 — 从牌库中将一个随机奥秘置入战场
		b.Card("FP1_004", deathrattle: new Summon(S.CONTROLLER, S.Random(S.FRIENDLY_DECK + S.SECRET)));

		// ═══════════════════════════════════════
		// 法术
		// ═══════════════════════════════════════

		// 毒种：消灭所有随从，为每方召唤等量的 2/2 树人
		b.Card("FP1_019", play: new ForEach(S.ALL_MINIONS,
			new Summon(S.CURRENT_CONTROLLER, "FP1_019t")));

		// 转生：消灭一个随从，然后重新召唤它
		b.Card("FP1_025", play: new Destroy(S.TARGET).Then(
			new SummonCopy(S.CONTROLLER, S.TARGET)),
			target: Target.Required().Minion());

		// ═══════════════════════════════════════
		// 奥秘
		// ═══════════════════════════════════════

		// 复制：当一个友方随从死亡时，将 2 张其副本置入你的手牌
		b.Card("FP1_018",
		secret: When<DeathEvent>(OWN, AFTER,
				new TargetedAction[] {
					new CopyToHand(S.CONTROLLER, S.EVT_TARGET),
					new CopyToHand(S.CONTROLLER, S.EVT_TARGET)
				},
				when: EventTargetIn<DeathEvent>(S.FRIENDLY_MINIONS)));

		// 复仇：当一个友方随从死亡时，使一个随机友方随从获得 +3/+2
		b.Card("FP1_020",
			secret: When<DeathEvent>(OWN, AFTER,
				new Enchant(S.RANDOM_FRIENDLY_MINION, "FP1_020e")));

		// ═══════════════════════════════════════
		// FP1_030e — 洛欧塞布附魔（复杂）
		// ═══════════════════════════════════════

		// 洛欧塞布减益：对手法术费用+5，持续对手的下一个回合
		// 附魔放在己方 Player 上，ENEMY_HAND 正确选到对手手牌
		// 对手回合结束时自动销毁（ENEMY TurnEnd）
		b.Card("FP1_030e",
			update: new[]
			{
				new Refresh(S.ENEMY_HAND + S.SPELL, GameTag.Cost, 5)
			},
			on: When<TurnEndEvent>(ENEMY, ON, new Destroy(S.SELF)));
		b.Card("FP1_008"); // 鬼灵骑士
		b.Card("FP1_010"); // 迈克斯纳
	}
}
