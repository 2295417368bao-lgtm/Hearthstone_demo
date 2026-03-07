using System.Linq;
using HearthstoneServer.Entities;

namespace HearthstoneServer.Dsl;

/// <summary>
/// Predefined selector constants —.
/// Import via: using static HearthstoneServer.Dsl.S;
/// Usage: S.FRIENDLY_MINIONS, S.ENEMY_HERO, etc.
/// </summary>
public static class S
{
	// ── AttrValues (comparable entity properties) ──

	public static readonly AttrValue ARMOR = new(GameTag.Armor);
	public static readonly AttrValue ATK = new(GameTag.Atk);
	public static readonly AttrValue CONTROLLER_ATTR = new(GameTag.Controller);
	public static readonly AttrValue MAX_HEALTH = new(GameTag.Health);
	public static readonly AttrValue CURRENT_HEALTH = new("health");
	public static readonly AttrValue CURRENT_DURABILITY = new("durability");
	public static readonly AttrValue COST = new(GameTag.Cost);
	public static readonly AttrValue DAMAGE = new(GameTag.Damage);
	public static readonly AttrValue MANA = new(GameTag.Resources);

	// ── Special selectors ──

	/// <summary>The source entity itself.</summary>
	public static readonly Selector SELF = new FuncSelector(
		(_, src) => new List<BaseEntity> { src });

	/// <summary>Action 回调上下文中当前操作目标（由 DealDamage/DrawCard 等在 .Then() 回调前设置）。</summary>
	public static readonly Selector CURRENT_TARGET = new FuncSelector(
		(_, src) => src.CurrentTarget != null
			? new List<BaseEntity> { src.CurrentTarget }
			: new List<BaseEntity>());

	/// <summary>事件发起者（攻击者、施法者、打出的随从）。由 BusEvent.When 设置。</summary>
	public static readonly Selector EVT_SOURCE = new FuncSelector(
		(_, src) => src.EventSource != null
			? new List<BaseEntity> { src.EventSource }
			: new List<BaseEntity>());

	/// <summary>事件目标/主体（防御者、死亡实体）。由 BusEvent.When 设置。</summary>
	public static readonly Selector EVT_TARGET = new FuncSelector(
		(_, src) => src.EventTarget != null
			? new List<BaseEntity> { src.EventTarget }
			: new List<BaseEntity>());

	/// <summary>所有已 Pick 的卡牌。</summary>
	public static readonly Selector PICKED = new FuncSelector(
		(_, src) => new List<BaseEntity>(src.PickedCards));

	/// <summary>第 1 次 Pick 的卡牌。</summary>
	public static readonly Selector PICKED_1 = new FuncSelector(
		(_, src) => src.PickedCards.Count > 0
			? new List<BaseEntity> { src.PickedCards[0] }
			: new List<BaseEntity>());

	/// <summary>第 2 次 Pick 的卡牌。</summary>
	public static readonly Selector PICKED_2 = new FuncSelector(
		(_, src) => src.PickedCards.Count > 1
			? new List<BaseEntity> { src.PickedCards[1] }
			: new List<BaseEntity>());

	/// <summary>第 3 次 Pick 的卡牌。</summary>
	public static readonly Selector PICKED_3 = new FuncSelector(
		(_, src) => src.PickedCards.Count > 2
			? new List<BaseEntity> { src.PickedCards[2] }
			: new List<BaseEntity>());

	/// <summary>CurrentTarget 的控制者（ForEach 遍历时引用当前实体的 Controller）。</summary>
	public static readonly Selector CURRENT_CONTROLLER = new FuncSelector(
		(_, src) =>
		{
			var ctrl = (src.CurrentTarget as BuffableEntity)?.Controller;
			return ctrl != null ? new List<BaseEntity> { ctrl } : new List<BaseEntity>();
		});

	/// <summary>The owner of the source entity (for enchantments: the entity the buff is attached to).</summary>
	public static readonly Selector OWNER = new FuncSelector(
		(_, src) =>
		{
			// For enchantments, OWNER is the entity this buff is attached to
			if (src is Enchantment ench && ench.Owner != null)
				return new List<BaseEntity> { ench.Owner };
			// Fallback to controller (Player)
			if (src is BuffableEntity be && be.Controller != null)
				return new List<BaseEntity> { be.Controller };
			return new List<BaseEntity>();
		});

	// ── Cost filter selectors ──

	/// <summary>费用 ≤ max 的实体。</summary>
	public static Selector MaxCost(int max) => new FilterSelector(
		(e, _) => e is BuffableEntity be && be.Cost <= max);

	/// <summary>费用 ≥ min 的实体。</summary>
	public static Selector MinCost(int min) => new FilterSelector(
		(e, _) => e is BuffableEntity be && be.Cost >= min);

	/// <summary>匹配指定 cardId 的实体。</summary>
	public static Selector CardId(string cardId) => new FilterSelector(
		(e, _) => (e as BuffableEntity)?.CardId == cardId);

	// ── Type selectors (EnumSelector on CardType) ──

	public static readonly Selector GAME = new EnumSelector(CardType.Game);
	public static readonly Selector PLAYER = new EnumSelector(CardType.Player);
	public static readonly Selector HERO = new EnumSelector(CardType.Hero);
	public static readonly Selector MINION = new EnumSelector(CardType.Minion);
	public static readonly Selector SPELL = new EnumSelector(CardType.Spell);
	public static readonly Selector WEAPON = new EnumSelector(CardType.Weapon);
	public static readonly Selector HERO_POWER = new EnumSelector(CardType.HeroPower);
	public static readonly Selector LOCATION = new EnumSelector(CardType.Location);
	public static readonly Selector CHARACTER = MINION | HERO;

	// ── Zone selectors ──

	public static readonly Selector IN_PLAY = new EnumSelector(Zone.Play);
	public static readonly Selector IN_DECK = new EnumSelector(Zone.Deck);
	public static readonly Selector IN_HAND = new EnumSelector(Zone.Hand);
	public static readonly Selector IN_SECRET = new EnumSelector(Zone.Secret);

	// ── Tag selectors (EnumSelector on GameTag — tests for truthy tag) ──

	public static readonly Selector BATTLECRY = new EnumSelector(GameTag.Battlecry);
	public static readonly Selector CHARGE = new EnumSelector(GameTag.Charge);
	public static readonly Selector COMBO = new EnumSelector(GameTag.Combo);
	public static readonly Selector DAMAGED = new EnumSelector(GameTag.Damage);
	public static readonly Selector DEATHRATTLE = new EnumSelector(GameTag.Deathrattle);
	public static readonly Selector DIVINE_SHIELD = new EnumSelector(GameTag.DivineShield);
	public static readonly Selector FROZEN = new EnumSelector(GameTag.Freeze);
	public static readonly Selector OVERLOAD_TAG = new EnumSelector(GameTag.Overload);
	public static readonly Selector SPELLPOWER = new EnumSelector(GameTag.Spellpower);
	public static readonly Selector STEALTH = new EnumSelector(GameTag.Stealth);
	public static readonly Selector TAUNT = new EnumSelector(GameTag.Taunt);
	public static readonly Selector WINDFURY = new EnumSelector(GameTag.Windfury);
	public static readonly Selector DORMANT = new EnumSelector(GameTag.Dormant);
	public static readonly Selector LIFESTEAL = new EnumSelector(GameTag.Lifesteal);
	public static readonly Selector IMMUNE = new EnumSelector(GameTag.Immune);
	public static readonly Selector RUSH = new EnumSelector(GameTag.Rush);
	public static readonly Selector ECHO = new EnumSelector(GameTag.Echo);
	public static readonly Selector REBORN = new EnumSelector(GameTag.Reborn);
	public static readonly Selector SECRET = new EnumSelector(GameTag.Secret);
	public static readonly Selector QUEST = new EnumSelector(GameTag.Quest);

	// ── Race selectors ──

	public static readonly Selector BEAST = new EnumSelector(Race.Beast);
	public static readonly Selector DEMON = new EnumSelector(Race.Demon);
	public static readonly Selector DRAGON = new EnumSelector(Race.Dragon);
	public static readonly Selector MECH = new EnumSelector(Race.Mechanical);
	public static readonly Selector MURLOC = new EnumSelector(Race.Murloc);
	public static readonly Selector PIRATE = new EnumSelector(Race.Pirate);
	public static readonly Selector TOTEM = new EnumSelector(Race.Totem);
	public static readonly Selector ELEMENTAL = new EnumSelector(Race.Elemental);
	public static readonly Selector UNDEAD = new EnumSelector(Race.Undead);

	// ── Rarity selectors ──

	public static readonly Selector COMMON = new EnumSelector(Rarity.Common);
	public static readonly Selector RARE = new EnumSelector(Rarity.Rare);
	public static readonly Selector EPIC = new EnumSelector(Rarity.Epic);
	public static readonly Selector LEGENDARY = new EnumSelector(Rarity.Legendary);

	// ── Controller selectors (FRIENDLY / ENEMY) ──
	// These use ComparisonSelector comparing CONTROLLER tag.
	// Full implementation requires Player references; simplified here.

	/// <summary>
	/// Selects entities controlled by the same player as source.
	/// Implementation: entity.Tags[CONTROLLER] == source.Tags[CONTROLLER].
	/// </summary>
	public static readonly Selector FRIENDLY = new FilterSelector(
				(entity, source) =>
		{
			var ec = (entity as BuffableEntity)?.Controller;
			var sc = (source as BuffableEntity)?.Controller;
			return ec != null && sc != null && ec == sc;
		});

	/// <summary>
	/// Selects entities controlled by the opponent of source.
	/// </summary>
	public static readonly Selector ENEMY = new FilterSelector(
				(entity, source) =>
		{
			var ec = (entity as BuffableEntity)?.Controller;
			var sc = (source as BuffableEntity)?.Controller;
			return ec != null && sc != null && ec != sc;
		});

	// ── Composite "ALL" selectors ──

	public static readonly Selector ALL_PLAYERS = IN_PLAY + PLAYER;
	public static readonly Selector ALL_HEROES = IN_PLAY + HERO;
	public static readonly Selector ALL_MINIONS = IN_PLAY + MINION - DORMANT;
	public static readonly Selector ALL_CHARACTERS = IN_PLAY + CHARACTER - DORMANT;
	public static readonly Selector ALL_WEAPONS = IN_PLAY + WEAPON;
	public static readonly Selector ALL_SECRETS = IN_SECRET + SECRET;
	public static readonly Selector ALL_QUESTS = IN_SECRET + QUEST;
	public static readonly Selector ALL_HERO_POWERS = IN_PLAY + HERO_POWER;
	public static readonly Selector ALL_LOCATIONS = IN_PLAY + LOCATION;

	// ── Controller-scoped composites ──

	public static readonly Selector CONTROLLER = ALL_PLAYERS + FRIENDLY;
	public static readonly Selector OPPONENT = ALL_PLAYERS + ENEMY;

	public static readonly Selector FRIENDLY_HAND = IN_HAND + FRIENDLY;
	public static readonly Selector FRIENDLY_DECK = IN_DECK + FRIENDLY;
	public static readonly Selector FRIENDLY_HERO = IN_PLAY + FRIENDLY + HERO;
	public static readonly Selector FRIENDLY_MINIONS = ALL_MINIONS + FRIENDLY;
	public static readonly Selector FRIENDLY_CHARACTERS = ALL_CHARACTERS + FRIENDLY;
	public static readonly Selector FRIENDLY_WEAPON = ALL_WEAPONS + FRIENDLY;
	public static readonly Selector FRIENDLY_SECRETS = ALL_SECRETS + FRIENDLY;
	public static readonly Selector FRIENDLY_HERO_POWER = ALL_HERO_POWERS + FRIENDLY;
	public static readonly Selector FRIENDLY_LOCATIONS = ALL_LOCATIONS + FRIENDLY;

	public static readonly Selector ENEMY_HAND = IN_HAND + ENEMY;
	public static readonly Selector ENEMY_DECK = IN_DECK + ENEMY;
	public static readonly Selector ENEMY_HERO = ALL_HEROES + ENEMY;
	public static readonly Selector ENEMY_MINIONS = ALL_MINIONS + ENEMY;
	public static readonly Selector ENEMY_CHARACTERS = ALL_CHARACTERS + ENEMY;
	public static readonly Selector ENEMY_WEAPON = ALL_WEAPONS + ENEMY;
	public static readonly Selector ENEMY_SECRETS = ALL_SECRETS + ENEMY;
	public static readonly Selector ENEMY_HERO_POWER = ALL_HERO_POWERS + ENEMY;
	public static readonly Selector ENEMY_LOCATIONS = ALL_LOCATIONS + ENEMY;

	// ── Random composites ──

	public static readonly Selector RANDOM_MINION = Random(ALL_MINIONS);
	public static readonly Selector RANDOM_CHARACTER = Random(ALL_CHARACTERS);
	public static readonly Selector RANDOM_OTHER_CHARACTER = Random(ALL_CHARACTERS - SELF);
	public static readonly Selector RANDOM_FRIENDLY_MINION = Random(FRIENDLY_MINIONS);
	public static readonly Selector RANDOM_OTHER_MINION = Random(ALL_MINIONS - SELF);
	public static readonly Selector RANDOM_ENEMY_MINION = Random(ENEMY_MINIONS);
	public static readonly Selector RANDOM_ENEMY_CHARACTER = Random(ENEMY_CHARACTERS);
	public static readonly Selector RANDOM_FRIENDLY_CHARACTER = Random(FRIENDLY_CHARACTERS);

	// ── Misc ──

	public static readonly Selector DAMAGED_CHARACTERS = ALL_CHARACTERS + DAMAGED;
	public static readonly Selector RANDOM_OTHER_FRIENDLY_MINION = Random(FRIENDLY_MINIONS - SELF);
	public static readonly Selector FRIENDLY_GRAVEYARD = new EnumSelector(Zone.Graveyard) + FRIENDLY;
	public static readonly Selector OVERLOAD = OVERLOAD_TAG; // alias
	public static readonly Selector KILLED_THIS_TURN = new FuncSelector(
		(entities, src) =>
		{
			int turn = src.Game?.Turn ?? -1;
			return entities
				.Where(e => e is BuffableEntity be
					&& be.Zone == Zone.Graveyard
					&& be.TurnPlayed == turn)
				.ToList();
		});

	/// <summary>本场战斗所有已死亡实体（Zone.Graveyard 中的所有实体）。</summary>
	public static readonly Selector KILLED = new EnumSelector(Zone.Graveyard);

	// ── KILLED 组合 ──
	public static readonly Selector FRIENDLY_KILLED = KILLED + FRIENDLY;
	public static readonly Selector FRIENDLY_KILLED_MINION = KILLED + FRIENDLY + MINION;
	public static readonly Selector FRIENDLY_KILLED_DEATHRATTLE_MINION = KILLED + FRIENDLY + MINION + DEATHRATTLE;

	// ── Target selector (set at play time) ──

	/// <summary>
	/// The primary target of the source entity (first target).
	/// Reads from source.CurrentTargets[0] (set by Game.ActionBlock / Game.PlayCard).
	/// </summary>
	public static readonly Selector TARGET = new FuncSelector(
		(_, src) =>
		{
			if (src.CurrentTargets.Count > 0)
				return new List<BaseEntity> { src.CurrentTargets[0] };
			return new List<BaseEntity>();
		});

	/// <summary>
	/// The secondary target (second target for multi-target cards like Chillin' Vol'jin).
	/// Reads from source.CurrentTargets[1].
	/// </summary>
	public static readonly Selector TARGET_2 = new FuncSelector(
		(_, src) =>
		{
			if (src.CurrentTargets.Count > 1)
				return new List<BaseEntity> { src.CurrentTargets[1] };
			return new List<BaseEntity>();
		});

	// ── Board position ──

	public static Selector LeftOf(Selector s) => new BoardPositionSelector(BoardDirection.Left, s);
	public static Selector RightOf(Selector s) => new BoardPositionSelector(BoardDirection.Right, s);
	public static Selector Adjacent(Selector s) => LeftOf(s) | RightOf(s);

	public static readonly Selector SELF_ADJACENT = Adjacent(SELF);
	public static readonly Selector TARGET_ADJACENT = Adjacent(TARGET);

	// ── Helper methods ──

	/// <summary>Create a random selector picking N from the child.</summary>
	public static RandomSelector Random(Selector child, int times = 1, bool distinctByType = false)
		=> new(child, times, distinctByType);

	/// <summary>Create an ID-matching filter selector.</summary>
	public static Selector Id(string id)
		=> new FilterSelector((e, _) => e.Data?.CardId == id);

	/// <summary>Create an ID-matching filter selector (uppercase alias).</summary>
	public static Selector ID(string id) => Id(id);

	// ── 实体历史选择器 ──
	// 用法: Count(FRIENDLY_DEATHS_THIS_TURN + ID("BRMA04_3"))

	// 回合级 — 本回合
	public static readonly Selector FRIENDLY_DEATHS_THIS_TURN = new FuncSelector(
		(_, src) => PlayerOf(src)?.DeathsThisTurn.Cast<BaseEntity>().ToList() ?? new());
	public static readonly Selector ENEMY_DEATHS_THIS_TURN = new FuncSelector(
		(_, src) => PlayerOf(src)?.Opponent?.DeathsThisTurn.Cast<BaseEntity>().ToList() ?? new());
	public static readonly Selector FRIENDLY_SPELLS_THIS_TURN = new FuncSelector(
		(_, src) => PlayerOf(src)?.SpellsCastThisTurn.Cast<BaseEntity>().ToList() ?? new());
	public static readonly Selector ENEMY_SPELLS_THIS_TURN = new FuncSelector(
		(_, src) => PlayerOf(src)?.Opponent?.SpellsCastThisTurn.Cast<BaseEntity>().ToList() ?? new());
	public static readonly Selector FRIENDLY_CARDS_PLAYED_THIS_TURN = new FuncSelector(
		(_, src) => PlayerOf(src)?.CardsPlayedThisTurnEntities.Cast<BaseEntity>().ToList() ?? new());
	public static readonly Selector ENEMY_CARDS_PLAYED_THIS_TURN = new FuncSelector(
		(_, src) => PlayerOf(src)?.Opponent?.CardsPlayedThisTurnEntities.Cast<BaseEntity>().ToList() ?? new());
	public static readonly Selector FRIENDLY_CARDS_DRAWN_THIS_TURN = new FuncSelector(
		(_, src) => PlayerOf(src)?.CardsDrawnThisTurnEntities.ToList() ?? new());

	// 回合级 — 上回合
	public static readonly Selector FRIENDLY_DEATHS_LAST_TURN = new FuncSelector(
		(_, src) => PlayerOf(src)?.DeathsLastTurn.Cast<BaseEntity>().ToList() ?? new());
	public static readonly Selector FRIENDLY_SPELLS_LAST_TURN = new FuncSelector(
		(_, src) => PlayerOf(src)?.SpellsCastLastTurn.Cast<BaseEntity>().ToList() ?? new());
	public static readonly Selector FRIENDLY_CARDS_PLAYED_LAST_TURN = new FuncSelector(
		(_, src) => PlayerOf(src)?.CardsPlayedLastTurnEntities.Cast<BaseEntity>().ToList() ?? new());
	public static readonly Selector ENEMY_CARDS_PLAYED_LAST_TURN = new FuncSelector(
		(_, src) => PlayerOf(src)?.Opponent?.CardsPlayedLastTurnEntities.Cast<BaseEntity>().ToList() ?? new());

	// 全局 — 整局游戏
	public static readonly Selector FRIENDLY_DEATHS_THIS_GAME = new FuncSelector(
		(_, src) => PlayerOf(src)?.DeathsThisGame.Cast<BaseEntity>().ToList() ?? new());
	public static readonly Selector FRIENDLY_SPELLS_THIS_GAME = new FuncSelector(
		(_, src) => PlayerOf(src)?.SpellsCastThisGame.Cast<BaseEntity>().ToList() ?? new());
	public static readonly Selector ENEMY_SPELLS_THIS_GAME = new FuncSelector(
		(_, src) => PlayerOf(src)?.Opponent?.SpellsCastThisGame.Cast<BaseEntity>().ToList() ?? new());

	/// <summary>获取 source entity 的 Controller Player。</summary>
	private static Player? PlayerOf(BaseEntity src) =>
		(src as BuffableEntity)?.Controller ?? (src as Player);

	// ── Hand position selectors ──

	/// <summary>手牌最左边的牌。</summary>
	public static readonly Selector LEFTMOST_HAND = new FuncSelector(
		(_, src) =>
		{
			var hand = PlayerOf(src)?.Hand;
			if (hand == null || hand.Count == 0) return new();
			return new List<BaseEntity> { hand[0] };
		});

	/// <summary>手牌最右边的牌。</summary>
	public static readonly Selector RIGHTMOST_HAND = new FuncSelector(
		(_, src) =>
		{
			var hand = PlayerOf(src)?.Hand;
			if (hand == null || hand.Count == 0) return new();
			return new List<BaseEntity> { hand[^1] };
		});
}
