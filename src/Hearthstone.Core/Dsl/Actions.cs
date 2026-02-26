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

// ─── P1: Control & utility actions ──────────

/// <summary>
/// Silence a minion — remove card text, enchantments, abilities.
/// </summary>
public class Silence : TargetedAction
{
	public Silence(object target) : base(target) { }

	public override object? Do(BaseEntity source, BaseEntity target, params object[] args)
	{
		if (target.Type != CardType.Minion) return null;
		Log.Information("Silencing {Target}", target);

		if (target is BuffableEntity be)
		{
			be.ClearBuffs();
			foreach (var attr in new[]
			{
				"taunt", "divine_shield", "stealth", "windfury",
				"charge", "rush", "lifesteal", "poisonous",
				"freeze", "cant_be_targeted_by_spells",
				"cant_be_targeted_by_hero_powers"
			})
				be.SetBoolProperty(attr, false);

			be.SetBoolProperty("silenced", true);
		}
		return null;
	}

	public override string ToString() => $"Silence({Args[0]})";
}

/// <summary>
/// Bounce a minion back to hand.
/// </summary>
public class ReturnToHand : TargetedAction
{
	public ReturnToHand(object target) : base(target) { }

	public override object? Do(BaseEntity source, BaseEntity target, params object[] args)
	{
		Log.Information("{Target} is bounced back to hand", target);
		if (target is BuffableEntity be)
			be.SetZone(Zone.Hand);
		return null;
	}

	public override string ToString() => $"Bounce({Args[0]})";
}

/// <summary>
/// Freeze a character target.
/// </summary>
public class Freeze : TargetedAction
{
	public Freeze(object target) : base(target) { }

	public override object? Do(BaseEntity source, BaseEntity target, params object[] args)
	{
		Log.Information("Freezing {Target}", target);
		if (target is BuffableEntity be)
			be.SetBoolProperty("frozen", true);
		return null;
	}

	public override string ToString() => $"Freeze({Args[0]})";
}

/// <summary>
/// Discard card targets from hand.
/// 弃牌 ≠ 摧毁(Mill) ≠ 死亡(Death)。
/// 弃牌会：标记 GameTag.Discarded、发布 DiscardEvent、加入 Player.DiscardedCards。
/// </summary>
public class Discard : TargetedAction
{
	public Discard(object target) : base(target) { }

	public override object? Do(BaseEntity source, BaseEntity target, params object[] args)
	{
		Log.Information("Discarding {Target}", target);
		if (target is BuffableEntity be)
		{
			be.SetBoolProperty("discarded", true);
			be.SetZone(Zone.Graveyard);

			// 执行被弃卡牌自身的 Discard 钩子（如银器侍从、拉凯希的母亲等）
			var script = CardScriptRegistry.Instance.GetScript(be.CardId ?? "");
			if (script != null && script.Discard.Length > 0)
			{
				foreach (var action in script.Discard)
				{
					if (action is TargetedAction ta) ta.Trigger(be);
					else if (action is GameAction ga) ga.Trigger(be);
				}
			}

			// 追踪弃牌历史 + 发布事件
			var player = be.Controller;
			if (player != null)
			{
				player.DiscardedCards.Add(be);
				var game = player.Game;
				game?.EventBus.Publish(new DiscardEvent(source, player, be), MiscPhase.On);
			}
		}
		return null;
	}
}

/// <summary>
/// Give player a card (put into hand).
/// </summary>
public class AddToHand : TargetedAction
{
	public AddToHand(object target, object card) : base(target, card) { }

	public override object? Do(BaseEntity source, BaseEntity target, params object[] args)
	{
		if (target is not Player player) return null;
		var cardArg = args.Length > 0 ? args[0] : null;

		// 统一按 cardId 创建新卡（不移动原卡）
		string? cardId = cardArg switch
		{
			string id => id,
			List<BaseEntity> list when list.Count > 0 => (list[0] as BuffableEntity)?.CardId,
			BuffableEntity be => be.CardId,
			_ => null,
		};

		if (cardId == null) return null;
		var entity = player.CreateCard(cardId, Zone.Hand);
		Log.Information("{Player} receives {Card}", player.Name, entity);
		return entity;
	}
}

/// <summary>
/// Shuffle a card into player's deck.
/// </summary>
public class ShuffleIntoDeck : TargetedAction
{
	public ShuffleIntoDeck(object target, object card) : base(target, card) { }

	public override object? Do(BaseEntity source, BaseEntity target, params object[] args)
	{
		if (target is not Player player) return null;
		var cardArg = args.Length > 0 ? args[0] : null;

		BuffableEntity? entity = null;
		if (cardArg is string cardId)
			entity = player.CreateCard(cardId, Zone.Deck);
		else if (cardArg is BaseEntity existing && existing is BuffableEntity be)
		{
			be.SetZone(Zone.Deck, player);
			entity = be;
		}

		player.ShuffleDeck();
		if (entity != null)
			Log.Information("Shuffled {Card} into {Player}'s deck", entity, player.Name);
		return entity;
	}
}

/// <summary>
/// Gain armor for hero targets.
/// </summary>
public class GainArmor : TargetedAction
{
	public GainArmor(object target, object amount) : base(target, amount) { }

	public override object? Do(BaseEntity source, BaseEntity target, params object[] args)
	{
		int amount = ActionHelper.ResolveInt(args, 0, source);
		if (target is BuffableEntity be)
		{
			int armor = be.GetIntProperty("armor");
			be.SetIntProperty("armor", armor + amount);
			Log.Information("{Target} gains {Amount} armor", target, amount);

			// Publish GainArmorEvent
			var player = be.Controller ?? (source as BuffableEntity)?.Controller;
			if (player != null && amount > 0)
				player.Game?.EventBus.Publish(
					new GainArmorEvent(source, player, amount), MiscPhase.On);
		}
		return null;
	}
}

/// <summary>
/// Give player mana crystals.
/// </summary>
public class GainMana : TargetedAction
{
	public GainMana(object target, object amount) : base(target, amount) { }

	public override object? Do(BaseEntity source, BaseEntity target, params object[] args)
	{
		int amount = ActionHelper.ResolveInt(args, 0, source);
		if (target is BuffableEntity be)
		{
			int maxMana = be.GetIntProperty("max_mana");
			be.SetIntProperty("max_mana", Math.Min(maxMana + amount, 10));
			Log.Information("{Target} gains {Amount} mana crystal(s)", target, amount);
		}
		return null;
	}
}

/// <summary>
/// Give temporary mana this turn.
/// </summary>
public class GainTemporaryMana : TargetedAction
{
	public GainTemporaryMana(object target, object amount) : base(target, amount) { }

	public override object? Do(BaseEntity source, BaseEntity target, params object[] args)
	{
		int amount = ActionHelper.ResolveInt(args, 0, source);
		if (target is Player player)
		{
			player.TempMana += amount;
			Log.Information("{Target} gains {Amount} mana this turn (TempMana={TempMana})",
				target, amount, player.TempMana);
		}
		else if (target is BuffableEntity be)
		{
			int tempMana = be.GetIntProperty("temp_mana");
			be.SetIntProperty("temp_mana", tempMana + amount);
			Log.Information("{Target} gains {Amount} mana this turn", target, amount);
		}
		return null;
	}
}

/// <summary>
/// Morph (transform) target into another card.
/// Replaces the target entity on the board with a new entity of the specified card.
/// </summary>
public class Transform : TargetedAction
{
	public Transform(object target, object card) : base(target, card) { }

	public override object? Do(BaseEntity source, BaseEntity target, params object[] args)
	{
		var cardArg = args.Length > 0 ? args[0] : null;
		if (target is not BuffableEntity targetBe) return null;
		var controller = targetBe.Controller;
		if (controller == null) return null;

		// Create the replacement entity
		BuffableEntity? replacement = null;
		if (cardArg is List<BaseEntity> list && list.Count > 0)
			replacement = list[0] as BuffableEntity;
		else if (cardArg is string cardId)
			replacement = controller.CreateCard(cardId, Zone.Invalid);
		else if (cardArg is BaseEntity existing && existing is BuffableEntity be)
			replacement = be;

		if (replacement == null) return null;

		// Remove old entity from its zone
		var oldZone = targetBe.Zone;
		targetBe.SetZone(Zone.RemovedFromGame);

		// Place replacement in the same zone
		replacement.Controller = controller;
		replacement.SetZone(oldZone, controller);

		Log.Information("{Target} morphed into {Replacement}", target, replacement);
		return replacement;
	}
}

/// <summary>
/// Steal (take control of) target — transfer control to source's controller.
/// </summary>
public class TakeControl : TargetedAction
{
	public TakeControl(object target) : base(target) { }

	public override object? Do(BaseEntity source, BaseEntity target, params object[] args)
	{
		if (target is not Character ch) return null;
		var newOwner = source is Player p ? p : (source as BuffableEntity)?.Controller;
		if (newOwner == null) return null;
		var oldOwner = ch.Controller;
		if (oldOwner == null || oldOwner == newOwner) return null;

		// Check new owner board space
		if (newOwner.Field.Count >= 7) return null;

		// Remove from old owner's field
		oldOwner.Field.Remove(target);

		// Transfer ownership
		ch.Controller = newOwner;
		newOwner.Field.Add(target);

		Log.Information("{NewOwner} takes control of {Target} from {OldOwner}",
			newOwner.Name, target, oldOwner.Name);
		return target;
	}
}

/// <summary>
/// Set the current health of target to amount.
/// </summary>
public class SetCurrentHealth : TargetedAction
{
	public SetCurrentHealth(object target, object amount) : base(target, amount) { }

	public override object? Do(BaseEntity source, BaseEntity target, params object[] args)
	{
		int amount = ActionHelper.ResolveInt(args, 0, source);
		if (target is BuffableEntity be)
		{
			be.SetIntProperty("health", amount);
			Log.Information("Setting {Target}'s health to {Amount}", target, amount);
		}
		return target;
	}
}

/// <summary>
/// Counter a card (prevent its effects).
/// </summary>
public class Counter : TargetedAction
{
	public Counter(object target) : base(target) { }

	public override object? Do(BaseEntity source, BaseEntity target, params object[] args)
	{
		Log.Information("[Counter] Attempting to counter: source={Source} target={Target} targetType={Type}",
			source?.ToString(), target?.ToString(), target?.GetType().Name);
		if (target is BuffableEntity be)
		{
			be.SetBoolProperty("countered", true);
			Log.Information("[Counter] Set 'countered' on {Entity} (CardId={CardId})",
				be.EntityId, be.CardId);
		}
		return null;
	}
}

/// <summary>
/// Swap two entities.
/// </summary>
public class SwapEntities : TargetedAction
{
	private readonly bool _keepBuffs;

	public SwapEntities(object target, object other, bool keepBuffs = false) : base(target, other)
	{
		_keepBuffs = keepBuffs;
	}

	public override object? Do(BaseEntity source, BaseEntity target, params object[] args)
	{
		if (args.Length == 0) return null;
		var other = args[0] as BaseEntity;
		if (other == null) return null;

		Log.Information("Swapping {Target} with {Other}", target, other);

		if (target is BuffableEntity beTarget && other is BuffableEntity beOther)
		{
			var targetOldZone = beTarget.Zone;
			var otherOldZone = beOther.Zone;
			var targetController = beTarget.Controller;
			var otherController = beOther.Controller;

			// Transit through SETASIDE to avoid zone conflicts
			beTarget.SetZone(Zone.SetAside);
			beOther.SetZone(Zone.SetAside);

			// Swap controllers
			beTarget.Controller = otherController;
			beOther.Controller = targetController;

			// Swap zones
			beTarget.SetZone(otherOldZone);
			beOther.SetZone(targetOldZone);

			// 离开战场的实体默认清除 buff（除非 keepBuffs=true）
			if (!_keepBuffs)
			{
				if (targetOldZone == Zone.Play && beTarget.Zone != Zone.Play)
					beTarget.ClearBuffs();
				if (otherOldZone == Zone.Play && beOther.Zone != Zone.Play)
					beOther.ClearBuffs();
			}
		}
		return null;
	}
}

/// <summary>
/// Reveal a secret.
/// </summary>
public class RevealSecret : TargetedAction
{
	public RevealSecret(object target) : base(target) { }

	public override object? Do(BaseEntity source, BaseEntity target, params object[] args)
	{
		Log.Information("Revealing {Target}", target);
		if (target is BuffableEntity be)
		{
			// Publish RevealSecretEvent before moving to graveyard
			be.Controller?.Game?.EventBus.Publish(
				new RevealSecretEvent(source, be), MiscPhase.On);
			be.SetZone(Zone.Graveyard);
		}
		return null;
	}
}

/// <summary>
/// Remove entity from game entirely.
/// </summary>
public class RemoveFromGame : TargetedAction
{
	public RemoveFromGame(object target) : base(target) { }

	public override object? Do(BaseEntity source, BaseEntity target, params object[] args)
	{
		if (target is BuffableEntity be)
			be.SetZone(Zone.RemovedFromGame);
		return null;
	}
}

// ─── Game-level actions ──────────

/// <summary>Process deaths in the PLAY zone.</summary>
public class ProcessDeaths : GameAction
{
	public override void Do(BaseEntity source, params object[] args)
		=> Log.Information("Processing deaths");
}

/// <summary>Death — move entity to GRAVEYARD, fire deathrattles.</summary>
public class ProcessDeath : GameAction
{
	public ProcessDeath(object entity) : base(entity) { }
	public override void Do(BaseEntity source, params object[] args)
		=> Log.Information("Death processing");
}

/// <summary>
/// Attack — 执行攻击逻辑：设置战斗标记，互击伤害，武器耐久。
/// </summary>
public class Attack : GameAction
{
	private static readonly ILogger _log = LogConfig.ForType<Attack>();

	public Attack(params object[] args) : base(args) { }

	public override void Do(BaseEntity source, params object[] args)
	{
		// args[0] = attacker, args[1] = defender
		if (args.Length < 2) return;
		if (args[0] is not Character attacker || args[1] is not Character defender) return;
		// 通过 Controller.Game fallback 获取 Game 引用（测试中实体可能未直接设 Game 属性）
		var game = source.Game
			?? (source as BuffableEntity)?.Controller?.Game
			?? (attacker as BuffableEntity)?.Controller?.Game;
		if (game == null) return;

		// 设置战斗标记
		attacker.AttackTarget = defender;
		defender.Defending = true;
		game.ProposedAttacker = attacker;
		game.ProposedDefender = defender;

		// 读回 ProposedDefender（可能被事件重定向）
		defender = game.ProposedDefender ?? defender;
		game.ProposedAttacker = null;
		game.ProposedDefender = null;

		// 检查攻击是否应中断（攻击者死亡/离开战场）
		if (attacker.ShouldExitCombat)
		{
			_log.Information("Attack interrupted: attacker exiting combat.");
			attacker.AttackTarget = null;
			defender.Defending = false;
			return;
		}

		// 保存攻击力（战斗中可能改变）
		int defAtk = defender.Atk;
		int atkAtk = attacker.Atk;

		// 攻击时移除潜行
		if (attacker.Stealth)
			attacker.Stealth = false;

		// 互击伤害
		defender.Hit(atkAtk);
		if (defAtk > 0)
			attacker.Hit(defAtk);

		// 武器耐久损耗
		if (attacker is Hero attackHero && attackHero.EquippedWeapon != null)
		{
			var weapon = attackHero.EquippedWeapon;
			weapon.LoseDurability();
			if (weapon.Dead)
			{
				_log.Information("Weapon {Weapon} destroyed (0 durability)", weapon.CardId);
				weapon.SetZone(Zone.Graveyard);
				attackHero.EquippedWeapon = null;
			}
		}

		// 清理战斗状态
		attacker.AttackTarget = null;
		defender.Defending = false;
		attacker.NumAttacks++;
	}
}



/// <summary>Game start setup.</summary>
public class GameStart : GameAction
{
	public override void Do(BaseEntity source, params object[] args)
		=> Log.Information("Game start");
}

// ─── P1: 战斗 / 变身 / Buff 扩展 ──────────

/// <summary>
/// 额外攻击机会。
/// </summary>
public class GrantWindfury : TargetedAction
{
	private static readonly ILogger _log = LogConfig.ForType<GrantWindfury>();

	public GrantWindfury(object target) : base(target) { }

	public override object? Do(BaseEntity source, BaseEntity target, params object[] args)
	{
		if (target is Character ch)
		{
			_log.Information("{Target} gets an extra attack chance", target);
			ch.NumAttacks = Math.Max(0, ch.NumAttacks - 1);
		}
		return null;
	}
}

/// <summary>
/// 溢出伤害。
/// </summary>
public class DealOverflowDamage : TargetedAction
{
	public DealOverflowDamage(object target, object amount) : base(target, amount) { }

	public override object? Do(BaseEntity source, BaseEntity target, params object[] args)
	{
		int amount = ActionHelper.ResolveInt(args, 0, source);
		if (amount <= 0) return 0;

		if (target is Character ch && source.Game != null)
		{
			if (ch.Health >= amount)
			{
				source.Game.QueueActions(source,
					new List<Action> { new PredamageAction(target, amount) });
				return 0;
			}
			else
			{
				int excess = amount - ch.Health;
				source.Game.QueueActions(source,
					new List<Action> { new PredamageAction(target, amount) });
				return excess;
			}
		}
		return 0;
	}
}

/// <summary>
/// 多重 Buff。
/// </summary>
public class MultiEnchant : TargetedAction
{
	public MultiEnchant(object target, object buffs) : base(target, buffs) { }

	public override object? Do(BaseEntity source, BaseEntity target, params object[] args)
	{
		if (args.Length == 0 || target is not BuffableEntity be) return target;

		// args[0] is a list of buff IDs or Enchantment objects
		var buffs = args[0] as IEnumerable<object>;
		if (buffs == null && args[0] is string singleBuff)
			buffs = new[] { (object)singleBuff };
		if (buffs == null) return target;

		var controller = source is BuffableEntity sb ? sb.Controller : null;

		foreach (var buff in buffs)
		{
			if (buff is string buffId && controller != null)
			{
				var enchantment = controller.CreateCard(buffId) as Enchantment;
				if (enchantment != null)
				{
					source.Game?.RegisterEntity(enchantment);
					enchantment.Apply(be);
				}
			}
			else if (buff is Enchantment enc)
			{
				enc.Apply(be);
			}
		}
		Log.Debug("MultiBuff applied to {Target}", target);
		return target;
	}
}

/// <summary>
/// 存储 Buff。
/// </summary>
public class StoreEnchant : TargetedAction
{
	public StoreEnchant(object target, object buff, object card) : base(target, buff, card) { }

	public override object? Do(BaseEntity source, BaseEntity target, params object[] args)
	{
		if (target is not BuffableEntity be) return null;
		if (args.Length < 2) return null;

		var controller = source is BuffableEntity sb ? sb.Controller : null;
		if (controller == null) return null;

		// args[0] = buff ID, args[1] = card to store
		var buffId = args[0] as string;
		var storeCard = args[1] as BaseEntity;

		if (buffId != null)
		{
			var enchantment = controller.CreateCard(buffId) as Enchantment;
			if (enchantment != null)
			{
				source.Game?.RegisterEntity(enchantment);
				if (storeCard != null)
					enchantment.StoreCard = storeCard;
				Log.Debug("StoringBuff {Buff} stores {Card}, applied to {Target}", buffId, storeCard, target);
				enchantment.Apply(be);
				return enchantment;
			}
		}
		return null;
	}
}

/// <summary>
/// 复制亡语 Buff。
/// </summary>
public class CopyDeathrattle : TargetedAction
{
	public CopyDeathrattle(object target, object buff) : base(target, buff) { }

	public override object? Do(BaseEntity source, BaseEntity target, params object[] args)
	{
		if (target is not BuffableEntity be) return null;
		if (!be.HasDeathrattle) return null;

		// 复制亡语到 source
		if (source is BuffableEntity sourceBe)
		{
			foreach (var deathrattle in be.Deathrattles)
			{
				sourceBe.Deathrattles.Add(deathrattle);
			}
		}
		return null;
	}
}

/// <summary>
/// 放到牌库顶。
/// </summary>
public class PlaceOnDeckTop : TargetedAction
{
	private static readonly ILogger _log = LogConfig.ForType<PlaceOnDeckTop>();

	public PlaceOnDeckTop(object target, object card) : base(target, card) { }

	public override object? Do(BaseEntity source, BaseEntity target, params object[] args)
	{
		if (target is not Player player) return null;
		if (args.Length == 0) return null;

		var cardArg = args[0];
		var cards = cardArg is List<BaseEntity> list ? list : new List<BaseEntity> { cardArg as BaseEntity ?? target };

		if (cards.Count > 0)
			player.ShuffleDeck();

		foreach (var card in cards)
		{
			if (card is not BuffableEntity be) continue;

			if (be.Controller != player)
			{
				be.SetZone(Zone.SetAside);
				be.Controller = player;
			}

			if (be.Zone != Zone.Deck && player.Deck.Count >= player.MaxDeckSize)
			{
				_log.Information("PutOnTop({Card}) fails because {Player}'s deck is full", card, player);
				continue;
			}

			be.SetZone(Zone.Deck);
			// 交换到顶部
			if (player.Deck.Count > 1)
			{
				int lastIdx = player.Deck.Count - 1;
				(player.Deck[0], player.Deck[lastIdx]) = (player.Deck[lastIdx], player.Deck[0]);
			}
		}
		return null;
	}
}

/// <summary>
/// 改变目标。
/// </summary>
public class RedirectTarget : TargetedAction
{
	private static readonly ILogger _log = LogConfig.ForType<RedirectTarget>();

	public RedirectTarget(object target, object newTarget) : base(target, newTarget) { }

	public override object? Do(BaseEntity source, BaseEntity target, params object[] args)
	{
		if (args.Length == 0) return null;
		var newTarget = args[0] as BaseEntity;
		if (newTarget == null) return null;

		_log.Information("Retargeting {Target} to {NewTarget}", target, newTarget);
		// Target 属性存储在实体的瞬态字段中
		if (target is BuffableEntity be)
		{
			be.SetIntProperty("_retarget", newTarget.EntityId);
		}
		return newTarget;
	}
}

/// <summary>
/// 设置标签。
/// </summary>
public class SetTags : TargetedAction
{
	public SetTags(object target, object tags) : base(target, tags) { }

	public override object? Do(BaseEntity source, BaseEntity target, params object[] args)
	{
		if (args.Length == 0) return null;

		if (args[0] is Dictionary<GameTag, int> tagDict)
		{
			target.Tags.Update(tagDict);
		}
		else if (args[0] is IEnumerable<GameTag> tagList)
		{
			foreach (var tag in tagList)
			{
				target.Tags[tag] = 1;
			}
		}
		return null;
	}
}

/// <summary>
/// 取消标签。
/// </summary>
public class UnsetTags : TargetedAction
{
	public UnsetTags(object target, object tags) : base(target, tags) { }

	public override object? Do(BaseEntity source, BaseEntity target, params object[] args)
	{
		if (args.Length == 0) return null;

		if (args[0] is IEnumerable<GameTag> tagList)
		{
			foreach (var tag in tagList)
			{
				if (target.Tags.Contains(tag))
					target.Tags[tag] = 0;
			}
		}
		return null;
	}
}

/// <summary>
/// 获取标签值。
/// </summary>
public class GetTag : TargetedAction
{
	public GetTag(object target, object tag) : base(target, tag) { }

	public override object? Do(BaseEntity source, BaseEntity target, params object[] args)
	{
		if (args.Length == 0) return null;
		if (args[0] is GameTag tag)
		{
			return target.Tags.Get(tag, 0);
		}
		return null;
	}
}

/// <summary>
/// 进化。
/// </summary>
public class Evolve : TargetedAction
{
	public Evolve(object target, object amount) : base(target, amount) { }

	public override object? Do(BaseEntity source, BaseEntity target, params object[] args)
	{
		if (target is not BuffableEntity be) return null;
		if (source.Game == null) return null;
		int amount = ActionHelper.ResolveInt(args, 0, source);

		int cost = be.Cost + amount;
		var picker = RandomCards.RandomMinion().Cost(cost);
		var cardSet = picker.FindCards(source);
		if (cardSet.Count > 0)
		{
			var cardId = cardSet[source.Game.Random.Next(cardSet.Count)];
			Log.Information("Evolve {Target} by {Amount} → {Card}", target, amount, cardId);
			source.Game.QueueActions(source,
				new List<Action> { new Transform(target, cardId) });
		}
		else
		{
			Log.Debug("Evolve {Target} by {Amount}: no minions at cost {Cost}", target, amount, cost);
		}
		return null;
	}
}


/// <summary>
/// 交换属性 Buff。
/// </summary>
public class SwapStats : TargetedAction
{
	public SwapStats(object target, object other, object buff) : base(target, other, buff) { }

	public override object? Do(BaseEntity source, BaseEntity target, params object[] args)
	{
		if (args.Length < 2) return null;

		// args[0] = other entity (or list), args[1] = buff ID
		BaseEntity? other = args[0] is IList<BaseEntity> list && list.Count > 0 ? list[0] : args[0] as BaseEntity;
		var buffId = args[1] as string;
		if (other == null || buffId == null) return null;
		if (target is not BuffableEntity beTarget) return null;
		if (other is not BuffableEntity beOther) return null;

		var controller = source is BuffableEntity sb ? sb.Controller : null;
		if (controller == null) return null;

		Log.Information("SwapStateBuff: swapping stats between {Target} and {Other}", target, other);

		// buff1: apply other's stats to target
		var buff1 = controller.CreateCard(buffId) as Enchantment;
		if (buff1 != null)
		{
			source.Game?.RegisterEntity(buff1);
			if (beOther is Minion mo) { buff1.SetIntProperty("_xatk", mo.Atk); buff1.SetIntProperty("_xhealth", mo.Health); }
			buff1.SetIntProperty("_xcost", beOther.Cost);
			buff1.Apply(beTarget);
		}

		// buff2: apply target's stats to other
		var buff2 = controller.CreateCard(buffId) as Enchantment;
		if (buff2 != null)
		{
			source.Game?.RegisterEntity(buff2);
			if (beTarget is Minion mt) { buff2.SetIntProperty("_xatk", mt.Atk); buff2.SetIntProperty("_xhealth", mt.Health); }
			buff2.SetIntProperty("_xcost", beTarget.Cost);
			buff2.Apply(beOther);
		}
		return null;
	}
}

/// <summary>
/// 复制属性 Buff。
/// </summary>
public class CopyStats : TargetedAction
{
	public CopyStats(object target, object other, object buff) : base(target, other, buff) { }

	public override object? Do(BaseEntity source, BaseEntity target, params object[] args)
	{
		// Copy target's stats onto source via buff
		if (args.Length < 1) return null;
		var buffId = args[0] as string;
		if (buffId == null) return null;
		if (source is not BuffableEntity beSource) return null;
		if (target is not BuffableEntity beTarget) return null;

		var controller = beSource.Controller;
		if (controller == null) return null;

		var buff = controller.CreateCard(buffId) as Enchantment;
		if (buff != null)
		{
			source.Game?.RegisterEntity(buff);
			if (beTarget is Minion m) { buff.SetIntProperty("_xatk", m.Atk); buff.SetIntProperty("_xhealth", m.Health); }
			Log.Information("CopyStateBuff: copying {Target} stats to {Source}", target, source);
			buff.Apply(beSource);
		}
		return null;
	}
}

/// <summary>
/// 设置属性 Buff。
/// </summary>
public class SetStats : TargetedAction
{
	public SetStats(object target, object other, object buff) : base(target, other, buff) { }

	public override object? Do(BaseEntity source, BaseEntity target, params object[] args)
	{
		// Set source's stats onto target via buff
		if (args.Length < 1) return null;
		var buffId = args[0] as string;
		if (buffId == null) return null;
		if (source is not BuffableEntity beSource) return null;
		if (target is not BuffableEntity beTarget) return null;

		var controller = beSource.Controller;
		if (controller == null) return null;

		var buff = controller.CreateCard(buffId) as Enchantment;
		if (buff != null)
		{
			source.Game?.RegisterEntity(buff);
			if (beSource is Minion m) { buff.SetIntProperty("_xatk", m.Atk); buff.SetIntProperty("_xhealth", m.Health); }
			Log.Information("SetStateBuff: setting {Source} stats on {Target}", source, target);
			buff.Apply(beTarget);
		}
		return null;
	}
}

/// <summary>
/// 玩家断线。
/// </summary>
public class Disconnect : GameAction
{
	public Disconnect(object player) : base(player) { }

	public override void Do(BaseEntity source, params object[] args)
	{
		if (args.Length > 0 && args[0] is Player player)
		{
			player.PlayState = PlayState.Disconnected;
		}
	}
}

/// <summary>
/// 失去圣盾。
/// </summary>
public class RemoveDivineShield : TargetedAction
{
	public RemoveDivineShield(object target) : base(target) { }

	public override object? Do(BaseEntity source, BaseEntity target, params object[] args)
	{
		if (target is Character ch)
		{
			ch.DivineShield = false;
		}
		return null;
	}
}

// ─── P1.4 新增 Action: 法力 / 牌库操作 ──────────

/// <summary>
/// Mill cards from the top of a player's deck (discard to graveyard).
/// 磨牌 = 从牌库销毁，不算弃牌（不触发 DiscardEvent，不标记 Discarded）。
/// </summary>
public class MillCards : TargetedAction
{
	public MillCards(object target) : base(target) { }
	public MillCards(object target, object count) : base(target, count) { }

	public override object? Do(BaseEntity source, BaseEntity target, params object[] args)
	{
		if (target is not Player player) return null;
		if (player.Deck.Count == 0) return null;

		var card = player.Deck[^1]; // top of deck is last element
		if (card is BuffableEntity be)
		{
			Log.Information("Mill: {Player} loses {Card} from top of deck", player.Name, be.CardId);
			be.SetZone(Zone.Graveyard);
		}
		return card;
	}
}

/// <summary>
/// Refill mana crystals for player targets.
/// </summary>
public class RefillMana : TargetedAction
{
	public RefillMana(object target, object amount) : base(target, amount) { }

	public override object? Do(BaseEntity source, BaseEntity target, params object[] args)
	{
		if (target is not Player player) return null;
		int amount = ActionHelper.ResolveInt(args, 0, source);
		player.UsedMana = Math.Max(0, player.UsedMana - amount);
		Log.Information("FillMana: {Player} refills {Amount} mana", player.Name, amount);
		return null;
	}
}

/// <summary>
/// Set a player's max mana crystals.
/// </summary>
public class SetMaxMana : TargetedAction
{
	public SetMaxMana(object target, object amount) : base(target, amount) { }

	public override object? Do(BaseEntity source, BaseEntity target, params object[] args)
	{
		if (target is not Player player) return null;
		int amount = ActionHelper.ResolveInt(args, 0, source);
		int oldMana = player.Mana;
		player.MaxMana = amount;
		player.UsedMana = Math.Max(0,
			player.MaxMana - player.OverloadLocked - oldMana + player.TempMana);
		Log.Information("SetMana: {Player} max mana set to {Amount}", player.Name, amount);
		return null;
	}
}

/// <summary>
/// Draw cards until the player has a certain number of cards in hand.
/// </summary>
public class DrawUntilCount : TargetedAction
{
	public DrawUntilCount(object target, object amount) : base(target, amount) { }

	public override object? Do(BaseEntity source, BaseEntity target, params object[] args)
	{
		if (target is not Player player) return null;
		int amount = ActionHelper.ResolveInt(args, 0, source);
		int difference = Math.Max(0, amount - player.Hand.Count);
		if (difference > 0 && source.Game != null)
		{
			for (int i = 0; i < difference; i++)
				player.DrawCard();
		}
		return null;
	}
}

/// <summary>
/// Unlock overloaded mana crystals for player targets.
/// </summary>
public class UnlockOverload : TargetedAction
{
	public UnlockOverload(object target) : base(target) { }

	public override object? Do(BaseEntity source, BaseEntity target, params object[] args)
	{
		if (target is not Player player) return null;
		player.Overloaded = 0;
		player.OverloadLocked = 0;
		Log.Information("UnlockOverload: {Player} overload cleared", player.Name);
		return null;
	}
}

// ─── P8: New composite actions ──────────

/// <summary>
/// Iterate over selector targets and run a per-entity action factory.
/// Used for Lightbomb (damage=ATK), Polarity Shift (swap stats), Poison Seeds, etc.
/// </summary>
public class ForEach : TargetedAction
{
	private readonly Func<BuffableEntity, Action?>? _factory;
	private readonly TargetedAction? _action;

	public ForEach(object selector, Func<BuffableEntity, Action?> factory) : base(selector)
	{
		_factory = factory;
	}

	/// <summary>声明式重载：直接传 TargetedAction，通过 CurrentTarget 上下文传递当前实体。</summary>
	public ForEach(object selector, TargetedAction action) : base(selector)
	{
		_action = action;
	}

	public override object? Do(BaseEntity source, BaseEntity target, params object[] args)
	{
		if (target is not BuffableEntity be) return null;

		if (_action != null)
		{
			var prev = source.CurrentTarget;
			source.CurrentTarget = be;
			try { _action.Trigger(source); }
			finally { source.CurrentTarget = prev; }
		}
		else if (_factory != null)
		{
			var action = _factory(be);
			if (action is TargetedAction ta)
				ta.Trigger(source);
		}
		return null;
	}
}

/// <summary>
/// Copy entities from a selector into a player's hand (clone as new cards).
/// <list type="bullet">
///   <item>keepBuffs=false（默认）：创建原始版本新卡</item>
///   <item>keepBuffs=true：复制当前属性（ATK/Health/Cost）到新卡</item>
/// </list>
/// </summary>
public class CopyToHand : TargetedAction
{
	private readonly bool _keepBuffs;

	public CopyToHand(object target, object sourceSelector, bool keepBuffs = false)
		: base(target, sourceSelector)
	{
		_keepBuffs = keepBuffs;
	}

	public override object? Do(BaseEntity source, BaseEntity target, params object[] args)
	{
		if (target is not Player player) return null;
		var toCopy = args.Length > 0 ? args[0] : null;

		var entities = toCopy switch
		{
			List<BaseEntity> list => list,
			BaseEntity e => new List<BaseEntity> { e },
			_ => new List<BaseEntity>()
		};

		foreach (var entity in entities)
		{
			if (entity is BuffableEntity be && be.CardId != null)
			{
				var copy = player.CreateCard(be.CardId, Zone.Hand);
				if (_keepBuffs && copy is BuffableEntity copyBe)
				{
					// 保留当前属性
					copyBe.SetIntProperty("atk", be.GetIntProperty("atk"));
					copyBe.SetIntProperty("health", be.GetIntProperty("health"));
					copyBe.SetIntProperty("cost", be.GetIntProperty("cost"));
				}
				Log.Information("{Player} copies {Card} to hand (keepBuffs={Keep})",
					player.Name, copy, _keepBuffs);
			}
		}
		return null;
	}
}

/// <summary>
/// 从墓地或场上召唤实体的副本。
/// 墓地来源的实体使用原始卡牌数据（不带 buff），场上来源保留当前状态。
/// 用于复活术、克尔苏加德、转生、回响软泥怪等。
/// </summary>
public class SummonCopy : TargetedAction
{
	public SummonCopy(object target, object sourceSelector) : base(target, sourceSelector) { }

	public override object? Do(BaseEntity source, BaseEntity target, params object[] args)
	{
		if (target is not Player player) return null;
		var toSummon = args.Length > 0 ? args[0] : null;

		var entities = toSummon switch
		{
			List<BaseEntity> list => list,
			BaseEntity e => new List<BaseEntity> { e },
			_ => new List<BaseEntity>()
		};

		foreach (var entity in entities)
		{
			if (entity is BuffableEntity be && be.CardId != null)
			{
				// 从墓地复活的随从使用原始卡牌数据（无 buff），场上复制保留状态
				bool fromGraveyard = be.Zone == Zone.Graveyard;
				var copy = player.CreateCard(be.CardId, Zone.Play) as Minion;
				if (copy != null)
				{
					// 场上复制：把来源的 buff 复制过去
					if (!fromGraveyard)
					{
						foreach (var (tag, val) in be.Tags.Items())
						{
							if (tag != GameTag.EntityId && tag != GameTag.Zone)
								copy.Tags[tag] = val;
						}
					}
					// 墓地复活：CreateCard 已用原始数据，无需额外操作

					player.Game?.RegisterEntity(copy);
					Log.Information("{Player} summons copy of {Card} (from {Zone})",
						player.Name, copy, fromGraveyard ? "graveyard" : "board");
					player.Game?.EventBus.Publish(
						new MinionSummonedEvent(source, copy), MinionPhase.AfterSummon);
				}
			}
		}
		return null;
	}
}

/// <summary>
/// 给目标随机授予风怒/嘲讽/圣盾之一。
/// 用于增强-o-机器人。
/// </summary>
public class GrantRandomAbility : TargetedAction
{
	private static readonly GameTag[] _abilities = { GameTag.Windfury, GameTag.Taunt, GameTag.DivineShield };

	public GrantRandomAbility(object target) : base(target) { }

	public override object? Do(BaseEntity source, BaseEntity target, params object[] args)
	{
		if (target is not BuffableEntity be) return null;
		var rng = source.Game?.Random ?? new Random();
		var tag = _abilities[rng.Next(_abilities.Length)];
		be.Tags[tag] = 1;
		Log.Information("{Target} gains {Tag}", target, tag);
		return tag;
	}
}

/// <summary>翻倍的属性标志。</summary>
[Flags]
public enum DoubleStat
{
	Atk = 1,
	Health = 2,
	Cost = 4,
	AtkAndHealth = Atk | Health,
	All = Atk | Health | Cost,
}

/// <summary>
/// 将目标的指定属性翻倍（攻击力、血量、费用，或任意组合）。
/// 用于戈拉蟾蜍（翻倍 ATK）、内战等。
/// <code>
/// new DoubleStats(S.SELF)                          // 翻倍 ATK（默认）
/// new DoubleStats(S.SELF, DoubleStat.Health)        // 翻倍血量
/// new DoubleStats(S.SELF, DoubleStat.AtkAndHealth)  // 翻倍攻防
/// new DoubleStats(S.SELF, DoubleStat.All)           // 翻倍全部
/// </code>
/// </summary>
public class DoubleStats : TargetedAction
{
	private readonly DoubleStat _stats;

	public DoubleStats(object target, DoubleStat stats = DoubleStat.Atk) : base(target)
	{
		_stats = stats;
	}

	public override object? Do(BaseEntity source, BaseEntity target, params object[] args)
	{
		if (target is not BuffableEntity be) return null;

		if (_stats.HasFlag(DoubleStat.Atk))
		{
			int atk = be.GetIntProperty("atk");
			be.SetIntProperty("atk", atk * 2);
			Log.Information("{Target} ATK doubled: {Old} → {New}", target, atk, atk * 2);
		}
		if (_stats.HasFlag(DoubleStat.Health))
		{
			int hp = be.GetIntProperty("health");
			be.SetIntProperty("health", hp * 2);
			Log.Information("{Target} Health doubled: {Old} → {New}", target, hp, hp * 2);
		}
		if (_stats.HasFlag(DoubleStat.Cost))
		{
			int cost = be.GetIntProperty("cost");
			be.SetIntProperty("cost", cost * 2);
			Log.Information("{Target} Cost doubled: {Old} → {New}", target, cost, cost * 2);
		}
		return null;
	}
}

/// <summary>BouncingBlade 的循环终止条件。</summary>
public enum BounceStop
{
	/// <summary>有随从死亡时停止（经典弹射之刃）。</summary>
	UntilDeath,
	/// <summary>一轮伤害后没有随从死亡则停止（伤害不致死即结束）。</summary>
	UntilNoDeath,
	/// <summary>仅按最大循环次数结束，不检查死亡。</summary>
	MaxOnly,
}

/// <summary>
/// 循环对目标池造成伤害，按指定条件终止。
/// <list type="bullet">
///   <item>随机模式（默认）：每次随机选一个目标</item>
///   <item>全体模式：每次对全部目标各造成伤害</item>
/// </list>
/// <code>
/// new RepeatingDamage(S.ALL_MINIONS)                                       // 随机×1，死亡停
/// new RepeatingDamage(S.ENEMY_MINIONS, 2)                                  // 随机×2，死亡停
/// new RepeatingDamage(S.ALL_MINIONS, 1, true)                              // 全体×1，死亡停
/// new RepeatingDamage(S.ALL_MINIONS, 1, false, BounceStop.UntilNoDeath)    // 随机×1，无死亡停
/// new RepeatingDamage(S.ALL_MINIONS, 1, true, BounceStop.MaxOnly, 3)       // 全体×1，固定3轮
/// </code>
/// </summary>
public class RepeatingDamage : TargetedAction
{
	private readonly int _damage;
	private readonly bool _hitAll;
	private readonly BounceStop _stop;
	private readonly int _maxRounds;

	/// <param name="poolSelector">目标池选择器</param>
	/// <param name="damage">每次伤害量（默认 1）</param>
	/// <param name="hitAll">true = 全体，false = 随机单体（默认）</param>
	/// <param name="stop">终止条件（默认 UntilDeath）</param>
	/// <param name="maxRounds">最大循环次数（默认 80）</param>
	public RepeatingDamage(object poolSelector, int damage = 1, bool hitAll = false,
		BounceStop stop = BounceStop.UntilDeath, int maxRounds = 80)
		: base(poolSelector)
	{
		_damage = damage;
		_hitAll = hitAll;
		_stop = stop;
		_maxRounds = maxRounds;
	}

	public override object? Do(BaseEntity source, BaseEntity target, params object[] args)
	{
		var game = (source as BuffableEntity)?.Controller?.Game;
		if (game == null) return null;
		var rng = game.Random ?? new Random();

		for (int i = 0; i < _maxRounds; i++)
		{
			var pool = GetTargets(source)
				.OfType<Minion>()
				.Where(m => m.Zone == Zone.Play && m.Health > 0)
				.ToList();

			if (pool.Count == 0) break;

			bool died = false;

			if (_hitAll)
			{
				foreach (var m in pool)
				{
					int dealt = m.Hit(_damage);
					if (dealt > 0)
						game.EventBus.Publish(new DamageEvent(source, m, dealt), MiscPhase.On);
					if (m.Health <= 0) died = true;
				}
			}
			else
			{
				var victim = pool[rng.Next(pool.Count)];
				int dealt = victim.Hit(_damage);
				if (dealt > 0)
					game.EventBus.Publish(new DamageEvent(source, victim, dealt), MiscPhase.On);
				if (victim.Health <= 0) died = true;
			}

			// 根据终止条件判断是否结束
			switch (_stop)
			{
				case BounceStop.UntilDeath:
					if (died) return null;
					break;
				case BounceStop.UntilNoDeath:
					if (!died) return null;
					break;
				case BounceStop.MaxOnly:
					break; // 不检查，仅靠 maxRounds 控制
			}
		}
		return null;
	}
}


// ─── Utility ──────────

/// <summary>
/// Resolve int values from action args (supports int, LazyNum, LazyValue).
/// </summary>
public static class ActionHelper
{
	public static int ResolveInt(object[] args, int index, BaseEntity source)
	{
		if (index >= args.Length) return 0;
		return args[index] switch
		{
			int i => i,
			LazyNum ln => ln.Value(source),
			LazyValue lv => lv.Evaluate(source) is int vi ? vi : 0,
			_ => 0
		};
	}
}

/// <summary>
/// 计算选择器匹配的实体数量，返回 LazyNum。
/// 用例：亡语伤害 = 本回合死亡的同名随从数量。
/// </summary>
public class CountSelector : LazyNum
{
	private readonly Selector _selector;

	public CountSelector(Selector selector) => _selector = selector;

	public override int Value(BaseEntity source)
	{
		var all = source.Game?.Entities ?? new List<BaseEntity>();
		return _selector.Eval(all, source).Count;
	}
}

/// <summary>
/// Mimiron's Head: destroy up to 3 friendly Mechs (including self), summon V-07-TR-0N.
/// </summary>
public class MimironTransform : GameAction
{
	public override void Do(BaseEntity source, params object[] args)
	{
		var game = source.Game;
		if (game == null) return;

		// Find friendly Mechs on field (including self)
		var mechs = S.FRIENDLY_MINIONS.Eval(game.Entities, source)
			.OfType<BuffableEntity>()
			.Where(m => m.Data?.Races?.Contains(Race.Mechanical) == true)
			.Take(3)
			.ToList();

		if (mechs.Count < 3) return;

		foreach (var m in mechs)
		{
			if (m is Character ch)
				ch.ToBeDestroyed = true;
		}

		game.ProcessDeaths();

		// Summon V-07-TR-0N
		new Summon(S.CONTROLLER, "GVG_111t").Trigger(source);
	}
}

/// <summary>
/// Swap a minion's Attack and Health values.
/// Reads the current (aggregated) ATK and MaxHealth, then writes the
/// swapped values into _fields. Existing buff/enchantment contributions
/// to ATK/Health are zeroed out so the result accurately reflects the swap.
/// DamageTaken is reset because the health baseline changed.
/// Used by: Crazed Alchemist (EX1_059), Reversing Switch, etc.
/// </summary>
public class SwapAtkHealth : TargetedAction
{
	public SwapAtkHealth(object target) : base(target) { }

	public override object? Do(BaseEntity source, BaseEntity target, params object[] args)
	{
		if (target is not Character ch) return null;

		int currentAtk = ch.Atk;          // aggregated: _fields + buffs + slots
		int currentMaxHp = ch.MaxHealth;   // aggregated

		// Zero out ATK/Health modifiers on existing enchantments
		// so they don't stack on top of the swapped base values
		foreach (var buff in ch.Buffs)
		{
			buff.SetIntProperty("atk", 0);
			buff.SetIntProperty("health", 0);
		}

		// Write swapped values into base _fields
		ch.SetIntProperty("atk", currentMaxHp);
		ch.SetIntProperty("health", currentAtk);

		// Reset damage since health baseline changed
		ch.DamageTaken = 0;

		Log.Information("{Target} ATK/Health swapped: {OldAtk}/{OldHp} → {NewAtk}/{NewHp}",
			target, currentAtk, currentMaxHp, currentMaxHp, currentAtk);

		return null;
	}

	public override string ToString() => $"SwapAtkHealth({string.Join(", ", Args)})";
}

// ═══════════════════════════════════════════════════
// RedirectDamageToWeapon — 伤害重定向到武器
// 用于 PreDamageEvent 处理器中（如埃辛诺斯壁垒）
// ═══════════════════════════════════════════════════

/// <summary>
/// When triggered inside a <see cref="PreDamageEvent"/> handler,
/// redirects the damage: the weapon loses 1 durability
/// and the original damage is cancelled.
/// </summary>
public class RedirectDamageToWeapon : TargetedAction
{
	private readonly int _durabilityLoss;

	public RedirectDamageToWeapon(int durabilityLoss = 1) : base()
	{
		_durabilityLoss = durabilityLoss;
	}

	public override object? Do(BaseEntity source, BaseEntity target, params object[] args)
	{
		var preEvt = source.ActivePreDamageEvent;
		if (preEvt == null)
		{
			Log.Warning("RedirectDamageToWeapon: no ActivePreDamageEvent on {Source}", source);
			return null;
		}

		// Find the hero's equipped weapon
		var hero = (source as BuffableEntity)?.Controller?.Hero;
		var weapon = hero?.EquippedWeapon;
		if (weapon == null)
		{
			Log.Debug("RedirectDamageToWeapon: no weapon equipped, damage not redirected");
			return null;
		}

		// Weapon absorbs the hit (can go negative, e.g. Bulwark at 1 dur + counter-attack)
		weapon.LoseDurability(_durabilityLoss);
		preEvt.Redirected = true;

		Log.Information("RedirectDamageToWeapon: {Weapon} loses {Loss} durability instead of {Amount} damage to hero",
			weapon.CardId, _durabilityLoss, preEvt.Amount);

		return null;
	}

	public override string ToString() => $"RedirectDamageToWeapon({_durabilityLoss})";
}

// ─────────────────────────────────────────────
// SwapCards — 交换两张牌的控制者和区域
// ─────────────────────────────────────────────

/// <summary>
/// 交换两张牌的所有者和区域位置。
/// 用于多步发现链的最后一步。
/// <para>用法：</para>
/// <code>new SwapCards(S.PICKED_1, S.PICKED_2)</code>
/// </summary>
public class SwapCards : TargetedAction
{
	private static readonly ILogger Log = LogConfig.ForType<SwapCards>();

	private readonly Selector _sel2;

	public SwapCards(object selector1, Selector selector2) : base(selector1)
	{
		_sel2 = selector2;
	}

	public override object? Do(BaseEntity source, BaseEntity target, params object[] args)
	{
		var entities = source.Game?.Entities ?? new List<BaseEntity>();
		var list2 = _sel2.Eval(entities, source);
		if (list2.Count == 0) return null;

		var card1 = target as BuffableEntity;
		var card2 = list2[0] as BuffableEntity;
		if (card1 == null || card2 == null) return null;

		// 保存原始状态
		var ctrl1 = card1.Controller;
		var zone1 = card1.Zone;
		var ctrl2 = card2.Controller;
		var zone2 = card2.Zone;

		// 交换
		card1.Controller = ctrl2;
		card1.SetZone(zone2);
		card2.Controller = ctrl1;
		card2.SetZone(zone1);

		Log.Information("SwapCards: {Card1} ↔ {Card2}", card1.CardId, card2.CardId);

		// 清理 PickedCards
		source.PickedCards.Clear();

		return null;
	}
}

// ─────────────────────────────────────────────
// MergeCards — 合并两张牌创建融合实体
// ─────────────────────────────────────────────

/// <summary>
/// 将两张 Pick 的卡牌（如两只野兽）合并为一张新卡。
/// 攻击力、生命值、法力值消耗相加；种族和关键词取并集。
/// 新卡加入手牌，PickedCards 清空。
/// <para>用法：</para>
/// <code>new MergeCards(S.PICKED_1, S.PICKED_2)</code>
/// </summary>
public class MergeCards : TargetedAction
{
	private static readonly ILogger Log = LogConfig.ForType<MergeCards>();

	private readonly Selector _sel2;

	public MergeCards(object selector1, Selector selector2) : base(selector1)
	{
		_sel2 = selector2;
	}

	public override object? Do(BaseEntity source, BaseEntity target, params object[] args)
	{
		var entities = source.Game?.Entities ?? new List<BaseEntity>();
		var list2 = _sel2.Eval(entities, source);
		if (list2.Count == 0) return null;

		var card1 = target as Character;
		var card2 = list2[0] as Character;
		if (card1 == null || card2 == null) return null;

		var player = (source as BuffableEntity)?.Controller;
		if (player == null) return null;

		// 以第一张卡为底板创建新实体
		var baseData = card1.CardData;
		if (baseData == null) return null;

		var merged = player.CreateCard(baseData, Zone.SetAside);
		if (merged is not Character mergedCh) return null;

		// 注册 card2 的脚本为额外脚本（合并效果）
		if (card2.CardId != null)
			mergedCh.ExtraScriptIds.Add(card2.CardId);

		// 合并属性：攻击力、生命值、法力消耗相加
		mergedCh.Atk = card1.Atk + card2.Atk;
		mergedCh.MaxHealth = card1.MaxHealth + card2.MaxHealth;
		mergedCh.Cost = card1.Cost + card2.Cost;

		// 合并种族（取并集）
		var mergedRaces = card1.Races.Union(card2.Races).Distinct().ToArray();
		mergedCh.SetRaces(mergedRaces);

		// 合并关键词（从两张卡复制机制标签）
		var mechanicTags = new[]
		{
			GameTag.Taunt, GameTag.DivineShield, GameTag.Stealth,
			GameTag.Windfury, GameTag.Charge, GameTag.Rush,
			GameTag.Lifesteal, GameTag.Poisonous, GameTag.Reborn,
			GameTag.Deathrattle, GameTag.Battlecry, GameTag.Elusive
		};
		foreach (var tag in mechanicTags)
		{
			if (card1.GetBoolProperty(tag) || card2.GetBoolProperty(tag))
			{
				var propName = Managers.CardManager.GetPropertyName(tag);
				if (propName != null)
					mergedCh.SetBoolProperty(propName, true);
			}
		}

		// 放入手牌
		mergedCh.SetZone(Zone.Hand);

		Log.Information("MergeCards: {Card1} + {Card2} → {Merged} ({Atk}/{Health} Cost {Cost})",
			card1.CardId, card2.CardId, mergedCh.CardId,
			mergedCh.Atk, mergedCh.MaxHealth, mergedCh.Cost);

		// 清理 PickedCards
		source.PickedCards.Clear();

		return mergedCh;
	}
}

// ─── Corpse (残骸) Actions ──────────

/// <summary>
/// 给控制者增加指定数量的残骸。
/// <code>new GainCorpses(S.CONTROLLER, 2)</code>
/// </summary>
public class GainCorpses : TargetedAction
{
	public GainCorpses(object target, object amount) : base(target, amount) { }

	public override object? Do(BaseEntity source, BaseEntity target, params object[] args)
	{
		int amount = ActionHelper.ResolveInt(args, 0, source);
		var player = target is Player p ? p : (target as BuffableEntity)?.Controller;
		if (player != null)
		{
			player.GainCorpses(amount);
			Log.Information("{Player} gains {Amount} corpse(s) (total: {Total})",
				player.Name, amount, player.Corpses);
		}
		return null;
	}
}

/// <summary>
/// 消耗指定数量的残骸并执行效果动作。残骸不足则不消耗也不执行。
/// <code>new SpendCorpses(S.CONTROLLER, 3, new DealDamage(S.ENEMY_CHARACTERS, 2))</code>
/// </summary>
public class SpendCorpses : TargetedAction
{
	private readonly int _count;
	private readonly Action[] _actions;

	public SpendCorpses(object target, int count, params Action[] actions) : base(target)
	{
		_count = count;
		_actions = actions;
	}

	public override object? Do(BaseEntity source, BaseEntity target, params object[] args)
	{
		var player = target is Player p ? p : (target as BuffableEntity)?.Controller;
		if (player == null) return null;

		if (player.SpendCorpses(_count) > 0)
		{
			Log.Information("{Player} spends {Count} corpse(s) (remaining: {Remaining})",
				player.Name, _count, player.Corpses);
			foreach (var action in _actions)
			{
				if (action is TargetedAction ta) ta.Trigger(source);
				else if (action is GameAction ga) ga.Trigger(source);
			}
		}
		return null;
	}
}

/// <summary>
/// 消耗残骸决定效果等级：残骸足够时执行增强效果，否则执行基础效果。
/// <code>new IfCorpses(S.CONTROLLER, 4, enhanced, baseline)</code>
/// </summary>
public class IfCorpses : TargetedAction
{
	private readonly int _count;
	private readonly Action _enhanced;
	private readonly Action? _baseline;

	public IfCorpses(object target, int count, Action enhanced, Action? baseline = null) : base(target)
	{
		_count = count;
		_enhanced = enhanced;
		_baseline = baseline;
	}

	public override object? Do(BaseEntity source, BaseEntity target, params object[] args)
	{
		var player = target is Player p ? p : (target as BuffableEntity)?.Controller;
		if (player == null) return null;

		if (player.SpendCorpses(_count) > 0)
		{
			Log.Information("{Player} spends {Count} corpse(s) for enhanced effect", player.Name, _count);
			if (_enhanced is TargetedAction ta) ta.Trigger(source);
			else if (_enhanced is GameAction ga) ga.Trigger(source);
		}
		else
		{
			if (_baseline != null)
			{
				if (_baseline is TargetedAction ta) ta.Trigger(source);
				else if (_baseline is GameAction ga) ga.Trigger(source);
			}
		}
		return null;
	}
}

/// <summary>
/// 消耗所有残骸，每份残骸召唤一个指定随从（最多受战场上限限制）。
/// <code>new SummonPerCorpse(S.CONTROLLER, "RLK_060t")</code>
/// </summary>
public class SummonPerCorpse : TargetedAction
{
	private readonly string _cardId;

	public SummonPerCorpse(object target, string cardId) : base(target)
	{
		_cardId = cardId;
	}

	public override object? Do(BaseEntity source, BaseEntity target, params object[] args)
	{
		var player = target is Player p ? p : (target as BuffableEntity)?.Controller;
		if (player == null) return null;

		int corpses = player.Corpses;
		if (corpses <= 0) return null;
		player.SpendCorpses(corpses);

		int summoned = 0;
		for (int i = 0; i < corpses && player.Field.Count < 7; i++)
		{
			var next = new Summon(target, _cardId);
			next.Trigger(source);
			summoned++;
		}

		Log.Information("{Player} uses {Corpses} corpse(s) to summon {Count} {Card}(s)",
			player.Name, corpses, summoned, _cardId);
		return summoned;
	}
}
