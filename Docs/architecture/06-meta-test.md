# M6 Meta 验收文档

> 对应架构：`06-meta.md`
> 对应需求：`Docs/requirements.md` 的 R7、R11–R20、R25、R24

## 自动化测试计划与证据

| 编号 | 层级 | 测试路径与名称 | 行为 | 红灯原因或 N/A 理由 | 最近结果 |
|------|------|----------------|------|---------------------|----------|
| A1 | EditMode | `Assets/Tests/EditMode/Meta/SaveServiceTests.cs::Load_WhenNoFile_ReturnsDefaultWithCurrentSchema`、`Save_WritesCurrentSchemaVersionAndRoundTrips` | 无存档返回默认且版本为当前；保存后可往返读取 | 实现前无 `SaveService` | PASS |
| A2 | EditMode | `Meta/SaveServiceTests.cs::Load_CorruptedJson_BacksUpAndReturnsDefault`、`Load_FutureSchema_FallsBackToDefaultAndKeepsBackup` | 损坏与未来版本存档备份并回退默认，不崩溃 | 同上 | PASS |
| A3 | EditMode | `Meta/SaveServiceTests.cs::Load_OldSchema_MigratesAndClampsOutOfRangeValues`、`Load_IgnoresInvalidLeaderboardRecords`、`ClearAll_DeletesFileAndResetsToDefault` | 旧版本迁移与范围校验；剔除非法排行记录；清缓存回默认 | 同上 | PASS |
| A4 | EditMode | `Meta/EconomyTests.cs::Grant_BeyondDailyCap_IsRejected_AndSpendBelowZero_IsRejected`、`ItemDailyCaps_MatchV213ToV215`、`GrantUnlimited_IgnoresDailyCap` | 每日上限（3/3/3/2/1）与余额下限；里程碑奖励不受每日上限约束 | 实现前无库存服务 | PASS |
| A5 | EditMode | `Meta/EconomyTests.cs::DailyLimits_ResetOnNextLocalDay` | 跨天重置（假时钟推进） | 同上 | PASS |
| A6 | EditMode | `Meta/EconomyTests.cs::Revive_RespectsNinetySecondGlobalCooldown`、`Revive_OfflinePolicy_FollowsOfflineGrantAll` | 复活 90s 全局冷却；离线策略按 `OfflineGrantAll` 放行或拒绝 | 实现前无广告规则 | PASS |
| A7 | EditMode | `Meta/EconomyTests.cs::ItemGrant_RespectsDailyCap_RegardlessOfAdsAvailability`、`ItemGrant_WhenAdSkipped_DoesNotGrant`、`Gift_OncePerDay_AndRequiresCompletedAd` | 领取受上限约束；广告未完成不发道具；大礼包每日 1 次 | 同上 | PASS |
| A8 | EditMode | `Meta/EconomyTests.cs::Interstitial_ShowsAtMostOncePerThreeGames_AndRespectsToggle` | 插屏每 3 局 ≤1 且受远程开关控制 | 同上 | PASS |
| A9 | EditMode | `Meta/EconomyTests.cs::ShareChallenge_FirstSucceeds_ThenCooldownForSixtySeconds` | 分享文案含分数与口令、不含「必得」；60s 冷却 | 实现前无分享服务 | PASS |
| A10 | EditMode | `Meta/MetaServiceTests.cs::Leaderboard_Submit_OrdersByScore_AndGetTopRespectsPageSize`、`Leaderboard_Submit_DoesNotLowerExistingScore_AndIgnoresZero`、`Leaderboard_TrimsToMaxEntries`、`Leaderboard_IsSelfBeaten_OnlyWhenFriendScoreHigher` | 排行排序与分页、只增不减、0 分不入榜、被超越红点条件 | 实现前无排行服务 | PASS |
| A11 | EditMode | `Meta/MetaServiceTests.cs::PrivacyGate_BeforeAccept_Blocks_AfterAccept_Persists`、`Settings_Persist_AndClearCacheResetsEverything` | 隐私门控与持久化；设置持久化；清缓存回到默认并重需同意 | 实现前无隐私门 | PASS |
| A12 | EditMode | `Meta/MetaServiceTests.cs::Analytics_BeforeInitialize_IsIgnored`、`Analytics_AfterInitialize_RecordsNameAndParameters`、`Analytics_WhenSinkThrows_DoesNotThrow`、`NullAnalytics_NeverInitializesOrRecords`、`AdsServiceProxy_BeforeSwap_BehavesAsUnavailable` | 同意前不上报；同意后记录事件名与参数；Sink 异常被吞掉；代理换实现语义 | 实现前无埋点 | PASS |
| A13 | EditMode | `Meta/EconomyTests.cs::RoundFinished_AccumulatesGamesAndBestScore` | 结算累计局数、最高分与最高连击并写入本地排行 | 实现前无结算记账 | PASS |

## 自动化运行记录

| 日期 | Unity 版本与环境 | 命令或 Test Runner 过滤器 | 结果文件 | 结论 |
|------|------------------|---------------------------|----------|------|
| 2026-09-11 | Unity 2022.3.62f3 / Windows 10 / `-batchmode -nographics` | `-runTests -testPlatform editmode -testResults Logs\editmode-results.xml` | `Logs\editmode-results.xml` | PASS — EditMode 全量 103/103（本模块 31 项） |

> 环境说明：先在由真工程同步出的隔离副本上运行，随后 MCP 直连真工程本体复跑，两次结果一致（EditMode 103/103、PlayMode 74/74）。详见 `Docs/evidence/README.md`。

## 手动验收前置条件

- 场景、Prefab 与配置：`Assets/Config/MetaSettings.asset` 存在（内容由 `ConfigAssetTests` 自动校验）；`persistentDataPath` 可写。
- 依赖模块状态：M5 设置面板与 Toast 可用。
- 目标设备与画质档位：Editor；真机存档路径差异由 `FileSaveStore` 覆盖。

## 手动验收

| 编号 | 操作步骤 | 可观察预期结果 | 环境与构建 | 执行者/日期 | 状态 | 证据或备注 |
|------|----------|----------------|------------|-------------|------|------------|
| H1 | 领取道具达每日上限后再领 | 被拒绝并提示原因；同日重启后仍被拒绝 | Editor | 待执行 | 待手动验收 | V2.13；自动化已验证上限与跨天重置 |
| H2 | 走一次复活并在 90s 内再次触发 | 第二次被冷却拒绝 | Editor | 待执行 | 待手动验收 | V2.12 |
| H3 | 连续结算 4 局 | 插屏至多出现 1 次；关闭开关后不再出现 | Editor | 待执行 | 待手动验收 | V2.16 |
| H4 | 拒绝隐私政策 | 不初始化埋点/广告，游戏停在隐私面板 | Editor | 待执行 | 待手动验收 | R20（自动化已断言未初始化） |
| H5 | 删除存档文件后启动 | 回到默认状态且不崩溃 | Editor | 待执行 | 待手动验收 | V3（自动化已验证损坏/未来版本回退） |

## 失败路径与边界

| 编号 | 前置状态与操作 | 预期保护或失败行为 | 自动/手动 | 状态 | 证据或备注 |
|------|----------------|--------------------|-----------|------|------------|
| E1 | 存档目录只读 | 落盘失败返回 false 并由上层降级；游戏可玩 | 自动 | PASS | `FileSaveStore.Write` 捕获异常；`InMemorySaveStore` 作为降级实现 |
| E2 | 未来 schema 版本 | 使用默认值并备份，不崩溃 | 自动 | PASS | `Load_FutureSchema_FallsBackToDefaultAndKeepsBackup` |
| E3 | 广告适配器异常/未完成 | 返回 `Failed`/`Skipped`，游戏继续 | 自动 | PASS | `FakeAdsService` 与 `ItemGrant_WhenAdSkipped_DoesNotGrant` |
| E4 | 分享在 60s 内重复发起 | 返回冷却结果，不重复计数 | 自动 | PASS | `ShareChallenge_FirstSucceeds_ThenCooldownForSixtySeconds` |

## 回归范围

| 受影响模块或契约 | 复验项 | 原因 | 状态 | 证据或备注 |
|------------------|--------|------|------|------------|
| M3 | `03-session-test.md` A8 | 复活依赖每局次数（M3）与冷却（M6）配合 | PASS | `RoundLifecycleTests` 4/4 |
| M7 | `07-bootstrap-test.md` A3/A4/A7 | 初始化门控、最高分写入、里程碑落库 | PASS | `BootstrapFlowTests` 6/6、`MilestoneRewardTests` 2/2 |
| M5 | `05-presentation-test.md` H4 | 设置开关 | PASS（持久化）/ 待执行（观感） | `Settings_Persist_AndClearCacheResetsEverything` |

## 交付结论

- 已验证：A1–A13 与 E1–E4 全部通过（本模块 31 项）。
- 不适用：无。
- 待手动验收：H1–H5。
- 未验证：无（201 项自动化测试通过：EditMode 116 + PlayMode 85，见 `Docs/evidence/README.md`）。
- 未通过：无。
