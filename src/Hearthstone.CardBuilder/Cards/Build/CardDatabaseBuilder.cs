using HearthstoneServer.Cards.Build.Model;

namespace HearthstoneServer.Cards.Build;

public static class CardDatabaseBuilder
{
    public static string BuildFinalDatabase(
        string cardDefsXmlPath,
        string newCardsArrayPath,
        string outputDirRes,
        bool pretty = false)
    {
        var basePath  = Path.Combine(outputDirRes, "cards_base_by_dbfid.json");
        var finalPath = Path.Combine(outputDirRes, "cards_final_by_dbfid.json");

        // 判断是否需要重建 final
        bool needRebuild =
            !File.Exists(finalPath) ||
            File.GetLastWriteTimeUtc(cardDefsXmlPath) > File.GetLastWriteTimeUtc(finalPath) ||
            File.GetLastWriteTimeUtc(newCardsArrayPath) > File.GetLastWriteTimeUtc(finalPath);

        if (!needRebuild)
        {
            Console.WriteLine("cards_final 已是最新：" + finalPath);
            return finalPath;
        }

        Directory.CreateDirectory(outputDirRes);

        // 1) XML -> base
        XmlCardDatabaseWriter.WriteBaseByDbfId(cardDefsXmlPath, basePath, pretty: pretty);
        Console.WriteLine("已生成 base：" + basePath);

        // 2) 读新 card.json extra（dbfId -> mechanics/races）
        Dictionary<string, ExtraTags> extra = ExtraTagsReader.LoadByDbfId(newCardsArrayPath);
        Console.WriteLine("extra 条目数：" + extra.Count);

        // 3) 合并到 tags -> final
        TagsMerger.MergeIntoTags(basePath, extra, finalPath, pretty: pretty);

        return finalPath;
    }
}