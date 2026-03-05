using System.Collections;
using Serilog;
using HearthstoneServer.Dsl;
using HearthstoneServer.Events;

namespace HearthstoneServer.Entities;

// ═══════════════════════════════════════════════════
// Game — the game state machine
// ═══════════════════════════════════════════════════

/// <summary>
/// Game state enum.
/// </summary>
public enum GameState
{
	Invalid = 0,
	Loading = 1,
	Running = 2,
	Complete = 3,
}

/// <summary>
/// The root game entity that manages turn flow, action queuing,
/// death processing, and game-over detection.
/// </summary>
public partial class Game : BuffableEntity
{
	private static readonly ILogger _log = LogConfig.ForType<Game>();

	// ── Constants ──

	public const int MAX_MINIONS_ON_FIELD = 7;
	public const int MAX_SECRETS_ON_PLAY = 5;

	// ── Players ──

	public Player Player1 { get; private set; } = null!;
	public Player Player2 { get; private set; } = null!;
	public Player[] Players { get; private set; } = Array.Empty<Player>();
	public Player? CurrentPlayer { get; set; }

	// ── State ──

	public GameState State { get; set; } = GameState.Invalid;
	public Step Step { get; set; } = Step.BeginFirst;
	public Step NextStep { get; set; } = Step.BeginShuffle;
	public int Turn { get; set; }
	public int Tick { get; set; }
	public new Zone Zone { get; set; } = Zone.Invalid;

	/// <summary>Whether this game uses Standard format (restricts card pool for Discover).</summary>
	public bool IsStandard { get; set; } = true;

	// ── Random ──

	public Random Random { get; private set; }

	// ── Global entity list ──

	/// <summary>All entities ever created in this game.</summary>
	public new List<BaseEntity> Entities { get; } = new();

	/// <summary>Card database for card lookups by CardId. Used by RandomCardPicker/Discover.</summary>
	private Dictionary<string, LoadCardIntoDbf.Card>? _cardDb;
	public Dictionary<string, LoadCardIntoDbf.Card>? CardDb
	{
		get => _cardDb;
		set
		{
			_cardDb = value;
			if (value != null)
			{
				var byDbf = new Dictionary<int, LoadCardIntoDbf.Card>();
				foreach (var c in value.Values)
				{
					if (c.DbfId != 0)
						byDbf[c.DbfId] = c;
				}
				CardDbByDbfId = byDbf;
			}
			else
			{
				CardDbByDbfId = null;
			}
		}
	}

	/// <summary>Card database indexed by DbfId, for O(1) lookups from tag references. Auto-built from CardDb.</summary>
	public Dictionary<int, LoadCardIntoDbf.Card>? CardDbByDbfId { get; private set; }

	/// <summary>Next available entity ID.</summary>
	private int _nextEntityId = 1;

	// ── Action / Play counter ──

	private const int MaxActionDepth = 10;
	private int _actionStack;
	private bool _processingDeaths;

	// ── Event System (Phase Pipeline) ──

	/// <summary>Central event bus for phase-based triggers.</summary>
	public EventBus EventBus { get; }

	/// <summary>Sequence executor for phased play/attack/heropower flows.</summary>
	public SequenceRunner Sequences { get; }

	/// <summary>回合管理器：BeginTurn / EndTurn 逻辑委托。</summary>
	public TurnManager Turns { get; }

	/// <summary>卡牌脚本注册表（可注入，默认为全局单例）。</summary>
	public CardScriptRegistry ScriptRegistry { get; }

	/// <summary>下一个可用的 PlayCounter 值。</summary>
	public int PlayCounterNext { get; set; }

	// ── PowerHistory (frontend communication) ──

	/// <summary>
	/// Callback invoked for each PowerHistory entry (ActionStart, ActionEnd, TagChange, etc.).
	/// The WebApi layer hooks this to stream events to the frontend.
	/// </summary>
	public Action<PowerHistory.PowerHistoryEntry>? OnPowerHistory { get; set; }

	/// <summary>Emit a PowerHistory entry to the observer.</summary>
	public void EmitPowerHistory(PowerHistory.PowerHistoryEntry entry)
		=> OnPowerHistory?.Invoke(entry);

	// ── Constructor ──

	public Game(Player player1, Player player2, int? seed = null, CardScriptRegistry? registry = null)
	{
		Type = CardType.Game;
		base.Game = this;
		Random = seed.HasValue ? new Random(seed.Value) : new Random();
		ScriptRegistry = registry ?? CardScriptRegistry.Instance;

		EventBus = new EventBus(this);
		Sequences = new SequenceRunner(this, EventBus);
		Turns = new TurnManager(this);

		Player1 = player1;
		Player2 = player2;
		Players = new[] { player1, player2 };

		foreach (var player in Players)
		{
			player.Game = this;
		}

		// Set opponent references
		Player1.Opponent = Player2;
		Player2.Opponent = Player1;
	}

	/// <summary>Parameterless constructor for test scenarios.</summary>
	public Game()
	{
		Type = CardType.Game;
		base.Game = this;
		Random = new Random();
		ScriptRegistry = CardScriptRegistry.Instance;
		EventBus = new EventBus(this);
		Sequences = new SequenceRunner(this, EventBus);
		Turns = new TurnManager(this);
	}

	// ── Entity management ──

	/// <summary>Register a new entity in the game.</summary>
	public int RegisterEntity(BaseEntity entity)
	{
		entity.EntityId = _nextEntityId++;
		entity.Game = this;
		Entities.Add(entity);

		// Auto-initialize Tags if not already set
		entity.EnsureTags();

		// Sync Controller tag for selector compatibility
		if (entity is BuffableEntity be && be.Controller != null)
			entity.Tags![GameTag.Controller] = be.Controller.EntityId;

		return entity.EntityId;
	}

	// ── Queries ──

	/// <summary>True if the game has ended.</summary>
	public bool Ended => State == GameState.Complete;

	/// <summary>All minions on the board (both players).</summary>
	public List<BaseEntity> Board
	{
		get
		{
			var list = new List<BaseEntity>();
			list.AddRange(Player1.Field);
			list.AddRange(Player2.Field);
			list.Sort((a, b) => a.PlayCounter.CompareTo(b.PlayCounter));
			return list;
		}
	}

	/// <summary>All characters (heroes + minions, both players).</summary>
	public List<BaseEntity> Characters
	{
		get
		{
			var list = new List<BaseEntity>();
			list.AddRange(Player1.Characters);
			list.AddRange(Player2.Characters);
			list.Sort((a, b) => a.PlayCounter.CompareTo(b.PlayCounter));
			return list;
		}
	}

	/// <summary>All live entities (field + heroes + weapons, both players).</summary>
	public List<BaseEntity> LiveEntities
	{
		get
		{
			var list = new List<BaseEntity>();
			foreach (var e in Player1.LiveEntities) list.Add(e);
			foreach (var e in Player2.LiveEntities) list.Add(e);
			list.Sort((a, b) => a.PlayCounter.CompareTo(b.PlayCounter));
			return list;
		}
	}

	/// <summary>Total minions killed this turn by both players.</summary>
	public int MinionsKilledThisTurn =>
		Player1.MinionsKilledThisTurn + Player2.MinionsKilledThisTurn;

	/// <summary>All hand entities (both players).</summary>
	public IEnumerable<BaseEntity> Hands
	{
		get
		{
			foreach (var e in Player1.Hand) yield return e;
			foreach (var e in Player2.Hand) yield return e;
		}
	}

	/// <summary>All deck entities (both players).</summary>
	public IEnumerable<BaseEntity> Decks
	{
		get
		{
			foreach (var e in Player1.Deck) yield return e;
			foreach (var e in Player2.Deck) yield return e;
		}
	}

	/// <summary>All active aura buffs in the game (for tick-based cleanup).</summary>
	public List<Auras.AuraBuff> ActiveAuraBuffs { get; } = new();

	// ── ActionBlock 引擎 ──

	/// <summary>
	/// Execute an action block: ActionStart → QueueActions → ActionEnd.
	/// When the action stack reaches 0, auto-calls RefreshAuras + ProcessDeaths.
	/// </summary>
	public List<object?> ActionBlock(BaseEntity source, List<Dsl.Action> actions,
		BlockType blockType = BlockType.Power, int index = -1,
		BaseEntity? target = null, object[]? eventArgs = null)
	{
		// 递归深度保护：防止事件触发链导致 StackOverflow
		if (_actionStack > MaxActionDepth)
		{
			_log.Warning("ActionBlock 递归深度超限 ({Depth})，跳过", _actionStack);
			return new List<object?>();
		}

		var oldTargets = new List<BaseEntity>(source.CurrentTargets);
		source.CurrentTarget = target;

		ActionStart(blockType, source, index, target);
		var ret = actions.Count > 0
			? QueueActions(source, actions, eventArgs)
			: new List<object?>();
		ActionEnd(blockType, source);

		source.CurrentTargets = oldTargets;
		return ret;
	}

	/// <summary>
	/// Start an action block. Increments the action stack (except for PLAY blocks).
	/// Emits PowerBlockStart to the frontend.
	/// </summary>
	public void ActionStart(BlockType type, BaseEntity source, int index, BaseEntity? target)
	{
		if (type != BlockType.Play)
			_actionStack++;
		var cardId = (source as BuffableEntity)?.CardId;
		EmitPowerHistory(new PowerHistory.PowerBlockStart(
			type, source.EntityId, index, target?.EntityId, cardId));
	}

	/// <summary>
	/// End an action block. When action stack reaches 0, auto-refreshes auras and processes deaths.
	/// </summary>
	public void ActionEnd(BlockType type, BaseEntity source)
	{
		if (Ended) return;

		if (type != BlockType.Play)
			_actionStack--;

		EmitPowerHistory(new PowerHistory.PowerBlockEnd(type, source.EntityId));

		if (_actionStack == 0)
		{
			_log.Debug("Empty action stack, refreshing auras and processing deaths");
			RefreshAuras();
			ProcessDeaths();
		}
	}

	/// <summary>
	/// Queue a list of actions for processing. Sets event_args on source during execution.
	/// </summary>
	public List<object?> QueueActions(BaseEntity source, List<Dsl.Action> actions, object[]? eventArgs = null)
	{
		var oldEventArgs = source.EventArgs;
		source.EventArgs = eventArgs;
		var ret = TriggerActions(source, actions);
		source.EventArgs = oldEventArgs;
		return ret;
	}

	/// <summary>
	/// Execute a list of actions from source.
	/// </summary>
	public List<object?> TriggerActions(BaseEntity source, List<Dsl.Action> actions)
	{
		var ret = new List<object?>();
		foreach (var action in actions)
		{
			if (action is Dsl.TargetedAction ta)
			{
				var result = ta.Trigger(source);
				ret.AddRange(result);
			}
			else if (action is Dsl.GameAction ga)
			{
				ga.Trigger(source);
				ret.Add(null);
			}
		}
		return ret;
	}

	/// <summary>
	/// Perform actions as a TRIGGER block (used by entity.trigger_event).
	/// </summary>
	public void TriggerBlock(BaseEntity source, List<Dsl.Action> actions, object[]? eventArgs = null)
	{
		ActionBlock(source, actions, BlockType.Trigger, eventArgs: eventArgs);
	}

	/// <summary>
	/// Perform a joust between a challenger and a defender.
	/// </summary>
	public void Joust(BaseEntity source, BaseEntity? challenger, BaseEntity? defender, Dsl.Action? callback)
	{
		var actions = callback != null ? new List<Dsl.Action> { callback } : new List<Dsl.Action>();
		ActionBlock(source, actions, BlockType.Joust, eventArgs: new object[] { challenger!, defender! });
	}



	/// <summary>
	/// Refresh all auras in the game.
	/// Iterates all entities' update_scripts, sorts by priority, executes, then cleans stale buffs.
	/// </summary>
	public void RefreshAuras()
	{
		// 收集所有 entity 的 update_scripts（按 EntityId 去重，防止重复刷新）
		var refreshQueue = new List<(BuffableEntity entity, Auras.Refresh script)>();
		var seen = new HashSet<int>();

		void CollectFrom(IEnumerable<BaseEntity> source)
		{
			foreach (var entity in source)
			{
				if (entity is BuffableEntity be && seen.Add(be.EntityId))
				{
					foreach (var script in be.UpdateScripts)
						refreshQueue.Add((be, script));
				}
			}
		}

		CollectFrom(Entities);
		CollectFrom(Hands);
		CollectFrom(Decks);

		// 按 priority 排序（数字越小越先执行）
		refreshQueue.Sort((a, b) => a.script.Priority.CompareTo(b.script.Priority));

		// 执行所有 refresh 脚本
		foreach (var (entity, script) in refreshQueue)
			script.Trigger(entity);

		// 清除过时的 aura buff（tick < 当前 tick）
		var toRemove = ActiveAuraBuffs.Where(b => b.Tick < Tick).ToList();
		foreach (var buff in toRemove)
			buff.Remove();

		Tick++;
	}



	/// <summary>
	/// Trade a card: shuffle a Tradeable card from hand into the deck and draw a new card.
	/// Costs 1 mana. The card must have the Tradeable mechanic.
	/// </summary>
	public void TradeCard(BuffableEntity card)
	{
		var player = card.Controller;
		if (player == null) return;

		// Validate Tradeable tag
		if (!card.GetBoolProperty(GameTag.Tradeable))
			throw new InvalidActionException($"{card.CardId} is not Tradeable");

		// Validate mana (costs 1)
		if (!player.CanPayCost(1))
			throw new InvalidActionException("Not enough mana to trade (costs 1)");

		_log.Information("Player {Player} trades {Card}", player.Name, card.CardId);

		// Pay 1 mana
		player.PayCost(1);

		// Remove from hand
		player.Hand.Remove(card);

		// Shuffle into deck at random position
		if (player.Deck.Count > 0)
		{
			int pos = Random.Next(player.Deck.Count + 1);
			player.Deck.Insert(pos, card);
		}
		else
		{
			player.Deck.Add(card);
		}
		card.SetZone(Zone.Deck);

		// Draw a new card
		player.DrawCard();
	}


	// ── Setup & Start ──

	/// <summary>
	/// Initialize the game: set player opponents, zones, prepare for game.
	/// </summary>
	public void Setup()
	{
		_log.Information("Setting up game");
		State = GameState.Running;
		Step = Step.BeginDraw;
		Zone = Zone.Play;

		Player1.Opponent = Player2;
		Player2.Opponent = Player1;

		foreach (var player in Players)
		{
			RegisterEntity(player);
			player.Zone = Zone.Play;

			// Register hero so selectors can find it (skip if already registered by PrepareForGame)
			if (player.Hero != null)
			{
				player.Hero.Controller = player;
				if (!Entities.Contains(player.Hero))
					RegisterEntity(player.Hero);
				player.Hero.Zone = Zone.Play;
			}
		}

		// Pick first player (default: Player1)
		var (first, second) = PickFirstPlayer();
		Player1 = first;
		Player1.FirstPlayer = true;
		Player2 = second;
		Player2.FirstPlayer = false;
		Players = new[] { Player1, Player2 };

		// ── EventBus 历史追踪订阅 ──
		SubscribeHistoryTracking();
	}

	/// <summary>
	/// 订阅 EventBus 事件，自动填充 Player 实体历史列表。
	/// </summary>
	private void SubscribeHistoryTracking()
	{
		EventBus.Subscribe<HealEvent>(null, MiscPhase.On,
			_ => true,
			evt =>
			{
				if (evt.Target is Hero hero && hero.Controller != null)
				{
					hero.Controller.HeroHealedThisTurn = true;
				}
			});

		EventBus.Subscribe<DeathEvent>(null, MiscPhase.On,
			_ => true,
			evt =>
			{
				if (evt.Dead is BuffableEntity be && be.Controller != null)
				{
					be.Controller.DeathsThisTurn.Add(be);
					be.Controller.DeathsThisGame.Add(be);
				}
			});

		EventBus.Subscribe<SpellCastEvent>(null, MiscPhase.On,
			_ => true,
			evt =>
			{
				if (evt.Spell.Controller != null)
				{
					evt.Spell.Controller.SpellsCastThisTurn.Add(evt.Spell);
					evt.Spell.Controller.SpellsCastThisGame.Add(evt.Spell);
				}
			});

		EventBus.Subscribe<CardPlayedEvent>(null, MiscPhase.On,
			_ => true,
			evt =>
			{
				if (evt.Card.Controller != null)
				{
					evt.Card.Controller.CardsPlayedThisTurnEntities.Add(evt.Card);
				}
			});

		EventBus.Subscribe<DrawCardEvent>(null, MiscPhase.On,
			_ => true,
			evt =>
			{
				if (evt.Card != null)
				{
					evt.Player.CardsDrawnThisTurnEntities.Add(evt.Card);
				}
			});
	}

}
