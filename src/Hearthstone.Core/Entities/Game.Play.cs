using Serilog;
using HearthstoneServer.Dsl;
using HearthstoneServer.Events;

namespace HearthstoneServer.Entities;

// Game partial: Card Playing, Hero Power, Location, Titan, Attack
public partial class Game
{
	/// <summary>
	/// Play a card from a player's hand (backward-compatible single-target overload).
	/// </summary>
	public void PlayCard(BuffableEntity card, Character? target = null, int index = -1)
	{
		PlayCard(card, target != null ? new[] { target } : null, index);
	}

	/// <summary>
	/// Play a card from a player's hand. Supports multiple targets for future cards
	/// (e.g. Chillin' Vol'jin: choose two minions, swap their stats).
	/// Delegates to the entity's Play() method which handles mana payment,
	/// zone transition, and script triggering.
	/// </summary>
	public void PlayCard(BuffableEntity card, Character[]? targets, int index = -1)
	{
		var player = card.Controller;
		if (player == null) return;

		var target = targets != null && targets.Length > 0 ? targets[0] : null;

		_log.Information("Player {Player} plays {Card}", player.Name, card.CardId ?? card.EntityId.ToString());

		// ── Target validation ──
		var script = ScriptRegistry.GetScript(card.CardId ?? "");

		// Validate target requirement
		if (card.RequiresTarget && target == null)
		{
			throw new InvalidActionException(
				$"{card.CardId} requires a target but none was provided");
		}

		// Validate target legality
		if (target != null && card.TargetReq != null)
		{
			if (!Targeting.TargetingRules.IsValidTarget(card, target, card.TargetReq))
			{
				throw new InvalidActionException(
					$"{target} is not a valid target for {card.CardId}");
			}
		}

		// Validate board-state play conditions (e.g. MinEnemyMinions for Multi-Shot)
		if (card.TargetReq != null)
		{
			var req = card.TargetReq;
			var opponent = player.Opponent;
			if (req.MinEnemyMinions > 0 && opponent != null)
			{
				int enemyMinionCount = opponent.Field.Count;
				if (enemyMinionCount < req.MinEnemyMinions)
				{
					throw new InvalidActionException(
						$"{card.CardId} requires at least {req.MinEnemyMinions} enemy minion(s) but found {enemyMinionCount}");
				}
			}
			if (req.MinTotalMinions > 0)
			{
				int totalMinions = player.Field.Count + (player.Opponent?.Field.Count ?? 0);
				if (totalMinions < req.MinTotalMinions)
				{
					throw new InvalidActionException(
						$"{card.CardId} requires at least {req.MinTotalMinions} minion(s) on the board but found {totalMinions}");
				}
			}
			if (req.NeedsWeapon && player.Hero?.EquippedWeapon == null)
			{
				throw new InvalidActionException(
					$"{card.CardId} requires a weapon equipped but none was found");
			}
		}

		// ── 法力校验 ──
		if (!player.CanPayCost(card.Cost))
			throw new InvalidActionException(
				$"Not enough mana to play {card.CardId} (cost={card.Cost}, available={player.Mana})");

		// ── 统计快照（用于异常回滚） ──
		var oldCardsPlayed = player.CardsPlayedThisTurn;
		var oldMinionsPlayed = player.MinionsPlayedThisTurn;
		var oldCombo = player.Combo;
		var oldSpentMana = player.SpentManaOnSpellsThisGame;
		var oldLastCard = player.LastCardPlayed;

		// ── 统计更新（在 SequenceRunner 执行前记录） ──
		player.CardsPlayedThisTurn++;
		if (card is Minion) player.MinionsPlayedThisTurn++;
		player.LastCardPlayed = card;
		player.CardsPlayedThisGame.Add(card);
		if (card.Type == CardType.Spell)
			player.SpentManaOnSpellsThisGame += card.Cost;

		// ── Combo 检测 ──
		bool useCombo = player.Combo
			&& script != null
			&& script.Combo.Length > 0;

		// ── Outcast 检测（打出前卡牌仍在手牌中） ──
		bool isOutcast = false;
		if (script != null && script.Outcast.Length > 0)
		{
			int handIdx = player.Hand.IndexOf(card);
			if (handIdx == 0 || handIdx == player.Hand.Count - 1)
				isOutcast = true;
		}

		// Combo flag: 打出第一张卡牌后激活
		if (player.CardsPlayedThisTurn >= 1)
			player.Combo = true;

		// ── Store all targets on the card entity for S.TARGET / S.TARGET_2 access ──
		if (targets != null && targets.Length > 0)
		{
			card.CurrentTargets = targets.Cast<BaseEntity>().ToList();
		}

		// ── 分派到 SequenceRunner（异常时回滚统计） ──
		try
		{
			var chooseCards = script?.ChooseCards ?? Array.Empty<string>();

			if (chooseCards.Length > 0)
			{
				// Choose One 分支
				bool chooseBoth = player.GetBoolProperty("choose_both");
				if (chooseBoth)
				{
					_log.Information("{Card} plays both choose-one options (ChooseBoth)", card.CardId);
					DispatchToSequence(card, script, target, index, false, isOutcast);
				}
				else
				{
					_log.Information("{Card} has choose-one options: {Options}", card.CardId, chooseCards);
					player.PayCost(card.Cost);
					card.SetZone(Zone.Play);
					card.TurnPlayed = Turn;

					var choiceEntities = new List<BaseEntity>();
					foreach (var choiceId in chooseCards)
					{
						var choiceCard = player.CreateCard(choiceId);
						if (choiceCard != null)
							choiceEntities.Add(choiceCard);
					}
					if (choiceEntities.Count > 0)
						player.Choice = new Dsl.ChooseOneChoice(card, target, choiceEntities);
				}
			}
			else
			{
				// 正常打出（或 Combo）
				DispatchToSequence(card, script, target, index, useCombo, isOutcast);
			}
		}
		catch
		{
			// 回滚统计
			player.CardsPlayedThisTurn = oldCardsPlayed;
			player.MinionsPlayedThisTurn = oldMinionsPlayed;
			player.Combo = oldCombo;
			player.SpentManaOnSpellsThisGame = oldSpentMana;
			player.LastCardPlayed = oldLastCard;
			player.CardsPlayedThisGame.Remove(card);
			throw;
		}
	}

	/// <summary>
	/// 分派到 SequenceRunner 执行正确的阶段流水线。
	/// </summary>
	private void DispatchToSequence(BuffableEntity card, Dsl.ICardScript? script, Character? target, int index, bool useCombo, bool isOutcast = false)
	{
		if (card is Minion minion)
		{
			Sequences.PlayMinion(minion, target, index, script, useCombo, isOutcast);
		}
		else if (card is Secret || card is Quest)
		{
			// Secret/Quest 继承 Spell，但需要走 PlaySecret 路径
			Sequences.PlaySecret(card, script);
		}
		else if (card.Type == CardType.Spell)
		{
			Sequences.PlaySpell(card, target, script, useCombo, isOutcast);
		}
		else if (card is Hero hero)
		{
			Sequences.PlayHeroCard(hero, target, script, useCombo);
		}
		else if (card is Weapon weapon)
		{
			Sequences.PlayWeapon(weapon, target, script);
		}
		else if (card is Location location)
		{
			Sequences.PlayLocation(location, script);
		}
		else
		{
			// Fallback: 未知卡牌类型，走通用逻辑
			var player = card.Controller;
			if (player == null) return;
			player.PayCost(card.Cost);
			card.TurnPlayed = Turn;
			if (script != null && script.Play.Length > 0)
				ActionBlock(card, script.Play.ToList(), BlockType.Power, target: target);
		}
	}

	/// <summary>
	/// Use a player's hero power. Validates playability, pays mana,
	/// and delegates to SequenceRunner for complete event pipeline.
	/// </summary>
	public void UseHeroPower(Player player, Character? target = null)
	{
		var power = player.Hero?.Power;
		if (power == null)
			throw new InvalidActionException("Player has no hero power");

		if (!power.IsUsable)
			throw new InvalidActionException($"Hero power is not usable (exhausted={power.Exhausted}, mana={player.Mana}/{power.Cost})");

		_log.Information("Player {Player} uses hero power {Power}", player.Name, power.CardId ?? power.EntityId.ToString());

		// Pay mana cost
		player.PayCost(power.Cost);

		// Increment activation counter
		power.ActivationsThisTurn++;

		// 委托给 SequenceRunner（完整事件管线：OnPlay → Effect → AfterPlay）
		Sequences.UseHeroPower(player, power, target);
	}

	/// <summary>
	/// Use a location card. Validates usability and triggers the location's effect.
	/// </summary>
	public void UseLocation(Player player, Location location, Character? target = null)
	{
		if (!location.IsUsable)
			throw new InvalidActionException(
				$"Location {location.CardId} is not usable (durability={location.Durability}, cooldown={location.Cooldown})");

		_log.Information("Player {Player} uses location {Location}",
			player.Name, location.CardId ?? location.EntityId.ToString());

		Sequences.UseLocation(player, location, target);
	}

	/// <summary>
	/// Use one of a Titan minion's abilities. Validates the titan and ability index.
	/// </summary>
	public void UseTitanAbility(Player player, Minion titan, int abilityIndex, Character? target = null)
	{
		if (!titan.IsTitan)
			throw new InvalidActionException($"{titan.CardId} is not a Titan");

		if (titan.Silenced)
			throw new InvalidActionException($"Titan {titan.CardId} is silenced");

		if (abilityIndex < 0 || abilityIndex > 2)
			throw new InvalidActionException($"Invalid ability index {abilityIndex} (must be 0-2)");

		bool used = abilityIndex switch
		{
			0 => titan.TitanAbility1Used,
			1 => titan.TitanAbility2Used,
			2 => titan.TitanAbility3Used,
			_ => true
		};
		if (used)
			throw new InvalidActionException($"Titan {titan.CardId} ability {abilityIndex + 1} already used");

		var abilityIds = titan.TitanAbilityCardIds;
		if (abilityIndex >= abilityIds.Length)
			throw new InvalidActionException($"Titan {titan.CardId} has no ability at index {abilityIndex}");

		_log.Information("Player {Player} uses Titan {Titan} ability {Index} ({AbilityId})",
			player.Name, titan.CardId, abilityIndex + 1, abilityIds[abilityIndex]);

		Sequences.UseTitanAbility(player, titan, abilityIndex, target);
	}

	// ── Combat ──

	/// <summary>The proposed attacker (for event interception/redirection).</summary>
	public Character? ProposedAttacker { get; set; }

	/// <summary>The proposed defender (for event interception/redirection).</summary>
	public Character? ProposedDefender { get; set; }

	/// <summary>
	/// Execute an attack action block: attacker trades blows with defender.
	/// 通过 ActionBlock(ATTACK) 包装，确保 _actionStack 正确管理。
	/// Validates attack legality including taunt enforcement.
	/// </summary>
	public void Attack(Character attacker, Character defender)
	{
		// Validate attack legality (includes taunt check via AttackTargets)
		if (!attacker.CanAttack(defender))
		{
			_log.Warning("{Attacker} cannot attack {Defender} (taunt or other restriction)",
				attacker.CardId ?? attacker.EntityId.ToString(),
				defender.CardId ?? defender.EntityId.ToString());
			return;
		}

		_log.Information("{Attacker} attacks {Defender}",
			attacker.CardId ?? attacker.EntityId.ToString(),
			defender.CardId ?? defender.EntityId.ToString());

		// Delegate to SequenceRunner for correct phase pipeline
		Sequences.Combat(attacker, defender);
	}


	/// <summary>
	/// Initialize the game to a mulligan-ready state:
	/// Setup → InitialDraw → GiveCoin.
	/// Callers are responsible for Mulligan + BeginFirstTurn.
	/// </summary>
	public void Initialize()
	{
		Setup();
		InitialDraw();
		GiveCoin(Player2);
	}

	/// <summary>
	/// Full start (no mulligan): Initialize + BeginTurn.
	/// Used by tests and AI solo.
	/// </summary>
	public void Start()
	{
		Initialize();
		BeginTurn(Player1);
	}

	/// <summary>
	/// Draw initial hands: first player draws 3 cards, second player draws 4.
	/// </summary>
	public void InitialDraw()
	{
		_log.Information("Initial draw: {P1} draws 3, {P2} draws 4",
			Player1.Name, Player2.Name);

		for (int i = 0; i < 3; i++)
			Player1.DrawCard();
		for (int i = 0; i < 4; i++)
			Player2.DrawCard();
	}

	/// <summary>
	/// Mulligan: replace selected cards from hand, redraw the same number, shuffle deck.
	/// </summary>
	public void Mulligan(Player player, List<BaseEntity> toReplace)
	{
		if (toReplace.Count == 0) return;

		int count = toReplace.Count;
		_log.Information("{Player} mulligans {Count} cards", player.Name, count);

		// Put cards back into deck (use manual list ops like DrawCard)
		foreach (var card in toReplace)
		{
			player.Hand.Remove(card);
			player.Deck.Add(card);
		}

		// Shuffle deck
		player.ShuffleDeck();

		// Draw replacement cards
		for (int i = 0; i < count; i++)
			player.DrawCard();
	}

	/// <summary>
	/// Give The Coin to the second player (going second).
	/// Creates a Coin entity (CS2_009t) and adds it to the player's hand.
	/// </summary>
	public void GiveCoin(Player player)
	{
		var coinData = new HearthstoneServer.LoadCardIntoDbf.Card
		{
			CardId = "GAME_005",
			CardType = CardType.Spell,
			Cost = 0
		};
		var coin = player.CreateCard(coinData, Zone.Hand);
		if (coin != null)
			_log.Information("{Player} receives The Coin", player.Name);
	}

	// ── Pick first player (CoinRules override) ──

	/// <summary>Randomly determine which player goes first.</summary>
	public virtual (Player first, Player second) PickFirstPlayer()
	{
		// CoinRules: random choice
		if (Random.Next(2) == 0)
			return (Player1, Player2);
		return (Player2, Player1);
	}

	// ── Turn flow ──

	/// <summary>
	/// Begin a new turn for the given player.
	/// 实际逻辑委托给 TurnManager。
	/// </summary>
	public virtual void BeginTurn(Player player) => Turns.BeginTurn(player);

	/// <summary>
	/// End the current turn and begin the opponent's turn.
	/// 实际逻辑委托给 TurnManager。
	/// </summary>
	public virtual void EndTurn() => Turns.EndTurn();

	// ── Death processing ──

	/// <summary>
	/// Check for and process dead entities.
	/// 用 ActionStart/ActionEnd(DEATHS) 包装，防止 ActionEnd→ProcessDeaths 递归。
	/// </summary>
	public void ProcessDeaths()
	{
		if (_processingDeaths) return; // 防止重入
		_processingDeaths = true;

		// 循环：亡语可能导致新死亡，需要反复检查直到无新死亡
		bool hasNewDeaths;
		do
		{
			var deadEntities = new List<IDestroyable>();

			foreach (var entity in LiveEntities)
			{
				if (entity is IDestroyable d && d.Dead)
					deadEntities.Add(d);
			}

			hasNewDeaths = deadEntities.Count > 0;
			if (!hasNewDeaths) break;

			_log.Debug("Processing {Count} deaths", deadEntities.Count);

			// 用 DEATHS 类型包装
			ActionStart(BlockType.Deaths, this, 0, null);

			foreach (var dead in deadEntities)
				dead.OnDeath(this);

			ActionEnd(BlockType.Deaths, this);

		} while (hasNewDeaths);

		_processingDeaths = false;

		// Check for game end after processing deaths
		CheckForEndGame();
	}

	// ── Game-over detection ──

	/// <summary>
	/// Check if one or more players are losing and end the game if so.
	/// </summary>
	public void CheckForEndGame()
	{
		bool gameover = false;

		foreach (var player in Players)
		{
			if (player.PlayState is PlayState.Conceded or PlayState.Disconnected)
				player.PlayState = PlayState.Losing;
			if (player.Hero != null && player.Hero.Dead)
				player.PlayState = PlayState.Losing;
			if (player.PlayState == PlayState.Losing)
				gameover = true;
		}

		if (!gameover) return;

		if (Player1.PlayState == Player2.PlayState)
		{
			// Both losing = tie
			foreach (var player in Players)
				player.PlayState = PlayState.Tied;
		}
		else
		{
			foreach (var player in Players)
			{
				if (player.PlayState == PlayState.Losing)
					player.PlayState = PlayState.Lost;
				else
					player.PlayState = PlayState.Won;
			}
		}

		State = GameState.Complete;
		_log.Information("Game over! P1={P1State}, P2={P2State}",
			Player1.PlayState, Player2.PlayState);
	}

	// ── Skip turn (testing helper) ──

	/// <summary>Skip two turns (current + opponent), returning to current player.</summary>
	public void SkipTurn()
	{
		EndTurn();
		EndTurn();
	}

	// ── CheatAction (testing helper) ──

	/// <summary>
	/// Perform actions as if a card had just triggered them.
	/// Useful for testing and debugging card effects outside normal play flow.
	/// </summary>
	public void CheatAction(BaseEntity source, List<Dsl.Action> actions)
	{
		_log.Debug("CheatAction: triggering {Count} actions from {Source}", actions.Count, source);
		TriggerActions(source, actions);
	}

	// ── Dump ──

	public override Dictionary<string, object> Dump()
	{
		var data = base.Dump();
		data["state"] = (int)State;
		data["step"] = (int)Step;
		data["turn"] = Turn;
		data["current_player"] = CurrentPlayer?.Name ?? "none";
		data["player1"] = Player1.Dump();
		data["player2"] = Player2.Dump();
		return data;
	}
}
