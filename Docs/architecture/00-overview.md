# MergeWater 架构总览

> 本文件是模块边界、依赖关系和任务路由的唯一索引。

## 技术与目标环境

- Unity：2022.3.62f3（LTS）
- 目标平台：微信小游戏优先（竖屏），抖音小游戏同构复用；Editor 内可完整游玩
- 渲染管线：URP 14.0.12（2D Renderer）
- 输入：旧版 Input（`Input.touches` / `Input.mousePosition`），单指拖动
- UI：uGUI + **TextMeshPro**（决策 **D16**）：文本一律用 TMP，字体为随包分发的 `ChineseUI SDF`（`Assets/Fonts/`，编辑期静态烘焙，运行时零字形生成）。原 legacy `Text` + 运行时系统字体的方案已废弃——微信小游戏（WebGL）无法访问系统字体，真机会显示方块。UI 对象全部预先存在于 `Main.unity`，运行时不生成界面；`HudBuilder` 在 Editor 程序集，运行时编译期不可引用
- 物理：Unity 2D 物理（Rigidbody2D + CircleCollider2D，CCD）
- 资源与配置：占位美术 `Assets/Resources/Placeholder/`（运行时兜底可 `Resources.Load`）、UI 美术 `Assets/UI/Art/`（**刻意不放 `Resources/`**——`Resources` 里的资源会被无条件打进包，而 UI 图只要被场景引用就会随包分发；2026-09-13 移出后首包少 4.9MB）、数值与开关 `Assets/Config/{GameBalance,MetaSettings}.asset`、中文字体 `Assets/Fonts/{TMPCharacters.txt,ChineseUI SDF.asset}`、主场景 `Assets/Scenes/Main.unity`；生成入口分别为菜单 `MergeWater/Generate Placeholder Art`、`MergeWater/Font/1·2`、`MergeWater/Generate Config Assets`、`MergeWater/Build Main Scene`。MVP 不引入 Addressables
- 自动化环境：Unity Test Framework 1.1.33；EditMode + PlayMode 各一个测试程序集。命令行（本机 Unity：`E:\Unity\Unity\2022.3.62f3\Editor\Unity.exe`）：`-batchmode -nographics -projectPath <工程> -runTests -testPlatform {editmode|playmode} -testResults <xml> -logFile <log>`。**注意**：批处理模式无法打开已被编辑器锁定的工程，真工程开着时需先关闭或在副本上运行
- 当前测试结论（2026-09-11）：**真工程本体** EditMode 103/103、PlayMode 74/74 全绿（先在隔离副本上运行，MCP 直连后复跑结果一致）；证据归档见 `Docs/evidence/`
- 第三方：DOTween（已存在于工程，MVP 运行时代码不依赖它，便于测试确定性）

## 模块清单

| 编号 | 模块 | 核心职责 | 明确不负责 | 架构文档 | 验收文档 |
|------|------|----------|------------|----------|----------|
| M1 | Core 契约与领域规则 | 水果等级/数值表、得分与连击规则、投放权重与队列、阶段里程碑、局内状态与事件契约、跨模块端口（`IFieldPort`/`IAimSource`） | 物理、输入、UI、存档、平台 SDK | `01-core.md` | `01-core-test.md` |
| M2 | Field 对局场地 | 水果实体的生成与销毁、2D 物理堆叠与合成解析、越线物理查询、道具对场地的直接作用（炸弹/锤子/撤销/摇一摇） | 计分与连击、状态机、广告与库存、HUD | `02-field.md` | `02-field-test.md` |
| M3 | Session 局内流程 | 局状态机、得分累计、连击计时、越线 1.2s 判负、复活流程编排、阶段目标发放、投放冷却与 next 队列 | 物理细节、具体 UI、平台服务 | `03-session.md` | `03-session-test.md` |
| M4 | Aim 输入与瞄准 | 指针输入 → 瞄准状态、落点 x 夹取、抛物线/反弹预演采样、道具瞄准模式、松手投放与目标指令 | 决定投放等级、消耗库存、物理生成 | `04-aim.md` | `04-aim-test.md` |
| M5 | Presentation 表现与反馈 | HUD 数据绑定、震屏、粒子、慢放与顿帧、飘字、连击音阶、警戒线脉冲、结算与设置面板、Toast | 规则与数值决策、存档写入、广告请求 | `05-presentation.md` | `05-presentation-test.md` |
| M6 | Meta 局外系统 | 本地存档与迁移、道具库存与每日上限、激励视频/插屏广告适配器与频次规则、本地排行榜、分享记录与冷却、隐私合规门、埋点 | 局内规则与物理、具体 UI 绘制 | `06-meta.md` | `06-meta-test.md` |
| M7 | Bootstrap 装配与引导 | 组合根与服务装配、初始化顺序、隐私流程门控、新手引导编排、两侧入口与道具使用编排 | 各模块内部实现 | `07-bootstrap.md` | `07-bootstrap-test.md` |

## 依赖图

箭头指向被依赖方：`A -> B` 表示 A 依赖 B。

```text
M2 Field ---------> M1 Core
M3 Session -------> M1 Core
M4 Aim -----------> M1 Core
M5 Presentation --> M1 Core（订阅 SessionEvents、通过 ISessionView 读取只读状态）
M6 Meta ----------> M1 Core（IAnalyticsService/IAdsService/ISaveStore 等接口定义在 M1）
M7 Bootstrap -----> M2, M3, M4, M5, M6, M1
M1 Core ----------> （仅 UnityEngine 少数适配类型；规则类不依赖 UnityEngine）
```

跨模块通信：M3 拥有一个 `SessionEvents` 实例（非静态总线）。M5 订阅该实例；M7 负责把各模块装配到一起并传引用。

## 任务索引

一条任务类型映射一个主要模块。模块文档再链接到所需需求章节和验收文档。

| 用户或开发者常用关键词 | 主要模块 | 读这个 |
|------------------------|----------|--------|
| 水果等级/半径/质量/得分表/投放权重/阶段里程碑/连击公式/倍率/数值调参 | M1 | `Docs/architecture/01-core.md` |
| 物理/堆叠/碰撞/合成/穿隧/抖动/CCD/限速/睡眠/警戒线检测/炸弹/锤子/撤销/摇一摇/水果生成 | M2 | `Docs/architecture/02-field.md` |
| 局状态机/开始/失败/结算/越线计时/1.2秒/复活/连击窗口/投放冷却/next队列/阶段目标 | M3 | `Docs/architecture/03-session.md` |
| 输入/拖动/瞄准/落点/预览线/抛物线/反弹预演/夹取/松手投放/道具瞄准 | M4 | `Docs/architecture/04-aim.md` |
| HUD/UI/顶栏/分数显示/进度条/震屏/顿帧/慢放/粒子/飘字/音效/BGM/警戒线脉冲/结算页/设置面板 | M5 | `Docs/architecture/05-presentation.md` |
| 存档/读档/版本迁移/最高分/道具库存/每日上限/激励视频/插屏/广告冷却/排行榜/分享/隐私政策/埋点/设置开关 | M6 | `Docs/architecture/06-meta.md` |
| 启动/初始化顺序/场景装配/组合根/新手引导/两侧入口/大礼包/红点/首启隐私弹窗 | M7 | `Docs/architecture/07-bootstrap.md` |
| 项目总览/模块依赖/任务归属 | 总览 | `Docs/architecture/00-overview.md` |

映射不唯一时不要猜测：列出候选模块和影响，请用户裁定后再修改。

## 跨模块契约

| 使用方 | 被依赖方 | 契约 | 失败或降级行为 |
|--------|----------|------|----------------|
| M3 | M2 | `IFieldPort`：`DropAt`/`LiveFruitCount`/`TryGetDangerViolation`/`RemoveFruit`/`RemoveHighestCluster`/`ApplyShake`/`Merged` 事件 | 场内无水果时返回安全空值；`DropAt` 生成失败返回 `false` 且不扣队列 |
| M3 | M1 | `SessionEvents`：`DropPerformed`/`Merged`/`Scored`/`ComboChanged`/`DangerStarted`/`DangerEnded`/`PhaseChanged`/`MilestoneReached`/`ItemUsed` | 无订阅者时事件为空调用，不影响流程 |
| M4 | M1 | `AimState`（`IsAiming`/`X`/`NormalizedX`）、`DropRequested(x)`、`ItemTargetRequested(kind, point)` | 未提供待投半径时使用默认半径；指针离开窗口时取消瞄准 |
| M5 | M1 | 只读 `ISessionView`（`RoundSnapshot` + `SessionEvents`） | 快照缺失字段显示占位符；无音频资源时静默降级 |
| M6 | M1 | `IAnalyticsService`/`IAdsService`/`ISaveStore`/`ILeaderboardService`/`IShareService`/`IClock` 接口 | 适配器不可用时使用内存/静默实现，游戏仍可完整游玩 |
| M7 | M6 | 广告位放行查询（复活 90s 冷却、每日上限、插屏频次） | 被拒绝时返回原因枚举，UI 显示可读文案；无广告适配器时复活直接放行（离线演示） |
| M7 | M4 | 道具瞄准模式：`BeginItemAim(kind)` / `CancelItemAim()` | 瞄准中取消不消耗道具 |
| M7 | M5 | 面板与 Toast 显示接口 | UI 缺失时仅记录日志，不阻塞流程 |

## 关键架构决策

| 编号 | 决策 | 影响模块 | 依据 |
|------|------|----------|------|
| A1 | 局内规则与时间判定放在不依赖 UnityEngine 的纯 C# 类中，物理与输入用薄适配器包住 | M1、M3、M4 | 决定 D3、D5 |
| A2 | 每个 Session 持有自己的 `SessionEvents`，不使用项目级静态事件总线 | M1、M3、M5、M7 | 架构参考「模块通信」 |
| A3 | M3 只通过 `IFieldPort` 访问物理场地，测试注入 Fake | M1、M3 | 决定 D5 |
| A4 | 平台能力（广告/分享/排行/埋点/存档）一律接口 + 可替换适配器，MVP 为测试实现 | M1、M6、M7 | 决定 D2 |
| A5 | 表现层只消费只读快照与事件，不改写业务状态 | M5、M3 | 架构参考「模块通信」 |
| A6 | 占位美术由编辑器工具程序化生成，资源接口按最终资源设计 | M2、M5 | 决定 D4 |
