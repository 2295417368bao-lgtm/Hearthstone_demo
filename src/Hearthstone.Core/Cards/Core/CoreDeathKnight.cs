using HearthstoneServer.Auras;
using HearthstoneServer.Events;
using static HearthstoneServer.Dsl.BusEvent;
using static HearthstoneServer.Cards.CardScriptUtils;
using static HearthstoneServer.Dsl.EventFilters;
using HearthstoneServer.Targeting;

namespace HearthstoneServer.Dsl.Cards;

// ══════════════════════════════════════════════════════
// 核心死亡骑士卡牌脚本 (Core Set)
// CardClass.DeathKnight = 554, Hero: HERO_11 (dbf=78065)
// ══════════════════════════════════════════════════════

public class CoreDeathKnight : CardPack
{
	public override void Register(CardBuilder b)
	{
		// ── 附魔 ──
		b.Buff("RLK_958e", atk: 2); // 骷髅帮手

		// ── 随从 ──

		// 骷髅帮手 RLK_958 — 战吼：使一个友方亡灵获得+2攻击力
		b.Card("RLK_958",
			battlecry: new Enchant(S.TARGET, "RLK_958e"),
			target: Target.IfAvailable().Friendly().Minion());

		// 扛包收尸人 RLK_503 — 战吼：获得两份残骸
		b.Card("RLK_503",
			battlecry: new GainCorpses(S.CONTROLLER, 2));

		// 战场通灵师 RLK_061 — 回合结束时，消耗一份残骸复活为 1/3 嘲讽
		b.Card("RLK_061",
			on: When<TurnEndEvent>(OWN, ON,
				new SpendCorpses(S.CONTROLLER, 1, new Summon(S.CONTROLLER, "RLK_061t"))));

		// 鲜血魔术师 CORE_RLK_066 — 战吼：消耗2残骸，发现一张鲜血符文牌
		b.Card("CORE_RLK_066",
			battlecry: new SpendCorpses(S.CONTROLLER, 2,
				MakeDiscover(S.CONTROLLER, S.SPELL)));

		// 死亡寒冰 CORE_RLK_083 — 法术后随机对两个敌人造成 1 点伤害
		b.Card("CORE_RLK_083",
			on: When<SpellCastEvent>(OWN, AFTER,
				new DealDamage(new RandomSelector(S.ENEMY_CHARACTERS, 2), 1)));

		// 死灵殡葬师 CORE_RLK_116 — 战吼：若上回合有友方亡灵死亡，发现邪恶符文牌
		// TODO: 条件检测（亡灵种族过滤）
		b.Card("CORE_RLK_116",
			battlecry: MakeDiscover(S.CONTROLLER, S.SPELL));

		// 寒冬先锋 RLK_511 — 亡语：抽一张冰霜法术牌
		b.Card("RLK_511",
			deathrattle: new DrawCard(S.CONTROLLER));

		// 堕寒男爵 RLK_708 — 战吼/亡语：抽一张牌
		b.Card("RLK_708",
			battlecry: new DrawCard(S.CONTROLLER),
			deathrattle: new DrawCard(S.CONTROLLER));

		// 法瑞克 CORE_EDR_003 — 残骸加倍（光环），战吼：抽一张消耗残骸的牌
		// TODO: 残骸加倍光环
		b.Card("CORE_EDR_003",
			battlecry: new DrawCard(S.CONTROLLER));

		// 死亡侍僧 CORE_RLK_121 — 友方亡灵死亡后抽牌
		b.Card("CORE_RLK_121",
			on: When<DeathEvent>(OWN, ON, new DrawCard(S.CONTROLLER)));

		// 死亡金属骑士 CORE_ETC_523 — 嘲讽
		// TODO: 条件改为消耗生命值
		b.Card("CORE_ETC_523");

		// 蛛魔护群守卫 CORE_RLK_062 — 嘲讽，战吼：召唤两个复制
		b.Card("CORE_RLK_062",
			battlecry: new SummonCopy(S.CONTROLLER, S.SELF) * 2);

		// 恶毒恐魔 CORE_RLK_745 — 复生，回合结束消耗2残骸召唤复制
		b.Card("CORE_RLK_745",
			on: When<TurnEndEvent>(OWN, ON,
				new SpendCorpses(S.CONTROLLER, 2, new SummonCopy(S.CONTROLLER, S.SELF))));

		// 萨萨里安 RLK_223 — 复生，战吼/亡语：随机对一个敌人造成 2 点伤害
		b.Card("RLK_223",
			battlecry: new DealDamage(S.RANDOM_ENEMY_CHARACTER, 2),
			deathrattle: new DealDamage(S.RANDOM_ENEMY_CHARACTER, 2));

		// 死亡使者萨鲁法尔 RLK_082 — 嘲讽，亡语：移回手牌（改为消耗生命值）
		// TODO: 消耗生命值机制
		b.Card("RLK_082",
			deathrattle: new AddToHand(S.CONTROLLER, "RLK_082"));

		// 髓骨使御者 CORE_RLK_505 — 战吼：消耗5残骸，每份造成 2 点伤害
		b.Card("CORE_RLK_505",
			battlecry: new SpendCorpses(S.CONTROLLER, 5,
				new DealDamage(S.RANDOM_ENEMY_CHARACTER, 2) * 5));

		// 侏儒嚼嚼怪 RLK_720 — 嘲讽/吸血，回合结束攻击生命值最低敌人
		b.Card("RLK_720",
			on: When<TurnEndEvent>(OWN, ON, new DealDamage(S.RANDOM_ENEMY_MINION, 6)));

		// 缝合巨人 RLK_744 — 消耗过残骸则减费（光环）
		// TODO: 光环减费（CorpsesSpentThisGame 计数）
		b.Card("RLK_744");

		// ── 法术 ──

		// 寒冬号角 CORE_RLK_042 — 复原两个法力水晶
		b.Card("CORE_RLK_042",
			play: new GainTemporaryMana(S.CONTROLLER, 2));

		// 毒性吐息 CORE_EDR_002 — 使一个友方亡灵获得剧毒
		b.Card("CORE_EDR_002",
			play: GivePoisonous(S.TARGET),
			target: Target.Required().Friendly().Minion());

		// 吸血鬼之血 CORE_RLK_051 — 英雄获得 +5 生命值
		b.Card("CORE_RLK_051",
			play: new RestoreHealth(S.FRIENDLY_HERO, 5));

		// 活力分流 CORE_RLK_712 — 手牌中所有随从 +1/+1
		b.Card("CORE_RLK_712",
			play: new Enchant(S.FRIENDLY_HAND + S.MINION, "CORE_RLK_712e"));
		b.Buff("CORE_RLK_712e", atk: 1, health: 1);

		// 冰霜打击 RLK_025 — 对一个随从造成 3 点伤害
		b.Card("RLK_025",
			play: new DealDamage(S.TARGET, 3),
			target: Target.Required().Minion());

		// 窒息 CORE_RLK_087 — 消灭攻击力最高的敌方随从
		b.Card("CORE_RLK_087",
			play: new Destroy(HIGHEST_ATK(S.ENEMY_MINIONS)));

		// 反魔法护罩 RLK_048 — 所有随从 +1/+1 和扰魔
		b.Card("RLK_048",
			play: new Enchant(S.FRIENDLY_MINIONS, "RLK_048e"));
		b.Buff("RLK_048e", atk: 1, health: 1, extraTags: (GameTag.CantBeTargetedBySpells, true));

		// 坟墓守卫 CORE_RLK_118 — 召唤两个 2/2 嘲讽僵尸
		b.Card("CORE_RLK_118",
			play: new Summon(S.CONTROLLER, "RLK_118t") * 2);

		// 灵界打击 RLK_024 — 吸血，对一个随从造成 6 点伤害
		b.Card("RLK_024",
			play: Acts(new DealDamage(S.TARGET, 6), new RestoreHealth(S.FRIENDLY_HERO, 6)),
			target: Target.Required().Minion());

		// 墓地之力 RLK_707 — 消耗4残骸时 +3攻，否则 +1攻
		b.Card("RLK_707",
			play: new IfCorpses(S.CONTROLLER, 4,
				new Enchant(S.FRIENDLY_MINIONS, "RLK_707e2"),
				new Enchant(S.FRIENDLY_MINIONS, "RLK_707e")));
		b.Buff("RLK_707e", atk: 1);
		b.Buff("RLK_707e2", atk: 3);

		// 冷酷严冬 RLK_709 — 对所有敌人造成 2 点伤害，抽一张牌
		b.Card("RLK_709",
			play: Acts(new DealDamage(S.ENEMY_CHARACTERS, 2), new DrawCard(S.CONTROLLER)));

		// 邪爆 CORE_RLK_035 — 消耗所有残骸，对所有随从各造成 1 点伤害（按残骸数循环）
		b.Card("CORE_RLK_035",
			play: new DealDamage(S.ALL_MINIONS, 3));
		// TODO: 循环引爆（按残骸数，每份 1 点全体伤害）

		// 亡者大军 RLK_060 — 将所有残骸复活为 2/2 突袭食尸鬼
		b.Card("RLK_060",
			play: new SummonPerCorpse(S.CONTROLLER, "RLK_060t"));

		// 冰霜巨龙之怒 CORE_RLK_063 — 造成 5 点伤害，冻结所有敌方随从，召唤 5/5 龙
		b.Card("CORE_RLK_063",
			play: Acts(new DealDamage(S.TARGET, 5), Freeze(S.ENEMY_MINIONS),
				new Summon(S.CONTROLLER, "RLK_063t")),
			target: Target.Required());

		// ── 武器 ──

		// 断魂 CORE_RLK_012 — 英雄攻击并消灭随从后，获得 2 份残骸
		b.Card("CORE_RLK_012",
			on: When<DeathEvent>(OWN, ON, new GainCorpses(S.CONTROLLER, 2)));

		// 霜之哀伤 CORE_RLK_086 — 亡语：召唤被该武器消灭的所有随从
		// TODO: 记录消灭的随从
		b.Card("CORE_RLK_086");
	}
}
