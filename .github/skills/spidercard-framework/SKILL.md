---
name: spidercard-framework
description: 'SpiderCard Unity 项目框架使用指南。Use when developing business code for this project: 添加 Store/Manager/Helper、写 UI 面板/模组/列表项、事件、存档、资源加载、对象池、音频、配表等。关键词：Mgr/Store、BasePanel、UIBinder、comps、mod_xxx、ScrollList、SaveManager、EventManager、ResManager、PoolManager、ConfigHelper。'
---

# SpiderCard 框架使用指南

Unity 项目 `SpiderCardProject` 的业务开发框架。所有框架代码在 `Assets/Scripts/Framework/`，业务代码在 `Assets/Scripts/` 下对应文件夹。

## 核心分层原则

| 层 | 职责 | 基类 | 命名 |
|----|------|------|------|
| **Store** | 纯数据存储，不写逻辑 | `StoreBase<T>` | `XxxStore` |
| **Manager** | 业务逻辑，操作 Store | `ManagerBase<T>` | `XxxManager` |
| **Helper** | 纯静态工具函数 | 无（static class） | `XxxHelper` |
| **UI** | 界面层，调 Manager | `NormalPanel`/`PopupPanel` | `XxxPanel` |

数据流：`UI → Mgr → Store`。UI 不直接碰 Store 数据，Manager 不直接碰数据本身。

## 命名规范（重要）

- 枚举统一 `E_xxx` 前缀，如 `E_EventEnum`、`E_UILayerEnum`、`E_SaveBasicEnum`
- 字段名 `camelCase`（首字母小写），类名 `PascalCase`
- 面板 `XxxPanel`，UI 模组 `XxxModule`，列表项 `XxxItem`

## 启动入口 FrameworkEntry

所有框架模块在 `FrameworkEntry.InitFramework()` 中按依赖顺序初始化。新增 Manager/Store 时在此登记：

```csharp
// Store（无依赖）→ Manager（依赖 Store）顺序
XxxStore.Instance.Init();
XxxManager.Instance.Init();
```

销毁按反序 `Dispose()`。

---

## 模块速查

### 1. 单例（Framework/Singleton/）

- `Singleton<T>`：普通 C# 类单例，Manager/Store 基类使用。`Init()`/`Dispose()` 生命周期。
- `MonoSingleton<T>`：MonoBehaviour 单例，只在需要挂 GameObject 时用（如 MonoManager）。

```csharp
public class CardManager : ManagerBase<CardManager> { }
public class CardStore : StoreBase<CardStore> { }
```

### 2. MonoManager（协程/Update 服务）

框架唯一的 MonoBehaviour，为纯 C# Manager 提供协程和 Update：

```csharp
MonoManager.Instance.StartCoroutine(MyCoroutine());
MonoManager.Instance.OnUpdate += MyUpdate;   // 记得在 Dispose 里 -= 注销
```

### 3. 事件中心（Framework/Event/）

- 枚举在 `EventEnum.cs` 的 `E_EventEnum` 中定义（参数类型写注释）。
- `EventManager` 提供 `AddListener` / `RemoveListener` / `Dispatch`。

```csharp
// 注册（OnOpen 或 Start）
EventManager.Instance.AddListener<int>(E_EventEnum.OnScoreChanged, OnScoreChanged);
// 取消（OnClose 或 OnDestroy，必须配对）
EventManager.Instance.RemoveListener<int>(E_EventEnum.OnScoreChanged, OnScoreChanged);
// 派发（任何地方）
EventManager.Instance.Dispatch(E_EventEnum.OnScoreChanged, 999);
```

### 4. 资源管理（Framework/Res/）

- `ResManager` 纯逻辑，`ResStore` 存缓存。引用计数 + 字典缓存。

```csharp
var prefab = ResManager.Instance.Load<GameObject>("UI/CardPanel");
ResManager.Instance.LoadAsync<Sprite>("Card/SpadeA", sprite => { ... });
ResManager.Instance.UnloadAsset<GameObject>("UI/CardPanel", isDel: true);
```

### 5. 本地存档（Framework/Save/）

- 枚举在 `SaveEnum.cs`：`E_SaveBasicEnum`（基础类型，共用一个 basic.json）、`E_SaveCustomEnum`（自定义类型，每个 key 一个 JSON）。
- 注册集中在 `SaveRegistry.RegisterAll()`（由 FrameworkEntry 调用）。

```csharp
// 注册
SaveManager.Instance.RegisterBasic(E_SaveBasicEnum.HighScore, 0);
SaveManager.Instance.RegisterCustom(E_SaveCustomEnum.PlayerData, "player_data");

// 读写
SaveManager.Instance.Save(E_SaveBasicEnum.HighScore, 8888);
int score = SaveManager.Instance.Load<int>(E_SaveBasicEnum.HighScore);
SaveManager.Instance.Save(E_SaveCustomEnum.PlayerData, data);
var data = SaveManager.Instance.Load<PlayerData>(E_SaveCustomEnum.PlayerData);
```

- 基础类型枚举名存入 JSON，改名需 `MigrateBasicKey("旧名", 新枚举)`；自定义类型改文件名用 `MigrateCustomPath("旧文件", 新枚举)`。

### 6. 对象池（Framework/Pool/）

```csharp
PoolManager.Instance.SpawnAsync("Card/CardPrefab", card => { ... });
PoolManager.Instance.Despawn(gameObject);
PoolManager.Instance.ClearOnSceneChange();  // 切场景清池
```

- 预制体挂 `PoolConfig`：`destroyOnSceneLoad`（切场景是否清空）、`preloadCount`、`maxSize`。

### 7. 音频（Framework/Audio/）

```csharp
BgmManager.Instance.Play("MainTheme");     // 自动拼 Audio/BGM/ 前缀
BgmManager.Instance.Pause();
BgmManager.Instance.SetVolume(0.5f);

SoundManager.Instance.Play("CardFlip");     // 自动拼 Audio/Sound/ 前缀
SoundManager.Instance.Play("CardFlip", 0.5f);
SoundManager.Instance.SetVolume(0.8f);
```

- BGM 放 `Resources/Audio/BGM/`，音效放 `Resources/Audio/Sound/`。
- 音效预制体 `Resources/FrameworkPrefab/Sound/Sound` 挂 `SoundPlayer` + `AudioSource` + `PoolConfig`。

### 8. 场景切换（Framework/SceneController.cs）

```csharp
SceneController.Instance.LoadScene("GameScene");
SceneController.Instance.OnBeforeLoad += () => { /* 遮罩淡入 */ };
SceneController.Instance.OnAfterLoad += () => { /* 遮罩淡出 */ };
```

---

## UI 架构（Framework/UI/）

### 面板基类

| 类 | 用途 |
|----|------|
| `BasePanel` | 所有面板基类，生命周期 OnOpen/OnShow/OnHide/OnClose（抽象，必须实现） |
| `NormalPanel` | 普通面板 |
| `PopupPanel` | 弹窗（栈管理，`CloseTopPopup()`/`CloseAllPopups()`） |

### 生命周期

```
首次 Show<T>() → OnOpen
Hide(panel)    → OnHide
再次 Show<T>() → OnShow
Close(panel)   → OnClose（销毁）
```

### 面板注册与显示

1. 面板注册在 `UIConfig.RegisterAll()`：`Register<MainPanel>("MainPanel", E_UILayerEnum.Normal);`
   - 路径自动拼 `Prefab/UI/` 前缀，预制体放 `Resources/Prefab/UI/`。
2. 显示：

```csharp
UIManager.Instance.ShowAsync<MainPanel>(panel => { ... });
UIManager.Instance.Hide(panel);   // 隐藏（保留缓存）
UIManager.Instance.Close(panel);  // 关闭（销毁）
```

### UIBinder 控件绑定

面板/模组/列表项根节点挂 `UIBinder`，子物体按前缀命名，编辑器点 **Refresh** → **Export Code** 生成 `.gen.cs`。

| GameObject 前缀 | 绑定类型 | 示例 |
|-----------------|---------|------|
| `btn_` | `Button` | `btn_Start` → `btnStart` |
| `txt_` | `TMP_Text` | `txt_Title` → `txtTitle` |
| `img_` | `Image` | `img_Icon` → `imgIcon` |
| `mod_` | `BaseModule` 子类 | `mod_Button` → `modButton` |
| `tog_` | `Toggle` | `tog_Mute` → `togMute` |
| `sld_` | `Slider` | `sld_Volume` → `sldVolume` |
| `list_` | `ScrollList` | `list_Shop` → `listShop` |

- 导出后生成 `XxxPanel.gen.cs`，内含 `comps` 字段和 `CompsData` 类（继承 `CompsDataBase`）。
- 控件名不能重复，改了子物体名要重新 Refresh + Export Code。
- 业务代码用 `comps.xxx` 访问控件。

### UI 模组（复用 UI 组合）

1. 写类 `public class ButtonModule : BaseModule { }`，重写 `OnBind()`/`OnUnBind()`（抽象，必须实现）。
2. 模组预制体根节点挂 `ButtonModule` + `UIBinder`，子控件按前缀命名。
3. 面板中用 `mod_Button` 命名该物体，导出后面板 `comps.modButton` 类型是 `ButtonModule`。

```csharp
// 访问模组内控件
comps.modButton.comps.txtLabel.text = "开始";
```

### 滚动列表（Framework/UI/ScrollList/）

- `ScrollList` 挂 Scroll View 上，`BaseListItem` 是 Cell 基类（OnActivate/OnRecycle 抽象）。
- Cell 预制体挂子类（`XxxItem : BaseListItem`）+ `UIBinder`。

```csharp
comps.listShop.OnItemRender = (index, item) =>
{
    var cell = item as ShopItem;
    cell.comps.txtName.text = dataList[index].Name;
};
comps.listShop.ItemCount = dataList.Count;   // 设值触发刷新
comps.listShop.ScrollTo(10);
```

- ScrollRect 配置：Horizontal 取消勾选、Vertical 勾选；Content 锚点 Top-Stretch、Pivot(0.5,1)；Item 锚点 Top-Left(0,1)、Pivot(0,1)（多列）。

---

## 配表系统

### Excel → JSON + C# 类

- Excel 放项目根 `Config/Excel/`，`.xlsx` 格式。
- 格式约定（行）：
  - 第1行：字段名（空字段名的列为注释列，跳过）
  - 第2行：类型（`int` / `string` / `float` / `bool`）
  - 第3行：注释
  - 第4行起：数据
- 主键字段名固定为 `Id`，类型用 `string`（支持带前导零的 id）。
- Unity 菜单 `Tools/Export All Configs` 生成：
  - `Assets/Resources/Config/Xxx.json`
  - `Assets/Scripts/Config/Xxx.cs`（数据类，自动生成，勿手动改）

### 运行时读取（ConfigHelper）

```csharp
ConfigHelper.LoadAll(); // 可选：启动时调用一次

var card = ConfigHelper.Get<CardConfig>("0100001"); // 按 string 主键查
var all = ConfigHelper.GetAll<CardConfig>(); // 拿全部

// 首次 Get/GetAll 会按类型名自动懒加载 Resources/Config/{TypeName}.json
```

---

## 开发新业务模块的步骤

1. **数据**：新建 `Assets/Scripts/Store/XxxStore.cs` 继承 `StoreBase<XxxStore>`。
2. **逻辑**：新建 `Assets/Scripts/Mgr/XxxManager.cs` 继承 `ManagerBase<XxxManager>`。
3. **工具**：按需加 `Assets/Scripts/Helper/XxxHelper.cs`（static）。
4. **登记**：在 `FrameworkEntry.InitFramework()` 中 Init/Dispose。
5. **UI**：面板继承 `NormalPanel`/`PopupPanel`，在 `UIConfig` 注册，挂 `UIBinder` 绑定控件。

---

## 新增框架模块（计时器、动画等）

在 `Assets/Scripts/Framework/` 下新建文件夹（如 `Timer/`），沿用约定：
- 数据层 `XxxStore : StoreBase`，逻辑层 `XxxManager : ManagerBase`。
- 新增后**更新本 Skill 文档**，在「模块速查」加一节。
