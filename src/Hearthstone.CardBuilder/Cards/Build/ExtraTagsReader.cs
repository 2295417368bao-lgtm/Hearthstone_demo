using System.Text.Json;
using HearthstoneServer.Cards.Build.Model;
namespace HearthstoneServer.Cards.Build;


public static class ExtraTagsReader
{
    public static Dictionary<string, ExtraTags> LoadByDbfId(string newCardsArrayPath)
    {
        using var doc = JsonDocument.Parse(File.ReadAllBytes(newCardsArrayPath));
        if (doc.RootElement.ValueKind != JsonValueKind.Array)
            throw new Exception("新 card.json 不是数组（期望 [ {...}, {...} ] ）");

        var dict = new Dictionary<string, ExtraTags>(StringComparer.Ordinal);

        foreach (var item in doc.RootElement.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object) continue;

            if (!item.TryGetProperty("dbfId", out var dbfEl) || !dbfEl.TryGetInt32(out var dbfId) || dbfId <= 0)
                continue;

            string key = dbfId.ToString();

            string? mechanicsJson = null;
            if (item.TryGetProperty("mechanics", out var mEl) && mEl.ValueKind == JsonValueKind.Array)
                mechanicsJson = mEl.GetRawText();

            string? racesJson = null;
            if (item.TryGetProperty("races", out var rEl) && rEl.ValueKind == JsonValueKind.Array)
                racesJson = rEl.GetRawText();
            
            string? classesJson = null;
            if (item.TryGetProperty("classes", out var cEl) && cEl.ValueKind == JsonValueKind.Array)
                classesJson = cEl.GetRawText();

            if (mechanicsJson is null && classesJson is null && racesJson is null) continue;

            dict[key] = new ExtraTags(mechanicsJson, racesJson, classesJson);
        }

        return dict;
    }
}