# M1 Core 契约与领域规则

> 相关需求：`Docs/requirements.md` 的 R1、R2、R3、R4、R5、R6、R7、R8、R9、R10、R11–R15、R22、R24
> 验收文档：`01-core-test.md`

## 职责边界

- 本模块负责：水果等级与数值表、得分规则、连击规则、投放等级队列、阶段里程碑、局内状态枚举与只读快照、`SessionEvents` 事件契约、以及跨模块端口接口（`IFieldPort`、`IAimSource`）与平台能力接口（`IAdsService`/`IAnalyticsService`/`ISaveStore`/`ILeaderboardService`/`IShareService`/`IClock`）。
- 本模块不负责：物理与场景对象（M2）、局状态机推进与计时（M3）、指针输入（M4）、UI 与表现（M5）、存档落盘与广告实现（M6）、装配（M7）。

规则类（`GameBalance`、`ComboTracker`、`ScoreRules`、`DropQueue`、`StageProgress`）不引用 `UnityEngine` 的类型；仅契约结构体使用 `Vector2`。

## 依赖

| 被依赖模块 | 只使用的公开契约 | 用途 | 缺失时的行为 |
|------------|------------------|------|--------------|
| 无 | — | 本模块是最底层契约层 | — |

## 设计与数据流

`GameBalance` 是全部数值的单一来源，由 `GameBalance.Default` 提供与需求 V1/V2 一致的代码默认值；`GameBalanceAsset`（ScriptableObject）仅作为编辑器调参载体，通过 `ToBalance()` 产出一个只读运行时 `GameBalance` 实例。

数据流：`GameBalance` → `DropQueue` 产生待投等级 → M3 通过 `IFieldPort.Drop` 交给 M2 生成实体 → M2 抛出 `MergeEvent` → `ScoreRules` 与 `ComboTracker` 算出得分与倍率 → M3 写入 `SessionEvents`。

`SessionEvents` 由 M3 每局实例化一次并对外暴露只读订阅入口；M5 订阅，M7 传引用。不使用静态事件总线。

## 关键机制

### 数值表与默认值

- 触发条件：应用启动或测试构造 `GameBalance.Default`。
- 处理顺序：按 V1 逐级填写 11 条等级记录，再填写 V2 的规则与手感数值。
- 成功结果：`Tiers.Length == 11`，等级索引 1..11 与 V1 完全一致；半径与质量严格递增；仅 1–3 级有非零投放权重且三者之和为 1.0。
- 失败与边界：找不到等级（越界）时返回结构体默认值并由 `ScoreRules.CanMerge` 判定为不可合成，不抛异常。

### 连击与倍率

- 触发条件：一次成功合成。
- 处理顺序：`ComboTracker.RegisterMerge()` 将当前连击 +1 并把窗口计时清零；`Advance(dt)` 累加时间，超过 V2.1 的 3.0s 时连击归零。
- 成功结果：倍率 = 1 + 0.1×(连击−1)，上限 2.0。
- 失败与边界：连击为 0 时倍率为 1.0；边界时刻（恰好 3.0s）不归零（使用严格大于判断）。

### 投放等级队列

- 触发条件：需要下一颗待投水果等级。
- 处理顺序：按当前累计投数选择等级池——<20 投仅 1–2 级（V2.4）；20–59 投为 1–3 级（V2.5）；≥60 投保持同一等级池与权重不变（V2.6）。在池内按 V1 权重做归一化加权随机。
- 成功结果：返回 1、2 或 3；同一 `seed` 得到可复现序列。
- 失败与边界：投数大于等于 20 后不再随投数改变池与权重；权重全为 0 时回退到等级 1。

### 阶段目标

- 触发条件：分数变化时查询。
- 处理顺序：`StageProgress.Advance(score)` 依次比较 V2.18 的里程碑节点（200/500/1000），返回本次新增达成的里程碑，内部记录已发放下标。
- 成功结果：每个里程碑每局只返回一次。
- 失败与边界：一次跨越多个节点时全部返回；分数回退（复活或撤销不扣分，故实际不回退）不会收回奖励。

## 对外契约

| 名称 | 类型 | 输入 | 输出或事件 | 错误/生命周期保证 |
|------|------|------|------------|-------------------|
| `GameBalance.Default` | 静态数据 | — | 与 V1/V2 一致的数值对象 | 纯数据，无生命周期 |
| `FruitTierDefinition` | 只读数据 | — | Level/DisplayName/Radius/Mass/Score/DropWeight | 越界查询返回默认结构体 |
| `ComboTracker` | 类 | `RegisterMerge()`、`Advance(dt)` | `Combo`、`Multiplier` | 可复用；`Reset()` 清空 |
| `ScoreRules.ScoreFor(level, multiplier)` | 静态方法 | 结果等级、当前倍率 | 四舍五入后的整数分 | 最高级仍返回顶点分（105 × 倍率） |
| `ScoreRules.CanMerge(level, balance)` | 静态方法 | 等级 | 是否可继续合成 | 11 级返回 false |
| `DropQueue` | 类 | 构造种子、`Next(dropCount)` | 下个等级 | 同种子可复现 |
| `StageProgress` | 类 | `Advance(score)` | 新达成里程碑集合 | 每节点仅返回一次 |
| `RoundSnapshot` | 只读结构体 | — | 分数/连击/倍率/阶段/投数/当前与下一等级/越线状态 | 值拷贝，无生命周期 |
| `SessionEvents` | 类 | 订阅 | Drop/Merged/Scored/Combo/Danger/Phase/Milestone/Item/Revive 事件 | 每局一实例；订阅者在销毁时取消 |
| `IFieldPort` | 接口 | 见 `02-field.md` | 物理场地能力 | 由 M2 实现；测试用 Fake 实现 |
| `ISessionView` | 接口 | — | `Events`/`Snapshot`/`Score`/`BestScore`/`CanAct`/`RevivesUsed` 只读视图 | 由 M3 实现，M5 只依赖本接口，避免 M5 → M3 依赖 |
| `IAimSource` | 接口 | 见 `04-aim.md` | 瞄准与投放指令 | 由 M4 实现 |
| `IAdsService`/`IAnalyticsService`/`ISaveStore`/`ILeaderboardService`/`IShareService`/`IClock` | 接口 | 见 `06-meta.md` | 平台能力 | 由 M6 实现；测试用内存实现 |

## Unity 装配

- 场景与 Prefab：本模块无场景对象。
- 组件与序列化引用：`GameBalanceAsset` 是唯一可序列化资产类型，存放于 `Assets/Config/GameBalance.asset`。
- 创建、启用、禁用和销毁：`GameBalanceAsset` 由编辑器工具生成；运行时只读，不写回资产。

## 影响与回归范围

- 直接影响模块：M2（等级与半径）、M3（得分、连击、队列、里程碑）、M4（夹取半径）、M5（展示数值）、M6（每日上限/广告冷却数值来源可按需引用）、M7（装配）。
- 必须复验的契约：`GameBalance.Default` 与 V1/V2 一致；`SessionEvents` 事件签名；`IFieldPort`/`IAimSource` 签名。
- 对应验收项：A1–A7（EditMode）。

## 待裁定事项

无。
