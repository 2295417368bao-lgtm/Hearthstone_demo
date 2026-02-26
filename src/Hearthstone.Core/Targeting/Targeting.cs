namespace HearthstoneServer.Targeting;

/// <summary>
/// 目标验证逻辑。
/// 判断一个实体是否能被某张卡牌/英雄技能在打出时选为目标。
/// </summary>
public static class TargetingRules
{
	/// <summary>
	/// 验证 <paramref name="target"/> 是否为 <paramref name="source"/> 的合法目标。
	/// </summary>
	public static bool IsValidTarget(
		Entities.BuffableEntity source,
		Entities.BaseEntity target,
		TargetRequirement? req = null)
	{
		// 不能选自身（战吼默认行为）
		if (ReferenceEquals(target, source)) return false;

		// 随从特有检查
		if (target.Type == CardType.Minion && target is Entities.Minion minion)
		{
			if (minion.Dormant) return false;
			if (minion is Entities.Character mChar && mChar.Dead) return false;
			if (minion.Stealth && source.Controller != minion.Controller) return false;
			if (minion.Immune && source.Controller != minion.Controller) return false;
			if (source.Type == CardType.Spell && minion.CantBeTargetedByAbilities) return false;
			if (source.Type == CardType.HeroPower && minion.CantBeTargetedByHeroPowers) return false;
		}

		// 不能被对手选中的角色
		if (target is Entities.Character charTarget)
		{
			if (charTarget.CantBeTargetedByOpponents && source.Controller != charTarget.Controller)
				return false;
		}

		// 获取目标需求
		var reqs = req ?? source.TargetReq;
		if (reqs == null || !reqs.HasTargeting) return false;

		// ── 目标类型过滤 ──

		if (reqs.MinionOnly && target.Type != CardType.Minion)
			return false;

		if (reqs.HeroOnly && target.Type != CardType.Hero)
			return false;

		if (reqs.LocationOnly && target.Type != CardType.Location)
			return false;

		if (reqs.MinionOrEnemyHero)
		{
			if (target.Type != CardType.Minion)
			{
				if (target is not Entities.Hero ||
					(target is Entities.BuffableEntity mh && mh.Controller == source.Controller))
					return false;
			}
		}

		// ── 阵营过滤 ──

		if (reqs.FriendlyOnly && target is Entities.BuffableEntity ft && ft.Controller != source.Controller)
			return false;

		if (reqs.EnemyOnly && target is Entities.BuffableEntity et && et.Controller == source.Controller)
			return false;

		if (reqs.NonSelf && ReferenceEquals(target, source))
			return false;

		// ── 属性过滤 ──

		if (reqs.MaxAttack >= 0 && target is Entities.Character mac && mac.Atk > reqs.MaxAttack)
			return false;

		if (reqs.MinAttack >= 0 && target is Entities.Character mic && mic.Atk < reqs.MinAttack)
			return false;

		if (reqs.DamagedOnly && target is Entities.Character dc && dc.DamageTaken <= 0)
			return false;

		if (reqs.UndamagedOnly && target is Entities.Character uc && uc.DamageTaken > 0)
			return false;

		if (reqs.FrozenOnly && target is Entities.Character fc && !fc.Frozen)
			return false;

		if (reqs.TauntOnly && (target is not Entities.Character tc || !tc.Taunt))
			return false;

		if (reqs.LegendaryOnly && target is Entities.BuffableEntity lt && lt.CardData?.Rarity != Rarity.Legendary)
			return false;

		if (reqs.WithMechanic.HasValue && target is Entities.BuffableEntity mechTarget
			&& mechTarget.CardData?.Has(reqs.WithMechanic.Value) != true)
			return false;

		if (reqs.WithRace >= 0 && target is Entities.Character rc && !rc.Races.Contains((Race)reqs.WithRace))
			return false;

		if (reqs.EnemyNotImmune && target is Entities.Character ei &&
			ei.Immune && source.Controller != ei.Controller)
			return false;

		if (reqs.FriendlyLackey && (target is not Entities.BuffableEntity lk || !lk.GetBoolProperty("mark_of_evil")))
			return false;

		if (reqs.AttackLessOrEqualMana && target is Entities.Character mam &&
			source.Controller != null && mam.Atk > source.Controller.Mana)
			return false;

		return true;
	}

	/// <summary>
	/// 枚举场上所有合法目标（用于随机目标选择等场景）。
	/// </summary>
	public static List<Entities.Character> GetValidTargets(
		Entities.BuffableEntity source,
		TargetRequirement req)
	{
		var player = source.Controller;
		if (player == null) return new();

		var candidates = new List<Entities.Character>();
		if (player.Hero != null) candidates.Add(player.Hero);
		if (player.Opponent?.Hero != null) candidates.Add(player.Opponent.Hero);
		foreach (var m in player.Field.OfType<Entities.Minion>()) candidates.Add(m);
		if (player.Opponent != null)
			foreach (var m in player.Opponent.Field.OfType<Entities.Minion>()) candidates.Add(m);

		var result = new List<Entities.Character>();
		foreach (var c in candidates)
		{
			if (IsValidTarget(source, c, req))
				result.Add(c);
		}
		return result;
	}
}
