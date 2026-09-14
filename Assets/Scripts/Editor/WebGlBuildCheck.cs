using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace MergeWater.Editor
{
    /// <summary>
    /// 打包前自检（2026-09-14）：**真的构建一次 WebGL**。
    ///
    /// <para>为什么值得做：工程里有两处代码**编辑器永远不会编译**——`SafeAreaSources` 的微信分支与
    /// `WeChatAdBridge.CreateRewardedVideo`（`WeChatWASM.WX` 只存在于运行时 DLL，编辑器版 DLL 没有它），
    /// 它们整段包在 `#if UNITY_WEBGL &amp;&amp; !UNITY_EDITOR` 里。2026-09-13 的 CS1503
    /// （`double` → `float`）就是这样漏到导出时才炸的。跑一次真实构建即可把这些分支编译一遍，
    /// 同时拿到可对比的体积数字。</para>
    ///
    /// <para>只产出到 <c>Builds/WebGLCheck</c>（已在 .gitignore 内），不触碰微信导出目录，
    /// 也不会改动任何工程设置。</para>
    /// </summary>
    public static class WebGlBuildCheck
    {
        private const string OutputDir = "Builds/WebGLCheck";

        [MenuItem("MergeWater/Check WebGL Build", priority = 40)]
        public static void CheckFromMenu() => Check(exitWhenDone: false);

        /// <summary>
        /// 批处理入口：
        /// <c>Unity.exe -batchmode -nographics -quit -projectPath &lt;proj&gt;
        /// -executeMethod MergeWater.Editor.WebGlBuildCheck.CheckFromCommandLine -logFile Logs/webgl-build.log</c>
        /// </summary>
        public static void CheckFromCommandLine() => Check(exitWhenDone: true);

        private static void Check(bool exitWhenDone)
        {
            var scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray();
            if (scenes.Length == 0)
            {
                Debug.LogError("[WebGlBuildCheck] Build Settings 里没有启用的场景，无法构建。");
                if (exitWhenDone)
                    EditorApplication.Exit(2);
                return;
            }

            Debug.Log($"[WebGlBuildCheck] 开始构建 WebGL：场景 {string.Join(", ", scenes)} → {OutputDir}");

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = OutputDir,
                target = BuildTarget.WebGL,
                options = BuildOptions.None
            });

            var summary = report.summary;
            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"[WebGlBuildCheck] ✅ WebGL 构建成功：总计 {summary.totalSize / 1048576f:0.00} MB，" +
                          $"耗时 {summary.totalTime.TotalSeconds:0} 秒（这一步同时验证了所有 #if UNITY_WEBGL 分支能编译）");
            }
            else
            {
                Debug.LogError($"[WebGlBuildCheck] ❌ WebGL 构建失败：{summary.result}，错误 {summary.totalErrors} 条" +
                               "（先看日志里的 error CS —— 那多半就是导出时才会暴露的 WebGL 专属编译错误）");
            }

            if (exitWhenDone)
                EditorApplication.Exit(summary.result == BuildResult.Succeeded ? 0 : 1);
        }
    }
}
