using Serilog;
using HearthstoneServer.Cards;
using HearthstoneServer.Entities;

namespace HearthstoneServer.Dsl;

/// <summary>
/// Replays a spell's effect layer on a specified target.
/// Does not publish spell-cast events or spend resources.
/// </summary>
public class CopySpellEffectOn : TargetedAction
{
	private static readonly ILogger _log = LogConfig.ForType<CopySpellEffectOn>();

	public CopySpellEffectOn(object spell, object target) : base(spell, target) { }

	public override object? Do(BaseEntity source, BaseEntity target, params object[] args)
	{
		if (target is not Spell spell || spell.Game == null)
			return null;

		var copiedTarget = args.Length > 0
			? (args[0] as List<BaseEntity>)?.OfType<Character>().FirstOrDefault()
			: null;

		_log.Information("{Source} copies spell effect {Spell} onto {Target}",
			source, spell.CardId, copiedTarget);

		spell.Game.Sequences.ResolveSpellEffect(spell, copiedTarget,
			includeOverload: true,
			includeTwinspell: true,
			useCombo: false,
			isOutcast: false);

		return null;
	}
}

/// <summary>
/// Replaces all cards in the target player's hand and deck with random legendary minions.
/// </summary>
public class ReplaceAllWithLegendaries : TargetedAction
{
	private static readonly ILogger _log = LogConfig.ForType<ReplaceAllWithLegendaries>();

	public ReplaceAllWithLegendaries(object target) : base(target) { }

	public override object? Do(BaseEntity source, BaseEntity target, params object[] args)
	{
		if (target is not Player player || player.Game?.CardDb == null)
			return null;

		var pool = RandomCards.RandomLegendaryMinion().FindCards(source);
		if (pool.Count == 0)
			return null;

		ReplaceZoneCards(source, player, player.Hand.OfType<BuffableEntity>().ToList(), Zone.Hand, pool);
		ReplaceZoneCards(source, player, player.Deck.OfType<BuffableEntity>().ToList(), Zone.Deck, pool);

		_log.Information("{Player} hand and deck replaced with legendary minions", player.Name);
		return null;
	}

	private static void ReplaceZoneCards(BaseEntity source, Player player, List<BuffableEntity> cards, Zone zone, List<string> pool)
	{
		var random = source.Game?.Random ?? new Random();
		foreach (var card in cards)
		{
			card.SetZone(Zone.SetAside);
			var replacementId = pool[random.Next(pool.Count)];
			player.CreateCard(replacementId, zone);
		}
	}
}

/// <summary>
/// Generates three distinct basic hero powers, excluding the controller's current one.
/// </summary>
public class BasicHeroPowerPicker : LazyValue
{
	public override object Evaluate(BaseEntity source)
	{
		var controller = (source as BuffableEntity)?.Controller;
		if (controller == null) return new List<BaseEntity>();

		var currentId = controller.Hero?.Power?.CardId;
		var excluded = new HashSet<string>(StringComparer.Ordinal)
		{
			currentId ?? string.Empty,
		};

		if (!string.IsNullOrEmpty(currentId) && currentId.EndsWith('2'))
			excluded.Add(currentId[..^1]);

		var pool = CardScriptUtils.BASIC_HERO_POWERS
			.Where(id => !excluded.Contains(id))
			.ToList();

		var chosen = RandomCardPicker.WeightedCardChoice(
			source,
			new List<int> { 1 },
			new List<List<string>> { pool },
			Math.Min(3, pool.Count));

		return Choice.CreateCardsFromIds(controller, chosen, Zone.SetAside);
	}
}
