using UnityEngine;

namespace MergeWater.Core
{
    /// <summary>
    /// 占位美术色板（决策 D4）。颜色 + 半径双重区分等级，满足色弱可辨要求；
    /// 替换为正式美术时只需替换 sprite 与色板，不改调用方。
    /// </summary>
    public static class FruitPalette
    {
        private static readonly Color[] Colors =
        {
            new Color(0.56f, 0.27f, 0.68f), // 1 葡萄
            new Color(0.91f, 0.30f, 0.24f), // 2 樱桃
            new Color(0.90f, 0.49f, 0.13f), // 3 橘子
            new Color(0.95f, 0.77f, 0.06f), // 4 柠檬
            new Color(0.49f, 0.70f, 0.26f), // 5 猕猴桃
            new Color(0.80f, 0.26f, 0.21f), // 6 番茄
            new Color(0.95f, 0.58f, 0.54f), // 7 桃子
            new Color(0.83f, 0.67f, 0.05f), // 8 菠萝
            new Color(0.63f, 0.25f, 0.00f), // 9 椰子
            new Color(0.13f, 0.60f, 0.33f)  // 10 西瓜（2026-09-13 起为顶级，原 11 大西瓜已移除）
        };

        public static Color ForLevel(int level)
        {
            if (level < GameBalance.MinTier || level > Colors.Length)
                return Color.magenta;

            return Colors[level - 1];
        }

        public static int Count => Colors.Length;
    }
}
