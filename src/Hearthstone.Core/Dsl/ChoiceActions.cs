using Serilog;
using HearthstoneServer.Entities;
using HearthstoneServer.Targeting;

namespace HearthstoneServer.Dsl;

// ═══════════════════════════════════════════════════
// Choice pipeline — Choice / GenericChoice / Discover
// ═══════════════════════════════════════════════════

/// <summary>
/// Present a set of cards for the player to choose from.
/// Sets player.Choice and blocks until Choose() is called.
/// </summary>
public class Choice : TargetedAction
{
	private static readonly ILogger _log = LogConfig.ForType<Choice>();

	// ── 选择状态 ──

	/// <summary>触发此选择的源实体。</summary>
	public BaseEntity? Source { get; set; }

	/// <summary>进行选择的玩家。</summary>
	public Player? SourcePlayer { get; set; }

	/// <summary>可供选择的卡牌列表。</summary>
	public List<BaseEntity> Cards { get; set; } = new();

	/// <summary>最少选几张。</summary>
	public int MinCount { get; set; } = 1;

	/// <summary>最多选几张。</summary>
	public int MaxCount { get; set; } = 1;

	/// <summary>保存的 callback（choose 时触发）。</summary>
	private Action[] _savedCallback = Array.Empty<Action>();

	// ── 构造 ──

	public Choice(object target, object cards) : base(target, cards) { }
	public Choice(object target) : base(target) { }
	protected Choice() { }

	// ── Action 执行 ──

	/// <summary>
	/// 解析 cards 参数（支持 Selector / LazyValue / List&lt;BaseEntity&gt; / List&lt;string&gt;）。
	/// </summary>
	public override object[] GetTargetArgs(BaseEntity source, BaseEntity target)
	{
		if (Args.Length < 2) return Array.Empty<object>();
		var cardsArg = Args[1];

		List<BaseEntity> cards;
		if (cardsArg is Selector sel)
		{
			cards = sel.Eval(source.Game?.Entities ?? new List<BaseEntity>(), source);
		}
		else if (cardsArg is LazyValue lv)
		{
			var result = lv.Evaluate(source);
			cards = EvalToEntityList(result);
		}
		else if (cardsArg is List<BaseEntity> entityList)
		{
			cards = new List<BaseEntity>(entityList);
		}
		else if (cardsArg is List<string> idList)
		{
			cards = new List<BaseEntity>();
			var controller = (source as BuffableEntity)?.Controller;
			if (controller != null)
			{
				foreach (var id in idList)
				{
					var created = controller.CreateCard(id);
					if (created != null) cards.Add(created);
				}
			}
		}
		else
		{
			cards = new List<BaseEntity>();
		}

		return new object[] { cards };
	}

	/// <summary>
	/// TargetedAction.Do 实现。解包 args 并调用 typed Do。
	/// </summary>
	public override object? Do(BaseEntity source, BaseEntity target, params object[] args)
	{
		var player = target as Player ?? (target as BuffableEntity)?.Controller;
		var cards = args.Length > 0 && args[0] is List<BaseEntity> list ? list : new List<BaseEntity>();
		DoChoice(source, player, cards);
		return null;
	}

	/// <summary>
	/// 执行选择：设置 player.Choice 阻塞等待 Choose()。
	/// </summary>
	public virtual void DoChoice(BaseEntity source, Player? player, List<BaseEntity> cards)
	{
		if (cards.Count == 0 || player == null) return;

		_log.Information("{Player} choice from {Cards}", player.Name, cards);
		player.Choice = this;
		_savedCallback = Callback;
		Callback = Array.Empty<Action>();
		Source = source;
		SourcePlayer = player;
		Cards = cards;
		MinCount = 1;
		MaxCount = 1;
	}

	/// <summary>
	/// 玩家选择一张卡。验证有效性 → 清除 player.Choice → 触发 callback。
	/// </summary>
	public virtual void Choose(BaseEntity card)
	{
		if (!Cards.Contains(card))
			throw new InvalidActionException(
				$"{card} is not a valid choice (one of {string.Join(", ", Cards)})");

		if (SourcePlayer != null) SourcePlayer.Choice = null;

		// 触发 callback，传递 [cards, card] 参数
		if (Source?.Game != null)
		{
			foreach (var action in _savedCallback)
			{
				Source.Game.TriggerBlock(Source,
					new List<Action> { action },
					new object[] { Cards, card });
			}
		}

		Callback = _savedCallback;
	}

	// ── 辅助 ──

	/// <summary>将 LazyValue 结果转换为 entity 列表。</summary>
	public static List<BaseEntity> EvalToEntityList(object? result)
	{
		if (result is List<BaseEntity> list) return list;
		if (result is BaseEntity e) return new List<BaseEntity> { e };
		if (result is IEnumerable<BaseEntity> en) return new List<BaseEntity>(en);
		return new List<BaseEntity>();
	}

	public override string ToString() => $"<Choice: {Cards.Count} cards>";
}

/// <summary>
/// Generic choice: after choosing, selected card goes to hand/play,
/// unselected cards are discarded.
/// </summary>
public class GenericChoice : Choice
{
	private static readonly ILogger _log = LogConfig.ForType<GenericChoice>();

	public GenericChoice(object target, object cards) : base(target, cards) { }
	public GenericChoice(object target) : base(target) { }
	protected GenericChoice() { }

	public override void Choose(BaseEntity card)
	{
		base.Choose(card);

		// 选中 → 放入 hand（HeroPower → play），未选中 → discard
		foreach (var c in Cards)
		{
			if (c is BuffableEntity be)
			{
				if (ReferenceEquals(c, card))
				{
					if (c is HeroPower)
					{
						be.SetZone(Zone.Play);
					}
					else if (SourcePlayer != null && SourcePlayer.Hand.Count < SourcePlayer.MaxHandSize)
					{
						be.SetZone(Zone.Hand);
					}
					else
					{
						be.SetZone(Zone.Graveyard); // discard - hand full
					}
				}
				else
				{
					be.SetZone(Zone.Graveyard); // discard unchosen
				}
			}
		}
	}

	public override string ToString() => $"<GenericChoice: {Cards.Count} cards>";
}

/// <summary>
/// Scry 选择：检视牌库顶部 N 张牌，选一张置于牌库顶，其余放回。
/// 用法：new ScryChoice(S.CONTROLLER, 3)
/// </summary>
public class ScryChoice : Choice
{
	private static readonly ILogger _log = LogConfig.ForType<ScryChoice>();

	private readonly int _count;

	public ScryChoice(object target, int count = 3) : base(target) { _count = count; }

	public override object? Do(BaseEntity source, BaseEntity target, params object[] args)
	{
		var player = target as Player ?? (target as BuffableEntity)?.Controller;
		if (player == null || player.Deck.Count == 0) return null;

		// 取牌库顶部 N 张
		var topN = player.Deck.Take(_count).ToList();
		foreach (var c in topN) player.Deck.Remove(c);

		_log.Information("{Player} scries top {N}: {Cards}", player.Name, _count, topN);

		Source = source;
		SourcePlayer = player;
		Cards = topN;
		MinCount = 1;
		MaxCount = 1;
		player.Choice = this;

		return null;
	}

	public override void Choose(BaseEntity card)
	{
		if (!Cards.Contains(card))
			throw new InvalidActionException(
				$"{card} is not a valid choice (one of {string.Join(", ", Cards)})");

		if (SourcePlayer != null) SourcePlayer.Choice = null;

		// 未选中的放回牌库并洗入
		foreach (var c in Cards)
		{
			if (!ReferenceEquals(c, card) && SourcePlayer != null)
				SourcePlayer.Deck.Add(c);
		}
		SourcePlayer?.ShuffleDeck();

		// 选中的插入牌库顶部
		SourcePlayer?.Deck.Insert(0, card);

		_log.Information("{Player} scried {Card} to top of deck", SourcePlayer?.Name, card);
	}

	public override string ToString() => $"<ScryChoice: top {_count}>";
}

/// <summary>
/// Discover: present 3 random cards matching a filter for the player to choose from.
/// </summary>
public class Discover : TargetedAction
{
	private static readonly ILogger _log = LogConfig.ForType<Discover>();

	// ── 选择状态（与 Choice 类似但独立管理） ──

	public BaseEntity? DiscoverSource { get; set; }
	public Player? DiscoverPlayer { get; set; }
	public BaseEntity? DiscoverTarget { get; set; }
	public List<BaseEntity> DiscoverCards { get; set; } = new();
	public int MinCount { get; set; } = 1;
	public int MaxCount { get; set; } = 1;

	// ── 构造 ──

	public DiscoverMode Mode { get; set; } = DiscoverMode.Create;

	public Discover(object target, object picker) : base(target, picker) { }
	public Discover(object target) : base(target) { }
	protected Discover() { }

	/// <summary>
	/// 使用 RandomCardPicker 生成 3 张候选卡。
	/// </summary>
	public override object[] GetTargetArgs(BaseEntity source, BaseEntity target)
	{
		if (Args.Length < 2) return Array.Empty<object>();
		var picker = Args[1];

		List<BaseEntity> cards;
		if (picker is RandomCardPicker rcp)
		{
			var tripled = rcp.WithCount(3);
			var result = tripled.Evaluate(source);
			cards = Choice.EvalToEntityList(result);
		}
		else if (picker is Selector sel)
		{
			// 从指定区域（牌库/手牌）选出实体，随机取3张
			var all = source.Game?.Entities ?? new List<BaseEntity>();
			var pool = sel.Eval(all, source);
			var rng = new Random();
			// Fisher-Yates shuffle, take min(3, pool.Count)
			int count = Math.Min(3, pool.Count);
			for (int i = 0; i < count; i++)
			{
				int j = rng.Next(i, pool.Count);
				(pool[i], pool[j]) = (pool[j], pool[i]);
			}
			cards = pool.Take(count).ToList();
		}
		else if (picker is List<BaseEntity> entityList)
		{
			cards = new List<BaseEntity>(entityList);
		}
		else
		{
			cards = new List<BaseEntity>();
		}

		return new object[] { cards };
	}

	/// <summary>
	/// TargetedAction.Do 实现。
	/// </summary>
	public override object? Do(BaseEntity source, BaseEntity target, params object[] args)
	{
		var cards = args.Length > 0 && args[0] is List<BaseEntity> list ? list : new List<BaseEntity>();

		_log.Information("{Source} discovers {Cards} for {Target}", source, cards, target);
		DiscoverCards = cards;
		DiscoverSource = source;
		DiscoverTarget = target;

		var player = (source as BuffableEntity)?.Controller;
		if (player == null) return null;

		DiscoverPlayer = player;

		// 创建 DiscoverChoiceWrapper 存入 player.Choice
		var choiceWrapper = new DiscoverChoiceWrapper(this, source, player, target, cards, Mode);
		player.Choice = choiceWrapper;

		return null;
	}

	public override string ToString() => $"<Discover: {DiscoverCards.Count} cards>";
}

/// <summary>
/// Internal Choice wrapper used by Discover to handle the choose flow.
/// Stored in player.Choice and delegates Choose() back to the Discover context.
/// </summary>
internal class DiscoverChoiceWrapper : Choice
{
	private static readonly ILogger _log = LogConfig.ForType<DiscoverChoiceWrapper>();
	private readonly Discover _discover;
	private readonly BaseEntity _source;
	private readonly BaseEntity? _discoverTarget;
	private readonly DiscoverMode _mode;

	internal DiscoverChoiceWrapper(Discover discover, BaseEntity source,
		Player player, BaseEntity target, List<BaseEntity> cards, DiscoverMode mode)
	{
		_discover = discover;
		_source = source;
		_discoverTarget = target;
		_mode = mode;
		Source = source;
		SourcePlayer = player;
		Cards = cards;
		MinCount = 1;
		MaxCount = 1;
	}

	public override void Choose(BaseEntity card)
	{
		if (!Cards.Contains(card))
			throw new InvalidActionException(
				$"{card} is not a valid choice (one of {string.Join(", ", Cards)})");

		if (SourcePlayer != null) SourcePlayer.Choice = null;

		// 根据模式处理选中的卡牌
		switch (_mode)
		{
			case DiscoverMode.Create:
				// 默认：卡牌已由 RandomCardPicker 生成，加入手牌
				if (card is BuffableEntity createBe && SourcePlayer != null)
				{
					createBe.Controller = SourcePlayer;
					createBe.SetZone(Zone.Hand);
					_log.Information("Discover Create: {Card} added to {Player} hand",
						createBe.CardId, SourcePlayer.Name);
				}
				break;

			case DiscoverMode.Copy:
				// 复制：原牌不动，复制品加入手牌
				if (card is BuffableEntity copyBe && SourcePlayer != null && copyBe.CardId != null)
				{
					var copy = SourcePlayer.CreateCard(copyBe.CardId);
					if (copy is BuffableEntity copyEntity)
					{
						copyEntity.Controller = SourcePlayer;
						copyEntity.SetZone(Zone.Hand);
						_log.Information("Discover Copy: {Card} copied to {Player} hand",
							copyEntity.CardId, SourcePlayer.Name);
					}
				}
				break;

			case DiscoverMode.Draw:
				// 抽牌：从原牌库移到己方手牌（支持从敌方牌库抽）
				if (card is BuffableEntity drawBe && SourcePlayer != null)
				{
					// 从卡牌当前所属的牌库中移除
					drawBe.Controller?.Deck.Remove(drawBe);
					drawBe.Controller = SourcePlayer;
					drawBe.SetZone(Zone.Hand);
					_log.Information("Discover Draw: {Card} drawn to {Player} hand",
						drawBe.CardId, SourcePlayer.Name);
				}
				break;

			case DiscoverMode.Pick:
				// 只记录，不移动卡牌
				_source.PickedCards.Add(card);
				_log.Information("Discover Pick: {Card} stored as pick #{N}",
					(card as BuffableEntity)?.CardId, _source.PickedCards.Count);
				break;
		}

		// 触发 callback，选中卡牌作为 CurrentTarget
		if (_source.Game != null)
		{
			foreach (var action in _discover.Callback)
			{
				_source.Game.ActionBlock(_source,
					new List<Action> { action },
					BlockType.Trigger,
					target: card as BuffableEntity,
					eventArgs: new object[] { _discoverTarget!, Cards, card });
			}
		}
	}

	// Not used for Discover wrapper but needed for abstract impl
	public override object? Do(BaseEntity source, BaseEntity target, params object[] args) => null;
}


/// <summary>
/// Choose One choice wrapper: presents two sub-card options.
/// When the player chooses, the sub-card's Play script is triggered on the original card.
/// </summary>
public class ChooseOneChoice : Choice
{
	private static readonly ILogger _log = LogConfig.ForType<ChooseOneChoice>();

	private readonly BuffableEntity _originalCard;
	private readonly Character? _target;

	public ChooseOneChoice(BuffableEntity originalCard, Character? target, List<BaseEntity> choiceCards)
	{
		_originalCard = originalCard;
		_target = target;
		Source = originalCard;
		SourcePlayer = originalCard.Controller;
		Cards = choiceCards;
		MinCount = 1;
		MaxCount = 1;
	}

	public override void Choose(BaseEntity card)
	{
		if (!Cards.Contains(card))
			throw new InvalidActionException(
				$"{card} is not a valid choice (one of {string.Join(", ", Cards)})");

		if (SourcePlayer != null) SourcePlayer.Choice = null;

		_log.Information("{Player} chose {Card} for choose-one card {Original}",
			SourcePlayer?.Name, (card as BuffableEntity)?.CardId, _originalCard.CardId);

		// Execute the chosen sub-card's Play script on the original card
		var chosenCardId = (card as BuffableEntity)?.CardId ?? "";
		var chosenScript = CardScriptRegistry.Instance.GetScript(chosenCardId);

		if (chosenScript != null && _originalCard.Game != null)
		{
			var playActions = chosenScript.Play.ToList();
			if (playActions.Count > 0)
			{
				_originalCard.Game.ActionBlock(_originalCard,
					playActions.Cast<Action>().ToList(),
					BlockType.Power,
					target: _target);
			}
		}
	}

	public override object? Do(BaseEntity source, BaseEntity target, params object[] args) => null;

	public override string ToString() => $"<ChooseOneChoice: {Cards.Count} options for {_originalCard.CardId}>";
}

// ───────────────────────────────────────
// MultipleChoice — multi-step choice (e.g. Kazakus)
// ───────────────────────────────────────

/// <summary>
/// A multi-step choice action where the player makes multiple sequential choices.
/// Used by cards like Kazakus that require choosing from cards multiple times.
/// </summary>
public class MultiStepChoice : Choice
{
	private static readonly ILogger _log = LogConfig.ForType<MultiStepChoice>();

	public int ChooseTimes { get; set; } = 2;
	public List<BaseEntity> ChosenCards { get; } = new();

	/// <summary>Callback to populate cards for each step.</summary>
	public Func<int, List<BaseEntity>>? StepProvider { get; set; }

	public MultiStepChoice(object target) : base(target) { }

	public override object? Do(BaseEntity source, BaseEntity target, params object[] args)
	{
		if (target is not Player player) return null;

		SourcePlayer = player;
		Source = source;
		ChosenCards.Clear();
		MinCount = 1;
		MaxCount = 1;

		// Start step 1
		if (StepProvider != null)
		{
			Cards = StepProvider(1);
			player.Choice = this;
			_log.Information("MultipleChoice: {Player} begins step 1/{Total}", player.Name, ChooseTimes);
		}

		return null;
	}

	public override void Choose(BaseEntity card)
	{
		if (!Cards.Contains(card))
			throw new InvalidActionException(
				$"{card} is not a valid choice (one of [{string.Join(", ", Cards)}])");

		ChosenCards.Add(card);
		int step = ChosenCards.Count;
		_log.Information("MultipleChoice: chose {Card}, step {Step}/{Total}", card, step, ChooseTimes);

		if (step < ChooseTimes)
		{
			// Continue to next step
			if (StepProvider != null)
			{
				Cards = StepProvider(step + 1);
			}
		}
		else
		{
			// All steps done
			if (SourcePlayer != null) SourcePlayer.Choice = null;
			OnComplete();
		}
	}

	/// <summary>Called when all steps are complete. Override for custom behavior.</summary>
	protected virtual void OnComplete()
	{
		_log.Information("MultipleChoice complete: {Count} cards chosen", ChosenCards.Count);
	}
}

// ───────────────────────────────────────
// BattlecryTargetChoice — 多目标战吼选择
// ───────────────────────────────────────

/// <summary>
/// 多目标战吼选择。逐一请求玩家选择目标，全部选完后执行战吼动作。
/// <para>流程：SequenceRunner 创建此实例 → 设置 Player.Choice →
/// 前端逐个 Choose(target) → 全部选完后自动执行战吼。</para>
/// <para>双倍战吼时，SequenceRunner 创建新的实例重新选择目标。</para>
/// </summary>
public class BattlecryTargetChoice : Choice
{
	private static readonly ILogger _log = LogConfig.ForType<BattlecryTargetChoice>();

	private readonly BuffableEntity _card;
	private readonly TargetRequirement _targetReq;
	private readonly List<Action> _battlecryActions;
	private readonly int _totalTargets;
	private readonly List<Character> _chosenTargets = new();

	/// <summary>已选择的目标列表（供外部查询）。</summary>
	public IReadOnlyList<Character> ChosenTargets => _chosenTargets;

	public BattlecryTargetChoice(
		BuffableEntity card,
		TargetRequirement targetReq,
		List<Action> battlecryActions,
		Character? firstTarget = null)
	{
		_card = card;
		_targetReq = targetReq;
		_battlecryActions = battlecryActions;
		_totalTargets = targetReq.TargetCount;

		Source = card;
		SourcePlayer = card.Controller;
		MinCount = 1;
		MaxCount = 1;

		// 如果已有第一个目标（PlayCard 传入的），直接记录
		if (firstTarget != null)
		{
			_chosenTargets.Add(firstTarget);
		}

		// 如果还需要更多目标，刷新合法目标列表
		if (_chosenTargets.Count < _totalTargets)
		{
			RefreshValidTargets();
		}
		else
		{
			// 所有目标已就绪，直接执行
			ExecuteBattlecry();
		}
	}

	/// <summary>开始选择流程：将自身设置到 Player.Choice。</summary>
	public void Begin()
	{
		if (_chosenTargets.Count < _totalTargets && SourcePlayer != null)
		{
			SourcePlayer.Choice = this;
			_log.Information("BattlecryTargetChoice: {Player} needs to pick target {N}/{Total} for {Card}",
				SourcePlayer.Name, _chosenTargets.Count + 1, _totalTargets, _card.CardId);
		}
	}

	public override void Choose(BaseEntity target)
	{
		if (target is not Character ch)
			throw new InvalidActionException($"{target} is not a valid Character target");

		// 验证目标合法性
		if (!TargetingRules.IsValidTarget(_card, target, _targetReq))
			throw new InvalidActionException($"{target} is not a valid target for {_card.CardId}");

		_chosenTargets.Add(ch);
		_log.Information("BattlecryTargetChoice: chose target {N}/{Total}: {Target}",
			_chosenTargets.Count, _totalTargets, target);

		if (_chosenTargets.Count < _totalTargets)
		{
			// 还需要更多目标，刷新列表（排除已选的）
			RefreshValidTargets();
		}
		else
		{
			// 全部选完，清除 Choice 并执行战吼
			if (SourcePlayer != null) SourcePlayer.Choice = null;
			ExecuteBattlecry();
		}
	}

	/// <summary>刷新当前可选目标列表（排除已选目标）。</summary>
	private void RefreshValidTargets()
	{
		var validTargets = TargetingRules.GetValidTargets(_card, _targetReq);

		// 排除已选的目标（多目标不能重复选同一个）
		foreach (var chosen in _chosenTargets)
		{
			validTargets.Remove(chosen);
		}

		Cards = validTargets.Cast<BaseEntity>().ToList();

		if (Cards.Count == 0)
		{
			// 没有合法目标了，用已选目标执行
			_log.Information("BattlecryTargetChoice: no more valid targets, executing with {Count} targets",
				_chosenTargets.Count);
			if (SourcePlayer != null) SourcePlayer.Choice = null;
			ExecuteBattlecry();
		}
	}

	/// <summary>将选中的目标存入 card.CurrentTargets，然后执行战吼动作。</summary>
	private void ExecuteBattlecry()
	{
		// 将所有选中目标存入 card 的 CurrentTargets
		_card.CurrentTargets = _chosenTargets.Cast<BaseEntity>().ToList();

		var game = _card.Game;
		if (game != null && _battlecryActions.Count > 0)
		{
			var target = _chosenTargets.Count > 0 ? _chosenTargets[0] : null;
			game.ActionBlock(_card, _battlecryActions, BlockType.Power, target: target);
			game.RefreshAuras();
			game.ProcessDeaths();

			_log.Information("BattlecryTargetChoice: executed battlecry with {Count} targets for {Card}",
				_chosenTargets.Count, _card.CardId);
		}
	}

	public override object? Do(BaseEntity source, BaseEntity target, params object[] args) => null;

	public override string ToString() =>
		$"<BattlecryTargetChoice: {_chosenTargets.Count}/{_totalTargets} targets for {_card.CardId}>";
}
