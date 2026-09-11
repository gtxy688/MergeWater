using System;

namespace MergeWater.Core
{
    /// <summary>得分与合成可行性规则（V2.3/V2.7）。</summary>
    public static class ScoreRules
    {
        /// <summary>等级是否还能继续合成。最高级返回 false（R3、V2.7）。</summary>
        public static bool CanMerge(int level, GameBalance balance)
        {
            if (balance == null)
                return false;

            return balance.HasTier(level) && balance.HasTier(level + 1);
        }

        /// <summary>最高可合成等级。</summary>
        public static int MaxTier(GameBalance balance) => balance?.TierCount ?? 0;

        /// <summary>
        /// 一次合成的得分 = 目标等级的表值 × 当前倍率，四舍五入取整。
        /// 未知等级返回 0；倍率 ≤0 时按 1.0 处理（防御性）。
        /// </summary>
        public static int ScoreFor(int resultLevel, float multiplier, GameBalance balance)
        {
            if (balance == null)
                return 0;

            var tier = balance.GetTier(resultLevel);
            if (!tier.IsValid)
                return 0;

            var effective = multiplier <= 0f ? 1f : multiplier;
            return (int)Math.Round(tier.Score * effective, MidpointRounding.AwayFromZero);
        }
    }
}
