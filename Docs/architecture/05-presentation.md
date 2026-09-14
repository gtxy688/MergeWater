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

### UI 来源：场景资产 + 手工编辑，工程里没有生成器（2026-09-12 重构，2026-09-13 移除生成器）

界面对象（HUD、面板、加载页、Toast、引导箭头等，共约 134 个 GameObject）全部是 `Assets/Scenes/Main.unity` 里 `GameRoot/Presentation` 子树下的**真实对象**，运行前就存在，运行时不再生成：

- **改 UI 只有一条路：在场景里改。** 加/删控件、改锚点与布局、换图、调色、改字号文案、连引用，都在编辑器的 Hierarchy / Inspector / Scene 视图里完成，改完保存场景即可——没有「改代码再重建界面」的步骤。
- 2026-09-13 需求方要求「以后只手动编辑 UI，不要程序自动生成」，因此三个程序化改界面的编辑器工具**已全部删除**：UI 生成器 `HudBuilder.cs`（含 `MergeWater/Assign Backdrop Art (No UI Rebuild)` 与「选中 Backdrop 节点」两个菜单）、场景构建器 `SceneBuilder.cs`（`MergeWater/Build Main Scene` / `MergeWater/Rebuild UI In Open Scene`）、一次性 TMP 迁移工具 `UiTmpMigrator.cs`。保留的编辑器工具都与界面结构无关：字体字符收集/烘焙（`TMPFontBuilder` / `TMPFontReferenceTool`）、配置资产生成（`ConfigAssetGenerator`）、占位美术生成（`PlaceholderArtGenerator`）、地面调参窗口（`FloorTuningWindow`），外加两个**只含常量**的类：`MainSceneAsset.cs`（主场景路径）与 `UiDesignSpec.cs`（1080×1920 画布、微信胶囊区、底部净空、相机清屏底色）。
- `GameBootstrapper` 原先有一条「缺表现层引用就现场搭一套 HUD」的兜底，已移除：缺失即明确 `Debug.LogError` 并停止初始化（避免「编辑器里看到的」与「运行时看到的」是两套界面）。
- 门禁：`UiSourceOfTruthTests`（运行时代码不得 `AddComponent` 任何 UI 组件；运行时程序集不得引用 `MergeWater.Editor`；`HudView` 不得有 setter）；场景侧的完整性由 `SceneAssetTests` / `HudLayoutTests` / `TMPFontAssetTests` / `UiArtSlicingTests` 守住。
- 接线规则：Inspector 里手工连的事件（持久监听）**会**随场景序列化；代码里 `AddListener` 只在该代码运行时生效。需要「随值同步的联动」（如音量条的填充）必须在运行时装配，见 `PanelController.HookButtons` 的注释。
- 仍有少量世界空间对象按事件在运行时创建：飘字 `FloatingText`（`FloatingTextSpawner.Spawn` 每次一个 `TextMesh`）与合成粒子（`ParticleBurst` 用常驻 `ParticleSystem` + `Emit`，不实例化）。这些是「一次性的瞬时特效」，不是界面结构。

音效优先用 `AudioDirector` 的 `sfxClips` **指派表**里的真实素材，未指派的 `SfxId` 回退到 `PlaceholderAudioFactory` 在运行时用正弦/方波包络合成的占位音（投放、合成、连击音阶、越线心跳、失败、按钮、领取、复活），因此「一个素材都没配」时也能听到反馈（决策 D4）。需求方（2026-09-13）指定合成音改用 `Assets/Audios/pop.ogg`：`Merge` 与 `ComboUp` 都指向该素材（连击音阶仍由 `pitch` 实现），指派发生在 `Main.unity` 的 `GameRoot/Presentation` 节点上，改素材只需在 Inspector 里换引用。BGM 槽位为可选：未指派时静默降级，不假装有音乐。

**BGM 只在「关闭音乐 → 重新打开」时从头播**（2026-09-14 需求方缺陷修复）：`AudioDirector.PlayMusic()` 是**幂等**的——只有「换了曲子」或「当前没在播」才真正 `Play()`，已经在放同一首时直接返回、不碰播放位置；规则收敛在纯函数 `AudioDirector.ShouldStartMusic(current, wanted, isPlaying)`。之所以必须幂等：任何设置变化都会经 `SettingsService.Changed → GameContext.ApplySettingsToAudio` 调用它（其中先 `SetMusicEnabled(已开)`、末尾再 `PlayMusic()` 一次），而**拖动音量条时存档值逐帧变化**，原先无条件 `Play()` 会把播放位置重置到 0 → 听感上音乐被反复从头播放。反向要求同样固化：「关闭音乐后再打开」必须从头播（`Stop()` 后 `isPlaying=false` → 重新 `Play()`）。门禁：EditMode `MusicPlaybackTests`（4 项规则）、PlayMode `AudioDirectorTests.VolumeChange_DoesNotRestartMusic` 与 `MusicToggledOffThenOn_RestartsFromTheBeginning`（断言 `AudioSource.timeSamples` 未被重置）。

`timeScale` 与顿帧由 `TimeDirector` 独占管理（慢放、顿帧、暂停），避免其他模块各自改 `timeScale`；M2 的 `SetSimulationEnabled` 与之解耦（冻结物理用 M2，视觉时间缩放用本模块）。

## 关键机制

### HUD 布局

- 触发条件：对局进行中。
- 处理顺序：顶栏左侧最高分、中央大号当前分与其下阶段目标进度条、右侧设置齿轮（置于系统胶囊左侧的安全区）；中央顶部待投水果与垂直虚线落点；右上角 next 预览；左侧入口自上而下为摇一摇、锤子·免费、大礼包；右侧入口自上而下为清屏·免费、炸弹·免费；警戒线为顶部下方横线；底部不放任何广告或推广位（R27）。
- 成功结果：所有元素随设备安全区自适应，竖屏不重叠。
- 失败与边界：安全区数据缺失时使用默认边距；缺少可选引用时跳过该元素并记录一次告警，不抛异常。

### 手感反馈编排

- 触发条件：`SessionEvents` 的合成与越线事件。
- 处理顺序：合成 → 粒子爆发 + 飘字（punch 缩放）+ 慢放（timeScale 0.2，0.15s）+ 顿帧（80–120ms，随连击递增）+ 震屏（3 档）+ 音高上行（+1 半音/连击，封顶 8 度）；越线 → 警戒线红色脉冲 + 心跳音，失败 → 重震 0.2s。
- 成功结果：每次合成与越线的反馈强度符合连击档位。
- 失败与边界：反馈进行中再次触发时按强度取最大值而非叠加，防止 `timeScale` 抖动；`TimeDirector` 保证结束时恢复 `timeScale = 1`。

### 对局背景

- 触发条件：`BackdropView.LateUpdate`——相机宽高比或正交尺寸变化时按 cover 重贴合（`Backdrop` 节点常驻场景，运行时不创建对象；`viewCamera` 为空时回落 `Camera.main`）。
- 当前状态（2026-09-13）：**已启用背景素材 `bg_star`**（`Assets/UI/Art/bg_star.png`，941×1672 竖版星体主视觉）。场景里 `Backdrop` 节点的 `SpriteRenderer` 已启用、排序值 −100。
- 处理顺序：`Backdrop` 节点（**世界空间** `SpriteRenderer` + `BackdropView`，挂在 `GameRoot/Presentation` 下、与 `Canvas` **平级**）持有素材；`BackdropView` 按「cover」缩放——取 `max(视口宽/素材宽, 视口高/素材高)` 乘 2% overscan，并把 sprite 居中到相机位置；排序值 -100，低于全部对局元素（场地视觉 -10、水果 100+、预览 300+、线 400+、粒子 550、飘字 600）。
- 成功结果：任意屏幕比例下背景铺满整屏且不遮挡任何对局元素。`bg_star` 宽高比 0.5628 ≈ 9:16，竖屏下几乎 1:1 贴合（cover 仅放大约 1.15×），裁切接近零。
- 失败与边界：素材名为空或素材缺失时关闭 `SpriteRenderer`（不告警、不抛异常），由相机纯色清屏兜底。
- 换背景图（全手动三步）：① 把图放进 `Assets/UI/Art/`（**不要放 `Resources/`**——那里的资源会被无条件打进包）；② 在场景里选中 `GameRoot/Presentation/Backdrop`，把 Sprite 拖到它的 `SpriteRenderer` 上并勾上渲染器（素材为空或被禁用时由相机纯色清屏兜底，不报错也不抛异常）；③ 想压暗就调同一个 `SpriteRenderer` 的 `Color` 乘色（例如 0.55/0.55/0.62）。**不要手改这个节点的 Transform**——缩放每帧由 `BackdropView` 按 cover 覆盖。该节点没素材时在 Scene 视图里完全看不见，可在 Hierarchy 搜索框里搜 `Backdrop` 定位。
- 可读性风险（观感类，需人工判断）：这张主视觉顶部带游戏标题、整体高对比高饱和，对局中它位于顶栏与水果之后，可能削弱落点预览线与警戒线的可读性。若观感上打架，直接在场景里调 `Backdrop` 的 `SpriteRenderer.Color` 乘色压暗（例如 0.55/0.55/0.62）即可，不必改代码。
- 决策：D14（不用 Canvas 底板的原因见该条与「HUD 实现约束」）。

### 面板与红点

- 触发条件：M7 请求显示/隐藏面板或设置红点状态。
- 处理顺序：面板显示时通过 `CanvasGroup` 淡入并阻断下层输入；红点由 M7 依据 M6 的可领取/被超越状态传入布尔值。
- 成功结果：进入对应页面后红点被清除（由 M7 调用）。
- 失败与边界：同时请求多个面板时后请求者优先，不叠层。
- 设置面板的三个开关（音效/音乐/震动）保持行高 84，其中**音效/音乐两行在行内右侧各带一条音量条**（勾选框 46..110、文字 110..260、滑条 270..660），因此补回音量条不占用额外纵向空间。音量条由美术包 Medium 版 `sound-bar-container`（390×44，轨道）与 `sound-bar-full`（376×24，分段填充）拼成，滑钮复用分段素材（34×34）。
- 音量条的**填充不交给 `Slider.fillRect`**：`Slider` 会改 fill 的锚点把整条分段压扁；改为运行时监听 `onValueChanged` 写 `Image.fillAmount`（`Image.Type.Filled`，水平、从左）。该监听必须挂在运行时装配的 `PanelController.HookButtons` 里：`Image.fillAmount` 与 `Slider.value` 的同步不是拖一个 Inspector 持久监听就能表达的联动（历史缺陷正是「只在编辑器期用代码挂监听」——运行时监听不随场景序列化，表现为「音量真的变了、填充条却一直满格」）。
- 音量条与开关正交：开关只静音，音量值保留；**取消勾选时对应音量条不可调**（`Slider.interactable = false`，靠 `DisabledColor` 变暗提示；勾回来恢复可调且档位保持原值 —— 需求方 2026-09-12「取消勾选时应该禁止调节大小」）。音量写入存档（`SaveData.settingsSfxVolume/settingsMusicVolume`，缺字段默认 100%，无需 schema 迁移），由 `GameContext.ApplySettingsToAudio` 统一应用到 `AudioDirector`（音乐基准增益 0.4，音效 1.0）。
- **可交互控件不得嵌在开关行的层级里**（2026-09-13 实测缺陷，需求方「调音量后勾选被自动取消」）：Unity 处理点击是**沿父链向上找第一个实现者**——按下时找到 `Slider`（它实现了 `IPointerDownHandler`）→ 音量照常改变；抬手时找 `IPointerClickHandler`，而 `Slider` **不实现**这个接口、`Toggle` 实现了，于是音量条的点击被外层开关「吸走」→ `ToggleValue()` → 勾选翻转（拖动位移超过点击阈值时 Unity 不发 click，故现象时有时无；音效/音乐两行同因）。**这个「向上找」是 Unity 有意的设计**——它让一个开关可以由多个子图形拼出来（点勾选框能开关就是靠它），代价是**任何嵌在 `Toggle` 层级里、自身不消费点击的控件（`Slider`、`ScrollRect`…）都会被吸走**。因此本项目取**结构性修法**：两条音量条（`SfxVolumeBar`/`MusicVolumeBar`）挂在 `Canvas/SettingsPanel/Panel` 下、与开关行（`SfxToggle`/`MusicToggle`）**同级**，而不是它们的子对象。几何上不损失任何交互——开关的唯一命中区本来就只有勾选框 `Box`（`Label` 与滑钮 `Handle` 的 `raycastTarget` 都是 0，音量条唯一命中区是 `Track`）。门禁 `SettingsToggleClickIsolationTests`：① 结构化（`Toggle` 层级内不得存在「有射线目标、且自身不实现 `IPointerClickHandler`」的控件）；② 机制级（直接问 `ExecuteEvents.GetEventHandler<IPointerClickHandler>`「点音量条时解析到谁」，不得是外层 Toggle）。**刻意不采用**「给音量条挂一个空 `IPointerClickHandler` 把事件截住」的补丁式修法：那只是盖住症状，控件仍然待在别人的点击区里。
- 面板与 UI 的调参入口（都在 Editor 程序集，运行时不可用）：
  - `MergeWater/Font/1. 收集字符 → TMPCharacters.txt`：扫描场景 / Prefab / C# 字面量（排除 `Assets/Art` 第三方素材包与 `Editor/` 下的日志与菜单文案），把真实会用到的文案写进 `Assets/Fonts/TMPCharacters.txt`；`#` 注释行不参与烘焙，其余行**每个字符**都会被烘，因此不需要去重。
  - `MergeWater/Font/2. 烘焙中文 TMP 字体资产`：读上面的 txt 烘出 Static 字体资产。候选顺序 1024² → 2048²@75pt → 2048²@68pt → 2048²@64pt → 允许多图集（最后手段）；实测本项目 1024² 装不下（缺 296 字），最终 2048² 单图集、占用率 51%、0 缺字。注意 TMP 运行时 API（`TryAddCharacters`）内部写死 `GlyphPackingMode.BestShortSideFit`（= Creator 窗口里的 Fast），Creator 的 Optimum 对应 `ContactPointRule`，没有参数可切——所以本工具用「单图集是否装得下 + 占用率」实测验收，而不是宣称用了 Optimum。
  - `MergeWater/Font/3. 重指到随包中文字体（可选）` + `MergeWater/Font/审计 TMP 字体引用`：把场景 / Prefab / TMP Settings 的字体与材质引用一次性指到目标字体资产，并修掉悬空引用（含世界空间 TMP 的 `MeshRenderer.m_Materials`——重建字体资产会换掉材质子资产 fileID，组件字段不会自动跟着变）。**只改字体与材质引用**，不动布局、字号、颜色、对齐、换行。
  - `MergeWater/Floor Tuning (Play Mode)`：地面高度实时滑杆。地面在 `Awake` 由相机算出（`屏幕底边 + FloorScreenInset`），**场景里手工挪 `Floor` 无效**——运行时 `EnsureArena` 会覆盖。调好可一键写回：同时改 `GameBalance.cs` 的字段默认值**并直接改资产 `GameBalance.asset` 的序列化字段**。**不要在这里改成调 `ConfigAssetGenerator.GenerateMenu()`**——生成器用 `ScriptableObject.CreateInstance` 取代码默认值，而写盘 .cs 的同一帧 Unity 还没重编译，程序集里的默认值仍是旧的，会把旧值原样写回资产（2026-09-13「参数调好了却没起作用」即此：.cs=0.06、资产=0.55）。
- 面板根节点挂 `UiPanel`（项目自定义基类，需求方要求「给 UI 面板搞个父类」）：把「可见」的定义收敛到一处（alpha + blocksRaycasts + interactable + activeSelf 必须同时正确），并提供 `Show()/Hide()/SetVisible()`。**场景里面板的初始状态是 alpha=1 且 active=false**——这样在编辑器里手动勾上 active 就能直接看到面板内容调布局；早期 alpha 被序列化成 0，勾 active 也看不见（需求方实际踩到的坑）。`UiPanel.OnEnable` 在非播放态被手动激活时会自动补 alpha=1，进一步避免该问题。
- 面板底板与按钮底用的是 `Assets/Resources/Art` 的九宫格素材，**必须按 PPU=100（= `Canvas.referencePixelsPerUnit`）导入**：`Image.Type.Sliced` 的边框设计单位 = `边框像素 × referencePixelsPerUnit ÷ 素材 PPU ÷ pixelsPerUnitMultiplier`，PPU 越小边框越大（`100/PPU` 倍），一旦边框超过元素尺寸，Unity 会把边框压满整个 RectTransform、素材被整体拉伸（2026-09-12 的「设置面板变成大白椭圆」即此）。门禁：`UiArtSlicingTests`。面板底是深紫，因此直接落在面板上的文字都是浅色（标题/正文为暖白、次要文字为浅紫灰、分数高亮为暖黄），按钮内部文字仍是深棕——这些颜色现在只存在于场景里的 TMP 组件上，直接选中文本对象改即可。

### 音效素材指派

- 触发条件：`AudioDirector.GetClip(SfxId)`（`PlaySfx` 的每条路径都经过它；`Configure` 与 `HasClip` 也会先装配一次）。
- 处理顺序：`EnsureClipsLoaded` 把序列化的 `SfxClipEntry[] sfxClips` 装进查询表 `_clips`（空槽位跳过，重复 `id` 时列表靠后者胜）→ 命中则用真实素材；未命中才现场合成占位音，并把该 `SfxId` 记入 `_generatedClips`。
- 当前装配（2026-09-13）：`Merge`（id 1）与 `ComboUp`（id 2）都指向 `Assets/Audios/pop.ogg`（8.4KB OGG，Vorbis、非 3D、`preloadAudioData=0`）。`ComboUp` 与 `Merge` 用同一素材是**有意为之**：连击音阶由 `pitch`（`ComboPitch`）实现，不必准备多条音频。
- 成功结果：合成/连击听到的是 `pop.ogg`（连击时音高上行），其余音效仍是占位音。
- 失败与边界：素材缺失或槽位为空 → 回退占位音（静默降级，`GetClip` 只在合成抛异常时记一次告警）；`sfxClips` 整个字段为空 → 与改动前行为完全一致（全占位音）。
- 释放边界（有一条回归用例守）：`OnDestroy` **只**释放 `_generatedClips` 里的占位音。工程资产（`pop.ogg`、`backgroundMusic`）被 `Destroy` 会连素材本体一起删掉。
- 换素材的正确做法：选中 `GameRoot/Presentation` → `Audio Director` → `Sfx Clips` 槽位里换引用（**不要**在代码里写路径，也不要放进 `Resources/`——那会被无条件打进包）。门禁：`SceneAssetTests.AudioDirector_MergeSfx_IsAssignedFromProjectAudioAssets` 断言 `Merge` 与 `ComboUp` 都指向 `Assets/Audios/` 下的 `.ogg`，防止「拖了没反应」这种无声失效。

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
| `AudioDirector.PlaySfx(SfxId, pitch)` | 方法 | 音效与音高 | — | 开关关闭静默；`sfxClips` 指派的真实素材优先，未指派用占位音 |
| `AudioDirector.HasClip(SfxId)` | 方法 | 音效标识 | 是否已有真实素材 | 纯查询，不合成；供场景装配门禁使用 |
| `AudioDirector.SfxClipEntry[] sfxClips` | 序列化字段 | `SfxId` + `AudioClip` | — | 场景序列化数据；空槽位跳过、重复 id 取靠后者；工程资产不随组件销毁释放 |
| `AudioDirector.SetSfxVolume(v)` / `SetMusicVolume(v)` | 方法 | 0..1 音量 | — | 与开关正交；音乐基准增益 0.4；音源缺失时只记录不报错 |
| `PanelController.SetVolumeStates(sfx, music)` | 方法 | 0..1 音量 | — | 读档回填滑条与填充；用 `SetValueWithoutNotify`，不触发「玩家改了设置」事件链 |
| `BackdropView.Configure(renderer, camera)` | 方法 | 背景渲染器与相机 | — | 幂等；素材为空时不动 Transform |
| `PanelController.Show/Hide(PanelId)` | 方法 | 面板 | — | 互斥显示 |
| `SetBadge(BadgeId, visible)` | 方法 | 红点 | — | 幂等 |
| `AimPreviewView.SetPoints(points)` / `SetVisible(bool)` | 方法 | 点列 | — | 点数 <2 时隐藏 |

## Unity 装配

- 场景与 Prefab：`Main.unity` 的 `Canvas`（Screen Space - Overlay，竖屏 1080×1920 参考分辨率）下含 `HudRoot`、`PanelRoot`、`FeedbackRoot`；`HudView` 等引用**全部手工在该场景里维护**（原 `SceneBuilder` 与 UI 生成器已删除，2026-09-13）。对局背景不在 Canvas 下，而在表现层根节点（世界空间）的 `Backdrop` 子节点。
- 组件与序列化引用：`HudBinder`、`FeedbackDirector`、`TimeDirector`、`AudioDirector`、`PanelController`、`ScreenShaker`、`FloatingTextSpawner`、`DangerLineView`、`AimPreviewView`、`BackdropView`；音频侧另有 `AudioDirector.sfxClips`（音效→素材指派表）、`sfxSource`/`musicSource`/`backgroundMusic`。
- 美术与音频资产：UI 图放 `Assets/UI/Art/`（如 `bg_star.png` 941×1672 竖版背景、`sound_bar_track.png` 390×44 轨道、`sound_bar_fill.png` 376×24 分段填充，均 PPU=100）；音效/BGM 放 `Assets/Audios/`（`pop.ogg` = 合成与连击音、`our_expanse_-_with_tail-version-.mp3` = 当前指派的 BGM）。**都不放 `Resources/`**，由场景引用决定是否进包。
- ScriptableObject / 其他资产：`UiTheme`（占位色板与字号，ScriptableObject）可选；无则用代码默认。
- 创建、启用、禁用和销毁：`HudBinder` 在 `OnEnable` 订阅、`OnDisable` 取消；`TimeDirector.OnDisable` 强制恢复 `timeScale`；占位音 `AudioClip`（运行时合成的那部分）缓存在 `AudioDirector` 内并在销毁时释放，`sfxClips`/`backgroundMusic` 指向的工程资产**不释放**。

## 影响与回归范围

- 直接影响模块：M7（面板与红点调用）、M3（订阅 `Events`，跨局重订阅）、M6（设置音量写入存档）。
- 必须复验的契约：`HudBinder.Bind/Unbind` 与跨局重订阅；`TimeDirector` 结束后 `timeScale` 恢复；`AudioDirector` 在无资源时的静默降级；`AudioDirector` 音量与开关正交；`AudioDirector` 的 `sfxClips` 指派优先于占位音且不释放工程资产；背景是否铺满视口且排在全部对局元素之后。
- 对应验收项：A1–A4（PlayMode 冒烟）+ A10–A12（背景与音量条）+ H1–H6（手动手感与观感）。

## 待裁定事项

无。
