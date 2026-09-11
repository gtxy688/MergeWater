using System.Collections;
using MergeWater.Core;
using MergeWater.Field;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MergeWater.Tests.PlayMode
{
    /// <summary>M2 验收 A5：越线物理查询与静止判定缓冲（R6、V2.8/V2.9）。</summary>
    public sealed class GameFieldDangerTests
    {
        private FieldTestRig _rig;

        [TearDown]
        public void TearDown()
        {
            _rig?.Dispose();
            _rig = null;
        }

        [UnityTest]
        public IEnumerator SettledFruitAboveLine_ReportsViolation()
        {
            _rig = new FieldTestRig(gravity: 0f);
            var line = _rig.Field.DangerLineY;
            var radius = _rig.Radius(1);

            _rig.Field.SpawnAt(1, new Vector2(0f, line + radius + 0.2f), out _);

            yield return new WaitForSeconds(_rig.Balance.DangerSettleGraceSeconds + 0.15f);

            Assert.That(_rig.Field.TryGetDangerViolation(out var violation), Is.True);
            Assert.That(violation.Level, Is.EqualTo(1));
            Assert.That(violation.Overflow, Is.GreaterThan(0f));
            Assert.That(violation.TopY, Is.GreaterThan(line));
        }

        [UnityTest]
        public IEnumerator SettledFruitBelowLine_DoesNotReportViolation()
        {
            _rig = new FieldTestRig(gravity: 0f);
            var line = _rig.Field.DangerLineY;

            _rig.Field.SpawnAt(1, new Vector2(0f, line - 1.5f), out _);

            yield return new WaitForSeconds(_rig.Balance.DangerSettleGraceSeconds + 0.15f);

            Assert.That(_rig.Field.TryGetDangerViolation(out _), Is.False);
        }

        [UnityTest]
        public IEnumerator FreshlySpawnedFruitAboveLine_IsNotReportedBeforeSettleGrace()
        {
            _rig = new FieldTestRig(gravity: 0f);
            var line = _rig.Field.DangerLineY;
            var radius = _rig.Radius(1);

            // 刚生成的水果速度为 0；若只看瞬时速度会误判为「已静止越线」。
            _rig.Field.SpawnAt(1, new Vector2(0f, line + radius + 0.2f), out _);

            yield return new WaitForFixedUpdate();

            Assert.That(_rig.Field.TryGetDangerViolation(out _), Is.False,
                "静止计时未达门槛前不应判为越线（V2.9 缓冲）");
        }

        [UnityTest]
        public IEnumerator FallingFruitAboveLine_IsNotReported()
        {
            _rig = new FieldTestRig(gravity: -20f);
            var line = _rig.Field.DangerLineY;

            _rig.Field.SpawnAt(1, new Vector2(0f, line + 1.5f), out _);

            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            Assert.That(_rig.Field.TryGetDangerViolation(out _), Is.False, "仍在掉落的水果不计入越线");
        }
    }
}
