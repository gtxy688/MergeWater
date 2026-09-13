using MergeWater.Bootstrap;
using MergeWater.Core;
using UnityEditor;
using UnityEngine;

namespace MergeWater.Editor
{
    /// <summary>
    /// 地面高度实时滑杆（需求方 2026-09-12 要求「让我来调」）。
    ///
    /// <para>为什么需要：地面在 `GameBootstrapper.Awake` 就按相机算好了位置
    /// （<c>floorY = 屏幕底边 + GameBalance.FloorScreenInset</c>），**场景里手工挪 `Floor` 无效**——
    /// 运行时 `EnsureArena` 会覆盖掉。所以唯一起作用的旋钮是一个数：`floorScreenInset`。
    /// 反复改数 → 重进 Play 目视的成本太高，本窗口让它在 Play 模式下实时可调。</para>
    ///
    /// <para>用法：进 Play 模式 → 菜单 `MergeWater/Floor Tuning (Play Mode)` → 拖滑杆。
    /// 「写回代码默认值」会把当前值写进 `GameBalance.cs` 并重新生成 `GameBalance.asset`，
    /// 保证「数值只在一处权威定义」这条约束不被破坏（本窗口只是调试通道，不新增第二份数值来源）。</para>
    /// </summary>
    public sealed class FloorTuningWindow : EditorWindow
    {
        /// <summary>允许的下限。0 就是「地面贴死屏幕底」——需求方反馈过水果会被下边缘切，故只允许略大于 0。</summary>
        private const float MinInset = 0.02f;

        private const float MaxInset = 1.5f;

        /// <summary>
        /// 滑杆当前值。这里**不复制**权威数值：真实值在首次 <see cref="OnGUI"/> 时从
        /// <c>GameBalance.FloorScreenInset</c> 载入（见 <c>_loaded</c>），此处的初始值只是占位。
        /// </summary>
        private float _inset = MinInset;
        private bool _loaded;

        [MenuItem("MergeWater/Floor Tuning (Play Mode)", priority = 6)]
        public static void Open()
        {
            var window = GetWindow<FloorTuningWindow>("地面调参");
            window.minSize = new Vector2(300f, 150f);
            window.Show();
        }

        private GameBootstrapper FindBootstrapper() => FindObjectOfType<GameBootstrapper>();

        private void OnGUI()
        {
            var bootstrapper = FindBootstrapper();

            if (!Application.isPlaying || bootstrapper == null || bootstrapper.Context == null)
            {
                EditorGUILayout.HelpBox(
                    "请先进入 Play 模式。\n\n" +
                    "地面位置在 GameBootstrapper.Awake 里按相机计算，" +
                    "所以必须在运行时调整（在场景里手工挪 Floor 会被覆盖）。",
                    MessageType.Info);
                return;
            }

            if (!_loaded)
            {
                _inset = bootstrapper.Context.Balance.FloorScreenInset;
                _loaded = true;
            }

            EditorGUILayout.LabelField("地面相对屏幕底边的抬升量（世界单位）", EditorStyles.boldLabel);
            EditorGUILayout.Space(4f);

            EditorGUI.BeginChangeCheck();
            _inset = EditorGUILayout.Slider(_inset, MinInset, MaxInset);
            var changed = EditorGUI.EndChangeCheck();

            EditorGUILayout.Space(2f);
            var camera = Camera.main;
            if (camera != null && camera.orthographic)
            {
                var screenBottom = camera.transform.position.y - camera.orthographicSize;
                EditorGUILayout.LabelField($"屏幕底边 y = {screenBottom:0.###}");
                EditorGUILayout.LabelField($"地面 y = {screenBottom + _inset:0.###}（当前实际 {bootstrapper.Context.Field.PlayFloorY:0.###}）");
                EditorGUILayout.LabelField($"屏幕高度占比 ≈ {(camera.orthographicSize * 2f <= 0f ? 0f : _inset / (camera.orthographicSize * 2f) * 100f):0.#}%");
            }

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("越大 = 地面越高（离屏幕底越远）；越小 = 越靠下。", EditorStyles.miniLabel);
            EditorGUILayout.LabelField("0 = 贴死屏幕底（水果会被下边缘切，不要用）。", EditorStyles.miniLabel);

            EditorGUILayout.Space(10f);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("应用（实时预览）", GUILayout.Height(26f)) || changed)
                    Apply(bootstrapper, _inset);

                if (GUILayout.Button("恢复数值表默认", GUILayout.Height(26f)))
                {
                    _inset = bootstrapper.Context.Balance.FloorScreenInset;
                    Apply(bootstrapper, _inset);
                }
            }

            EditorGUILayout.Space(4f);

            if (GUILayout.Button("写回代码默认值（GameBalance.cs + 同步资产）", GUILayout.Height(28f)))
                WriteBack(_inset);
        }

        private static void Apply(GameBootstrapper bootstrapper, float inset)
        {
            var camera = Camera.main;
            bootstrapper.Context.SetPlayAreaFromCamera(camera, inset);
        }

        /// <summary>
        /// 把调好的值写回 `GameBalance.cs` 的字段默认值，并同步 `GameBalance.asset`。
        /// 数值的权威定义始终是 `GameBalance`（见 AGENTS.md 硬约束 2），本方法只是替你把那一处改掉。
        ///
        /// <para><b>为什么不能在这里调 <c>ConfigAssetGenerator.GenerateMenu()</c></b>：
        /// 生成器用 <c>ScriptableObject.CreateInstance</c> 取代码默认值，而此刻 Unity **还没重新编译**
        /// 刚写回的 .cs——字段初始化器仍来自上一次编译的程序集，生成出来的资产会把旧值原样写回。
        /// 2026-09-13 需求方「参数调好了，但好像没起作用」就是这条：.cs 写成了 0.06、资产仍是 0.55
        /// （实测时间戳：.cs 10:01:34.931 → 资产 10:01:35.208 → Core.dll 10:01:38.268），
        /// 运行时读的是资产，于是滑杆白调。</para>
        ///
        /// <para>因此这里改为**直接改资产的序列化字段**：不经由程序集默认值，写进去的就是刚调好的数，
        /// 不需要等重编译，也不会被旧默认值覆盖。.cs 那份照常写，等 Unity 自行重编译后两边自洽。</para>
        /// </summary>
        private static void WriteBack(float inset)
        {
            const string path = "Assets/Scripts/Core/Config/GameBalance.cs";
            var text = System.IO.File.ReadAllText(path);

            var pattern = new System.Text.RegularExpressions.Regex(
                @"(private float floorScreenInset = )([0-9.]+)f");
            var match = pattern.Match(text);

            if (!match.Success)
            {
                Debug.LogError($"[FloorTuning] 在 {path} 里找不到 floorScreenInset 字段，请手动修改。");
                return;
            }

            var oldValue = match.Groups[2].Value;
            var updated = pattern.Replace(text,
                $"${{1}}{inset.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)}f", 1);
            System.IO.File.WriteAllText(path, updated);

            var clamped = Mathf.Max(0f, inset);

            if (TryPatchAsset(clamped))
            {
                Debug.Log($"[FloorTuning] floorScreenInset: {oldValue} -> {clamped:0.###}" +
                          $"（已写回 {path} 并同步 {ConfigAssetGenerator.BalancePath}）");
            }
            else
            {
                Debug.LogError($"[FloorTuning] 已把 {clamped:0.###} 写进 {path}，" +
                               $"但未能同步 {ConfigAssetGenerator.BalancePath}（资产缺失或字段路径已变）。" +
                               "请等 Unity 重编译结束后运行菜单 MergeWater/Generate Config Assets。");
            }

            AssetDatabase.Refresh();
        }

        /// <summary>
        /// 把新值直接写进配置资产的序列化字段（<c>balance.floorScreenInset</c>）。
        /// 返回 false 表示资产不存在或字段路径不匹配，调用方需提示手动重新生成。
        /// </summary>
        private static bool TryPatchAsset(float inset)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameBalanceAsset>(ConfigAssetGenerator.BalancePath);
            if (asset == null)
                return false;

            var serialized = new SerializedObject(asset);
            var property = serialized.FindProperty("balance")?.FindPropertyRelative("floorScreenInset");
            if (property == null)
                return false;

            property.floatValue = inset;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            return true;
        }
    }
}
