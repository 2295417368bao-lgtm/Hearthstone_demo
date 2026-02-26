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
// 黑石山 — 可收集卡牌脚本
// ═════════════════════════════════════════

public class BlackrockCollectible : CardPack
{
	public override void Register(CardBuilder b)
	{
		// ═══════════════════════════════════════
		// 附魔
		// ═══════════════════════════════════════

		b.Buff("BRM_004e", health: 2);             // 暮光幼龙 +2 生命值
		b.Buff("BRM_012e", atk: 1);                // 火卫守护者 +1 攻击力
		b.Buff("BRM_014e", atk: 3, health: 3);    // 核心犬 +3/+3
		b.Buff("BRM_020e", atk: 1, health: 1);    // 龙人巫师 +1/+1
		b.Buff("BRM_024e", atk: 3, health: 3);    // 龙骑粉碎者 +3/+3
		b.Buff("BRM_033e", atk: 1, health: 1);    // 黑翼技师 +1/+1

		// BRM_028e — 索瑞森大帝：费用 -1，打出后移除
		b.Buff("BRM_028e", on: REMOVED_IN_PLAY,
			extraTags: (GameTag.Cost, -1));

		// BRM_003e — 龙之吐息（未使用）：费用 -3，打出后移除
		b.Buff("BRM_003e", on: REMOVED_IN_PLAY,
			extraTags: (GameTag.Cost, -3));

		// BRM_001e — 熔化：攻击力设为 0
		b.Buff("BRM_001e", atkMod: (_, _) => 0);

		// ═══════════════════════════════════════
		// 随从 — 事件
		// ═══════════════════════════════════════

		// BRM_002 — 火妖：每当你施放一个法术后，对随机敌方角色造成 1 点伤害×2
		b.Card("BRM_002",
			on: new[] {
				When<SpellCastEvent>(OWN, AFTER, new DealDamage(S.RANDOM_ENEMY_CHARACTER, 1)),
				When<SpellCastEvent>(OWN, AFTER, new DealDamage(S.RANDOM_ENEMY_CHARACTER, 1))
			});

		// BRM_006 — 小鬼首领：每当该随从受到伤害时，召唤一个 1/1 的小鬼
		b.Card("BRM_006",
			on: When<DamageEvent>(SELF_TRIGGER, ON, new Summon(S.CONTROLLER, "BRM_006t")));

		// BRM_016 — 甩斧者：每当该随从受到伤害时，对敌方英雄造成 2 点伤害
		b.Card("BRM_016",
			on: When<DamageEvent>(SELF_TRIGGER, ON, new DealDamage(S.ENEMY_HERO, 2)));

		// BRM_019 — 恐怖的奴隶主：每当受到伤害且存活，召唤一个副本
		b.Card("BRM_019",
			on: When<DamageEvent>(SELF_TRIGGER, ON, new Summon(S.CONTROLLER, "BRM_019"),
				when: SelfIsAlive()));

		// BRM_020 — 龙人巫师：每当你用法术指向该随从时，获得 +1/+1
		b.Card("BRM_020",
			on: When<SpellCastEvent>(OWN, AFTER, new Enchant(S.SELF, "BRM_020e"),
				when: SpellTargetsSelf()));

		// BRM_022 — 龙蛋：每当该随从受到伤害时，召唤一个 2/1 的雏龙
		b.Card("BRM_022",
			on: When<DamageEvent>(SELF_TRIGGER, ON, new Summon(S.CONTROLLER, "BRM_022t")));

		// BRM_028 — 索瑞森大帝：回合结束时，你手牌中所有卡牌的法力值消耗减少 (1) 点
		b.Card("BRM_028",
			on: When<TurnEndEvent>(OWN, ON, new Enchant(S.FRIENDLY_HAND, "BRM_028e")));

		// BRM_031 — 克洛玛古斯：每当你抽牌时，将该牌的副本置入你的手牌
		b.Card("BRM_031",
			on: When<DrawCardEvent>(OWN, AFTER, new CopyToHand(S.CONTROLLER, S.TARGET)));

		// ═══════════════════════════════════════
		// 随从 — 战吼
		// ═══════════════════════════════════════

		// BRM_004 — 暮光幼龙：若手中有龙，获得 +2 生命值
		b.Card("BRM_004",
			battlecry: IfElse(HOLDING_DRAGON,
				new Enchant(S.SELF, "BRM_004e"), NOTHING));

		// BRM_008 — 暗铁潜伏者：战吼：对所有未受伤的敌方随从造成 2 点伤害
		b.Card("BRM_008", battlecry: new DealDamage(S.ENEMY_MINIONS - S.DAMAGED, 2));

		// BRM_012 — 火卫守护者：战吼：随机获得 1~4 点攻击力
		b.Card("BRM_012", battlecry: new Enchant(S.SELF, "BRM_012e") * new RandomAmount(1, 4));

		// BRM_014 — 核心犬：若手牌为空，获得 +3/+3
		b.Card("BRM_014",
			battlecry: IfElse(EMPTY_HAND,
				new Enchant(S.SELF, "BRM_014e"), NOTHING));

		// BRM_024 — 龙骑粉碎者：若敌方英雄生命值 ≤15，获得 +3/+3
		b.Card("BRM_024",
			battlecry: IfElse(
				new LazyNumEvaluator(new Attr(S.ENEMY_HERO, GameTag.Health), 15, LE()),
				new Enchant(S.SELF, "BRM_024e"), NOTHING));

		// BRM_026 — 饥饿的巨龙：战吼：为对手召唤一个随机 1 费随从
		b.Card("BRM_026", battlecry: new Summon(S.OPPONENT, RandomMinion().Cost(1)));

		// BRM_027 — 管理者埃克索图斯：亡语 — 将英雄替换为拉格纳罗斯
		b.Card("BRM_027", deathrattle: new Summon(S.CONTROLLER, "BRM_027h"));

		// BRM_027p — 去死吧，虫子！：对一个随机敌方角色造成 8 点伤害
		b.Card("BRM_027p", activate: new DealDamage(S.RANDOM_ENEMY_CHARACTER, 8));
		b.Card("BRM_027pH", activate: new DealDamage(new RandomSelector(S.ENEMY_CHARACTERS, 2), 8));

		// BRM_029 — 勒索斯·霜语：若手中有龙，消灭一个目标传说随从
		b.Card("BRM_029",
			battlecry: new Destroy(S.TARGET),
			target: Target.IfAvailable().Minion().Legendary().WithDragonInHand());

		// BRM_030 — 奈法利安：战吼：将 2 个随机法术置入你的手牌
		b.Card("BRM_030", battlecry: new AddToHand(S.CONTROLLER, RandomSpell()) * 2);

		// BRM_030t — 尾部横扫：造成 4 点伤害
		b.Card("BRM_030t", play: new DealDamage(S.TARGET, 4),
			target: Target.Required());

		// BRM_033 — 黑翼技师：若手中有龙，获得 +1/+1
		b.Card("BRM_033",
			battlecry: IfElse(HOLDING_DRAGON,
				new Enchant(S.SELF, "BRM_033e"), NOTHING));

		// BRM_034 — 黑翼腐蚀者：若手中有龙，造成 3 点伤害
		b.Card("BRM_034",
			battlecry: IfElse(HOLDING_DRAGON,
				new DealDamage(S.TARGET, 3), NOTHING),
			target: Target.IfAvailable());

		// ═══════════════════════════════════════
		// 随从 — 光环
		// ═══════════════════════════════════════

		// BRM_018 — 龙王配偶：战吼：你的下一条龙法力值消耗减少 (2) 点
		b.Card("BRM_018", battlecry: new Enchant(S.CONTROLLER, "BRM_018e"));

		// BRM_018e — 龙王配偶增益：光环 -2 费龙牌，打出龙后销毁
		b.Card("BRM_018e",
			update: new[]
			{
				new Refresh(S.FRIENDLY_HAND + S.DRAGON, new() { { GameTag.Cost, -2 } })
			},
			on: When<CardPlayedEvent>(OWN, AFTER, new Destroy(S.SELF),
				when: PlayedRace(Race.Dragon)));

		// ═══════════════════════════════════════
		// 随从 — 费用修改
		// ═══════════════════════════════════════

		// BRM_009 — 火山伐木者：本回合每有一个随从死亡，法力值消耗减少 (1) 点
		b.Card("BRM_009",
			costMod: new Count(S.KILLED_THIS_TURN + S.MINION) * -1);

		// BRM_025 — 火山巨龙：同上
		b.Card("BRM_025",
			costMod: new Count(S.KILLED_THIS_TURN + S.MINION) * -1);

		// ═══════════════════════════════════════
		// 随从 — 抉择（德鲁伊）
		// ═══════════════════════════════════════

		// BRM_010 — 烈焰德鲁伊
		b.Card("BRM_010", chooseCards: new[] { "BRM_010a", "BRM_010b" });
		b.Card("BRM_010a", play: new Transform(S.SELF, "BRM_010t"));
		b.Card("BRM_010b", play: new Transform(S.SELF, "BRM_010t2"));

		// ═══════════════════════════════════════
		// 法术
		// ═══════════════════════════════════════

		// BRM_001 — 严肃的守夜：抽 2 张牌
		b.Card("BRM_001", play: new DrawCard(S.CONTROLLER) * 2,
			costMod: new Count(S.KILLED_THIS_TURN + S.MINION) * -1);

		// BRM_003 — 龙息术：造成 4 点伤害
		b.Card("BRM_003", play: new DealDamage(S.TARGET, 4),
			target: Target.Required(),
			costMod: new Count(S.KILLED_THIS_TURN + S.MINION) * -1);

		// BRM_005 — 恶魔之怒：对所有非恶魔随从造成 2 点伤害
		b.Card("BRM_005", play: new DealDamage(S.ALL_MINIONS - S.DEMON, 2));

		// BRM_007 — 组队行动：将 3 个目标随从的副本洗入你的牌库
		b.Card("BRM_007", play: new ShuffleIntoDeck(S.CONTROLLER, S.TARGET) * 3,
			target: Target.Required().Minion());

		// BRM_011 — 熔岩震击：造成 2 点伤害，解锁过载的法力水晶
		b.Card("BRM_011", play: Acts(
			new DealDamage(S.TARGET, 2),
			new UnlockOverload(S.CONTROLLER)),
			target: Target.Required());

		// BRM_013 — 快速射击：造成 3 点伤害，若手牌为空则抽一张牌
		b.Card("BRM_013", play: Acts(
			new DealDamage(S.TARGET, 3),
			new ConditionalAction(EMPTY_HAND,
				new Action[] { new DrawCard(S.CONTROLLER) })),
			target: Target.Required());

		// BRM_015 — 复仇：对所有随从造成 1 点伤害（英雄生命值 ≤12 时造成 3 点）
		b.Card("BRM_015",
			play: IfElse(
				new LazyNumEvaluator(new Attr(S.FRIENDLY_HERO, GameTag.Health), 12, LE()),
				new DealDamage(S.ALL_MINIONS, 3), new DealDamage(S.ALL_MINIONS, 1)));

		// BRM_017 — 复活术：召唤一个已死亡的随机友方随从的副本
		b.Card("BRM_017", play: new SummonCopy(S.CONTROLLER,
			S.Random(S.FRIENDLY_GRAVEYARD + S.MINION)));
	}
}
