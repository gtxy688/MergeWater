using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace MergeWater.Editor
{
    /// <summary>
    /// 打包前的一次性工程设置（2026-09-14，V2.61 优化）。**只写设置，不产出任何资产**。
    ///
    /// <para>为什么用脚本而不是手改 `ProjectSettings.asset`：剥离级别与 WebGL 内存在这份 YAML 里是
    /// 按平台存的结构，手写容易写坏；交给 Unity 自己的 API 最稳。</para>
    ///
    /// <para>做了什么：</para>
    /// <list type="bullet">
    /// <item><c>managedStrippingLevel</c>（WebGL）：默认 Low → **Medium**，减小 wasm；
    /// 反射相关的第三方程序集由 `Assets/link.xml` 保留（见该文件里的维护约定）。</item>
    /// <item><c>WebGL 初始内存</c>：32MB → **256MB**（上限保持 2048MB）。32MB 对"2048² 字体图集 +
    /// 精灵图集 + 10 张星球贴图 + BGM"根本不现实，只会让运行时反复扩容。
    /// 注意：微信插件的转换面板里还有自己的 UnityHeap 设置，**以那个为准**，这里只是把工程默认值调成合理值。</item>
    /// </list>
    ///
    /// <para>回滚：把剥离级别改回 Low、初始内存改回 32 再跑一次即可（本脚本会打印改动前后的值）。</para>
    /// </summary>
    public static class WebGlReleaseSettings
    {
        private const int TargetInitialMemoryMb = 256;
        private const int TargetMaximumMemoryMb = 2048;

        [MenuItem("MergeWater/Apply WebGL Release Settings", priority = 41)]
        public static void ApplyFromMenu() => Apply(exitWhenDone: false);

        /// <summary>批处理入口：<c>-executeMethod MergeWater.Editor.WebGlReleaseSettings.ApplyFromCommandLine</c></summary>
        public static void ApplyFromCommandLine() => Apply(exitWhenDone: true);

        private static void Apply(bool exitWhenDone)
        {
            var group = BuildTargetGroup.WebGL;
            var before = PlayerSettings.GetManagedStrippingLevel(NamedBuildTarget.WebGL);
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.WebGL, ManagedStrippingLevel.Medium);
            var after = PlayerSettings.GetManagedStrippingLevel(NamedBuildTarget.WebGL);
            Debug.Log($"[WebGlReleaseSettings] 托管剥离级别（WebGL）：{before} → {after}" +
                      "（反射程序集由 Assets/link.xml 保留）");

            var beforeInit = PlayerSettings.WebGL.initialMemorySize;
            PlayerSettings.WebGL.initialMemorySize = TargetInitialMemoryMb;
            PlayerSettings.WebGL.maximumMemorySize = TargetMaximumMemoryMb;
            Debug.Log($"[WebGlReleaseSettings] WebGL 内存：初始 {beforeInit}MB → " +
                      $"{PlayerSettings.WebGL.initialMemorySize}MB，上限 {PlayerSettings.WebGL.maximumMemorySize}MB" +
                      "（微信插件转换面板的 UnityHeap 若另有设置，以面板为准）");

            AssetDatabase.SaveAssets();
            Debug.Log($"[WebGlReleaseSettings] 完成（BuildTargetGroup={group}）。");

            if (exitWhenDone)
                EditorApplication.Exit(0);
        }
    }
}
