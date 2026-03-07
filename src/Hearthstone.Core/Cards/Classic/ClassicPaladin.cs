using HearthstoneServer.Auras;
using HearthstoneServer.Entities;
using HearthstoneServer.Events;
using static HearthstoneServer.Dsl.BusEvent;
using static HearthstoneServer.Cards.CardScriptUtils;
using static HearthstoneServer.Dsl.EventFilters;
using static HearthstoneServer.Dsl.LazyNum;
using HearthstoneServer.Targeting;

namespace HearthstoneServer.Dsl.Cards;

// 经典圣骑士卡牌脚本
public class ClassicPaladin : CardPack
{
	public override void Register(CardBuilder b)
	{
		// 附魔
		b.Buff("CS2_087e", atk: 3);                    // 力量祝福
		b.Buff("CS2_092e", atk: 4, health: 4);        // 王者祝福
		b.Buff("EX1_355e", atkMod: MultiplyBy(2));    // 祝福勇士
		b.Buff("EX1_360e", atkMod: SetTo(1));         // 谦逊
		b.Buff("EX1_366e", atk: 1, health: 1);        // 正义之剑
		b.Buff("EX1_382e", atkMod: SetTo(1));         // 奥尔多卫士
		b.Buff("EX1_379e", maxHealthMod: SetTo(1));   // 忏悔
		b.Buff("EX1_619e", maxHealthMod: SetTo(1));   // 生而平等

		// 随从

		// 列王守卫：战吼，恢复英雄 6 点生命值。
		b.Card("CS2_088", battlecry: new RestoreHealth(S.FRIENDLY_HERO, 6));

		// 银色保卫者：战吼，给一个友方随从圣盾。
		b.Card("EX1_362",
			battlecry: GiveDivineShield(S.TARGET),
			target: Target.IfAvailable().Minion().Friendly().NonSelf());

		// 奥尔多卫士：战吼，将敌方随从攻击力设为 1。
		b.Card("EX1_382",
			battlecry: new Enchant(S.TARGET, "EX1_382e"),
			target: Target.IfAvailable().Minion().Enemy());

		// 提里奥·弗丁：亡语，装备灰烬使者。
		b.Card("EX1_383", deathrattle: new Summon(S.CONTROLLER, "EX1_383t"));

		// 法术

		// 力量祝福：+3 攻击力。
		b.Card("CS2_087",
			play: new Enchant(S.TARGET, "CS2_087e"),
			target: Target.Required().Minion());

		// 圣光术：恢复 6 点生命值。
		b.Card("CS2_089",
			play: new RestoreHealth(S.TARGET, 6),
			target: Target.Required());

		// 王者祝福：+4/+4。
		b.Card("CS2_092",
			play: new Enchant(S.TARGET, "CS2_092e"),
			target: Target.Required().Minion());

		// 奉献：对所有敌方角色造成 2 点伤害。
		b.Card("CS2_093", play: new DealDamage(S.ENEMY_CHARACTERS, 2));

		// 愤怒之锤：造成 3 点伤害，抽一张牌。
		b.Card("CS2_094",
			play: Acts(new DealDamage(S.TARGET, 3), new DrawCard(S.CONTROLLER)),
			target: Target.Required());

		// 神恩术：抽牌直到你的手牌数等于对手。
		b.Card("EX1_349", play: new DrawUntilCount(S.CONTROLLER, new Count(S.ENEMY_HAND)));

		// 圣疗术：恢复 8 点生命值，抽 3 张牌。
		b.Card("EX1_354",
			play: Acts(
				new RestoreHealth(S.TARGET, 8),
				new DrawCard(S.CONTROLLER),
				new DrawCard(S.CONTROLLER),
				new DrawCard(S.CONTROLLER)),
			target: Target.Required());

		// 祝福勇士：攻击力翻倍。
		b.Card("EX1_355",
			play: new Enchant(S.TARGET, "EX1_355e"),
			target: Target.Required().Minion());

		// 谦逊：将随从攻击力设为 1。
		b.Card("EX1_360",
			play: new Enchant(S.TARGET, "EX1_360e"),
			target: Target.Required().Minion());

		// 智慧祝福：随从攻击时抽一张牌。
		b.Card("EX1_363",
			play: new Enchant(S.TARGET, "EX1_363e"),
			target: Target.Required().Minion());
		b.Card("EX1_363e",
			on: new[] { When<AttackEvent>(TriggerOwner.Own, TriggerTiming.On, new DrawCard(S.CONTROLLER)) });

		// 神圣愤怒：抽一张牌，对目标造成等于其法力值消耗的伤害。
		b.Card("EX1_365",
			play: new DrawCard(S.CONTROLLER)
				.Then(new DealDamage(S.TARGET_2, new Attr(S.CURRENT_TARGET, GameTag.Cost))),
			target: Target.Required());

		// 保护之手：给一个随从圣盾。
		b.Card("EX1_371",
			play: GiveDivineShield(S.TARGET),
			target: Target.Required().Minion());

		// 复仇之怒：随机分配 8 点伤害。
		b.Card("EX1_384", play: new DealDamage(S.RANDOM_ENEMY_CHARACTER, 1) * 8);

		// 生而平等：所有随从生命值变为 1。
		b.Card("EX1_619", play: new Enchant(S.ALL_MINIONS, "EX1_619e"));

		// 正义：给所有友方随从圣盾。
		b.Card("EX1_184", play: GiveDivineShield(S.FRIENDLY_MINIONS));

		// 奥秘

		// 崇高牺牲：敌方角色攻击时，召唤一个 2/1 保卫者。
		b.Card("EX1_130",
			secret: When<AttackEvent>(ENEMY, ON,
				new Summon(S.CONTROLLER, "EX1_130a")));

		// 以眼还眼：你的英雄受到多少伤害，就对敌方英雄造成多少伤害。
		b.Card("EX1_132",
			secret: When<DamageEvent>(OWN, ON,
				new DealDamage(S.ENEMY_HERO, EVT_AMOUNT),
				when: TargetIsHero()));

		// 救赎：友方随从死亡时以 1 点生命值复活。
		b.Card("EX1_136",
			secret: When<DeathEvent>(OWN, ON,
				new Summon(S.CONTROLLER, "EX1_136t"),
				when: DeadIsMinion()));

		// 忏悔：对手打出随从后将其生命值降为 1。
		b.Card("EX1_379",
			secret: When<MinionPlayedEvent>(ENEMY, AFTER,
				new Enchant(S.EVT_SOURCE, "EX1_379e")));

		// 武器

		// 真银圣剑：英雄攻击时恢复 2 点生命值。
		b.Card("CS2_097",
			on: When<AttackEvent>(OWN, ON, new RestoreHealth(S.FRIENDLY_HERO, 2)));

		// 正义之剑：召唤随从后给它 +1/+1，并损失 1 点耐久（当前实现复用 1 点自伤）。
		b.Card("EX1_366",
			on: When<MinionSummonedEvent>(OWN, AFTER,
				new TargetedAction[] { new Enchant(S.SELF, "EX1_366e"), new DealDamage(S.SELF, 1) }));

		b.Card("GIFT_05"); // 乌瑟尔的礼物
	}
}
