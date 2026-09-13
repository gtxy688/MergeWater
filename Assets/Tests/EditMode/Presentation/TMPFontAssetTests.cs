using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MergeWater.Tests.EditMode
{
    /// <summary>
    /// D16 字形门禁：随包中文字体必须存在、是 Static、**覆盖玩家会看到的全部文案**，
    /// 且主场景里每个 TMP 组件都指向它、材质不悬空。
    ///
    /// <para>补这组测试的原因（2026-09-13）：此前**没有任何测试覆盖字体资产**。
    /// 字体被重新烘焙后，场景里 39/52 个组件的材质引用悬空（指向已不存在的子资产），
    /// 并且 23 个实际用到的字（含隐私说明里的「微 信 平 台 服 务」等）没被烘进图集——
    /// Static 模式下这些字运行时直接渲染为空白，而当时 201 个测试全绿。
    /// 数值漂移由 `ConfigAssetTests` 守，字形漂移由这里守。</para>
    /// </summary>
    public sealed class TMPFontAssetTests
    {
        private const string FontAssetPath = MergeWater.Editor.TMPFontBuilder.OutputPath;

        [Test]
        public void BundledFont_Exists_IsStatic_AndSingleAtlas()
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);

            Assert.That(font, Is.Not.Null,
                $"缺少随包中文字体：{FontAssetPath}。先运行 MergeWater/Font/2. 烘焙中文 TMP 字体资产");

            Assert.That(font.atlasPopulationMode, Is.EqualTo(AtlasPopulationMode.Static),
                "必须是 Static：微信小游戏跑在 WebGL 派生运行时上，动态字形生成不可靠（D16）");

            Assert.That(font.atlasTextures, Is.Not.Null.And.Length.GreaterThan(0), "字体资产必须带图集纹理");
            Assert.That(font.atlasTextures.Length, Is.EqualTo(1),
                $"图集应保持 1 张（多图集会成倍放大包体）：现在是 {font.atlasTextures.Length} 张，" +
                "请重跑烘焙并检查字符文件是否被塞进了大量用不到的字");
        }

        [Test]
        public void BundledFont_CoversRequiredTexts_AndPrintablePunctuation()
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            Assert.That(font, Is.Not.Null);

            var baked = new HashSet<char>();
            foreach (var character in font.characterTable)
                baked.Add((char)character.unicode);

            // 1) 需求方指定的隐私说明 / 入口文案，以及合规的健康游戏忠告
            foreach (var text in MergeWater.Editor.TMPFontBuilder.RequiredTexts)
            {
                var missing = new List<char>();
                foreach (var c in text)
                    if (c != '\n' && c != '\r' && !baked.Contains(c))
                        missing.Add(c);

                Assert.That(missing, Is.Empty,
                    $"必备文案有 {missing.Count} 个字没烘进图集：{new string(missing.ToArray())}\n原文：{text}");
            }

            // 2) ASCII：分数、最高分、版本号、百分比等运行时拼接都要用
            for (var c = (char)0x20; c < 0x7F; c++)
                Assert.That(baked, Does.Contain(c), $"缺 ASCII 字符 U+{(int)c:X4}（'{c}'）");

            // 3) 常用中文标点
            foreach (var c in MergeWater.Editor.TMPFontBuilder.RequiredPunctuation)
                Assert.That(baked, Does.Contain(c), $"缺中文标点「{c}」");
        }

        [Test]
        public void EveryTmpText_InMainScene_UsesBundledFont_WithNoDanglingMaterial()
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            Assert.That(font, Is.Not.Null);
            Assert.That(font.material, Is.Not.Null, "字体资产必须带默认材质");

            var scene = EditorSceneManager.OpenScene(MergeWater.Editor.SceneBuilder.ScenePath,
                OpenSceneMode.Additive);

            try
            {
                var texts = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<TMP_Text>(true))
                    .ToArray();

                Assert.That(texts.Length, Is.GreaterThan(0), "主场景里应有 TMP 文本组件");

                foreach (var text in texts)
                {
                    var serialized = new SerializedObject(text);
                    var fontProperty = serialized.FindProperty("m_fontAsset");
                    var materialProperty = serialized.FindProperty("m_sharedMaterial");
                    var where = PathOf(text.transform);

                    Assert.That(fontProperty, Is.Not.Null, $"{where} 上没有 m_fontAsset 字段");
                    Assert.That(fontProperty.objectReferenceValue, Is.SameAs(font),
                        $"{where} 用的不是随包中文字体——Static 模式下缺字会直接渲染成空白");

                    Assert.That(materialProperty, Is.Not.Null, $"{where} 上没有 m_sharedMaterial 字段");
                    Assert.That(materialProperty.objectReferenceValue, Is.SameAs(font.material),
                        $"{where} 的材质引用不是该字体的材质（重建字体资产后容易悬空）");
                }
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static string PathOf(Transform transform)
        {
            var builder = new StringBuilder(transform.name);
            for (var parent = transform.parent; parent != null; parent = parent.parent)
                builder.Insert(0, parent.name + "/");

            return builder.ToString();
        }
    }
}
