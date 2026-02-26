using HearthstoneServer.Auras;

using static HearthstoneServer.Cards.CardScriptUtils;

namespace HearthstoneServer.Dsl.Cards;

// ═════════════════════════════════════════
// 游戏规则：硬币、核心游戏衍生物、
// 全局附魔（GBL_xxx）
// ═════════════════════════════════════════

public class GameRulesCards : CardPack
{
	public override void Register(CardBuilder b)
	{
		// ── 核心游戏卡牌 ──

		// 幸运币：本回合获得一个法力水晶
		b.Card("GAME_005", play: new GainTemporaryMana(S.CONTROLLER, 1));

		// 硬币的祝福：+3 生命值（增益）
		b.Buff("GAME_001", health: 3);

		// 硬币的复仇（卡牌） — TODO: 当打出触发 DSL 就绪后添加事件
		b.Buff("GAME_003");

		// 硬币的复仇 +1/+1 增益
		b.Buff("GAME_003e", atk: 1, health: 1);

		// ── GBL 附魔（全局费用修改器） ──
		// SET(N) = 将费用覆盖为固定 N
		// 标签 COST: -N = 减少费用 N
		// 全部具有 REMOVED_IN_PLAY：卡牌打出后自动移除

		// GBL_001e：费用设为 1
		b.Buff("GBL_001e", costModFunc: SetTo(1), on: REMOVED_IN_PLAY);

		// GBL_002e：费用 -2
		b.Buff("GBL_002e", on: REMOVED_IN_PLAY,
			extraTags: new[] { (GameTag.Cost, (object)(-2)) });

		// GBL_003e：费用 -1
		b.Buff("GBL_003e", on: REMOVED_IN_PLAY,
			extraTags: new[] { (GameTag.Cost, (object)(-1)) });

		// GBL_004e：费用 -3
		b.Buff("GBL_004e", on: REMOVED_IN_PLAY,
			extraTags: new[] { (GameTag.Cost, (object)(-3)) });

		// GBL_005e：费用 +2
		b.Buff("GBL_005e", on: REMOVED_IN_PLAY,
			extraTags: new[] { (GameTag.Cost, (object)2) });

		// GBL_006e：费用设为 2
		b.Buff("GBL_006e", costModFunc: SetTo(2), on: REMOVED_IN_PLAY);

		// GBL_007e：费用设为 10
		b.Buff("GBL_007e", costModFunc: SetTo(10), on: REMOVED_IN_PLAY);

		// GBL_008e：费用 -4
		b.Buff("GBL_008e", on: REMOVED_IN_PLAY,
			extraTags: new[] { (GameTag.Cost, (object)(-4)) });

		// GBL_009e：费用设为 0
		b.Buff("GBL_009e", costModFunc: SetTo(0), on: REMOVED_IN_PLAY);
	}
}
