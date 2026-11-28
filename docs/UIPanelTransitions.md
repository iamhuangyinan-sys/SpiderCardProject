# 面板淡入淡出与背景

`NormalPanel` Inspector 的 **Fade Target** 指定需要淡入淡出的 CanvasGroup。留空使用根节点；已有飞入、飞出仍操作根节点。时长默认 0.2 秒。

## 主界面、菜单、设置和胜利界面

- `BeginPanel/FadeRoot`：Logo、按钮、版本号等内容。`BackgroundContent` 保留原来放在 bg 下的内容及其坐标系。
- `BeginPanel/bg`：不淡入淡出。根节点的 `PanelBackdrop` 在 Awake 将 bg 独立到面板同级，设置打开期间仍显示，切场景时自动隐藏。
- `MenuPanel/FadeRoot`：菜单按钮等内容。菜单 bg 同样由 `PanelBackdrop` 独立保存，首次显示菜单和最终关闭菜单时跟随淡入淡出，在设置切换期间保持显示。
- `SettingsPanel/FadeRoot`：设置的黑底、标题、列表和关闭按钮。从开始界面进入时全组淡入淡出；从菜单进入时关闭本面板 bg，仅控件淡入淡出，使用菜单保留的黑底。
- `WinPanel/FadeRoot`：背景和内容一起淡入淡出。最终关地图退场后打开胜利页，胜利页淡出完成后切回主界面。
- `SecondConfirmPanel/FadeRoot`：标题、提示和按钮一起淡入淡出。主界面进入时由确认面板的 `PanelBackdrop` 单独处理黑底，游戏菜单进入时保留菜单已有黑底。取消后恢复来源面板，确认后执行本次操作。

主界面和游戏菜单的「退出游戏」共用退出确认。点击「确定」立即执行退出，游戏菜单会在退出前保存本局进度。点击「取消」时，确认面板按原过渡退场。

在预制体模式中调整 bg 的图片、颜色和尺寸，运行时分离不会修改资产。调整 FadeRoot 下的内容，不改变 UIBinder 的组件引用或字段名。

## 生命周期

`Hide` 保留面板实例，退场完成后才停用；`Close` 退场完成后执行 OnClose 并销毁。`HideSelf(Action)` 和 `CloseSelf(Action)` 用完成回调衔接下一面板，不等待额外一帧。

过渡中再次 Show 会打断旧退场并取消旧完成回调。场景输入锁保持到交接完成。框架退出时 CloseAll 立即清理，不等待动画。

切场遮罩达到全黑后，`UIManager` 同步隐藏旧面板并完成已开始的退场。正在关闭的面板执行 `OnClose` 并移出缓存，其余面板可再次显示。独立背景通过 `OnBeforeLoad` 同步收起。

`UIManager.Show<T>(configure)` 在面板打开和动画开始前执行配置。SettingsPanel 用它按来源选择自己的黑底是否显示。

## 展示牌面板与单牌详情

两个面板都是 `NormalPanel + ISceneInputBlockPanel`，内容淡入淡出 0.2 秒；黑底交给 `PanelBackdrop` 管理（运行时把 `bg` 独立到面板同级，面板隐藏时黑底可以留着）。

- `ShowCardsPanel`：黑底单独淡入；`OnPanelClosing` 时除非在给详情面板交接（`KeepBackground()`），否则黑底一起淡出。
- `ShowOneCardPanel` 有两个入口，过渡方式不同：

| 入口 | 黑底 | 过渡 | 左右切换 |
|------|------|------|------|
| 展示牌面板点格子 `OpenFrom(source, index)` | 复用来源面板的黑底（自己的 `bg` 保持关闭） | 先淡出列表内容（黑底不动）→ 淡入详情内容；关闭时淡出详情 → 重新显示列表并淡入 | 有（`btn_Left` / `btn_Right`，首尾隐藏） |
| 场上牌右键 `ShowOne(...)` | 自己的黑底 | 整个界面（黑底 + 内容）一起淡入淡出 | 无（两个按钮都隐藏） |

交接细节（与菜单 / 设置一致）：`source.KeepBackground()` 必须在 `HideSelf` 之前调用，否则退场时 `OnPanelClosing` 会把黑底一起收掉；详情面板关闭用 `CloseSelf(returnAction)`，淡出播完再 `UIManager.Show<ShowCardsPanel>()`；重新显示时 `PanelBackdrop.Show` 发现黑底已完全不透明会跳过重复淡入，不会闪一下。
