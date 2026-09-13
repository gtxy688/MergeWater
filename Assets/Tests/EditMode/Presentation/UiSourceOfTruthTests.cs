using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using MergeWater.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace MergeWater.Tests.EditMode.Presentation
{
    /// <summary>
    /// UI 来源不变量：界面必须**只**来自场景资产（运行前就已存在），运行时不生成。
    ///
    /// <para>为什么需要：UI 曾经既能在编辑期由场景构建工具生成、又能在运行时由
    /// <c>GameBootstrapper</c> 的「缺引用就现场搭一套」兜底生成。那时「编辑器里看到的」
    /// 与「运行时看到的」可能不是同一套界面，改布局/美术必须先改代码，而且手工调整会在
    /// 下次重建场景时静默丢失。</para>
    ///
    /// <para>2026-09-13 需求方要求「以后只手动编辑 UI，不要程序自动生成」，因此工程里已经
    /// **不存在**任何 UI 生成器（`HudBuilder` / `SceneBuilder` / `UiTmpMigrator` 三个编辑器工具
    /// 连同菜单一并删除）。本测试把这条约束固化成两道门禁：① 运行时代码不得创建 UI 组件；
    /// ② 运行时程序集不得引用 <c>MergeWater.Editor</c>。谁想把界面生成挪回代码里，这里就会红。</para>
    /// </summary>
    public sealed class UiSourceOfTruthTests
    {
        /// <summary>
        /// 一旦在运行时代码里 AddComponent 这些类型，就等于「代码在造界面」。
        /// 注意**刻意不含** <c>CanvasGroup</c>：<see cref="UiPanel"/> 会在缺组件时给自己补一个
        /// （它只是透明度载体，不产生任何界面元素），这类自愈不属于「生成界面」。
        ///
        /// <para>扫描范围是 <c>Assets/Scripts/</c>（跳过其中的 <c>Editor/</c>）：**测试**在
        /// <c>Assets/Tests/</c> 下自建最小 HUD（`PresentationHarness`）是允许的——测试不是运行时装配。</para>
        /// </summary>
        private static readonly string[] UiComponentTypes =
        {
            "Canvas", "CanvasScaler", "GraphicRaycaster", "EventSystem", "StandaloneInputModule",
            "Image", "RawImage", "Button", "Toggle", "Slider", "ScrollRect", "Mask", "RectMask2D",
            "TextMeshProUGUI", "TextMeshPro", "HudView", "PanelController", "UiPanel",
            "HorizontalLayoutGroup", "VerticalLayoutGroup", "GridLayoutGroup", "ContentSizeFitter",
            "LayoutElement"
        };

        [Test]
        public void RuntimeSources_DoNotCreateUiComponents()
        {
            var scriptsRoot = Path.Combine(Application.dataPath, "Scripts").Replace('\\', '/');
            Assert.That(Directory.Exists(scriptsRoot), Is.True, $"找不到运行时代码目录：{scriptsRoot}");

            var offenders = new List<string>();

            foreach (var path in Directory.GetFiles(scriptsRoot, "*.cs", SearchOption.AllDirectories))
            {
                var normalized = path.Replace('\\', '/');

                // `Editor/` 下是编辑器工具，不进运行时装配（当前连 UI 生成器都没有了）。
                if (normalized.Contains("/Editor/"))
                    continue;

                var lineNo = 0;
                foreach (var line in File.ReadLines(path))
                {
                    lineNo++;

                    var trimmed = line.TrimStart();
                    if (trimmed.StartsWith("//", StringComparison.Ordinal))
                        continue;

                    foreach (var type in UiComponentTypes)
                    {
                        if (Regex.IsMatch(line, $@"AddComponent<\s*{type}\s*>"))
                            offenders.Add($"{normalized.Substring(scriptsRoot.Length + 1)}:{lineNo} " +
                                          $"AddComponent<{type}>");
                    }
                }
            }

            Assert.That(offenders, Is.Empty,
                "运行时代码在创建 UI 组件——界面必须只来自 Assets/Scenes/Main.unity" +
                "（在编辑器里手动维护），不要在代码里生成或拼装界面：\n" + string.Join("\n", offenders));
        }

        [Test]
        public void RuntimeAssemblies_DoNotReferenceTheEditorAssembly()
        {
            string[] runtimeAssemblyNames = { "MergeWater.Presentation", "MergeWater.Bootstrap" };

            var offenders = runtimeAssemblyNames
                .Select(name => AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(assembly => assembly.GetName().Name == name))
                .Where(assembly => assembly != null)
                .SelectMany(assembly => assembly.GetReferencedAssemblies()
                    .Where(reference => reference.Name == "MergeWater.Editor")
                    .Select(reference => $"{assembly.GetName().Name} → {reference.Name}"))
                .ToArray();

            Assert.That(offenders, Is.Empty,
                "运行时程序集不得引用 MergeWater.Editor（编辑器工具不该进运行时装配）：\n"
                + string.Join("\n", offenders));
        }

        [Test]
        public void HudViewFields_AreFilledFromTheSceneNotFromCode()
        {
            // HudView 的字段全部是 public 字段 + 场景序列化引用；不应有任何「运行时赋值 UI 引用」的代码在
            // 运行时程序集里（否则又会出现两套来源）。这里检查 HudView 自身不含静态构造/初始化逻辑。
            var constructors = typeof(HudView)
                .GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(c => c.GetParameters().Length > 0)
                .ToArray();

            Assert.That(constructors, Is.Empty,
                "HudView 不应有带参数的构造函数：它的引用应由场景序列化填充");

            var setters = typeof(HudView)
                .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(m => m.IsSpecialName && m.Name.StartsWith("set_"))
                .Select(m => m.Name)
                .ToArray();

            Assert.That(setters, Is.Empty,
                "HudView 不应有属性 setter：UI 引用一律由场景提供，避免出现「代码改引用」的第二条路径。发现："
                + string.Join("、", setters));
        }
    }
}
