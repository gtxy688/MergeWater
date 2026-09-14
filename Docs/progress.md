# 进度追踪

> 更新日期：2026-09-13
> 本文件只记录状态和阻塞，不定义需求或架构。

> 玩家反馈的现象与交接说明见 `Docs/bug-handoff-2026-09-11.md`（含未复现声明、环境坑、定位脚本与待查方向）。

## 模块进度

| 模块 | 阶段 | 最近验证 | 待手动验收 | 阻塞 | 下一步 |
|------|------|----------|------------|------|--------|
| M1 Core | 待验收（自动化全绿） | 2026-09-11 EditMode 30 项 PASS | 无（N/A） | 无 | 等真工程本体复跑 + 人工确认 |
| M2 Field | 待验收（自动化全绿） | 2026-09-11 PlayMode 21 项 + 物理诊断 3 项 PASS | H1–H3 | 无 | 手动验收堆叠/合成/越线观感 |
| M3 Session | 待验收（自动化全绿） | 2026-09-11 EditMode 17 项 + PlayMode 4 项 PASS | H1–H3 | 无 | 手动验收单局体验与复活 |
| M4 Aim | 待验收（自动化全绿） | 2026-09-11 15 项 PASS（EditMode 8 + PlayMode 7） | H1–H3 | 无 | 手动验收拖动跟手与真机触屏 |
| M5 Presentation | 待验收（自动化全绿） | 2026-09-13 V2.53 音效轮：新增 `SfxClipAssignmentTests` 5 项 + 真场景音效门禁 1 项全 PASS；全量 EditMode 140/141、PlayMode 87/87 | H1–H6（H2 追加：`pop.ogg` 听感与连击变调） | 无 | 手动验收手感、观感、真机 60fps、中文渲染；**合成音已是 `pop.ogg`，其余音效仍是占位合成音**（要换就继续往 `sfxClips` 里加槽位） |
| M6 Meta | 待验收（自动化全绿） | 2026-09-11 EditMode 31 项 PASS | H1–H5 | 无 | 手动验收上限/冷却/插屏/隐私 |
| M7 Bootstrap | 待验收（自动化全绿） | 2026-09-11 31 项 PASS（EditMode 8 + PlayMode 23） | H1–H4 | 无 | 手动验收首启流程、引导、真机 |

## 自动化测试汇总（2026-09-13 最新一轮）

| 套件 | 结果 | 执行方式 | 证据 |
|------|------|----------|------|
| EditMode | **140 / 141**（唯一失败：`HudLayoutTests.SettingsPanel_BottomBlock_SitsAboveTheBottomEdgeWithBalancedSpacing`——场景里 `CloseButton` 被手工改成距面板底 216、门槛上限 200，**场景侧既有问题、与本轮改动无关**，见下方「待裁定」条目） | **隔离副本 `E:\MergeWaterVerify`** 命令行（2026-09-13 音效轮 V2.53：真工程被运行中的编辑器锁定，故回到副本；批处理退出码 2 = 有失败） | `E:\MergeWaterVerify\Logs\full-editmode-sfx.xml`；真工程本体上一轮为 `Docs/evidence/editmode-results.xml`（含 HUD 布局与安全区、九宫格 PPU、UI 来源不变量、设置面板几何、字体字集、水果美术资产与 `Resources` 死重等门禁） |
| PlayMode | **PASS 87 / 87**（0 失败） | 同上（退出码 0） | `E:\MergeWaterVerify\Logs\full-playmode-sfx.xml`；真工程本体上一轮为 `Docs/evidence/playmode-results.xml`（含加载页单帧卡顿、合成水平初速、地面抬升、真场景端到端、水果视觉尺寸与等级→贴图对应） |
| 合计 | **227 / 228**（无一项失败来自本轮改动） | 同上 | `Docs/evidence/README.md`（真工程本体上一轮） |

> 2026-09-13 本轮复跑（改动：删除全部程序化 UI 工具 + 重收字符集，V2.51）：EditMode **134/135**、PlayMode **87/87**。UI 来源源码门禁做了**反证**——往 `UiPanel.Group` 里临时塞一行 `AddComponent<Canvas>()`，用例立刻失败并指出 `Presentation/UiPanel.cs:32 AddComponent<Canvas>`（证据 `Docs/evidence/uigate-falsify-results.xml`，改回后恢复通过）；字符集重收集后核对「收集字符 ⊆ 已烘字形」= **464 ⊆ 508、缺失 0**（改动前的收集文件留档 `Docs/evidence/TMPCharacters.before-2026-09-13.txt`）。

> 2026-09-13 音效轮复跑（改动：合成音改用真实素材 `Assets/Audios/pop.ogg`，V2.53）：隔离副本 `E:\MergeWaterVerify`（真工程被运行中的编辑器锁定）EditMode **140/141**、PlayMode **87/87**，定向新门禁 **13/13**（`SfxClipAssignmentTests` 5 项 + `SceneAssetTests` 8 项）。唯一失败仍是既存的 `CloseButton` 场景几何（见「待裁定」）。**红灯**：`error CS0246: SfxClipEntry` 找不到（2 处，先写用例后写实现）。命令与结果 XML 见 `Docs/architecture/05-presentation-test.md` 的「自动化运行记录」。

> 2026-09-13 文案轮复跑（改动：玩家可见文案「水果」→「行星」+ 随之重烘字体，V2.52）：EditMode **134/135**、PlayMode **87/87**（数字与 V2.51 轮相同；唯一失败仍是既存的 `CloseButton` 场景几何）。三条字体门禁 `BundledFont_Exists_IsStatic_AndSingleAtlas` / `BundledFont_CoversRequiredTexts_AndPrintablePunctuation` / `EveryTmpText_InMainScene_UsesBundledFont_WithNoDanglingMaterial` 全 Passed；「收集 ⊆ 已烘」= **464 ⊆ 482、缺失 0**。详见 `Docs/evidence/README.md` 的「文案『水果』→『行星』」一节。

> 2026-09-13 全量复跑（本轮改动：水果等级 11→10 + 水果图换 `kenney_planets`，V2.43/V2.44）：EditMode **128/128**、PlayMode **87/87**，`failed=0`，批处理退出码均为 0。运行命令与红/绿灯过程见 `Docs/evidence/README.md`。
>
> 历史：2026-09-11 EditMode 103 / PlayMode 74（真工程 MCP 直连）；2026-09-12 EditMode 116 / PlayMode 85；2026-09-13（减级）EditMode 120 / PlayMode 85；2026-09-13（V2.51）EditMode 134/135 / PlayMode 87/87；2026-09-13（V2.52 文案）同上 134/135 + 87/87；2026-09-13（V2.53 音效）EditMode **140/141** / PlayMode 87/87。

**验证环境说明**：首次运行发生在「由真实工程 `Assets` 同步出的隔离副本 `E:\MergeWaterVerify`」——原因是真工程当时被运行中的编辑器锁定（`Temp/UnityLockfile`），批处理模式无法打开同一工程。随后 MCP 桥接直连本工程，已在**真工程本体**复跑并复核。因此测试结论成立，无遗留验证缺口。详见 `Docs/evidence/README.md`。**2026-09-13（V2.51 起）**：编辑器可关闭，本轮直接在**真工程本体**跑完 EditMode + PlayMode，不再需要副本。另注：Unity 批处理需要写 `%LOCALAPPDATA%\Unity` 下的许可数据库，受限沙箱会以 `attempt to write a readonly database` + 退出码 199 失败——这是环境问题，不是工程问题。

> 副本同步注意：**不要**把真工程的 `Packages/manifest.json` 同步过去（含 Git URL 与 `file:../` 依赖，副本解析不了会让批处理静默卡死）。只同步 `Assets` 即可。

## 真工程本体证据（2026-09-11）

| 检查 | 结果 |
|------|------|
| 真工程内 Test Runner（MCP 直连，含布局修复后复跑） | EditMode 103/103、PlayMode 74/74，0 失败 |
| 真工程编辑器成功编译全部程序集 | 是：8 个运行时/编辑器 DLL + 2 个测试 DLL 均在；无 `.cs` 新于 DLL |
| 编译错误 / MergeWater 异常 | 无（`Logs/AssetImportWorker*.log`） |
| 场景引用可解析 | 是：`Main.unity` 140 处 `guid:` 引用中，抽查 `GameBootstrapper`/`HudBinder`/`GameBalance.asset`/`MetaSettings.asset`/`fruit_circle.png` 全部命中 |
| 构建场景 GUID 一致 | 是（`EditorBuildSettings.asset` 与 `Main.unity.meta` 相同） |

## 已实现能力（对照 GDD）

- 核心循环：按住拖动选落点 → 松手投放 → 2D 物理堆叠 → 同级合成 → 连锁连击 → 越线失败 → 复活/结算 → 再开一局（R1–R7、R26）
- 数值与节奏：10 级水果表（2026-09-13 由 11 级下调，V2.43）、投放等级池按投数分段、连击倍率封顶 2.0、阶段目标 200/500/1000（V1、V2.1–V2.7、V2.18）
- 四种道具：清屏（原「撤销」，2026-09-13 改写 R11）/炸弹/锤子/摇一摇，含每日上限与领取编排（R11–R15、V2.13–V2.15、V2.17）
- 广告与合规：激励视频（复活/道具/摇一摇/大礼包）、插屏每 3 局 ≤1 且可开关、隐私门控、分享 60s 冷却（R16、R17、R19、R20、V2.12、V2.16、V2.19）
- 手感与表现：吸附 60ms、粒子、慢放 0.2×/0.15s、顿帧 80–120ms、震屏三档、连击音阶、警戒线脉冲、占位合成音（R22、V2.20–V2.25）
- 局外：本地存档与迁移、本地排行榜、设置面板、红点、新手引导、埋点（R18、R21、R23、R24、R25）
- 明确未做（范围外）：支付、底部 Banner/信息流/交叉推广、独立开始页、开放数据域好友排行与订阅召回（V1/V1.1）

## 生成物与入口

| 内容 | 路径 | 重建入口 |
|------|------|----------|
| 占位美术 | `Assets/Resources/Placeholder/`（fruit_circle、ui_arrow、ui_dot） | 菜单 `MergeWater/Generate Placeholder Art` |
| 配置资产 | `Assets/Config/GameBalance.asset`、`Assets/Config/MetaSettings.asset` | 菜单 `MergeWater/Generate Config Assets` |
| 主场景 | `Assets/Scenes/Main.unity`（已加入构建场景列表；**UI 的唯一来源**） | **没有生成器**：在编辑器里手工维护并保存（2026-09-13 删除 `Build Main Scene` / `Rebuild UI In Open Scene`，见下） |

## 待办

- [x] 需求文档与架构总览
- [x] 7 个模块架构文档与验收文档
- [x] 根指令文件 `AGENTS.md`
- [x] 程序集边界（8 个运行时/编辑器 + 2 个测试 asmdef）
- [x] 占位美术与配置资产生成工具（原「主场景生成工具」`MergeWater/Build Main Scene` 已于 2026-09-13 按 V2.51 删除，主场景改为手工维护）
- [x] M1–M7 实现
- [x] 全量 EditMode + PlayMode 测试运行与证据记录（最新 **201/201**：EditMode 116 + PlayMode 85，0 失败）
- [x] UI 改为「运行前就存在、运行时不生成」（2026-09-12 需求方要求方便调整）：`HudBuilder` 移入 **Editor 程序集**（运行时编译期无法引用）、删除 `GameBootstrapper` 的运行时兜底（缺引用改为明确报错，并补上原先只校验 `field` 的缺口——`aim` 为空时会在 `GameContext` 构造器里 NRE）、新增 `MergeWater/Rebuild UI In Open Scene`（只重建 UI 子树，保留场景其余内容）、`Build Main Scene` 加「会丢失手工调整」确认；新增 `UiSourceOfTruthTests`（3 项，含反证）。场景等价性核对：139 条路径、0 数值差异（重构未改动任何 UI 内容）
- [x] 对局地面从屏幕底边抬起 0.55（2026-09-12 需求方截图「这个底部，调上面一点」，V2.42）：新增 `GameBalance.floorScreenInset`，`GameContext.SetPlayAreaFromCamera` 改为「屏幕底 + inset」；水果落地后距屏底 96 px（此前 0 px，紧贴最后一行像素像被切）。地面外观仍隐藏、左右墙仍贴屏幕边（D13）。新增 `PlayAreaFloorTests`（真场景 + 渲染复核 + 反证）；同时修正 `PhysicsAndPreviewDiagnostics` 里「地面应贴在屏幕底」的旧断言。**注**：需求方上一轮的同一句话被误当成设置面板底部块处理（V2.41），该面板值保留不回退。**抬升量后经需求方目视定档为 0.06**（2026-09-13，见下条）
- [x] 验收文档回填与任务索引校对
- [x] 真工程编译/资产/场景接线验证
- [x] 真工程本体跑完 Test Runner（MCP 直连，全绿）
- [x] 修复「HUD 满屏底板盖住整个世界」（玩家实测「看不出落点/改完没区别」的根因，2026-09-11 晚）
- [x] 启动加载页（占位美术）+ 待投水果「回中央 / 延迟 0.45s / 渐显 0.25s」（V2.33–V2.35，2026-09-12）
- [x] 容器框改为「屏幕即边框」（方案 B：隐藏墙/地面外观 + 物理边界外移到屏幕边缘，决策 D13，2026-09-12）
- [x] 物理手感二次调整（线性阻尼 2.8→1.4 / 摩擦 0.6→0.5 / 弹性 0.5→0.55 / 合成向上初速 0→0.35，V2.27/V2.29/V2.30/V2.31a）
- [x] 加载页美术化 + 展示时长 1.2→2.0s（V2.35，加缎带标题/果实球/适龄角标/美术进度条）；提示条加宽换行，修掉长句两端裁切
- [x] UI 接入 `Assets/Art` 美术包（面板/按钮/进度条/图标/果实球，V2.36；缺资源时回退占位图）
- [x] 第三轮需求（2026-09-12）：移除 NEXT 预览与阶段目标进度条（V2.37）；里程碑不再发奖、奖励只走广告（V2.38）；合成改左右推 + 弹性压低（V2.27/V2.29/V2.30/V2.31b）；加载页改为「点击开始」保证可见（V2.35）；设置钮换圆形齿轮；修正九宫格圆角缩放（PPU 按用途设置——**方向反了，见下方 V2.40 修复**）
- [x] 连击提示改为在合成位置飘字（V2.39）：顶栏 comboText 移除；「N 连击」字号/颜色随连击升级 + 下方一行「+分数 ×倍率」
- [x] 修「加载页进度条永远是 100%、不会动」（2026-09-12 玩家实测）：进度不再直接累加 `Time.unscaledDeltaTime`（首帧初始化/GC/失焦恢复的单帧可达 0.5~3 s，2 s 最短加载被一两帧走完），改为单帧最多计入 0.15 s（`GameBootstrapper.MaxLoadingStepSeconds`，V2.35）；回归守卫 `BootstrappedSceneTests.LoadingProgress_AfterLongFrameStall_DoesNotJumpStraightToFull`
- [x] 合成力度加大（2026-09-12 需求方「合成给的力度太吝啬」）：`mergeResultSideImpulse` 0.45→1.5 m/s（V2.31b，同步 `Assets/Config/GameBalance.asset`）；新增 `GameFieldMergeTests.MergeResult_SidePushEqualsConfiguredImpulse` 与 `GameBalanceTests` 数值锁定
- [x] 修「设置面板这 UI 是个啥」（2026-09-12 需求方截图，V2.40）：**根因是九宫格边框缩放方向搞反了**——上一轮把素材 PPU 设成 1/2（想「让圆角变大」），而 `Image` 的边框设计单位 = `边框像素 × Canvas.referencePixelsPerUnit(100) ÷ 素材 PPU`，于是 51px 边框变成 5100 设计单位、远超元素尺寸，Unity 只能把边框压满整个 RectTransform，素材被整体拉伸 → 面板成了「大白椭圆套品红圈」、按钮圆角畸形。改为**一律 PPU=100**（10 张九宫格素材）+ 面板文字改浅色（紫底上深棕文字对比度不足）+ 新增 `UiArtSlicingTests`（2 项）与 `UiScreenshotDiagnostics`（把面板渲染成 `Logs/Diagnostics/ui-*.png` 供复核），并重建 `Main.unity`（HUD 是场景序列化产物，改 `HudBuilder` 必须重建场景才生效）
- [x] 设置面板底部块（版本 + 关闭）上移 40 设计单位（2026-09-12 需求方「把底部上移一点」，V2.41）：`HudBuilder` 里 `Version` 160→200、`CloseButton` 60→100（距面板底）；改后「清除缓存 ↔ 底部块」94、按钮下方留白 100，上下接近均分。重建 `Main.unity`（语义比对：139 条路径一致，只有这 2 处位移），截图复核见 `Logs/Diagnostics/ui-02-settings.png`
- [ ] 手动验收（手感、观感、真机 60fps、中文渲染、`Main.unity` 目视检查），清单见各 `<0X>-*-test.md` 的「手动验收」表
- [ ] 正式美术替换（水果本体仍为程序化圆片；UI 已用美术包，但仍是免费素材风格）
- [ ] 发布前把 `GameBootstrapper.requirePrivacyConsent` 打开（恢复 R20 隐私门控），并接入真实激励视频/插屏 SDK（现为 `MockAdsService` 测试位）、微信胶囊/分享/开放数据域
- [ ] 清理临时验证目录 `E:\MergeWaterVerify`（清理前请确认 `Docs/evidence/` 已保留结果 XML）
- [ ] 提交当前工作区（现在只有 1 次初始提交，M1–M7 与全部修复都还是未提交状态）

- [x] UI 全面迁移到 TextMeshPro（决策 D16，2026-09-12）：`HudView`/`HudBuilder`/测试全改 TMP；字体用 SIMYOU 静态烘焙资产；新建 `UiPanel` 面板基类（把「可见」定义收敛到 alpha+raycast+interactable+active 一处）；场景就地迁移工具 `UiTmpMigrator`（40 个 legacy Text → TMP、7 个面板挂 UiPanel 且 alpha 0→1、54 个 HudView 引用重接、12 条飘字池预置），**布局数值与需求方手工调的 Floor 均保留**；运行时不再生成任何 UI（飘字改预置池）。修复 `FloatingTextItem` 与 `FloatingTextSpawner` 同类不同文件导致的 Missing Script
- [x] **中文字体管线重构**（2026-09-13）：改为「字符收集文件 + Static 烘焙」。新增 `Assets/Fonts/TMPCharacters.txt`（烘焙读其中全部非注释字符，无需去重）与 `Assets/Fonts/ChineseUI SDF.asset`（2048² 单图集 / Static / Padding 5 / SDFAA / **508 字 / 0 缺字**，1024² 实测装不下故升档）；工具 `TMPFontBuilder`（收集 + 烘焙，候选 1024²→2048²@75→68→64→多图集）与 `TMPFontReferenceTool`（审计 + 重指）。源 ttf 与字体资产**移出 `Resources/`**（`Resources` 里的资源会被无条件打进包；源 ttf 仅编辑期烘焙需要）→ `Assets/Fonts/`。删除旧工具 `TmpFontAssetBuilder.cs`
- [x] 字体相关**两个真 bug**（2026-09-13，先前就有、本轮查错时发现）：① 隐私说明有 19 个字（微 信 平 台 服 务 等 述 规 则 由 供 及 公 示 者 闲 休 传 力）没烘进字库→Static 模式直接渲染空白；② 飘字池 12 个条目是 Missing Script（`FloatingTextItem.cs` 拆成独立文件后场景仍指向旧 GUID `1887573d…`），`FloatingTextSpawner.Spawn` 遇到 null 条目静默 return → 合成时**完全不出现飘字且不报错**。另修掉 39 处 `m_sharedMaterial` + 12 处 `MeshRenderer.m_Materials` 悬空引用（重建字体资产会换材质子资产 fileID）。门禁：新增 `TMPFontAssetTests`（3 条）与 `SceneAssetTests.MainScene_HasNoReferencesToMissingScripts`（此前无任何测试覆盖脚本引用失效）。验证：反证 `Expected: greater than 0, But was: 0`（连续两次失败，非偶发）→ 修复后 EditMode **120/120**、PlayMode **85/85**
- [x] 对齐/换行核查（2026-09-13）：全场景 52 个 TMP **无一处** Justified/Flush（不存在中文异常间距）；隐私正文已是左上 + 自动换行；加载页《健康游戏忠告》关闭自动换行但 3 行显式换行在 930 宽内放得下（约需 810），按「非排版错误不改」保留
- [x] UI 美术移出 `Resources`（2026-09-13，首包 −4.9MB）：`Assets/Resources/Art`（28 张 UI 图 5.10MB）→ `Assets/UI/Art`，`.meta` 一起移动所以 GUID 保留、场景引用不受影响。`Resources` 里的资源会被无条件打进包，实测其中 4 张零引用（`bg_night.png` 一张 5.0MB + 3 个 icon）纯粹因为躺在 `Resources` 下才进包，移出后不再进。`HudBuilder.LoadArt` 改为 `AssetDatabase` 取图（编辑器工具）；`UiArtSlicingTests`/`BackdropViewTests` 同步改路径。`Assets/Resources/Placeholder/` 按硬约束 7 保留（运行时兜底需要）。验证：场景贴图引用 0 处丢失，EditMode **120/120**、PlayMode **85/85**
- [x] 文档与约束补强（2026-09-13）：AGENTS.md 新增硬约束 9（字体字集唯一来源是 `Assets/Fonts/TMPCharacters.txt`，改文案必须重跑收集+烘焙，否则新字在 Static 下空白）、硬约束 10（`Resources/` 只放运行时要 `Resources.Load` 的东西）；补录 `Assets/Fonts/`、`Assets/UI/Art/` 目录约定
- [x] 地面调参工具 `MergeWater/Floor Tuning (Play Mode)`：地面由 `Awake` 按相机算出，场景里手工挪 `Floor` 会被覆盖；滑杆实时预览 + 一键写回（同时改 `GameBalance.cs` 并直接同步 `GameBalance.asset` 的序列化字段）
- [x] **水果等级 11→10**（2026-09-13，需求方「减少一级，来适配我们的美术资源」，V2.43）：移除原 11 级「大西瓜」，顶级变为 10 级「西瓜」；1–10 级的名称/半径/质量/得分/投放权重**一律不变**。实现改动仅三处：`GameBalance.MaxTier` 11→10 与 `tiers` 少一条、`FruitPalette` 色板 11→10、运行时资产 `Assets/Config/GameBalance.asset` 删掉对应 6 行。**资产改法与一次被证伪的假设**：`Main.unity` 引用了 `GameBalance.asset` 的 GUID（`5af7ebfb…`，1 处），故先按「保留 GUID」的思路**直接手改 `.asset` 的序列化行**（只动 `.asset`，`.meta` 的 LastWriteTime 保持 10:01:35 未变）。随后在隔离副本上实测菜单 `Generate Config Assets`：**执行前后 guid 均为 `5af7ebfb88891b047a82807affaff7c1`，未变化**（Unity 退出码 0）——即「`DeleteAsset` + `CreateAsset` 会换 GUID、导致场景引用失效」的假设**不成立**，该菜单可安全照用；再把副本的菜单生成结果与真工程的手改结果逐行比对，**无任何差异**。两条路径等价，另有 `ConfigAssetTests` 的整表断言（等级数 + 逐级五字段 + 51 项标量）兜底。流程为规格先行：先只改规格测试 → **红灯 5 项**（全部是 `Expected: 10 / False` vs `was 11 / True`，Unity 退出码 2）→ 改实现 → EditMode **120/120**；PlayMode 首次 **84/85**，失败项 `BootstrappedSceneTests.Merge_ProducesVisibleFeedback`（该用例也用「两颗 10 级合成」触发飘字，减级后 10 级不再合成——属规格点漏改，非实现缺陷）→ 改为 9 级对后 PlayMode **85/85**。连带影响（**待手动验收**）：① 顶级分 105→91；② `GetGravityScale` 插值跨度 10→9，**中间等级重力倍率轻微变化**；③ 顶级半径 1.12→1.00。**未重烘字体**：`DisplayName` 不参与任何渲染（`HudBinder.TierName` 是死代码），`大西瓜` 三字形留在图集内无害，`Assets/Fonts/TMPCharacters.txt` 仍保留该条目，下次跑 `MergeWater/Font/1·2` 会自动清掉
- [x] **水果图换成 `kenney_planets`**（2026-09-13，需求方「把水果的图片，从 1-10 依次换成 Planets 中的图片」，V2.44）：等级 1..10 → `planet00..09`，**专属美术不染色**（保留每颗星球自身的配色），缺图或越界才回退占位圆片 + 调色板染色。素材侧：原图备份到 `ArtBackup/kenney_planets_originals_1280/`（在 `Assets/` 之外、不进包）、降采样到 512²、**裁掉四周透明留白**（不透明填充率 0.865→0.998，否则贴在一起的水果之间会露出约 13% 直径的缝）、PPU 统一为 512、同批 `Parts/` 42 张 4.24MB 移出 `Resources` 到 `Assets/Art/kenney_planets/`。代码侧：新增 `FruitArt`（「等级→贴图/染色」的**唯一**规则，M2 生成水果与 M5 待投预览共用）、`FruitBody.EnsureVisual` 改为按贴图**实际世界尺寸**归一化（原写法固定「半径×2」，只在贴图恰好 1×1 世界单位时成立，换成 1280px@PPU100 会放大 12.8 倍直接糊满屏）、`GameBootstrapper` 一次性装载并注入。规格先行：红灯 2 项（`Expected: "planet00", But was: "fruit_circle"`；`Expected: 0.36, But was: 0.9216`，正好是贴图世界尺寸 2.56 倍的放大）→ 实现 → EditMode **128/128**、PlayMode **87/87**。新增门禁 10 项，含「`Resources/kenney_planets` 下除 `Planets/` 外不得有任何文件」的**包体守卫**。**加载页与场景零改动**：`Main.unity`、`HudBuilder.cs`、`FruitPalette.cs` 的 SHA256 与改动前完全一致（需求方明确要求别动手工调好的加载页图）。授权：Kenney「Planets」为 CC0，可商用
- [x] **对局背景换成 `bg_star`**（2026-09-13，需求方「我们游戏场景的背景是怎么生成的？我要把他替换掉」，V2.45）：盘点发现当时**根本没有背景图**——D14 时代素材被关掉后底色是相机纯色清屏米色，`Backdrop` 节点与 `BackdropView` 一直在场、只是 `SpriteRenderer.m_Enabled = 0`。本次启用 `Assets/UI/Art/bg_star.png`（941×1672 星体主视觉）：`Backdrop` 已指派 sprite 并启用，排序值 −100（低于全部对局元素）。**新增菜单 `MergeWater/Assign Backdrop Art (No UI Rebuild)`**，只改 `Backdrop` 一个节点并保存场景——避免用 `Rebuild UI In Open Scene`（会重建整个 UI 子树、覆盖手工调整过的加载页与 HUD）去换一张背景图。素材位置：原先放在 `Assets/Resources/BG/`（`Resources` 下会被**无条件**打进包，2.21MB）→ 移到 `Assets/UI/Art/`；同批的 `小程序头像.png`（小游戏后台商店素材，不属于游戏包）移出 `Assets/` 到 `StoreAssets/`。应用通过 Unity 序列化器（批处理 `-executeMethod`）完成，**没有手改场景 YAML**，场景仅 +145 字节。验证：PlayMode **87/87 全绿**（含 `Backdrop_CoversViewport_AndSitsBehindTheGameplay`）。**待手动验收**：观感与可读性——主视觉高对比高饱和且顶部带标题，可能削弱落点预览线与警戒线的可辨识度（H8；压暗只需改场景里 `Backdrop` 的 SpriteRenderer.Color）
- [ ] **待裁定（不是本次改动引入）**：`HudLayoutTests.SettingsPanel_BottomBlock_...` 当前失败——设置面板 `CloseButton` 在已提交版本里是 y=100（V2.41 既定值），工作区被手工改成 **y=216**，超出门槛上限 200。git 对照已证明成因在场景侧的手工编辑，与背景改动无关。需要需求方决定：改回 100，还是把 V2.41 的既定几何改成 216（并同步门槛与文档）。**未擅自改动**
- [x] **第四轮需求三项**（2026-09-13，需求方）：① **小球出现间隔** V2.33 0.45s → **1.0s**（`GameBalance.nextFruitRevealDelaySeconds` + 资产同步；需求方先说 2 秒，确认代码现值 0.45 秒后定为 1 秒）；② **「撤销」改为清屏**（V2.46）：`ItemUseController.UseUndo` → `UseClearField`（`ClearAll()` 清空全场；空场拒绝且不扣库存），并**删除撤销专用机器** `DropRecord` / `IFieldPort.TryPeekLastDrop` / `GameField.MarkLastDropMerged` / `_lastDrop`；按钮与 Toast 文案改为「清屏」（内部 id 仍为 `ItemKind.Undo`，存档字段 `undoCount`/`undoGrantedToday` 不动以避开 schema 迁移）；③ **清掉运行期 Console 调试信息**（V2.47）：埋点默认走 `NullAnalyticsSink`（要人工看日志就勾 `GameBootstrapper.logAnalyticsToConsole`）、删掉 `[AudioDirector] 未指派 BGM` 启动日志、`AimController` 落点反转告警与 `FloatingTextSpawner` 空池告警改为**只报一次**。验证：EditMode **127/128**（唯一失败仍是需求方手工改的 CloseButton 几何，与本轮无关）、PlayMode **86/86** 全绿。过程中被门禁拦下两处：编译门禁抓出我自己误删的 `GameBootstrapper.SaveStoreOverride`/`ClockOverride` 两个属性；17 项 PlayMode 因 `LogAssert.Expect("未指派 BGM")`（分布在 3 个文件）失败——那正是「删日志」这一**行为变更**的规格点，已同步更新用例与用例名
- [x] **渲染管线由 URP 切回 Built-in RP**（2026-09-13，需求方「我能不能把 URP 改成其他的」→ 选定方案 B，V2.48）：`GraphicsSettings.m_CustomRenderPipeline` 置空、`m_SRPDefaultSettings` 清空、删除 `Main.unity` 里 Main Camera 上的 `UniversalAdditionalCameraData`（共 2 个文件、45 行）。动因：① 去掉微信小游戏「**必须 WebGL2/ES3**」这条硬约束（`WXConvertCore.cs:776-783` 按 `Webgl2` 开关在 ES3/ES2 间二选一，而 URP 需要 ES3）；② 减小包体（URP 会带进整套 shader 变体与管线代码，本工程一个 URP 特性都没用）。**可行性证据（切换前实测）**：代码**零** URP API 调用（全量 grep 只剩 `HudBuilder` 两处 `Shader.Find`，且优先 `Sprites/Default`）、工程内只有 TMP 自带的两个示例材质、场景 3 处 shader 引用**全是内置 shader**、URP 的唯一绑定点就是 `GraphicsSettings` 一处（QualitySettings 各档全是 `{fileID: 0}`）。**验证**：EditMode 127/128（唯一失败是与本轮无关的 CloseButton 几何）、PlayMode **86/86**；并额外用**带图形设备**的批处理（去掉 `-nographics`，否则 `CaptureCamera` 会跳过渲染只写 `.skipped.txt`）产出真实渲染帧逐张目视核对——**世界**（星球堆叠 + `bg_star` 背景 + 警戒线）与 **UI**（加载页《健康游戏忠告》/进度条/百分比/加载中…）全部正常，**无品红、无材质丢失、中文字形完整**。URP 包**保留安装**（`com.unity.feature.2d` 依赖它），`Assets/Settings/{UniversalRP,Renderer2D}.asset` 与 `Assets/UniversalRenderPipelineGlobalSettings.asset` 变为未引用，留作回退
- [x] **小球出现间隔再调大：1.0 → 1.5 秒**（2026-09-13，需求方「把水果落地后的生成时间间隔，再调大点，调成 1.5」，V2.33 的第三次调整）：`GameBalance.nextFruitRevealDelaySeconds` 1.0 → 1.5，同步 `Assets/Config/GameBalance.asset`。**测试影响：无**——唯一涉及时长的 `PhysicsAndPreviewDiagnostics.AfterDrop_PendingFruitReturnsToCenter_AndRevealsGradually` 用 `+3f` 超时轮询（1.5 + 0.25 渐显 = 1.75s 仍在窗口内）；`ConfigAssetTests` 的「V2.33 下一颗延迟」标量断言自动守住磁盘/代码一致。验证：EditMode 127/128（唯一失败仍是 CloseButton 几何）、PlayMode 86/86
- [x] **验证副本补齐微信插件**（2026-09-13，验证环境改造）：需求方按指引给 `GameRoot/EventSystem` 挂上 `WXTouchInputOverride` 后，隔离副本因为没有微信插件而把它判成 Missing Script → `SceneAssetTests.MainScene_HasNoReferencesToMissingScripts` 报假警报。修法：把 `com.qq.weixin.minigame` 作为**内嵌包**放进副本的 `Packages/`（从真工程 `Library/PackageCache` 复制，`.meta` 一并复制所以 GUID 不变，脚本引用照样解析），并在副本 `manifest.json` 加 `"com.qq.weixin.minigame": "file:com.qq.weixin.minigame"`（**不是**同步真工程的 manifest——那份含 git URL 与 `file:../` 依赖，副本解析不了会静默卡死，见既有注意事项）。结果：副本编译无错、`MainScene_HasNoReferencesToMissingScripts` 恢复通过、EditMode 127/128。**副本身份也随之升级**：现在能验证依赖插件的场景，也具备在副本里试跑导出的条件（无需占用真工程编辑器）

- [x] **删除全部程序化 UI 工具：UI 改为纯手工维护**（2026-09-13，需求方「把这个编辑器工具删除，以后只通过手动编辑 UI，而不是程序自动生成」，V2.51）：删除 `HudBuilder.cs`（1408 行 UI 生成器，内含 `Assign Backdrop Art (No UI Rebuild)` 与「选中 Backdrop 节点」两个菜单）、`SceneBuilder.cs`（`Build Main Scene` 用 `NewScene(EmptyScene)` 整场重建；`Rebuild UI In Open Scene` 删掉并重建 UI 子树——**没有 HudBuilder 它只会生成一个没 UI 的废场景**，故一并删除）、`UiTmpMigrator.cs`（2026-09-12 的一次性 legacy Text→TMP 迁移工具，同样程序化改场景里的 UI）。**替代物只有常量，没有任何生成逻辑**：新增 `Assets/Scripts/Editor/MainSceneAsset.cs`（主场景路径，原 `SceneBuilder.ScenePath` 的 5 处引用改指它）与 `UiDesignSpec.cs`（1080×1920 画布、微信胶囊区 300×115、底部净空 60、清屏底色——原 `HudBuilder` 的常量，门禁 `HudLayoutTests` 继续按这套规格断言场景）。**门禁改造**：`UiSourceOfTruthTests` 前两项原先按类型名扫反射（生成器删除后会退化成空断言），改为**扫运行时代码源码**——`AddComponent<Canvas/Image/Button/Toggle/Slider/TMP/HudView/PanelController/UiPanel/…>` 一律红灯（**刻意不含 `CanvasGroup`**：`UiPanel` 在缺组件时给自己补一个，它只是透明度载体，不产生界面元素；注释里写明了这个例外）；「运行时程序集不得引用 `MergeWater.Editor`」作为独立不变量保留。`TMPFontBuilder` 的代码扫描白名单里 `HudBuilder.cs` 一并移除（玩家可见文案现在全部随 UI 对象存在于 `Main.unity`，场景扫描已覆盖）。**场景资产零改动**：`Main.unity` 未被本次改动触碰，加载页与 HUD 的既有手工调整原样保留。**新工作流**：① 换背景图 = 在场景里选中 `GameRoot/Presentation/Backdrop`，拖 Sprite / 调 `SpriteRenderer.Color`；② 加删控件、改锚点与布局、接线全在场景里做（Inspector 的持久监听会被序列化）；③ 改完 UI 靠 EditMode 门禁兜底（`SceneAssetTests` / `HudLayoutTests` / `TMPFontAssetTests` / `UiArtSlicingTests` / `UiSourceOfTruthTests`）。**验证**：真工程本体批处理 EditMode **134/135**（唯一失败是既存的 `CloseButton` 场景几何，见「待裁定」）、PlayMode **87/87**；源码门禁做了反证（临时插入 `AddComponent<Canvas>()` → 立刻红并报出文件行号，证据 `Docs/evidence/uigate-falsify-results.xml`）。**顺带查清的一件事（字符集）**：删掉 `HudBuilder.cs` 白名单后重跑字符收集，字符数 492 → 464：25 个字是只出现在 `HudBuilder` 字面量里的编辑器文案（逐个核对：**都不在 `Main.unity`、不在 Prefab、在运行时代码里只出现在注释与 `Debug.Log` 行**，即玩家看不到），另 2 个字（把/派）是**我自己新写的 `Debug.LogError` 文案**被行级 `Debug.Log` 过滤漏掉带进来的——已把两句报错文案改成不含新字的措辞，使「收集字符 ⊆ 已烘字形」这条不变量成立（复核实测 **464 ⊆ 508、缺失 0**；收集文件改动前留档 `Docs/evidence/TMPCharacters.before-2026-09-13.txt`）。**未重烘字体**：修正后没有任何玩家可见字符缺字形，重烘反而会换掉材质子资产 fileID（历史坑）

- [x] **玩家可见文案「水果」→「行星」**（2026-09-13，需求方「描述改一下，别什么同级水果了，现在是同级行星」，V2.52）：改了 8 处**面向玩家**的文案——`TutorialDirector` 教学提示「同级**行星**相撞会合成更大！」、`GameContext` 摇一摇「**行星**重新落位」、`ItemUseController` 6 条（炸弹无目标/炸弹生效/锤子无目标/锤子生效/清屏无目标/瞄准提示）。**场景 UI 零改动**：`Main.unity` 里没有任何含「水果」的文本（由字符收集文件直接证实），加载页/HUD/面板都不用碰。**连带必做的字体链**：新字「星」（U+661F）**不在已烘图集里**——Static 模式下会渲染成空白，因此按硬约束 9 跑「收集 → 烘焙 → 重指」：收集 464 字（新增「星」）→ 重烘（1024² 装不下 270 个 → **2048² @75pt 单图集、未装入 0 个、占用率 47.9%**，必备文案覆盖检查 ✓）→ 重指。**「重指」不是可选项**：重烘换掉了材质子资产 fileID，审计实测 48 个 TMP 组件里「材质引用为空（悬空）**39** 个」+「MeshRenderer 材质未指向目标材质 **12** 个」→ 重指后 **0 / 0**（与 2026-09-13 早期「重烘后 39/52 悬空、飘字池 12 条 MeshRenderer 指向已删材质」是同型陷阱）。验证：EditMode **134/135**（唯一失败仍是既存的 `CloseButton` 场景几何）、PlayMode **87/87**，三条字体门禁全 Passed；不变量「收集 ⊆ 已烘」= **464 ⊆ 482、缺失 0**。**刻意未改**：代码内部标识（`FruitBody`/`FruitArt`/`FruitPalette`）、`GameBalance.tiers.DisplayName`（西瓜/葡萄…）与 GDD/需求正文仍用「水果」——那是内部命名与策划原始案，统一它是一次纯改名重构，需要时单独立项。日志归档：`Docs/evidence/font-rebake-2026-09-13.log`、`font-repoint-2026-09-13.log`
- [x] **合成音效改用真实素材 `pop.ogg`**（2026-09-13，需求方「查找合成星体的音效文件在哪设置」→「帮我改一下，使用 `pop.ogg`」，V2.53）：改前 `AudioDirector` **只有 BGM 槽位**，8 条音效全部由 `PlaceholderAudioFactory` 现场合成（合成音 = `CreateTone("mw_sfx_merge", 523.25Hz, 0.16s)`），所以把 `pop.ogg` 放进工程也听不到、且不报错。改动三处：① 新增 `SfxClipEntry`（`SfxId` + `AudioClip`）与序列化指派表 `AudioDirector.sfxClips`，`GetClip` 先查表、未命中才合成占位音（`HasClip` 供门禁查询）；② `Main.unity` 的 `GameRoot/Presentation` 上把 **`Merge`（id 1）与 `ComboUp`（id 2）都指向 `pop.ogg`**——连击音阶仍由 `pitch`（`ComboPitch`）实现，不需要第二条素材；③ **修掉一个会删素材的既有缺陷**：`OnDestroy` 原先 `Destroy` 查询表里的全部 `AudioClip`，接上真实素材后会把 `pop.ogg`（还有 BGM）**资产本体删掉**，改为只释放运行时自建的占位音（`_generatedClips`）。规格先行：先写 `SfxClipAssignmentTests`（5 项：指派优先/两 id 共用一个素材/未指派仍回退/空槽位不崩/销毁不删工程资产）+ 真场景门禁 `SceneAssetTests.AudioDirector_MergeSfx_IsAssignedFromProjectAudioAssets`（断言指向 `Assets/Audios/` 下的 `.ogg`，防「拖了没反应」这种无声失效）→ 红灯实测 `error CS0246: SfxClipEntry` 找不到（2 处，正是预期原因）→ 实现后定向 **13/13**、隔离副本全量 EditMode **140/141**、PlayMode **87/87**。**运行环境提醒**（本轮踩到并记入 `05-presentation-test.md`）：`Unity.exe` 是 GUI 子系统程序，PowerShell 直接 `& Unity.exe -batchmode ...` **不会等待**进程结束（表现为空 `$LASTEXITCODE` + 0 字节日志 + 测试还在后台跑），必须用 `Start-Process -Wait -PassThru`；真工程被运行中的编辑器锁定，批处理仍走隔离副本 `E:\MergeWaterVerify`。**待手动验收**：`pop.ogg` 的实际听感与连击变调是否自然（H2 追加项）

- [x] **音量条点击冒泡缺陷改走结构性修法，补丁已撤**（2026-09-13，需求方「这不是屎山吗」→ 明确不要补丁）：两条音量条从开关行下移出，改为 `Panel` 的子对象（`SfxVolumeBar` / `MusicVolumeBar`，与 `SfxToggle`/`MusicToggle` 同级）——它们**不该待在别人的点击区里**，这是根因，不是症状。**几何零位移**（实测）：`anchoredPosition` = `(380.00, 399.02)` / `(380.00, 299.02)`，换算到面板局部仍是 `L=-70 R=320 B=377.02 T=421.02`（音乐行 `B=277.02 T=321.02`），与改前逐位一致（面板 900×1434.047；行左边距面板左边 110，条原在行内 x=270 → 面板内 380）。**不损失任何交互**：开关唯一命中区本就只有勾选框 `Box`（`Label`/`Handle` 的 raycastTarget 都是 0）。曾短暂试过的补丁 `PointerClickBlocker.cs`（空 `IPointerClickHandler` 截断冒泡）**已删除**，工程内引用 0 处。门禁 `SettingsToggleClickIsolationTests` 两条（①结构化：`Toggle` 层级内不得有「有射线目标且自身不实现 `IPointerClickHandler`」的控件；②机制级：直接问事件系统点音量条时解析到谁）；规则与几何依据见 `Docs/architecture/05-presentation.md`「面板与红点」。**验证**：门禁 `SettingsToggleClickIsolationTests` **2/2 Passed**（2026-09-14，需求方在 Test Runner 内定向执行：结构化 + 机制级各一条）；全量 EditMode / PlayMode 回归待跑

## 阻塞

无。模块状态为「待验收」仅因手动验收项尚未由人工确认。

## 过程中发现并修复的实现缺陷（供回归参考）

| 缺陷 | 影响 | 修复 | 覆盖测试 |
|------|------|------|----------|
| `Ready` 阶段输入被 `CanAct`（仅 Playing）挡掉 | **首投无法进行，游戏不可玩** | 新增 `RoundSession.CanAcceptDrop`（Ready 或 Playing），`GameContext.SetInputBlocked` 与 `OnDropRequested` 改用 | `RoundSessionTests.StartRound_...`、`BootstrapFlowTests.SharedScene_OnAccept_RoundIsPlayableThroughRealAimPath` |
| `NewRound()` 未解绑上一局的 `IFieldPort.Merged` | 重开后新旧两局同时计分并重复写存档 | `NewRound` 先 `Session?.Dispose()` | `RoundLifecycleTests.NewRound_..._WithoutLeakingPreviousEvents` |
| 越线判定只看瞬时速度 | 刚生成的水果（速度为 0）会被误判为「静止越线」 | `FruitBody.SettledDuration` 连续静止计时 + `DangerSettleGraceSeconds` | `GameFieldDangerTests.FreshlySpawnedFruitAboveLine_IsNotReportedBeforeSettleGrace` |
| 复活广告可能被播放两次（Session 与 Economy 各播一次） | 重复激励视频、冷却记账错乱 | 职责拆分：M3 只做每局次数与状态应用（`ApplyRevive`），M6/M7 负责冷却与播放 | `RoundLifecycleTests.Revive_WhenAdCompletes_...`（断言 `RewardedShown == 1`） |
| 贴边元素的 pivot 与锚点不一致（pivot 恒为 0.5） | 设置按钮顶部被裁出画布 4 单位（视觉缺陷，逻辑测试抓不到） | `HudBuilder.Place` 改为 pivot 跟随锚点：贴边用同侧 pivot，偏移量语义统一为「距该边距离」 | `HudLayoutTests.AllVisibleHudElements_FitInsideDesignCanvas`、`TopAnchoredElements_UseSameSidePivot_SoTheyAreNotClipped` |
| 设置按钮置于右上角仅留 190 设计单位 | 与微信胶囊保留区（约 300 单位宽）重叠，设置入口会被遮挡 | 重排顶栏：中央分数收窄到 440（右边缘 760 < 胶囊区 780），设置按钮下移到胶囊区下方（纵向 140..216 > 115） | `HudLayoutTests.TopBar_DoesNotEnterWeChatCapsuleZone` |
| **`PanelController` 的面板查找表与按钮监听只在编辑器期装配** | **运行时 `_panels` 为空：隐私弹窗永不显示、所有按钮失效 → 用户实测「启动后没反应」** | `Awake` 中调用幂等的 `EnsureConfigured()` 在运行时重建查找表与监听（`Configure` 改为注入视图 + 触发装配） | `BootstrappedSceneTests`（5 项真场景端到端） |
| `FeedbackDirector` 的组件引用从未接线（`HudBuilder` 未调用其 `Configure`） | R22 手感反馈全部静默空转：无粒子、无顿帧、无慢放、无震屏、无飘字 | `HudBuilder` 构建期接线组件引用；`GameBootstrapper` 在运行时补入数值与震屏目标 | `BootstrappedSceneTests.Merge_ProducesVisibleFeedback` |
| 真场景测试把原生对象留到进程退出时由 GC 终结器回收 | 终结器在 `PhysicsManager` 销毁后访问它 → 批处理模式退出阶段崩溃、退出码 21 | TearDown 中 `Resources.UnloadUnusedAssets()` + `GC.Collect()` + `GC.WaitForPendingFinalizers()`，在物理管理器存活时排空终结器队列 | 复跑 `exit=0`（PlayMode 71/71） |
| **瞄准预览把下落段也算进 V2.26 的 0.8s 预算** | 从生成高度落到地面需约 1.4s，预测线截断在半空（实测最低只到 y≈2.06）→ 玩家「无法判断落点」 | 预览改为「先下落到落点表面，再做 ≤0.8s 反弹预演」；落点表面由 `IPreviewObstacle`（`GameField.GetLandingY`）提供，能停在堆叠面上；重力按当前等级实际值（V2.28）计算，由 M7 每帧同步 | `PhysicsAndPreviewDiagnostics.Aiming_ShowsPreviewReachingTheLandingSurface`（断言最低点到达地面） |
| **圆形碰撞体几乎没有滚动阻力** | 水果落地后一路滚到墙角（实测偏移 1.95，正好是左墙静止位），堆成一层的平摊形态 | 按 D12 采纳参考实现参数：线性阻尼 2.8（V2.27）、重力倍率随等级 1.6→3.6（V2.28）、摩擦 0.6（V2.29）、弹性 0.5（V2.30） | `PhysicsAndPreviewDiagnostics.DroppedFruits_FallMergeAndDoNotScatterToWalls`、`FruitsOfDifferentTiers_StackOnEachOther`（实测 4 颗叠成 4 层塔、横向偏移 0.03） |
| **合成结果被施加向上初速（0.6）** | 结果水果跳到别处并在堆顶制造扰动，助推「堆不平」 | 按 V2.31 改为零初速生成，就地对位由求解器推开邻居 | `GameFieldMergeTests.MergeResult_SpawnsAtRestAtMidpoint` |
| **同级水果只监听碰撞进入** | 因一次合成被取消而持续贴合的同级水果再也不触发合成 → 表现为「贴着但不动/不合成」 | 按 V2.32 在碰撞停留时也尝试合成（`_merging` 去重保证安全） | `FruitBody.OnCollisionStay2D`；`GameFieldMergeTests` |
| **改动 `GameBalance` 代码默认值后未重新生成配置资产** | 运行时仍读旧值（本轮摩擦/阻尼/重力倍率一度全部未生效，白跑一轮） | `AGENTS.md` 硬约束 2 补注：必须重新生成 `Assets/Config/GameBalance.asset`；`ConfigAssetTests` 会失败以提示 | `ConfigAssetTests.GameBalanceAsset_Exists_AndMatchesCodeDefault` |
| **水果生成高度落在 HUD 顶栏之后** | 待投水果与预测线上段被大号分数/设置按钮遮住（实测中心在屏幕顶部 7%，顶栏占到 15%）→ 玩家「看不出落点、以为拖了没反应」 | 生成高度 5.2 → 4.0（V2.26a），使待投水果落在「顶栏之下、警戒线之上」的可见区间；预览重力按当前等级实际值（V2.28）计算 | `PhysicsAndPreviewDiagnostics.Aiming_ShowsPreviewReachingTheLandingSurface`（断言中心在顶栏之下：实测 17.8%） |
| **容器墙体/地面只有碰撞体、没有渲染器** | 水果像掉进虚空：玩家看不到场地边界与地面位置；且容器原本只在「第一次投放」时才创建，开局第一帧更是什么都没有 | 墙体/地面增加子节点视觉（`ui_square` 占位图 + 颜色，排序号 -10），并在 `GameField.Awake` 就建好容器。**2026-09-12 起改走 D13「屏幕即边框」：外观默认隐藏、边界外移到屏幕边缘**，该视觉代码保留在 `showArenaVisuals` 开关后 | `PhysicsAndPreviewDiagnostics`（现断言：Arena 三个子节点保留启用的碰撞体、外观已隐藏，且 `PlayHalfWidth/PlayFloorY` 贴合相机可视范围） |
| **`DangerLineView` 的线引用是非序列化字段** | 运行时为 null → `SetLine` 提前返回 → **警戒线从未显示过**（位置与颜色都没写进去）；且场景里烘焙的常态色是「白色 35% 透明」，画在米色背景上等于没有 | 改用序列化引用 + 运行时兜底解析；常态色改为可见柔和红，并在 `SetLine` 时写入 LineRenderer（不只改字段）；另加运行时可见性守卫 | `PhysicsAndPreviewDiagnostics`（断言警戒线常态 alpha ≥ 0.4） |
| **`Configure(buildArena:false)` 未清理 `Awake` 已建的场地** | 该语义下墙体仍存在，拦住了本该掉出场地的水果 | 显式要求无容器时销毁 Arena 根节点 | `GameFieldLifecycleTests.EscapedFruit_IsRecycledInsteadOfLeaking` |
| **诊断截图工具自身说谎** | 为把 HUD 拍进图而临时把 Canvas 切成 ScreenSpaceCamera，UI（排序号 0）压住了排序号为负的容器墙体 → 截图误示「容器不可见」，差点去改一个不存在的 bug | 截图工具回到「只渲染世界」；结论以像素/坐标数据为准 | 复查像素与渲染包围盒后确认容器正常 |
| **HUD 的满屏不透明底板盖住整个世界** | Canvas 是 ScreenSpaceOverlay，**永远绘制在世界之上**：`Canvas/Background`（铺满屏幕、alpha=1）把水果、容器、警戒线、待投水果与落点预测线全部遮住 → 玩家只能看到 HUD，实测为「无法拖动水果确定落点」「改了预览/物理参数也没区别」（与输入链路、物理参数无关） | 删除该满屏底板，底色改由相机 SolidColor 清屏提供：新增 `HudBuilder.BackgroundColor`（0.99/0.93/0.84，与原相机清屏色一致），`HudBuilder.Build` 与 `SceneBuilder.BuildCamera` 共用同一常量；重建 `Main.unity` 使既有场景同步 | `HudLayoutTests.NoOpaqueFullScreenGraphic_HidesTheGameWorld`（新增，扫 Canvas 下所有启用图形，禁止「铺满 + 不透明」；改 `HudBuilder` 后忘记重建场景也会被它拦住） |
| **对局底色是一块米色纯色，需求方认为「丑」** | 米色底在竖屏上占满整屏、没有任何内容；美术包 `Assets/Art` 里现成的 `GUI/Background_Images/background-1` 一直没被用上 | 按需求方「可以用 Art 文件夹的资源」，复制为 `Art/Resources/bg_night.png`（3840×2160，PPU=100，maxTextureSize 4096），新增 `BackdropView` 用**世界空间 SpriteRenderer + cover 缩放**铺满视口（排序 -100，低于全部对局元素），相机清屏色改为与素材一致的夜空色作为兜底；三张候选背景做了竖屏居中裁切对比后选 `background-1`（中央留白最干净、装饰云留在被裁掉的两侧） | `BackdropViewTests`（5 项：三种宽高比覆盖 + 超宽屏重贴合 + 无素材降级）、`SceneAssetTests.Backdrop_IsBehindEveryWorldElement`、`BootstrappedSceneTests.Backdrop_CoversViewport_AndSitsBehindTheGameplay` |
| **设置面板没有音量条**（需求方反馈「音量条丢失」） | 设置页只有 音效/音乐/震动 三个开关，玩家只能开/关、无法调音量；美术包 Settings_Demo 本身就是 Sound/Music 两条音量条 | 用美术包 Medium 版 `sound-bar-container`（390×44 轨道）+ `sound-bar-full`（376×24 分段填充）+ 分段素材做滑钮，拼成音量条**行内嵌在 音效/音乐 两行右侧**（不占额外纵向空间）；存档新增 `settingsSfxVolume/settingsMusicVolume`（缺字段默认 100%），`AudioDirector` 音量与开关正交（音乐基准增益 0.4） | `SceneAssetTests.HudView_RequiredElementsAreWired`、`BootstrappedSceneTests.VolumeSliders_AreWiredAtRuntime_AndChangeAudio`；运行时复验：读档回填 0.35/0.60 → 拖动后存档与音频同为 0.80/0.40、填充同步、关开关后音量保留 |
| **音量条填充只由 `HudBuilder` 挂监听** | 编辑器期挂的 `onValueChanged` 不会被序列化进场景：实机表现为「音量真的变了、填充条却一直满格」（本次实现中真实复现） | 填充与音量的绑定统一挪到运行时装配的 `PanelController.HookButtons`（与按钮监听同一个坑），`HudBuilder` 只创建控件、不再挂监听 | 运行时脚本复验（填充 0.80 跟随）；`BootstrappedSceneTests` 断言 `fillAmount` |
| **背景图换上后需求方要求「换成原来的就行」** | 米色纯色底 vs 美术包夜空图属观感取舍，需求方看过实际效果后选择保留原底色 | `HudBuilder.BackdropArtName` 置空（`BuildBackdrop` 自动关渲染器）、相机清屏色恢复米色 `(0.99,0.93,0.84)`；`Backdrop` 节点与 `BackdropView` 保留，重新启用只需填回资源名并重建场景。场景断言改为「节点在 + 排序为负 + 有素材时渲染器必须启用」，两种状态都成立 | EditMode 复跑 112/112；运行时复验 `sprite=null / enabled=False / 清屏色=米色` |
| **取消勾选音效/音乐后仍能拖动音量条** | 开关只静音、滑条照旧可拖，玩家会以为「关了还能调」/「调了没反应」（需求方 2026-09-12：「取消勾选时应该禁止调节大小」） | `PanelController.HookButtons` 运行时接线：开关变化 → 对应滑条 `interactable = isOn`（`DisabledColor` 变暗提示），`SetVolumeStates` 在打开设置/读档回填时对齐；不做强制清零，档位保留 | 运行时脚本复验：取消勾选 → `interactable=False` 且存档音量保留 0.45，重新勾选 → 可调且值仍 0.45；`BootstrappedSceneTests` 同步断言 |
| **`AudioDirector.OnDestroy` 会销毁查询表里的全部 `AudioClip`**（接真实素材前无害，因为表里只有自己 new 出来的占位音） | 一旦把工程素材填进 `sfxClips`，销毁组件就会对 `pop.ogg`/BGM 调 `Destroy` → **素材本体被删**（历史形态：`Destroy` 一个资产引用在编辑器里是真的删文件） | 新增 `_generatedClips` 记录「运行时自建的占位音」，`OnDestroy` 只释放这些；工程资产不动。指派表由 `EnsureClipsLoaded` 装配（`GetClip`/`Configure`/`HasClip` 都先调它，故 `AddComponent` 后立刻取也拿得到） | `SfxClipAssignmentTests.OnDestroy_DoesNotDestroyClipsThatComeFromProjectAssets` |
| **音效没有任何「SfxId → 素材」入口** | 需求方把 `pop.ogg` 放进 `Assets/Audios/` 后合成音仍是运行时合成的 523Hz 短音，且**不报错**（占位音设计使然），排查只能靠读代码 | 新增序列化指派表 `AudioDirector.sfxClips`（`SfxClipEntry{ id, clip }`），在 `Main.unity` 的 `GameRoot/Presentation` 上指派 `Merge`/`ComboUp` → `pop.ogg` | `SfxClipAssignmentTests`（5 项）+ 真场景门禁 `SceneAssetTests.AudioDirector_MergeSfx_IsAssignedFromProjectAudioAssets` |
| **开关行里嵌了音量条 → 点音量条会把开关的勾选取消** | 需求方 2026-09-13 实测：「打开设置页调整音量后，会自动把音量的勾选取消」（音效行同理）。**成因不在业务代码，在场景层级**：Unity 处理点击是沿父链向上找第一个实现者——按下的 `IPointerDownHandler` 找到 `Slider`（音量正常改），抬手的 `IPointerClickHandler` 却跳过 `Slider`（**它不实现这个接口**）落到外层 `Toggle` → `ToggleValue()`。拖动位移超过点击阈值时 Unity 不发 click，所以现象时有时无 | **结构性修法**（需求方明确不要补丁）：把两条音量条（`SfxVolumeBar`/`MusicVolumeBar`）从 `SfxToggle`/`MusicToggle` 下**移到 `Panel` 下同级**；几何上零损失——开关唯一命中区本就只有勾选框 `Box`（`Label`/`Handle` 的 raycastTarget 都是 0）。曾短暂试过「挂空 `IPointerClickHandler` 截断事件」的补丁（`PointerClickBlocker`），**已弃用并删除** | `SettingsToggleClickIsolationTests` 两条：①结构化（`Toggle` 层级内不得有「有射线目标且自身不实现 `IPointerClickHandler`」的控件）；②机制级（`ExecuteEvents.GetEventHandler<IPointerClickHandler>` 解析结果不得是外层 Toggle） |
| **任何设置变化都会让 BGM 重头播放** | 需求方 2026-09-14 实测：「修改音效为什么会导致音乐重新播放」（改音乐音量同理）。根因：`SettingsService.Changed` → `GameContext.ApplySettingsToAudio` 里先 `SetMusicEnabled(已开=true)`（内部调 `PlayMusic`）、末尾再补一次 `PlayMusic`，而 `PlayMusic` 原先**无条件** `musicSource.Play()` = 把播放位置重置到 0；**拖音量条时存档值逐帧变化 → 逐帧重播** | `PlayMusic()` 幂等化：换曲子、或当前没在播才真正 `Play()`，已在放同一首则直接返回；判定抽成纯函数 `AudioDirector.ShouldStartMusic`。需求方明确要求：只有「关闭音乐后再打开」才允许重播 | `MusicPlaybackTests`（EditMode 4 项，规则）+ PlayMode `AudioDirectorTests.VolumeChange_DoesNotRestartMusic`、`MusicToggledOffThenOn_RestartsFromTheBeginning`（播放位置断言） |

## 文档健康度

- [x] 已实现模块的公开契约与代码一致（含 `ISessionView`、`IFieldPort.HasFruitInRadius`、`IAdsService.IsShowing` 等实现中新增的契约，已回写架构文档）
- [x] 行为变化已更新对应验收文档（复活职责拆分、D9 字体、D10 反馈驱动、D11 领取即用均已记录）
- [x] 新任务关键词已加入 `00-overview.md` 唯一索引
- [x] 需求数值没有复制到其他权威位置（架构与验收仅引用编号）
- [x] 各模块验收文档的自动化项均已回填 PASS 与运行记录；未执行项按要求区分为「待手动验收」与「未验证」
