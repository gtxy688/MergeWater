using System.Collections.Generic;
using System.Linq;
using System.Text;
using MergeWater.Presentation;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MergeWater.Tests.EditMode
{
    /// <summary>
    /// 「可交互控件不得嵌在 Toggle 的点击区里」门禁 —— 2026-09-13 实测缺陷的回归守卫。
    ///
    /// <para><b>缺陷现象</b>：打开设置页调整音量，音效/音乐开关的勾选会被自动取消。
    /// <b>成因不在业务代码，而在场景层级</b>：Unity 处理点击时是**沿父链向上找第一个实现者**——
    /// 按下时找到 `Slider`（它实现了 `IPointerDownHandler`）→ 音量正常改变；抬手时找
    /// `IPointerClickHandler`，而 <c>Slider</c> **不实现**这个接口、<c>Toggle</c> 实现了，
    /// 于是音量条的点击被外层的开关「吸走」→ `ToggleValue()` → 勾选翻转。
    /// 拖动位移超过点击阈值时 Unity 不发 click，所以现象时有时无，音效/音乐两行同因。</para>
    ///
    /// <para><b>为什么不能靠"补丁"了事</b>：给音量条挂一个空实现的 `IPointerClickHandler` 能截住事件，
    /// 但那只是把症状盖住——控件本身仍然待在别人的点击区里。本项目选择**结构性修法**：
    /// 可交互控件（`Slider`/`ScrollRect` 等自身不消费点击者）**不得**位于 `Toggle` 的层级内。</para>
    ///
    /// <para>两条断言分工：①结构化门禁（策略线，能在没人点之前就拦住）；②机制级断言（直接问事件系统
    /// 「点这里时解析到谁」，与 `StandaloneInputModule` 抬手发 click 用的是同一个 API）。</para>
    /// </summary>
    public sealed class SettingsToggleClickIsolationTests
    {
        private Scene _scene;

        [SetUp]
        public void SetUp()
        {
            _scene = EditorSceneManager.OpenScene(MergeWater.Editor.MainSceneAsset.Path, OpenSceneMode.Additive);
        }

        [TearDown]
        public void TearDown()
        {
            EditorSceneManager.CloseScene(_scene, true);
        }

        [Test]
        public void ClickableControlsInsideAToggle_DoNotExist()
        {
            var toggles = AllToggles();

            Assert.That(toggles.Length, Is.GreaterThan(0), "主场景里应有 Toggle（设置页的音效/音乐/震动）");

            var failures = new List<string>();
            var checkedControls = 0;

            foreach (var toggle in toggles)
            {
                foreach (var control in toggle.GetComponentsInChildren<Selectable>(true))
                {
                    // 行自身、以及本身就会消费点击的控件（Button/Toggle/Dropdown/TMP_InputField…）不算风险：
                    // 它们自己就是第一个 IPointerClickHandler，事件不会继续往上冒泡。
                    if (control == toggle || control is IPointerClickHandler)
                        continue;

                    if (!HasRaycastTarget(control))
                        continue; // 点不到的控件（纯装饰）不参与判定

                    checkedControls++;
                    failures.Add($"{PathOf(control.transform)}（位于 {PathOf(toggle.transform)} 内）");
                }
            }

            Assert.That(failures, Is.Empty,
                $"以下控件位于 Toggle 的层级内（本次检查 {checkedControls} 个可点控件），自身不实现 IPointerClickHandler，" +
                "点击会被外层 Toggle 吸走 → 连带切换开关（2026-09-13 实测：点音量条把开关的勾选取消了）：\n" +
                string.Join("\n", failures) +
                "\n结构性修法（二选一，任选其一即可）：\n" +
                "  ① 把该控件移出 Toggle 的层级（例：把 `SfxToggle/VolumeBar`、`MusicToggle/VolumeBar` 拖到 `Panel` 下，与开关行同级）；\n" +
                "  ② 或者把 Toggle 的点击区收小到勾选框上（带一张透明 Image 的小命中区），别让整行都是它的地盘。");
        }

        /// <summary>
        /// **机制级**断言：真去问事件系统「点这里时，沿层级找到的第一个点击处理器是谁」——
        /// 与 <c>StandaloneInputModule</c> 抬手发 click 时用的是同一个 API（<c>ExecuteEvents.GetEventHandler</c>）。
        ///
        /// <para>它不关心你用什么办法修（移出层级、缩小 Toggle 点击区…）：只要解析结果不再落到外层
        /// <c>Toggle</c> 就算过。上面那条管「结构上不许」，这条管「机制上真的不会误触」。</para>
        /// </summary>
        [Test]
        public void ClickingAVolumeBar_DoesNotResolveToAnEnclosingToggle()
        {
            var hud = _scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<HudView>(true))
                .FirstOrDefault();

            Assert.That(hud, Is.Not.Null, "主场景里应有 HudView");
            Assert.That(hud.sfxSlider, Is.Not.Null, "应有音效音量条");
            Assert.That(hud.musicSlider, Is.Not.Null, "应有音乐音量条");

            var failures = new List<string>();

            foreach (var slider in new[] { hud.sfxSlider, hud.musicSlider })
            {
                var toggle = slider.GetComponentInParent<Toggle>(true);
                if (toggle == null)
                    continue; // 已经不在 Toggle 层级里了——结构上就不可能被吸走

                // 模拟一次真实点击：命中点取该音量条上真正可点的图形（Track），没有就退回控件自身。
                var hit = FirstRaycastTarget(slider) ?? slider.gameObject;
                var resolved = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hit);

                if (resolved == toggle.gameObject)
                    failures.Add($"{PathOf(slider.transform)}：点击会解析到外层 Toggle " +
                                 $"({PathOf(toggle.transform)}) → 调音量会顺手把开关关掉");
            }

            Assert.That(failures, Is.Empty,
                "点击音量条时，事件系统沿层级找到的第一个 IPointerClickHandler 落在了外层 Toggle 上" +
                "（2026-09-13 实测缺陷：调音量把开关的勾选取消）：\n" + string.Join("\n", failures));
        }

        private Toggle[] AllToggles() => _scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Toggle>(true))
            .ToArray();

        /// <summary>该控件（含子对象）是否有实际可点的图形：启用的 Graphic + raycastTarget。</summary>
        private static bool HasRaycastTarget(Selectable control) =>
            control.GetComponentsInChildren<Graphic>(false).Any(g => g.raycastTarget);

        /// <summary>控件上第一个可点的图形（raycastTarget 打开的 Graphic）。</summary>
        private static GameObject FirstRaycastTarget(Selectable control)
        {
            var graphic = control.GetComponentsInChildren<Graphic>(false).FirstOrDefault(g => g.raycastTarget);
            return graphic != null ? graphic.gameObject : null;
        }

        private static string PathOf(Transform transform)
        {
            var builder = new StringBuilder(transform.name);
            for (var parent = transform.parent; parent != null; parent = parent.parent)
                builder.Insert(0, parent.name + "/");

            return builder.ToString();
        }
    }
}
