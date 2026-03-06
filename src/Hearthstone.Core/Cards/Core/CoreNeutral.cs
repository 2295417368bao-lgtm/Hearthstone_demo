using HearthstoneServer.Auras;
using HearthstoneServer.Entities;
using HearthstoneServer.Events;
using static HearthstoneServer.Dsl.BusEvent;
using static HearthstoneServer.Cards.CardScriptUtils;
using static HearthstoneServer.Dsl.EventFilters;
using static HearthstoneServer.Dsl.RandomCards;
using static HearthstoneServer.Dsl.LazyNum;
using HearthstoneServer.Targeting;

namespace HearthstoneServer.Dsl.Cards;

// ══════════════════════════════════════════════════════
// 核心中立卡牌脚本 (Core Set) — 82 张可收集
// ══════════════════════════════════════════════════════

public class CoreNeutral : CardPack
{
	public override void Register(CardBuilder b)
	{
		// ── 附魔 ──
		b.Buff("CORE_CS2_188e", atk: 2,
			on: When<TurnEndEvent>(OWN, ON, new Destroy(S.SELF))); // 叫嚣的中士（本回合）
		b.Buff("CORE_EX1_162o", atk: 1);  // 恐狼前锋
		b.Buff("CORE_CS2_122e", atk: 1);  // 团队领袖
		b.Buff("CORE_EX1_507e", atk: 2);  // 鱼人领军
		b.Buff("CORE_NEW1_027e", atk: 1, health: 1); // 南海船长
		b.Buff("CORE_CS2_222e", atk: 1, health: 1);  // 暴风城勇士
		b.Buff("CORE_ULD_191e", health: 2); // 欢快的同伴
		b.Buff("CORE_EX1_058e", extraTags: (GameTag.Taunt, true)); // 日怒保卫者
		b.Buff("CORE_EX1_043e", health: 1); // 暮光幼龙（每张手牌+1 hp）
		b.Buff("CORE_WON_141e", atk: 1, health: 1); // 展馆茶杯
		b.Buff("CORE_WON_142e", atk: 2, health: 2); // 展馆茶壶

		// ═══ 1 费 ═══

		// 小精灵 CORE_CS2_231 — 纯属性
		b.Card("CORE_CS2_231");

		// 叫嚣的中士 CORE_CS2_188 — 战吼：使一个随从本回合 +2 攻击力
		b.Card("CORE_CS2_188",
			battlecry: new Enchant(S.TARGET, "CORE_CS2_188e"),
			target: Target.IfAvailable().Minion());

		// 精灵弓箭手 CORE_CS2_189 — 战吼：造成 1 点伤害
		b.Card("CORE_CS2_189",
			battlecry: new DealDamage(S.TARGET, 1),
			target: Target.IfAvailable());

		// 鱼人木乃伊 CORE_ULD_723 — 复生（纯属性）
		b.Card("CORE_ULD_723");

		// 鱼人招潮者 CORE_EX1_509 — 每当你召唤一个鱼人，获得 +1 攻击力
		b.Card("CORE_EX1_509",
			on: When<MinionSummonedEvent>(OWN, ON, new Enchant(S.SELF, "CORE_EX1_509e"),
				when: SummonedRace(Race.Murloc)));
		b.Buff("CORE_EX1_509e", atk: 1);

		// 欢快的同伴 CORE_ULD_191 — 战吼：使一个友方随从获得 +2 生命值
		b.Card("CORE_ULD_191",
			battlecry: new Enchant(S.TARGET, "CORE_ULD_191e"),
			target: Target.IfAvailable().Friendly().Minion());

		// 火羽精灵 CORE_UNG_809 — 战吼：将一张 1/2 元素置入手牌
		b.Card("CORE_UNG_809",
			battlecry: new AddToHand(S.CONTROLLER, "UNG_809t1"));

		// 凶恶的滑矛纳迦 CORE_TSC_827 — 施放法术后本回合 +1 攻击力
		b.Card("CORE_TSC_827",
			on: When<SpellCastEvent>(OWN, AFTER, new Enchant(S.SELF, "CORE_TSC_827e")));
		b.Buff("CORE_TSC_827e", atk: 1,
			on: When<TurnEndEvent>(OWN, ON, new Destroy(S.SELF)));

		// 狼人渗透者 CORE_EX1_010 — 潜行（纯属性）
		b.Card("CORE_EX1_010");

		// 巫医 CORE_EX1_011 — 战吼：恢复 2 点生命值
		b.Card("CORE_EX1_011",
			battlecry: new RestoreHealth(S.TARGET, 2),
			target: Target.IfAvailable());

		// 冰川裂片 CORE_UNG_205 — 战吼：冻结一个敌人
		b.Card("CORE_UNG_205",
			battlecry: Freeze(S.TARGET),
			target: Target.IfAvailable().Enemy());

		// ═══ 2 费 ═══

		// 蛛魔之卵 CORE_FP1_007 — 亡语：召唤 4/4 蛛魔
		b.Card("CORE_FP1_007",
			deathrattle: new Summon(S.CONTROLLER, "FP1_007t"));

		// 游学者周卓 CORE_EX1_100 — 每当一个玩家施放法术，复制给另一个玩家
		b.Card("CORE_EX1_100",
			on: new[]
			{
				When<SpellCastEvent>(OWN, AFTER, new AddToHand(S.OPPONENT, S.EVT_SOURCE)),
				When<SpellCastEvent>(ENEMY, AFTER, new AddToHand(S.CONTROLLER, S.EVT_SOURCE))
			});

		// 末日预言者 CORE_NEW1_021 — 回合开始时消灭所有随从
		b.Card("CORE_NEW1_021",
			on: When<TurnStartEvent>(OWN, ON, new Destroy(S.ALL_MINIONS)));

		// 血法师萨尔诺斯 CORE_EX1_012 — 法术伤害+1，亡语：抽一张牌
		b.Card("CORE_EX1_012",
			deathrattle: new DrawCard(S.CONTROLLER));

		// 迷宫向导 CORE_REV_308 — 战吼：随机召唤一个 2 费随从
		b.Card("CORE_REV_308",
			battlecry: new Summon(S.CONTROLLER, RandomCard().Minion().Cost(2)));

		// 吵吵机器人 CORE_GVG_085 — 嘲讽，圣盾（纯属性）
		b.Card("CORE_GVG_085");

		// 战利品贮藏者 CORE_EX1_096 — 亡语：抽一张牌
		b.Card("CORE_EX1_096",
			deathrattle: new DrawCard(S.CONTROLLER));

		// 鱼人猎潮者 CORE_EX1_506 — 战吼：召唤 1/1 鱼人斥候
		b.Card("CORE_EX1_506",
			battlecry: new Summon(S.CONTROLLER, "EX1_506a"));

		// 狗头人地卜师 CORE_CS2_142 — 法术伤害+1（纯属性）
		b.Card("CORE_CS2_142");

		// 疯狂的炼金师 CORE_EX1_059 — 战吼：使一个随从攻击力和生命值互换
		b.Card("CORE_EX1_059",
			battlecry: new SwapAtkHealth(S.TARGET),
			target: Target.IfAvailable().Minion());

		// 恐狼前锋 CORE_EX1_162 — 相邻随从 +1 攻击力
		b.Card("CORE_EX1_162", update: new[]
		{
			new Refresh(S.SELF_ADJACENT, buff: "CORE_EX1_162o")
		});

		// 雾帆劫掠者 CS3_022 — 战吼：如果你装备着武器，造成 2 点伤害
		b.Card("CS3_022",
			battlecry: IfElse(new Find(S.FRIENDLY_WEAPON),
				new DealDamage(S.TARGET, 2), NOTHING),
			target: Target.IfAvailable());

		// 奖品商贩 CORE_DMF_067 — 战吼/亡语：每个玩家抽一张牌
		b.Card("CORE_DMF_067",
			battlecry: Acts(new DrawCard(S.CONTROLLER), new DrawCard(S.OPPONENT)),
			deathrattle: Acts(new DrawCard(S.CONTROLLER), new DrawCard(S.OPPONENT)));

		// 日怒保卫者 CORE_EX1_058 — 战吼：使相邻随从获得嘲讽
		b.Card("CORE_EX1_058",
			battlecry: new Enchant(S.SELF_ADJACENT, "CORE_EX1_058e"));

		// 虚空幽龙史学家 CORE_KAR_062 — 战吼：如果手牌有龙，发现一张龙牌
		b.Card("CORE_KAR_062",
			battlecry: MakeDiscover(S.CONTROLLER, S.MINION));

		// 硬壳甲虫 CORE_LOOT_413 — 亡语：获得 3 点护甲
		b.Card("CORE_LOOT_413",
			deathrattle: new GainArmor(S.FRIENDLY_HERO, 3));

		// 血帆袭击者 CORE_NEW1_018 — 战吼：获得等同于武器攻击力的攻击力
		b.Card("CORE_NEW1_018",
			battlecry: new Enchant(S.SELF, "CORE_NEW1_018e"));
		b.Buff("CORE_NEW1_018e", atk: WEAPON_ATK);

		// 卑劣的脏鼠 CORE_CFM_790 — 嘲讽，战吼：对手随机从手牌召唤一个随从
		b.Card("CORE_CFM_790",
			battlecry: new Summon(S.OPPONENT, S.Random(S.ENEMY_HAND + S.MINION)));

		// 受伤的托维尔人 CORE_ULD_271 — 嘲讽，战吼：对自身造成 3 点伤害
		b.Card("CORE_ULD_271",
			battlecry: new DealDamage(S.SELF, 3));

		// 红鳃锋颚战士 CS3_038 — 突袭（纯属性）
		b.Card("CS3_038");

		// 年轻的酒仙 CORE_EX1_049 — 战吼：将一个友方随从移回手牌
		b.Card("CORE_EX1_049",
			battlecry: new ReturnToHand(S.TARGET),
			target: Target.IfAvailable().Friendly().Minion());

		// 疯狂投弹者 CORE_EX1_082 — 战吼：造成 3 点伤害随机分配
		b.Card("CORE_EX1_082",
			battlecry: new DealDamage(S.RANDOM_OTHER_CHARACTER, 1) * 3);

		// 狂野炎术师 CORE_NEW1_020 — 施放法术后对所有随从造成 1 点伤害
		b.Card("CORE_NEW1_020",
			on: When<SpellCastEvent>(OWN, AFTER, new DealDamage(S.ALL_MINIONS, 1)));

		// 精灵龙 CORE_NEW1_023 — 扰魔（纯属性）
		b.Card("CORE_NEW1_023");

		// 异教低阶牧师 CORE_SCH_713 — 战吼：下回合对手法术费用+1
		b.Card("CORE_SCH_713",
			battlecry: new Enchant(S.CONTROLLER, "CORE_SCH_713e"));
		b.Buff("CORE_SCH_713e",
			update: new Refresh(S.ENEMY_HAND + S.SPELL, GameTag.Cost, +1),
			on: When<TurnEndEvent>(ENEMY, ON, new Destroy(S.SELF)));

		// ═══ 3 费 ═══

		// 苦痛侍僧 CORE_EX1_007 — 每当受到伤害，抽一张牌
		b.Card("CORE_EX1_007",
			on: When<DamageEvent>(ANY, ON, new DrawCard(S.CONTROLLER),
				when: (Func<DamageEvent, BuffableEntity, bool>)((evt, self) => evt.Target == self)));

		// 焦油爬行者 CORE_UNG_928 — 嘲讽，对手回合 +2 攻击力
		b.Card("CORE_UNG_928", update: new[]
		{
			new Refresh(S.SELF, GameTag.Atk, 2, condition: new CurrentPlayer(S.OPPONENT))
		});

		// 石丘防御者 Core_UNG_072 — 嘲讽，战吼：发现一张嘲讽随从
		b.Card("Core_UNG_072",
			battlecry: MakeDiscover(S.CONTROLLER, S.MINION));

		// 团队领袖 CORE_CS2_122 — 其他随从 +1 攻击力
		b.Card("CORE_CS2_122", update: new[]
		{
			new Refresh(S.FRIENDLY_MINIONS - S.SELF, buff: "CORE_CS2_122e")
		});

		// 寒光先知 CORE_EX1_103 — 战吼：其他鱼人 +2 生命值
		b.Card("CORE_EX1_103",
			battlecry: new Enchant(S.FRIENDLY_MINIONS + S.MURLOC - S.SELF, "CORE_EX1_103e"));
		b.Buff("CORE_EX1_103e", health: 2);

		// 狐人恶棍 CORE_ULD_209 — 战吼：发现一张法术牌
		b.Card("CORE_ULD_209",
			battlecry: MakeDiscover(S.CONTROLLER, S.SPELL));

		// 喷灯破坏者 CORE_DRG_403 — 战吼：对手下一个英雄技能费用+2
		b.Card("CORE_DRG_403",
			battlecry: new Enchant(S.OPPONENT, "CORE_DRG_403e"));
		b.Buff("CORE_DRG_403e",
			update: new Refresh(S.FRIENDLY_HERO_POWER, GameTag.Cost, +2),
			on: When<HeroPowerUsedEvent>(OWN, ON, new Destroy(S.SELF)));

		// 鱼人领军 CORE_EX1_507 — 其他鱼人 +2 攻击力
		b.Card("CORE_EX1_507", update: new[]
		{
			new Refresh(S.FRIENDLY_MINIONS + S.MURLOC - S.SELF, buff: "CORE_EX1_507e")
		});

		// 荆棘帮暴徒 CORE_GIL_534 — 英雄攻击后获得 +1/+1
		b.Card("CORE_GIL_534",
			on: When<AfterAttackEvent>(OWN, AFTER,
				new Enchant(S.SELF, "CORE_GIL_534e"),
				when: AttackerIsHero()));
		b.Buff("CORE_GIL_534e", atk: 1, health: 1);

		// 南海船长 CORE_NEW1_027 — 其他海盗 +1/+1
		b.Card("CORE_NEW1_027", update: new[]
		{
			new Refresh(S.FRIENDLY_MINIONS + S.PIRATE - S.SELF, buff: "CORE_NEW1_027e")
		});

		// 拆迁修理工 CORE_REV_023 — 战吼：摧毁一个敌方地标
		b.Card("CORE_REV_023",
			battlecry: new Destroy(S.TARGET),
			target: Target.IfAvailable().Enemy().Location());

		// 展馆茶杯 CORE_WON_141 — 战吼：随机三个不同类型友方随从 +1/+1
		b.Card("CORE_WON_141",
			battlecry: new Enchant(S.Random(S.FRIENDLY_MINIONS - S.SELF, 3, distinctByType: true), "CORE_WON_141e"));

		// A3型机械金刚 CORE_LOE_039 — 战吼：如果有其他机械，发现一张机械牌
		b.Card("CORE_LOE_039",
			battlecry: MakeDiscover(S.CONTROLLER, S.MINION));

		// 锈烂蝰蛇 CORE_SW_072 — 战吼：摧毁对手武器
		b.Card("CORE_SW_072",
			battlecry: new Destroy(S.ENEMY_WEAPON));

		// 穆克拉 CORE_EX1_014 — 战吼：使对手获得两根香蕉
		b.Card("CORE_EX1_014",
			battlecry: new AddToHand(S.OPPONENT, "EX1_014t") * 2);

		// ═══ 4 费 ═══

		// 吸血蚊 CORE_GIL_622 — 战吼：对敌方英雄造成 3 点伤害，恢复 3 点
		b.Card("CORE_GIL_622",
			battlecry: Acts(new DealDamage(S.ENEMY_HERO, 3), new RestoreHealth(S.FRIENDLY_HERO, 3)));

		// 恐怖海盗 CORE_NEW1_022 — 嘲讽，武器攻击力减费
		b.Card("CORE_NEW1_022",
			costMod: WEAPON_ATK);

		// 火羽凤凰 CORE_UNG_084 — 战吼：造成 3 点伤害
		b.Card("CORE_UNG_084",
			battlecry: new DealDamage(S.TARGET, 3),
			target: Target.IfAvailable());

		// 森金持盾卫士 CORE_CS2_179 — 嘲讽（纯属性）
		b.Card("CORE_CS2_179");

		// 暮光幼龙 CORE_EX1_043 — 战吼：每有一张手牌 +1 生命值
		b.Card("CORE_EX1_043",
			battlecry: new Enchant(S.SELF, "CORE_EX1_043e"));
		b.Buff("CORE_EX1_043e", health: HAND_COUNT);

		// 王牌猎人 CORE_EX1_005 — 战吼：消灭一个攻击力≥7 的随从
		b.Card("CORE_EX1_005",
			battlecry: new Destroy(S.TARGET),
			target: Target.IfAvailable().Minion().MinAttack(7));

		// 黑骑士 CORE_EX1_002 — 战吼：消灭一个具有嘲讽的敌方随从
		b.Card("CORE_EX1_002",
			battlecry: new Destroy(S.TARGET),
			target: Target.IfAvailable().Enemy().Minion().Taunt());

		// 王室图书管理员 CORE_SW_066 — 战吼：沉默一个随从
		b.Card("CORE_SW_066",
			battlecry: new Silence(S.TARGET),
			target: Target.IfAvailable().Minion());

		// ═══ 5 费 ═══

		// 展馆茶壶 CORE_WON_142 — 战吼：随机三个不同类型友方随从 +2/+2
		b.Card("CORE_WON_142",
			battlecry: new Enchant(S.Random(S.FRIENDLY_MINIONS - S.SELF, 3, distinctByType: true), "CORE_WON_142e"));

		// 泰兰·弗丁 CS3_024 — 嘲讽，圣盾，亡语：抽费用最高的随从牌
		b.Card("CS3_024",
			deathrattle: new DrawCard(S.CONTROLLER));

		// 飞火流星·芬杰 CORE_CFM_344 — 潜行，攻击并消灭随从后从牌库召唤两个鱼人
		b.Card("CORE_CFM_344",
			on: When<AfterAttackEvent>(OWN, AFTER,
				new TargetedAction[] {
					new Summon(S.CONTROLLER, S.Random(S.FRIENDLY_DECK + S.MURLOC)),
					new Summon(S.CONTROLLER, S.Random(S.FRIENDLY_DECK + S.MURLOC))
				},
				when: And(SelfIsAttacker(), DefenderDied())));

		// 伦萨克大王 CS3_025 — 突袭，攻击时手牌中所有随从 +1/+1
		b.Card("CS3_025",
			on: When<AfterAttackEvent>(OWN, AFTER,
				new Enchant(S.FRIENDLY_HAND + S.MINION, "CS3_025e"),
				when: SelfIsAttacker()));
		b.Buff("CS3_025e", atk: 1, health: 1);

		// 馆长 CORE_KAR_061 — 嘲讽，战吼：从牌库抽野兽/龙/鱼人各一张
		b.Card("CORE_KAR_061",
			battlecry: Acts(
				new DrawCard(S.CONTROLLER, Race.Beast),
				new DrawCard(S.CONTROLLER, Race.Dragon),
				new DrawCard(S.CONTROLLER, Race.Murloc)));

		// 荆棘谷猛虎 CORE_EX1_028 — 潜行（纯属性）
		b.Card("CORE_EX1_028");

		// 蒸汽清洁器 CORE_REV_946 — 战吼：摧毁双方牌库中非套牌原始的牌
		b.Card("CORE_REV_946",
			battlecry: Acts(
				new RemoveNonOriginalFromDeck(S.CONTROLLER),
				new RemoveNonOriginalFromDeck(S.OPPONENT)));

		// ═══ 6 费 ═══

		// 侏儒飞行员诺莉亚 CORE_TOY_100 — 突袭，溅射伤害，亡语：对所有敌人造成 2 点
		b.Card("CORE_TOY_100",
			deathrattle: new DealDamage(S.ENEMY_CHARACTERS, 2));

		// 考内留斯·罗姆 CORE_SW_080 — 每个回合开始和结束时抽牌
		b.Card("CORE_SW_080",
			on: new[]
			{
				When<TurnStartEvent>(ANY, ON, new DrawCard(S.CONTROLLER)),
				When<TurnEndEvent>(ANY, ON, new DrawCard(S.CONTROLLER))
			});

		// 辟法巨龙 CORE_DRG_079 — 突袭，圣盾，扰魔（纯属性）
		b.Card("CORE_DRG_079");

		// 凯恩·血蹄 CORE_EX1_110 — 嘲讽，亡语：召唤 5/5 贝恩·血蹄
		b.Card("CORE_EX1_110",
			deathrattle: new Summon(S.CONTROLLER, "EX1_110t"));

		// 石拳食人魔 Core_CS2_200 — 纯属性
		b.Card("Core_CS2_200");

		// ═══ 7 费 ═══

		// 斯尼德的伐木机 CORE_GVG_114 — 亡语：随机召唤一个传说随从
		b.Card("CORE_GVG_114",
			deathrattle: new Summon(S.CONTROLLER, RandomLegendaryMinion()));

		// 冰喉 CORE_AT_123 — 嘲讽，亡语：如果手牌有龙，对所有随从造成 3 点
		b.Card("CORE_AT_123",
			deathrattle: IfElse(new Find(S.FRIENDLY_HAND + S.DRAGON),
				new DealDamage(S.ALL_MINIONS, 3), NOTHING));

		// 暴风城勇士 CORE_CS2_222 — 其他随从 +1/+1
		b.Card("CORE_CS2_222", update: new[]
		{
			new Refresh(S.FRIENDLY_MINIONS - S.SELF, buff: "CORE_CS2_222e")
		});

		// 希亚玛特 CORE_ULD_178 — 战吼：获得两种效果
		b.Card("CORE_ULD_178",
			battlecry: new BeginSiamatChoice());
		b.Card("ULD_178a", play: new SetTag(S.SELF, GameTag.Windfury, 1));
		b.Card("ULD_178a2", play: new SetTag(S.SELF, GameTag.DivineShield, 1));
		b.Card("ULD_178a3", play: new SetTag(S.SELF, GameTag.Taunt, 1));
		b.Card("ULD_178a4", play: new SetTag(S.SELF, GameTag.Rush, 1));

		// 永恒者诺兹多姆 CS3_035 — 如果双方都有这张牌，每回合 15 秒
		b.Card("CS3_035",
			on: WhenDo<StartOfGameEvent>(OWN, ON, (evt, self) =>
			{
				var game = self.Game;
				if (game == null)
					return;

				bool p1Has = game.Player1.Deck.Any(c => (c as BuffableEntity)?.CardId == "CS3_035");
				bool p2Has = game.Player2.Deck.Any(c => (c as BuffableEntity)?.CardId == "CS3_035");
				if (!p1Has || !p2Has)
					return;

				game.Player1.Timeout = 15;
				game.Player2.Timeout = 15;
			}).InDeck());

		// ═══ 8 费 ═══

		// 始生幼龙 CORE_UNG_848 — 嘲讽，战吼：对所有其他随从造成 2 点伤害
		b.Card("CORE_UNG_848",
			battlecry: new DealDamage(S.ALL_MINIONS - S.SELF, 2));

		// 炎魔之王拉格纳罗斯 CORE_EX1_298 — 无法攻击，回合结束随机对敌人造成 8 点
		b.Card("CORE_EX1_298",
			on: When<TurnEndEvent>(OWN, ON, new DealDamage(S.RANDOM_ENEMY_CHARACTER, 8)));

		// 莫尔葛熔魔 CORE_SW_068 — 嘲讽，亡语：获得 8 点护甲
		b.Card("CORE_SW_068",
			deathrattle: new GainArmor(S.FRIENDLY_HERO, 8));

		// 八爪巨怪 CORE_ULD_177 — 亡语：抽八张牌
		b.Card("CORE_ULD_177",
			deathrattle: new DrawCard(S.CONTROLLER) * 8);

		// ═══ 9 费 ═══

		// 贪睡巨龙 CORE_LOOT_137 — 嘲讽（纯属性）
		b.Card("CORE_LOOT_137");

		// ═══ 10 费 ═══

		// 窜逃的黑翼龙 CORE_YOP_034 — 回合结束随机对敌方随从造成 10 点伤害
		b.Card("CORE_YOP_034",
			on: When<TurnEndEvent>(OWN, ON, new DealDamage(S.RANDOM_ENEMY_MINION, 10)));
	}
}
