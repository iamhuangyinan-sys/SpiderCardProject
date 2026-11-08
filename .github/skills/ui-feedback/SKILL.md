---
name: ui-feedback
description: 维护 SpiderCard 的 UIFeedback UI 交互效果，包括控件表现代码、预制体、DOTween 效果与 Inspector；系统能力变化时同步 docs/UIFeedback.md。
---

# UI 交互效果

## 任务范围与入口

先区分本次任务：单个控件的表现调整、共享系统能力变更，或仅查询/提供方案。仅查询或方案请求不直接修改实现。撤销时只撤销用户指定的改动，保留已有调参和其它修改。

先读 `docs/UIFeedback.md` 和本次涉及的实现；文档与代码不一致时查明实际行为，不照搬过时示例。涉及业务模块、资源加载或 UIBinder 时，同时读取项目的 `spidercard-framework` Skill。

| 职责 | 项目入口 |
| --- | --- |
| 输入、状态、调度 | `Assets/Scripts/UI/Common/Feedback/` 下的 `UIFeedbackButton.cs`、`UIFeedbackHoverInput.cs`、`UIFeedbackController.cs`、`UIFeedbackState.cs` |
| 效果及基类 | `Assets/Scripts/UI/Common/Feedback/Effect/` |
| Inspector、创建菜单、预览 | `Assets/Scripts/Editor/UIFeedback/` |
| 使用与架构文档 | `docs/UIFeedback.md` |

## 表现代码与状态

- `UIFeedbackButton` 继承 `Button`，自动要求 Controller；UIBinder 可继续按 `Button` 获取。不要再叠加一个原生 Button，内置 Transition 不负责这套视觉反馈。
- 同一个 Controller 只选一个输入来源：FeedbackButton、HoverInput、或宿主手动驱动。现有卡牌格由 `CardModule.OnHoverChanged` 驱动，不要为了加阴影重复挂输入组件。
- 悬停、按住、点击成功和导航焦点是不同事件。按住是 Pressed，焦点不是按住；松开后回到当前输入对应的状态。状态优先级以 Controller 为准。
- `interactable` 控制交互资格，`SetAttention` 控制提醒/呼吸，两者不等价。禁用时可选的悬停效果由各效果的 `Hover When Disabled` 控制；不要通过恢复可点击来实现。
- 模组负责业务资格、图片资源及提示内容，效果负责视觉。避免业务脚本和 UIColorEffect 同时改一个 Graphic 的颜色。
- 对象池换数据前使用 `ResetFeedback`，再设置新业务状态。订阅与取消订阅配对；购买飞行等接管视觉前停止反馈，避免 Tween 或阴影跟随继续写位置。

## 增加或优化效果

1. 先判断需求是状态过渡、持续循环还是一次性动画；已有组件能表达时优先调参。不要在业务脚本复制一套指针输入和 Tween 管理。
2. 新效果继承 `UIFeedbackEffect`，与 Controller 挂在同节点，Target 指向实际视觉节点。按现有签名实现 `Capture`、`ApplyState`、`RestoreValues` 和 `CollectBindings`。
3. 明确初始值、状态值是绝对值还是相对值、参数单位及默认外观；重复切状态不能积累位移或缩放。还原应恢复捕获值，允许下一次使用重新捕获。
4. 用 DOTween 做动画，遵守 Duration、Ease、UseUnscaledTime 和 immediate。通过基类管理自身 Tween，不调用目标级 `DOKill`。额外循环、协程、LateUpdate 跟随也必须在复位/禁用时停止。
5. 一个目标属性只交给一个效果。缩放与呼吸使用嵌套节点；位移与阴影不要同时写同一个 RectTransform 的位置。为新效果声明实际绑定，使 Inspector 冲突检测有效。
6. `PlayClick` 是点击成功反馈扩展点，不阻塞 `onClick`。当前基类每次 Apply 会取消它持有的 Tween；实现连续回弹时先设计状态切换与一次性动画的中断关系，不假设点击动画一定播完，也不擅自延迟切场景业务。
7. 如果效果需要在 Button 中勾选，补齐效果列表、自动挂载/目标绑定及必要的 Inspector、菜单支持。编辑器增删与赋值支持 Undo；勾选状态以实际组件为准。

阴影和描边使用独立 Image，不重新使用原生 Outline 或旧 HoverScaleBehaviour。Sprite 留空时不显示默认矩形。`UIShadowEffect.Target` 是阴影图片，`Reference Target` 是可选的几何参考；留空仍可以用自身尺寸与 Offset 显示。指定参考时要在阴影父坐标系计算，避免共同缩放重复计算；卡牌阴影参考牌面，不参考包含价格的整个格子。

## 修改预制体

修改层级、组件、绑定或实例覆盖前，读取 [预制体修改规范](references/prefab-editing.md)。根据实际调用关系选择修改共享源预制体还是指定实例，不把示例参数套到全部控件。

参数未生效时先核对：编辑的是预制体资产还是运行实例、实际状态、Target/Reference Target、实例覆盖、布局以及其它属性写入者。不要只凭截图判断“写死”。编辑模式可用现有 Controller 静态状态预览；停止预览再调参数。仅调整 Hovered 字段不会自动触发悬停；不要因此擅自新增一套实时预览功能。

## 必须同步文档的情况

**共享系统发生以下变化时，必须在同一次任务中更新 `docs/UIFeedback.md`：**

- 新增、删除效果或公共调用方式；输入、状态优先级、生命周期、动画中断或复位语义改变。
- 通用参数的含义、单位、默认行为或目标/参考对象的计算规则改变。
- 推荐层级、挂载职责、自动绑定、Inspector、创建菜单或预览流程改变。
- 系统迁移影响已有控件的配置方式，需要说明迁移方法。

仅调整某个预制体的数值、图片或业务显示条件，通常不需要扩写系统文档；若使已有说明失真，修正对应段落。直接维护当前说明，避免追加互相矛盾的历史记录。系统入口或核心约定改变时也更新本 Skill，参数明细仍以使用文档为主。

## 验证与交付

- 代码变化做可用的编译检查；独立编译若使用 Unity 引用，确认确实包含新增/移动的源码，不能以空编译成功代替检查。
- 预制体变化检查引用、绑定、层级及实例覆盖，静态检查不等于已在 Unity 中验证外观。删除脚本前搜索旧类名和 `.meta` GUID 的残留引用。
- 按任务约定交付测试；用户自行试效果时，完成编译/静态检查并说明实际验证范围，不强行启动游戏或关闭用户编辑器。
- 说明具体改动、调参位置和使用方法；只报告真实执行的检查。纯 Skill 修改无需编译 Unity。
