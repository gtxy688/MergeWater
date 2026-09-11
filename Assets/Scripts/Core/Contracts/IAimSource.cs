using System;
using UnityEngine;

namespace MergeWater.Core
{
    /// <summary>
    /// 瞄准输入端口（M4 实现，M3/M7 消费）。实现必须保证：
    /// 每次松手最多发出一次 <see cref="DropRequested"/> 或 <see cref="ItemTargetRequested"/>；
    /// <see cref="SetInteractable"/> 为 false 时取消瞄准且不发指令。
    /// </summary>
    public interface IAimSource
    {
        /// <summary>当前瞄准状态，供表现层绘制预览。</summary>
        AimState State { get; }

        /// <summary>松手投放（世界 x）。</summary>
        event Action<float> DropRequested;

        /// <summary>道具瞄准松手（种类 + 世界点）。</summary>
        event Action<ItemKind, Vector2> ItemTargetRequested;

        /// <summary>设置当前待投水果半径，影响落点夹取。</summary>
        void SetDropRadius(float radius);

        /// <summary>设置可放置的 x 区间。</summary>
        void SetDropBounds(float minX, float maxX);

        /// <summary>启用/禁用交互；禁用时取消瞄准且不发指令。</summary>
        void SetInteractable(bool interactable);

        /// <summary>进入道具瞄准模式。</summary>
        void BeginItemAim(ItemKind kind, float radius);

        /// <summary>取消道具瞄准（不产生副作用）。</summary>
        void CancelItemAim();

        /// <summary>
        /// 采样当前落点的预测路径（垂直下落 + 镜像反弹，V2.26）；返回采样点数，写入 <paramref name="points"/>。
        /// 表现层用其绘制虚线，不自行推算物理。
        /// </summary>
        int BuildPreview(System.Collections.Generic.List<Vector2> points);
    }
}
