# 微信小游戏发布工作流（MergeWater）

> 性质：**发布规划文档**。不定义需求与数值（数值唯一来源仍是 `Docs/requirements.md` + `GameBalance`），也不改动模块架构契约。
> 证据基线：2026-09-13 对**本工程**与**本机已安装的微信 SDK** 的实测（下方每条结论都标注了文件/行号或菜单名）。
> 政策类条目（备案、类目、版号、流量主门槛）会变动，标注 ⚠️ 的必须以微信公众平台后台**当日**要求为准。

---

## 0. 结论（三句话）

1. **不用换引擎**。工程已装 `com.qq.weixin.minigame`（`Packages/manifest.json` 指向 `minigame-tuanjie-transform-sdk`，落地在 `Library/PackageCache/com.qq.weixin.minigame@d288776c50`，`WXPluginVersion.pluginVersion = 202609030748`）。该 SDK 对非团结引擎走 **`BuildTarget.WebGL`** 分支（`Editor/WXConvertCore.cs:729/1289/1301`，团结专属能力都在 `TUANJIE_2022_3_OR_NEWER` 之后），所以 **Unity 2022.3.62f3 + 菜单 `微信小游戏 / 转换小游戏` 直接可用**。
2. **真正的发布工作量不在"导出"，而在 3 件事**：① 平台适配层从 Mock 换成真微信 API（广告/分享/存档/埋点）；② 包体与构建配置（**2026-09-13 已切回 Built-in RP，去掉「必须 WebGL2」这条硬约束**，见 V2.48）；③ 主体资质与备案。
3. **当前工程第一次点「转换」会直接失败**：`MiniGameConfig.asset` 的 `relativeDST` 为空，`WXConvertCore.PreCheck()` 会报「请先配置游戏导出路径」（`WXConvertCore.cs:231-235`）。这是第一个必须先填的字段。

---

## 1. 现状盘点

### 1.1 已经具备（无需重复劳动）

| 项 | 现状 | 证据 |
|----|------|------|
| 微信转换 SDK | 已装，插件版本 20260903（CHANGELOG v0.1.34） | `Library/PackageCache/com.qq.weixin.minigame@d288776c50` |
| 导出菜单 | `微信小游戏 / 转换小游戏`、`/ 转换小游戏试玩`、`/ 多包融合工具` | `Editor/WXEditorWindow.cs:10` 等 |
| 目标平台 SDK 开关 | 竖屏 `Orientation: 0`=Portrait；`assetLoadType: 0`=CDN | `WXEditorSettingHelper.cs:323/362` + `MiniGameConfig.asset` |
| WebGL 构建基础 | IL2CPP（`scriptingBackend: WebGL: 1`）、Wasm linker、压缩 Disabled、`stripEngineCode: 1` | `ProjectSettings/ProjectSettings.asset` |
| DOTween 裁剪 | 已为 WebGL 定义 `DOTWEEN_NOAUDIO/NOPHYSICS/NOPHYSICS2D/NOSPRITES` | `ProjectSettings.asset` 脚本定义 |
| 中文字体 | 静态烘焙 2048²/508 字/0 缺字，**运行时零字形生成**——正是 WebGL/小游戏唯一可行方案（D16） | `Assets/Fonts/ChineseUI SDF.asset` |
| 首包瘦身（第一轮） | UI 美术已移出 `Resources/`（−4.9MB） | `Docs/progress.md` 2026-09-13 |
| 合规话术铺垫 | 《健康游戏忠告》/适龄角标在加载页；隐私门控已实现（D 系列） | 需求 R20、`GameBootstrapper.privacy*` |
| 无诱导分享风险 | 分享**不发任何奖励**，只记 `lastShareUtcTicks` + 60s 冷却 | `Assets/Scripts/Meta/ShareService.cs:25-38` |
| 平台能力已接口化 | `IAdsService`/`IShareService`/`ISaveStore`/`IAnalyticsService`/`ILeaderboardService`/`IClock` | `Assets/Scripts/Core/Contracts/PlatformServices.cs` |
| 空实现与代理 | `NullAdsService` / `AdsServiceProxy` / `MockAdsService` | `Assets/Scripts/Meta/AdsAdapters.cs` |
| WX 类型不被裁剪 | `link.xml` 已就位（**不要删**） | `Assets/WX-WASM-SDK-V2/Runtime/Plugins/link.xml` |
| Node.js | 本机 `C:\Program Files\nodejs\node.exe` v26.7.0；SDK 只自带 `binaryen`（无 node.exe），可用 `CustomNodePath` 指定 | 实测 |

### 1.2 缺失（发布前必须补）

| 缺口 | 位置 | 影响 |
|------|------|------|
| `MiniGameConfig` 未配置：`Appid` / `CDN` / `relativeDST` 全空 | `Assets/WX-WASM-SDK-V2/Editor/MiniGameConfig.asset` | **转换直接失败** |
| 广告是测试位 | `GameContext.ApplyConsent()` 无条件 new `MockAdsService`（`GameContext.cs:107-124`） | 真机没有广告 → 无收入、复活/道具链路不可用 |
| 隐私门控是关的 | `GameBootstrapper.cs:32` `requirePrivacyConsent`（场景序列化值） | ⚠️ 未同意隐私即初始化广告/埋点 = 合规事故 |
| 隐私政策/用户协议/防沉迷是 Toast 占位 | `GameBootstrapper.cs:387-389` | 提审会被驳回 |
| 分享未拉真微信 | `ShareService` 只生成文案，平台层未调 `WX.ShareAppMessage` | 分享按钮在真机无反应 |
| 存档只写本地文件 + 只在 `OnApplicationPause(true)` 写 | `GameBootstrapper.cs:239-243`、`Meta.FileSaveStore` | 小游戏切后台丢档风险（见 §10.3） |
| uGUI 触摸未修正 | `WXTouchInputOverride` 需手动挂到 EventSystem（`[RequireComponent(typeof(StandaloneInputModule))]`） | 真机多点触控粘连 |
| `WX.InitSDK` 未调用 | 必须在使用任何 `WX.*` 前调用（`Runtime/WXBase.cs:55`） | 真机 WX API 全失效 |
| `MergeWater.Meta` 未引用 `Wx` 程序集 | `Assets/Scripts/Meta/MergeWater.Meta.asmdef`（asmdef 之间不自动引用） | 写适配器时编译不过 |
| 构建场景列表含 `SampleScene` | `ProjectSettings/EditorBuildSettings.asset`（`enabled: 1`） | `GetScenePaths()` 只取 enabled → 白进包（见 §10.1） |
| URP 需要 WebGL2 | `MiniGameConfig.CompileOptions.Webgl2: 0` | 不开则被设为 `{OpenGLES2}` → URP 渲染失败（见 §10.2） |
| `Assets/WX-WASM-SDK-V2/` 是插件硬编码路径 | `WXConvertCore.defaultImgSrc`、`WXEditorSettingHelper`「更多配置项」 | 不能移动/改名 |

---

## 2. 技术路线判定（先定这个，再动手）

| 路线 | 说明 | 对本工程的成本 | 建议 |
|------|------|----------------|------|
| **A. 留在 Unity 2022.3.62f3**（走 SDK 的 WebGL 分支） | 现有 SDK 已支持，改动最小，Editor 与真机同一套 Assets | 0 迁移 | ✅ **推荐**（作品集 Demo） |
| B. 迁到团结引擎 1.x | BuildProfile 面板、渲染线程（`enableRenderThread`）、`fbslim` 首包优化、WebAES/WebMD5 数据保护等**团结专属**能力（均在 `#if TUANJIE_*` 内） | 换编辑器 + 全量重测 201 项 + URP/材质复查 | 只有当"帧率/包体不达标且 A 路线已调无可调"时才考虑 |

**路线 A 下拿不到的团结专属能力（明确写清，避免期待落空）**：
`TUANJIE` 符号相关的 `WeixinMiniGame` 构建目标、`PlayerSettings.WeixinMiniGame.*`、多线程渲染、`fbslim`（`WXEditorSettingHelper.cs:397` 对 Unity 引擎自动禁用）、`Runtime/WebAES.cs`、`Runtime/WebMD5.cs`（文件首行 `#if WEIXINMINIGAME && TUANJIE_2022_3_OR_NEWER`）。

---

## 3. 阶段 A — 账号与合规（可与开发并行，最容易被低估）

> ⚠️ 全部条目以微信公众平台后台当日要求为准；下列是**要准备的东西**，不是政策结论。

| # | 事项 | 产出 | 阻塞关系 |
|---|------|------|----------|
| A1 | 注册**小游戏**账号（主体：个人 / 企业） | AppID | 阻塞 C1/D1（AppID 要填进 `MiniGameConfig`） |
| A2 | 主体资质：企业营业执照 或 个人实名 | 认证通过 | 阻塞 A3 |
| A3 | **小程序/小游戏备案**（工信部，2023-09 起强制） | 备案号 | ⚠️ **未备案无法发布**；周期长，**最先启动** |
| A4 | 类目选择（游戏类）+ 平台要求的资质材料 | 类目审核通过 | ⚠️ 是否需要**版号**取决于是否含内购/虚拟支付；纯免费 + 广告变现通常不需版号，但必须以后台结论为准 |
| A5 | 隐私：开发者后台填写《用户隐私保护指引》 | 审核通过 | 与 B3/B5 对应 |
| A6 | 广告：开通**流量主**（有门槛 ⚠️，历史门槛为累计独立访客数达标）后创建**激励视频**与**插屏**广告位 | `adUnitId` × N | 阻塞 B1 |
| A7 | 资源 CDN 域名（https + 备案域名；微信云开发亦可） | CDN 根 URL | 阻塞 C1（`assetLoadType: 0`=CDN 是默认值） |
| A8 | **微信开发者工具**（稳定版）安装 | 可导入 `minigame/` 目录 | 阻塞 D2 |
| A9 | 体验成员 / 测试号（可选，用于真机调试） | 成员列表 | 阻塞 E1 |

**本工程已有的合规素材**：加载页《健康游戏忠告》+ 适龄角标 + 隐私弹窗（R20）——文案与入口要与后台填写内容**保持一致**（后台写"不收集"而代码里有埋点 = 不一致，会被驳回）。

---

## 4. 阶段 B — 平台适配层（唯一的"写代码"阶段）

**架构约束（`AGENTS.md` 硬约束 4）**：调用方不得直接依赖具体适配器；所有平台能力走 `MergeWater.Core` 接口，实现放 M6，注入点在 M7 组合根。

### 4.1 前置：程序集引用

`Assets/Scripts/Meta/MergeWater.Meta.asmdef` 的 `references` 加 `"Wx"`（`Wx` 是 SDK 运行时程序集名，`includePlatforms: []` 即所有平台含 Editor，能编、且能被 EditMode 测试覆盖）。asmdef 之间**不会**自动引用，`autoReferenced: true` 只对预定义程序集生效——不加这一行，`using WeChatWASM;` 编译不过。

### 4.2 要新增的适配器（全部落在 `Assets/Scripts/Meta/`）

| 适配器 | 实现接口 | 关键 WX API（包内证据） | 注意 |
|--------|----------|--------------------------|------|
| `WXAdsService` | `IAdsService` | `WX.CreateRewardedVideoAd`（`WXBase.cs:316`） | `IsShowing` 要真实反映播放中（`GameContext.Update` 用它冻结输入）；`AdPlacement` → `adUnitId` 映射表 |
| `WXSaveStore` | `ISaveStore` | `WX.StorageSetStringSync`（`WXBase.cs:160`）/ `GetStringSync` | 见 §10.3；`FileSaveStore` 可在 Editor 保留 |
| `WXShareService` | `IShareService` | `WX.ShareAppMessage`（`WX.cs:3060`）+ `WX.OnShareAppMessage`（`WXBase.cs:252`，菜单右上角转发） | 现有 `ShareService` 已负责文案与 60s 冷却，**只加"拉起微信"这一层**，不要重复记账 |
| `WXAnalyticsService` | `IAnalyticsService` | `WX.ReportEvent` 自定义分析 | 必须在隐私同意后才 `Initialize()`（R20） |
| （可选）`WXLeaderboardService` | `ILeaderboardService` | 开放数据域（`Runtime/wechat-default/open-data/` 已就位） | MVP 明确范围外（V1/V1.1），可不做 |

全部适配器必须包在 `#if UNITY_WEBGL && !UNITY_EDITOR` 之类的条件编译里，或提供 Editor 降级实现，保证 **Editor 内仍可完整游玩**（`00-overview.md` 目标环境要求）。

### 4.3 关键改动点（精确到行）

| # | 文件 | 改什么 | 为什么 |
|---|------|--------|--------|
| B1 | `Assets/Scripts/Bootstrap/GameContext.cs:107-124` | `ApplyConsent()` 里的 `new Meta.MockAdsService()` 换成按平台选择真实适配器 | 现在**无条件**造 Mock；且 `if (Ads.Inner is Meta.MockAdsService) return;` 分支在真机是死代码 |
| B2 | `Assets/Scripts/Bootstrap/GameBootstrapper.cs:32` | `requirePrivacyConsent` 置 `true`（这是 `[SerializeField]`，**改代码默认值后必须重建场景**或直接在 `Main.unity` 上勾选） | 恢复 R20 隐私门控 |
| B3 | `Assets/Scripts/Bootstrap/GameBootstrapper.cs:387-389` | 三处 Toast 占位换成真实跳转：隐私政策/用户协议（小游戏内需可读全文）/ 实名与防沉迷入口 | 提审必备 |
| B4 | 新增 `WX.InitSDK` 引导 | 在**首场景最早**处调用一次（`WXBase.cs:55` 要求回调后再跑主逻辑）；建议放 M7 引导流程里，位于`BeginEntryFlow()` 之前 | 不调用则所有 WX API 无效 |
| B5 | `Assets/Scripts/Bootstrap/GameBootstrapper.cs:239-243` | `OnApplicationPause(true)` 之外补 `WX.OnHide` 兜底 + 同步存储 | 见 §10.3 |
| B6 | EventSystem 所在对象 | 挂 `WXTouchInputOverride`（SDK 自带，`[RequireComponent(typeof(StandaloneInputModule))]`） | Unity WebGL 多点触控粘连，uGUI 按钮会点错 |
| B7 | `MiniGameConfig.SDKOptions` | 按需评估 `UseCompressedTexture` / `PreloadWXFont`（本工程字体已自烘焙，**不需要**预加载系统字体）/ `disableMultiTouch` | 降低首包与内存 |
| B8 | 帧率 | 小游戏侧设置目标帧率；`GameBalance`/`TimeDirector` 不动（硬约束 5） | 见 E2 |

**文案改动提醒（硬约束 9）**：B3 一定会新增面向玩家的中文文案。改完**必须**重跑 `MergeWater/Font/1. 收集字符` + `MergeWater/Font/2. 烘焙中文 TMP 字体资产`，否则新字在 Static 模式下渲染为**空白**（`TMPFontAssetTests` 是门禁）。这条在发布阶段最容易忘。

---

## 5. 阶段 C — 构建与包体工程化

### C1 填 `MiniGameConfig`（`Assets/WX-WASM-SDK-V2/Editor/MiniGameConfig.asset`，或菜单 `微信小游戏 / 转换小游戏` 面板 →「更多配置项」）

| 字段 | 当前 | 目标 | 依据 |
|------|------|------|------|
| `ProjectConf.Appid` | 空 | A1 的 AppID（`wx` 开头） | 必填 |
| `ProjectConf.relativeDST` | **空** | 例如 `wx-export`（相对工程根，或绝对路径） | **为空则 PreCheck 直接失败** |
| `ProjectConf.CDN` | 空 | A7 的 CDN 根 URL | `assetLoadType: 0`=CDN |
| `ProjectConf.assetLoadType` | 0 (CDN) | 保持 0；若临时本地调试可改 1（小游戏包内），但**总包会超限** | `WXEditorSettingHelper.cs:362` |
| `ProjectConf.Orientation` | 0 | 保持 0 = Portrait（竖屏） | `WXEditorSettingHelper.cs:323` |
| `CompileOptions.Webgl2` | 0 | **可选**——2026-09-13 切 Built-in 后已不是硬要求（见 §10.2） | 勾 = ES3（性能更好，但依赖设备支持 WebGL2）；不勾 = ES2（兼容面更宽） |
| `CompileOptions.Il2CppOptimizeSize` | 1 | 保持 1（体积优先） | 已在 `WXConvertCore.cs:1201` 映射到 `OptimizeSize` |
| `CompileOptions.DevelopBuild` | 0 | 调试期可开，**提审必须关** | — |
| `ProjectConf.projectName` | 空 | 填 `MergeWater` | 导出工程名 |
| `ProjectConf.defaultReleaseSize` | 31457280 | 保持（30MB，即平台总包上限的量级） | 佐证包体预算 |

### C2 构建配置清理

1. **移出 `SampleScene`**：`ProjectSettings/EditorBuildSettings.asset` 里 `Assets/Scenes/SampleScene.unity` 的 `enabled: 1` 改为 0（或从列表删除）。`WXConvertCore.GetScenePaths()` 只收集 enabled 场景，它和它的资源会一起进包。菜单建议：`File > Build Settings`。
2. **Managed Stripping**：`ProjectSettings.asset` 的 `managedStrippingLevel` 没有 WebGL 条目（走默认）。建议显式设为 Medium/High，靠 `link.xml` 保住 `Wx` 类型。
3. **保持 `Assets/WX-WASM-SDK-V2/` 原地不动**（插件硬编码路径：`WXConvertCore.defaultImgSrc`、`WXEditorSettingHelper` 的 `AssetDatabase.LoadAssetAtPath`）。
4. **`Assets/WebGLTemplates/` 4 个副本**（`WXTemplate`、`WXTemplate2020`、`WXTemplate2022`、`WXTemplate2022TJ`）与包内模板**内容一致**（2026-09-13 实测 `WXTemplate2022/index.html` SHA256 相同）。插件对 Unity 设的是 `PROJECT:WXTemplate2022` 前缀，即**优先用 `Assets/` 下的副本**。⚠️ 因此**升级 SDK 时必须同步这 4 个副本**，否则会静默用旧模板（若要删副本让包内模板生效，需先单独验证，本次未验证）。

### C3 包体预算（本项目实测参考）

| 层 | 内容 | 现状 |
|----|------|------|
| 主包（平台限制 4MB ⚠️） | `game.js` / 框架 / 首屏 | 模板已把 `wasmcode`、`data-package` 拆成**分包**（`Runtime/wechat-default/game.json` 的 `subpackages` + `parallelPreloadSubpackages`），所以 wasm 与首包资源不占主包 |
| 首包资源（CDN） | `webgl.data`（本工程几乎只有占位图 + 字体 + 配置资产） | 已通过"UI 图移出 Resources"瘦身 −4.9MB |
| 总包（⚠️ 30MB 量级） | 全部分包之和 | 已由 `defaultReleaseSize: 31457280` 反映 |

**本项目最大的包体风险曾是 URP** —— 2026-09-13 已按 V2.48 **切回 Built-in RP** 解决（URP 会带进一整套 shader 变体与管线代码，而本工程一个 URP 特性都没用）。当时的核对结论留档：

1. 场景里的 3 处 shader 引用**全是内置 shader**（GUID `0000…f000…`）；当时还存在的 UI 生成器 `HudBuilder.CreateSpriteMaterial` 本来就是**优先** `Sprites/Default`、URP 只作备选（该生成器已于 2026-09-13 随 V2.51 删除）；
2. 工程内**只有 TMP 自带的两个示例材质**（`.mat`），没有任何项目材质依赖 URP shader；
3. URP 的**唯一**绑定点是 `GraphicsSettings.m_CustomRenderPipeline`（QualitySettings 各档全是 `{fileID: 0}`）—— 所以切换只需两处改动。

剩下可做的减重（按收益排序）：① 核对已用 UI 图的纹理压缩格式；② `Resources/` 继续只留运行时 `Load` 的东西（硬约束 10）；③ 按真机表现决定是否开 `UseCompressedTexture`。

---

## 6. 阶段 D — 导出与本地调试

### D1 导出

1. Unity 菜单 `微信小游戏 / 转换小游戏`（`WXEditorWindow.cs:10`）。
2. 面板填 AppID / CDN / 导出路径，点 **「生成并转换」**（= `WXConvertCore.DoExport(buildWebGL: true)`，先 Build WebGL 再转换）。
   - 已有 `webgl/` 产物想跳过重编时用 **「WebGL转小游戏(不常用)」**（`DoExport(false)`）。
3. 产物（相对 `relativeDST`）：

```
<relativeDST>/webgl/            # WebGL 中间产物（BuildPipeline.BuildPlayer 输出）
<relativeDST>/minigame/         # ✅ 微信开发者工具导入这个目录
    game.js  game.json  project.config.json
    unity-sdk/  wasmcode/  data-package/  images/  workers/  open-data/
```
（`WXConvertCore.cs:84-85` `webglDir`/`miniGameDir`）
4. 日志过滤 `[Builder]` / `[WX]`；失败时先看是不是 §10 的坑。

**CI 化（可选）**：`DoExport()` 是 public static，可在 Editor 脚本里调；生命周期钩子在 `WeChatWASM.LifeCycleEvent`（面板里「了解如何实现自定义构建」指向官方 QA 文档）。

### D2 微信开发者工具调试

1. 导入 `<relativeDST>/minigame`（AppID 已在 `project.config.json`）。
2. 模拟器跑通 → 检查控制台无 `WXSDKManagerHandler` 报错。
3. **真机调试**（扫码）→ 这一步才算真正"跑起来"。

### D3 本地自测清单（每次导出后跑）

- [ ] 加载页进度条会动、能点到「开始」
- [ ] 首投可玩（历史上 `Ready` 阶段被 `CanAct` 挡过，见 `Docs/progress.md`）
- [ ] 中文全部显示（**无空白字**；有新文案就是字体没重烘）
- [ ] 拖动手感、落点预测线到底
- [ ] 合成、连击飘字、粒子、震屏、慢放
- [ ] 越线失败 → 复活（真实激励视频）→ 结算 → 插屏频次
- [ ] 设置面板：开关 + 音量条填充跟随
- [ ] 隐私弹窗（B2 打开后）→ 拒绝/同意分支
- [ ] 切后台再回来：分数与道具库存没丢（§10.3）
- [ ] 分享按钮真的拉起微信

---

## 7. 阶段 E — 真机性能与体验验收（`05-presentation-test.md` 的「手动验收」表）

| # | 项 | 判据 |
|---|-----|------|
| E1 | iOS + Android 各 1 台真机跑通 | 无黑屏、无卡死 |
| E2 | 帧率 | 竖屏稳定 60fps（`WXPerfEngine` 可开性能面板：`CompileOptions.enableProfileStats` / `enablePerfAnalysis`，仅 Dev Build） |
| E3 | 触屏 | 单指拖动跟手；uGUI 按钮不误触（B6）；全面屏安全区、**右上角微信胶囊**不遮挡（已有 `HudLayoutTests.TopBar_DoesNotEnterWeChatCapsuleZone` 门禁，真机复核） |
| E4 | iOS 内存 | 不 OOM（`ProjectConf.MemorySize: 256` 会通过 emscriptenArgs 设 `TOTAL_MEMORY`，`WXConvertCore.cs:1163-1177`） |
| E5 | 冷启动时间 | 首屏可接受（`LoadingMinSeconds` 与 CDN 命中率相关） |
| E6 | 断网/弱网 | 有可读提示，不白屏 |
| E7 | 音频 | 首次触摸后能出声（小游戏音频需用户交互解锁） |

---

## 8. 阶段 F — 提审与上线

1. 开发者工具 **「上传」** → 后台设为**体验版** → 内部/体验成员验证。
2. 提交审核：填版本说明、类目、隐私指引、测试账号（如无登录则说明）。
3. 审核通过 → **发布**（可灰度）。
4. 上线后：看后台留存/时长/广告 eCPM；用 `AnalyticsService` 的事件（`AnalyticsEventNames`）对齐自建埋点。
5. 版本更新：`ProjectSettings` 的 `bundleVersion` 要跟着升（设置面板显示了 `Application.version`，`GameBootstrapper.cs:118`）。
6. ⚠️ 小游戏有「**首次进入需展示适龄提示**」「实名认证/防沉迷」等平台要求，按后台当日清单逐条核对（本工程适龄角标已在加载页，实名入口目前是 Toast 占位 → B3）。

---

## 9. 抖音小游戏复用（目标环境写了"同构复用"）

- 复用面：**全部业务代码与场景**，因为平台能力已接口化（架构决策 A4）。预期只需**再写一套适配器**（`TTAdsService`/`TTShareService`/`TTSaveStore`/`TTAnalyticsService`）+ 一个 `BuildProfile`/转换插件（抖音侧的 Unity 转换插件，接口名不同：`tt.createRewardedVideoAd`、`tt.shareAppMessage`、`tt.setStorageSync`）。
- 不可复用面：微信专有的开放数据域、胶囊避让、`WX-WASM-SDK-V2` 目录、`MiniGameConfig`。
- **建议**：先把微信这一套跑通并提审，抖音作为第二阶段；两套适配器并存时，注入点（B1）应改成"按平台选工厂"，而不是 `if/else` 堆在 `GameContext` 里。

---

## 10. 风险与坑（本项目实测，按"会不会真的踩"排序）

### 10.1 `SampleScene` 会白进包（高）
`EditorBuildSettings.asset` 里两个场景都是 `enabled: 1`；`WXConvertCore.GetScenePaths()` 只收集 enabled 场景。Unity 默认的 SampleScene 与它引用的资源会一起打进包体预算。**发布前第一件事就是移出。**

### 10.2 ~~URP 必须开 WebGL2~~ → **已作废**（2026-09-13 切回 Built-in RP，V2.48）

原风险：`MiniGameConfig.CompileOptions.Webgl2: 0` 时 `WXConvertCore.cs:776-783` 会把 WebGL 图形 API 设成 `{OpenGLES2}`，而 URP 需要 ES3/WebGL2。

**现已不适用**：工程已把渲染管线切回 **Built-in RP**（`GraphicsSettings` 不再绑定 URP —— 那是 URP 在本项目里唯一的绑定点）。Built-in 在 ES2 下即可正常工作，因此 `WebGL2.0` 从**硬要求**变成**可选优化**：想要更好的真机性能可以勾（代价是依赖设备支持 WebGL2，iOS 需 15+），追求最大兼容面就保持默认不勾。

### 10.3 存档可能丢（中高）
- 现状：`Meta.FileSaveStore` 写 `persistentDataPath`（WebGL 下由 SDK 的 Emscripten 文件系统落到微信本地存储），而**写盘时机只有 `OnApplicationPause(true)`**（`GameBootstrapper.cs:239-243`）。
- 风险：小游戏切后台是否必然触发 `OnApplicationPause` **未验证**；且同步存储才有保证。
- 做法：① 真机验证切后台/杀进程两种路径；② 补 `WX.OnHide` 兜底写盘；③ 关键数据（最高分、道具库存、每日计数、设置）改用 `WX.StorageSetStringSync` 的 `WXSaveStore`（`ISaveStore` 接口不变，`SaveService` 已有版本迁移，改存储不动上层）。

### 10.4 隐私门控与后台声明不一致（高，但确定性高、好修）
`requirePrivacyConsent` 现在是**关闭**的，且 `GameContext.ApplyConsent()` 会立刻初始化埋点。发布形态下这是"未获同意即处理数据"。B2 + A5 必须同时做完。

### 10.5 新文案导致缺字（中，最容易忘）
硬约束 9：任何面向玩家的文案变更 → 重跑 `MergeWater/Font/1·2`。B3 一定会改文案。

### 10.6 `Assets/WebGLTemplates/` 副本与 SDK 版本漂移（中，长期）
见 C2-4。当前一致，但升级 SDK 后不会自动更新。

### 10.7 Node.js 版本（低）
SDK 的 `Editor/Node` 只带 `binaryen`，需系统 Node。本机 v26.7.0 属于很新的版本；若转换阶段报 Node 相关错误，用 `CompileOptions.CustomNodePath` 指向 Node 18/20 LTS。

### 10.8 小程序包体限制是**平台侧**限制（中）
4MB 主包 / 30MB 总包一类数字会调整，⚠️ 以微信后台与开发者工具的实际报错为准；`defaultReleaseSize` 只是 SDK 的默认值。

### 10.9 无需担心的项（已符合）
- 中文字体：静态 TMP 烘焙，**运行时零字形生成**——小游戏无系统字体，方案正确（D16）。
- 分享：不承诺奖励，无诱导分享风险（`ShareService.cs`）。
- 无独立开始页、无支付、无底部 Banner：范围外，少一堆合规与接口工作。
- `link.xml` 已在，`Wx` 类型不会被 strip。

---

## 11. 排期建议（按依赖关系）

| 顺序 | 工作 | 阻塞于 | 可并行 |
|------|------|--------|--------|
| 1 | A3 备案 + A1 AppID + A6 流量主 | — | ✅ 立刻启动（周期最长） |
| 2 | C2 构建清理（SampleScene / stripping）+ C1 填 `MiniGameConfig`（先填 DST，AppID 后补） | 1(部分) | ✅ |
| 3 | D1 首次导出（AppID 可先用测试号）拿到 `minigame/`，**打通链路** | 2 | — |
| 4 | B4 `WX.InitSDK` + B6 `WXTouchInputOverride` + B5 存档兜底 | 3 | ✅ 与 5 并行 |
| 5 | B1/B2/B3 组合根与合规入口 + B（适配器） | 1(广告位) | ✅ |
| 6 | 文案变更 → **重烘字体** → 跑 EditMode/PlayMode | 5 | — |
| 7 | E 真机调优（帧率/内存/包体） | 6 | — |
| 8 | F 提审 | 全部 | — |
| 9 | 抖音复用 | 8 | — |

---

## 12. 需要你（需求方）提供或决策的信息

1. **主体与资质**：个人还是企业主体？是否已注册小游戏账号/是否已有备案？
2. **变现**：只做广告（激励视频+插屏），还是将来要内购？→ 决定是否需要版号。
3. **CDN**：用微信云开发还是自备已备案域名？
4. **技术路线**：确认走 §2 的路线 A（留 Unity 2022.3.62f3）。
5. **范围**：微信小游戏是否就是终态（抖音是否本期要做）？
6. **正式美术**：`Docs/progress.md` 里「正式美术替换」仍是未完成项——发布前是否要替换？（会同时触发包体与字体（若含美术字）的复查）

---

## 附：一条命令/一个菜单速查

| 目标 | 入口 |
|------|------|
| 导出微信小游戏 | 菜单 `微信小游戏 / 转换小游戏` →「生成并转换」 |
| 只转换不重编 | 同面板 →「WebGL转小游戏(不常用)」 |
| 小游戏配置 | 同面板 →「更多配置项」（或 `Assets/WX-WASM-SDK-V2/Editor/MiniGameConfig.asset`） |
| 分包融合 | 菜单 `微信小游戏 / 多包融合工具` |
| 构建场景列表 | `File > Build Settings`（**移除 SampleScene**） |
| 重新生成配置资产 | 菜单 `MergeWater/Generate Config Assets`（改过 `GameBalance` 默认值后必跑） |
| 字体收集 + 烘焙 | 菜单 `MergeWater/Font/1. 收集字符` → `/2. 烘焙中文 TMP 字体资产` |
| 场景与 UI 改动 | **在编辑器里手工改 `Assets/Scenes/Main.unity` 并保存**（2026-09-13 V2.51 起无生成器：原 `Rebuild UI In Open Scene` / `Build Main Scene` 菜单已删除） |
| EditMode 测试 | `Unity.exe -batchmode -nographics -projectPath <工程> -runTests -testPlatform editmode -testResults Logs/editmode-results.xml -logFile Logs/editmode.log` |
| PlayMode 测试 | 同上，`-testPlatform playmode` |
