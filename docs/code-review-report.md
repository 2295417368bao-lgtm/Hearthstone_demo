# Hearthstone_demo 代码审查报告

审查范围：`src/Hearthstone.Core`、`src/Hearthstone.WebApi` 关键流程代码。

## 总结

本次审查发现 **5 个高优先级问题**（含功能正确性与可维护性/可部署性风险）。其中 3 个会直接影响对局合法性或流程正确性。

## 发现的问题

### 1) 预置卡组张数不满足 30 张（规则正确性）
- `GameSetup.GetDeck` 中多个职业卡组均未达到 30 张。
- 炉石构筑模式默认规则是 30 张卡组，当前实现会导致：
  - 起手/抽牌阶段与真实规则偏离；
  - 牌库更快抽空，影响疲劳节奏；
  - 测试或对战平衡出现系统性偏差。
- 通过脚本统计当前张数：
  - Mage: 28
  - Hunter: 27
  - Warrior: 28
  - Warlock: 29
  - Paladin: 26

建议：
- 在 `GetDeck` 返回前添加统一断言（长度必须 30，不满足则抛异常并附职业名）。
- 将硬编码卡组迁移至外部数据文件并加 CI 校验脚本，避免手工维护错误。

### 2) PvP 允许同一玩家重复 Mulligan（流程漏洞）
- `GameHub.Mulligan` 在 PvP 模式下，**先执行** `ctrl.Mulligan(caller, cardIndices)`，再将 `Player1MulliganDone/Player2MulliganDone` 标记为 `true`。
- 但没有在函数入口阻止“已完成 Mulligan 的玩家再次提交”，导致同一玩家可重复重抽，直到另一方完成为止。

影响：
- 破坏公平性（可通过多次重抽优化起手）。
- 服务端状态复杂化，可能引入难复现 bug。

建议：
- 在 PvP 分支入口先判断该玩家是否已 MulliganDone，若是则直接返回错误。
- 建议把 Mulligan 阶段建模为显式状态机（WaitingP1 / WaitingP2 / Done），避免布尔变量组合导致漏洞。

### 3) `GameUtils.SetupGame` 生成了随机卡组但未赋值给玩家（功能不一致）
- `SetupGame` 中虽然计算了 `deck1/deck2`，但后续没有赋给 `player1.StartingDeck/player2.StartingDeck`。
- 同时也未设置 `StartingHero`，仅创建了玩家名。

影响：
- 方法行为与注释“Set up a new game with random decks and random classes”不一致。
- `PlayFullGame` 基于 `SetupGame`，可能运行在空/异常初始状态，导致测试与预期偏离。

建议：
- 显式设置 `StartingDeck` 与 `StartingHero` 后再 `game.Setup()`。
- 为 `SetupGame` 添加单元测试：断言双方初始牌库数量、英雄职业与随机选择一致。

### 4) 默认卡牌数据库路径包含硬编码 Windows 绝对路径（可部署性风险）
- `CardDatabase.Initialize()` 默认路径找不到时，回退到 `d:\hs\Hearthstone.CSharp\data\cards_dbfid.json`。

影响：
- Linux/macOS 与容器部署几乎必然失效。
- 本地开发路径泄露到生产代码，不利于配置治理。

建议：
- 统一改为配置驱动（环境变量 / appsettings / 命令行参数）。
- 在回退失败时给出明确异常信息，不要依赖开发机路径。

### 5) 可用职业列表与 Hero 映射不一致（产品行为不一致）
- `HeroIds` 已包含 9 职业，但 `AvailableClasses` 仅返回 5 职业。

影响：
- 前后端展示和可选项可能不一致（部分职业“存在但不可选”）。
- 扩展职业时容易遗漏，造成隐藏功能与灰态数据。

建议：
- `AvailableClasses` 直接由 `HeroIds.Keys` 派生，避免双份维护。
- 加测试确保 `AvailableClasses` 与 `HeroIds` 一致。

## 审查时执行的检查

- 尝试运行测试：`dotnet test Hearthstone.CSharp.sln -v minimal`（当前环境缺少 `dotnet`）。
- 通过 Python 脚本对 `GameSetup.GetDeck` 的硬编码列表做静态计数，确认卡组张数异常。

