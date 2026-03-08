using HearthstoneServer.Auras;
using HearthstoneServer.Events;
using static HearthstoneServer.Dsl.BusEvent;
using static HearthstoneServer.Dsl.EventFilters;
using static HearthstoneServer.Dsl.RandomCards;
using static HearthstoneServer.Cards.CardScriptUtils;
using HearthstoneServer.Targeting;

namespace HearthstoneServer.Dsl.Cards;

// ═════════════════════════════════════════
// 黑石山 — 冒险模式（Boss）卡牌脚本
// ═════════════════════════════════════════

public class BlackrockAdventure : CardPack
{
	public override void Register(CardBuilder b)
	{
		// ═══════════════════════════════════════
		// 附魔
		// ═══════════════════════════════════════

		b.Buff("BRMA05_3e_dmg");  // 活体炸弹 — 事件附魔占位

		// ═══════════════════════════════════════
		// 英雄技能
		// ═══════════════════════════════════════

		// BRMA01_2 — 堆上去！：从双方牌库各召唤一个随机随从
		b.Card("BRMA01_2", activate: Acts(
			new Summon(S.CONTROLLER, S.Random(S.FRIENDLY_DECK + S.MINION)),
			new Summon(S.OPPONENT, S.Random(S.ENEMY_DECK + S.MINION))));
		b.Card("BRMA01_2H", activate: Acts(
			new Summon(S.CONTROLLER, S.Random(S.FRIENDLY_DECK + S.MINION)),
			new Summon(S.CONTROLLER, S.Random(S.FRIENDLY_DECK + S.MINION)),
			new Summon(S.OPPONENT, S.Random(S.ENEMY_DECK + S.MINION))));

		// BRMA01_3 — 黑铁弹跳者：仅属性（乱斗必胜）
		b.Card("BRMA01_3");

		// BRMA02_2/2H — 嘲讽的人群：召唤一个 1/1 的观众
		b.Card("BRMA02_2", activate: new Summon(S.CONTROLLER, "BRMA02_2t"));
		b.Card("BRMA02_2H", activate: new Summon(S.CONTROLLER, "BRMA02_2t"));

		// BRMA03_2 — 火焰领主之力：造成 2 点伤害
		b.Card("BRMA03_2", activate: new DealDamage(S.TARGET, 2),
			target: Target.Required());

		// BRMA04_2 — 岩浆脉冲：对所有随从造成 1 点伤害
		b.Card("BRMA04_2", activate: new DealDamage(S.ALL_MINIONS, 1));

		// BRMA05_2/2H — 点燃法力：若对手已用完法力，造成 5/10 点伤害
		b.Card("BRMA05_2", activate: new ConditionalAction(
			new OpponentManaEmpty(),
			new Action[] { new DealDamage(S.ENEMY_HERO, 5) }));
		b.Card("BRMA05_2H", activate: new ConditionalAction(
			new OpponentManaEmpty(),
			new Action[] { new DealDamage(S.ENEMY_HERO, 10) }));

		// BRMA06_2/2H — 管理者：召唤侍僧
		b.Card("BRMA06_2", activate: new Summon(S.CONTROLLER, "BRMA06_4"));
		b.Card("BRMA06_2H", activate: new Summon(S.CONTROLLER, "BRMA06_4H"));

		// BRMA07_2 — 我要砸扁你！：消灭一个随机受伤的敌方随从
		b.Card("BRMA07_2", activate: new Destroy(S.Random(S.ENEMY_MINIONS + S.DAMAGED)));
		b.Card("BRMA07_2H", activate: new Destroy(S.RANDOM_ENEMY_MINION));

		// BRMA08_2/2H — 凝视：光环 — 所有卡牌费用为 1，最大法力 1/2
		b.Card("BRMA08_2", update: new[]
		{
			new Refresh(S.IN_HAND, setTags: new() { { GameTag.Cost, 1 } })
		});
		b.Card("BRMA08_2H", update: new[]
		{
			new Refresh(S.IN_HAND, setTags: new() { { GameTag.Cost, 1 } })
		});

		// BRMA09_2/2H — 打开城门：召唤随从
		b.Card("BRMA09_2", entourage: new[] { "BRMA09_3", "BRMA09_4" },
			activate: new Summon(S.CONTROLLER, "BRMA09_2t") * 3);
		b.Card("BRMA09_2H", entourage: new[] { "BRMA09_3H", "BRMA09_4H" },
			activate: new Summon(S.CONTROLLER, "BRMA09_2Ht") * 3);

		// BRMA09_3/3H — 旧部落
		b.Card("BRMA09_3", entourage: new[] { "BRMA09_2", "BRMA09_4", "BRMA09_5" },
			activate: new Summon(S.CONTROLLER, "BRMA09_3t") * 2);
		b.Card("BRMA09_3H", entourage: new[] { "BRMA09_2H", "BRMA09_4H", "BRMA09_5H" },
			activate: new Summon(S.CONTROLLER, "BRMA09_3Ht") * 2);

		// BRMA09_4/4H — 黑翼
		b.Card("BRMA09_4", entourage: new[] { "BRMA09_2", "BRMA09_3", "BRMA09_5" },
			activate: new Summon(S.CONTROLLER, "BRMA09_4t"));
		b.Card("BRMA09_4H", entourage: new[] { "BRMA09_2H", "BRMA09_3H", "BRMA09_5H" },
			activate: new Summon(S.CONTROLLER, "BRMA09_4Ht"));

		// BRMA09_5/5H — 下马
		b.Card("BRMA09_5", entourage: new[] { "BRMA09_2", "BRMA09_3", "BRMA09_4" },
			activate: new Summon(S.CONTROLLER, "BRMA09_5t"));
		b.Card("BRMA09_5H", entourage: new[] { "BRMA09_2H", "BRMA09_3H", "BRMA09_4H" },
			activate: new Summon(S.CONTROLLER, "BRMA09_5Ht"));

		// BRMA10_3/3H — 栖巢地：强化蛋 + 召唤蛋
		b.Card("BRMA10_3", activate: Acts(
			new Enchant(S.ALL_MINIONS + S.Id("BRMA10_4"), "BRMA10_3e"),
			new Summon(S.CONTROLLER, "BRMA10_4")));
		b.Card("BRMA10_3H", activate: Acts(
			new Enchant(S.ALL_MINIONS + S.Id("BRMA10_4"), "BRMA10_3e"),
			new Summon(S.CONTROLLER, "BRMA10_4")));

		// BRMA11_2/2H — 红龙精华：双方各抽 2 张（英雄模式：抽 3 + 法力）
		b.Card("BRMA11_2", activate: new DrawCard(S.ALL_PLAYERS) * 2);
		b.Card("BRMA11_2H", activate: Acts(
			new DrawCard(S.ALL_PLAYERS), new DrawCard(S.ALL_PLAYERS), new DrawCard(S.ALL_PLAYERS),
			new GainMana(S.CONTROLLER, 1)));

		// BRMA12_2/2H — 血缘灾祸：给对手一个随机灾祸
		b.Card("BRMA12_2", entourage: new[] { "BRMA12_6", "BRMA12_5", "BRMA12_7", "BRMA12_4", "BRMA12_3" },
			activate: new AddToHand(S.OPPONENT, new RandomEntourage()));
		b.Card("BRMA12_2H", entourage: new[] { "BRMA12_3H", "BRMA12_4H", "BRMA12_5H", "BRMA12_6H", "BRMA12_7H" },
			activate: new AddToHand(S.OPPONENT, new RandomEntourage()));

		// BRMA12_10 — 突变：弃掉一张随机手牌
		b.Card("BRMA12_10", activate: new Discard(S.Random(S.FRIENDLY_HAND)));

		// BRMA13_2/2H — 真身：召唤 + 抽 2 张 + 30 护甲
		b.Card("BRMA13_2", activate: Acts(
			new Summon(S.CONTROLLER, "BRMA13_3"),
			new DrawCard(S.CONTROLLER), new DrawCard(S.CONTROLLER),
			new GainArmor(S.FRIENDLY_HERO, 30)));
		b.Card("BRMA13_2H", activate: Acts(
			new Summon(S.CONTROLLER, "BRMA13_3H"),
			new DrawCard(S.CONTROLLER), new DrawCard(S.CONTROLLER),
			new GainArmor(S.FRIENDLY_HERO, 30)));

		// BRMA13_4/4H — 狂野魔法：从对手职业获取一个随机法术
		b.Card("BRMA13_4", play: new AddToHand(S.CONTROLLER, RandomCard().Spell().ClassOf(S.OPPONENT)));
		b.Card("BRMA13_4H", play: new AddToHand(S.CONTROLLER, RandomCard().Spell().ClassOf(S.OPPONENT)));

		// BRMA14_2/2H — 激活奥法机器人
		b.Card("BRMA14_2", activate: Acts(
			new Summon(S.CONTROLLER, "BRMA14_3"), new Summon(S.CONTROLLER, "BRMA14_4")));
		b.Card("BRMA14_2H", activate: Acts(
			new Summon(S.CONTROLLER, "BRMA14_3"), new Summon(S.CONTROLLER, "BRMA14_4H")));

		// BRMA14_4/4H — 激活毒液机器人
		b.Card("BRMA14_4", activate: Acts(
			new Summon(S.CONTROLLER, "BRMA14_5"), new Summon(S.CONTROLLER, "BRMA14_6")));
		b.Card("BRMA14_4H", activate: Acts(
			new Summon(S.CONTROLLER, "BRMA14_5H"), new Summon(S.CONTROLLER, "BRMA14_6H")));

		// BRMA14_6/6H — 激活电子机器人
		b.Card("BRMA14_6", activate: Acts(
			new Summon(S.CONTROLLER, "BRMA14_7"), new Summon(S.CONTROLLER, "BRMA14_8")));
		b.Card("BRMA14_6H", activate: Acts(
			new Summon(S.CONTROLLER, "BRMA14_7H"), new Summon(S.CONTROLLER, "BRMA14_8H")));

		// BRMA14_8/8H — 激活熔岩机器人
		b.Card("BRMA14_8", activate: Acts(
			new Summon(S.CONTROLLER, "BRMA14_9"), new Summon(S.CONTROLLER, "BRMA14_10")));
		b.Card("BRMA14_8H", activate: Acts(
			new Summon(S.CONTROLLER, "BRMA14_9H"), new Summon(S.CONTROLLER, "BRMA14_10H")));

		// BRMA14_10/10H — 激活！：从随行中召唤一个随机机器人
		b.Card("BRMA14_10", entourage: new[] { "BRMA14_3", "BRMA14_5", "BRMA14_7", "BRMA14_9" },
			activate: new Summon(S.CONTROLLER, new RandomEntourage()));
		b.Card("BRMA14_10H", entourage: new[] { "BRMA14_3", "BRMA14_5H", "BRMA14_7H", "BRMA14_9H" },
			activate: new Summon(S.CONTROLLER, new RandomEntourage()));

		// BRMA15_2 — 炼金术士：交换所有已召唤随从的攻防
		b.Card("BRMA15_2",
			on: When<MinionSummonedEvent>(ANY, AFTER,
				new ForEach(S.ALL_MINIONS, e => new SwapStats(e, e, "BRMA15_2e"))));
		b.Card("BRMA15_2H",
			on: When<MinionSummonedEvent>(ANY, AFTER,
				new ForEach(S.ALL_MINIONS, e => new SwapStats(e, e, "BRMA15_2e"))));

		// BRMA16_2/2H — 回声定位：召唤龙齿武器
		b.Card("BRMA16_2", activate: new Summon(S.CONTROLLER, "BRMA16_5"));
		b.Card("BRMA16_2H", activate: new Summon(S.CONTROLLER, "BRMA16_5"));

		// BRMA17_5/5H — 骨骸爪牙：召唤 2 个骷髅兵
		b.Card("BRMA17_5", activate: new Summon(S.CONTROLLER, "BRMA17_6") * 2);
		b.Card("BRMA17_5H", activate: new Summon(S.CONTROLLER, "BRMA17_6H") * 2);

		// BRMA17_8/8H — 奈法利安来袭！：随机 0~20 点伤害
		b.Card("BRMA17_8", activate: new DealDamage(S.ENEMY_HERO, new RandomAmount(0, 20)));
		b.Card("BRMA17_8H", activate: new DealDamage(S.ENEMY_HERO, new RandomAmount(0, 20)));

		// ═══════════════════════════════════════
		// 冒险模式随从
		// ═══════════════════════════════════════

		// BRMA03_3/3H — 莫伊拉·铜须：光环 — 无法使用火焰领主之力
		b.Card("BRMA03_3", update: new[]
		{
			new Refresh(S.ALL_HERO_POWERS + S.Id("BRMA03_2"), new() { { GameTag.CantPlay, 1 } })
		});
		b.Card("BRMA03_3H", update: new[]
		{
			new Refresh(S.ALL_HERO_POWERS + S.Id("BRMA03_2"), new() { { GameTag.CantPlay, 1 } })
		});

		// BRMA10_4/4H — 腐蚀之卵：生命值 ≥4/5 时销毁并召唤
		b.Card("BRMA10_4",
			on: When<DamageEvent>(OWN, AFTER,
				new TargetedAction[] { new Destroy(S.SELF), new Summon(S.CONTROLLER, "BRMA10_1") },
				when: Condition<DamageEvent>(new LazyNumEvaluator(CURRENT_HEALTH(S.SELF), 4, GE()))));
		b.Card("BRMA10_4H",
			on: When<DamageEvent>(OWN, AFTER,
				new TargetedAction[] { new Destroy(S.SELF), new Summon(S.CONTROLLER, "BRMA10_1H") },
				when: Condition<DamageEvent>(new LazyNumEvaluator(CURRENT_HEALTH(S.SELF), 5, GE()))));

		// BRMA04_3/3H — 火誓者：亡语 — 对敌方英雄造成等于本回合死亡的火誓者数量的伤害
		b.Card("BRMA04_3", deathrattle: new DealDamage(S.ENEMY_HERO,
			new Count(S.FRIENDLY_DEATHS_THIS_TURN + S.ID("BRMA04_3"))));
		b.Card("BRMA04_3H", deathrattle: new DealDamage(S.ENEMY_HERO,
			new Count(S.FRIENDLY_DEATHS_THIS_TURN + S.ID("BRMA04_3H"))));

		// BRMA12_8t — 多彩龙人：对手施放法术后获得 +2/+2
		b.Card("BRMA12_8t",
			on: When<SpellCastEvent>(ANY, AFTER, new Enchant(S.SELF, "BRMA12_8te")));

		// BRMA13_5 — 烈焰之子：战吼：造成 6 点伤害
		b.Card("BRMA13_5", play: new DealDamage(S.TARGET, 6),
			target: Target.IfAvailable());

		// BRMA14_3 — 奥法机器人：光环 — 所有法术 +2 法术伤害
		b.Card("BRMA14_3", update: new[]
		{
			new Refresh(S.ALL_PLAYERS, new() { { GameTag.Spellpower, 2 } })
		});

		// BRMA14_5/5H — 毒液机器人：回合开始时对所有其他随从造成 1 点伤害
		b.Card("BRMA14_5",
			on: When<TurnStartEvent>(OWN, ON, new DealDamage(S.ALL_MINIONS - S.SELF, 1)));
		b.Card("BRMA14_5H",
			on: When<TurnStartEvent>(OWN, ON, new DealDamage(S.ALL_MINIONS - S.SELF, 1)));

		// BRMA14_7/7H — 电子机器人：光环 — 法术费用减少 3
		b.Card("BRMA14_7", update: new[]
		{
			new Refresh(S.IN_HAND + S.SPELL, new() { { GameTag.Cost, -3 } })
		});
		b.Card("BRMA14_7H", update: new[]
		{
			new Refresh(S.IN_HAND + S.SPELL, new() { { GameTag.Cost, -3 } })
		});

		// BRMA14_9/9H — 熔岩机器人：任意卡牌被打出后，对双方英雄造成 2 点伤害
		b.Card("BRMA14_9",
			on: When<CardPlayedEvent>(ANY, AFTER, new DealDamage(S.ALL_HEROES, 2)));
		b.Card("BRMA14_9H",
			on: When<CardPlayedEvent>(ANY, AFTER, new DealDamage(S.ALL_HEROES, 2)));

		// BRMA16_5 — 龙齿（武器）：对手打出卡牌时获得 +1 攻击力
		b.Card("BRMA16_5",
			on: When<CardPlayedEvent>(ANY, AFTER, new Enchant(S.SELF, "BRMA16_5e"),
				when: EventCardIn<CardPlayedEvent>(S.ENEMY)));

		// BRMA10_6 — 狂龙之爪：蛋死亡时获得 +1 攻击力
		b.Card("BRMA10_6",
			on: When<DeathEvent>(ANY, AFTER, new Enchant(S.SELF, "BRMA10_6e"),
				when: EventTargetIn<DeathEvent>(S.Id("BRMA10_4"))));

		// ═══════════════════════════════════════
		// 冒险模式法术
		// ═══════════════════════════════════════

		// BRMA_01 — 火焰之心：抽 2 张牌 + 4 点护甲
		b.Card("BRMA_01", play: Acts(
			new DrawCard(S.CONTROLLER), new DrawCard(S.CONTROLLER),
			new GainArmor(S.FRIENDLY_HERO, 4)));

		// BRMA01_4 — 冲啊！：召唤 4 个侏儒
		b.Card("BRMA01_4", play: new Summon(S.CONTROLLER, "BRMA01_4t") * 4);

		// BRMA05_3 — 活体炸弹：附给目标
		b.Card("BRMA05_3", play: new Enchant(S.TARGET, "BRMA05_3e"),
			target: Target.Required().Minion().Enemy());
		// BRMA05_3e 附魔：回合开始时对敌方角色造成 5 点伤害
		b.Card("BRMA05_3e",
			on: When<TurnStartEvent>(OWN, ON, new DealDamage(S.ENEMY_CHARACTERS, 5)));

		b.Card("BRMA05_3H", play: new Enchant(S.TARGET, "BRMA05_3He"),
			target: Target.Required().Minion().Enemy());
		b.Card("BRMA05_3He",
			on: When<TurnStartEvent>(OWN, ON, new DealDamage(S.ENEMY_CHARACTERS, 10)));

		// BRMA07_3 — 该砸了！：对随机敌方随从造成 5 点伤害 + 5 护甲
		b.Card("BRMA07_3", play: Acts(
			new DealDamage(S.RANDOM_ENEMY_MINION, 5), new GainArmor(S.FRIENDLY_HERO, 5)));

		// BRMA08_3 — 达基萨斯之令：消灭目标 + 10 护甲
		b.Card("BRMA08_3", play: Acts(new Destroy(S.TARGET), new GainArmor(S.FRIENDLY_HERO, 10)),
			target: Target.Required().Minion());

		// BRMA09_6 — 真正的大酋长：消灭目标传说随从
		b.Card("BRMA09_6", play: new Destroy(S.TARGET),
			target: Target.Required().Minion());

		// BRMA04_4/4H — 嗨起来！：召唤 3 个火誓者
		b.Card("BRMA04_4", play: new Summon(S.CONTROLLER, "BRMA04_3") * 3);
		b.Card("BRMA04_4H", play: new Summon(S.CONTROLLER, "BRMA04_3H") * 3);

		// BRMA11_3 — 灼热肾上腺素：对敌方英雄造成 2 点伤害
		b.Card("BRMA11_3", play: new DealDamage(S.ENEMY_HERO, 2));

		// BRMA12_8 — 多彩突变：将一个随从变形为龙人
		b.Card("BRMA12_8", play: new Transform(S.TARGET, "BRMA12_8t"),
			target: Target.Required().Minion());

		// BRMA14_11 — 充能：恢复法力
		b.Card("BRMA14_11", play: new RefillMana(S.CONTROLLER, 10));

		// BRMA13_8 — 去死吧，虫子！：对一个随机敌方角色造成 8 点伤害
		b.Card("BRMA13_8", play: new DealDamage(S.RANDOM_ENEMY_CHARACTER, 8));

		// BRMA15_3 — 放出畸变体！：召唤 3 个畸变体
		b.Card("BRMA15_3", play: new Summon(S.CONTROLLER, "BRMA15_4") * 3);

		// BRMA16_3 — 超音速吐息：造成 3 点伤害 + 强化武器
		b.Card("BRMA16_3", play: Acts(new DealDamage(S.TARGET, 3),
			new Enchant(S.FRIENDLY_WEAPON, "BRMA16_3e")),
			target: Target.Required().Minion());

		// BRMA16_4 — 回音之锣：消灭敌方武器
		b.Card("BRMA16_4", play: new Destroy(S.ENEMY_WEAPON));

		// BRMA17_4 — 岩浆！：对所有随从造成 2 点伤害
		b.Card("BRMA17_4", play: new DealDamage(S.ALL_MINIONS, 2));

		// ═══════════════════════════════════════
		// 血缘灾祸（手牌内效果）
		// ═══════════════════════════════════════

		// BRMA12_3/3H — 红色：回合开始时对你的英雄造成 1/3 点伤害（仅在手牌中）
		b.Card("BRMA12_3",
			on: When<TurnStartEvent>(OWN, ON, new DealDamage(S.FRIENDLY_HERO, 1)).InHand());
		b.Card("BRMA12_3H",
			on: When<TurnStartEvent>(OWN, ON, new DealDamage(S.FRIENDLY_HERO, 3)).InHand());

		// BRMA12_4/4H — 绿色：回合开始时恢复敌方英雄生命值
		b.Card("BRMA12_4",
			on: When<TurnStartEvent>(OWN, ON, new RestoreHealth(S.ENEMY_HERO, 2)));
		b.Card("BRMA12_4H",
			on: When<TurnStartEvent>(OWN, ON, new RestoreHealth(S.ENEMY_HERO, 6)));

		// BRMA12_5/5H — 蓝色：光环 — 敌方法术费用降低
		b.Card("BRMA12_5", handUpdate: new[]
		{
			new Refresh(S.ENEMY_HAND + S.SPELL, new() { { GameTag.Cost, -1 } })
		});
		b.Card("BRMA12_5H", handUpdate: new[]
		{
			new Refresh(S.ENEMY_HAND + S.SPELL, new() { { GameTag.Cost, -3 } })
		});

		// BRMA12_6/6H — 黑色：对手抽牌时复制该牌到你的手牌
		b.Card("BRMA12_6",
			on: When<DrawCardEvent>(ENEMY, AFTER, new CopyToHand(S.CONTROLLER, S.EVT_TARGET)));
		b.Card("BRMA12_6H",
			on: When<DrawCardEvent>(ENEMY, AFTER, new CopyToHand(S.CONTROLLER, S.EVT_TARGET)));

		// BRMA12_7/7H — 青铜色：光环 — 敌方随从费用降低
		b.Card("BRMA12_7", handUpdate: new[]
		{
			new Refresh(S.ENEMY_HAND + S.MINION, new() { { GameTag.Cost, -1 } })
		});
		b.Card("BRMA12_7H", handUpdate: new[]
		{
			new Refresh(S.ENEMY_HAND + S.MINION, new() { { GameTag.Cost, -3 } })
		});
	}
}
