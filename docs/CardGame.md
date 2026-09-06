# 纸牌游戏架构设计（蜘蛛纸牌）

> 用法：在 VS Code 中打开本文件，点右上角「预览」图标，或 `Ctrl+Shift+V` / `Ctrl+K V` 查看渲染后的类图。

## 架构分层

采用 MVC 分层：数据层（Store）→ 逻辑层（Manager）→ 控制层（Controller）→ 表现层（View）。
数据变化通过事件中心 `EventManager` 派发，表现层订阅事件做「拉取式刷新」。

| 层 | 职责 | 类 |
|----|------|----|
| 数据层 Model | 场上卡牌纯数据，不引用 UnityEngine 对象 | `CardsStore`、`CardData`、`E_CardSuitEnum` |
| 逻辑层 Manager | 组牌/洗牌/发牌/移动、规则判定、资源加载、对象池 | `CardsManager`、`CardRuleManager`、`CardResManager`、`CardPoolManager` |
| 表现层 View | 创建/摆放/回收 CardView，订阅事件刷新 | `CardViewManager`、`CardView` |
| 控制层 Controller | 拖拽、点击发牌堆等输入交互 | `CardController`、`DrawPileController` |
| 配表 | Excel 驱动卡牌数据（自动生成 .cs + .json） | `CardConfig`、`ConfigHelper`、`ConfigExporter` |
| 门面 / 入口 | 模块初始化编排、场景入口、UI 入口 | `CardGameModule`、`CardGameEntry`、`GameEntry` |

## 类图

```mermaid
classDiagram
    direction TB

    %% ============ 数据层（Model） ============
    class E_CardSuitEnum {
        <<enumeration>>
        Hearts = 0
        Clubs = 1
        Spades = 2
        Diamonds = 3
    }
    class CardData {
        +string id
        +E_CardSuitEnum suit
        +int rank
        +bool isFaceUp
        +bool isSingleGrab
    }
    class CardsStore {
        +int columnCount
        +int pocketCount
        +Stack~CardData~ drawPile
        +List~List~CardData~~ columns
        +List~CardData~ discardPile
        +List~CardData~ pockets
        +Clear()
        +Refresh()
    }

    %% ============ 逻辑层（Manager） ============
    class CardsManager {
        +bool isTestMode
        +StartNewGame(columnCount, pocketCount)
        +DealFromDrawPile()
        +MoveCards(draggedCards, targetColumnIndex)
        +MoveToPocket(card, pocketIndex)
        +MoveFromPocket(pocketIndex, targetColumnIndex)
    }
    class CardRuleManager {
        +CanDrag(anchor) bool
        +IsSpider(card) bool
        +IsBlack(card) bool
        +GetDraggableCards(anchor) List~CardData~
        +CanMove(draggedCards, targetColumnIndex) bool
        +CanDeal() bool
        +GetCompletedSequence(column) List~CardData~
        +CanMoveToPocket(card, pocketIndex) bool
        +CanMoveFromPocket(pocketIndex, targetColumnIndex) bool
        +FindColumnIndex(card) int
        +FindPocketIndex(card) int
    }
    class CardPoolManager {
        +GetCard() CardView
        +GetCardAsync(onGot)
        +Recycle(view)
    }
    class CardResManager {
        +Preload()
        +GetBackSprite() Sprite
        +GetCardConfig(id) CardConfig
        +GetCardImage(imagePath) Sprite
    }

    %% ============ 表现层（View） ============
    class CardViewManager {
        +GetView(data) CardView
        +GetColumnIndexAt(worldPos) int
        +GetPocketIndexAt(worldPos) int
    }
    class CardView {
        +CardData data
        +int columnIndex
        +Bind(data)
        +Unbind()
        +SetPosition(pos)
        +SetSortingOrder(order)
        +Refresh()
    }

    %% ============ 控制层（Controller） ============
    class CardController {
        <<MonoBehaviour>>
        拖拽交互：起拖/跟手/落点判定/回弹
    }
    class DrawPileController {
        <<MonoBehaviour>>
        点击发牌堆 → DealFromDrawPile()
    }

    %% ============ 配表 ============
    class CardConfig {
        +string Id
        +string Image
        +int Suit
        +int Rank
        +bool SingleGrab
    }

    %% ============ 门面 / 入口 ============
    class CardGameModule {
        +StartNewGame(columnCount, pocketCount)
        +OnInit()
        +OnDispose()
    }
    class CardGameEntry {
        <<MonoBehaviour>>
        +Start()
        +OnDestroy()
    }
    class GameEntry {
        <<MonoBehaviour>>
        +Start()
    }

    %% ============ 框架基础设施 ============
    class EventManager {
        <<框架>>
    }
    class PoolManager {
        <<框架>>
    }
    class ResManager {
        <<框架>>
    }
    class ConfigHelper {
        <<框架>>
    }

    %% ============ 关系 ============
    CardsStore o-- CardData : 持有
    CardData --> E_CardSuitEnum : 花色

    CardsManager --> CardsStore : 读写
    CardsManager ..> CardRuleManager : 规则判定
    CardsManager ..> ConfigHelper : 读配表
    CardsManager ..> EventManager : 派发

    CardRuleManager --> CardsStore : 读数据

    CardPoolManager ..> PoolManager : 委托
    CardResManager ..> ResManager : 委托
    CardResManager ..> ConfigHelper : 读配表
    CardResManager ..> CardConfig : 使用

    CardViewManager --> CardsStore : 读数据
    CardViewManager --> CardPoolManager : 取牌/回收
    CardViewManager o-- CardView : 管理
    CardViewManager ..> EventManager : 订阅

    CardView --> CardData : 持有
    CardView ..> CardResManager : 取整图/牌背

    CardController ..> CardRuleManager : 判定合法牌串
    CardController ..> CardViewManager : 落点查询
    CardController ..> CardsManager : 执行移动
    DrawPileController ..> CardsManager : 发牌

    CardGameModule --> CardsStore : 初始化
    CardGameModule --> CardResManager : 初始化
    CardGameModule --> CardPoolManager : 初始化
    CardGameModule --> CardViewManager : 初始化
    CardGameModule --> CardRuleManager : 初始化
    CardGameModule --> CardsManager : 初始化

    CardGameEntry --> CardGameModule : Init / StartNewGame(11, 3)
    GameEntry ..> UIManager : ShowAsync~BeginPanel~
```

## 规则说明

### 花色

`E_CardSuitEnum` 枚举值恒等于花色图片索引、配表 `Suit`、id 花色位：

| 值 | 花色 |
|----|------|
| 0 | 红心 Hearts |
| 1 | 梅花 Clubs |
| 2 | 黑桃 Spades |
| 3 | 方块 Diamonds |

### 点数

`rank` 取值含义：

| rank | 含义 |
|------|------|
| 1-13 | 普通牌，1=A，13=K |
| -1 | 蜘蛛牌（同花色可连接） |
| -2 | 黑色牌（无花色无点数） |

### 特殊牌

| 类型 | rank | 规则 |
|------|------|------|
| 蜘蛛牌 | -1 | 参与连接时同花色即可（不要求逐张减 1）；不参与 A-K 顺子判定 |
| 黑色牌 | -2 | 无花色无点数，不能与任何牌连接；只能放空列；单抓 |

### 单抓属性 `isSingleGrab`

通用属性：**只能单独拖起单张，且必须位于列顶**（被压住时不可拖）。由配表 `SingleGrab` 决定，`CreateCard` 时读取：

- `SingleGrab = true` → 这张牌 `isSingleGrab = true`，拖拽走单抓分支
- `SingleGrab = false` → 正常整串拖拽

黑色牌只是该属性的一个使用方（配 `true`），未来其他特殊牌甚至普通牌都可复用。
注意：黑色牌「不能连接」「只能放空列」仍由 `IsBlack` 专属判定，不属于单抓属性。

## 配表约定

`CardConfig.xlsx`（`Config/Excel/`）由 `Tools/Export All Configs` 导出为 `Assets/Resources/Config/CardConfig.json` + `Assets/Scripts/Config/CardConfig.cs`。

| 字段 | 类型 | 说明 |
|------|------|------|
| Id | string | 主键，带前导零 |
| Image | string | 整张牌图文件名（相对 `Resources/GamePlay/Card/`，不含扩展名） |
| Suit | int | 花色 0-3 |
| Rank | int | 点数 1-13；-1 蜘蛛牌；-2 黑色牌 |
| SingleGrab | bool | 单抓属性 |

### id 编码

- 普通牌：`01` + 花色两位（00-03）+ 点数三位（001-013），如 `0100001` = 红心 A
- 特殊牌：`02` + 5 位编号，如 `0200005` = 黑色牌

### Excel 格式约定

- 第 1 行：字段名（空字段名的列为注释列，跳过）
- 第 2 行：类型（int / string / float / bool）
- 第 3 行：注释
- 第 4 行起：数据

## 事件一览（`E_EventEnum`）

| 事件 | 参数 | 含义 |
|------|------|------|
| OnTableChanged | 无 | 整桌刷新（发牌 / 重开） |
| OnColumnChanged | int 列索引 | 该列整列重建 |
| OnColumnAppend | int 列索引 | 该列末尾增量追加一张 |
| OnDrawPileChanged | int 数量 | 发牌堆剩余数量 |
| OnDiscardPileChanged | int 数量 | 弃牌堆数量 |
| OnPocketChanged | int 牌包索引 | 牌包变化 |
| OnCardChanged | CardData | 单张牌变化（如翻牌） |

## 资源目录

- 牌背、整张牌图统一放 `Assets/Resources/GamePlay/Card/`（`cardBack` + 各牌整图）
- 卡牌预制体：`Resources/Prefab/GamePlay/Card/CardPrefab`
- 配表 JSON：`Assets/Resources/Config/`


