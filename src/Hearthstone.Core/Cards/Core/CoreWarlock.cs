using HearthstoneServer.Auras;
using HearthstoneServer.Entities;
using HearthstoneServer.Events;
using static HearthstoneServer.Dsl.BusEvent;
using static HearthstoneServer.Cards.CardScriptUtils;
using static HearthstoneServer.Dsl.EventFilters;
using HearthstoneServer.Targeting;

namespace HearthstoneServer.Dsl.Cards;

// ══════════════════════════════════════════════════════
// 核心术士卡牌脚本 (Core Set)
// ══════════════════════════════════════════════════════

public class CoreWarlock : CardPack
{
	public override void Register(CardBuilder b)
	{
		// ── 附魔 ──
		b.Buff("CORE_SCH_158e",
			update: new Refresh(S.FRIENDLY_HAND + new EnumSelector(Race.Demon), GameTag.Cost, -1),
			on: When<CardPlayedEvent>(OWN, AFTER, new Destroy(S.SELF),
				when: PlayedRace(Race.Demon)));

		// ── 随从 / 英雄 ──

		// 加拉克苏斯大王 CORE_EX1_323 — 英雄牌，战吼：装备 3/8 血怒
		// 英雄替换 + 护甲 + 技能替换由 PlayHeroCard 自动处理
		b.Card("CORE_EX1_323",
			battlecry: new EquipWeapon(S.CONTROLLER, "EX1_323w"));

		// 邪魔仆从 CORE_YOD_026 — 亡语：随机使一个友方随从获得本随从的攻击力
		b.Card("CORE_YOD_026",
			deathrattle: new Enchant(S.Random(S.FRIENDLY_MINIONS), "CORE_YOD_026e"));
		b.Buff("CORE_YOD_026e", atk: BuffValue.SOURCE_ATK);

		// 烈焰小鬼 CORE_EX1_319 — 战吼：对自身英雄造成 3 点伤害
		b.Card("CORE_EX1_319",
			battlecry: new DealDamage(S.FRIENDLY_HERO, 3));

		// 黑市摊贩 CORE_WON_096 — 战吼：发现一张 1 费卡牌
		b.Card("CORE_WON_096",
			battlecry: MakeDiscover(S.CONTROLLER, DiscoverFilter.All().Cost(1)));

		// 粗俗的矮劣魔 CORE_LOOT_013 — 嘲讽，战吼：对自身英雄造成 2 点伤害
		b.Card("CORE_LOOT_013",
			battlecry: new DealDamage(S.FRIENDLY_HERO, 2));

		// 卑鄙的恐惧魔王 CORE_ICC_075 — 回合结束时对所有敌方随从造成 1 点伤害
		b.Card("CORE_ICC_075",
			on: When<TurnEndEvent>(OWN, ON, new DealDamage(S.ENEMY_MINIONS, 1)));

		// 末日守卫 CORE_EX1_310 — 冲锋，战吼：随机弃两张牌
		b.Card("CORE_EX1_310",
			battlecry: new Discard(new RandomSelector(S.FRIENDLY_HAND, 2)));

		// 改进型恐惧魔王 CORE_BT_304 — 嘲讽，亡语：召唤 5/5 吸血恐惧魔王
		b.Card("CORE_BT_304",
			deathrattle: new Summon(S.CONTROLLER, "BT_304t"));

		// ── 法术 ──

		// 灵魂炸弹 CORE_BOT_222 — 对一个随从和你的英雄各造成 4 点伤害
		b.Card("CORE_BOT_222",
			play: Acts(new DealDamage(S.TARGET, 4), new DealDamage(S.FRIENDLY_HERO, 4)),
			target: Target.Required().Minion());

		// 莫瑞甘的灵界 CORE_BOT_568 — 抽三张临时牌（回合结束时弃掉）
		b.Card("CORE_BOT_568",
			play: new DrawCard(S.CONTROLLER)
				.Then(new SetTag(S.CURRENT_TARGET, GameTag.Temporary, 1)) * 3);

		// 死亡缠绕 CORE_EX1_302 — 对一个随从造成 1 点伤害，若消灭则抽牌
		b.Card("CORE_EX1_302",
			play: new DealDamage(S.TARGET, 1).IfThen(KILLED, new DrawCard(S.CONTROLLER)),
			target: Target.Required().Minion());

		// 恶魔研习 CORE_SCH_158 — 发现一张恶魔牌，下一张恶魔牌费用 -1
		b.Card("CORE_SCH_158",
			play: Acts(
				MakeDiscover(S.CONTROLLER, DiscoverFilter.OfRace(Race.Demon)),
				new Enchant(S.CONTROLLER, "CORE_SCH_158e")));

		// 吸取灵魂 CORE_ICC_055 — 吸血，对一个随从造成 3 点伤害
		b.Card("CORE_ICC_055",
			play: new DealDamage(S.TARGET, 3).Lifesteal(),
			target: Target.Required().Minion());

		// 地狱烈焰 CORE_CS2_062 — 对所有角色造成 3 点伤害
		b.Card("CORE_CS2_062",
			play: new DealDamage(S.ALL_CHARACTERS, 3));

		// 灵魂虹吸 CORE_EX1_309 — 消灭一个随从，恢复 3 点生命值
		b.Card("CORE_EX1_309",
			play: Acts(new Destroy(S.TARGET), new RestoreHealth(S.FRIENDLY_HERO, 3)),
			target: Target.Required().Minion());

		// 暗巷契约 CORE_SW_085 — 召唤一个嘲讽邪魔（属性等于手牌数）
		b.Card("CORE_SW_085",
			play: new Summon(S.CONTROLLER, "SW_085t")
				.WithStats(atk: LazyNum.HAND_COUNT, health: LazyNum.HAND_COUNT));

		// 恶魔来袭 CORE_SW_088 — 造成 3 点伤害，召唤两个 1/3 虚空行者
		b.Card("CORE_SW_088",
			play: new DealDamage(S.TARGET, 3)
				.Then(new Summon(S.CONTROLLER, "CS2_065") * 2),
			target: Target.Required());
	}
}
