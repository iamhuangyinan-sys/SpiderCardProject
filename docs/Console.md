# 控制台（调试工具）

编辑器模式下的运行时命令行，用于快速验证游戏逻辑。

## 结构与流程

```
GameEntry.InitGame()  ──[#if UNITY_EDITOR]──▶  Show<ConsoleEntryPanel>()
                                                     │ 点 btnConsoleEntry
                                                     ▼
                                        Hide(ConsoleEntryPanel)
                                        Show<ConsolePanel>        ← 输入命令
                                                     │ 点 btnClose
                                                     ▼
                                        Hide(ConsolePanel)
                                        Show(ConsoleEntryPanel)
```

- 两个面板都注册在 `E_UILayerEnum.Top`，预制体放在 `Resources/Prefab/UI/Console/`。
- 仅编辑器模式（`#if UNITY_EDITOR`）在启动时显示入口；`UICanvas` 为 `DontDestroyOnLoad`，切场景后入口仍在。

## 面板脚本

| 脚本 | 控件（UIBinder 前缀） | 作用 |
|------|----------------------|------|
| `ConsoleEntryPanel` | `btn_ConsoleEntry` | 点击 → 隐藏自己 + `Show<ConsolePanel>()` |
| `ConsolePanel` | `ipt_Console` / `btn_Ok` / `btn_Close` | 输入命令行，确定或回车执行；关闭 → 隐藏自己 + `Show<ConsoleEntryPanel>()` |

命令执行结果通过 `Debug.Log("[Console] > 输入\n输出")` 输出，未知命令为 `Debug.LogWarning`。

## 命令系统

`ConsoleCommandManager`（`Assets/Scripts/Console/`，`ManagerBase`）：

```csharp
// 注册
ConsoleCommandManager.Instance.Register("level skip", args => "ok", "说明");

// 执行（返回是否命中）
ConsoleCommandManager.Instance.Execute("level skip", out string output);
```

- 命令行按空格切分为「段」，段数一致且每段都匹配才算命中。
- 每段支持 `*` 通配符（大小写不敏感）：`*` 整段任意、`lv*` 段内通配、`*a*` 包含匹配。
- 回调签名 `string Handler(string[] args)`，`args` 是完整分段（含命令本身），返回值即输出文本。

| 写法 | 匹配 |
|------|------|
| `level skip` | 精确匹配 `level skip` |
| `card add *` | `card add 0100001`，回调里 `args[2]` 即 id |
| `lv* skip` | `lv1 skip` / `lv2 skip` |

## 命令注册

所有命令统一写在 `ConsoleCommandRegistry.RegisterAll()`（由 `ConsoleCommandManager.OnInit` 调用），新增命令加一行 `Register` 即可：

```csharp
public static void RegisterAll()
{
    var cmd = ConsoleCommandManager.Instance;

    cmd.Register("level skip", CmdLevelSkip, "直接完成当前关卡");

    // 示例：cmd.Register("card add *", CmdCardAdd, "向场上添加一张牌（* = 牌 id）");
}
```

## 已有命令

| 命令 | 行为 |
|------|------|
| `level skip` | 直接完成当前关卡：调用 `CardGameModule.DebugCompleteLevel()`，走正常收牌结算并弹选关 |

前置校验（不满足时直接返回失败文本）：

- `CardGameEntry.IsInCardScene` —— 是否在卡牌场景
- `CardsManager.IsPlaying` —— 是否正在一局对局中
- `CardsManager.IsSettling` —— 是否正在结算收牌
