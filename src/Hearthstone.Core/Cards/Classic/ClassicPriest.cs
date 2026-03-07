using HearthstoneServer.Auras;
using HearthstoneServer.Entities;
using HearthstoneServer.Events;
using static HearthstoneServer.Dsl.BusEvent;
using static HearthstoneServer.Cards.CardScriptUtils;
using static HearthstoneServer.Dsl.EventFilters;
using HearthstoneServer.Targeting;

namespace HearthstoneServer.Dsl.Cards;

// ═════════════════════════════════════════
// 经典牧师卡牌脚本
// ═════════════════════════════════════════

public class ClassicPriest : CardPack
{
	public override void Register(CardBuilder b)
	{
		// ── 附魔 ──
		b.Buff("EX1_623e", health: 3);      // 圣殿执行者
		b.Buff("CS2_236e", atk: 5);         // 神圣之灵（简化版）

		// ── 随从 ──

		// 北郡牧师：每当一个随从被治疗时，抽一张牌
		b.Card("CS2_235",
			on: When<HealEvent>(ANY, ON, new DrawCard(S.CONTROLLER),
				when: HealTargetIsMinion()));

		// 暗影狂乱牧师：战吼：夺取攻击力 ≤2 的敌方随从
		b.Card("EX1_091",
			battlecry: new TakeControl(S.TARGET),
			target: Target.IfAvailable().Minion().Enemy().MaxAttack(2));

		// 光耀之子：攻击力等于生命值
		b.Card("EX1_335", update: new[]
		{
			new Refresh(S.SELF, new Dictionary<GameTag, object>
			{
				[GameTag.Atk] = 0
			})
		});

		// 光明之泉：回合开始时，恢复一个随机友方角色 3 点生命值
		b.Card("EX1_341",
			on: When<TurnStartEvent>(OWN, ON, new RestoreHealth(S.RANDOM_FRIENDLY_CHARACTER, 3)));

		// 先知维伦：法术伤害和治疗效果翻倍
		b.Card("EX1_350", update: new[]
		{
			new Refresh(S.CONTROLLER, new Dictionary<GameTag, object>
			{
				[GameTag.Spellpower] = 0
			})
		});

		// 圣殿执行者：战吼：使一个友方随从获得 +3 生命值
		b.Card("EX1_623",
			battlecry: new Enchant(S.TARGET, "EX1_623e"),
			target: Target.IfAvailable().Minion().Friendly());

		// 奥金尼灵魂祭司：你的治疗效果改为造成伤害
		b.Card("EX1_591", update: new[]
		{
			new Refresh(S.CONTROLLER, new Dictionary<GameTag, object>
			{
				[GameTag.HealingDoesDamage] = true
			})
		});

		// ── 法术 ──

		// 神圣惩击：造成 2 点伤害
		b.Card("CS1_130",
			play: new DealDamage(S.TARGET, 2),
			target: Target.Required());

		// 心灵震爆：对敌方英雄造成 5 点伤害
		b.Card("DS1_233", play: new DealDamage(S.ENEMY_HERO, 5));

		// 心灵视界：复制对手手牌中的一张随机卡牌
		b.Card("CS2_003", play: new AddToHand(S.CONTROLLER, new Copy(S.Random(S.ENEMY_HAND))));

		// 暗言术：痛：消灭一个攻击力 ≤3 的随从
		b.Card("CS2_234",
			play: new Destroy(S.TARGET),
			target: Target.Required().Minion().MaxAttack(3));

		// 神圣之灵：使一个随从的生命值翻倍
		b.Card("CS2_236",
			play: new Enchant(S.TARGET, "CS2_236e"),
			target: Target.Required().Minion());

		// 暗言术：灭：消灭一个攻击力 ≥5 的随从
		b.Card("EX1_622",
			play: new Destroy(S.TARGET),
			target: Target.Required().Minion().MinAttack(5));

		// 群体驱散：沉默所有敌方随从
		b.Card("EX1_626", play: new Silence(S.ENEMY_MINIONS));

		// 神圣新星：对所有敌方角色造成 2 点伤害，恢复所有友方角色 2 点生命值
		b.Card("CS1_112", play: Acts(new DealDamage(S.ENEMY_CHARACTERS, 2), new RestoreHealth(S.FRIENDLY_CHARACTERS, 2)));

		// 治疗之环：恢复所有随从 4 点生命值
		b.Card("EX1_621", play: new RestoreHealth(S.ALL_MINIONS, 4));

		// 暗影狂乱：夺取一个攻击力 ≤3 的敌方随从至本回合结束
		b.Card("EX1_334",
			play: new TakeControl(S.TARGET),
			target: Target.Required().Minion().Enemy().MaxAttack(3));

		// 精神控制：夺取一个敌方随从
		b.Card("CS1_113",
			play: new TakeControl(S.TARGET),
			target: Target.Required().Minion().Enemy());

		// 暗影形态：替换英雄技能
		b.Card("EX1_625", play: new Summon(S.CONTROLLER, "EX1_625t"));
		// 心灵之火：使一个随从的攻击力等同于其当前生命值
		b.Card("CS1_129",
			play: new SetAttackFromHealth(S.TARGET),
			target: Target.Required().Minion());
		// 沉默：移除一个随从的文字和附加效果
		b.Card("EX1_332",
			play: new Silence(S.TARGET),
			target: Target.Required().Minion());
		// 思维窃取：复制敌方牌库中的两张随机牌到你的手牌
		b.Card("EX1_339",
			play: new CopyToHand(S.CONTROLLER, new RandomSelector(S.ENEMY_DECK, 2)));
		// 控心术：夺取一个敌方随从的控制权
		b.Card("EX1_345",
			play: new TakeControl(S.TARGET),
			target: Target.Required().Minion().Enemy());
		// 神圣之火：造成 5 点伤害，并为你的英雄恢复 5 点生命值
		b.Card("EX1_624",
			play: Acts(
				new DealDamage(S.TARGET, 5),
				new RestoreHealth(S.FRIENDLY_HERO, 5)),
			target: Target.Required());
		b.Card("GIFT_12"); // 安度因的礼物
	}
}
