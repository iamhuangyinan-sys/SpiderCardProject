# 面板淡入淡出与背景

`NormalPanel` Inspector 的 **Fade Target** 指定需要淡入淡出的 CanvasGroup。留空使用根节点；已有飞入、飞出仍操作根节点。时长默认 0.2 秒。

## 当前三个面板

- `BeginPanel/FadeRoot`：Logo、按钮、版本号等内容。`BackgroundContent` 保留原来放在 bg 下的内容及其坐标系。
- `BeginPanel/bg`：不淡入淡出。根节点的 `PanelBackdrop` 在 Awake 将 bg 独立到面板同级，设置打开期间仍显示，切场景时自动隐藏。
- `MenuPanel/FadeRoot`：菜单按钮等内容。菜单 bg 同样由 `PanelBackdrop` 独立保存，首次显示菜单和最终关闭菜单时跟随淡入淡出，在设置切换期间保持显示。
- `SettingsPanel/FadeRoot`：设置的黑底、标题、列表和关闭按钮。从开始界面进入时全组淡入淡出；从菜单进入时关闭本面板 bg，仅控件淡入淡出，使用菜单保留的黑底。

在预制体模式中调整 bg 的图片、颜色和尺寸，运行时分离不会修改资产。调整 FadeRoot 下的内容，不改变 UIBinder 的组件引用或字段名。

## 生命周期

`Hide` 保留面板实例，退场完成后才停用；`Close` 退场完成后执行 OnClose 并销毁。`HideSelf(Action)` 和 `CloseSelf(Action)` 用完成回调衔接下一面板，不等待额外一帧。

过渡中再次 Show 会打断旧退场并取消旧完成回调。场景输入锁保持到交接完成。框架退出时 CloseAll 立即清理，不等待动画。

`UIManager.Show<T>(configure)` 在面板打开和动画开始前执行配置。SettingsPanel 用它按来源选择自己的黑底是否显示。
