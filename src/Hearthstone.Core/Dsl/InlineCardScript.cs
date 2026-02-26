using HearthstoneServer.Auras;
using HearthstoneServer.Entities;
using HearthstoneServer.Events;
using HearthstoneServer.Targeting;

namespace HearthstoneServer.Dsl;

// ═══════════════════════════════════════════════════
// Inline Card Script — 通用内联脚本实现
// 由 CardBuilder 自动创建，支持全部 ICardScript 属性
// ═══════════════════════════════════════════════════

/// <summary>
/// 通用内联 ICardScript 实现。所有属性通过构造函数或 init 设置。
/// 无需为每张卡写单独 class —— CardBuilder 自动创建此实例。
/// </summary>
public class InlineCardScript : ICardScript
{
	public Action[] Play { get; init; } = Array.Empty<Action>();
	public Action[] Battlecry { get; init; } = Array.Empty<Action>();
	public System.Action<BuffableEntity, Game>? OnActivate { get; init; }
	public Action[] Deathrattle { get; init; } = Array.Empty<Action>();
	public Action[] Combo { get; init; } = Array.Empty<Action>();
	public Action[] Inspire { get; init; } = Array.Empty<Action>();
	public Refresh[] Update { get; init; } = Array.Empty<Refresh>();
	public TargetRequirement TargetReq { get; init; } = TargetRequirement.None;
	public string[] ChooseCards { get; init; } = Array.Empty<string>();
	public string[] Entourage { get; init; } = Array.Empty<string>();
	public Refresh[] Enrage { get; init; } = Array.Empty<Refresh>();

	public LazyNum? CostMod { get; init; }
	public Dictionary<GameTag, object>? Tags { get; init; }
	public Func<BaseEntity, int, int>? MaxHealthMod { get; init; }
	public Func<BaseEntity, int, int>? AtkMod { get; init; }
	public Func<BaseEntity, int, int>? CostModFunc { get; init; }
	public Func<BaseEntity, int, int>? DurabilityMod { get; init; }
	public Func<BaseEntity, int, int>? ArmorMod { get; init; }

	// ── New hooks ──
	public Action[] Activate { get; init; } = Array.Empty<Action>();
	public Action[] Location { get; init; } = Array.Empty<Action>();
	public Action[] Outcast { get; init; } = Array.Empty<Action>();
	public Action[] Awaken { get; init; } = Array.Empty<Action>();
	public Action[] Reward { get; init; } = Array.Empty<Action>();
	public Action[] AddProgress { get; init; } = Array.Empty<Action>();
	public Action[] Discard { get; init; } = Array.Empty<Action>();
	public Action[][] SecretDeathrattles { get; init; } = Array.Empty<Action[]>();
	public Action[] Overkill { get; init; } = Array.Empty<Action>();
	public Action[] Draw { get; init; } = Array.Empty<Action>();
	public Refresh[] HandUpdate { get; init; } = Array.Empty<Refresh>();
	public Refresh[] DeckUpdate { get; init; } = Array.Empty<Refresh>();
	public int ProgressTotal { get; init; }
	public int DormantTurns { get; init; }
}
