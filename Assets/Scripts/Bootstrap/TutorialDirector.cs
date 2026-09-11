using MergeWater.Core;
using MergeWater.Presentation;
using MergeWater.Session;
using UnityEngine;
using UnityEngine.UI;

namespace MergeWater.Bootstrap
{
    /// <summary>
    /// 新手引导（R23）：第 1 局显示拖动箭头与文案，第一次合成高亮提示，越线前预警，
    /// 前 3 局结算页提示分享与排行。标记写回存档，前 3 局后不再打扰。
    /// </summary>
    public sealed class TutorialDirector : MonoBehaviour
    {
        [SerializeField] private PanelController panels;
        [SerializeField] private Image arrow;

        private GameContext _context;
        private RoundSession _subscribedSession;
        private bool _dropHintShown;
        private bool _mergeHintShown;
        private bool _dangerHintShown;
        private bool _arrowVisible;
        private float _bobPhase;
        private Vector2 _arrowBasePosition;

        public bool ArrowVisible => _arrowVisible;

        public void Configure(PanelController panelController, Image arrowImage)
        {
            panels = panelController;
            arrow = arrowImage;
            if (arrow != null)
            {
                _arrowBasePosition = arrow.rectTransform.anchoredPosition;
                arrow.gameObject.SetActive(false);
            }
        }

        public void Initialize(GameContext context)
        {
            _context = context;
        }

        public void OnRoundStarted(RoundSession session, int roundsSeen)
        {
            if (session == null)
                return;

            Unsubscribe();

            _subscribedSession = session;
            session.Events.Merged += OnMerged;
            session.Events.DangerStarted += OnDangerStarted;

            var firstRound = roundsSeen <= 0;
            SetArrowVisible(firstRound);

            if (firstRound && !_dropHintShown)
            {
                _dropHintShown = true;
                Show("按住屏幕左右拖动选落点，松手投放", 2.8f);
                _context?.MarkTutorialFlags(true, _mergeHintShown);
            }
        }

        public void OnDropPerformed(RoundSession session)
        {
            SetArrowVisible(false);
        }

        /// <summary>结算页出现时调用（前 3 局）。</summary>
        public void OnSettlementShown(int roundsSeen)
        {
            if (roundsSeen >= 3)
                return;

            Show("试试「分享挑战」和「好友排行」", 2.4f);
        }

        public void ResetHintsForTesting()
        {
            _dropHintShown = false;
            _mergeHintShown = false;
            _dangerHintShown = false;
        }

        private void OnMerged(MergeEvent evt)
        {
            if (_mergeHintShown)
                return;

            _mergeHintShown = true;
            Show("同级水果相撞会合成更大！", 2.4f);
            _context?.MarkTutorialFlags(_dropHintShown, true);
        }

        private void OnDangerStarted(DangerViolation violation)
        {
            if (_dangerHintShown)
                return;

            _dangerHintShown = true;
            Show("小心警戒线：静止越线满 1.2 秒就失败", 2.6f);
        }

        private void Show(string message, float seconds)
        {
            panels?.ShowToast(message, seconds);
        }

        private void SetArrowVisible(bool visible)
        {
            _arrowVisible = visible;
            if (arrow != null)
                arrow.gameObject.SetActive(visible);
        }

        private void Update()
        {
            if (!_arrowVisible || arrow == null)
                return;

            _bobPhase += Time.unscaledDeltaTime * 4f;
            var offset = Mathf.Sin(_bobPhase) * 26f;
            arrow.rectTransform.anchoredPosition = _arrowBasePosition + new Vector2(0f, offset);
        }

        private void OnDisable() => SetArrowVisible(false);

        private void Unsubscribe()
        {
            if (_subscribedSession == null)
                return;

            _subscribedSession.Events.Merged -= OnMerged;
            _subscribedSession.Events.DangerStarted -= OnDangerStarted;
            _subscribedSession = null;
        }

        private void OnDestroy() => Unsubscribe();
    }
}
