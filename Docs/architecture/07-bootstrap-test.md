# M7 Bootstrap 验收文档

> 对应架构：`07-bootstrap.md`
> 对应需求：`Docs/requirements.md` 的 R10、R11–R17、R20、R21、R23、R26

## 自动化测试计划与证据

| 编号 | 层级 | 测试路径与名称 | 行为 | 红灯原因或 N/A 理由 | 最近结果 |
|------|------|----------------|------|---------------------|----------|
| A1 | EditMode | `Assets/Tests/EditMode/Bootstrap/SceneAssetTests.cs::MainScene_ContainsBootstrapperFieldAimCanvasAndPresentation`、`Bootstrapper_SerializedReferencesAreWired`、`HudView_RequiredElementsAreWired`、`MainScene_HasNoBottomBannerOrCrossPromotionSlots`、`MainScene_IsInBuildSettings` | 真实打开 `Main.unity` 并断言组件齐备、11 个序列化引用已装配、HUD 关键元素在位、无广告/推广节点、已加入构建场景 | 场景未生成时失败 | PASS |
| A2 | EditMode | `Bootstrap/ConfigAssetTests.cs::GameBalanceAsset_Exists_AndMatchesCodeDefault`、`MetaSettingsAsset_Exists_WithExpectedDefaults`、`PlaceholderArt_ExistsAndImportsAsSprite` | 磁盘配置资产与代码默认值（即需求 V1/V2）逐项一致；占位图正确导入为 Sprite | 资产未生成时失败 | PASS |
| A3 | PlayMode | `Assets/Tests/PlayMode/Bootstrap/BootstrapFlowTests.cs::WhenPrivacyNotAccepted_AnalyticsAndAdsAreNotInitialized_AndPanelShown`、`Analytics_BeforeConsent_DropsEvents` | 未同意隐私时零上报零广告并显示隐私弹窗 | 实现前无门控 | PASS |
| A4 | PlayMode | `Bootstrap/BootstrapFlowTests.cs::AfterPrivacyAccepted_RoundStartsDirectlyWithoutHomePage`、`AcceptButton_InitializesAnalyticsAndStartsRound`、`SharedScene_OnAccept_RoundIsPlayableThroughRealAimPath` | 同意后直接进入可操作对局（R26）；真实「按下→松手」路径能完成首投 | 同上 | PASS |
| A5 | PlayMode | `Bootstrap/RoundLifecycleTests.cs::NewRound_ResetsScoreComboDangerAndQueue_WithoutLeakingPreviousEvents`、`RetryButton_CompletesRoundAndStartsNewOne` | 重开一局完整重置、旧局事件不串局；「再来一局」结算并累计局数 | 实现前无 `NewRound` | PASS |
| A6 | PlayMode | `Bootstrap/ItemUseTests.cs`（7 项，含 `UseBomb_DeductsStockAndRemovesFruits`、`ClearField_RemovesEveryFruit_AndDeductsStock`、`ClearField_OnEmptyField_IsRejectedWithoutSpendingStock`、`CancelAim_DoesNotDeductStockOrRemoveFruit`、`EmptyStock_PlaysRewardedAdThenContinuesToAim`、`Shake_PlaysAd_MovesFruitsAndConsumesDailyQuota`、`UseBomb_WithNoFruitInRadius_DoesNotDeductStock`） | 道具生效与扣减一致；库存为 0 时先看广告再自动继续使用（D11）；取消/空放都不扣库存。**2026-09-13：「撤销」改清屏后，原 `Undo_RemovesLastUnmergedDrop_AndDeductsStock` 改写为两条用例**（清空全场并扣 1；空场拒绝且不扣） | 实现前无编排 | PASS |
| A7 | PlayMode | `Bootstrap/MilestoneRewardTests.cs::MilestoneReached_GrantsItemOnceAndPersists`、`MilestoneReached_IsNotGrantedTwiceInSameRound` | 阶段目标奖励每局每节点一次并写入存档 | 同上 | PASS |
| A8 | PlayMode | `Bootstrap/RoundLifecycleTests.cs::Revive_WhenAdCompletes_RemovesClusterAndResumes_ThenSecondAttemptRejected`、`Revive_WhenAdSkipped_KeepsRevivingState` | 复活只播放一次广告；完成后清簇续玩并写冷却；广告未完成时状态不变 | 同上 | PASS |
| A9 | PlayMode | `Bootstrap/BootstrapFlowTests.cs::MissingFieldReference_LogsErrorAndDoesNotThrow` | 场景缺少 `GameField` 时记录错误并停止对局初始化，不抛未捕获异常 | 同上 | PASS |
| A10 | PlayMode | `Bootstrap/BootstrappedSceneTests.cs`（9 项：`FirstLaunch_LoadingScreenWaitsForTapBeforeStarting`、`LoadingProgress_AfterLongFrameStall_DoesNotJumpStraightToFull`、`FirstLaunch_LoadingScreenShowsThenStartsRoundDirectly`、`WhenPrivacyConsentRequired_PrivacyPanelIsActuallyVisible`、`AcceptButton_IsWiredAtRuntime_AndStartsRoundDirectly`、`FirstRound_InEditor_ShowsMouseAndGameViewInstructions`、`SettingsButton_IsWiredAtRuntime_AndPanelBecomesVisible`、`RealDrop_ThroughAimPath_SpawnsFruit`、`Merge_ProducesVisibleFeedback`） | **加载真场景**走真实 UI/输入路径，断言玩家可观察结果：加载页真的可见、进度满才提示「点击开始」、隐私弹窗真的可见可点、同意按钮真的生效并直接开局、设置按钮真的能打开面板、真实「按下→松手」真的能生成水果、合成真的产生飘字且粒子/震屏已接线 | **修复「启动后无响应」后新增**：其余测试用代码搭脚手架并自行调用 `Configure`，会掩盖「只在编辑器期装配、运行时失效」的缺陷。`LoadingProgress_AfterLongFrameStall_...` 为 2026-09-12「进度条永远 100%、不会动」的回归守卫 | PASS |

## 自动化运行记录

| 日期 | Unity 版本与环境 | 命令或 Test Runner 过滤器 | 结果文件 | 结论 |
|------|------------------|---------------------------|----------|------|
| 2026-09-11 | Unity 2022.3.62f3 / Windows 10 / `-batchmode -nographics` | EditMode 与 PlayMode 各一次 | `Logs\editmode-results.xml`、`Logs\playmode-verify.xml` | PASS — 本模块 31 项（EditMode 8 + PlayMode 23） |

> 环境说明：先在由真工程同步出的隔离副本上运行，随后 MCP 直连真工程本体复跑，两次结果一致（EditMode 103/103、PlayMode 74/74）。详见 `Docs/evidence/README.md`。

## 手动验收前置条件

- 场景、Prefab 与配置：直接使用已提交的 `Assets/Scenes/Main.unity`（**场景与界面靠手工维护，没有生成器**）；占位美术/配置资产由菜单 `MergeWater/Generate Placeholder Art` / `MergeWater/Generate Config Assets` 生成。
- 依赖模块状态：M1–M6 全部实现。
- 目标设备与画质档位：Editor Play Mode；真机竖屏。

## 手动验收

| 编号 | 操作步骤 | 可观察预期结果 | 环境与构建 | 执行者/日期 | 状态 | 证据或备注 |
|------|----------|----------------|------------|-------------|------|------------|
| H1 | 首次启动（无存档） | 必现隐私弹窗；同意后直接进入可操作对局，无独立开始页 | Editor | 待执行 | 待手动验收 | R20/R26（自动化已验证门控与直接开局） |
| H2 | 首局按提示完成第一次合成 | 出现拖动箭头、首次合成高亮、越线预警；前 3 局结算页有分享/排行引导 | Editor | 待执行 | 待手动验收 | R23 |
| H3 | 依次使用清屏/炸弹/锤子/摇一摇 | 各自效果符合 R11–R14（R11 已改为**清屏**），数量正确扣减，摇一摇不直接消除水果；**空场时点清屏应被拒绝且不扣库存** | Editor | 待执行 | 待手动验收 | R11–R14（自动化已验证效果与扣减） |
| H4 | 真机竖屏完整试玩一局 | 3 秒内理解操作；单局 2–4 分钟；中端机 60fps；顶部设置按钮不被微信胶囊遮挡 | 真机 | 待执行 | 待手动验收 | GDD §12、V3 |
| H5 | 启动游戏（含慢机/编辑器卡顿、失焦后切回） | 进度条从 0 看得见地走到 100%，再提示「点击开始」；不会一上来就是 100% 且静止 | Editor + 真机 | 待执行 | 待手动验收 | V2.35（2026-09-12「进度条永远是 100%、不会动」；自动化已用 3 s 单帧卡顿复现并锁定） |

## 失败路径与边界

| 编号 | 前置状态与操作 | 预期保护或失败行为 | 自动/手动 | 状态 | 证据或备注 |
|------|----------------|--------------------|-----------|------|------------|
| E1 | 场景缺少 `GameField` 引用 | 记录错误并停止对局初始化，不抛未捕获异常 | 自动 | PASS | `BootstrapFlowTests.MissingFieldReference_LogsErrorAndDoesNotThrow` |
| E2 | 连点「再来一局」 | 幂等，不产生多个 Session 或重复订阅 | 自动 | PASS | `RoundLifecycleTests.NewRound_..._WithoutLeakingPreviousEvents` |
| E3 | 广告播放期间点击屏幕 | 不投放、不使用道具 | 自动 | PASS | `SetInputBlocked` 依据 `Ads.IsShowing` 与当前面板；`ItemUseTests` |
| E4 | 清除缓存后 | 立刻回到隐私弹窗，需重新同意；不在未同意状态下开局 | 自动 + 手动 | PASS（逻辑）/ 待执行（观感） | `GameBootstrapper.OnClearCache`、`Settings_Persist_AndClearCacheResetsEverything` |
| E5 | 运行时装配失效：面板查找表与按钮监听只在编辑器期建立 | 隐私弹窗不显示、所有按钮点不动（**用户实测表现：启动后毫无反应**） | 自动 | PASS | `BootstrappedSceneTests.FirstLaunch_PrivacyPanelIsActuallyVisible`、`SettingsButton_IsWiredAtRuntime_AndPanelBecomesVisible`；修复见 `PanelController.Awake` |
| E6 | `FeedbackDirector` 的组件引用从未接线 | R22 手感反馈（粒子/顿帧/慢放/震屏/飘字）全部静默空转 | 自动 | PASS | `BootstrappedSceneTests.Merge_ProducesVisibleFeedback`、`FeedbackDirector.ScreenShakeTargetAvailable` |
| E7 | 加载/卸载真场景后原生对象由 GC 终结器在关机时回收 | 批处理模式退出阶段崩溃、退出码非 0（破坏 CI 门禁） | 自动 | PASS | TearDown 中 `UnloadUnusedAssets` + `GC.Collect` + `WaitForPendingFinalizers`；复跑 `exit=0` |

## 回归范围

| 受影响模块或契约 | 复验项 | 原因 | 状态 | 证据或备注 |
|------------------|--------|------|------|------------|
| M1–M6 | 各自验收文档的自动化项 | 组合根装配变更影响全链路 | PASS | 合计 177 项全绿 |

## 交付结论

- 已验证：A1–A10 与 E1–E7 全部通过（本模块 31 项；全项目 177 项全绿，两套件退出码均为 0）。
- 不适用：无。
- 待手动验收：H1–H5（首启流程、引导、道具手感、真机、加载页进度条）。
- 未验证：无（201 项自动化测试通过：EditMode 116 + PlayMode 85，见 `Docs/evidence/README.md`）。`Main.unity` 的目视检查并入 H1/H4 的手动验收。
- 未通过：无。
