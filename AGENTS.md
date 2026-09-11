# 项目概述

《合成果园》MergeWater —— 竖屏 2D 投放堆叠合成游戏（合成大西瓜 like）的 MVP 作品集 Demo。

- Unity：2022.3.62f3（LTS），URP 14.0.12（2D Renderer）
- 目标平台：微信小游戏优先（竖屏），抖音小游戏同构复用；Editor 内可完整游玩
- 需求文档：`Docs/requirements.md`
- 架构与任务索引：`Docs/architecture/00-overview.md`
- 策划原始案：`Docs/2026-09-11-merge-watermelon-gdd.md`

# 项目硬约束

1. 业务规则不得依赖具体 UI、音效、相机或平台 SDK 类型。表现层（M5）只消费只读快照与事件，不改写业务状态；规则层（M1/M3）通过接口访问物理与平台能力。
2. 数值只在 `Docs/requirements.md` 定义一次。代码中的单一来源是 `GameBalance`（`Default` 为权威默认值），其他位置只能引用，不得复制成第二份权威数值。
3. 不使用项目级静态事件总线。局内事件由 M3 的 `SessionEvents` 实例承载，M5 订阅、M7 传递引用；每局重建并重新订阅。
4. 任何平台能力（广告/分享/排行/埋点/存档）必须走 `Core` 中的接口，MVP 使用测试/内存实现；调用方不得直接依赖具体适配器。
5. 修改 `timeScale` 只能通过 M5 的 `TimeDirector`；冻结物理只能通过 M2 的 `IFieldPort.SetSimulationEnabled`。其他模块不得自行改动。
6. 引入新资源或第三方库前先确认必要性并更新 `00-overview.md` 的「技术与目标环境」。MVP 运行时代码不依赖 DOTween。
7. 美术资源在 MVP 阶段使用程序化占位符（`Assets/Resources/Placeholder/`，需放在 Resources 下以便运行时兜底也能加载），但资源接口与引用方式按最终资源设计，替换时不需要改代码结构。生成入口：菜单 `MergeWater/Generate Placeholder Art`。

ScriptableObject、事件、状态模式、对象池等是按场景选择的架构手段，除非本项目在此明确指定，否则不是默认硬规则。

# 文档访问

1. 从 `Docs/architecture/00-overview.md` 的唯一索引定位主要模块。
2. 声明本任务的最小必要文档集后继续；只有映射不唯一、缺失信息造成真实歧义或影响范围不清楚时才等待确认。资料缺失但行为可唯一确定时，报告缺口并补齐文档。
3. 默认读取当前模块架构、其引用的需求章节和对应验收文档。
4. 只有存在显式依赖或回归影响时，才读取其他模块的对外契约或验收章节。
5. 不通读整个 `Docs/`，也不拒绝读取完成任务所必需的已声明依赖。
6. 需求、架构、代码或测试证据冲突时，列出差异和影响，暂停有歧义的修改；裁定后同步文档。

# 实现与测试流程

1. 运行并记录相关自动化测试基线；已有失败必须确认为本次问题复现，或与本次改动无关且可明确隔离。
2. 为新行为或 Bug 编写最小测试，并确认它因预期原因失败。
3. 编写最小实现使目标测试通过。
4. 运行目标测试和受影响的回归套件。
5. 更新实际受影响的架构契约、验收文档、任务索引和进度。
6. 交付执行过的命令、结果、未验证项和手动验收步骤。

主观体验无法合理自动化时，先把操作和可观察结果写入验收文档，再实现和手动验证。

# 测试策略

- EditMode：纯规则与数值、存档迁移、广告频次与冷却、场景与配置资产校验、无需进入 Play 的行为。
- PlayMode：物理堆叠与合成、输入集成、MonoBehaviour 生命周期、场景装配、启动与重开局流程。
- 手动验收：手感、观感、可读性、真机帧率与触屏体验（见各模块 `<0X>-*-test.md` 的「手动验收」表）。
- 本地命令（本机 Unity 路径：`E:\Unity\Unity\2022.3.62f3\Editor\Unity.exe`）：
  ```powershell
  $Unity = "E:\Unity\Unity\2022.3.62f3\Editor\Unity.exe"
  & $Unity -batchmode -nographics -projectPath $ProjectPath -runTests -testPlatform editmode -testResults Logs/editmode-results.xml -logFile Logs/editmode.log
  & $Unity -batchmode -nographics -projectPath $ProjectPath -runTests -testPlatform playmode -testResults Logs/playmode-results.xml -logFile Logs/playmode.log
  ```
  注意：批处理模式无法打开已被编辑器锁定的工程；若真工程正开着，请先关闭或在副本工程上运行。
- 编辑器内：`Window > General > Test Runner`，按程序集过滤 `MergeWater.Tests.EditMode` / `MergeWater.Tests.PlayMode`。
- 结果位置：`Logs/*-results.xml` 与 `Logs/*.log`；不只看日志末行，同时检查进程退出码、结果 XML 的失败数与编译错误。

# 完成与验收

AI 可以报告实现及已运行的自动化验证完成，但必须分别列出：

- 已验证：实际执行且通过；
- 不适用：经说明理由后无需执行的检查；
- 待手动验收：需要用户在 Unity 或目标设备确认；
- 未验证：本应执行但受环境、资源或权限阻塞。

所有适用自动化验证和手动验收均完成后，才能把模块标为「已验收」。

# 代码与仓库约定

- 命名空间与程序集：`MergeWater.Core` / `.Field` / `.Session` / `.Aim` / `.Presentation` / `.Meta` / `.Bootstrap` / `.Editor`；每个模块一个 `.asmdef`，目录与程序集同名。测试程序集 `MergeWater.Tests.EditMode` / `MergeWater.Tests.PlayMode`。
- 目录和序列化：运行时代码放 `Assets/Scripts/<Module>/`，测试放 `Assets/Tests/{EditMode,PlayMode}/<Module>/`，编辑器工具放 `Assets/Scripts/Editor/`，占位美术放 `Assets/Resources/Placeholder/`，配置资产放 `Assets/Config/`，主场景 `Assets/Scenes/Main.unity`（由 `MergeWater/Build Main Scene` 生成）。
- 序列化字段使用 `[SerializeField] private`；公开只读状态用属性；不用 public 字段暴露可变状态。
- 日志与错误处理：运行时报错用 `Debug.LogError` 且必须可降级；预期内的拒绝（库存不足、广告冷却）用返回值枚举表达并记 `Debug.Log`，不抛异常。
- 提交格式：`feat(<module>): 描述` / `fix(<module>): 描述` / `docs(<area>): 描述` / `test(<module>): 描述`；功能连同测试一起提交。
