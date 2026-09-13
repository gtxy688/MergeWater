# M3 Session 验收文档

> 对应架构：`03-session.md`
> 对应需求：`Docs/requirements.md` 的 R4、R5、R6、R7、R9、R10、R26

## 自动化测试计划与证据

| 编号 | 层级 | 测试路径与名称 | 行为 | 红灯原因或 N/A 理由 | 最近结果 |
|------|------|----------------|------|---------------------|----------|
| A1 | EditMode | `Assets/Tests/EditMode/Session/RoundSessionTests.cs::StartRound_ResetsStateAndEntersReady_ThenFirstDropEntersPlaying` | 开局重置并进入 Ready；Ready 必须允许首投（`CanAcceptDrop`）但不允许道具 | 实现前无 `RoundSession` | PASS |
| A2 | EditMode | `Session/RoundSessionTests.cs::ReleaseDrop_WhilePlaying_UsesQueueTierAndAdvancesQueue`、`DropCooldown_BlocksImmediateSecondDrop_ThenAllowsAfterTick` | 投放取当前等级并前移队列；冷却内拒绝、冷却后放行 | 同上 | PASS |
| A3 | EditMode | `Session/RoundSessionTests.cs::ReleaseDrop_WhenFieldRejects_DoesNotConsumeQueue` | 生成失败不消耗队列、不计数 | 同上 | PASS |
| A4 | EditMode | `Session/RoundSessionTests.cs::Merged_AddsScoreWithMultiplier_AndTracksBestScore` | 得分 = 表值×倍率；最高分同步并回调持久化 | 实现前无计分 | PASS |
| A5 | EditMode | `Session/RoundSessionTests.cs::Combo_WithinThreeSeconds_IncreasesThenResetsAfterWindow` | 3s 连击窗口、倍率与归零 | 同上 | PASS |
| A6 | EditMode | `Session/RoundSessionTests.cs::Danger_RequiresContinuousOnePointTwoSeconds_ThenEntersRevivingPage`、`Danger_AfterReviveUsed_SecondFailureGoesToGameOver` | 越线需持续 1.2s → 失败页；已用过复活则直接 GameOver | 实现前无越线计时 | PASS |
| A7 | EditMode | `Session/RoundSessionTests.cs::DangerEvents_ArePairedOncePerViolationEpisode`、`Danger_TimerResets_WhenViolationClears` | `DangerStarted`/`DangerEnded` 严格配对；中断清零 | 同上 | PASS |
| A8 | EditMode | `Session/RoundSessionTests.cs::ApplyRevive_OnFirstCall_RemovesClusterAndResumes_SecondCallIsRejected`、`ApplyRevive_WhenNotInReviving_ReturnsNotInGameOver_AndChangesNothing` | 复活每局限 1 次、清除 ≤3 颗、失败不改变状态 | 实现前无复活流程 | PASS |
| A9 | EditMode | `Session/RoundSessionTests.cs::MilestoneReached_IsGrantedOncePerRound` | 阶段目标每局每节点一次，奖励种类正确 | 实现前无里程碑 | PASS |
| A10 | EditMode | `Session/RoundSessionTests.cs::ReleaseDrop_AfterGameOver_IsRejected_AndLateMergesDoNotScore`、`Reviving_RejectsDropAndItemUse`、`MergeDuringReviving_IsIgnored`、`Dispose_UnsubscribesFromField` | 结算后拒绝操作；失败页期间拒绝投放/道具/计分；解绑后不再响应 | 同上 | PASS |

## 自动化运行记录

| 日期 | Unity 版本与环境 | 命令或 Test Runner 过滤器 | 结果文件 | 结论 |
|------|------------------|---------------------------|----------|------|
| 2026-09-11 | Unity 2022.3.62f3 / Windows 10 / `-batchmode -nographics` | `-runTests -testPlatform editmode -testResults Logs\editmode-results.xml` | `Logs\editmode-results.xml` | PASS — EditMode 全量 103/103（本模块 17 项） |

> 环境说明：先在由真工程同步出的隔离副本上运行，随后 MCP 直连真工程本体复跑，两次结果一致（EditMode 103/103、PlayMode 74/74）。详见 `Docs/evidence/README.md`。

## 手动验收前置条件

- 场景、Prefab 与配置：`Main.unity` 可用，`GameBootstrapper` 持有 `RoundSession`。
- 依赖模块状态：M1 数值资产、M2 场地。
- 目标设备与画质档位：Editor Play Mode。

## 手动验收

| 编号 | 操作步骤 | 可观察预期结果 | 环境与构建 | 执行者/日期 | 状态 | 证据或备注 |
|------|----------|----------------|------------|-------------|------|------------|
| H1 | 完整游玩一局至失败 | 分数与连击符合预期，失败有「差一步」感（GDD §1） | Editor | 待执行 | 待手动验收 | 体验口径 |
| H2 | 失败后走一次复活 | 看广告 → 最高一簇消失 → 可继续玩；每局限 1 次 | Editor | 待执行 | 待手动验收 | R7；自动化已验证状态机与清簇，手感需人判断 |
| H3 | 单局内达成 200/500 分里程碑 | 进度条推进且各获得一次道具 | Editor | 待执行 | 待手动验收 | R10；自动化已验证发放一次与落库 |

## 失败路径与边界

| 编号 | 前置状态与操作 | 预期保护或失败行为 | 自动/手动 | 状态 | 证据或备注 |
|------|----------------|--------------------|-----------|------|------------|
| E1 | `Reviving` 期间投放或用道具 | 被拒绝，不消耗队列/库存 | 自动 | PASS | `Reviving_RejectsDropAndItemUse` |
| E2 | 本局已用过复活后再次越线失败 | 直接进入 `GameOver`，不再进入失败页 | 自动 | PASS | `Danger_AfterReviveUsed_SecondFailureGoesToGameOver` |
| E3 | 越线后水果被道具清除 | 计时清零并发 `DangerEnded` | 自动 | PASS | `Danger_TimerResets_WhenViolationClears` |
| E4 | 复活清场期间收到残留 `Merged` | 忽略，不计分 | 自动 | PASS | `MergeDuringReviving_IsIgnored` |
| E5 | 重开一局后旧局场地事件 | 旧局已解绑，不会重复计分 | 自动 | PASS | `RoundLifecycleTests.NewRound_ResetsScoreComboDangerAndQueue_WithoutLeakingPreviousEvents` |

## 回归范围

| 受影响模块或契约 | 复验项 | 原因 | 状态 | 证据或备注 |
|------------------|--------|------|------|------------|
| M5 | `05-presentation-test.md` A1/A2/A6/A7 | HUD 订阅快照与事件、结算面板 | PASS | `HudBinderTests` 9/9 |
| M7 | `07-bootstrap-test.md` A4/A5 | 构造 Session、重开局、复活编排 | PASS | `BootstrapFlowTests` 6/6、`RoundLifecycleTests` 4/4 |
| M2 | `02-field-test.md` A3–A5 | 事件与越线查询来源 | PASS | `GameFieldMergeTests`、`GameFieldDangerTests` |

## 交付结论

- 已验证：A1–A10 与 E1–E5 全部通过（EditMode 17 项 + PlayMode 集成 4 项）。
- 不适用：无。
- 待手动验收：H1–H3。
- 未验证：无（201 项自动化测试通过：EditMode 116 + PlayMode 85，见 `Docs/evidence/README.md`）。
- 未通过：无。
