using HearthstoneServer.Dsl;
using Serilog;

namespace HearthstoneServer.Entities;

// ═══════════════════════════════════════════════════
// Secret — a secret spell card
// ═══════════════════════════════════════════════════

/// <summary>
/// 奥秘实体。打牌时进入 Zone.Secret 而非 Zone.Play。
/// 当条件满足时触发，触发后移至墓地。
/// </summary>
public class Secret : Spell
{
	private static readonly ILogger _log = LogConfig.ForType<Secret>();

	public Secret()
	{
		Type = CardType.Spell;
	}

	/// <summary>奥秘是否已消耗（自己回合中奥秘不触发）。</summary>
	public virtual bool Exhausted =>
		Zone == Zone.Secret && Controller != null && Controller.IsCurrentPlayer;



	// ── Play override ──

	/// <summary>
	/// Play this secret: pay cost then move to Zone.Secret (not graveyard).
	/// Secrets stay in Zone.Secret until triggered/revealed.
	/// </summary>
	public override void Play(Character? target = null, int index = -1)
	{
		// Pay cost via BuffableEntity.Play, but don't go to graveyard like Spell does
		Controller?.PayCost(Cost);
		SetZone(Zone.Play); // Redirects to Zone.Secret via SetZone override
	}

	/// <summary>
	/// Zone.Play → Zone.Secret 重定向。
	/// </summary>
	public override void SetZone(Zone zone, Player? controller = null)
	{
		if (zone == Zone.Play)
			zone = Zone.Secret;
		base.SetZone(zone, controller);
	}

	/// <summary>
	/// 奥秘唯一性 + 数量上限检查。
	/// </summary>
	public override bool IsSummonable()
	{
		if (Controller == null) return false;

		// 奥秘唯一性：同一 ID 只能存在一个
		foreach (var s in Controller.Secrets)
		{
			if (s is BuffableEntity be && be.CardId == CardId)
				return false;
		}

		// 数量上限
		if (Controller.Secrets.Count >= Game.MAX_SECRETS_ON_PLAY)
			return false;

		return base.IsSummonable();
	}
}
