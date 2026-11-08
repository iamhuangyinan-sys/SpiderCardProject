# UI 交互效果

项目的 UI 悬停缩放已统一迁移到这套系统，旧缩放组件及其元数据已删除。运行时脚本位于 `Assets/Scripts/UI/Common/Feedback/`。

## 快速试用

1. 等待 Unity 完成编译。在 Hierarchy 中选中一个 Canvas 或 UI 容器。
2. 菜单 **GameObject → UI → Feedback Button** 创建一个新按钮。没有 Canvas / EventSystem 时会创建所需对象；预制体编辑模式不创建 EventSystem。
3. 新按钮默认只有基础组件，不挂任何效果。在 `UIFeedbackButton` 的「可选效果」里勾选缩放、位移、描边、阴影、呼吸、颜色，会自动挂载对应组件并绑定标准层级的 Target。进入 Play 查看效果。`UIFeedbackButton` 保留 `Button.onClick` / `interactable`，UIBinder 仍可按 `btn_` 绑定为 Button。
4. 在根节点的 `UIFeedbackController` 上勾选运行时的「需要提醒 / 呼吸」，测试呼吸；停止 Play 后，勾选序列化字段 `Attention On Start` 可设为初次显示的默认配置。
5. 修改各效果组件中的五种状态参数。Play 中通过 Inspector 调整参数会重新应用；Play 中的调整遵循 Unity 规则，退出后不会保存。
6. 编辑模式选中 Controller，可以预览普通、悬停、按下、焦点、禁用的最终外观。呼吸预览显示峰值；实际动画在 Play 中查看。停止预览后再调参数。

菜单只创建新的场景对象，不保存预制体、不修改选中的现有按钮。试验满意后可自行拖入 Project 保存成新预制体。

也可以自行添加 `UIFeedbackButton`，它会通过 RequireComponent 自动添加 Controller。在它的 Inspector 中勾选效果即可自动添加组件；自建层级需要在效果组件上手动指定 Target，缩放与呼吸要使用不同节点。勾选框反映实际组件是否存在，手动添加的效果也会显示为已勾选。

取消勾选会移除该类型的所有效果组件（包括多个 UIColorEffect）并丢弃其参数，可通过 Undo 撤销。如果只想暂时关闭效果并保留参数，使用效果组件自己的启用开关。增删效果在编辑模式、停止预览后操作；没有选择阴影时，菜单生成的 Shadow 图片保持透明。描边首次勾选时会创建或绑定 OutlineVisual 的 Image，移除效果后保留轮廓节点。Source Image 留空时保持透明，后续自行指定轮廓 Sprite。

## 分工与挂载

所有效果和 Controller 挂在同一个根节点，效果的 Target 指向内部视觉节点；Controller 自动收集同节点效果，不扫描子按钮。

```text
btn_Feedback  [Image 透明点击区域、UIFeedbackButton、Controller、各效果]
└─ ScaleRoot              ← UIScaleEffect
   └─ BreatheRoot         ← UIBreathingEffect
      ├─ Shadow           ← UIShadowEffect；独立 Image
      └─ Surface          ← UIOffsetEffect
         ├─ Background    ← UIColorEffect
         ├─ OutlineVisual ← UIOutlineEffect，独立 Image
         └─ Content       [HorizontalLayoutGroup]
            └─ Label      [TextMeshProUGUI]
```

根节点不做动画，保持布局和点击区域稳定。所有装饰 Graphic 关闭 Raycast Target。图标可添加到 Content 中。

| 组件 | 参数含义 |
| --- | --- |
| UIScaleEffect | 各状态倍率相对于目标初始 localScale；不假设初始缩放一定是 1 |
| UIOffsetEffect | 各状态位移加在目标初始 anchoredPosition 上；目标不要交给 LayoutGroup 直接布局 |
| UIOutlineEffect | 指向独立轮廓 Image；各状态设置 Color、Padding（每侧外扩）和 Offset（偏移）；实际线条粗细由 Sprite 决定 |
| UIShadowEffect | 指向独立 Image；offset 是该阴影的 anchoredPosition，extraSize 加在初始 sizeDelta 上，color 是绝对颜色 |
| UIBreathingEffect | 只有 Attention 为 true 且状态为 Normal 时播放；Min / Max 是相对于初始缩放的倍率，Half Cycle 是半个周期 |
| UIColorEffect | 与 Graphic 初始颜色相乘，白色保留原色；可以挂多个组件，分别调背景、文字和图标 |
| UIRaiseEffect | 悬停/按下时把 Target 容器提到同级最后；Duration 后恢复排序；父节点带 LayoutGroup 时不提层 |

各组件的 Duration / Ease 仅控制自身状态过渡，时间默认不受 Time.timeScale 影响。呼吸循环使用 InOutSine，周期单独设置。

示例阴影使用内置按钮 Sprite 来展示厚度，不包含模糊。需要柔和阴影时，替换 Shadow 的图片为适合的九宫格软阴影 Sprite。

## 状态与业务调用

Controller 状态优先级：**Disabled → Pressed → Hovered → Focused → Normal**。鼠标是否在按钮内和导航焦点分别记录；按住移出时撤销下压，移回恢复。不可交互包含 Button.interactable 和父 CanvasGroup 的限制。

```csharp
// 普通点击沿用 Button，禁用不再触发内置颜色过渡。
button.onClick.AddListener(OnClick);
button.interactable = canClick;

var feedback = button.GetComponent<UIFeedbackController>();
feedback.SetAttention(needsAttention); // 可点不代表必须呼吸，由业务明确指定。
feedback.HoverChanged += OnHoverChanged;
// 取消订阅要与业务生命周期配对。

// 对象池重新绑定前清除旧业务提醒、输入和视觉。
feedback.ResetFeedback();
```

隐藏对象会停止自身 Tween、还原初始属性并清空瞬时输入，保留 Attention 意图；重新显示时恢复提醒。对象池换数据时请调用 ResetFeedback，然后设置新的 Attention。

面板内容过渡由 `NormalPanel` 的 `Fade Target` 指定 CanvasGroup，不属于按钮 Feedback。需要保持显示的背景放在目标外；开始界面和菜单用 `PanelBackdrop` 独立保存背景。面板退场完成后才停用或销毁，使对象的 OnDisable 复位发生在不可见之后；设置来源的背景保留策略由面板业务处理。

不需要按钮的卡牌格可挂 Controller + 效果，由宿主调用 `SetHovered` / `SetInteractable`。希望零代码响应指针进出时挂 `UIFeedbackHoverInput`。**FeedbackButton、HoverInput、宿主手动驱动三选一**，不要重复输入。

## 扩展与边界

- 新效果继承 `UIFeedbackEffect`，实现 Capture、ApplyState、RestoreValues、CollectBindings；无需修改 Button 或 Controller。
- PlayClick 是点击成功的一次性反馈扩展点；目前六个效果主要覆盖持续状态，不延迟业务 onClick。
- 一个目标属性只交给一个效果。尤其是呼吸和状态缩放必须用不同节点；Inspector 会提示同一控制器内的目标属性冲突。
- 效果不调用目标的 DOKill，只停止自己创建的 Tween，避免误杀同目标其它属性的动画。
- 编辑器预览使用 AnimationMode 快照，在切换选择、保存场景/预制体、重编译和进入 Play 时还原；其它动画工具占用 AnimationMode 时不开始预览。
- 不包含模糊 Shader、点击波纹和音效。悬停提层可在 Button 的可选效果中单独勾选，Target 默认当前节点，也可指定外层容器。

## LevelBtnModule 迁移

`LevelBtnModule.prefab` 已迁移。`btn_Level` 使用 UIFeedbackButton，保留原组件 fileID，UIBinder 和生成的 Button 字段无需更换。

```text
LevelBtnModule                 ← UIRaiseEffect 的 Target，仍是地图定位的零尺寸容器
└─ btn_Level                   [透明 Image、UIFeedbackButton、Controller、Scale/Breathing/Raise]
   └─ ScaleRoot                ← 交互缩放：悬停 1.3，过渡 0.1 秒
      └─ BreatheRoot           ← 呼吸：0.9～1.2，半周期 1 秒
         ├─ Background         ← 原来的关卡图标 Image，也是 Button.targetGraphic
         └─ img_Arrow          ← 原来的玩家位置箭头
```

业务调用 SetAttention 决定是否呼吸；悬停/按下/禁用时停止呼吸，重新空闲后恢复。关卡不使用描边、位移或通用变色；悬停阴影复用当前 Icon。

关卡按钮的 UIScaleEffect 和 UIRaiseEffect 均开启 `Hover When Disabled`：未解锁、已走过和地图查看中的不可点击关卡，鼠标移入仍放大并提层，移出后恢复。它们的交互状态始终保持 Disabled，不能点击，也不呼吸，业务亮/灰颜色保持不变。其它按钮默认关闭此开关。

| 业务状态 | 外观 | 点击 | 呼吸 |
| --- | --- | --- | --- |
| 可选关卡 | 原色 | 可点 | 空闲时播放 |
| 已走过关卡 | 原色 | 沿用地图解锁规则 | 仅真正可点时播放 |
| 未解锁关卡 | Locked Color 与原色相乘 | 不可点 | 停止 |
| 选关停留期间 | 保留亮/灰 | 不可点 | 停止 |
| 地图查看模式 | 已走过关卡亮，其余按原地图规则置灰 | 不可点 | 停止 |

颜色由模组的业务状态设置，Locked Color 在 LevelBtnModule 组件上调；不再借用 Button.colors.disabledColor，图标上也不叠加 UIColorEffect。提示由模组接收指针进出，保留不可点击关卡的信息提示，关闭面板时清理。提示定位使用实际图标，包含缩放后的尺寸。

## 其余控件迁移

| 预制体 | 迁移节点 | 缩放 |
| --- | --- | --- |
| BeginBuildPanel | btn_Right、btn_Left、btn_Check、btn_Continue | 1.3 |
| LevelPanel / ShowCardsPanel / MenuPanel | 各自的 btn_Close | 1.2 |
| LittleShopPanel | btn_Skip | 1.3 |
| ShopPanel | btn_DeleteCard、btn_Continue | 1.3 |
| IconInfoModule | 图标与信息模组 | 1.2，MainTopPanel 中各实例的覆盖值保留 |
| ShopCardItem | 原有居中的视觉容器，包含价格 | 1.3 |
| ShowCardItem | 原有 mod_Card 视觉目标 | 1.4 |

普通按钮初次迁移使用 UIFeedbackButton + Controller + Scale/Raise/Color。可见 Image 和内容移到内部视觉层，根节点使用透明点击区域；原 Button、图标、文字及 UIBinder 的组件标识保持不变。UIColorEffect 承接原有 Button Color Tint 的状态颜色；后续单独调整的按钮以实际效果组件配置为准。

BeginBuildPanel 的 btn_Left / btn_Right、ShopPanel 的 btn_Continue / btn_DeleteCard、LittleShopPanel 的 btn_Skip 已按 btn_Check 配置为 Scale/Raise/Shadow，不再额外变色。缩放过渡为 0.1 秒、OutBack，普通/按下/焦点/禁用倍率为 1，悬停为 1.3。阴影复用各按钮自身图标，配置与 btn_Check 一致；Reference Target 绑定可见 Background，删除按钮使用 DelIcon，不包含价格和次数文字，跳过按钮也只参考自身图标。新增 Shadow 在 BreatheRoot 下位于 Surface 之前，关闭 Raycast Target。

IconInfoModule 订阅 Controller.HoverChanged 控制提示；不可交互时停止悬停并收起提示。提示对齐到实际缩放后的图标。MainTopPanel 中的五个嵌套实例随共享预制体更新，两处自定义缩放覆盖也已迁移到新字段。

两个卡牌格使用 Controller + Scale/Raise，由 CardModule.OnHoverChanged 驱动 SetHovered，不在外层挂 FeedbackButton 或 HoverInput。展示牌可以保持不可点击，同时仍然悬停放大、查看提示。列表激活/回收时 ResetFeedback；商店购买飞行开始时先复位并停止接收悬停变化，避免干扰飞行动画。


## 轮廓图片配置

通用 Button 的原生 Outline 已迁移为独立 OutlineVisual 子节点，Target 已绑定，Source Image 留空。请在 Image 上指定透明背景的轮廓 Sprite；需要通过状态染色时使用白色轮廓图。矩形框可使用 Sliced 与九宫格资源，不规则图案可改用 Simple。节点的 Raycast Target 关闭，排序位于背景之后、文字/箭头之前。Sprite 留空时运行与预览都不显示，Inspector 会提示待配置。原来的 Distance 已迁移为 Padding；Padding 每侧增加对应距离，不代表图中线条宽度。


### 关卡按钮不使用描边

LevelBtnModule 已移除轮廓节点、UIOutlineEffect 和对应绑定，LevelResManager 只加载 IconImage。关卡保留缩放、呼吸、提层、悬停阴影、亮灰状态和悬停提示，不读取轮廓配表或资源。通用按钮的可选描边组件仍可单独使用。

### 关卡阴影与提示间距

LevelBtnModule 的 img_Shadow 位于 BreatheRoot 下、Background 之前，图案在绑定时复用当前 Icon Sprite，无需配表或额外阴影资源。只有可选关卡悬停时显示阴影，普通、按下、焦点和禁用状态透明；不可选关卡仍可悬停缩放、提层，但不显示阴影。参数在 btn_Level 的 UIShadowEffect 中调，Hover When Disabled 保持关闭。模块上的 Tip Extra Gap 默认6，通过 HoverTipHelper 的 extraGap 增加提示框间距。

### 卡牌列表的悬停阴影

ShopCardItem 和 ShowCardItem 均挂载 UIShadowEffect，使用 Assets/ArtRes/Img/Cards/cardShadow.png（Sliced）。普通状态透明，悬停时为黑色、Alpha 0.5，偏移(6,-6)，过渡0.1秒；移出或回收时隐藏。Shadow 关闭 Raycast Target，在牌面之前绘制，并跟随原有缩放。展示牌新增 ScaleRoot 包裹牌面与阴影，商店格使用原有视觉 root。无需新增业务输入；购买飞行沿用现有 ResetFeedback 清理阴影。

### 指定阴影参考对象

UIShadowEffect 新增可选的 Reference Target。指定时，在阴影父节点坐标系中计算参考对象的位置和尺寸，并叠加 Offset / Extra Size；LateUpdate 跟随布局和缩放，避免共同父节点的缩放重复计算。留空时保持旧的 anchoredPosition / 初始 sizeDelta 逻辑。ShopCardItem 已绑定内部 mod_Card 的 RectTransform，不再以包含价格的外层格子确定阴影范围。颜色仍为黑色 Alpha 0.5。复位或购买飞行时停止跟随并恢复原值。

