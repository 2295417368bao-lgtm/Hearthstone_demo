using System.Text.Encodings.Web;
using System.Text.Json;
using System.Xml;

namespace HearthstoneServer.Cards.Build;
public static class CardDefsStreamConverter
{
    // 输出：{ "2566": { cardId:"AT_014", dbfId:2566, ... }, ... }
    public static void ConvertToJsonObjectByDbfId(string xmlPath, string jsonPath)
    {
        var xmlSettings = new XmlReaderSettings
        {
            IgnoreComments = true,
            IgnoreProcessingInstructions = true,
            DtdProcessing = DtdProcessing.Ignore,
            XmlResolver = null
        };

        using var xml = XmlReader.Create(xmlPath, xmlSettings);

        var jsonOptions = new JsonWriterOptions
        {
            Indented = true, // 想小体积就 false
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        Directory.CreateDirectory(Path.GetDirectoryName(jsonPath)!);

        using var fs = File.Create(jsonPath);
        using var jw = new Utf8JsonWriter(fs, jsonOptions);

        jw.WriteStartObject(); // { "dbfId": { ... }, ... }

        while (xml.Read())
        {
            if (xml.NodeType == XmlNodeType.Element && xml.Name == "Entity")
            {
                WriteOneEntityByDbfId(xml, jw);
            }
        }

        jw.WriteEndObject();
        jw.Flush();
    }

    private static void WriteOneEntityByDbfId(XmlReader xml, Utf8JsonWriter jw)
    {
        var cardId = xml.GetAttribute("CardID") ?? "";
        int dbfId = ParseInt(xml.GetAttribute("ID"));
        int version = ParseInt(xml.GetAttribute("version"));

        // dbfId=0 通常是无效/占位，跳过
        if (dbfId <= 0)
        {
            xml.Skip();
            return;
        }

        var tags = new Dictionary<string, object?>(StringComparer.Ordinal);
        var referencedTags = new Dictionary<string, object?>(StringComparer.Ordinal);

        if (!xml.IsEmptyElement)
        {
            int entityDepth = xml.Depth;

            while (xml.Read())
            {
                if (xml.NodeType == XmlNodeType.EndElement && xml.Name == "Entity" && xml.Depth == entityDepth)
                    break;

                if (xml.NodeType != XmlNodeType.Element) continue;

                if (xml.Name == "Tag") ReadTagIntoDict(xml, tags);
                else if (xml.Name == "ReferencedTag") ReadReferencedTagIntoDict(xml, referencedTags);
            }
        }

        // ✅ key 改成 dbfId 字符串
        jw.WritePropertyName(dbfId.ToString());
        jw.WriteStartObject();

        jw.WriteString("cardId", cardId);
        jw.WriteNumber("dbfId", dbfId);
        jw.WriteNumber("version", version);

        jw.WritePropertyName("tags");
        WriteDict(jw, tags);

        jw.WritePropertyName("referencedTags");
        WriteDict(jw, referencedTags);

        jw.WriteEndObject();
    }

    private static void ReadTagIntoDict(XmlReader xml, Dictionary<string, object?> dict)
    {
        var enumIdStr = xml.GetAttribute("enumID");
        if (string.IsNullOrWhiteSpace(enumIdStr))
        {
            xml.Skip();
            return;
        }

        var type = xml.GetAttribute("type") ?? "";
        var valueAttr = xml.GetAttribute("value");

        if (type == "LocString")
        {
            var loc = new Dictionary<string, string>(StringComparer.Ordinal);

            if (!xml.IsEmptyElement)
            {
                int tagDepth = xml.Depth;
                while (xml.Read())
                {
                    if (xml.NodeType == XmlNodeType.EndElement && xml.Name == "Tag" && xml.Depth == tagDepth)
                        break;

                    if (xml.NodeType == XmlNodeType.Element)
                    {
                        var lang = xml.Name;
                        var text = xml.ReadElementContentAsString();
                        if (!string.IsNullOrEmpty(text))
                            loc[lang] = text;
                    }
                }
            }

            dict[enumIdStr] = loc;
            return;
        }
        bool treatAsInt = type == "Int" || type == "Card";
        if (!string.IsNullOrEmpty(valueAttr))
        {
            dict[enumIdStr] = treatAsInt && int.TryParse(valueAttr, out var n) ? n : valueAttr;
            xml.Skip();
            return;
        }

        var inner = xml.ReadElementContentAsString();
        dict[enumIdStr] = type == "Int" && int.TryParse(inner, out var n2) ? n2 : inner;
    }

    private static void ReadReferencedTagIntoDict(XmlReader xml, Dictionary<string, object?> dict)
    {
        var enumIdStr = xml.GetAttribute("enumID");
        if (string.IsNullOrWhiteSpace(enumIdStr))
        {
            xml.Skip();
            return;
        }

        var type = xml.GetAttribute("type") ?? "";
        var valueAttr = xml.GetAttribute("value");

        if (!string.IsNullOrEmpty(valueAttr))
        {
            dict[enumIdStr] = type == "Int" && int.TryParse(valueAttr, out var n) ? n : valueAttr;
            xml.Skip();
            return;
        }

        var inner = xml.ReadElementContentAsString();
        dict[enumIdStr] = type == "Int" && int.TryParse(inner, out var n2) ? n2 : inner;
    }

    private static void WriteDict(Utf8JsonWriter jw, Dictionary<string, object?> dict)
    {
        jw.WriteStartObject();
        foreach (var (k, v) in dict)
        {
            jw.WritePropertyName(k);
            switch (v)
            {
                case null: jw.WriteNullValue(); break;
                case int n: jw.WriteNumberValue(n); break;
                case string s: jw.WriteStringValue(s); break;
                case Dictionary<string, string> loc:
                    jw.WriteStartObject();
                    foreach (var (lang, text) in loc) jw.WriteString(lang, text);
                    jw.WriteEndObject();
                    break;
                default:
                    jw.WriteStringValue(v.ToString());
                    break;
            }
        }
        jw.WriteEndObject();
    }

    private static int ParseInt(string? s) => int.TryParse(s, out var n) ? n : 0;
}
