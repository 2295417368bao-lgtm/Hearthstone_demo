using Serilog;

namespace HearthstoneServer.Entities;

// ═══════════════════════════════════════════════════
// Location — a location card placed on the battlefield
// ═══════════════════════════════════════════════════

/// <summary>
/// 地标实体。地标放置在战场上占一个格子，具有耐久度和冷却机制。
/// 每次使用消耗 1 耐久并进入 1 回合冷却，耐久归零后销毁。
/// 地标不可被攻击，但可以被效果（如战吼）指定为目标并摧毁。
/// </summary>
public class Location : BuffableEntity, IDestroyable
{
	private static readonly ILogger _log = LogConfig.ForType<Location>();

	public Location()
	{
		Type = CardType.Location;
	}

	/// <summary>最大耐久度（从 CardData.Health 读取）。</summary>
	public int MaxDurability
	{
		get => GetIntProperty("durability");
		set => SetIntProperty("durability", value);
	}

	/// <summary>已消耗耐久。</summary>
	public int DamageTaken
	{
		get => GetIntProperty("damage");
		set => SetIntProperty("damage", value);
	}

	/// <summary>当前耐久 = MaxDurability - DamageTaken。</summary>
	public int Durability => Math.Max(0, MaxDurability - DamageTaken);

	/// <summary>剩余冷却回合数（0 = 可使用）。</summary>
	public int Cooldown
	{
		get => GetIntProperty("location_cooldown");
		set => SetIntProperty("location_cooldown", value);
	}

	/// <summary>地标是否可使用（有耐久且无冷却）。</summary>
	public bool IsUsable => Durability > 0 && Cooldown == 0;

	/// <summary>法力消耗。</summary>
	public override int Cost
	{
		get => GetIntProperty("cost");
		set => SetIntProperty("cost", value);
	}

	/// <summary>
	/// 使用地标：消耗 1 耐久，进入 1 回合冷却。
	/// </summary>
	public void Use()
	{
		DamageTaken++;
		Cooldown = 1;
		_log.Debug("Location {Location} used (Durability={Dur}, Cooldown={CD})",
			EntityId, Durability, Cooldown);
	}

	/// <summary>
	/// 回合开始时刷新冷却。
	/// </summary>
	public void TickCooldown()
	{
		if (Cooldown > 0)
		{
			Cooldown = Math.Max(0, Cooldown - 1);
			_log.Debug("Location {Location} cooldown ticked (Cooldown={CD})",
				EntityId, Cooldown);
		}
	}

	/// <summary>耐久归零则销毁。</summary>
	public bool Dead => Durability <= 0;

	/// <summary>地标的控制者。</summary>
	public override Player? Controller { get; set; }

	/// <summary>
	/// 打出地标：放入战场。
	/// </summary>
	public override void Play(Character? target = null, int index = -1)
	{
		base.Play(target, index);
		var player = Controller;
		if (player == null) return;

		// 放入战场
		player.Summon(this, index);
		_log.Debug("Location {Location} placed (Durability={Dur})",
			EntityId, Durability);
	}

	// ── IDestroyable ──

	/// <summary>
	/// Execute death processing: move to graveyard, publish event.
	/// </summary>
	public void OnDeath(Game game)
	{
		Controller?.Field.Remove(this);
		Controller?.Graveyard.Add(this);
		Zone = Zone.Graveyard;
		_log.Debug("Location {EntityId} destroyed", EntityId);

		game.EventBus.Publish(new Events.DeathEvent(this, this), Events.MiscPhase.On);
		game.EventBus.UnsubscribeAll(this);
	}

	/// <summary>
	/// 销毁地标：移至墓地。
	/// </summary>
	public void Destroy()
	{
		Controller?.Field.Remove(this);
		SetZone(Zone.Graveyard);
		_log.Debug("Location {Location} destroyed", EntityId);
	}

	/// <summary>
	/// Override SetZone: 离开 Play 时从 Field 移除。
	/// </summary>
	public override void SetZone(Zone zone, Player? controller = null)
	{
		if (Zone == Zone.Play && zone != Zone.Play)
		{
			Controller?.Field.Remove(this);
		}
		base.SetZone(zone, controller);
	}

	public override Dictionary<string, object> Dump()
	{
		var data = base.Dump();
		data["durability"] = Durability;
		data["max_durability"] = MaxDurability;
		data["cooldown"] = Cooldown;
		data["is_usable"] = IsUsable;
		return data;
	}
}
