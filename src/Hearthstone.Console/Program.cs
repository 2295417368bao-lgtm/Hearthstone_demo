// ═══════════════════════════════════════════════
// 炉石传说 — 控制台对战 (Human vs AI)
// ═══════════════════════════════════════════════
using HearthstoneServer;
using HearthstoneServer.Console;
using HearthstoneServer.Dsl;
using HearthstoneServer.Entities;

Console.OutputEncoding = System.Text.Encoding.UTF8;
Console.Title = "炉石传说 — 控制台版";

// ── 1. 加载卡牌数据库 ──
Console.ForegroundColor = ConsoleColor.DarkGray;
Console.Write("  加载卡牌数据库...");
var cardDb = GameSetup.GetCardDb();
Console.WriteLine($" ✓ 已加载 {cardDb.Count} 张卡牌");
CardScriptRegistry.Instance.Initialize();
Console.WriteLine("  ✓ 脚本引擎已就绪");
Console.ResetColor();

// ── 2. 玩家选择职业 ──
Console.WriteLine();
Console.ForegroundColor = ConsoleColor.Cyan;
Console.WriteLine("  ═══════════════════════════════");
Console.WriteLine("  🎮 炉石传说 — 控制台版");
Console.WriteLine("  ═══════════════════════════════");
Console.ResetColor();
Console.WriteLine();
Console.ForegroundColor = ConsoleColor.Yellow;
Console.WriteLine("  选择你的职业:");
Console.ResetColor();

var classes = GameSetup.AvailableClasses;
for (int i = 0; i < classes.Length; i++)
{
	string cn = classes[i] switch
	{
		"Mage" => "法师 (Mage)",
		"Hunter" => "猎人 (Hunter)",
		"Warrior" => "战士 (Warrior)",
		"Warlock" => "术士 (Warlock)",
		"Paladin" => "圣骑士 (Paladin)",
		_ => classes[i],
	};
	Console.WriteLine($"    [{i + 1}] {cn}");
}

string playerClass;
while (true)
{
	Console.Write("  > ");
	var input = Console.ReadLine()?.Trim();
	if (int.TryParse(input, out int idx) && idx >= 1 && idx <= classes.Length)
	{
		playerClass = classes[idx - 1];
		break;
	}
	Console.ForegroundColor = ConsoleColor.Red;
	Console.WriteLine("  请输入有效数字");
	Console.ResetColor();
}

// AI 随机选一个不同的职业
var rng = new Random();
string aiClass;
do { aiClass = classes[rng.Next(classes.Length)]; } while (aiClass == playerClass && classes.Length > 1);

Console.ForegroundColor = ConsoleColor.Green;
Console.WriteLine($"\n  你选择了: {playerClass}");
Console.ForegroundColor = ConsoleColor.Red;
Console.WriteLine($"  AI 选择了: {aiClass}");
Console.ResetColor();
Console.WriteLine("\n  按 Enter 开始游戏...");
Console.ReadLine();

// ── 3. 创建游戏 ──
var ctrl = GameSetup.CreateAIGame(playerClass, aiClass);
var game = ctrl.Game;
var humanPlayer = ctrl.Player1;
var aiPlayer = ctrl.Player2;

var ai = new HearthstoneServer.AI.SimpleAI(game, aiPlayer);
ai.OnAction = ConsoleRenderer.ShowAIAction;

// ── 4. 游戏主循环 ──
// 先手：玩家
game.BeginTurn(humanPlayer);

while (game.State != GameState.Complete)
{
	ConsoleRenderer.RenderBoard(game, humanPlayer);

	if (game.CurrentPlayer == humanPlayer)
	{
		// ── 玩家回合 ──
		HumanTurn(game, humanPlayer);
	}
	else
	{
		// ── AI 回合 ──
		ConsoleRenderer.ShowInfo($"AI 的回合 (回合 {game.Turn})");
		ai.PlayTurn();
		ConsoleRenderer.RenderBoard(game, humanPlayer);
		Thread.Sleep(500);

		if (game.State == GameState.Complete) break;
		game.EndTurn();
	}
}

// ── 5. 游戏结束 ──
ConsoleRenderer.RenderBoard(game, humanPlayer);
ConsoleRenderer.ShowGameOver(game, humanPlayer);
Console.WriteLine("\n  按 Enter 退出...");
Console.ReadLine();


// ══════════════════════════════════════════
//                 方法定义
// ══════════════════════════════════════════

void HumanTurn(Game game, Player player)
{
	while (true)
	{
		if (game.State == GameState.Complete) return;

		ConsoleRenderer.RenderMenu(player);
		var key = Console.ReadLine()?.Trim().ToUpper();

		switch (key)
		{
			case "P":
				HandlePlayCard(game, player);
				ConsoleRenderer.RenderBoard(game, player);
				break;
			case "A":
				HandleAttack(game, player);
				ConsoleRenderer.RenderBoard(game, player);
				break;
			case "E":
				game.EndTurn(); // → AI turn
				return;
			case "Q":
				Console.ForegroundColor = ConsoleColor.Yellow;
				Console.WriteLine("\n  确定退出? (Y/N)");
				Console.ResetColor();
				Console.Write("  > ");
				if (Console.ReadLine()?.Trim().ToUpper() == "Y")
					Environment.Exit(0);
				ConsoleRenderer.RenderBoard(game, player);
				break;
			default:
				ConsoleRenderer.ShowError("无效操作，请输入 P/A/E/Q");
				break;
		}
	}
}

void HandlePlayCard(Game game, Player player)
{
	if (player.Hand.Count == 0)
	{
		ConsoleRenderer.ShowError("没有手牌");
		return;
	}

	Console.ForegroundColor = ConsoleColor.Yellow;
	Console.WriteLine("\n  选择要打出的手牌编号 (0=取消):");
	Console.ResetColor();
	Console.Write("  > ");
	var input = Console.ReadLine()?.Trim();
	if (!int.TryParse(input, out int idx) || idx < 0 || idx > player.Hand.Count)
	{
		ConsoleRenderer.ShowError("无效编号");
		return;
	}
	if (idx == 0) return;

	var card = player.Hand[idx - 1] as BuffableEntity;
	if (card == null)
	{
		ConsoleRenderer.ShowError("无法识别的卡牌");
		return;
	}

	if (player.Mana < card.Cost)
	{
		ConsoleRenderer.ShowError($"法力不足 (需要 {card.Cost}, 当前 {player.Mana})");
		return;
	}

	if (card is Minion && player.Field.Count >= 7)
	{
		ConsoleRenderer.ShowError("场面已满 (最多7个随从)");
		return;
	}

	// 检查是否需要目标
	Character? target = null;
	if (card.RequiresTarget)
	{
		target = SelectTarget(game, player);
		if (target == null)
		{
			ConsoleRenderer.ShowInfo("已取消");
			return;
		}
	}

	try
	{
		string cardName = ConsoleRenderer.GetCardName(card);
		game.PlayCard(card, target);
		ConsoleRenderer.ShowInfo($"打出: {cardName}");
	}
	catch (Exception ex)
	{
		ConsoleRenderer.ShowError($"出牌失败: {ex.Message}");
	}
}

void HandleAttack(Game game, Player player)
{
	var attackers = player.Field.OfType<Minion>().Where(m => m.CanAttack()).ToList();
	if (attackers.Count == 0)
	{
		ConsoleRenderer.ShowError("没有可攻击的随从");
		return;
	}

	Console.ForegroundColor = ConsoleColor.Yellow;
	Console.WriteLine("\n  选择攻击者 (场面编号, 0=取消):");
	Console.ResetColor();

	for (int i = 0; i < player.Field.Count; i++)
	{
		var m = player.Field[i] as Minion;
		if (m == null) continue;
		bool canAtk = m.CanAttack();
		Console.ForegroundColor = canAtk ? ConsoleColor.Green : ConsoleColor.DarkGray;
		string name = ConsoleRenderer.GetCardName(m);
		Console.WriteLine($"    [{i + 1}] {name} {m.Atk}/{m.Health} {(canAtk ? "⚔" : "💤")}");
	}
	Console.ResetColor();
	Console.Write("  > ");

	var input = Console.ReadLine()?.Trim();
	if (!int.TryParse(input, out int atkIdx) || atkIdx < 0 || atkIdx > player.Field.Count)
	{
		ConsoleRenderer.ShowError("无效编号");
		return;
	}
	if (atkIdx == 0) return;

	var attacker = player.Field[atkIdx - 1] as Minion;
	if (attacker == null || !attacker.CanAttack())
	{
		ConsoleRenderer.ShowError("该随从无法攻击");
		return;
	}

	// 选择目标
	var opponent = player.Opponent!;
	Console.ForegroundColor = ConsoleColor.Yellow;
	Console.WriteLine("\n  选择目标 (0=取消):");
	Console.ResetColor();

	var targets = new List<Character>();
	// 英雄
	if (opponent.Hero != null && !opponent.Hero.Dead)
	{
		targets.Add(opponent.Hero);
		Console.ForegroundColor = ConsoleColor.Red;
		Console.WriteLine($"    [1] 英雄 ❤{opponent.Hero.Health}");
	}
	// 敌方随从
	for (int i = 0; i < opponent.Field.Count; i++)
	{
		var m = opponent.Field[i] as Minion;
		if (m == null || m.Dead) continue;
		targets.Add(m);
		string name = ConsoleRenderer.GetCardName(m);
		Console.ForegroundColor = m.Taunt ? ConsoleColor.Yellow : ConsoleColor.Red;
		Console.WriteLine($"    [{targets.Count}] {name} {m.Atk}/{m.Health}{(m.Taunt ? " 🛡嘲讽" : "")}");
	}
	Console.ResetColor();
	Console.Write("  > ");

	var tgtInput = Console.ReadLine()?.Trim();
	if (!int.TryParse(tgtInput, out int tgtIdx) || tgtIdx < 0 || tgtIdx > targets.Count)
	{
		ConsoleRenderer.ShowError("无效编号");
		return;
	}
	if (tgtIdx == 0) return;

	// 检查嘲讽
	var hasTaunts = opponent.Field.OfType<Minion>().Any(m => m.Taunt && !m.Dead);
	var chosenTarget = targets[tgtIdx - 1];
	if (hasTaunts && chosenTarget is not Minion { Taunt: true })
	{
		ConsoleRenderer.ShowError("必须先攻击嘲讽随从!");
		return;
	}

	try
	{
		string atkName = ConsoleRenderer.GetCardName(attacker);
		string defName = ConsoleRenderer.GetCardName(chosenTarget);
		game.Attack(attacker, chosenTarget);
		game.ProcessDeaths();
		ConsoleRenderer.ShowInfo($"{atkName} → {defName}");
	}
	catch (Exception ex)
	{
		ConsoleRenderer.ShowError($"攻击失败: {ex.Message}");
	}
}

Character? SelectTarget(Game game, Player player)
{
	var opponent = player.Opponent!;

	Console.ForegroundColor = ConsoleColor.Yellow;
	Console.WriteLine("\n  选择目标 (0=取消):");
	Console.ResetColor();

	var targets = new List<Character>();

	// 敌方英雄
	if (opponent.Hero != null && !opponent.Hero.Dead)
	{
		targets.Add(opponent.Hero);
		Console.WriteLine($"    [{targets.Count}] 敌方英雄 ❤{opponent.Hero.Health}");
	}
	// 敌方随从
	foreach (var m in opponent.Field.OfType<Minion>().Where(m => !m.Dead))
	{
		targets.Add(m);
		string name = ConsoleRenderer.GetCardName(m);
		Console.WriteLine($"    [{targets.Count}] {name} {m.Atk}/{m.Health}");
	}
	// 友方随从
	foreach (var m in player.Field.OfType<Minion>().Where(m => !m.Dead))
	{
		targets.Add(m);
		string name = ConsoleRenderer.GetCardName(m);
		Console.ForegroundColor = ConsoleColor.Green;
		Console.WriteLine($"    [{targets.Count}] (我方) {name} {m.Atk}/{m.Health}");
	}
	Console.ResetColor();
	Console.Write("  > ");

	var input = Console.ReadLine()?.Trim();
	if (!int.TryParse(input, out int idx) || idx < 1 || idx > targets.Count)
		return null;

	return targets[idx - 1];
}
