# MergeWater 缺陷交接报告

> 日期：2026-09-11
> 工程：`E:\Unity\Projects\DemoProjects\MergeWater`（Unity 2022.3.62f3 + URP 14.0.12，2D）
> 目的：把「玩家报告的现象 + 当前代码状态 + 我查到的结论 + 剩余待查方向」交接给下一位修复者。
> **请先读第 2 节和第 3 节**——第 2 节说明我**未能复现**玩家现象，第 3 节是复现时最容易踩的环境坑。

---

## 1. 一句话现状

核心玩法（投放、物理堆叠、同级合成、连击、越线判负、复活、道具、结算、存档、广告位、隐私合规）已实现，**177 项自动化测试在真工程与本工程副本上均全绿**（EditMode 103 / PlayMode 74，退出码 0）。
但**玩家在编辑器里试玩时反馈「看不出落点」「水果堆在同一层」且「改完没区别」**，这两点我**无法在自己的验证环境里复现**，也未能在玩家自己的会话中观测到（原因见第 2 节）。

---

## 2. 玩家报告的现象 与 我的复现结果

### 2.1 玩家原话（按时间顺序）

1. 「游戏启动后，没反应」
2. 「1. 无法拖动水果确定落点，或者说，具体操作是？啥叫按住，我现在是 windows 开发环境，怎么按住呢？ 2. 水果碰撞后，不会动，而是一直在一个图层上堆积」
3. 「还是一样的，没区别」（在我修了预览、物理参数之后）
4. 「还是一个样」

### 2.2 我的复现结果（**关键：未能复现**）

| 玩家描述 | 我的观测 | 环境 |
|---|---|---|
| 启动后没反应 | **已修复并复现验证**：`PanelController` 运行时装配缺失导致隐私弹窗不显示、按钮全失效。修复后实测弹窗 `activeSelf=True/alpha=1/blocksRaycasts=True`，点同意 → 直接开局 | 真工程（MCP） |
| 无法确定落点 | **找到并修复**：① 预览线把下落段算进 0.8s 预算 → 线截断在半空（实测最低 y≈2.06）；② 待投水果生成高度落在 HUD 顶栏之后（中心在屏幕顶部 7%，顶栏占 15%）→ 被分数挡住。修复后实测线最低到 **-4.20（地面）**，水果中心在 **17.8%** | 本工程副本（批处理 + 相机截图） |
| 水果堆在一层 | **找到并修复**：滚动阻尼不足 → 水果滚到墙角（实测横向偏移 **1.95**，正好是左墙静止位）。修复后 6 次同点投放横向偏移 **0.03**，4 颗不同等级叠成 **4 层竖塔** | 同上 |
| 「改完没区别」 | **无法复现/无法证伪**：我确认过玩家工程里的参数确实是新的（运行时实测 `friction=0.6 bounce=0.5 linDrag=2.8 mergeImpulse=0`），但**玩家的编辑器当时不在 Play 模式**，我无法知道他每一次测试实际跑的是哪一版 | 真工程（MCP） |

### 2.3 我为什么没能复现玩家的现象（重要）

1. **无法注入真实鼠标输入**：我所有的自动化测试都是直接调用瞄准接口（`AimController.HandleFrame`），**绕过了 `Input.GetMouseButton` 这条真实输入链路**。因此「真实鼠标按住拖动到底有没有反应」这条我从未验证过，而它恰好是玩家最直接的抱怨点。
2. **无法抓取玩家 Game 视图画面**：`ScreenCapture.CaptureScreenshot` 需要渲染到帧末；玩家的编辑器窗口失焦时 Unity 会节流播放器循环（实测 `frame=4` 且不推进），截图文件根本不会生成。MCP 的相机构图截图又会排除 Screen Space-Overlay 的 HUD。
3. **玩家不在旁边**：无法请他「按一下鼠标，然后告诉我控制台输出了什么」。

> 结论：**剩余问题最可能在「真实鼠标输入链路」或「玩家对画面的预期与我不同」这两处。** 第 4 节给出把这两处一次性定位的方法。

---

## 3. 复现时的环境坑（每条我都踩过，会造成「改了没区别」的假象）

1. **Play 模式下不重编译脚本、不重载 ScriptableObject 资产。**
   任何脚本或 `Assets/Config/GameBalance.asset` 的改动，都必须**退出 Play 再重新进入**才生效。**这是「改完没区别」最常见的原因。**
2. **运行时的数值来自资产，不是代码默认值。**
   改 `GameBalance` 的 `[SerializeField]` 默认值后**必须**重新生成资产：菜单 `MergeWater/Generate Config Assets`（或 `ConfigAssetGenerator.GenerateMenu()`）。否则运行时仍是旧值。`ConfigAssetTests` 会因两者不一致而失败以提示。
3. **场景里烘焙的旧值会覆盖代码默认值。**
   序列化到 `Main.unity` 的值（如 `DangerLineView.normalColor`、`GameField.wallColor`）优先级高于 C# 初始化器。改动默认值后需**重建场景**：菜单 `MergeWater/Build Main Scene`。相关代码已加运行时兜底守卫，但仍有此类风险。
4. **MCP 桥接在 Unity 域重载后会失效**（报 `Session not found` / ping 无响应），需要重载 ZCode 会话。
5. **编辑器失焦时播放器循环被节流**：PlayMode 长测在 MCP 直连下会挂住（`blocked_reason: editor_unfocused`）。**长测请走批处理**（见第 8 节）。
6. **批处理 `-nographics` 下调用 `Camera.Render()` 会让进程 SIGSEGV**（日志：`GfxDevice renderer is null`）。截图类测试必须用 `SystemInfo.graphicsDeviceType != Null` 守卫，并**不要加 `-nographics`**。
7. **诊断工具自己会说谎**：为把 HUD 拍进图而临时把 Canvas 切成 ScreenSpaceCamera 时，UI（排序号 0）会压住排序号为负的容器墙体，截图误示「容器不可见」。**结论以坐标/像素数据交叉验证为准。**

---

## 4. 建议的下一步：一次性定位「输入链路 vs 画面预期」

### 4.1 让玩家做的事（最少的两次操作）

1. **退出 Play 模式**，确认 Console 无编译错误，**再按 Play**。
2. 在 **Game 视图**里按住鼠标左键左右移动然后松开，同时看 Console。

### 4.2 给玩家一段定位脚本（贴进 Console 执行，或用 MCP 执行）

在 Play 模式下执行下列代码，它会告诉我们「输入到底有没有到达游戏」以及「画面上应该看到什么」：

```csharp
// 每帧打印：鼠标状态、瞄准状态、局状态、面板状态、待投水果的屏幕位置
var aim = UnityEngine.Object.FindObjectOfType<MergeWater.Aim.AimController>();
var boot = UnityEngine.Object.FindObjectOfType<MergeWater.Bootstrap.GameBootstrapper>();
var view = UnityEngine.Object.FindObjectOfType<MergeWater.Presentation.HudView>();
var cam = UnityEngine.Camera.main;

var sb = new System.Text.StringBuilder();
sb.Append("mousePresent=" + UnityEngine.Input.mousePresent);
sb.Append(" down=" + UnityEngine.Input.GetMouseButtonDown(0));
sb.Append(" held=" + UnityEngine.Input.GetMouseButton(0));
sb.Append(" up=" + UnityEngine.Input.GetMouseButtonUp(0));
sb.Append(" mousePos=" + UnityEngine.Input.mousePosition);
sb.Append("\naim: enabled=" + aim.enabled + " interactable=" +
    typeof(MergeWater.Aim.AimController)
        .GetField("_interactable", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
        .GetValue(aim));
sb.Append(" state=" + aim.State.IsAiming + " x=" + aim.State.X.ToString("0.00"));
sb.Append("\nctx: session=" + (boot.Context.Session == null ? "null" : boot.Context.Session.Phase.ToString()));
sb.Append(" panel=" + boot.Context.Panels.CurrentPanel);
sb.Append(" liveFruits=" + UnityEngine.Object.FindObjectOfType<MergeWater.Field.GameField>().LiveFruitCount);
sb.Append("\npendingFruitScreenPos=" + cam.WorldToScreenPoint(new UnityEngine.Vector3(aim.State.X, boot.Context.Balance.DropSpawnY, 0f)));
return sb.ToString();
```

**判读**：
- `mousePresent=False` 或三个按钮值恒为 False → **输入链路问题**（检查 `ProjectSettings/ProjectSettings.asset` 的 `activeInputHandler`，当前为 `0`＝旧版 Input Manager，正确；Input System 包未安装）。
- 按钮值正常、`aim.interactable=False` → 输入被上层挡住了（检查 `PanelController.CurrentPanel` 是否非 None、`Ads.IsShowing`）。
- 按钮值正常、`interactable=True`、但 `state` 不变 → `AimController.Update` 没跑或被禁用（检查组件 `enabled`、`DefaultPointerSource`）。
- 都正常 → 是**画面预期**问题，让玩家按 4.3 描述应该看到什么。

### 4.3 玩家应该看到的画面（附我复核过的截图）

用世界视图截图 `Logs/Diagnostics/while-aiming.png` 对照：按住时应有 **顶部一颗待投水果 + 一条贯穿到地面的预测线 + 粉色警戒线 + 棕色左右墙与地面**。
若玩家看不到这些，多半是**在 Scene 视图而非 Game 视图观察**（Scene 视图不渲染 Screen Space-Overlay 的 HUD，且鼠标用于场景导航、不会驱动游戏输入）。

---

## 5. 当前状态：已确认可用的部分（含证据）

| 项 | 证据 |
|---|---|
| 自动化测试 | EditMode **103/103**、PlayMode **74/74**，`exit=0`；XML 归档在 `Docs/evidence/` |
| 真工程编译 | `Library/ScriptAssemblies/` 下 8 个运行时/编辑器 DLL + 2 个测试 DLL 齐全，无 `error CS` |
| 真工程参数生效 | 运行时实测 `friction=0.6 bounce=0.5 linDrag=2.8 angDrag=0.05 mergeImpulse=0 gScale(1)=1.60 gScale(11)=3.60`，求解迭代 8/3 |
| 真工程场景接线 | `Main.unity` 140 处 GUID 引用抽查全部命中；构建场景 GUID 与 `.meta` 一致 |
| 隐私门控 → 开局 | MCP 实测：未同意时弹窗可见可点、`Session=null`；点同意后 `Session=Ready`、埋点与广告初始化 |
| 投放 → 物理 | 真实瞄准路径投放后 `phase=Playing`、场地生成水果；6 次同点投放横向偏移 0.03；4 颗不同等级叠成 4 层塔 |
| 合成 | 同级相撞合成成功（6 次投放 → 3 颗；测试断言 `LiveFruitCount < drops`） |
| 预览 | 47 个采样点，最低点 **-4.20** 到达地面；线材质 `Sprites/Default`，待投水果 sprite/颜色正确 |
| 容器与警戒线可见 | 左右墙/地面有 `ui_square` 渲染器（排序 -10）；警戒线常态 alpha 0.55 |

---

## 6. 我已修复的缺陷（**请勿回退**；附根因与覆盖测试）

按发现顺序，同一类根因出现多次，接手时请注意这个模式：**「编辑器期装配、运行时失效」**。

| # | 缺陷 | 根因 | 覆盖测试 |
|---|---|---|---|
| 1 | 首投无法进行（游戏不可玩） | `Ready` 阶段的输入被只允许 `Playing` 的 `CanAct` 挡掉 | `RoundSessionTests.StartRound_...`、`BootstrappedSceneTests.RealDrop_ThroughAimPath_...` |
| 2 | 重开局重复计分 | `NewRound()` 未解绑上一局的 `IFieldPort.Merged` | `RoundLifecycleTests.NewRound_..._WithoutLeakingPreviousEvents` |
| 3 | 越线误判（刚生成的水果算已静止） | 只看瞬时速度 | `GameFieldDangerTests.FreshlySpawnedFruitAboveLine_...` |
| 4 | 复活广告可能播两次 | Session 与 Economy 各播一次 | `RoundLifecycleTests.Revive_WhenAdCompletes_...`（断言 `RewardedShown == 1`） |
| 5 | **隐私弹窗不显示、所有按钮失效（玩家「启动后没反应」）** | `PanelController` 的面板查找表与按钮监听是非序列化状态，只在编辑器期装配 | `BootstrappedSceneTests`（5 项真场景端到端） |
| 6 | 手感反馈全部空转 | `HudBuilder` 从未调用 `FeedbackDirector.Configure` | `BootstrappedSceneTests.Merge_ProducesVisibleFeedback` |
| 7 | 真场景测试导致批处理退出崩溃（退出码 21） | 原生对象留给 GC 终结器在 `PhysicsManager` 销毁后回收 | TearDown 排空终结器队列；复跑 `exit=0` |
| 8 | 贴边元素被裁出画布（设置按钮顶部 -4） | 贴边锚点配 `pivot 0.5` | `HudLayoutTests.AllVisibleHudElements_FitInsideDesignCanvas` |
| 9 | 设置按钮与微信胶囊重叠 | 右侧只留 190 单位，胶囊区需约 300 | `HudLayoutTests.TopBar_DoesNotEnterWeChatCapsuleZone` |
| 10 | 预览线断在半空 | 下落段也算进 V2.26 的 0.8s 预算 | `PhysicsAndPreviewDiagnostics.Aiming_ShowsPreviewReachingTheLandingSurface` |
| 11 | 水果滚到墙角、摊平成一层 | 圆形碰撞体纯滚动无阻力（`angularDrag`/`linearDrag` 过低） | `PhysicsAndPreviewDiagnostics.DroppedFruits_...`、`FruitsOfDifferentTiers_StackOnEachOther` |
| 12 | 合成结果乱跳 | 给结果施加了 0.6 的向上初速（V2.31 已改为零初速） | `GameFieldMergeTests.MergeResult_SpawnsAtRestAtMidpoint` |
| 13 | 同级水果贴着却不合成 | 只监听碰撞进入，未监听碰撞停留（V2.32） | `GameFieldMergeTests` |
| 14 | 待投水果被 HUD 顶栏遮挡 | 生成高度 5.2 落在顶栏带内（V2.26a 改为 4.0） | `PhysicsAndPreviewDiagnostics.Aiming_...`（断言中心在顶栏之下） |
| 15 | 容器不可见（水果像掉进虚空） | 墙/地只有碰撞体没有渲染器；且只在首次投放后才创建 | `PhysicsAndPreviewDiagnostics`（断言 Arena 三子节点都有渲染器） |
| 16 | **警戒线从未显示过** | `DangerLineView._line` 是非序列化字段，运行时为 null → `SetLine` 提前返回 | `PhysicsAndPreviewDiagnostics`（断言常态 alpha ≥ 0.4） |
| 17 | 「无容器」配置仍被 Awake 建出的墙拦住 | `Configure(buildArena:false)` 未清理 | `GameFieldLifecycleTests.EscapedFruit_IsRecycledInsteadOfLeaking` |

物理手感参数（决策 D12）参考了同品类开源实现并按需求编号登记：

- `V2.27` 线性阻尼 2.8、`V2.28` 重力倍率随等级 1.6→3.6、`V2.29` 摩擦 0.6、`V2.30` 弹性 0.5、`V2.31` 合成零初速、`V2.32` 碰撞停留也尝试合成、`V2.26a` 生成高度 4.0。
- 参考实现：`https://github.com/BcoffeeDev/game-core-melon-merge`（MIT；本机副本 `E:\refs\game-core-melon-merge`）。**只采纳了参数与两个行为思路，未拷贝代码。**
- **V1 的半径与质量仍严格按策划案**（质量 0.10→3.94，比值 39×，比参考实现的 3× 大得多），因此更依赖高阻尼。

---

## 7. 剩余待查方向（按可能性排序）

### A. 真实鼠标输入链路（最高优先级）

自动化从未覆盖 `Input.GetMouseButton`。请用 4.2 的脚本确认：

- `AimController.LegacyPointerSource` 读的是旧版 `Input`；`activeInputHandler` 当前为 `0`（正确）。
- `AimController.Update` 每帧取 `DefaultPointerSource ?? (_input = new LegacyPointerSource())`；若 `_interactable=False` 直接返回。
- `GameBootstrapper.Update` 每帧调用 `Context.SetInputBlocked(ads.IsShowing || panels.CurrentPanel != None)`。
- **可疑点**：`SetInputBlocked` 现在要求 `Phase == Ready || Playing`；若出现「面板已关闭但 `CurrentPanel` 未归零」或「`Ads.IsShowing` 卡住」，输入会被永久禁用。

### B. 玩家观察的位置（Scene 视图 vs Game 视图）

Scene 视图不渲染 Overlay HUD、鼠标用于场景导航 → 表现为「完全没反应」。请让玩家明确在 **Game 视图**操作（并确认 Game 视图分辨率不是极端长宽比）。

### C. 「同在一個图层」的其它解读

若玩家指的是 **渲染排序**（而非物理摊平）：水果的 `SpriteRenderer.sortingOrder = 100 + Level`，容器为 -10，警戒线 540，预览 560。请确认玩家看到的重叠是否只是同层同序号的视觉重叠。

### D. 合成手感预期

当前合成是「吸附 60ms → 原地升级、零初速」（V2.21/V2.31）。若玩家期待的是「碰撞瞬间爆开」，那是**产品预期差异**，需要与策划确认，不是 bug。

---

## 8. 常用命令与关键文件

**测试（本机 Unity 路径）**

```powershell
$Unity = "E:\Unity\Unity\2022.3.62f3\Editor\Unity.exe"
$ProjectPath = "E:\Unity\Projects\DemoProjects\MergeWater"   # 若编辑器开着，请改用副本

# EditMode（快，~2s）
& $Unity -batchmode -nographics -projectPath $ProjectPath -runTests -testPlatform editmode `
  -testResults Logs\editmode-results.xml -logFile Logs\editmode.log

# PlayMode（~15s；不要加 -nographics，否则相机截图会崩）
& $Unity -batchmode -projectPath $ProjectPath -runTests -testPlatform playmode `
  -testResults Logs\playmode-results.xml -logFile Logs\playmode.log
```

- **批处理无法打开已被编辑器锁定的工程**；编辑器开着时请在副本上跑（我此前用 `E:\MergeWaterVerify`）。
- 判成功要看**退出码 + 结果 XML 的 failed 数 + 日志里的 `error CS`**，不能只看日志末行。

**编辑器菜单**

| 菜单 | 作用 |
|---|---|
| `MergeWater/Generate Placeholder Art` | 生成占位图（含 `ui_square`）到 `Assets/Resources/Placeholder/` |
| `MergeWater/Generate Config Assets` | 从代码默认值重建 `Assets/Config/{GameBalance,MetaSettings}.asset` |
| `MergeWater/Build Main Scene` | 重建 `Assets/Scenes/Main.unity`（含容器外观、HUD 布局），并写入构建场景列表 |

**关键文件**

| 关注点 | 文件 |
|---|---|
| 瞄准/预览/落点 | `Assets/Scripts/Aim/AimController.cs`、`AimSolver.cs`、`PointerSource.cs` |
| 物理与合成 | `Assets/Scripts/Field/GameField.cs`、`FruitBody.cs` |
| HUD 布局与反馈 | `Assets/Scripts/Presentation/HudBuilder.cs`、`HudBinder.cs`、`FeedbackDirector.cs`、`PanelController.cs`、`DangerLineView.cs` |
| 数值单一来源 | `Assets/Scripts/Core/Config/GameBalance.cs` + `Assets/Config/GameBalance.asset` |
| 需求与数值表 | `Docs/requirements.md`（V1 表、V2.20–V2.32、决策 D1–D12） |
| 启动与装配 | `Assets/Scripts/Bootstrap/GameBootstrapper.cs`、`GameContext.cs` |
| 诊断与真场景测试 | `Assets/Tests/PlayMode/Bootstrap/{BootstrappedSceneTests,PhysicsAndPreviewDiagnostics}.cs` |

**证据与产物**

- `Docs/evidence/{editmode,playmode}-results.xml`：本目录归档的测试结果
- `Docs/evidence/README.md`：每轮修复的复跑记录与结论
- `Logs/Diagnostics/`：`while-aiming.png`（容器/警戒线/预览）、`stacked-tiers.png`（4 层竖塔）、`*.txt`（坐标/速度/参数实测）
- `Docs/progress.md`：模块进度 + 缺陷总表 + 已知环境限制
- `AGENTS.md`：项目硬约束（含「改默认值后必须重新生成资产」的提醒）

---

## 9. 测试基线（接手前请先复现这个基线）

| 套件 | 结果 | 退出码 |
|---|---|---|
| EditMode | 103 / 103 | 0 |
| PlayMode | 74 / 74 | 0 |
| 合计 | **177 / 177** | 均 0 |

若你的基线不是这个数，先弄清差异来源（很可能是第 3 节的环境坑），再开始定位玩家问题。

---

## 10. 需要产品/玩家确认的未决问题

1. 玩家是在 **Game 视图**还是 **Scene 视图**观察的？（决定「完全没反应」是否只是视图选错）
2. 「水果堆在一个图层上」指的是**物理摊平**还是**渲染排序重叠**？
3. 合成的手感预期：「吸附后就地变大」（当前实现）还是「碰撞瞬间爆开」？
4. 是否接受当前的占位观感（程序化圆片 + 圆角方块的容器）？正式美术替换点已在 `HudBuilder` / `PlaceholderArtGenerator` / `GameField.WallSprite` 收敛。

---

## 11. 后续修复结论（2026-09-11 晚，接手会话补充）

第 7 节的待查方向 A/B/C 都不是根因。真正的根因在**渲染层**：

> `HudBuilder.BuildCanvas` 在 Canvas 下建了一块满屏 `Background` Image（1080×1920 拉伸、`alpha=1`）。
> Canvas 是 ScreenSpaceOverlay，**永远绘制在世界之上**，因此水果、容器、警戒线、待投水果与预测线
> 全部被这块不透明底板遮住 —— 玩家在 Game 视图里只看得到 HUD，于是表现为「无法拖动水果确定落点」
> 「看不到水果堆叠」「改了预览与物理参数也没区别」。

- 该底板自首次提交 `46fba3a` 起就存在；第 5 节表格里所有「世界可见性」自检用的都是**只渲染世界**的相机截图，故一直未暴露（与第 3 节第 7 条同源：诊断视角选错会持续骗过验证）。
- 修复：删除满屏底板，底色改由相机 SolidColor 清屏提供（`HudBuilder.BackgroundColor`，与相机原清屏色逐值一致），`SceneBuilder` 共用该常量，并重建 `Main.unity` 使既有场景同步。
- 新增门禁 `HudLayoutTests.NoOpaqueFullScreenGraphic_HidesTheGameWorld`（禁止 HUD 出现「铺满屏幕 + 不透明」的图形；改 `HudBuilder` 后忘记重建场景也会被它拦住）。
- 复跑：EditMode **104/104**、PlayMode **75/75**，`failed=0`（XML 见 `Docs/evidence/`）。证据链与判定过程见 `Docs/evidence/README.md`。

未被本会话改动的次要隐患：`AimController` 的「指针离开屏幕即取消瞄准」在桌面编辑器里会因光标移出 Game 视图而**静默**取消投放（移动端语义正确），见 `Docs/evidence/README.md` 的「尚未完成」。

