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
| A6 | PlayMode | `Bootstrap/ItemUseTests.cs`（6 项，含 `UseBomb_DeductsStockAndRemovesFruits`、`Undo_RemovesLastUnmergedDrop_AndDeductsStock`、`CancelAim_DoesNotDeductStockOrRemoveFruit`、`EmptyStock_PlaysRewardedAdThenContinuesToAim`、`Shake_PlaysAd_MovesFruitsAndConsumesDailyQuota`、`UseBomb_WithNoFruitInRadius_DoesNotDeductStock`） | 道具生效与扣减一致；库存为 0 时先看广告再自动继续使用（D11）；取消/空放都不扣库存 | 实现前无编排 | PASS |
| A7 | PlayMode | `Bootstrap/MilestoneRewardTests.cs::MilestoneReached_GrantsItemOnceAndPersists`、`MilestoneReached_IsNotGrantedTwiceInSameRound` | 阶段目标奖励每局每节点一次并写入存档 | 同上 | PASS |
| A8 | PlayMode | `Bootstrap/RoundLifecycleTests.cs::Revive_WhenAdCompletes_RemovesClusterAndResumes_ThenSecondAttemptRejected`、`Revive_WhenAdSkipped_KeepsRevivingState` | 复活只播放一次广告；完成后清簇续玩并写冷却；广告未完成时状态不变 | 同上 | PASS |
| A9 | PlayMode | `Bootstrap/BootstrapFlowTests.cs::MissingFieldReference_LogsErrorAndDoesNotThrow` | 场景缺少 `GameField` 时记录错误并停止对局初始化，不抛未捕获异常 | 同上 | PASS |

## 自动化运行记录

| 日期 | Unity 版本与环境 | 命令或 Test Runner 过滤器 | 结果文件 | 结论 |
|------|------------------|---------------------------|----------|------|
| 2026-09-11 | Unity 2022.3.62f3 / Windows 10 / `-batchmode -nographics` | EditMode 与 PlayMode 各一次 | `Logs\editmode-results.xml`、`Logs\playmode-verify.xml` | PASS — 本模块 26 项（EditMode 8 + PlayMode 18） |

> 环境说明：同 `01-core-test.md` —— 运行在由真实工程同步出的隔离副本上。`Main.unity` 与配置资产由 `MergeWater/Build Main Scene` 在副本内生成后再回同步到真工程，回同步后用真工程 `Assets` 重建副本复跑过一次（EditMode 97/97、PlayMode 66/66），因此场景引用与 `.meta` GUID 的有效性已间接验证。

## 手动验收前置条件

- 场景、Prefab 与配置：先用 `MergeWater/Build Main Scene` 生成场景与占位美术/配置（菜单一次完成），或直接使用已提交的 `Main.unity`。
- 依赖模块状态：M1–M6 全部实现。
- 目标设备与画质档位：Editor Play Mode；真机竖屏。

## 手动验收

| 编号 | 操作步骤 | 可观察预期结果 | 环境与构建 | 执行者/日期 | 状态 | 证据或备注 |
|------|----------|----------------|------------|-------------|------|------------|
| H1 | 首次启动（无存档） | 必现隐私弹窗；同意后直接进入可操作对局，无独立开始页 | Editor | 待执行 | 待手动验收 | R20/R26（自动化已验证门控与直接开局） |
| H2 | 首局按提示完成第一次合成 | 出现拖动箭头、首次合成高亮、越线预警；前 3 局结算页有分享/排行引导 | Editor | 待执行 | 待手动验收 | R23 |
| H3 | 依次使用撤销/炸弹/锤子/摇一摇 | 各自效果符合 R11–R14，数量正确扣减，摇一摇不直接消除水果 | Editor | 待执行 | 待手动验收 | R11–R14（自动化已验证效果与扣减） |
| H4 | 真机竖屏完整试玩一局 | 3 秒内理解操作；单局 2–4 分钟；中端机 60fps；顶部设置按钮不被微信胶囊遮挡 | 真机 | 待执行 | 待手动验收 | GDD §12、V3 |

## 失败路径与边界

| 编号 | 前置状态与操作 | 预期保护或失败行为 | 自动/手动 | 状态 | 证据或备注 |
|------|----------------|--------------------|-----------|------|------------|
| E1 | 场景缺少 `GameField` 引用 | 记录错误并停止对局初始化，不抛未捕获异常 | 自动 | PASS | `BootstrapFlowTests.MissingFieldReference_LogsErrorAndDoesNotThrow` |
| E2 | 连点「再来一局」 | 幂等，不产生多个 Session 或重复订阅 | 自动 | PASS | `RoundLifecycleTests.NewRound_..._WithoutLeakingPreviousEvents` |
| E3 | 广告播放期间点击屏幕 | 不投放、不使用道具 | 自动 | PASS | `SetInputBlocked` 依据 `Ads.IsShowing` 与当前面板；`ItemUseTests` |
| E4 | 清除缓存后 | 立刻回到隐私弹窗，需重新同意；不在未同意状态下开局 | 自动 + 手动 | PASS（逻辑）/ 待执行（观感） | `GameBootstrapper.OnClearCache`、`Settings_Persist_AndClearCacheResetsEverything` |

## 回归范围

| 受影响模块或契约 | 复验项 | 原因 | 状态 | 证据或备注 |
|------------------|--------|------|------|------------|
| M1–M6 | 各自验收文档的自动化项 | 组合根装配变更影响全链路 | PASS | 合计 163 项全绿 |

## 交付结论

- 已验证：A1–A9 与 E1–E4 全部通过（本模块 26 项；全项目 163 项全绿）。
- 不适用：无。
- 待手动验收：H1–H4（首启流程、引导、道具手感、真机）。
- 未验证：在真工程本体内执行 Test Runner；以及在真工程中打开 `Main.unity` 后的目视检查。（真工程的编译、资产导入与场景接线已验证，见 `Docs/evidence/README.md`）
- 未通过：无。
