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
// 地精大战侏儒 — 卡牌脚本
// ═════════════════════════════════════════

public class GvgCards : CardPack
{
	public override void Register(CardBuilder b)
	{
		// ═══════════════════════════════════════
		// 附魔
		// ═══════════════════════════════════════

		b.Buff("GVG_030ae", atk: 1);              // 阳极氧化幼崽 +1 攻击力
		b.Buff("GVG_030be", health: 1);            // 阳极氧化幼崽 +1 生命值
		b.Buff("GVG_046e", atk: 1);                // 百兽之王 +1 攻击力
		b.Buff("GVG_048e", atk: 2);                // 金属牙飞跃者 +2 攻击力
		b.Buff("GVG_043e", atk: 1);                // 动力弩炮 +1 攻击力
		b.Buff("GVG_060e", atk: 2, health: 2);    // 军需官 +2/+2
		b.Buff("GVG_063a", atk: 1);                // 博尔瓦·弗塔根 +1 攻击力
		b.Buff("GVG_057a", atk: 2,
			on: When<TurnEndEvent>(OWN, ON, new Destroy(S.SELF)));  // 光明封印（本回合）
		b.Buff("GVG_041c", atk: 5, health: 5, extraTags: (GameTag.Taunt, true)); // 暗色低语 +5/+5 嘲讽
		b.Buff("GVG_011a", atk: -2);               // 缩小术师 -2 攻击力
		b.Buff("GVG_069a", health: 4);             // 升级维修机器人 +4 生命值
		b.Buff("GVG_010b", atk: 2, health: 4, extraTags: (GameTag.Spellpower, true)); // 维伦的抉择 +2/+4 法术伤害+1
		b.Buff("GVG_023a", atk: 1);                // 地精剃刀手 +1 攻击力
		b.Buff("GVG_027e", atk: 2, health: 2);    // 钢铁武道家 +2/+2
		b.Buff("GVG_022a", atk: 3);                // 工匠的磨刀油 武器 +3
		b.Buff("GVG_022b", atk: 3);                // 工匠的磨刀油 随从 +3
		b.Buff("GVG_055e", atk: 2, health: 2);    // 螺丝钳废铁工 +2/+2
		b.Buff("GVG_051e", atk: 1);                // 战争机器人 激怒 +1 攻击力
		b.Buff("GVG_086e", atk: 1);                // 攻城引擎 +1 攻击力
		b.Buff("GVG_019e", atk: 5, health: 5);    // 恶魔之心 +5/+5
		b.Buff("GVG_021e", atk: 2, health: 2);    // 玛尔加尼斯 +2/+2
		b.Buff("GVG_100e", atk: 2, health: 2);    // 漂浮观察者 +2/+2
		b.Buff("GVG_067a", atk: 1);                // 石裂碎石岩魔 +1 攻击力
		b.Buff("GVG_068a", atk: 2);                // 壮硕的石腭岩魔 +2 攻击力
		b.Buff("GVG_102e", atk: 1, health: 1);    // 修补匠城技师 +1/+1
		b.Buff("GVG_076a", atk: 1);                // 微型机器 +1 攻击力
		b.Buff("GVG_101e", atk: 1, health: 1);    // 小个子驱魔人 +1/+1
		b.Buff("GVG_104a", atk: 2, health: 2);    // 大胖 +2/+2
		b.Buff("GVG_106e", atk: 2, health: 2);    // 废铁机器人 +2/+2
		b.Buff("BRM_012e", atk: 1);                // 火卫守护者 +1 攻击力（GVG 也使用）
		b.Buff("BRMA16_3e", atk: 3);              // 超音速吐息 +3 攻击力
		b.Buff("BRMA16_5e", atk: 1);              // 龙齿 +1 攻击力
		b.Buff("BRMA10_6e", atk: 1);              // 狂龙之爪 +1 攻击力
		b.Buff("BRMA12_8te", atk: 2, health: 2);  // 多彩龙人 +2/+2
		b.Buff("BRMA15_2He", atk: 2, health: 2);  // 强力药水 +2/+2

		// GVG_017e — 呼唤宠物增益：费用 -4，打出后移除
		b.Buff("GVG_017e", on: REMOVED_IN_PLAY,
			extraTags: (GameTag.Cost, -4));

		// GVG_003e — 不稳定的传送门增益：费用 -3，打出后移除
		b.Buff("GVG_003e", on: REMOVED_IN_PLAY,
			extraTags: (GameTag.Cost, -3));

		// ═══════════════════════════════════════
		// 零件
		// ═══════════════════════════════════════

		b.Buff("PART_001e", health: 1);    // 装甲镀层 +1 生命值
		b.Buff("PART_007e", atk: 1);       // 旋转刀片 +1 攻击力

		// PART_001 — 装甲镀层：使目标随从获得 +1 生命值
		b.Card("PART_001", play: new Enchant(S.TARGET, "PART_001e"),
			target: Target.Required().Minion());

		// PART_002 — 时间倒流：将友方随从移回手牌
		b.Card("PART_002", play: new ReturnToHand(S.TARGET),
			target: Target.Required().Minion().Friendly());

		// PART_003 — 生锈的号角：使随从获得嘲讽
		b.Card("PART_003", play: new Enchant(S.TARGET, "PART_003e"),
			target: Target.Required().Minion());

		// PART_004 — 精密斗篷：使随从获得潜行（简化版：永久而非一回合）
		b.Card("PART_004", play: Stealth(S.TARGET),
			target: Target.Required().Minion().Friendly());

		// PART_005 — 紧急冷却器：冻结目标随从
		b.Card("PART_005", play: Freeze(S.TARGET),
			target: Target.Required().Minion());

		// PART_006 — 反转开关：交换攻防
		b.Card("PART_006", play: new SwapStats(S.TARGET, S.TARGET, "PART_006e"),
			target: Target.Required().Minion());

		// PART_007 — 旋转刀片：+1 攻击力
		b.Card("PART_007", play: new Enchant(S.TARGET, "PART_007e"),
			target: Target.Required().Minion());

		// ═══════════════════════════════════════
		// 德鲁伊
		// ═══════════════════════════════════════

		// GVG_030 — 阳极氧化幼崽：抉择
		b.Card("GVG_030", chooseCards: new[] { "GVG_030a", "GVG_030b" },
			play: new Enchant(S.SELF, "GVG_030ae"));
		b.Card("GVG_030a", play: new Enchant(S.SELF, "GVG_030ae"));
		b.Card("GVG_030b", play: new Enchant(S.SELF, "GVG_030be"));

		// GVG_032 — 树丛守護者：抉择
		b.Card("GVG_032", chooseCards: new[] { "GVG_032a", "GVG_032b" },
			play: new GainMana(S.ALL_PLAYERS, 1));
		b.Card("GVG_032a", play: new GainMana(S.ALL_PLAYERS, 1));
		b.Card("GVG_032b", play: new DrawCard(S.ALL_PLAYERS));

		// GVG_034 — 机械熊猫：受到伤害时将一个随机零件置入你的手牌
		b.Card("GVG_034",
			on: When<DamageEvent>(SELF_TRIGGER, ON, new AddToHand(S.CONTROLLER, RandomSparePart())));

		// GVG_035 — 玛洛恩：亡语 — 洗入拥有者牌库
		b.Card("GVG_035", deathrattle: new ShuffleIntoDeck(S.CONTROLLER, "GVG_035"));

		// GVG_080 — 毒蛇德鲁伊：若你有野兽，变形为 7/7
		b.Card("GVG_080",
			battlecry: IfElse(new Find(S.FRIENDLY_MINIONS + S.BEAST),
				new Transform(S.SELF, "GVG_080t"), NOTHING));

		// GVG_031 — 回收：将敌方随从洗入对手牌库
		b.Card("GVG_031", play: new ShuffleIntoDeck(S.OPPONENT, S.TARGET),
			target: Target.Required().Minion().Enemy());

		// GVG_033 — 生命之树：完全恢复所有角色
		b.Card("GVG_033", play: new FullRestore(S.ALL_CHARACTERS));

		// GVG_041 — 暗色低语：抉择
		b.Card("GVG_041", chooseCards: new[] { "GVG_041a", "GVG_041b" });
		b.Card("GVG_041a", play: new Enchant(S.TARGET, "GVG_041c"),
			target: Target.Required().Minion());
		b.Card("GVG_041b", play: new Summon(S.CONTROLLER, "CS2_231") * 5);

		// ═══════════════════════════════════════
		// 猎人
		// ═══════════════════════════════════════

		// GVG_046 — 百兽之王：每有一个友方野兽获得 +1 攻击力
		b.Card("GVG_046", battlecry: new Enchant(S.SELF, "GVG_046e") * new Count(S.FRIENDLY_MINIONS + S.BEAST));

		// GVG_048 — 金属牙飞跃者：使一个随机其他友方机械获得 +2 攻击力
		b.Card("GVG_048", battlecry: new Enchant(S.Random(S.FRIENDLY_MINIONS + S.MECH - S.SELF), "GVG_048e"));

		// GVG_049 — 加兹瑞拉：每当该随从受到伤害时，攻击力翻倍
		b.Card("GVG_049",
			on: When<DamageEvent>(SELF_TRIGGER, ON, new DoubleStats(S.SELF)));

		// GVG_087 — 蒸汽码头狙击手：光环 — 你的英雄技能可以指定目标
		b.Card("GVG_087", update: new[]
		{
			new Refresh(S.CONTROLLER, new() { { GameTag.SteadyShotCanTarget, 1 } })
		});

		// GVG_017 — 呼唤宠物：抽一张牌，若为野兽则费用 -4
		b.Card("GVG_017", play: new DrawCard(S.CONTROLLER).Then(
			new ConditionalAction(
				new Find(S.TARGET + S.BEAST),
				new Action[] { new Enchant(S.TARGET, "GVG_017e") })));

		// GVG_026 — 假死：触发所有友方随从的亡语
		b.Card("GVG_026", play: new TriggerDeathrattle(S.FRIENDLY_MINIONS));

		// GVG_073 — 眼镜蛇射击：对目标和敌方英雄各造成 3 点伤害
		b.Card("GVG_073", play: Acts(new DealDamage(S.TARGET, 3), new DealDamage(S.ENEMY_HERO, 3)),
			target: Target.Required().Minion());

		// GVG_043 — 动力弩炮：战吼 — 使一个随机友方随从获得 +1 攻击力
		b.Card("GVG_043", battlecry: new Enchant(S.RANDOM_FRIENDLY_MINION, "GVG_043e"));

		// ═══════════════════════════════════════
		// 法师
		// ═══════════════════════════════════════

		// GVG_002 — 雪人：冻结受到伤害的角色（通过冻结标签）
		b.Card("GVG_002");

		// GVG_004 — 地精爆破手：若你有机械，随机造成 1 点伤害 ×4
		b.Card("GVG_004",
			battlecry: IfElse(new Find(S.FRIENDLY_MINIONS + S.MECH),
				new DealDamage(S.RANDOM_ENEMY_CHARACTER, 1) * 4,
				new DealDamage(S.RANDOM_ENEMY_CHARACTER, 1) * 2));

		// GVG_007 — 烈焰巨兽：被抽到时对所有角色造成 2 点伤害
		b.Card("GVG_007", draw: new DealDamage(S.ALL_CHARACTERS, 2));

		// GVG_001 — 烈焰加农炮：对一个随机敌方随从造成 4 点伤害
		b.Card("GVG_001", battlecry: new DealDamage(S.RANDOM_ENEMY_MINION, 4));

		// GVG_003 — 不稳定的传送门：将一个随机随从置入你的手牌（费用 -3）
		b.Card("GVG_003", play: new AddToHand(S.CONTROLLER, RandomMinion()).Then(
			new Enchant(S.TARGET, "GVG_003e")));

		// GVG_005 — 麦迪文的回音：将所有友方随从的副本复制到手牌
		b.Card("GVG_005", play: new CopyToHand(S.CONTROLLER, S.FRIENDLY_MINIONS));

		// ═══════════════════════════════════════
		// 圣骑士
		// ═══════════════════════════════════════

		// GVG_060 — 军需官：使所有友方白银之手新兵获得 +2/+2
		b.Card("GVG_060", battlecry: new Enchant(S.FRIENDLY_MINIONS + S.Id("CS2_101t"), "GVG_060e"));

		// GVG_062 — 钴蓝守卫：每当你召唤一个机械，获得圣盾
		b.Card("GVG_062",
			on: When<MinionSummonedEvent>(OWN, AFTER, GiveDivineShield(S.SELF)));

		// GVG_063 — 博尔瓦·弗塔根：在手中时，每当一个友方随从死亡获得 +1 攻击力
		b.Card("GVG_063",
			on: When<DeathEvent>(OWN, AFTER, new Enchant(S.SELF, "GVG_063a"),
				when: DeadIsMinion()).InHand());

		// GVG_101 — 猩红净化者：对所有具有亡语的随从造成 2 点伤害
		b.Card("GVG_101", battlecry: new DealDamage(S.ALL_MINIONS + S.DEATHRATTLE, 2));

		// GVG_057 — 光明封印：恢复 4 点生命值，本回合获得 +2 攻击力
		b.Card("GVG_057", play: Acts(new RestoreHealth(S.FRIENDLY_HERO, 4), new Enchant(S.FRIENDLY_HERO, "GVG_057a")));

		// GVG_061 — 动员：召唤 3 个新兵 + 装备光明之剑
		b.Card("GVG_061", play: Acts(
			new Summon(S.CONTROLLER, "CS2_101t"), new Summon(S.CONTROLLER, "CS2_101t"), new Summon(S.CONTROLLER, "CS2_101t"),
			new Summon(S.CONTROLLER, "CS2_091")));

		// GVG_059 — 齿轮锤：使一个随机友方随从获得嘲讽和圣盾
		b.Card("GVG_059", play: Acts(
			new SetTag(S.RANDOM_FRIENDLY_MINION, GameTag.Taunt, 1),
			new SetTag(S.RANDOM_FRIENDLY_MINION, GameTag.DivineShield, 1)));

		// ═══════════════════════════════════════
		// 牧师
		// ═══════════════════════════════════════

		// GVG_009 — 暗影轰炸者：对双方英雄各造成 3 点伤害
		b.Card("GVG_009", battlecry: new DealDamage(S.ALL_HEROES, 3));

		// GVG_011 — 缩小术师：使目标随从 -2 攻击力
		b.Card("GVG_011", battlecry: new Enchant(S.TARGET, "GVG_011a"),
			target: Target.IfAvailable().Minion());

		// GVG_014 — 沃金：与目标交换生命值
		b.Card("GVG_014", battlecry: new SwapStats(S.SELF, S.TARGET, "GVG_014a"),
			target: Target.IfAvailable().Minion());

		// GVG_072 — 暗影拳击手：每当一个角色被治疗，对一个随机敌方角色造成 1 点伤害
		b.Card("GVG_072",
			on: When<HealEvent>(ANY, AFTER, new DealDamage(S.RANDOM_ENEMY_CHARACTER, 1)));

		// GVG_083 — 升级维修机器人：使一个友方机械获得 +4 生命值
		b.Card("GVG_083", battlecry: new Enchant(S.TARGET, "GVG_069a"),
			target: Target.IfAvailable().Minion().Friendly());

		// GVG_008 — 光炸弹：对每个随从造成等于其攻击力的伤害
		b.Card("GVG_008", play: new ForEach(S.ALL_MINIONS,
			new DealDamage(S.TARGET, new Attr(S.TARGET, GameTag.Atk))));

		// GVG_010 — 维伦的抉择：+2/+4 和法术伤害 +1
		b.Card("GVG_010", play: new Enchant(S.TARGET, "GVG_010b"),
			target: Target.Required().Minion());

		// GVG_012 — 纳鲁之光：恢复 3 点生命，若仍受伤则召唤 1 个光明行者
		b.Card("GVG_012", play: Acts(
			new RestoreHealth(S.TARGET, 3),
			new ConditionalAction(
				new Find(S.TARGET + S.DAMAGED),
				new Action[] { new Summon(S.CONTROLLER, "EX1_001") })),
			target: Target.Required());

		// ═══════════════════════════════════════
		// 盗贼
		// ═══════════════════════════════════════

		// GVG_023 — 地精剃刀手：使你的武器获得 +1 攻击力
		b.Card("GVG_023", battlecry: new Enchant(S.FRIENDLY_WEAPON, "GVG_023a"));

		// GVG_025 — 独眼欺诈者：每当你召唤一个海盗，获得潜行
		b.Card("GVG_025",
			on: When<MinionSummonedEvent>(OWN, AFTER, Stealth(S.SELF)));

		// GVG_027 — 钢铁武道家：回合结束时使一个随机其他友方机械获得 +2/+2
		b.Card("GVG_027",
			on: When<TurnEndEvent>(OWN, ON, new Enchant(S.Random(S.FRIENDLY_MINIONS + S.MECH - S.SELF), "GVG_027e")));

		// GVG_028 — 贸易大王加里维克斯：对手施放法术后将副本加入手牌并给对手硬币
		b.Card("GVG_028",
			on: When<SpellCastEvent>(ENEMY, AFTER,
				new CopyToHand(S.CONTROLLER, S.TARGET), new AddToHand(S.OPPONENT, "GVG_028t")));


		// GVG_028t — 加里维克斯的硬币
		b.Card("GVG_028t", play: new GainTemporaryMana(S.CONTROLLER, 1));

		// GVG_088 — 食人魔忍者：50% 概率攻击错误目标（FORGETFUL 在战斗阶段处理）
		b.Card("GVG_088");

		// GVG_022 — 工匠的磨刀油：武器 +3，连击：随从也 +3
		b.Card("GVG_022", play: new Enchant(S.FRIENDLY_WEAPON, "GVG_022a"),
			combo: Acts(new Enchant(S.FRIENDLY_WEAPON, "GVG_022a"),
				new Enchant(S.RANDOM_FRIENDLY_CHARACTER, "GVG_022b")));

		// GVG_047 — 破坏：消灭一个随机敌方随从；连击：同时消灭敌方武器
		b.Card("GVG_047", play: new Destroy(S.RANDOM_ENEMY_MINION),
			combo: Acts(new Destroy(S.RANDOM_ENEMY_MINION), new Destroy(S.ENEMY_WEAPON)));

		// GVG_024 — 齿轮大师之扳手：若你有机械则 +2 攻击力
		b.Card("GVG_024", update: new[]
		{
			new Refresh(S.SELF, new() { { GameTag.Atk, 2 } },
				condition: new Find(S.FRIENDLY_MINIONS + S.MECH))
		});

		// ═══════════════════════════════════════
		// 萨满
		// ═══════════════════════════════════════

		// GVG_039 — 生命图腾：回合结束时恢复英雄 4 点生命值
		b.Card("GVG_039",
			on: When<TurnEndEvent>(OWN, ON, new RestoreHealth(S.FRIENDLY_HERO, 4)));

		// GVG_040 — 泥鳍先知：友方鱼人死亡时抽一张牌
		b.Card("GVG_040",
			on: When<DeathEvent>(OWN, AFTER, new DrawCard(S.CONTROLLER),
				when: DeadRace(Race.Murloc)));

		// GVG_042 — 海王：将 4 个随机鱼人置入你的手牌
		b.Card("GVG_042", battlecry: new AddToHand(S.CONTROLLER, RandomMurloc()) * 4);

		// GVG_066 — 沙丘魔术师：FORGETFUL
		b.Card("GVG_066");

		// GVG_029 — 先祖回响：从双方手牌各召唤一个随机随从
		b.Card("GVG_029", play: Acts(
			new Summon(S.CONTROLLER, S.Random(S.FRIENDLY_HAND + S.MINION)),
			new Summon(S.OPPONENT, S.Random(S.ENEMY_HAND + S.MINION))));

		// GVG_038 — 碎裂：造成 3~6 点随机伤害
		b.Card("GVG_038", play: new DealDamage(S.TARGET, new RandomAmount(3, 6)),
			target: Target.Required());

		// GVG_036 — 动力锤：亡语 — 使一个随机友方机械获得 +2/+2
		b.Buff("GVG_036e", atk: 2, health: 2);
		b.Card("GVG_036", deathrattle: new Enchant(S.Random(S.FRIENDLY_MINIONS + S.MECH), "GVG_036e"));

		// ═══════════════════════════════════════
		// 术士
		// ═══════════════════════════════════════

		// GVG_020 — 魔能加农炮：回合结束时对一个随机非机械随从造成 2 点伤害
		b.Card("GVG_020",
			on: When<TurnEndEvent>(OWN, ON, new DealDamage(S.Random(S.ALL_MINIONS - S.MECH), 2)));

		// GVG_021 — 玛尔加尼斯：光环 — 友方恶魔 +2/+2，英雄免疫
		b.Card("GVG_021", update: new[]
		{
			new Refresh(S.FRIENDLY_MINIONS + S.DEMON - S.SELF, buff: "GVG_021e"),
			new Refresh(S.FRIENDLY_HERO, new() { { GameTag.CantBeDamaged, 1 } })
		});

		// GVG_077 — 活化傀儡：回合结束时若没有其他友方随从，自毁
		b.Card("GVG_077",
			on: When<TurnEndEvent>(OWN, ON, new Destroy(S.SELF),
				when: Not(new Find(S.FRIENDLY_MINIONS - S.SELF))));

		// GVG_100 — 漂浮观察者：你的英雄在你的回合受到伤害时获得 +2/+2
		b.Card("GVG_100",
			on: When<DamageEvent>(ANY, AFTER, new Enchant(S.SELF, "GVG_100e"),
				when: DamagedIsFriendlyHero()));

		// GVG_015 — 暗黑炸弹：造成 3 点伤害
		b.Card("GVG_015", play: new DealDamage(S.TARGET, 3),
			target: Target.Required());

		// GVG_019 — 恶魔之心：若目标为友方恶魔则 +5/+5，否则造成 5 点伤害
		b.Card("GVG_019", play: new ConditionalAction(
			new Find(S.TARGET + S.FRIENDLY + S.DEMON),
			new Action[] { new Enchant(S.TARGET, "GVG_019e") },
			new Action[] { new DealDamage(S.TARGET, 5) }),
			target: Target.Required().Minion());

		// GVG_045 — 小鬼爆裂：造成 2~4 点随机伤害，召唤等量的 1/1 小鬼
		b.Card("GVG_045", play: new DealDamage(S.TARGET, new RandomAmount(2, 4))
				.Then(new Summon(S.CONTROLLER, "GVG_045t")),
			target: Target.Required().Minion());

		// ═══════════════════════════════════════
		// 战士
		// ═══════════════════════════════════════

		// GVG_051 — 战争机器人：激怒 — +1 攻击力
		b.Card("GVG_051", enrage: new[]
		{
			new Refresh(S.SELF, buff: "GVG_051e")
		});

		// GVG_053 — 铁甲侍女：战吼 — +5 护甲
		b.Card("GVG_053", battlecry: new GainArmor(S.FRIENDLY_HERO, 5));

		// GVG_055 — 螺丝钳废铁工：使目标友方机械获得 +2/+2
		b.Card("GVG_055", battlecry: new Enchant(S.TARGET, "GVG_055e"),
			target: Target.IfAvailable().Minion().Friendly());

		// GVG_056 — 钢铁巨象：将一颗潜伏地雷洗入对手牌库
		b.Card("GVG_056", battlecry: new ShuffleIntoDeck(S.OPPONENT, "GVG_056t"));

		// GVG_056t — 潜伏地雷：抽到时施放，对英雄造成 10 点伤害
		b.Card("GVG_056t", play: new DealDamage(S.FRIENDLY_HERO, 10),
			draw: CastWhenDrawn());

		// GVG_086 — 攻城引擎：每当你获得护甲时获得 +1 攻击力
		b.Card("GVG_086",
			on: When<GainArmorEvent>(OWN, ON, new Enchant(S.SELF, "GVG_086e"),
				when: GainArmorIsFriendly()));

		// GVG_050 — 弹射之刃：反复对随机随从造成 1 点伤害直至有随从死亡
		b.Card("GVG_050", play: new RepeatingDamage(S.ALL_MINIONS));

		// GVG_052 — 碾压：消灭目标随从，若你有受伤随从则费用 -4
		b.Card("GVG_052", play: new Destroy(S.TARGET),
			target: Target.Required().Minion(),
			costModFunc: SetToIf(new Find(S.FRIENDLY_MINIONS + S.DAMAGED), 3));

		// GVG_054 — 食人魔战锤：FORGETFUL
		b.Card("GVG_054");

		// ═══════════════════════════════════════
		// 中立 普通
		// ═══════════════════════════════════════

		// GVG_006 — 机械跃迁者：友方机械费用减少 1
		b.Card("GVG_006", update: new[]
		{
			new Refresh(S.FRIENDLY_HAND + S.MECH, new() { { GameTag.Cost, -1 } })
		});

		// GVG_013 — 齿轮大师：若你有机械则 +2 攻击力
		b.Card("GVG_013", update: new[]
		{
			new Refresh(S.SELF, new() { { GameTag.Atk, 2 } },
				condition: new Find(S.FRIENDLY_MINIONS + S.MECH))
		});

		// GVG_065 — 食人魔蛮兵：FORGETFUL
		b.Card("GVG_065");

		// GVG_067 — 石裂碎石岩魔：对手施放法术时获得 +1 攻击力
		b.Card("GVG_067",
			on: When<SpellCastEvent>(ENEMY, AFTER, new Enchant(S.SELF, "GVG_067a")));

		// GVG_068 — 壮硕的石腭岩魔：有人施放法术时获得 +2 攻击力
		b.Card("GVG_068",
			on: When<SpellCastEvent>(ANY, AFTER, new Enchant(S.SELF, "GVG_068a")));

		// GVG_069 — 古代治疗机器人：战吼 — 恢复英雄 8 点生命值
		b.Card("GVG_069", battlecry: new RestoreHealth(S.FRIENDLY_HERO, 8));

		// GVG_075 — 船载加农炮：每当你召唤一个海盗，对一个随机敌方角色造成 2 点伤害
		b.Card("GVG_075",
			on: When<MinionSummonedEvent>(OWN, AFTER, new DealDamage(S.RANDOM_ENEMY_CHARACTER, 2),
				when: SummonedRace(Race.Pirate)));

		// GVG_076 — 爆炸绵羊：亡语 — 对所有随从造成 2 点伤害
		b.Card("GVG_076", deathrattle: new DealDamage(S.ALL_MINIONS, 2));

		// GVG_078 — 机械雪人：亡语 — 给双方各一个随机零件
		b.Card("GVG_078", deathrattle: Acts(
			new AddToHand(S.CONTROLLER, RandomSparePart()),
			new AddToHand(S.OPPONENT, RandomSparePart())));

		// GVG_082 — 机械侏儒：亡语 — 将一个零件置入你的手牌
		b.Card("GVG_082", deathrattle: new AddToHand(S.CONTROLLER, RandomSparePart()));

		// GVG_090 — 疯狂投弹者：随机造成 1 点伤害 ×6
		b.Card("GVG_090", battlecry: new DealDamage(S.RANDOM_OTHER_CHARACTER, 1) * 6);

		// GVG_096 — 载人粉碎机：亡语 — 召唤一个随机 2 费随从
		b.Card("GVG_096", deathrattle: new Summon(S.CONTROLLER, RandomMinion().Cost(2)));

		// GVG_102 — 修补匠城技师：若你有机械则 +1/+1 + 一个零件
		b.Card("GVG_102",
			battlecry: IfElse(new Find(S.FRIENDLY_MINIONS + S.MECH),
				new Action[] { new Enchant(S.SELF, "GVG_102e"), new AddToHand(S.CONTROLLER, "PART_001") },
				NOTHING));

		// GVG_103 — 微型机器：每个回合开始时获得 +1 攻击力
		b.Card("GVG_103",
			on: When<TurnStartEvent>(ANY, ON, new Enchant(S.SELF, "GVG_076a")));

		// ═══════════════════════════════════════
		// 中立 稀有
		// ═══════════════════════════════════════

		// GVG_074 — 吉赞女巫：偷取一个随机敌方奥秘
		b.Card("GVG_074", battlecry: new TakeControl(S.Random(S.ENEMY_SECRETS)));

		// GVG_089 — 照亮者：回合结束时若你有奥秘，恢复英雄 4 点生命值
		b.Card("GVG_089",
			on: When<TurnEndEvent>(OWN, ON, new RestoreHealth(S.FRIENDLY_HERO, 4),
				when: new Find(S.FRIENDLY_SECRETS)));

		// GVG_094 — 吉维斯：每个回合结束时为手牌最少方抽牌至 3 张
		b.Card("GVG_094",
			on: When<TurnEndEvent>(ANY, ON, new DrawUntilCount(S.CONTROLLER, 3)));

		// GVG_095 — 地精工兵：若对手手牌 ≥6 张则 +4 攻击力
		b.Card("GVG_095", update: new[]
		{
			new Refresh(S.SELF, new() { { GameTag.Atk, 4 } },
				condition: new LazyNumEvaluator(
					new Count(S.ENEMY_HAND), 6, GE()))
		});

		// GVG_097 — 小个子驱魔人：每有一个敌方亡语随从获得 +1/+1
		b.Card("GVG_097", battlecry: new Enchant(S.SELF, "GVG_101e") * new Count(S.ENEMY_MINIONS + S.DEATHRATTLE));

		// GVG_099 — 投弹手：对一个随机敌方随从造成 4 点伤害
		b.Card("GVG_099", battlecry: new DealDamage(S.RANDOM_ENEMY_MINION, 4));

		// ═══════════════════════════════════════
		// 中立 史诗
		// ═══════════════════════════════════════

		// GVG_016 — 魔能机甲：对手打出卡牌时弃掉你牌库顶部 3 张
		b.Card("GVG_016",
			on: When<CardPlayedEvent>(ENEMY, AFTER,
				new MillCards(S.CONTROLLER), new MillCards(S.CONTROLLER), new MillCards(S.CONTROLLER)));

		// GVG_092 — 侏儒实验体：抽一张牌，若为随从则变形为小鸡
		b.Card("GVG_092", battlecry: new DrawCard(S.CONTROLLER).Then(
			new ConditionalAction(
				new Find(S.TARGET + S.MINION),
				new Action[] { new Transform(S.TARGET, "GVG_092t") })));

		// GVG_104 — 大胖：你打出 1 攻随从时使其获得 +2/+2
		b.Card("GVG_104",
			on: When<CardPlayedEvent>(OWN, AFTER, new Enchant(S.SELF, "GVG_104a"),
				when: PlayedAtk(1)));

		// GVG_105 — 载人天空魔像：亡语 — 召唤一个随机 4 费随从
		b.Card("GVG_105", deathrattle: new Summon(S.CONTROLLER, RandomMinion().Cost(4)));

		// GVG_106 — 废铁机器人：友方机械死亡时获得 +2/+2
		b.Card("GVG_106",
			on: When<DeathEvent>(OWN, AFTER, new Enchant(S.SELF, "GVG_106e"),
				when: DeadRace(Race.Mechanical)));

		// GVG_107 — 强化机甲：随机给友方随从风怒/嘲讽/圣盾
		b.Card("GVG_107", battlecry: new GrantRandomAbility(S.FRIENDLY_MINIONS));

		// GVG_108 — 重组仪：将目标变为同费用的随机随从
		b.Card("GVG_108", battlecry: new Evolve(S.TARGET, 0),
			target: Target.IfAvailable().Minion().Friendly());

		// GVG_121 — 发条巨人：对手手牌每多一张费用减少 1
		b.Card("GVG_121",
			costMod: new Count(S.ENEMY_HAND) * -1);

		// GVG_122 — 矮人法术压制者：相邻随从不能成为法术和英雄技能的目标
		b.Card("GVG_122", update: new[]
		{
			new Refresh(S.SELF_ADJACENT, new() {
				{ GameTag.CantBeTargetedBySpells, 1 },
				{ GameTag.CantBeTargetedByHeroPowers, 1 }
			})
		});

		// ═══════════════════════════════════════
		// 中立 传说
		// ═══════════════════════════════════════

		// GVG_110 — 砰砰博士：召唤 2 个砰砰机器人（两侧各一个）
		b.Card("GVG_110", battlecry: new Summon(S.CONTROLLER, "GVG_110t") * 2);

		// GVG_110t — 砰砰机器人：亡语 — 对一个随机敌方角色造成 1~4 点伤害
		b.Card("GVG_110t", deathrattle: new DealDamage(S.RANDOM_ENEMY_CHARACTER, new RandomAmount(1, 4)));

		// GVG_111 — 米米尔隆的头部：回合开始时若你有 3 个以上机械，销毁它们并召唤 V-07-TR-0N
		b.Card("GVG_111",
			on: When<TurnStartEvent>(OWN, ON, new MimironTransform()));

		// GVG_112 — 食人魔老莫格：所有攻击有 50% 概率攻击错误目标
		b.Card("GVG_112", update: new[]
		{
			new Refresh(S.ALL_CHARACTERS, new() { { GameTag.Forgetful, 1 } })
		});

		// GVG_113 — 仇敌收割者4000型：攻击时同时伤害相邻随从
		b.Card("GVG_113",
			on: When<AfterAttackEvent>(OWN, AFTER,
				new DealDamage(S.Adjacent(S.CURRENT_TARGET), new Attr(S.SELF, GameTag.Atk)),
				when: SelfIsAttacker()));

		// GVG_114 — 斯尼德的老伐木机：亡语 — 召唤一个随机传说随从
		b.Card("GVG_114", deathrattle: new Summon(S.CONTROLLER, RandomLegendaryMinion()));

		// GVG_115 — 托什雷：战吼 + 亡语 — 各获得一个零件
		b.Card("GVG_115", battlecry: new AddToHand(S.CONTROLLER, RandomSparePart()),
			deathrattle: new AddToHand(S.CONTROLLER, RandomSparePart()));

		// GVG_116 — 麦田傀儡工程师：敌方随从死亡时召唤一个麻风侏儒
		b.Card("GVG_116",
			on: When<DeathEvent>(ANY, AFTER, new Summon(S.CONTROLLER, "EX1_029"),
				when: DeadIsEnemy()));

		// GVG_117 — 加兹鲁维：你使用 1 费法术时将一个随机机械置入手牌
		b.Card("GVG_117",
			on: When<CardPlayedEvent>(OWN, AFTER, new AddToHand(S.CONTROLLER, RandomMech()),
				when: PlayedIsSpellCost(1)));

		// GVG_118 — 大胖岩魔：对手施放法术后召唤一个壮硕的石腭岩魔
		b.Card("GVG_118",
			on: When<SpellCastEvent>(ENEMY, AFTER, new Summon(S.CONTROLLER, "GVG_068")));

		// GVG_119 — 闪金3000型：给双方各一把随机武器
		b.Card("GVG_119", battlecry: Acts(
			new AddToHand(S.CONTROLLER, RandomWeapon()),
			new AddToHand(S.OPPONENT, RandomWeapon())));

		// GVG_120 — 赫美特·奈辛瓦里：战吼 — 消灭目标野兽
		b.Card("GVG_120", battlecry: new Destroy(S.TARGET),
			target: Target.IfAvailable().Minion().Race(Race.Beast));
	}
}
