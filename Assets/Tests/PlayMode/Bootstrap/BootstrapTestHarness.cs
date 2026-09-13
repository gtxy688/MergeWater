using System.Collections;
using MergeWater.Aim;
using MergeWater.Bootstrap;
using MergeWater.Core;
using MergeWater.Field;
using MergeWater.Meta;
using MergeWater.Presentation;
using UnityEngine;
using UnityEngine.TestTools;

namespace MergeWater.Tests.PlayMode
{
    /// <summary>
    /// M7 集成测试脚手架：在未激活的根节点上先注入测试接缝（内存存档、可控广告、内存埋点），
    /// 再激活以触发 <c>Awake</c>/<c>Start</c>，从而完整走真实启动流程。
    /// </summary>
    internal sealed class BootstrapTestHarness
    {
        public GameObject Root;
        public InMemorySaveStore Store;
        public FixedClock Clock;
        public InMemoryAnalyticsSink Analytics;
        public GameBootstrapper Bootstrapper;
        public PresentationHarness Presentation;

        public GameContext Context => Bootstrapper != null ? Bootstrapper.Context : null;

        public HudView View => Presentation.View;

        public PanelController Panels => Presentation.Panels;

        public GameField Field { get; private set; }

        /// <param name="gravity">
        /// 默认使用较强重力，让水果快速落到地面以下，避免「停在警戒线上方」误触发判负；
        /// 需要测试越线判负时传 0，让水果静止悬停在警戒线上方。
        /// </param>
        /// <param name="requirePrivacyConsent">
        /// 是否要求隐私同意后才开局。默认 false（与 Demo 一致：加载页结束后直接开局）；
        /// 需要验证 R20 门控的用例显式传 true。
        /// </param>
        public static BootstrapTestHarness Create(bool privacyAccepted, float gravity = -60f,
            bool requirePrivacyConsent = false)
        {
            var harness = new BootstrapTestHarness();

            var data = SaveData.CreateDefault();
            data.privacyAccepted = privacyAccepted;

            harness.Store = new InMemorySaveStore(JsonUtility.ToJson(data));
            harness.Clock = new FixedClock();
            harness.Analytics = new InMemoryAnalyticsSink();

            Physics2D.gravity = new Vector2(0f, gravity);

            harness.Root = new GameObject("BootstrapTestRoot");
            harness.Root.SetActive(false);

            harness.Presentation = PresentationHarness.Create(harness.Root.transform);
            harness.Presentation.CreateBinder();

            var fieldGo = new GameObject("Field");
            fieldGo.transform.SetParent(harness.Root.transform, false);
            harness.Field = fieldGo.AddComponent<GameField>();

            var items = harness.Root.AddComponent<ItemUseController>();
            var tutorial = harness.Root.AddComponent<TutorialDirector>();

            var aim = harness.Root.AddComponent<AimController>();
            aim.enabled = false; // 输入由测试直接驱动，避免真实 Input 干扰
            aim.SetCamera(harness.Presentation.Camera);
            aim.SetDropBounds(-2.2f, 2.2f);
            aim.SetDropGeometry(5.2f, -4.2f, -9.81f, 0.8f);

            var bootstrapper = harness.Root.AddComponent<GameBootstrapper>();
            bootstrapper.SaveStoreOverride = harness.Store;
            bootstrapper.ClockOverride = harness.Clock;
            bootstrapper.AnalyticsSinkOverride = harness.Analytics;
            bootstrapper.RequirePrivacyConsent = requirePrivacyConsent;
            bootstrapper.LoadingMinSecondsOverride = 0f; // 测试不需要真的等加载页
            bootstrapper.RequireTapToStart = false;       // 测试不点「开始」，进度满即开局
            bootstrapper.ConfigureReferences(
                harness.Field,
                aim,
                harness.Presentation.Root.GetComponent<HudBinder>(),
                harness.Presentation.Panels,
                harness.Presentation.Audio,
                harness.Presentation.Feedback,
                tutorial,
                items,
                null,
                null,
                harness.Presentation.Camera);

            harness.Bootstrapper = bootstrapper;
            return harness;
        }

        /// <summary>激活根节点让 Awake/Start 运行，并等加载页流程结束（Demo 版结束后直接开局）。</summary>
        public IEnumerator Activate()
        {
            Root.SetActive(true);
            yield return null;

            var deadline = Time.realtimeSinceStartup + 5f;
            while (Time.realtimeSinceStartup < deadline && Panels != null && Panels.CurrentPanel == PanelId.Loading)
                yield return null;

            // 开局当帧输入仍被屏蔽（下一帧才按面板/广告状态放行），等它真的可交互，
            // 否则测试直接驱动 AimController 会被忽略。停在隐私弹窗时不等待。
            var aim = Root.GetComponent<AimController>();
            var interactableDeadline = Time.realtimeSinceStartup + 5f;
            while (Time.realtimeSinceStartup < interactableDeadline
                   && Panels != null && Panels.CurrentPanel == PanelId.None
                   && aim != null && !aim.Interactable)
                yield return null;
        }

        public MockAdsService GetMockAds() => Context?.Ads.Inner as MockAdsService;

        /// <summary>通过真实输入路径投放一次（按下 → 松手）。</summary>
        public void DropOnce()
        {
            var aim = Root.GetComponent<MergeWater.Aim.AimController>();
            var screen = Presentation.Camera.WorldToScreenPoint(Vector3.zero);
            var point = new Vector2(screen.x, screen.y);

            aim.HandleFrame(new MergeWater.Aim.PointerFrame(true, true, false, true, point));
            aim.HandleFrame(new MergeWater.Aim.PointerFrame(false, false, true, true, point));
        }

        public void Dispose()
        {
            if (Root != null)
                Object.Destroy(Root);

            Presentation?.Dispose();
            Presentation = null;
            Root = null;
            Bootstrapper = null;
            Physics2D.gravity = new Vector2(0f, -9.81f);
        }
    }

    /// <summary>可推进的固定时钟（PlayMode 版本）。</summary>
    internal sealed class FixedClock : IClock
    {
        public System.DateTime Now { get; set; } =
            new System.DateTime(2026, 9, 11, 10, 0, 0, System.DateTimeKind.Local);

        public string Today => Now.ToString("yyyy-MM-dd");

        public void Advance(System.TimeSpan delta) => Now += delta;
    }
}
