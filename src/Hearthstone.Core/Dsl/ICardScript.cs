using HearthstoneServer.Auras;
using HearthstoneServer.Entities;
using HearthstoneServer.Events;
using HearthstoneServer.Targeting;

namespace HearthstoneServer.Dsl;

// ═══════════════════════════════════════════════════
// Card Script Framework — interfaces & attributes
// ═══════════════════════════════════════════════════

/// <summary>
/// Attribute to register a card script class with a card ID.
/// Usage: [CardScript("CS2_029")] class Fireball : CardScriptBase { ... }
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public class CardScriptAttribute : Attribute
{
	public string CardId { get; }
	public CardScriptAttribute(string cardId) => CardId = cardId;
}

/// <summary>
/// Interface for card scripts. Each property returns the actions/events
/// associated with a card effect trigger.
/// </summary>
public interface ICardScript
{
	/// <summary>法术效果 / 通用打出效果。</summary>
	Action[] Play { get; }

	/// <summary>战吼效果（仅从手牌打出随从/武器/英雄时触发，受布莱恩加倍）。</summary>
	Action[] Battlecry { get; }

	/// <summary>
	/// Activation callback — called when the entity enters play (or secret zone).
	/// Used to register EventBus subscriptions. Returns IDisposable handles for cleanup.
	/// </summary>
	System.Action<BuffableEntity, Game>? OnActivate { get; }

	/// <summary>Actions triggered when the entity dies.</summary>
	Action[] Deathrattle { get; }

	/// <summary>Combo actions (triggered if another card was played this turn).</summary>
	Action[] Combo { get; }

	/// <summary>Inspire actions (triggered when hero power is used).</summary>
	Action[] Inspire { get; }

	/// <summary>Aura update definition (continuous effects).</summary>
	Refresh[] Update { get; }

	/// <summary>目标选择与打出条件。</summary>
	TargetRequirement TargetReq { get; }

	/// <summary>Choose-one card IDs.</summary>
	string[] ChooseCards { get; }

	/// <summary>Entourage card IDs.</summary>
	string[] Entourage { get; }

	/// <summary>Enrage aura — applied while entity is damaged (Refresh actions).</summary>
	Refresh[] Enrage { get; }

	/// <summary>Powered-up evaluator — condition for enhanced play effect.</summary>


	/// <summary>Cost modifier — lazy num subtracted from card cost.</summary>
	LazyNum? CostMod { get; }

	/// <summary>Static tags applied by this enchantment script.</summary>
	Dictionary<GameTag, object>? Tags { get; }

	/// <summary>Max health modifier function for enchantments (e.g. SET(1)).</summary>
	Func<BaseEntity, int, int>? MaxHealthMod { get; }

	/// <summary>Attack modifier function for enchantments (e.g. SET(1)).</summary>
	Func<BaseEntity, int, int>? AtkMod { get; }

	/// <summary>Cost modifier function for enchantments.</summary>
	Func<BaseEntity, int, int>? CostModFunc { get; }

	/// <summary>Durability modifier function for enchantments.</summary>
	Func<BaseEntity, int, int>? DurabilityMod { get; }

	/// <summary>Armor modifier function for enchantments.</summary>
	Func<BaseEntity, int, int>? ArmorMod { get; }

	// ── New hooks  ──

	/// <summary>Hero power activate effect.</summary>
	Action[] Activate { get; }

	/// <summary>地标使用效果 — 每次使用地标时触发的行动。</summary>
	Action[] Location { get; }

	/// <summary>Outcast effect — triggered when card is played from leftmost/rightmost hand position (Demon Hunter).</summary>
	Action[] Outcast { get; }

	/// <summary>Awaken effect — triggered when a dormant minion wakes up.</summary>
	Action[] Awaken { get; }

	/// <summary>Quest reward — actions triggered when quest is completed.</summary>
	Action[] Reward { get; }

	/// <summary>Quest progress handler — triggered on quest counter increment.</summary>
	Action[] AddProgress { get; }

	/// <summary>Discard trigger — actions when this card is discarded from hand.</summary>
	Action[] Discard { get; }

	/// <summary>Secret deathrattles — indexed deathrattle variants (e.g. for Secrets that choose a deathrattle).</summary>
	Action[][] SecretDeathrattles { get; }

	/// <summary>Overkill effect — triggered when damage dealt to a minion exceeds its remaining health.</summary>
	Action[] Overkill { get; }

	/// <summary>Draw trigger — actions when this card is drawn.</summary>
	Action[] Draw { get; }

	/// <summary>Hand zone aura — continuous effects while card is in hand (e.g. cost reduction).</summary>
	Refresh[] HandUpdate { get; }

	/// <summary>Deck zone aura — continuous effects while card is in deck.</summary>
	Refresh[] DeckUpdate { get; }

	/// <summary>Quest completion threshold — number of progress ticks needed to complete.</summary>
	int ProgressTotal { get; }

	/// <summary>Number of turns a minion stays dormant before awakening.</summary>
	int DormantTurns { get; }
}

/// <summary>
/// Base class with empty defaults for all script hooks.
/// Card scripts inherit from this and override only what they need.
/// </summary>
public abstract class CardScriptBase : ICardScript
{
	public virtual Action[] Play => Array.Empty<Action>();
	public virtual Action[] Battlecry => Array.Empty<Action>();
	public virtual System.Action<BuffableEntity, Game>? OnActivate => null;
	public virtual Action[] Deathrattle => Array.Empty<Action>();
	public virtual Action[] Combo => Array.Empty<Action>();
	public virtual Action[] Inspire => Array.Empty<Action>();
	public virtual Refresh[] Update => Array.Empty<Refresh>();
	public virtual TargetRequirement TargetReq => TargetRequirement.None;
	public virtual string[] ChooseCards => Array.Empty<string>();
	public virtual string[] Entourage => Array.Empty<string>();
	public virtual Refresh[] Enrage => Array.Empty<Refresh>();

	public virtual LazyNum? CostMod => null;
	public virtual Dictionary<GameTag, object>? Tags => null;
	public virtual Func<BaseEntity, int, int>? MaxHealthMod => null;
	public virtual Func<BaseEntity, int, int>? AtkMod => null;
	public virtual Func<BaseEntity, int, int>? CostModFunc => null;
	public virtual Func<BaseEntity, int, int>? DurabilityMod => null;
	public virtual Func<BaseEntity, int, int>? ArmorMod => null;

	// ── New hooks ──
	public virtual Action[] Activate => Array.Empty<Action>();
	public virtual Action[] Location => Array.Empty<Action>();
	public virtual Action[] Outcast => Array.Empty<Action>();
	public virtual Action[] Awaken => Array.Empty<Action>();
	public virtual Action[] Reward => Array.Empty<Action>();
	public virtual Action[] AddProgress => Array.Empty<Action>();
	public virtual Action[] Discard => Array.Empty<Action>();
	public virtual Action[][] SecretDeathrattles => Array.Empty<Action[]>();
	public virtual Action[] Overkill => Array.Empty<Action>();
	public virtual Action[] Draw => Array.Empty<Action>();
	public virtual Refresh[] HandUpdate => Array.Empty<Refresh>();
	public virtual Refresh[] DeckUpdate => Array.Empty<Refresh>();
	public virtual int ProgressTotal => 0;
	public virtual int DormantTurns => 0;
}
