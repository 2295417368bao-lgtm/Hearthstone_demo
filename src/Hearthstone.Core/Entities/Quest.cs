using HearthstoneServer.Dsl;
using Serilog;

namespace HearthstoneServer.Entities;

// ═══════════════════════════════════════════════════
// Quest — a quest spell card
// ═══════════════════════════════════════════════════

/// <summary>
/// 任务实体。打牌时进入 Zone.Secret（在 secrets 列表最前面）。
/// </summary>
public class Quest : Spell
{
	private static readonly ILogger _log = LogConfig.ForType<Quest>();

	public Quest()
	{
		Type = CardType.Spell;
	}

	/// <summary>任务进度。</summary>
	public int Progress
	{
		get => GetIntProperty("progress");
		set => SetIntProperty("progress", value);
	}

	/// <summary>任务完成所需的进度总量。自动从 CardData 的 QuestProgressTotal (tag 535) 读取。</summary>
	public int ProgressTotal
	{
		get
		{
			var val = GetIntProperty("progress_total");
			if (val > 0) return val;
			// 自动从 CardData 读取
			return CardData?.GetTag(GameTag.QuestProgressTotal) ?? 0;
		}
		set => SetIntProperty("progress_total", value);
	}



	// ── Play override ──

	/// <summary>
	/// Play this quest: pay cost then move to Zone.Secret (not graveyard).
	/// Quests stay in Zone.Secret until completed.
	/// </summary>
	public override void Play(Character? target = null, int index = -1)
	{
		Controller?.PayCost(Cost);
		SetZone(Zone.Play); // Redirects to Zone.Secret via SetZone override
	}

	/// <summary>
	/// Zone.Play → Zone.Secret 重定向。
	/// Quest 插入到 secrets 列表最前面。
	/// </summary>
	public override void SetZone(Zone zone, Player? controller = null)
	{
		if (zone == Zone.Play)
			zone = Zone.Secret;

		base.SetZone(zone, controller);

		// Quest 需要在 secrets 列表最前面（Insert(0)）
		if (zone == Zone.Secret && Controller != null)
		{
			Controller.Secrets.Remove(this);
			Controller.Secrets.Insert(0, this);
		}
	}

	/// <summary>
	/// 只能存在一个任务。
	/// </summary>
	public override bool IsSummonable()
	{
		if (Controller == null) return false;

		// 已有任务则不能再打
		if (Controller.Secrets.Count > 0 && Controller.Secrets[0] is Quest)
			return false;

		if (Controller.Secrets.Count >= Game.MAX_SECRETS_ON_PLAY)
			return false;

		return base.IsSummonable();
	}

	/// <summary>增加任务进度。完成时触发奖励并移到墓地。任务线自动推进下一阶段。</summary>
	public void AddProgress(int amount = 1)
	{
		Progress += amount;
		_log.Debug("Quest {Quest} progress: {Progress}/{Total}", CardId, Progress, ProgressTotal);

		if (Progress >= ProgressTotal)
		{
			_log.Information("Quest {Quest} completed!", CardId);

			// 触发奖励动作 (script.Reward)
			var script = CardScriptRegistry.Instance.GetScript(CardId ?? "");
			if (script != null && script.Reward.Length > 0 && Controller?.Game != null)
			{
				Controller.Game.ActionBlock(this, script.Reward.ToList(),
					BlockType.Power);
			}

			// 任务线自动推进：tag 1089 指定下一阶段 dbfId
			var nextDbfId = CardData?.GetTag(GameTag.QuestRewardDatabaseId) ?? 0;
			if (nextDbfId > 0 && Controller?.Game?.CardDbByDbfId != null
				&& Controller.Game.CardDbByDbfId.TryGetValue(nextDbfId, out var nextCardData))
			{
				_log.Information("Questline {Quest} advances to next stage: {NextId}",
					CardId, nextCardData.CardId);

				var nextQuest = Controller.CreateCard(nextCardData.CardId, Zone.Secret);
				if (nextQuest != null)
				{
					// 注册触发器
					var nextScript = CardScriptRegistry.Instance.GetScript(nextCardData.CardId);
					nextScript?.OnActivate?.Invoke(nextQuest, Controller.Game);
				}
			}

			// 任务完成 → 移到墓地
			SetZone(Zone.Graveyard);
		}
	}
}
