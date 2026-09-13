# 结题报告配图（Mermaid）

> 用法：VS Code 打开本文件按 `Ctrl+K V` 预览，或把每段 ```mermaid 代码块粘到 https://mermaid.live 导出 PNG/SVG，再插入报告对应位置。

| 图号 | 内容 | 插入位置（报告章节） |
|------|------|----------------------|
| 图3-1 | 框架模块图 | 3.2 系统功能 |
| 图3-2 | 卡牌玩法 MVC 分层图 | 3.3 主要类图 |
| 图4-1 | UI 类架构图（面板基类 / 绑定 / 模组 / 列表） | 4.1 界面设计 |
| 图4-2 | 控件绑定与 UI 复用链路图 | 4.1 界面设计 |
| 图4-3 | 拖拽落牌流程图 | 4.2 流程图 |

---

## 图3-1 框架模块图

```mermaid
flowchart TB
    Entry["FrameworkEntry<br/>框架入口：常驻场景，按依赖顺序初始化 → 反序销毁"]

    A1["ResManager / ResStore<br/>资源加载 · 引用计数"]
    A2["PoolManager / PoolStore<br/>对象池（预生成 / 上限 / 切场景清理）"]
    A3["EventManager<br/>事件中心（枚举 Key + 订阅 / 派发）"]
    A4["UIManager / UIConfig<br/>面板层级 · 生命周期 · 场景遮罩"]
    A5["SaveManager<br/>本地 JSON 存档"]
    A6["BgmManager / SoundManager<br/>背景音乐 / 音效"]
    A7["SceneController<br/>异步切场景与过渡"]
    A8["ConfigHelper<br/>配表读取"]
    A9["ConsoleCommandManager<br/>运行时调试命令"]

    Mono["MonoManager（框架中唯一的 MonoBehaviour）<br/>为纯 C# 模块代理协程与逐帧回调"]

    Game["玩法层<br/>CardGameModule 门面 + CardGameEntry 场景入口"]

    Entry --> A1
    Entry --> A2
    Entry --> A3
    Entry --> A4
    Entry --> A5
    Entry --> A6
    Entry --> A7
    Entry --> A8
    Entry --> A9

    A1 -.-> Mono
    A2 -.-> Mono
    A4 -.-> Mono

    Game -->|"使用框架能力"| A3
    Game --> A4
    Game --> A5
    Game --> A8
```

框架层只提供基础能力、不出现任何纸牌概念；初始化顺序即依赖顺序，销毁反序进行。

---

## 图3-2 卡牌玩法 MVC 分层图

```mermaid
flowchart TB
    subgraph VIEW["表现层 / 控制层（View · Controller）"]
        V1["CardViewManager<br/>订阅事件：创建 / 摆放 / 回收卡牌"]
        V2["CardView<br/>只把绑定到的数据画出来"]
        V3["CardController<br/>拖拽输入：起拖 / 跟手 / 落点判定"]
        V4["CardAnimationHelper<br/>动画封装与时长、层级常量"]
    end

    subgraph LOGIC["逻辑层（Manager · Rule）"]
        M1["CardsManager<br/>流程编排：组牌 / 发牌 / 移动 / 回收 / 洗回 / 结算"]
        M2["CardRuleManager<br/>纯规则判定：能否拖起 / 能否落列 / 是否成顺"]
    end

    subgraph DATA["数据层（Store）"]
        D1["CardsStore<br/>发牌堆 / 弃牌堆 / 各列 / 牌包"]
        D2["CardData<br/>单张牌数据（含单抓、任意落点、落牌技能、沉底等规则属性）"]
    end

    V3 -->|"判定合法性"| M2
    V3 -->|"执行移动"| M1
    M1 -->|"修改数据"| D1
    M2 -->|"只读"| D1
    M1 -->|"派发事件"| V1
    D1 -->|"派发事件"| V1
    V1 --> V2
    V1 --> V4
    V2 -.->|"只读，不修改数据"| D2
```

依赖方向单向：输入 / 界面 → 逻辑 → 数据；表现层通过事件被动刷新，不反向调用逻辑层。

---

## 图4-1 UI 类架构图

```mermaid
classDiagram
    direction TB

    %% ===== 管理层 =====
    class UIManager {
        <<框架 ManagerBase>>
        +Show~T~() / ShowAsync~T~()
        +Hide(panel) / Close(panel)
        +HideAll() / CloseAll()
        +FadeInSceneMask() / FadeOutSceneMask()
    }
    class UIConfig {
        <<静态注册表>>
        +RegisterAll()
        +Register~T~(prefabPath, layer)
    }
    class E_UILayerEnum {
        <<enumeration>>
        Bottom = 0
        Normal = 100
        Popup = 200
        Top = 300
        System = 400
    }

    %% ===== 面板基类 =====
    class BasePanel {
        <<abstract MonoBehaviour>>
        +bool IsOpen
        #CanvasGroup CanvasGroup
        #OnOpen() / OnShow() / OnHide() / OnClose()
        #OnPanelOpened() / OnPanelClosing()
        +HideSelf() / CloseSelf()
    }
    class NormalPanel {
        <<abstract>>
        #FlyInOnOpen / FlyOutOnClose
        #FadeInOnOpen / FadeOutOnClose
    }
    class FullScreenPanel {
        <<abstract>>
        +static bool IsBlockingInput
        打开期间锁住场景输入
    }
    class PopupPanel {
        <<abstract>>
        弹窗栈：CloseTopPopup()
    }

    %% ===== 控件绑定 =====
    class UIBinder {
        <<MonoBehaviour>>
        +Bind()
        按前缀扫描子物体
    }
    class CompsDataBase {
        <<生成代码基类>>
        XxxPanel.gen.cs 内的 CompsData
    }

    %% ===== 模组与列表（两类复用单元）=====
    class BaseModule {
        <<abstract MonoBehaviour>>
        #OnBind() / #OnUnBind()
        Awake 时自行 UIBinder.Bind()
    }
    class ScrollList {
        <<MonoBehaviour>>
        +int ItemCount
        +OnItemRender(index, item)
        +ScrollTo(index)
        内部 ListItemPool 虚拟滚动
    }
    class BaseListItem {
        <<abstract MonoBehaviour>>
        #OnActivate() / #OnRecycle()
    }

    %% ===== 关系 =====
    UIManager --> UIConfig : 查注册表
    UIManager --> E_UILayerEnum : 控制层级排序
    UIManager o-- BasePanel : 缓存 · 显示 · 隐藏
    UIConfig ..> BasePanel : 类型 → 预制体 + 层级

    BasePanel <|-- NormalPanel : 声明式开关动画
    BasePanel <|-- FullScreenPanel : 锁场景输入
    BasePanel <|-- PopupPanel : 弹窗栈

    BasePanel o-- UIBinder : 根节点持有
    BaseModule o-- UIBinder : 自行绑定
    BaseListItem o-- UIBinder : Cell 绑定
    UIBinder ..> CompsDataBase : Export Code 生成

    ScrollList o-- BaseListItem : 池化复用
```

`BasePanel` 统一生命周期（Open / Show / Hide / Close），在其上派生三种面板：普通面板只声明开关动画、全屏面板打开时锁场景输入、弹窗面板参与弹窗栈；`UIBinder` 是面板 / 模组 / 列表项共用的绑定入口，`BaseModule`（模组）与 `ScrollList + BaseListItem`（列表）是两类可复用的 UI 单元。

---

## 图4-2 控件绑定与 UI 复用链路图

```mermaid
flowchart TB
    subgraph NAME["① 命名约定（预制体子物体）"]
        direction LR
        N1["btn_Start → Button"]
        N2["txt_Title → TMP_Text"]
        N3["img_Icon → Image"]
        N4["mod_Button → BaseModule 子类"]
        N5["list_Shop → ScrollList"]
        N6["tog_ / sld_ / ipt_ → Toggle / Slider / InputField"]
    end

    subgraph BIND["② 绑定与生成"]
        direction LR
        B1["UIBinder<br/>挂在面板 / 模组 / 列表项根节点，Bind() 时按前缀扫描子物体"]
        B2["编辑器：Refresh → Export Code"]
        B3["XxxPanel.gen.cs<br/>CompsData 字段（继承 CompsDataBase）"]
        B1 --> B2 --> B3
    end

    subgraph USE["③ 业务访问（不手写 GetComponent）"]
        direction LR
        U1["面板：comps.btnStart / comps.txtTitle"]
        U2["模组：comps.modButton.comps.imgIcon"]
        U3["列表：listShop.OnItemRender = (index, item) => item.Bind(...)"]
    end

    NAME --> B1
    B3 --> U1
    B3 --> U2
    B3 --> U3

    U2 -.->|"复用单元 A"| MOD["BaseModule：模组自带 UIBinder，Awake 自动绑定<br/>用于可复用 UI 组合（如关卡按钮 LevelBtnModule）"]
    U3 -.->|"复用单元 B"| LIST["ScrollList + BaseListItem：虚拟滚动 + 对象池<br/>用于长列表（商店商品 ShopCardItem、展示牌 ShowCardItem）"]
```

控件不靠 `GetComponent` 手动查找，而是「命名约定 → 自动绑定 → 生成代码 → `comps.xxx` 访问」；模组与列表分别把「可复用的 UI 组合」和「长列表」封装起来，面板只负责组织它们。

---

## 图4-3 拖拽落牌流程图

```mermaid
flowchart TB
    S(["鼠标按下"]) --> P{"CanCardInput<br/>对局中 · 未结算 · 无动画 · 无全屏面板"}
    P -- 否 --> Z0["忽略输入"]
    P -- 是 --> A["物理射线取命中牌，取渲染最靠前的一张"]
    A --> B{"该牌正面朝上？"}
    B -- 否 --> Z1["不响应"]
    B -- 是 --> C["CardRuleManager.GetDraggableCards<br/>收集锚点及其下方牌串"]
    C --> D{"整串翻开且同花色逐张减一？"}
    D -- 否 --> Z2["拖不起来：阻塞牌闪灰提示"]
    D -- 是 --> E["开始拖拽：跟随鼠标<br/>临时抬高渲染层级 + 发光 / 投影"]
    E --> F(["松开鼠标"])
    F --> G["判定落点：牌包 or 目标列"]
    G --> H{"目标是否合法？"}
    H -- 否 --> I["回弹：恢复原位置与层级"]
    H -- 是 --> J["CardsManager 修改数据<br/>派发 OnColumnChanged / OnCardToDiscard 等事件"]
    J --> K["CardViewManager 重建受影响列<br/>播放位移动画 / 翻牌动画"]
    K --> L(["结束：数据与画面一致"])
    I --> L
```

数据只被逻辑层修改一次，表现层只负责「读数据画画面」；动画未结束时输入保持锁定。
