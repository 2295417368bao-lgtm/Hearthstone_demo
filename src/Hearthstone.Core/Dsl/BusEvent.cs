using HearthstoneServer.Entities;
using HearthstoneServer.Events;

namespace HearthstoneServer.Dsl;

// ═══════════════════════════════════════════════════
// BusEvent — 声明式 EventBus 触发器描述符
// ═══════════════════════════════════════════════════
//
// 用法：using static HearthstoneServer.Dsl.BusEvent;
//
//   b.Card("NEW1_012", on: When<SpellCastEvent>(OWN, AFTER, new Enchant(S.SELF, "o")));
//   b.Card("EX1_287", on: When<SpellCastEvent>(ENEMY, ON, handler).Once());
//

// ── 枚举 ──

/// <summary>触发器所有权过滤。</summary>
public enum TriggerOwner
{
	/// <summary>己方（事件发起者 == 我的控制者）</summary>
	Own,
	/// <summary>对手（事件发起者 != 我的控制者）</summary>
	Enemy,
	/// <summary>自身（我是事件的主体/目标）</summary>
	Self,
	/// <summary>任意（不过滤）</summary>
	Any,
}

/// <summary>触发时机。</summary>
public enum TriggerTiming
{
	/// <summary>事件发生时（On Play Phase 等）</summary>
	On,
	/// <summary>事件发生后（After Spell Phase 等）</summary>
	After,
}

// ── 描述符 ──

/// <summary>
/// 声明式 EventBus 触发器。由 CardBuilder 转化为 OnActivate 回调。
/// </summary>
public class BusEvent
{
	private readonly System.Action<BuffableEntity, EventBus, bool, Zone[]?> _register;
	private readonly bool _once;
	private readonly Zone[]? _zones;

	private BusEvent(System.Action<BuffableEntity, EventBus, bool, Zone[]?> register, bool once = false, Zone[]? zones = null)
	{
		_register = register;
		_once = once;
		_zones = zones;
	}


	/// <summary>标记为一次性（奥秘用）。</summary>
	public BusEvent Once() => new(_register, true, _zones);

	/// <summary>
	/// 包装为奥秘触发：动作执行前自动 RevealSecret + 自动 Once()。
	/// 在 Activate 时设置 "__secret__" 标志，BindAndRun 检查此标志自动注入 RevealSecret。
	/// </summary>
	public BusEvent WrapSecret()
	{
		var original = _register;
		return new BusEvent((self, bus, once, zones) =>
		{
			self.SetBoolProperty("__secret__", true);
			original(self, bus, true, zones);
		}, true, _zones);
	}

	// ── 区域声明 ──

	/// <summary>仅在手牌中触发。</summary>
	public BusEvent InHand() => new(_register, _once, new[] { Zone.Hand });

	/// <summary>仅在牌库中触发。</summary>
	public BusEvent InDeck() => new(_register, _once, new[] { Zone.Deck });

	/// <summary>仅在场上触发（默认行为的显式版本）。</summary>
	public BusEvent InPlay() => new(_register, _once, new[] { Zone.Play });

	/// <summary>在手牌或场上触发。</summary>
	public BusEvent InHandOrPlay() => new(_register, _once, new[] { Zone.Hand, Zone.Play });

	/// <summary>在手牌或牌库中触发。</summary>
	public BusEvent InHandOrDeck() => new(_register, _once, new[] { Zone.Hand, Zone.Deck });

	/// <summary>自定义区域列表。</summary>
	public BusEvent InZone(params Zone[] zones) => new(_register, _once, zones);

	/// <summary>在卡牌入场时注册订阅。</summary>
	internal void Activate(BuffableEntity entity, Game game)
		=> _register(entity, game.EventBus, _once, _zones);

	/// <summary>隐式转换：单个 BusEvent → BusEvent[]。</summary>
	public static implicit operator BusEvent[](BusEvent single) => new[] { single };

	// ═══════════════════════════════════════════════
	// 常量 — using static HearthstoneServer.Dsl.BusEvent;
	// ═══════════════════════════════════════════════

	public const TriggerOwner OWN = TriggerOwner.Own;
	public const TriggerOwner ENEMY = TriggerOwner.Enemy;
	public const TriggerOwner SELF_TRIGGER = TriggerOwner.Self;
	public const TriggerOwner ANY = TriggerOwner.Any;

	public const TriggerTiming ON = TriggerTiming.On;
	public const TriggerTiming AFTER = TriggerTiming.After;

	// ═══════════════════════════════════════════════
	// When<T> — 3 种重载
	// ═══════════════════════════════════════════════

	/// <summary>
	/// 模式 1：简单动作列表。
	/// <code>When&lt;SpellCastEvent&gt;(OWN, AFTER, new Enchant(S.SELF, "o"))</code>
	/// </summary>
	public static BusEvent When<TEvent>(
		TriggerOwner owner, TriggerTiming timing, params TargetedAction[] actions)
		where TEvent : GameEvent
	{
		var phase = ResolvePhase<TEvent>(timing);
		return new BusEvent((self, bus, once, zones) =>
		{
			bus.Subscribe<TEvent>(
				self, phase,
				evt => OwnerMatches(evt, self, owner),
				evt => BindAndRun(evt, self, () => { foreach (var a in actions) a.Trigger(self); }),
				once,
				zones);
		});
	}

	/// <summary>
	/// 模式 1b：单个 GameAction（无需 target selector 的动作）。
	/// <code>When&lt;TurnStartEvent&gt;(OWN, ON, new MimironTransform())</code>
	/// </summary>
	public static BusEvent When<TEvent>(
		TriggerOwner owner, TriggerTiming timing, GameAction action)
		where TEvent : GameEvent
	{
		var phase = ResolvePhase<TEvent>(timing);
		return new BusEvent((self, bus, once, zones) =>
		{
			bus.Subscribe<TEvent>(
				self, phase,
				evt => OwnerMatches(evt, self, owner),
				evt => BindAndRun(evt, self, () => action.Trigger(self)),
				once,
				zones);
		});
	}

	/// <summary>
	/// 模式 2：自定义 handler 回调（复杂逻辑）。
	/// <code>When&lt;SpellCastEvent&gt;(ENEMY, ON, (evt, self) =&gt; { ... })</code>
	/// </summary>
	public static BusEvent WhenDo<TEvent>(
		TriggerOwner owner, TriggerTiming timing,
		System.Action<TEvent, BuffableEntity> handler)
		where TEvent : GameEvent
	{
		var phase = ResolvePhase<TEvent>(timing);
		return new BusEvent((self, bus, once, zones) =>
		{
			bus.Subscribe<TEvent>(
				self, phase,
				evt => OwnerMatches(evt, self, owner),
				evt => BindAndRun(evt, self, () => handler(evt, self)),
				once,
				zones);
		});
	}

	/// <summary>
	/// 模式 3：动作列表 + 额外条件（Func）。
	/// <code>When&lt;TurnEndEvent&gt;(OWN, ON, actions, when: (evt, self) =&gt; hasSecret)</code>
	/// </summary>
	public static BusEvent When<TEvent>(
		TriggerOwner owner, TriggerTiming timing,
		TargetedAction[] actions,
		Func<TEvent, BuffableEntity, bool> when)
		where TEvent : GameEvent
	{
		var phase = ResolvePhase<TEvent>(timing);
		return new BusEvent((self, bus, once, zones) =>
		{
			bus.Subscribe<TEvent>(
				self, phase,
				evt => OwnerMatches(evt, self, owner) && when(evt, self),
				evt => BindAndRun(evt, self, () => { foreach (var a in actions) a.Trigger(self); }),
				once,
				zones);
		});
	}

	/// <summary>
	/// 模式 4a：动作列表 + Evaluator 条件。
	/// <code>When&lt;TurnEndEvent&gt;(OWN, ON, actions, when: new Find(S.FRIENDLY_SECRETS))</code>
	/// </summary>
	public static BusEvent When<TEvent>(
		TriggerOwner owner, TriggerTiming timing,
		TargetedAction[] actions,
		Evaluator when)
		where TEvent : GameEvent
	{
		var phase = ResolvePhase<TEvent>(timing);
		return new BusEvent((self, bus, once, zones) =>
		{
			bus.Subscribe<TEvent>(
				self, phase,
				evt => OwnerMatches(evt, self, owner) && when.Check(self),
				evt => BindAndRun(evt, self, () => { foreach (var a in actions) a.Trigger(self); }),
				once,
				zones);
		});
	}

	/// <summary>
	/// 模式 4b：单动作 + Evaluator 条件。
	/// <code>When&lt;TurnEndEvent&gt;(OWN, ON, new Destroy(S.SELF), when: Not(new Find(...)))</code>
	/// </summary>
	public static BusEvent When<TEvent>(
		TriggerOwner owner, TriggerTiming timing,
		TargetedAction action,
		Evaluator when)
		where TEvent : GameEvent
	{
		var phase = ResolvePhase<TEvent>(timing);
		return new BusEvent((self, bus, once, zones) =>
		{
			bus.Subscribe<TEvent>(
				self, phase,
				evt => OwnerMatches(evt, self, owner) && when.Check(self),
				evt => BindAndRun(evt, self, () => action.Trigger(self)),
				once,
				zones);
		});
	}

	/// <summary>
	/// 模式 3b：单动作 + Func 额外条件。
	/// <code>When&lt;DamageEvent&gt;(OWN, ON, new GainArmor(...), when: (evt, self) =&gt; evt.Target is Minion)</code>
	/// </summary>
	public static BusEvent When<TEvent>(
		TriggerOwner owner, TriggerTiming timing,
		TargetedAction action,
		Func<TEvent, BuffableEntity, bool> when)
		where TEvent : GameEvent
	{
		var phase = ResolvePhase<TEvent>(timing);
		return new BusEvent((self, bus, once, zones) =>
		{
			bus.Subscribe<TEvent>(
				self, phase,
				evt => OwnerMatches(evt, self, owner) && when(evt, self),
				evt => BindAndRun(evt, self, () => action.Trigger(self)),
				once,
				zones);
		});
	}

	// ═══════════════════════════════════════════════
	// 事件上下文绑定
	// ═══════════════════════════════════════════════

	/// <summary>
	/// 在调用 action 前绑定事件上下文，调用后清除。
	/// 使 S.EVT_SOURCE / S.EVT_TARGET 在 action 内可用。
	/// 若实体标记了 "__secret__"，则自动先调用 RevealSecret。
	/// </summary>
	private static void BindAndRun<TEvent>(TEvent evt, BuffableEntity self, System.Action run)
		where TEvent : GameEvent
	{
		self.EventSource = GetEventSource(evt);
		self.EventTarget = GetSubject(evt);
		if (evt is PreDamageEvent pde)
			self.ActivePreDamageEvent = pde;
		try
		{
			if (self.GetBoolProperty("__secret__"))
				new RevealSecret(S.SELF).Trigger(self);
			run();
		}
		finally
		{
			self.EventSource = null;
			self.EventTarget = null;
			self.ActivePreDamageEvent = null;
		}
	}

	/// <summary>事件的发起实体（攻击者、法术、打出的随从等）。</summary>
	private static BaseEntity? GetEventSource(GameEvent evt) => evt switch
	{
		SpellCastEvent e => e.Spell,
		CardPlayedEvent e => e.Card,
		MinionPlayedEvent e => e.Minion,
		MinionSummonedEvent e => e.Minion,
		AttackEvent e => e.Attacker,
		ProposedAttackEvent e => e.Attacker,
		AfterAttackEvent e => e.Attacker,
		DamageEvent e => e.Source,
		PreDamageEvent e => e.Source,
		HealEvent e => e.Source,
		DeathEvent e => e.Dead,
		DrawCardEvent e => e.Card,
		RevealSecretEvent e => e.Secret,
		_ => evt.Source,
	};

	// ═══════════════════════════════════════════════
	// 阶段自动推断
	// ═══════════════════════════════════════════════

	private static object ResolvePhase<TEvent>(TriggerTiming timing) where TEvent : GameEvent
	{
		var t = typeof(TEvent);

		if (t == typeof(SpellCastEvent) || t == typeof(CardPlayedEvent))
			return timing == TriggerTiming.After ? SpellPhase.AfterSpell : SpellPhase.OnPlay;

		if (t == typeof(MinionPlayedEvent))
			return timing == TriggerTiming.After ? MinionPhase.AfterPlay : MinionPhase.OnPlay;

		if (t == typeof(MinionSummonedEvent))
			return timing == TriggerTiming.After ? MinionPhase.AfterSummon : MinionPhase.OnSummon;

		if (t == typeof(AttackEvent) || t == typeof(ProposedAttackEvent))
			return CombatPhase.Preparation;

		if (t == typeof(AfterAttackEvent))
			return CombatPhase.Combat;

		if (t == typeof(TurnStartEvent))
			return TurnPhase.Start;

		if (t == typeof(TurnEndEvent))
			return TurnPhase.End;

		if (t == typeof(DamageEvent) || t == typeof(HealEvent)
		|| t == typeof(PreDamageEvent)
		|| t == typeof(DeathEvent) || t == typeof(OverloadEvent)
		|| t == typeof(RevealSecretEvent) || t == typeof(GainArmorEvent)
		|| t == typeof(DrawCardEvent) || t == typeof(SecretPlayedEvent))
			return timing == TriggerTiming.After ? MiscPhase.After : MiscPhase.On;

		if (t == typeof(HeroPowerUsedEvent))
			return timing == TriggerTiming.After ? HeroPowerPhase.AfterPlay : HeroPowerPhase.OnPlay;

		throw new ArgumentException($"No default phase mapping for {t.Name}");
	}

	// ═══════════════════════════════════════════════
	// 所有权匹配
	// ═══════════════════════════════════════════════

	private static bool OwnerMatches<TEvent>(TEvent evt, BuffableEntity self, TriggerOwner owner)
		where TEvent : GameEvent
	{
		if (owner == TriggerOwner.Any) return true;

		if (owner == TriggerOwner.Self)
			return GetSubject(evt) == self;

		// For Damage/Heal/Death events, check the *affected* entity's controller
		// so OWN means "friendly took damage/heal/died"
		var controller = evt switch
		{
			DamageEvent d => d.Target.Controller,
			PreDamageEvent d => d.Target.Controller,
			HealEvent h => h.Target.Controller,
			DeathEvent d => (d.Dead as BuffableEntity)?.Controller,
			_ => GetController(evt),
		};
		return owner switch
		{
			TriggerOwner.Own => controller == self.Controller,
			TriggerOwner.Enemy => controller != null && controller != self.Controller,
			_ => true,
		};
	}

	/// <summary>事件的控制者（谁发起了这个动作）。</summary>
	private static Player? GetController(GameEvent evt) => evt switch
	{
		SpellCastEvent e => e.Spell.Controller,
		CardPlayedEvent e => e.Card.Controller,
		MinionPlayedEvent e => e.Minion.Controller,
		MinionSummonedEvent e => e.Minion.Controller,
		AttackEvent e => e.Attacker.Controller,
		ProposedAttackEvent e => e.Attacker.Controller,
		AfterAttackEvent e => e.Attacker.Controller,
		TurnStartEvent e => e.Player,
		TurnEndEvent e => e.Player,
		DrawCardEvent e => e.Player,
		HeroPowerUsedEvent e => e.Player,
		DamageEvent e => (e.Source as BuffableEntity)?.Controller,
		PreDamageEvent e => (e.Source as BuffableEntity)?.Controller,
		HealEvent e => (e.Source as BuffableEntity)?.Controller,
		DeathEvent e => (e.Dead as BuffableEntity)?.Controller,
		OverloadEvent e => e.Player,
		RevealSecretEvent e => e.Secret.Controller,
		_ => (evt.Source as BuffableEntity)?.Controller,
	};

	/// <summary>事件的主体（谁是目标/受影响者）。</summary>
	private static BaseEntity? GetSubject(GameEvent evt) => evt switch
	{
		DamageEvent e => e.Target,
		PreDamageEvent e => e.Target,
		HealEvent e => e.Target,
		DeathEvent e => e.Dead,
		_ => evt.Source,
	};
}
