using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MergeWater.Editor
{
    /// <summary>
    /// 清理微信 SDK 在**关场景时**留下的 `WXSDKManagerHandler` 单例，消除 Unity 的
    /// 「Some objects were not cleaned up when closing the scene. (Did you spawn new GameObjects from OnDestroy?)」
    /// 警告（2026-09-14）。
    ///
    /// <para><b>证据（不是猜的）</b>：Editor.log 里该警告紧跟着
    /// <c>WXTouchInputOverride.OnDisable() → UnregisterWechatTouchEvents() → WXBase.cs:990</c>——
    /// 也就是**插件自己在 OnDisable 里去取 SDK 单例**，而它的 `Instance` 是懒加载的：
    /// `wx-runtime-editor.dll` 里既有 `WeChatWASM.WXSDKManagerHandler` 类型，也有一次 `DontDestroyOnLoad`。
    /// 于是在场景关闭过程中新建了一个跨场景对象，Unity 便报"没清理干净"。</para>
    ///
    /// <para><b>与本工程的关系</b>：无关。我们的 C# 从不引用该类型（全仓 grep 只在 WebGL 模板的 JS 里出现
    /// <c>SendMessage('WXSDKManagerHandler', ...)</c>），构建产物也不受影响——纯编辑器噪声。
    /// 这里只在**非播放状态**下清掉它（播放中不动，避免干扰运行中的游戏）；插件下次需要时会自行重建。</para>
    /// </summary>
    [InitializeOnLoad]
    internal static class WeChatSdkLeakCleaner
    {
        private const string LeakedName = "WXSDKManagerHandler";

        static WeChatSdkLeakCleaner()
        {
            EditorSceneManager.sceneClosing += (_, __) => Cleanup();
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredEditMode)
                    Cleanup();
            };
        }

        private static void Cleanup()
        {
            // 播放中（或即将进入播放）不动它：那可能是插件正常工作需要的东西。
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            var cleaned = 0;
            foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (go == null || go.name != LeakedName)
                    continue;

                if (EditorUtility.IsPersistent(go) || go.scene.IsValid())
                    continue; // 资产、或仍属于某个场景的对象都不动

                Object.DestroyImmediate(go);
                cleaned++;
            }

            if (cleaned > 0)
            {
                Debug.Log($"[WeChatSdkLeakCleaner] 已清理微信插件遗留的 {LeakedName} ×{cleaned}。" +
                          "成因：插件 WXTouchInputOverride.OnDisable → WXBase.cs:990 懒加载创建了" +
                          "跨场景单例（与本工程代码无关，也不影响构建产物）；插件下次需要时会自行重建。");
            }
        }
    }
}
