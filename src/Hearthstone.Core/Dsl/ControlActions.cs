using Serilog;
using HearthstoneServer.Entities;
using HearthstoneServer.Events;

namespace HearthstoneServer.Dsl;

// ─── P1: Control & utility actions ──────────

/// <summary>
/// Silence a minion — remove card text, enchantments, abilities.
/// </summary>
public class Silence : TargetedAction
{
	public Silence(object target) : base(target) { }

	public override object? Do(BaseEntity source, BaseEntity target, params object[] args)
	{
		if (target.Type != CardType.Minion) return null;
		Log.Information("Silencing {Target}", target);

		if (target is BuffableEntity be)
		{
			be.ClearBuffs();
			foreach (var attr in new[]
			{
				"taunt", "divine_shield", "stealth", "windfury",
				"charge", "rush", "lifesteal", "poisonous",
				"freeze", "cant_be_targeted_by_spells",
				"cant_be_targeted_by_hero_powers"
			})
				be.SetBoolProperty(attr, false);

			be.SetBoolProperty("silenced", true);
		}
		return null;
	}

	public override string ToString() => $"Silence({Args[0]})";
}

/// <summary>
/// Bounce a minion back to hand.
/// </summary>
public class ReturnToHand : TargetedAction
{
	public ReturnToHand(object target) : base(target) { }

	public override object? Do(BaseEntity source, BaseEntity target, params object[] args)
	{
		Log.Information("{Target} is bounced back to hand", target);
		if (target is BuffableEntity be)
			be.SetZone(Zone.Hand);
		return null;
	}

	public override string ToString() => $"Bounce({Args[0]})";
}

/// <summary>
/// Freeze a character target.
/// </summary>
public class Freeze : TargetedAction
{
	public Freeze(object target) : base(target) { }

	public override object? Do(BaseEntity source, BaseEntity target, params object[] args)
	{
		Log.Information("Freezing {Target}", target);
		if (target is BuffableEntity be)
			be.SetBoolProperty("frozen", true);
		return null;
	}

	public override string ToString() => $"Freeze({Args[0]})";
}

/// <summary>
/// Discard card targets from hand.
/// 弃牌 ≠ 摧毁(Mill) ≠ 死亡(Death)。
/// 弃牌会：标记 GameTag.Discarded、发布 DiscardEvent、加入 Player.DiscardedCards。
/// </summary>
public class Discard : TargetedAction
{
	public Discard(object target) : base(target) { }

	public override object? Do(BaseEntity source, BaseEntity target, params object[] args)
	{
		Log.Information("Discarding {Target}", target);
		if (target is BuffableEntity be)
		{
			be.SetBoolProperty("discarded", true);
			be.SetZone(Zone.Graveyard);

			// 执行被弃卡牌自身的 Discard 钩子（如银器侍从、拉凯希的母亲等）
			var script = CardScriptRegistry.Instance.GetScript(be.CardId ?? "");
			if (script != null && script.Discard.Length > 0)
			{
				foreach (var action in script.Discard)
				{
					if (action is TargetedAction ta) ta.Trigger(be);
					else if (action is GameAction ga) ga.Trigger(be);
				}
			}

			// 追踪弃牌历史 + 发布事件
			var player = be.Controller;
			if (player != null)
			{
				player.DiscardedCards.Add(be);
				var game = player.Game;
				game?.EventBus.Publish(new DiscardEvent(source, player, be), MiscPhase.On);
			}
		}
		return null;
	}
}

/// <summary>
/// Give player a card (put into hand).
/// </summary>
public class AddToHand : TargetedAction
{
	public AddToHand(object target, object card) : base(target, card) { }

	public override object? Do(BaseEntity source, BaseEntity target, params object[] args)
	{
		if (target is not Player player) return null;
		var cardArg = args.Length > 0 ? args[0] : null;

		// 统一按 cardId 创建新卡（不移动原卡）
		string? cardId = cardArg switch
		{
			string id => id,
			List<BaseEntity> list when list.Count > 0 => (list[0] as BuffableEntity)?.CardId,
			BuffableEntity be => be.CardId,
			_ => null,
		};

		if (cardId == null) return null;
		var entity = player.CreateCard(cardId, Zone.Hand);
		Log.Information("{Player} receives {Card}", player.Name, entity);
		return entity;
	}
}

/// <summary>
/// 给玩家一张卡并设置 GameTag。
/// <code>new AddToHandWithTag(S.CONTROLLER, "RLK_082", GameTag.CostBlood, 1)</code>
/// </summary>
public class AddToHandWithTag : TargetedAction
{
	private readonly GameTag _tag;
	private readonly int _value;

	public AddToHandWithTag(object target, object card, GameTag tag, int value) : base(target, card) 
	{ 
		_tag = tag;
		_value = value;
	}

	public override object? Do(BaseEntity source, BaseEntity target, params object[] args)
	{
		if (target is not Player player) return null;
		var cardArg = args.Length > 0 ? args[0] : null;

		string? cardId = cardArg switch
		{
			string id => id,
			List<BaseEntity> list when list.Count > 0 => (list[0] as BuffableEntity)?.CardId,
			BuffableEntity be => be.CardId,
			_ => null,
		};

		if (cardId == null) return null;
		var entity = player.CreateCard(cardId, Zone.Hand);
		if (entity != null)
		{
			entity.EnsureTags();
			entity.Tags[_tag] = _value;
		}
		Log.Information("{Player} receives {Card} with {Tag}={Value}", player.Name, entity, _tag, _value);
		return entity;
	}
}

/// <summary>
/// Shuffle a card into player's deck.
/// </summary>
public class ShuffleIntoDeck : TargetedAction
{
	public ShuffleIntoDeck(object target, object card) : base(target, card) { }

	public override object? Do(BaseEntity source, BaseEntity target, params object[] args)
	{
		if (target is not Player player) return null;
		var cardArg = args.Length > 0 ? args[0] : null;

		BuffableEntity? entity = null;
		if (cardArg is string cardId)
			entity = player.CreateCard(cardId, Zone.Deck);
		else if (cardArg is BaseEntity existing && existing is BuffableEntity be)
		{
			be.SetZone(Zone.Deck, player);
			entity = be;
		}

		player.ShuffleDeck();
		if (entity != null)
			Log.Information("Shuffled {Card} into {Player}'s deck", entity, player.Name);
		return entity;
	}
}

/// <summary>
/// Gain armor for hero targets.
/// </summary>
public class GainArmor : TargetedAction
{
	public GainArmor(object target, object amount) : base(target, amount) { }

	public override object? Do(BaseEntity source, BaseEntity target, params object[] args)
	{
		int amount = ActionHelper.ResolveInt(args, 0, source);
		if (target is BuffableEntity be)
		{
			int armor = be.GetIntProperty("armor");
			be.SetIntProperty("armor", armor + amount);
			Log.Information("{Target} gains {Amount} armor", target, amount);

			// Publish GainArmorEvent
			var player = be.Controller ?? (source as BuffableEntity)?.Controller;
			if (player != null && amount > 0)
				player.Game?.EventBus.Publish(
					new GainArmorEvent(source, player, amount), MiscPhase.On);
		}
		return null;
	}
}

/// <summary>
/// Give player mana crystals.
/// </summary>
public class GainMana : TargetedAction
{
	public GainMana(object target, object amount) : base(target, amount) { }

	public override object? Do(BaseEntity source, BaseEntity target, params object[] args)
	{
		int amount = ActionHelper.ResolveInt(args, 0, source);
		if (target is BuffableEntity be)
		{
			int maxMana = be.GetIntProperty("max_mana");
			be.SetIntProperty("max_mana", Math.Min(maxMana + amount, 10));
			Log.Information("{Target} gains {Amount} mana crystal(s)", target, amount);
		}
		return null;
	}
}

/// <summary>
/// Give temporary mana this turn.
/// </summary>
public class GainTemporaryMana : TargetedAction
{
	public GainTemporaryMana(object target, object amount) : base(target, amount) { }

	public override object? Do(BaseEntity source, BaseEntity target, params object[] args)
	{
		int amount = ActionHelper.ResolveInt(args, 0, source);
		if (target is Player player)
		{
			player.TempMana += amount;
			Log.Information("{Target} gains {Amount} mana this turn (TempMana={TempMana})",
				target, amount, player.TempMana);
		}
		else if (target is BuffableEntity be)
		{
			int tempMana = be.GetIntProperty("temp_mana");
			be.SetIntProperty("temp_mana", tempMana + amount);
			Log.Information("{Target} gains {Amount} mana this turn", target, amount);
		}
		return null;
	}
}

/// <summary>
/// Morph (transform) target into another card.
/// Replaces the target entity on the board with a new entity of the specified card.
/// </summary>
public class Transform : TargetedAction
{
	public Transform(object target, object card) : base(target, card) { }

	public override object? Do(BaseEntity source, BaseEntity target, params object[] args)
	{
		var cardArg = args.Length > 0 ? args[0] : null;
		if (target is not BuffableEntity targetBe) return null;
		var controller = targetBe.Controller;
		if (controller == null) return null;

		// Create the replacement entity
		BuffableEntity? replacement = null;
		if (cardArg is List<BaseEntity> list && list.Count > 0)
			replacement = list[0] as BuffableEntity;
		else if (cardArg is string cardId)
			replacement = controller.CreateCard(cardId, Zone.Invalid);
		else if (cardArg is BaseEntity existing && existing is BuffableEntity be)
			replacement = be;

		if (replacement == null) return null;

		// Remove old entity from its zone
		var oldZone = targetBe.Zone;
		targetBe.SetZone(Zone.RemovedFromGame);

		// Place replacement in the same zone
		replacement.Controller = controller;
		replacement.SetZone(oldZone, controller);

		Log.Information("{Target} morphed into {Replacement}", target, replacement);
		return replacement;
	}
}

/// <summary>
/// Steal (take control of) target — transfer control to source's controller.
/// </summary>
public class TakeControl : TargetedAction
{
	public TakeControl(object target) : base(target) { }

	public override object? Do(BaseEntity source, BaseEntity target, params object[] args)
	{
		if (target is not Character ch) return null;
		var newOwner = source is Player p ? p : (source as BuffableEntity)?.Controller;
		if (newOwner == null) return null;
		var oldOwner = ch.Controller;
		if (oldOwner == null || oldOwner == newOwner) return null;

		// Check new owner board space
		if (newOwner.Field.Count >= 7) return null;

		// Remove from old owner's field
		oldOwner.Field.Remove(target);

		// Transfer ownership
		ch.Controller = newOwner;
		newOwner.Field.Add(target);

		Log.Information("{NewOwner} takes control of {Target} from {OldOwner}",
			newOwner.Name, target, oldOwner.Name);
		return target;
	}
}

/// <summary>
/// Set the current health of target to amount.
/// </summary>
public class SetCurrentHealth : TargetedAction
{
	public SetCurrentHealth(object target, object amount) : base(target, amount) { }

	public override object? Do(BaseEntity source, BaseEntity target, params object[] args)
	{
		int amount = ActionHelper.ResolveInt(args, 0, source);
		if (target is BuffableEntity be)
		{
			be.SetIntProperty("health", amount);
			Log.Information("Setting {Target}'s health to {Amount}", target, amount);
		}
		return target;
	}
}

/// <summary>
/// Counter a card (prevent its effects).
/// </summary>
public class Counter : TargetedAction
{
	public Counter(object target) : base(target) { }

	public override object? Do(BaseEntity source, BaseEntity target, params object[] args)
	{
		Log.Information("[Counter] Attempting to counter: source={Source} target={Target} targetType={Type}",
			source?.ToString(), target?.ToString(), target?.GetType().Name);
		if (target is BuffableEntity be)
		{
			be.SetBoolProperty("countered", true);
			Log.Information("[Counter] Set 'countered' on {Entity} (CardId={CardId})",
				be.EntityId, be.CardId);
		}
		return null;
	}
}

/// <summary>
/// Swap two entities.
/// </summary>
public class SwapEntities : TargetedAction
{
	private readonly bool _keepBuffs;

	public SwapEntities(object target, object other, bool keepBuffs = false) : base(target, other)
	{
		_keepBuffs = keepBuffs;
	}

	public override object? Do(BaseEntity source, BaseEntity target, params object[] args)
	{
		if (args.Length == 0) return null;
		var other = args[0] as BaseEntity;
		if (other == null) return null;

		Log.Information("Swapping {Target} with {Other}", target, other);

		if (target is BuffableEntity beTarget && other is BuffableEntity beOther)
		{
			var targetOldZone = beTarget.Zone;
			var otherOldZone = beOther.Zone;
			var targetController = beTarget.Controller;
			var otherController = beOther.Controller;

			// Transit through SETASIDE to avoid zone conflicts
			beTarget.SetZone(Zone.SetAside);
			beOther.SetZone(Zone.SetAside);

			// Swap controllers
			beTarget.Controller = otherController;
			beOther.Controller = targetController;

			// Swap zones
			beTarget.SetZone(otherOldZone);
			beOther.SetZone(targetOldZone);

			// 离开战场的实体默认清除 buff（除非 keepBuffs=true）
			if (!_keepBuffs)
			{
				if (targetOldZone == Zone.Play && beTarget.Zone != Zone.Play)
					beTarget.ClearBuffs();
				if (otherOldZone == Zone.Play && beOther.Zone != Zone.Play)
					beOther.ClearBuffs();
			}
		}
		return null;
	}
}

/// <summary>
/// Reveal a secret.
/// </summary>
public class RevealSecret : TargetedAction
{
	public RevealSecret(object target) : base(target) { }

	public override object? Do(BaseEntity source, BaseEntity target, params object[] args)
	{
		Log.Information("Revealing {Target}", target);
		if (target is BuffableEntity be)
		{
			// Publish RevealSecretEvent before moving to graveyard
			be.Controller?.Game?.EventBus.Publish(
				new RevealSecretEvent(source, be), MiscPhase.On);
			be.SetZone(Zone.Graveyard);
		}
		return null;
	}
}

/// <summary>
/// Remove entity from game entirely.
/// </summary>
public class RemoveFromGame : TargetedAction
{
	public RemoveFromGame(object target) : base(target) { }

	public override object? Do(BaseEntity source, BaseEntity target, params object[] args)
	{
		if (target is BuffableEntity be)
			be.SetZone(Zone.RemovedFromGame);
		return null;
	}
}

// ─── Game-level actions ──────────

/// <summary>Process deaths in the PLAY zone.</summary>
public class ProcessDeaths : GameAction
{
	public override void Do(BaseEntity source, params object[] args)
		=> Log.Information("Processing deaths");
}

/// <summary>Death — move entity to GRAVEYARD, fire deathrattles.</summary>
public class ProcessDeath : GameAction
{
	public ProcessDeath(object entity) : base(entity) { }
	public override void Do(BaseEntity source, params object[] args)
		=> Log.Information("Death processing");
}

/// <summary>
/// Attack — 执行攻击逻辑：设置战斗标记，互击伤害，武器耐久。
/// </summary>
public class Attack : GameAction
{
	private static readonly ILogger _log = LogConfig.ForType<Attack>();

	public Attack(params object[] args) : base(args) { }

	public override void Do(BaseEntity source, params object[] args)
	{
		// args[0] = attacker, args[1] = defender
		if (args.Length < 2) return;
		if (args[0] is not Character attacker || args[1] is not Character defender) return;
		// 通过 Controller.Game fallback 获取 Game 引用（测试中实体可能未直接设 Game 属性）
		var game = source.Game
			?? (source as BuffableEntity)?.Controller?.Game
			?? (attacker as BuffableEntity)?.Controller?.Game;
		if (game == null) return;

		// 设置战斗标记
		attacker.AttackTarget = defender;
		defender.Defending = true;
		game.ProposedAttacker = attacker;
		game.ProposedDefender = defender;

		// 读回 ProposedDefender（可能被事件重定向）
		defender = game.ProposedDefender ?? defender;
		game.ProposedAttacker = null;
		game.ProposedDefender = null;

		// 检查攻击是否应中断（攻击者死亡/离开战场）
		if (attacker.ShouldExitCombat)
		{
			_log.Information("Attack interrupted: attacker exiting combat.");
			attacker.AttackTarget = null;
			defender.Defending = false;
			return;
		}

		// 保存攻击力（战斗中可能改变）
		int defAtk = defender.Atk;
		int atkAtk = attacker.Atk;

		// 攻击时移除潜行
		if (attacker.Stealth)
			attacker.Stealth = false;

		// 互击伤害
		defender.Hit(atkAtk);
		if (defAtk > 0)
			attacker.Hit(defAtk);

		// 武器耐久损耗
		if (attacker is Hero attackHero && attackHero.EquippedWeapon != null)
		{
			var weapon = attackHero.EquippedWeapon;
			weapon.LoseDurability();
			if (weapon.Dead)
			{
				_log.Information("Weapon {Weapon} destroyed (0 durability)", weapon.CardId);
				weapon.SetZone(Zone.Graveyard);
				attackHero.EquippedWeapon = null;
			}
		}

		// 清理战斗状态
		attacker.AttackTarget = null;
		defender.Defending = false;
		attacker.NumAttacks++;
	}
}

/// <summary>
/// 将当前待结算的攻击随机改向到另一个合法角色。
/// 用于实现“误导”这类在攻击前重写 ProposedDefender 的效果。
/// </summary>
public class RedirectAttackToRandomOtherCharacter : GameAction
{
	public override void Do(BaseEntity source, params object[] args)
	{
		var game = source.Game;
		if (game?.ProposedAttacker is not Character attacker)
			return;

		var originalDefender = game.ProposedDefender;
		if (originalDefender == null)
			return;

		var candidates = new List<Character>();
		foreach (var player in new[] { attacker.Controller, attacker.Controller?.Opponent })
		{
			if (player == null) continue;
			foreach (var entity in player.Characters)
			{
				if (entity is not Character character) continue;
				if (character == attacker || character == originalDefender) continue;
				if (character.Immune || character.Stealth) continue;
				candidates.Add(character);
			}
		}

		if (candidates.Count == 0)
			return;

		game.ProposedDefender = candidates[game.Random.Next(candidates.Count)];
	}
}



/// <summary>Game start setup.</summary>
public class GameStart : GameAction
{
	public override void Do(BaseEntity source, params object[] args)
		=> Log.Information("Game start");
}

