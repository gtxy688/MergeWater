# M2 Field 验收文档

> 对应架构：`02-field.md`
> 对应需求：`Docs/requirements.md` 的 R2、R3、R6、R11、R12、R13、R14、V3

## 自动化测试计划与证据

| 编号 | 层级 | 测试路径与名称 | 行为 | 红灯原因或 N/A 理由 | 最近结果 |
|------|------|----------------|------|---------------------|----------|
| A1 | PlayMode | `Assets/Tests/PlayMode/Field/GameFieldDropTests.cs::Drop_ValidTier_SpawnsOneFruitWithConfiguredRadiusAndMass`、`Drop_OutOfBoundsX_ClampsInsideField` | 生成一颗水果，半径/质量等于 V1，开启 CCD；落点 x 被夹取到场内 | 实现前无 `GameField` | PASS |
| A2 | PlayMode | `Field/GameFieldDropTests.cs::Drop_InvalidTierOrBeyondLimit_ReturnsFalseWithoutSpawning` | 越界等级与超上限（120）不生成，并告警一次 | 同上 | PASS |
| A3 | PlayMode | `Field/GameFieldMergeTests.cs::SameTierCollision_MergesIntoNextTierAndRaisesMergedOnce`、`MergeResult_PopsUpward_SoItDoesNotImmediatelyReMerge` | 同级相撞 → 两颗消失、生成高一级、`Merged` 恰好一次、结果带上跳初速 | 实现前无合成逻辑 | PASS |
| A4 | PlayMode | `Field/GameFieldMergeTests.cs::TopTierCollision_DoesNotMerge` | 11 级相撞不合成、不发事件 | 同上 | PASS |
| A5 | PlayMode | `Field/GameFieldDangerTests.cs`（4 项，含 `SettledFruitAboveLine_ReportsViolation`、`FreshlySpawnedFruitAboveLine_IsNotReportedBeforeSettleGrace`、`FallingFruitAboveLine_IsNotReported`） | 静止越线判定；刚生成/仍在掉落的水果不误判（V2.9 缓冲） | 实现前无越线查询 | PASS |
| A6 | PlayMode | `Field/GameFieldItemTests.cs`（7 项，含 `Bomb_RemovesFruitsInRadius`、`Hammer_RemovesSingleNearest`、`Shake_MovesFruitsWithoutRemoving`、`Undo_RemovesLastUnmergedDrop`、`LastDrop_ThatMerged_IsMarkedMerged`） | 四种道具的场地效果；合成后的投放标记为 Merged 以禁用撤销 | 实现前无道具作用 | PASS |
| A7 | PlayMode | `Field/GameFieldLifecycleTests.cs::ClearAll_RemovesEveryFruitAndDropRecord`、`SimulationToggle_FreezesAndRestoresBodies`、`EscapedFruit_IsRecycledInsteadOfLeaking` | 重开清理、仿真冻结/恢复、逃逸回收 | 同上 | PASS |

## 自动化运行记录

| 日期 | Unity 版本与环境 | 命令或 Test Runner 过滤器 | 结果文件 | 结论 |
|------|------------------|---------------------------|----------|------|
| 2026-09-11 | Unity 2022.3.62f3 / Windows 10 / `-batchmode -nographics` | `-runTests -testPlatform playmode -testResults Logs\playmode-verify.xml` | `Logs\playmode-verify.xml` | PASS — PlayMode 全量 66/66（本模块 21 项） |

> 环境说明：同 `01-core-test.md` —— 运行在由真实工程同步出的隔离副本上；副本与真工程 `.meta` GUID 一致。

## 手动验收前置条件

- 场景、Prefab 与配置：`Main.unity` 已由 `MergeWater/Build Main Scene` 生成，`Field` 节点引用占位 sprite。
- 依赖模块状态：M1 数值资产存在（`Assets/Config/GameBalance.asset`）。
- 目标设备与画质档位：Editor Play Mode；真机项见 `07-bootstrap-test.md`。

## 手动验收

| 编号 | 操作步骤 | 可观察预期结果 | 环境与构建 | 执行者/日期 | 状态 | 证据或备注 |
|------|----------|----------------|------------|-------------|------|------------|
| H1 | 连续投放 30 颗 1–3 级水果至堆满 | 无穿墙、无穿隧、无持续抖动；水果自然堆叠 | Editor | 待执行 | 待手动验收 | V3；需人判断堆叠观感 |
| H2 | 让同级水果在堆顶相撞 | 可见吸附 60ms 后合成，结果水果大小与颜色符合等级 | Editor | 待执行 | 待手动验收 | V2.21 |
| H3 | 堆到警戒线附近 | 短暂超过线不判负，稳定越线后才触发 | Editor | 待执行 | 待手动验收 | V2.8/V2.9 |

## 失败路径与边界

| 编号 | 前置状态与操作 | 预期保护或失败行为 | 自动/手动 | 状态 | 证据或备注 |
|------|----------------|--------------------|-----------|------|------------|
| E1 | 合成吸附期间用道具移除其中一颗 | 取消合成，不产生新水果、不发 `Merged` | 自动 | PASS | `GameFieldMergeTests.MergeCancelled_WhenOneFruitRemovedDuringAbsorb` |
| E2 | 水果掉出场地下边界 | 回收并告警一次，不残留空引用 | 自动 | PASS | `GameFieldLifecycleTests.EscapedFruit_IsRecycledInsteadOfLeaking` |
| E3 | 场地空时使用炸弹/锤子/撤销 | 返回 0/false，不抛异常 | 自动 | PASS | `GameFieldItemTests.Bomb_WithNonPositiveRadius_RemovesNothing`、`Hammer_WhenNothingInRange_ReturnsFalse` |
| E4 | 达到 120 颗后继续投放 | 拒绝生成，`LiveFruitCount` 不超过上限 | 自动 | PASS | `GameFieldDropTests.Drop_InvalidTierOrBeyondLimit_ReturnsFalseWithoutSpawning` |

## 回归范围

| 受影响模块或契约 | 复验项 | 原因 | 状态 | 证据或备注 |
|------------------|--------|------|------|------------|
| M3 | `03-session-test.md` A6/A7/A8 | Session 依赖 `IFieldPort` 越线与复活清场 | PASS | `RoundSessionTests` 17/17 |
| M5 | `05-presentation-test.md` A4/A9 | 合成事件与越线事件的表现 | PASS | `HudBinderTests` 9/9 |
| M7 | `07-bootstrap-test.md` A6 | 装配与道具编排 | PASS | `ItemUseTests` 6/6 |

## 交付结论

- 已验证：A1–A7 与 E1–E4 全部通过（PlayMode，本模块 21 项）。
- 不适用：无。
- 待手动验收：H1–H3（堆叠观感、合成手感、越线判定观感）。
- 未验证：在真工程本体内执行 Test Runner（真工程的编译、资产导入与场景接线已验证，见 `Docs/evidence/README.md`）。
- 未通过：无。
