using System.Text.Encodings.Web;
using System.Text.Json;

namespace HearthstoneServer.LoadCardIntoDbf;

public static class CardDebugPrinter
{
    public static void PrintCard(Card c, bool printTags = true, bool printReferencedTags = true)
    {
        Console.WriteLine("========== Card ==========");
        Console.WriteLine($"dbfId     : {c.DbfId}");
        Console.WriteLine($"cardId    : {c.CardId}");
        Console.WriteLine($"version   : {c.Version}");
        Console.WriteLine();

        Console.WriteLine("---- Stats ----");
        Console.WriteLine($"COST={c.Cost}  ATK={c.Attack}  HEALTH={c.Health}  DURABILITY={c.Durability}");
        Console.WriteLine($"CardType={c.CardType}  CardClass={c.CardClass}  CardSet={c.CardSet}  Faction={c.Faction}  Race={c.Race}  Rarity={c.Rarity}  SpellSchool={c.SpellSchool}  FactionType={c.FactionType}");
        Console.WriteLine();

        Console.WriteLine("---- MECHANICS ----");
        Console.WriteLine(c.Mechanics.Length == 0 ? "(none)" : string.Join(", ", c.Mechanics));
        Console.WriteLine();

        Console.WriteLine("---- RACES ----");
        Console.WriteLine(c.Races.Length == 0 ? "(none)" : string.Join(", ", c.Races));
        Console.WriteLine();

        Console.WriteLine("---- CLASSES ----");
        Console.WriteLine(c.Classes.Length == 0 ? "(none)" : string.Join(", ", c.Classes));
        Console.WriteLine();

        var jsonOpt = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        if (printTags)
        {
            Console.WriteLine("---- TAGS_INT (enumId -> int) ----");
            Console.WriteLine(JsonSerializer.Serialize(ToEnumIdKeyDict(c.TagsInt), jsonOpt));
            Console.WriteLine();

            Console.WriteLine("---- TAGS_LOC (enumId -> {locale->text}) ----");
            Console.WriteLine(JsonSerializer.Serialize(ToEnumIdKeyDict(c.TagsLoc), jsonOpt));
            Console.WriteLine();

            Console.WriteLine("---- TAGS_STR (enumId -> string) ----");
            Console.WriteLine(JsonSerializer.Serialize(ToEnumIdKeyDict(c.TagsStr), jsonOpt));
            Console.WriteLine();

            Console.WriteLine("---- UNKNOWN_TAGS_INT (enumId -> int) ----");
            Console.WriteLine(JsonSerializer.Serialize(c.UnknownTagsInt, jsonOpt));
            Console.WriteLine();
        }

        if (printReferencedTags)
        {
            Console.WriteLine("---- REFERENCED_TAGS_INT (enumId -> int) ----");
            Console.WriteLine(JsonSerializer.Serialize(ToEnumIdKeyDict(c.ReferencedTagsInt), jsonOpt));
            Console.WriteLine();

            Console.WriteLine("---- UNKNOWN_REFERENCED_TAGS_INT (enumId -> int) ----");
            Console.WriteLine(JsonSerializer.Serialize(c.UnknownReferencedTagsInt, jsonOpt));
            Console.WriteLine();
        }
    }

    private static Dictionary<string, int> ToEnumIdKeyDict(Dictionary<GameTag, int> d)
        => d.ToDictionary(kv => ((int)kv.Key).ToString(), kv => kv.Value, StringComparer.Ordinal);

    private static Dictionary<string, string> ToEnumIdKeyDict(Dictionary<GameTag, string> d)
        => d.ToDictionary(kv => ((int)kv.Key).ToString(), kv => kv.Value, StringComparer.Ordinal);

    private static Dictionary<string, Dictionary<string, string>> ToEnumIdKeyDict(Dictionary<GameTag, Dictionary<string, string>> d)
        => d.ToDictionary(kv => ((int)kv.Key).ToString(), kv => kv.Value, StringComparer.Ordinal);
}
