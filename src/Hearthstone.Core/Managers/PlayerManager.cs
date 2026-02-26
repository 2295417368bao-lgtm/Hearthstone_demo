namespace HearthstoneServer.Managers;

/// <summary>
/// 玩家管理器。
/// 定义 Player 实体上 GameTag → 属性名的映射。
/// </summary>
public class PlayerManager : Manager
{
    private static readonly Dictionary<GameTag, string?> _map = new()
    {
        [GameTag.CantDraw] = "cant_draw",
        [GameTag.CardType] = "type",
        [GameTag.ComboActive] = "combo",
        [GameTag.Controller] = "controller",
        [GameTag.CurrentPlayer] = "current_player",
        [GameTag.CurrentSpellpower] = "spellpower",
        [GameTag.CurrentHeropowerDamageBonus] = "heropower_damage",
        [GameTag.EmbraceTheShadow] = "healing_as_damage",
        [GameTag.Fatigue] = "fatigue_counter",
        [GameTag.FirstPlayer] = "first_player",
        [GameTag.HealingDouble] = "healing_double",
        [GameTag.HeroEntity] = "hero",
        [GameTag.InvokeCounter] = "invoke_counter",
        [GameTag.LastCardPlayed] = "last_card_played",
        [GameTag.Maxhandsize] = "max_hand_size",
        [GameTag.Maxresources] = "max_resources",
        [GameTag.NumCardsDrawnThisTurn] = "cards_drawn_this_turn",
        [GameTag.NumCardsPlayedThisTurn] = "cards_played_this_turn",
        [GameTag.NumHeroPowerDamageThisGame] = "hero_power_damage_this_game",
        [GameTag.AmountHealedThisGame] = "healed_this_game",
        [GameTag.NumMinionsPlayedThisTurn] = "minions_played_this_turn",
        [GameTag.NumMinionsPlayerKilledThisTurn] = "minions_killed_this_turn",
        [GameTag.NumTimesHeroPowerUsedThisGame] = "times_hero_power_used_this_game",
        [GameTag.OverloadLocked] = "overload_locked",
        [GameTag.OverloadOwed] = "overloaded",
        [GameTag.OverloadThisGame] = "overloaded_this_game",
        [GameTag.Playstate] = "playstate",
        [GameTag.Resources] = "max_mana",
        [GameTag.ResourcesUsed] = "used_mana",
        [GameTag.SpellpowerDouble] = "spellpower_double",
        [GameTag.Starthandsize] = "start_hand_size",
        [GameTag.HeroPowerDouble] = "hero_power_double",
        [GameTag.TempResources] = "temp_mana",
        [GameTag.Timeout] = "timeout",
        [GameTag.TurnStart] = "turn_start",
        // 内部标签
        [GameTag.CantOverload] = "cant_overload",
        [GameTag.ElementalPlayedLastTurn] = "elemental_played_last_turn",
    };

    protected override Dictionary<GameTag, string?> Map => _map;

    public PlayerManager(Entities.BaseEntity obj) : base(obj) { }
}
