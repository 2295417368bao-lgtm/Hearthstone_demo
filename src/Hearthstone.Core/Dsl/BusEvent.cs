using HearthstoneServer.Entities;
using HearthstoneServer.Events;

namespace HearthstoneServer.Dsl;

/// <summary>触发器所有权过滤。</summary>
public enum TriggerOwner
{
	/// <summary>己方。</summary>
	Own,
	/// <summary>敌方。</summary>
	Enemy,
	/// <summary>自身作为事件主体。</summary>
	Self,
	/// <summary>不限制所有权。</summary>
	Any,
}

/// <summary>触发时机。</summary>
public enum TriggerTiming
{
	/// <summary>事件前，可用于拦截。</summary>
	Before,
	/// <summary>事件进行中。</summary>
	On,
	/// <summary>事件完成后。</summary>
	After,
}

/// <summary>
/// 声明式 EventBus 触发器描述符。
/// 由 CardBuilder 在 OnActivate 时注册到 EventBus。
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

	/// <summary>仅触发一次，常用于奥秘。</summary>
	public BusEvent Once() => new(_register, true, _zones);

	/// <summary>
	/// 包装为奥秘触发。
	/// 激活时会先打上 secret 标记，真正触发时由 BindAndRun 自动 Reveal。
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

	/// <summary>仅在手牌中触发。</summary>
	public BusEvent InHand() => new(_register, _once, new[] { Zone.Hand });
	/// <summary>仅在牌库中触发。</summary>
	public BusEvent InDeck() => new(_register, _once, new[] { Zone.Deck });
	/// <summary>仅在场上触发。</summary>
	public BusEvent InPlay() => new(_register, _once, new[] { Zone.Play });
	/// <summary>在手牌或场上触发。</summary>
	public BusEvent InHandOrPlay() => new(_register, _once, new[] { Zone.Hand, Zone.Play });
	/// <summary>在手牌或牌库中触发。</summary>
	public BusEvent InHandOrDeck() => new(_register, _once, new[] { Zone.Hand, Zone.Deck });
	/// <summary>自定义可触发区域。</summary>
	public BusEvent InZone(params Zone[] zones) => new(_register, _once, zones);

	/// <summary>在实体激活时完成事件订阅注册。</summary>
	internal void Activate(BuffableEntity entity, Game game)
		=> _register(entity, game.EventBus, _once, _zones);

	public static implicit operator BusEvent[](BusEvent single) => new[] { single };

	public const TriggerOwner OWN = TriggerOwner.Own;
	public const TriggerOwner ENEMY = TriggerOwner.Enemy;
	public const TriggerOwner SELF_TRIGGER = TriggerOwner.Self;
	public const TriggerOwner ANY = TriggerOwner.Any;

	public const TriggerTiming BEFORE = TriggerTiming.Before;
	public const TriggerTiming ON = TriggerTiming.On;
	public const TriggerTiming AFTER = TriggerTiming.After;

	/// <summary>简单动作列表版本。</summary>
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
				evt => BindAndRun(evt, self, phase, () =>
				{
					foreach (var action in actions)
						action.Trigger(self);
				}),
				once,
				zones);
		});
	}

	/// <summary>单个 GameAction 版本。</summary>
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
				evt => BindAndRun(evt, self, phase, () => action.Trigger(self)),
				once,
				zones);
		});
	}

	/// <summary>单个 GameAction + 额外条件。</summary>
	public static BusEvent When<TEvent>(
		TriggerOwner owner, TriggerTiming timing, GameAction action,
		Func<TEvent, BuffableEntity, bool> when)
		where TEvent : GameEvent
	{
		var phase = ResolvePhase<TEvent>(timing);
		return new BusEvent((self, bus, once, zones) =>
		{
			bus.Subscribe<TEvent>(
				self, phase,
				evt => OwnerMatches(evt, self, owner)
					&& BindAndCheck(evt, self, phase, () => when(evt, self)),
				evt => BindAndRun(evt, self, phase, () => action.Trigger(self)),
				once,
				zones);
		});
	}

	/// <summary>支持 Action[] 的声明式版本。</summary>
	public static BusEvent When<TEvent>(
		TriggerOwner owner, TriggerTiming timing,
		Dsl.Action[] actions,
		Func<TEvent, BuffableEntity, bool>? when = null)
		where TEvent : GameEvent
	{
		var phase = ResolvePhase<TEvent>(timing);
		return new BusEvent((self, bus, once, zones) =>
		{
			bus.Subscribe<TEvent>(
				self, phase,
				evt => OwnerMatches(evt, self, owner)
					&& BindAndCheck(evt, self, phase, () => when?.Invoke(evt, self) ?? true),
				evt => BindAndRun(evt, self, phase, () =>
				{
					foreach (var action in actions)
					{
						if (action is TargetedAction targeted) targeted.Trigger(self);
						else if (action is GameAction gameAction) gameAction.Trigger(self);
					}
				}),
				once,
				zones);
		});
	}

	/// <summary>复杂逻辑回调版本。</summary>
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
				evt => BindAndRun(evt, self, phase, () => handler(evt, self)),
				once,
				zones);
		});
	}

	/// <summary>TargetedAction[] + Func 条件。</summary>
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
				evt => OwnerMatches(evt, self, owner)
					&& BindAndCheck(evt, self, phase, () => when(evt, self)),
				evt => BindAndRun(evt, self, phase, () =>
				{
					foreach (var action in actions)
						action.Trigger(self);
				}),
				once,
				zones);
		});
	}

	/// <summary>TargetedAction[] + Evaluator 条件。</summary>
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
				evt => OwnerMatches(evt, self, owner)
					&& BindAndCheck(evt, self, phase, () => when.Check(self)),
				evt => BindAndRun(evt, self, phase, () =>
				{
					foreach (var action in actions)
						action.Trigger(self);
				}),
				once,
				zones);
		});
	}

	/// <summary>单个 TargetedAction + Evaluator 条件。</summary>
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
				evt => OwnerMatches(evt, self, owner)
					&& BindAndCheck(evt, self, phase, () => when.Check(self)),
				evt => BindAndRun(evt, self, phase, () => action.Trigger(self)),
				once,
				zones);
		});
	}

	/// <summary>单个 TargetedAction + Func 条件。</summary>
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
				evt => OwnerMatches(evt, self, owner)
					&& BindAndCheck(evt, self, phase, () => when(evt, self)),
				evt => BindAndRun(evt, self, phase, () => action.Trigger(self)),
				once,
				zones);
		});
	}

	/// <summary>
	/// 绑定事件上下文，供 S.EVT_SOURCE / S.EVT_TARGET / EVT_AMOUNT 等 DSL 读取。
	/// 触发完成后会清理绑定状态。
	/// </summary>
	private static void BindAndRun<TEvent>(TEvent evt, BuffableEntity self, object phase, System.Action run)
		where TEvent : GameEvent
	{
		BindContext(evt, self, phase, () =>
		{
			run();
			return true;
		}, revealSecret: true);
	}

	private static bool BindAndCheck<TEvent>(TEvent evt, BuffableEntity self, object phase, Func<bool> check)
		where TEvent : GameEvent
		=> BindContext(evt, self, phase, check, revealSecret: false);

	private static TResult BindContext<TEvent, TResult>(
		TEvent evt, BuffableEntity self, object phase,
		Func<TResult> run, bool revealSecret)
		where TEvent : GameEvent
	{
		self.EventSource = GetEventSource(evt);
		self.EventCard = GetEventCard(evt);
		self.EventTarget = GetSubject(evt);
		self.EventAmount = GetEventAmount(evt);
		if (evt is PreDamageEvent preDamageEvent)
			self.ActivePreDamageEvent = preDamageEvent;
		if (evt is WeaponEquipEvent weaponEquipEvent)
		{
			self.ActiveWeaponEquipEvent = weaponEquipEvent;
			self.ActiveWeaponPhase = phase is WeaponPhase weaponPhase ? weaponPhase : null;
		}
		if (evt is WeaponDurabilityLossEvent durabilityLossEvent)
		{
			self.ActiveWeaponDurabilityLossEvent = durabilityLossEvent;
			self.ActiveWeaponDurabilityLossPhase = phase is WeaponDurabilityLossPhase durabilityLossPhase ? durabilityLossPhase : null;
		}
		try
		{
			if (revealSecret && self.GetBoolProperty("__secret__"))
				new RevealSecret(S.SELF).Trigger(self);
			return run();
		}
		finally
		{
			self.EventSource = null;
			self.EventCard = null;
			self.EventTarget = null;
			self.EventAmount = null;
			self.ActivePreDamageEvent = null;
			self.ActiveWeaponEquipEvent = null;
			self.ActiveWeaponPhase = null;
			self.ActiveWeaponDurabilityLossEvent = null;
			self.ActiveWeaponDurabilityLossPhase = null;
		}
	}

	/// <summary>事件来源实体。</summary>
	private static BaseEntity? GetEventSource(GameEvent evt) => evt switch
	{
		SpellCastEvent e => e.Spell,
		CardPlayedEvent e => e.Card,
		MinionPlayedEvent e => e.Minion,
		AttackEvent e => e.Attacker,
		ProposedAttackEvent e => e.Attacker,
		AfterAttackEvent e => e.Attacker,
		RevealSecretEvent e => e.Secret,
		_ => evt.Source,
	};

	/// <summary>事件携带的卡牌实体。</summary>
	private static BaseEntity? GetEventCard(GameEvent evt) => evt switch
	{
		CardPlayedEvent e => e.Card,
		DrawCardEvent e => e.Card,
		DiscardEvent e => e.Card,
		_ => null,
	};

	/// <summary>事件主数值。</summary>
	private static int? GetEventAmount(GameEvent evt) => evt switch
	{
		DamageEvent e => e.Amount,
		PreDamageEvent e => e.Amount,
		HealEvent e => e.Amount,
		OverhealEvent e => e.Amount,
		GainArmorEvent e => e.Amount,
		OverloadEvent e => e.Amount,
		WeaponDurabilityLossEvent e => e.Amount,
		_ => null,
	};

	/// <summary>根据事件类型和触发时机推导默认 phase。</summary>
	private static object ResolvePhase<TEvent>(TriggerTiming timing) where TEvent : GameEvent
	{
		var eventType = typeof(TEvent);

		if (eventType == typeof(WeaponEquipEvent))
		{
			return timing switch
			{
				TriggerTiming.Before => WeaponPhase.BeforeEquip,
				TriggerTiming.On => WeaponPhase.OnEquip,
				TriggerTiming.After => WeaponPhase.AfterEquip,
				_ => throw new ArgumentOutOfRangeException(nameof(timing)),
			};
		}

		if (eventType == typeof(WeaponDurabilityLossEvent))
		{
			return timing switch
			{
				TriggerTiming.Before => WeaponDurabilityLossPhase.BeforeLoseDurability,
				TriggerTiming.After => WeaponDurabilityLossPhase.AfterLoseDurability,
				_ => throw new ArgumentOutOfRangeException(nameof(timing)),
			};
		}

		if (timing == TriggerTiming.Before)
			throw new ArgumentException($"{eventType.Name} does not support BEFORE timing");

		if (eventType == typeof(SpellCastEvent) || eventType == typeof(CardPlayedEvent))
			return timing == TriggerTiming.After ? SpellPhase.AfterSpell : SpellPhase.OnPlay;

		if (eventType == typeof(MinionPlayedEvent))
			return timing == TriggerTiming.After ? MinionPhase.AfterPlay : MinionPhase.OnPlay;

		if (eventType == typeof(MinionSummonedEvent))
			return timing == TriggerTiming.After ? MinionPhase.AfterSummon : MinionPhase.OnSummon;

		if (eventType == typeof(AttackEvent) || eventType == typeof(ProposedAttackEvent))
			return CombatPhase.Preparation;

		if (eventType == typeof(AfterAttackEvent))
			return CombatPhase.Combat;

		if (eventType == typeof(TurnStartEvent))
			return TurnPhase.Start;

		if (eventType == typeof(TurnEndEvent))
			return TurnPhase.End;

		if (eventType == typeof(StartOfGameEvent))
			return timing == TriggerTiming.After ? StartOfGamePhase.After : StartOfGamePhase.On;

		if (eventType == typeof(DamageEvent)
			|| eventType == typeof(HealEvent)
			|| eventType == typeof(OverhealEvent)
			|| eventType == typeof(PreDamageEvent)
			|| eventType == typeof(DeathEvent)
			|| eventType == typeof(OverloadEvent)
			|| eventType == typeof(RevealSecretEvent)
			|| eventType == typeof(GainArmorEvent)
			|| eventType == typeof(DrawCardEvent)
			|| eventType == typeof(DiscardEvent)
			|| eventType == typeof(SecretPlayedEvent)
			|| eventType == typeof(MillEvent))
			return timing == TriggerTiming.After ? MiscPhase.After : MiscPhase.On;

		if (eventType == typeof(HeroPowerUsedEvent))
			return timing == TriggerTiming.After ? HeroPowerPhase.AfterPlay : HeroPowerPhase.OnPlay;

		throw new ArgumentException($"No default phase mapping for {eventType.Name}");
	}

	/// <summary>判断事件是否满足 OWN / ENEMY / SELF / ANY 条件。</summary>
	private static bool OwnerMatches<TEvent>(TEvent evt, BuffableEntity self, TriggerOwner owner)
		where TEvent : GameEvent
	{
		if (owner == TriggerOwner.Any) return true;
		if (owner == TriggerOwner.Self) return GetSubject(evt) == self;

		var controller = evt switch
		{
			DamageEvent damageEvent => damageEvent.Target.Controller,
			PreDamageEvent preDamageEvent => preDamageEvent.Target.Controller,
			HealEvent healEvent => healEvent.Target.Controller,
			OverhealEvent overhealEvent => overhealEvent.Target.Controller,
			DeathEvent deathEvent => (deathEvent.Dead as BuffableEntity)?.Controller,
			_ => GetController(evt),
		};

		return owner switch
		{
			TriggerOwner.Own => controller == self.Controller,
			TriggerOwner.Enemy => controller != null && controller != self.Controller,
			_ => true,
		};
	}

	/// <summary>推导事件所属控制者。</summary>
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
		StartOfGameEvent e => e.Player,
		DrawCardEvent e => e.Player,
		DiscardEvent e => e.Player,
		HeroPowerUsedEvent e => e.Player,
		OverloadEvent e => e.Player,
		GainArmorEvent e => e.Player,
		WeaponEquipEvent e => e.Player,
		WeaponDurabilityLossEvent e => e.Hero.Controller,
		DamageEvent e => e.Target.Controller,
		PreDamageEvent e => e.Target.Controller,
		HealEvent e => e.Target.Controller,
		OverhealEvent e => e.Target.Controller,
		DeathEvent e => (e.Dead as BuffableEntity)?.Controller,
		RevealSecretEvent e => e.Secret.Controller,
		_ => (evt.Source as BuffableEntity)?.Controller,
	};

	/// <summary>推导事件主体或受影响实体。</summary>
	private static BaseEntity? GetSubject(GameEvent evt) => evt switch
	{
		SpellCastEvent e => e.Target,
		DamageEvent e => e.Target,
		PreDamageEvent e => e.Target,
		HealEvent e => e.Target,
		OverhealEvent e => e.Target,
		GainArmorEvent e => e.Player,
		MinionSummonedEvent e => e.Minion,
		DeathEvent e => e.Dead,
		DrawCardEvent e => e.Card,
		DiscardEvent e => e.Card,
		AfterAttackEvent e => e.Defender,
		AttackEvent e => e.Defender,
		ProposedAttackEvent e => e.Defender,
		WeaponEquipEvent e => e.Weapon,
		WeaponDurabilityLossEvent e => e.Defender,
		_ => evt.Source,
	};
}
