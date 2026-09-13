using UnityEngine;

namespace MergeWater.Core
{
    /// <summary>
    /// 屏幕可用区域与系统遮挡（刘海 / 灵动岛 / 挖孔 / 微信右上角胶囊）。
    ///
    /// <para><b>坐标一律是 Unity 约定</b>：屏幕像素、原点在左下角。平台差异
    /// （微信 <c>wx.getWindowInfo().safeArea</c> 的原点在左上角、且单位是 CSS 逻辑像素而非设备像素）
    /// 全部由适配器吸收，上层只消费这里的 <see cref="Rect"/>。</para>
    ///
    /// <para>为什么需要它：设计稿只能按「假定胶囊区」预留固定高度，而真机（尤其带刘海/灵动岛的 iPhone）
    /// 的实际遮挡更低（2026-09-13 需求方真机截图：中间分数被刘海挡住、右上齿轮与微信胶囊重叠）。</para>
    /// </summary>
    public interface ISafeAreaSource
    {
        /// <summary>安全区（已扣除刘海/灵动岛/挖孔）。无可用信息时返回整屏。</summary>
        Rect SafeArea { get; }

        /// <summary>微信右上角胶囊按钮的矩形（Unity 约定）。非微信平台返回 false。</summary>
        bool TryGetMenuButton(out Rect rect);
    }
}
