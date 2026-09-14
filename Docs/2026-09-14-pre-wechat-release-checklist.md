# 打包微信小程序前的检查清单（2026-09-14）

> 本文件是**交接清单**：左边是我（AI）已经做完并验证过的，中间是你必须在微信后台/导出面板做的，
> 右边是导出到真机后建议立刻验证的。最后两节记录**已知取舍**与**我没做的可选优化**（附理由）。

## 一、我已经做完并有证据的

| 项 | 结论 / 证据 |
|---|---|
| 自动化测试 | **EditMode 160/160、PlayMode 85/85 全绿**（批处理实测，见 `Docs/progress.md`） |
| WebGL 专属代码编译 | 新增 `MergeWater/Check WebGL Build`（`Assets/Scripts/Editor/WebGlBuildCheck.cs`）：真构建一次 WebGL，把编辑器**永不编译**的 `#if UNITY_WEBGL` 分支编译一遍——`SafeAreaSources` 微信分支与 `WeChatAdBridge`（`WeChatWASM.WX` 只在运行时 DLL）。2026-09-13 的 CS1503 就是这样漏到导出才炸的 |
| 渲染批处理 | UI Sprite Atlas 已启用：开局 Batches **12 → 3**、堆满星球 **20 → 15**；星球图集刻意未启用（+2.6MB 包体换约 8 个 batch，实测帧时 3.4ms 未饱和，不值） |
| 帧率 | WebGL / 小游戏**显式锁 60 帧**（`GameBootstrapper.Awake`，编辑器不生效便于调试） |
| 中文字体 | 静态烘焙 `ChineseUI SDF.asset`（2048² 单图集，492→464 字收集、必备文案 0 缺字），运行时零字形生成 |
| 包体卫生 | `Resources/` 只放运行时 `Resources.Load` 的东西（16 个文件 3.27MB：星球 10 张 + 占位图）；UI 美术在 `Assets/UI/Art`、素材包在 `Assets/Art/kenney_planets`（刻意不进包）；`.gitignore` 已覆盖 `[Bb]uilds/` |
| 输入 / 广告 | 广告播放期输入冻结、遮罩、重入保护、销毁兜底；Mock/真微信一键切换（`adsMode`），六入口失败文案统一 |
| 玩法现状 | 道具只剩**清屏**与**摇一摇**；排行榜、大礼包、炸弹、锤子已按需求删除（V2.58） |

### WebGL 构建自检结果（2026-09-14，`MergeWater/Check WebGL Build`，本地实测）

```
✅ 构建成功：总计 48.22 MB（未压缩），耗时 135 秒
   WebGLCheck.wasm        29,411 KB   ← 未压缩；Brotli 后约 6.7MB（与之前导出报告的 wasmcode 6.73MB 一致）
   WebGLCheck.data        13,159 KB   ← 数据包（贴图/字体/音频等）
   WebGLCheck.symbols.json 6,265 KB   ← 调试符号（**不该进包**，见下面第 1.5 条）
   framework.js 495 KB + loader/index 等
```

**这一步的真正价值**：它是**唯一**能编译 `#if UNITY_WEBGL && !UNITY_EDITOR` 分支的手段——
`SafeAreaSources` 的微信安全区分支与 `WeChatAdBridge.CreateRewardedVideo`（`WeChatWASM.WX` 只在运行时 DLL）。
本次构建通过 ⇒ 这两段在真实导出时不会出现编译错误（2026-09-13 的 CS1503 就是漏在这里）。

**新发现（值得你在导出后核对一次）**：产物里的符号文件叫 **`WebGLCheck.symbols.json`**，
而插件默认忽略规则写的是 `.symbols.unityweb` / `.symbols.unityweb.br` —— 名字对不上时符号文件会**进包**（本机测到 6.2MB）。
导出后请在 `minigame/project.config.json` 的 `packOptions.ignore` 里确认实际文件名（含 `.br` 变体）。

**但这条建议是有条件的（2026-09-14 修正）**：插件源码 `WXConvertCore.cs:1647` 明确写着「**代码分包需要 symbol 文件以进行增量更新**」——
也就是说符号文件是 **wasm 代码分包**能力的一部分。取舍关系是：

| 你的选择 | 符号文件 | 结果 |
|---|---|---|
| 启用 **wasm 代码分包**（微信侧能力，配合 CDN 使用） | **必须保留** | 支持增量更新；包体多约 6MB |
| 不启用代码分包 | 排除它（`packOptions.ignore`） | 省约 6MB 包体 |

两者不可兼得，按需选一个；不要不加区分地"一律排除"。
## 二、必须你在微信后台 / 导出面板做的（我做不了）

1. **首包体积（最重要）**：现在 `assetLoadType = 1`，导致 `data-package`（约 19.7MB）挤在包内，
   首包 0.88MB 只是假象。请配置 **CDN 或云开发托管**，并把 `assetLoadType` 改回 `0`。
   步骤见 `Docs/2026-09-13-wechat-minigame-release-workflow.md` 的 A7。
2. 导出面板确认：**UnityHeap / 内存**（建议 ≥256MB；当前 `ProjectSettings` 里是初始 32MB / 上限 2048MB，
   插件的转换面板会覆盖它，以面板里的值为准）、调试符号按需关闭（符号文件很占体积）。
3. 提审前：基础库版本、隐私协议、类目资质、体验版二维码。

## 三、导出到真机后建议立刻验证的

| 项 | 怎么看 |
|---|---|
| 安全区 | 刘海 / 灵动岛机型：顶栏没有被微信右上角胶囊遮挡（`SafeAreaSources` 的 WX 分支） |
| 音频 | **首次交互后** BGM 与音效正常（WebGL 需要用户手势解锁音频）；设置里关音乐再开音乐应从头发声（V2.54） |
| 设置面板 | 拖音量条**不会**把开关取消勾选、不会重播 BGM（V2.53/V2.54 的两个缺陷） |
| 广告 | 把 `GameRoot → GameBootstrapper` 的 `adsMode` 切 `WeChat` + 填 `adUnitId` 后：复活路径按 `isEnded` 发奖；中途关闭不发奖（当前默认 Mock，没有广告位） |
| 分享 | 回调不可信，只按「发起即记录 + 60s 冷却」；文案不含「必得」承诺 |
| 内存 / 稳定性 | 连玩 3–5 局、反复开关面板与复活，观察是否稳定（WebGL 内存按几何增长，上限 2048MB） |
| 帧率 | 真机帧率与发热；本作渲染很轻（20 batch / 1.3k Tris），若掉帧更可能是物理或 JS 侧 |

## 四、已知取舍（是决策，不是缺陷）

- **插屏未接真广告**：真机上 `ShowInterstitial` 返回 `Unavailable`（MVP 只接激励视频）。
- **排行榜已删**：原为本地排行榜（含红点与埋点名），V2.58 整条移除。
- **大礼包已删**：其按钮**从未接线**（`GameContext.RequestGift` 零调用方），一直是个死按钮。
- **炸弹 / 锤子已删**：原实现**没有半径 / 命中预览**，玩家看不到"会打到哪"，体验不完整。
- **阶段目标奖励**改为 200/500→清屏、1000→摇一摇（原 500→炸弹、1000→锤子已随道具删除）。

## 五、优化记录（2026-09-14，V2.61，全部为本机实测）

| 优化 | 改动 | 实测收益 |
|---|---|---|
| **托管代码剥离** | WebGL `managedStrippingLevel`：Low → **Medium**（菜单 `MergeWater/Apply WebGL Release Settings`，脚本 `Assets/Scripts/Editor/WebGlReleaseSettings.cs`）；反射依赖的第三方程序集由 **`Assets/link.xml`** 保留（`WeChatWASM` / `wx-runtime`） | **wasm 29,411 KB → 24,889 KB（−4.5 MB）** |
| **BGM 导入设置** | `our_expanse_-_with_tail-version-.mp3`：`DecompressOnLoad` → **`CompressedInMemory`**（内存里不再解成 PCM）、Vorbis 质量 **100% → 60%** | **data 13,159 KB → 11,357 KB（−1.8 MB）**，运行时内存同步下降 |
| 调试符号 | 剥离后符号表同步缩小 | `symbols.json` 6,265 KB → **5,126 KB（−1.1 MB）**（符号本不该进包，见第二节第 1.5 条） |
| **WebGL 内存** | 初始 **32 MB → 256 MB**，上限保持 2048 MB | 不再运行时反复扩容（2048² 字体图集 + 2048² 精灵图集 + 10 张星球贴图 + BGM，32 MB 初始值不现实） |
| **死资源清理** | 删除零引用的 `Assets/Audios/Out in Space.ogg`（2.32 MB，全仓 GUID 引用数 = 0） | 不进包，属仓库卫生。恢复：`git checkout 0d9b91e -- "Assets/Audios/Out in Space.ogg" "Assets/Audios/Out in Space.ogg.meta"` |

**合计：WebGL 构建 48.22 MB → 40.92 MB（−7.30 MB，−15.1%）**；同期 `EditMode 160/160`、`PlayMode 85/85` 保持全绿。
（Brotli 压缩后按比例推算：wasm 约 6.7 MB → **约 5.7 MB**，即总包再省约 1 MB。）

**回滚办法**
- 剥离级别 / 内存：把 `WebGlReleaseSettings` 里的 Medium 改回 `Low`、`TargetInitialMemoryMb` 改回 32 再跑一次（脚本会打印改动前后值）。
- 音频：`loadType` 改回 `0`、`quality` 改回 `1`。

**待真机验证（剥离的固有风险，必须做）**：Medium 剥离靠 `link.xml` 保护反射路径，但**一定要在体验版上把设置面板、存档、分享、广告、复活各走一遍**；若出现 `MissingMethodException` / `TypeLoadException`，按上面的回滚办法改回 Low 即可。

## 六、仍未做的优化（附理由）

| 项 | 预期收益 | 为什么没做 |
|---|---|---|
| 星球贴图移出 `Resources/` | 首包 −3.27 MB | 与硬约束 10 的既有设计冲突（`FruitArt.LoadPlanetSprites` 按命名约定 `Resources.Load`），且真正的首包方案是 CDN（第二节第 1 条），收益重叠、改动面大 |
| `wx.setKeepScreenOn`（防熄屏） | 体验 | 属"只在 WebGL 编译"的新代码，需要一次真实导出才能验证；建议放到下次迭代 |
| 关闭调试符号 | 子包 −6 MB | 是否产出符号由微信插件的导出流程决定（V2.48 实测：插件会强制 External），改工程设置无效——请在插件的导出面板或 `packOptions.ignore` 里处理（第二节第 1.5 条） |

## 七、已知的编辑器噪声（不影响构建产物）

| 现象 | 成因（有证据） | 处理 |
|---|---|---|
| Console 报 `Some objects were not cleaned up when closing the scene. ... WXSDKManagerHandler` | **微信插件自己造成的**：Editor.log 里该警告紧跟着 `WXTouchInputOverride.OnDisable() → UnregisterWechatTouchEvents() → WXBase.cs:990`——插件在 `OnDisable` 里去取 SDK 单例，而该单例的 `Instance` 是懒加载的（`wx-runtime-editor.dll` 里既有 `WeChatWASM.WXSDKManagerHandler` 类型，也有一次 `DontDestroyOnLoad`），于是在关场景过程中新建了一个跨场景对象。**本工程代码从不引用该类型**（全仓只有 WebGL 模板的 JS 会 `SendMessage('WXSDKManagerHandler', ...)`） | 新增 `Assets/Scripts/Editor/WeChatSdkLeakCleaner.cs`：只在**非播放状态**下清理它（播放中不动，避免干扰运行中的游戏；插件下次需要会自行重建），清理时打一行日志说明成因 |

> 判断依据：警告出现的位置与插件调用栈在同一段日志里相连，且我们的代码对 `WeChatSDKManagerHandler` 零引用。

## 八、微信导出面板开关对照表（2026-09-14，从插件源码逐个核对）

真机性能面板给出的两条建议（「未使用 wasm 代码分包」「未使用预下载能力」）都是**开微信侧能力**，不是改代码。
插件导出面板里对应的项（键名为 `WXEditorSettingHelper.cs` 中的实际字段名）：

| 面板项（键名） | 中文标签 | 说明 |
|---|---|---|
| `assetLoadType` | 首包资源加载方式 | **CDN(0) / 小游戏包内(1)** ← 主开关；当前为「小游戏包内」 |
| `cdn` | 游戏资源CDN | 切 CDN 模式时必须填写 |
| `preloadFiles` | **预下载文件列表** | **直接对应「未使用预下载能力」**；`;` 分隔、支持模糊匹配，插件会写进 `game.json` 的 `parallelPreloadSubpackages` |
| `compressDataPackage` | 压缩首包资源 | Brotli 压缩首包资源（官方提示：首次启动可能 +200ms，推荐与分包加载配合） |
| `fbslim` | 首包资源优化 | 导出时清理 Unity 默认打包但游戏未使用的资源（团结引擎已内置，无需开启） |
| `brotliMT` | brotli多线程压缩 | 出包更快、压缩率更低；提示语：「**如若不使用 wasm 代码分包请勿用多线程出包上线**」 |
| `showMonitorSuggestModal` | 显示优化建议弹窗 | 就是真机上那个「优化建议」弹窗的开关 |
| `enableProfileStats` / `enablePerfAnalysis` | 显示性能面板 / 集成性能分析工具 | 真机性能面板与 CPU Profile 相关开关 |

### 建议的开启顺序（避免白屏）

1. 微信后台开通 **CDN 或云开发托管** → `assetLoadType` 切 **CDN(0)** + 填 `cdn` 地址 → **单独真机验证一次能正常进游戏**（CDN 配错会直接白屏）
2. 填 `preloadFiles`（先看 `minigame/` 里资源包的真实文件名再填）
3. 再跑一次真机性能面板 → 上述两条建议应消失；按需打开 `compressDataPackage`（省包体、首启略慢）与 `fbslim`

> 注意与第七节的符号文件取舍联动：启用 wasm 代码分包 与 省掉 6MB 符号文件**不可兼得**。