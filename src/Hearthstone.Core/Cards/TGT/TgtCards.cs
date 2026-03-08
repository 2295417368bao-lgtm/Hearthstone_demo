using HearthstoneServer.Auras;
using HearthstoneServer.Entities;
using HearthstoneServer.Events;
using HearthstoneServer.Targeting;
using static HearthstoneServer.Cards.CardScriptUtils;
using static HearthstoneServer.Dsl.BusEvent;
using static HearthstoneServer.Dsl.EventFilters;
using static HearthstoneServer.Dsl.LazyNum;
using static HearthstoneServer.Dsl.RandomCards;

namespace HearthstoneServer.Dsl.Cards;

/// <summary>
/// TGT（冠军的试炼）卡牌脚本。
/// 这里按职业集中注册，并为每张卡补充效果注释，便于对照卡面与脚本实现。
/// </summary>
public class TgtCards : CardPack
{
	public override void Register(CardBuilder b)
	{
		Selector silverHandRecruit = new FilterSelector((entity, _) => (entity as BuffableEntity)?.CardId == "CS2_101t");
		Selector friendlySilverHands = S.FRIENDLY_MINIONS + silverHandRecruit;
		Selector friendlySpellPowerMinions = S.FRIENDLY_MINIONS + new FilterSelector((entity, _) => entity is Minion minion && minion.SpellPower > 0);

		Evaluator JoustWin() => new JoustEvaluator(new RandomSelector(S.FRIENDLY_DECK + S.MINION), new RandomSelector(S.ENEMY_DECK + S.MINION));
		Evaluator HasFriendlyBeast() => new Find(S.FRIENDLY_MINIONS + S.BEAST);
		Evaluator HasFriendlyPirate() => new Find(S.FRIENDLY_MINIONS + S.PIRATE);

		// 附魔与子卡。
		b.Buff("AT_021e", atk: 2, health: 1);
		b.Buff("AT_024e", atk: 3, health: 3);
		b.Buff("AT_028e", atk: 4);
		b.Buff("AT_029e", atk: 1);
		b.Buff("AT_032e", atk: 1, health: 1);
		b.Buff("AT_039e", atk: 2, on: When<TurnEndEvent>(OWN, ON, new Destroy(S.SELF)));
		b.Buff("AT_040e", health: 3);
		b.Buff("AT_057o",
			on: When<TurnEndEvent>(OWN, ON, new Destroy(S.SELF)),
			extraTags: new[] { (GameTag.CantBeDamaged, (object)true), (GameTag.CantBeTargetedByOpponents, (object)true) });
		b.Buff("AT_061e", on: new[]
		{
			When<SpellCastEvent>(OWN, AFTER, new AddToHand(S.CONTROLLER, RandomCard().Class(CardClass.Hunter))),
			When<TurnEndEvent>(OWN, ON, new Destroy(S.SELF))
		});
		b.Buff("AT_068e", atk: 2, health: 2);
		b.Buff("AT_071e", atk: 1, extraTags: (GameTag.Charge, true));
		b.Buff("AT_073e", atk: 1, health: 1);
		b.Buff("AT_074e2", atk: 3, extraTags: (GameTag.DivineShield, true));
		b.Buff("AT_075e", atk: 2, extraTags: (GameTag.Taunt, true));
		b.Buff("AT_077e", durability: 1);
		b.Buff("AT_081e", atkMod: SetTo(1));
		b.Buff("AT_082e", atk: 1);
		b.Buff("AT_083e",
			on: When<TurnEndEvent>(OWN, ON, new Destroy(S.SELF)),
			extraTags: new[] { (GameTag.Windfury, (object)true) });
		b.Buff("AT_084e", atk: 2);
		b.Buff("AT_089e", health: 1);
		b.Buff("AT_090e", atk: 1, health: 1);
		b.Buff("AT_109e", on: When<TurnEndEvent>(OWN, ON, new Destroy(S.SELF)));
		b.Buff("AT_115e",
			update: new Refresh(S.FRIENDLY_HERO_POWER, GameTag.Cost, -2),
			on: When<HeroPowerUsedEvent>(OWN, ON, new Destroy(S.SELF)));
		b.Buff("AT_116e", atk: 1, extraTags: (GameTag.Taunt, true));
		b.Buff("AT_117e", atk: 2, health: 2);
		b.Buff("AT_119e", atk: 2, health: 2);
		b.Buff("AT_121e", atk: 1, health: 1);
		b.Buff("AT_133e", atk: 1, health: 1);
		b.Buff("AT_017e", atk: 1, extraTags: (GameTag.Taunt, true));
		b.Buff("AT_006e", extraTags: (GameTag.Spellpower, 1));
		b.Buff("AT_011e", atk: 2);
		b.Buff("AT_007e", cost: -2);
		b.Buff("AT_014e", cost: -1);
		b.Buff("AT_034e", atk: 1);
		b.Buff("AT_019e",
			on: When<TurnEndEvent>(OWN, ON,
				Acts(
					new Summon(S.CONTROLLER, "AT_019"),
					new Destroy(S.SELF))));
		b.Buff("AT_038e", on: When<TurnEndEvent>(OWN, ON, new Destroy(S.SELF)));
		b.Buff("AT_045e", costModFunc: SetTo(1));
		b.Buff("AT_086e",
			update: new Refresh(S.FRIENDLY_HERO_POWER, GameTag.Cost, +5),
			on: When<HeroPowerUsedEvent>(OWN, ON, new Destroy(S.SELF)));
		b.Buff("AT_132_DRUIDe", atk: 2, on: When<TurnEndEvent>(OWN, ON, new Destroy(S.SELF)));

		// AT_050t 雷霆震击：造成 2 点伤害。
		b.Card("AT_050t",
			play: new DealDamage(S.TARGET, 2),
			target: Target.Required());

		// AT_037 活体根须：抉择，造成 2 点伤害；或者召唤两个 1/1 树苗。
		b.Card("AT_037",
			chooseCards: new[] { "AT_037a", "AT_037b" });
		// AT_037a 活体根须（伤害）：造成 2 点伤害。
		b.Card("AT_037a",
			play: new DealDamage(S.TARGET, 2),
			target: Target.Required());
		// AT_037b 活体根须（召唤）：召唤两个 1/1 树苗。
		b.Card("AT_037b", play: new Summon(S.CONTROLLER, "AT_037t") * 2);

		// AT_042 刃牙德鲁伊：抉择，变形为 2/1 冲锋；或者 3/2 潜行。
		b.Card("AT_042",
			chooseCards: new[] { "AT_042a", "AT_042b" });
		// AT_042a 刃牙德鲁伊（雄狮形态）：变形为 2/1，具有冲锋。
		b.Card("AT_042a", play: new Transform(S.SELF, "AT_042t"));
		// AT_042b 刃牙德鲁伊（黑豹形态）：变形为 3/2，具有潜行。
		b.Card("AT_042b", play: new Transform(S.SELF, "AT_042t2"));

		// 德鲁伊
		// AT_038 达纳苏斯豹骑士：战吼获得 1 个空的法力水晶；亡语失去 1 个法力水晶。
		b.Card("AT_038",
			battlecry: GainEmptyMana(S.CONTROLLER, 1),
			deathrattle: new GainMana(S.CONTROLLER, -1));
		// AT_039 狂野斗士：激励，本回合你的英雄获得 +2 攻击力。
		b.Card("AT_039", inspire: new Enchant(S.FRIENDLY_HERO, "AT_039e"));
		// AT_040 荒野行者：战吼，使一个友方野兽获得 +3 生命值。
		b.Card("AT_040",
			battlecry: new Enchant(S.TARGET, "AT_040e"),
			target: Target.IfAvailable().Friendly().Minion().Race(Race.Beast));
		// AT_041 荒野骑士：每有一只你本局召唤过的野兽，此牌法力值消耗减少（1）。
		b.Card("AT_041",
			costMod: new SummonedThisGameCount(Race.Beast) * -1);
		// AT_043 星界沟通：获得 10 个法力水晶，弃掉你的手牌。
		b.Card("AT_043", play: Acts(new GainMana(S.CONTROLLER, 10), new Discard(S.FRIENDLY_HAND)));
		// AT_044 腐根：消灭一个随从，随机将一张随从牌置入对手手牌。
		b.Card("AT_044",
			play: Acts(new Destroy(S.TARGET), new AddToHand(S.OPPONENT, RandomCard().Minion())),
			target: Target.Required().Minion());
		// AT_045 艾维娜：你的随从牌法力值消耗变为（1）。
		b.Card("AT_045",
			update: new[] { new Refresh(S.FRIENDLY_HAND + S.MINION, buff: "AT_045e") });

		// 猎人
		// AT_010 暴躁的牧羊人：战吼，如果你控制野兽，则随机召唤一只野兽。
		b.Card("AT_010",
			battlecry: IfElse(HasFriendlyBeast(), new Summon(S.CONTROLLER, RandomCard().Minion().Race(Race.Beast)), NOTHING));
		// AT_056 强风射击：对一个随从及其相邻随从各造成 2 点伤害。
		b.Card("AT_056", play: new DealDamage(S.TARGET + S.TARGET_ADJACENT, 2), target: Target.Required().Minion());
		// AT_057 角斗大师：战吼，本回合使一个友方野兽获得免疫。
		b.Card("AT_057",
			battlecry: new Enchant(S.TARGET, "AT_057o"),
			target: Target.IfAvailable().Friendly().Minion().Race(Race.Beast));
		// AT_058 皇家雷象：战吼，比武获胜则抽一张牌。
		b.Card("AT_058",
			battlecry: IfElse(JoustWin(), new DrawCard(S.CONTROLLER), NOTHING));
		// AT_059 神勇弓箭手：激励，如果你没有其他手牌，则对敌方英雄造成 2 点伤害。
		b.Card("AT_059",
			inspire: IfElse(EMPTY_HAND, new DealDamage(S.ENEMY_HERO, 2), NOTHING));
		// AT_060 捕熊陷阱：奥秘，当你的英雄受到攻击后，召唤一只 3/3 且具有嘲讽的熊。
		b.Card("AT_060",
			secret: When<AfterAttackEvent>(ENEMY, AFTER,
				new Summon(S.CONTROLLER, "CS2_125"),
				when: EventTargetIn<AfterAttackEvent>(S.FRIENDLY_HERO)));
		// AT_061 子弹上膛：本回合中，每当你施放一个法术，随机获得一张猎人牌。
		b.Card("AT_061",
			play: new Enchant(S.CONTROLLER, "AT_061e"));
		// AT_062 天降蛛群：召唤三只 1/1 并具有亡语的结网蛛。
		b.Card("AT_062", play: new Summon(S.CONTROLLER, "FP1_002t") * 3);
		// AT_063 酸喉：每当有敌方随从受到伤害时，将其消灭。
		b.Card("AT_063",
			on: When<DamageEvent>(ANY, AFTER,
				new Destroy(S.EVT_TARGET),
				when: EventTargetIn<DamageEvent>(S.ENEMY_MINIONS)));
		b.Card("AT_063t");

		// 法师
		// AT_001 炎枪术：对一个随从造成 25 点伤害。
		b.Card("AT_001",
			play: new DealDamage(S.TARGET, 25),
			target: Target.Required().Minion());
		// AT_002 轮回：奥秘，当一个友方随从死亡时，随机召唤一个同费用随从。
		b.Card("AT_002",
			secret: When<DeathEvent>(OWN, AFTER,
				new Summon(S.CONTROLLER, RandomCard().Minion().Cost(new Attr(S.EVT_TARGET, GameTag.Cost))),
				when: EventTargetIn<DeathEvent>(S.MINION)));
		// AT_003 英雄之魂：你的英雄技能额外造成 1 点伤害。
		b.Card("AT_003");
		// AT_004 奥术冲击：造成 2 点伤害，受法术伤害加成时翻倍计算。
		b.Card("AT_004",
			play: new DealDamage(S.TARGET, Of(source => 2 + (((source as BuffableEntity)?.Controller?.SpellPower ?? 0) * 2))),
			target: Target.Required().Minion());
		// AT_005 变形术：野猪：将一个随从变形为 4/2 并具有冲锋的野猪。
		b.Card("AT_005",
			play: new Transform(S.TARGET, "AT_005t"),
			target: Target.Required().Minion());
		// AT_006 达拉然铁骑士：法术伤害 +1；激励，再获得法术伤害 +1。
		b.Card("AT_006", inspire: new Enchant(S.SELF, "AT_006e"));
		// AT_007 嗜法者：战吼，双方各随机获得一张法术牌，你获得的那张法力值消耗减少（2）。
		b.Card("AT_007",
			battlecry: Acts(
				new AddToHand(S.OPPONENT, RandomSpell()),
				new AddToHand(S.CONTROLLER, RandomSpell()).Then(new Enchant(S.CURRENT_TARGET, "AT_007e"))));
		// AT_008 考达拉幼龙：你可以使用任意次数的英雄技能。
		b.Card("AT_008",
			update: new[]
			{
				new Refresh(S.FRIENDLY_HERO_POWER, setTags: new Dictionary<GameTag, object>
				{
					[GameTag.HeropowerAdditionalActivations] = -1
				})
			});
		// AT_009 罗宁：亡语，将三张奥术飞弹置入你的手牌。
		b.Card("AT_009",
			deathrattle: new AddToHand(S.CONTROLLER, "EX1_277") * 3);

		// 圣骑士
		// AT_073 争强好胜：奥秘，在你的回合开始时，使你的所有随从获得 +1/+1。
		b.Card("AT_073",
			secret: When<TurnStartEvent>(OWN, ON, new Enchant(S.FRIENDLY_MINIONS, "AT_073e")));
		// AT_074 英勇圣印：使一个随从获得 +3 攻击力和圣盾。
		b.Card("AT_074",
			play: new Enchant(S.TARGET, "AT_074e2"),
			target: Target.Required().Minion());
		// AT_075 战马训练师：你的白银之手新兵拥有 +2 攻击力和嘲讽。
		b.Card("AT_075",
			update: new[] { new Refresh(friendlySilverHands, buff: "AT_075e") });
		// AT_076 鱼人骑士：激励，随机召唤一个鱼人。
		b.Card("AT_076",
			inspire: new Summon(S.CONTROLLER, RandomCard().Minion().Race(Race.Murloc)));
		// AT_077 白银之枪：战吼，比武获胜则获得 +1 耐久度。
		b.Card("AT_077",
			battlecry: IfElse(JoustWin(), new Enchant(S.SELF, "AT_077e"), NOTHING));
		// AT_078 精英对决：除了每个玩家攻击力最高的随从外，消灭其他所有随从。
		b.Card("AT_078",
			play: new Destroy(S.ALL_MINIONS - HIGHEST_ATK(S.FRIENDLY_MINIONS) - HIGHEST_ATK(S.ENEMY_MINIONS)));
		// AT_079 神秘挑战者：战吼，将牌库中每种不同的奥秘直接置入战场。
		b.Card("AT_079",
			battlecry: new PlayDistinctSecretsFromDeck(S.CONTROLLER));
		// AT_081 纯洁者耶德瑞克：战吼，使所有敌方随从的攻击力变为 1。
		b.Card("AT_081",
			battlecry: new Enchant(S.ENEMY_MINIONS, "AT_081e"));
		// AT_104 海象人骑士：战吼，比武获胜则为你的英雄恢复 7 点生命值。
		b.Card("AT_104",
			battlecry: IfElse(JoustWin(), new RestoreHealth(S.FRIENDLY_HERO, 7), NOTHING));

		// 牧师
		// AT_011 神圣勇士：过量治疗时获得 +2 攻击力。
		b.Card("AT_011",
			on: When<OverhealEvent>(ANY, ON,
				new Enchant(S.SELF, "AT_011e"),
				when: EventTargetIn<OverhealEvent>(S.SELF)));
		// AT_012 暗影子嗣：战吼与激励，均对每个英雄造成 4 点伤害。
		b.Card("AT_012",
			battlecry: Acts(new DealDamage(S.FRIENDLY_HERO, 4), new DealDamage(S.ENEMY_HERO, 4)),
			inspire: Acts(new DealDamage(S.FRIENDLY_HERO, 4), new DealDamage(S.ENEMY_HERO, 4)));
		// AT_013 真言术：耀：选择一个随从，每当其攻击时，为你的英雄恢复 4 点生命值。
		b.Card("AT_013",
			play: new Enchant(S.TARGET, "AT_013e"),
			target: Target.Required().Minion());
		// AT_013e 真言术：耀附魔：该随从攻击后，为友方英雄恢复 4 点生命值。
		b.Card("AT_013e",
			on: When<AfterAttackEvent>(OWN, AFTER,
				new RestoreHealth(S.FRIENDLY_HERO, 4),
				when: EventSourceIn<AfterAttackEvent>(S.SELF)));
		// AT_014 暗影魔：每当你抽一张牌时，使其法力值消耗减少（1）。
		b.Card("AT_014",
			on: When<DrawCardEvent>(OWN, ON, new Enchant(S.EVT_TARGET, "AT_014e")));
		// AT_015 策反：复制一个敌方随从置入你的手牌，其法力值消耗变为（1）。
		b.Card("AT_015",
			play: new CopyToHand(S.CONTROLLER, S.TARGET)
				.Then(new Enchant(S.CURRENT_TARGET, "GBL_001e")),
			target: Target.Required().Enemy().Minion());
		// AT_016 错乱：交换所有随从的攻击力和生命值。
		b.Card("AT_016", play: new SwapAtkHealth(S.ALL_MINIONS));
		// AT_018 银色神官帕尔崔丝：战吼与激励，随机召唤一个传说随从。
		b.Card("AT_018",
			battlecry: new Summon(S.CONTROLLER, RandomCard().Minion().Legendary()),
			inspire: new Summon(S.CONTROLLER, RandomCard().Minion().Legendary()));
		// AT_055 快速治疗：恢复 5 点生命值。
		b.Card("AT_055",
			play: new RestoreHealth(S.TARGET, 5),
			target: Target.Required());
		// AT_116 龙眠教官：战吼，若手牌中有龙，则获得 +1 攻击力和嘲讽。
		b.Card("AT_116",
			battlecry: IfElse(HOLDING_DRAGON, new Enchant(S.SELF, "AT_116e"), NOTHING));

		// 潜行者
		// AT_028 影袭骑兵：连击，获得 +4 攻击力。
		b.Card("AT_028", combo: new Enchant(S.SELF, "AT_028e"));
		// AT_029 锈水海盗：每当你装备一把武器，使该武器获得 +1 攻击力。
		b.Card("AT_029",
			on: When<CardPlayedEvent>(OWN, AFTER,
				new Enchant(S.FRIENDLY_WEAPON, "AT_029e"),
				when: PlayedType(CardType.Weapon)));
		// AT_030 幽暗城勇士：连击，造成 1 点伤害。
		b.Card("AT_030",
			combo: new DealDamage(S.TARGET, 1),
			target: Target.IfAvailable());
		// AT_031 窃贼：每当本随从攻击英雄时，将幸运币置入你的手牌。
		b.Card("AT_031",
			on: When<AfterAttackEvent>(OWN, AFTER,
				new AddToHand(S.CONTROLLER, THE_COIN),
				when: And(
					EventSourceIn<AfterAttackEvent>(S.SELF),
					EventTargetIn<AfterAttackEvent>(S.ALL_HEROES))));
		// AT_032 走私商贩：战吼，如果你控制海盗，则获得 +1/+1。
		b.Card("AT_032",
			battlecry: IfElse(HasFriendlyPirate(), new Enchant(S.SELF, "AT_032e"), NOTHING));
		// AT_033 剽窃：随机获得三张对手职业的牌。
		b.Card("AT_033",
			play: new AddToHand(S.CONTROLLER, RandomCard().ClassOf(S.OPPONENT)) * 3);
		// AT_034 淬毒利刃：你的英雄技能不会替换这把武器，而是改为使其获得 +1 攻击力。
		b.Card("AT_034",
			on: When<WeaponEquipEvent>(OWN, BEFORE,
				Acts(
					new CancelWeaponEquip(),
					new Enchant(S.SELF, "AT_034e")),
				when: EventSourceIn<WeaponEquipEvent>(S.FRIENDLY_HERO_POWER)));
		// AT_035 危机四伏：将三张伏击牌洗入对手牌库，对手抽到时为你召唤 4/4 蛛魔。
		b.Card("AT_035",
			play: new ShuffleIntoDeck(S.OPPONENT, "AT_035t") * 3);
		// AT_035t 蛛魔伏击：抽到时为对手召唤 4/4 蛛魔。
		b.Card("AT_035t",
			draw: new Summon(S.OPPONENT, "FP1_007t"));
		// AT_036 阿努巴拉克：亡语，召唤一个 4/4 且具有同样亡语的蛛魔。
		b.Card("AT_036",
			deathrattle: new Summon(S.CONTROLLER, "AT_036t"));
		// AT_036t 蛛魔：亡语，召唤阿努巴拉克。
		b.Card("AT_036t",
			deathrattle: new Summon(S.CONTROLLER, "AT_036"));

		// 萨满
		// AT_046 海象人图腾师：战吼，随机召唤一个基础图腾。
		b.Card("AT_046",
			battlecry: new Summon(S.CONTROLLER, new RandomEntourage()),
			entourage: BASIC_TOTEMS);
		// AT_047 德莱尼图腾师：战吼，每有一个友方图腾便获得 +1/+1。
		b.Card("AT_047",
			battlecry: new Enchant(S.SELF,
				atk: new Count(S.FRIENDLY_MINIONS + S.TOTEM),
				health: new Count(S.FRIENDLY_MINIONS + S.TOTEM)));
		// AT_048 治疗波：恢复 8 点生命值；比武获胜则改为恢复 16 点。
		b.Card("AT_048",
			play: IfElse(JoustWin(), new RestoreHealth(S.TARGET, 16), new RestoreHealth(S.TARGET, 8)),
			target: Target.Required());
		// AT_049 雷鸣图腾勇士：战吼与激励，使你的图腾获得 +2 攻击力。
		b.Card("AT_049",
			battlecry: new Enchant(S.FRIENDLY_MINIONS + S.TOTEM, "AT_084e"),
			inspire: new Enchant(S.FRIENDLY_MINIONS + S.TOTEM, "AT_084e"));
		// AT_050 风暴之锤：亡语，你的英雄技能改为“造成 2 点伤害”。
		b.Card("AT_050",
			deathrattle: new ReplaceHeroPower(S.CONTROLLER, "AT_050t"));
		// AT_051 元素毁灭：对所有随从造成 4 点伤害。
		b.Card("AT_051", play: new DealDamageRandomOnce(S.ALL_MINIONS, 4, 5));
		// AT_052 图腾魔像：过载（1）。
		b.Card("AT_052");
		// AT_053 先祖知识：抽两张牌。
		b.Card("AT_053", play: new DrawCard(S.CONTROLLER) * 2);
		// AT_054 伐木机伊萨尔：战吼，使你手牌和牌库中的所有随从获得 +1/+1。
		b.Card("AT_054",
			battlecry: Acts(
				new Enchant(S.FRIENDLY_HAND + S.MINION, "AT_090e"),
				new Enchant(S.FRIENDLY_DECK + S.MINION, "AT_090e")));

		// 术士
		// AT_019 恐惧战马：亡语，赋予玩家一个会在回合结束时召回本体的附魔。
		b.Card("AT_019",
			deathrattle: new Enchant(S.CONTROLLER, "AT_019e"));
		// AT_020 恐惧末日守卫：白板随从。
		b.Card("AT_020");
		// AT_021 小鬼骑士：每当你弃掉一张牌时，便获得 +2/+1。
		b.Card("AT_021",
			on: When<DiscardEvent>(OWN, AFTER, new Enchant(S.SELF, "AT_021e")));
		// AT_022 加拉克苏斯之拳：当你使用或弃掉这张牌时，随机对一个敌人造成 4 点伤害。
		b.Card("AT_022",
			play: new DealDamage(S.RANDOM_ENEMY_CHARACTER, 4),
			discard: new DealDamage(S.RANDOM_ENEMY_CHARACTER, 4));
		// AT_023 虚空碾压者：激励，随机消灭每个玩家的一个随从。
		b.Card("AT_023",
			inspire: Acts(
				new Destroy(S.Random(S.FRIENDLY_MINIONS)),
				new Destroy(S.Random(S.ENEMY_MINIONS))));
		// AT_024 恶魔融合：使一个恶魔获得 +3/+3。
		b.Card("AT_024",
			play: new Enchant(S.TARGET, "AT_024e"),
			target: Target.Required().Minion().Race(Race.Demon));
		// AT_025 黑暗交易：随机消灭两个敌方随从，并随机弃两张牌。
		b.Card("AT_025",
			play: Acts(
				new Destroy(S.Random(S.ENEMY_MINIONS, 2)),
				new Discard(S.Random(S.FRIENDLY_HAND, 2))));
		// AT_026 愤怒卫士：每当本随从受到伤害，对你的英雄造成等量伤害。
		b.Card("AT_026",
			on: When<DamageEvent>(ANY, ON,
				new DealDamage(S.FRIENDLY_HERO, EVT_AMOUNT),
				when: EventTargetIn<DamageEvent>(S.SELF)));
		// AT_027 威尔弗雷德·菲兹班：通过英雄技能抽到的牌，其法力值消耗变为（0）。
		b.Card("AT_027",
			on: When<DrawCardEvent>(OWN, ON,
				new Enchant(S.EVT_TARGET, "GBL_009e"),
				when: EventSourceIn<DrawCardEvent>(S.FRIENDLY_HERO_POWER)));

		// 战士
		// AT_064 怒袭：造成 3 点伤害，并获得 3 点护甲。
		b.Card("AT_064",
			play: Acts(new DealDamage(S.TARGET, 3), new GainArmor(S.FRIENDLY_HERO, 3)),
			target: Target.Required());
		// AT_065 国王护卫者：战吼，如果你控制具有嘲讽的随从，则获得 +1 耐久度。
		b.Card("AT_065",
			battlecry: IfElse(new Find(S.FRIENDLY_MINIONS + S.TAUNT), new Enchant(S.SELF, "AT_077e"), NOTHING));
		// AT_066 奥格瑞玛狼骑士：激励，使你的武器获得 +1 攻击力。
		b.Card("AT_066",
			inspire: new Enchant(S.FRIENDLY_WEAPON, "AT_066e"));
		b.Buff("AT_066e", atk: 1);
		// AT_067 猛犸人头领：同时对其攻击目标相邻的随从造成伤害。
		b.Card("AT_067",
			on: When<AfterAttackEvent>(OWN, AFTER,
				new DealDamage(S.Adjacent(S.EVT_TARGET), ATK(S.SELF)),
				when: And(
					EventSourceIn<AfterAttackEvent>(S.SELF),
					EventTargetIn<AfterAttackEvent>(S.ALL_MINIONS))));
		// AT_068 加固：使你具有嘲讽的随从获得 +2/+2。
		b.Card("AT_068",
			play: new Enchant(S.FRIENDLY_MINIONS + S.TAUNT, "AT_068e"));
		// AT_069 格斗陪练师：战吼，使一个随从获得嘲讽。
		b.Card("AT_069",
			battlecry: Taunt(S.TARGET),
			target: Target.IfAvailable().Minion());
		// AT_071 阿莱克丝塔萨的勇士：战吼，若你手牌中有龙，则获得 +1 攻击力和冲锋。
		b.Card("AT_071",
			battlecry: IfElse(HOLDING_DRAGON, new Enchant(S.SELF, "AT_071e"), NOTHING));
		// AT_072 瓦里安·乌瑞恩：战吼，抽三张牌，将抽到的随从直接置入战场。
		b.Card("AT_072",
			battlecry: new DrawCard(S.CONTROLLER)
				.IfThen(new Find(S.CURRENT_TARGET + S.MINION), new Summon(S.CONTROLLER, S.CURRENT_TARGET)) * 3);
		// AT_130 破海者：当你抽到这张牌时，对你的随从造成 1 点伤害。
		b.Card("AT_130",
			draw: new DealDamage(S.FRIENDLY_MINIONS, 1));

		// 中立：简单、激励、触发
		// AT_017 暮光守护者：战吼，如果你的手牌中有龙，则获得 +1 攻击力和嘲讽。
		b.Card("AT_017", battlecry: IfElse(HOLDING_DRAGON, new Enchant(S.SELF, "AT_017e"), NOTHING));
		// AT_080 要塞指挥官：每回合你可以使用两次英雄技能。
		b.Card("AT_080",
			update: new[] { new Refresh(S.FRIENDLY_HERO_POWER, GameTag.HeropowerAdditionalActivations, +1) });
		// AT_082 低阶侍从：激励，获得 +1 攻击力。
		b.Card("AT_082", inspire: new Enchant(S.SELF, "AT_082e"));
		// AT_083 龙鹰骑士：激励，本回合获得风怒。
		b.Card("AT_083", inspire: new Enchant(S.SELF, "AT_083e"));
		// AT_084 持枪侍从：战吼，使一个友方随从获得 +2 攻击力。
		b.Card("AT_084",
			battlecry: new Enchant(S.TARGET, "AT_084e"),
			target: Target.IfAvailable().Friendly().Minion());
		// AT_085 湖之仙女：你的英雄技能法力值消耗变为（1）。
		b.Card("AT_085",
			update: new[]
			{
				new Refresh(S.FRIENDLY_HERO_POWER, setTags: new Dictionary<GameTag, object>
				{
					[GameTag.Cost] = 1
				})
			});
		// AT_086 破坏者：战吼，下个回合敌方英雄技能法力值消耗增加（5）。
		b.Card("AT_086",
			battlecry: new Enchant(S.OPPONENT, "AT_086e"));
		// AT_089 白骨卫士军官：激励，获得 +1 生命值。
		b.Card("AT_089", inspire: new Enchant(S.SELF, "AT_089e"));
		// AT_090 穆克拉的勇士：激励，使你的其他随从获得 +1/+1。
		b.Card("AT_090", inspire: new Enchant(S.FRIENDLY_MINIONS - S.SELF, "AT_090e"));
		// AT_091 竞技场医师：激励，为你的英雄恢复 2 点生命值。
		b.Card("AT_091", inspire: new RestoreHealth(S.FRIENDLY_HERO, 2));
		// AT_094 火焰杂耍者：战吼，随机对一个敌人造成 1 点伤害。
		b.Card("AT_094", battlecry: new DealDamage(S.RANDOM_ENEMY_CHARACTER, 1));
		// AT_096 发条骑士：战吼，使一个友方机械获得 +1/+1。
		b.Card("AT_096",
			battlecry: new Enchant(S.TARGET, "AT_032e"),
			target: Target.IfAvailable().Friendly().Minion().Race(Race.Mechanical));
		// AT_099 科多兽骑手：激励，召唤一个 3/5 的作战科多兽。
		b.Card("AT_099", inspire: new Summon(S.CONTROLLER, "AT_099t"));
		// AT_100 白银之手教官：激励，召唤一个 1/1 白银之手新兵。
		b.Card("AT_100", inspire: new Summon(S.CONTROLLER, "CS2_101t"));
		// AT_103 北海海妖：战吼，造成 4 点伤害。
		b.Card("AT_103",
			battlecry: new DealDamage(S.TARGET, 4),
			target: Target.Required());
		// AT_105 受伤的克瓦迪尔：战吼，对自身造成 3 点伤害。
		b.Card("AT_105", battlecry: new DealDamage(S.SELF, 3));
		// AT_106 圣光勇士：战吼，沉默一个恶魔。
		b.Card("AT_106",
			battlecry: new Silence(S.TARGET),
			target: Target.IfAvailable().Minion().Race(Race.Demon));
		// AT_108 重甲战马：战吼，比武获胜则获得冲锋。
		b.Card("AT_108", battlecry: IfElse(JoustWin(), GiveCharge(S.SELF), NOTHING));
		// AT_109 银色警卫：无法攻击；激励，本回合可以正常攻击。
		b.Card("AT_109",
			inspire: new SetTag(S.SELF, GameTag.CantAttack, 0));
		// AT_110 角斗场主管：激励，将本随从移回你的手牌。
		b.Card("AT_110", inspire: new ReturnToHand(S.SELF));
		// AT_111 零食商贩：战吼，为每个英雄恢复 4 点生命值。
		b.Card("AT_111",
			battlecry: Acts(new RestoreHealth(S.FRIENDLY_HERO, 4), new RestoreHealth(S.ENEMY_HERO, 4)));
		// AT_112 大师级枪骑士：战吼，比武获胜则获得嘲讽和圣盾。
		b.Card("AT_112",
			battlecry: IfElse(JoustWin(), Acts(Taunt(S.SELF), GiveDivineShield(S.SELF)), NOTHING));
		// AT_113 征募官：激励，将一个 2/2 侍从置入你的手牌。
		b.Card("AT_113", inspire: new AddToHand(S.CONTROLLER, "AT_113t"));
		// AT_115 击剑教头：战吼，你的下一个英雄技能法力值消耗减少（2）。
		b.Card("AT_115", battlecry: new Enchant(S.CONTROLLER, "AT_115e"));
		// AT_117 庆典司仪：战吼，如果你控制法术伤害随从，则获得 +2/+2。
		b.Card("AT_117",
			battlecry: IfElse(new Find(friendlySpellPowerMinions), new Enchant(S.SELF, "AT_117e"), NOTHING));
		// AT_118 十字军统领：战吼，随机将一张圣骑士牌置入你的手牌。
		b.Card("AT_118", battlecry: new AddToHand(S.CONTROLLER, RandomCard().Class(CardClass.Paladin)));
		// AT_119 克瓦迪尔劫掠者：激励，获得 +2/+2。
		b.Card("AT_119", inspire: new Enchant(S.SELF, "AT_119e"));
		// AT_120 冰霜巨人：每使用一次英雄技能，此牌法力值消耗减少（1）。
		b.Card("AT_120",
			costMod: Of(source => (source as BuffableEntity)?.Controller?.TimesHeroPowerUsedThisGame ?? 0));
		// AT_121 人气选手：每当你使用一张具有战吼的牌，便获得 +1/+1。
		b.Card("AT_121",
			on: When<CardPlayedEvent>(OWN, AFTER,
				new Enchant(S.SELF, "AT_121e"),
				when: PlayedHasMechanic(Mechanic.BATTLECRY)));
		// AT_122 穿刺者戈莫克：战吼，如果你拥有至少四个其他随从，则造成 4 点伤害。
		b.Card("AT_122",
			battlecry: IfElse(
				new LazyNumEvaluator(new Count(S.FRIENDLY_MINIONS - S.SELF), 4, (a, b) => a >= b),
				new DealDamage(S.TARGET, 4),
				NOTHING),
			target: Target.IfAvailable());
		// AT_123 冰喉：嘲讽；亡语，如果你手牌中有龙，则对所有随从造成 3 点伤害。
		b.Card("AT_123",
			deathrattle: IfElse(HOLDING_DRAGON, new DealDamage(S.ALL_MINIONS, 3), NOTHING));
		// AT_124 博尔夫·碎盾：每当你的英雄受到伤害，改为由本随从承担。
		b.Card("AT_124",
			on: When<PreDamageEvent>(OWN, ON,
				new RedirectPreDamageToCharacter(S.SELF),
				when: EventTargetIn<PreDamageEvent>(S.FRIENDLY_HERO)));
		// AT_127 虚灵勇士萨兰德：激励，随机将一张法术牌置入你的手牌。
		b.Card("AT_127", inspire: new AddToHand(S.CONTROLLER, RandomSpell()));
		// AT_128 骑乘战狼骑士：亡语，比武获胜则将其移回你的手牌。
		b.Card("AT_128",
			deathrattle: IfElse(JoustWin(), new ReturnToHand(S.SELF), NOTHING));
		// AT_129 光明邪使菲奥拉：每当你以本随从为目标施放法术时，获得圣盾。
		b.Card("AT_129",
			on: When<SpellCastEvent>(OWN, AFTER,
				GiveDivineShield(S.SELF),
				when: SpellTargets(S.SELF)));
		// AT_131 黑暗邪使艾蒂丝：每当你以本随从为目标施放法术时，随机对一个敌人造成 3 点伤害。
		b.Card("AT_131",
			on: When<SpellCastEvent>(OWN, AFTER,
				new DealDamage(S.RANDOM_ENEMY_CHARACTER, 3),
				when: SpellTargets(S.SELF)));
		// AT_132 裁决者图哈特：以更强的英雄技能替换你的初始英雄技能。
		b.Card("AT_132",
			battlecry: new UpgradeStartingHeroPower(S.CONTROLLER));
		b.Card("AT_132_SHAMANa",
			on: When<TurnEndEvent>(OWN, ON, new RestoreHealth(S.FRIENDLY_MINIONS, 1)));
		b.Card("AT_132_SHAMANe",
			on: When<TurnEndEvent>(OWN, ON, new Enchant(S.RANDOM_OTHER_FRIENDLY_MINION, "AT_082e")));
		// AT_133 加基森枪骑士：战吼，比武获胜则获得 +1/+1。
		b.Card("AT_133",
			battlecry: IfElse(JoustWin(), new Enchant(S.SELF, "AT_133e"), NOTHING));

		// 以下卡主要依赖卡牌数据自带标签或白板属性。
		foreach (var cardId in new[]
		{
			"AT_087", "AT_088", "AT_092", "AT_093", "AT_095", "AT_097",
			"AT_101", "AT_102", "AT_114", "AT_125"
		})
		{
			b.Card(cardId);
		}

		// AT_098 杂耍吞法者：战吼，复制对手的英雄技能。
		b.Card("AT_098", battlecry: new CopyOpponentHeroPower(S.CONTROLLER));
		// AT_070 天空上尉库拉格：冲锋；每有一个友方海盗，此牌法力值消耗减少（1）。
		b.Card("AT_070", costMod: new Count(S.FRIENDLY_MINIONS + S.PIRATE));
	}
}
