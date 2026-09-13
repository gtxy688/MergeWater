# M3 Session 局内流程

> 相关需求：`Docs/requirements.md` 的 R2、R4、R5、R6、R7、R9、R10、R16、R26
> 验收文档：`03-session-test.md`

## 职责边界

- 本模块负责：局状态机（`Booting → Ready → Playing → Reviving → GameOver`）、得分累计与最高分同步、连击计时、越线 1.2s 判负、复活次数校验与复活结果应用、阶段目标发放、投放冷却与 next 队列、`SessionEvents` 的拥有与发布、只读 `RoundSnapshot`。
- 本模块不负责：物理实现（M2）、等级表与规则公式（M1）、指针输入（M4）、UI 与音画表现（M5）、广告放行与实际播放、存档落盘（M6）、装配（M7）。
- 复活拆分为两半：**每局 1 次的次数校验与状态应用在本模块**，**90s 全局冷却与实际广告播放由 M6/M7 负责**，避免同一动作播放两次广告。
- 本模块只通过 `IFieldPort` 访问场地，因此可在 EditMode 用 Fake 完整测试。

## 依赖

| 被依赖模块 | 只使用的公开契约 | 用途 | 缺失时的行为 |
|------------|------------------|------|--------------|
| M1 | `GameBalance`、`ComboTracker`、`ScoreRules`、`DropQueue`、`StageProgress`、`SessionEvents`、`RoundSnapshot`、`IFieldPort`、`IAnalyticsService` | 规则与契约 | 确定性依赖 |

不依赖 `IAdsService`：广告由 M6/M7 负责（见「关键机制·复活」）。

## 设计与数据流

`RoundSession` 是纯 C# 类，构造入参：`GameBalance`、`IFieldPort`、`IAnalyticsService`、初始最高分、随机种子，以及两个持久化回调（`Action<int> onBestScoreChanged`、`Action<StageMilestone> onMilestoneReward`，由 M7 桥接 M6）。对外暴露 `Events`（`SessionEvents` 实例）与 `Snapshot`。

`Tick(dt)` 是唯一时间入口：推进连击计时、越线计时、投放冷却、广告冷却提示。物理帧由 Field 自由运行，Session 只在 `Tick` 时轮询 `TryGetDangerViolation`。

`ReleaseDrop(x)` 由 M4/M7 在松手时调用：校验阶段与冷却 → 从队列取当前等级 → `IFieldPort.Drop` → 记录 `DropRecord` → 队列前移 → 发 `DropPerformed`。`MergeEvent` 到达时计分：`ScoreRules.ScoreFor(resultLevel, combo.Multiplier)` → 累加 → `StageProgress.Advance` → 发 `Scored`/`ComboChanged`/`MilestoneReached`；顶级（10 级）不产生 `Merged`，故不计分。

`GameOver` 由越线计时触发：发 `DangerStarted` 后累计超过 V2.8 的 1.2s 即进入 `Reviving`（若本局未用过复活）或直接 `GameOver`。M7 在确认广告冷却并播放完成后调用 `ApplyRevive()`：清除最高一簇（`RemoveHighestCluster`）、重置越线计时与连击、回到 `Playing`；失败或重复调用返回带原因的结果，且不改变局状态。本模块不接触广告。

## 关键机制

### 局状态机

- 触发条件：`StartRound()`、越线判负、`ApplyRevive()`、`EndRound()`。
- 处理顺序：`Booting → Ready`（首次投放前）→ `Playing`；越线满 1.2s → 若可复活则 `Reviving`，否则 `GameOver`；复活成功 → `Playing`；`EndRound()` → `GameOver`。
- 成功结果：非法转换被忽略并返回 false，不抛异常。
- 失败与边界：`Reviving` 期间 `ReleaseDrop` 与道具使用被拒绝；`GameOver` 后除 `StartRound` 外全部拒绝。

### 连击与得分

- 触发条件：`IFieldPort.Merged`。
- 处理顺序：`ComboTracker.RegisterMerge()` → 取倍率 → `ScoreRules.ScoreFor` → `Score += delta` → `BestScore = max(BestScore, Score)` → 发 `Scored` 与 `ComboChanged`。
- 成功结果：每次合成恰好一次计分。
- 失败与边界：`Merged` 在非 `Playing` 阶段到达时忽略（防止复活清场期间误加分）。

### 越线判负

- 触发条件：每帧 `Tick` 轮询 `TryGetDangerViolation`。
- 处理顺序：违规存在 → 累加 `_dangerTimer` 并保证只发一次 `DangerStarted`；违规消失 → 计时清零并发 `DangerEnded`；`_dangerTimer ≥ 1.2s` → 触发失败流程。
- 成功结果：判负前有完整的开始/结束事件对，UI 可做脉冲与心跳。
- 失败与边界：`DangerStarted` 与 `DangerEnded` 严格配对；1.2s 使用 `>=` 判定。

### 复活

- 触发条件：失败后 M7 调用 `ApplyRevive()`（此前 M7 已用 M6 的规则确认广告冷却并播放完成激励视频）。
- 处理顺序：校验阶段与每局次数（V2.10=1）→ 次数 +1 → `RemoveHighestCluster(3)`（V2.11）→ 清空越线计时与连击 → 解冻仿真 → `Playing` → 发布 `ReviveResolved`。
- 成功结果：返回 `ReviveOutcome.Applied` 并移除 ≤3 颗水果。
- 失败与边界：返回 `NotInGameOver`/`AlreadyUsed` 之一；任何失败都不改变局状态。广告失败/跳过由 M7 处理，本模块根本不参与，因此不会重复播放广告。

### 投放与 next 队列

- 触发条件：`ReleaseDrop(x)`。
- 处理顺序：校验 `Playing` 与冷却（V2.20 的 0.05s 震屏窗口后即可再次投放，冷却取手感值 `DropCooldown`）→ 取当前等级 → 投放 → `NextLevel = queue.Peek(dropCount)` → 发 `DropPerformed`。
- 成功结果：`DropCount` 递增；`Snapshot.CurrentLevel` 与 `NextLevel` 始终可读。
- 失败与边界：`IFieldPort.Drop` 返回 false 时队列不前移、不计数。

### 阶段目标

- 触发条件：得分变化。
- 处理顺序：`StageProgress.Advance(Score)` → 每个新达成里程碑发出 `MilestoneReached`，并通过 `onItemsGranted` 回调交给 M6 增加一次性道具。
- 成功结果：每局每节点一次。
- 失败与边界：里程碑奖励发放失败（背包已满等）不影响分数与状态。

## 对外契约

| 名称 | 类型 | 输入 | 输出或事件 | 错误/生命周期保证 |
|------|------|------|------------|-------------------|
| `RoundSession(balance, field, analytics, initialBestScore, seed, onBestScoreChanged, onMilestoneReward)` | 构造 | 见上 | — | 一局一实例；重开需新建 |
| `StartRound()` | 方法 | — | — | 重置分数/连击/投数/队列/越线/复活与里程碑；进入 `Ready` |
| `Tick(dt)` | 方法 | 帧间隔 | — | 非正 dt 被忽略；唯一时间入口 |
| `ReleaseDrop(x)` | 方法 | 世界 x | bool | 非 `Playing`/冷却中/生成失败返回 false |
| `CanRevive` | 属性 | — | bool | `Reviving` 且本局未用过复活 |
| `ApplyRevive()` | 方法 | — | `ReviveOutcome` | 失败不改变状态；不对接广告 |
| `EndRound()` | 方法 | — | — | 幂等 |
| `Snapshot` | 只读结构体 | — | 分数/最高分/连击/倍率/阶段/投数/当前与下一等级/越线中/可复活 | 每次读取为值拷贝 |
| `Events` | `SessionEvents` | — | 见 `01-core.md` | 每局一个实例，跨局不共享 |
| `NotifyItemUsed(kind, result)` | 方法 | 道具与结果 | — | 由 M7 调用，统一发事件与埋点 |
| `CanUseItem()` | 方法 | — | bool | 仅 `Playing` |

## Unity 装配

- 场景与 Prefab：无。`RoundSession` 为纯 C# 对象，由 M7 在 `GameBootstrapper` 中构造并持有。
- 组件与序列化引用：无。
- 创建、启用、禁用和销毁：M7 负责在重开一局时新建 `RoundSession` 并让 M5 重新订阅 `Events`。
- 清理：`Dispose()` 解绑 `IFieldPort.Merged` 订阅。

## 影响与回归范围

- 直接影响模块：M5（订阅事件与快照）、M7（构造与生命周期）、M6（通过回调写入最高分与道具）。
- 必须复验的契约：`ReleaseDrop` 的失败语义；`DangerStarted/Ended` 配对；`ApplyRevive` 的结果枚举语义；`Events` 每局重订阅。
- 对应验收项：A1–A9（EditMode，Fake Field）。

## 待裁定事项

无。
