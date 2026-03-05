using HearthstoneServer.Dsl;
using HearthstoneServer.Entities;
using HearthstoneServer.LoadCardIntoDbf;

namespace HearthstoneServer.Dto;

/// <summary>
/// Full game state DTO for initial load and reconnect.
/// Moved from Hearthstone.WebApi to Hearthstone.Core for shared use.
/// </summary>
public static class GameStateMapper
{
    public static object ToFullState(Game game, Player viewer)
    {
        var opponent = viewer.Opponent!;
        return new
        {
            turn = game.Turn,
            gameState = game.State.ToString(),
            currentPlayerId = game.CurrentPlayer?.EntityId,
            isMyTurn = game.CurrentPlayer == viewer,
            mulliganState = game.Turn == 0 ? "waiting" : "done",

            player = MapPlayer(viewer, isViewer: true),
            opponent = MapPlayer(opponent, isViewer: false),

            // 多目标战吼选择状态
            pendingTargetChoice = MapPendingTargetChoice(viewer),
        };
    }

    /// <summary>如果玩家有待处理的 BattlecryTargetChoice，序列化给前端。</summary>
    private static object? MapPendingTargetChoice(Player viewer)
    {
        if (viewer.Choice is not BattlecryTargetChoice btc) return null;
        return new
        {
            type = "battlecryTarget",
            validTargetIds = btc.Cards.Select(c => c.EntityId).ToArray(),
            chosenCount = btc.ChosenTargets.Count,
            totalNeeded = btc.Cards.Count + btc.ChosenTargets.Count, // remaining + already chosen
            sourceCardId = (btc.Source as BuffableEntity)?.CardId,
        };
    }

    private static object MapPlayer(Player p, bool isViewer)
    {
        return new
        {
            entityId = p.EntityId,
            name = p.Name,
            mana = p.Mana,
            maxMana = p.MaxMana,
            overload = p.Overloaded,
            spellPower = p.SpellPower,
            deckCount = p.Deck.Count,
            hero = p.Hero == null ? null : MapHero(p.Hero),
            field = p.Field.OfType<Minion>().Select(MapMinion).ToArray(),
            hand = isViewer
                ? p.Hand.OfType<BuffableEntity>().Select(MapHandCard).ToArray()
                : null,
            handCount = p.Hand.Count,
            secrets = isViewer
                ? p.Secrets.OfType<BuffableEntity>().Select(e =>
                    new { entityId = e.EntityId, cardId = e.CardId }).ToArray()
                : null,
            secretCount = p.Secrets.Count,
        };
    }

    private static object MapHero(Hero h)
    {
        return new
        {
            entityId = h.EntityId,
            cardId = h.CardId,
            health = h.Health,
            maxHealth = h.MaxHealth,
            armor = h.Armor,
            atk = h.Atk,
            weapon = h.EquippedWeapon == null ? null : new
            {
                entityId = h.EquippedWeapon.EntityId,
                cardId = h.EquippedWeapon.CardId,
                atk = h.EquippedWeapon.Atk,
                durability = h.EquippedWeapon.Durability,
            },
            heroPower = h.Power == null ? null : new
            {
                entityId = h.Power.EntityId,
                cardId = h.Power.CardId,
                cost = h.Power.Cost,
                exhausted = h.Power.Exhausted,
                requiresTarget = h.Power.CardId != null
                    && HearthstoneServer.Dsl.CardScriptRegistry.Instance.GetScript(h.Power.CardId) is { } hpScript
                    && hpScript.TargetReq != HearthstoneServer.Targeting.TargetRequirement.None,
            },
        };
    }

    private static object MapMinion(Minion m)
    {
        return new
        {
            entityId = m.EntityId,
            cardId = m.CardId,
            name = GetCardName(m),
            atk = m.Atk,
            health = m.Health,
            maxHealth = m.MaxHealth,
            canAttack = m.CanAttack(),
            taunt = m.Taunt,
            divineShield = m.DivineShield,
            stealth = m.Stealth,
            frozen = m.Frozen,
            silenced = m.Silenced,
            windfury = m.Windfury,
            poisonous = m.Poisonous,
            deathrattle = m.HasDeathrattle,
            spellDamage = m.SpellPower > 0,
            spellPowerValue = m.SpellPower,
            zonePosition = m.ZonePosition,
            buffIds = MapBuffIds(m),
        };
    }

    private static object MapHandCard(BuffableEntity e)
    {
        var atk = e is Minion m1 ? m1.Atk : (e is Weapon w1 ? w1.Atk : (int?)null);
        var health = e is Minion m2 ? m2.Health : (int?)null;
        var durability = e is Weapon w2 ? w2.Durability : (int?)null;
        return new
        {
            entityId = e.EntityId,
            cardId = e.CardId,
            name = GetCardName(e),
            cost = e.Cost,
            atk,
            health,
            durability,
            type = e.Type.ToString(),
            requiresTarget = e.RequiresTarget,
            canPlay = CanPlayCard(e),
            validTargets = GetValidTargets(e),
            buffIds = MapBuffIds(e),
        };
    }

    /// <summary>Check if a card can be played by its controller.</summary>
    private static bool CanPlayCard(BuffableEntity card)
    {
        var player = card.Controller;
        if (player == null) return false;
        if (card.Cost > player.Mana) return false;

        if (card.TargetReq != null)
        {
            var req = card.TargetReq;
            var opponent = player.Opponent;
            if (req.MinEnemyMinions > 0 && opponent != null)
            {
                if (opponent.Field.Count < req.MinEnemyMinions) return false;
            }
            if (req.MinTotalMinions > 0)
            {
                int total = player.Field.Count + (player.Opponent?.Field.Count ?? 0);
                if (total < req.MinTotalMinions) return false;
            }
            if (req.NeedsWeapon && player.Hero?.EquippedWeapon == null) return false;
        }

        if (card.RequiresTarget)
        {
            var targets = GetValidTargets(card);
            if (targets.Length == 0) return false;
        }

        if (card is Minion && player.Field.Count >= 7) return false;

        return true;
    }

    /// <summary>Get valid target entity IDs for a card.</summary>
    private static int[] GetValidTargets(BuffableEntity card)
    {
        var player = card.Controller;
        if (player == null || card.TargetReq == null) return Array.Empty<int>();
        if (!card.RequiresTarget && !card.TargetReq.TargetIfAvailable) return Array.Empty<int>();

        var targets = new List<int>();
        var req = card.TargetReq;

        var candidates = new List<Character>();
        if (player.Hero != null) candidates.Add(player.Hero);
        if (player.Opponent?.Hero != null) candidates.Add(player.Opponent.Hero);
        foreach (var m in player.Field.OfType<Minion>()) candidates.Add(m);
        if (player.Opponent != null)
            foreach (var m in player.Opponent.Field.OfType<Minion>()) candidates.Add(m);

        foreach (var c in candidates)
        {
            if (Targeting.TargetingRules.IsValidTarget(card, c, req))
                targets.Add(c.EntityId);
        }

        return targets.ToArray();
    }

    /// <summary>Get card display name: zhCN → enUS → cardId.</summary>
    private static string GetCardName(BuffableEntity e)
    {
        if (e.CardData == null) return e.CardId ?? "???";
        var cd = e.CardData;
        // GameTag 185 = CARDNAME
        if (cd.TagsLoc.TryGetValue((GameTag)185, out var locDict))
        {
            if (locDict.TryGetValue("zhCN", out var zhName)) return zhName;
            if (locDict.TryGetValue("enUS", out var enName)) return enName;
        }
        return e.CardId ?? "???";
    }

    /// <summary>Map entity buff cardIds for frontend lookup.</summary>
    private static string[] MapBuffIds(BuffableEntity e)
    {
        if (e.Buffs.Count == 0) return Array.Empty<string>();
        return e.Buffs
            .OfType<Enchantment>()
            .Where(b => b.BuffId != null || b.CardId != null)
            .Select(b => b.BuffId ?? b.CardId!)
            .ToArray();
    }
}
