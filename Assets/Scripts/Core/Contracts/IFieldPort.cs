using System;
using UnityEngine;

namespace MergeWater.Core
{
    /// <summary>
    /// 局内流程访问物理场地的唯一端口（M2 实现，M3 消费）。
    /// 实现必须保证：失败时不产生副作用；<see cref="Merged"/> 每次成功合成恰好一次。
    /// </summary>
    public interface IFieldPort
    {
        /// <summary>当前存活水果数量。</summary>
        int LiveFruitCount { get; }

        /// <summary>警戒线世界 y（M5 用其绘制，M3 用其判定）。</summary>
        float DangerLineY { get; }

        /// <summary>在落点 x 生成指定等级的水果。失败（越界/超上限/未初始化）返回 false 且无副作用。</summary>
        bool Drop(int level, float x, out int fruitId);

        /// <summary>查询是否存在「已静止且越过警戒线」的水果，返回溢出量最大者。</summary>
        bool TryGetDangerViolation(out DangerViolation violation);

        /// <summary>按 id 移除水果。幂等；不存在返回 false。</summary>
        bool RemoveFruit(int fruitId);

        /// <summary>移除指定点半径内的全部水果，返回移除数量。</summary>
        int RemoveFruitInRadius(Vector2 point, float radius);

        /// <summary>指定点半径内是否有水果；用于道具命中判定，不产生副作用。</summary>
        bool HasFruitInRadius(Vector2 point, float radius);

        /// <summary>移除距离指定点最近的一颗水果（限定最大半径）。</summary>
        bool RemoveSingleNearest(Vector2 point, float maxRadius, out int removedId);

        /// <summary>移除全场最高水果所在的接触连通簇，最多 maxCount 颗，优先高等级。</summary>
        int RemoveHighestCluster(int maxCount);

        /// <summary>对全场水果施加小幅确定性随机冲量（同 seed 可复现），不直接消除任何水果。</summary>
        void ApplyShakeShuffle(float impulse, int seed);

        /// <summary>冻结/恢复仿真。冻结时缓存速度，恢复时还原。用于暂停、结算与广告播放。</summary>
        void SetSimulationEnabled(bool enabled);

        /// <summary>清理全场水果与投放记录（重开一局）。</summary>
        void ClearAll();

        /// <summary>成功合成时发布一次。</summary>
        event Action<MergeEvent> Merged;
    }
}
