# SpiderCard 关卡外系统

本文用于定位从开新局到续玩的整局游戏流程，以及多处共用的牌定义。牌桌规则、输入和牌局状态见 [关卡内系统](CardGame.md)。通用框架见 [框架指南](Framework.md)。

## 纸牌基本数据

`Assets/Scripts/GamePlay/Card/Model/CardDef.cs` 是牌的静态定义，供初始牌组、整局牌组、商店和存档使用。打开打牌关时，`Assets/Scripts/GamePlay/Card/Factory/CardFactory.cs` 复制定义与自带属性，创建关卡内的 `CardData`。以下代码路径相对于 [`Assets/Scripts/`](../Assets/Scripts/)。

| 概念 | 数据和入口 |
| --- | --- |
| 点数 | `CardDef.ranks` 支持多个点数，也可为空。普通点数为 A 到 K（1–13），`CardRankConst` 定义 `Wild`（−1，万能放下）和 `Ignore`（−2，无视点数）。牌桌如何使用这些值见 `CardRuleManager` |
| 花色 | `CardDef.suits` 支持多个花色，也可为空。`E_CardSuitEnum` 当前顺序是红心 0、方块 1、黑桃 2、梅花 3 |
| 技能 | `CardDef.placeSkill` 和 `flipSkill` 分别对应落牌、翻牌。枚举在 `GamePlay/Card/CardEnum.cs`，配置分别为 `PlaceSkillConfig`、`FlipSkillConfig`，牌桌触发入口在 `CardsManager` |
| 属性 | `CardDef.attributes` 是牌自带属性，可同时有多个。枚举 `E_CardAttributeEnum` 对应 `CardAttributeConfig`。开局后 `CardData.attributes` 单独记录运行时属性，商店删牌等关卡外校验读取牌定义 |
| 牌预设 | `CardPreSetConfig` 定义可复用的组合与定价。`GamePlay/Card/Helper/CardHelper.cs` 将预设转为 `CardDef`，并解析随机点数和花色槽位 |

牌组和商品持有实际的 `CardDef`。配表 ID 用来查预设，牌组中同一份定义重复出现表示多张牌。配置源表在 [`Config/Excel/`](../Config/Excel/)，导出产物在 [`Assets/Scripts/Config/`](../Assets/Scripts/Config/) 与 [`Assets/Resources/Config/`](../Assets/Resources/Config/)。

## 整局流程和代码位置

一大局跨越多个关卡，持有自己的牌组、金币、难度和地图。

| 模块 | 入口 |
| --- | --- |
| 主菜单和场景分流 | `UI/Main/BeginPanel.cs` 发出开始新局或继续的入口，`GamePlay/Card/CardGameEntry.cs` 负责进入初始牌组选择、选关、打牌关或商店 |
| 初始牌组与难度 | `UI/GamePlay/BeginBuildPanel.cs` 接收初始牌组、难度和可选地图种子，`Helper/BeginBuildHelper.cs` 按 `BeginBuildConfig`、`DifficultyConfig` 构建牌组，`GamePlay/Run/RunManager.cs` 创建整局 |
| 整局资产 | `GamePlay/Run/` 下的 `RunStore.cs` 持有金币、牌组和难度，`RunManager.cs` 负责资产读写与变更通知 |
| 地图和选关 | `GamePlay/Level/` 下的 `LevelManager.cs`、`LevelStore.cs` 管理路线、解锁、当前关与奖励。`RunLevelData.cs` 定义节点与地图快照，`LevelGenerationHelper.cs`、`LevelGenerationSettings.cs` 管理生成和调参。界面在 `UI/GamePlay/LevelPanel.cs`，可选关或只读查看地图 |
| 商店关 | `GamePlay/Shop/` 下的 `ShopManager.cs`、`ShopStore.cs` 管理商品、买牌、删牌和商店关完成，界面在 `UI/GamePlay/ShopPanel.cs` |
| 通关小商店 | `GamePlay/Shop/` 下的 `LittleShopManager.cs`、`LittleShopStore.cs` 管理打牌关通关后可领取或购买的牌与待处理状态，界面在 `UI/GamePlay/LittleShopPanel.cs` |
| 存档 | `GamePlay/Run/RunSaveData.cs` 定义数据，`GamePlay/Card/CardGameModule.cs` 编排各 Manager 的导入导出，`Framework/Save/SaveRegistry.cs` 将 `RunData` 注册到 `Saves/run_data.json` |
| 顶栏和整局牌组查看 | `UI/Main/MainTopPanel.cs` 提供整局信息和入口，`UI/GamePlay/ShowCardsPanel.cs` 复用牌列表，`UI/Main/MenuPanel.cs` 提供保存和返回入口 |

## 存档与牌桌的边界

- 新局确认初始牌组和难度后生成地图，续玩使用存档里的地图快照。地图生成、调参与验证见 [随机关卡](RandomLevels.md)。
- 存档保存牌组、金币、难度、地图、选关轨迹、进行中的关卡、待发奖励与两类商店状态。进行中的打牌关凭关卡 ID 重开，场上的 `CardData`、牌堆和 Buff 每关重新创建。
- 选中打牌关后，由关卡内系统将整局牌组实例化为本关牌桌。买牌或删牌写回整局牌组，后续开局从更新后的牌组组牌。
