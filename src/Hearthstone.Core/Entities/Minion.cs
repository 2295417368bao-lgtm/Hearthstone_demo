using Serilog;

namespace HearthstoneServer.Entities;

// ═══════════════════════════════════════════════════
// Minion — a minion on the battlefield
// ═══════════════════════════════════════════════════

/// <summary>
/// A minion entity. Minions have keywords, can be silenced, and have
/// board-position-dependent behaviors (adjacent minions, asleep).
/// </summary>
public class Minion : Character, IDestroyable
{
	private static readonly ILogger _log = LogConfig.ForType<Minion>();

	public Minion()
	{
		Type = CardType.Minion;
	}

	// ── Minion-specific keywords ──

	public bool Reborn
	{
		get => GetBoolProperty("reborn");
		set => SetBoolProperty("reborn", value);
	}

	public bool Dormant
	{
		get => GetBoolProperty("dormant");
		set => SetBoolProperty("dormant", value);
	}

	public int DormantTurns
	{
		get => GetIntProperty("dormant_turns");
		set => SetIntProperty("dormant_turns", value);
	}

	public bool CantBeTargetedByAbilities
	{
		get => GetBoolProperty("cant_be_targeted_by_abilities");
		set => SetBoolProperty("cant_be_targeted_by_abilities", value);
	}

	public bool CantBeTargetedByHeroPowers
	{
		get => GetBoolProperty("cant_be_targeted_by_hero_powers");
		set => SetBoolProperty("cant_be_targeted_by_hero_powers", value);
	}

	public bool HasInspire
	{
		get => GetBoolProperty("has_inspire");
		set => SetBoolProperty("has_inspire", value);
	}

	public int SpellPower
	{
		get => GetIntProperty("spellpower");
		set => SetIntProperty("spellpower", value);
	}

	public int HeroPowerDamage
	{
		get => GetIntProperty("heropower_damage");
		set => SetIntProperty("heropower_damage", value);
	}

	/// <summary>
	/// Magnetic keyword — when played to the left of a Mech, can merge with it.
	/// The actual merge logic is handled via the card script 'magnetic' hook.
	/// </summary>
	public bool HasMagnetic
	{
		get => GetBoolProperty("has_magnetic");
		set => SetBoolProperty("has_magnetic", value);
	}

	// ── Titan ──

	/// <summary>是否是泰坦随从（基于实体 GameTag）。</summary>
	public bool IsTitan => CardData?.HasTag(GameTag.Titan) == true;

	/// <summary>泰坦技能 1 是否已使用。</summary>
	public bool TitanAbility1Used
	{
		get => GetBoolProperty("titan_ability_used_1");
		set => SetBoolProperty("titan_ability_used_1", value);
	}

	/// <summary>泰坦技能 2 是否已使用。</summary>
	public bool TitanAbility2Used
	{
		get => GetBoolProperty("titan_ability_used_2");
		set => SetBoolProperty("titan_ability_used_2", value);
	}

	/// <summary>泰坦技能 3 是否已使用。</summary>
	public bool TitanAbility3Used
	{
		get => GetBoolProperty("titan_ability_used_3");
		set => SetBoolProperty("titan_ability_used_3", value);
	}

	/// <summary>是否仍有未使用的泰坦技能。沉默后返回 false。</summary>
	public bool HasUnusedTitanAbility => IsTitan && !Silenced
		&& !(TitanAbility1Used && TitanAbility2Used && TitanAbility3Used);

	/// <summary>
	/// 泰坦 3 个技能的卡牌 ID（从 entourage 读取）。
	/// </summary>
	public string[] TitanAbilityCardIds
	{
		get
		{
			if (!IsTitan || CardId == null) return System.Array.Empty<string>();
			var script = Dsl.CardScriptRegistry.Instance.GetScript(CardId);
			return script?.Entourage ?? System.Array.Empty<string>();
		}
	}

	// ── Board position ──

	/// <summary>The minion's zone position (1-indexed).</summary>
	public int ZonePosition { get; set; }

	// ── State ──

	/// <summary>True if the minion was just summoned this turn (can't attack yet unless Charge/Rush).</summary>
	public bool Asleep => TurnsInPlay == 0 && !Charge && !Rush;

	/// <summary>True if the minion is enraged (damaged).</summary>
	public bool Enraged => DamagedThisTurn > 0 || DamageTaken > 0;

	/// <summary>
	/// Override UpdateScripts to include Enrage auras when damaged.
	/// </summary>
	public override List<Auras.Refresh> UpdateScripts
	{
		get
		{
			var scripts = base.UpdateScripts;
			if (Enraged)
			{
				var script = Dsl.CardScriptRegistry.Instance.GetScript(CardId ?? "");
				if (script != null && script.Enrage.Length > 0)
					scripts.AddRange(script.Enrage);
			}
			return scripts;
		}
	}

	// ── 邻位随从 ──

	/// <summary>左侧邻位随从（按 ZonePosition 查找）。</summary>
	public Minion? LeftMinion
	{
		get
		{
			if (Controller == null || ZonePosition <= 1) return null;
			return Controller.Field
				.OfType<Minion>()
				.FirstOrDefault(m => m.ZonePosition == ZonePosition - 1);
		}
	}

	/// <summary>右侧邻位随从（按 ZonePosition 查找）。</summary>
	public Minion? RightMinion
	{
		get
		{
			if (Controller == null) return null;
			return Controller.Field
				.OfType<Minion>()
				.FirstOrDefault(m => m.ZonePosition == ZonePosition + 1);
		}
	}

	/// <summary>相邻随从列表。</summary>
	public List<Minion> AdjacentMinions
	{
		get
		{
			var result = new List<Minion>(2);
			var left = LeftMinion;
			var right = RightMinion;
			if (left != null) result.Add(left);
			if (right != null) result.Add(right);
			return result;
		}
	}

	// ── Play override ──

	/// <summary>
	/// Play this minion — 轻量钩子。SequenceRunner 处理费用、入场、战吼。
	/// 仅保留 Zone 变换逻辑以供直接调用场景（ForcePlay/测试）使用。
	/// </summary>
	public override void Play(Character? target = null, int index = -1)
	{
		base.Play(target, index);
		SetZone(Zone.Play);

		// 分配 ZonePosition
		if (Controller != null)
			ZonePosition = Controller.Field.IndexOf(this) + 1;
	}

	// ── Attackable ──

	/// <summary>True if the minion can be attacked (not stealthed, not dormant, not immune).</summary>
	public override bool Attackable => !Stealth && !Dormant && base.Attackable;

	// ── Combat ──

	public override bool CanAttack(Character? target = null)
	{
		if (HasUnusedTitanAbility) return false;
		if (Asleep) return false;
		if (Dormant) return false;
		return base.CanAttack(target);
	}

	// ── Silence ──

	/// <summary>
	/// Silence the minion: clear buffs, remove keywords, disable scripts.
	/// </summary>
	public void Silence()
	{
		_log.Debug("Silencing {Entity}", EntityId);
		ClearBuffs();
		Taunt = false;
		DivineShield = false;
		Stealth = false;
		Windfury = false;
		Frozen = false;
		Poisonous = false;
		Lifesteal = false;
		Charge = false;
		Rush = false;
		HasDeathrattle = false;
		HasInspire = false;
		Reborn = false;
		Silenced = true;

		// 沉默后清除 EventBus 订阅
		Controller?.Game?.EventBus.UnsubscribeAll(this);
	}

	// ── Bounce ──

	/// <summary>Return the minion to its owner's hand.</summary>
	public void Bounce()
	{
		_log.Debug("Bouncing {Entity} back to hand", EntityId);
		// 回手时重置泰坦技能状态
		if (IsTitan)
		{
			TitanAbility1Used = false;
			TitanAbility2Used = false;
			TitanAbility3Used = false;
		}
		Zone = Zone.Hand;
	}

	// ── Summonable check ──

	/// <summary>True if the minion can be placed on the board.</summary>
	public override bool IsSummonable()
	{
		if (Controller == null) return false;
		return Controller.Field.Count < 7; // MAX_MINIONS_ON_FIELD
	}

	// ── IDestroyable ──

	/// <summary>
	/// Execute death processing: move to graveyard, publish event, trigger deathrattle.
	/// </summary>
	public void OnDeath(Game game)
	{
		// Save field position for deathrattle summon positioning
		int fieldIdx = Controller?.Field.IndexOf(this) ?? -1;
		SetIntProperty("death_field_index", fieldIdx);
		SummonIndex = 0;

		// 移入墓地
		Controller?.Field.Remove(this);
		Controller?.Graveyard.Add(this);
		if (Controller != null)
			Controller.MinionsKilledThisTurn++;
		Zone = Zone.Graveyard;
		_log.Debug("Minion {EntityId} dies", EntityId);

		// 残骸系统
		Controller?.GainCorpses(1);

		// Publish DeathEvent
		game.EventBus.Publish(new Events.DeathEvent(this, this), Events.MiscPhase.On);

		// 触发亡语脚本
		var script = game.ScriptRegistry.GetScript(CardId ?? "");
		if (script?.Deathrattle.Length > 0)
		{
			_log.Debug("Triggering deathrattle for {EntityId}", EntityId);
			game.ActionBlock(this, script.Deathrattle.ToList(), BlockType.Trigger);
		}

		// 秘密抉择亡语
		int secretIdx = GetIntProperty("secret_deathrattle");
		if (secretIdx > 0 && script?.SecretDeathrattles.Length >= secretIdx)
		{
			_log.Debug("Triggering secret deathrattle index {Idx} for {EntityId}",
				secretIdx, EntityId);
			game.ActionBlock(this, script.SecretDeathrattles[secretIdx - 1].ToList(), BlockType.Trigger);
		}

		// 死亡后清除 EventBus 订阅
		game.EventBus.UnsubscribeAll(this);
	}

	// ── Dump ──

	public override Dictionary<string, object> Dump()
	{
		var data = base.Dump();
		data["zone_position"] = ZonePosition;
		if (Charge) data["charge"] = true;
		if (Stealth) data["stealth"] = true;
		if (Poisonous) data["poisonous"] = true;
		if (Reborn) data["reborn"] = true;
		if (Dormant) data["dormant"] = true;
		if (Silenced) data["silenced"] = true;
		if (SpellPower > 0) data["spellpower"] = SpellPower;
		if (IsTitan)
		{
			data["is_titan"] = true;
			data["titan_ability_1_used"] = TitanAbility1Used;
			data["titan_ability_2_used"] = TitanAbility2Used;
			data["titan_ability_3_used"] = TitanAbility3Used;
		}
		return data;
	}
}
