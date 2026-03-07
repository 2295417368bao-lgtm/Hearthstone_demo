using HearthstoneServer.Events;
using Serilog;

namespace HearthstoneServer.Entities;

/// <summary>
/// 实体基类。
/// 所有游戏对象（Game、Player、Card 等）的根类型。
/// </summary>
public abstract class BaseEntity
{
	private static readonly ILogger _log = LogConfig.ForType<BaseEntity>();
	/// <summary>唯一实例 ID（由 GameManager 分配）</summary>
	public int EntityId { get; set; }

	/// <summary>UUID 用于比较同一性</summary>
	public Guid Uuid { get; } = Guid.NewGuid();

	/// <summary>卡牌类型</summary>
	public virtual CardType Type { get; set; } = CardType.Invalid;

	/// <summary>出牌计数器（用于排序在场序）</summary>
	public int PlayCounter { get; set; }

	/// <summary>标签管理器（惰性初始化，永不为 null）</summary>
	private Managers.Manager? _tags;
	public Managers.Manager Tags
	{
		get => _tags ??= new Managers.CardManager(this);
		protected set => _tags = value;
	}

	/// <summary>
	/// 确保 Tags 已初始化。现在 Tags getter 已自动惰性初始化，此方法保留兼容。
	/// </summary>
	public void EnsureTags()
	{
		_ = Tags; // trigger lazy init
	}

	/// <summary>是否忽略脚本回调</summary>
	public bool IgnoreScripts { get; set; }

	/// <summary>当前事件参数（在事件回调期间有效）</summary>
	public object[]? EventArgs { get; set; }

	/// <summary>
	/// 当前 ActionBlock 的所有目标实体（在 ActionBlock 执行期间由 Game 设置）。
	/// 支持多目标卡牌（如惬意的沃金：选择两个随从交换属性）。
	/// 用于 S.TARGET / S.TARGET_2 selector 解析。
	/// </summary>
	public List<BaseEntity> CurrentTargets { get; set; } = new();

	/// <summary>
	/// 便利访问器：第一个目标（向后兼容）。
	/// </summary>
	public BaseEntity? CurrentTarget
	{
		get => CurrentTargets.Count > 0 ? CurrentTargets[0] : null;
		set
		{
			CurrentTargets.Clear();
			if (value != null) CurrentTargets.Add(value);
		}
	}

	/// <summary>事件上下文：事件发起者（攻击者、施法者、打出的随从等）。由 BusEvent.When 在触发 action 前设置。</summary>
	public BaseEntity? EventSource { get; set; }

	/// <summary>
	/// 多步发现选中的卡牌列表（由 DiscoverMode.Pick 追加）。
	/// 用于 S.PICKED / S.PICKED_1 / S.PICKED_2 选择器。
	/// </summary>
	public List<BaseEntity> PickedCards { get; set; } = new();

	/// <summary>事件上下文：事件目标/主体（防御者、死亡实体等）。由 BusEvent.When 在触发 action 前设置。</summary>
	public BaseEntity? EventTarget { get; set; }

	/// <summary>事件上下文：当前事件的主数值（伤害、治疗、护甲等）。由 BusEvent.When 在触发 action 前设置。</summary>
	public int? EventAmount { get; set; }

	/// <summary>事件上下文：当前 PreDamageEvent（仅在 PreDamageEvent 处理期间设置）。</summary>
	public PreDamageEvent? ActivePreDamageEvent { get; set; }

	/// <summary>事件上下文：当前 WeaponEquipEvent（仅在 WeaponEquipEvent 处理期间设置）。</summary>
	public WeaponEquipEvent? ActiveWeaponEquipEvent { get; set; }

	/// <summary>事件上下文：当前 WeaponEquipEvent 所处阶段。</summary>
	public WeaponPhase? ActiveWeaponPhase { get; set; }

	/// <summary>事件上下文：当前 WeaponDurabilityLossEvent（仅在武器耐久损失处理期间设置）。</summary>
	public WeaponDurabilityLossEvent? ActiveWeaponDurabilityLossEvent { get; set; }

	/// <summary>事件上下文：当前 WeaponDurabilityLossEvent 所处阶段。</summary>
	public WeaponDurabilityLossPhase? ActiveWeaponDurabilityLossPhase { get; set; }

	/// <summary>最近一次对该实体造成伤害的来源。用于死亡事件还原真正 cause。</summary>
	public BaseEntity? LastDamageSource { get; set; }

	/// <summary>显式摧毁等效果预先写入的死亡原因。死亡事件发布后清空。</summary>
	public BaseEntity? PendingDeathCause { get; set; }

	/// <summary>关联的数据层卡牌定义（可为 null，如 Game / Player 实体）</summary>
	public virtual LoadCardIntoDbf.Card? Data { get; set; }

	// ── 属性判断 ──

	/// <summary>
	/// 是否为一张真实的卡牌（而非 Game/Player）
	/// </summary>
	public bool IsCard => Type > CardType.Player;

	/// <summary>
	/// 所属的游戏实体引用。
	/// </summary>
	public virtual Game? Game { get; set; }

	// ── 伤害 / 治疗计算 ──

	/// <summary>
	/// 计算对目标造成的最终伤害量。子类可重写以应用免疫 / 休眠等。
	/// </summary>
	public virtual int GetDamage(int amount, BaseEntity target)
	{
		if (target is BuffableEntity be)
		{
			if (be.GetBoolProperty(GameTag.Dormant))
			{
				_log.Debug("{Target} is dormant, ignoring {Amount} damage from {Source}",
					target.EntityId, amount, EntityId);
				return 0;
			}
			if (be.GetBoolProperty(GameTag.Immune))
			{
				_log.Debug("{Target} is immune to {Amount} damage from {Source}",
					target.EntityId, amount, EntityId);
				return 0;
			}
		}
		return amount;
	}

	/// <summary>
	/// 计算对目标的最终治疗量。
	/// </summary>
	public virtual int GetHeal(int amount, BaseEntity target) => amount;

	// ── 调试 ──

	public virtual Dictionary<string, object> Dump()
	{
		return new()
		{
			["entity_id"] = EntityId,
			["type"] = (int)Type,
		};
	}
}
