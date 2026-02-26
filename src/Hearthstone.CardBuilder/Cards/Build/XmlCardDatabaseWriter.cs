// 直接把你现有的 CardDefsStreamConverter.ConvertToJsonObjectByDbfId
// 拷到这个命名空间下即可
namespace HearthstoneServer.Cards.Build;

public static class XmlCardDatabaseWriter
{
    public static void WriteBaseByDbfId(string xmlPath, string jsonPath, bool pretty = false)
    {
        // 这里调用你现成的实现：ConvertToJsonObjectByDbfId(xmlPath, jsonPath)
        // 你可以把实现直接搬进来并让 pretty 控制 Indented
        CardDefsStreamConverter.ConvertToJsonObjectByDbfId(xmlPath, jsonPath);
    }
}