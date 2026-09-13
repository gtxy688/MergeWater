using UnityEngine;

namespace MergeWater.Presentation
{
    /// <summary>
    /// 所有弹窗面板的基类（需求方 2026-09-12：「给 UI 面板搞个父类，这样方便些」）。
    ///
    /// <para>面板对象是 <c>Main.unity</c> 里**预先存在**的对象，本类只负责它自己的显示/隐藏，
    /// 不决定何时显示（那是 <see cref="PanelController"/> 的职责）。把显隐封装在这里的好处：
    /// ① 面板在编辑器里选中就能看到自己的开关状态，不必去 <c>PanelController</c> 的字典里找；
    /// ② 「可见」的定义只有一处（alpha + raycast + interactable + activeSelf 必须同时正确），
    /// 不会出现「active 了却看不见」（alpha=0）这类不一致。</para>
    ///
    /// <para>面板根节点的初始状态：<b>场景里保持 alpha=1、active=false</b>。
    /// 这样你在编辑器里手动勾上 active 就能直接看到面板内容来调布局，不用先改 alpha。</para>
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class UiPanel : MonoBehaviour
    {
        [Tooltip("面板在编辑器里被手动激活时是否自动显示（默认 true，方便直接调布局）。")]
        [SerializeField] private bool visibleWhenActivated = true;

        private CanvasGroup _group;

        /// <summary>面板根节点的 CanvasGroup（缺失时自动补一个，避免空引用）。</summary>
        public CanvasGroup Group
        {
            get
            {
                if (_group == null)
                    _group = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
                return _group;
            }
        }

        /// <summary>面板当前是否可见（以 alpha 为准，active 只是伴随状态）。</summary>
        public bool IsVisible => Group.alpha > 0.5f;

        /// <summary>显示/隐藏面板。可见性必须同时改 alpha、射线、可交互与 active，缺一项就会出问题。</summary>
        public void SetVisible(bool visible)
        {
            var group = Group;
            group.alpha = visible ? 1f : 0f;
            group.blocksRaycasts = visible;
            group.interactable = visible;

            if (gameObject.activeSelf != visible)
                gameObject.SetActive(visible);
        }

        public void Show() => SetVisible(true);

        public void Hide() => SetVisible(false);

        /// <summary>
        /// 编辑器里手动激活时立即把 alpha 设为 1（默认行为），
        /// 避免出现「勾了 active 却因为 alpha=0 而看不见」——这是需求方实际踩到的坑。
        /// 关掉 <see cref="visibleWhenActivated"/> 可保持 active 与 alpha 互不影响。
        /// </summary>
        protected virtual void OnEnable()
        {
            if (!Application.isPlaying && visibleWhenActivated && Group.alpha <= 0.5f)
            {
                Group.alpha = 1f;
                Group.blocksRaycasts = true;
                Group.interactable = true;
            }
        }
    }
}
