# M5 Presentation 验收文档

> 对应架构：`05-presentation.md`
> 对应需求：`Docs/requirements.md` 的 R6、R8、R10、R12、R18、R20、R21、R22、R23、R25、R27

## 自动化测试计划与证据

| 编号 | 层级 | 测试路径与名称 | 行为 | 红灯原因或 N/A 理由 | 最近结果 |
|------|------|----------------|------|---------------------|----------|
| A1 | PlayMode | `Assets/Tests/PlayMode/Presentation/HudBinderTests.cs::Scored_RaisesScoreTextToSnapshotValue`、`PhaseToReviving_ShowsSettlementWithReviveAvailable`、`PhaseToGameOver_ShowsSettlementWithoutRevive`、`MilestoneReached_ShowsClaimToast` | 得分事件刷新分数文本；失败页/结算页与复活按钮可用性；里程碑 Toast | 实现前无 `HudBinder` | PASS |
| A2 | PlayMode | `Presentation/HudBinderTests.cs::BindAfterRebind_SubscribesNewEventsOnlyOnce`、`Unbind_StopsReceivingEvents` | 跨局重新绑定不重复订阅；解绑后不再响应 | 同上 | PASS |
| A3 | PlayMode | `Presentation/TimeDirectorTests.cs`（6 项，含 `SlowMoAndHitStop_FinishRestoreTimeScaleToOne`、`HitStop_FreezesThenRestoresTimeScale`、`NestedSlowMo_TakesStrongestScaleAndLongestDuration`、`ResetToNormal_AlwaysRestoresOne`） | 慢放与顿帧结束后 `timeScale` 必为 1；嵌套取最强；非法请求忽略 | 实现前无 `TimeDirector` | PASS |
| A4 | PlayMode | `Presentation/AudioDirectorTests.cs`（5 项，含 `PlaySfx_WithoutSourceOrWithSfxDisabled_DoesNotThrow`、`PlaceholderClips_AreGeneratedWithAudioData`、`PlayMusic_WithoutClip_LogsOnceAndStaysSilent`） | 关闭音效/无音源时静默不报错；占位音可生成并缓存；无 BGM 时只告警一次 | 同上 | PASS |
| A5 | EditMode | `Assets/Tests/EditMode/Presentation/ComboPitchTests.cs::ComboPitch_AddsSemitonePerCombo_CappedAtOneOctave`、`HitStopSeconds_GrowsWithCombo_AndStaysWithinV223Range`、`ShakeTier_IsThreeLevels` | 音阶 +1 半音/连击、封顶 8 度（12 半音）；顿帧 80–120ms 随连击递增；震屏三档 | 实现前无音阶函数 | PASS |
| A6 | PlayMode | `Presentation/HudBinderTests.cs::DangerEvents_ToggleDangerLinePulse`、`Update_RefreshesNextPreviewAndPendingFruit`、`Update_WhileAiming_DrawsTrajectory` | 警戒线脉冲随越线事件开关；next 预览与待投水果随快照刷新；瞄准时绘制虚线 | 实现前无 `DangerLineView`/`AimPreviewView` | PASS |

## 自动化运行记录

| 日期 | Unity 版本与环境 | 命令或 Test Runner 过滤器 | 结果文件 | 结论 |
|------|------------------|---------------------------|----------|------|
| 2026-09-11 | Unity 2022.3.62f3 / Windows 10 / `-batchmode -nographics` | EditMode 与 PlayMode 各一次 | `Logs\editmode-results.xml`、`Logs\playmode-verify.xml` | PASS — 本模块 23 项（EditMode 3 + PlayMode 20） |

> 环境说明：同 `01-core-test.md` —— 运行在由真实工程同步出的隔离副本上。

## 手动验收前置条件

- 场景、Prefab 与配置：由 `MergeWater/Build Main Scene` 生成的 `Main.unity`；竖屏 1080×1920 参考分辨率。
- 依赖模块状态：M1/M2/M3/M4/M6 已实现并可开局。
- 目标设备与画质档位：Editor Play Mode；真机（中端）。

## 手动验收

| 编号 | 操作步骤 | 可观察预期结果 | 环境与构建 | 执行者/日期 | 状态 | 证据或备注 |
|------|----------|----------------|------------|-------------|------|------------|
| H1 | 观察对局 HUD | 顶栏含最高分/当前分/阶段进度/设置；中央待投水果与垂直虚线；左右入口齐备；底部无任何 Banner/信息流/交叉推广 | Editor | 待执行 | 待手动验收 | GDD §6.2、R27（自动化已断言无广告位节点） |
| H2 | 连续制造 2/4/6 连击 | 顿帧与震屏随连击增强，音高上行，飘字 punch 明显 | Editor | 待执行 | 待手动验收 | V2.23/V2.24 |
| H3 | 堆到警戒线附近并越线 | 警戒线红色脉冲 + 心跳音；失败时重震 0.2s | Editor | 待执行 | 待手动验收 | V2.25 |
| H4 | 打开设置面板切换音效/音乐/震动 | 开关即时生效、重启后保持 | Editor | 待执行 | 待手动验收 | R25（持久化已自动化验证） |
| H5 | 首次进入查看隐私弹窗与结算页引导 | 首启必现隐私弹窗；前 3 局结算页出现分享/排行引导 | Editor | 待执行 | 待手动验收 | R20/R23 |
| H6 | 真机竖屏试玩 3 分钟 | 60fps 稳定，UI 不被微信胶囊遮挡，触屏落点准确 | 真机 | 待执行 | 待手动验收 | V3 |

> 中文渲染备注（决策 D9）：UI 用 uGUI 旧版 `Text` + 运行时解析的系统 CJK 字体。若目标设备缺少候选中文字体，会回退内置字体并告警一次，此时中文可能显示为方块——需要在真机 H6 中确认。

## 失败路径与边界

| 编号 | 前置状态与操作 | 预期保护或失败行为 | 自动/手动 | 状态 | 证据或备注 |
|------|----------------|--------------------|-----------|------|------------|
| E1 | 慢放/顿帧期间再次触发合成 | 取最强反馈，不叠加；结束后 `timeScale=1` | 自动 | PASS | `TimeDirectorTests.NestedSlowMo_TakesStrongestScaleAndLongestDuration` |
| E2 | 面板显示时点击下层对局 | 下层不可交互 | 自动 + 手动 | PASS（自动）/ 待执行（观感） | `GameBootstrapper.Update` 的 `SetInputBlocked`；`ItemUseTests` |
| E3 | 缺少可选 UI 引用 | 跳过该元素并告警一次，不抛异常 | 自动 | PASS | `PanelController`/`HudView` 全字段空值保护；`PresentationHarness` 最小 HUD 即为该场景 |

## 回归范围

| 受影响模块或契约 | 复验项 | 原因 | 状态 | 证据或备注 |
|------------------|--------|------|------|------------|
| M3 | `03-session-test.md` | 事件契约变化会影响 Binding | PASS | `HudBinderTests` 9/9 |
| M7 | `07-bootstrap-test.md` | 面板与红点调用方 | PASS | `BootstrapFlowTests` 6/6 |

## 交付结论

- 已验证：A1–A6 与 E1/E3 通过（本模块 23 项）。
- 不适用：无。
- 待手动验收：H1–H6（观感、手感、真机帧率与中文渲染）。
- 未验证：在真工程本体内执行 Test Runner（真工程的编译、资产导入与场景接线已验证，见 `Docs/evidence/README.md`）。
- 未通过：无。
