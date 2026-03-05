using Serilog;
using HearthstoneServer.Entities;
using HearthstoneServer.Events;

namespace HearthstoneServer.Dsl;

// ═══════════════════════════════════════════════════
// Concrete Action Implementations
// ═══════════════════════════════════════════════════

// ─── P0: Core combat & resource actions ──────────

/// <summary>
/// Hit character targets by amount.
/// Core damage pipeline entry point.
/// </summary>
public class DealDamage : TargetedAction
{
	private bool _lifesteal;

	public DealDamage(object target, object amount) : base(target, amount) { }

	/// <summary>标记为吸血：实际造成的伤害恢复给友方英雄。</summary>
	public DealDamage Lifesteal() { _lifesteal = true; return this; }

	public override object? Do(BaseEntity source, BaseEntity target, params object[] args)
	{
		int amount = ActionHelper.ResolveInt(args, 0, source);
		if (amount <= 0) return 0;

		if (target is Character ch)
		{
			// Publish PreDamageEvent — handlers may redirect/cancel
			var game = (source as BuffableEntity)?.Controller?.Game
				?? ch.Controller?.Game;
			if (game != null)
			{
				var preEvt = new PreDamageEvent(source, ch, amount);
				game.EventBus.Publish(preEvt, MiscPhase.On);
				if (preEvt.Redirected) return 0;
			}

			int dealt = ch.Hit(amount);
			if (dealt > 0)
			{
				game?.EventBus.Publish(new DamageEvent(source, ch, dealt), MiscPhase.On);

				// Lifesteal: 恢复实际造成的伤害给友方英雄
				if (_lifesteal || (source is BuffableEntity srcLs && srcLs.GetBoolProperty("lifesteal")))
				{
					var hero = (source as BuffableEntity)?.Controller?.Hero;
					if (hero != null)
					{
						hero.Heal(dealt);
						Log.Information("Lifesteal: {Source} heals hero for {Amount}", source, dealt);
					}
				}

				// Freeze-on-damage: if the source has Freeze tag, freeze the target
				if (source is BuffableEntity srcBe && srcBe.GetBoolProperty("freeze"))
				{
					ch.SetBoolProperty("frozen", true);
					Log.Information("{Source} freezes {Target}", source, ch);
				}
			}

			// .Then() callback handling
			if (dealt > 0 && Callback.Length > 0)
			{
				if (CallbackCondition != null)
				{
					// 条件模式：设置 CurrentTarget → 检查条件 → 执行一次
					// 用途：.IfThen(KILLED, new Summon(...))
					source.CurrentTarget = ch;
					try
					{
						if (CallbackCondition.Check(source))
						{
							foreach (var cb in Callback)
							{
								if (cb is TargetedAction ta) ta.Trigger(source);
							}
						}
					}
					finally { source.CurrentTarget = null; }
				}
				else
				{
					// 无条件模式：执行一次（用 action * N 控制重复次数）
					// 用途：.Then(new Summon(...) * 3)  → 召唤 3 个
					foreach (var cb in Callback)
					{
						if (cb is TargetedAction ta) ta.Trigger(source);
					}
				}
			}
			return dealt;
		}
		return 0;
	}

	public override string ToString() => $"Hit({Args[0]}, {Args[1]})";
}

/// <summary>
/// Heal character targets by amount.
/// </summary>
public class RestoreHealth : TargetedAction
{
	public RestoreHealth(params object[] args) : base(args) { }

	public override object? Do(BaseEntity source, BaseEntity target, params object[] args)
	{
		int amount = ActionHelper.ResolveInt(args, 0, source);
		if (amount <= 0) return 0;

		if (target is Character ch)
		{
			int heal = Math.Min(amount, ch.DamageTaken);
			if (heal <= 0) return 0;
			ch.DamageTaken -= heal;
			ch.HealedThisTurn += heal;
			Log.Information("{Source} heals {Target} for {Amount}", source, target, heal);
			var game = (source as BuffableEntity)?.Controller?.Game
				?? ch.Controller?.Game;
			game?.EventBus.Publish(new HealEvent(source, ch, heal), MiscPhase.On);
			return heal;
		}
		return 0;
	}

	public override string ToString() => $"Heal({Args[0]}, {Args[1]})";
}

/// <summary>
/// Fully heal character targets to max health.
/// </summary>
public class FullRestore : TargetedAction
{
	public FullRestore(object target) : base(target) { }

	public override object? Do(BaseEntity source, BaseEntity target, params object[] args)
	{
		if (target is Character ch)
		{
			ch.DamageTaken = 0;
			Log.Information("{Source} fully heals {Target}", source, target);
		}
		return null;
	}
}

/// <summary>
/// Buff entity target with an enchantment.
/// </summary>
public class Enchant : TargetedAction
{
	private readonly LazyNum? _atkValue;
	private readonly LazyNum? _healthValue;

	/// <summary>使用预注册的附魔 ID。</summary>
	public Enchant(object target, object buffId) : base(target, buffId) { }

	/// <summary>
	/// 动态附魔：在运行时计算 atk/health 数值。
	/// <para>用法：new Enchant(S.SELF, health: LazyNum.HEALTH)</para>
	/// </summary>
	public Enchant(object target, LazyNum? atk = null, LazyNum? health = null) : base(target)
	{
		_atkValue = atk;
		_healthValue = health;
	}

	public override object? Do(BaseEntity source, BaseEntity target, params object[] args)
	{
		if (target is not BuffableEntity beTarget) return null;

		// 动态模式：直接用 LazyNum 计算数值
		if (_atkValue != null || _healthValue != null)
		{
			var ench = new Enchantment
			{
				Source = source as BuffableEntity,
				Owner = beTarget,
				BuffId = $"dynamic_{source.EntityId}_{target.EntityId}"
			};

			if (_atkValue != null)
				ench.SetIntProperty("atk", _atkValue.Value(source));
			if (_healthValue != null)
				ench.SetIntProperty("health", _healthValue.Value(source));

			source.Game?.RegisterEntity(ench);
			ench.Apply(beTarget);
			return ench;
		}

		// 预注册模式：通过 buffId 查找
		var buffId = Args.Length > 1 ? Args[1] as string : null;
		if (buffId == null) return null;

		Log.Information("{Source} buffs {Target} with {BuffId}", source, target, buffId);

		// Look up the enchant script's Tags
		var script = CardScriptRegistry.Instance.GetScript(buffId);

		// Create the Enchantment entity
		var ench2 = new Enchantment
		{
			Source = source as BuffableEntity,
			Owner = beTarget,
			BuffId = buffId
		};

		// Apply tag-based attribute modifications from the enchant script
		if (script?.Tags != null)
		{
			foreach (var (tag, value) in script.Tags)
			{
				var propName = Managers.CardManager.GetPropertyName(tag);
				if (propName == null) continue;

				if (value is BuffValue bv)
					ench2.SetIntProperty(propName, bv.Resolve(source as BuffableEntity));
				else if (value is int iv)
					ench2.SetIntProperty(propName, iv);
				else if (value is bool bl)
					ench2.SetBoolProperty(propName, bl);
			}
		}

		// Copy function-based modifiers from the script
		if (script?.CostModFunc != null)
			ench2.CostModFunc = script.CostModFunc;
		if (script?.AtkMod != null)
			ench2.AtkMod = script.AtkMod;
		if (script?.MaxHealthMod != null)
			ench2.MaxHealthMod = script.MaxHealthMod;
		if (script?.DurabilityMod != null)
			ench2.DurabilityMod = script.DurabilityMod;
		if (script?.ArmorMod != null)
			ench2.ArmorMod = script.ArmorMod;

		// Register and apply
		source.Game?.RegisterEntity(ench2);

		// 自动读取持续回合数（从 buff 卡牌数据的 QuestProgressTotal tag）
		if (source.Game?.CardDb != null && buffId != null
			&& source.Game.CardDb.TryGetValue(buffId, out var buffCardData))
		{
			var duration = buffCardData.GetTag(GameTag.QuestProgressTotal);
			if (duration > 0)
				ench2.Duration = duration;
		}

		ench2.Apply(beTarget);

		// Activate event-driven buff effects (e.g. "destroy at end of turn")
		if (script?.OnActivate != null && source is BuffableEntity beSrc)
		{
			script.OnActivate(ench2, beSrc.Controller?.Game!);
		}

		return ench2;
	}

	public override string ToString() => $"Buff({Args[0]}, {(Args.Length > 1 ? Args[1] : "?")})";
}

/// <summary>
/// Draw a card from the player's deck.
/// </summary>
public class DrawCard : TargetedAction
{
	private readonly Func<BaseEntity, bool>? _filter;

	public DrawCard(object target) : base(target) { _filter = null; }

	/// <summary>带过滤器的抽牌（如抽一张海盗牌、法术牌等）。</summary>
	public DrawCard(object target, Func<BaseEntity, bool> filter) : base(target) { _filter = filter; }

	/// <summary>使用 DrawFilter 流式筛选条件抽牌。</summary>
	public DrawCard(object target, DrawFilter filter) : base(target) { _filter = filter.ToFunc(); }

	/// <summary>抽一张指定类型的牌。</summary>
	public DrawCard(object target, CardType type) : this(target,
		e => e is BuffableEntity be && be.CardData?.CardType == type) { }

	/// <summary>抽一张指定种族的牌。</summary>
	public DrawCard(object target, Race race) : this(target,
		e => e is BuffableEntity be && be.CardData?.Races.Contains(race) == true) { }

	/// <summary>抽一张指定机制的牌。</summary>
	public DrawCard(object target, Mechanic mechanic) : this(target,
		e => e is BuffableEntity be && be.CardData?.Has(mechanic) == true) { }

	/// <summary>抽一张指定法术派系的牌。</summary>
	public DrawCard(object target, SpellSchool school) : this(target,
		e => e is BuffableEntity be && be.CardData?.SpellSchool == school) { }

	public override object? Do(BaseEntity source, BaseEntity target, params object[] args)
	{
		if (target is not Player player) return null;

		Log.Information("{Player} draws a card", player.Name);
		var card = _filter != null ? player.DrawCard(_filter) : player.DrawCard();

		// If this Draw has a callback, execute it with the drawn card as context
		if (card != null && Callback.Length > 0)
		{
			source.CurrentTarget = card;
			try
			{
				foreach (var cb in Callback)
				{
					if (cb is TargetedAction ta) ta.Trigger(source);
				}
			}
			finally { source.CurrentTarget = null; }
		}
		return card;
	}

	public override string ToString() => $"Draw({Args[0]})";
}

// ═══════════════════════════════════════════════════
// DrawFilter — 流式抽牌过滤器
// ═══════════════════════════════════════════════════

/// <summary>
/// 流式构建抽牌过滤条件，与 DiscoverFilter API 风格一致。
/// <para>用法：</para>
/// <code>
/// DrawFilter.Spell()                        // 法术
/// DrawFilter.Spell().Cost(1)                // 1 费法术
/// DrawFilter.Minion().Race(Race.Beast)      // 野兽随从
/// DrawFilter.HasMechanic(Mechanic.Deathrattle) // 有亡语的牌
/// </code>
/// </summary>
public class DrawFilter
{
	private CardType? _type;
	private int? _cost;
	private Race? _race;
	private SpellSchool? _school;
	private Mechanic? _mechanic;

	// ── 静态工厂 ──

	/// <summary>所有牌。</summary>
	public static DrawFilter All() => new();

	/// <summary>随从。</summary>
	public static DrawFilter Minion() => new() { _type = CardType.Minion };

	/// <summary>法术。</summary>
	public static DrawFilter Spell() => new() { _type = CardType.Spell };

	/// <summary>武器。</summary>
	public static DrawFilter Weapon() => new() { _type = CardType.Weapon };

	// ── 流式过滤方法 ──

	/// <summary>按费用过滤。</summary>
	public DrawFilter Cost(int c) { _cost = c; return this; }

	/// <summary>按种族过滤。</summary>
	public DrawFilter Race(Race r) { _race = r; return this; }

	/// <summary>按法术派系过滤。</summary>
	public DrawFilter School(SpellSchool s) { _school = s; return this; }

	/// <summary>按机制过滤。</summary>
	public DrawFilter HasMechanic(Mechanic m) { _mechanic = m; return this; }

	// ── 转换 ──

	/// <summary>生成 Func&lt;BaseEntity, bool&gt; 用于 DrawCard 过滤。</summary>
	public Func<BaseEntity, bool> ToFunc()
	{
		// 捕获当前值的快照
		var type = _type;
		var cost = _cost;
		var race = _race;
		var school = _school;
		var mechanic = _mechanic;

		return e =>
		{
			if (e is not BuffableEntity be) return false;
			var data = be.CardData;
			if (data == null) return false;

			if (type.HasValue && data.CardType != type.Value) return false;
			if (cost.HasValue && be.Cost != cost.Value) return false;
			if (race.HasValue && !data.Races.Contains(race.Value)) return false;
			if (school.HasValue && data.SpellSchool != school.Value) return false;
			if (mechanic.HasValue && !data.Has(mechanic.Value)) return false;

			return true;
		};
	}
}

/// <summary>
/// Summon a minion/weapon onto the field.
/// </summary>
public class Summon : TargetedAction
{
	private object? _customAtk;
	private object? _customHealth;
	private object? _customCost;

	public Summon(object target, object card) : base(target, card) { }

	/// <summary>
	/// 设置召唤物的自定义属性（覆盖卡牌数据中的默认值）。
	/// 支持 int 或 LazyNum。
	/// <code>new Summon(S.CONTROLLER, "SW_085t").WithStats(atk: handCount, health: handCount)</code>
	/// </summary>
	public Summon WithStats(object? atk = null, object? health = null, object? cost = null)
	{
		_customAtk = atk;
		_customHealth = health;
		_customCost = cost;
		return this;
	}

	public override object? Do(BaseEntity source, BaseEntity target, params object[] args)
	{
		if (target is not Player player) return null;

		var cardArg = args.Length > 0 ? args[0] : null;

		BuffableEntity? entity = null;

		if (cardArg is List<BaseEntity> list && list.Count > 0)
		{
			entity = list[0] as BuffableEntity;
		}
		else if (cardArg is string cardId)
		{
			// Create entity from card ID
			entity = player.CreateCard(cardId, Zone.Invalid);
		}
		else if (cardArg is BaseEntity existing)
		{
			// Already an entity (e.g. from Copy selector)
			entity = existing as BuffableEntity;
		}

		if (entity == null) return null;

		// ── Apply custom stats ──
		if (entity is Character statsTarget)
		{
			if (_customAtk != null)
				statsTarget.Atk = ResolveValue(_customAtk, source);
			if (_customHealth != null)
				statsTarget.MaxHealth = ResolveValue(_customHealth, source);
		}
		if (_customCost != null)
			entity.Cost = ResolveValue(_customCost, source);

		// ── Determine summon position ──
		int summonIndex = CalculateSummonIndex(source, player);

		Log.Information("{Player} summons {Entity} at index {Index}", player.Name, entity, summonIndex);
		player.Summon(entity, summonIndex);

		// Track that source has summoned one more minion (for R-L-R-L alternation)
		if (source is BuffableEntity srcBe)
			srcBe.SummonIndex++;

		// Publish MinionSummonedEvent for non-play summons (e.g. Deathrattle, spell effects)
		if (entity is Minion m)
		{
			player.Game?.EventBus.Publish(new MinionSummonedEvent(source, m), MinionPhase.AfterSummon);

			// Colossal: 附肢召唤（非打出路径也需要，如随机召唤/复活等）
			if (m.CardData?.HasTag(GameTag.Colossal) == true)
				player.Game?.Sequences.SummonColossalLimbs(m, player);
		}

		return entity;
	}

	/// <summary>
	/// Calculate the Field insertion index based on Hearthstone positioning rules:
	/// - Minion on field (battlecry/effect): right of source, alternating R-L-R-L for multi-summon
	/// - Deathrattle (source is dead minion): use saved death position
	/// - Spell / Hero Power / Secret: rightmost (return -1)
	/// </summary>
	protected int CalculateSummonIndex(BaseEntity source, Player player)
	{
		if (source is Minion srcMinion)
		{
			int srcFieldIdx = player.Field.IndexOf(srcMinion);

			if (srcFieldIdx >= 0)
			{
				// Source is alive on the field — summon relative to it
				// Alternation pattern: R, L, R, L, ... (0-based SummonIndex)
				// SummonIndex 0 → right (+1), 1 → left (+0), 2 → right (+2), 3 → left (-1), ...
				int n = srcMinion.SummonIndex; // how many already summoned
											   // Right side: insert after source; Left side: insert before source
											   // Each pair (R,L) expands outward: R+1, L+0, R+2, L-1, R+3, L-2, ...
				if (n % 2 == 0)
				{
					// Right side: insert at srcFieldIdx + 1 + (n/2)
					return Math.Min(srcFieldIdx + 1 + n / 2, player.Field.Count);
				}
				else
				{
					// Left side: insert at srcFieldIdx - (n/2)
					return Math.Max(srcFieldIdx - n / 2, 0);
				}
			}
			else
			{
				// Source minion is NOT on field (dead — deathrattle case)
				// Use the saved death position from SummonIndex, or rightmost
				int deathPos = srcMinion.GetIntProperty("death_field_index");
				if (deathPos >= 0)
				{
					// Insert at the death position + offset for subsequent summons
					int offset = srcMinion.SummonIndex;
					return Math.Min(deathPos + offset, player.Field.Count);
				}
				return -1; // fallback: rightmost
			}
		}

		// Spell, Hero Power, Secret, or non-minion source → rightmost
		return -1;
	}

	private static int ResolveValue(object val, BaseEntity source)
	{
		return val switch
		{
			int i => i,
			LazyNum ln => (int)(ln.Evaluate(source) ?? 0),
			_ => 0
		};
	}

	public override string ToString() => $"Summon({Args[0]}, {(Args.Length > 1 ? Args[1] : "?")})";
}

/// <summary>
/// 将自身从当前区域（牌库/手牌/墓地）移出并召唤到场上。
/// 用于帕奇斯型效果："当你打出一个海盗时，从牌库中召唤该随从"
/// <code>
/// b.Card("CFM_637",
///     on: When&lt;CardPlayedEvent&gt;(OWN, AFTER, new SummonSelf(),
///         when: And(PlayedRace(Race.Pirate), InZone&lt;CardPlayedEvent&gt;(Zone.Deck))));
/// </code>
/// </summary>
public class SummonSelf : GameAction
{
	public override void Do(BaseEntity source, params object[] args)
	{
		if (source is not BuffableEntity entity) return;
		var player = entity.Controller;
		if (player == null) return;

		// 场上已满则无法召唤
		if (player.Field.Count >= 7) return;

		// 从当前区域移除
		var oldZone = entity.Zone;
		switch (oldZone)
		{
			case Zone.Deck:
				player.Deck.Remove(entity);
				break;
			case Zone.Hand:
				player.Hand.Remove(entity);
				break;
			case Zone.Graveyard:
				player.Graveyard.Remove(entity);
				break;
			default:
				return; // 已在场上或无效区域
		}

		Log.Information("[SummonSelf] {Entity} summons itself from {Zone}", entity.CardId, oldZone);

		// 召唤到场上最右侧
		player.Summon(entity);

		// 发布召唤事件
		if (entity is Minion m)
		{
			player.Game?.EventBus.Publish(
				new MinionSummonedEvent(entity, m), MinionPhase.AfterSummon);
		}
	}

	public override string ToString() => "SummonSelf()";
}

/// <summary>
/// Destroy character targets (move to GRAVEYARD).
/// </summary>
public class Destroy : TargetedAction
{
	public Destroy(object target) : base(target) { }

	public override object? Do(BaseEntity source, BaseEntity target, params object[] args)
	{
		Log.Information("{Source} destroys {Target}", source, target);
		if (target is Character ch)
			ch.ToBeDestroyed = true;
		return null;
	}

	public override string ToString() => $"Destroy({Args[0]})";
}

/// <summary>
/// 将目标随从送入未来 N 个己方回合后。
/// 随从silently移到 Zone.SetAside（不触发亡语/死亡事件），N 个己方回合后在 BeginTurn 中回归战场。
/// 若回归时战场已满，随从直接消失（不视为死亡）。回归后有召唤疲劳（不能立即攻击）。
/// </summary>
public class SendToFuture : TargetedAction
{
	private readonly int _turns;

	/// <param name="target">选择器（要送走的目标）</param>
	/// <param name="turns">己方回合数（不算对手回合）</param>
	public SendToFuture(object target, int turns = 2) : base(target)
	{
		_turns = turns;
	}

	public override object? Do(BaseEntity source, BaseEntity target, params object[] args)
	{
		if (target is not BuffableEntity be || be.Zone != Zone.Play) return null;

		var owner = be.Controller;
		if (owner == null) return null;

		// 计算回归的全局回合号：当前回合 + 己方回合数 × 2（每轮双方各一回合）
		var game = be.Game;
		if (game == null) return null;
		int returnTurn = game.Turn + _turns * 2;

		Log.Information("{Source} sends {Target} to {Turns} turns in the future (return at turn {ReturnTurn})",
			source, target, _turns, returnTurn);

		// 静默移到 SetAside — 不触发亡语、不触发死亡事件
		be.SetZone(Zone.SetAside);

		// 记录到 owner 的 FutureMinions 列表
		owner.FutureMinions.Add((be, returnTurn));

		return null;
	}

	public override string ToString() => $"SendToFuture({Args[0]}, {_turns} turns)";
}

/// <summary>
/// Damage target by amount (lower-level, after modifiers).
/// </summary>
public class RawDamage : TargetedAction
{
	public RawDamage(object target) : base(target) { }
	public RawDamage(object target, object amount) : base(target, amount) { }

	public override object? Do(BaseEntity source, BaseEntity target, params object[] args)
	{
		int amount = ActionHelper.ResolveInt(args, 0, source);
		if (target is BuffableEntity be)
		{
			int health = be.GetIntProperty("health");
			be.SetIntProperty("health", health - amount);
			Log.Information("{Target} takes {Amount} damage", target, amount);
			return amount;
		}
		return 0;
	}
}

/// <summary>
/// 强制一个角色攻击指定目标，绕过常规攻击校验。
/// 联动攻击不计入主动攻击次数。
/// <code>new ForceAttack(S.SELF, S.EVT_TARGET)</code>
/// </summary>
public class ForceAttack : GameAction
{
	private readonly Selector _attacker;
	private readonly Selector _defender;

	public ForceAttack(Selector attacker, Selector defender)
	{
		_attacker = attacker;
		_defender = defender;
	}

	public override void Do(BaseEntity source, params object[] args)
	{
		var game = source.Game;
		if (game == null) return;

		var atk = _attacker.Eval(game.Entities, source).OfType<Character>().FirstOrDefault();
		var def = _defender.Eval(game.Entities, source).OfType<Character>().FirstOrDefault();
		if (atk == null || def == null || atk.Dead || def.Dead) return;
		if (atk.Zone != Zone.Play) return;

		// 保存攻击次数，联动攻击后恢复
		int savedAttacks = atk.NumAttacks;
		game.Sequences.Combat(atk, def);
		atk.NumAttacks = savedAttacks;
	}
}
