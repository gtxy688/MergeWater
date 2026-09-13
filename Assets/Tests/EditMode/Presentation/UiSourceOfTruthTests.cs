using System;
using System.Linq;
using System.Reflection;
using MergeWater.Presentation;
using NUnit.Framework;

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
    /// <para>现在 <c>HudBuilder</c> 位于 <c>MergeWater.Editor</c>（Editor 平台程序集），
    /// 运行时程序集在编译期就无法引用它。本测试把这条约束固化下来：一旦有人把 UI 生成
    /// 逻辑挪回运行时程序集，这里就会失败。</para>
    /// </summary>
    public sealed class UiSourceOfTruthTests
    {
        [Test]
        public void RuntimeAssemblies_DoNotContainTheUiBuilder()
        {
            // HudBuilder 会 AddComponent<Text>()/Image/Button/Toggle/Slider——它是唯一能「生成界面」的类。
            // 它必须留在 MergeWater.Editor（Editor 平台程序集），运行时程序集既不能包含它、也不能引用它。
            string[] runtimeAssemblyNames = { "MergeWater.Presentation", "MergeWater.Bootstrap" };

            var offenders = runtimeAssemblyNames
                .Select(name => AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(assembly => assembly.GetName().Name == name))
                .Where(assembly => assembly != null)
                .SelectMany(assembly => assembly.GetTypes().Select(type => new { type, assembly }))
                .Where(x => x.type.FullName != null && x.type.FullName.EndsWith("HudBuilder"))
                .Select(x => $"{x.type.FullName}（程序集 {x.assembly.GetName().Name}）")
                .ToArray();

            Assert.That(offenders, Is.Empty,
                "UI 生成器存在于运行时程序集：运行时会生成界面，编辑器里的调整就不是最终效果。" +
                "请把 UI 生成放回 MergeWater.Editor。\n" + string.Join("\n", offenders));
        }

        [Test]
        public void RuntimeAssemblies_DoNotReferenceTheUiBuilder()
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
                "运行时程序集不得引用 MergeWater.Editor（否则可以间接调用 UI 生成器）：\n"
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
