namespace MergeWater.Core
{
    /// <summary>
    /// 落点预测所需的场地查询（M2 实现，M4 消费）。
    /// 有了它，瞄准预览才能画出「落到堆叠表面」的真实落点，而不是把水果画穿过去、
    /// 或者只画一段固定时长的下落弧。
    /// </summary>
    public interface IPreviewObstacle
    {
        /// <summary>
        /// 返回自 (<paramref name="x"/>, <paramref name="fromY"/>) 垂直向下第一个可落表面的世界 y。
        /// 没有任何水果位于该 x 的横向范围内时，返回场地地面高度。
        /// </summary>
        float GetLandingY(float x, float fromY);
    }
}
