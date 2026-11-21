# SpiderCard 关卡内系统

本文用于定位一关打牌从开局到收牌的代码。牌的静态定义、整局牌组、关卡地图、商店与存档见 [关卡外系统](CardRun.md)。通用框架见 [框架指南](Framework.md)。

## 牌桌概念

- **列**是接龙区域。列末尾是可操作的一端，从翻开的牌开始可以尝试拖起一串牌。
- **牌包**是单张牌的暂存槽，和列之间的移动也走牌桌规则。
- **发牌堆**提供开局和后续发牌。**弃牌堆**接收已回收的牌，并可在发牌堆耗尽时洗回。牌堆查看与发牌操作各有入口。
- **连接、落列、完成接龙**是三种判定。连接决定一串牌能否一起拖起，落列检查目标列，完成接龙识别列末尾完整的同花 K→A。回收牌与计入接龙进度由牌局流程分别处理。
- **落牌技能、翻牌技能、牌局属性与 Buff**在当前牌局中生效。牌本身携带技能和自带属性，牌局还会管理临时属性及独立的 Buff 状态。

## 代码位置

下表路径相对于 [`Assets/Scripts/`](../Assets/Scripts/)。

| 职责 | 入口 |
| --- | --- |
| 场景和业务生命周期 | `GamePlay/Card/CardGameEntry.cs` 管理牌桌可见性和界面切换，`GamePlay/Card/CardGameModule.cs` 统一初始化和释放业务系统，也是开局和输入门控的入口 |
| 牌桌状态 | `GamePlay/Card/Model/` 下的 `CardsStore.cs` 保存列、牌包、两种牌堆和接龙进度，`CardData.cs` 保存单张场上牌的朝向、运行时属性和牌面显示状态 |
| 发牌、移动与结算 | `GamePlay/Card/Manager/CardsManager.cs` 编排组牌、发牌、移牌、翻牌、技能生效、回收、洗回与关卡收牌 |
| 规则判定 | `GamePlay/Card/Manager/CardRuleManager.cs` 集中判断拖起、落列、牌包、手动发牌和完整接龙 |
| 场景输入 | `GamePlay/Card/Controller/` 下的 `CardController`、`DrawPileController` 与两个牌堆查看 Controller，统一受 `CardGameModule.CanCardInput` 约束 |
| 牌桌表现 | `GamePlay/Card/View/` 下的 `CardViewManager.cs` 监听事件、摆放牌桌与同步动画，`CardView.cs` 显示单张牌。`GamePlay/Card/Manager/` 下的 `CardPoolManager`、`CardResManager` 分别负责场景牌实例复用和牌面资源 |
| 开局事件 | `GamePlay/Level/LevelEventRegistry.cs` 将事件 ID 映射到发牌前的行为，事件结束后由 `CardsManager` 统一进入发牌 |
| 牌局 Buff | `GamePlay/Buff/` 下的 `BuffManager`、`BuffStore` 管理层数、激活和消耗，`UI/GamePlay/BuffPanel.cs` 展示 |
| 牌面查看 | `UI/GamePlay/` 下的 `ShowCardsPanel.cs` 和 `ShowOneCardPanel.cs` 提供牌列表与单牌详情，场上可见性由对应 Controller 决定 |

场景在 [`Assets/Scenes/CardGameScene.unity`](../Assets/Scenes/CardGameScene.unity)。场景牌预制体在 [`Assets/Resources/Prefab/GamePlay/Card/CardPrefab.prefab`](../Assets/Resources/Prefab/GamePlay/Card/CardPrefab.prefab)，牌面资源在 [`Assets/Resources/GamePlay/Card/`](../Assets/Resources/GamePlay/Card/)。

## 与关卡外交接

- **开局**由 `LevelManager.SelectLevel` 选定打牌关，经 `CardGameModule.StartNewGame` 开桌。`CardsManager` 读取关卡参数，从 `RunManager.Deck` 取得牌定义，再由 `CardFactory` 创建独立的 `CardData`。
- **操作**由 Controller 接收输入，规则判定集中在 `CardRuleManager`，牌桌变更由 `CardsManager` 执行。牌桌、顶栏和 Buff UI 通过事件同步，事件定义在 `Framework/Event/EventEnum.cs`。
- **结束**由 `CardsManager` 在接龙进度达标时交给 `LevelManager` 记录通关，并准备符合条件的通关小商店。收牌结束派发 `OnCardsCollected`，`CardGameEntry` 据此进入小商店或返回选关。
