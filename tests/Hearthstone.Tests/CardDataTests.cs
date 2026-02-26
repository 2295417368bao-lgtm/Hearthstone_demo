using HearthstoneServer;
using HearthstoneServer.LoadCardIntoDbf;

namespace Hearthstone.Tests;

/// <summary>
/// Card 数据层测试�?
/// 覆盖：Card 模型字段、Has() 方法、TagsInt/ReferencedTagsInt/Unknown 区分�?
/// 
/// 重要说明�?
/// - TagsInt + ReferencedTagsInt 合起来记录的�?GameTag 枚举中定义的所有标�?
/// - UnknownTagsInt / UnknownReferencedTagsInt 记录的是 GameTag 枚举中不存在的标签（key �?raw int�?
/// </summary>
public class CardDataTests
{
    // ── Card 基本字段 ──

    [Fact]
    public void Card_BasicFields_Init()
    {
        var card = new Card
        {
            DbfId = 1234,
            CardId = "CS2_024",
            Cost = 3,
            Attack = 3,
            Health = 3,
            CardClass = CardClass.Warrior,
            CardSet = CardSet.Expert1,
            CardType = CardType.Minion,
            Rarity = Rarity.Common,
        };

        Assert.Equal(1234, card.DbfId);
        Assert.Equal("CS2_024", card.CardId);
        Assert.Equal(3, card.Cost);
        Assert.Equal(3, card.Attack);
        Assert.Equal(3, card.Health);
        Assert.Equal(CardClass.Warrior, card.CardClass);
        Assert.Equal(CardSet.Expert1, card.CardSet);
        Assert.Equal(CardType.Minion, card.CardType);
        Assert.Equal(Rarity.Common, card.Rarity);
    }

    [Fact]
    public void Card_DefaultValues()
    {
        var card = new Card();
        Assert.Equal(0, card.DbfId);
        Assert.Equal("", card.CardId);
        Assert.Equal(CardClass.Invalid, card.CardClass);
        Assert.Equal(CardSet.Invalid, card.CardSet);
        Assert.Equal(CardType.Invalid, card.CardType);
        Assert.Equal(Faction.Invalid, card.Faction);
        Assert.Equal(Race.Invalid, card.Race);
        Assert.Equal(Rarity.Invalid, card.Rarity);
        Assert.Equal(SpellSchool.None, card.SpellSchool);
        Assert.Equal(FactionType.Generic, card.FactionType);
    }

    // ── Mechanics ──

    [Fact]
    public void Card_Has_ReturnsTrueForPresentMechanic()
    {
        var card = new Card
        {
            Mechanics = new[] { Mechanic.TAUNT, Mechanic.DIVINE_SHIELD, Mechanic.CHARGE },
        };

        Assert.True(card.Has(Mechanic.TAUNT));
        Assert.True(card.Has(Mechanic.DIVINE_SHIELD));
        Assert.True(card.Has(Mechanic.CHARGE));
    }

    [Fact]
    public void Card_Has_ReturnsFalseForAbsentMechanic()
    {
        var card = new Card
        {
            Mechanics = new[] { Mechanic.TAUNT },
        };

        Assert.False(card.Has(Mechanic.CHARGE));
        Assert.False(card.Has(Mechanic.BATTLECRY));
    }

    [Fact]
    public void Card_Has_EmptyMechanics_AlwaysFalse()
    {
        var card = new Card();
        Assert.False(card.Has(Mechanic.TAUNT));
    }

    // ── Races / Classes 数组 ──

    [Fact]
    public void Card_MultiRace()
    {
        var card = new Card
        {
            Races = new[] { Race.Beast, Race.Dragon },
            Race = Race.Beast, // 主种�?
        };

        Assert.Equal(2, card.Races.Length);
        Assert.Contains(Race.Beast, card.Races);
        Assert.Contains(Race.Dragon, card.Races);
    }

    [Fact]
    public void Card_MultiClass()
    {
        var card = new Card
        {
            Classes = new[] { CardClass.Mage, CardClass.Rogue },
        };

        Assert.Equal(2, card.Classes.Length);
        Assert.Contains(CardClass.Mage, card.Classes);
        Assert.Contains(CardClass.Rogue, card.Classes);
    }

    // ── TagsInt �?记录 GameTag 枚举中存在的标签 ──

    [Fact]
    public void Card_TagsInt_StoresKnownGameTags()
    {
        var card = new Card
        {
            TagsInt = new Dictionary<GameTag, int>
            {
                [GameTag.Atk] = 4,
                [GameTag.Health] = 5,
                [GameTag.Cost] = 3,
                [GameTag.Taunt] = 1,
                [GameTag.CardType] = (int)CardType.Minion,
                [GameTag.Class] = (int)CardClass.Paladin,
            },
        };

        Assert.Equal(4, card.TagsInt[GameTag.Atk]);
        Assert.Equal(5, card.TagsInt[GameTag.Health]);
        Assert.Equal(3, card.TagsInt[GameTag.Cost]);
        Assert.Equal(1, card.TagsInt[GameTag.Taunt]);
        Assert.Equal((int)CardType.Minion, card.TagsInt[GameTag.CardType]);
    }

    // ── ReferencedTagsInt �?也是 GameTag 枚举中的标签 ──

    [Fact]
    public void Card_ReferencedTagsInt_StoresKnownGameTags()
    {
        // ReferencedTags 通常记录�?"当打出此牌时，参考这�?tag �? 的标�?
        var card = new Card
        {
            ReferencedTagsInt = new Dictionary<GameTag, int>
            {
                [GameTag.TagScriptDataNum1] = 3,
                [GameTag.TagScriptDataNum2] = 1,
            },
        };

        Assert.Equal(3, card.ReferencedTagsInt[GameTag.TagScriptDataNum1]);
        Assert.Equal(1, card.ReferencedTagsInt[GameTag.TagScriptDataNum2]);
    }

    [Fact]
    public void Card_TagsIntAndReferencedTags_Together_CoverAllKnownTags()
    {
        // TagsInt + ReferencedTagsInt 合在一起才是所�?GameTag 枚举中定义的标签
        var card = new Card
        {
            TagsInt = new Dictionary<GameTag, int>
            {
                [GameTag.Atk] = 4,
                [GameTag.Health] = 5,
            },
            ReferencedTagsInt = new Dictionary<GameTag, int>
            {
                [GameTag.TagScriptDataNum1] = 2,
            },
        };

        // 所有已知标�?= TagsInt �?ReferencedTagsInt
        var allKnownTags = new Dictionary<GameTag, int>(card.TagsInt);
        foreach (var (k, v) in card.ReferencedTagsInt)
            allKnownTags[k] = v;

        Assert.Equal(3, allKnownTags.Count);
        Assert.True(allKnownTags.ContainsKey(GameTag.Atk));
        Assert.True(allKnownTags.ContainsKey(GameTag.Health));
        Assert.True(allKnownTags.ContainsKey(GameTag.TagScriptDataNum1));

        // 验证：所�?key 都是有效�?GameTag 枚举�?
        foreach (var tag in allKnownTags.Keys)
        {
            Assert.True(Enum.IsDefined(typeof(GameTag), tag),
                $"Tag {tag} ({(int)tag}) should be defined in GameTag enum");
        }
    }

    // ── UnknownTagsInt �?不在 GameTag 枚举中的标签 ──

    [Fact]
    public void Card_UnknownTagsInt_StoresNonEnumTags()
    {
        // Unknown tags �?key �?raw int（不�?GameTag 枚举值）
        var card = new Card
        {
            UnknownTagsInt = new Dictionary<int, int>
            {
                [99999] = 1,
                [88888] = 42,
            },
        };

        Assert.Equal(1, card.UnknownTagsInt[99999]);
        Assert.Equal(42, card.UnknownTagsInt[88888]);

        // 验证：这�?key 不应该在 GameTag 枚举中有定义
        foreach (var rawId in card.UnknownTagsInt.Keys)
        {
            Assert.False(Enum.IsDefined(typeof(GameTag), rawId),
                $"Raw tag id {rawId} should NOT be defined in GameTag enum (it's unknown)");
        }
    }

    [Fact]
    public void Card_UnknownReferencedTagsInt_StoresNonEnumTags()
    {
        var card = new Card
        {
            UnknownReferencedTagsInt = new Dictionary<int, int>
            {
                [77777] = 5,
            },
        };

        Assert.Equal(5, card.UnknownReferencedTagsInt[77777]);
        Assert.False(Enum.IsDefined(typeof(GameTag), 77777));
    }

    // ── TagsLoc �?多语言字符�?──

    [Fact]
    public void Card_TagsLoc_StoresLocStrings()
    {
        var card = new Card
        {
            TagsLoc = new Dictionary<GameTag, Dictionary<string, string>>
            {
                [GameTag.Cardname] = new()
                {
                    ["enUS"] = "Frostbolt",
                    ["zhCN"] = "寒冰箭",
                },
            },
        };

        Assert.Equal("Frostbolt", card.TagsLoc[GameTag.Cardname]["enUS"]);
        Assert.Equal("寒冰箭", card.TagsLoc[GameTag.Cardname]["zhCN"]);
    }

    // ── TagsStr �?普通字符串标签 ──

    [Fact]
    public void Card_TagsStr_StoresStringValues()
    {
        var card = new Card
        {
            TagsStr = new Dictionary<GameTag, string>
            {
                [GameTag.Artistname] = "Alex Horley",
            },
        };

        Assert.Equal("Alex Horley", card.TagsStr[GameTag.Artistname]);
    }

    // ── 额外字段 ──

    [Fact]
    public void Card_SpellSchool()
    {
        var card = new Card { SpellSchool = SpellSchool.Frost };
        Assert.Equal(SpellSchool.Frost, card.SpellSchool);
    }

    [Fact]
    public void Card_Durability_ForWeapons()
    {
        var card = new Card
        {
            CardType = CardType.Weapon,
            Attack = 3,
            Durability = 2,
        };

        Assert.Equal(CardType.Weapon, card.CardType);
        Assert.Equal(3, card.Attack);
        Assert.Equal(2, card.Durability);
    }

    [Fact]
    public void Card_QuestProgressTotal()
    {
        var card = new Card { QuestProgressTotal = 5 };
        Assert.Equal(5, card.QuestProgressTotal);
    }
}
