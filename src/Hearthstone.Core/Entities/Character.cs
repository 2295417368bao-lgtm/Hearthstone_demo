using Serilog;
using HearthstoneServer.Targeting;

namespace HearthstoneServer.Entities;

// ═══════════════════════════════════════════════════
// Character — base for Hero and Minion
// ═══════════════════════════════════════════════════

/// <summary>
/// A character entity — something with health, attack, and the ability to
/// participate in combat. Base class for Hero and Minion.
/// </summary>
public abstract class Character : BuffableEntity
{
	private static readonly ILogger _log = LogConfig.ForType<Character>();

	// ── Core stats ──

	/// <summary>Maximum health (base + buffs, ordered).</summary>
	public int MaxHealth
	{
		get
		{
			int health = GetField("health");

			// 按 Buffs 顺序依次：先加 tag 值，再跑 Mod
			foreach (var buff in Buffs)
			{
				health = buff.AggregateAttr("health", health);
				if (buff is Enchantment ench && ench.MaxHealthMod != null)
					health = ench.MaxHealthMod(this, health);
			}
			foreach (var slot in Slots)
				health = slot.AggregateAttr("health", health);

			return Math.Max(0, health);
		}
		set => SetIntProperty("health", value);
	}

	/// <summary>Damage taken (reduces current health).</summary>
	public int DamageTaken
	{
		get => GetIntProperty("damage");
		set => SetIntProperty("damage", value);
	}

	/// <summary>Current health = MaxHealth - DamageTaken.</summary>
	public virtual int Health => Math.Max(0, MaxHealth - DamageTaken);

	// ── 卡牌数据属性（从 CardData 读取） ──

	/// <summary>
	/// 种族列表（支持多种族）。优先读运行时覆盖，否则从 CardData.Races 读取。
	/// </summary>
	private Race[]? _racesOverride;
	public Race[] Races => _racesOverride ?? CardData?.Races ?? Array.Empty<Race>();

	/// <summary>运行时覆盖种族（用于 MergeCards 等合并场景）。</summary>
	public void SetRaces(Race[] races) => _racesOverride = races;

	/// <summary>稀有度。从 CardData 读取。</summary>
	public Rarity Rarity => CardData?.Rarity ?? Rarity.Invalid;

	/// <summary>Attack power.</summary>
	public int Atk
	{
		get
		{
			int atk = GetField("atk");

			// 按 Buffs 顺序依次：先加 tag 值，再跑 Mod
			foreach (var buff in Buffs)
			{
				atk = buff.AggregateAttr("atk", atk);
				if (buff is Enchantment ench && ench.AtkMod != null)
					atk = ench.AtkMod(this, atk);
			}
			foreach (var slot in Slots)
				atk = slot.AggregateAttr("atk", atk);

			return Math.Max(0, atk);
		}
		set => SetIntProperty("atk", value);
	}

	// ── Boolean properties ──

	public bool Frozen
	{
		get => GetBoolProperty("frozen");
		set
		{
			SetBoolProperty("frozen", value);
			// Also sync int field to prevent GetBoolProperty's
			// GetIntProperty fallback from reading stale values
			SetIntProperty("frozen", value ? 1 : 0);
		}
	}

	public bool Immune
	{
		get => GetBoolProperty("immune");
		set => SetBoolProperty("immune", value);
	}

	public bool CantAttack
	{
		get => GetBoolProperty("cant_attack");
		set => SetBoolProperty("cant_attack", value);
	}

	public bool CantBeDamaged
	{
		get => GetBoolProperty("cant_be_damaged");
		set => SetBoolProperty("cant_be_damaged", value);
	}

	public bool CantBeTargetedByOpponents
	{
		get => GetBoolProperty("cant_be_targeted_by_opponents");
		set => SetBoolProperty("cant_be_targeted_by_opponents", value);
	}

	public bool Taunt
	{
		get => GetBoolProperty("taunt");
		set => SetBoolProperty("taunt", value);
	}

	public bool DivineShield
	{
		get => GetBoolProperty("divine_shield");
		set => SetBoolProperty("divine_shield", value);
	}

	public bool Stealth
	{
		get => GetBoolProperty("stealth");
		set => SetBoolProperty("stealth", value);
	}

	public bool Windfury
	{
		get => GetBoolProperty("windfury");
		set => SetBoolProperty("windfury", value);
	}

	public bool Lifesteal
	{
		get => GetBoolProperty("lifesteal");
		set => SetBoolProperty("lifesteal", value);
	}

	public bool Poisonous
	{
		get => GetBoolProperty("poisonous");
		set => SetBoolProperty("poisonous", value);
	}

	public bool Silenced
	{
		get => GetBoolProperty("silenced");
		set => SetBoolProperty("silenced", value);
	}

	// ── Combat tracking ──

	/// <summary>Number of attacks made this turn.</summary>
	public int NumAttacks { get; set; }

	/// <summary>Damage taken this turn (for Enrage tracking).</summary>
	public int DamagedThisTurn { get; set; }

	/// <summary>Amount healed this turn.</summary>
	public int HealedThisTurn { get; set; }

	/// <summary>How many turns this entity has been in play.</summary>
	public int TurnsInPlay { get; set; }

	// ── Controller ──

	/// <summary>The player controlling this character.</summary>
	public override Player? Controller { get; set; }

	// ── Death detection ──

	/// <summary>True if the character should be destroyed.</summary>
	public bool ToBeDestroyed { get; set; }

	/// <summary>True if the character is dead (health ≤ 0 or marked for destruction).</summary>
	public virtual bool Dead => Health <= 0 || ToBeDestroyed;

	// ── Combat state ──

	/// <summary>The current attack target during combat (null when not attacking).</summary>
	public Character? AttackTarget { get; set; }

	/// <summary>True if this character is currently being attacked.</summary>
	public bool Defending { get; set; }

	/// <summary>True while in an active attack (has a target).</summary>
	public bool Attacking => AttackTarget != null;

	// ── Combat flags ──

	public bool IgnoreTaunt
	{
		get => GetBoolProperty("ignore_taunt");
		set => SetBoolProperty("ignore_taunt", value);
	}

	public bool CannotAttackHeroes
	{
		get => GetBoolProperty("cannot_attack_heroes");
		set => SetBoolProperty("cannot_attack_heroes", value);
	}

	public bool Rush
	{
		get => GetBoolProperty("rush");
		set => SetBoolProperty("rush", value);
	}

	public bool Charge
	{
		get => GetBoolProperty("charge");
		set => SetBoolProperty("charge", value);
	}

	public bool MegaWindfury
	{
		get => GetBoolProperty("mega_windfury");
		set => SetBoolProperty("mega_windfury", value);
	}

	public bool UnlimitedAttacks
	{
		get => GetBoolProperty("unlimited_attacks");
		set => SetBoolProperty("unlimited_attacks", value);
	}

	public override bool HasDeathrattle
	{
		get => GetBoolProperty("has_deathrattle");
		set => SetBoolProperty("has_deathrattle", value);
	}

	// ── Combat computation ──

	/// <summary>Maximum attacks per turn (1/2/4 depending on Windfury/MegaWindfury).</summary>
	public virtual int MaxAttacks
	{
		get
		{
			if (MegaWindfury) return 4;
			if (Windfury) return 2;
			return 1;
		}
	}

	/// <summary>True if the character has used all attacks this turn.</summary>
	public virtual bool Exhausted => UnlimitedAttacks ? false : NumAttacks >= MaxAttacks;

	/// <summary>Whether the character can be attacked. Heroes: not if immune. Overridden by Minion.</summary>
	public virtual bool Attackable => !Immune;

	/// <summary>
	/// True if combat should be interrupted (attacker dies or leaves play mid-combat).
	/// </summary>
	public bool ShouldExitCombat => Attacking && (Dead || Zone != Zone.Play);

	/// <summary>
	/// Get all valid attack targets for this character.
	/// Handles Taunt, Rush, CannotAttackHeroes filtering.
	/// </summary>
	public virtual List<Character> AttackTargets
	{
		get
		{
			if (Controller?.Opponent == null) return new List<Character>();

			// Start with opponent's characters (hero + field minions)
			var targets = new List<Character>();
			if (Controller.Opponent.Hero != null && !CannotAttackHeroes)
				targets.Add(Controller.Opponent.Hero);
			// Add field minions only (heroes are handled separately above)
			targets.AddRange(Controller.Opponent.Field.OfType<Minion>());

			// Rush minions can only attack minions on their first turn
			if (Rush && TurnsInPlay == 0)
			{
				targets.RemoveAll(t => t is Hero);
			}

			// Filter: remove dormant minions
			targets.RemoveAll(t => t is Minion m && m.Dormant);

			// Taunt check: if there are taunts, can only attack them (unless IgnoreTaunt)
			if (!IgnoreTaunt)
			{
				var taunts = targets.Where(t => t.Taunt && t.Attackable).ToList();
				if (taunts.Count > 0)
					return taunts;
			}

			// Final filter: only attackable targets
			return targets.Where(t => t.Attackable).ToList();
		}
	}

	/// <summary>
	/// Full can-attack check including zone, controller, targets.
	/// </summary>
	public virtual bool CanAttack(Character? target = null)
	{
		if (Controller == null) return false;
		if (!Controller.IsCurrentPlayer) return false;
		if (Zone != Zone.Play) return false;
		if (CantAttack) return false;
		if (Atk <= 0) return false;
		if (Exhausted) return false;
		if (Frozen) return false;
		var targets = AttackTargets;
		if (targets.Count == 0) return false;
		if (target != null && !targets.Contains(target)) return false;
		return true;
	}

	/// <summary>
	/// Initiate an attack against the target.
	/// </summary>
	public void Attack(Character target)
	{
		if (!CanAttack(target))
		{
			_log.Warning("{Attacker} cannot attack {Target}", EntityId, target.EntityId);
			return;
		}
		Controller!.Game!.Attack(this, target);
	}

	// ── Damage / Heal ──

	/// <summary>
	/// Apply damage to this character. Returns the actual damage dealt.
	/// </summary>
	public virtual int Hit(int amount)
	{
		if (amount <= 0) return 0;
		if (CantBeDamaged || Immune) return 0;

		if (DivineShield)
		{
			DivineShield = false;
			_log.Debug("{Entity} Divine Shield absorbed {Amount} damage", EntityId, amount);
			return 0;
		}

		DamageTaken += amount;
		DamagedThisTurn += amount;
		_log.Debug("{Entity} takes {Amount} damage (health now {Health})", EntityId, amount, Health);
		return amount;
	}

	/// <summary>
	/// Heal this character by amount. Cannot exceed max health.
	/// </summary>
	public virtual int Heal(int amount)
	{
		if (amount <= 0) return 0;
		int actual = Math.Min(amount, DamageTaken);
		DamageTaken -= actual;
		HealedThisTurn += actual;
		if (actual > 0)
			_log.Debug("{Entity} heals for {Amount} (health now {Health})", EntityId, actual, Health);
		return actual;
	}

	/// <summary>Set current health to a specific amount (e.g. Equality).</summary>
	public void SetCurrentHealth(int amount)
	{
		DamageTaken = Math.Max(0, MaxHealth - amount);
	}

	// ── Dump ──

	public override Dictionary<string, object> Dump()
	{
		var data = base.Dump();
		data["atk"] = Atk;
		data["health"] = Health;
		data["max_health"] = MaxHealth;
		data["damage"] = DamageTaken;
		if (Frozen) data["frozen"] = true;
		if (Immune) data["immune"] = true;
		if (Taunt) data["taunt"] = true;
		if (DivineShield) data["divine_shield"] = true;
		return data;
	}
}
