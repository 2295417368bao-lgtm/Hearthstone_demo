using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using HearthstoneServer.Cards.Build.Model;

namespace HearthstoneServer.Cards.Build;

public static class TagsMerger
{
    // existingCardsPath：{ "2566": { tags:{...}, ... }, ... }
    public static void MergeIntoTags(
        string existingCardsPath,
        Dictionary<string, ExtraTags> extraByDbfId,
        string outputPath,
        bool pretty = false)
    {
        var root = JsonNode.Parse(File.ReadAllText(existingCardsPath)) as JsonObject
                   ?? throw new Exception("existing cards.json 根节点不是 JSON Object（期望 { \"dbfId\": {...} }）");

        int hit = 0, mechWrite = 0, raceWrite = 0, classWrite=0;

        foreach (var (dbfIdKey, cardNode) in root)
        {
            if (cardNode is not JsonObject cardObj) continue;
            if (!extraByDbfId.TryGetValue(dbfIdKey, out var ex)) continue;

            // 方案 A：mechanics / races 挂在顶层字段，不进 tags
            if (!string.IsNullOrWhiteSpace(ex.MechanicsJson))
            {
                var mNode = JsonNode.Parse(ex.MechanicsJson);
                if (mNode is JsonArray)
                {
                    cardObj["mechanics"] = mNode; // 直接挂数组
                    mechWrite++;
                }
            }

            if (!string.IsNullOrWhiteSpace(ex.RacesJson))
            {
                var rNode = JsonNode.Parse(ex.RacesJson);
                if (rNode is JsonArray)
                {
                    cardObj["races"] = rNode;
                    raceWrite++;
                }
            }
            
            if (!string.IsNullOrWhiteSpace(ex.ClassesJson))
            {
                var rNode = JsonNode.Parse(ex.ClassesJson);
                if (rNode is JsonArray)
                {
                    cardObj["classes"] = rNode;
                    classWrite++;
                }
            }

            hit++;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

        var options = new JsonSerializerOptions
        {
            WriteIndented = pretty,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        File.WriteAllText(outputPath, root.ToJsonString(options));
        Console.WriteLine($"合并完成：命中 {hit}，写入 mechanics {mechWrite}，写入 races {raceWrite}，写入 classes {classWrite}，输出：{Path.GetFullPath(outputPath)}");
    }
}
