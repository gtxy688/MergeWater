using System.Collections;
using System.IO;
using System.Text.RegularExpressions;
using MergeWater.Aim;
using MergeWater.Bootstrap;
using MergeWater.Core;
using MergeWater.Field;
using MergeWater.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace MergeWater.Tests.PlayMode
{
    /// <summary>
    /// 真实场景（`Assets/Scenes/Main.unity`）的端到端验收：加载**真场景**、走真实 UI 与输入路径。
    ///
    /// 存在的理由：其余 PlayMode 测试都用代码搭最小脚手架，脚手架会自己调用
    /// `PanelController.Configure(view)` 等装配方法，从而掩盖「只在编辑器期装配、运行时失效」的缺陷。
    /// 曾真实发生：`PanelController` 的查找表（非序列化）与按钮监听只在编辑期建立，运行时为空，
    /// 导致隐私弹窗不显示、所有按钮点不动——玩家看到的就是「启动后毫无反应」；
    /// 同批还发现 `FeedbackDirector` 的组件引用从未接线，R22 手感反馈全部空转。
    /// 因此这组测试只断言**玩家可观察的结果**（面板真的可见、点击真的生效、合成真的有反馈）。
    /// </summary>
    public sealed class BootstrappedSceneTests
    {
        private const string SceneName = "Main";

        private static readonly string SavePath = Path.Combine(Application.persistentDataPath, "mwater_save.json");

        private Scene _scene;
        private bool _loaded;
        private bool _hadSave;
        private string _saveBackup;

        [SetUp]
        public void SetUp()
        {
            // 用全新存档保证「首启未同意隐私」这条路径可复现；结束后还原玩家存档。
            _hadSave = File.Exists(SavePath);
            if (_hadSave)
            {
                _saveBackup = File.ReadAllText(SavePath);
                File.Delete(SavePath);
            }
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_loaded)
            {
                var unload = SceneManager.UnloadSceneAsync(_scene);
                while (unload != null && !unload.isDone)
                    yield return null;

                _loaded = false;
                yield return null;
            }

            if (_hadSave)
                File.WriteAllText(SavePath, _saveBackup);
            else if (File.Exists(SavePath))
                File.Delete(SavePath);

            // 加载/卸载真场景会留下若干 Unity 原生对象的托管包装（材质、字体等）。
            // 若留到进程退出时由 GC 终结器回收，它们可能在 PhysicsManager 已被销毁之后
            // 触碰物理管理器，导致批处理模式在**退出阶段**崩溃（退出码非 0，破坏 CI 门禁）。
            // 因此在物理管理器仍存活时主动排空终结器队列。
            Resources.UnloadUnusedAssets();
            System.GC.Collect();
            System.GC.WaitForPendingFinalizers();

            yield return null;
        }

        private IEnumerator LoadRealScene()
        {
            var load = SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Additive);
            Assert.That(load, Is.Not.Null, $"场景 {SceneName} 必须已加入构建场景列表");

            while (!load.isDone)
                yield return null;

            _scene = SceneManager.GetSceneByName(SceneName);
            Assert.That(_scene.IsValid(), Is.True, "场景应已加载");
            _loaded = true;

            yield return null; // Awake
            yield return null; // Start
        }

        private static T Find<T>() where T : Object => Object.FindObjectOfType<T>();

        /// <summary>飘字池里当前处于激活（正在播放）状态的条数。</summary>
        private static int CountActiveFloatingText(FloatingTextSpawner spawner)
        {
            var items = spawner.GetComponentsInChildren<FloatingTextItem>(true);
            var active = 0;
            for (var i = 0; i < items.Length; i++)
            {
                if (items[i] != null && items[i].gameObject.activeSelf)
                    active++;
            }

            return active;
        }

        private static void ClickAccept(GameBootstrapper bootstrapper)
        {
            var view = Find<HudView>();
            Assert.That(view.privacyAcceptButton, Is.Not.Null, "隐私弹窗必须有同意按钮");
            view.privacyAcceptButton.onClick.Invoke();
        }

        /// <summary>等待加载页结束（Demo 版随后直接开局；要求门控时随后停在隐私弹窗）。</summary>
        private static IEnumerator WaitForLoadingDone(GameBootstrapper bootstrapper)
        {
            // 真场景默认要求点击「开始」才离开加载页；这里走测试接缝跳过点击，
            // 点击路径由 FirstLaunch_LoadingScreenWaitsForTapBeforeStarting 单独覆盖。
            bootstrapper.RequireTapToStart = false;

            var deadline = Time.realtimeSinceStartup + 8f;

            while (Time.realtimeSinceStartup < deadline)
            {
                if (bootstrapper.Context != null && bootstrapper.Context.Panels.CurrentPanel != PanelId.Loading)
                    yield break;

                yield return null;
            }

            Assert.Fail("加载页未在超时内结束");
        }

        /// <summary>等到进入对局为止；需要隐私门控时由测试代劳点击同意（只关心「能开局」的用例用）。</summary>
        private static IEnumerator WaitForRound(GameBootstrapper bootstrapper)
        {
            // 真场景默认要求点击「开始」才进游戏（需求方要求加载页必须被看到）；
            // 只关心「能开局」的用例走测试接缝跳过点击。点击路径由
            // FirstLaunch_LoadingScreenWaitsForTapBeforeStarting 单独覆盖。
            bootstrapper.RequireTapToStart = false;

            var deadline = Time.realtimeSinceStartup + 10f;

            while (Time.realtimeSinceStartup < deadline)
            {
                var context = bootstrapper.Context;
                if (context != null && context.Session != null)
                    break;

                if (context != null && context.Panels.CurrentPanel == PanelId.Privacy)
                    ClickAccept(bootstrapper);

                yield return null;
            }

            Assert.That(bootstrapper.Context.Session, Is.Not.Null, "入口流程未在超时内进入对局");

            // 开局当帧输入仍被屏蔽（GameBootstrapper.Update 下一帧才按面板/广告状态放行），
            // 直接驱动 AimController 会被忽略，因此这里等它真的可交互。
            var aim = Find<AimController>();
            var interactableDeadline = Time.realtimeSinceStartup + 5f;
            while (Time.realtimeSinceStartup < interactableDeadline && aim != null && !aim.Interactable)
                yield return null;

            Assert.That(aim == null || aim.Interactable, Is.True, "开局后瞄准应变为可交互");
        }

        [UnityTest]
        public IEnumerator FirstLaunch_LoadingScreenWaitsForTapBeforeStarting()
        {
            yield return LoadRealScene();

            var bootstrapper = Find<GameBootstrapper>();
            var view = Find<HudView>();

            // 需求方（2026-09-12）反馈「按 Play 后根本没看到加载页」：现在进度走满后停在
            // 「点击开始」，必须由玩家确认才进游戏，保证加载页一定被看到。
            Assert.That(view.loadingPanel.gameObject.activeSelf, Is.True, "启动时必须显示加载页");

            var deadline = Time.realtimeSinceStartup + 10f;
            while (Time.realtimeSinceStartup < deadline && view.loadingHintText.text != "点击开始")
                yield return null;

            Assert.That(view.loadingHintText.text, Is.EqualTo("点击开始"), "进度满后应提示点击开始");
            Assert.That(bootstrapper.Context.Session, Is.Null, "未点击前不应开局");
            Assert.That(view.loadingPanel.gameObject.activeSelf, Is.True, "未点击前加载页应保持显示");

            bootstrapper.NotifyStartTapped();
            yield return null;

            Assert.That(bootstrapper.Context.Session, Is.Not.Null, "点击后应直接进入对局（R26）");
            Assert.That(view.loadingPanel.gameObject.activeSelf, Is.False, "进入对局后加载页应关闭");
        }

        /// <summary>
        /// 玩家实测（2026-09-12）：「加载页面，进度条永远是 100%，不会动」。
        /// 根因：进度直接按 <c>Time.unscaledDeltaTime</c> 累计，而编辑器/低配设备上单帧
        /// deltaTime 可达 0.5~3 s（首帧初始化、GC 停顿、失焦后恢复），2 s 的最短加载被一两帧走完。
        /// 这里用真实的 3 s 主线程阻塞复现「单帧卡顿」，锁住「一帧不得从 0 跳到满」。
        /// </summary>
        [UnityTest]
        public IEnumerator LoadingProgress_AfterLongFrameStall_DoesNotJumpStraightToFull()
        {
            yield return LoadRealScene();

            var bootstrapper = Find<GameBootstrapper>();
            var view = Find<HudView>();

            bootstrapper.BeginEntryFlow();
            yield return null;

            Assert.That(view.loadingProgressFill.fillAmount, Is.LessThan(0.2f), "加载页应从 0 开始推进");

            // 阻塞主线程 3 s：之后一帧的 unscaledDeltaTime 会远大于最短加载时长（2 s）。
            System.Threading.Thread.Sleep(3000);
            yield return null; // 卡顿帧
            yield return null; // 确保 GameBootstrapper.Update 至少用这个 deltaTime 推进过一次

            Assert.That(view.loadingProgressFill.fillAmount, Is.LessThan(0.5f),
                "单帧卡顿不得让进度条一帧从 0 跳到 100%（玩家实测现象）");
            Assert.That(view.loadingHintText.text, Is.EqualTo("加载中…"), "进度未走满前不应提示「点击开始」");
            Assert.That(bootstrapper.Context.Session, Is.Null, "进度未走满前不应开局");
        }

        [UnityTest]
        public IEnumerator FirstLaunch_LoadingScreenShowsThenStartsRoundDirectly()
        {
            yield return LoadRealScene();

            var bootstrapper = Find<GameBootstrapper>();
            var view = Find<HudView>();

            Assert.That(bootstrapper, Is.Not.Null, "真场景应有 GameBootstrapper");
            Assert.That(bootstrapper.Context, Is.Not.Null, "启动后应构建出 Context");
            Assert.That(bootstrapper.Context.Privacy.IsAccepted, Is.False, "全新存档应未同意隐私");
            Assert.That(bootstrapper.Context.Session, Is.Null, "加载页期间不应开局");

            // 关键回归点：入口界面必须真的显示出来，否则玩家只能看到静止画面、任何操作都无响应。
            Assert.That(view.loadingPanel, Is.Not.Null, "真场景应装配 loadingPanel");
            Assert.That(view.loadingPanel.gameObject.activeSelf, Is.True, "启动时必须显示加载页");
            Assert.That(view.loadingPanel.Group.alpha, Is.GreaterThan(0.99f), "加载页必须完全不透明");
            Assert.That(bootstrapper.Context.Panels.CurrentPanel, Is.EqualTo(PanelId.Loading));
            Assert.That(view.loadingHintText.text, Is.EqualTo("加载中…"));

            yield return WaitForRound(bootstrapper);

            Assert.That(bootstrapper.Context.Session, Is.Not.Null, "加载页结束后应直接进入对局（R26，无开始页）");
            Assert.That(bootstrapper.Context.Session.Phase, Is.EqualTo(RoundPhase.Ready));
            Assert.That(bootstrapper.Context.Analytics.IsInitialized, Is.True, "直接开局时埋点应已初始化");
            Assert.That(view.loadingPanel.gameObject.activeSelf, Is.False, "进入对局后加载页应关闭");
            Assert.That(view.privacyPanel.gameObject.activeSelf, Is.False, "Demo 版不再弹隐私协议");
            Assert.That(view.scoreText.text, Is.EqualTo("0"), "分数应初始化为 0");
        }

        [UnityTest]
        public IEnumerator WhenPrivacyConsentRequired_PrivacyPanelIsActuallyVisible()
        {
            yield return LoadRealScene();

            var bootstrapper = Find<GameBootstrapper>();
            var view = Find<HudView>();

            // 正式发布路径：要求隐私同意后才开局（R20）。显式开启门控并重走入口流程。
            bootstrapper.RequirePrivacyConsent = true;
            bootstrapper.BeginEntryFlow();
            yield return WaitForLoadingDone(bootstrapper);

            Assert.That(bootstrapper.Context.Privacy.IsAccepted, Is.False, "未点同意前不应视为已同意");
            Assert.That(bootstrapper.Context.Session, Is.Null, "未同意时不应开局");
            Assert.That(view.loadingPanel.gameObject.activeSelf, Is.False, "加载页应已关闭");

            // 关键回归点：弹窗必须真的显示出来，否则玩家只能看到静止画面、任何操作都无响应。
            Assert.That(view.privacyPanel, Is.Not.Null, "真场景应装配 privacyPanel");
            Assert.That(view.privacyPanel.gameObject.activeSelf, Is.True,
                "隐私弹窗的 GameObject 必须是激活的（曾因运行时查找表为空而保持隐藏）");
            Assert.That(view.privacyPanel.Group.alpha, Is.GreaterThan(0.99f), "隐私弹窗必须完全不透明");
            Assert.That(view.privacyPanel.Group.blocksRaycasts, Is.True, "隐私弹窗必须能接收点击");
            Assert.That(view.privacyPanel.Group.interactable, Is.True, "隐私弹窗必须可交互");
        }

        [UnityTest]
        public IEnumerator AcceptButton_IsWiredAtRuntime_AndStartsRoundDirectly()
        {
            yield return LoadRealScene();

            var bootstrapper = Find<GameBootstrapper>();
            var view = Find<HudView>();

            bootstrapper.RequirePrivacyConsent = true;
            bootstrapper.BeginEntryFlow();
            yield return WaitForLoadingDone(bootstrapper);

            ClickAccept(bootstrapper);
            yield return null;

            Assert.That(bootstrapper.Context.Privacy.IsAccepted, Is.True, "点击同意应写入同意状态");
            Assert.That(bootstrapper.Context.Analytics.IsInitialized, Is.True, "同意后才初始化埋点（R20）");
            Assert.That(bootstrapper.Context.Ads.IsAvailable, Is.True, "同意后才启用广告适配器（R20）");
            Assert.That(bootstrapper.Context.Session, Is.Not.Null, "同意后应直接进入对局（R26，无开始页）");
            Assert.That(bootstrapper.Context.Session.Phase, Is.EqualTo(RoundPhase.Ready));
            Assert.That(view.privacyPanel.gameObject.activeSelf, Is.False, "同意后弹窗应关闭");
            Assert.That(view.scoreText.text, Is.EqualTo("0"), "分数应初始化为 0");
        }

        [UnityTest]
        public IEnumerator FirstRound_InEditor_ShowsMouseAndGameViewInstructions()
        {
            yield return LoadRealScene();

            var bootstrapper = Find<GameBootstrapper>();
            var view = Find<HudView>();

            yield return WaitForRound(bootstrapper);

#if UNITY_EDITOR
            Assert.That(view.toastText.text, Does.Contain("Game 视图"),
                "Editor 首局提示必须说明操作发生在 Game 视图，避免玩家在 Scene 视图拖动却看不到响应");
            Assert.That(view.toastText.text, Does.Contain("鼠标左键"),
                "Editor 首局提示必须明确 Windows 用户需要按住鼠标左键");
            yield return new WaitForSecondsRealtime(2.2f);
            Assert.That(view.toastPanel.Group.alpha, Is.GreaterThan(0f),
                "首次操作提示不应被默认 1.8 秒 Toast 过早隐藏，玩家需要足够时间读完并照做");
#endif
        }

        [UnityTest]
        public IEnumerator SettingsButton_IsWiredAtRuntime_AndPanelBecomesVisible()
        {
            yield return LoadRealScene();

            var bootstrapper = Find<GameBootstrapper>();
            var view = Find<HudView>();

            yield return WaitForRound(bootstrapper);

            Assert.That(view.settingsButton, Is.Not.Null, "真场景应有设置按钮");
            view.settingsButton.onClick.Invoke();
            yield return null;

            // 按钮监听是运行时 AddListener，编辑器期挂的监听不会被序列化——这里正是它的回归守卫。
            Assert.That(view.settingsPanel, Is.Not.Null);
            Assert.That(view.settingsPanel.gameObject.activeSelf, Is.True, "点击设置后设置面板必须真的显示");
            Assert.That(view.settingsPanel.Group.alpha, Is.GreaterThan(0.99f));
            Assert.That(bootstrapper.Context.Panels.CurrentPanel, Is.EqualTo(PanelId.Settings));
        }

        /// <summary>
        /// 设置页音量条（2026-09-12 需求方反馈「音量条丢失」）：
        /// 拉动音量条必须立刻改音频音量并写入存档——只画一条好看但不能拖的条子不算完成。
        /// </summary>
        [UnityTest]
        public IEnumerator VolumeSliders_AreWiredAtRuntime_AndChangeAudio()
        {
            yield return LoadRealScene();

            var bootstrapper = Find<GameBootstrapper>();
            var view = Find<HudView>();

            yield return WaitForRound(bootstrapper);

            bootstrapper.Context.Panels.Show(PanelId.Settings);
            yield return null;

            Assert.That(view.sfxSlider, Is.Not.Null, "设置页应有音效音量条");
            Assert.That(view.musicSlider, Is.Not.Null, "设置页应有音乐音量条");

            view.sfxSlider.value = 0.35f;
            view.musicSlider.value = 0.6f;
            yield return null;

            Assert.That(bootstrapper.Context.SettingsService.SfxVolume, Is.EqualTo(0.35f).Within(0.001f),
                "音效音量条应写入存档");
            Assert.That(bootstrapper.Context.SettingsService.MusicVolume, Is.EqualTo(0.6f).Within(0.001f),
                "音乐音量条应写入存档");

            var audio = Find<AudioDirector>();
            Assert.That(audio, Is.Not.Null, "真场景应有 AudioDirector");
            Assert.That(audio.SfxVolume, Is.EqualTo(0.35f).Within(0.001f), "音量条应立刻作用到音效音量");
            Assert.That(audio.MusicVolume, Is.EqualTo(0.6f).Within(0.001f), "音量条应立刻作用到音乐音量");
            Assert.That(view.sfxVolumeFill.fillAmount, Is.EqualTo(0.35f).Within(0.001f),
                "分段填充要跟随音量，否则玩家看不出当前档位");

            // 取消勾选后禁止调节大小（需求方 2026-09-12）；勾回来恢复可调，且音量值保留。
            view.sfxToggle.isOn = false;
            view.musicToggle.isOn = false;
            yield return null;

            Assert.That(view.sfxSlider.interactable, Is.False, "取消勾选音效后音量条不可调");
            Assert.That(view.musicSlider.interactable, Is.False, "取消勾选音乐后音量条不可调");
            Assert.That(bootstrapper.Context.SettingsService.SfxVolume, Is.EqualTo(0.35f).Within(0.001f),
                "关掉开关只是静音，音量值应保留");

            view.sfxToggle.isOn = true;
            yield return null;
            Assert.That(view.sfxSlider.interactable, Is.True, "重新勾选后音量条恢复可调");
            Assert.That(view.sfxSlider.value, Is.EqualTo(0.35f).Within(0.001f), "重新勾选后音量档位保持原值");
        }

        /// <summary>
        /// 对局背景（2026-09-12）：背景节点必须在场并排在玩法元素之后。
        /// 需求方先要求换成美术包背景、随后要求换回原来的纯色底，因此当前不指派素材
        /// （渲染器关闭、由相机清屏色兜底）；指派了素材时才校验铺满与重贴合。
        /// </summary>
        [UnityTest]
        public IEnumerator Backdrop_CoversViewport_AndSitsBehindTheGameplay()
        {
            yield return LoadRealScene();

            var backdrop = Find<BackdropView>();
            Assert.That(backdrop, Is.Not.Null, "真场景应有对局背景节点");
            Assert.That(backdrop.Renderer, Is.Not.Null, "背景应有 SpriteRenderer");
            Assert.That(backdrop.Renderer.sortingOrder, Is.LessThan(0), "背景排序值必须为负，否则会挡住玩法画面");

            var camera = Camera.main != null ? Camera.main : Find<Camera>();
            yield return null; // 等 BackdropView.LateUpdate 按当前视口贴合

            if (backdrop.Renderer.sprite == null)
            {
                // 纯色底模式：没有素材，背景不应参与绘制（相机 SolidColor 兜底）。
                Assert.That(backdrop.Renderer.enabled, Is.False, "无素材时背景渲染器必须关闭，避免残留上一次的图");
                yield break;
            }

            var viewHeight = camera.orthographicSize * 2f;
            var viewWidth = viewHeight * camera.aspect;

            Assert.That(backdrop.Renderer.bounds.size.y, Is.GreaterThanOrEqualTo(viewHeight), "背景高度未盖满视口");
            Assert.That(backdrop.Renderer.bounds.size.x, Is.GreaterThanOrEqualTo(viewWidth), "背景宽度未盖满视口");

            camera.aspect = 2.2f;
            yield return null;

            var changedWidth = camera.orthographicSize * 2f * camera.aspect;
            Assert.That(backdrop.Renderer.bounds.size.x, Is.GreaterThanOrEqualTo(changedWidth),
                "宽高比变化后背景未重新贴合，会露出相机清屏色边带");
        }

        [UnityTest]
        public IEnumerator RealDrop_ThroughAimPath_SpawnsFruit()
        {
            yield return LoadRealScene();

            var bootstrapper = Find<GameBootstrapper>();
            yield return WaitForRound(bootstrapper);

            var aim = Find<AimController>();
            var camera = Camera.main != null ? Camera.main : Find<Camera>();
            var field = Find<GameField>();

            var screenPoint = camera.WorldToScreenPoint(Vector3.zero);
            var pointer = new Vector2(screenPoint.x, screenPoint.y);

            aim.HandleFrame(new PointerFrame(true, true, false, true, pointer));
            aim.HandleFrame(new PointerFrame(false, false, true, true, pointer));
            yield return null;

            Assert.That(bootstrapper.Context.Session.Phase, Is.EqualTo(RoundPhase.Playing),
                "真实「按下→松手」路径应完成首次投放（曾因 Ready 阶段禁用输入而完全无法开局）");
            Assert.That(bootstrapper.Context.Session.DropCount, Is.EqualTo(1));
            Assert.That(field.LiveFruitCount, Is.EqualTo(1), "应真的在场地生成了水果");
        }

        [UnityTest]
        public IEnumerator Merge_ProducesVisibleFeedback()
        {
            // 真场景的埋点用 UnityDebugSink（会输出日志）且占位音效会真实播放，
            // 本测试只关心「手感反馈是否真的接线」，故忽略这些噪声日志。
            LogAssert.ignoreFailingMessages = true;

            yield return LoadRealScene();

            var bootstrapper = Find<GameBootstrapper>();
            yield return WaitForRound(bootstrapper);

            var aim = Find<AimController>();
            var camera = Camera.main != null ? Camera.main : Find<Camera>();
            var field = Find<GameField>();

            var screenPoint = camera.WorldToScreenPoint(Vector3.zero);
            var pointer = new Vector2(screenPoint.x, screenPoint.y);
            aim.HandleFrame(new PointerFrame(true, true, false, true, pointer));
            aim.HandleFrame(new PointerFrame(false, false, true, true, pointer));
            yield return null;

            var spawner = Find<FloatingTextSpawner>();
            var burst = Find<ParticleBurst>();

            Assert.That(spawner, Is.Not.Null, "真场景应有飘字生成器");
            Assert.That(burst, Is.Not.Null, "真场景应有粒子爆发");

            // 飘字改为**预置对象池**（运行时不生成 UI，2026-09-12 需求）：合成不再新增子对象，
            // 而是从池里取一条激活播放。因此断言「有飘字被激活」，而不是「子对象变多」。
            var activeBefore = CountActiveFloatingText(spawner);

            // 两颗 9 级相邻放置，生成当帧即接触合成。
            // 2026-09-13 水果等级 11→10 后顶级（10 级）不再合成，故下移一级取 9 级
            //（半径 0.88，圆心距 1.74 < 直径和 1.76 才会接触）。
            field.SpawnAt(9, new Vector2(-0.87f, 0f), out _);
            field.SpawnAt(9, new Vector2(0.87f, 0f), out _);

            yield return new WaitForSeconds(0.4f);

            // ParticleSystem 挂在 ParticleBurst 的子节点上（ParticleBurst 只持有引用）。
            var particleSystem = burst.GetComponentInChildren<ParticleSystem>();
            Assert.That(particleSystem, Is.Not.Null, "粒子爆发应持有 ParticleSystem");

            Assert.That(CountActiveFloatingText(spawner), Is.GreaterThan(activeBefore),
                "合成应激活飘字（证明 FeedbackDirector 的飘字引用已接线）");

            // 粒子存活数依赖 headless 下的粒子模拟，改用「引用已接线 + 已在播放」做确定性断言。
            Assert.That(particleSystem.isPlaying, Is.True,
                "粒子系统应已由 ParticleBurst 启动（证明粒子引用已接线）");

            var feedback = Find<FeedbackDirector>();
            Assert.That(feedback, Is.Not.Null);
            Assert.That(feedback.ScreenShakeTargetAvailable, Is.True,
                "震屏目标必须指向相机（否则震屏静默失效）");
        }

        /// <summary>
        /// 2026-09-13：水果图换成 `kenney_planets`（planet00 → 1 级 … planet09 → 10 级）。
        /// 断言「等级 → 贴图」的对应关系与「专属美术不染色」。
        ///
        /// <para>存在的理由：这组贴图是在 `GameBootstrapper` 里**运行时装载并注入**给
        /// `GameField` / `HudBinder` 的（占位图仍是缺图兜底），接错不会抛错，
        /// 只会静默地一直用占位圆片——正是那种「看起来能跑、但美术没生效」的缺陷。</para>
        /// </summary>
        [UnityTest]
        public IEnumerator SpawnedFruit_UsesItsLevelPlanetArt()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return LoadRealScene();

            var bootstrapper = Find<GameBootstrapper>();
            yield return WaitForRound(bootstrapper);

            var field = Find<GameField>();

            Assert.That(field.SpawnAt(1, Vector2.zero, out var level1Id), Is.True, "生成 1 级水果");
            Assert.That(field.SpawnAt(10, new Vector2(0f, 3f), out var level10Id), Is.True, "生成 10 级水果");
            yield return null;

            var level1 = FindFruitBodyById(level1Id);
            var level10 = FindFruitBodyById(level10Id);

            Assert.That(level1, Is.Not.Null);
            Assert.That(level10, Is.Not.Null);

            var renderer1 = level1.Visual.GetComponent<SpriteRenderer>();
            var renderer10 = level10.Visual.GetComponent<SpriteRenderer>();

            Assert.That(renderer1.sprite, Is.Not.Null, "1 级水果必须有贴图");
            Assert.That(renderer1.sprite.name, Is.EqualTo("planet00"), "1 级 → planet00（按 Planets 顺序依次对应）");
            Assert.That(renderer10.sprite.name, Is.EqualTo("planet09"), "10 级 → planet09");
            Assert.That(renderer1.color, Is.EqualTo(Color.white),
                "使用专属美术时不染色——染成调色板颜色会毁掉星球本身的可辨识度");

            var worldSize = renderer10.bounds.size;
            Assert.That(worldSize.x, Is.EqualTo(level10.Radius * 2f).Within(1e-3f),
                "10 级水果的视觉直径必须等于碰撞直径（1280px 素材按 PPU 隐式换算会大 12.8 倍）");
        }

        private static FruitBody FindFruitBodyById(int fruitId)
        {
            foreach (var body in Object.FindObjectsOfType<FruitBody>())
            {
                if (body.FruitId == fruitId)
                    return body;
            }

            return null;
        }
    }
}
