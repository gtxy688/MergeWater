using System;
using MergeWater.Core;

namespace MergeWater.Meta
{
    /// <summary>
    /// 每日领取记录（V2.13–V2.15）。以本地日期键为准，跨天首次访问自动清零。
    /// </summary>
    public sealed class DailyLimitService
    {
        private readonly SaveData _data;
        private readonly IClock _clock;

        public DailyLimitService(SaveData data, IClock clock)
        {
            _data = data ?? throw new ArgumentNullException(nameof(data));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        }

        /// <summary>跨天时清零计数；返回是否发生了重置。</summary>
        public bool EnsureCurrentDay()
        {
            var today = _clock.Today;
            if (string.Equals(_data.dailyKey, today, StringComparison.Ordinal))
                return false;

            _data.dailyKey = today;
            _data.undoGrantedToday = 0;
            _data.bombGrantedToday = 0;
            _data.hammerGrantedToday = 0;
            _data.shakeGrantedToday = 0;
            return true;
        }

        public int GrantedToday(ItemKind kind)
        {
            EnsureCurrentDay();
            switch (kind)
            {
                case ItemKind.Undo: return _data.undoGrantedToday;
                case ItemKind.Bomb: return _data.bombGrantedToday;
                case ItemKind.Hammer: return _data.hammerGrantedToday;
                case ItemKind.Shake: return _data.shakeGrantedToday;
                default: return 0;
            }
        }

        public void RecordGrant(ItemKind kind)
        {
            EnsureCurrentDay();
            switch (kind)
            {
                case ItemKind.Undo: _data.undoGrantedToday++; break;
                case ItemKind.Bomb: _data.bombGrantedToday++; break;
                case ItemKind.Hammer: _data.hammerGrantedToday++; break;
                case ItemKind.Shake: _data.shakeGrantedToday++; break;
            }
        }

        /// <summary>按 GameBalance 的上限计算今日剩余领取次数。</summary>
        public int Remaining(ItemKind kind, GameBalance balance)
        {
            var cap = balance?.GetDailyCap(kind) ?? 0;
            return Math.Max(0, cap - GrantedToday(kind));
        }

    }

    /// <summary>道具库存（V2.13–V2.15）。领取受每日上限约束，使用即时且无冷却。</summary>
    public sealed class InventoryService
    {
        private readonly SaveData _data;
        private readonly DailyLimitService _daily;

        public InventoryService(SaveData data, DailyLimitService daily)
        {
            _data = data ?? throw new ArgumentNullException(nameof(data));
            _daily = daily ?? throw new ArgumentNullException(nameof(daily));
        }

        public int Count(ItemKind kind)
        {
            switch (kind)
            {
                case ItemKind.Undo: return _data.undoCount;
                case ItemKind.Bomb: return _data.bombCount;
                case ItemKind.Hammer: return _data.hammerCount;
                case ItemKind.Shake: return _data.shakeCount;
                default: return 0;
            }
        }

        public int RemainingToday(ItemKind kind, GameBalance balance) => _daily.Remaining(kind, balance);
        public GrantResult Grant(ItemKind kind, GameBalance balance)
        {
            if (kind == ItemKind.None)
                return GrantResult.Failed;

            if (_daily.Remaining(kind, balance) <= 0)
                return GrantResult.DailyCapReached;

            if (Count(kind) >= SaveData.MaxItemCount)
                return GrantResult.Failed;

            Add(kind, 1);
            _daily.RecordGrant(kind);
            return GrantResult.Granted;
        }

        public SpendResult Spend(ItemKind kind)
        {
            if (Count(kind) <= 0)
                return SpendResult.NoStock;

            Add(kind, -1);
            return SpendResult.Spent;
        }

        /// <summary>阶段目标等一次性奖励：不受每日上限约束。</summary>
        public GrantResult GrantUnlimited(ItemKind kind)
        {
            if (kind == ItemKind.None || Count(kind) >= SaveData.MaxItemCount)
                return GrantResult.Failed;

            Add(kind, 1);
            return GrantResult.Granted;
        }

        private void Add(ItemKind kind, int delta)
        {
            switch (kind)
            {
                case ItemKind.Undo: _data.undoCount = Clamp(_data.undoCount + delta); break;
                case ItemKind.Bomb: _data.bombCount = Clamp(_data.bombCount + delta); break;
                case ItemKind.Hammer: _data.hammerCount = Clamp(_data.hammerCount + delta); break;
                case ItemKind.Shake: _data.shakeCount = Clamp(_data.shakeCount + delta); break;
            }
        }

        private static int Clamp(int value)
        {
            if (value < 0) return 0;
            if (value > SaveData.MaxItemCount) return SaveData.MaxItemCount;
            return value;
        }
    }
}
