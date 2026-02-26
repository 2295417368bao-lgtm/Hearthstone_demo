using Serilog;

namespace HearthstoneServer.Managers;

/// <summary>
/// 游戏管理器。
/// 管理实体 ID 计数器、Tag 映射和 Observer 通知。
/// </summary>
public class GameManager : Manager
{
	private static readonly ILogger _log = LogConfig.ForType<GameManager>();
	private int _counter;

	private static readonly Dictionary<GameTag, string?> _map = new()
	{
		[GameTag.CardType] = "type",
		[GameTag.NextStep] = "next_step",
		[GameTag.NumMinionsKilledThisTurn] = "minions_killed_this_turn",
		[GameTag.ProposedAttacker] = "proposed_attacker",
		[GameTag.ProposedDefender] = "proposed_defender",
		[GameTag.State] = "state",
		[GameTag.Step] = "step",
		[GameTag.Turn] = "turn",
		[GameTag.Zone] = "zone",
	};

	protected override Dictionary<GameTag, string?> Map => _map;

	public GameManager(Entities.BaseEntity obj) : base(obj)
	{
		_counter = 1;
		obj.EntityId = _counter;
	}

	// ── 实体注册 ──

	/// <summary>
	/// 注册新实体并分配 EntityId。
	/// </summary>
	public void NewEntity(Entities.BaseEntity entity)
	{
		_counter++;
		entity.EntityId = _counter;

		if (Obj is Entities.Game game)
		{
			game.Entities.Add(entity);
			entity.Game = game;
		}

		_log.Debug("NewEntity id={EntityId} type={Type}", entity.EntityId, entity.Type);
		foreach (var obs in Observers)
			obs.NewEntity(entity);
	}

	// ── 事件通知 ──

	public void ActionStart(BlockType type, Entities.BaseEntity source, int index, Entities.BaseEntity? target)
	{
		foreach (var obs in Observers)
			obs.ActionStart(type, source, index, target);
	}

	public void ActionEnd(BlockType type, Entities.BaseEntity source)
	{
		foreach (var obs in Observers)
			obs.ActionEnd(type, source);
	}

	public void StartGame()
	{
		foreach (var obs in Observers)
			obs.StartGame();
	}

	/// <summary>
	/// 设置游戏步骤，并通知观察者。
	/// </summary>
	public void SetStep(Step step, Step? nextStep = null)
	{
		_log.Debug("SetStep step={Step} next={NextStep}", step, nextStep);
		foreach (var obs in Observers)
			obs.GameStep(step, nextStep);

		if (Obj is Entities.BuffableEntity be)
		{
			be.SetIntProperty("step", (int)step);
			if (nextStep.HasValue)
				be.SetIntProperty("next_step", (int)nextStep.Value);
		}
	}

	/// <summary>
	/// 通知回合开始。
	/// </summary>
	public void NotifyTurn(Entities.BaseEntity player)
	{
		foreach (var obs in Observers)
			obs.Turn(player);
	}

	public void NotifyGameAction(object action, Entities.BaseEntity source, params object[] args)
	{
		foreach (var obs in Observers)
			obs.GameAction(action, source, args);
	}

	public void NotifyTargetedAction(object action, Entities.BaseEntity source, Entities.BaseEntity target, params object[] args)
	{
		foreach (var obs in Observers)
			obs.TargetedAction(action, source, target, args);
	}
}
