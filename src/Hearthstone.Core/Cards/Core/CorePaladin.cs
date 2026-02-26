using HearthstoneServer.Auras;
using HearthstoneServer.Events;
using static HearthstoneServer.Dsl.BusEvent;
using static HearthstoneServer.Cards.CardScriptUtils;
using static HearthstoneServer.Dsl.EventFilters;
using static HearthstoneServer.Dsl.RandomCards;
using HearthstoneServer.Targeting;

namespace HearthstoneServer.Dsl.Cards;

// ══════════════════════════════════════════════════════
// 核心圣骑士卡牌脚本 (Core Set)
// ══════════════════════════════════════════════════════

public class CorePaladin : CardPack
{
	public override void Register(CardBuilder b)
	{
		// ── 附魔 ──
		b.Buff("CORE_BT_292e", atk: 2, health: 1); // 阿达尔之手
		b.Buff("CORE_CFM_753e", atk: 1, health: 1); // 污手街供货商
		// 剑龙骑术附魔：+2/+6 嘲讽 + 亡语召唤剑龙
		b.Buff("CORE_UNG_952e", atk: 2, health: 6,
			deathrattle: new Action[] { new Summon(S.CONTROLLER, "UNG_810") },
			extraTags: (GameTag.Taunt, true));

		// ── 随从 ──

		// 正义保护者 CORE_ICC_038 — 嘲讽，圣盾（纯属性）
		b.Card("CORE_ICC_038");

		// 污手街供货商 CORE_CFM_753 — 战吼：手牌中所有随从 +1/+1
		b.Card("CORE_CFM_753",
			battlecry: new Enchant(S.FRIENDLY_HAND + S.MINION, "CORE_CFM_753e"));

		// 赤鳞驯龙者 CORE_DMF_194 — 亡语：抽一张龙牌
		b.Card("CORE_DMF_194",
			deathrattle: new DrawCard(S.CONTROLLER, Race.Dragon));

		// 银色保卫者 CORE_EX1_362 — 战吼：使一个其他友方随从获得圣盾
		b.Card("CORE_EX1_362",
			battlecry: GiveDivineShield(S.TARGET),
			target: Target.IfAvailable().Friendly().Minion());

		// 青铜龙探险者 CORE_DRG_229 — 吸血，战吼：发现一张龙牌
		b.Card("CORE_DRG_229",
			battlecry: MakeDiscover(S.CONTROLLER, DiscoverFilter.OfRace(Race.Dragon)));

		// 阿纳克洛斯 CORE_RLK_919 — 战吼：将所有其他随从送入 2 回合后的未来
		b.Card("CORE_RLK_919",
			battlecry: new SendToFuture(S.ALL_MINIONS - S.SELF, turns: 2));

		// 提里奥·弗丁 CORE_EX1_383 — 圣盾，嘲讽，亡语：装备 5/3 灰烬使者
		b.Card("CORE_EX1_383",
			deathrattle: new EquipWeapon(S.CONTROLLER, "EX1_383t"));

		// ── 法术 ──

		// 阿达尔之手 CORE_BT_292 — 使一个随从获得 +2/+1，抽一张牌
		b.Card("CORE_BT_292",
			play: Acts(new Enchant(S.TARGET, "CORE_BT_292e"), new DrawCard(S.CONTROLLER)),
			target: Target.Required().Minion());

		// 生而平等 CORE_EX1_619 — 将所有随从的生命值变为 1
		b.Card("CORE_EX1_619",
			play: new Enchant(S.ALL_MINIONS, "CORE_EX1_619e"));
		b.Buff("CORE_EX1_619e", maxHealthMod: SetTo(1));

		// 圣光闪现 CORE_TRL_307 — 恢复 4 点生命值，抽一张牌
		b.Card("CORE_TRL_307",
			play: Acts(new RestoreHealth(S.TARGET, 4), new DrawCard(S.CONTROLLER)),
			target: Target.Required());

		// 抗性光环 TTN_851 — 你对手法术的法力值消耗增加(1)点。持续@个敌方回合。
		// Duration 从 CardData 的 QuestProgressTotal tag 自动读取
		// 附魔放在己方英雄上，这样 ENEMY_HAND 从 source(附魔) 的 Controller 视角才能正确选到对手手牌
		b.Card("TTN_851",
			play: new Enchant(S.FRIENDLY_HERO, "TTN_851e"));
		b.Buff("TTN_851e",
			update: new Refresh(S.ENEMY_HAND + S.SPELL, GameTag.Cost, +1));

		// 奉献 CORE_CS2_093 — 对所有敌人造成 2 点伤害
		b.Card("CORE_CS2_093", play: new DealDamage(S.ENEMY_CHARACTERS, 2));

		// 愤怒之锤 CORE_CS2_094 — 造成 3 点伤害，抽一张牌
		b.Card("CORE_CS2_094",
			play: Acts(new DealDamage(S.TARGET, 3), new DrawCard(S.CONTROLLER)),
			target: Target.Required());

		// 作战动员 CORE_GVG_061 — 召唤三个 1/1 白银之手新兵，装备 1/4 武器
		b.Card("CORE_GVG_061",
			play: Acts(new Summon(S.CONTROLLER, "CS2_101t"),
				new Summon(S.CONTROLLER, "CS2_101t"),
				new Summon(S.CONTROLLER, "CS2_101t"),
				new EquipWeapon(S.CONTROLLER, "CS2_091")));

		// 银月城传送门 CORE_KAR_077 — 使一个随从获得 +2/+2，随机召唤一个 2 费随从
		b.Card("CORE_KAR_077",
			play: Acts(new Enchant(S.TARGET, "CORE_KAR_077e"),
				new Summon(S.CONTROLLER, RandomCard().Minion().Cost(2))),
			target: Target.Required().Minion());
		b.Buff("CORE_KAR_077e", atk: 2, health: 2);

		// 十字军光环 TTN_908 — 每当一个友方随从攻击时，使其获得+2/+1。持续@回合。
		b.Card("TTN_908",
			play: new Enchant(S.FRIENDLY_HERO, "TTN_908e"));
		b.Buff("TTN_908e",
			on: When<AfterAttackEvent>(OWN, AFTER,
				new Enchant(S.EVT_SOURCE, "TTN_908e2"),
				when: AttackerIsFriendlyMinion()));
		b.Buff("TTN_908e2", atk: 2, health: 1);

		// 剑龙骑术 CORE_UNG_952 — 使一个随从获得 +2/+6 和嘲讽，亡语召唤剑龙
		b.Card("CORE_UNG_952",
			play: new Enchant(S.TARGET, "CORE_UNG_952e"),
			target: Target.Required().Minion());

		// 永存石中 CORE_TSC_076 — 召唤 4/8、2/4、1/2 嘲讽元素各一个
		b.Card("CORE_TSC_076",
			play: Acts(
				new Summon(S.CONTROLLER, "TSC_076t"),
				new Summon(S.CONTROLLER, "TSC_076t2"),
				new Summon(S.CONTROLLER, "TSC_076t3")));

		// ── 武器 ──

		// 幽光鱼竿 CORE_BT_018 — 英雄攻击后，随机将一张鱼人牌置入手牌
		b.Card("CORE_BT_018",
			on: When<AfterAttackEvent>(OWN, AFTER,
				new AddToHand(S.CONTROLLER, RandomMurloc()),
				when: AttackerIsHero()));
	}
}
