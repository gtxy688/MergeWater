using System;
using System.Collections.Generic;

namespace MergeWater.Core
{
    /// <summary>
    /// 待投等级队列（V2.4–V2.6）。同一 seed 序列可复现。
    /// 投数 &lt;20 时仅 1–2 级；20–59 投为 1–3 级；≥60 投保持同一等级池与权重不变。
    /// </summary>
    public sealed class DropQueue
    {
        private readonly GameBalance _balance;
        private readonly Random _rng;
        private readonly List<FruitTierDefinition> _pool = new List<FruitTierDefinition>(3);

        public DropQueue(GameBalance balance, int seed)
        {
            _balance = balance ?? throw new ArgumentNullException(nameof(balance));
            _rng = new Random(seed);
        }

        /// <summary>本次待投等级池的最大等级。</summary>
        public int MaxLevelForDropIndex(int dropIndex) =>
            dropIndex < _balance.EarlyPhaseDropCount ? 2 : 3;

        /// <summary>按已完成的投数取下一颗等级。</summary>
        public int LevelForDropIndex(int dropIndex)
        {
            var maxLevel = Math.Min(MaxLevelForDropIndex(dropIndex), _balance.TierCount);
            BuildPool(maxLevel);

            if (_pool.Count == 0)
                return GameBalance.MinTier;

            var total = 0f;
            for (var i = 0; i < _pool.Count; i++)
                total += _pool[i].DropWeight;

            if (total <= 0f)
                return _pool[0].Level;

            var roll = (float)_rng.NextDouble() * total;
            var accum = 0f;
            for (var i = 0; i < _pool.Count; i++)
            {
                accum += _pool[i].DropWeight;
                if (roll < accum)
                    return _pool[i].Level;
            }

            return _pool[_pool.Count - 1].Level;
        }

        private void BuildPool(int maxLevel)
        {
            _pool.Clear();
            for (var level = GameBalance.MinTier; level <= maxLevel; level++)
            {
                var tier = _balance.GetTier(level);
                if (tier.IsValid && tier.DropWeight > 0f)
                    _pool.Add(tier);
            }
        }
    }
}
