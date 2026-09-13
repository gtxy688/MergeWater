using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace MergeWater.Editor
{
    /// <summary>
    /// 中文字体管线（微信小游戏取向）：**字符收集文件 + 静态烘焙的 TMP Font Asset**。
    ///
    /// <para><b>为什么必须静态</b>：WebGL / 微信小游戏无法访问系统字体，运行时的动态字形生成也不可靠，
    /// 因此运行时要用到的每一个字都必须在构建前烘进图集（决策 D16）。Static 的代价是
    /// 「没烘到的字运行时显示为空白」，所以字符集合必须来自真实文案，且新增文案后要重跑本工具。</para>
    ///
    /// <para><b>两步用法</b>：
    /// 1. `MergeWater/Font/1. 收集字符 → TMPCharacters.txt`：扫描场景 / Prefab / C# 字面量，
    ///    把真实会用到的文案写进 `Assets/Fonts/TMPCharacters.txt`（人工可读、可手工追加）。
    /// 2. `MergeWater/Font/2. 烘焙中文 TMP 字体资产`：读取该 txt，按「1024² 优先、放不下再 2048²」
    ///    烘出 Static 字体资产。</para>
    ///
    /// <para><b>刻意不做「整个 CJK 区间全量烘焙」</b>：4E00–9FFF 有 2 万多字，哪怕 32pt 也放不进
    /// 4096² 图集，包体直接崩。这里只烘实际用到的字符（本项目约 600 字）。</para>
    /// </summary>
    public static class TMPFontBuilder
    {
        // ── 路径 ─────────────────────────────────────────────────────
        /// <summary>源字体（SIMYOU 幼圆）。**不放在 Resources 下**：Resources 里的资源会被无条件打进包，
        /// 而 6.7MB 的 .ttf 只在编辑期烘焙时用得到，运行时完全不需要。</summary>
        public const string SourceFontPath = "Assets/Fonts/SIMYOU.TTF";

        /// <summary>字符收集文件（人工可读、可追加；烘焙时读取其中全部字符）。</summary>
        public const string CharactersPath = "Assets/Fonts/TMPCharacters.txt";

        /// <summary>输出字体资产。</summary>
        public const string OutputPath = "Assets/Fonts/ChineseUI SDF.asset";

        // ── 烘焙参数 ─────────────────────────────────────────────────
        /// <summary>采样点大小。沿用需求方此前手动烘焙的 75pt（字号 24–120 的 UI 都够清晰），
        /// 不随意改动以免文字观感发生变化。</summary>
        public const int SamplingPointSize = 75;

        /// <summary>图集内边距。项目 TMP 文本没有 Outline（`m_fontStyle: 0`、无 `m_OutlineWidth`），
        /// 因此按需求取 5；若将来加了粗描边，把它调到 8–10。</summary>
        public const int Padding = 5;

        /// <summary>
        /// 候选图集规格：(宽, 高, 采样点, 是否允许第二张图集)。按顺序尝试，取第一个装得下全部字符的规格。
        ///
        /// <para>顺序 = 体积/清晰度偏好：先按需求试 1024²（Alpha8 仅 1MB）；装不下升到 2048² 并**保留 75pt**
        /// （与需求方此前手调的观感一致）；2048² 也装不下时才逐步降低采样点到 68 / 64（同尺寸下更省空间，
        /// 代价是大字号略软）；都不行才允许第二张图集（包体 +4MB），保证不出现空白字。</para>
        /// </summary>
        private static readonly (int Width, int Height, int PointSize, bool AllowMultiAtlas)[] AtlasCandidates =
        {
            (1024, 1024, SamplingPointSize, false), // 需求：优先 1024²
            (2048, 2048, SamplingPointSize, false), // 1024² 装不下 → 2048²，采样不变
            (2048, 2048, 68, false),                // 2048² 仍装不下 → 降采样点换容量
            (2048, 2048, 64, false),
            (2048, 2048, SamplingPointSize, true)   // 最后手段：允许多图集，宁可多 4MB 也不留空白字
        };

        /// <summary>
        /// 扫描时跳过的目录：第三方 UI 素材包。它的示例场景/Prefab 里有 "Button"、"CLAIM"、
        /// 零宽空格（U+200B）之类内容——不是本作文案，收进来既白占图集，又会因为源字体没有该字形
        /// 把「烘焙成功」误报成「缺字」。
        /// </summary>
        private static readonly string[] ScanSkipRoots = { "Assets/Art/" };

        /// <summary>
        /// 代码扫描的例外名单。**当前为空**：编辑器目录下的文案是菜单项、弹窗与日志（玩家看不到），
        /// 而玩家可见的 UI 文案全部随 UI 对象存在于 `Assets/Scenes/Main.unity`，由上面的场景扫描收齐
        /// （2026-09-13 删除 UI 生成器后，原先唯一的例外 `HudBuilder.cs` 已不存在）。
        /// 不收编辑器文案还有一个实际原因：工具自己的日志里会有 `✓` 这类字符，
        /// 源字体没有该字形，会把「烘焙成功」误报成「缺字」。
        /// </summary>
        private static readonly string[] ScriptScanAllowlist = { };

        /// <summary>必带标点：ASCII 已在别处补齐，这里只管中文标点与常见符号。</summary>
        public const string RequiredPunctuation = "，。！？：；、“”‘’《》（）【】+-*/%=.%";

        /// <summary>
        /// 必带文案（需求方指定）：隐私说明正文与入口，以及合规的健康游戏忠告。
        /// 即使这些字当前没出现在场景里也要烘进去——它们属于「随时可能上线」的合规文案。
        /// </summary>
        public const string RequiredPrivacy =
            "本游戏为单机休闲游戏。\n" +
            "游戏仅在本地保存游戏进度、最高分及游戏设置等必要数据，上述数据不会上传至开发者服务器。\n" +
            "当游戏运行于微信小游戏平台时，部分平台能力可能由微信提供，相关信息处理规则以微信平台及本小游戏公示的《用户隐私保护指引》为准。\n" +
            "你可以在游戏设置中清除本地游戏数据。\n" +
            "进入游戏\n" +
            "查看隐私保护指引";

        public static readonly string[] RequiredTexts =
        {
            RequiredPrivacy,
            "《健康游戏忠告》\n抵制不良游戏，拒绝盗版游戏。注意自我保护，谨防受骗上当\n" +
            "适度游戏益脑，沉迷游戏伤身。合理安排时间，享受健康生活"
        };

        // ── 菜单 ─────────────────────────────────────────────────────

        [MenuItem("MergeWater/Font/1. 收集字符 → TMPCharacters.txt", priority = 20)]
        public static void CollectMenu()
        {
            var (texts, chars) = Collect();
            WriteCharactersFile(texts, chars);
            EditorUtility.DisplayDialog("MergeWater",
                $"已写入 {CharactersPath}\n\n文本条目 {texts.Count} 条，去重字符 {chars.Count} 个。\n" +
                "（下一步：MergeWater/Font/2. 烘焙中文 TMP 字体资产）", "好的");
        }

        [MenuItem("MergeWater/Font/2. 烘焙中文 TMP 字体资产", priority = 21)]
        public static void BuildMenu()
        {
            var ok = Build();
            EditorUtility.DisplayDialog("MergeWater",
                ok ? $"已生成 {OutputPath}（详见 Console 报告）。" : "烘焙失败，请看 Console。", "好的");
        }

        [MenuItem("MergeWater/Font/审计 TMP 字体引用", priority = 22)]
        public static void AuditMenu() => TMPFontReferenceTool.AuditAndReport();

        // ── 命令行入口 ───────────────────────────────────────────────

        public static void CollectFromCommandLine()
        {
            var (texts, chars) = Collect();
            WriteCharactersFile(texts, chars);
            EditorApplication.Exit(0);
        }

        public static void BuildFromCommandLine()
        {
            EditorApplication.Exit(Build() ? 0 : 1);
        }

        /// <summary>批处理一次跑完「收集 + 烘焙」，省掉重复启动编辑器的开销。</summary>
        public static void CollectAndBuildFromCommandLine()
        {
            var (texts, chars) = Collect();
            WriteCharactersFile(texts, chars);
            var ok = Build();
            EditorApplication.Exit(ok ? 0 : 1);
        }

        // ── 字符收集 ─────────────────────────────────────────────────

        /// <summary>收集项目里会显示给玩家的全部文本，返回（文本条目, 去重字符集）。</summary>
        public static (List<(string Source, string Text)> Texts, HashSet<char> Chars) Collect()
        {
            var texts = new List<(string, string)>();
            var chars = new HashSet<char>();

            void Add(string source, string text)
            {
                if (string.IsNullOrEmpty(text))
                    return;
                texts.Add((source, text));
                foreach (var c in text)
                {
                    if (c != '\r' && c != '\n' && c != '\t' && IsPrintable(c))
                        chars.Add(c);
                }
            }

            // 1) 打印可见 ASCII：字母 / 数字 / 半角标点（分数、百分比、版本号等都要用）
            for (var c = (char)0x20; c < 0x7F; c++)
                chars.Add(c);

            // 2) 必带中文标点
            foreach (var c in RequiredPunctuation)
                chars.Add(c);

            // 3) 必带文案（合规 / 隐私）
            foreach (var t in RequiredTexts)
                Add("必带文案", t);

            // 4) 场景里的 TMP 文本（Unity 以 \uXXXX 转义写盘）
            foreach (var path in FindFiles("Assets", "*.unity"))
            {
                if (IsSkipped(path))
                    continue;
                foreach (var text in ExtractTextFields(File.ReadAllText(path)))
                    Add(path, text);
            }

            // 5) Prefab 里的 TMP 文本（排除第三方素材包）
            foreach (var path in FindFiles("Assets", "*.prefab"))
            {
                if (IsSkipped(path))
                    continue;
                foreach (var text in ExtractTextFields(File.ReadAllText(path)))
                    Add(path, text);
            }

            // 6) C# 字符串字面量：运行时拼接的文案（"本局：" + score 之类）不在场景里，必须单独收
            foreach (var path in FindFiles("Assets/Scripts", "*.cs"))
            {
                var normalized = path.Replace('\\', '/');

                // Editor/ 下的字符串是菜单项、弹窗与日志文案，玩家看不到
                if (normalized.Contains("/Editor/") && !IsAllowlistedScript(normalized))
                    continue;

                var lineNo = 0;
                foreach (var line in File.ReadLines(path))
                {
                    lineNo++;
                    var trimmed = line.TrimStart();
                    if (trimmed.StartsWith("//"))
                        continue;

                    // 调试日志不面向玩家；且工具日志里会出现 ✓ 之类源字体没有的符号，
                    // 收进来会污染字符集（烘焙时报「缺字」，实际玩家永远看不到那个字）。
                    if (trimmed.Contains("Debug.Log"))
                        continue;

                    foreach (Match m in Regex.Matches(line, "\"((?:[^\"\\\\]|\\\\.)*)\""))
                        Add($"{path}:{lineNo}", Unescape(m.Groups[1].Value));
                }
            }

            return (texts, chars);
        }

        private static bool IsAllowlistedScript(string normalizedPath)
        {
            foreach (var allowed in ScriptScanAllowlist)
                if (string.Equals(normalizedPath, allowed, StringComparison.OrdinalIgnoreCase))
                    return true;

            return false;
        }

        private static bool IsSkipped(string assetPath)
        {
            var normalized = assetPath.Replace('\\', '/');
            foreach (var root in ScanSkipRoots)
                if (normalized.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                    return true;

            return false;
        }

        /// <summary>
        /// 是否是可显示字符。控制符、零宽字符、格式符（Unicode Cf，如 U+200B 零宽空格）、
        /// 代理对与私用区都没有可渲染的字形：烘不进去，还会让「是否有缺字」的判断失去意义。
        /// 实测踩到的例子：`Assets/Art` 素材包的登录示例场景里有 U+200B。
        /// </summary>
        private static bool IsPrintable(char c)
        {
            switch (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c))
            {
                case System.Globalization.UnicodeCategory.Control:
                case System.Globalization.UnicodeCategory.Format:
                case System.Globalization.UnicodeCategory.Surrogate:
                case System.Globalization.UnicodeCategory.PrivateUse:
                case System.Globalization.UnicodeCategory.OtherNotAssigned:
                    return false;
                default:
                    return true;
            }
        }

        private static IEnumerable<string> FindFiles(string root, string pattern)
        {
            if (!Directory.Exists(root))
                yield break;

            foreach (var path in Directory.GetFiles(root, pattern, SearchOption.AllDirectories))
                yield return path.Replace('\\', '/');
        }

        /// <summary>抽取序列化文件里的 <c>m_text</c> 并反转义（Unity 把非 ASCII 写成 \uXXXX）。</summary>
        private static IEnumerable<string> ExtractTextFields(string fileContent)
        {
            foreach (Match m in Regex.Matches(fileContent, @"(?m)^  m_text: (.*)$"))
            {
                var raw = m.Groups[1].Value.Trim();
                if (raw.Length >= 2 && raw[0] == '"' && raw[raw.Length - 1] == '"')
                    raw = raw.Substring(1, raw.Length - 2);

                var text = Unescape(raw);
                if (!string.IsNullOrEmpty(text))
                    yield return text;
            }
        }

        private static string Unescape(string raw)
        {
            var sb = new StringBuilder(raw.Length);
            var i = 0;
            while (i < raw.Length)
            {
                var c = raw[i];
                if (c == '\\' && i + 1 < raw.Length)
                {
                    var n = raw[i + 1];
                    if (n == 'u' && i + 5 < raw.Length &&
                        int.TryParse(raw.Substring(i + 2, 4), System.Globalization.NumberStyles.HexNumber,
                            System.Globalization.CultureInfo.InvariantCulture, out var code))
                    {
                        sb.Append((char)code);
                        i += 6;
                        continue;
                    }

                    switch (n)
                    {
                        case 'n': sb.Append('\n'); i += 2; continue;
                        case 't': sb.Append('\t'); i += 2; continue;
                        case 'r': sb.Append('\r'); i += 2; continue;
                        case '\\': sb.Append('\\'); i += 2; continue;
                        case '"': sb.Append('"'); i += 2; continue;
                        default: sb.Append(n); i += 2; continue;
                    }
                }

                sb.Append(c);
                i++;
            }

            return sb.ToString();
        }

        /// <summary>
        /// 写出字符收集文件。格式刻意保持「人可读、可直接贴文案」：
        /// 以 <c>#</c> 开头的行是注释，烘焙时会跳过；其余行的**每个字符**都会被烘进图集，
        /// 所以不需要手工去重，也不需要维护一份字符清单。
        /// </summary>
        private static void WriteCharactersFile(List<(string Source, string Text)> texts, HashSet<char> chars)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# ============================================================");
            sb.AppendLine("# MergeWater 字体字符收集文件（自动生成，可手工追加）");
            sb.AppendLine("#");
            sb.AppendLine("# 烘焙时读取本文件里除注释行（# 开头）与换行以外的**全部字符**：");
            sb.AppendLine("# 直接把完整文案贴进来即可，不需要去重，也不需要维护字符清单。");
            sb.AppendLine("# 重新生成：菜单 MergeWater/Font/1. 收集字符 → TMPCharacters.txt");
            sb.AppendLine("# 烘焙：   菜单 MergeWater/Font/2. 烘焙中文 TMP 字体资产");
            sb.AppendLine("# ============================================================");
            sb.AppendLine();

            sb.AppendLine("[必带：ASCII 可打印字符（字母 / 数字 / 半角标点）]");
            for (var c = (char)0x20; c < 0x7F; c++)
                sb.Append(c);
            sb.AppendLine();
            sb.AppendLine();

            sb.AppendLine("[必带：常用中文标点]");
            sb.AppendLine(RequiredPunctuation);
            sb.AppendLine();

            sb.AppendLine("[必带：隐私说明 / 入口文案（需求方指定，即使当前未显示也要烘）]");
            sb.AppendLine(RequiredPrivacy);
            sb.AppendLine();

            sb.AppendLine("[必带：健康游戏忠告（合规文案）]");
            sb.AppendLine(RequiredTexts[1]);
            sb.AppendLine();

            var currentSource = (string)null;
            foreach (var (source, text) in texts)
            {
                if (source == "必带文案")
                    continue; // 上面已单独成段

                if (source != currentSource)
                {
                    currentSource = source;
                    sb.AppendLine($"[来源：{source}]");
                }

                sb.AppendLine(text);
            }

            sb.AppendLine();
            sb.AppendLine("# ---- 去重字符统计（仅供参考，烘焙时不读这一行）----");
            sb.AppendLine($"# 共 {chars.Count} 个不同字符");

            var directory = Path.GetDirectoryName(CharactersPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllText(CharactersPath, sb.ToString(), new UTF8Encoding(false));
            AssetDatabase.ImportAsset(CharactersPath, ImportAssetOptions.ForceUpdate);

            Debug.Log($"[TMPFontBuilder] 已写出 {CharactersPath}：文本条目 {texts.Count} 条，去重字符 {chars.Count} 个。");
        }

        /// <summary>读取收集文件里除注释行与换行以外的全部字符。</summary>
        public static HashSet<char> ReadCharactersFile()
        {
            var set = new HashSet<char>();
            if (!File.Exists(CharactersPath))
            {
                Debug.LogError($"[TMPFontBuilder] 找不到字符文件 {CharactersPath}；先运行 MergeWater/Font/1. 收集字符。");
                return set;
            }

            foreach (var line in File.ReadLines(CharactersPath))
            {
                if (line.Length > 0 && line[0] == '#')
                    continue;

                foreach (var c in line)
                    if (c != '\r' && c != '\n' && c != '\t' && IsPrintable(c))
                        set.Add(c);
            }

            return set;
        }

        // ── 烘焙 ─────────────────────────────────────────────────────

        public static bool Build()
        {
            var source = AssetDatabase.LoadAssetAtPath<Font>(SourceFontPath);
            if (source == null)
            {
                Debug.LogError($"[TMPFontBuilder] 找不到源字体：{SourceFontPath}。" +
                               "若刚移动过文件，等 Unity 重新导入后再试。");
                return false;
            }

            var requested = ReadCharactersFile();
            if (requested.Count == 0)
                return false;

            // 先按「源字体是否真有这个字形」过滤：这样「字体没有该字」与「图集装不下」是两件
            // 分别可读的事——否则前者会被误判成容量问题，把图集一路升到 2048² 也解决不了。
            var characters = new HashSet<char>();
            var fontLacks = new List<char>();
            foreach (var c in requested)
            {
                if (source.HasCharacter(c))
                    characters.Add(c);
                else
                    fontLacks.Add(c);
            }

            if (fontLacks.Count > 0)
            {
                fontLacks.Sort();
                Debug.LogWarning($"[TMPFontBuilder] 源字体没有这 {fontLacks.Count} 个字形，已跳过" +
                                 $"（若其中有用在 UI 上的字，需要换字体或改成别字）：{new string(fontLacks.ToArray())}");
            }

            var all = new string(System.Linq.Enumerable.ToArray(characters));
            Debug.Log($"[TMPFontBuilder] 待烘字符 {characters.Count} 个（请求 {requested.Count}，" +
                      $"源字体缺字形 {fontLacks.Count}）；源字体 {SourceFontPath}；" +
                      $"Padding {Padding}、渲染 SDFAA。");

            // 「Packing Method: Optimum」的说明：
            // TMP 的 Font Asset Creator 窗口里 Optimum = GlyphPackingMode.ContactPointRule(4)，
            // 但**运行时 API `TryAddCharacters` 内部写死 BestShortSideFit(=Creator 的 Fast)**，
            // 没有对外参数可切到 Optimum。这里用运行时 API 烘焙，并用「是否单图集装得下 + 图集占用率」
            // 实测验证结果——占用率会打进日志，所以有没有浪费是可核对的事实，而不是假设。
            var lastMissing = string.Empty;
            for (var i = 0; i < AtlasCandidates.Length; i++)
            {
                var (width, height, pointSize, allowMultiAtlas) = AtlasCandidates[i];
                var isLastCandidate = i == AtlasCandidates.Length - 1;

                var fontAsset = CreateDynamic(source, pointSize, width, height, allowMultiAtlas);
                if (fontAsset == null)
                {
                    Debug.LogError("[TMPFontBuilder] CreateFontAsset 返回 null；检查源字体 Import Settings " +
                                   "是否勾选 Include Font Data。");
                    return false;
                }

                fontAsset.TryAddCharacters(all, out var missing, includeFontFeatures: true);
                var atlasCount = fontAsset.atlasTextures != null ? fontAsset.atlasTextures.Length : 0;
                var fits = string.IsNullOrEmpty(missing) && (allowMultiAtlas || atlasCount <= 1);

                Debug.Log($"[TMPFontBuilder] 尝试 {width}×{height} @{pointSize}pt" +
                          (allowMultiAtlas ? "（允许多图集）" : "") +
                          $"：图集 {atlasCount} 张，未装入 {missing?.Length ?? 0} 个字符，" +
                          $"占用率 {Occupancy(fontAsset):P1}" + (fits ? " → 采用。" : " → 装不下，换下一档。"));

                if (fits || isLastCandidate)
                {
                    lastMissing = missing ?? string.Empty;
                    return Save(fontAsset, width, height, pointSize, characters, atlasCount, lastMissing);
                }

                // 丢弃这一档的中间对象（图集纹理 / 材质），避免在编辑器里堆积泄漏。
                UnityEngine.Object.DestroyImmediate(fontAsset, true);
            }

            return false;
        }

        private static TMP_FontAsset CreateDynamic(Font source, int pointSize, int width, int height,
            bool allowMultiAtlas)
        {
            // 先以 Dynamic 创建：TryAddCharacters 只在 Dynamic 模式下工作（Static 会直接返回 false），
            // 字形全部写入后再切成 Static。默认关闭 multi-atlas，这样「装不下」会以 missing 形式暴露，
            // 而不是悄悄多开一张图集把包体翻倍。
            return TMP_FontAsset.CreateFontAsset(source, pointSize, Padding, GlyphRenderMode.SDFAA,
                width, height, AtlasPopulationMode.Dynamic, enableMultiAtlasSupport: allowMultiAtlas);
        }

        /// <summary>
        /// 已放置字形占图集面积的比例（按字形 rect，不含 padding，因此是**下界**）。
        /// 用来判断「图集是不是被浪费了」：占用率低却装不下 = 装箱碎片化，不是真的没空间。
        /// </summary>
        private static float Occupancy(TMP_FontAsset fontAsset)
        {
            if (fontAsset.glyphTable == null || fontAsset.atlasWidth <= 0 || fontAsset.atlasHeight <= 0)
                return 0f;

            long used = 0;
            foreach (var glyph in fontAsset.glyphTable)
                used += (long)glyph.glyphRect.width * glyph.glyphRect.height;

            return used / (float)((long)fontAsset.atlasWidth * fontAsset.atlasHeight);
        }

        private static bool Save(TMP_FontAsset fontAsset, int width, int height, int pointSize,
            HashSet<char> requested, int atlasCount, string missing)
        {
            fontAsset.name = Path.GetFileNameWithoutExtension(OutputPath);

            // 切静态：运行时不再生成字形（微信小游戏 / WebGL 的运行时字形生成不可靠）。
            fontAsset.atlasPopulationMode = AtlasPopulationMode.Static;
            fontAsset.ReadFontAssetDefinition();

            var directory = Path.GetDirectoryName(OutputPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            AssetDatabase.CreateAsset(fontAsset, OutputPath);

            // 图集纹理必须是子资产，否则引用会在重载后断开。
            if (fontAsset.atlasTextures != null)
            {
                for (var i = 0; i < fontAsset.atlasTextures.Length; i++)
                {
                    var texture = fontAsset.atlasTextures[i];
                    if (texture == null)
                        continue;
                    texture.name = $"{fontAsset.name} Atlas {i}";
                    AssetDatabase.AddObjectToAsset(texture, fontAsset);
                }
            }

            if (fontAsset.material != null)
            {
                fontAsset.material.name = $"{fontAsset.name} Material";
                AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
            }

            EditorUtility.SetDirty(fontAsset);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // ── 报告 ────────────────────────────────────────────────
            var baked = new HashSet<char>();
            if (fontAsset.characterTable != null)
                foreach (var ch in fontAsset.characterTable)
                    baked.Add((char)ch.unicode);

            var missingRequested = new List<char>();
            foreach (var c in requested)
                if (!baked.Contains(c))
                    missingRequested.Add(c);
            missingRequested.Sort();

            var atlasBytes = (long)width * height * atlasCount; // Alpha8 = 1 字节/像素
            Debug.Log(
                $"[TMPFontBuilder] 完成：{OutputPath}\n" +
                $"  图集 {width}×{height} × {atlasCount} 张（Alpha8 ≈ {atlasBytes / 1024f / 1024f:0.0} MB），" +
                $"采样 {pointSize}pt，Padding {Padding}，占用率 {Occupancy(fontAsset):P1}（下界，不含 padding）\n" +
                $"  字符 {fontAsset.characterTable?.Count ?? 0} 个 / 字形 {fontAsset.glyphTable?.Count ?? 0} 个，" +
                $"模式 {fontAsset.atlasPopulationMode}\n" +
                $"  请求 {requested.Count} 个字符，未烘进去 {missingRequested.Count} 个" +
                (missingRequested.Count > 0 ? "：" + new string(missingRequested.ToArray()) : "") +
                (string.IsNullOrEmpty(missing) ? "" : $"\n  未装入（图集容量）：{missing}"));

            ReportRequiredCoverage(baked);

            AssetDatabase.SaveAssets();
            return (fontAsset.characterTable?.Count ?? 0) > 0 && missingRequested.Count == 0;
        }

        /// <summary>逐条核对需求方指定的必备文案是否 100% 烘进去了。</summary>
        private static void ReportRequiredCoverage(HashSet<char> baked)
        {
            var labels = new[] { "隐私说明/入口文案", "健康游戏忠告" };
            for (var i = 0; i < RequiredTexts.Length && i < labels.Length; i++)
            {
                var missing = new List<char>();
                foreach (var c in RequiredTexts[i])
                    if (c != '\r' && c != '\n' && c != '\t' && !baked.Contains(c))
                        missing.Add(c);

                if (missing.Count == 0)
                    Debug.Log($"[TMPFontBuilder] 覆盖检查：{labels[i]} 全部字符已烘入 ✓");
                else
                    Debug.LogError($"[TMPFontBuilder] 覆盖检查：{labels[i]} 仍缺 {missing.Count} 个字符：" +
                                   new string(missing.ToArray()));
            }

            var missingPunct = new List<char>();
            foreach (var c in RequiredPunctuation)
                if (!baked.Contains(c))
                    missingPunct.Add(c);

            if (missingPunct.Count > 0)
                Debug.LogError($"[TMPFontBuilder] 覆盖检查：常用中文标点仍缺：{new string(missingPunct.ToArray())}");
        }
    }
}
