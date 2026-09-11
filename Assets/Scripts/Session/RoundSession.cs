using System;
using System.Collections.Generic;
using MergeWater.Core;

namespace MergeWater.Session
{
    /// <summary>
    /// 局内流程：状态机、得分与连击、越线 1.2s 判负、复活次数与状态应用、阶段目标与投放队列。
    /// 纯 C# 对象；只通过 <see cref="IFieldPort"/> 与场地交互，因此可用 Fake 在 EditMode 完整测试。
    /// 广告本身由 M6/M7 负责：本类只校验「每局 1 次」并应用复活结果，避免重复播放广告。
    /// </summary>
    public sealed class RoundSession : ISessionView, IDisposable
    {
        private readonly GameBalance _balance;
        private readonly IFieldPort _field;
        private readonly IAnalyticsService _analytics;
        private readonly Action<int> _onBestScoreChanged;
        private readonly Action<StageMilestone> _onMilestoneReward;

        private readonly ComboTracker _combo;
        private readonly DropQueue _queue;
        private readonly StageProgress _stage;
        private readonly List<StageMilestone> _milestoneBuffer = new List<StageMilestone>(4);

        private RoundPhase _phase = RoundPhase.Booting;
        private int _score;
        private int _bestScore;
        private int _dropCount;
        private int _currentLevel;
        private int _nextLevel;
        private int _revivesUsed;
        private int _lastPublishedCombo;
        private float _dangerTimer;
        private float _dropCooldown;
        private bool _dangerActive;
        private bool _firstDropTracked;
        private bool _disposed;

        public RoundSession(
            GameBalance balance,
            IFieldPort field,
            IAnalyticsService analytics,
            int initialBestScore,
            int seed,
            Action<int> onBestScoreChanged = null,
            Action<StageMilestone> onMilestoneReward = null)
        {
            _balance = balance ?? throw new ArgumentNullException(nameof(balance));
            _field = field ?? throw new ArgumentNullException(nameof(field));
            _analytics = analytics;
            _onBestScoreChanged = onBestScoreChanged;
            _onMilestoneReward = onMilestoneReward;

            _bestScore = Math.Max(0, initialBestScore);
            _combo = new ComboTracker(_balance);
            _queue = new DropQueue(_balance, seed);
            _stage = new StageProgress(_balance);

            _field.Merged += OnFieldMerged;
        }

        public SessionEvents Events { get; } = new SessionEvents();

        public GameBalance Balance => _balance;

        public RoundPhase Phase => _phase;

        public int Score => _score;

        public int BestScore => _bestScore;

        public int DropCount => _dropCount;

        public int CurrentLevel => _currentLevel;

        public int NextLevel => _nextLevel;

        public float DropCooldownRemaining => _dropCooldown;

        public int RevivesUsed => _revivesUsed;

        /// <summary>仅 Playing 阶段允许使用道具。</summary>
        public bool CanAct => _phase == RoundPhase.Playing;

        /// <summary>是否接受投放：Ready（首次投放）与 Playing 均允许。</summary>
        public bool CanAcceptDrop => _phase == RoundPhase.Ready || _phase == RoundPhase.Playing;

        /// <summary>本局是否还有复活次数且处于失败页（广告冷却由 M7 另行判断）。</summary>
        public bool CanRevive => _phase == RoundPhase.Reviving && _revivesUsed < _balance.RevivePerRound;

        public RoundSnapshot Snapshot => new RoundSnapshot(
            _score, _bestScore, _combo.Combo, _combo.Multiplier, _phase,
            _dropCount, _currentLevel, _nextLevel, _dangerActive, CanRevive,
            _stage.ReachedCount);

        public void SetBestScore(int bestScore) => _bestScore = Math.Max(_bestScore, bestScore);

        public void StartRound()
        {
            _combo.Reset();
            _stage.Reset();
            _score = 0;
            _dropCount = 0;
            _revivesUsed = 0;
            _dangerTimer = 0f;
            _dropCooldown = 0f;
            _dangerActive = false;
            _lastPublishedCombo = 0;
            _firstDropTracked = false;
            _currentLevel = _queue.LevelForDropIndex(0);
            _nextLevel = _queue.LevelForDropIndex(1);

            _field.ClearAll();
            _field.SetSimulationEnabled(true);

            SetPhase(RoundPhase.Ready);
        }

        public void Tick(float deltaSeconds)
        {
            if (deltaSeconds <= 0f || _phase != RoundPhase.Playing)
                return;

            if (_dropCooldown > 0f)
                _dropCooldown = Math.Max(0f, _dropCooldown - deltaSeconds);

            _combo.Advance(deltaSeconds);
            PublishComboIfChanged();

            if (_field.TryGetDangerViolation(out var violation))
            {
                if (!_dangerActive)
                {
                    _dangerActive = true;
                    _dangerTimer = 0f;
                    Events.PublishDangerStarted(violation);
                    Track(AnalyticsEventNames.DangerStart, AnalyticsParam.Int("fruit", violation.FruitId));
                }

                _dangerTimer += deltaSeconds;

                if (_dangerTimer >= _balance.DangerHoldSeconds)
                    ExpireDanger();
            }
            else if (_dangerActive)
            {
                _dangerActive = false;
                _dangerTimer = 0f;
                Events.PublishDangerEnded();
                Track(AnalyticsEventNames.DangerEnd);
            }
        }

        public bool ReleaseDrop(float x)
        {
            if (_phase == RoundPhase.Ready)
                SetPhase(RoundPhase.Playing);

            if (_phase != RoundPhase.Playing || _dropCooldown > 0f)
                return false;

            if (!_field.Drop(_currentLevel, x, out var fruitId))
                return false;

            var level = _currentLevel;
            Events.PublishDropPerformed(new DropPerformedEvent(fruitId, level, x, _dropCount));

            if (!_firstDropTracked)
            {
                _firstDropTracked = true;
                Track(AnalyticsEventNames.FirstDrop, AnalyticsParam.Int("level", level));
            }

            _dropCount++;
            _dropCooldown = _balance.DropCooldownSeconds;
            _currentLevel = _nextLevel;
            _nextLevel = _queue.LevelForDropIndex(_dropCount + 1);
            return true;
        }

        /// <summary>
        /// 应用一次复活（广告已由 M7 播放并确认完成）。任何失败都不改变局状态。
        /// </summary>
        public ReviveOutcome ApplyRevive()
        {
            if (_phase != RoundPhase.Reviving)
                return ReviveOutcome.NotInGameOver;

            if (_revivesUsed >= _balance.RevivePerRound)
                return ReviveOutcome.AlreadyUsed;

            _revivesUsed++;
            var removed = _field.RemoveHighestCluster(_balance.ReviveClusterMax);

            _dangerTimer = 0f;
            _dangerActive = false;
            _combo.Reset();
            PublishComboIfChanged();

            _field.SetSimulationEnabled(true);
            SetPhase(RoundPhase.Playing);

            Track(AnalyticsEventNames.ReviveComplete, AnalyticsParam.Int("removed", removed));
            Events.PublishReviveResolved(ReviveOutcome.Applied);
            return ReviveOutcome.Applied;
        }

        public void EndRound()
        {
            if (_phase == RoundPhase.GameOver)
                return;

            if (_dangerActive)
            {
                _dangerActive = false;
                _dangerTimer = 0f;
                Events.PublishDangerEnded();
                Track(AnalyticsEventNames.DangerEnd);
            }

            _field.SetSimulationEnabled(false);
            SetPhase(RoundPhase.GameOver);

            if (_score > 0)
                _onBestScoreChanged?.Invoke(_bestScore);

            Track(AnalyticsEventNames.RoundEnd,
                AnalyticsParam.Int("score", _score),
                AnalyticsParam.Int("best", _bestScore),
                AnalyticsParam.Int("drops", _dropCount));
        }

        /// <summary>M7 在道具实际生效/被拒后调用，用于统一发布事件与埋点。</summary>
        public void NotifyItemUsed(ItemKind kind, ItemUseResult result)
        {
            Events.PublishItemUsed(kind, result);

            if (result == ItemUseResult.Applied)
                Track(AnalyticsEventNames.ItemUse, AnalyticsParam.Text("item", kind.ToString()));
        }

        /// <summary>开启道具瞄准前调用：仅 Playing 且库存充足时允许。</summary>
        public bool CanUseItem() => _phase == RoundPhase.Playing;

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _field.Merged -= OnFieldMerged;
        }

        private void OnFieldMerged(MergeEvent evt)
        {
            // 复活清场或结算期间的残留事件不计分（验收 E4）。
            if (_phase != RoundPhase.Playing)
                return;

            _combo.RegisterMerge();
            var multiplier = _combo.Multiplier;
            var delta = ScoreRules.ScoreFor(evt.ResultLevel, multiplier, _balance);

            _score += delta;
            if (_score > _bestScore)
            {
                _bestScore = _score;
                _onBestScoreChanged?.Invoke(_bestScore);
            }

            Events.PublishMerged(evt);
            Events.PublishScored(new ScoreEvent(delta, _score, multiplier, _combo.Combo, evt.ResultLevel, evt.Position));
            Events.PublishComboChanged(_combo.Combo);
            _lastPublishedCombo = _combo.Combo;

            Track(AnalyticsEventNames.Merge,
                AnalyticsParam.Int("tier", evt.ResultLevel),
                AnalyticsParam.Int("combo", _combo.Combo));

            var reached = _stage.Advance(_score, _milestoneBuffer);
            for (var i = 0; i < reached; i++)
            {
                var milestone = _milestoneBuffer[i];
                Events.PublishMilestoneReached(milestone);
                _onMilestoneReward?.Invoke(milestone);
                Track(AnalyticsEventNames.MilestoneReached,
                    AnalyticsParam.Int("score", milestone.Score),
                    AnalyticsParam.Text("reward", milestone.Reward.ToString()));
            }
        }

        private void ExpireDanger()
        {
            _dangerActive = false;
            _dangerTimer = 0f;
            Events.PublishDangerEnded();
            Track(AnalyticsEventNames.DangerEnd, AnalyticsParam.Text("result", "failed"));

            _field.SetSimulationEnabled(false);

            // 还有复活次数时停在失败页（Reviving），否则直接结算。
            SetPhase(_revivesUsed < _balance.RevivePerRound ? RoundPhase.Reviving : RoundPhase.GameOver);

            if (_phase == RoundPhase.GameOver)
            {
                Track(AnalyticsEventNames.RoundEnd,
                    AnalyticsParam.Int("score", _score),
                    AnalyticsParam.Int("best", _bestScore),
                    AnalyticsParam.Int("drops", _dropCount));
            }
        }

        private void PublishComboIfChanged()
        {
            if (_lastPublishedCombo == _combo.Combo)
                return;

            _lastPublishedCombo = _combo.Combo;
            Events.PublishComboChanged(_combo.Combo);
        }

        private void SetPhase(RoundPhase next)
        {
            if (_phase == next)
                return;

            var previous = _phase;
            _phase = next;
            Events.PublishPhaseChanged(previous, next);
        }

        private void Track(string eventName, params AnalyticsParam[] parameters)
        {
            _analytics?.Track(eventName, parameters);
        }
    }
}
