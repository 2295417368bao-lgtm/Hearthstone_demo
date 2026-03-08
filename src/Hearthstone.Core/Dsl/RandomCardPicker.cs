using HearthstoneServer.Entities;

namespace HearthstoneServer.Dsl;

// ═══════════════════════════════════════════════════
// RandomCardPicker — weighted random card selection
// ═══════════════════════════════════════════════════

/// <summary>
/// Store filters and generate random cards matching the filters.
/// Supports weighted filter sets for Discover-style class weighting.
/// </summary>
public class RandomCardPicker : LazyValue
{
	/// <summary>Global filters applied to all card searches.</summary>
	public Dictionary<string, object> Filters { get; set; } = new();

	/// <summary>Weights for each weighted filter set.</summary>
	public List<int> Weights { get; set; } = new();

	/// <summary>Additional weighted filter sets (merged with global filters).</summary>
	public List<Dictionary<string, object>> WeightedFilters { get; set; } = new();

	/// <summary>Number of cards to pick.</summary>
	public int Count { get; set; } = 1;

	// ── 构造 ──

	public RandomCardPicker(Dictionary<string, object>? filters = null)
	{
		Filters = filters ?? new Dictionary<string, object>();
	}

	/// <summary>Create a copy with the count set to n.
	public RandomCardPicker WithCount(int n)
	{
		var clone = Clone();
		clone.Count = n;
		return clone;
	}

	/// <summary>Create a copy with an additional weighted filter set.</summary>
	public RandomCardPicker CopyWithWeighting(int weight, Dictionary<string, object> extraFilters)
	{
		var clone = Clone();
		clone.Weights.Add(weight);
		clone.WeightedFilters.Add(extraFilters);
		return clone;
	}

	/// <summary>Deep clone this picker.</summary>
	public RandomCardPicker Clone()
	{
		return new RandomCardPicker(new Dictionary<string, object>(Filters))
		{
			Count = Count,
			Weights = new List<int>(Weights),
			WeightedFilters = WeightedFilters
				.Select(d => new Dictionary<string, object>(d)).ToList(),
		};
	}

	/// <summary>
	/// Find cards from the card database matching the given filters.
	/// </summary>
	public virtual List<string> FindCards(BaseEntity source, Dictionary<string, object>? overrideFilters = null)
	{
		var filters = overrideFilters != null
			? new Dictionary<string, object>(overrideFilters)
			: new Dictionary<string, object>(Filters);

		// 如果是标准模式，自动过滤
		if (source.Game?.IsStandard == true && !filters.ContainsKey("is_standard"))
			filters["is_standard"] = true;

		// 解析 LazyValue / Selector 过滤器值
		foreach (var key in filters.Keys.ToList())
		{
			if (filters[key] is LazyValue lv)
				filters[key] = lv.Evaluate(source) ?? filters[key];
			else if (filters[key] is Selector sel)
			{
				var resolved = sel.Eval(
					source.Game?.Entities ?? new List<BaseEntity>(), source);
				// card_class 需要从 Player → Hero → CardClass
				if (key == "card_class")
				{
					var player = resolved.OfType<Player>().FirstOrDefault();
					if (player?.Hero?.Data != null)
						filters[key] = player.Hero.Data.CardClass;
					else
						filters.Remove(key);
				}
				else
					filters[key] = resolved;
			}
		}

		// 从卡牌数据库过滤
		return FilterCardDb(source, filters);
	}

	/// <summary>从卡牌 DB 通过 filters 过滤。</summary>
	protected virtual List<string> FilterCardDb(BaseEntity source, Dictionary<string, object> filters)
	{
		var allCards = source.Game?.CardDb;
		if (allCards == null) return new List<string>();

		var result = new List<string>();
		foreach (var kvp in allCards)
		{
			var card = kvp.Value;
			// 排除巨型随从（不能被随机召唤/进化）
			if (card.HasTag(GameTag.Colossal)) continue;
			if (MatchesFilters(card, filters))
				result.Add(kvp.Key);
		}
		return result;
	}

	// ── 标准模式卡包定义 ──

	/// <summary>
	/// Card sets considered legal in Standard format.
	/// This set can be updated as new expansions rotate in/out.
	/// </summary>
	public static HashSet<CardSet> StandardSets { get; set; } = new()
	{
		CardSet.Core,
		CardSet.Expert1,     // Classic
		CardSet.Legacy,
		// Recent expansions (update as rotations happen):
		CardSet.Titans,
		CardSet.WildWest,
		CardSet.WhizbangsWorkshop,
		CardSet.IslandVacation,
		CardSet.Space,
		CardSet.TheLostCity,
		CardSet.TimeTravel,
		CardSet.Cataclysm,
	};

	/// <summary>检查一张卡是否匹配所有过滤条件。</summary>
	protected static bool MatchesFilters(LoadCardIntoDbf.Card card, Dictionary<string, object> filters)
	{
		foreach (var kvp in filters)
		{
			var key = kvp.Key;
			var value = kvp.Value;

			switch (key)
			{
				case "collectible":
					if (card.Collectible != (bool)value) return false;
					break;
				case "type":
					if (card.CardType != (CardType)value) return false;
					break;
				case "card_class":
					if (card.CardClass != (CardClass)value) return false;
					break;
				case "race":
					if (!card.Races.Contains((Race)value)) return false;
					break;
				case "rarity":
					if (card.Rarity != (Rarity)value) return false;
					break;
				case "cost":
					if (card.Cost != (int)value) return false;
					break;
				case "card_set":
					if (card.CardSet != (CardSet)value) return false;
					break;
				case "is_standard":
					if ((bool)value && !StandardSets.Contains(card.CardSet)) return false;
					break;
				case "spell_school":
					if (card.SpellSchool != (SpellSchool)value) return false;
					break;
				case "mechanic":
					if (!card.Has((Mechanic)value)) return false;
					break;
				case "tag":
					if (!card.HasTag((GameTag)value)) return false;
					break;
				default:
					break;
			}
		}
		return true;
	}

	/// <summary>
	/// Pick random cards from filtered pool.
	/// </summary>
	public override object Evaluate(BaseEntity source)
	{
		List<List<string>> cardSets;
		List<int> weights;

		if (WeightedFilters.Count == 0)
		{
			weights = new List<int> { 1 };
			cardSets = new List<List<string>> { FindCards(source) };
		}
		else
		{
			weights = new List<int>(Weights);
			cardSets = new List<List<string>>();
			foreach (var wf in WeightedFilters)
			{
				var merged = new Dictionary<string, object>(Filters);
				foreach (var kvp in wf) merged[kvp.Key] = kvp.Value;
				cardSets.Add(FindCards(source, merged));
			}
		}

		var chosen = WeightedCardChoice(source, weights, cardSets, Count);

		// 将 card ID 转换为实体
		var entities = new List<BaseEntity>();
		var controller = (source as BuffableEntity)?.Controller;
		if (controller != null)
		{
			foreach (var id in chosen)
			{
				var created = controller.CreateCard(id);
				if (created != null) entities.Add(created);
			}
		}

		return entities;
	}

	// ═══════════════════════════════════════════════════
	// WeightedCardChoice — static utility
	// ═══════════════════════════════════════════════════

	/// <summary>
	/// Take a list of weights and card pools, produce a random weighted sample without replacement.
	/// </summary>
	public static List<string> WeightedCardChoice(
		BaseEntity source,
		List<int> weights,
		List<List<string>> cardSets,
		int count)
	{
		var chosen = new List<string>();
		var random = source.Game?.Random ?? new Random();

		// 构建累积权重
		var cumWeights = new List<double>();
		double totalWeight = 0;
		for (int i = 0; i < weights.Count; i++)
		{
			totalWeight += weights[i] * cardSets[i].Count;
			cumWeights.Add(totalWeight);
		}

		if (totalWeight <= 0) return chosen;

		for (int i = 0; i < count; i++)
		{
			if (totalWeight <= 0) break;

			double r = random.NextDouble() * totalWeight;
			int chosenSet = 0;
			for (int j = 0; j < cumWeights.Count; j++)
			{
				if (r < cumWeights[j])
				{
					chosenSet = j;
					break;
				}
			}

			if (cardSets[chosenSet].Count == 0) continue;

			int idx = random.Next(cardSets[chosenSet].Count);
			chosen.Add(cardSets[chosenSet][idx]);

			// 无放回 — 移除已选卡并更新权重
			cardSets[chosenSet].RemoveAt(idx);
			totalWeight -= weights[chosenSet];
			for (int j = chosenSet; j < cumWeights.Count; j++)
				cumWeights[j] -= weights[chosenSet];
		}

		return chosen;
	}

	public override string ToString() => $"<RandomCardPicker: {Filters.Count} filters, count={Count}>";
}

// ═══════════════════════════════════════════════════
// RandomCardFilter — 流式随机卡牌构建器
// ═══════════════════════════════════════════════════

/// <summary>
/// 流式构建随机卡牌过滤条件，类似 DiscoverFilter。
/// <para>用法：</para>
/// <code>
/// RandomCard().ClassOf(S.OPPONENT)           // 对手职业随机卡
/// RandomCard().Spell().ClassOf(S.OPPONENT)   // 对手职业随机法术
/// RandomCard().Race(Race.Elemental)          // 随机元素
/// RandomCard().Minion().Legendary()          // 随机传说随从
/// RandomCard().Minion().Race(Race.Beast)     // 随机野兽
/// </code>
/// </summary>
public class RandomCardFilter : LazyValue
{
	private readonly Dictionary<string, object> _filters = new() { ["collectible"] = true };
	private Selector? _classSelector;

	// ── 静态工厂 ──

	/// <summary>创建一个空的随机卡牌过滤器（默认可收集）。</summary>
	public static RandomCardFilter Create() => new();

	// ── 流式过滤方法 ──

	/// <summary>随从。</summary>
	public RandomCardFilter Minion() { _filters["type"] = CardType.Minion; return this; }

	/// <summary>法术。</summary>
	public RandomCardFilter Spell() { _filters["type"] = CardType.Spell; return this; }

	/// <summary>武器。</summary>
	public RandomCardFilter Weapon() { _filters["type"] = CardType.Weapon; return this; }

	/// <summary>按种族过滤。</summary>
	public RandomCardFilter Race(Race r) { _filters["race"] = r; return this; }

	/// <summary>按稀有度过滤。</summary>
	public RandomCardFilter Rarity(Rarity r) { _filters["rarity"] = r; return this; }

	/// <summary>传说品质。</summary>
	public RandomCardFilter Legendary() { _filters["rarity"] = HearthstoneServer.Rarity.Legendary; return this; }

	/// <summary>按费用过滤。支持 int 或 LazyNum。</summary>
	public RandomCardFilter Cost(object c) { _filters["cost"] = c; return this; }

	/// <summary>按法术派系过滤。</summary>
	public RandomCardFilter School(SpellSchool s) { _filters["spell_school"] = s; return this; }

	/// <summary>按机制过滤（战吼、亡语等）。</summary>
	public RandomCardFilter HasMechanic(Mechanic m) { _filters["mechanic"] = m; return this; }

	/// <summary>按 GameTag 过滤（如 SparePart、Secret 等卡牌标签）。</summary>
	public RandomCardFilter HasTag(GameTag tag) { _filters["tag"] = tag; return this; }

	/// <summary>按职业过滤（静态）。</summary>
	public RandomCardFilter Class(CardClass c) { _filters["card_class"] = c; return this; }

	/// <summary>运行时从指定 Player 的职业动态过滤。</summary>
	public RandomCardFilter ClassOf(Selector playerSelector) { _classSelector = playerSelector; return this; }

	/// <summary>不限制可收集（用于图腾等基础卡）。</summary>
	public RandomCardFilter AnyCollectibility() { _filters.Remove("collectible"); return this; }

	// ── 评估 ──

	public override object? Evaluate(BaseEntity source)
	{
		var picker = BuildPicker(source);
		return picker?.Evaluate(source);
	}

	/// <summary>转换为 RandomCardPicker（仅静态过滤器），用于 DiscoverCards 等。</summary>
	public RandomCardPicker ToPicker()
	{
		if (_classSelector != null)
			throw new InvalidOperationException("Cannot convert to static picker when OfClass selector is set");
		return new RandomCardPicker(new Dictionary<string, object>(_filters));
	}

	/// <summary>查找匹配的卡牌 ID 列表（运行时解析动态过滤器）。</summary>
	public List<string> FindCards(BaseEntity source)
	{
		var picker = BuildPicker(source);
		return picker?.FindCards(source) ?? new List<string>();
	}

	/// <summary>内部构建 picker，解析运行时职业选择器。</summary>
	private RandomCardPicker? BuildPicker(BaseEntity source)
	{
		var filters = new Dictionary<string, object>(_filters);

		if (_classSelector != null)
		{
			var game = source.Game;
			if (game == null) return null;
			var player = _classSelector.Eval(game.Entities, source)
				.OfType<Player>().FirstOrDefault();
			if (player?.Hero?.Data == null) return null;
			var heroClass = player.Hero.Data.CardClass;
			if (heroClass == CardClass.Neutral) return null;
			filters["card_class"] = heroClass;
		}

		return new RandomCardPicker(filters);
	}

	/// <summary>隐式转换为 RandomCardPicker（仅静态过滤器）。</summary>
	public static implicit operator RandomCardPicker(RandomCardFilter f) => f.ToPicker();

	public int Count { get; set; } = 1;

	public override string ToString() => $"<RandomCardFilter: {_filters.Count} filters>";
}

/// <summary>
/// 便利工厂方法（向后兼容 + 简写）。
/// </summary>
public static class RandomCards
{
	/// <summary>流式构建随机卡牌过滤器。</summary>
	public static RandomCardFilter RandomCard() => RandomCardFilter.Create();

	// ── 向后兼容快捷方法 ──

	public static RandomCardFilter RandomCollectible() => RandomCard();
	public static RandomCardFilter RandomMinion() => RandomCard().Minion();
	public static RandomCardFilter RandomSpell() => RandomCard().Spell();
	public static RandomCardFilter RandomWeapon() => RandomCard().Weapon();
	public static RandomCardFilter RandomBeast() => RandomCard().Minion().Race(Race.Beast);
	public static RandomCardFilter RandomDemon() => RandomCard().Minion().Race(Race.Demon);
	public static RandomCardFilter RandomDragon() => RandomCard().Minion().Race(Race.Dragon);
	public static RandomCardFilter RandomLegendaryMinion() => RandomCard().Minion().Legendary();
	public static RandomCardFilter RandomMech() => RandomCard().Minion().Race(Race.Mechanical);
	public static RandomCardFilter RandomMurloc() => RandomCard().Minion().Race(Race.Murloc);
	public static RandomCardFilter RandomElemental() => RandomCard().Minion().Race(Race.Elemental);
	public static RandomCardFilter RandomTotem() => RandomCard().AnyCollectibility().Race(Race.Totem);
	public static RandomCardPicker RandomSparePart() => RandomCard().HasTag(GameTag.SparePart);
}

// ═══════════════════════════════════════════════════
// RandomEntourage — pick from source's entourage list
// ═══════════════════════════════════════════════════

/// <summary>
/// Pick random cards from the source entity's entourage list.
/// Entourage is stored on the ICardScript, accessed via CardScriptRegistry.
/// </summary>
public class RandomEntourage : RandomCardPicker
{
	public RandomEntourage() : base() { }

	public override object Evaluate(BaseEntity source)
	{
		if (source is BuffableEntity be && be.CardId != null)
		{
			var script = CardScriptRegistry.Instance.GetScript(be.CardId);
			if (script != null && script.Entourage.Length > 0)
			{
				var entourage = script.Entourage.ToList();
				var chosen = WeightedCardChoice(source, new List<int> { 1 },
					new List<List<string>> { entourage }, Count);

				var entities = new List<BaseEntity>();
				var controller = be.Controller;
				if (controller != null)
				{
					foreach (var id in chosen)
					{
						var created = controller.CreateCard(id);
						if (created != null) entities.Add(created);
					}
				}
				return entities;
			}
		}
		return new List<BaseEntity>();
	}
}

// ═══════════════════════════════════════════════════
// RandomID — pick from a fixed list of card IDs
// ═══════════════════════════════════════════════════

/// <summary>
/// Pick random cards from a fixed list of card IDs.
/// </summary>
public class RandomID : RandomCardPicker
{
	private readonly string[] _cards;

	public RandomID(params string[] cardIds) : base()
	{
		_cards = cardIds;
	}

	public override object Evaluate(BaseEntity source)
	{
		var chosen = WeightedCardChoice(source, new List<int> { 1 },
			new List<List<string>> { _cards.ToList() }, Count);

		var entities = new List<BaseEntity>();
		var controller = (source as BuffableEntity)?.Controller;
		if (controller != null)
		{
			foreach (var id in chosen)
			{
				var created = controller.CreateCard(id);
				if (created != null) entities.Add(created);
			}
		}
		return entities;
	}
}
