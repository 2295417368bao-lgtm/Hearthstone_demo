using HearthstoneServer.Events;
using Serilog;

namespace HearthstoneServer.Entities;

// ═══════════════════════════════════════════════════
// Player — a game participant
// ═══════════════════════════════════════════════════

/// <summary>
/// Represents a player in the game. Manages zones (hand, deck, field, graveyard, secrets),
/// mana, and player-level actions (draw, summon, concede).
/// </summary>
public class Player : BuffableEntity
{
	private static readonly ILogger _log = LogConfig.ForType<Player>();

	public Player(string name)
	{
		Type = CardType.Player;
		Name = name;
	}

	// ── Identity ──

	public string Name { get; set; }
	public Player? Opponent { get; set; }
	public bool FirstPlayer { get; set; }
	public PlayState PlayState { get; set; } = PlayState.Invalid;

	// ── Hero ──

	public Hero? Hero { get; set; }

	// ── Zones ──

	/// <summary>Cards in hand.</summary>
	public List<BaseEntity> Hand { get; } = new();

	/// <summary>Cards in deck.</summary>
	public List<BaseEntity> Deck { get; } = new();

	/// <summary>Minions on the battlefield.</summary>
	public List<BaseEntity> Field { get; } = new();

	/// <summary>Dead/destroyed cards.</summary>
	public List<BaseEntity> Graveyard { get; } = new();

	/// <summary>Active secrets.</summary>
	public List<BaseEntity> Secrets { get; } = new();

	/// <summary>本局游戏中被弃置的卡牌（从手牌弃牌，非磨牌/非死亡）。</summary>
	public List<BuffableEntity> DiscardedCards { get; } = new();

	/// <summary>被送入未来的随从（Zone.SetAside），到达 returnTurn 时回归战场。</summary>
	public List<(BuffableEntity minion, int returnTurn)> FutureMinions { get; } = new();

	// ── Mana ──

	private int _maxMana;

	/// <summary>Maximum mana crystals (capped at MaxResources).</summary>
	public int MaxMana
	{
		get => _maxMana;
		set
		{
			_maxMana = Math.Clamp(value, 0, MaxResources);
			_log.Debug("{Player} is now at {Mana} mana crystals", Name, _maxMana);
		}
	}

	/// <summary>Mana used this turn.</summary>
	public int UsedMana { get; set; }

	/// <summary>Temporary mana (e.g. The Coin, Innervate).</summary>
	public int TempMana { get; set; }

	/// <summary>Mana locked by overload this turn.</summary>
	public int OverloadLocked { get; set; }

	/// <summary>Overload queued for next turn.</summary>
	public int Overloaded { get; set; }

	/// <summary>Total overloaded mana this game.</summary>
	public int OverloadedThisGame { get; set; }

	/// <summary>Maximum resources (default 10).</summary>
	public int MaxResources { get; set; } = 10;

	/// <summary>Available mana = max(0, MaxMana - UsedMana - OverloadLocked) + TempMana.</summary>
	public int Mana => Math.Max(0, MaxMana - UsedMana - OverloadLocked) + TempMana;

	// ── Hand limits ──

	public int MaxHandSize { get; set; } = 10;
	public int MaxDeckSize { get; set; } = 60;

	// ── Combat / turn tracking ──

	public int FatigueCounter { get; set; }
	public bool CantDraw { get; set; }
	public bool CantFatigue { get; set; }

	/// <summary>当前等待的选择 action（Discover/Choice）。非空时玩家必须先 Choose()。</summary>
	public Dsl.Choice? Choice { get; set; }

	// ── Game tracking ──

	/// <summary>Last card played this turn.</summary>
	public BaseEntity? LastCardPlayed { get; set; }

	/// <summary>All cards played this game (in order).</summary>
	public List<BaseEntity> CardsPlayedThisGame { get; } = new();

	/// <summary>Hero power damage dealt this game.</summary>
	public int HeroPowerDamageThisGame { get; set; }

	/// <summary>Starting deck card IDs (for PrepareForGame).</summary>
	public List<string>? StartingDeck { get; set; }

	/// <summary>Starting hero card ID (for PrepareForGame).</summary>
	public string? StartingHero { get; set; }

	/// <summary>Minion entities that entered play for this player this game.</summary>
	public List<BuffableEntity> SummonedMinionsThisGame { get; } = new();

	/// <summary>
	/// Choose a card from the current pending choice.
	/// Convenience method — delegates to Choice.Choose(card).
	/// </summary>
	public void Choose(BaseEntity card)
	{
		if (Choice == null)
			throw new InvalidActionException("No pending choice to make");
		Choice.Choose(card);
	}

	/// <summary>调度阶段状态。</summary>
	public Mulligan MulliganState { get; set; } = Mulligan.Invalid;

	public bool Combo { get; set; }
	public int CardsPlayedThisTurn { get; set; }
	public int CardsDrawnThisTurn { get; set; }
	public int MinionsPlayedThisTurn { get; set; }
	public int MinionsKilledThisTurn { get; set; }
	public int Turn { get; set; }
	public int? LastTurn { get; set; }
	public int Timeout { get; set; } = 75;

	// ── Overload 控制 ──

	/// <summary>无法被过载（如某些卡牌效果阻止过载）。</summary>
	public bool CantOverload { get; set; }

	// ── 额外战吼/亡语/连击 ──

	/// <summary>布莱恩·铜须效果：战吼触发两次。</summary>
	public bool ExtraBattlecries { get; set; }
	/// <summary>随从额外战吼（如深渊领主等）。</summary>
	public bool MinionExtraBattlecries { get; set; }
	/// <summary>随从额外连击（如鲨鱼之灵）。</summary>
	public bool MinionExtraCombos { get; set; }
	/// <summary>额外亡语触发（如瑞文戴尔男爵）。</summary>
	public bool ExtraDeathrattles { get; set; }

	// ── Spell power ──

	/// <summary>Total spell power from all minions on the field.</summary>
	public int SpellPower
	{
		get
		{
			int power = 0;
			foreach (var entity in Field)
			{
				if (entity is Minion m && !m.Dormant)
					power += m.SpellPower;
			}
			return power;
		}
	}

	/// <summary>Spell power doubling (from aura effects).</summary>
	public int SpellPowerDouble { get; set; }

	/// <summary>Hero power damage from minions.</summary>
	public int HeroPowerDamage
	{
		get
		{
			int power = 0;
			foreach (var entity in Field)
			{
				if (entity is Minion m && !m.Dormant)
					power += m.HeroPowerDamage;
			}
			return power;
		}
	}

	public int HeroPowerDouble { get; set; }
	public int HealingDouble { get; set; }
	public bool HeroHealedThisTurn { get; set; }

	// ── Game stats ──

	public int TimesHeroPowerUsedThisGame { get; set; }
	public int SpentManaOnSpellsThisGame { get; set; }
	public int HealedThisGame { get; set; }
	public int ElementalPlayedThisTurn { get; set; }
	public int ElementalPlayedLastTurn { get; set; }
	public int JadeGolem { get; set; } = 1;
	public int InvokeCounter { get; set; }

	// ── Corpse (残骸) 资源 ──

	/// <summary>当前可用残骸数量（友方随从死亡时 +1）。</summary>
	public int Corpses { get; set; }

	/// <summary>整局游戏消耗的残骸总数。</summary>
	public int CorpsesSpentThisGame { get; set; }

	/// <summary>残骸获取加倍（法瑞克光环）。</summary>
	public bool ExtraCorpses { get; set; }

	/// <summary>
	/// 消耗指定数量的残骸。返回实际消耗数量（不足时返回 0，不消耗）。
	/// </summary>
	public int SpendCorpses(int count)
	{
		if (Corpses < count) return 0;
		Corpses -= count;
		CorpsesSpentThisGame += count;
		return count;
	}

	/// <summary>获得指定数量的残骸。</summary>
	public void GainCorpses(int count)
	{
		if (ExtraCorpses) count *= 2;
		Corpses += count;
	}

	// ── 实体级历史追踪 ──

	/// <summary>本回合死亡的实体（回合开始时清空）。</summary>
	public List<BuffableEntity> DeathsThisTurn { get; } = new();
	/// <summary>本回合施放的法术（回合开始时清空）。</summary>
	public List<BuffableEntity> SpellsCastThisTurn { get; } = new();
	/// <summary>本回合打出的卡牌（回合开始时清空）。</summary>
	public List<BuffableEntity> CardsPlayedThisTurnEntities { get; } = new();
	/// <summary>本回合抽到的卡牌（回合开始时清空）。</summary>
	public List<BaseEntity> CardsDrawnThisTurnEntities { get; } = new();

	/// <summary>上回合死亡的实体。</summary>
	public List<BuffableEntity> DeathsLastTurn { get; set; } = new();
	/// <summary>上回合施放的法术。</summary>
	public List<BuffableEntity> SpellsCastLastTurn { get; set; } = new();
	/// <summary>上回合打出的卡牌。</summary>
	public List<BuffableEntity> CardsPlayedLastTurnEntities { get; set; } = new();

	/// <summary>整局游戏死亡的实体。</summary>
	public List<BuffableEntity> DeathsThisGame { get; } = new();
	/// <summary>整局游戏施放的法术。</summary>
	public List<BuffableEntity> SpellsCastThisGame { get; } = new();
	// CardsPlayedThisGame 已存在 (List<BaseEntity>)

	// ── Slot properties ──

	/// <summary>Force all targets to be chosen randomly (Yogg-Saron effect).</summary>
	public bool AllTargetsRandom { get; set; }

	/// <summary>Secrets trigger twice (e.g. secret trigger aura).</summary>
	public bool ExtraTriggerSecret { get; set; }

	/// <summary>Healing is converted to damage (Auchenai Soulpriest).</summary>
	public bool HealingAsDamage { get; set; }

	/// <summary>Spells cost health instead of mana.</summary>
	public bool SpellsCostHealth { get; set; }

	/// <summary>Murlocs cost health instead of mana.</summary>
	public bool MurlocsCostHealth { get; set; }

	/// <summary>Extra end turn effect (e.g. Drakkari Enchanter).</summary>
	public bool ExtraEndTurnEffect { get; set; }

	// ── C'Thun tracking ──

	/// <summary>The player's C'Thun entity (buffed throughout the game).</summary>
	public BaseEntity? CThun { get; set; }

	/// <summary>
	/// Copy all C'Thun buffs onto another card (e.g. when summoning C'Thun).
	/// </summary>
	public void CopyCThunBuff(BuffableEntity card)
	{
		if (CThun is not BuffableEntity cthun) return;
		foreach (var buff in cthun.Buffs.ToList())
		{
			if (buff is not Enchantment enc) continue;

			// Create a new enchantment copying the C'Thun buff's stat modifications
			var newBuff = new Enchantment();
			newBuff.SetIntProperty("atk", enc.GetIntProperty("atk"));
			newBuff.SetIntProperty("health", enc.GetIntProperty("health"));
			if (enc.GetBoolProperty("taunt")) newBuff.SetBoolProperty("taunt", true);

			newBuff.Apply(card);
		}
	}

	// ── Convenience: current player check ──

	/// <summary>True if this is the current player's turn.</summary>
	public bool IsCurrentPlayer => Game?.CurrentPlayer == this;

	/// <summary>Controller always returns self for Player.</summary>
	public override Player? Controller { get => this; set { } }

	// ── Characters ──

	/// <summary>All characters (hero + minions on field).</summary>
	public List<BaseEntity> Characters
	{
		get
		{
			var list = new List<BaseEntity>();
			if (Hero != null) list.Add(Hero);
			list.AddRange(Field);
			return list;
		}
	}

	/// <summary>All live entities (field + hero + weapon).</summary>
	public IEnumerable<BaseEntity> LiveEntities
	{
		get
		{
			foreach (var m in Field) yield return m;
			if (Hero != null) yield return Hero;
			if (Hero?.EquippedWeapon != null) yield return Hero.EquippedWeapon;
		}
	}

	/// <summary>Available minion slots on the board.</summary>
	public int MinionSlots => Math.Max(0, 7 - Field.Count);

	// ── Start hand size ──

	private int _startHandSize = 3;

	/// <summary>Starting hand size (3 for first player, 4 for second).</summary>
	public int StartHandSize => FirstPlayer ? _startHandSize : _startHandSize + 1;

	// ── Entity enumeration (recursive) ──

	/// <summary>
	/// All entities owned by this player (field minions + their sub-entities, secrets, hero, buffs, self).
	/// </summary>
	public new IEnumerable<BaseEntity> Entities
	{
		get
		{
			foreach (var m in Field)
			{
				yield return m;
				if (m is BuffableEntity be)
					foreach (var buff in be.Buffs) yield return buff;
			}
			foreach (var s in Secrets) yield return s;
			foreach (var buff in Buffs) yield return buff;
			if (Hero != null)
			{
				yield return Hero;
				if (Hero.Power != null) yield return Hero.Power;
				if (Hero.EquippedWeapon != null) yield return Hero.EquippedWeapon;
				foreach (var buff in Hero.Buffs) yield return buff;
			}
			yield return this;
		}
	}

	/// <summary>
	/// Actionable entities: characters + hand + hero power.
	/// </summary>
	public IEnumerable<BaseEntity> ActionableEntities
	{
		get
		{
			foreach (var c in Characters) yield return c;
			foreach (var c in Hand) yield return c;
			if (Hero?.Power != null) yield return Hero.Power;
		}
	}

	// ── Spell damage / heal calculations ──

	/// <summary>
	/// Calculate spell damage after applying spell power.
	/// </summary>
	public int GetSpellDamage(int amount)
	{
		amount += SpellPower;
		amount <<= SpellPowerDouble;
		return amount;
	}

	/// <summary>
	/// Calculate spell heal after applying healing bonuses.
	/// </summary>
	public int GetSpellHeal(int amount)
	{
		amount <<= HealingDouble;
		return amount;
	}

	/// <summary>Calculate hero power damage after bonuses.</summary>
	public int GetHeroPowerDamage(int amount)
	{
		amount += HeroPowerDamage;
		amount <<= HeroPowerDouble;
		return amount;
	}

	/// <summary>Calculate hero power heal after bonuses.</summary>
	public int GetHeroPowerHeal(int amount)
	{
		amount <<= HeroPowerDouble;
		return amount;
	}

	// ── Mana operations ──

	/// <summary>Check if the player can pay the mana cost of a card.</summary>
	public bool CanPayCost(int cost) => Mana >= cost;

	/// <summary>
	/// Spend mana. Consumes temp mana first, then regular mana.
	/// Returns actual mana spent.
	/// </summary>
	public int PayCost(int amount)
	{
		if (TempMana > 0)
		{
			int fromTemp = Math.Min(TempMana, amount);
			TempMana -= fromTemp;
			amount -= fromTemp;
		}
		UsedMana += amount;
		return amount;
	}

	/// <summary>
	/// Create a card entity from CardData and register it in the game.
	/// Optionally place it into a zone (default: hand).
	/// </summary>
	public BuffableEntity? CreateCard(LoadCardIntoDbf.Card cardData, Zone zone = Zone.Hand)
	{
		var entity = CardFactory.Create(cardData);

		entity.Controller = this;

		Game?.RegisterEntity(entity);
		entity.SetZone(zone, this);

		// 在创建时立即注册所有事件订阅（不依赖区域）
		// EventBus.IsInActiveZone 会根据 Subscription.ActiveZones 过滤
		if (Game != null)
		{
			foreach (var script in entity.GetAllScripts())
				script.OnActivate?.Invoke(entity, Game);
		}

		return entity;
	}

	/// <summary>
	/// Create a card from a card ID string. Looks up the CardDb on the Game.
	/// Returns null if the card ID is not found.
	/// </summary>
	public BuffableEntity? CreateCard(string cardId, Zone zone = Zone.Hand)
	{
		var cardData = Game?.CardDb != null && Game.CardDb.TryGetValue(cardId, out var c)
			? c : null;
		if (cardData == null) return null;
		return CreateCard(cardData, zone);
	}

	// ── Draw ──

	/// <summary>
	/// Draw one card from the deck. Returns the drawn entity, or null on fatigue.
	/// </summary>
	public BaseEntity? DrawCard(BaseEntity? source = null)
	{
		if (CantDraw)
		{
			_log.Debug("{Player} can't draw", Name);
			return null;
		}

		if (Deck.Count == 0)
		{
			// Fatigue
			if (!CantFatigue)
			{
				FatigueCounter++;
				_log.Debug("{Player} fatigues for {Damage}", Name, FatigueCounter);
				Hero?.Hit(FatigueCounter);
			}
			return null;
		}

		// Draw from top of deck
		var card = Deck[0];

		if (Hand.Count >= MaxHandSize)
		{
			// Hand is full — mill (burn) the card, goes to SetAside (not graveyard)
			_log.Debug("{Player} hand is full, milling card", Name);
			Deck.RemoveAt(0);
			if (card is BuffableEntity millBe)
				millBe.Zone = Zone.SetAside;

			// 发布焚烧事件
			Game?.EventBus.Publish(
				new MillEvent(card is BuffableEntity mbe ? mbe : Hero!, this, card),
				MiscPhase.On);
			return null;
		}

		// Ensure the card is properly removed from Deck before zone transition.
		// Cards may have Zone = Invalid if they were added to Deck without SetZone,
		// so we manually remove from Deck first, then transition to Hand.
		Deck.RemoveAt(0);

		// ── "抽到时" 触发（加入手牌前） ──
		if (card is BuffableEntity drawBe)
		{
			var cardId = drawBe.CardId ?? "";
			var script = Dsl.CardScriptRegistry.Instance.GetScript(cardId);
			if (script != null && script.Draw.Length > 0)
			{
				_log.Debug("{Player} triggers Draw effect for {Card}", Name, cardId);
				Game?.QueueActions(drawBe, script.Draw.ToList<Dsl.Action>());

				// "施放时抽到" 类型的卡不进手牌（如地雷、燃烧卷轴等）
				// 如果脚本把卡移到了其他区域（如 Graveyard），不再加入手牌
				if (drawBe.Zone != Zone.Deck && drawBe.Zone != Zone.Invalid)
				{
					CardsDrawnThisTurn++;
					CardsDrawnThisTurnEntities.Add(card);
					Game?.EventBus.Publish(
						new DrawCardEvent(source ?? this, this, card), MiscPhase.On);
					return card;
				}
			}
		}

		// ── 正常加入手牌 ──
		if (card is BuffableEntity buffable)
		{
			buffable.Controller ??= this;
			// Manually add to Hand and update zone tracking
			Hand.Add(buffable);
			buffable.OldZone = Zone.Deck;
			buffable.Zone = Zone.Hand;
		}
		else
		{
			Hand.Add(card);
		}
		CardsDrawnThisTurn++;
		CardsDrawnThisTurnEntities.Add(card);
		_log.Debug("{Player} draws a card (hand size {HandSize})", Name, Hand.Count);

		// 发布抽牌事件
		Game?.EventBus.Publish(
			new DrawCardEvent(source ?? this, this, card),
			MiscPhase.On);

		return card;
	}

	/// <summary>
	/// Draw a specific card from the deck matching the filter.
	/// Searches the deck for the first match, moves it to the top, then draws normally.
	/// Returns null if no matching card is found (does NOT trigger fatigue).
	/// </summary>
	public BaseEntity? DrawCard(Func<BaseEntity, bool> filter, BaseEntity? source = null)
	{
		var index = -1;
		for (int i = 0; i < Deck.Count; i++)
		{
			if (filter(Deck[i]))
			{
				index = i;
				break;
			}
		}
		if (index < 0) return null;

		// Move matching card to top of deck, then draw normally
		if (index > 0)
		{
			var card = Deck[index];
			Deck.RemoveAt(index);
			Deck.Insert(0, card);
		}
		return DrawCard(source);
	}

	// ── Summon ──

	/// <summary>Summon an entity onto the battlefield at a specific position.</summary>
	/// <param name="entity">Entity to summon.</param>
	/// <param name="index">Position in Field to insert at. -1 = rightmost (append).</param>
	public bool Summon(BaseEntity entity, int index = -1)
	{
		if (Field.Count >= 7)
		{
			_log.Debug("{Player} board is full, can't summon", Name);
			return false;
		}
		if (entity is Character c)
			c.Controller = this;

		if (entity is BuffableEntity be)
		{
			be.Controller = this;
			if (be.Zone != Zone.Play)
			{
				be.SetZone(Zone.Play, this);
			}
			else if (!Field.Contains(entity))
			{
				if (index >= 0 && index <= Field.Count)
					Field.Insert(index, entity);
				else
					Field.Add(entity);
			}

			if (index >= 0 && index < Field.Count)
			{
				Field.Remove(entity);
				Field.Insert(index, entity);
			}
		}
		else
		{
			if (index >= 0 && index <= Field.Count)
				Field.Insert(index, entity);
			else
				Field.Add(entity);
		}

		// Recalculate ZonePosition for all minions on the field
		for (int i = 0; i < Field.Count; i++)
		{
			if (Field[i] is Minion m)
				m.ZonePosition = i + 1;
		}

		_log.Debug("{Player} summons entity {EntityId} at index {Index}",
			Name, entity.EntityId, index);
		return true;
	}

	public void TrackSummonedMinion(BuffableEntity entity)
	{
		if (entity is not Minion) return;
		if (SummonedMinionsThisGame.Contains(entity)) return;
		SummonedMinionsThisGame.Add(entity);
	}

	// ── Concede ──

	/// <summary>Concede the game.</summary>
	public void Concede()
	{
		PlayState = PlayState.Conceded;
		_log.Information("{Player} concedes", Name);
	}

	// ── Shuffle ──

	/// <summary>Shuffle the deck using the game's random instance.</summary>
	public void ShuffleDeck()
	{
		if (Game == null) return;
		var rng = Game.Random;
		int n = Deck.Count;
		while (n > 1)
		{
			n--;
			int k = rng.Next(n + 1);
			(Deck[k], Deck[n]) = (Deck[n], Deck[k]);
		}
		_log.Debug("{Player} shuffles their deck", Name);
	}

	// ── Discard hand ──

	/// <summary>Discard the entire hand (标记弃牌、发布事件).</summary>
	public void DiscardHand()
	{
		_log.Debug("{Player} discards their entire hand", Name);
		foreach (var card in Hand.ToList())
		{
			Hand.Remove(card);
			Graveyard.Add(card);
			if (card is BuffableEntity be)
			{
				be.SetBoolProperty("discarded", true);
				DiscardedCards.Add(be);
				Game?.EventBus.Publish(
					new Events.DiscardEvent(be, this, be), Events.MiscPhase.On);
			}
		}
	}

	// ── Give ──

	/// <summary>
	/// Give a card by ID to this player's hand.
	/// </summary>
	public BuffableEntity? Give(string cardId)
	{
		return CreateCard(cardId, Zone.Hand);
	}

	// ── Steal ──

	/// <summary>
	/// Steal a card from the opponent's board.
	/// Moves the entity from opponent's field to this player's field.
	/// </summary>
	public bool Steal(BaseEntity card)
	{
		if (Field.Count >= 7)
		{
			_log.Debug("{Player} board is full, can't steal", Name);
			return false;
		}

		var oldOwner = (card as BuffableEntity)?.Controller;
		if (oldOwner != null)
		{
			oldOwner.Field.Remove(card);
			oldOwner.Hand.Remove(card);
		}

		if (card is BuffableEntity be)
		{
			be.Controller = this;
			be.Zone = Zone.Play;
		}
		Field.Add(card);

		_log.Debug("{Player} steals entity {EntityId}", Name, card.EntityId);
		return true;
	}

	// ── PrepareForGame ──

	/// <summary>
	/// Prepare this player for a game: create hero, populate deck from StartingDeck,
	/// shuffle, set PlayState.
	/// </summary>
	public void PrepareForGame()
	{
		if (Game == null) return;

		// Create hero from StartingHero (if specified and CardDb is available)
		if (StartingHero != null && Hero == null && Game.CardDb != null)
		{
			var heroData = Game.CardDb.TryGetValue(StartingHero, out var hd) ? hd : null;
			if (heroData != null)
			{
				var hero = new Hero { Controller = this };
				Game.RegisterEntity(hero);
				hero.MaxHealth = heroData.Health > 0 ? heroData.Health : 30;
				hero.CardData = heroData;
				Hero = hero;

				// Create hero power if defined
				// Tag 380 (GameTag.HeroPower) stores the hero power's DbfId as an int
				LoadCardIntoDbf.Card? hpData = null;
				if (heroData.TagsInt.TryGetValue(GameTag.HeroPower, out var hpDbfId) && hpDbfId > 0)
				{
					Game.CardDbByDbfId?.TryGetValue(hpDbfId, out hpData);
				}
				if (hpData != null)
				{
					var power = new HeroPower { Controller = this, CardData = hpData };
					Game.RegisterEntity(power);
					power.Cost = hpData.Cost;
					hero.Power = power;
				}
			}
		}

		// Populate deck from StartingDeck card IDs
		if (StartingDeck != null && Game.CardDb != null)
		{
			foreach (var cardId in StartingDeck)
			{
				var cardData = Game.CardDb.TryGetValue(cardId, out var cd) ? cd : null;
				if (cardData != null)
					CreateCard(cardData, Zone.Deck);
			}
		}

		ShuffleDeck();
		PlayState = PlayState.Playing;

		_log.Information("{Player} prepared for game. Hero={Hero}, Deck={DeckSize} cards",
			Name, Hero?.CardData?.CardId ?? "none", Deck.Count);
	}

	// ── Dump ──

	public override Dictionary<string, object> Dump()
	{
		var data = base.Dump();
		data["name"] = Name;
		data["max_mana"] = MaxMana;
		data["mana"] = Mana;
		data["deck_size"] = Deck.Count;
		data["hand_size"] = Hand.Count;
		data["field_size"] = Field.Count;
		data["fatigue_counter"] = FatigueCounter;
		data["playstate"] = (int)PlayState;
		if (Hero != null) data["hero"] = Hero.Dump();
		return data;
	}

	public override string ToString() => Name;
}
