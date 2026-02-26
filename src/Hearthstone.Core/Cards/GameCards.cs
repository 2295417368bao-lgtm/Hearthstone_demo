using HearthstoneServer.Auras;
using HearthstoneServer.Events;
using static HearthstoneServer.Dsl.BusEvent;
using static HearthstoneServer.Cards.CardScriptUtils;
using HearthstoneServer.Targeting;

namespace HearthstoneServer.Dsl.Cards;

// ═════════════════════════════════════════
// 游戏卡牌：基础英雄技能和衍生物
// ═════════════════════════════════════════

public class GameCards : CardPack
{
	public override void Register(CardBuilder b)
	{
		// -- 英雄技能 --
		// 注册两个 cardId 版本：CS2_* (经典) 和 HERO_*bp (新版数据库)

		// 火焰冲击（法师）：造成 1 点伤害
		b.Card("CS2_034",
			play: new DealDamage(S.TARGET, 1),
			target: Target.Required());
		b.Card("HERO_08bp",
			play: new DealDamage(S.TARGET, 1),
			target: Target.Required());

		// 次级治疗术（牧师）：恢复 2 点生命值
		b.Card("CS1_001",
			play: new RestoreHealth(S.TARGET, 2),
			target: Target.Required());
		b.Card("HERO_09bp",
			play: new RestoreHealth(S.TARGET, 2),
			target: Target.Required());

		// 匕首精通（盗贼）：装备 1/2 匕首
		b.Card("CS2_083b", play: new Summon(S.CONTROLLER, "CS2_082"));
		b.Card("HERO_03bp", play: new Summon(S.CONTROLLER, "CS2_082"));

		// 图腾召唤（萨满）：召唤一个随机基础图腾
		b.Card("CS2_049",
			play: new Summon(S.CONTROLLER, new RandomCardPicker()),
			entourage: new[] { "CS2_050", "CS2_051", "CS2_052", "NEW1_009" });
		b.Card("HERO_02bp",
			play: new Summon(S.CONTROLLER, new RandomCardPicker()),
			entourage: new[] { "CS2_050", "CS2_051", "CS2_052", "NEW1_009" });

		// 援军（圣骑士）：召唤一个 1/1 的白银之手新兵
		b.Card("CS2_101", play: new Summon(S.CONTROLLER, "CS2_101t"));
		b.Card("HERO_04bp", play: new Summon(S.CONTROLLER, "CS2_101t"));

		// 全副武装！（战士）：获得 2 点护甲
		b.Card("CS2_102", play: new GainArmor(S.FRIENDLY_HERO, 2));
		b.Card("HERO_01bp", play: new GainArmor(S.FRIENDLY_HERO, 2));

		// 稳固射击（猎人）：对敌方英雄造成 2 点伤害
		b.Card("DS1h_292", play: new DealDamage(S.ENEMY_HERO, 2));
		b.Card("HERO_05bp", play: new DealDamage(S.ENEMY_HERO, 2));

		// 生命分流（术士）：抽一张牌，对自身造成 2 点伤害
		b.Card("CS2_056", play: Acts(new DealDamage(S.FRIENDLY_HERO, 2), new DrawCard(S.CONTROLLER)));
		b.Card("HERO_07bp", play: Acts(new DealDamage(S.FRIENDLY_HERO, 2), new DrawCard(S.CONTROLLER)));

		// 变形术（德鲁伊）：本回合 +1 攻击力，+1 护甲
		b.Card("CS2_017", play: Acts(new Enchant(S.FRIENDLY_HERO, "CS2_017o"), new GainArmor(S.FRIENDLY_HERO, 1)));
		b.Card("HERO_06bp", play: Acts(new Enchant(S.FRIENDLY_HERO, "CS2_017o"), new GainArmor(S.FRIENDLY_HERO, 1)));
		b.Buff("CS2_017o", atk: 1,
			on: When<TurnEndEvent>(OWN, ON, new Destroy(S.SELF)));  // 变形术（本回合）

		// 恶魔之爪（恶魔猎手）：本回合 +1 攻击力
		b.Card("HERO_10bp", play: new Enchant(S.FRIENDLY_HERO, "HERO_10pe"));
		b.Buff("HERO_10pe", atk: 1,
			on: When<TurnEndEvent>(OWN, ON, new Destroy(S.SELF)));  // 恶魔之爪（本回合）
	}
}
