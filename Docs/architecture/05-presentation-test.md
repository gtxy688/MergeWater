# M5 Presentation 验收文档

> 对应架构：`05-presentation.md`
> 对应需求：`Docs/requirements.md` 的 R6、R8、R10、R18、R20、R21、R22、R23、R25、R27

## 自动化测试计划与证据

| 编号 | 层级 | 测试路径与名称 | 行为 | 红灯原因或 N/A 理由 | 最近结果 |
|------|------|----------------|------|---------------------|----------|
| A1 | PlayMode | `Assets/Tests/PlayMode/Presentation/HudBinderTests.cs::Scored_RaisesScoreTextToSnapshotValue`、`PhaseToReviving_ShowsSettlementWithReviveAvailable`、`PhaseToGameOver_ShowsSettlementWithoutRevive`、`MilestoneReached_ShowsClaimToast` | 得分事件刷新分数文本；失败页/结算页与复活按钮可用性；里程碑 Toast | 实现前无 `HudBinder` | PASS |
| A2 | PlayMode | `Presentation/HudBinderTests.cs::BindAfterRebind_SubscribesNewEventsOnlyOnce`、`Unbind_StopsReceivingEvents` | 跨局重新绑定不重复订阅；解绑后不再响应 | 同上 | PASS |
| A3 | PlayMode | `Presentation/TimeDirectorTests.cs`（6 项，含 `SlowMoAndHitStop_FinishRestoreTimeScaleToOne`、`HitStop_FreezesThenRestoresTimeScale`、`NestedSlowMo_TakesStrongestScaleAndLongestDuration`、`ResetToNormal_AlwaysRestoresOne`） | 慢放与顿帧结束后 `timeScale` 必为 1；嵌套取最强；非法请求忽略 | 实现前无 `TimeDirector` | PASS |
| A4 | PlayMode | `Presentation/AudioDirectorTests.cs`（5 项，含 `PlaySfx_WithoutSourceOrWithSfxDisabled_DoesNotThrow`、`PlaceholderClips_AreGeneratedWithAudioData`、`PlayMusic_WithoutClip_StaysSilentWithoutLogging`） | 关闭音效/无音源时静默不报错；占位音可生成并缓存；无 BGM 时静默降级（不写 Console） | 同上 | PASS |
| A5 | EditMode | `Assets/Tests/EditMode/Presentation/ComboPitchTests.cs::ComboPitch_AddsSemitonePerCombo_CappedAtOneOctave`、`HitStopSeconds_GrowsWithCombo_AndStaysWithinV223Range`、`ShakeTier_IsThreeLevels` | 音阶 +1 半音/连击、封顶 8 度（12 半音）；顿帧 80–120ms 随连击递增；震屏三档 | 实现前无音阶函数 | PASS |
| A6 | PlayMode | `Presentation/HudBinderTests.cs::DangerEvents_ToggleDangerLinePulse`、`Update_RefreshesNextPreviewAndPendingFruit`、`Update_WhileAiming_DrawsTrajectory` | 警戒线脉冲随越线事件开关；next 预览与待投水果随快照刷新；瞄准时绘制虚线 | 实现前无 `DangerLineView`/`AimPreviewView` | PASS |

| A7 | EditMode | `Assets/Tests/EditMode/Bootstrap/HudLayoutTests.cs`（6 项：`AllVisibleHudElements_FitInsideDesignCanvas`、`TopAnchoredElements_UseSameSidePivot_SoTheyAreNotClipped`、`TopBar_DoesNotEnterWeChatCapsuleZone`、`BottomBand_IsClearOfHudElements`、`SideEntries_SitOnTheirOwnHalf`、`Canvas_UsesPortraitReferenceResolution`） | HUD 布局与安全区不变量：元素必须落在 1080×1920 设计画布内；贴边元素 pivot 必须与锚点同侧；顶栏不得进入微信胶囊保留区（300×115）；底部 60 单位净空（R27）；两侧入口不越中线；Canvas 竖屏参考分辨率 | **修复两个真实布局缺陷后新增**（见 E4/E5）；此前无任何布局约束测试 | PASS |
| A8 | EditMode | `Assets/Tests/EditMode/Presentation/UiArtSlicingTests.cs::ArtWithNineSliceBorder_IsImportedAtCanvasReferencePixelsPerUnit`、`SceneSlicedImages_DoNotClampTheirBordersIntoTheWholeRect` | 九宫格 UI 素材必须以 PPU=100（= `Canvas.referencePixelsPerUnit`）导入；场景里任何 `Image.Type.Sliced` 的边框换算后不得接近/超过元素最小边的一半（否则 Unity 把边框压满整个 Rect，素材被整体拉伸） | **修复「设置面板被拉伸成大白椭圆」后新增**（V2.40）：此前无任何九宫格缩放约束，素材 PPU 被设成 1/2，边框实际放大 100/50 倍 | PASS |
| A9 | PlayMode | `Assets/Tests/PlayMode/Presentation/UiScreenshotDiagnostics.cs::CapturePanels_ForVisualReview` | 把加载页/设置/隐私/结算四个面板渲染成 `Logs/Diagnostics/ui-0X-*.png`（Canvas 临时切 ScreenSpaceCamera + 独立相机 → RenderTexture），供人工与多模态复核；`-nographics` 下写 `.skipped.txt` 而不崩溃 | UI 观感只能靠看图判断（与 `PhysicsAndPreviewDiagnostics` 同类诊断测试） | PASS |
| A13 | EditMode | `HudLayoutTests.cs::SettingsPanel_BottomBlock_SitsAboveTheBottomEdgeWithBalancedSpacing` | 设置面板底部块（版本 + 关闭）：下沿距面板底 ≥ 90 且 ≤ 面板高的 25%，**与「清除缓存」不重叠**，版本在关闭之上。2026-09-14（V2.59）改为**基于实际矩形**（`RectTransformUtility` 换算到面板局部空间）——面板已是拉伸锚点，旧的底锚点推算不成立、导致该用例长期假红 | PASS（V2.59，EditMode 160/160） |
| A14 | EditMode | `Assets/Tests/EditMode/Presentation/UiSourceOfTruthTests.cs`（3 项：`RuntimeSources_DoNotCreateUiComponents`、`RuntimeAssemblies_DoNotReferenceTheEditorAssembly`、`HudViewFields_AreFilledFromTheSceneNotFromCode`） | UI 来源不变量：界面只来自场景资产、**只靠手动编辑**——运行时代码不得 `AddComponent` 任何 UI 组件（`Canvas`/`Image`/`Button`/`Toggle`/`Slider`/TMP/`HudView`/`PanelController`…），运行时程序集不得引用 `MergeWater.Editor`（编辑器工具不进运行时装配），`HudView` 不得有属性 setter（UI 引用只能由场景序列化提供） | **UI 改为「运行前就存在」后新增**（2026-09-12 需求方要求方便调整）；2026-09-13 生成器被删除后，前两项从「按类型名扫反射」改为**源码扫描**门禁——生成器不存在了，扫反射会变成空断言 | PASS |
| A15 | EditMode | `Assets/Tests/EditMode/Presentation/SpriteAtlasTests.cs`（4 项：UI 图集内容与 `Assets/UI/Art` 实际文件一致、排除项没进图集、尺寸 ≤ 2048、星球图集条件校验） | UI 图集必须真的覆盖它该覆盖的 27 张小图、且**不含**满屏大图（`bg_star`/`Gamebg`/`bg_night`）；尺寸上限 ≤ 2048（ES2 只保证 2048²）；新增美术若既没进图集也不在排除名单里 → 红灯（漂移守卫） | 需求方（2026-09-14）「帮我打一下图集」；实测收益：开局无堆叠 `Batches 12 → 3`、堆满星球 `20 → 15` | PASS |
| A16 | EditMode | `Assets/Tests/EditMode/Core/FruitAccentPaletteTests.cs`（6 项：`AccentPalette_MatchesTheActualPlanetArt_SoTheArtCannotDriftSilently`、`AccentPalette_CoversEveryTier_AndRejectsOutOfRange`、`AccentColors_AreNotTheFruitPalette_SoTheRegressionCannotComeBack`、`BurstColorForLevel_UsesPlanetAccent_WhenArtExists`、`BurstColorForLevel_FallsBackToFruitPalette_WhenArtIsMissing`、`BurstColorForLevel_OutOfRange_FallsBackWithoutThrowing`）+ `Assets/Tests/EditMode/Bootstrap/SceneAssetTests.cs::MergeParticle_UsesSpaceVfxSprite_NotTheFruitPlaceholder`（真场景） | 合成特效取色与形态的不变量：① `FruitAccentPalette` 必须与 10 张 planetNN.png **按同一口径实测**的主色一致（口径 = 不透明像素中饱和度最高 20% 的 RGB 均值；容差 4/255）——美术换了而表没跟着改会立刻红灯；② 星球主色不得退回水果色板（整体距离守卫）；③ 有美术 → 星球主色、缺图/越界 → 水果色板且不抛异常；④ 真场景里合成粒子的贴图必须是 `Assets/Art/Vfx/merge_spark.png`，不得再用水果占位圆片 | 需求方（2026-09-14）「项目从水果变成行星了，合成特效有点违和感」。根因：V2.44 换美术、V2.52 改文案后，**合成粒子仍停留在水果时代**（贴图 = 水果占位圆片、颜色 = 水果色板）。**反证（隔离副本实测）**：把 L1 主色改回葡萄紫 → 2 项红（`Expected: 0.343911201f, But was: 0.560000002f`）；把粒子贴图换回 `fruit_circle` → 1 项红（`Expected: "Assets/Art/Vfx/merge_spark.png", But was: "Assets/Resources/Placeholder/fruit_circle.png"`） | PASS |
| A17 | PlayMode | `Assets/Tests/PlayMode/Bootstrap/BootstrappedSceneTests.cs::Merge_ProducesVisibleFeedback`（本次扩充） | 真场景端到端：真实「按下→松手」投放两颗 9 级星球 → 合成 10 级；断言 **合成火花的颜色 = 结果星球（planet09）主色提亮一档**、**尺寸 = `ParticleBurst.SparkWorldSize`（0 档连击基准 0.18）**。读的是 `ParticleBurst.Play` 同步写入的配置值，不依赖 headless 下的粒子模拟，因此确定性成立 | **V2.56 扩充**。这条断言同时守住「`GameBootstrapper` 真的把 `FruitArt` 注入了 `FeedbackDirector`」——漏注入**不会报错**，只会静默退回水果色板。**反证（隔离副本实测）**：摘掉 `feedback.SetFruitArt(fruitArt)` 一行 → `Expected: 0.553000033f +/- 0.001f, But was: 0.347499996f`（正是水果色板里 10 级「西瓜绿」的提亮值） | PASS |
| A10 | EditMode | `Assets/Tests/EditMode/Presentation/BackdropViewTests.cs`（5 项：`Backdrop_CoversWholeCameraView_竖屏/横屏/正方形`、`Backdrop_RefitsWhenCameraAspectChanges`、`Backdrop_WithoutSprite_DoesNotThrow`） | 背景必须按 cover 盖满任意宽高比的视口（1080×1920 / 1920×1080 / 1:1）、居中于相机、缩放贴近理论 cover 值；超宽屏（2.2:1）重新贴合后仍盖满；素材缺失时不抛异常且不动 Transform | **2026-09-12 需求方「背景图太丑，换一个」后新增**（D14）：cover 逻辑写错就会露出相机清屏色边带，逻辑测试抓不到 | PASS |
| A11 | EditMode | `Assets/Tests/EditMode/Bootstrap/SceneAssetTests.cs::Backdrop_IsBehindEveryWorldElement`、`HudView_RequiredElementsAreWired`（本次扩充） | 场景必须有背景节点且排序值为负（低于场景里每一个 `SpriteRenderer`/`LineRenderer`，否则挡住玩法画面）；指派了素材时渲染器必须启用。设置页必须有音效/音乐两条音量条与分段填充，取值范围 0..1 | **同上新增**：需求方反馈「音量条丢失」；背景断言随后改为兼容「换回原来的纯色底」 | PASS |
| A12 | PlayMode | `Assets/Tests/PlayMode/Bootstrap/BootstrappedSceneTests.cs::VolumeSliders_AreWiredAtRuntime_AndChangeAudio`、`Backdrop_CoversViewport_AndSitsBehindTheGameplay` | 真场景端到端：拖动音量条必须立刻改 `AudioDirector` 音量、写入存档、并让分段填充跟随；**取消勾选对应开关后音量条必须不可调、勾回来恢复且档位保留**。背景节点必须在场且排序在水果之前；指派了素材时还要盖满视口并在宽高比变化后重贴合（**2026-09-13 起走「有素材」分支**：`bg_star` 已启用） | **同上新增**（R25「即时生效并写入存档」+ 2026-09-12「取消勾选时禁止调节大小」） | PASS（2026-09-13 启用背景素材后复跑仍全绿） |
| A15 | EditMode | `Assets/Tests/EditMode/Presentation/SfxClipAssignmentTests.cs`（5 项：`GetClip_WithAssignedClip_ReturnsTheAssignedAssetInsteadOfPlaceholder`、`GetClip_WithComboUpSharingTheMergeAsset_ResolvesBothIds`、`GetClip_ForUnassignedId_StillFallsBackToPlaceholder`、`GetClip_WithNullEntry_DoesNotThrowAndFallsBack`、`OnDestroy_DoesNotDestroyClipsThatComeFromProjectAssets`）+ `Assets/Tests/EditMode/Bootstrap/SceneAssetTests.cs::AudioDirector_MergeSfx_IsAssignedFromProjectAudioAssets`（真场景） | `sfxClips` 指派的真实素材必须优先于占位音（`Merge`/`ComboUp` 可共用一条素材）；未指派或槽位留空仍回退占位音；`OnDestroy` 只释放自己合成的占位音、不销毁工程资产；真场景里 `Merge` 与 `ComboUp` 都指向 `Assets/Audios/` 下的 `.ogg` | 需求方（2026-09-13）「合成音改用 `pop.ogg`」：在本次改动前 `AudioDirector` 根本没有「SfxId → 素材」的入口，拖进工程也听不到；反过来若 `OnDestroy` 照旧销毁查询表里的全部 clip，会把 `pop.ogg` 与 BGM 素材本体删掉。红灯实测：先写用例 → `error CS0246: SfxClipEntry` 找不到（2 处），实现后转绿 | PASS（2026-09-13，EditMode 定向 13/13、全量 140/141） |

## 自动化运行记录

| 日期 | Unity 版本与环境 | 命令或 Test Runner 过滤器 | 结果文件 | 结论 |
|------|------------------|---------------------------|----------|------|
| 2026-09-11 | Unity 2022.3.62f3 / Windows 10 / `-batchmode -nographics` | EditMode 与 PlayMode 各一次 | `Logs\editmode-results.xml`、`Logs\playmode-verify.xml` | PASS — 本模块 29 项（EditMode 9 + PlayMode 20） |
| 2026-09-12 | Unity 2022.3.62f3 / Windows 10 / 编辑器内 Test Runner | EditMode 全量（本次改动后） | 控制台输出（未落 XML） | PASS — EditMode **112/112**（含本次新增 6 项：A10 的 3 个宽高比 + 重贴合 + 无素材 + A11 的背景层级） |
| 2026-09-12 | Unity 2022.3.62f3 / Windows 10 / 编辑器内 Test Runner | EditMode 全量（背景换回纯色底 + 音量条禁用联动后复跑） | 控制台输出（未落 XML） | PASS — EditMode **112/112** |
| 2026-09-12 | 同上 / Play 模式运行时脚本 | `exec_runtime_script` 复验真场景（编辑器正开，PlayMode 套件需关闭编辑器跑批处理，本次未跑） | `Temp/verify-settings-disabled.png` 等 | PASS — 背景 `sprite=null / enabled=False / 相机清屏色=米色`；音量条 0.45 → 取消勾选音效后 `interactable=False` 且存档保留 0.45 → 重新勾选后 `interactable=True`、值 0.45、填充 0.45；取消勾选音乐后 `interactable=False` |
| 2026-09-12 | Unity 2022.3.62f3 / Windows 10 / 隔离副本 `-batchmode -nographics` | EditMode 与 PlayMode 全量（设置面板底部块上移 V2.41 后） | `Logs\editmode-results.xml`、`Logs\playmode-results.xml` | PASS — EditMode **113/113**、PlayMode **83/83**，`failed=0`；补上了上一条遗留的「PlayMode 未跑」缺口 |

| 2026-09-13 | Unity 2022.3.62f3 / Windows 10 / **真工程本体** `-batchmode -nographics`（编辑器已关闭） | EditMode 与 PlayMode 全量（删除全部程序化 UI 工具 + 重收字符集后，V2.51） | `Docs/evidence/editmode-results.xml`、`Docs/evidence/playmode-results.xml`、`Docs/evidence/uigate-falsify-results.xml` | EditMode **134/135**（唯一失败是既存的面板几何 —— 场景里 `CloseButton` 被手工改到 216、门槛上限 200，与本轮无关）、PlayMode **87/87**、PlayMode `failed=0`。A14 的源码门禁另做了**反证**：临时插入 `AddComponent<Canvas>()` 后立刻失败并报出 `Presentation/UiPanel.cs:32`；字符集核对「收集 ⊆ 已烘」= 464 ⊆ 508、缺失 0 |
| 2026-09-13 | Unity 2022.3.62f3 / Windows 10 / **隔离副本 `E:\MergeWaterVerify`**（真工程被运行中的编辑器 41832 锁定） | 先定向 `-testFilter 'MergeWater.Tests.EditMode.SfxClipAssignmentTests\|MergeWater.Tests.EditMode.SceneAssetTests'`，再 EditMode + PlayMode 全量（合成音改用 `pop.ogg` 后，V2.53） | `E:\MergeWaterVerify\Logs\red-sfx.log`（红灯）、`green-sfx-scene.xml`、`full-editmode-sfx.xml`、`full-playmode-sfx.xml` | 红灯成立（`error CS0246: SfxClipEntry` 找不到，2 处，正是预期原因）；定向 **13/13**；EditMode **140/141**（唯一失败仍是上面那条既存面板几何 `HudLayoutTests.SettingsPanel_BottomBlock_...`，与本轮无关）、PlayMode **87/87**、`failed=0`。注：`Unity.exe` 是 GUI 子系统程序，PowerShell 直接 `&` 调用**不会等待**进程结束（会拿到空 `$LASTEXITCODE` 与 0 字节日志），必须用 `Start-Process -Wait` |

> 环境说明：先在由真工程同步出的隔离副本上运行，随后 MCP 直连真工程本体复跑。最近一轮（2026-09-12，含九宫格 PPU 门禁、面板截图诊断与设置面板底部块上移 V2.41）为批处理全量运行：EditMode 113/113、PlayMode 83/83，`failed=0`。详见 `Docs/evidence/README.md`。

## 手动验收前置条件

- 场景、Prefab 与配置：已提交的 `Assets/Scenes/Main.unity`（**UI 在该场景里手工维护，没有生成器**）；竖屏 1080×1920 参考分辨率。
- 依赖模块状态：M1/M2/M3/M4/M6 已实现并可开局。
- 目标设备与画质档位：Editor Play Mode；真机（中端）。

## 手动验收

| 编号 | 操作步骤 | 可观察预期结果 | 环境与构建 | 执行者/日期 | 状态 | 证据或备注 |
|------|----------|----------------|------------|-------------|------|------------|
| H1 | 观察对局 HUD | 顶栏含最高分/当前分/阶段进度/设置；中央待投水果与垂直虚线；左右入口齐备；底部无任何 Banner/信息流/交叉推广 | Editor | 待执行 | 待手动验收 | GDD §6.2、R27（自动化已断言无广告位节点） |
| H2 | 连续制造 2/4/6 连击 | 顿帧与震屏随连击增强，音高上行，飘字 punch 明显 | Editor | 待执行 | 待手动验收 | V2.23/V2.24；**V2.53 后合成与连击音改用 `Assets/Audios/pop.ogg`**（原为运行时合成占位音）：本轮需一并确认「pop 音本身好听、连击变调自然、与 BGM 不打架」 |
| H3 | 堆到警戒线附近并越线 | 警戒线红色脉冲 + 心跳音；失败时重震 0.2s | Editor | 待执行 | 待手动验收 | V2.25 |
| H4 | 打开设置面板切换音效/音乐/震动 | 开关即时生效、重启后保持 | Editor | 待执行 | 待手动验收 | R25（持久化已自动化验证） |
| H7 | 打开设置面板拖动「音效/音乐」音量条；再取消对应勾选 | 滑钮跟随手指、分段填充随音量增减；**取消勾选后该音量条不可调（变暗），勾回来恢复且档位保持**；关掉开关再打开音量值保留 | Editor | 已执行（运行时脚本） | 待手动验收（触感） | A12；实测：取消勾选后 `interactable=False`、存档音量保留 0.45，重新勾选后 `interactable=True`、值仍 0.45、填充 0.45 |
| H8 | 观察对局背景（竖屏）+ 落点预览线与警戒线的可辨识度 | 背景为 `bg_star` 星体主视觉、铺满且无黑边/色带；**水果、落点预览线、警戒线在高对比高饱和背景上仍清晰可辨**（这是本次换图最大的观感风险）；顶部标题不被顶栏分数区压成杂乱 | Editor | 待执行（需人工目视） | 待手动验收（观感 + 可读性） | A10/A11/A12；若观感打架，直接在场景里改 `Backdrop` 的 SpriteRenderer.Color 压暗（如 0.55/0.55/0.62），无需改代码 |
| H5 | 首次进入查看隐私弹窗与结算页引导 | 首启必现隐私弹窗；前 3 局结算页出现分享/排行引导 | Editor | 待执行 | 待手动验收 | R20/R23 |
| H6 | 真机竖屏试玩 3 分钟 | 60fps 稳定，UI 不被微信胶囊遮挡，触屏落点准确 | 真机 | 待执行 | 待手动验收 | V3 |
| H9 | 连续合成不同等级的星球（尤其 1 级青蓝星球与 9/10 级暖色星球），观察合成瞬间的火花 | 火花是**四芒星**形态（不是圆点）、向外炸开后淡出收缩，**颜色与刚合成出的那颗星球同色系**（青蓝星球 → 青蓝火花，不该再出现葡萄紫/西瓜绿这类水果色）；连击越高越亮越大 | Editor | 已执行（运行时脚本 + 录屏） | 待手动验收（观感） | A16/A17、V2.56。**运行时实测**：10 个等级的火花色分别 = 对应星球主色提亮一档（如 L1 `(0.508,0.726,0.783)`、L10 `(0.553,0.441,0.727)`），与水果色板距离 0.39~1.28（确实换掉了）。**视频复核**（`screenshots/mergefx-sparks-v3.mp4`，7.5× 慢放）：四芒星形态可见、向外辐射并淡出、观感属「星尘/宇宙火花」。**仍需人工确认**：1080×1920 竖屏下的实际亮度/尺寸是否合意（单颗 0.18 世界单位） |

> 中文渲染备注（决策 D9）：UI 用 uGUI 旧版 `Text` + 运行时解析的系统 CJK 字体。若目标设备缺少候选中文字体，会回退内置字体并告警一次，此时中文可能显示为方块——需要在真机 H6 中确认。

## 失败路径与边界

| 编号 | 前置状态与操作 | 预期保护或失败行为 | 自动/手动 | 状态 | 证据或备注 |
|------|----------------|--------------------|-----------|------|------------|
| E1 | 慢放/顿帧期间再次触发合成 | 取最强反馈，不叠加；结束后 `timeScale=1` | 自动 | PASS | `TimeDirectorTests.NestedSlowMo_TakesStrongestScaleAndLongestDuration` |
| E2 | 面板显示时点击下层对局 | 下层不可交互 | 自动 + 手动 | PASS（自动）/ 待执行（观感） | `GameBootstrapper.Update` 的 `SetInputBlocked`；`ItemUseTests` |
| E3 | 缺少可选 UI 引用 | 跳过该元素并告警一次，不抛异常 | 自动 | PASS | `PanelController`/`HudView` 全字段空值保护；`PresentationHarness` 最小 HUD 即为该场景 |
| E4 | 贴边元素的 pivot 与锚点不一致 | 元素被裁出画布（**实际发生**：设置按钮顶部 −4 单位） | 自动 | PASS | `HudLayoutTests.TopAnchoredElements_UseSameSidePivot_SoTheyAreNotClipped`、`AllVisibleHudElements_FitInsideDesignCanvas` |
| E5 | 顶栏元素进入微信胶囊保留区 | 与系统胶囊重叠、设置入口被遮挡（**实际风险**：原设计右侧仅留 190 单位，胶囊区需约 300） | 自动 | PASS | `HudLayoutTests.TopBar_DoesNotEnterWeChatCapsuleZone` |
| E6 | 手感反馈组件引用未接线 | R22 的粒子/顿帧/慢放/震屏/飘字全部静默空转（**实际发生**：`HudBuilder` 从未调用 `FeedbackDirector.Configure`） | 自动 | PASS | `BootstrappedSceneTests.Merge_ProducesVisibleFeedback`（真场景）；静态接线见 `HudLayoutTests` 同批自检 |
| E7 | 背景素材缺失（`Art/bg_star` 未导入/被删），或场景里 `Backdrop` 的 Sprite 为空、渲染器被关 | 不能崩：不缩放、不抛异常，由相机 SolidColor 清屏兜底（运行时不创建对象、也不去找素材） | 自动 | PASS | `BackdropViewTests.Backdrop_WithoutSprite_DoesNotThrow`；`SceneAssetTests.Backdrop_IsBehindEveryWorldElement`（节点在 + 排序为负 + 有素材时渲染器必须启用） |
| E8 | 场景里删掉/改坏了必需的 UI 元素（历史上是「只改了生成器却忘记重建 `Main.unity`」） | 真场景缺控件或布局越界 → 运行时静默失效（「改了没反应」）。UI 现在**只在场景里维护**，`Main.unity` 与这几道门禁就是唯一防线 | 自动 | PASS | `SceneAssetTests.HudView_RequiredElementsAreWired`（音量条等在位）+ `HudLayoutTests`（画布内/胶囊避让/底部净空）+ `TMPFontAssetTests.EveryTmpText_InMainScene_UsesBundledFont` |
| E9 | 音量条填充没有运行时接线（历史上是「只在编辑器期用代码挂监听」） | 编辑器期挂的运行时监听不随场景序列化：**音量真的变了、填充条却一直满格**（实际复现过） | 自动 | PASS | `BootstrappedSceneTests.VolumeSliders_AreWiredAtRuntime_AndChangeAudio` 断言 `fillAmount`；接线点在 `PanelController.HookButtons` |
| E10 | 有人把 UI 生成逻辑写回运行时代码（又造一个「Builder」） | 界面重新出现两套来源，「编辑器里看到的」不再是最终效果 | 自动 | PASS | `UiSourceOfTruthTests.RuntimeSources_DoNotCreateUiComponents`（源码扫描）+ `RuntimeAssemblies_DoNotReferenceTheEditorAssembly` |
| E12 | 可交互控件嵌在 `Toggle` 的点击区里（设置页的音量条 `Slider` 曾是开关行的子对象） | 调音量会连带把音效/音乐开关的勾选取消（**实际发生**，2026-09-13 需求方实测）。成因：Unity 沿父链向上找点击处理器，`Slider` 不实现 `IPointerClickHandler` 而 `Toggle` 实现 → 音量条的点击被外层开关吸走；拖动位移超过点击阈值时不发 click，故现象时有时无 | 自动 | **PASS**（结构性修法已落地：两条音量条移出开关行、场景几何零位移；`SettingsToggleClickIsolationTests` **2/2** 由需求方在 Test Runner 内跑通，2026-09-14） | `SettingsToggleClickIsolationTests` 两条：①结构化（`Toggle` 层级内不得有「有射线目标且自身不实现 `IPointerClickHandler`」的控件）；②机制级（`ExecuteEvents.GetEventHandler<IPointerClickHandler>` 的解析结果不得是外层 Toggle）。规则与几何见 `Docs/architecture/05-presentation.md`「面板与红点」 |
| E13 | 任何设置变化（含逐帧拖动音量条）触发 `GameContext.ApplySettingsToAudio` | 正在播放的 BGM 被反复从头播放（**实际发生**，2026-09-14 需求方：「修改音效为什么会导致音乐重新播放」）。成因：`AudioDirector.PlayMusic()` 原先无条件执行 `musicSource.Play()`，等于把播放位置重置到 0；而拖音量条时设置变更**逐帧**触发 | 自动 | **PASS**（需求方在 Test Runner 内跑通：EditMode `MusicPlaybackTests` 4/4、PlayMode `AudioDirectorTests` 含 2 项新增，2026-09-14） | `MusicPlaybackTests`（EditMode 4 项：已在放同一首不重播/首次启动要播/停止后重开要播/换曲子要播）；PlayMode `AudioDirectorTests.VolumeChange_DoesNotRestartMusic` 与 `MusicToggledOffThenOn_RestartsFromTheBeginning`（断言播放位置，无音频设备时 `Assert.Ignore` 跳过，规则由 EditMode 覆盖） |
| E11 | 首次评估安全区时微信桥尚未就绪（开发者工具冷启动会出现 `[jsbridge] invoke getSystemInfo fail: jsbridge not ready`） | `WxSafeAreaSource` 抛异常即回落 `Screen.safeArea`（不崩不卡）；但 `HudBinder.RefreshSafeArea` **只在 `Screen.width/height` 变化时重算**（`HudBinder.cs:336-339`），若首次评估 latch 了回退值、此后画布不再 resize，微信专属补差就不会应用。真机实测正常（画布在 loader 拿到窗口信息后会 resize 一次，从而触发重算） | 手动 | 待观察（**有意不加固**） | 2026-09-13 与需求方确认「能运行、不用改」，故不加「值变化即重算」逻辑，仅留档：将来若出现「顶栏避让在小游戏里没生效」，先查这里 |

## 回归范围

| 受影响模块或契约 | 复验项 | 原因 | 状态 | 证据或备注 |
|------------------|--------|------|------|------------|
| M3 | `03-session-test.md` | 事件契约变化会影响 Binding | PASS | `HudBinderTests` 9/9 |
| M7 | `07-bootstrap-test.md` | 面板与红点调用方、场景装配 | PASS | `BootstrapFlowTests` 6/6、`SceneAssetTests` 6/6 |
| M6 | `06-meta-test.md` | 存档新增 `settingsSfxVolume/settingsMusicVolume`（缺字段按 100%，无 schema 迁移） | PASS | EditMode 全量回归 112/112（含存档迁移/校验用例） |

## 交付结论

- 已验证：A1–A7、A10–A11、A13 与 E1–E6 通过（EditMode 全量 113/113，含本次新增的底部块几何用例）；A8/A9（九宫格 PPU 门禁与面板截图诊断）与 A12 的两项 PlayMode 用例（背景覆盖/层级、音量条读档回填/拖动/存档/填充/开关正交）已由 **PlayMode 全量 83/83** 覆盖。
- 已验证（2026-09-13，V2.53 合成音改用 `pop.ogg`）：**A15 新增 6 项全绿**（定向 13/13）；隔离副本全量 EditMode **140/141**、PlayMode **87/87**、`failed=0`。唯一失败是**既存**的 `HudLayoutTests.SettingsPanel_BottomBlock_SitsAboveTheBottomEdgeWithBalancedSpacing`（场景里 `CloseButton` 被手工改到距底 216、门槛上限 200），本轮未触碰面板几何，与本改动无关。
- 不适用：无。
- 待手动验收：H1–H6（观感、手感、真机帧率）、H7–H8（音量条触感、背景观感）。中文渲染已在真工程中确认解析到系统中文字体 `Microsoft YaHei`，真机仍需确认。**H2 本轮追加待验内容**：`pop.ogg` 的实际听感与连击变调是否自然。
- 未验证：无（此前遗留的「PlayMode 全量未运行」已在本轮批处理运行中补跑：EditMode 116/116 + PlayMode 85/85，`failed=0`）。
- **追加（2026-09-14，V2.56 合成特效行星主题化）**：已验证 A16/A17 与 A1–A15 的回归——隔离副本 `E:\MergeWaterVerify` 全量 **EditMode 147/148**（唯一失败是**既存**的 `HudLayoutTests.SettingsPanel_BottomBlock_SitsAboveTheBottomEdgeWithBalancedSpacing`：场景里 `CloseButton` 被手工改到距底 216、门槛上限 200，本轮未触碰面板几何，与本改动无关）、**PlayMode 87/87**、`failed=0`。反证两项均成立（改回水果主色 / 换回水果占位贴图 / 摘掉 `FruitArt` 注入 → 各自红在预期断言上）。结果 XML：`E:\MergeWaterVerify\Logs\final-editmode.xml`、`final-playmode.xml`、`red-mergefx.xml`、`red-inject.xml`。**待手动验收**：H9（火花观感、亮度、与星球配色是否协调）。
- 未通过：无。
