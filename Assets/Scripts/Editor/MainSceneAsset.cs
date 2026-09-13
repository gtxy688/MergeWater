namespace MergeWater.Editor
{
    /// <summary>
    /// 主场景路径的**唯一来源**。
    ///
    /// <para>UI 全部是 <c>Main.unity</c> 里的实际对象（`GameRoot/Presentation` 子树），
    /// 改界面一律在编辑器里手动调整；工程里**没有**任何生成 / 重建界面的编辑器工具
    /// （2026-09-13 需求方要求删除程序化 UI 生成，见 `Docs/progress.md`）。
    /// 本常量只被编辑器工具与 EditMode 门禁用来定位场景资产。</para>
    /// </summary>
    public static class MainSceneAsset
    {
        public const string Path = "Assets/Scenes/Main.unity";
    }
}
