using System.Collections.Concurrent;
using System.Reflection;
using HearthstoneServer.Dsl.Cards;
using Serilog;

namespace HearthstoneServer.Dsl;

// ═══════════════════════════════════════════════════
// Card Script Registry — assembly scanning & lookup
// ═══════════════════════════════════════════════════

/// <summary>
/// Scans assemblies for classes decorated with [CardScript("id")]
/// and maps card IDs to ICardScript instances.
/// </summary>
public sealed class CardScriptRegistry
{
	private static CardScriptRegistry? _instance;
	private readonly ConcurrentDictionary<string, ICardScript> _scripts = new(StringComparer.Ordinal);
	private readonly object _initLock = new();
	private volatile bool _initialized;

	/// <summary>Singleton instance.</summary>
	public static CardScriptRegistry Instance => _instance ??= new CardScriptRegistry();

	/// <summary>Number of registered scripts.</summary>
	public int Count => _scripts.Count;

	/// <summary>All registered card IDs.</summary>
	public IEnumerable<string> CardIds => _scripts.Keys;

	/// <summary>
	/// Initialize the registry by scanning all loaded assemblies.
	/// Safe to call multiple times — only scans once.
	/// </summary>
	public void Initialize(params Assembly[] additionalAssemblies)
	{
		if (_initialized) return;
		lock (_initLock)
		{
			if (_initialized) return;

			// Scan the core assembly + any additional ones
			var assemblies = new List<Assembly> { typeof(CardScriptRegistry).Assembly };
			assemblies.AddRange(additionalAssemblies);

			foreach (var assembly in assemblies.Distinct())
				ScanAssembly(assembly);

			// Register declarative CardPack scripts (new API)
			var builder = new CardBuilder(this);
			CardPack.RegisterAll(builder);

			_initialized = true;
			Log.Information("CardScriptRegistry initialized with {Count} scripts", _scripts.Count);
		}
	}

	/// <summary>
	/// Look up a card script by card ID.
	/// Returns null if no script is registered for this card.
	/// </summary>
	public ICardScript? GetScript(string cardId)
	{
		return _scripts.TryGetValue(cardId, out var script) ? script : null;
	}

	/// <summary>
	/// Check if a script is registered for the given card ID.
	/// </summary>
	public bool HasScript(string cardId) => _scripts.ContainsKey(cardId);

	/// <summary>
	/// Manually register a script for a card ID.
	/// Useful for tests or custom cards.
	/// </summary>
	public void Register(string cardId, ICardScript script)
	{
		_scripts[cardId] = script;
	}

	/// <summary>
	/// Clear all registered scripts. Primarily for testing.
	/// </summary>
	public void Clear()
	{
		_scripts.Clear();
		_initialized = false;
	}

	private void ScanAssembly(Assembly assembly)
	{
		foreach (var type in assembly.GetTypes())
		{
			if (!typeof(ICardScript).IsAssignableFrom(type) || type.IsAbstract || type.IsInterface)
				continue;

			var attrs = type.GetCustomAttributes<CardScriptAttribute>();
			foreach (var attr in attrs)
			{
				try
				{
					var instance = (ICardScript)Activator.CreateInstance(type)!;
					_scripts[attr.CardId] = instance;
				}
				catch (Exception ex)
				{
					Log.Warning(ex, "Failed to create card script for {CardId} from {Type}",
						attr.CardId, type.FullName);
				}
			}
		}
	}
}
