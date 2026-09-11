# M2 Field 对局场地

> 相关需求：`Docs/requirements.md` 的 R2、R3、R6、R11、R12、R13、R14、V3
> 验收文档：`02-field-test.md`

## 职责边界

- 本模块负责：水果实体的生成/销毁与视觉同步、2D 物理堆叠参数、同级碰撞到合成的解析与吸附时序、越线物理查询、道具对场地的直接作用（撤销/炸弹/锤子/摇一摇）、场地边界与仿真开关。
- 本模块不负责：得分与连击计算（M3）、投放等级决策（M3）、指针输入与瞄准（M4）、库存与广告（M6）、HUD 表现（M5 负责订阅事件后的表现）。
- 本模块实现 `Core.IFieldPort`。

## 依赖

| 被依赖模块 | 只使用的公开契约 | 用途 | 缺失时的行为 |
|------------|------------------|------|--------------|
| M1 | `GameBalance`、`FruitTierDefinition`、`MergeEvent`、`DangerViolation`、`DropRecord`、`ItemKind`、`IFieldPort` | 数值、事件结构、端口定义 | 缺失则无法编译，属确定性依赖 |
| Unity | `Rigidbody2D`、`CircleCollider2D`、`PhysicsMaterial2D`、`SpriteRenderer` | 物理与显示 | — |

## 设计与数据流

`GameField` 是场地唯一入口（MonoBehaviour，实现 `IFieldPort`），持有 `FruitBody` 列表与 `fruitId → FruitBody` 字典。生成的水果一律是运行时构造的 GameObject：`SpriteRenderer`（占位圆片，按等级着色并按半径缩放）+ `CircleCollider2D`（半径 = V1 半径）+ `Rigidbody2D`（质量 = V1 质量）+ `FruitBody`。

场地由左右墙与地面构成（`BoxCollider2D`，静态）；警戒线只是一个 y 坐标阈值，由 `GameField.DangerLineY` 暴露，M5 用其绘制警戒线视觉。

合成数据流：`FruitBody.OnCollisionEnter2D` → `GameField.RequestMerge(a, b)` → 双方进入 `Pending`、碰撞暂停、向中点吸附 → 吸附结束销毁双方并按结果等级生成新水果 → 抛出 `Merged`。M3 收到事件后计分。

## 关键机制

### 水果生成

- 触发条件：`IFieldPort.Drop(level, x, out id)`。
- 处理顺序：夹取 x 到场地可放范围；构造实体；设置物理参数（`gravityScale`、`collisionDetectionMode = Continuous`、`interpolation = Interpolate`、`sleepMode = StartAwake`）；登记 id；初始化时给一个极小随机水平扰动以避免完全垂直堆叠的数值退化。
- 成功结果：返回 true 并输出唯一 id。
- 失败与边界：场地对象未初始化、等级越界或达到实例上限（V3 的 120）时返回 false，且不产生任何对象。

### 物理稳定性

- 触发条件：任何水果进入仿真。
- 处理顺序：圆碰撞体 + 共享 `PhysicsMaterial2D`（低弹性、中摩擦）；`Rigidbody2D` 限速（`maxLinearVelocity` 由 `GameBalance` 提供，默认 20 m/s）；开启 CCD；睡眠阈值由 `Rigidbody2D.sleepThreshold` 配置。
- 成功结果：堆叠稳定，无持续抖动与穿隧。
- 失败与边界：速度超过上限时被裁剪而非积分爆炸；水果掉出场地底部（y 低于 `killY`）时由 `FieldSanitizer` 回收并记一条告警，不影响对局（属异常保护）。

### 合成解析与吸附

- 触发条件：两个 `Level` 相同、均非 `Pending`、且 `ScoreRules.CanMerge(level)` 为真的碰撞。
- 处理顺序：`RequestMerge` 立即把双方标记 `Pending`、互相忽略碰撞、置为动态但关闭重力，随后在 `AbsorbDuration`（V2.21，默认 60ms）内把两者向中点插值；到期时销毁双方，在中点按 `level+1` 生成结果水果，并给结果一个轻微向上初速以脱离相邻堆叠。
- 成功结果：抛出一次 `Merged{ SourceLevel, ResultLevel, Position }`。
- 失败与边界：任一方在吸附期间被道具移除或已销毁时取消合成，不产生新水果也不加 `Merged` 事件；11 级相撞不进入本流程（`CanMerge` 为 false），只保留物理碰撞。

### 越线物理查询

- 触发条件：M3 每帧轮询 `TryGetDangerViolation`。
- 处理顺序：遍历所有已登记水果，取 `collider.bounds.max.y` 超过 `DangerLineY` 且 `linearVelocity.magnitude` 与 `angularVelocity` 均低于 `GameBalance` 的静止阈值（V2.9）的水果；若有多个，返回溢出量最大者。
- 成功结果：`violation.FruitId/TopY/Overflow` 被填充并返回 true。
- 失败与边界：仍在弹跳的水果（速度高于阈值）不视为违规；场内无水果返回 false。

### 道具对场地的作用

- 撤销：`TryPeekLastDrop(out record)` 返回本局最后一颗「未被标记 Merged」的投放记录；`RemoveFruit(id)` 移除它。是否可用由 M3/M7 依据 `record.Merged` 判断。
- 炸弹：`RemoveFruitInRadius(point, 0.6m)` 移除半径内全部水果（V2.17），返回移除数量。
- 锤子：`RemoveSingleNearest(point, maxRadius, out id)` 只移除最近的一颗。
- 摇一摇：`ApplyShakeShuffle(impulse, seed)` 对全场水果施加小幅确定性随机冲量（同一 seed 可复现），不直接移除任何水果（R14）。
- 边界：道具作用于空场地时返回 0/false，不报错。

### 仿真开关

- `SetSimulationEnabled(false)` 把场地内所有刚体置为 `Kinematic` 并缓存原有速度，用于暂停/结算/广告播放；恢复时还原速度与 `Dynamic`。这是唯一允许冻结物理的入口，避免各模块各自改 `timeScale` 造成冲突。

## 对外契约

| 名称 | 类型 | 输入 | 输出或事件 | 错误/生命周期保证 |
|------|------|------|------------|-------------------|
| `Drop(level, x, out id)` | `IFieldPort` | 等级 1–11、世界 x | bool、唯一 id | 失败时不产生对象；id 单调递增 |
| `LiveFruitCount` | `IFieldPort` | — | 当前存活数量 | 恒等于字典大小 |
| `TryGetDangerViolation(out v)` | `IFieldPort` | — | 是否越线 + 最严重者 | 无水果返回 false |
| `TryPeekLastDrop(out r)` | `IFieldPort` | — | 最后一颗未合成投放记录 | 无投放返回 false |
| `RemoveFruit(id)` | `IFieldPort` | id | 是否移除 | 幂等；不存在返回 false |
| `RemoveFruitInRadius(point, r)` | `IFieldPort` | 点、半径 | 移除数量 | 半径 ≤0 返回 0 |
| `HasFruitInRadius(point, r)` | `IFieldPort` | 点、半径 | 是否有水果 | 无副作用，用于道具命中判定；半径 ≤0 返回 false |
| `RemoveSingleNearest(point, max, out id)` | `IFieldPort` | 点、最大半径 | 是否移除 | 范围内无水果返回 false |
| `RemoveHighestCluster(max)` | `IFieldPort` | 最多移除数 | 实际移除数 | 按「最高水果所在连通簇」由高到低挑选，≤max |
| `ApplyShakeShuffle(impulse, seed)` | `IFieldPort` | 冲量、种子 | — | 同种子可复现；不消除水果 |
| `SetSimulationEnabled(bool)` | `IFieldPort` | — | — | 成对调用；恢复后速度还原 |
| `Merged` | 事件 | — | `MergeEvent` | 每次成功合成恰好一次 |
| `DangerLineY` | 属性 | — | 世界 y | 只读 |
| `AbsorbDuration` | 属性 | 秒 | — | 测试可设为 0 以加速 |

## Unity 装配

- 场景与 Prefab：`Main.unity` 中 `GameField` 挂在 `Field` 根节点；左右墙与地面为其子节点；水果不使用 Prefab，全部运行时构造，避免资源依赖。
- 组件与序列化引用：`GameField` 需序列化 `PhysicsMaterial2D`、`TileLike` 占位 sprite、`DangerLineY`、`killY` 与场地可放置 x 范围。
- ScriptableObject / 其他资产：占位圆片 sprite 由 `PlaceholderArtGenerator` 生成于 `Assets/Art/Placeholder/`。
- 创建、启用、禁用和销毁：`GameField` 在 `Awake` 建立字典与边界；`OnDestroy` 解绑碰撞回调并清空列表；`ClearAll()` 用于重开一局时整体清理。

## 影响与回归范围

- 直接影响模块：M3（通过 `IFieldPort`）、M5（`Merged`/`DangerStarted` 的表现）、M7（装配与道具）、M4（夹取范围）。
- 必须复验的契约：`IFieldPort` 全部方法签名与失败语义；`Merged` 事件恰好一次。
- 对应验收项：A1–A7（PlayMode）。

## 待裁定事项

| 问题 | 候选方案 | 影响 | 需要谁裁定 |
|------|----------|------|------------|
| 复活时「最高一簇」的簇定义 | (a) 以最高水果为种子做接触连通簇，由高到低取 ≤3 颗；(b) 仅取全场最高的 ≤3 颗，不要求连通 | (a) 更符合 R7「连成簇」文案；(b) 实现更简单但可能拆散堆叠 | 已按 GDD R7 采用 (a)，记录为决策 D7 |
