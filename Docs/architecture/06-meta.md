# M6 Meta 局外系统

> 相关需求：`Docs/requirements.md` 的 R7、R11–R19、R20、R21、R24、R25
> 验收文档：`06-meta-test.md`

## 职责边界

- 本模块负责：本地存档（schema 版本与迁移）、道具库存与每日上限、激励视频/插屏广告的接口与测试适配器、广告位放行规则（复活 90s 全局冷却、每日上限、插屏每 3 局 ≤1 且可远程开关）、本地排行榜、分享记录与 60s 冷却、隐私合规门、埋点（接口 + 本地实现）、设置开关持久化。
- 本模块不负责：局内规则与物理（M1/M2/M3）、UI 绘制（M5）、初始化顺序与流程编排（M7）。
- 本模块实现 M1 中的 `ISaveStore`/`IAdsService`/`IAnalyticsService`/`ILeaderboardService`/`IShareService`/`IClock` 接口；核心逻辑可在 EditMode 用内存实现与假时钟测试。

## 依赖

| 被依赖模块 | 只使用的公开契约 | 用途 | 缺失时的行为 |
|------------|------------------|------|--------------|
| M1 | 上述平台接口、`ItemKind`、`GameBalance`（上限与冷却数值） | 接口与数值 | 确定性依赖 |
| Unity | `Application.persistentDataPath`、`JsonUtility`、`PlayerPrefs`（可选） | 落盘与序列化 | 落盘失败回退内存并告警 |

## 设计与数据流

`SaveData` 是可序列化 POCO（`schemaVersion`、`bestScore`、`itemCounts`、每日领取记录与日期键、`settings`、`gamesPlayed`、`interstitialCounter`、`leaderboard`、`tutorialFlags`、`privacyAccepted`、`lastShareUtc`、`lastReviveAdUtc`）。`SaveService` 通过 `ISaveStore` 读写：生产用 `FileSaveStore`（`persistentDataPath/mwater_save.json`），测试用 `InMemorySaveStore`。加载流程：读 JSON → `Migrate(fromVersion)` → 校验字段范围 → 落回当前 schema；任何异常回退 `SaveData.CreateDefault()` 并保留损坏文件副本。

`EconomyService` 是局外规则的统一入口，组合 `InventoryService`、`DailyLimitService`、`AdsPlacementRules` 与 `IAdsService`。UI 与 M7 只调用它，不直接改存档。

`DailyLimitService` 以 `IClock.Today`（本地日期字符串 `yyyy-MM-dd`）为键，跨天自动清零计数。

`AnalyticsService` 接收 `IAnalyticsSink`；生产为 `UnityDebugSink`（`Debug.Log` 结构化事件）与预留的 `BufferedSink`，测试为 `InMemorySink`。事件名使用 `requirements.md` R24 的固定枚举，避免字符串漂移。

## 关键机制

### 存档与迁移

- 触发条件：启动加载、每次状态变更后节流保存。
- 处理顺序：加载 → 版本比较 → 迁移链（v1→v2→…）→ 范围校验（分数非负、道具数量夹到 [0,999]、日期键合法）→ 写回。
- 成功结果：`Load()` 总能返回可用 `SaveData`。
- 失败与边界：文件不存在返回默认值；JSON 损坏时改名保留 `.bak` 并返回默认值；版本高于当前程序时按默认值处理并告警（不崩溃）。

### 道具库存与每日上限

- 触发条件：领取（看广告/分享）或使用道具。
- 处理顺序：领取前检查当日已领取次数（V2.13 各 3、V2.14 摇一摇 2、V2.15 大礼包 1）；通过则 `+1` 并记当日计数；使用时要求数量 >0 则 `−1`（使用即时、无冷却）。
- 成功结果：返回 `GrantResult`/`SpendResult`；上限与余额在存档中持久化。
- 失败与边界：达到上限返回 `DailyCapReached`；余额为 0 返回 `NoStock`；跨天首次访问自动重置当日计数。

### 广告位规则

- 触发条件：复活、道具领取、摇一摇、大礼包、结算插屏。
- 处理顺序：`AdsPlacementRules.CanShow(placement, now, save)` 依次检查开关、每日上限与冷却（复活全局 90s、分享助力 60s、插屏每 3 局 ≤1）→ 放行后调用 `IAdsService.ShowRewarded/ShowInterstitial` → 结果写回存档（`lastReviveAdUtc`、`interstitialCounter`）。
- 成功结果：返回 `AdDecision.Allowed` 或带原因的拒绝。
- 失败与边界：`IAdsService.Availability == Unavailable`（Editor/离线）时，复活按 D6 直接放行且不写冷却；道具领取返回 `Unavailable` 并提示「测试环境直接领取」由 `AdsPlacementRules` 的 `OfflineGrantAll` 策略决定（默认允许，便于演示）。

### 本地排行榜

- 触发条件：结算或查看排行。
- 处理顺序：`LeaderboardService.Submit(score, displayName)` 插入并排序，取前 N（每页 20）；本地单机用户名默认「我」。
- 成功结果：破纪录后第一名更新为当前分数。
- 失败与边界：空榜返回空列表；分数为 0 不写入（避免污染）。

### 分享与冷却

- 触发条件：`IShareService.ShareChallenge(score, combo)`。
- 处理顺序：检查 60s 冷却（V2.19）→ 记录时间与事件 → 返回文案（含分数与口令）→ 由平台层展示。回调不可信，只按「发起即记录」发放助力。
- 成功结果：返回 `ShareResult` 与文案。
- 失败与边界：冷却中返回 `Cooldown`，文案不做「必得」承诺。

### 隐私合规门

- 触发条件：启动时读取 `privacyAccepted`。
- 处理顺序：未同意 → `PrivacyGate.IsAccepted == false`，M7 阻止初始化埋点与广告并显示隐私面板；同意 → 写入存档并放行。
- 成功结果：同意前 `IAnalyticsService`/`IAdsService` 不被 M7 调用。
- 失败与边界：拒绝不退出游戏，仅停在隐私面板；清除缓存会重置 `privacyAccepted`（下次启动重新弹窗）。

### 设置与埋点

- 触发条件：设置开关变更、R24 事件发生。
- 处理顺序：设置写入存档并立即应用于 `AudioDirector`/震动；埋点事件进 `IAnalyticsSink` 并携带关键参数。
- 成功结果：重启后开关保持；事件可在日志中检索。
- 失败与边界：Sink 抛异常时被捕获并降级为一次告警，不影响游戏流程。

## 对外契约

| 名称 | 类型 | 输入 | 输出或事件 | 错误/生命周期保证 |
|------|------|------|------------|-------------------|
| `SaveService.Load()/Save()` | 类 | — | `SaveData` | 总能返回可用对象；损坏时回退默认 |
| `ISaveStore.Read()/Write(json)` | 接口 | 字符串 | 字符串/bool | 失败返回 false 由上层降级 |
| `InventoryService.Grant/Spend(kind, n)` | 类 | 道具与数量 | 结果枚举 | 夹取上限与下限，持久化 |
| `DailyLimitService.GetRemaining(kind)` | 类 | 道具 | 剩余次数 | 跨天自动重置 |
| `EconomyService.TryConsumeForItem(kind)` | 类 | 道具 | `AdDecision` + 文案 | 汇总上限、冷却、适配器可用性 |
| `IAdsService.ShowRewarded(placement)` | 接口 | 广告位 | `RewardedResult`（Completed/Skipped/Failed/Unavailable） | 不抛异常；结果枚举稳定 |
| `IAdsService.ShowInterstitial()` | 接口 | — | `InterstitialResult` | 同上 |
| `AdsPlacementRules.CanShow(...)` | 静态/类 | 广告位、时间、存档 | `AdDecision` | 纯逻辑，可 EditMode 测 |
| `LeaderboardService.Submit/GetTop` | 类 | 分数、名字、条数 | 列表 | 排序稳定，空榜安全 |
| `ShareService.ShareChallenge(score, combo)` | 类 | 分数、连击 | `ShareResult` + 文案 | 60s 冷却；不承诺必得 |
| `PrivacyGate.Accept()/IsAccepted` | 类 | — | — | 幂等；持久化 |
| `AnalyticsService.Track(name, params)` | 类 | 事件名与参数 | — | 异常不影响调用方 |
| `IClock.Now/Today` | 接口 | — | 时间 | 测试用实现可注入 |

## Unity 装配

- 场景与 Prefab：无必需场景对象；`GameBootstrapper` 在运行时构造这些服务。
- 组件与序列化引用：可选 `MetaSettings`（ScriptableObject）用于广告开关等远程可配项；缺失用默认。
- ScriptableObject / 其他资产：`MetaSettings.asset`（`Assets/Config/`），含 `interstitialEnabled`（默认 true）等。
- 创建、启用、禁用和销毁：服务为普通 C# 对象，生命周期与 `GameBootstrapper` 一致；`SaveService` 在应用失焦/退出时 `Save()`。
- 清理：无全局静态状态；不缓存跨会话可变数据。

## 影响与回归范围

- 直接影响模块：M3（复活广告与最高分回调）、M7（初始化顺序与门控）、M5（设置与红点数据）。
- 必须复验的契约：`SaveData` schema 与迁移；`AdDecision`/`RewardedResult` 枚举语义；每日上限与冷却边界。
- 对应验收项：A1–A11（EditMode）。

## 待裁定事项

无。
