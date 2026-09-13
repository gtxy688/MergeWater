# M2 Field 验收文档

> 对应架构：`02-field.md`
> 对应需求：`Docs/requirements.md` 的 R2、R3、R6、R11、R12、R13、R14、V3

## 自动化测试计划与证据

| 编号 | 层级 | 测试路径与名称 | 行为 | 红灯原因或 N/A 理由 | 最近结果 |
|------|------|----------------|------|---------------------|----------|
| A1 | PlayMode | `Assets/Tests/PlayMode/Field/GameFieldDropTests.cs::Drop_ValidTier_SpawnsOneFruitWithConfiguredRadiusAndMass`、`Drop_OutOfBoundsX_ClampsInsideField` | 生成一颗水果，半径/质量等于 V1，开启 CCD；落点 x 被夹取到场内 | 实现前无 `GameField` | PASS |
| A2 | PlayMode | `Field/GameFieldDropTests.cs::Drop_InvalidTierOrBeyondLimit_ReturnsFalseWithoutSpawning` | 越界等级与超上限（120）不生成，并告警一次 | 同上 | PASS |
| A3 | PlayMode | `Field/GameFieldMergeTests.cs::SameTierCollision_MergesIntoNextTierAndRaisesMergedOnce`、`MergeResult_PushedSidewaysAtMidpoint`、`MergeResult_SidePushEqualsConfiguredImpulse` | 同级相撞 → 两颗消失、生成高一级、`Merged` 恰好一次；结果是**水平初速且不向上蹦**（V2.31b），初速大小等于 `GameBalance.MergeResultSideImpulse`（2026-09-12「力度太吝啬」后 0.45→1.5） | 实现前无合成逻辑 | PASS |
| A4 | PlayMode | `Field/GameFieldMergeTests.cs::TopTierCollision_DoesNotMerge` | 顶级（10 级）相撞不合成、不发事件 | 同上 | PASS |
| A5 | PlayMode | `Field/GameFieldDangerTests.cs`（4 项，含 `SettledFruitAboveLine_ReportsViolation`、`FreshlySpawnedFruitAboveLine_IsNotReportedBeforeSettleGrace`、`FallingFruitAboveLine_IsNotReported`） | 静止越线判定；刚生成/仍在掉落的水果不误判（V2.9 缓冲） | 实现前无越线查询 | PASS |
| A6 | PlayMode | `Field/GameFieldItemTests.cs`（7 项，含 `Bomb_RemovesFruitsInRadius`、`Hammer_RemovesSingleNearest`、`Shake_MovesFruitsWithoutRemoving`、`Undo_RemovesLastUnmergedDrop`、`LastDrop_ThatMerged_IsMarkedMerged`） | 四种道具的场地效果；合成后的投放标记为 Merged 以禁用撤销 | 实现前无道具作用 | PASS |
| A7 | PlayMode | `Field/GameFieldLifecycleTests.cs::ClearAll_RemovesEveryFruitAndDropRecord`、`SimulationToggle_FreezesAndRestoresBodies`、`EscapedFruit_IsRecycledInsteadOfLeaking` | 重开清理、仿真冻结/恢复、逃逸回收 | 同上 | PASS |
| A8 | PlayMode | `Assets/Tests/PlayMode/Bootstrap/PhysicsAndPreviewDiagnostics.cs::DroppedFruits_FallMergeAndDoNotScatterToWalls` | 6 次同点投放后：全部落地、不互相穿插、**不滚到贴墙**（判据由场地几何推导）、仍为 Dynamic、同级碰撞确实合成 | **修复「滚到墙角摊平」后新增**（修复前实测偏移 1.95） | PASS |
| A9 | PlayMode | `PhysicsAndPreviewDiagnostics.cs::FruitsOfDifferentTiers_StackOnEachOther` | 用互不相邻同级的 4 颗水果叠放（不会合成，故确定性）：至少有一颗被别的水果托起（不在底面），且无穿插 | **修复「摊平一层」后新增**：实测 4 颗叠成 4 层竖塔（y=-3.74/-2.64/-1.12/0.88，横向全为 0.00，速度全为 0） | PASS |
| A10 | PlayMode | `Assets/Tests/PlayMode/Field/PlayAreaFloorTests.cs::PlayFloor_SitsAboveTheScreenBottom_SoGroundFruitIsNotClipped` | 运行时地面 = 屏幕底边 + `GameBalance.FloorScreenInset`（0.06）；贴地水果的可见下沿高于屏幕底 ≥0.03 世界单位（1080×1920 实测 ≈11 px，此前 0 px） | **修复「水果贴屏底像被切」后新增**（V2.42，需求方截图指认）：原 D13 让地面精确贴屏幕底边；**反证**：`floorScreenInset` 改回 0 后该用例失败（`Expected > 0.0f, But was 0.0f`）。2026-09-13 需求方目视定档 0.06：间隙下限从 0.2 下调到 0.03——**0.2 是本测试自估的保守值、没有需求依据**，会把定档值误判为回归；下限现在只用于拦「回到贴死屏幕底」 | PASS |
| A11 | PlayMode | `Field/FruitArtVisualTests.cs::VisualSize_MatchesColliderDiameter_EvenWhenSpriteImportSettingsDiffer` | 水果的视觉直径 == 碰撞直径，**与贴图的 PPU / 像素尺寸无关**（用 256px @ PPU100、世界尺寸 2.56 的贴图构造） | **水果图换成 `kenney_planets` 后新增**（V2.44）：反证实测 `Expected: 0.360000014f, But was: 0.921600044f`——正好是该贴图世界尺寸 2.56 倍的放大 | PASS |
| A12 | PlayMode | `Bootstrap/BootstrappedSceneTests.cs::SpawnedFruit_UsesItsLevelPlanetArt` | 真场景中 1 级 → `planet00`、10 级 → `planet09`；**专属美术不被染色**；视觉直径等于碰撞直径 | **同上**：反证实测 `Expected: "planet00", But was: "fruit_circle"`——未接线时会**静默**退回占位圆片且不报错，正是「看起来能跑、美术其实没生效」那类缺陷 | PASS |

## 自动化运行记录

| 日期 | Unity 版本与环境 | 命令或 Test Runner 过滤器 | 结果文件 | 结论 |
|------|------------------|---------------------------|----------|------|
| 2026-09-11 | Unity 2022.3.62f3 / Windows 10 / `-batchmode -nographics` | `-runTests -testPlatform playmode -testResults Logs\playmode-verify.xml` | `Logs\playmode-verify.xml` | PASS — PlayMode 全量 74/74（本模块 21 项 + 物理诊断 3 项） |

> 环境说明：先在由真工程同步出的隔离副本上运行，随后 MCP 直连真工程本体复跑，两次结果一致（EditMode 103/103、PlayMode 74/74）。详见 `Docs/evidence/README.md`。

## 手动验收前置条件

- 场景、Prefab 与配置：`Main.unity` 已由 `MergeWater/Build Main Scene` 生成；水果贴图与染色由 M7 在运行时通过 `FruitArt` 注入（`GameField.SetFruitArt`），**不需要重建场景**。
- 依赖模块状态：M1 数值资产存在（`Assets/Config/GameBalance.asset`）。
- 目标设备与画质档位：Editor Play Mode；真机项见 `07-bootstrap-test.md`。

## 手动验收

| 编号 | 操作步骤 | 可观察预期结果 | 环境与构建 | 执行者/日期 | 状态 | 证据或备注 |
|------|----------|----------------|------------|-------------|------|------------|
| H1 | 连续投放 30 颗 1–3 级水果至堆满 | 无穿墙、无穿隧、无持续抖动；水果自然堆叠 | Editor | 待执行 | 待手动验收 | V3；需人判断堆叠观感 |
| H2 | 让同级水果在堆顶相撞 | 可见吸附 60ms 后合成，结果水果大小与颜色符合等级 | Editor | 待执行 | 待手动验收 | V2.21 |
| H3 | 堆到警戒线附近 | 短暂超过线不判负，稳定越线后才触发 | Editor | 待执行 | 待手动验收 | V2.8/V2.9 |
| H4 | 让同级水果在堆叠中间相撞 | 结果水果被**明显横向推开**（位移肉眼可见，而不只是「换了个位置」），不向上蹦；周围水果被挤开一点 | Editor | 待执行 | 待手动验收 | V2.31b（2026-09-12「合成力度太吝啬」：水平初速 0.45→1.5 m/s；自动化已验证初速等于配置值） |
| H5 | 观察对局底部的贴地水果 | 水果完整可见，下沿与屏幕底之间留有一条窄空隙（不贴着屏幕下边缘、不被切） | Editor + 真机 | 待执行 | 待手动验收 | V2.42（自动化已验证抬升量与可见间隙；观感需人工确认 0.06 是否合适——这是需求方目视定档值，偏小可在滑杆上手动调回） |

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

- 已验证：A1–A9 与 E1–E4 全部通过（PlayMode，本模块 21 项 + 物理诊断 3 项）。
- 不适用：无。
- 待手动验收：H1–H5（堆叠观感、合成手感、越线判定观感、合成推力观感、贴地水果不被切）。
- 未验证：无（201 项自动化测试通过：EditMode 116 + PlayMode 85，见 `Docs/evidence/README.md`）。
- 未通过：无。
