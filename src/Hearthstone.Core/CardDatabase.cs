using HearthstoneServer.LoadCardIntoDbf;

namespace HearthstoneServer;

/// <summary>
/// 全局卡牌数据库。服务器启动时调用 Initialize() 加载一次，
/// 之后所有地方通过 CardDatabase.ByCardId / CardDatabase.ByDbfId 访问。
/// </summary>
public static class CardDatabase
{
    private static Dictionary<string, Card>? _byCardId;
    private static Dictionary<int, Card>? _byDbfId;

    /// <summary>按 CardId 索引的卡牌字典。</summary>
    public static Dictionary<string, Card> ByCardId =>
        _byCardId ?? throw new InvalidOperationException("CardDatabase not initialized. Call Initialize() first.");

    /// <summary>按 DbfId 索引的卡牌字典。</summary>
    public static Dictionary<int, Card> ByDbfId =>
        _byDbfId ?? throw new InvalidOperationException("CardDatabase not initialized. Call Initialize() first.");

    public static bool IsInitialized => _byCardId != null;

    /// <summary>
    /// 加载卡牌数据库。应在服务器启动时调用一次。
    /// </summary>
    public static void Initialize(string cardsPath)
    {
        if (_byCardId != null) return; // 已初始化

        var (byDbfId, byCardId) = CardDbLoader.Load(cardsPath);
        _byCardId = byCardId;
        _byDbfId = byDbfId;
    }

    /// <summary>
    /// 使用默认路径初始化。
    /// </summary>
    public static void Initialize()
    {
        var cardsPath = Path.Combine(AppContext.BaseDirectory, "data", "cards_dbfid.json");
        if (!File.Exists(cardsPath))
            cardsPath = @"d:\hs\Hearthstone.CSharp\data\cards_dbfid.json";
        Initialize(cardsPath);
    }
}
