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

UI 文本一律使用 **TextMeshPro**（`TextMeshProUGUI` / 世界空间 `TextMeshPro`），字体是随包分发的 `Assets/Fonts/ChineseUI SDF.asset`（决策 **D16**）：字形在编辑期静态烘进图集，运行时零字形生成。**原 legacy `Text` + 运行时系统字体方案已废弃**——目标平台微信小游戏跑在 WebGL 派生运行时上，无法访问系统字体，`Font.CreateDynamicFontFromOSFont` 在真机上会让中文显示为方块（`UiFontProvider` 已删除）。新增文案若用到新字，必须重跑 `MergeWater/Font/1. 收集字符 → TMPCharacters.txt` + `MergeWater/Font/2. 烘焙中文 TMP 字体资产`（字形集合的唯一来源是 `Assets/Fonts/TMPCharacters.txt`）；门禁是 `TMPFontAssetTests`。

### UI 来源：场景资产，运行时不生成（2026-09-12 重构）

界面对象（HUD、面板、加载页、Toast、引导箭头等，共约 134 个 GameObject）全部是 `Assets/Scenes/Main.unity` 里 `GameRoot/Presentation` 子树下的**真实对象**，运行前就存在，运行时不再生成：

- 生成器 `HudBuilder` 位于 `Assets/Scripts/Editor/HudBuilder.cs`，属于 **Editor 平台程序集**（`MergeWater.Editor`）。运行时程序集（`MergeWater.Presentation` / `MergeWater.Bootstrap`）在编译期无法引用它——这是结构性保证，不是约定。守卫：`UiSourceOfTruthTests`（3 项：运行时程序集不得包含生成器、不得引用 `MergeWater.Editor`、`HudView` 不得有 setter）。
- `GameBootstrapper` 原先有一条「缺表现层引用就现场搭一套 HUD」的兜底，已移除：缺失即明确 `Debug.LogError` 并停止初始化（避免「编辑器里看到的」与「运行时看到的」是两套界面）。
- 改 UI 的三种方式：① 位置/颜色/文字/字号等微调 → 直接在场景里改；② 结构性布局 → 改 `HudBuilder.cs` 后执行 `MergeWater/Rebuild UI In Open Scene`（只重建 UI 子树，场景其余部分与手工调整保留）；③ 整场重置才用 `MergeWater/Build Main Scene`（会重建整个场景，菜单会先确认）。
- 仍有少量世界空间对象按事件在运行时创建：飘字 `FloatingText`（`FloatingTextSpawner.Spawn` 每次一个 `TextMesh`）与合成粒子（`ParticleBurst` 用常驻 `ParticleSystem` + `Emit`，不实例化）。这些是「一次性的瞬时特效」，不是界面结构。

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

### 对局背景

- 触发条件：`HudBuilder.Build` 构建表现层时（编辑器构建场景与运行时兜底共用这条路径）。
- 当前状态（2026-09-13）：**已启用背景素材 `bg_star`**（`Assets/UI/Art/bg_star.png`，941×1672 竖版星体主视觉）。场景里 `Backdrop` 节点的 `SpriteRenderer` 已启用、排序值 −100。
- 处理顺序（启用素材时）：在**世界空间**创建 `Backdrop`（`SpriteRenderer` + `BackdropView`），素材由 `HudBuilder.BackdropArtName` 指定；`BackdropView` 按「cover」缩放——取 `max(视口宽/素材宽, 视口高/素材高)` 乘 2% overscan，并把 sprite 居中到相机位置；排序值 -100，低于全部对局元素（场地视觉 -10、水果 100+、预览 300+、线 400+、粒子 550、飘字 600）；相机宽高比或正交尺寸变化时下一帧自动重贴合。
- 成功结果：任意屏幕比例下背景铺满整屏且不遮挡任何对局元素。`bg_star` 宽高比 0.5628 ≈ 9:16，竖屏下几乎 1:1 贴合（cover 仅放大约 1.15×），裁切接近零。
- 失败与边界：素材名为空或素材缺失时关闭 `SpriteRenderer`（不告警、不抛异常），由相机纯色清屏兜底。
- 换背景图（**不要用整场重建**）：把图放进 `ArtSpriteRoot`（`Assets/UI/Art/`），在 `HudBuilder.BackdropArtName` 填资源名，再执行菜单 `MergeWater/Assign Backdrop Art (No UI Rebuild)`——它只改 `Backdrop` 一个节点并保存场景。`Rebuild UI In Open Scene` 会重建整个 UI 子树、覆盖手工调整过的加载页与 HUD，换背景不必付这个代价。
- 可读性风险（观感类，需人工判断）：这张主视觉顶部带游戏标题、整体高对比高饱和，对局中它位于顶栏与水果之后，可能削弱落点预览线与警戒线的可读性。若观感上打架，直接在场景里调 `Backdrop` 的 `SpriteRenderer.Color` 乘色压暗（例如 0.55/0.55/0.62）即可，不必改代码。
- 决策：D14（不用 Canvas 底板的原因见该条与「HUD 实现约束」）。

### 面板与红点

- 触发条件：M7 请求显示/隐藏面板或设置红点状态。
- 处理顺序：面板显示时通过 `CanvasGroup` 淡入并阻断下层输入；红点由 M7 依据 M6 的可领取/被超越状态传入布尔值。
- 成功结果：进入对应页面后红点被清除（由 M7 调用）。
- 失败与边界：同时请求多个面板时后请求者优先，不叠层。
- 设置面板的三个开关（音效/音乐/震动）保持行高 84，其中**音效/音乐两行在行内右侧各带一条音量条**（勾选框 46..110、文字 110..260、滑条 270..660），因此补回音量条不占用额外纵向空间。音量条由美术包 Medium 版 `sound-bar-container`（390×44，轨道）与 `sound-bar-full`（376×24，分段填充）拼成，滑钮复用分段素材（34×34）。
- 音量条的**填充不交给 `Slider.fillRect`**：`Slider` 会改 fill 的锚点把整条分段压扁；改为运行时监听 `onValueChanged` 写 `Image.fillAmount`（`Image.Type.Filled`，水平、从左）。该监听必须挂在运行时装配的 `PanelController.HookButtons` 里——`HudBuilder` 在编辑器期挂的监听不会被序列化进场景（与按钮监听是同一个坑）。
- 音量条与开关正交：开关只静音，音量值保留；**取消勾选时对应音量条不可调**（`Slider.interactable = false`，靠 `DisabledColor` 变暗提示；勾回来恢复可调且档位保持原值 —— 需求方 2026-09-12「取消勾选时应该禁止调节大小」）。音量写入存档（`SaveData.settingsSfxVolume/settingsMusicVolume`，缺字段默认 100%，无需 schema 迁移），由 `GameContext.ApplySettingsToAudio` 统一应用到 `AudioDirector`（音乐基准增益 0.4，音效 1.0）。
- 面板与 UI 的调参入口（都在 Editor 程序集，运行时不可用）：
  - `MergeWater/Font/1. 收集字符 → TMPCharacters.txt`：扫描场景 / Prefab / C# 字面量（排除 `Assets/Art` 第三方素材包与 `Editor/` 下的日志与菜单文案），把真实会用到的文案写进 `Assets/Fonts/TMPCharacters.txt`；`#` 注释行不参与烘焙，其余行**每个字符**都会被烘，因此不需要去重。
  - `MergeWater/Font/2. 烘焙中文 TMP 字体资产`：读上面的 txt 烘出 Static 字体资产。候选顺序 1024² → 2048²@75pt → 2048²@68pt → 2048²@64pt → 允许多图集（最后手段）；实测本项目 1024² 装不下（缺 296 字），最终 2048² 单图集、占用率 51%、0 缺字。注意 TMP 运行时 API（`TryAddCharacters`）内部写死 `GlyphPackingMode.BestShortSideFit`（= Creator 窗口里的 Fast），Creator 的 Optimum 对应 `ContactPointRule`，没有参数可切——所以本工具用「单图集是否装得下 + 占用率」实测验收，而不是宣称用了 Optimum。
  - `MergeWater/Font/3. 重指到随包中文字体（可选）` + `MergeWater/Font/审计 TMP 字体引用`：把场景 / Prefab / TMP Settings 的字体与材质引用一次性指到目标字体资产，并修掉悬空引用（含世界空间 TMP 的 `MeshRenderer.m_Materials`——重建字体资产会换掉材质子资产 fileID，组件字段不会自动跟着变）。**只改字体与材质引用**，不动布局、字号、颜色、对齐、换行。
  - `MergeWater/Migrate UI To TMP (In Open Scene)`：把场景里的 legacy `Text` 就地换成 TMP、给面板挂 `UiPanel`、并预置飘字池。**只换组件类型，不动布局数值**（用于已有手工调整的场景）。
  - `MergeWater/Floor Tuning (Play Mode)`：地面高度实时滑杆。地面在 `Awake` 由相机算出（`屏幕底边 + FloorScreenInset`），**场景里手工挪 `Floor` 无效**——运行时 `EnsureArena` 会覆盖。调好可一键写回：同时改 `GameBalance.cs` 的字段默认值**并直接改资产 `GameBalance.asset` 的序列化字段**。**不要在这里改成调 `ConfigAssetGenerator.GenerateMenu()`**——生成器用 `ScriptableObject.CreateInstance` 取代码默认值，而写盘 .cs 的同一帧 Unity 还没重编译，程序集里的默认值仍是旧的，会把旧值原样写回资产（2026-09-13「参数调好了却没起作用」即此：.cs=0.06、资产=0.55）。
- 面板根节点挂 `UiPanel`（项目自定义基类，需求方要求「给 UI 面板搞个父类」）：把「可见」的定义收敛到一处（alpha + blocksRaycasts + interactable + activeSelf 必须同时正确），并提供 `Show()/Hide()/SetVisible()`。**场景里面板的初始状态是 alpha=1 且 active=false**——这样在编辑器里手动勾上 active 就能直接看到面板内容调布局；早期 alpha 被序列化成 0，勾 active 也看不见（需求方实际踩到的坑）。`UiPanel.OnEnable` 在非播放态被手动激活时会自动补 alpha=1，进一步避免该问题。
- 面板底板与按钮底用的是 `Assets/Resources/Art` 的九宫格素材，**必须按 PPU=100（= `Canvas.referencePixelsPerUnit`）导入**：`Image.Type.Sliced` 的边框设计单位 = `边框像素 × referencePixelsPerUnit ÷ 素材 PPU ÷ pixelsPerUnitMultiplier`，PPU 越小边框越大（`100/PPU` 倍），一旦边框超过元素尺寸，Unity 会把边框压满整个 RectTransform、素材被整体拉伸（2026-09-12 的「设置面板变成大白椭圆」即此）。门禁：`UiArtSlicingTests`。面板底是深紫，因此直接落在面板上的文字用 `HudBuilder.PanelTextColor` / `PanelTextMutedColor` / `PanelScoreColor`，按钮内部文字仍用 `TextColor`。

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
| `AudioDirector.SetSfxVolume(v)` / `SetMusicVolume(v)` | 方法 | 0..1 音量 | — | 与开关正交；音乐基准增益 0.4；音源缺失时只记录不报错 |
| `PanelController.SetVolumeStates(sfx, music)` | 方法 | 0..1 音量 | — | 读档回填滑条与填充；用 `SetValueWithoutNotify`，不触发「玩家改了设置」事件链 |
| `BackdropView.Configure(renderer, camera)` | 方法 | 背景渲染器与相机 | — | 幂等；素材为空时不动 Transform |
| `PanelController.Show/Hide(PanelId)` | 方法 | 面板 | — | 互斥显示 |
| `SetBadge(BadgeId, visible)` | 方法 | 红点 | — | 幂等 |
| `AimPreviewView.SetPoints(points)` / `SetVisible(bool)` | 方法 | 点列 | — | 点数 <2 时隐藏 |

## Unity 装配

- 场景与 Prefab：`Main.unity` 的 `Canvas`（Screen Space - Overlay，竖屏 1080×1920 参考分辨率）下含 `HudRoot`、`PanelRoot`、`FeedbackRoot`；`HudView` 等引用由编辑器工具 `SceneBuilder` 自动填充。对局背景不在 Canvas 下，而在表现层根节点（世界空间）的 `Backdrop` 子节点。
- 组件与序列化引用：`HudBinder`、`FeedbackDirector`、`TimeDirector`、`AudioDirector`、`PanelController`、`ScreenShaker`、`FloatingTextSpawner`、`DangerLineView`、`AimPreviewView`、`BackdropView`。
- 美术资产：`Assets/Resources/Art/`（由美术包 `Assets/Art` 挑选复制，均为 PPU=100 的 Sprite）。本次新增 `bg_night`（3840×2160 背景）、`sound_bar_track`（390×44 轨道）、`sound_bar_fill`（376×24 分段填充）。
- ScriptableObject / 其他资产：`UiTheme`（占位色板与字号，ScriptableObject）可选；无则用代码默认。
- 创建、启用、禁用和销毁：`HudBinder` 在 `OnEnable` 订阅、`OnDisable` 取消；`TimeDirector.OnDisable` 强制恢复 `timeScale`；占位音 `AudioClip` 缓存在 `AudioDirector` 内并在销毁时释放。

## 影响与回归范围

- 直接影响模块：M7（面板与红点调用）、M3（订阅 `Events`，跨局重订阅）、M6（设置音量写入存档）。
- 必须复验的契约：`HudBinder.Bind/Unbind` 与跨局重订阅；`TimeDirector` 结束后 `timeScale` 恢复；`AudioDirector` 在无资源时的静默降级；`AudioDirector` 音量与开关正交；背景是否铺满视口且排在全部对局元素之后。
- 对应验收项：A1–A4（PlayMode 冒烟）+ A10–A12（背景与音量条）+ H1–H6（手动手感与观感）。

## 待裁定事项

无。
