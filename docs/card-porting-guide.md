# 卡牌移植指南 — 声明式 C# API 完全参考

本文档详细介绍所有可用于卡牌脚本的 API，以及如何将炉石传说卡牌效果移植到新的声明式 C# 接口。

---

## 1. 文件结构与命名空间

每个扩展包创建一个文件，继承 `CardPack`，放在 `Cards/` 目录下。

```csharp
using HearthstoneServer.Auras;
using static HearthstoneServer.Dsl.Events;
using static HearthstoneServer.Cards.CardScriptUtils;

namespace HearthstoneServer.Dsl.Cards;

public class MyExpansion : CardPack
{
    public override void Register(CardBuilder b)
    {
        // 在这里注册卡牌和 buff...
    }
}
```

### 必需导入说明

| using 语句 | 作用 |
|-----------|------|
| `HearthstoneServer.Auras` | 提供 `Refresh` 光环类 |
| `using static HearthstoneServer.Dsl.Events` | 提供事件常量 `OWN_TURN_BEGIN` 等 |
| `using static HearthstoneServer.Cards.CardScriptUtils` | 提供 `Freeze()`、`Acts()`、`ATK()` 等快捷方法 |

> [!IMPORTANT]
> `CardPack` 子类无需手动注册。`CardScriptRegistry.Initialize()` 会自动扫描程序集中所有 `CardPack` 子类并调用它们的 `Register()` 方法。

---

## 2. CardBuilder API

`CardBuilder` 是注册卡牌脚本的核心入口，提供两个方法。

### 2.1 `b.Card()` — 注册卡牌脚本

```csharp
public void Card(string cardId,
    Action[]?            play         = null,   // 战吼 / 法术施放效果
    EventListener[]?     events       = null,   // 场上持续事件监听
    EventListener[]?     secret       = null,   // 奥秘触发条件
    Action[]?            deathrattle  = null,   // 亡语
    Action[]?            combo        = null,   // 连击
    Action[]?            inspire      = null,   // 激励
    Refresh[]?           update       = null,   // 光环（每帧刷新）
    Dictionary<PlayReq,int>? requirements = null, // 使用条件
    string[]?            chooseCards  = null,   // 抉择子卡 ID
    string[]?            entourage    = null,   // 随从列表
    Refresh[]?           enrage       = null,   // 激怒光环
    Evaluator?           poweredUp    = null,   // 增强条件
    LazyNum?             costMod      = null);  // 费用修改器
```

**所有参数都是可选的**，只写需要的部分。每个参数的详细说明：

| 参数 | 类型 | 说明 |
|------|------|------|
| `cardId` | `string` | 卡牌 ID，如 `"CS2_029"`（火球术） |
| `play` | `Action[]` | 卡牌打出时执行的动作。随从 = 战吼，法术 = 施放效果。**支持隐式转换**：单个 `Action` 会自动转为 `Action[]`，无需包装 |
| `events` | `EventListener[]` | 卡牌在场时持续监听的事件。随从离场后停止。每个 `EventListener` 由触发条件 + 响应动作组成 |
| `secret` | `EventListener[]` | 奥秘专用事件监听。触发后奥秘会被消耗。通常配合 `Reveal(S.SELF)` 使用 |
| `deathrattle` | `Action[]` | 亡语效果，随从死亡时执行 |
| `combo` | `Action[]` | 连击效果。当本回合已打出过其他牌时，替代 `play` 执行 |
| `inspire` | `Action[]` | 激励效果，使用英雄技能后触发 |
| `update` | `Refresh[]` | 光环定义。游戏每次状态更新时刷新。用于持续性 buff，如暴风城勇士的 +1/+1 |
| `requirements` | `Dictionary<PlayReq, int>` | 使用条件限制。控制卡牌何时可以打出、需要什么目标 |
| `chooseCards` | `string[]` | 抉择卡牌 ID 列表，如德鲁伊的两个选项 |
| `entourage` | `string[]` | 随从列表，如伊瑟拉的梦境卡。用于随机选取 |
| `enrage` | `Refresh[]` | 激怒光环。仅当随从受伤时生效，满血后失效 |
| `poweredUp` | `Evaluator` | 增强条件。满足条件时卡牌视觉高亮（条件评估器） |
| `costMod` | `LazyNum` | 费用修改器。动态修改卡牌费用的 LazyNum 值 |

### 2.2 `b.Buff()` — 注册 Enchantment（附魔/增益）

```csharp
public void Buff(string cardId,
    int atk = 0,                                  // 攻击力加成
    int health = 0,                                // 生命值加成
    Func<BaseEntity, int, int>? atkMod = null,     // 攻击力修改函数
    Func<BaseEntity, int, int>? maxHealthMod = null, // 最大生命修改函数
    params (GameTag tag, object value)[] extraTags); // 额外 GameTag 标签
```

| 参数 | 类型 | 说明 |
|------|------|------|
| `cardId` | `string` | Enchantment 的卡牌 ID，如 `"CS2_222o"`。通常以 `o`、`e`、`e2` 结尾 |
| `atk` | `int` | 攻击力加成值。正数增加，负数减少。`atk: 2` 表示 +2 攻击力 |
| `health` | `int` | 生命值加成值。`health: 1` 表示 +1 生命值 |
| `atkMod` | `Func<BaseEntity, int, int>` | 攻击力修改函数。接收 (实体, 当前值)，返回新值。用于覆盖式修改如 `SET(1)` |
| `maxHealthMod` | `Func<BaseEntity, int, int>` | 最大生命修改函数。同上，用于设置最大生命值 |
| `extraTags` | `(GameTag, object)[]` | 额外标签。如 `(GameTag.Taunt, true)` 给予嘲讽，`(GameTag.Cost, -1)` 减费 |

---

## 3. Action（动作）完整列表

每个 Action 代表一个游戏状态变更操作。所有 Action 类定义在 [Actions.cs](file:///d:/hs/fireplace/fireplace/Fireplace.CSharp/src/Fireplace.Core/Dsl/Actions.cs)。

### 3.1 核心战斗类

| Action | 构造函数 | 说明 | 示例 |
|--------|----------|------|------|
| `Hit` | `Hit(target, amount)` | 对目标造成指定数值的伤害。`amount` 可以是 `int` 常量或 `LazyNum`（如 `ATK(SELF)`）。这是伤害管线的入口点，会触发法术伤害加成 | `new Hit(S.TARGET, 6)` |
| `Heal` | `Heal(target, amount)` | 恢复目标指定数值的生命值。不会超过最大生命值上限 | `new Heal(S.TARGET, 4)` |
| `FullHeal` | `FullHeal(target)` | 将目标生命值完全恢复到最大值。无需指定数值 | `new FullHeal(S.TARGET)` |
| `Damage` | `Damage(target)` / `Damage(target, amount)` | 底层伤害动作（修正后）。**也用作事件触发器**：`new Damage(S.SELF)` 代表"被伤害"事件 | `new Damage(S.FRIENDLY_MINIONS)` |
| `HitExcessDamage` | `HitExcessDamage(target, amount)` | 溢出伤害。超过目标生命的多余伤害会转移到其他目标 | `new HitExcessDamage(S.TARGET, 4)` |

### 3.2 增益类

| Action | 构造函数 | 说明 | 示例 |
|--------|----------|------|------|
| `Buff` | `Buff(target, buffId)` | 给目标施加 Enchantment（需预先用 `b.Buff()` 注册）。`buffId` 是字符串 | `new Buff(S.TARGET, "CS2_222o")` |
| `MultiBuff` | `MultiBuff(target, buffs)` | 同时施加多个 Buff | `new MultiBuff(S.SELF, buffs)` |
| `StoringBuff` | `StoringBuff(target, buff, card)` | 存储型 Buff，记住被 Buff 的卡牌信息 | `new StoringBuff(S.TARGET, "XX_e", card)` |
| `CopyDeathrattleBuff` | `CopyDeathrattleBuff(target, buff)` | 复制目标的亡语，附加为一个新的亡语 Buff | `new CopyDeathrattleBuff(S.TARGET, "XX_e")` |
| `SwapStateBuff` | `SwapStateBuff(target, other, buff)` | 交换两个实体的攻击/生命属性 Buff | 内握法有用 |
| `CopyStateBuff` | `CopyStateBuff(target, other, buff)` | 将一个实体的属性复制到另一个 | 影子狂乱等 |
| `SetStateBuff` | `SetStateBuff(target, other, buff)` | 设置目标属性为固定值的 Buff | 将攻击力设为 1 |

### 3.3 抽牌与手牌操作类

| Action | 构造函数 | 说明 | 示例 |
|--------|----------|------|------|
| `Draw` | `Draw(player)` | 从牌库顶抽一张牌到手牌。牌库空时触发疲劳 | `new Draw(S.CONTROLLER)` |
| `Give` | `Give(player, cardId)` | 将指定卡牌（按 ID）直接加入手牌，不从牌库抽取 | `new Give(S.CONTROLLER, "CS2_029")` |
| `Discard` | `Discard(target)` | 从手牌中丢弃指定卡牌到墓地 | `new Discard(S.Random(S.FRIENDLY_HAND))` |
| `Mill` | `Mill(player)` / `Mill(player, count)` | 从牌库顶部将牌直接丢入墓地（对方不获得）。无参数时默认 1 张 | `new Mill(S.OPPONENT, 2)` |
| `Shuffle` | `Shuffle(player, cardId)` | 将指定卡牌洗入牌库中的随机位置 | `new Shuffle(S.CONTROLLER, "XXX")` |
| `PutOnTop` | `PutOnTop(player, cardId)` | 将指定卡牌放到牌库顶部（下次抽牌时必定抽到） | `new PutOnTop(S.CONTROLLER, "XXX")` |
| `Bounce` | `Bounce(target)` | 将战场上的随从退回手牌。目标必须在场上 | `new Bounce(S.TARGET)` |

### 3.4 召唤与变形类

| Action | 构造函数 | 说明 | 示例 |
|--------|----------|------|------|
| `Summon` | `Summon(player, cardId)` | 在指定玩家场上召唤一个随从。`cardId` 可以是字符串或 `Copy` 对象 | `new Summon(S.CONTROLLER, "CS2_mirror")` |
| `SummonBothSides` | `SummonBothSides(target, card)` | 在目标两侧各召唤一个随从 | `new SummonBothSides(S.SELF, "XXX")` |
| `SummonCustomMinion` | `SummonCustomMinion(target, card, cost, atk, health)` | 召唤带有自定义属性的随从 | 翡翠魔像等 |
| `Morph` | `Morph(target, cardId)` | 将目标变形为另一张卡（替换实体，保留位置）。常用于变形术 | `new Morph(S.TARGET, "CS2_tk1")` |
| `Evolve` | `Evolve(target, amount)` | 进化随从，变形为费用高 `amount` 点的随机随从 | `new Evolve(S.FRIENDLY_MINIONS, 1)` |

### 3.5 控制与状态类

| Action | 构造函数 | 说明 | 示例 |
|--------|----------|------|------|
| `Destroy` | `Destroy(target)` | 消灭目标，将其移入墓地。会触发其亡语效果 | `new Destroy(S.TARGET)` |
| `Silence` | `Silence(target)` | 沉默随从——移除所有卡牌文字、Enchantment、特殊能力 | `new Silence(S.TARGET)` |
| `Freeze` | `Freeze(target)` | 冻结角色。被冻结的角色下一回合无法攻击 | `new Freeze(S.ENEMY_MINIONS)` |
| `Steal` | `Steal(target)` | 夺取目标控制权，将其转移到己方场上 | `new Steal(S.TARGET)` |
| `Counter` | `Counter(target)` | 反制卡牌——阻止其效果生效（用于反制术） | `new Counter(S.TARGET)` |
| `Remove` | `Remove(target)` | 将实体完全移出游戏（不算死亡，不触发亡语） | `new Remove(S.SELF)` |
| `Reveal` | `Reveal(target)` | 揭示奥秘。奥秘触发时必须调用，从奥秘区移除 | `new Reveal(S.SELF)` |
| `Retarget` | `Retarget(target, newTarget)` | 改变攻击/法术目标。误导等效果使用 | `new Retarget(S.TARGET, S.RANDOM_ENEMY_CHARACTER)` |
| `ExtraAttack` | `ExtraAttack(target)` | 额外攻击机会。给予目标一次额外攻击（风怒效果） | `new ExtraAttack(S.FRIENDLY_HERO)` |
| `LosesDivineShield` | `LosesDivineShield(target)` | 移除圣盾 | `new LosesDivineShield(S.TARGET)` |

### 3.6 标签操作类

| Action | 构造函数 | 说明 | 示例 |
|--------|----------|------|------|
| `SetTags` | `SetTags(target, tags)` | 设置 GameTag 标签为 true（如嘲讽、冲锋） | `SetTag(S.TARGET, GameTag.Taunt)` |
| `UnsetTags` | `UnsetTags(target, tags)` | 移除 GameTag 标签 | `UnsetTag(S.TARGET, GameTag.Stealth)` |
| `GetTag` | `GetTag(target, tag)` | 读取实体的 GameTag 值（通常用于条件判断） | `new GetTag(S.SELF, GameTag.Atk)` |
| `SetCurrentHealth` | `SetCurrentHealth(target, amount)` | 直接设置目标当前生命值（如先祖之魂） | `new SetCurrentHealth(S.TARGET, 1)` |
| `Swap` | `Swap(target, other)` | 交换两个实体的属性（攻击/生命互换） | `new Swap(S.TARGET, S.SELF)` |

### 3.7 资源类

| Action | 构造函数 | 说明 | 示例 |
|--------|----------|------|------|
| `GainArmor` | `GainArmor(target, amount)` | 英雄获得护甲。 `target` 通常是 `S.FRIENDLY_HERO` | `new GainArmor(S.FRIENDLY_HERO, 5)` |
| `GainMana` | `GainMana(player, amount)` | 获得空法力水晶（永久增加可用上限） | `new GainMana(S.CONTROLLER, 1)` |
| `ManaThisTurn` | `ManaThisTurn(player, amount)` | 获得本回合临时法力（回合结束后消失） | `new ManaThisTurn(S.CONTROLLER, 1)` |
| `FillMana` | `FillMana(player, amount)` | 恢复已消耗的法力水晶（不增加上限） | `new FillMana(S.CONTROLLER, 2)` |

### 3.8 游戏级事件类（也用作事件触发器）

| Action | 构造函数 | 说明 |
|--------|----------|------|
| `Attack` | `Attack(attacker)` / `Attack(attacker, target)` | 攻击事件。作为事件用法：`new Attack(S.FRIENDLY_HERO).After(...)` |
| `Death` | `Death(entity)` | 死亡事件。作为事件用法：`new Death(S.FRIENDLY_MINIONS).After(...)` |
| `Play` | `Play(player)` / `Play(player, filter)` | 打牌事件。`filter` 可筛选卡牌类型，如 `S.SPELL` |
| `BeginTurn` | `BeginTurn()` / `BeginTurn(player)` | 回合开始事件 |
| `EndTurn` | `EndTurn()` / `EndTurn(player)` | 回合结束事件 |
| `OverloadTrigger` | `OverloadTrigger(player)` | 过载事件。玩家打出过载牌时触发 |

### 3.9 特殊操作

| Action | 参数 | 说明 |
|--------|------|------|
| `Action * n` | 运算符重载 | 重复执行 n 次。如 `new Draw(S.CONTROLLER) * 2` 抽 2 张牌 |

---

## 4. 事件系统（EventListener）

事件监听由 **触发器** + **时机** + **响应动作** 三部分组成。

### 4.1 创建事件监听

每个事件类（`Attack`、`Play`、`Damage` 等）都有两个方法来创建 `EventListener`：

```csharp
// .On()  — 在事件发生时（同步）触发
trigger.On(actions...)

// .After() — 在事件完成后触发
trigger.After(actions...)
```

区别：`.On()` 可以修改/阻止正在发生的事件，`.After()` 在事件结算后执行。

### 4.2 预定义事件常量

在 [EventConstants.cs](file:///d:/hs/fireplace/fireplace/Fireplace.CSharp/src/Fireplace.Core/Dsl/EventConstants.cs) 中定义，需 `using static HearthstoneServer.Dsl.Events;`

| 常量 | 定义 | 说明 |
|------|------|------|
| `OWN_CARD_PLAY` | `Play(CONTROLLER)` | 己方打出任意卡牌 |
| `OWN_MINION_PLAY` | `Play(CONTROLLER, MINION)` | 己方打出随从 |
| `OWN_SECRET_PLAY` | `Play(CONTROLLER, SECRET)` | 己方打出奥秘 |
| `OWN_SPELL_PLAY` | `Play(CONTROLLER, SPELL)` | 己方打出法术 |
| `TURN_BEGIN` | `BeginTurn()` | 任意回合开始 |
| `OWN_TURN_BEGIN` | `BeginTurn(CONTROLLER)` | 己方回合开始 |
| `TURN_END` | `EndTurn()` | 任意回合结束 |
| `OWN_TURN_END` | `EndTurn(CONTROLLER)` | 己方回合结束 |
| `SELF_DAMAGE` | `Damage(SELF)` | 自身受到伤害 |
| `OWN_OVERLOAD` | `OverloadTrigger(CONTROLLER)` | 己方打出过载牌 |
| `REMOVED_IN_PLAY` | `Death(OWNER).After(Destroy(SELF))` | 拥有者离场时销毁自身附魔 |

### 4.3 自定义事件触发器

常量不够用时，可以直接构造：

```csharp
// 敌方随从攻击己方英雄
new Attack(S.ENEMY_MINIONS, S.FRIENDLY_HERO).On(...)

// 己方打出法术后
new Play(S.CONTROLLER, S.SPELL).After(...)

// 任何友方随从死亡后
new Death(S.FRIENDLY_MINIONS).After(...)

// 己方友方随从受伤时
new Damage(S.FRIENDLY_MINIONS).On(...)

// 对手打出随从时
new Play(S.OPPONENT, S.MINION).After(...)
```

### 4.4 事件使用示例

```csharp
// 爆破者：回合开始时对随机敌人造成 2 点伤害
b.Card("EX1_102", events: new[]
{
    OWN_TURN_BEGIN.On(new Hit(S.RANDOM_ENEMY_CHARACTER, 2))
});

// 铸甲师：友方随从受伤时获得 1 点护甲
b.Card("EX1_402", events: new[]
{
    new Damage(S.FRIENDLY_MINIONS).On(new GainArmor(S.FRIENDLY_HERO, 1))
});

// 大法师安东尼达斯：打出法术后将火球术加入手牌
b.Card("EX1_559", events: new[]
{
    new Play(S.CONTROLLER, S.SPELL).After(new Give(S.CONTROLLER, "CS2_029"))
});

// 无羁元素：过载时 +1/+1
b.Card("EX1_258", events: new[]
{
    OWN_OVERLOAD.After(new Buff(S.SELF, "EX1_258e"))
});
```

---

## 5. Selector（选择器）完整列表

所有选择器在 [SelectorConstants.cs](file:///d:/hs/fireplace/fireplace/Fireplace.CSharp/src/Fireplace.Core/Dsl/SelectorConstants.cs) 中定义，通过 `S.XXX` 访问。

### 5.1 特殊选择器

| Selector | 说明 | 典型用法 |
|----------|------|----------|
| `S.SELF` | 当前卡牌自身 | 激怒、光环中引用自己 |
| `S.TARGET` | 打出时选择的目标 | 法术/战吼的目标 |
| `S.OWNER` | 附魔的宿主实体 | Enchantment 脚本中引用被 Buff 的对象 |
| `S.CONTROLLER` | 己方玩家（Player 实体） | 抽牌、获得法力的操作对象 |
| `S.OPPONENT` | 对方玩家 | 对手相关操作 |

### 5.2 类型选择器

| Selector | 说明 |
|----------|------|
| `S.MINION` | 随从 |
| `S.SPELL` | 法术 |
| `S.WEAPON` | 武器 |
| `S.HERO` | 英雄 |
| `S.HERO_POWER` | 英雄技能 |
| `S.CHARACTER` | 角色（= 随从 + 英雄） |
| `S.PLAYER` | 玩家实体 |

### 5.3 区域选择器

| Selector | 说明 |
|----------|------|
| `S.IN_PLAY` | 战场区 |
| `S.IN_HAND` | 手牌区 |
| `S.IN_DECK` | 牌库区 |
| `S.IN_SECRET` | 奥秘区 |

### 5.4 阵营选择器

| Selector | 说明 |
|----------|------|
| `S.FRIENDLY` | 与 source 同控制者 |
| `S.ENEMY` | 与 source 不同控制者 |

### 5.5 标签选择器

| Selector | 对应 GameTag | 说明 |
|----------|-------------|------|
| `S.TAUNT` | Taunt | 有嘲讽 |
| `S.CHARGE` | Charge | 有冲锋 |
| `S.STEALTH` | Stealth | 有潜行 |
| `S.DIVINE_SHIELD` | DivineShield | 有圣盾 |
| `S.WINDFURY` | Windfury | 有风怒 |
| `S.FROZEN` | Freeze | 被冻结 |
| `S.DAMAGED` | Damage | 受了伤 |
| `S.DEATHRATTLE` | Deathrattle | 有亡语 |
| `S.BATTLECRY` | Battlecry | 有战吼 |
| `S.COMBO` | Combo | 有连击 |
| `S.SECRET` | Secret | 是奥秘 |
| `S.QUEST` | Quest | 是任务 |
| `S.SPELLPOWER` | Spellpower | 有法术伤害 |
| `S.IMMUNE` | Immune | 免疫 |
| `S.LIFESTEAL` | Lifesteal | 吸血 |
| `S.RUSH` | Rush | 突袭 |
| `S.ECHO` | Echo | 回响 |
| `S.REBORN` | Reborn | 复生 |
| `S.DORMANT` | Dormant | 休眠 |
| `S.OVERLOAD` | Overload | 有过载 |

### 5.6 种族选择器

| Selector | 种族 |
|----------|------|
| `S.BEAST` | 野兽 |
| `S.DEMON` | 恶魔 |
| `S.DRAGON` | 龙 |
| `S.MECH` | 机械 |
| `S.MURLOC` | 鱼人 |
| `S.PIRATE` | 海盗 |
| `S.TOTEM` | 图腾 |
| `S.ELEMENTAL` | 元素 |

### 5.7 稀有度选择器

| Selector | 稀有度 |
|----------|--------|
| `S.COMMON` | 普通 |
| `S.RARE` | 稀有 |
| `S.EPIC` | 史诗 |
| `S.LEGENDARY` | 传说 |

### 5.8 预组合选择器

这些是最常用的，由基础选择器组合而成：

| Selector | 定义 | 说明 |
|----------|------|------|
| **己方** |||
| `S.FRIENDLY_HERO` | `IN_PLAY + FRIENDLY + HERO` | 己方英雄 |
| `S.FRIENDLY_MINIONS` | `ALL_MINIONS + FRIENDLY` | 己方场上随从 |
| `S.FRIENDLY_CHARACTERS` | `ALL_CHARACTERS + FRIENDLY` | 己方所有角色 |
| `S.FRIENDLY_HAND` | `IN_HAND + FRIENDLY` | 己方手牌 |
| `S.FRIENDLY_DECK` | `IN_DECK + FRIENDLY` | 己方牌库 |
| `S.FRIENDLY_WEAPON` | `ALL_WEAPONS + FRIENDLY` | 己方武器 |
| `S.FRIENDLY_SECRETS` | `ALL_SECRETS + FRIENDLY` | 己方奥秘 |
| `S.FRIENDLY_HERO_POWER` | `ALL_HERO_POWERS + FRIENDLY` | 己方英雄技能 |
| **敌方** |||
| `S.ENEMY_HERO` | `ALL_HEROES + ENEMY` | 敌方英雄 |
| `S.ENEMY_MINIONS` | `ALL_MINIONS + ENEMY` | 敌方场上随从 |
| `S.ENEMY_CHARACTERS` | `ALL_CHARACTERS + ENEMY` | 敌方所有角色 |
| `S.ENEMY_HAND` | `IN_HAND + ENEMY` | 敌方手牌 |
| `S.ENEMY_DECK` | `IN_DECK + ENEMY` | 敌方牌库 |
| `S.ENEMY_WEAPON` | `ALL_WEAPONS + ENEMY` | 敌方武器 |
| **全局** |||
| `S.ALL_MINIONS` | `IN_PLAY + MINION - DORMANT` | 所有场上随从（排除休眠） |
| `S.ALL_CHARACTERS` | `IN_PLAY + CHARACTER - DORMANT` | 所有角色 |
| `S.ALL_HEROES` | `IN_PLAY + HERO` | 所有英雄 |
| `S.ALL_WEAPONS` | `IN_PLAY + WEAPON` | 所有武器 |
| `S.ALL_SECRETS` | `IN_SECRET + SECRET` | 所有奥秘 |

### 5.9 预组合随机选择器

| Selector | 说明 |
|----------|------|
| `S.RANDOM_MINION` | 随机场上随从 |
| `S.RANDOM_CHARACTER` | 随机角色 |
| `S.RANDOM_OTHER_CHARACTER` | 随机其他角色（排除自身） |
| `S.RANDOM_FRIENDLY_MINION` | 随机己方随从 |
| `S.RANDOM_OTHER_MINION` | 随机其他随从 |
| `S.RANDOM_ENEMY_MINION` | 随机敌方随从 |
| `S.RANDOM_ENEMY_CHARACTER` | 随机敌方角色 |
| `S.RANDOM_FRIENDLY_CHARACTER` | 随机己方角色 |
| `S.RANDOM_OTHER_FRIENDLY_MINION` | 随机己方其他随从 |

### 5.10 位置选择器

| Selector | 说明 |
|----------|------|
| `S.SELF_ADJACENT` | 自身两侧的相邻随从 |
| `S.TARGET_ADJACENT` | 目标两侧的相邻随从 |
| `S.LeftOf(sel)` | 指定者左侧的随从 |
| `S.RightOf(sel)` | 指定者右侧的随从 |
| `S.Adjacent(sel)` | 指定者两侧的相邻随从 |

### 5.11 其他选择器

| Selector | 说明 |
|----------|------|
| `S.DAMAGED_CHARACTERS` | 所有受伤角色 |
| `S.FRIENDLY_GRAVEYARD` | 己方墓地 |
| `S.KILLED_THIS_TURN` | 本回合被杀死的实体 |

### 5.12 选择器工具方法

| 方法 | 说明 | 示例 |
|------|------|------|
| `S.Random(sel, n)` | 从 `sel` 中随机选 `n` 个（默认 1） | `S.Random(S.ENEMY_HAND)` |
| `S.Id(cardId)` | 按卡牌 ID 筛选 | `S.Id("CS2_029")` |

### 5.13 选择器运算符

选择器支持 3 种组合运算：

| 运算符 | 含义 | 示例 | 结果 |
|--------|------|------|------|
| `+` | 交集（AND） | `S.FRIENDLY_MINIONS + S.BEAST` | 己方场上的野兽 |
| `-` | 排除（NOT） | `S.ALL_MINIONS - S.SELF` | 所有其他随从 |
| `\|` | 并集（OR） | `S.MINION \| S.HERO` | 随从或英雄 |

```csharp
S.FRIENDLY_MINIONS - S.SELF        // 己方其他随从
S.FRIENDLY_HAND + S.SPELL          // 手牌中的法术
S.ENEMY_CHARACTERS - S.IMMUNE      // 所有非免疫敌方角色
S.ALL_MINIONS + S.BEAST + S.ENEMY  // 敌方场上的野兽
```

---

## 6. CardScriptUtils 工具方法

在 [CardScriptUtils.cs](file:///d:/hs/fireplace/fireplace/Fireplace.CSharp/src/Fireplace.Core/Cards/CardScriptUtils.cs) 中定义，需 `using static HearthstoneServer.Cards.CardScriptUtils;`

### 6.1 标签快捷方法

这些方法是 `SetTag` / `UnsetTag` 的快捷封装，返回 `SetTags` 或 `UnsetTags` 类型：

| 方法 | 返回类型 | 说明 |
|------|----------|------|
| `Freeze(target)` | `SetTags` | 冻结目标（设置 `GameTag.Frozen`） |
| `Stealth(target)` | `SetTags` | 给予潜行 |
| `Unstealth(target)` | `UnsetTags` | 移除潜行 |
| `Taunt(target)` | `SetTags` | 给予嘲讽 |
| `GiveCharge(target)` | `SetTags` | 给予冲锋 |
| `GiveDivineShield(target)` | `SetTags` | 给予圣盾 |
| `GiveWindfury(target)` | `SetTags` | 给予风怒 |
| `GivePoisonous(target)` | `SetTags` | 给予剧毒 |
| `GiveLifesteal(target)` | `SetTags` | 给予吸血 |
| `GiveRush(target)` | `SetTags` | 给予突袭 |
| `GiveReborn(target)` | `SetTags` | 给予复生 |

### 6.2 属性读取方法（LazyNum）

动态读取实体属性值，用于伤害计算等：

| 方法 | 返回类型 | 说明 | 常见用法 |
|------|----------|------|----------|
| `ATK(sel)` | `Attr` (LazyNum) | 获取实体攻击力 | `new Hit(S.TARGET, ATK(S.SELF))` — 等攻伤害 |
| `ARMOR(sel)` | `Attr` | 获取实体护甲值 | `new Hit(S.ENEMY_HERO, ARMOR(S.FRIENDLY_HERO))` |
| `CURRENT_HEALTH(sel)` | `Attr` | 获取当前生命值 | 用于等生命伤害 |
| `MANA(sel)` | `Attr` | 获取法力水晶数 | 用于费用相关计算 |
| `MAX_MANA(sel)` | `Attr` | 获取最大法力 | |
| `OVERLOAD_LOCKED(sel)` | `Attr` | 获取锁定过载值 | |
| `OVERLOAD_OWED(sel)` | `Attr` | 获取欠缴过载值 | |

### 6.3 组合选择器方法

| 方法 | 返回类型 | 说明 |
|------|----------|------|
| `HIGHEST_ATK(sel)` | `Selector` | 从 `sel` 结果中选攻击力最高的实体 |
| `CLEAVE` | `Hit` | 对攻击目标的相邻随从造成等于自身攻击力的伤害 |

### 6.4 条件评估器

| 方法/常量 | 类型 | 说明 |
|-----------|------|------|
| `EMPTY_BOARD` | `Evaluator` | 己方场上无随从 |
| `EMPTY_HAND` | `Evaluator` | 己方手牌为空 |
| `FULL_BOARD` | `Evaluator` | 己方场上有 7 个随从 |
| `FULL_HAND` | `Evaluator` | 手牌已满 |
| `HOLDING_DRAGON` | `Evaluator` | 手牌中有龙（不含自身） |
| `AT_MAX_MANA(sel)` | `Evaluator` | 玩家法力已满 |

### 6.5 动作组合工具

| 方法 | 说明 | 示例 |
|------|------|------|
| `Acts(a, b, ...)` | 将多个 Action 封装为 `Action[]` | `Acts(new Hit(S.TARGET, 4), Freeze(S.TARGET))` |
| `GainEmptyMana(sel, n)` | 获得 n 个空法力水晶 | `GainEmptyMana(S.CONTROLLER, 1)` |
| `SET(n)` | 创建固定值修改函数 `(_, _) => n` | `b.Buff("XXX", atkMod: SET(1))` — 攻击力设为 1 |
| `CastWhenDrawn()` | 抽到时施放效果 | 疲劳卡使用 |
| `RECRUIT` | 从牌库召唤随机随从 | `play: RECRUIT` |
| `Recruit(sel)` | 带筛选的征召 | `Recruit(S.MECH)` — 征召一个机械 |

### 6.6 卡牌 ID 集合

| 常量 | 说明 |
|------|------|
| `BASIC_HERO_POWERS` | 基础英雄技能 ID 列表 |
| `UPGRADED_HERO_POWERS` | 升级英雄技能 ID 列表 |
| `BASIC_TOTEMS` | 萨满基础图腾 ID 列表 |
| `POTIONS` | 药水卡 ID 列表 |
| `LICH_KING_CARDS` | 巫妖王卡牌列表 |
| `THE_COIN` | 硬币 ID (`"GAME_005"`) |
| `LACKEY_CARDS` | 跟班卡 ID 列表 |

---

## 7. Refresh（光环系统）

`Refresh` 定义在 [Refresh.cs](file:///d:/hs/fireplace/fireplace/Fireplace.CSharp/src/Fireplace.Core/Auras/Refresh.cs)，用于持续性效果。

### 构造函数

```csharp
new Refresh(Selector selector,
    Dictionary<GameTag, object>? tags = null,  // 直接设置标签（二选一）
    string? buff = null,                        // 引用注册的 buff ID（二选一）
    int priority = 50)                          // 执行优先级（越小越先）
```

| 参数 | 说明 |
|------|------|
| `selector` | 光环影响范围。如 `S.FRIENDLY_MINIONS - S.SELF`（友方其他随从） |
| `tags` | 直接设置 GameTag 值（如激怒 +3 攻击力）。与 `buff` 二选一 |
| `buff` | 引用注册的 Enchantment ID（如 `"CS2_222o"`）。与 `tags` 二选一 |
| `priority` | 多个光环冲突时的执行顺序。默认 50 |

### 光环 vs Buff 的区别

- **光环（`update`/`enrage`）**：持续刷新，实体离场即消失
- **Buff（`play` 中使用）**：一次性施加，永久保留直到被沉默

### 示例

```csharp
// 暴风城勇士：友方其他随从 +1/+1（持续光环）
b.Card("CS2_222", update: new[]
{
    new Refresh(S.FRIENDLY_MINIONS - S.SELF, buff: "CS2_222o")
});
b.Buff("CS2_222o", atk: 1, health: 1);

// 火舌图腾：相邻随从 +2 攻击
b.Card("EX1_565", update: new[]
{
    new Refresh(S.SELF_ADJACENT, buff: "EX1_565o")
});
b.Buff("EX1_565o", atk: 2);

// 激怒：用 tags 直接设置
b.Card("EX1_393", enrage: new[]
{
    new Refresh(S.SELF, new Dictionary<GameTag, object>
    {
        [GameTag.Atk] = 3
    })
});

// 巫师学徒：手牌中法术减费
b.Card("EX1_608", update: new[]
{
    new Refresh(S.FRIENDLY_HAND + S.SPELL - S.SELF, buff: "EX1_608e")
});
b.Buff("EX1_608e", extraTags: (GameTag.Cost, -1));
```

---

## 8. PlayReq（使用条件）完整列表

控制卡牌的使用条件和目标选择。定义在 `PlayReq` 枚举中。

### 目标要求

| PlayReq | 说明 | 常见搭配 |
|---------|------|----------|
| `TargetToPlay` | **必须**选择目标才能打出 | 火球术、变形术 |
| `TargetIfAvailable` | 有合法目标就必须选，没有也能打 | 巫医 |
| `TargetForCombo` | 连击时需要选择目标 | 军情七处特工 |

### 目标类型限制

| PlayReq | 说明 | 常见搭配 |
|---------|------|----------|
| `MinionTarget` | 目标必须是随从 | 变形术、暗言术 |
| `FriendlyTarget` | 目标必须是己方 | 治疗之触 |
| `EnemyTarget` | 目标必须是敌方 | 冰霜震击 |
| `NonSelfTarget` | 不能选自己 | 暗影步 |

### 数量条件

| PlayReq | 值含义 | 说明 |
|---------|--------|------|
| `MinimumEnemyMinions` | 值 = N | 敌方场上至少 N 个随从 |
| `MinimumTotalMinions` | 值 = N | 场上至少 N 个随从 |
| `MinimumFriendlyMinions` | 值 = N | 己方场上至少 N 个随从 |

### 组合示例

```csharp
// 火球术：必须选目标（任何角色）
requirements: new() { [PlayReq.TargetToPlay] = 0 }

// 变形术：目标必须是随从
requirements: new()
{
    [PlayReq.MinionTarget] = 0,
    [PlayReq.TargetToPlay] = 0
}

// 冰霜震击：目标必须是敌方
requirements: new()
{
    [PlayReq.EnemyTarget] = 0,
    [PlayReq.TargetToPlay] = 0
}

// 叉状闪电：场上至少 1 个敌方随从
requirements: new() { [PlayReq.MinimumEnemyMinions] = 1 }

// 岩石比特：必须是友方目标
requirements: new()
{
    [PlayReq.FriendlyTarget] = 0,
    [PlayReq.TargetToPlay] = 0
}
```

---

## 9. 常用模式速查

### 9.1 基础战吼

```csharp
b.Card("EX1_593", play: new Hit(S.ENEMY_HERO, 3));       // 夜刃刺客
b.Card("EX1_015", play: new Draw(S.CONTROLLER));           // 工程师学徒
```

### 9.2 多个动作

```csharp
b.Card("CS2_024",
    play: Acts(new Hit(S.TARGET, 3), Freeze(S.TARGET)),    // 寒冰箭
    requirements: new() { [PlayReq.TargetToPlay] = 0 });
```

### 9.3 重复动作

```csharp
b.Card("CS2_023", play: new Draw(S.CONTROLLER) * 2);              // 奥术智慧
b.Card("BT_036", play: new Summon(S.CONTROLLER, "BT_036t") * 3);  // 协调攻击
```

### 9.4 亡语

```csharp
b.Card("FP1_002", deathrattle: new Summon(S.CONTROLLER, "FP1_002t") * 2);
```

### 9.5 连击

```csharp
b.Card("CS2_073",
    play: new Buff(S.TARGET, "CS2_073e"),            // 冷血：+2 攻击
    combo: new Buff(S.TARGET, "CS2_073e2"),           // 连击改为 +4 攻击
    requirements: new() { [PlayReq.MinionTarget] = 0, [PlayReq.TargetToPlay] = 0 });
```

### 9.6 奥秘

```csharp
b.Card("EX1_294", secret: new[]                       // 镜像实体
{
    new Play(S.OPPONENT, S.MINION).After(
        new Reveal(S.SELF), new Summon(S.CONTROLLER, new Copy(S.TARGET)))
});
```

### 9.7 抉择

```csharp
b.Card("EX1_164", chooseCards: new[] { "EX1_164a", "EX1_164b" });  // 滋养
```

### 9.8 法术费用降低光环

```csharp
b.Card("EX1_608", update: new[]                        // 巫师学徒
{
    new Refresh(S.FRIENDLY_HAND + S.SPELL - S.SELF, buff: "EX1_608e")
});
b.Buff("EX1_608e", extraTags: (GameTag.Cost, -1));
```

### 9.9 过载事件触发

```csharp
b.Card("EX1_258", events: new[]                        // 无羁元素
{
    OWN_OVERLOAD.After(new Buff(S.SELF, "EX1_258e"))
});
b.Buff("EX1_258e", atk: 1, health: 1);
```

---

## 10. Python → C# 迁移对照

| Python | C# |
|--------|-----|
| `class CS2_029(SpellCard):` | `b.Card("CS2_029", ...)` |
| `play = Hit(TARGET, 6)` | `play: new Hit(S.TARGET, 6)` |
| `Draw(CONTROLLER) * 2` | `new Draw(S.CONTROLLER) * 2` |
| `requirements = {PlayReq.REQ_TARGET_TO_PLAY: 0}` | `requirements: new() { [PlayReq.TargetToPlay] = 0 }` |
| `update = Refresh(FRIENDLY_MINIONS - SELF, buff="XXo")` | `update: new[] { new Refresh(S.FRIENDLY_MINIONS - S.SELF, buff: "XXo") }` |
| `events = [OWN_TURN_END.on(Hit(SELF, 1))]` | `events: new[] { OWN_TURN_END.On(new Hit(S.SELF, 1)) }` |
| `CS2_222o = buff(atk=1, health=1)` | `b.Buff("CS2_222o", atk: 1, health: 1)` |
| `combo = Hit(TARGET, 2)` | `combo: new Hit(S.TARGET, 2)` |
| `deathrattle = Summon(CONTROLLER, "token")` | `deathrattle: new Summon(S.CONTROLLER, "token")` |
| `choose = ("EX1_164a", "EX1_164b")` | `chooseCards: new[] { "EX1_164a", "EX1_164b" }` |
| `enrage = Refresh(SELF, {Atk: 3})` | `enrage: new[] { new Refresh(S.SELF, new Dictionary<GameTag, object> { [GameTag.Atk] = 3 }) }` |

### 关键语法差异

1. **`S.` 前缀**：C# 中所有选择器需要 `S.` 前缀（Python 中直接用 `TARGET`）
2. **`new` 关键字**：C# 中创建 Action 需要 `new`（Python 中直接调用）
3. **数组语法**：`events`/`secret`/`update` 需要 `new[]{ }` 包装
4. **Requirements 语法**：`new() { [Key] = Value }` 代替 Python 的 `{Key: Value}`
5. **单个 Action 自动转换**：当 `play`/`combo`/`deathrattle` 只有一个 Action 时，C# 会自动转换为数组，无需显式包装

---

## 11. 移植五步流程

### Step 1: 确定卡牌 ID
从 `cards.json` 或 HearthstoneJSON 查找。

### Step 2: 分析效果
将卡牌文字拆解为 Action 组合。

### Step 3: 注册 Enchantment
如果涉及 Buff，先用 `b.Buff()` 注册。

### Step 4: 注册卡牌
用 `b.Card()` 注册卡牌脚本。

### Step 5: 编译验证

```bash
dotnet build src\Fireplace.Core\Fireplace.Core.csproj
dotnet test tests\Fireplace.Tests\Fireplace.Tests.csproj
```
