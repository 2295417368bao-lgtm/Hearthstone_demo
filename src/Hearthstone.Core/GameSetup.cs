using HearthstoneServer.Entities;
using HearthstoneServer.LoadCardIntoDbf;

namespace HearthstoneServer;

/// <summary>
/// Game creation utilities: deck data, hero IDs, and factory methods.
/// Moved from GameHub.GameSession and Console.DeckBuilder.
/// </summary>
public static class GameSetup
{
	// ── Hero IDs ──

	private static readonly Dictionary<string, string> HeroIds = new()
	{
		["Mage"]    = "HERO_08",
		["Hunter"]  = "HERO_05",
		["Warrior"] = "HERO_01",
		["Priest"]  = "HERO_09",
		["Druid"]   = "HERO_06",
		["Warlock"] = "HERO_07",
		["Paladin"] = "HERO_04",
		["Rogue"]   = "HERO_03",
		["Shaman"]  = "HERO_02",
	};

	public static string[] AvailableClasses => new[] { "Mage", "Hunter", "Warrior", "Warlock", "Paladin" };

	public static string GetHeroId(string className) =>
		HeroIds.TryGetValue(className, out var id) ? id : "HERO_08";

	// ── Card Database ──

	public static Dictionary<string, Card> GetCardDb() => CardDatabase.ByCardId;

	// ── Decks (hardcoded classic) ──

	public static List<string> GetDeck(string cls) => cls switch
	{
		"Mage" => new()
		{
			// 法术 (14)
			"CS2_029","CS2_029", // 火球术 (4费, 6伤)
			"CS2_024","CS2_024", // 寒冰箭 (2费, 3伤+冻结)
			"CS2_023","CS2_023", // 奥术智慧 (3费, 抽2)
			"CS2_025","CS2_025", // 奥术飞弹 (1费, 3枚1伤)
			"CS2_032",           // 烈焰风暴 (7费, AOE4伤)
			"CS2_026",           // 冰霜新星 (3费, 冻结敌方)
			"CS2_022",           // 变形术 (4费, 变羊)
			"EX1_277",           // 奥术飞弹 (1费, 随机3伤)
			"CS2_027",           // 镜像 (1费, 2个0/2嘲讽)
			"CS2_028",           // 暴风雪 (6费, AOE2伤+冻结)
			// 随从 (10)
			"NEW1_012","NEW1_012", // 法力龙鹰 (1费, 2/1 法伤+1)
			"EX1_608",             // 巫师学徒 (2费, 法术减1费)
			"EX1_274",             // 大法师安东尼达斯
			// 中立 (6)
			"EX1_015","EX1_015", // 工程师学徒 (2费, 战吼抽牌)
			"EX1_593","EX1_593", // 夜刃刺客 (5费, 战吼3伤)
			"CS2_189","CS2_189", // 精灵弓箭手 (1费, 战吼1伤)
			"EX1_050",           // 冷光先知 (3费, 抽2牌)
			"CS2_147",           // 侏儒发明家 (4费, 抽牌)
			"EX1_096",           // 战利品贮藏者 (2费, 亡语抽牌)
			"EX1_012",           // 血法师萨尔诺斯 (2费, 法伤+1, 亡语抽牌)
		},
		"Hunter" => new()
		{
			// 法术 (12)
			"DS1_185","DS1_185", // 奥术射击 (1费, 2伤)
			"EX1_539","EX1_539", // 杀戮命令 (3费, 3/5伤)
			"DS1_183","DS1_183", // 多重射击 (4费, 2随机3伤)
			"EX1_538","EX1_538", // 关门放狗 (3费, 召唤猎犬)
			"CS2_084","CS2_084", // 猎人印记 (1费, 生命变1)
			"EX1_617",           // 致命射击 (3费, 消灭随机敌随从)
			// 奥秘 (4)
			"EX1_610","EX1_610", // 爆炸陷阱 (2费, AOE2伤)
			"EX1_609","EX1_609", // 狙击 (2费, 打出随从4伤)
			// 随从 (8)
			"DS1_175","DS1_175", // 森林狼 (1费, 其他野兽+1攻)
			"DS1_178",           // 冻原犀牛 (5费, 野兽冲锋)
			"EX1_534",           // 萨凡纳雄狮 (6费, 亡语召2个2/2)
			"EX1_531",           // 腐肉食尸鬼 (3费, 友方野兽死+2/+1)
			"NEW1_031",          // 动物伙伴 (3费, 随机伙伴)
			// 中立 (6)
			"CS2_189","CS2_189", // 精灵弓箭手 (1费, 战吼1伤)
			"EX1_593","EX1_593", // 夜刃刺客 (5费, 战吼3伤)
			"EX1_029",           // 麻风侏儒 (1费, 亡语2伤英雄)
			"EX1_096",           // 战利品贮藏者 (2费, 亡语抽牌)
		},
		"Warrior" => new()
		{
			// 法术 (12)
			"CS2_108","CS2_108", // 斩杀 (3费, 消灭受伤随从)
			"EX1_400","EX1_400", // 旋风斩 (1费, AOE1伤)
			"EX1_606","EX1_606", // 盾牌格挡 (3费, +5甲抽牌)
			"CS2_105","CS2_105", // 英勇打击 (2费, +4攻)
			"EX1_410",           // 升级！(1费, 装备1/3或+1/+1)
			"EX1_407","EX1_407", // 蛮力猛击 (2费, 消灭受伤随从)
			"CS2_114",           // 顺劈斩 (2费, 2伤2个随从)
			"EX1_392",           // 战斗怒火 (2费, 每受伤随从抽牌)
			"NEW1_036",          // 指挥怒吼 (2费, 所有友方+1攻)
			// 随从 (10)
			"EX1_402",           // 铠甲匠 (1费, 受伤+1甲)
			"EX1_604",           // 暴乱狂战士 (3费, 受伤+1攻)
			"EX1_603","EX1_603", // 恐怖的奴隶主 (5费, 受伤召唤复制)
			"EX1_414",           // 格鲁尔的护卫 (3费, 受损+3攻)
			"EX1_391",           // 砰砰博士
			// 中立 (8)
			"EX1_593","EX1_593", // 夜刃刺客 (5费, 战吼3伤)
			"CS2_189","CS2_189", // 精灵弓箭手 (1费, 战吼1伤)
			"EX1_096",           // 战利品贮藏者 (2费, 亡语抽牌)
			"EX1_399",           // 暴怒的狗头人 (4费, 受伤+1攻)
			"EX1_029",           // 麻风侏儒 (1费, 亡语2伤)
			"EX1_110",           // 收割机 (4费, 亡语召唤2/1)
		},
		"Warlock" => new()
		{
			// 法术 (12)
			"EX1_302","EX1_302", // 灵魂之火 (1费, 4伤弃1牌)
			"CS2_062",           // 地狱烈焰 (4费, AOE3伤)
			"EX1_312",           // 扭曲虚空 (8费, 消灭全部)
			"EX1_308","EX1_308", // 灵魂虹吸 (5费, 消灭+恢复3)
			"EX1_319","EX1_319", // 烈焰小鬼 (1费, 弃1牌)
			"EX1_317",           // 感知恶魔 (3费, 抽2牌)
			"CS2_063","CS2_063", // 腐烂 (1费, 造成1伤)
			"CS2_057","CS2_057", // 暗影箭 (3费, 4伤随从)
			// 随从 (12)
			"CS2_065","CS2_065", // 虚空行者 (1费, 1/3嘲讽)
			"EX1_306",           // 成功的术士
			"EX1_596","EX1_596", // 恐惧地狱火 (6费, 6/6)
			// 中立 (6)
			"EX1_593","EX1_593", // 夜刃刺客
			"CS2_189","CS2_189", // 精灵弓箭手
			"EX1_015","EX1_015", // 工程师学徒
			"EX1_029","EX1_029", // 麻风侏儒
			"EX1_096","EX1_096", // 战利品贮藏者
			"EX1_556",           // 收割机
		},
		"Paladin" => new()
		{
			// 法术 (10)
			"CS2_093","CS2_093", // 奉献 (4费, AOE2伤)
			"EX1_371","EX1_371", // 力量祝福 (1费, +3攻)
			"EX1_360",           // 谦逊 (1费, 攻击变1)
			"EX1_354","EX1_354", // 奥尔德城卫兵
			"CS2_092","CS2_092", // 保佑 (3费, +2/+2)
			"EX1_363",           // 保护之手 (1费, 圣盾)
			// 随从 (14)
			"CS2_088","CS2_088", // 护卫 (2费, 2/2嘲讽圣盾)
			"EX1_382","EX1_382", // 奥尔德城英雄
			"CS2_097","CS2_097", // 真银圣剑 (4费, 4/2武器)
			// 中立
			"EX1_593","EX1_593", // 夜刃刺客
			"CS2_189","CS2_189", // 精灵弓箭手
			"EX1_015","EX1_015", // 工程师学徒
			"EX1_029","EX1_029", // 麻风侏儒
			"EX1_110",           // 收割机
			"CS2_147",           // 侏儒发明家
		},
		_ => GetDeck("Mage"),
	};

	// ── Factory Methods ──

	/// <summary>Create an AI game. Returns a GameController ready for mulligan.</summary>
	public static GameController CreateAIGame(string playerClass = "Mage", string aiClass = "Hunter", string[]? customDeck = null)
	{
		var (game, p1, p2) = CreateGameInternal(
			playerClass, customDeck?.ToList(),
			aiClass, null,
			"玩家", "对手");
		return new GameController(game, p1, p2);
	}

	/// <summary>Create a PvP game. Returns a GameController ready for mulligan.</summary>
	public static GameController CreatePvPGame(
		string p1Class, string[]? p1Deck,
		string p2Class, string[]? p2Deck)
	{
		var (game, p1, p2) = CreateGameInternal(
			p1Class, p1Deck?.ToList(),
			p2Class, p2Deck?.ToList(),
			"玩家1", "玩家2");
		return new GameController(game, p1, p2);
	}

	/// <summary>
	/// Internal: create Game, players, setup, initial draw.
	/// Uses Game.InitialDraw() instead of manual for loops.
	/// </summary>
	private static (Game game, Player p1, Player p2) CreateGameInternal(
		string p1Class, List<string>? p1CustomDeck,
		string p2Class, List<string>? p2CustomDeck,
		string p1Name, string p2Name)
	{
		// Initialize card scripts
		Dsl.CardScriptRegistry.Instance.Initialize(typeof(Game).Assembly);

		var p1 = new Player(p1Name)
		{
			StartingHero = GetHeroId(p1Class),
			StartingDeck = p1CustomDeck != null && p1CustomDeck.Count > 0
				? p1CustomDeck
				: GetDeck(p1Class),
		};
		var p2 = new Player(p2Name)
		{
			StartingHero = GetHeroId(p2Class),
			StartingDeck = p2CustomDeck != null && p2CustomDeck.Count > 0
				? p2CustomDeck
				: GetDeck(p2Class),
		};

		var cardDb = GetCardDb();
		var game = new Game(p1, p2);
		game.CardDb = cardDb;

		p1.PrepareForGame();
		p2.PrepareForGame();
		game.Setup();
		game.InitialDraw();  // 优化点4: 复用 Game.InitialDraw()

		return (game, p1, p2);
	}
}
