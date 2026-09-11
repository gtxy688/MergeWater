using System;
using System.Collections.Generic;

namespace MergeWater.Core
{
    /// <summary>
    /// 阶段目标进度（V2.18）。里程碑节点由 <see cref="GameBalance.StageMilestones"/> 提供，
    /// 每个节点每局只发放一次。
    /// </summary>
    public sealed class StageProgress
    {
        private readonly StageMilestone[] _milestones;
        private int _nextIndex;

        public StageProgress(GameBalance balance)
        {
            _milestones = balance?.StageMilestones ?? Array.Empty<StageMilestone>();
        }

        /// <summary>已发放到的里程碑下标（即已达成的节点数）。</summary>
        public int ReachedCount => _nextIndex;

        public int MilestoneCount => _milestones.Length;

        public void Reset() => _nextIndex = 0;

        /// <summary>
        /// 依据当前分数返回本次新达成的里程碑，追加到 <paramref name="reached"/>（先清空）。
        /// 一次跨越多个节点时全部返回。
        /// </summary>
        public int Advance(int score, List<StageMilestone> reached)
        {
            reached?.Clear();
            var count = 0;
            while (_nextIndex < _milestones.Length && score >= _milestones[_nextIndex].Score)
            {
                reached?.Add(_milestones[_nextIndex]);
                _nextIndex++;
                count++;
            }

            return count;
        }

        /// <summary>下一个未达成节点；全部达成返回 false。</summary>
        public bool TryGetNext(out StageMilestone milestone)
        {
            if (_nextIndex < _milestones.Length)
            {
                milestone = _milestones[_nextIndex];
                return true;
            }

            milestone = default;
            return false;
        }

        /// <summary>进度条的归一化进度：以上一节点为 0、下一节点为 1；全部达成返回 1。</summary>
        public float GetNormalizedProgress(int score)
        {
            var from = 0;
            for (var i = 0; i < _milestones.Length; i++)
            {
                if (score >= _milestones[i].Score)
                {
                    from = _milestones[i].Score;
                    continue;
                }

                var span = _milestones[i].Score - from;
                if (span <= 0)
                    return 1f;

                return Clamp01((score - from) / (float)span);
            }

            return 1f;
        }

        private static float Clamp01(float value)
        {
            if (value < 0f) return 0f;
            if (value > 1f) return 1f;
            return value;
        }
    }
}
