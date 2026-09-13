using System.Collections;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using MergeWater.Aim;
using MergeWater.Bootstrap;
using MergeWater.Core;
using MergeWater.Field;
using MergeWater.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace MergeWater.Tests.PlayMode
{
    /// <summary>
    /// 物理与瞄准预览的诊断测试：在真实场景里复现玩家操作，把可观察结果写成断言与截图
    /// （截图输出到 Logs/Diagnostics/）。
    ///
    /// 存在理由：玩家反馈「看不出落点」「水果碰撞后堆在同一层」。这两条只能靠真的投放并观察物理来判定。
    /// 本测试已发现并驱动修复两个缺陷：
    /// （1）预览线把下落段也算进 0.8s 预算，而落到地面需约 1.4s，于是线在半空截断（y≈2.06），看不出落点；
    /// （2）圆形碰撞体角阻尼取默认值，落地后一路滚到墙角（实测偏移 1.95），摊平成一层而非堆成堆。
    /// </summary>
    public sealed class PhysicsAndPreviewDiagnostics
    {
        private const string SceneName = "Main";
        private const int CaptureWidth = 600;
        private const int CaptureHeight = 1000;


        private static readonly string SavePath = Path.Combine(Application.persistentDataPath, "mwater_save.json");

        private Scene _scene;
        private bool _loaded;
        private bool _hadSave;
        private string _saveBackup;

        [SetUp]
        public void SetUp()
        {
            _hadSave = File.Exists(SavePath);
            if (_hadSave)
            {
                _saveBackup = File.ReadAllText(SavePath);
                File.Delete(SavePath);
            }
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_loaded)
            {
                var unload = SceneManager.UnloadSceneAsync(_scene);
                while (unload != null && !unload.isDone)
                    yield return null;

                _loaded = false;
                yield return null;
            }

            if (_hadSave)
                File.WriteAllText(SavePath, _saveBackup);
            else if (File.Exists(SavePath))
                File.Delete(SavePath);

            Resources.UnloadUnusedAssets();
            System.GC.Collect();
            System.GC.WaitForPendingFinalizers();
            yield return null;
        }

        private IEnumerator LoadAndAccept()
        {
            LogAssert.Expect(LogType.Log, new Regex("未指派 BGM"));

            var load = SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Additive);
            while (!load.isDone)
                yield return null;

            _scene = SceneManager.GetSceneByName(SceneName);
            _loaded = true;
            yield return null;
            yield return null;

            // Demo 版入口是加载页（不再是隐私弹窗）：等它结束，结束后直接开局。
            var bootstrapper = Object.FindObjectOfType<GameBootstrapper>();
            bootstrapper.RequireTapToStart = false;   // 测试接缝：不点「开始」，进度满即开局
            var deadline = Time.realtimeSinceStartup + 10f;
            while (Time.realtimeSinceStartup < deadline && bootstrapper.Context.Session == null)
                yield return null;

            Assert.That(bootstrapper.Context.Session, Is.Not.Null, "应已开局");

            // 开局当帧输入仍被屏蔽（GameBootstrapper.Update 下一帧才放行），等瞄准真的可交互。
            var aim = Object.FindObjectOfType<AimController>();
            var interactableDeadline = Time.realtimeSinceStartup + 5f;
            while (Time.realtimeSinceStartup < interactableDeadline && aim != null && !aim.Interactable)
                yield return null;
        }

        private static void DropAt(AimController aim, Camera camera, float worldX)
        {
            var screen = camera.WorldToScreenPoint(new Vector3(worldX, 0f, 0f));
            var point = new Vector2(screen.x, screen.y);
            aim.HandleFrame(new PointerFrame(true, true, false, true, point));
            aim.HandleFrame(new PointerFrame(false, false, true, true, point));
        }

        /// <summary>
        /// 是否存在真实图形设备。批处理 -nographics 下 GfxDevice 为空，
        /// 调用 Camera.Render() 会让进程 SIGSEGV（不可捕获），因此必须前置跳过。
        /// </summary>
        private static bool CanRender =>
            SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null;

        private static void CaptureCamera(Camera camera, string fileName)
        {
            var directory = Path.Combine("Logs", "Diagnostics");
            Directory.CreateDirectory(directory);

            if (!CanRender)
            {
                File.WriteAllText(Path.Combine(directory, fileName + ".skipped.txt"),
                    "no graphics device (batch -nographics)");
                return;
            }

            // 只渲染世界，不伪装 Canvas：早期为了把 HUD 拍进图里，临时把 Canvas 切到
            // ScreenSpaceCamera，结果 UI（排序号 0）压住了排序号为负的容器墙体，
            // 让截图误示「看不到容器」。世界截图才是可信的物理证据。
            var previous = camera.targetTexture;
            var target = new RenderTexture(CaptureWidth, CaptureHeight, 24);
            camera.targetTexture = target;
            camera.Render();

            var active = RenderTexture.active;
            RenderTexture.active = target;

            var texture = new Texture2D(CaptureWidth, CaptureHeight, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, CaptureWidth, CaptureHeight), 0, 0);
            texture.Apply();

            camera.targetTexture = previous;
            RenderTexture.active = active;

            File.WriteAllBytes(Path.Combine(directory, fileName), texture.EncodeToPNG());
            Object.Destroy(texture);
            Object.Destroy(target);
        }

        private static string DumpFruits(GameField field)
        {
            var builder = new StringBuilder();
            var bodies = Object.FindObjectsOfType<FruitBody>();

            builder.AppendLine("fruitCount=" + bodies.Length
                               + "  liveFromField=" + field.LiveFruitCount
                               + "  gravity=" + Physics2D.gravity
                               + "  timeScale=" + Time.timeScale);

            for (var i = 0; i < bodies.Length; i++)
            {
                var body = bodies[i];
                builder.AppendLine("  id=" + body.FruitId + " lvl=" + body.Level
                                   + " r=" + body.Radius.ToString("0.00")
                                   + " pos=" + body.transform.position.x.ToString("0.00") + ","
                                   + body.transform.position.y.ToString("0.00")
                                   + " vel=" + body.Body.velocity.x.ToString("0.00") + ","
                                   + body.Body.velocity.y.ToString("0.00")
                                   + " angVel=" + body.Body.angularVelocity.ToString("0.0")
                                   + " drag=" + body.Body.drag.ToString("0.00")
                                   + " angDrag=" + body.Body.angularDrag.ToString("0.00")
                                   + " settled=" + body.SettledDuration.ToString("0.00"));
            }

            return builder.ToString();
        }

        private static void WriteDiagnostics(string name, string content)
        {
            var directory = Path.Combine("Logs", "Diagnostics");
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, name), content);
        }

        private static void AssertNoDeepInterpenetration(FruitBody[] bodies, string dump)
        {
            for (var i = 0; i < bodies.Length; i++)
            {
                for (var j = i + 1; j < bodies.Length; j++)
                {
                    var a = bodies[i];
                    var b = bodies[j];
                    var distance = ((Vector2)a.transform.position - (Vector2)b.transform.position).magnitude;
                    var contact = a.Radius + b.Radius;

                    Assert.That(distance, Is.GreaterThanOrEqualTo(contact * 0.9f),
                        "水果 id=" + a.FruitId + " 与 id=" + b.FruitId + " 互相穿插：distance="
                        + distance.ToString("0.000") + " < contact=" + contact.ToString("0.000") + "\n" + dump);
                }
            }
        }

        private static void AssertAllDynamic(FruitBody[] bodies, string dump)
        {
            foreach (var body in bodies)
            {
                Assert.That(body.Body.bodyType, Is.EqualTo(RigidbodyType2D.Dynamic),
                    "水果 id=" + body.FruitId + " 被冻结（非 Dynamic）\n" + dump);
            }
        }

        [UnityTest]
        public IEnumerator DroppedFruits_FallMergeAndDoNotScatterToWalls()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return LoadAndAccept();

            var context = Object.FindObjectOfType<GameBootstrapper>().Context;
            var aim = Object.FindObjectOfType<AimController>();
            var camera = Camera.main;
            var field = Object.FindObjectOfType<GameField>();

            // 全部投在同一 x：这是「滚到墙角、摊平一层」最容易出现的场景。
            // 两种初始等级共 6 次投放，鸽巢原理保证至少一对同级相遇，必然发生合成。
            const int drops = 6;
            const float dropX = 0f;
            for (var i = 0; i < drops; i++)
            {
                DropAt(aim, camera, dropX);
                yield return new WaitForSeconds(context.Balance.DropCooldownSeconds + 0.05f);
            }

            yield return new WaitForSeconds(2.5f);

            var dump = DumpFruits(field);
            WriteDiagnostics("after-6-drops.txt", dump);
            CaptureCamera(camera, "after-6-drops.png");

            var bodies = Object.FindObjectsOfType<FruitBody>();
            Assert.That(bodies.Length, Is.GreaterThan(0), "应至少留下水果");

            var spawnY = context.Balance.DropSpawnY;
            var floorY = field.PlayFloorY;

            // 1) 全都落下来了（不是悬在生成高度），也没有穿过地面
            foreach (var body in bodies)
            {
                Assert.That(body.transform.position.y, Is.LessThan(spawnY - 0.5f),
                    "水果 id=" + body.FruitId + " 没有下落，仍在生成高度附近 y="
                    + body.transform.position.y.ToString("0.00") + "\n" + dump);

                Assert.That(body.transform.position.y, Is.GreaterThan(floorY - 0.1f),
                    "水果 id=" + body.FruitId + " 穿过了地面 y="
                    + body.transform.position.y.ToString("0.00") + "\n" + dump);
            }

            // 2) 不互相穿插（圆心距判定；并排接触是合法状态，不能误判为重叠）
            AssertNoDeepInterpenetration(bodies, dump);

            // 3) 不应滚到墙角：本次修复的核心断言。修复前实测偏移达 1.95（正好停在左墙）。
            //    判据由场地几何推导：贴墙静止位是 (场地半宽 − 半径)，超过即「滚到墙上」；
            //    修复前实测偏移 1.95，正好落在左墙静止位 (2.2 − 0.24 = 1.96)。
            var maxRadius = 0f;
            foreach (var body in bodies)
                maxRadius = Mathf.Max(maxRadius, body.Radius);

            var maxScatterFromDrop = field.PlayHalfWidth - maxRadius - 0.15f;
            foreach (var body in bodies)
            {
                var scatter = Mathf.Abs(body.transform.position.x - dropX);
                Assert.That(scatter, Is.LessThanOrEqualTo(maxScatterFromDrop),
                    "水果 id=" + body.FruitId + " 散得太远：距投放点 " + scatter.ToString("0.00")
                    + " > " + maxScatterFromDrop + "（滚动阻尼不足会让水果滚到墙角、摊平成一层）\n" + dump);
            }

            AssertAllDynamic(bodies, dump);

            // 4) 同级碰撞真的合成了：6 次投放后剩余必须少于投放数
            Assert.That(field.LiveFruitCount, Is.LessThan(drops),
                drops + " 次同 x 投放后仍剩 " + field.LiveFruitCount + " 颗，说明同级碰撞没有合成。\n" + dump);
        }

        [UnityTest]
        public IEnumerator FruitsOfDifferentTiers_StackOnEachOther()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return LoadAndAccept();

            var context = Object.FindObjectOfType<GameBootstrapper>().Context;
            var field = Object.FindObjectOfType<GameField>();
            var camera = Camera.main;

            // 用互不相同、且永不相邻同级的等级叠放：因为不会合成，才能确定性地验证「会不会叠起来」。
            // 4 颗的直径合计 (0.92+1.30+1.76+2.00)=5.98 > 容器宽 4.4，因此必然有水果被顶到别的水果之上。
            // 这也是玩家抱怨的场景：以前它们会滚开摊平在地面，现在必须叠住。
            // 顶级由 11 级下调为 10 级（2026-09-13），故末位取 10 而不是 11。
            var tiers = new[] { 5, 7, 9, 10 };
            for (var i = 0; i < tiers.Length; i++)
            {
                Assert.That(field.SpawnAt(tiers[i], new Vector2(0f, field.PlayFloorY + 0.6f + i * 2.6f), out _),
                    Is.True, "spawn tier " + tiers[i]);
                yield return new WaitForSeconds(0.5f);
            }

            yield return new WaitForSeconds(2.5f);

            var dump = DumpFruits(field);
            WriteDiagnostics("stacked-tiers.txt", dump);
            CaptureCamera(camera, "stacked-tiers.png");

            var bodies = Object.FindObjectsOfType<FruitBody>();
            Assert.That(bodies.Length, Is.EqualTo(tiers.Length), "不同等级不应发生合成\n" + dump);

            var floorY = field.PlayFloorY;

            // 至少有一颗水果不在地面上 —— 即它被别的水果托住了，堆叠真的发生。
            var stackedCount = 0;
            foreach (var body in bodies)
            {
                var bottom = body.transform.position.y - body.Radius;
                if (bottom > floorY + 0.35f)
                    stackedCount++;
            }

            Assert.That(stackedCount, Is.GreaterThanOrEqualTo(1),
                "没有任何水果被托起，说明它们都摊在地面上而不是叠起来。\n" + dump);

            AssertNoDeepInterpenetration(bodies, dump);
            AssertAllDynamic(bodies, dump);
        }

        [UnityTest]
        public IEnumerator Aiming_ShowsPreviewReachingTheLandingSurface()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return LoadAndAccept();

            var context = Object.FindObjectOfType<GameBootstrapper>().Context;
            var aim = Object.FindObjectOfType<AimController>();
            var camera = Camera.main;
            var preview = Object.FindObjectOfType<AimPreviewView>();
            var field = Object.FindObjectOfType<GameField>();

            // 进入瞄准态（按住不放）并拖到 x=+1.0
            var start = camera.WorldToScreenPoint(new Vector3(0f, 0f, 0f));
            aim.HandleFrame(new PointerFrame(true, true, false, true, new Vector2(start.x, start.y)));

            var drag = camera.WorldToScreenPoint(new Vector3(1.0f, 0f, 0f));
            aim.HandleFrame(new PointerFrame(false, true, false, true, new Vector2(drag.x, drag.y)));

            yield return null;
            yield return null;

            CaptureCamera(camera, "while-aiming.png");

            var line = preview.GetComponentInChildren<LineRenderer>();
            Assert.That(line, Is.Not.Null, "应有预测路径 LineRenderer");
            Assert.That(aim.State.IsAiming, Is.True, "按住后应处于瞄准态");
            Assert.That(aim.State.X, Is.EqualTo(1.0f).Within(0.05f), "拖动应更新落点 x");
            Assert.That(line.enabled, Is.True, "瞄准时预测路径应可见");
            Assert.That(line.positionCount, Is.GreaterThanOrEqualTo(2), "预测路径应至少 2 个点");
            Assert.That(line.startWidth, Is.GreaterThan(0f), "线宽应大于 0");

            // 关键：路径必须真的画到落点表面。修复前只画 0.8s 的下落，断在半空（y≈2.06）。
            var buffer = new Vector3[line.positionCount];
            line.GetPositions(buffer);
            var lowestY = float.MaxValue;
            for (var i = 0; i < buffer.Length; i++)
                lowestY = Mathf.Min(lowestY, buffer[i].y);

            // 方案 B：地面在屏幕底边上方（V2.42 起抬起 FloorScreenInset），预测线应画到该地面。
            var floorY = field.PlayFloorY;
            Assert.That(lowestY, Is.LessThanOrEqualTo(floorY + 0.2f),
                "预测路径最低只到 y=" + lowestY.ToString("0.00") + "，没有画到地面 y="
                + floorY.ToString("0.00") + " —— 玩家看不出落点");

            var pending = preview.GetComponentInChildren<SpriteRenderer>();
            Assert.That(pending, Is.Not.Null, "应有待投水果");
            Assert.That(pending.enabled, Is.True, "待投水果应可见");
            Assert.That(pending.sprite, Is.Not.Null, "待投水果应有 sprite");

            // 待投水果必须落在 HUD 顶栏之下的可见区间。
            // HUD 顶栏（分数/阶段进度等中央列）约占屏幕顶部 15.1%（290/1920 设计单位）；
            // 生成高度 5.2 时待投水果位于顶部约 7%，会被大号分数挡住，玩家「看不到要投的水果」。
            var worldTop = camera.transform.position.y + camera.orthographicSize;
            var worldBottom = camera.transform.position.y - camera.orthographicSize;
            var centerFromTop = (worldTop - pending.transform.position.y) / (worldTop - worldBottom);
            const float hudTopBarFraction = 0.151f;

            Assert.That(centerFromTop, Is.GreaterThan(hudTopBarFraction),
                "待投水果被 HUD 顶栏遮挡：其中心位于屏幕顶部 " + (centerFromTop * 100f).ToString("0.0")
                + "%，而顶栏占到 " + (hudTopBarFraction * 100f).ToString("0.0") + "%");

            // 方案 B「屏幕即边框」：容器外观默认隐藏，但碰撞体必须保留，否则水果会掉出场地。
            var arena = Object.FindObjectOfType<GameField>().transform.Find("Arena");
            Assert.That(arena, Is.Not.Null, "应生成 Arena 容器（碰撞体仍在）");

            for (var i = 0; i < arena.childCount; i++)
            {
                var wall = arena.GetChild(i);
                var wallCollider = wall.GetComponent<BoxCollider2D>();
                Assert.That(wallCollider, Is.Not.Null, wall.name + " 必须保留碰撞体");
                Assert.That(wallCollider.enabled, Is.True, wall.name + " 的碰撞体必须启用");

                var visual = wall.Find("Visual");
                var wallRenderer = visual != null ? visual.GetComponent<SpriteRenderer>() : null;
                Assert.That(wallRenderer == null || !wallRenderer.enabled, Is.True,
                    wall.name + " 的可见外观应已隐藏（方案 B：屏幕即边框，不再画棕色框）");
            }

            // 场地边界必须贴合屏幕：左右墙在屏幕左右边缘、地面在屏幕底（否则水果会悬空停住）。
            var playHalfWidth = field.PlayHalfWidth;
            var screenHalfWidth = camera.orthographicSize * camera.aspect;
            Assert.That(playHalfWidth, Is.EqualTo(screenHalfWidth).Within(0.05f),
                "场地半宽应等于屏幕可视半宽（实测 " + playHalfWidth.ToString("0.00")
                + " vs " + screenHalfWidth.ToString("0.00") + "）");

            var playFloorY = field.PlayFloorY;
            var screenBottom = camera.transform.position.y - camera.orthographicSize;
            // V2.42（需求方「底部调上面一点」）：地面不再贴死屏幕底，而是抬起 FloorScreenInset，
            // 否则水果落地后紧贴最后一行像素、看起来被下边缘切掉。左右墙仍然贴屏幕左右边缘。
            var expectedInset = context.Balance.FloorScreenInset;
            Assert.That(expectedInset, Is.GreaterThan(0f),
                "地面必须从屏幕底边抬起一段（V2.42）");
            Assert.That(playFloorY, Is.EqualTo(screenBottom + expectedInset).Within(0.05f),
                "地面应位于「屏幕底边 + FloorScreenInset」（实测 " + playFloorY.ToString("0.00")
                + " vs 期望 " + (screenBottom + expectedInset).ToString("0.00") + "）");

            // 警戒线在常态下也必须看得见（早期白色 35% 透明画在米色背景上，等于没有）。
            var danger = Object.FindObjectOfType<DangerLineView>();
            var dangerLine = danger.GetComponentInChildren<LineRenderer>();
            Assert.That(dangerLine, Is.Not.Null, "应有警戒线");
            Assert.That(dangerLine.startColor.a, Is.GreaterThanOrEqualTo(0.4f),
                "警戒线常态透明度过低，画面上看不见");

            // 生成点本身必须在警戒线之上。
            // 注意：不能要求「水果底边也在线上」——那会把生成高度顶回顶栏后面（两者不可兼得）。
            // 水果生成瞬间会短暂跨越警戒线，由 V2.9 的静止门槛兜住（未静止不计越线），
            // 该行为由 GameFieldDangerTests.FreshlySpawnedFruitAboveLine_IsNotReportedBeforeSettleGrace 覆盖。
            Assert.That(context.Balance.DropSpawnY, Is.GreaterThan(context.Balance.DangerLineY),
                "生成点必须在警戒线之上");

            WriteDiagnostics("aiming.txt",
                "aiming=" + aim.State.IsAiming + " x=" + aim.State.X.ToString("0.00")
                + " lineEnabled=" + line.enabled + " points=" + line.positionCount
                + " lowestY=" + lowestY.ToString("0.00") + " floorY=" + floorY.ToString("0.00")
                + " lineMaterial=" + (line.sharedMaterial == null ? "NULL" : line.sharedMaterial.shader.name)
                + " pendingSprite=" + (pending.sprite == null ? "NULL" : pending.sprite.name)
                + " pendingColor=" + pending.color
                + " pendingPos=" + pending.transform.position.ToString("0.00")
                + " centerFromTop=" + (centerFromTop * 100f).ToString("0.0") + "%");

            var up = new PointerFrame(false, false, true, true, new Vector2(drag.x, drag.y));
            aim.HandleFrame(up);
        }

        /// <summary>
        /// V2.33/V2.34：投放后下一颗待投水果不能立刻在原落点位置冒出，而是要
        /// ①回到屏幕中央、②先等待一小段时间、③再渐显出现。
        /// </summary>
        [UnityTest]
        public IEnumerator AfterDrop_PendingFruitReturnsToCenter_AndRevealsGradually()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return LoadAndAccept();

            var context = Object.FindObjectOfType<GameBootstrapper>().Context;
            var aim = Object.FindObjectOfType<AimController>();
            var camera = Camera.main;
            var preview = Object.FindObjectOfType<AimPreviewView>();
            var pending = preview.GetComponentInChildren<SpriteRenderer>();

            Assert.That(pending, Is.Not.Null, "应有待投水果");

            // 投到明显偏离中央的 x=+1.4，松手后下一颗必须回到中央。
            DropAt(aim, camera, 1.4f);
            yield return null;

            var balance = context.Balance;
            Assert.That(aim.State.X, Is.EqualTo(0f).Within(0.05f), "投放后待投位置应回到屏幕中央");
            Assert.That(preview.IsRevealing, Is.True, "投放后应进入「渐显」出现流程（V2.33/V2.34）");
            Assert.That(pending.color.a, Is.LessThan(0.5f),
                "延迟期间待投水果不应已经完整出现（玩家来不及看清上一颗）");

            var deadline = Time.realtimeSinceStartup + 3f;
            while (Time.realtimeSinceStartup < deadline && preview.IsRevealing)
                yield return null;

            Assert.That(preview.IsRevealing, Is.False, "渐显应在超时内完成");
            Assert.That(pending.enabled, Is.True, "渐显结束后待投水果应可见");
            Assert.That(pending.color.a, Is.GreaterThan(0.95f), "渐显结束后应完全不透明");
            Assert.That(pending.transform.position.x, Is.EqualTo(0f).Within(0.05f), "应出现在屏幕中央");
            Assert.That(pending.transform.position.y, Is.EqualTo(balance.DropSpawnY).Within(0.25f),
                "应出现在生成高度（含上下浮动余量）");
        }
    }
}
