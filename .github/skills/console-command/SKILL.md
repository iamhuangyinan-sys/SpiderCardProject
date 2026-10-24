---
name: console-command
description: 'SpiderCard 项目控制台调试命令的维护：新增 / 修改 / 删除。Use when: 开发者描述一条命令要做进游戏内控制台（ConsolePanel），或要求改命令的名字 / 参数 / 行为 / 输出、删掉某条命令；涉及 ConsoleCommandRegistry、ConsoleCommandManager、docs/Console.md。关键词：控制台命令、console command、注册命令、Register、skip、add、help、修改命令、删除命令、移除命令、牌 id、N0_1、S_m_0200001、docs/Console.md。'
argument-hint: '描述要做什么：新增 / 修改 / 删除 + 命令名 + 作用 + 参数 + 期望输出'
---

# 控制台命令

本 skill 负责游戏内控制台（`ConsolePanel`）的**全部命令维护**：新增、修改、删除。

每次改动都要同步两个地方：

1. `Assets/Scripts/Console/ConsoleCommandRegistry.cs` —— 注册与 handler
2. `docs/Console.md` —— 用户文档（命令表 + 用法 + 示例）

## 第 0 步：先读

| 文件 | 看什么 |
|---|---|
| `Assets/Scripts/Console/ConsoleCommandRegistry.cs` | 现有注册顺序、handler 写法、失败文案风格 |
| `Assets/Scripts/Console/ConsoleCommandManager.cs` | 匹配规则（段数必须一致、`*` 匹配整段、`args` 含命令本身） |
| `docs/Console.md` | 文档格式（只写「命令有哪些 + 怎么写」，不写内部结构） |
| 命令要用到的 Manager | 例：加牌 `CardsManager`、跳关 `CardGameModule`、关卡 `LevelManager` |

## 一条命令的三个落点

| 位置 | 内容 |
|---|---|
| `RegisterAll()` | `cmd.Register("pattern", CmdXxx, "说明")` |
| handler | `private static string CmdXxx(string[] args)` |
| `docs/Console.md` | 命令表的一行 + 参数用法 + 示例 |

改 / 删之前先 `grep` 把这三处都找出来（**以代码为准**，文档可能已经和代码不一致）。

## 新增

### 注册

在 `RegisterAll()` 里按现有分组加一行：

```csharp
// xxx <参数> —— 一句话说明
cmd.Register("xxx *", CmdXxx, "一句话说明（* = 参数含义）");
```

规则：

- `pattern` 用空格分段；`*` 匹配**整段**任意内容，也支持段内通配（如 `lv*`）。
- **段数必须和用户输入一致**，否则不会命中 —— 带几个参数就写几段。
- 不要重复注册同一个 pattern（先注册的生效）。
- 说明文案会进 `GetHelp()`，写清楚参数含义。

### handler

```csharp
/// <summary>xxx &lt;参数&gt; —— 一句话说明</summary>
private static string CmdXxx(string[] args)
{
    // 1. 前置校验：需要什么状态就加什么（现有两个命令都要求在同一对局里）
    if (!CardGameEntry.IsInCardScene) return "执行失败：当前不在卡牌场景";
    if (!CardsManager.Instance.IsPlaying) return "执行失败：当前没有进行中的对局";
    if (CardsManager.Instance.IsSettling) return "执行失败：正在结算收牌";

    // 2. 取参：args[0] 是命令段本身；先判长度，别越界
    string value = args.Length > 1 ? args[1] : string.Empty;
    if (string.IsNullOrEmpty(value)) return "执行失败：缺少参数（用法：xxx <参数>）";

    // 3. 调 Manager 干活，命令层不写业务逻辑
    if (!XxxManager.Instance.DoSomething(value)) return $"执行失败：…（{value}）";

    return $"已…：{value}";
}
```

约定：

- 返回值就是游戏内控制台的输出；成功用「已…」开头，失败用「执行失败：…」/「…失败：…」开头。
- 纯无参命令直接干活，不用写 `args` 判断。
- 数据 / 状态一律走现有 Manager，不在命令层访问 Store 或直接改字段。
- 不要用 `Debug.Log` 当输出（返回值才是输出）。需要排查细节时可以 `Debug.LogWarning`，方便在 Unity Console 看。
- 不需要 `#if UNITY_EDITOR`（只有控制台入口面板是编辑器专用）。

## 修改

按改的内容决定动哪几处，**永远包括文档**：

| 改什么 | 要动的地方 |
|---|---|
| 命令名 / 参数个数 | `Register` 的 pattern + handler 里 `args` 的下标 + 文档的用法与示例 |
| 参数含义 / 格式 | handler 的校验与提示 + 文档的用法说明 |
| 行为 / 输出文案 | handler（和新调用的 Manager）+ 文档「作用」一栏 |
| 只改说明文本 | `Register` 的说明 + 文档「作用」一栏 |

注意：

- 改 pattern 段数会让之前的输入不再命中，改了就要在文档里写清新用法。
- 保留现有的失败文案风格，别另起一套。
- 不要顺手改动不相关的命令。

## 删除

1. 删掉 `RegisterAll()` 里对应的 `Register` 行。
2. 删掉 handler；**先 `grep` handler 名**，确认没有别处引用（被别的命令复用就保留）。
3. 文档里删掉命令表的行、对应的用法说明、以及示例里用到它的那条；示例删空了就补一个别的。
4. `grep` 一遍命令名 / pattern，确认代码和文档里都没有残留（包括注释）。

## 参数是牌 id 时

格式以 `docs/Console.md` 为准，速查：

```
普通牌：N{花色}_{点数}               例 N0_1、N02_0jk
特殊牌：S{花色}_{点数}_{配表 id}      例 S_m_0200001、S__0300001
花色：0 红心 / 1 方块 / 2 黑桃 / 3 梅花（空 = 无花色）
点数：1-9 = A-9、0 = 10、j/q/k、s = -2、m = -1（空 = 无点数）
点数最多 5 个（含主点数），超了会被拒绝
```

校验一律交给 `CardIdCodec` / `CardFactory`，命令层不要自己拆 id。

## 自检

- [ ] `get_errors` 无错误
- [ ] pattern 段数 = 用法里的参数个数
- [ ] 参数缺失 / 非法时有可读的失败提示
- [ ] 业务逻辑在 Manager 里，命令层只做校验 + 转发
- [ ] `docs/Console.md` 的命令表、用法、示例都已同步（新增 / 修改 / 删除同理）
- [ ] 删除时没有残留引用（代码 + 文档 + 注释）
- [ ] 编辑器里实际输入一次命令（含失败情况），文案符合预期
