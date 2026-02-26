using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using HearthstoneServer;
using HearthstoneServer.Entities;
using HearthstoneServer.PowerHistory;
using HearthstoneServer.Dto;

namespace HearthstoneServer.Web.Hubs;

/// <summary>
/// SignalR hub for real-time game communication.
/// Thin adapter layer: delegates game logic to GameController, CardCatalog, GameSetup.
/// Only handles connection management, broadcasting, and PvP room management.
/// </summary>
public class GameHub : Hub
{
	// In-memory game store — ConnectionId → session (thread-safe)
	private static readonly ConcurrentDictionary<string, HubSession> _sessions = new();
	// PvP room management — RoomCode → session (thread-safe)
	private static readonly ConcurrentDictionary<string, HubSession> _rooms = new();

	// ── Game Creation ──

	/// <summary>Create a new AI game and return initial state.</summary>
	public async Task NewGame(string playerClass = "Mage", string aiClass = "Hunter", string[]? customDeck = null)
	{
		var ctrl = GameSetup.CreateAIGame(playerClass, aiClass, customDeck);
		var session = new HubSession
		{
			Controller = ctrl,
			Mode = "ai",
			AiPlayer = ctrl.Player2,
			Player1ConnectionId = Context.ConnectionId,
		};
		_sessions[Context.ConnectionId] = session;

		var state = ctrl.GetState(ctrl.Player1);
		await Clients.Caller.SendAsync("FullState", state);
	}

	/// <summary>Create a PvP room and wait for opponent.</summary>
	public async Task CreateRoom()
	{
		var rng = new Random();
		string code;
		do { code = rng.Next(1000, 9999).ToString(); } while (_rooms.ContainsKey(code));

		var session = new HubSession
		{
			Mode = "pvp",
			RoomCode = code,
			Player1ConnectionId = Context.ConnectionId,
		};
		_sessions[Context.ConnectionId] = session;
		_rooms[code] = session;

		await Clients.Caller.SendAsync("RoomCreated", code);
	}

	/// <summary>Join an existing PvP room.</summary>
	public async Task JoinRoom(string roomCode)
	{
		if (!_rooms.TryGetValue(roomCode, out var session))
		{
			await Clients.Caller.SendAsync("Error", "房间不存在");
			return;
		}
		if (session.Player2ConnectionId != null)
		{
			await Clients.Caller.SendAsync("Error", "房间已满");
			return;
		}

		session.Player2ConnectionId = Context.ConnectionId;
		_sessions[Context.ConnectionId] = session;

		await Clients.Caller.SendAsync("RoomJoined", roomCode);
		if (session.Player1ConnectionId != null)
			await Clients.Client(session.Player1ConnectionId).SendAsync("OpponentJoined");
	}

	/// <summary>Submit deck for PvP. When both players submit, game starts.</summary>
	public async Task SubmitDeck(string playerClass, string[]? customDeck = null)
	{
		if (!TryGetSession(out var session)) return;
		if (session!.Mode != "pvp") return;

		if (Context.ConnectionId == session.Player1ConnectionId)
		{
			session.PvpP1Class = playerClass;
			session.PvpP1Deck = customDeck;
			session.Player1Ready = true;
		}
		else if (Context.ConnectionId == session.Player2ConnectionId)
		{
			session.PvpP2Class = playerClass;
			session.PvpP2Deck = customDeck;
			session.Player2Ready = true;
		}
		else return;

		await Clients.Caller.SendAsync("DeckSubmitted");

		var otherId = Context.ConnectionId == session.Player1ConnectionId
			? session.Player2ConnectionId : session.Player1ConnectionId;
		if (otherId != null)
			await Clients.Client(otherId).SendAsync("OpponentReady");

		// Both ready → create game
		if (session.Player1Ready && session.Player2Ready
			&& session.Player1ConnectionId != null && session.Player2ConnectionId != null)
		{
			var ctrl = GameSetup.CreatePvPGame(
				session.PvpP1Class, session.PvpP1Deck,
				session.PvpP2Class, session.PvpP2Deck);
			session.Controller = ctrl;

			var state1 = ctrl.GetState(ctrl.Player1);
			var state2 = ctrl.GetState(ctrl.Player2);
			await Clients.Client(session.Player1ConnectionId).SendAsync("FullState", state1);
			await Clients.Client(session.Player2ConnectionId).SendAsync("FullState", state2);
		}
	}

	// ── Card Data (available at load time) ──

	/// <summary>Get all ported cards for a class.</summary>
	public object[] GetAvailableCards(string className) =>
		CardCatalog.GetAvailableCards(className);

	/// <summary>Get ALL card data for frontend lookup.</summary>
	public static object[] GetAllCardDataStatic() =>
		CardCatalog.GetAllCardData();

	/// <summary>Map a single card for browser display.</summary>
	public static object MapCardForBrowser(LoadCardIntoDbf.Card c) =>
		CardCatalog.MapCardForBrowser(c);

	// ── Game Operations ──

	/// <summary>Mulligan: replace selected cards from starting hand.</summary>
	public async Task Mulligan(int[] cardIndices)
	{
		if (!TryGetSession(out var session)) return;
		var ctrl = session!.Controller;
		if (ctrl == null) return;

		var caller = GetCallerPlayer(session);
		if (caller == null) return;

		try
		{
			var history = ctrl.SubmitMulligan(caller, cardIndices);

			// AI 模式：替 AI 也完成 Mulligan
			if (session.Mode == "ai" && !ctrl.Player2MulliganDone)
			{
				var aiReplace = ctrl.Player2.Hand.OfType<BuffableEntity>()
					.Where(c => c.Cost >= 4 && c.CardId != "GAME_005")
					.Select(c => ctrl.Player2.Hand.IndexOf(c))
					.Where(i => i >= 0).ToArray();
				history.AddRange(ctrl.SubmitMulligan(ctrl.Player2, aiReplace));
			}

			await BroadcastActions(session, history);
			await SendStateToBoth(session);
		}
		catch (InvalidActionException ex)
		{
			await Clients.Caller.SendAsync("Error", ex.Message);
		}
	}

	/// <summary>Play a card from hand.</summary>
	public async Task PlayCard(int cardIndex, int[]? targetEntityIds = null)
	{
		if (!TryGetSession(out var session)) return;
		var ctrl = session!.Controller;
		if (ctrl == null) return;

		var player = GetCallerPlayer(session);
		if (player == null) return;

		try
		{
			var history = ctrl.PlayCard(player, cardIndex, targetEntityIds);
			await BroadcastActions(session, history);
			await SendStateToBoth(session);
		}
		catch (InvalidActionException ex)
		{
			await Clients.Caller.SendAsync("Error", ex.Message);
		}
	}

	/// <summary>Attack with a character.</summary>
	public async Task Attack(int attackerEntityId, int defenderEntityId)
	{
		if (!TryGetSession(out var session)) return;
		var ctrl = session!.Controller;
		if (ctrl == null) return;

		var player = GetCallerPlayer(session);
		if (player == null) return;

		try
		{
			var history = ctrl.Attack(player, attackerEntityId, defenderEntityId);
			await BroadcastActions(session, history);
			await SendStateToBoth(session);
		}
		catch (InvalidActionException ex)
		{
			await Clients.Caller.SendAsync("Error", ex.Message);
		}
	}

	/// <summary>Use the hero power.</summary>
	public async Task UseHeroPower(int? targetEntityId = null)
	{
		if (!TryGetSession(out var session)) return;
		var ctrl = session!.Controller;
		if (ctrl == null) return;

		var player = GetCallerPlayer(session);
		if (player == null) return;

		try
		{
			var history = ctrl.UseHeroPower(player, targetEntityId);
			await BroadcastActions(session, history);
			await SendStateToBoth(session);
		}
		catch (InvalidActionException ex)
		{
			await Clients.Caller.SendAsync("Error", ex.Message);
		}
	}

	/// <summary>End the current turn.</summary>
	public async Task EndTurn()
	{
		if (!TryGetSession(out var session)) return;
		var ctrl = session!.Controller;
		if (ctrl == null) return;

		var player = GetCallerPlayer(session);
		if (player == null) return;

		try
		{
			var history = ctrl.EndTurn(player);

			// AI turn (only in AI mode — GameController doesn't care about AI)
			if (session.Mode == "ai" && !ctrl.Game.Ended && ctrl.Game.CurrentPlayer == session.AiPlayer)
			{
				var ai = new HearthstoneServer.AI.SimpleAI(ctrl.Game, session.AiPlayer!);
				ctrl.Game.OnPowerHistory = entry => history.Add(entry);
				try { ai.PlayTurn(); }
				finally { ctrl.Game.OnPowerHistory = null; }
			}

			await BroadcastActions(session, history);
			await SendStateToBoth(session);
		}
		catch (InvalidActionException ex)
		{
			await Clients.Caller.SendAsync("Error", ex.Message);
		}
	}

	/// <summary>Request the full current game state (for reconnect).</summary>
	public async Task GetState()
	{
		if (!TryGetSession(out var session)) return;
		var ctrl = session!.Controller;
		if (ctrl == null) return;
		var viewer = GetCallerPlayer(session) ?? ctrl.Player1;
		var state = ctrl.GetState(viewer);
		await Clients.Caller.SendAsync("FullState", state);
	}

	// ── Helper methods ──

	/// <summary>Determine which Player object the caller controls.</summary>
	private Player? GetCallerPlayer(HubSession session)
	{
		if (session.Controller == null) return null;
		if (session.Mode == "ai") return session.Controller.Player1;
		if (Context.ConnectionId == session.Player1ConnectionId) return session.Controller.Player1;
		if (Context.ConnectionId == session.Player2ConnectionId) return session.Controller.Player2;
		return null;
	}

	/// <summary>Send actions to relevant clients.</summary>
	private async Task BroadcastActions(HubSession session, List<PowerHistoryEntry> history)
	{
		await Clients.Caller.SendAsync("Actions", history);
		if (session.Mode == "pvp")
		{
			var otherId = Context.ConnectionId == session.Player1ConnectionId
				? session.Player2ConnectionId
				: session.Player1ConnectionId;
			if (otherId != null)
				await Clients.Client(otherId).SendAsync("Actions", history);
		}
	}

	/// <summary>Send game state to all relevant clients (each sees their own perspective).</summary>
	private async Task SendStateToBoth(HubSession session)
	{
		var ctrl = session.Controller!;
		if (session.Mode == "ai")
		{
			var state = ctrl.GetState(ctrl.Player1);
			if (session.Player1ConnectionId != null)
				await Clients.Client(session.Player1ConnectionId).SendAsync("FullState", state);
		}
		else
		{
			if (session.Player1ConnectionId != null)
			{
				var s1 = ctrl.GetState(ctrl.Player1);
				await Clients.Client(session.Player1ConnectionId).SendAsync("FullState", s1);
			}
			if (session.Player2ConnectionId != null)
			{
				var s2 = ctrl.GetState(ctrl.Player2);
				await Clients.Client(session.Player2ConnectionId).SendAsync("FullState", s2);
			}
		}
	}

	private bool TryGetSession(out HubSession? session)
	{
		return _sessions.TryGetValue(Context.ConnectionId, out session);
	}

	/// <summary>Clean up on disconnect.</summary>
	public override Task OnDisconnectedAsync(Exception? exception)
	{
		if (_sessions.TryRemove(Context.ConnectionId, out var session))
		{
			if (session.RoomCode != null)
				_rooms.TryRemove(session.RoomCode, out _);
		}
		return base.OnDisconnectedAsync(exception);
	}
}

/// <summary>
/// Lightweight session for SignalR hub.
/// Game logic lives in GameController; this only holds connection metadata.
/// </summary>
public class HubSession
{
	public GameController? Controller { get; set; }
	public Player? AiPlayer { get; set; }         // AI mode: AI player reference
	public string Mode { get; set; } = "ai";       // "ai" or "pvp"
	public string? Player1ConnectionId { get; set; }
	public string? Player2ConnectionId { get; set; }
	public string? RoomCode { get; set; }

	// PvP setup (temporary, before game creation)
	public string PvpP1Class { get; set; } = "Mage";
	public string[]? PvpP1Deck { get; set; }
	public string PvpP2Class { get; set; } = "Mage";
	public string[]? PvpP2Deck { get; set; }
	public bool Player1Ready { get; set; }
	public bool Player2Ready { get; set; }
}
