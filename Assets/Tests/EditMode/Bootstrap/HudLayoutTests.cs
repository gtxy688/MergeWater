using System.Collections.Generic;
using System.IO;
using System.Linq;
using MergeWater.Editor;
using MergeWater.Presentation;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MergeWater.Tests.EditMode
{
    /// <summary>
    /// HUD 布局与安全区不变量（GDD §6.2、R27）。
    ///
    /// 这些不变量只在视觉上暴露，普通逻辑测试抓不到；本测试用于守住此类缺陷——
    /// 曾真实发生两起：① 贴边锚点配 pivot 0.5 导致设置按钮顶部被裁出画布 4 单位；
    /// ② 设置按钮置于右上角，与微信胶囊保留区重叠。
    ///
    /// 度量方式刻意使用 **锚点/偏移数据 + 参考分辨率**（而不是渲染后的像素矩形）：
    /// 编辑器非播放态下 CanvasScaler 不生效，渲染矩形会随 Game View 尺寸变化而使断言失真；
    /// 锚点数据与画布缩放无关，是确定性的。
    /// </summary>
    public sealed class HudLayoutTests
    {
        private const float Tolerance = 0.01f;

        private Scene _scene;
        private bool _opened;
        private HudView _view;
        private Canvas _canvas;

        [SetUp]
        public void SetUp()
        {
            Assert.That(File.Exists(MergeWater.Editor.MainSceneAsset.Path), Is.True,
                "缺少主场景资产 Assets/Scenes/Main.unity（UI 全部在该场景里手工维护，不要删除或改名）");

            _scene = EditorSceneManager.OpenScene(MergeWater.Editor.MainSceneAsset.Path, OpenSceneMode.Additive);
            _opened = true;

            _canvas = Object.FindObjectsOfType<Canvas>(true).FirstOrDefault(c => c.gameObject.scene == _scene);
            Assert.That(_canvas, Is.Not.Null, "场景应包含 Canvas");

            _view = Object.FindObjectsOfType<HudView>(true).FirstOrDefault(v => v.gameObject.scene == _scene);
            Assert.That(_view, Is.Not.Null, "场景应包含 HudView");
        }

        [TearDown]
        public void TearDown()
        {
            if (_opened)
                EditorSceneManager.CloseScene(_scene, true);

            _opened = false;
        }

        /// <summary>HUD 上应完整可见的元素（不含按设计铺满屏幕的背景与模态面板）。</summary>
        private List<KeyValuePair<string, Component>> VisibleHudElements()
        {
            var elements = new List<KeyValuePair<string, Component>>
            {
                Pair("bestScoreText", _view.bestScoreText),
                Pair("scoreText", _view.scoreText),
                // 需求方（2026-09-12）已移除：comboText（连击提示改到合成位置飘字）、
                // 阶段目标（stageProgressLabel/Fill）与 NEXT 预览（nextFruitIcon/Label）。
                Pair("settingsButton", _view.settingsButton),
                Pair("shakeButton", _view.shakeButton),
                Pair("undoButton", _view.undoButton),
                Pair("toastPanel", _view.toastPanel)
            };

            return elements.Where(pair => pair.Value != null).ToList();
        }

        private static KeyValuePair<string, Component> Pair(string name, Component component) =>
            new KeyValuePair<string, Component>(name, component);

        /// <summary>
        /// 把元素换算到「以画布左上角为原点的参考分辨率矩形」。
        /// 仅支持单点锚（贴边/居中）元素；拉伸锚元素在 HUD 中只有按钮内的 Label，不在检查清单内。
        /// </summary>
        private static bool TryGetDesignRect(Component component, out Rect designRect, out string error)
        {
            designRect = default;
            error = null;

            var rect = component.GetComponent<RectTransform>();
            if (rect == null)
            {
                error = "缺少 RectTransform";
                return false;
            }

            if (!Mathf.Approximately(rect.anchorMin.x, rect.anchorMax.x) ||
                !Mathf.Approximately(rect.anchorMin.y, rect.anchorMax.y))
            {
                error = "锚点为拉伸模式，不在本测试检查范围内";
                return false;
            }

            var anchorX = rect.anchorMin.x * UiDesignSpec.ReferenceWidth;
            var anchorY = rect.anchorMin.y * UiDesignSpec.ReferenceHeight;

            // 以左上角为原点的设计坐标（y 向下为正）。
            var pivotX = anchorX + rect.anchoredPosition.x;
            var pivotY = anchorY + rect.anchoredPosition.y;
            var left = pivotX - rect.pivot.x * rect.sizeDelta.x;
            var top = UiDesignSpec.ReferenceHeight - (pivotY + (1f - rect.pivot.y) * rect.sizeDelta.y);

            designRect = new Rect(left, top, rect.sizeDelta.x, rect.sizeDelta.y);
            return true;
        }

        [Test]
        public void AllVisibleHudElements_FitInsideDesignCanvas()
        {
            var failures = new List<string>();

            foreach (var pair in VisibleHudElements())
            {
                if (!TryGetDesignRect(pair.Value, out var rect, out var error))
                {
                    if (error != null && error.Contains("拉伸"))
                        continue;

                    failures.Add($"{pair.Key}: {error}");
                    continue;
                }

                if (rect.xMin < -Tolerance)
                    failures.Add($"{pair.Key} 左侧越界 xMin={rect.xMin:0.#}");

                if (rect.yMin < -Tolerance)
                    failures.Add($"{pair.Key} 顶部越界 yMin={rect.yMin:0.#}");

                if (rect.xMax > UiDesignSpec.ReferenceWidth + Tolerance)
                    failures.Add($"{pair.Key} 右侧越界 xMax={rect.xMax:0.#}");

                if (rect.yMax > UiDesignSpec.ReferenceHeight + Tolerance)
                    failures.Add($"{pair.Key} 底部越界 yMax={rect.yMax:0.#}");
            }

            Assert.That(failures, Is.Empty,
                "以下 HUD 元素超出 1080x1920 参考画布：\n" + string.Join("\n", failures));
        }

        [Test]
        public void TopAnchoredElements_UseSameSidePivot_SoTheyAreNotClipped()
        {
            // 回归守卫：贴边锚点必须配同侧 pivot，否则 anchoredPosition 的语义会从
            // 「距该边距离」变成「中心偏移」，元素就会被裁出画布（曾发生：设置按钮顶部 -4）。
            var failures = new List<string>();

            foreach (var pair in VisibleHudElements())
            {
                var rect = pair.Value.GetComponent<RectTransform>();
                if (rect == null)
                    continue;

                // 拉伸锚（如进度条填充）由父级决定尺寸，不参与 pivot 一致性检查。
                var singleX = Mathf.Approximately(rect.anchorMin.x, rect.anchorMax.x);
                var singleY = Mathf.Approximately(rect.anchorMin.y, rect.anchorMax.y);

                if (singleX && Mathf.Approximately(rect.anchorMin.x, 1f) && !Mathf.Approximately(rect.pivot.x, 1f))
                    failures.Add($"{pair.Key}: 右贴边但 pivot.x={rect.pivot.x:0.##}");

                if (singleX && Mathf.Approximately(rect.anchorMin.x, 0f) && !Mathf.Approximately(rect.pivot.x, 0f))
                    failures.Add($"{pair.Key}: 左贴边但 pivot.x={rect.pivot.x:0.##}");

                if (singleY && Mathf.Approximately(rect.anchorMin.y, 1f) && !Mathf.Approximately(rect.pivot.y, 1f))
                    failures.Add($"{pair.Key}: 上贴边但 pivot.y={rect.pivot.y:0.##}");

                if (singleY && Mathf.Approximately(rect.anchorMin.y, 0f) && !Mathf.Approximately(rect.pivot.y, 0f))
                    failures.Add($"{pair.Key}: 下贴边但 pivot.y={rect.pivot.y:0.##}");
            }

            Assert.That(failures, Is.Empty,
                "贴边元素的 pivot 与锚点不一致（会被裁切）：\n" + string.Join("\n", failures));
        }

        [Test]
        public void TopBar_DoesNotEnterWeChatCapsuleZone()
        {
            // 胶囊保留区：右上角 CapsuleZoneWidth × CapsuleZoneHeight（GDD §6.2）。
            var capsule = new Rect(
                UiDesignSpec.ReferenceWidth - UiDesignSpec.CapsuleZoneWidth, 0f,
                UiDesignSpec.CapsuleZoneWidth, UiDesignSpec.CapsuleZoneHeight);

            Assert.That(_view.settingsButton, Is.Not.Null, "应有设置按钮");

            var checkedCount = 0;

            // 顶栏元素（最高分 / 总分 / 设置按钮）都不得进入胶囊区。
            // 需求方（2026-09-12）已移除阶段目标与 NEXT 预览，故顶栏只剩这三项。
            var topBar = new[]
            {
                Pair("bestScoreText", _view.bestScoreText),
                Pair("scoreText", _view.scoreText),
                Pair("settingsButton", _view.settingsButton)
            };

            var failures = new List<string>();

            foreach (var pair in topBar)
            {
                if (pair.Value == null)
                    continue;

                if (!TryGetDesignRect(pair.Value, out var rect, out _))
                    continue;

                checkedCount++;

                if (rect.Overlaps(capsule))
                    failures.Add($"{pair.Key} 与胶囊区重叠 rect={rect} capsule={capsule}");
            }

            Assert.That(checkedCount, Is.GreaterThanOrEqualTo(3), "应检查到全部顶栏元素（最高分/总分/设置）");
            Assert.That(failures, Is.Empty,
                "以下顶栏元素会与微信胶囊重叠：\n" + string.Join("\n", failures));
        }

        [Test]
        public void SettingsPanel_BottomBlock_SitsAboveTheBottomEdgeWithBalancedSpacing()
        {
            // 2026-09-14（需求方「我一直用着没问题啊，把测试改掉或者删除」）：
            // 原用例假设「面板子元素全部底锚点 + 底侧 pivot」，用 `面板高 + anchoredPosition.y` 推算空档；
            // 但设置面板后来重新排版成**拉伸锚点**，该假设从一开始就不成立——于是它长期红着，
            // 而且算出来的 -28.9 是模型噪声、不是观感问题。现在改用 RectTransformUtility 把子元素换算到
            // 面板局部空间（任何锚点模式都适用），只断言真正的观感约束、不写死像素值。
            var panelRect = _view.settingsPanel.GetComponent<RectTransform>();
            var closeRect = _view.settingsCloseButton.GetComponent<RectTransform>();
            var versionRect = _view.versionText.GetComponent<RectTransform>();
            var clearCacheRect = _view.clearCacheButton.GetComponent<RectTransform>();

            Assert.That(panelRect, Is.Not.Null, "设置面板应有 RectTransform");
            Assert.That(closeRect, Is.Not.Null, "关闭按钮应有 RectTransform");
            Assert.That(versionRect, Is.Not.Null, "版本号应有 RectTransform");
            Assert.That(clearCacheRect, Is.Not.Null, "清除缓存按钮应有 RectTransform");

            // 子元素在**面板局部空间**的包围盒（y 向上为正）；面板下沿 = panelRect.rect.yMin
            var close = RectTransformUtility.CalculateRelativeRectTransformBounds(panelRect, closeRect);
            var version = RectTransformUtility.CalculateRelativeRectTransformBounds(panelRect, versionRect);
            var clearCache = RectTransformUtility.CalculateRelativeRectTransformBounds(panelRect, clearCacheRect);
            var bottomClearance = close.min.y - panelRect.rect.yMin;

            // ① 底部块不贴边：下沿距面板底 ≥ 90（微信胶囊 / 系统手势区）
            Assert.That(bottomClearance, Is.GreaterThanOrEqualTo(90f),
                $"关闭按钮下沿距面板底仅 {bottomClearance:0.#}：需求方要求底部块上移（V2.41，原值 60）");

            // ② 不被推到面板中部（真正的回归形态；宽松上限 = 面板高的 25%）
            Assert.That(bottomClearance, Is.LessThanOrEqualTo(panelRect.rect.height * 0.25f),
                $"关闭按钮下沿距面板底 {bottomClearance:0.#} 超过面板高度的 25%" +
                $"（{panelRect.rect.height * 0.25f:0.#}），底部块会被推到面板中部");

            // ③ 底部块（版本 + 关闭）不得与上方内容重叠——这才是原用例想守的不变量
            Assert.That(close.Intersects(clearCache), Is.False,
                $"关闭按钮与「清除缓存」重叠：close.min={close.min} close.max={close.max} " +
                $"clearCache.min={clearCache.min} clearCache.max={clearCache.max}");
            Assert.That(version.Intersects(clearCache), Is.False,
                $"版本号与「清除缓存」重叠：version.min={version.min} version.max={version.max} " +
                $"clearCache.min={clearCache.min} clearCache.max={clearCache.max}");

            // ④ 版本号在关闭按钮之上
            Assert.That(version.min.y, Is.GreaterThanOrEqualTo(close.max.y - 1f),
                $"版本号应位于关闭按钮之上（version.min.y={version.min.y:0.#}，close.max.y={close.max.y:0.#}）");
        }

        [Test]
        public void BottomBand_IsClearOfHudElements()
        {
            var failures = new List<string>();

            foreach (var pair in VisibleHudElements())
            {
                if (!TryGetDesignRect(pair.Value, out var rect, out _))
                    continue;

                var bottomMargin = UiDesignSpec.ReferenceHeight - rect.yMax;
                if (bottomMargin < UiDesignSpec.BottomClearance - Tolerance)
                    failures.Add($"{pair.Key} 侵入底部净空区，余量仅 {bottomMargin:0.#}");
            }

            Assert.That(failures, Is.Empty,
                $"底部 {UiDesignSpec.BottomClearance} 单位内不应有 HUD 元素（R27）：\n" + string.Join("\n", failures));
        }

        [Test]
        public void SideEntries_SitOnTheirOwnHalf()
        {
            var center = UiDesignSpec.ReferenceWidth * 0.5f;
            var failures = new List<string>();

            var leftEntries = new[] { _view.shakeButton };
            var rightEntries = new[] { _view.undoButton };

            foreach (var entry in leftEntries)
            {
                if (entry == null || !TryGetDesignRect(entry, out var rect, out _))
                    continue;

                if (rect.xMax > center)
                    failures.Add($"{entry.name}（左侧入口）右边缘 {rect.xMax:0.#} 越过中线 {center:0.#}");
            }

            foreach (var entry in rightEntries)
            {
                if (entry == null || !TryGetDesignRect(entry, out var rect, out _))
                    continue;

                if (rect.xMin < center)
                    failures.Add($"{entry.name}（右侧入口）左边缘 {rect.xMin:0.#} 越过中线 {center:0.#}");
            }

            Assert.That(failures, Is.Empty, string.Join("\n", failures));
        }

        [Test]
        public void Canvas_UsesPortraitReferenceResolution()
        {
            var scaler = _canvas.GetComponent<CanvasScaler>();
            Assert.That(scaler, Is.Not.Null, "Canvas 应配置 CanvasScaler");

            Assert.That(_canvas.renderMode, Is.EqualTo(RenderMode.ScreenSpaceOverlay));
            Assert.That(scaler.uiScaleMode, Is.EqualTo(CanvasScaler.ScaleMode.ScaleWithScreenSize));
            Assert.That(scaler.referenceResolution.x, Is.EqualTo(UiDesignSpec.ReferenceWidth).Within(1f));
            Assert.That(scaler.referenceResolution.y, Is.EqualTo(UiDesignSpec.ReferenceHeight).Within(1f));
            Assert.That(scaler.referenceResolution.y, Is.GreaterThan(scaler.referenceResolution.x),
                "应为竖屏参考分辨率（高 > 宽）");
        }

        /// <summary>
        /// HUD 内不得存在「铺满屏幕 + 不透明」的图形：Canvas 是 ScreenSpaceOverlay，永远绘制在世界之上，
        /// 一块不透明的满屏底板会盖住水果、容器、警戒线与落点预览线等全部世界内容。
        ///
        /// 曾真实发生：Canvas/Background（满屏、alpha=1）把整个世界遮住，玩家只能看到 HUD，
        /// 表现为「无法拖动水果确定落点」「改了预览与物理参数也没区别」。
        /// 底色应由相机 SolidColor 清屏提供，见 <see cref="MergeWater.Editor.UiDesignSpec.BackgroundColor"/>。
        /// </summary>
        [Test]
        public void NoOpaqueFullScreenGraphic_HidesTheGameWorld()
        {
            var failures = new List<string>();

            foreach (var graphic in _canvas.GetComponentsInChildren<Graphic>(true))
            {
                if (!graphic.isActiveAndEnabled)
                    continue; // 未显示的面板 / Toast 不参与判定

                if (graphic.color.a < 0.99f)
                    continue; // 半透明遮罩按设计允许（模态面板的 Dim 为 0.62）

                if (!CoversDesignCanvas(graphic.transform))
                    continue;

                failures.Add($"{HierarchyPath(graphic.transform)} color={graphic.color}");
            }

            Assert.That(failures, Is.Empty,
                "HUD 存在满屏不透明底板，会挡住全部世界内容（玩家将看不到对局画面）：\n" +
                string.Join("\n", failures));
        }

        /// <summary>沿父链判断该节点是否（连同祖先）铺满整个画布。</summary>
        private bool CoversDesignCanvas(Transform start)
        {
            var node = start;

            while (node != null && node != _canvas.transform)
            {
                if (!(node is RectTransform rect))
                    return false;

                if (!Mathf.Approximately(rect.anchorMin.x, 0f) || !Mathf.Approximately(rect.anchorMin.y, 0f) ||
                    !Mathf.Approximately(rect.anchorMax.x, 1f) || !Mathf.Approximately(rect.anchorMax.y, 1f))
                    return false;

                if (rect.offsetMin.sqrMagnitude > Tolerance || rect.offsetMax.sqrMagnitude > Tolerance)
                    return false;

                node = node.parent;
            }

            return node == _canvas.transform;
        }

        private static string HierarchyPath(Transform transform)
        {
            var path = transform.name;
            var node = transform.parent;

            while (node != null)
            {
                path = node.name + "/" + path;
                node = node.parent;
            }

            return path;
        }
    }
}
