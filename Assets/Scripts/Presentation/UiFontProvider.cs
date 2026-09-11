using System;
using UnityEngine;

namespace MergeWater.Presentation
{
    /// <summary>
    /// 中文字体解析（决策 D9）：优先用系统中文字体的动态字体，失败时回退内置字体并告警一次。
    /// 正式美术阶段迁移到 TMP + 打包字体时，只需替换本类与 HudBuilder 的文本创建。
    /// </summary>
    public static class UiFontProvider
    {
        private static readonly string[] PreferredFonts =
        {
            "Microsoft YaHei",
            "微软雅黑",
            "PingFang SC",
            "Noto Sans CJK SC",
            "Noto Sans SC",
            "Source Han Sans SC",
            "SimHei",
            "Droid Sans Fallback",
            "Arial Unicode MS"
        };

        private static Font _cached;
        private static bool _warned;

        public static Font Resolve()
        {
            if (_cached != null)
                return _cached;

            if (!TryResolveFromOs(out _cached))
            {
                _cached = ResolveBuiltin();

                if (!_warned)
                {
                    _warned = true;
                    Debug.LogWarning("[UiFontProvider] 未找到系统中文字体，回退内置字体；中文可能显示为方块（决策 D9）。");
                }
            }

            return _cached;
        }

        private static bool TryResolveFromOs(out Font font)
        {
            font = null;

            try
            {
                var installed = Font.GetOSInstalledFontNames();
                if (installed == null || installed.Length == 0)
                    return false;

                for (var i = 0; i < PreferredFonts.Length; i++)
                {
                    var name = PreferredFonts[i];
                    if (Array.IndexOf(installed, name) < 0)
                        continue;

                    font = Font.CreateDynamicFontFromOSFont(name, 44);
                    if (font != null)
                        return true;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[UiFontProvider] 查询系统字体失败：{e.Message}");
            }

            return false;
        }

        private static Font ResolveBuiltin()
        {
            try
            {
                return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }
            catch (Exception)
            {
                // 旧版本 Unity 的内置字体名不同，继续尝试下一个。
            }

            try
            {
                return Resources.GetBuiltinResource<Font>("Arial.ttf");
            }
            catch (Exception e)
            {
                Debug.LogError($"[UiFontProvider] 内置字体也不可用：{e.Message}");
                return null;
            }
        }
    }
}
