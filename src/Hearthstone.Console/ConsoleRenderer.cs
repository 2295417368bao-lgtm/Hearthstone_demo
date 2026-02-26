namespace HearthstoneServer.Console;

using HearthstoneServer.Entities;

/// <summary>
/// 控制台棋盘渲染器 — 用彩色文字显示游戏状态
/// </summary>
public static class ConsoleRenderer
{
	// ── 颜色常量 ──
	static void SetColor(ConsoleColor fg) => System.Console.ForegroundColor = fg;
	static void ResetColor() => System.Console.ResetColor();

	/// <summary>
	/// 渲染完整棋盘
	/// </summary>
	public static void RenderBoard(Game game, Player humanPlayer)
	{
		var opponent = humanPlayer.Opponent!;
		System.Console.Clear();

		PrintSeparator("═", ConsoleColor.DarkCyan);
		PrintHeader("🎮 炉石传说 — 控制台版", ConsoleColor.Cyan);
		PrintSeparator("═", ConsoleColor.DarkCyan);

		// 对手信息
		System.Console.WriteLine();
		PrintHeader("【对手】", ConsoleColor.Red);
		RenderHero(opponent, ConsoleColor.Red);
		RenderField(opponent, "对手场面", ConsoleColor.Red);

		// 分隔线
		System.Console.WriteLine();
		PrintSeparator("─", ConsoleColor.DarkGray);
		System.Console.WriteLine();

		// 我方信息
		RenderField(humanPlayer, "我方场面", ConsoleColor.Green);
		RenderHero(humanPlayer, ConsoleColor.Green);
		PrintHeader("【我方】", ConsoleColor.Green);
		System.Console.WriteLine();

		// 手牌
		RenderHand(humanPlayer);

		// 回合信息
		PrintSeparator("─", ConsoleColor.DarkGray);
		SetColor(ConsoleColor.Yellow);
		System.Console.WriteLine($"  回合 {game.Turn}  |  {game.CurrentPlayer?.Name} 的回合");
		ResetColor();
		PrintSeparator("═", ConsoleColor.DarkCyan);
	}

	/// <summary>
	/// 渲染英雄信息
	/// </summary>
	static void RenderHero(Player player, ConsoleColor color)
	{
		var hero = player.Hero;
		if (hero == null) return;

		SetColor(color);
		string heroName = hero.CardData?.CardId ?? "Hero";
		string className = GetClassName(heroName);
		int hp = hero.Health;
		int armor = hero.Armor;
		int mana = player.Mana;
		int maxMana = player.MaxMana;
		int handCount = player.Hand.Count;
		int deckCount = player.Deck.Count;

		System.Console.Write($"  [{className}] ");
		SetColor(hp <= 10 ? ConsoleColor.Red : ConsoleColor.White);
		System.Console.Write($"❤{hp}");
		if (armor > 0)
		{
			SetColor(ConsoleColor.Cyan);
			System.Console.Write($" 🛡{armor}");
		}
		SetColor(ConsoleColor.Blue);
		System.Console.Write($"  💧{mana}/{maxMana}");
		SetColor(ConsoleColor.DarkGray);
		System.Console.Write($"  🃏{handCount}  📚{deckCount}");
		ResetColor();
		System.Console.WriteLine();
	}

	/// <summary>
	/// 渲染场面随从
	/// </summary>
	static void RenderField(Player player, string label, ConsoleColor color)
	{
		SetColor(ConsoleColor.DarkGray);
		System.Console.Write($"  {label}: ");

		if (player.Field.Count == 0)
		{
			SetColor(ConsoleColor.DarkGray);
			System.Console.WriteLine("(空)");
			ResetColor();
			return;
		}

		System.Console.WriteLine();
		for (int i = 0; i < player.Field.Count; i++)
		{
			var m = player.Field[i] as Minion;
			if (m == null) continue;

			System.Console.Write("    ");
			SetColor(ConsoleColor.DarkYellow);
			System.Console.Write($"[{i + 1}] ");
			SetColor(color);

			string name = GetCardName(m);
			System.Console.Write(name);

			SetColor(ConsoleColor.White);
			System.Console.Write($" {m.Atk}/{m.Health}");

			// 状态图标
			if (m.Frozen) { SetColor(ConsoleColor.Cyan); System.Console.Write(" ❄"); }
			if (m.Taunt) { SetColor(ConsoleColor.Yellow); System.Console.Write(" 🛡"); }
			if (m.DivineShield) { SetColor(ConsoleColor.Yellow); System.Console.Write(" ✨"); }
			if (m.CanAttack()) { SetColor(ConsoleColor.Green); System.Console.Write(" ⚔"); }
			if (m.TurnsInPlay == 0 && !m.Charge) { SetColor(ConsoleColor.DarkGray); System.Console.Write(" 💤"); }

			ResetColor();
			System.Console.WriteLine();
		}
	}

	/// <summary>
	/// 渲染手牌
	/// </summary>
	static void RenderHand(Player player)
	{
		SetColor(ConsoleColor.Yellow);
		System.Console.WriteLine("  ═══════════ 手 牌 ═══════════");
		ResetColor();

		if (player.Hand.Count == 0)
		{
			SetColor(ConsoleColor.DarkGray);
			System.Console.WriteLine("    (无手牌)");
			ResetColor();
			return;
		}

		for (int i = 0; i < player.Hand.Count; i++)
		{
			var card = player.Hand[i] as BuffableEntity;
			if (card == null) continue;

			int cost = card.Cost;
			bool canAfford = player.Mana >= cost;

			System.Console.Write("    ");
			SetColor(ConsoleColor.DarkYellow);
			System.Console.Write($"[{i + 1}] ");

			// 费用
			SetColor(canAfford ? ConsoleColor.Blue : ConsoleColor.DarkGray);
			System.Console.Write($"({cost}费) ");

			// 卡牌名
			SetColor(canAfford ? ConsoleColor.White : ConsoleColor.DarkGray);
			string name = GetCardName(card);
			System.Console.Write(name);

			// 类型信息
			if (card is Minion m)
			{
				SetColor(canAfford ? ConsoleColor.Green : ConsoleColor.DarkGray);
				System.Console.Write($" [{m.Atk}/{m.MaxHealth}]");
			}
			else if (card.Type == CardType.Spell)
			{
				SetColor(canAfford ? ConsoleColor.Magenta : ConsoleColor.DarkGray);
				System.Console.Write(" [法术]");
			}
			else if (card is Weapon w)
			{
				SetColor(canAfford ? ConsoleColor.DarkYellow : ConsoleColor.DarkGray);
				System.Console.Write($" [{w.Atk}/{w.MaxDurability}武器]");
			}

			ResetColor();
			System.Console.WriteLine();
		}
	}

	/// <summary>
	/// 渲染操作菜单
	/// </summary>
	public static void RenderMenu(Player player)
	{
		System.Console.WriteLine();
		SetColor(ConsoleColor.Cyan);
		System.Console.WriteLine("  ══════════ 操 作 ══════════");
		SetColor(ConsoleColor.White);
		System.Console.WriteLine("  [P] 出牌   [A] 攻击   [E] 结束回合   [Q] 退出");
		ResetColor();
		System.Console.Write("  > ");
	}

	/// <summary>
	/// 显示AI回合信息
	/// </summary>
	public static void ShowAIAction(string message)
	{
		SetColor(ConsoleColor.DarkYellow);
		System.Console.WriteLine($"  🤖 AI: {message}");
		ResetColor();
		Thread.Sleep(600); // 让玩家能看清AI操作
	}

	/// <summary>
	/// 错误提示
	/// </summary>
	public static void ShowError(string message)
	{
		SetColor(ConsoleColor.Red);
		System.Console.WriteLine($"  ❌ {message}");
		ResetColor();
	}

	/// <summary>
	/// 信息提示
	/// </summary>
	public static void ShowInfo(string message)
	{
		SetColor(ConsoleColor.Cyan);
		System.Console.WriteLine($"  ℹ {message}");
		ResetColor();
	}

	/// <summary>
	/// 游戏结束
	/// </summary>
	public static void ShowGameOver(Game game, Player humanPlayer)
	{
		System.Console.WriteLine();
		PrintSeparator("═", ConsoleColor.Yellow);
		if (humanPlayer.PlayState == PlayState.Won)
		{
			SetColor(ConsoleColor.Green);
			System.Console.WriteLine("  🎉🎉🎉  恭喜你赢了！  🎉🎉🎉");
		}
		else if (humanPlayer.PlayState == PlayState.Lost)
		{
			SetColor(ConsoleColor.Red);
			System.Console.WriteLine("  💀💀💀  你输了…  💀💀💀");
		}
		else
		{
			SetColor(ConsoleColor.Yellow);
			System.Console.WriteLine("  🤝  平局！");
		}
		ResetColor();
		PrintSeparator("═", ConsoleColor.Yellow);
	}

	// ── 辅助方法 ──

	static void PrintSeparator(string ch, ConsoleColor color)
	{
		SetColor(color);
		System.Console.WriteLine("  " + string.Concat(Enumerable.Repeat(ch, 36)));
		ResetColor();
	}

	static void PrintHeader(string text, ConsoleColor color)
	{
		SetColor(color);
		System.Console.WriteLine($"  {text}");
		ResetColor();
	}

	public static string GetCardName(BaseEntity entity)
	{
		if (entity is BuffableEntity be && be.CardData != null)
		{
			if (be.CardData.TagsLoc.TryGetValue(GameTag.Cardname, out var locName) &&
				locName.TryGetValue("zhCN", out var zhName) && !string.IsNullOrEmpty(zhName))
				return zhName;
			if (be.CardData.TagsLoc.TryGetValue(GameTag.Cardname, out var enName) &&
				enName.TryGetValue("enUS", out var en) && !string.IsNullOrEmpty(en))
				return en;
			return be.CardData.CardId;
		}
		return (entity as BuffableEntity)?.CardId ?? $"Entity#{entity.EntityId}";
	}

	static string GetClassName(string heroId) => heroId switch
	{
		"HERO_01" => "战士",
		"HERO_02" => "萨满",
		"HERO_03" => "潜行者",
		"HERO_04" => "圣骑士",
		"HERO_05" => "猎人",
		"HERO_06" => "德鲁伊",
		"HERO_07" => "术士",
		"HERO_08" => "法师",
		"HERO_09" => "牧师",
		"HERO_10" => "恶魔猎手",
		_ => heroId,
	};
}
