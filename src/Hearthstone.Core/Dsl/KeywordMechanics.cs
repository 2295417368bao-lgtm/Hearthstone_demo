using HearthstoneServer.Entities;
using Serilog;

namespace HearthstoneServer.Dsl;

// ═══════════════════════════════════════════════════
// Keyword Mechanics — 微缩 / 扩大
// ═══════════════════════════════════════════════════

// ─── AddMiniCopyToHand ──────────

/// <summary>
/// 微缩：创建 1/1/1 副本 + Mini 标记 + CreatedByMiniaturize 标记 → 加入手牌。
/// </summary>
public class AddMiniCopyToHand : GameAction
{
	private readonly Minion _source;

	public AddMiniCopyToHand(Minion source) : base()
	{
		_source = source;
	}

	public override void Do(BaseEntity source, params object[] args)
	{
		var player = _source.Controller;
		if (player == null) return;

		var cardId = _source.CardId;
		if (cardId == null) return;

		// 创建新卡实体放到手牌
		var copy = player.CreateCard(cardId, Zone.Hand);
		if (copy == null) return;

		// 设置为 1/1/1
		copy.Cost = 1;
		if (copy is Character ch)
		{
			ch.Atk = 1;
			ch.MaxHealth = 1;
		}

		// 添加标记（使用属性系统）
		copy.SetIntProperty("mini", 1);
		copy.SetIntProperty("created_by_miniaturize", 1);

		Log.Information("{Player} gets Mini copy of {Card} (1/1/1)", player.Name, cardId);
	}

	public override string ToString() => $"AddMiniCopyToHand({_source.CardId})";
}

// ─── AddGiganticCopyToHand ──────────

/// <summary>
/// 扩大：创建 8/8/8 版本 + CreatedByGigantify + Gigantic 标记 → 加入手牌。
/// </summary>
public class AddGiganticCopyToHand : GameAction
{
	private readonly Minion _source;

	public AddGiganticCopyToHand(Minion source) : base()
	{
		_source = source;
	}

	public override void Do(BaseEntity source, params object[] args)
	{
		var player = _source.Controller;
		if (player == null) return;

		var cardId = _source.CardId;
		if (cardId == null) return;

		// 创建新卡实体放到手牌
		var copy = player.CreateCard(cardId, Zone.Hand);
		if (copy == null) return;

		// 设置为 8/8/8
		copy.Cost = 8;
		if (copy is Character ch)
		{
			ch.Atk = 8;
			ch.MaxHealth = 8;
		}

		// 添加标记（使用属性系统）
		copy.SetIntProperty("created_by_gigantify", 1);
		copy.SetIntProperty("gigantic", 1);

		Log.Information("{Player} gets Gigantic copy of {Card} (8/8/8)", player.Name, cardId);
	}

	public override string ToString() => $"AddGiganticCopyToHand({_source.CardId})";
}
