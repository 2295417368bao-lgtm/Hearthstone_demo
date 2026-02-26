using HearthstoneServer.Dsl;
using Serilog;

namespace HearthstoneServer.Entities;

// ═══════════════════════════════════════════════════
// HeroPower — a hero's special ability
// ═══════════════════════════════════════════════════

/// <summary>
/// A hero power entity. Hero powers can be activated a limited number
/// of times per turn (normally once).
/// </summary>
public class HeroPower : BuffableEntity
{
	private static readonly ILogger _log = LogConfig.ForType<HeroPower>();

	public HeroPower()
	{
		Type = CardType.HeroPower;
	}

	/// <summary>Hero powers are used from the Play zone (not from Hand).</summary>
	public override Zone PlayableZone => Zone.Play;

	/// <summary>Number of activations used this turn.</summary>
	public int ActivationsThisTurn { get; set; }

	/// <summary>Extra activations granted this turn (e.g. by Coldarra Drake).</summary>
	public int AdditionalActivationsThisTurn { get; set; }

	/// <summary>Permanent extra activations (e.g. by Coldarra Drake aura). -1 = unlimited.</summary>
	public int AdditionalActivations
	{
		get => GetIntProperty("additional_activations");
		set => SetIntProperty("additional_activations", value);
	}

	/// <summary>Whether the hero power is disabled.</summary>
	public bool HeroPowerDisabled
	{
		get => GetBoolProperty("heropower_disabled");
		set => SetBoolProperty("heropower_disabled", value);
	}

	/// <summary>The controller of this hero power.</summary>
	public override Player? Controller { get; set; }

	/// <summary>Mana cost to use the hero power.</summary>
	public override int Cost
	{
		get => GetIntProperty("cost");
		set => SetIntProperty("cost", value);
	}

	// ── Exhaustion ──

	/// <summary>
	/// 英雄技能是否已耗尽（本回合不能再用）。
	/// </summary>
	public bool Exhausted
	{
		get
		{
			if (HeroPowerDisabled) return true;
			if (AdditionalActivations == -1) return false; // unlimited
			return ActivationsThisTurn >= (1 + AdditionalActivations + AdditionalActivationsThisTurn);
		}
	}

	// ── Usability ──

	/// <summary>
	/// 英雄技能是否可以使用：未耗尽 + 有足够法力。
	/// </summary>
	public bool IsUsable
	{
		get
		{
			if (Exhausted) return false;
			if (Controller == null) return false;
			return Controller.Mana >= Cost;
		}
	}

	// ── Use ──

	/// <summary>
	/// 使用英雄技能：支付费用、执行脚本、增加激活计数。
	/// </summary>
	public void Use(Character? target = null)
	{
		if (!IsUsable)
		{
			_log.Warning("Hero power {EntityId} is not usable", EntityId);
			return;
		}
		if (Controller == null) return;

		_log.Debug("{Player} uses hero power {HeroPower} on {Target}",
			Controller.EntityId, EntityId, target?.EntityId);

		// 支付费用
		Controller.PayCost(Cost);

		// 增加激活计数
		ActivationsThisTurn++;

		// 触发脚本动作
		var script = CardScriptRegistry.Instance.GetScript(CardId ?? "");
		if (script != null && script.Play.Length > 0)
		{
			Controller.Game?.ActionBlock(this, script.Play.ToList(),
				BlockType.Power, target: target);
		}

		Controller.TimesHeroPowerUsedThisGame++;
	}

	// ── Damage / Heal bonuses ──

	/// <summary>
	/// 英雄技能伤害（应用英雄技能伤害加成）。
	/// </summary>
	public int GetDamage(int amount)
	{
		if (Controller == null) return amount;
		return Controller.GetHeroPowerDamage(amount);
	}

	/// <summary>
	/// 英雄技能治疗量（应用治疗加成）。
	/// </summary>
	public int GetHealAmount(int amount)
	{
		// Hero power heal bonus (currently passthrough; add Player method when needed)
		return amount;
	}

	public override Dictionary<string, object> Dump()
	{
		var data = base.Dump();
		data["cost"] = Cost;
		data["activations_this_turn"] = ActivationsThisTurn;
		data["is_usable"] = IsUsable;
		return data;
	}
}
