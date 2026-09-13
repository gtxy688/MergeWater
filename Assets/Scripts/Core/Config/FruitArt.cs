using UnityEngine;

namespace MergeWater.Core
{
    /// <summary>
    /// 「等级 → 水果外观」的唯一取用规则。M2 生成水果、M5 待投预览都从这里取图与染色，
    /// 避免同一条规则在两处各写一份而漂移。
    ///
    /// <para>数据由组合根（M7）在运行时注入：每级一张正式美术；缺图（或越界）时回退到
    /// 占位圆片 + <see cref="FruitPalette"/> 染色，保住 D4 的「颜色 + 半径双重区分等级」。</para>
    ///
    /// <para>注意：这里只负责「用哪张图、染不染色」，不负责缩放。视觉缩放由
    /// <c>FruitBody</c> 按贴图的实际世界尺寸归一化到碰撞半径——因此素材换成任何
    /// 像素尺寸 / PPU 都不需要改代码。</para>
    /// </summary>
    public sealed class FruitArt
    {
        private readonly Sprite[] _sprites;

        public FruitArt(Sprite[] sprites, Sprite fallback)
        {
            _sprites = sprites ?? System.Array.Empty<Sprite>();
            Fallback = fallback;
        }

        /// <summary>缺图时的兜底贴图（程序化占位圆片）。</summary>
        public Sprite Fallback { get; }

        /// <summary>已配置专属美术的等级数。</summary>
        public int Count => _sprites.Length;

        /// <summary>该等级是否有专属美术。</summary>
        public bool HasArt(int level) =>
            level >= GameBalance.MinTier && level <= _sprites.Length && _sprites[level - 1] != null;

        /// <summary>该等级使用的贴图；无专属美术或越界时回退到兜底图。</summary>
        public Sprite ForLevel(int level) => HasArt(level) ? _sprites[level - 1] : Fallback;

        /// <summary>
        /// 该等级的染色。**专属美术按原色显示**（白 = 不染色，10 颗各自的颜色就是等级标识）；
        /// 只有回退到占位圆片时才用调色板染色——否则星球会被染成单色，反而更难分辨。
        /// </summary>
        public Color TintForLevel(int level) =>
            HasArt(level) ? Color.white : FruitPalette.ForLevel(level);

        /// <summary>
        /// 按 `planet00..planet09` 的命名约定装载正式水果美术（等级 1 对应 planet00，依次顺延）。
        /// 单张缺失不抛异常，返回 null 由 <see cref="ForLevel"/> 走回退路径。
        /// </summary>
        public static Sprite[] LoadPlanetSprites(string folder = "kenney_planets/Planets", int count = 10)
        {
            var sprites = new Sprite[count];
            for (var i = 0; i < count; i++)
                sprites[i] = Resources.Load<Sprite>($"{folder}/planet{i:00}");

            return sprites;
        }
    }
}
