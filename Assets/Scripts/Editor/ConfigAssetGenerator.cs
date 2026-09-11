using MergeWater.Core;
using MergeWater.Meta;
using UnityEditor;
using UnityEngine;

namespace MergeWater.Editor
{
    /// <summary>
    /// 从代码默认值生成配置资产，保证磁盘资产与 <c>Docs/requirements.md</c> 的 V1/V2 表一致。
    /// 生成后由 EditMode 测试 `ConfigAssetTests` 校验，避免资产与代码默认值漂移。
    /// </summary>
    public static class ConfigAssetGenerator
    {
        public const string ConfigFolder = "Assets/Config";
        public const string BalancePath = ConfigFolder + "/GameBalance.asset";
        public const string MetaSettingsPath = ConfigFolder + "/MetaSettings.asset";

        [MenuItem("MergeWater/Generate Config Assets", priority = 2)]
        public static void GenerateMenu() => GenerateAll(force: true);

        public static void GenerateAll(bool force)
        {
            EnsureFolder();

            CreateOrResetAsset<GameBalanceAsset>(BalancePath, force);
            CreateOrResetAsset<MetaSettings>(MetaSettingsPath, force);

            AssetDatabase.SaveAssets();
            Debug.Log($"[ConfigAssetGenerator] 配置资产已就绪：{BalancePath}、{MetaSettingsPath}");
        }

        private static void CreateOrResetAsset<T>(string path, bool force) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);

            if (existing != null && !force)
                return;

            if (existing != null)
                AssetDatabase.DeleteAsset(path);

            var asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
        }

        private static void EnsureFolder()
        {
            if (AssetDatabase.IsValidFolder(ConfigFolder))
                return;

            AssetDatabase.CreateFolder("Assets", "Config");
        }
    }
}
