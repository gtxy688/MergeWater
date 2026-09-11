# M4 Aim 输入与瞄准

> 相关需求：`Docs/requirements.md` 的 R1、R2、R8、R11、R12、R13、V2.26
> 验收文档：`04-aim-test.md`

## 职责边界

- 本模块负责：把指针（触摸/鼠标）输入翻译为瞄准状态；落点 x 的夹取；抛物线 + 反弹预演的采样；道具瞄准模式（锤子/炸弹定点）；松手时发出投放或道具目标指令；把可交互性交给 M7/M3 控制。
- 本模块不负责：决定投放等级或消耗队列（M3）、实际生成水果（M2）、消耗道具库存（M6/M7）、绘制预览线（M5 订阅 `AimState` 绘制）。
- 本模块实现 `Core.IAimSource`；纯计算部分（`AimSolver`）不依赖 MonoBehaviour，可在 EditMode 测试。

## 依赖

| 被依赖模块 | 只使用的公开契约 | 用途 | 缺失时的行为 |
|------------|------------------|------|--------------|
| M1 | `AimState`、`ItemKind`、`IAimSource`、`GameBalance`（预演时长与场地范围） | 契约与数值 | 确定性依赖 |
| Unity | `Input`、`Camera`、`Screen` | 指针与坐标转换 | — |

## 设计与数据流

`AimSolver`（静态纯函数）：给定世界 y 起点、重力、可放置 x 区间与水果半径，产出 `ClampX(x)` 与 `SampleTrajectory(startX, startY, vx, vy, maxTime, step)` 的点列；反弹预演按抛物线与左右墙的镜像反射简化计算，最多 V2.26 的 0.8s。

`AimController`（MonoBehaviour，实现 `IAimSource`）：每帧读取 `Input`。按下 → `IsAiming = true`、记录起点；拖动 → 更新 `State.X`（经 `ClampX` 夹取）；松手 → 若是普通模式发 `DropRequested(x)`，若是道具模式发 `ItemTargetRequested(kind, worldPoint)`，然后回到 `Idle`。指针离开窗口或被 `SetInteractable(false)` 时取消瞄准且不发指令。

坐标转换：屏幕点 → 世界点用正交相机 `ScreenToWorldPoint`，以 `z = -cameraZ` 得到与场地同平面的点。

## 关键机制

### 落点夹取

- 触发条件：指针位置变化。
- 处理顺序：`ClampX(rawX)` 把 x 夹到 `[minX + radius, maxX - radius]`，其中 `radius` 来自 `SetDropRadius`（当前待投水果半径）。
- 成功结果：`State.X` 始终在可放置区间内，`NormalizedX ∈ [0,1]`。
- 失败与边界：区间因半径过大而反转时（理论上不会发生，最大半径 1.12 < 场地半宽），取区间中点并告警一次。

### 抛物线 + 反弹预演

- 触发条件：`IsAiming` 为真且 M5 请求采样。
- 处理顺序：以落点 x 与固定初始速度（由 `GameBalance` 的 `DropInitialSpeed` 提供，向上初速）积分采样，纵向到地面；横向遇墙按镜像反射，累计时间不超过 V2.26 的 0.8s。
- 成功结果：返回有序点列，至少 2 个点。
- 失败与边界：重力或速度为 0 时返回两点直线，不死循环。

### 道具瞄准模式

- 触发条件：`BeginItemAim(kind, radius)`。
- 处理顺序：置 `State.AimItem = kind`、记录命中半径；拖动时 `State.X` 与命中点跟随指针；松手发 `ItemTargetRequested`；`CancelItemAim()` 只清状态、不发指令。
- 成功结果：一次瞄准最多发一次目标指令。
- 失败与边界：`SetInteractable(false)` 或指针离开时自动取消，不消耗道具（M7 依据是否收到目标指令决定是否扣除）。

### 互斥与可交互性

- 触发条件：`SetInteractable(false)`（广告播放、结算、复活、暂停、隐私弹窗）。
- 处理顺序：取消当前瞄准并忽略后续输入直到恢复。
- 成功结果：广告与弹窗期间不会误投放。
- 失败与边界：恢复可交互后不补发被忽略的指令。

## 对外契约

| 名称 | 类型 | 输入 | 输出或事件 | 错误/生命周期保证 |
|------|------|------|------------|-------------------|
| `State` | `AimState` 只读 | — | `IsAiming`/`AimItem`/`X`/`NormalizedX` | 每帧可读 |
| `DropRequested` | 事件 | 世界 x | — | 每次松手恰好一次；取消时不发 |
| `ItemTargetRequested` | 事件 | `ItemKind`、世界点 | — | 每次道具瞄准松手恰好一次 |
| `SetDropRadius(radius)` | 方法 | 半径 | — | 立即影响夹取 |
| `SetDropBounds(minX, maxX)` | 方法 | 世界 x 区间 | — | 立即生效 |
| `SetInteractable(bool)` | 方法 | — | — | false 时取消瞄准且不发指令 |
| `BeginItemAim(kind, radius)` / `CancelItemAim()` | 方法 | 道具种类、命中半径 | — | 成对使用；取消不产生副作用 |
| `BuildPreview(points)` | 方法 | 点列容器 | 采样点数 | 垂直下落 + 镜像反弹（V2.26）；点数 <2 时表现层应隐藏虚线 |
| `AimSolver.ClampX` / `SampleTrajectory` | 静态纯函数 | 见上 | 数值/点列 | 无状态，可重入 |

## Unity 装配

- 场景与 Prefab：`Main.unity` 中 `AimController` 挂在 `GameRoot`，序列化引用主相机。
- 组件与序列化引用：主相机（正交）、`GameField` 的场地范围由 M7 通过 `SetDropBounds` 注入。
- 创建、启用、禁用和销毁：`Update` 中仅当 `Interactable` 为真且 M7 注入的 `IAimSource` 被启用时处理输入；`OnDisable` 取消瞄准。
- 清理：无外部订阅。

## 影响与回归范围

- 直接影响模块：M3（消费投放指令）、M5（绘制预览）、M7（道具编排与可交互性开关）。
- 必须复验的契约：`DropRequested`/`ItemTargetRequested` 的「恰好一次」语义；`SetInteractable(false)` 的取消语义。
- 对应验收项：A1–A5（EditMode 纯计算）+ A6–A7（PlayMode 输入集成）。

## 待裁定事项

无。
