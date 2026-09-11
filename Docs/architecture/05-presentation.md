# M5 Presentation 表现与反馈

> 相关需求：`Docs/requirements.md` 的 R6、R8、R10、R12、R13、R18、R20、R21、R22、R23、R25、R26、R27
> 验收文档：`05-presentation-test.md`

## 职责边界

- 本模块负责：对局 HUD 的数据绑定与布局（GDD §6.2）、结算页、设置面板、隐私政策面板、Toast、红点、警戒线视觉与脉冲、投放置信/瞄准预览线绘制、以及手感反馈（震屏、粒子、慢放、顿帧、飘字、连击音阶、失败重震、占位音效）。
- 本模块不负责：任何业务规则与数值决策（M1/M3）、物理（M2）、输入采集（M4）、存档与广告请求（M6）、流程编排与初始化顺序（M7）。
- 本模块只消费 M1 的只读 `ISessionView`（`RoundSnapshot` + `SessionEvents`）与 M7 注入的只读视图模型；不写回业务状态。因为依赖的是 Core 中的接口，本模块不引用 M3 程序集。

## 依赖

| 被依赖模块 | 只使用的公开契约 | 用途 | 缺失时的行为 |
|------------|------------------|------|--------------|
| M1 | `ISessionView`（`RoundSnapshot`/`SessionEvents`）、`GameBalance`、`ItemKind`、`FruitTierDefinition` | 展示数值与事件 | 默认值展示；事件未订阅时静态显示 |
| M4 | `IAimSource.State` | 绘制预览线与待投水果位置 | 预览隐藏 |
| Unity | uGUI（旧版 `Text` + 运行时系统中文字体，决策 D9）、`TextMesh`（飘字）、ParticleSystem、AudioSource、Camera | 表现实现 | — |

## 设计与数据流

`HudView` 是纯引用容器（序列化 `Text`、`Image`、`Button`、`Toggle`、`CanvasGroup`、`ParticleSystem`、`Camera`），不含逻辑。`HudBinder` 每帧读取 M7 提供的 `RoundSnapshot` 与 `IAimSource.State` 刷新视图，并订阅 `SessionEvents` 触发放大型反馈。

`FeedbackDirector` 不自行订阅，而由 `HudBinder` 在事件回调中调用其 `Play*` 方法（决策 D10）。这样全局只有一处订阅点，重开一局时不会漏解绑。反馈按 V2.20–V2.25 编排：吸附感由 M2 的吸附时长负责，本模块负责合成后的粒子爆发、`timeScale` 慢放、顿帧、震屏与飘字，并按连击提高震屏档位与音高。

UI 文本使用 uGUI 旧版 `Text`，字体由 `UiFontProvider` 在运行时解析系统中文字体（决策 D9）；解析失败时回退内置字体并告警一次。`Text` 是 MVP 占位方案，正式美术阶段迁移到 TMP 时只需替换 `HudBuilder` 中的文本创建。

占位音效由 `PlaceholderAudioFactory` 在运行时用正弦/方波包络合成短音（投放、合成、连击音阶、越线心跳、失败、按钮、领取、复活），无需音频资产；未指派真实 `AudioClip` 时优先使用占位音。BGM 槽位为可选：未指派时静默并在启动时记录一次日志（诚实降级，不假装有音乐）。

`timeScale` 与顿帧由 `TimeDirector` 独占管理（慢放、顿帧、暂停），避免其他模块各自改 `timeScale`；M2 的 `SetSimulationEnabled` 与之解耦（冻结物理用 M2，视觉时间缩放用本模块）。

## 关键机制

### HUD 布局

- 触发条件：对局进行中。
- 处理顺序：顶栏左侧最高分、中央大号当前分与其下阶段目标进度条、右侧设置齿轮（置于系统胶囊左侧的安全区）；中央顶部待投水果与垂直虚线落点；右上角 next 预览；左侧入口自上而下为摇一摇、锤子·免费、大礼包；右侧入口自上而下为撤销·免费、炸弹·免费；警戒线为顶部下方横线；底部不放任何广告或推广位（R27）。
- 成功结果：所有元素随设备安全区自适应，竖屏不重叠。
- 失败与边界：安全区数据缺失时使用默认边距；缺少可选引用时跳过该元素并记录一次告警，不抛异常。

### 手感反馈编排

- 触发条件：`SessionEvents` 的合成与越线事件。
- 处理顺序：合成 → 粒子爆发 + 飘字（punch 缩放）+ 慢放（timeScale 0.2，0.15s）+ 顿帧（80–120ms，随连击递增）+ 震屏（3 档）+ 音高上行（+1 半音/连击，封顶 8 度）；越线 → 警戒线红色脉冲 + 心跳音，失败 → 重震 0.2s。
- 成功结果：每次合成与越线的反馈强度符合连击档位。
- 失败与边界：反馈进行中再次触发时按强度取最大值而非叠加，防止 `timeScale` 抖动；`TimeDirector` 保证结束时恢复 `timeScale = 1`。

### 面板与红点

- 触发条件：M7 请求显示/隐藏面板或设置红点状态。
- 处理顺序：面板显示时通过 `CanvasGroup` 淡入并阻断下层输入；红点由 M7 依据 M6 的可领取/被超越状态传入布尔值。
- 成功结果：进入对应页面后红点被清除（由 M7 调用）。
- 失败与边界：同时请求多个面板时后请求者优先，不叠层。

### 音阶与占位音

- 触发条件：合成连击变化。
- 处理顺序：`ComboPitch(combo) = 2^(min(combo-1, 12)/12)`（+1 半音/连击，封顶 8 度 = 12 半音，音高 ×2），应用到合成音 `AudioSource.pitch`。
- 成功结果：连击越高音高越高。
- 失败与边界：combo ≤ 1 时为原调；音频被设置面板关闭时不播放。

## 对外契约

| 名称 | 类型 | 输入 | 输出或事件 | 错误/生命周期保证 |
|------|------|------|------------|-------------------|
| `HudBinder.Bind(session, aimSource, balance)` | 方法 | Session 与瞄准源 | — | 重复绑定先解绑；重开局需重新绑定 |
| `HudBinder.Unbind()` | 方法 | — | — | 幂等 |
| `HudView` | 序列化容器 | — | 各 UI 引用 | 引用可空，缺失时降级 |
| `FeedbackDirector.Play*` | 方法 | 反馈参数 | — | 由 `HudBinder` 调用，不反向依赖业务 |
| `TimeDirector.RequestSlowMo(scale, duration)` / `RequestHitStop(ms)` | 方法 | 参数 | — | 结束恢复 `timeScale=1`；嵌套取最强 |
| `AudioDirector.PlaySfx(SfxId, pitch)` | 方法 | 音效与音高 | — | 开关关闭静默；无资源用占位音 |
| `PanelController.Show/Hide(PanelId)` | 方法 | 面板 | — | 互斥显示 |
| `SetBadge(BadgeId, visible)` | 方法 | 红点 | — | 幂等 |
| `AimPreviewView.SetPoints(points)` / `SetVisible(bool)` | 方法 | 点列 | — | 点数 <2 时隐藏 |

## Unity 装配

- 场景与 Prefab：`Main.unity` 的 `Canvas`（Screen Space - Overlay，竖屏 1080×1920 参考分辨率）下含 `HudRoot`、`PanelRoot`、`FeedbackRoot`；`HudView` 等引用由编辑器工具 `SceneBuilder` 自动填充。
- 组件与序列化引用：`HudBinder`、`FeedbackDirector`、`TimeDirector`、`AudioDirector`、`PanelController`、`ScreenShaker`、`FloatingTextSpawner`、`DangerLineView`、`AimPreviewView`。
- ScriptableObject / 其他资产：`UiTheme`（占位色板与字号，ScriptableObject）可选；无则用代码默认。
- 创建、启用、禁用和销毁：`HudBinder` 在 `OnEnable` 订阅、`OnDisable` 取消；`TimeDirector.OnDisable` 强制恢复 `timeScale`；占位音 `AudioClip` 缓存在 `AudioDirector` 内并在销毁时释放。

## 影响与回归范围

- 直接影响模块：M7（面板与红点调用）、M3（订阅 `Events`，跨局重订阅）。
- 必须复验的契约：`HudBinder.Bind/Unbind` 与跨局重订阅；`TimeDirector` 结束后 `timeScale` 恢复；`AudioDirector` 在无资源时的静默降级。
- 对应验收项：A1–A4（PlayMode 冒烟）+ H1–H6（手动手感与观感）。

## 待裁定事项

无。
