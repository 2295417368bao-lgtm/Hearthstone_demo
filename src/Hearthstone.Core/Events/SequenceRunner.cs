using HearthstoneServer.Dsl;
using HearthstoneServer.Entities;
using Serilog;

namespace HearthstoneServer.Events;

// ═══════════════════════════════════════════════════
// SequenceRunner — Phase 管线编排器
// 控制流程顺序 + 在正确位置发射 Block 日志
// ═══════════════════════════════════════════════════

/// <summary>
/// Orchestrates game sequences (PlaySpell, PlayMinion, Combat, etc.)
/// using the phase pipeline model from the Advanced Rulebook.
/// 
/// Each sequence:
///   1. Wraps the entire operation in an outer Block (PLAY/ATTACK)
///   2. Executes phases in order, publishing events via EventBus
///   3. Calls RefreshAuras + ProcessDeaths between phases
///   4. Uses ActionBlock(Power) for effect execution within phases
/// </summary>
public class SequenceRunner
{
	private static readonly ILogger _log = Log.ForContext<SequenceRunner>();

	private readonly Game _game;
	private readonly EventBus _bus;

	public SequenceRunner(Game game, EventBus bus)
	{
		_game = game;
		_bus = bus;
	}

	/// <summary>
	/// 支付卡牌费用：CostBlood=1 时扣生命值，否则扣法力。
	/// </summary>
	private void PayCardCost(Player player, BuffableEntity card)
	{
		int cb = card.Tags.Contains(GameTag.CostBlood) ? card.Tags[GameTag.CostBlood] : 0;
		bool payWithHealth = cb == 1;
		
		if (payWithHealth && player.Hero != null)
		{
			player.Hero.Hit(card.Cost);
			_log.Information("{Player} pays {Cost} health for {Card}",
				player.Name, card.Cost, card.CardId);
		}
		else
		{
			player.PayCost(card.Cost);
		}
	}

	/// <summary>
	/// 战斗中的伤害结算入口。
	/// 与法术伤害一致，先发布 PreDamageEvent，再按改写后的伤害值结算。
	/// </summary>
	private int ApplyCombatDamage(Character source, Character target, int amount)
	{
		if (amount <= 0) return 0;

		var preEvt = new PreDamageEvent(source, target, amount);
		_bus.Publish(preEvt, MiscPhase.On);
		if (preEvt.Redirected || preEvt.Amount <= 0) return 0;

		int dealt = target.Hit(preEvt.Amount);
		if (dealt > 0)
		{
			target.LastDamageSource = source;
			_bus.Publish(new DamageEvent(source, target, dealt), MiscPhase.On);
		}
		return dealt;
	}

	/// <summary>
	/// 处理英雄持武器攻击后的默认耐久损失，允许脚本替换为其他代价。
	/// </summary>
	private void ResolveWeaponDurabilityLoss(Hero hero, Character defender, int amount = 1)
	{
		var weapon = hero.EquippedWeapon;
		if (weapon == null) return;

		var evt = new WeaponDurabilityLossEvent(weapon, hero, weapon, defender, amount);
		_bus.Publish(evt, WeaponDurabilityLossPhase.BeforeLoseDurability);

		if (!evt.Replaced)
			weapon.LoseDurability(amount);

		_bus.Publish(evt, WeaponDurabilityLossPhase.AfterLoseDurability);

		if (weapon.Dead)
		{
			_log.Information("Weapon {Weapon} destroyed (0 durability)", weapon.CardId);
			weapon.SetZone(Zone.Graveyard);
			hero.EquippedWeapon = null;
		}
	}

	// ═══════════════════════════════════════════════
	// PlaySpell — 打出法术
	// ═══════════════════════════════════════════════

	/// <summary>
	/// Execute the full spell-play sequence with correct phase ordering.
	/// Block structure: BEGIN(Play) → OnPlay → SpellText → AfterSpell → END(Play)
	/// </summary>
	public void PlaySpell(BuffableEntity spell, Character? target, ICardScript? script = null, bool useCombo = false, bool isOutcast = false)
	{
		var player = spell.Controller;
		if (player == null) return;

		script ??= _game.ScriptRegistry.GetScript(spell.CardId ?? "");

		_log.Information("[Sequence] PlaySpell: {Spell} target={Target} combo={Combo}",
			spell.CardId ?? spell.EntityId.ToString(),
			target?.CardId ?? target?.EntityId.ToString() ?? "none",
			useCombo);

		// ── Block: 外层 PLAY 块 ──
		_game.ActionStart(BlockType.Play, spell, -1, target);

		// Step 1: 准备 — 支付费用，处理过载
		PayCardCost(player, spell);
		if (spell.Overload > 0)
		{
			player.Overloaded += spell.Overload;
			_bus.Publish(new OverloadEvent(spell, player, spell.Overload), MiscPhase.On);
		}
		spell.TurnPlayed = _game.Turn;

		// Twinspell: add copy before effects
		if (spell is Spell s && s.Twinspell && s.TwinspellCopyId != null)
			player.Give(s.TwinspellCopyId);

		// 注册 OnActivate（入场后、OnPlay 前）
		// 先清理手牌/牌库阶段注册的订阅，防止双重注册
		_bus.UnsubscribeAll(spell);
		script?.OnActivate?.Invoke(spell, _game);

		// Step 2: On Play Phase — "每当打出卡牌/法术" + 反制
		bool countered = false;
		_log.Information("[PlaySpell] Before OnPlay events: spell={SpellId} eid={Eid}",
			spell.CardId, spell.EntityId);
		_bus.Publish(new CardPlayedEvent(spell, spell, target), SpellPhase.OnPlay);
		_bus.Publish(new SpellCastEvent(spell, spell, target), SpellPhase.OnPlay);

		// Check if spell was countered during On Play phase
		var isCountered = spell.GetBoolProperty("countered");
		_log.Information("[PlaySpell] After OnPlay events: countered={Countered}", isCountered);
		if (isCountered)
			countered = true;

		_game.RefreshAuras();
		_game.ProcessDeaths();

		// Step 3: Spell Text Phase — 如未被反制
		if (!countered)
		{
			// 选择 Combo 或 Play 动作
			var actions = useCombo && script != null && script.Combo.Length > 0
				? script.Combo
				: script?.Play ?? Array.Empty<GameAction>();

			if (actions.Length > 0)
			{
				// ★ ActionBlock(Power) — 产生标准 POWER Block
				_game.ActionBlock(spell, actions.ToList(), BlockType.Power, target: target);
			}
			_game.RefreshAuras();
			_game.ProcessDeaths();

			// Outcast: 流放额外效果（法术从最左/右打出时）
			if (isOutcast && script != null && script.Outcast.Length > 0)
			{
				_game.ActionBlock(spell, script.Outcast.ToList(), BlockType.Power, target: target);
				_game.RefreshAuras();
				_game.ProcessDeaths();
			}
		}

		// Step 4: After Spell Phase — "在打出法术后" 触发
		_bus.Publish(new CardPlayedEvent(spell, spell, target), SpellPhase.AfterSpell);
		_bus.Publish(new SpellCastEvent(spell, spell, target), SpellPhase.AfterSpell);
		_game.RefreshAuras();
		_game.ProcessDeaths();

		// 清理 — 法术送入墓地
		spell.SetZone(Zone.Graveyard);

		// ── Block: 关闭 PLAY 块 ──
		_game.ActionEnd(BlockType.Play, spell);
		_game.CheckForEndGame();
	}

	// ═══════════════════════════════════════════════
	// PlayMinion — 打出随从
	// ═══════════════════════════════════════════════

	/// <summary>
	/// Execute the full minion-play sequence with correct phase ordering.
	/// Block structure: BEGIN(Play) → OnPlay → OnSummon → Battlecry → AfterPlay → AfterSummon → END(Play)
	/// </summary>
	public void PlayMinion(Minion minion, Character? target, int index = -1, ICardScript? script = null, bool useCombo = false, bool isOutcast = false)
	{
		var player = minion.Controller;
		if (player == null) return;

		script ??= _game.ScriptRegistry.GetScript(minion.CardId ?? "");

		_log.Information("[Sequence] PlayMinion: {Minion} target={Target} index={Index} combo={Combo}",
			minion.CardId ?? minion.EntityId.ToString(),
			target?.CardId ?? target?.EntityId.ToString() ?? "none",
			index,
			useCombo);

		// ── Block: 外层 PLAY 块 ──
		_game.ActionStart(BlockType.Play, minion, index, target);

		// Step 1: 准备 — 支付费用，过载，进入场上
		PayCardCost(player, minion);
		if (minion.Overload > 0)
		{
			player.Overloaded += minion.Overload;
			_bus.Publish(new OverloadEvent(minion, player, minion.Overload), MiscPhase.On);
		}
		minion.TurnPlayed = _game.Turn;
		minion.SetZone(Zone.Play);

		// 分配 ZonePosition
		minion.ZonePosition = player.Field.IndexOf(minion) + 1;

		// 注册 OnActivate（入场后、OnPlay 前）
		// 先清理手牌/牌库阶段注册的订阅，防止双重注册
		_bus.UnsubscribeAll(minion);
		script?.OnActivate?.Invoke(minion, _game);

		// Step 2: On Play Phase — "每当打出卡牌/随从"
		_bus.Publish(new CardPlayedEvent(minion, minion, target), MinionPhase.OnPlay);
		_bus.Publish(new MinionPlayedEvent(minion, minion, target), MinionPhase.OnPlay);
		_game.RefreshAuras();
		_game.ProcessDeaths();
		if (minion.Dead) goto End;

		// Step 3: On Summon Phase — "每当你召唤随从" (Summon Resolution Step)
		_bus.Publish(new MinionSummonedEvent(minion, minion), MinionPhase.OnSummon);
		_game.RefreshAuras();
		_game.ProcessDeaths();
		if (minion.Dead) goto End;

		// Step 4: Battlecry Phase
		{
			// 选择 Combo 或 Play（战吼）动作
			var actions = useCombo && script != null && script.Combo.Length > 0
				? script.Combo
				: script?.Battlecry ?? Array.Empty<GameAction>();

			if (actions.Length > 0)
			{
				var targetReq = script?.TargetReq;
				int targetCount = targetReq?.TargetCount ?? 1;

				if (targetCount > 1 && targetReq != null)
				{
					// ★ 多目标战吼：创建 BattlecryTargetChoice 逐一收集目标
					var battlecryChoice = new BattlecryTargetChoice(
						minion, targetReq, actions.ToList(), firstTarget: target);
					battlecryChoice.Begin();

					// 双倍战吼：如果 Player 有 ExtraBattlecries，需要在前端解决
					// 第一轮后创建第二个 BattlecryTargetChoice（由 BattlecryTargetChoice.ExecuteBattlecry 后续处理）
					// 注意：双倍战吼的重新选择由前端在解决完第一轮后再次调用 PlayCard/ResolveBattlecry 实现
				}
				else
				{
					// ★ 单目标战吼（原有路径）
					_game.ActionBlock(minion, actions.ToList(), BlockType.Power, target: target);

					// 双倍战吼（布莱恩·铜须等）
					if (player.ExtraBattlecries && minion.HasBattlecry
						|| player.MinionExtraBattlecries && minion.HasBattlecry
						|| player.MinionExtraCombos && minion.HasCombo && player.Combo)
					{
						_game.ActionBlock(minion, actions.ToList(), BlockType.Power, target: target);
					}
				}
			}
			_game.RefreshAuras();
			_game.ProcessDeaths();
			if (minion.Dead) goto End;
		}

		// Step 4.3: Outcast — 流放额外效果（从最左/右手牌打出时）
		if (isOutcast && script != null && script.Outcast.Length > 0)
		{
			_game.ActionBlock(minion, script.Outcast.ToList(), BlockType.Power, target: target);
			_game.RefreshAuras();
			_game.ProcessDeaths();
			if (minion.Dead) goto End;
		}

		// Step 4.5: Colossal — 召唤附肢（战吼后）
		SummonColossalLimbs(minion, player);

		// Step 4.6: Miniaturize / Gigantify — 战吼后触发
		{
			var cardData = minion.CardData;
			if (cardData != null && cardData.HasTag(GameTag.Miniaturize))
			{
				_game.QueueActions(minion, new List<Dsl.Action> { new AddMiniCopyToHand(minion) });
			}
			if (cardData != null && cardData.HasTag(GameTag.Gigantify))
			{
				_game.QueueActions(minion, new List<Dsl.Action> { new AddGiganticCopyToHand(minion) });
			}
		}

		// Step 5: After Play + After Summon Phase
		_bus.Publish(new MinionPlayedEvent(minion, minion, target), MinionPhase.AfterPlay);
		_game.RefreshAuras();
		_game.ProcessDeaths();

		_bus.Publish(new MinionSummonedEvent(minion, minion), MinionPhase.AfterSummon);
		_game.RefreshAuras();
		_game.ProcessDeaths();

		// Step 6: Auto-Dormant — 若脚本定义了 DormantTurns，自动进入休眠
		if (script != null && script.DormantTurns > 0 && !minion.Dead)
		{
			minion.Dormant = true;
			minion.DormantTurns = script.DormantTurns;
		}

	End:
		// ── Block: 关闭 PLAY 块 ──
		_game.ActionEnd(BlockType.Play, minion);
		_game.CheckForEndGame();
	}

	// ═══════════════════════════════════════════════
	// PlaySecret — 打出奥秘
	// ═══════════════════════════════════════════════

	/// <summary>
	/// Execute the secret-play sequence.
	/// Secret enters the Secret zone and registers its trigger.
	/// </summary>
	public void PlaySecret(BuffableEntity secret, ICardScript? script = null)
	{
		var player = secret.Controller;
		if (player == null) return;

		script ??= _game.ScriptRegistry.GetScript(secret.CardId ?? "");

		_log.Information("[Sequence] PlaySecret: {Secret}", secret.CardId ?? secret.EntityId.ToString());

		_game.ActionStart(BlockType.Play, secret, -1, null);

		// 支付费用
		PayCardCost(player, secret);
		if (secret.Overload > 0)
		{
			player.Overloaded += secret.Overload;
			_bus.Publish(new OverloadEvent(secret, player, secret.Overload), MiscPhase.On);
		}
		secret.TurnPlayed = _game.Turn;
		secret.SetZone(Zone.Secret);

		// 注册 OnActivate（奥秘入场后立即注册触发器）
		script?.OnActivate?.Invoke(secret, _game);

		// On Play Phase
		_bus.Publish(new CardPlayedEvent(secret, secret, null), SpellPhase.OnPlay);
		_bus.Publish(new SecretPlayedEvent(secret, secret), MiscPhase.On);
		_game.RefreshAuras();
		_game.ProcessDeaths();

		_game.ActionEnd(BlockType.Play, secret);
		_game.CheckForEndGame();
	}

	// ═══════════════════════════════════════════════
	// PlayWeapon — 打出武器
	// ═══════════════════════════════════════════════

	/// <summary>
	/// Execute the weapon-play sequence.
	/// Weapon equips to hero, destroying old weapon first.
	/// </summary>
	public void PlayWeapon(Weapon weapon, Character? target, ICardScript? script = null)
	{
		var player = weapon.Controller;
		if (player == null) return;

		script ??= _game.ScriptRegistry.GetScript(weapon.CardId ?? "");

		_log.Information("[Sequence] PlayWeapon: {Weapon}", weapon.CardId ?? weapon.EntityId.ToString());

		_game.ActionStart(BlockType.Play, weapon, -1, target);

		// 支付费用
		PayCardCost(player, weapon);
		if (weapon.Overload > 0)
		{
			player.Overloaded += weapon.Overload;
			_bus.Publish(new OverloadEvent(weapon, player, weapon.Overload), MiscPhase.On);
		}
		weapon.TurnPlayed = _game.Turn;

		// 装备武器（销毁旧武器）
		weapon.Equip();

		// 注册 OnActivate
		script?.OnActivate?.Invoke(weapon, _game);

		// On Play Phase
		_bus.Publish(new CardPlayedEvent(weapon, weapon, target), SpellPhase.OnPlay);
		_game.RefreshAuras();
		_game.ProcessDeaths();

		// 战吼/效果
		if (script != null && script.Play.Length > 0)
		{
			_game.ActionBlock(weapon, script.Play.ToList(), BlockType.Power, target: target);
			_game.RefreshAuras();
			_game.ProcessDeaths();
		}

		_game.ActionEnd(BlockType.Play, weapon);
		_game.CheckForEndGame();
	}

	// ═══════════════════════════════════════════════
	// PlayHeroCard — 打出英雄牌
	// ═══════════════════════════════════════════════

	/// <summary>
	/// Execute the hero-card-play sequence.
	/// Replaces the current hero, swaps hero power, adds armor, runs battlecry.
	/// </summary>
	public void PlayHeroCard(Hero hero, Character? target, ICardScript? script = null, bool useCombo = false)
	{
		var player = hero.Controller;
		if (player == null) return;

		script ??= _game.ScriptRegistry.GetScript(hero.CardId ?? "");

		_log.Information("[Sequence] PlayHeroCard: {Hero}", hero.CardId ?? hero.EntityId.ToString());

		_game.ActionStart(BlockType.Play, hero, -1, target);

		// 支付费用
		PayCardCost(player, hero);
		hero.TurnPlayed = _game.Turn;

		// 替换英雄
		var oldHero = player.Hero;
		if (oldHero != null)
		{
			// 继承旧英雄的伤害
			hero.DamageTaken = oldHero.DamageTaken;
			hero.Armor = oldHero.Armor;

			// 旧英雄进墓地
			oldHero.Zone = Zone.Graveyard;
			_game.EventBus.UnsubscribeAll(oldHero);
		}

		// 设置新英雄
		player.Hero = hero;
		_game.RegisterEntity(hero);
		hero.Zone = Zone.Play;

		// 英雄卡自带护甲 (tag 292)
		int cardArmor = hero.CardData?.GetTag(GameTag.Armor) ?? 0;
		if (cardArmor > 0)
			hero.Armor += cardArmor;

		// 替换英雄技能 (tag 380 → heroPower dbfId)
		int hpDbfId = hero.CardData?.GetTag(GameTag.HeroPower) ?? 0;
		if (hpDbfId > 0 && _game.CardDbByDbfId != null)
		{
			_game.CardDbByDbfId.TryGetValue(hpDbfId, out var hpData);
			if (hpData != null)
			{
				var newPower = new HeroPower { Controller = player, CardData = hpData };
				_game.RegisterEntity(newPower);
				newPower.Cost = hpData.Cost;
				hero.Power = newPower;
				_log.Information("Hero power replaced: {HeroPower}", hpData.CardId);
			}
		}

		// 注册 OnActivate
		script?.OnActivate?.Invoke(hero, _game);

		// On Play Phase
		_bus.Publish(new CardPlayedEvent(hero, hero, target), SpellPhase.OnPlay);
		_game.RefreshAuras();
		_game.ProcessDeaths();

		// 战吼/Combo
		var actions = useCombo && script?.Combo.Length > 0 ? script.Combo : script?.Play ?? Array.Empty<Dsl.Action>();
		if (actions.Length > 0)
		{
			_game.ActionBlock(hero, actions.ToList(), BlockType.Power, target: target);
			_game.RefreshAuras();
			_game.ProcessDeaths();
		}

		_game.ActionEnd(BlockType.Play, hero);
		_game.CheckForEndGame();
	}

	// ═══════════════════════════════════════════════
	// Combat — 战斗
	// ═══════════════════════════════════════════════

	/// <summary>
	/// Execute the full combat sequence.
	/// Block structure: BEGIN(Attack) → Trigger* → damage → Trigger* → END(Attack)
	/// </summary>
	public void Combat(Character attacker, Character defender)
	{
		_log.Information("[Sequence] Combat: {Attacker} → {Defender}",
			attacker.CardId ?? attacker.EntityId.ToString(),
			defender.CardId ?? defender.EntityId.ToString());

		// ── Block: 外层 ATTACK 块 ──
		_game.ActionStart(BlockType.Attack, attacker, -1, defender);

		// Step 1: Combat Preparation Phase — 攻击提议，可重定向
		_game.ProposedAttacker = attacker;
		_game.ProposedDefender = defender;

		_bus.Publish(new ProposedAttackEvent(attacker, attacker, defender), CombatPhase.Preparation);

		// FORGETFUL: 50% chance to attack wrong enemy target
		var proposedAtkr = _game.ProposedAttacker ?? attacker;
		if (proposedAtkr.GetBoolProperty("forgetful")
			&& _game.Random.Next(2) == 0)
		{
			var opponentPlayer = proposedAtkr.Controller?.Opponent;
			if (opponentPlayer != null)
			{
				var others = opponentPlayer.Characters
					.OfType<Character>()
					.Where(c => c != (_game.ProposedDefender ?? defender) && !c.Immune && !c.Stealth)
					.ToList();
				if (others.Count > 0)
					_game.ProposedDefender = others[_game.Random.Next(others.Count)];
			}
		}

		// Use possibly-redirected targets
		var actualAttacker = _game.ProposedAttacker ?? attacker;
		var actualDefender = _game.ProposedDefender ?? defender;

		_bus.Publish(new AttackEvent(actualAttacker, actualAttacker, actualDefender), CombatPhase.Preparation);
		actualAttacker.Stealth = false;

		_game.RefreshAuras();
		_game.ProcessDeaths();
		_game.CheckForEndGame();

		if (actualAttacker.Dead || actualDefender.Dead) goto End;

		// Step 2: Combat Phase — 同时造成伤害
		{
			int atkDamage = actualAttacker.Atk;
			int defDamage = actualDefender.Atk;

			// 记录防御者受伤前的生命值（用于超杀检测）
			int defHealthBefore = actualDefender.Health;

			// 使用 Character.Hit() 造成伤害
			if (atkDamage > 0) ApplyCombatDamage(actualAttacker, actualDefender, atkDamage);
			if (defDamage > 0) ApplyCombatDamage(actualDefender, actualAttacker, defDamage);

			// 武器耐久度损失
			if (actualAttacker is Hero h && h.EquippedWeapon != null)
				ResolveWeaponDurabilityLoss(h, actualDefender);

			attacker.NumAttacks++;

			_bus.Publish(new AfterAttackEvent(actualAttacker, actualAttacker, actualDefender), CombatPhase.Combat);

			// Step 2.5: Overkill — 超杀检测（仅在己方回合触发）
			if (atkDamage > defHealthBefore && actualAttacker.Controller == _game.CurrentPlayer)
			{
				// 攻击者自身的超杀
				var atkScript = _game.ScriptRegistry.GetScript(actualAttacker.CardId ?? "");
				if (atkScript != null && atkScript.Overkill.Length > 0)
				{
					_game.ActionBlock(actualAttacker, atkScript.Overkill.ToList(), BlockType.Power);
				}

				// 英雄攻击时，武器的超杀
				if (actualAttacker is Hero hero && hero.EquippedWeapon != null)
				{
					var wpnScript = _game.ScriptRegistry.GetScript(hero.EquippedWeapon.CardId ?? "");
					if (wpnScript != null && wpnScript.Overkill.Length > 0)
					{
						_game.ActionBlock(hero.EquippedWeapon, wpnScript.Overkill.ToList(), BlockType.Power);
					}
				}
			}

			_game.RefreshAuras();
			_game.ProcessDeaths();
		}

	End:
		_game.ProposedAttacker = null;
		_game.ProposedDefender = null;

		// ── Block: 关闭 ATTACK 块 ──
		_game.ActionEnd(BlockType.Attack, attacker);
		_game.CheckForEndGame();
	}

	// ═══════════════════════════════════════════════
	// UseHeroPower — 使用英雄技能
	// ═══════════════════════════════════════════════

	/// <summary>
	/// Execute the hero power sequence.
	/// </summary>
	public void UseHeroPower(Player player, HeroPower power, Character? target)
	{
		_log.Information("[Sequence] UseHeroPower: {Player} uses {Power}",
			player.Name, power.CardId ?? power.EntityId.ToString());

		_game.ActionStart(BlockType.Play, power, -1, target);

		// On Play Phase
		_bus.Publish(new HeroPowerUsedEvent(power, player), HeroPowerPhase.OnPlay);
		_game.RefreshAuras();
		_game.ProcessDeaths();

		// Effect Phase
		var script = _game.ScriptRegistry.GetScript(power.CardId ?? "");
		if (script != null && script.Play.Length > 0)
		{
			_game.ActionBlock(power, script.Play.ToList(), BlockType.Power, target: target);
		}
		_game.RefreshAuras();
		_game.ProcessDeaths();

		// After Play Phase
		_bus.Publish(new HeroPowerUsedEvent(power, player), HeroPowerPhase.AfterPlay);
		_game.RefreshAuras();
		_game.ProcessDeaths();

		// Inspire：英雄技能结算后，依次触发己方场上随从的激励效果。
		foreach (var minion in player.Field.OfType<Minion>().ToList())
		{
			if (minion.Dead) continue;
			var minionScript = _game.ScriptRegistry.GetScript(minion.CardId ?? "");
			if (minionScript == null || minionScript.Inspire.Length == 0) continue;

			_game.ActionBlock(minion, minionScript.Inspire.ToList(), BlockType.Power);
			_game.RefreshAuras();
			_game.ProcessDeaths();
		}

		_game.ActionEnd(BlockType.Play, power);
		_game.CheckForEndGame();
	}
	// ═══════════════════════════════════════════════
	// PlayLocation — 打出地标
	// ═══════════════════════════════════════════════

	/// <summary>
	/// Execute the location-play sequence.
	/// Location enters the battlefield (Field), no immediate effect.
	/// </summary>
	public void PlayLocation(Location location, ICardScript? script = null)
	{
		var player = location.Controller;
		if (player == null) return;

		script ??= _game.ScriptRegistry.GetScript(location.CardId ?? "");

		_log.Information("[Sequence] PlayLocation: {Location}", location.CardId ?? location.EntityId.ToString());

		_game.ActionStart(BlockType.Play, location, -1, null);

		// 支付费用
		PayCardCost(player, location);
		location.TurnPlayed = _game.Turn;

		// 放入战场
		player.Summon(location);

		// 注册 OnActivate
		script?.OnActivate?.Invoke(location, _game);

		// On Play Phase
		_bus.Publish(new CardPlayedEvent(location, location, null), SpellPhase.OnPlay);
		_game.RefreshAuras();
		_game.ProcessDeaths();

		_game.ActionEnd(BlockType.Play, location);
		_game.CheckForEndGame();
	}

	// ═══════════════════════════════════════════════
	// UseLocation — 使用地标
	// ═══════════════════════════════════════════════

	/// <summary>
	/// Execute the location-use sequence.
	/// Triggers the Location hook, consumes 1 durability, sets cooldown.
	/// </summary>
	public void UseLocation(Player player, Location location, Character? target)
	{
		_log.Information("[Sequence] UseLocation: {Player} uses {Location}",
			player.Name, location.CardId ?? location.EntityId.ToString());

		var script = _game.ScriptRegistry.GetScript(location.CardId ?? "");

		_game.ActionStart(BlockType.Play, location, -1, target);

		// Store target for S.TARGET access
		if (target != null)
			location.CurrentTargets = new List<BaseEntity> { target };

		// Effect Phase — 执行 Location 钩子
		if (script != null && script.Location.Length > 0)
		{
			_game.ActionBlock(location, script.Location.ToList(), BlockType.Power, target: target);
		}

		// 消耗耐久 + 设置冷却
		location.Use();

		_game.RefreshAuras();
		_game.ProcessDeaths();

		_game.ActionEnd(BlockType.Play, location);
		_game.CheckForEndGame();
	}

	// ═══════════════════════════════════════════════
	// UseTitanAbility — 使用泰坦技能
	// ═══════════════════════════════════════════════

	/// <summary>
	/// Execute one of a Titan's three abilities.
	/// Looks up the ability card script, executes its Play actions,
	/// marks the ability as used, and consumes one attack action.
	/// </summary>
	public void UseTitanAbility(Player player, Minion titan, int abilityIndex, Character? target)
	{
		var abilityIds = titan.TitanAbilityCardIds;
		var abilityCardId = abilityIds[abilityIndex];

		_log.Information("[Sequence] UseTitanAbility: {Player} uses {Titan} ability {Index} ({AbilityId})",
			player.Name, titan.CardId, abilityIndex + 1, abilityCardId);

		var script = _game.ScriptRegistry.GetScript(abilityCardId);

		_game.ActionStart(BlockType.Power, titan, -1, target);

		// Store target for S.TARGET access
		if (target != null)
			titan.CurrentTargets = new List<BaseEntity> { target };

		// Execute ability script (using its Play hook)
		if (script != null && script.Play.Length > 0)
		{
			_game.ActionBlock(titan, script.Play.ToList(), BlockType.Power, target: target);
		}

		// Mark ability as used
		switch (abilityIndex)
		{
			case 0: titan.TitanAbility1Used = true; break;
			case 1: titan.TitanAbility2Used = true; break;
			case 2: titan.TitanAbility3Used = true; break;
		}

		// Consume one attack action
		titan.NumAttacks++;

		_game.RefreshAuras();
		_game.ProcessDeaths();

		_game.ActionEnd(BlockType.Power, titan);
		_game.CheckForEndGame();
	}

	// ── Colossal helper ──

	/// <summary>
	/// 巨型附肢召唤：扫描 {cardId}t, t2 ... t9，仅召唤存在且带 ColossalLimb 标记的。
	/// 附肢也走 Player.Summon 流程，触发入场事件。
	/// </summary>
	public void SummonColossalLimbs(Minion source, Player player)
	{
		var cardData = source.CardData;
		if (cardData == null || !cardData.HasTag(GameTag.Colossal)) return;

		var cardDb = _game.CardDb;
		if (cardDb == null) return;

		var cardId = source.CardId ?? "";

		for (int i = 1; i <= 9; i++)
		{
			var limbId = i == 1 ? cardId + "t" : cardId + "t" + i;
			if (cardDb.TryGetValue(limbId, out var limbData) && limbData.IsColossalLimb)
			{
				var limb = player.CreateCard(limbData, Zone.Invalid);
				if (limb != null)
				{
					_log.Information("[Colossal] {Source} summons limb {Limb}", cardId, limbId);
					player.Summon(limb);

					// 注册附肢脚本效果（光环、触发等）
					var limbScript = _game.ScriptRegistry.GetScript(limbId);
					limbScript?.OnActivate?.Invoke(limb, _game);

					// 附肢入场事件
					if (limb is Minion limbMinion)
						_bus.Publish(new MinionSummonedEvent(source, limbMinion), MinionPhase.AfterSummon);
				}
			}
		}
	}
}
