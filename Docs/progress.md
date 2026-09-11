# 进度追踪

> 更新日期：2026-09-11
> 本文件只记录状态和阻塞，不定义需求或架构。

## 模块进度

| 模块 | 阶段 | 最近验证 | 待手动验收 | 阻塞 | 下一步 |
|------|------|----------|------------|------|--------|
| M1 Core | 待验收（自动化全绿） | 2026-09-11 EditMode 30 项 PASS | 无（N/A） | 无 | 等真工程本体复跑 + 人工确认 |
| M2 Field | 待验收（自动化全绿） | 2026-09-11 PlayMode 21 项 PASS | H1–H3 | 无 | 手动验收堆叠/合成/越线观感 |
| M3 Session | 待验收（自动化全绿） | 2026-09-11 EditMode 17 项 + PlayMode 4 项 PASS | H1–H3 | 无 | 手动验收单局体验与复活 |
| M4 Aim | 待验收（自动化全绿） | 2026-09-11 15 项 PASS（EditMode 8 + PlayMode 7） | H1–H3 | 无 | 手动验收拖动跟手与真机触屏 |
| M5 Presentation | 待验收（自动化全绿） | 2026-09-11 23 项 PASS（EditMode 3 + PlayMode 20） | H1–H6 | 无 | 手动验收手感、观感、真机 60fps、中文渲染 |
| M6 Meta | 待验收（自动化全绿） | 2026-09-11 EditMode 31 项 PASS | H1–H5 | 无 | 手动验收上限/冷却/插屏/隐私 |
| M7 Bootstrap | 待验收（自动化全绿） | 2026-09-11 26 项 PASS（EditMode 8 + PlayMode 18） | H1–H4 | 无 | 手动验收首启流程、引导、真机 |

## 自动化测试汇总（2026-09-11）

| 套件 | 结果 | 命令 | 结果文件（已归档到仓库） |
|------|------|------|--------------------------|
| EditMode | PASS 97 / 97 | `Unity.exe -batchmode -nographics -projectPath <工程> -runTests -testPlatform editmode -testResults Logs\editmode-results.xml` | `Docs/evidence/editmode-results.xml`（原始输出 `E:\MergeWaterVerify\Logs\editmode-results.xml`） |
| PlayMode | PASS 66 / 66 | `... -testPlatform playmode -testResults Logs\playmode-verify.xml` | `Docs/evidence/playmode-results.xml`（原始输出 `E:\MergeWaterVerify\Logs\playmode-verify.xml`） |
| 合计 | **PASS 163 / 163** | 同上 | 同上 |

**验证环境说明（重要）**：以上运行发生在「由真实工程 `Assets` 同步出的隔离副本 `E:\MergeWaterVerify`」中。原因：真工程被运行中的 Unity 编辑器锁定（`Temp/UnityLockfile`），批处理模式无法打开同一工程；同时本会话的 Unity MCP 桥接连接的是另一个工程（LittleGunfight），无法驱动本工程。副本与真工程的源码、资产、`.meta`（GUID）一致，且在生成场景与配置资产后**重新从真工程同步并复跑通过**，因此结果可代表真工程；**真工程本体直跑尚待补**（关闭编辑器后或在 MCP 直连后执行）。详见 `Docs/evidence/README.md`。

## 真工程本体证据（不依赖 MCP，2026-09-11 取得）

| 检查 | 结果 |
|------|------|
| 真工程编辑器成功编译全部程序集 | 是：`Library/ScriptAssemblies/` 下 8 个运行时/编辑器 DLL + 2 个测试 DLL 均在（13:38–13:44）；无 `.cs` 文件新于 DLL |
| 编译错误 | 无（`Logs/AssetImportWorker*.log` 中无 `error CS`） |
| MergeWater 相关异常 | 无 |
| 场景引用可解析 | 是：`Main.unity` 的 140 处 `guid:` 引用中，抽查 `GameBootstrapper`/`HudBinder`/`GameBalance.asset`/`MetaSettings.asset`/`fruit_circle.png` 全部命中 |
| 构建场景 GUID 一致 | 是（`EditorBuildSettings.asset` 与 `Main.unity.meta` 相同） |

> 结论：真工程的代码编译、资产导入与场景接线均已验证可用；唯一未做的是在真工程内执行 Test Runner（需要编辑器不占用工程或 MCP 直连）。

## 已实现能力（对照 GDD）

- 核心循环：按住拖动选落点 → 松手投放 → 2D 物理堆叠 → 同级合成 → 连锁连击 → 越线失败 → 复活/结算 → 再开一局（R1–R7、R26）
- 数值与节奏：11 级水果表、投放等级池按投数分段、连击倍率封顶 2.0、阶段目标 200/500/1000（V1、V2.1–V2.7、V2.18）
- 四种道具：撤销/炸弹/锤子/摇一摇，含每日上限与领取编排（R11–R15、V2.13–V2.15、V2.17）
- 广告与合规：激励视频（复活/道具/摇一摇/大礼包）、插屏每 3 局 ≤1 且可开关、隐私门控、分享 60s 冷却（R16、R17、R19、R20、V2.12、V2.16、V2.19）
- 手感与表现：吸附 60ms、粒子、慢放 0.2×/0.15s、顿帧 80–120ms、震屏三档、连击音阶、警戒线脉冲、占位合成音（R22、V2.20–V2.25）
- 局外：本地存档与迁移、本地排行榜、设置面板、红点、新手引导、埋点（R18、R21、R23、R24、R25）
- 明确未做（范围外）：支付、底部 Banner/信息流/交叉推广、独立开始页、开放数据域好友排行与订阅召回（V1/V1.1）

## 生成物与入口

| 内容 | 路径 | 重建入口 |
|------|------|----------|
| 占位美术 | `Assets/Resources/Placeholder/`（fruit_circle、ui_arrow、ui_dot） | 菜单 `MergeWater/Generate Placeholder Art` |
| 配置资产 | `Assets/Config/GameBalance.asset`、`Assets/Config/MetaSettings.asset` | 菜单 `MergeWater/Generate Config Assets` |
| 主场景 | `Assets/Scenes/Main.unity`（已加入构建场景列表） | 菜单 `MergeWater/Build Main Scene`（会先跑前两项） |

## 待办

- [x] 需求文档与架构总览
- [x] 7 个模块架构文档与验收文档
- [x] 根指令文件 `AGENTS.md`
- [x] 程序集边界（8 个运行时/编辑器 + 2 个测试 asmdef）
- [x] 占位美术、配置资产与主场景生成工具
- [x] M1–M7 实现
- [x] 全量 EditMode + PlayMode 测试运行与证据记录（163/163）
- [x] 验收文档回填与任务索引校对
- [x] 真工程编译/资产/场景接线验证（不依赖 MCP，见上表）
- [ ] 真工程本体跑 Test Runner（需关闭锁定工程的编辑器，或用 MCP 直连本工程）
- [ ] 手动验收（手感、观感、真机 60fps、中文渲染），清单见各 `<0X>-*-test.md` 的「手动验收」表
- [ ] 清理临时验证目录 `E:\MergeWaterVerify`（清理前请确认 `Docs/evidence/` 已保留结果 XML）

## 阻塞

- 真工程本体直跑：受「工程被编辑器锁定 + MCP 未连接本工程」限制。不阻塞手动验收（可直接在编辑器里按清单核对）。

## 过程中发现并修复的实现缺陷（供回归参考）

| 缺陷 | 影响 | 修复 | 覆盖测试 |
|------|------|------|----------|
| `Ready` 阶段输入被 `CanAct`（仅 Playing）挡掉 | **首投无法进行，游戏不可玩** | 新增 `RoundSession.CanAcceptDrop`（Ready 或 Playing），`GameContext.SetInputBlocked` 与 `OnDropRequested` 改用 | `RoundSessionTests.StartRound_...`、`BootstrapFlowTests.SharedScene_OnAccept_RoundIsPlayableThroughRealAimPath` |
| `NewRound()` 未解绑上一局的 `IFieldPort.Merged` | 重开后新旧两局同时计分并重复写存档 | `NewRound` 先 `Session?.Dispose()` | `RoundLifecycleTests.NewRound_..._WithoutLeakingPreviousEvents` |
| 越线判定只看瞬时速度 | 刚生成的水果（速度为 0）会被误判为「静止越线」 | `FruitBody.SettledDuration` 连续静止计时 + `DangerSettleGraceSeconds` | `GameFieldDangerTests.FreshlySpawnedFruitAboveLine_IsNotReportedBeforeSettleGrace` |
| 复活广告可能被播放两次（Session 与 Economy 各播一次） | 重复激励视频、冷却记账错乱 | 职责拆分：M3 只做每局次数与状态应用（`ApplyRevive`），M6/M7 负责冷却与播放 | `RoundLifecycleTests.Revive_WhenAdCompletes_...`（断言 `RewardedShown == 1`） |

## 文档健康度

- [x] 已实现模块的公开契约与代码一致（含 `ISessionView`、`IFieldPort.HasFruitInRadius`、`IAdsService.IsShowing` 等实现中新增的契约，已回写架构文档）
- [x] 行为变化已更新对应验收文档（复活职责拆分、D9 字体、D10 反馈驱动、D11 领取即用均已记录）
- [x] 新任务关键词已加入 `00-overview.md` 唯一索引
- [x] 需求数值没有复制到其他权威位置（架构与验收仅引用编号）
- [x] 各模块验收文档的自动化项均已回填 PASS 与运行记录；未执行项按要求区分为「待手动验收」与「未验证」
