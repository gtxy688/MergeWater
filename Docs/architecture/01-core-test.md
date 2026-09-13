# M1 Core 验收文档

> 对应架构：`01-core.md`
> 对应需求：`Docs/requirements.md` 的 R2、R3、R4、R5、R6、R7、R9、R10

## 自动化测试计划与证据

| 编号 | 层级 | 测试路径与名称 | 行为 | 红灯原因或 N/A 理由 | 最近结果 |
|------|------|----------------|------|---------------------|----------|
| A1 | EditMode | `Assets/Tests/EditMode/Core/GameBalanceTests.cs::Default_MatchesRequirementsV1Table` | 11 级等级表的名称/半径/质量/得分/权重逐项等于 V1 | 实现前无 `GameBalance` | PASS |
| A2 | EditMode | `Core/GameBalanceTests.cs::Default_Tiers_AreMonotonicAndWeightsSumToOne`、`Default_FeelAndRuleValues_MatchV2`、`Default_StageMilestones_MatchV218` | 半径/质量/得分严格递增；1–3 级权重和为 1；V2 规则与手感数值逐项一致 | 同上 | PASS |
| A3 | EditMode | `Core/GameBalanceTests.cs::GetTier_OutOfRange_ReturnsInvalidDefaultWithoutThrowing`、`Clone_IsDeepCopy_AndDoesNotShareArrays` | 越界查询返回无效默认值不抛异常；克隆为深拷贝 | 同上 | PASS |
| A4 | EditMode | `Core/ComboTrackerTests.cs`（6 项，含 `Advance_BeyondWindow_ResetsCombo`、`Advance_ExactlyAtWindowBoundary_DoesNotReset`） | 3s 窗口内连击递增、倍率封顶 2.0；超窗归零；恰好 3.0s 不归零；非正 dt 忽略 | 实现前无 `ComboTracker` | PASS |
| A5 | EditMode | `Core/DropQueueTests.cs`（6 项，含 `Next_FirstTwentyDrops_OnlyTierOneOrTwo`、`Next_AtSixtyDropsAndBeyond_KeepsSamePool`、`Next_WeightDistribution_MatchesV1WithinTolerance`、`SameSeed_ProducesReproducibleSequence`） | 等级池节奏（V2.4–V2.6）与归一化权重；同 seed 可复现 | 实现前无 `DropQueue` | PASS |
| A6 | EditMode | `Core/ScoreRulesTests.cs`（6 项，含 `ScoreFor_AppliesMultiplierAndRounds`、`CanMerge_TopTier_IsFalse`） | 得分 = 表值×倍率并四舍五入；11 级不可合成；未知等级返回 0 | 实现前无 `ScoreRules` | PASS |
| A7 | EditMode | `Core/StageProgressTests.cs`（6 项，含 `Advance_CrossingMultipleMilestones_ReturnsEachOnce`、`Reset_AllowsMilestonesToBeGrantedAgainInNextRound`） | 200/500/1000 每局每节点只发放一次；进度条归一化 | 实现前无 `StageProgress` | PASS |
| A8 | EditMode | `Core/GameBalanceTests.cs::Default_MatchesRequirementsV1Table`（含 11 级逐项断言） | 等级表数据驱动的完整性守卫：任一级数值被改动即使 11 项断言中的一项失败 | 实现前无 `GameBalance` | PASS |

## 自动化运行记录

| 日期 | Unity 版本与环境 | 命令或 Test Runner 过滤器 | 结果文件 | 结论 |
|------|------------------|---------------------------|----------|------|
| 2026-09-11 | Unity 2022.3.62f3 / Windows 10 / `-batchmode -nographics` | `-runTests -testPlatform editmode -testResults Logs\editmode-results.xml` | `Logs\editmode-results.xml` | PASS — EditMode 全量 103/103（本模块 30 项） |

> 环境说明：先在由真工程同步出的隔离副本上运行，随后 MCP 直连真工程本体复跑，两次结果一致（EditMode 103/103、PlayMode 74/74）。详见 `Docs/evidence/README.md`。

## 手动验收前置条件

- 无（本模块全部内容可自动化判定）。

## 手动验收

| 编号 | 操作步骤 | 可观察预期结果 | 环境与构建 | 执行者/日期 | 状态 | 证据或备注 |
|------|----------|----------------|------------|-------------|------|------------|
| — | — | — | — | — | N/A | 纯数值与规则，无主观体验项 |

## 失败路径与边界

| 编号 | 前置状态与操作 | 预期保护或失败行为 | 自动/手动 | 状态 | 证据或备注 |
|------|----------------|--------------------|-----------|------|------------|
| E1 | 查询等级 0 或 12 | 返回默认结构体，不抛异常 | 自动 | PASS | `GameBalanceTests.GetTier_OutOfRange_...` |
| E2 | 池内权重全为 0 | 回退到池内首个等级，不死循环 | 自动 | PASS | `DropQueueTests`（`total <= 0` 分支） |
| E3 | 恰好 3.0s 后再次合成 | 不归零（严格大于判断） | 自动 | PASS | `ComboTrackerTests.Advance_ExactlyAtWindowBoundary_DoesNotReset` |
| E4 | 倍率为 0 或负 | 按 1.0 处理，得分不为 0 | 自动 | PASS | `ScoreRulesTests.ScoreFor_NonPositiveMultiplier_FallsBackToOne` |

## 回归范围

| 受影响模块或契约 | 复验项 | 原因 | 状态 | 证据或备注 |
|------------------|--------|------|------|------------|
| M3 | `03-session-test.md` 全部 17 项 | 得分、连击、队列、里程碑直接消费本模块 | PASS | `RoundSessionTests` 17/17 |
| M5 | `05-presentation-test.md` A5 | 倍率与阶段进度展示 | PASS | `ComboPitchTests` 3/3 |
| M7 | `07-bootstrap-test.md` A2 | 配置资产与代码默认值一致 | PASS | `ConfigAssetTests` 3/3 |

## 交付结论

- 已验证：A1–A8 全部通过（EditMode，本模块 30 项）。
- 不适用：手动验收（纯逻辑，无主观体验项）。
- 待手动验收：无。
- 未验证：无（201 项自动化测试通过：EditMode 116 + PlayMode 85，见 `Docs/evidence/README.md`）。
- 未通过：无。
