using HearthstoneServer.Entities;
using HearthstoneServer.PowerHistory;

namespace HearthstoneServer;

/// <summary>
/// Public API facade for game operations.
/// Each method validates inputs, executes via the Game engine,
/// and returns collected PowerHistory entries.
/// Does NOT contain AI logic — AI turns are orchestrated by callers.
/// </summary>
public class GameController
{
	public Game Game { get; }
	public Player Player1 { get; }
	public Player Player2 { get; }

	// ── Mulligan 状态 ──
	public bool Player1MulliganDone { get; private set; }
	public bool Player2MulliganDone { get; private set; }
	public bool MulliganDone => Player1MulliganDone && Player2MulliganDone;

	public GameController(Game game, Player player1, Player player2)
	{
		Game = game;
		Player1 = player1;
		Player2 = player2;
	}

	// ── Game Operations ──

	/// <summary>
	/// Play a card from a player's hand. Supports multiple targets.
	/// </summary>
	public List<PowerHistoryEntry> PlayCard(Player player, int cardIndex,
		int[]? targetEntityIds = null, int boardIndex = -1)
	{
		ValidateTurn(player);

		if (cardIndex < 0 || cardIndex >= player.Hand.Count)
			throw new InvalidActionException("无效的手牌编号");

		var card = player.Hand[cardIndex] as BuffableEntity
			?? throw new InvalidActionException("无法打出该卡牌");

		Character[]? targets = ResolveTargets(targetEntityIds);

		return Execute(() => Game.PlayCard(card, targets, boardIndex));
	}

	/// <summary>
	/// Trade a Tradeable card from a player's hand: costs 1 mana,
	/// shuffles the card into the deck, then draws a new card.
	/// </summary>
	public List<PowerHistoryEntry> TradeCard(Player player, int cardIndex)
	{
		ValidateTurn(player);

		if (cardIndex < 0 || cardIndex >= player.Hand.Count)
			throw new InvalidActionException("无效的手牌编号");

		var card = player.Hand[cardIndex] as BuffableEntity
			?? throw new InvalidActionException("无法交易该卡牌");

		return Execute(() => Game.TradeCard(card));
	}

	/// <summary>Attack with a character.</summary>
	public List<PowerHistoryEntry> Attack(Player player, int attackerEntityId, int defenderEntityId)
	{
		ValidateTurn(player);

		var attacker = FindEntity<Character>(attackerEntityId, "无效的攻击者");
		var defender = FindEntity<Character>(defenderEntityId, "无效的目标");

		if (!attacker.CanAttack(defender))
			throw new InvalidActionException("无法攻击该目标");

		return Execute(() => Game.Attack(attacker, defender));
	}

	/// <summary>Use the hero power.</summary>
	public List<PowerHistoryEntry> UseHeroPower(Player player, int? targetEntityId = null)
	{
		ValidateTurn(player);

		Character? target = null;
		if (targetEntityId.HasValue)
			target = FindEntity<Character>(targetEntityId.Value, "无效的英雄技能目标");

		return Execute(() => Game.UseHeroPower(player, target));
	}

	/// <summary>End the current turn.</summary>
	public List<PowerHistoryEntry> EndTurn(Player player)
	{
		ValidateTurn(player);

		return Execute(() => Game.EndTurn());
	}

	/// <summary>
	/// Mulligan: replace selected cards from starting hand.
	/// cardIndices are indices into the player's current hand.
	/// </summary>
	public List<PowerHistoryEntry> Mulligan(Player player, int[] cardIndices)
	{
		var hand = player.Hand.OfType<BuffableEntity>().ToList();
		var toReplace = new List<BaseEntity>();
		foreach (var idx in cardIndices.Distinct())
		{
			if (idx >= 0 && idx < hand.Count)
				toReplace.Add(hand[idx]);
		}

		return Execute(() => Game.Mulligan(player, toReplace));
	}

	/// <summary>
	/// Submit a player's mulligan with guard and auto-transition.
	/// Blocks duplicate submissions, and when both players are done,
	/// automatically triggers BeginFirstTurn.
	/// </summary>
	public List<PowerHistoryEntry> SubmitMulligan(Player player, int[] cardIndices)
	{
		if (player == Player1 && Player1MulliganDone)
			throw new InvalidActionException("Player 1 已完成 Mulligan");
		if (player == Player2 && Player2MulliganDone)
			throw new InvalidActionException("Player 2 已完成 Mulligan");

		var history = Mulligan(player, cardIndices);

		if (player == Player1) Player1MulliganDone = true;
		else Player2MulliganDone = true;

		// 双方都完成后自动开始第一回合
		if (MulliganDone)
			history.AddRange(BeginFirstTurn(Game.CurrentPlayer));

		return history;
	}

	/// <summary>Begin the first turn (called after mulligan). Collects PowerHistory.</summary>
	public List<PowerHistoryEntry> BeginFirstTurn(Player player)
	{
		return Execute(() => Game.BeginTurn(player));
	}

	/// <summary>Get the full game state DTO for a viewer.</summary>
	public object GetState(Player viewer)
	{
		return Dto.GameStateMapper.ToFullState(Game, viewer);
	}

	// ── Private Helpers ──

	/// <summary>Validate that it's the given player's turn.</summary>
	private void ValidateTurn(Player player)
	{
		if (Game.CurrentPlayer != player)
			throw new InvalidActionException("不是你的回合");
	}

	/// <summary>Find an entity by ID, throw if not found.</summary>
	private T FindEntity<T>(int entityId, string errorMessage) where T : class
	{
		return Game.Entities.OfType<T>()
			.FirstOrDefault(e => e is BaseEntity be && be.EntityId == entityId)
			?? throw new InvalidActionException(errorMessage);
	}

	/// <summary>Resolve an array of entity IDs to Character targets.</summary>
	private Character[]? ResolveTargets(int[]? targetEntityIds)
	{
		if (targetEntityIds == null || targetEntityIds.Length == 0)
			return null;

		return targetEntityIds.Select(id =>
			FindEntity<Character>(id, $"无效目标 {id}"))
			.ToArray();
	}

	/// <summary>
	/// Execute an action while collecting PowerHistory entries.
	/// Unified helper that sets up and tears down the OnPowerHistory callback.
	/// </summary>
	private List<PowerHistoryEntry> Execute(Action action)
	{
		var history = new List<PowerHistoryEntry>();
		Game.OnPowerHistory = entry => history.Add(entry);

		try
		{
			action();
		}
		finally
		{
			Game.OnPowerHistory = null;
		}

		return history;
	}
}
