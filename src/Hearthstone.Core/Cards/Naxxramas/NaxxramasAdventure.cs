using HearthstoneServer.Auras;
using HearthstoneServer.Events;
using static HearthstoneServer.Dsl.BusEvent;
using static HearthstoneServer.Dsl.EventFilters;
using static HearthstoneServer.Cards.CardScriptUtils;
using HearthstoneServer.Targeting;

namespace HearthstoneServer.Dsl.Cards;

// ═════════════════════════════════════════
// 纳克萨玛斯 — 冒险模式（Boss）卡牌脚本
// ═════════════════════════════════════════

public class NaxxramasAdventure : CardPack
{
	public override void Register(CardBuilder b)
	{
		// ═══════════════════════════════════════
		// 附魔
		// ═══════════════════════════════════════

		b.Buff("NAX6_03te", atk: 8);              // 死亡绽放 +8 攻击力
		b.Buff("NAX9_07e", atk: 1, health: 1);    // 骑士印记 +1/+1
		b.Buff("NAX11_04e", atk: 4, health: 4, extraTags: (GameTag.Taunt, true)); // 突变注射 +4/+4 嘲讽
		b.Buff("NAX12_04e", atk: 6);              // 激怒 +6 攻击力
		b.Buff("NAX13_03e", health: 2);            // 超级充能 +2 生命值
		b.Buff("NAX12_03e", atk: 2);              // 巨颚 +2 攻击力
		b.Buff("BRMA10_3e", health: 1);            // 栖巢地 +1 生命值（黑石山冒险也使用）

		// NAX12_02e：最大生命值设为 1 — 斩杀
		b.Buff("NAX12_02e", maxHealthMod: SetTo(1));

		// ═══════════════════════════════════════
		// 英雄技能
		// ═══════════════════════════════════════

		// NAX1_04 — 飞扑：召唤一个 3/1 的蛛魔
		b.Card("NAX1_04", activate: new Summon(S.CONTROLLER, "NAX1_03"),
			target: Target.IfAvailable());

		// NAX1h_04 — 飞扑（英雄模式）
		b.Card("NAX1h_04", activate: new Summon(S.CONTROLLER, "NAX1h_03"));

		// NAX2_03 — 火焰之雨：每对手手牌 1 张，对随机敌方随从造成 1 点伤害
		b.Card("NAX2_03", activate: new DealDamage(S.RANDOM_ENEMY_MINION, 1) * new Count(S.ENEMY_HAND));
		b.Card("NAX2_03H", activate: new DealDamage(S.RANDOM_ENEMY_MINION, 1) * new Count(S.ENEMY_HAND));

		// NAX2_05 — 崇拜者：光环 — 你的回合时英雄 +1 攻击力
		b.Card("NAX2_05", update: new[]
		{
			new Refresh(S.FRIENDLY_HERO, new() { { GameTag.Atk, 1 } },
				condition: new CurrentPlayer(S.FRIENDLY_HERO))
		});
		b.Card("NAX2_05H", update: new[]
		{
			new Refresh(S.FRIENDLY_HERO, new() { { GameTag.Atk, 3 } },
				condition: new CurrentPlayer(S.FRIENDLY_HERO))
		});

		// NAX3_02 — 蛛网缠绕：弹回一个随机敌方随从
		b.Card("NAX3_02", activate: new ReturnToHand(S.RANDOM_ENEMY_MINION));
		b.Card("NAX3_02H", activate: new ReturnToHand(new RandomSelector(S.ENEMY_MINIONS, 2)));

		// NAX4_04 — 死灵复活：敌方随从死亡时召唤骷髅兵
		b.Card("NAX4_04",
			on: When<DeathEvent>(ANY, ON, new Summon(S.CONTROLLER, "NAX4_03")));
		b.Card("NAX4_04H",
			on: When<DeathEvent>(ANY, ON, new Summon(S.CONTROLLER, "NAX4_03H")));

		// NAX5_02 — 喷发：对最左侧敌方随从造成 2 点伤害
		b.Card("NAX5_02", activate: new DealDamage(S.LeftOf(S.ENEMY_MINIONS), 2));
		b.Card("NAX5_02H", activate: new DealDamage(S.LeftOf(S.ENEMY_MINIONS), 3));

		// NAX6_02 — 冥灵之气：对敌方英雄造成 3 点伤害
		b.Card("NAX6_02", activate: new DealDamage(S.ENEMY_HERO, 3));
		b.Card("NAX6_02H", activate: new DealDamage(S.ENEMY_HERO, 3));

		// NAX7_03 — 失衡打击：造成 3(4) 点伤害
		b.Card("NAX7_03", activate: new DealDamage(S.TARGET, 3),
			target: Target.Required());
		b.Card("NAX7_03H", activate: new DealDamage(S.TARGET, 4),
			target: Target.Required());

		// NAX8_02 — 收割：抽一张牌
		b.Card("NAX8_02", activate: new DrawCard(S.CONTROLLER));
		b.Card("NAX8_02H", activate: Acts(new DrawCard(S.CONTROLLER), new GainMana(S.CONTROLLER, 1)));

		// NAX9_06 — 邪恶之影：抽 2 张牌
		b.Card("NAX9_06", activate: new DrawCard(S.CONTROLLER) * 2);

		// NAX10_03 — 仇恨打击：消灭一个随从
		b.Card("NAX10_03", activate: new Destroy(S.TARGET),
			target: Target.Required().Minion());
		b.Card("NAX10_03H", activate: new Destroy(S.TARGET),
			target: Target.Required().Minion());

		// NAX11_02 — 毒云：对所有随从造成 1 点伤害，每击杀一个召唤 1/2 软泥怪
		b.Card("NAX11_02", activate: new DealDamage(S.ALL_MINIONS, 1)
			.IfThen(KILLED, new Summon(S.CONTROLLER, "NAX11_02t")));
		b.Card("NAX11_02H", activate: new DealDamage(S.ENEMY_CHARACTERS, 2)
			.IfThen(KILLED, new Summon(S.CONTROLLER, "NAX11_02t")));

		// NAX12_02 — 斩杀：将所有敌方随从的生命值设为 1
		b.Card("NAX12_02", activate: new Enchant(S.ENEMY_MINIONS, "NAX12_02e"));
		b.Card("NAX12_02H", activate: new Enchant(S.ENEMY_MINIONS, "NAX12_02e"));

		// NAX13_02 — 极性转换：交换所有随从的攻防
		b.Card("NAX13_02", activate: new ForEach(S.ALL_MINIONS,
			e => new SwapStats(e, e, "NAX13_02e")));

		// NAX14_02 — 冰霜吐息：消灭所有未冻结的敌方随从
		b.Card("NAX14_02", activate: new Destroy(S.ENEMY_MINIONS - S.FROZEN));

		// NAX15_02 — 冰霜冲击：对敌方英雄造成 2 点伤害并冻结
		b.Card("NAX15_02", activate: Acts(new DealDamage(S.ENEMY_HERO, 2), new Freeze(S.ENEMY_HERO)));
		b.Card("NAX15_02H", activate: Acts(new DealDamage(S.ENEMY_HERO, 3), new Freeze(S.ENEMY_HERO)));

		// NAX15_04 — 锁链：临时夺取目标（附魔使其在回合结束时销毁）
		b.Card("NAX15_04", activate: Acts(
				new TakeControl(S.TARGET),
				new Enchant(S.TARGET, "NAX15_04a")),
			target: Target.Required().Minion());
		b.Card("NAX15_04H", activate: Acts(
				new TakeControl(S.RANDOM_ENEMY_MINION),
				new Enchant(S.RANDOM_ENEMY_MINION, "NAX15_04a")));

		// NAX15_04a 附魔 — 回合结束时销毁自身
		b.Card("NAX15_04a",
			on: When<TurnEndEvent>(ANY, ON, new Destroy(S.SELF)));

		// ═══════════════════════════════════════
		// 冒险模式随从
		// ═══════════════════════════════════════

		// FP1_006 — 死亡战马：亡语 — 对你的英雄造成 3 点伤害
		b.Card("FP1_006", deathrattle: new DealDamage(S.FRIENDLY_HERO, 3));

		// NAX8_03 — 不屈的学员：亡语 — 为对手召唤一个幽灵学员
		b.Card("NAX8_03", deathrattle: new Summon(S.OPPONENT, "NAX8_03t"));

		// NAX8_03t — 幽灵学员：回合开始时对你的英雄造成 1 点伤害
		b.Card("NAX8_03t",
			on: When<TurnStartEvent>(OWN, ON, new DealDamage(S.FRIENDLY_HERO, 1)));

		// NAX8_04 — 不屈的战士
		b.Card("NAX8_04", deathrattle: new Summon(S.OPPONENT, "NAX8_04t"));
		b.Card("NAX8_04t",
			on: When<TurnStartEvent>(OWN, ON, new DealDamage(S.FRIENDLY_HERO, 1)));

		// NAX8_05 — 不屈的骑手
		b.Card("NAX8_05", deathrattle: new Summon(S.OPPONENT, "NAX8_05t"));
		b.Card("NAX8_05t",
			on: When<TurnStartEvent>(OWN, ON, new DealDamage(S.FRIENDLY_HERO, 1)));

		// NAX9_02, NAX9_03, NAX9_04 — 天启四骑士：光环 — 英雄不能受到伤害
		b.Card("NAX9_02", update: new[]
		{
			new Refresh(S.FRIENDLY_HERO, new() { { GameTag.CantBeDamaged, 1 } })
		});
		b.Card("NAX9_02H", update: new[]
		{
			new Refresh(S.FRIENDLY_HERO, new() { { GameTag.CantBeDamaged, 1 } })
		});
		b.Card("NAX9_03", update: new[]
		{
			new Refresh(S.FRIENDLY_HERO, new() { { GameTag.CantBeDamaged, 1 } })
		});
		b.Card("NAX9_03H", update: new[]
		{
			new Refresh(S.FRIENDLY_HERO, new() { { GameTag.CantBeDamaged, 1 } })
		});
		b.Card("NAX9_04", update: new[]
		{
			new Refresh(S.FRIENDLY_HERO, new() { { GameTag.CantBeDamaged, 1 } })
		});
		b.Card("NAX9_04H", update: new[]
		{
			new Refresh(S.FRIENDLY_HERO, new() { { GameTag.CantBeDamaged, 1 } })
		});

		// NAX14_03 — 冻结的勇士：始终冻结
		b.Card("NAX14_03", update: new[]
		{
			new Refresh(S.SELF, new() { { GameTag.Freeze, 1 } })
		});

		// NAXM_001 — 亡灵骑士：亡语 — 消灭相邻随从
		b.Card("NAXM_001", deathrattle: new Destroy(S.SELF_ADJACENT));

		// NAXM_002 — 骷髅铁匠：亡语 — 消灭敌方武器
		b.Card("NAXM_002", deathrattle: new Destroy(S.ENEMY_WEAPON));

		// ═══════════════════════════════════════
		// 冒险模式法术
		// ═══════════════════════════════════════

		// NAX1_05 — 蝗虫群：对所有敌方随从造成 3 点伤害，恢复英雄 3 点生命值
		b.Card("NAX1_05", play: Acts(new DealDamage(S.ENEMY_MINIONS, 3), new RestoreHealth(S.FRIENDLY_HERO, 3)));

		// NAX3_03 — 坏死之毒：消灭目标随从
		b.Card("NAX3_03", play: new Destroy(S.TARGET),
			target: Target.Required().Minion());

		// NAX4_05 — 瘟疫：消灭所有随从（骷髅兵除外）
		b.Card("NAX4_05", play: new Destroy(S.ALL_MINIONS - S.Id("NAX4_03") - S.Id("NAX4_03H")));

		// NAX5_03 — 思维末日：双方各抽 2 张并获得 1 点法力
		b.Card("NAX5_03", play: Acts(new DrawCard(S.ALL_PLAYERS), new DrawCard(S.ALL_PLAYERS), new GainMana(S.ALL_PLAYERS, 1)));

		// NAX6_03 — 死亡绽放：对目标造成 5 点伤害，召唤孢子
		b.Card("NAX6_03", play: Acts(new DealDamage(S.TARGET, 5), new Summon(S.CONTROLLER, "NAX6_03t")),
			target: Target.Required().Minion());

		// NAX6_03t — 孢子：亡语 — 使所有敌方随从获得 +8 攻击力
		b.Card("NAX6_03t", deathrattle: new Enchant(S.ENEMY_MINIONS, "NAX6_03te"));

		// NAX6_04 — 孢子爆裂：对所有敌方随从造成 1 点伤害，召唤孢子
		b.Card("NAX6_04", play: Acts(new DealDamage(S.ENEMY_MINIONS, 1), new Summon(S.CONTROLLER, "NAX6_03t")));

		// NAX7_05 — 精神控制水晶：夺取敌方替补
		b.Card("NAX7_05", play: new TakeControl(S.ENEMY_MINIONS + S.Id("NAX7_02")));

		// NAX9_07 — 骑士印记：使所有友方随从和武器获得 +1/+1
		b.Card("NAX9_07", play: new Enchant(S.FRIENDLY_MINIONS | S.FRIENDLY_WEAPON, "NAX9_07e"));

		// NAX11_04 — 突变注射：使目标随从获得 +4/+4 和嘲讽
		b.Card("NAX11_04", play: new Enchant(S.TARGET, "NAX11_04e"),
			target: Target.Required().Minion());

		// NAX12_04 — 激怒：英雄自身获得 +6 攻击力
		b.Card("NAX12_04", play: new Enchant(S.SELF, "NAX12_04e"));

		// NAX13_03 — 超级充能：使所有友方随从获得 +2 生命值
		b.Card("NAX13_03", play: new Enchant(S.FRIENDLY_MINIONS, "NAX13_03e"));

		// NAX14_04 — 极寒：对敌方英雄造成 8 点伤害并冻结
		b.Card("NAX14_04", play: Acts(new DealDamage(S.ENEMY_HERO, 8), new Freeze(S.ENEMY_HERO)));

		// ═══════════════════════════════════════
		// 冒险模式武器
		// ═══════════════════════════════════════

		// NAX7_04 — 巨型符文之刃：+5 攻击力光环
		b.Card("NAX7_04", update: new[]
		{
			new Refresh(S.SELF, new() { { GameTag.Atk, 5 } })
		});
		b.Card("NAX7_04H", update: new[]
		{
			new Refresh(S.SELF, new() { { GameTag.Atk, 5 } })
		});

		// NAX9_05 — 符文之刃：若有骑士存活则 +3 攻击力
		b.Card("NAX9_05", update: new[]
		{
			new Refresh(S.SELF, new() { { GameTag.Atk, 3 } },
				condition: new Find(S.FRIENDLY_MINIONS
					+ (S.Id("NAX9_01") | S.Id("NAX9_02") | S.Id("NAX9_03") | S.Id("NAX9_04"))))
		});
		b.Card("NAX9_05H", update: new[]
		{
			new Refresh(S.SELF, new() { { GameTag.Atk, 3 } },
				condition: new Find(S.FRIENDLY_MINIONS
					+ (S.Id("NAX9_01") | S.Id("NAX9_02") | S.Id("NAX9_03") | S.Id("NAX9_04"))))
		});

		// NAX10_02 — 钩子：亡语 — 回到手牌
		b.Card("NAX10_02", deathrattle: new AddToHand(S.CONTROLLER, "NAX10_02"));
		b.Card("NAX10_02H", deathrattle: new AddToHand(S.CONTROLLER, "NAX10_02H"));

		// NAX12_03 — 巨颚：亡语随从死亡时获得 +2 攻击力
		b.Card("NAX12_03",
			on: When<DeathEvent>(ANY, AFTER, new Enchant(S.SELF, "NAX12_03e"),
				when: DeadHas(GameTag.Deathrattle)));
		b.Card("NAX12_03H",
			on: When<DeathEvent>(ANY, AFTER, new Enchant(S.SELF, "NAX12_03e"),
				when: DeadHas(GameTag.Deathrattle)));
	}
}
