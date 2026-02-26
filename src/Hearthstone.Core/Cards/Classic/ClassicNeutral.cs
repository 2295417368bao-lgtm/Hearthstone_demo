using HearthstoneServer.Auras;
using HearthstoneServer.Events;
using static HearthstoneServer.Dsl.BusEvent;

using static HearthstoneServer.Cards.CardScriptUtils;
using HearthstoneServer.Targeting;

namespace HearthstoneServer.Dsl.Cards;

// ═════════════════════════════════════════
// 经典中立卡牌脚本
// ═════════════════════════════════════════

public class ClassicNeutral : CardPack
{
	public override void Register(CardBuilder b)
	{
		// ── 附魔 ──
		b.Buff("CS2_222o", atk: 1, health: 1);                // 暴风城勇士
		b.Buff("CS2_226e", atk: 0, health: 1);                // 霜狼督军（简化版）
		b.Buff("EX1_162o", atk: 1);                           // 恐狼前锋
		b.Buff("EX1_399e", atk: 1);                           // 古拉巴什狂暴者
		b.Buff("EX1_590e", atk: 1, health: 1);                // 血骑士
		b.Buff("CS2_181e", atk: 2);                           // 受伤的剑圣（简化版）
		b.Buff("EX1_093e", atk: 1);                           // 银色保卫者
		b.Buff("NEW1_027e", atk: 1, health: 1);               // 南海船长
		b.Buff("EX1_058e", atk: 1, health: 1);                // 阳炎守卫（简化版）

		// ── 简单战吼随从 ──

		// 精灵弓箭手：战吼：造成 1 点伤害
		b.Card("CS2_189",
			battlecry: new DealDamage(S.TARGET, 1),
			target: Target.IfAvailable());

		// 巫医：战吼：恢复 2 点生命值
		b.Card("EX1_011",
			battlecry: new RestoreHealth(S.TARGET, 2),
			target: Target.IfAvailable());

		// 工程师学徒：战吼：抽一张牌
		b.Card("EX1_015", battlecry: new DrawCard(S.CONTROLLER));

		// 破碎残阳祭司：战吼：使一个友方随从获得 +1/+1
		b.Card("EX1_019",
			battlecry: new Enchant(S.TARGET, "EX1_019e"),
			target: Target.IfAvailable().Minion().Friendly().NonSelf());
		b.Buff("EX1_019e", atk: 1, health: 1);

		// 叫嚣的中士：战吼：使一个随从本回合获得 +2 攻击力
		b.Card("CS2_188",
			battlecry: new Enchant(S.TARGET, "CS2_188o"),
			target: Target.IfAvailable().Minion());
		b.Buff("CS2_188o", atk: 2,
			on: When<TurnEndEvent>(OWN, ON, new Destroy(S.SELF)));  // 叫嚣的中士（本回合）

		// 铁炉堡火枪手：战吼：造成 1 点伤害
		b.Card("CS2_141",
			battlecry: new DealDamage(S.TARGET, 1),
			target: Target.IfAvailable());

		// 夜刃刺客：战吼：对敌方英雄造成 3 点伤害
		b.Card("EX1_593", battlecry: new DealDamage(S.ENEMY_HERO, 3));

		// 暗鳞治疗者：战吼：恢复所有友方角色 2 点生命值
		b.Card("DS1_055", battlecry: new RestoreHealth(S.FRIENDLY_CHARACTERS, 2));

		// 雷矛特种兵：战吼：造成 2 点伤害
		b.Card("CS2_150",
			battlecry: new DealDamage(S.TARGET, 2),
			target: Target.IfAvailable());

		// 侏儒发明家：战吼：抽一张牌
		b.Card("CS2_147", battlecry: new DrawCard(S.CONTROLLER));

		// 银色保卫者：战吼：使相邻随从获得 +1/+1 和嘲讽
		b.Card("EX1_093", battlecry: new Enchant(S.SELF_ADJACENT, "EX1_093e"));
		b.Buff("EX1_093e_full", atk: 1, health: 1, extraTags: (GameTag.Taunt, true));

		// 寒光先知：战吼：双方各抽两张牌
		b.Card("EX1_050", battlecry: Acts(new DrawCard(S.CONTROLLER) * 2));

		// 践踏科多兽：战吼：消灭一个攻击力 ≤2 的随机敌方随从
		b.Card("NEW1_041", battlecry: new Destroy(S.RANDOM_ENEMY_MINION));

		// ── 光环/刷新随从 ──

		// 暴风城勇士：使其他友方随从获得 +1/+1
		b.Card("CS2_222", update: new[]
		{
			new Refresh(S.FRIENDLY_MINIONS - S.SELF, buff: "CS2_222o")
		});

		// 突袭队长：使其他友方随从获得 +1 攻击力
		b.Card("CS2_122", update: new[]
		{
			new Refresh(S.FRIENDLY_MINIONS - S.SELF, buff: "CS2_122e")
		});
		b.Buff("CS2_122e", atk: 1);

		// 恐狼前锋：相邻随从获得 +1 攻击力
		b.Card("EX1_162", update: new[]
		{
			new Refresh(S.SELF_ADJACENT, buff: "EX1_162o")
		});

		// 南海船长：使其他友方海盗获得 +1/+1
		b.Card("NEW1_027", update: new[]
		{
			new Refresh(S.FRIENDLY_MINIONS + S.PIRATE - S.SELF, buff: "NEW1_027e")
		});

		// 鱼人领军：使其他友方鱼人获得 +2 攻击力
		b.Card("EX1_507", update: new[]
		{
			new Refresh(S.FRIENDLY_MINIONS + S.MURLOC - S.SELF, buff: "EX1_507e")
		});
		b.Buff("EX1_507e", atk: 2);

		// ── 事件驱动随从 ──

		// 古拉巴什狂暴者：每当该随从受到伤害时，获得 +3 攻击力
		b.Card("EX1_399",
			on: When<DamageEvent>(SELF_TRIGGER, ON, new Enchant(S.SELF, "EX1_399e")));

		// 飞刀杂耍者：每当你召唤一个随从，对一个随机敌方角色造成 1 点伤害
		b.Card("NEW1_019",
			on: When<MinionSummonedEvent>(OWN, AFTER, new DealDamage(S.RANDOM_ENEMY_CHARACTER, 1)));

		// 狂野炎术师：每当你施放一个法术后，对所有随从造成 1 点伤害
		b.Card("NEW1_020",
			on: When<SpellCastEvent>(OWN, AFTER, new DealDamage(S.ALL_MINIONS, 1)));

		// 苦痛侍僧：每当该随从受到伤害时，抽一张牌
		b.Card("EX1_007",
			on: When<DamageEvent>(SELF_TRIGGER, ON, new DrawCard(S.CONTROLLER)));

		// 加基森拍卖师：每当你施放一个法术，抽一张牌
		b.Card("EX1_095",
			on: When<SpellCastEvent>(OWN, AFTER, new DrawCard(S.CONTROLLER)));

		// 任务达人：每当你使用一张牌，获得 +1/+1
		b.Card("EX1_044",
			on: When<CardPlayedEvent>(OWN, AFTER, new Enchant(S.SELF, "EX1_044e")));
		b.Buff("EX1_044e", atk: 1, health: 1);

		// ── 亡语随从 ──

		// 战利品贮藏者：亡语：抽一张牌
		b.Card("EX1_096", deathrattle: new DrawCard(S.CONTROLLER));

		// 麦田傀儡：亡语：召唤一个 2/1 的损坏的傀儡
		b.Card("EX1_556", deathrattle: new Summon(S.CONTROLLER, "skele21"));

		// 凯恩·血蹄：亡语：召唤贝恩·血蹄
		b.Card("EX1_110", deathrattle: new Summon(S.CONTROLLER, "EX1_110t"));

		// 希尔瓦娜斯·风行者：亡语：夺取一个随机敌方随从
		b.Card("EX1_016", deathrattle: new TakeControl(S.RANDOM_ENEMY_MINION));

		// 麻风侏儒：亡语：对敌方英雄造成 2 点伤害
		b.Card("EX1_029", deathrattle: new DealDamage(S.ENEMY_HERO, 2));

		// 憎恶：亡语：对所有角色造成 2 点伤害
		b.Card("EX1_097", deathrattle: new DealDamage(S.ALL_CHARACTERS, 2));

		// ── 特殊随从 ──

		// 无面操纵者：战吼：变为一个随从的复制
		b.Card("EX1_564",
			battlecry: new Transform(S.SELF, new Copy(S.TARGET)),
			target: Target.IfAvailable().Minion());

		// 受伤的剑圣：战吼：对自身造成 4 点伤害
		b.Card("CS2_181", battlecry: new DealDamage(S.SELF, 4));

		// 破法者：战吼：沉默一个随从
		b.Card("EX1_048",
			battlecry: new Silence(S.TARGET),
			target: Target.IfAvailable().Minion());

		// 阳炎守卫：战吼：使相邻随从获得嘲讽
		b.Card("EX1_058", battlecry: new Enchant(S.SELF_ADJACENT, "EX1_058e"));
		b.Buff("EX1_058t", extraTags: (GameTag.Taunt, true));

		// 践踏科多兽：消灭一个攻击力 ≤2 的随机敌方随从

		// 疯狂的炼金师：交换一个随从的攻击力和生命值
		b.Card("EX1_059",
			battlecry: new SwapAtkHealth(S.TARGET),
			target: Target.IfAvailable().Minion());

		// 大猎物看守者：战吼：消灭一个攻击力 ≥7 的随从
		b.Card("EX1_005",
			battlecry: new Destroy(S.TARGET),
			target: Target.IfAvailable().Minion().MinAttack(7));

		// 碧蓝幼龙：法术伤害 +1，战吼：抽一张牌
		b.Card("EX1_284", battlecry: new DrawCard(S.CONTROLLER));

		// 哈里森·琼斯：战吼：消灭敌方武器，抽等同于其耐久度的牌
		b.Card("EX1_558",
			battlecry: Acts(new Destroy(S.ENEMY_WEAPON), new DrawCard(S.CONTROLLER)));

		// 黑骑士：战吼：消灭一个具有嘲讽的敌方随从
		b.Card("EX1_002",
			battlecry: new Destroy(S.TARGET),
			target: Target.IfAvailable().Minion());

		// 阿莱克斯塔萨：战吼：将一个英雄的生命值设为 15
		b.Card("EX1_561",
			battlecry: new RestoreHealth(S.TARGET, 15),
			target: Target.Required().Hero());

		// 死亡之翼：战吼：消灭所有其他随从，弃掉你的手牌
		b.Card("NEW1_030",
			battlecry: Acts(new Destroy(S.ALL_MINIONS - S.SELF), new Discard(S.CONTROLLER)));

		// 血法师萨尔诺斯：法术伤害 +1，亡语：抽一张牌
		b.Card("EX1_012", deathrattle: new DrawCard(S.CONTROLLER));

		// 迦顿男爵：回合结束时对所有其他角色造成 2 点伤害
		b.Card("EX1_249",
			on: When<TurnEndEvent>(OWN, ON, new DealDamage(S.ALL_CHARACTERS - S.SELF, 2)));

		// 拉格纳罗斯：回合结束时对一个随机敌方角色造成 8 点伤害（不能攻击）
		b.Card("EX1_298",
			on: When<TurnEndEvent>(OWN, ON, new DealDamage(S.RANDOM_ENEMY_CHARACTER, 8)));

		// 伊瑟拉：回合结束时将一张随机梦境卡牌置入你的手牌
		b.Card("EX1_572",
			on: When<TurnEndEvent>(OWN, ON, new AddToHand(S.CONTROLLER, new RandomCardPicker())),
			entourage: new[] { "DREAM_01", "DREAM_02", "DREAM_03", "DREAM_04", "DREAM_05" });

		// 阿曼尼狂暴者：激怒：+3 攻击力
		b.Card("EX1_393", enrage: new[]
		{
			new Refresh(S.SELF, new Dictionary<GameTag, object> { [GameTag.Atk] = 3 })
		});
		b.Buff("EX1_393e", atk: 3);

		// 攻城车：回合开始时对一个随机敌方角色造成 2 点伤害
		b.Card("EX1_102",
			on: When<TurnStartEvent>(OWN, ON, new DealDamage(S.RANDOM_ENEMY_CHARACTER, 2)));
	}
}
