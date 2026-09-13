# M7 Bootstrap 装配与引导

> 相关需求：`Docs/requirements.md` 的 R10、R11–R17、R20、R21、R23、R26
> 验收文档：`07-bootstrap-test.md`

## 职责边界

- 本模块负责：组合根（`GameContext`）与服务装配、初始化顺序、启动流程（Logo/加载 → 隐私弹窗 → 直接进入对局，R26）、广告与埋点的隐私门控、重开一局的生命周期、新手引导编排（R23）、两侧入口与道具使用的编排（含库存扣减、瞄准模式切换、场地作用）、阶段目标奖励落库、红点状态汇总。
- 本模块不负责：各模块内部实现；不重新定义规则或数值。
- 本模块是唯一允许同时引用 M1–M6 的模块（架构总览依赖图）。

## 依赖

| 被依赖模块 | 只使用的公开契约 | 用途 | 缺失时的行为 |
|------------|------------------|------|--------------|
| M1 | `GameBalance`、`IFieldPort`、`IAimSource`、`SessionEvents`、`ItemKind` | 契约 | 确定性依赖 |
| M2 | `GameField`（含 `IFieldPort` 实现） | 场地 | 缺失时启动报错并禁用对局 |
| M3 | `RoundSession` | 局内流程 | 同上 |
| M4 | `AimController`（含 `IAimSource` 实现） | 输入 | 缺失时仅鼠标路径不可用 |
| M5 | `HudBinder`、`PanelController`、`FeedbackDirector`、`AudioDirector`、`AimPreviewView` | 表现 | 缺失时降级为无 UI，仅日志 |
| M6 | `SaveService`、`EconomyService`、`LeaderboardService`、`ShareService`、`PrivacyGate`、`AnalyticsService`、`MetaSettings` | 局外 | 缺失时用内存实现 |

## 设计与数据流

`GameContext` 是普通 C# 类，持有全部服务与模块引用，提供 `NewRound()` 与 `Dispose()`。`GameBootstrapper`（MonoBehaviour）在 `Awake` 构建 `GameContext`（构造顺序：Clock → SaveService → PrivacyGate → Analytics（同意前为 Null 实现）→ Ads → Economy → Leaderboard → Share → Balance → Field → Aim → 每局 Session），`Start` 走启动流程：加载存档 → 若未同意隐私则显示隐私面板并停止（不初始化埋点/广告，R20）→ 同意后初始化埋点与广告 → `NewRound()` → 直接进入对局（R26，无独立开始页）。

`NewRound()`：解绑上一局 `SessionEvents` 订阅 → `field.ClearAll()` → 新建 `RoundSession` → `hud.Bind(session, aim, balance)` → `aim.SetInteractable(true)` → `session.StartRound()` → 引导：若为首局则启动 `TutorialDirector`。

输入编排：`AimController.DropRequested` → `session.ReleaseDrop(x)`；`AimController.ItemTargetRequested` → `ItemUseController.Apply(kind, point)`：校验 `economy` 库存 → 调用对应场地方法（炸弹/锤子）或 `session`/`field`（清屏/摇一摇，摇一摇先要广告放行）→ 扣减库存 → `session.NotifyItemUsed(kind, result)` 发事件 → `analytics.Track`。全程在 `session.Snapshot.Phase == Playing` 且非瞄准广告时执行。

`TutorialDirector`：读取存档 `tutorialFlags`；第 1 局显示「按住拖动—松手」箭头、首次合成高亮、越线前脉冲预警（由 `DangerStarted` 触发）；前 3 局结算页显示分享/排行引导；标记写回存档。

红点汇总：`ItemUseController`/`EconomyService` 计算「存在可领取机会」与「被超越」状态 → `PanelController.SetBadge`。

## 关键机制

### 初始化顺序与隐私门控

- 触发条件：应用启动。
- 处理顺序：本地存档 → 隐私判定 → （未同意）显示隐私面板，埋点/广告使用 `NullAnalytics`/`UnavailableAds` → 同意后替换为真实实现并初始化 → 开局。
- 成功结果：同意前不产生任何上报或广告请求（R20）。
- 失败与边界：存档损坏时按默认值继续；`GameField` 缺失时记录错误并停止对局初始化，不抛未捕获异常。

### 重开一局的生命周期

- 触发条件：结算页「再来一局」、或 `NewRound()` 被引导调用。
- 处理顺序：解绑 → 清场 → 新建 Session → 重绑定 → 重置瞄准 → `StartRound()`。
- 成功结果：分数、连击、越线、复活、队列、里程碑全部重置；上一局事件不会串入新局。
- 失败与边界：上一局残留协程在 `field.ClearAll()` 后自然结束；重复调用 `NewRound()` 幂等（先 `Dispose` 旧局）。

### 道具使用编排

- 触发条件：点击两侧入口并完成瞄准，或直接使用摇一摇。
- 处理顺序：库存校验 → 摇一摇需先过广告放行 → 进入道具瞄准模式（清屏无需瞄准，直接清空全场）→ 收到目标点 → 场地生效 → 扣库存 → 发 `ItemUsed` → 埋点。
- 成功结果：道具数量 −1，场地按 R11–R14 生效。
- 失败与边界：库存为 0 或广告被拒时给出 Toast 并不进入瞄准；瞄准中取消不扣库存。

### 阶段目标奖励落库

- 触发条件：`SessionEvents.MilestoneReached`。
- 处理顺序：`economy.GrantMilestoneReward(kind)` → 存档更新 → Toast + 红点更新。
- 成功结果：每局每节点只发一次（由 M1/M3 保证）。
- 失败与边界：发奖失败只记日志，不影响对局。

### 广告与结算编排

- 触发条件：失败页点复活、结算页显示。
- 处理顺序：复活 → `Session.CanRevive`（每局 1 次）与 `economy.CanRevive()`（90s 全局冷却、离线降级）双重校验 → `aim.SetInteractable(false)` → 广告 → 完成后 `session.ApplyRevive()` → 解冻 → 恢复交互。结算 → 写入最高分与排行 → 按 V2.16 决定插屏 → 前 3 局显示分享/排行引导。
- 成功结果：广告期间无法误操作；插屏频次合规；同一动作只播放一次广告。
- 失败与边界：广告失败/跳过 → 不调用 `ApplyRevive`，Toast 提示，恢复交互，局状态保持 `Reviving`。

## 对外契约

| 名称 | 类型 | 输入 | 输出或事件 | 错误/生命周期保证 |
|------|------|------|------------|-------------------|
| `GameContext` | 类 | 服务集合 | `NewRound()`/`Dispose()` | 单实例；`Dispose` 后不可再用 |
| `GameBootstrapper` | MonoBehaviour | 序列化引用 | `Context` 只读 | `Awake` 构建；`OnDestroy` 释放 |
| `NewRound()` | 方法 | — | — | 幂等；重置全部局内状态 |
| `PrivacyGate` 门控 | 流程 | 同意/拒绝 | — | 同意前零上报零广告 |
| `ItemUseController.Apply(kind, point)` | 方法 | 道具与目标点 | `ItemUseResult` | 失败不扣库存 |
| `TutorialDirector` | 类 | 存档标记 | 引导显示指令 | 首局/前 3 局规则一旦写入不再重复 |

## Unity 装配

- 场景与 Prefab：`Assets/Scenes/Main.unity` 由编辑器工具 `MergeWater/Build Main Scene` 生成，根节点 `GameRoot` 挂 `GameBootstrapper`、`AimController`、`ItemUseController`，子节点 `Field`（`GameField`）与 `Canvas`（HUD 与面板）；同时写入 `EditorBuildSettings`。
- 组件与序列化引用：`GameBootstrapper` 序列化 `GameField`、`AimController`、`HudBinder`、`PanelController`、`AudioDirector`、`GameBalanceAsset`、`MetaSettings`、`TutorialDirector` 引用。
- ScriptableObject / 其他资产：`Assets/Config/GameBalance.asset`、`Assets/Config/MetaSettings.asset`，由编辑器工具从代码默认值生成；缺失时运行时回退 `GameBalance.Default` 并告警。
- 创建、启用、禁用和销毁：`GameBootstrapper` 在 `Awake` 构造、`Start` 初始化、`OnApplicationPause(true)` 保存、`OnDestroy` 保存并 `Dispose`。
- 清理：重开局只重建 Session，不重建服务；退出时保存一次。

## 影响与回归范围

- 直接影响模块：全部（作为组合根）。
- 必须复验的契约：`NewRound()` 的重置完整性；隐私门控；道具扣减与场地效果一致；插屏频次。
- 对应验收项：A1–A6（PlayMode 集成 + EditMode 资产校验）+ H1–H4（手动）。

## 待裁定事项

无。
