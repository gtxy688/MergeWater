using System;
using System.Collections.Generic;
using MergeWater.Core;

namespace MergeWater.Meta
{
    /// <summary>
    /// 本地排行榜（R18）。MVP 不接开放数据域，数据只来自本地存档；
    /// 接口按「好友分数也可提交」设计，V1 接入开放数据域时无需改调用方。
    /// </summary>
    public sealed class LeaderboardService : ILeaderboardService
    {
        public const string SelfName = "我";

        private readonly SaveData _data;
        private readonly int _maxEntries;

        public LeaderboardService(SaveData data, int maxEntries = 50)
        {
            _data = data ?? throw new ArgumentNullException(nameof(data));
            _maxEntries = Math.Max(1, maxEntries);
        }

        public int Count => _data.leaderboard?.Count ?? 0;

        /// <summary>自己是否已被他人超越（R21 红点）。本地单机时恒为 false，V1 接入好友数据后生效。</summary>
        public bool IsSelfBeaten
        {
            get
            {
                var selfScore = SelfBestScore();
                if (selfScore <= 0)
                    return false;

                for (var i = 0; i < Count; i++)
                {
                    var record = _data.leaderboard[i];
                    if (!IsSelf(record.name) && record.score > selfScore)
                        return true;
                }

                return false;
            }
        }

        public void Submit(string displayName, int score)
        {
            if (score <= 0)
                return;

            var name = string.IsNullOrEmpty(displayName) ? SelfName : displayName;

            for (var i = 0; i < Count; i++)
            {
                if (!string.Equals(_data.leaderboard[i].name, name, StringComparison.Ordinal))
                    continue;

                if (score <= _data.leaderboard[i].score)
                    return;

                var updated = _data.leaderboard[i];
                updated.score = score;
                _data.leaderboard[i] = updated;
                SortAndTrim();
                return;
            }

            _data.leaderboard.Add(new LeaderboardRecord(name, score));
            SortAndTrim();
        }

        public IReadOnlyList<LeaderboardEntry> GetTop(int count, int page = 1)
        {
            var result = new List<LeaderboardEntry>();
            if (count <= 0 || Count == 0)
                return result;

            var skip = Math.Max(0, (page - 1) * count);
            var end = Math.Min(Count, skip + count);

            for (var i = skip; i < end; i++)
            {
                var record = _data.leaderboard[i];
                result.Add(new LeaderboardEntry(record.name, record.score, IsSelf(record.name)));
            }

            return result;
        }

        public int SelfBestScore()
        {
            var best = 0;
            for (var i = 0; i < Count; i++)
            {
                if (IsSelf(_data.leaderboard[i].name))
                    best = Math.Max(best, _data.leaderboard[i].score);
            }

            return best;
        }

        private static bool IsSelf(string record) => string.Equals(record, SelfName, StringComparison.Ordinal);

        private void SortAndTrim()
        {
            _data.leaderboard.Sort((a, b) => b.score.CompareTo(a.score));

            while (_data.leaderboard.Count > _maxEntries)
                _data.leaderboard.RemoveAt(_data.leaderboard.Count - 1);
        }
    }
}
