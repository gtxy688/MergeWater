using MergeWater.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace MergeWater.Tests.EditMode
{
    /// <summary>
    /// 警戒线颜色门禁（2026-09-14，V2.62）。
    ///
    /// <para>来历：需求方在进游戏前调好了警戒线颜色，一进游戏又被打回默认色——原因是
    /// <c>DangerLineView.EnsureVisibleColors()</c> 会把「alpha &lt; 0.45 或 RGB 全 &gt; 0.95」的常态色
    /// 替换成默认值。需求方要求删掉那段代码，并要求「场景里设的就是权威」。</para>
    ///
    /// <para>本门禁守住这个约定：**看起来"不可见"的颜色也必须原样保留**（半透明、近白、甚至全透明），
    /// 代码不得以"为你好"的名义改写它。</para>
    /// </summary>
    public sealed class DangerLineViewTests
    {
        private GameObject _root;
        private LineRenderer _line;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("DangerLineViewTest");
            _line = _root.AddComponent<LineRenderer>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null)
                Object.DestroyImmediate(_root);

            _root = null;
            _line = null;
        }

        [Test]
        public void SetLine_KeepsSemiTransparentAuthoredColor()
        {
            var authored = new Color(0.2f, 0.9f, 0.9f, 0.2f); // alpha 0.2：旧版会被"纠正"成默认色
            _line.startColor = authored;
            _line.endColor = authored;

            var view = _root.AddComponent<DangerLineView>();
            view.Configure(_line);
            view.SetLine(3.4f, 2.5f, 0.05f);

            Assert.That(_line.startColor, Is.EqualTo(authored), "场景里设的半透明颜色必须原样保留");
            Assert.That(_line.endColor, Is.EqualTo(authored));
        }

        [Test]
        public void SetLine_KeepsNearWhiteAuthoredColor()
        {
            var authored = new Color(1f, 1f, 1f, 1f); // 近白：旧版同样会被替换
            _line.startColor = authored;
            _line.endColor = authored;

            var view = _root.AddComponent<DangerLineView>();
            view.Configure(_line);
            view.SetLine(3.4f, 2.5f, 0.05f);

            Assert.That(_line.startColor, Is.EqualTo(authored), "近白也属于玩家的选择，不得被改写");
        }

        [Test]
        public void PulsingRestoresTheAuthoredColorWhenItEnds()
        {
            var authored = new Color(0.1f, 0.1f, 0.1f, 0.3f);
            _line.startColor = authored;
            _line.endColor = authored;

            var view = _root.AddComponent<DangerLineView>();
            view.Configure(_line);

            view.SetPulsing(true);
            view.SetPulsing(false);

            Assert.That(_line.startColor, Is.EqualTo(authored), "脉冲结束后应还原成场景里设的颜色");
            Assert.That(view.IsPulsing, Is.False);
        }

        [Test]
        public void Configure_WithoutSceneReference_StillKeepsTheRendererColor()
        {
            // 线引用为空（场景里没接线）时走 GetComponentInChildren 兜底，颜色同样不得被改写。
            var authored = new Color(0.5f, 0.5f, 0.5f, 0.1f);
            _line.startColor = authored;
            _line.endColor = authored;

            var view = _root.AddComponent<DangerLineView>();
            view.SetLine(1f, 1f, 0.05f);

            Assert.That(_line.startColor, Is.EqualTo(authored));
        }
    }
}
