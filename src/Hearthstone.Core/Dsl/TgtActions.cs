using HearthstoneServer.Entities;
using HearthstoneServer.Events;
using System.Linq;

namespace HearthstoneServer.Dsl;

/// <summary>
/// TGT 仍需要保留的少量通用扩展 action。
/// 这里只保留当前 DSL 还不方便直接声明式表达、但又具备复用价值的能力。
/// </summary>
public class ReplaceHeroPower : TargetedAction
{
	public ReplaceHeroPower(object target, object heroPower) : base(target, heroPower) { }

	public override object? Do(BaseEntity source, BaseEntity target, params object[] args)
	{
		var player = target as Player ?? (target as BuffableEntity)?.Controller;
		if (player?.Hero == null) return null;

		string? heroPowerId = args.FirstOrDefault() switch
		{
			string id => id,
			HeroPower hp => hp.CardId,
			BuffableEntity be => be.CardId,
			List<BaseEntity> list => list.OfType<BuffableEntity>().FirstOrDefault()?.CardId,
			_ => null,
		};
		if (string.IsNullOrEmpty(heroPowerId)) return null;

		if (player.Hero.Power != null)
			player.Hero.Power.Zone = Zone.Invalid;

		var entity = player.CreateCard(heroPowerId, Zone.Play) as HeroPower;
		if (entity == null) return null;

		entity.Controller = player;
		player.Hero.Power = entity;
		return entity;
	}
}

/// <summary>
/// 裁决者图哈特：将当前英雄技能替换为其强化版本。
/// 优先读取当前英雄技能数据上的 UpgradedHeroPower tag。
/// </summary>
public class UpgradeStartingHeroPower : TargetedAction
{
	private static readonly Dictionary<CardClass, string> UpgradedHeroPowers = new()
	{
		[CardClass.Druid] = "AT_132_DRUIDa",
		[CardClass.Hunter] = "AT_132_HUNTER_H1",
		[CardClass.Mage] = "CS2_034_H1_AT_132",
		[CardClass.Paladin] = "CS2_101_H1_AT_132",
		[CardClass.Priest] = "CS1h_001_H1_AT_132",
		[CardClass.Rogue] = "AT_132_ROGUE_H1",
		[CardClass.Shaman] = "CS2_049_H1_AT_132",
		[CardClass.Warlock] = "AT_132_WARLOCKa",
		[CardClass.Warrior] = "CS2_102_H1_AT_132",
	};

	public UpgradeStartingHeroPower(object target) : base(target) { }

	public override object? Do(BaseEntity source, BaseEntity target, params object[] args)
	{
		var player = target as Player ?? (target as BuffableEntity)?.Controller;
		var currentPower = player?.Hero?.Power;
		if (player == null || currentPower?.CardData == null) return null;

		string? heroPowerId = null;
		int upgradedDbfId = currentPower.CardData.GetTag(GameTag.UpgradedHeroPower);
		if (upgradedDbfId > 0
			&& player.Game?.CardDbByDbfId != null
			&& player.Game.CardDbByDbfId.TryGetValue(upgradedDbfId, out var upgradedData))
		{
			heroPowerId = upgradedData.CardId;
		}

		if (string.IsNullOrEmpty(heroPowerId))
		{
			var heroClass = player.Hero?.CardData?.CardClass;
			if (heroClass == null || !UpgradedHeroPowers.TryGetValue(heroClass.Value, out heroPowerId))
				return null;
		}

		return new ReplaceHeroPower(player, heroPowerId).Do(source, player, heroPowerId);
	}
}

/// <summary>
/// 杂耍吞法者：复制对手当前英雄技能。
/// </summary>
public class CopyOpponentHeroPower : TargetedAction
{
	public CopyOpponentHeroPower(object target) : base(target) { }

	public override object? Do(BaseEntity source, BaseEntity target, params object[] args)
	{
		var player = target as Player ?? (target as BuffableEntity)?.Controller;
		var opponentPowerId = player?.Opponent?.Hero?.Power?.CardId;
		if (player == null || string.IsNullOrEmpty(opponentPowerId)) return null;

		return new ReplaceHeroPower(player, opponentPowerId).Do(source, player, opponentPowerId);
	}
}

/// <summary>
/// 神秘挑战者：将牌库中每种不同的奥秘直接置入奥秘区。
/// </summary>
public class PlayDistinctSecretsFromDeck : TargetedAction
{
	public PlayDistinctSecretsFromDeck(object target) : base(target) { }

	public override object? Do(BaseEntity source, BaseEntity target, params object[] args)
	{
		var player = target as Player ?? (target as BuffableEntity)?.Controller;
		var game = player?.Game;
		if (player == null || game == null) return null;

		var secrets = player.Deck
			.OfType<BuffableEntity>()
			.Where(card => card.CardData?.HasTag(GameTag.Secret) == true)
			.GroupBy(card => card.CardId)
			.Select(group => group.First())
			.Take(Math.Max(0, Game.MAX_SECRETS_ON_PLAY - player.Secrets.Count))
			.ToList();

		foreach (var secret in secrets)
		{
			secret.SetZone(Zone.Secret);
			var script = CardScriptRegistry.Instance.GetScript(secret.CardId ?? "");
			script?.OnActivate?.Invoke(secret, game);
			game.EventBus.Publish(new SecretPlayedEvent(secret, secret), MiscPhase.On);
		}

		return secrets.Count;
	}
}
