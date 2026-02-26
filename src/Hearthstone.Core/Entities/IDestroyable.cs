namespace HearthstoneServer.Entities;

/// <summary>
/// Implemented by entities that can die and be processed by Game.ProcessDeaths.
/// </summary>
public interface IDestroyable
{
	/// <summary>True if the entity should be processed as dead.</summary>
	bool Dead { get; }

	/// <summary>
	/// Execute death processing: move to graveyard, publish event,
	/// trigger deathrattle, unsubscribe from EventBus.
	/// </summary>
	void OnDeath(Game game);
}
