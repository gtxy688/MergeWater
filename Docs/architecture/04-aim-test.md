# M4 Aim 验收文档

> 对应架构：`04-aim.md`
> 对应需求：`Docs/requirements.md` 的 R1、R2、R8、R11、R12、R13

## 自动化测试计划与证据

| 编号 | 层级 | 测试路径与名称 | 行为 | 红灯原因或 N/A 理由 | 最近结果 |
|------|------|----------------|------|---------------------|----------|
| A1 | EditMode | `Assets/Tests/EditMode/Aim/AimSolverTests.cs::ClampX_KeepsDropInsideBoundsAccountingForRadius`、`ClampX_WithoutRadius_MatchesRawBounds` | 夹取考虑水果半径；半径为 0 时退化为原始边界 | 实现前无 `AimSolver` | PASS |
| A2 | EditMode | `Aim/AimSolverTests.cs::ClampX_WhenBoundsInvert_ReturnsMidpoint` | 区间反转的边界保护 | 同上 | PASS |
| A3 | EditMode | `Aim/AimSolverTests.cs::SampleTrajectory_ReturnsOrderedPoints_AndRespectsMaxTime`、`SampleTrajectory_WithInitialHorizontalVelocity_ProducesParabola` | 采样点数与时长上限；存在水平速度时产生抛物线并触墙镜像反射 | 同上 | PASS |
| A4 | EditMode | `Aim/AimSolverTests.cs::SampleTrajectory_WithZeroGravity_ReturnsTwoPoints`、`SampleTrajectory_WithNullOutput_ReturnsZero` | 退化输入返回两点直线，不死循环；空输出安全 | 同上 | PASS |
| A5 | EditMode | `Aim/AimSolverTests.cs::NormalizeX_IsWithinZeroToOne` | 归一化落点（含越界夹取与退化区间） | 同上 | PASS |
| A6 | PlayMode | `Assets/Tests/PlayMode/Aim/AimControllerTests.cs::PressDragRelease_RaisesDropRequestedOnceWithClampedX`、`DragBeyondBounds_ClampsToRadiusAdjustedLimit`、`AimState_NormalizedX_TracksBounds` | 按下-拖动-松手发出恰好一次投放指令且 x 被夹取；状态归一化正确 | 实现前无 `AimController` | PASS |
| A7 | PlayMode | `Aim/AimControllerTests.cs::SetInteractableFalse_CancelsAimWithoutRaisingRequests`、`PointerLeavingScreen_OnRelease_CancelsInsteadOfDropping`、`ItemAim_ReleasesOneTargetRequest_AndCancelRaisesNothing`、`BuildPreview_ReturnsVerticalPathWithinField` | 不可交互/指针离屏时取消且不发指令；道具瞄准恰好一次；预览为垂直下落路径 | 同上 | PASS |
| A8 | PlayMode | `Assets/Tests/PlayMode/Bootstrap/PhysicsAndPreviewDiagnostics.cs::Aiming_ShowsPreviewReachingTheLandingSurface` | 按住并拖动后：预测线可见、落点 x 跟随（1.00）、**路径最低点到达落点表面**（实测 lowestY=-4.20 = 地面）、待投水果可见且有 sprite | **修复「线断在半空」后新增**（修复前最低只到 y≈2.06） | PASS |

## 自动化运行记录

| 日期 | Unity 版本与环境 | 命令或 Test Runner 过滤器 | 结果文件 | 结论 |
|------|------------------|---------------------------|----------|------|
| 2026-09-11 | Unity 2022.3.62f3 / Windows 10 / `-batchmode -nographics` | EditMode 与 PlayMode 各一次（见 `01-core-test.md` / `02-field-test.md` 记录） | `Logs\editmode-results.xml`、`Logs\playmode-verify.xml` | PASS — 本模块 16 项（EditMode 8 + PlayMode 8） |

> 环境说明：先在由真工程同步出的隔离副本上运行，随后 MCP 直连真工程本体复跑，两次结果一致（EditMode 103/103、PlayMode 74/74）。详见 `Docs/evidence/README.md`。

## 手动验收前置条件

- 场景、Prefab 与配置：`Main.unity`，主相机正交（size 5.6，位置 (0, 0.4, -10)），`AimController` 已装配并注入相机。
- 依赖模块状态：M3 可接收投放指令。
- 目标设备与画质档位：Editor + 真机触屏。

## 手动验收

| 编号 | 操作步骤 | 可观察预期结果 | 环境与构建 | 执行者/日期 | 状态 | 证据或备注 |
|------|----------|----------------|------------|-------------|------|------------|
| H1 | 手指（鼠标）按住并左右拖动 | 预览线跟随手指，松手点与预览一致，无延迟感 | Editor + 真机 | 待执行 | 待手动验收 | R1、V2.26 |
| H2 | 拖到屏幕最左/最右后松手 | 水果贴边落下但不越出场地 | Editor | 待执行 | 待手动验收 | 夹取（自动化已验证） |
| H3 | 广告播放或弹窗打开时点击屏幕 | 不产生投放 | Editor | 待执行 | 待手动验收 | 互斥（自动化已验证 `SetInteractable`） |

## 失败路径与边界

| 编号 | 前置状态与操作 | 预期保护或失败行为 | 自动/手动 | 状态 | 证据或备注 |
|------|----------------|--------------------|-----------|------|------------|
| E1 | 按住后指针移出游戏窗口再松手 | 不发投放指令，状态回到 Idle | 自动 | PASS | `AimControllerTests.PointerLeavingScreen_OnRelease_CancelsInsteadOfDropping` |
| E2 | 道具瞄准中按取消 | 不发目标指令、不消耗道具 | 自动 | PASS | `AimControllerTests.ItemAim_ReleasesOneTargetRequest_AndCancelRaisesNothing`、`ItemUseTests.CancelAim_DoesNotDeductStockOrRemoveFruit` |
| E3 | 未设置落点半径即瞄准 | 使用默认半径，不抛异常 | 自动 | PASS | `AimControllerTests`（`defaultRadius` 分支） |

## 回归范围

| 受影响模块或契约 | 复验项 | 原因 | 状态 | 证据或备注 |
|------------------|--------|------|------|------------|
| M3 | `03-session-test.md` A1/A2 | 投放指令消费方；`CanAcceptDrop` 覆盖 Ready | PASS | `RoundSessionTests` 17/17、`BootstrapFlowTests.SharedScene_OnAccept_RoundIsPlayableThroughRealAimPath` |
| M7 | `07-bootstrap-test.md` A6 | 可交互性开关与道具编排 | PASS | `ItemUseTests` 7/7 |

## 交付结论

- 已验证：A1–A8 与 E1–E3 全部通过（本模块 16 项）。
- 不适用：无。
- 待手动验收：H1–H3（手感与真机触屏）。
- 未验证：无（201 项自动化测试通过：EditMode 116 + PlayMode 85，见 `Docs/evidence/README.md`）。
- 未通过：无。
