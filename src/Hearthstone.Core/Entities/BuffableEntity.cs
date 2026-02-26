namespace HearthstoneServer.Entities;

using HearthstoneServer.Dsl;

/// <summary>
/// 可被 Buff / AuraSlot 修改属性的实体。
/// 提供 _getattr 风格的属性聚合：field → buffs → slots → script。
/// </summary>
public class BuffableEntity : BaseEntity
{
	/// <summary>持久 Buff 列表（沉默时清除）</summary>
	public List<BuffableEntity> Buffs { get; } = new();

	/// <summary>Aura Slot 列表（光环产生的临时 buff）</summary>
	public List<Auras.AuraBuff> Slots { get; } = new();

	/// <summary>
	/// 额外注册的脚本 ID（用于合并卡等场景）。
	/// 游戏读取脚本时，除了 CardId 以外，也会加载这些 ID 的脚本。
	/// </summary>
	public List<string> ExtraScriptIds { get; } = new();

	/// <summary>
	/// 获取该实体关联的所有脚本（CardId + ExtraScriptIds）。
	/// </summary>
	public IEnumerable<Dsl.ICardScript> GetAllScripts()
	{
		var primary = Dsl.CardScriptRegistry.Instance.GetScript(CardId ?? "");
		if (primary != null) yield return primary;
		foreach (var id in ExtraScriptIds)
		{
			var extra = Dsl.CardScriptRegistry.Instance.GetScript(id);
			if (extra != null) yield return extra;
		}
	}


	// ── Aura 目标方法 ──

	/// <summary>
	/// 刷新已有 buff 的 tick，或新建 aura buff。
	/// </summary>
	public void RefreshBuff(BuffableEntity source, string buffId)
	{
		// 查找已有 buff
		foreach (var buff in Buffs)
		{
			if (buff is Enchantment ench && ench.SourceCard == source && ench.BuffId == buffId)
			{
				ench.AuraTick = source.Game?.Tick ?? 0;
				return;
			}
		}

		// 未找到 → 新建 buff（通过 source.buff(this, id) 等价物）
		var newBuff = new Enchantment { SourceCard = source, BuffId = buffId };
		newBuff.AuraTick = source.Game?.Tick ?? 0;

		// 从注册表加载 buff 脚本的 tag 值（如 atk、health）
		var buffScript = Dsl.CardScriptRegistry.Instance.GetScript(buffId);
		if (buffScript?.Tags != null)
		{
			foreach (var (tag, value) in buffScript.Tags)
			{
				var propName = Managers.CardManager.GetPropertyName(tag);
				if (propName != null)
				{
					if (value is int intVal)
						newBuff.SetIntProperty(propName, intVal);
					else if (value is bool boolVal)
						newBuff.SetBoolProperty(propName, boolVal);
				}
			}
		}

		Buffs.Add(newBuff);
		source.Game?.ActiveAuraBuffs.Add(new Auras.AuraBuff(source, this)
		{
			Id = buffId,
			Tick = source.Game?.Tick ?? 0
		});
	}

	/// <summary>
	/// 刷新已有 AuraBuff 的 tags，或新建 AuraBuff slot。
	/// </summary>
	public void RefreshTags(BuffableEntity source, Dictionary<GameTag, int> tags)
	{
		// 查找已有 AuraBuff slot
		foreach (var slot in Slots)
		{
			if (slot.Source == source)
			{
				slot.UpdateTags(tags);
				slot.Tick = source.Game?.Tick ?? 0;
				return;
			}
		}

		// 未找到 → 新建 AuraBuff slot
		var buff = new Auras.AuraBuff(source, this);
		buff.UpdateTags(tags);
		buff.Tick = source.Game?.Tick ?? 0;
		Slots.Add(buff);
		source.Game?.ActiveAuraBuffs.Add(buff);
	}

	/// <summary>
	/// 返回该实体的 aura 刷新脚本列表。
	/// 从 CardScriptRegistry 加载 script.Update（场上）或 script.HandUpdate（手牌）。
	/// 子类可 override 以添加额外刷新逻辑（如 Enrage）。
	/// </summary>
	public virtual List<Auras.Refresh> UpdateScripts
	{
		get
		{
			var scripts = new List<Auras.Refresh>();
			foreach (var script in GetAllScripts())
			{
				if (Zone == Zone.Play)
					scripts.AddRange(script.Update);
				else if (Zone == Zone.Hand)
					scripts.AddRange(script.HandUpdate);
				else if (Zone == Zone.Deck)
					scripts.AddRange(script.DeckUpdate);
			}
			return scripts;
		}
	}

	// ── 卡牌数据绑定 ──

	/// <summary>
	/// 关联的卡牌数据模型。
	/// 运行时实体通过此引用读取静态卡牌属性（费用、种族、稀有度等）。
	/// </summary>
	public LoadCardIntoDbf.Card? CardData { get; set; }

	/// <summary>卡牌 ID（如 "CS2_032"），从 CardData 读取。</summary>
	public string? CardId => CardData?.CardId;

	/// <summary>卡牌名称（英文），从 CardData 的 LocString 中读取。</summary>
	public string? NameEnUS
	{
		get
		{
			if (CardData == null) return null;
			// CARDNAME = GameTag 185
			if (CardData.TagsLoc.TryGetValue((GameTag)185, out var locDict) &&
				locDict.TryGetValue("enUS", out var name))
				return name;
			return null;
		}
	}

	// ── 区域管理 ──

	/// <summary>当前所在区域。</summary>
	public Zone Zone { get; set; } = Zone.Invalid;

	/// <summary>前一个所在区域。</summary>
	public Zone OldZone { get; internal set; } = Zone.Invalid;

	/// <summary>可被打出的来源区域（默认手牌）。</summary>
	public virtual Zone PlayableZone => Zone.Hand;

	/// <summary>The controller of this entity (set by subclasses).</summary>
	public virtual Player? Controller { get; set; }

	/// <summary>The turn this card was drawn.</summary>
	public int TurnDrawn { get; set; } = -1;

	/// <summary>The turn this card was played.</summary>
	public int TurnPlayed { get; set; } = -1;

	/// <summary>Mana cost of this entity (overridden by subclasses).</summary>
	public virtual int Cost
	{
		get
		{
			int cost = GetField("cost");

			// 按 Buffs 顺序依次：先加 tag 值，再跑 CostModFunc
			foreach (var buff in Buffs)
			{
				cost = buff.AggregateAttr("cost", cost);
				if (buff is Enchantment ench && ench.CostModFunc != null)
					cost = ench.CostModFunc(this, cost);
			}
			foreach (var slot in Slots)
				cost = slot.AggregateAttr("cost", cost);

			// Apply script-level CostMod (LazyNum, e.g. 山岭巨人: cost -= handCount)
			if (Zone == Zone.Hand)
			{
				var script = Dsl.CardScriptRegistry.Instance.GetScript(CardId ?? "");
				if (script?.CostMod != null)
					cost += script.CostMod.Value(this);
			}

			return Math.Max(0, cost);
		}
		set => SetIntProperty("cost", value);
	}



	/// <summary>Whether this card has battlecry.</summary>
	public bool HasBattlecry { get; set; }

	/// <summary>Whether this card has combo.</summary>
	public bool HasCombo { get; set; }

	/// <summary>Overload amount (locked mana next turn).</summary>
	public int Overload { get; set; }

	/// <summary>是否有亡语效果。</summary>
	public virtual bool HasDeathrattle { get; set; }

	/// <summary>
	/// 亡语列表。每个亡语是一组 Action 列表或一个返回 Action 列表的委托。
	/// </summary>
	public List<object> Deathrattles { get; } = new();

	/// <summary>
	/// 所有附加实体（含自身及 buff 实体）。用于遍历亡语等效果。
	/// </summary>
	public virtual IEnumerable<BuffableEntity> Entities
	{
		get
		{
			yield return this;
			foreach (var buff in Buffs)
				yield return buff;
		}
	}

	/// <summary>
	/// 获取指定类型的 action 列表（play / combo / deathrattle 等）。
	/// </summary>
	public virtual List<Dsl.Action>? GetActions(string type) => null;

	/// <summary>
	/// 检查战吼是否需要目标。
	/// </summary>
	public virtual bool BattlecryRequiresTarget()
	{
		var req = TargetReq;
		if (req == null) return false;
		return req.MustTarget || req.TargetIfAvailable || req.TargetForCombo;
	}

	/// <summary>召唤位置索引。</summary>
	public int SummonIndex { get; set; }

	/// <summary>
	/// 卡牌的目标选择与打出条件。
	/// 自动从 ICardScript.TargetReq 加载。
	/// </summary>
	private Targeting.TargetRequirement? _targetReq;
	public Targeting.TargetRequirement? TargetReq
	{
		get
		{
			if (_targetReq != null) return _targetReq;
			// 从卡牌脚本自动加载
			var script = CardScriptRegistry.Instance.GetScript(CardId ?? "");
			if (script != null && script.TargetReq != Targeting.TargetRequirement.None)
			{
				_targetReq = script.TargetReq;
				return _targetReq;
			}
			return null;
		}
		set => _targetReq = value;
	}

	/// <summary>
	/// 区域转换核心方法。从旧区域列表移除，添加到新区域列表。
	/// </summary>
	public virtual void SetZone(Zone newZone, Player? controller = null)
	{
		var ctrl = controller ?? Controller;
		OldZone = Zone;

		if (OldZone == newZone) return; // 同区域移动，忽略

		// 从旧区域的列表移除
		if (ctrl != null && OldZone != Zone.Invalid)
		{
			RemoveFromZone(ctrl, OldZone);
		}

		Zone = newZone;

		// 添加到新区域的列表
		if (ctrl != null && newZone != Zone.Invalid && newZone != Zone.RemovedFromGame)
		{
			AddToZone(ctrl, newZone);
		}

		// Play/Secret 区域分配 PlayCounter
		if (newZone == Zone.Play || newZone == Zone.Secret)
		{
			if (ctrl?.Game != null)
			{
				PlayCounter = ctrl.Game.PlayCounterNext;
				ctrl.Game.PlayCounterNext++;
			}
		}
	}

	/// <summary>从指定区域的列表中移除自己。</summary>
	protected virtual void RemoveFromZone(Player controller, Zone zone)
	{
		switch (zone)
		{
			case Zone.Hand:
				controller.Hand.Remove(this);
				break;
			case Zone.Deck:
				controller.Deck.Remove(this);
				break;
			case Zone.Play:
				controller.Field.Remove(this);
				break;
			case Zone.Graveyard:
				controller.Graveyard.Remove(this);
				break;
			case Zone.Secret:
				controller.Secrets.Remove(this);
				break;
		}
	}

	/// <summary>添加自己到指定区域的列表，并注册区域相关的脚本效果。</summary>
	protected virtual void AddToZone(Player controller, Zone zone)
	{
		switch (zone)
		{
			case Zone.Hand:
				controller.Hand.Add(this);
				break;
			case Zone.Deck:
				controller.Deck.Add(this);
				break;
			case Zone.Play:
				controller.Field.Add(this);
				break;
			case Zone.Graveyard:
				controller.Graveyard.Add(this);
				break;
			case Zone.Secret:
				controller.Secrets.Add(this);
				break;
		}

		// 进入牌库/手牌时，自动注册声明式触发器（如帕奇斯从牌库监听事件）
		if (zone == Zone.Deck || zone == Zone.Hand)
		{
			foreach (var script in GetAllScripts())
				script.OnActivate?.Invoke(this, controller.Game!);
		}
	}

	/// <summary>
	/// 召唤/打出前检查是否可召唤。子类可 override 添加额外限制（如 Secret 唯一性）。
	/// </summary>
	public virtual bool IsSummonable() => true;

	/// <summary>
	/// 基础 IsPlayable 检查。
	/// </summary>
	public virtual bool IsPlayable()
	{
		if (Controller == null) return false;
		if (!Controller.IsCurrentPlayer) return false;
		if (Zone != PlayableZone) return false;
		if (Controller.Mana < Cost) return false;
		if (!IsSummonable()) return false;
		return true;
	}

	/// <summary>
	/// 打出卡牌 — 基础钩子。支付费用和过载处理。
	/// 通过 Game.PlayCard 调用时，SequenceRunner 会处理这些；
	/// 直接调用 Play() 时（ForcePlay、测试），这里完成基本费用支付。
	/// </summary>
	public virtual void Play(Character? target = null, int index = -1)
	{
		if (Controller == null) return;

		// 支付费用
		Controller.PayCost(Cost);

		// 处理过载
		if (Overload > 0)
		{
			Controller.Overloaded += Overload;
		}

		TurnPlayed = Controller.Game?.Turn ?? -1;
	}

	// ── 字段存储 ──

	/// <summary>
	/// 存储 _field 值。
	/// </summary>
	private readonly Dictionary<string, int> _fields = new(StringComparer.Ordinal);

	/// <summary>
	/// 布尔型字段存储。
	/// </summary>
	private readonly Dictionary<string, bool> _boolFields = new(StringComparer.Ordinal);

	// ── int_property 等价：聚合 field + buffs + slots + script ──

	/// <summary>
	/// 获取整型属性值。
	/// 遍历 buffs 和 slots 来聚合值，最终 max(0, result)。
	/// </summary>
	public int GetIntProperty(string attr)
	{
		int i = GetField(attr);
		foreach (var buff in Buffs)
			i = buff.AggregateAttr(attr, i);
		foreach (var slot in Slots)
			i = slot.AggregateAttr(attr, i);
		// script 回调（当前跳过，在 DSL 阶段实现）
		return Math.Max(0, i);
	}

	/// <summary>
	/// 设置整型属性的底层 _field 值。
	/// </summary>
	public void SetIntProperty(string attr, int value)
	{
		_fields[attr] = value;
	}

	// ── boolean_property 等价 ──

	/// <summary>
	/// 获取布尔型属性值。
	/// 只要 _field / 任意 buff / 任意 slot 中有一个为 true，结果就为 true。
	/// </summary>
	public bool GetBoolProperty(string attr)
	{
		if (GetBoolField(attr)) return true;
		foreach (var buff in Buffs)
			if (buff.GetBoolField(attr)) return true;
		foreach (var slot in Slots)
			if (slot.HasBoolAttr(attr)) return true;

		// Fallback: check int property (nonzero = true)
		if (GetIntProperty(attr) > 0) return true;

		// script 回调（当前跳过）
		return false;
	}

	/// <summary>
	/// 通过 GameTag 获取布尔属性（查找 CardManager 映射后的属性名）。
	/// </summary>
	public bool GetBoolProperty(GameTag tag)
	{
		var name = Managers.CardManager.GetPropertyName(tag);
		return name != null && GetBoolProperty(name);
	}

	/// <summary>
	/// 设置布尔型属性的底层 _field 值。
	/// </summary>
	public void SetBoolProperty(string attr, bool value)
	{
		_boolFields[attr] = value;
	}

	// ── slot_property 等价 ──

	/// <summary>
	/// 获取 slot 属性。
	/// 检查所有 slots，任意一个包含该属性即返回 true。
	/// </summary>
	public bool GetSlotProperty(string attr)
	{
		foreach (var slot in Slots)
			if (slot.HasBoolAttr(attr)) return true;
		return false;
	}

	// ── Buff 聚合辅助 ──

	/// <summary>
	/// 对指定属性值进行聚合（被其他实体的 Buffs 列表调用）。
	/// </summary>
	public int AggregateAttr(string attr, int current)
	{
		return current + GetField(attr);
	}

	/// <summary>
	/// 清除所有 Buff。
	/// </summary>
	public void ClearBuffs()
	{
		foreach (var buff in Buffs.ToList())
		{
			// 在完整实现中，buff.Remove() 会从各列表中移除
			Buffs.Remove(buff);
		}
	}

	// ── 内部辅助 ──

	protected int GetField(string attr)
	{
		return _fields.TryGetValue(attr, out var v) ? v : 0;
	}

	private bool GetBoolField(string attr)
	{
		return _boolFields.TryGetValue(attr, out var v) && v;
	}

	private bool HasBoolAttr(string attr)
	{
		return _boolFields.TryGetValue(attr, out var v) && v;
	}

	// ── 目标系统 ──

	/// <summary>
	/// 获取有效的打出目标列表。
	/// </summary>
	public List<Character> PlayTargets
	{
		get
		{
			if (Controller?.Game == null) return new();
			var result = new List<Character>();
			foreach (var entity in Controller.Game.Characters)
			{
				if (entity is Character c && Targeting.TargetingRules.IsValidTarget(this, c, TargetReq))
					result.Add(c);
			}
			return result;
		}
	}

	/// <summary>
	/// 当前打出是否需要目标。
	/// </summary>
	public bool RequiresTarget
	{
		get
		{
			var req = TargetReq;
			if (req == null) return false;
			if (req.MustTarget) return true;
			if (req.TargetIfAvailable) return PlayTargets.Count > 0;
			if (req.TargetForCombo && HasCombo && Controller?.Combo == true)
				return PlayTargets.Count > 0;
			return false;
		}
	}
}
