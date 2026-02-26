using Serilog;
using HearthstoneServer.Entities;

namespace HearthstoneServer.Dsl;

// ═══════════════════════════════════════════════════════════
// Phase 11 — Switch DSL
// ═══════════════════════════════════════════════════════════

/// <summary>
/// Switch statement on the ID of an entity selected by a selector.
/// Evaluates to actions from the map based on the entity's card ID.
/// Use empty string "" as the default/fallback key.
/// </summary>
public class Switch
{
	private static readonly ILogger _log = LogConfig.ForType<Switch>();

	private readonly Selector _selector;
	private readonly Dictionary<string, List<Action>> _map;

	/// <summary>
	/// Create a Switch with a selector and a map of card ID → actions.
	/// Use empty string "" for the default fallback.
	/// </summary>
	public Switch(Selector selector, Dictionary<string, List<Action>> map)
	{
		_selector = selector;
		_map = map;
	}

	/// <summary>
	/// The default actions when no matching ID is found in the map.
	/// Keyed by empty string "" in the map dictionary.
	/// </summary>
	public List<Action> Default =>
		_map.TryGetValue("", out var actions) ? actions : new List<Action>();

	/// <summary>
	/// Evaluate the switch: find the entity via selector, look up its ID in the map.
	/// </summary>
	public List<Action> Evaluate(BaseEntity source)
	{
		var entities = _selector.Eval(
			source.GetGame()?.Entities ?? new List<BaseEntity>(), source);

		if (entities.Count == 0)
			return Default;

		if (entities.Count > 1)
		{
			_log.Warning("Switch() on more than 1 entity: {Entities}", entities);
		}

		var entity = entities[0];
		var cardId = (entity as BuffableEntity)?.CardId;

		if (cardId == null || !_map.ContainsKey(cardId))
			return Default;

		return _map[cardId];
	}

	/// <summary>
	/// Trigger the switch: evaluate and execute the resulting actions.
	/// </summary>
	public void Trigger(BaseEntity source)
	{
		var actions = Evaluate(source);
		if (actions.Count > 0 && source.Game != null)
		{
			source.Game.QueueActions(source, actions);
		}
	}
}
