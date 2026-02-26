using Serilog;
using HearthstoneServer.Entities;
using HearthstoneServer.LoadCardIntoDbf;

namespace HearthstoneServer;

/// <summary>
/// Game utility functions: random deck building, game setup, and AI play.
/// </summary>
public static class GameUtils
{
	private static readonly ILogger Log = Serilog.Log.ForContext(typeof(GameUtils));
	private static readonly Random _rng = new();

	/// <summary>
	/// Pick a random playable class (2..10 maps to CardClass enum).
	/// </summary>
	public static CardClass RandomClass(Random? rng = null)
	{
		rng ??= _rng;
		return (CardClass)rng.Next(2, 11);
	}

	/// <summary>
	/// Generate a random 30-card deck for the given class.
	/// Requires a card database to select from.
	/// </summary>
	public static List<string> RandomDraft(
		CardClass cardClass,
		Dictionary<string, Card> cardDb,
		List<string>? exclude = null,
		List<string>? include = null,
		Random? rng = null,
		int maxCards = Deck.DEFAULT_MAX_CARDS)
	{
		rng ??= _rng;
		exclude ??= new List<string>();
		var deck = new List<string>(include ?? new List<string>());

		// Build the collection of eligible cards
		var collection = new List<Card>();
		foreach (var (id, card) in cardDb)
		{
			if (exclude.Contains(id)) continue;
			if (!card.Collectible) continue;
			if (card.CardType == CardType.Hero) continue;
			if (card.CardClass != CardClass.Invalid &&
				card.CardClass != cardClass &&
				card.CardClass != CardClass.Neutral)
				continue;
			collection.Add(card);
		}

		if (collection.Count == 0)
		{
			Log.Warning("No eligible cards found for class {Class}", cardClass);
			return deck;
		}

		// Fill deck to max
		int maxAttempts = maxCards * 100; // prevent infinite loop
		int attempts = 0;
		while (deck.Count < maxCards && attempts < maxAttempts)
		{
			attempts++;
			var card = collection[rng.Next(collection.Count)];
			int countInDeck = deck.Count(c => c == card.CardId);
			int maxCopies = card.Rarity == Rarity.Legendary
				? Deck.MAX_UNIQUE_LEGENDARIES
				: Deck.MAX_UNIQUE_CARDS;
			if (countInDeck < maxCopies)
				deck.Add(card.CardId);
		}

		return deck;
	}

	/// <summary>
	/// Set up a new game with random decks and random classes.
	/// </summary>
	public static Game SetupGame(Dictionary<string, Card>? cardDb = null, Random? rng = null)
	{
		rng ??= _rng;

		var class1 = RandomClass(rng);
		var class2 = RandomClass(rng);

		var deck1 = cardDb != null ? RandomDraft(class1, cardDb, rng: rng) : new List<string>();
		var deck2 = cardDb != null ? RandomDraft(class2, cardDb, rng: rng) : new List<string>();

		var player1 = new Player($"Player1 ({class1})")
		{
			StartingHero = GameSetup.GetHeroId(ClassToName(class1)),
			StartingDeck = deck1,
		};
		var player2 = new Player($"Player2 ({class2})")
		{
			StartingHero = GameSetup.GetHeroId(ClassToName(class2)),
			StartingDeck = deck2,
		};

		var game = new Game(player1, player2);
		game.CardDb = cardDb;

		player1.PrepareForGame();
		player2.PrepareForGame();
		game.Setup();

		Log.Information("Game setup: {P1} vs {P2}", player1.Name, player2.Name);
		return game;
	}

	/// <summary>Convert CardClass enum to class name string.</summary>
	private static string ClassToName(CardClass cc) => cc switch
	{
		CardClass.Mage => "Mage",
		CardClass.Hunter => "Hunter",
		CardClass.Warrior => "Warrior",
		CardClass.Priest => "Priest",
		CardClass.Druid => "Druid",
		CardClass.Warlock => "Warlock",
		CardClass.Paladin => "Paladin",
		CardClass.Rogue => "Rogue",
		CardClass.Shaman => "Shaman",
		_ => "Mage",
	};

	/// <summary>
	/// Play one turn with random AI decisions.
	/// The AI will play affordable cards from hand and attack with ready characters.
	/// </summary>
	public static Game PlayTurn(Game game, Random? rng = null)
	{
		rng ??= _rng;
		var player = game.CurrentPlayer;
		if (player == null || game.Ended)
		{
			Log.Warning("No current player or game ended — skipping turn");
			return game;
		}

		// Play cards from hand (iterate snapshot to avoid modification during iteration)
		var handSnapshot = player.Hand.OfType<BuffableEntity>().ToList();
		foreach (var card in handSnapshot)
		{
			if (game.Ended) break;
			if (card.Cost > player.Mana) continue;
			if (rng.NextDouble() < 0.5) continue; // 50% chance to play

			try
			{
				// For minions, need a free field slot
				if (card.Type == CardType.Minion && player.MinionSlots <= 0) continue;

				game.PlayCard(card);
				Log.Debug("AI played {CardId} (cost={Cost})", card.CardId, card.Cost);
			}
			catch (Exception ex)
			{
				Log.Debug("AI failed to play {CardId}: {Error}", card.CardId, ex.Message);
			}
		}

		// Attack with characters
		var chars = player.Characters.OfType<Character>().ToList();
		foreach (var character in chars)
		{
			if (game.Ended) break;
			if (character.Dead) continue;
			if (character.Atk <= 0) continue;
			if (character.Frozen) continue;

			// Find valid targets (opponent's characters)
			var opponent = player.Opponent;
			if (opponent == null) continue;

			var targets = opponent.Characters.OfType<Character>().Where(t => !t.Dead).ToList();
			if (targets.Count == 0) continue;

			// Must attack taunters first
			var taunters = targets.Where(t => t.Taunt).ToList();
			var target = taunters.Count > 0
				? taunters[rng.Next(taunters.Count)]
				: targets[rng.Next(targets.Count)];

			try
			{
				game.Attack(character, target);
				Log.Debug("AI attacked {Target} with {Attacker}", target.EntityId, character.EntityId);
			}
			catch (Exception ex)
			{
				Log.Debug("AI attack failed: {Error}", ex.Message);
			}
		}

		game.EndTurn();
		return game;
	}

	/// <summary>
	/// Simulate a full game from start to finish with random AI.
	/// Returns the completed game.
	/// </summary>
	public static Game PlayFullGame(Dictionary<string, Card>? cardDb = null, int maxTurns = 200, Random? rng = null)
	{
		rng ??= _rng;
		var game = SetupGame(cardDb, rng);

		int turns = 0;
		while (!game.Ended && turns < maxTurns)
		{
			PlayTurn(game, rng);
			turns++;
		}

		Log.Information("Game completed after {Turns} turns (ended={Ended})", turns, game.Ended);
		return game;
	}
}
