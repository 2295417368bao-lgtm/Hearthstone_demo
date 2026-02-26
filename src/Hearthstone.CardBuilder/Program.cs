using HearthstoneServer.Cards;
using HearthstoneServer.Cards.Build;

// ── 默认路径 ──
var resDir = args.Length > 0
    ? args[0]
    : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "data", "res"));

var cardDefsXml   = Paths.FindResFileIn(resDir, "CardDefs.xml");
var newCardsJson  = Paths.FindResFileIn(resDir, "cards.json");
var outputDir     = Path.Combine(resDir, "output");

Console.WriteLine($"CardDefs.xml : {cardDefsXml}");
Console.WriteLine($"cards.json   : {newCardsJson}");
Console.WriteLine($"Output dir   : {outputDir}");
Console.WriteLine();

var finalPath = CardDatabaseBuilder.BuildFinalDatabase(
    cardDefsXml, newCardsJson, outputDir, pretty: true);

Console.WriteLine();
Console.WriteLine($"✅ 完成！最终数据: {finalPath}");
