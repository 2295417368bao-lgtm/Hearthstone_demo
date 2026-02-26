using HearthstoneServer.Auras;
using HearthstoneServer.Events;
using static HearthstoneServer.Dsl.BusEvent;
using static HearthstoneServer.Cards.CardScriptUtils;
using static HearthstoneServer.Dsl.EventFilters;
using static HearthstoneServer.Dsl.RandomCards;
using HearthstoneServer.Targeting;

namespace HearthstoneServer.Dsl.Cards;

// ══════════════════════════════════════════════════════
// 核心盗贼卡牌脚本 (Core Set)
// ══════════════════════════════════════════════════════

public class CoreRogue : CardPack
{
	public override void Register(CardBuilder b)
	{
		// ── 附魔 ──
		b.Buff("CORE_EX1_145e",
			update: new Refresh(S.FRIENDLY_HAND + S.SPELL, GameTag.Cost, -2),
			on: When<CardPlayedEvent>(OWN, AFTER, new Destroy(S.SELF),
				when: PlayedIsSpell()));
		b.Buff("CORE_DAL_720e", cost: -2); // 摇摆矿锄弹回
		b.Buff("CORE_DMF_511e",
			update: new Refresh(S.FRIENDLY_HAND + new EnumSelector(GameTag.Combo), GameTag.Cost, -2),
			on: When<CardPlayedEvent>(OWN, AFTER, new Destroy(S.SELF),
				when: PlayedHasMechanic(Mechanic.COMBO)));

		// ── 随从 ──

		// 吹嘘海盗 CORE_KAR_069 — 战吼：随机将一张另一职业的卡牌置入手牌
		b.Card("CORE_KAR_069",
			battlecry: new AddToHand(S.CONTROLLER, RandomCard().ClassOf(S.OPPONENT)));

		// 幽暗城商贩 CORE_OG_330 — 亡语：随机获取对手职业的卡牌
		b.Card("CORE_OG_330",
			deathrattle: new AddToHand(S.CONTROLLER, RandomCard().ClassOf(S.OPPONENT)));

		// 狐人老千 CORE_DMF_511 — 战吼：下一张连击牌费用减少 (2)
		b.Card("CORE_DMF_511",
			battlecry: new Enchant(S.CONTROLLER, "CORE_DMF_511e"));

		// 迪菲亚头目 CORE_EX1_131 — 连击：召唤一个 2/1 迪菲亚强盗
		b.Card("CORE_EX1_131",
			combo: new Summon(S.CONTROLLER, "EX1_131t"));

		// 收集者沙库尔 CORE_CFM_781 — 潜行，攻击时获取对手职业卡
		b.Card("CORE_CFM_781",
			on: When<AfterAttackEvent>(OWN, AFTER,
				new AddToHand(S.CONTROLLER, RandomCard().ClassOf(S.OPPONENT)),
				when: SelfIsAttacker()));

		// 军情七处特工 CORE_EX1_134 — 连击：造成 3 点伤害
		b.Card("CORE_EX1_134",
			combo: new DealDamage(S.TARGET, 3),
			target: Target.IfAvailable());

		// 荆棘帮蟊贼 CORE_DAL_416 — 战吼：发现一张另一职业的法术牌
		b.Card("CORE_DAL_416",
			battlecry: MakeDiscover(S.CONTROLLER, DiscoverFilter.Spell().ClassOf(S.OPPONENT)));

		// 苔丝·格雷迈恩 CORE_GIL_598 — 战吼：重新使用所有使用过的另一职业卡牌
		b.Card("CORE_GIL_598",
			battlecry: new ReplayOtherClassCards(S.CONTROLLER));

		// ── 法术 ──

		// 背刺 CORE_CS2_072 — 对一个未受伤的随从造成 2 点伤害
		b.Card("CORE_CS2_072",
			play: new DealDamage(S.TARGET, 2),
			target: Target.Required().Minion().Undamaged());

		// 暗影步 CORE_EX1_144 — 将一个友方随从移回手牌，费用减少 (2)
		b.Card("CORE_EX1_144",
			play: Acts(new ReturnToHand(S.TARGET), new Enchant(S.TARGET, "CORE_DAL_720e")),
			target: Target.Required().Friendly().Minion());

		// 伺机待发 CORE_EX1_145 — 下一个法术费用减少 (2)
		b.Card("CORE_EX1_145",
			play: new Enchant(S.CONTROLLER, "CORE_EX1_145e"));

		// 致命药膏 CORE_CS2_074 — 使你的武器获得 +2 攻击力
		b.Card("CORE_CS2_074",
			play: new Enchant(S.FRIENDLY_WEAPON, "CORE_CS2_074e"));
		b.Buff("CORE_CS2_074e", atk: 2);

		// 刺骨 CORE_EX1_124 — 造成 2 点伤害，连击：改为 4 点
		b.Card("CORE_EX1_124",
			play: new DealDamage(S.TARGET, 2),
			combo: new DealDamage(S.TARGET, 4),
			target: Target.Required());

		// 刀扇 CORE_EX1_129 — 对所有敌方随从造成 1 点伤害，抽一张牌
		b.Card("CORE_EX1_129",
			play: Acts(new DealDamage(S.ENEMY_MINIONS, 1), new DrawCard(S.CONTROLLER)));

		// 团伙劫掠 CORE_TRL_124 — 抽两张海盗牌，连击：并抽一张武器牌
		b.Card("CORE_TRL_124",
			play: new DrawCard(S.CONTROLLER, Race.Pirate) * 2,
			combo: Acts(new DrawCard(S.CONTROLLER, Race.Pirate),
				new DrawCard(S.CONTROLLER, Race.Pirate),
				new DrawCard(S.CONTROLLER, CardType.Weapon)));

		// 刺杀 CORE_CS2_076 — 消灭一个敌方随从
		b.Card("CORE_CS2_076",
			play: new Destroy(S.TARGET),
			target: Target.Required().Enemy().Minion());

		// ── 武器 ──

		// 摇摆矿锄 CORE_DAL_720 — 亡语：随机将一个友方随从移回手牌，费用减少 (2)
		b.Card("CORE_DAL_720",
			deathrattle: new ReturnToHand(S.Random(S.FRIENDLY_MINIONS, 1))
				.Then(new Enchant(S.CURRENT_TARGET, "CORE_DAL_720e")));
	}
}
