using System.Text.Json;
using Serilog;

namespace HearthstoneServer.LoadCardIntoDbf;

public static class CardDbLoader
{
	private static readonly ILogger _log = LogConfig.ForType(typeof(CardDbLoader));
	// mechanics: string -> Mechanic
	private static readonly Dictionary<string, Mechanic> MechanicMap =
		Enum.GetValues<Mechanic>().ToDictionary(m => m.ToString(), m => m, StringComparer.Ordinal);

	// races: string -> Race
	private static readonly Dictionary<string, Race> RaceMap = BuildRaceMap();

	// classes: string -> CardClass（支持 DEATH_KNIGHT / DEATHKNIGHT 等）
	private static readonly Dictionary<string, CardClass> CardClassMap = BuildCardClassMap();

	/// <summary>
	/// 加载卡牌数据，返回两个字典：
	/// 1. Key 为 DbfId (int)
	/// 2. Key 为 CardId (string)
	/// </summary>
	public static (Dictionary<int, Card> ByDbfId, Dictionary<string, Card> ByCardId) Load(string cardsFinalPath)
	{
		_log.Information("Loading cards from {Path}", cardsFinalPath);
		using var doc = JsonDocument.Parse(File.ReadAllBytes(cardsFinalPath));
		var root = doc.RootElement;

		var dictByDbfId = new Dictionary<int, Card>();
		// CardId 通常区分大小写（虽然一般是大写），使用 Ordinal 比较器
		var dictByCardId = new Dictionary<string, Card>(StringComparer.Ordinal);

		foreach (var prop in root.EnumerateObject())
		{
			if (!int.TryParse(prop.Name, out int dbfId)) continue;

			var cardObj = prop.Value;

			var cardId = cardObj.TryGetProperty("cardId", out var cidEl) ? (cidEl.GetString() ?? "") : "";
			var version = cardObj.TryGetProperty("version", out var verEl) && verEl.TryGetInt32(out var v) ? v : 0;

			// tags
			var (tagsInt, tagsLoc, tagsStr, unknownTagsInt) =
				cardObj.TryGetProperty("tags", out var tagsEl) && tagsEl.ValueKind == JsonValueKind.Object
					? ReadTags(tagsEl)
					: (new Dictionary<GameTag, int>(),
					   new Dictionary<GameTag, Dictionary<string, string>>(),
					   new Dictionary<GameTag, string>(),
					   new Dictionary<int, int>());

			// referencedTags
			var (refInt, unknownRefInt) =
				cardObj.TryGetProperty("referencedTags", out var refEl) && refEl.ValueKind == JsonValueKind.Object
					? ReadRefTags(refEl)
					: (new Dictionary<GameTag, int>(), new Dictionary<int, int>());

			var card = new Card
			{
				DbfId = dbfId,
				CardId = cardId,
				Version = version,

				Cost = GetInt(tagsInt, GameTag.Cost),
				Attack = GetInt(tagsInt, GameTag.Atk),
				Health = GetInt(tagsInt, GameTag.Health),
				Durability = GetInt(tagsInt, GameTag.Durability),

				CardClass = (CardClass)GetInt(tagsInt, GameTag.Class),
				CardSet = (CardSet)GetInt(tagsInt, GameTag.CardSet),
				CardType = (CardType)GetInt(tagsInt, GameTag.CardType),
				Faction = (Faction)GetInt(tagsInt, GameTag.Faction),
				Race = (Race)GetInt(tagsInt, GameTag.CardRace),
				Rarity = (Rarity)GetInt(tagsInt, GameTag.Rarity),
				SpellSchool = (SpellSchool)GetInt(tagsInt, GameTag.SpellSchool),
				FactionType = GetFactionType(tagsInt),
				QuestProgressTotal = GetInt(tagsInt, GameTag.QuestProgressTotal),
				Collectible = GetInt(tagsInt, GameTag.Collectible) == 1,

				Mechanics = ReadMechanicsFromCardObj(cardObj),
				Races = ReadRacesFromCardObj(cardObj),
				Classes = ReadClassesFromCardObj(cardObj),

				TagsInt = tagsInt,
				TagsLoc = tagsLoc,
				TagsStr = tagsStr,
				ReferencedTagsInt = refInt,
				UnknownTagsInt = unknownTagsInt,
				UnknownReferencedTagsInt = unknownRefInt
			};

			// 1. 存入 DbfId 字典
			dictByDbfId[dbfId] = card;

			// 2. 存入 CardId 字典 (防止空字符串作为 Key)
			if (!string.IsNullOrEmpty(cardId))
			{
				// 注意：如果数据源中有重复的 CardId，这里会保留最后一个读取到的（覆盖前者）
				dictByCardId[cardId] = card;
			}
		}

		_log.Information("Loaded {Count} cards ({ByIdCount} by CardId)",
			dictByDbfId.Count, dictByCardId.Count);
		return (dictByDbfId, dictByCardId);
	}

	// -------------------- 顶层 mechanics / races / classes --------------------

	private static Mechanic[] ReadMechanicsFromCardObj(JsonElement cardObj)
	{
		if (!cardObj.TryGetProperty("mechanics", out var arr) || arr.ValueKind != JsonValueKind.Array)
			return Array.Empty<Mechanic>();

		var list = new List<Mechanic>();
		foreach (var x in arr.EnumerateArray())
		{
			if (x.ValueKind != JsonValueKind.String) continue;
			var s = x.GetString();
			if (string.IsNullOrWhiteSpace(s)) continue;

			if (MechanicMap.TryGetValue(s, out var m))
				list.Add(m);
		}

		return list.Distinct().ToArray();
	}

	private static Race[] ReadRacesFromCardObj(JsonElement cardObj)
	{
		if (!cardObj.TryGetProperty("races", out var arr) || arr.ValueKind != JsonValueKind.Array)
			return Array.Empty<Race>();

		var list = new List<Race>();
		foreach (var x in arr.EnumerateArray())
		{
			if (x.ValueKind != JsonValueKind.String) continue;
			var s = x.GetString();
			if (string.IsNullOrWhiteSpace(s)) continue;

			var key = NormalizeKey(s);
			if (RaceMap.TryGetValue(key, out var race))
				list.Add(race);
		}

		return list.Distinct().ToArray();
	}

	private static CardClass[] ReadClassesFromCardObj(JsonElement cardObj)
	{
		if (!cardObj.TryGetProperty("classes", out var arr) || arr.ValueKind != JsonValueKind.Array)
			return Array.Empty<CardClass>();

		var list = new List<CardClass>();
		foreach (var x in arr.EnumerateArray())
		{
			if (x.ValueKind != JsonValueKind.String) continue;
			var s = x.GetString();
			if (string.IsNullOrWhiteSpace(s)) continue;

			var key = NormalizeKey(s);
			if (CardClassMap.TryGetValue(key, out var cc))
				list.Add(cc);
		}

		return list.Distinct().ToArray();
	}

	// -------------------- tags：分流为 int / loc / string / unknown-int --------------------

	private static (Dictionary<GameTag, int> tagsInt,
					Dictionary<GameTag, Dictionary<string, string>> tagsLoc,
					Dictionary<GameTag, string> tagsStr,
					Dictionary<int, int> unknownInt)
		ReadTags(JsonElement obj)
	{
		var tagsInt = new Dictionary<GameTag, int>();
		var tagsLoc = new Dictionary<GameTag, Dictionary<string, string>>();
		var tagsStr = new Dictionary<GameTag, string>();
		var unknownInt = new Dictionary<int, int>();

		foreach (var p in obj.EnumerateObject())
		{
			if (!int.TryParse(p.Name, out var enumId)) continue;

			var el = p.Value;
			bool known = Enum.IsDefined(typeof(GameTag), enumId);

			// 1) 数值 / bool -> int
			if (TryReadIntLike(el, out var iv))
			{
				if (known) tagsInt[(GameTag)enumId] = iv;
				else unknownInt[enumId] = iv;
				continue;
			}

			// 2) LocString（完整多语言 dict）——只给 known 存
			if (known && TryReadLocString(el, out var loc))
			{
				tagsLoc[(GameTag)enumId] = loc;
				continue;
			}

			// 3) 普通 string（可选）
			if (known && el.ValueKind == JsonValueKind.String)
			{
				var s = el.GetString();
				if (!string.IsNullOrEmpty(s))
					tagsStr[(GameTag)enumId] = s;
				continue;
			}

			// 4) 其它（数组/复杂对象）：这里为了省内存直接忽略
		}

		return (tagsInt, tagsLoc, tagsStr, unknownInt);
	}

	// referencedTags：全 int（并允许 unknown int）
	private static (Dictionary<GameTag, int> known, Dictionary<int, int> unknown) ReadRefTags(JsonElement obj)
	{
		var known = new Dictionary<GameTag, int>();
		var unknown = new Dictionary<int, int>();

		foreach (var p in obj.EnumerateObject())
		{
			if (!int.TryParse(p.Name, out var enumId)) continue;
			if (!TryReadIntLike(p.Value, out var iv)) continue;

			if (Enum.IsDefined(typeof(GameTag), enumId))
				known[(GameTag)enumId] = iv;
			else
				unknown[enumId] = iv;
		}

		return (known, unknown);
	}

	// -------------------- 值解析 helpers --------------------

	private static bool TryReadIntLike(JsonElement el, out int value)
	{
		value = 0;

		if (el.ValueKind == JsonValueKind.Number && el.TryGetInt32(out var n))
		{
			value = n;
			return true;
		}

		if (el.ValueKind == JsonValueKind.True)
		{
			value = 1;
			return true;
		}

		if (el.ValueKind == JsonValueKind.False)
		{
			value = 0;
			return true;
		}

		return false;
	}

	// 识别 LocString：要求“对象里全部是 string”，并且 key 看起来像 locale（enUS/zhCN/...）
	// 只保留 zhCN / enUS
	private static bool TryReadLocString(JsonElement el, out Dictionary<string, string> loc)
	{
		loc = null!;
		if (el.ValueKind != JsonValueKind.Object) return false;

		// 只要包含 zhCN 或 enUS 任意一个 string，就认为是 LocString
		bool has = false;
		var dict = new Dictionary<string, string>(StringComparer.Ordinal);

		if (el.TryGetProperty("zhCN", out var zh) && zh.ValueKind == JsonValueKind.String)
		{
			dict["zhCN"] = zh.GetString() ?? "";
			has = true;
		}

		if (el.TryGetProperty("enUS", out var en) && en.ValueKind == JsonValueKind.String)
		{
			dict["enUS"] = en.GetString() ?? "";
			has = true;
		}

		if (!has) return false;

		loc = dict;
		return true;
	}


	private static int GetInt(Dictionary<GameTag, int> tags, GameTag tag, int def = 0)
		=> tags.TryGetValue(tag, out var v) ? v : def;

	private static FactionType GetFactionType(Dictionary<GameTag, int> tags)
	{
		if (tags.ContainsKey(GameTag.GrimyGoons)) return FactionType.GrimyGoons;
		if (tags.ContainsKey(GameTag.JadeLotus)) return FactionType.JadeLotus;
		if (tags.ContainsKey(GameTag.Kabal)) return FactionType.Kabal;
		if (tags.ContainsKey(GameTag.Zerg)) return FactionType.Zerg;
		if (tags.ContainsKey(GameTag.Terran)) return FactionType.Terran;
		if (tags.ContainsKey(GameTag.Protoss)) return FactionType.Protoss;
		return FactionType.Generic;
	}

	// -------------------- 映射表 --------------------

	private static Dictionary<string, Race> BuildRaceMap()
	{
		var map = new Dictionary<string, Race>(StringComparer.Ordinal);

		foreach (var r in Enum.GetValues<Race>())
			map[NormalizeKey(r.ToString())] = r;

		map[NormalizeKey("MECH")] = Race.Mechanical;
		map[NormalizeKey("MECHANICAL")] = Race.Mechanical;

		return map;
	}

	private static Dictionary<string, CardClass> BuildCardClassMap()
	{
		var map = new Dictionary<string, CardClass>(StringComparer.Ordinal);
		foreach (var c in Enum.GetValues<CardClass>())
			map[NormalizeKey(c.ToString())] = c;

		map[NormalizeKey("DEATH_KNIGHT")] = CardClass.Deathknight;
		map[NormalizeKey("DEMON_HUNTER")] = CardClass.Demonhunter;

		return map;
	}

	private static string NormalizeKey(string s)
	{
		Span<char> buf = stackalloc char[s.Length];
		int j = 0;
		foreach (var ch in s)
			if (char.IsLetterOrDigit(ch))
				buf[j++] = char.ToUpperInvariant(ch);
		return new string(buf[..j]);
	}
}
