using System.Text.Json.Serialization;

namespace HearthstoneServer.PowerHistory;

/// <summary>
/// Base type for all PowerHistory entries. These form the communication protocol
/// between the game engine and the frontend, matching Hearthstone's PowerHistory format.
/// </summary>
[JsonDerivedType(typeof(PowerBlockStart), "BlockStart")]
[JsonDerivedType(typeof(PowerBlockEnd), "BlockEnd")]
[JsonDerivedType(typeof(PowerTagChange), "TagChange")]
[JsonDerivedType(typeof(PowerCreateEntity), "CreateEntity")]
[JsonDerivedType(typeof(PowerShowEntity), "ShowEntity")]
[JsonDerivedType(typeof(PowerFullState), "FullState")]
public abstract record PowerHistoryEntry(string Type);

/// <summary>ActionBlock start — source performs an action targeting something.</summary>
public record PowerBlockStart(
	BlockType BlockType,
	int SourceId,
	int Index,
	int? TargetId,
	string? SourceCardId = null
) : PowerHistoryEntry("BlockStart");

/// <summary>ActionBlock end — signals the end of a nested action block.</summary>
public record PowerBlockEnd(
	BlockType BlockType,
	int SourceId
) : PowerHistoryEntry("BlockEnd");

/// <summary>A tag on an entity changed value.</summary>
public record PowerTagChange(
	int EntityId,
	GameTag Tag,
	int OldValue,
	int NewValue
) : PowerHistoryEntry("TagChange");

/// <summary>A new entity was created (revealed to this player).</summary>
public record PowerCreateEntity(
	int EntityId,
	string? CardId,
	Dictionary<string, object>? Tags
) : PowerHistoryEntry("CreateEntity");

/// <summary>A previously hidden entity is now revealed.</summary>
public record PowerShowEntity(
	int EntityId,
	string? CardId
) : PowerHistoryEntry("ShowEntity");

/// <summary>Full game state snapshot (for initial load or reconnect).</summary>
public record PowerFullState(
	object State
) : PowerHistoryEntry("FullState");
