using HearthstoneServer.Entities;

namespace HearthstoneServer.AI;

/// <summary>
/// Simple greedy AI: play cards → attack → end turn.
/// Merged from Console/SimpleAI.cs (more complete) and GameHub.RunAiTurn.
/// Operates directly on Game (no GameController needed — AI holds entity references).
/// </summary>
public class SimpleAI
{
	readonly Game _game;
	readonly Player _aiPlayer;

	/// <summary>Optional callback for logging AI actions (e.g. ConsoleRenderer.ShowAIAction).</summary>
	public Action<string>? OnAction { get; set; }

	public SimpleAI(Game game, Player aiPlayer)
	{
		_game = game;
		_aiPlayer = aiPlayer;
	}

	/// <summary>Execute the AI turn: play cards, attack, end turn.</summary>
	public void PlayTurn()
	{
		OnAction?.Invoke("思考中…");

		// 1) Play cards: low cost first
		PlayCards();

		// 2) Attack with all available minions
		DoAttacks();

		// 3) End turn
		if (!_game.Ended)
		{
			OnAction?.Invoke("结束回合");
			_game.EndTurn();
		}
	}

	internal void PlayCards()
	{
		bool played;
		do
		{
			played = false;

			var playableCards = _aiPlayer.Hand
				.OfType<BuffableEntity>()
				.Where(c => _aiPlayer.Mana >= c.Cost)
				.OrderBy(c => c.Cost)
				.ToList();

			foreach (var card in playableCards)
			{
				if (_game.State == GameState.Complete) return;
				if (!_aiPlayer.Hand.Contains(card)) continue;
				if (_aiPlayer.Mana < card.Cost) continue;

				try
				{
					if (card is Minion)
					{
						if (_aiPlayer.Field.Count >= 7) continue;
						Character? minionTarget = null;
						if (card.RequiresTarget)
						{
							minionTarget = FindSpellTarget(card);
							if (minionTarget == null) continue;
						}
						string cardName = GetCardName(card);
						string tgtInfo = minionTarget != null ? $" → {GetCardName(minionTarget)}" : "";
						OnAction?.Invoke($"出牌: {cardName} ({card.Cost}费){tgtInfo}");
						_game.PlayCard(card, minionTarget);
						played = true;
						break;
					}
					else if (card.Type == CardType.Spell)
					{
						Character? target = FindSpellTarget(card);
						if (card.RequiresTarget && target == null) continue;

						string cardName = GetCardName(card);
						OnAction?.Invoke(target != null
							? $"施法: {cardName} → {GetCardName(target)}"
							: $"施法: {cardName}");
						_game.PlayCard(card, target);
						played = true;
						break;
					}
					else if (card is Weapon)
					{
						string cardName = GetCardName(card);
						OnAction?.Invoke($"装备武器: {cardName}");
						_game.PlayCard(card);
						played = true;
						break;
					}
				}
				catch
				{
					continue;
				}
			}
		} while (played);
	}

	internal void DoAttacks()
	{
		if (_game.State == GameState.Complete) return;

		var opponent = _aiPlayer.Opponent!;

		var attackers = _aiPlayer.Field
			.OfType<Minion>()
			.Where(m => m.CanAttack())
			.ToList();

		foreach (var attacker in attackers)
		{
			if (_game.State == GameState.Complete) return;
			if (!attacker.CanAttack()) continue;

			var target = FindAttackTarget(opponent);
			if (target == null) continue;

			try
			{
				string atkName = GetCardName(attacker);
				string defName = GetCardName(target);
				OnAction?.Invoke($"攻击: {atkName} → {defName}");
				_game.Attack(attacker, target);
				_game.ProcessDeaths();
			}
			catch
			{
				continue;
			}
		}
	}

	internal Character? FindAttackTarget(Player opponent)
	{
		// Must hit taunts first
		var taunts = opponent.Field
			.OfType<Minion>()
			.Where(m => m.Taunt && !m.Dead)
			.ToList();

		if (taunts.Count > 0)
			return taunts.OrderBy(m => m.Health).First();

		// Go face
		if (opponent.Hero != null && !opponent.Hero.Dead)
			return opponent.Hero;

		// Hit minions (lowest health first)
		var minions = opponent.Field
			.OfType<Minion>()
			.Where(m => !m.Dead)
			.OrderBy(m => m.Health)
			.ToList();

		if (minions.Count > 0) return minions[0];
		return opponent.Hero;
	}

	internal Character? FindSpellTarget(BuffableEntity spell)
	{
		var opponent = _aiPlayer.Opponent!;

		// Simple strategy: target enemy hero or highest ATK minion
		if (opponent.Hero != null && !opponent.Hero.Dead)
			return opponent.Hero;

		var enemyMinion = opponent.Field
			.OfType<Minion>()
			.Where(m => !m.Dead)
			.OrderByDescending(m => m.Atk)
			.FirstOrDefault();

		return (Character?)enemyMinion ?? opponent.Hero;
	}

	/// <summary>Get display name for a card entity.</summary>
	private static string GetCardName(BuffableEntity e)
	{
		if (e.CardData == null) return e.CardId ?? "???";
		var cd = e.CardData;
		if (cd.TagsLoc.TryGetValue((GameTag)185, out var locDict))
		{
			if (locDict.TryGetValue("zhCN", out var zhName)) return zhName;
			if (locDict.TryGetValue("enUS", out var enName)) return enName;
		}
		return e.CardId ?? "???";
	}
}
