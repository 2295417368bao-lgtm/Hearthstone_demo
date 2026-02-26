using HearthstoneServer.Dsl;
using HearthstoneServer.Events;
using Serilog;

namespace HearthstoneServer.Entities;

/// <summary>
/// 回合管理器：从 Game 中提取的回合流转逻辑。
/// 负责 BeginTurn / EndTurn 的全部细节，包括计数器重置、抽牌、
/// 休眠处理、未来随从回归、冻结解除、临时牌/附魔到期等。
/// </summary>
public class TurnManager
{
	private static readonly ILogger _log = LogConfig.ForType<TurnManager>();

	private readonly Game _game;

	public TurnManager(Game game) => _game = game;

	/// <summary>
	/// 开始新回合。
	/// </summary>
	public void BeginTurn(Player player)
	{
		_game.Turn++;
		_game.CurrentPlayer = player;

		foreach (var p in _game.Players)
			p.CardsDrawnThisTurn = 0;

		player.LastTurn = player.Turn;
		player.Turn = _game.Turn;
		player.CardsPlayedThisTurn = 0;
		player.MinionsPlayedThisTurn = 0;
		player.MinionsKilledThisTurn = 0;
		player.Combo = false;
		player.MaxMana += 1;
		player.UsedMana = 0;
		player.OverloadLocked = player.Overloaded;
		player.Overloaded = 0;
		player.ElementalPlayedLastTurn = player.ElementalPlayedThisTurn;
		player.ElementalPlayedThisTurn = 0;

		// 轮转实体历史：当前回合 → 上回合 → 清空
		player.DeathsLastTurn = new(player.DeathsThisTurn);
		player.SpellsCastLastTurn = new(player.SpellsCastThisTurn);
		player.CardsPlayedLastTurnEntities = new(player.CardsPlayedThisTurnEntities);
		player.DeathsThisTurn.Clear();
		player.SpellsCastThisTurn.Clear();
		player.CardsPlayedThisTurnEntities.Clear();
		player.CardsDrawnThisTurnEntities.Clear();

		// Increment turns_in_play for all live entities
		foreach (var entity in _game.LiveEntities)
		{
			if (entity is Character c && entity.Type != CardType.Player)
				c.TurnsInPlay++;
		}

		// Reset attack counters
		foreach (var entity in _game.Characters)
		{
			if (entity is Character c)
			{
				c.NumAttacks = 0;
				c.DamagedThisTurn = 0;
				c.HealedThisTurn = 0;
			}
		}

		// Reset hero power
		if (player.Hero?.Power != null)
		{
			player.Hero.Power.ActivationsThisTurn = 0;
			player.Hero.Power.AdditionalActivationsThisTurn = 0;
		}

		// Tick location cooldowns
		foreach (var entity in player.Field)
		{
			if (entity is Location loc)
				loc.TickCooldown();
		}

		// Draw a card
		player.DrawCard();

		// Process deaths AFTER draw — fatigue damage may kill the hero
		_game.ProcessDeaths();
		if (_game.State == GameState.Complete) return;

		_log.Information("Turn {Turn}: {Player}'s turn begins (mana {Mana}/{MaxMana})",
			_game.Turn, player.Name, player.Mana, player.MaxMana);

		// ── Dormant Processing — 休眠倒计时 + 自动苏醒 ──
		var dormantMinions = player.Field.OfType<Minion>().Where(m => m.Dormant).ToList();
		foreach (var dm in dormantMinions)
		{
			dm.DormantTurns = Math.Max(0, dm.DormantTurns - 1);

			// 倒计时归零 → 苏醒
			if (dm.DormantTurns <= 0)
			{
				dm.Dormant = false;
				dm.TurnsInPlay = 0;

				// 执行苏醒效果
				var dmScript = _game.ScriptRegistry.GetScript(dm.CardId ?? "");
				if (dmScript != null && dmScript.Awaken.Length > 0)
				{
					_game.ActionBlock(dm, dmScript.Awaken.ToList(), BlockType.Power);
				}

				_game.RefreshAuras();
				_game.ProcessDeaths();
			}
		}

		// ── Future Minions — 从未来回归战场 ──
		var dueMinions = player.FutureMinions.Where(f => f.returnTurn <= _game.Turn).ToList();
		if (dueMinions.Count > 0)
		{
			foreach (var (minion, returnTurn) in dueMinions)
			{
				player.FutureMinions.Remove((minion, returnTurn));

				// 战场已满 → 迷失在时空中（不视为死亡、不触发亡语）
				if (player.Field.Count >= 7)
				{
					_log.Information("{Minion} is lost in time — board is full", minion);
					continue;
				}

				// 回归战场（通过 Summon 正式召唤，巨型随从会生成附肢）
				_log.Information("{Minion} returns from the future", minion);
				player.Summon(minion);

				// 召唤疲劳：不能立即攻击
				if (minion is Character ch)
					ch.TurnsInPlay = 0;

				// 发布召唤事件（巨型随从附肢、"每当召唤随从"等触发）
				if (minion is Minion m)
					_game.EventBus.Publish(new MinionSummonedEvent(minion, m), MinionPhase.AfterSummon);
			}

			_game.RefreshAuras();
			_game.ProcessDeaths();
		}

		// Publish to EventBus for When<TurnStartEvent> subscriptions
		_game.EventBus.Publish(new TurnStartEvent(player, player), TurnPhase.Start);
	}

	/// <summary>
	/// 结束当前回合并开始对手回合。
	/// </summary>
	public void EndTurn()
	{
		var currentPlayer = _game.CurrentPlayer;
		if (currentPlayer == null) return;
		if (_game.State == GameState.Complete) return;

		_log.Information("{Player} ends turn {Turn}", currentPlayer.Name, _game.Turn);

		// Publish to EventBus for When<TurnEndEvent> subscriptions
		_game.EventBus.Publish(new TurnEndEvent(currentPlayer, currentPlayer), TurnPhase.End);

		// Clear temp mana
		currentPlayer.TempMana = 0;

		// Unfreeze characters that didn't attack this turn
		foreach (var entity in currentPlayer.Characters)
		{
			if (entity is Character c && c.Frozen)
			{
				if (c.NumAttacks == 0)
				{
					_log.Debug("Freeze fades from {EntityId} (didn't attack)", c.EntityId);
					c.Frozen = false;
				}
			}
		}

		// Reset minions killed counter
		foreach (var p in _game.Players)
			p.MinionsKilledThisTurn = 0;

		// 弃掉 Temporary 临时牌
		var tempCards = currentPlayer.Hand
			.OfType<BuffableEntity>()
			.Where(c => c.Tags.Contains(GameTag.Temporary) && c.Tags[GameTag.Temporary] > 0)
			.ToList();
		var discardAction = new Discard(null!);
		foreach (var temp in tempCards)
		{
			_log.Debug("Temporary card {Card} discarded at end of turn", temp.CardId);
			discardAction.Do(temp, temp);
		}

		// 点燃（Burning）倒计时
		var burningCards = currentPlayer.Hand
			.OfType<BuffableEntity>()
			.Where(c => c.Tags.Contains(GameTag.Burning) && c.Tags[GameTag.Burning] > 0)
			.ToList();
		foreach (var burn in burningCards)
		{
			var remaining = burn.Tags[GameTag.Burning] - 1;
			if (remaining <= 0)
			{
				_log.Debug("Burning card {Card} destroyed (timer expired)", burn.CardId);
				currentPlayer.Hand.Remove(burn);
				burn.Zone = Zone.RemovedFromGame;
			}
			else
			{
				burn.Tags[GameTag.Burning] = remaining;
				_log.Debug("Burning card {Card} timer: {Remaining} turns left", burn.CardId, remaining);
			}
		}

		// 附魔持续时间到期
		var expiredEnchs = new List<Enchantment>();
		foreach (var entity in _game.Entities.OfType<Enchantment>().ToList())
		{
			if (entity.Duration > 0 && entity.Zone == Zone.Play)
			{
				entity.TurnsActive++;
				if (entity.TurnsActive >= entity.Duration)
				{
					_log.Debug("Enchantment {BuffId} on {Owner} expired after {Duration} turns",
						entity.BuffId, entity.Owner?.EntityId, entity.Duration);
					expiredEnchs.Add(entity);
				}
			}
		}
		foreach (var ench in expiredEnchs)
			ench.Remove();

		// Begin opponent's turn
		BeginTurn(currentPlayer.Opponent!);
	}
}
