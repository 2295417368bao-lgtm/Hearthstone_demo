using HearthstoneServer.Dsl;
using HearthstoneServer.Entities;
using Serilog;

namespace HearthstoneServer.Events;

// ═══════════════════════════════════════════════════
// EventBus — 中央事件总线
// 按入场顺序排序触发器，深度优先执行
// ═══════════════════════════════════════════════════

/// <summary>
/// A subscription to an event on the EventBus.
/// </summary>
public class Subscription : IDisposable
{
	public BuffableEntity? Subscriber { get; }
	public Func<GameEvent, bool> Condition { get; }
	public Action<GameEvent> Handler { get; }
	public bool Once { get; }
	public Zone[]? ActiveZones { get; }
	public bool IsAlive { get; private set; } = true;

	private readonly EventBus _bus;
	private readonly (Type eventType, object phase) _key;

	public Subscription(
		EventBus bus,
		(Type eventType, object phase) key,
		BuffableEntity? subscriber,
		Func<GameEvent, bool> condition,
		Action<GameEvent> handler,
		bool once,
		Zone[]? activeZones = null)
	{
		_bus = bus;
		_key = key;
		Subscriber = subscriber;
		Condition = condition;
		Handler = handler;
		Once = once;
		ActiveZones = activeZones;
	}

	public void Dispose()
	{
		if (!IsAlive) return;
		IsAlive = false;
		_bus.RemoveSubscription(_key, this);
	}
}

/// <summary>
/// Central event bus. Entities subscribe to game events at specific phases.
/// When events are published, subscribers are invoked in play-order (depth-first).
/// Each trigger wraps its execution in an ActionBlock(Trigger) for correct Block log nesting.
/// </summary>
public class EventBus
{
	private static readonly ILogger _log = Log.ForContext<EventBus>();

	private readonly Game _game;

	// (event type, phase enum value) → list of subscriptions
	private readonly Dictionary<(Type, object), List<Subscription>> _subscriptions = new();

	public EventBus(Game game)
	{
		_game = game;
	}

	/// <summary>
	/// Subscribe to events of type <typeparamref name="TEvent"/> at the given phase.
	/// </summary>
	/// <param name="subscriber">The entity subscribing (used for play-order sorting and lifecycle).</param>
	/// <param name="phase">Phase enum value (e.g. SpellPhase.OnPlay).</param>
	/// <param name="condition">Predicate to filter events. Return true to trigger.</param>
	/// <param name="handler">Action to execute when triggered.</param>
	/// <param name="once">If true, unsubscribe after first trigger (secrets).</param>
	/// <returns>IDisposable to unsubscribe.</returns>
	public IDisposable Subscribe<TEvent>(
		BuffableEntity? subscriber,
		object phase,
		Func<TEvent, bool> condition,
		Action<TEvent> handler,
		bool once = false,
		Zone[]? zones = null) where TEvent : GameEvent
	{
		var key = (typeof(TEvent), phase);
		if (!_subscriptions.ContainsKey(key))
			_subscriptions[key] = new List<Subscription>();

		var sub = new Subscription(
			this, key, subscriber,
			evt => evt is TEvent te && condition(te),
			evt => { if (evt is TEvent te) handler(te); },
			once,
			zones);

		_subscriptions[key].Add(sub);
		_log.Debug("Subscribed {Entity} to {Event}@{Phase} (once={Once})",
			subscriber?.CardId ?? subscriber?.EntityId.ToString() ?? "(global)",
			typeof(TEvent).Name, phase, once);

		return sub;
	}

	/// <summary>
	/// Publish an event at the given phase.
	/// Collects all matching subscribers, sorts by play-order, executes depth-first.
	/// Each trigger is wrapped in a TRIGGER ActionBlock for correct Block log output.
	/// </summary>
	public void Publish<TEvent>(TEvent evt, object phase) where TEvent : GameEvent
	{
		var key = (typeof(TEvent), phase);
		if (!_subscriptions.TryGetValue(key, out var subs)) return;

		// Snapshot and sort by play-order (stable: ties resolved by registration order)
		var queue = subs
			.Where(s => s.IsAlive && IsInActiveZone(s) && s.Condition(evt))
			.OrderBy(s => s.Subscriber?.PlayCounter ?? 0)
			.ToList();

		_log.Debug("Publishing {Event}@{Phase}: {Count} triggers",
			typeof(TEvent).Name, phase, queue.Count);

		foreach (var sub in queue)
		{
			if (!sub.IsAlive) continue; // may have been disposed by a prior trigger

			_log.Debug("  → Trigger on {Entity}",
				sub.Subscriber?.CardId ?? sub.Subscriber?.EntityId.ToString() ?? "(global)");

			// 用 try-catch 包裹，防止单个订阅者异常中断整条事件链
			try
			{
				if (sub.Subscriber != null)
					_game.ActionStart(BlockType.Trigger, sub.Subscriber, -1, null);
				sub.Handler(evt); // 深度优先：可能导致嵌套 Publish 调用
			}
			catch (Exception ex)
			{
				_log.Error(ex, "EventBus 订阅者执行失败: {Entity} @ {Event}",
					sub.Subscriber?.CardId ?? sub.Subscriber?.EntityId.ToString() ?? "(global)",
					typeof(TEvent).Name);
			}
			finally
			{
				if (sub.Subscriber != null)
					_game.ActionEnd(BlockType.Trigger, sub.Subscriber);
			}

			if (sub.Once)
			{
				sub.Dispose();

				// Secret reveal: if subscriber is in Secret zone, move to graveyard
				if (sub.Subscriber?.Zone == Zone.Secret)
				{
					_log.Information("Secret revealed: {Entity}",
						sub.Subscriber.CardId ?? sub.Subscriber.EntityId.ToString());
					sub.Subscriber.SetZone(Zone.Graveyard);
				}
			}
		}
	}

	/// <summary>
	/// Check if a subscriber is in an active zone where it should receive events.
	/// Uses subscription-level ActiveZones if specified; otherwise defaults to Play + Secret.
	/// Players always receive events regardless of zone.
	/// </summary>
	private static bool IsInActiveZone(Subscription sub)
	{
		var entity = sub.Subscriber;
		if (entity is Player) return true;
		if (entity == null) return false;
		var zones = sub.ActiveZones;
		if (zones == null) return entity.Zone == Zone.Play || entity.Zone == Zone.Secret;
		return zones.Contains(entity.Zone);
	}

	/// <summary>
	/// Unsubscribe all subscriptions for the given entity (on silence, death, etc.).
	/// </summary>
	public void UnsubscribeAll(BuffableEntity entity)
	{
		foreach (var (_, subs) in _subscriptions)
		{
			foreach (var sub in subs.ToList())
			{
				if (sub.Subscriber == entity)
					sub.Dispose();
			}
		}
	}

	/// <summary>
	/// Internal: remove a subscription from the list (called by Subscription.Dispose).
	/// </summary>
	internal void RemoveSubscription((Type, object) key, Subscription sub)
	{
		if (_subscriptions.TryGetValue(key, out var subs))
			subs.Remove(sub);
	}
}
