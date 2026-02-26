using HearthstoneServer.Dsl;

namespace HearthstoneServer.Entities;

// ═══════════════════════════════════════════════════
// SideQuest — a side quest spell card
// ═══════════════════════════════════════════════════

/// <summary>
/// 支线任务实体。继承 Quest，拥有完整的进度系统。
/// 与 Quest 的区别：
/// - 不占用任务栏位（可与 Quest 共存）
/// - 同 ID 不能重复（唯一性）
/// </summary>
public class SideQuest : Quest
{
	/// <summary>
	/// 支线任务唯一性 + 数量上限检查。
	/// 与 Quest 不同：不阻止已有 Quest 的情况，只检查同 ID 唯一。
	/// </summary>
	public override bool IsSummonable()
	{
		if (Controller == null) return false;

		// 同 ID 唯一性
		foreach (var s in Controller.Secrets)
		{
			if (s is BuffableEntity be && be.CardId == CardId)
				return false;
		}

		if (Controller.Secrets.Count >= Game.MAX_SECRETS_ON_PLAY)
			return false;

		// 跳过 Quest.IsSummonable 的 "只能一个任务" 检查
		return Controller.Mana >= Cost;
	}
}
