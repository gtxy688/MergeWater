using UnityEngine;

namespace MergeWater.Core
{
    /// <summary>
    /// 编辑器调参载体。运行时应通过 <see cref="ToBalance"/> 取得克隆，避免把运行时状态写回资产。
    /// 由 `MergeWater/Generate Placeholder Art and Config` 从 <see cref="GameBalance.CreateDefault"/> 生成。
    /// </summary>
    [CreateAssetMenu(fileName = "GameBalance", menuName = "MergeWater/Game Balance")]
    public sealed class GameBalanceAsset : ScriptableObject
    {
        [SerializeField] private GameBalance balance = GameBalance.CreateDefault();

        public GameBalance ToBalance() => (balance ?? GameBalance.CreateDefault()).Clone();

        /// <summary>校验用：直接读取资产内的数值（不克隆）。</summary>
        public GameBalance RawBalance => balance;
    }
}
