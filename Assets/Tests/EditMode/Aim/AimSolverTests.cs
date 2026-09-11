using System.Collections.Generic;
using MergeWater.Aim;
using NUnit.Framework;
using UnityEngine;

namespace MergeWater.Tests.EditMode
{
    /// <summary>M4 验收 A1–A5：瞄准纯计算（R1、V2.26）。</summary>
    public sealed class AimSolverTests
    {
        private const float MinX = -2.2f;
        private const float MaxX = 2.2f;

        [Test]
        public void ClampX_KeepsDropInsideBoundsAccountingForRadius()
        {
            Assert.That(AimSolver.ClampX(0f, MinX, MaxX, 0.5f, out var inverted), Is.EqualTo(0f).Within(1e-4f));
            Assert.That(inverted, Is.False);

            Assert.That(AimSolver.ClampX(99f, MinX, MaxX, 0.5f, out _), Is.EqualTo(1.7f).Within(1e-4f),
                "右侧夹取到 maxX − radius");
            Assert.That(AimSolver.ClampX(-99f, MinX, MaxX, 0.5f, out _), Is.EqualTo(-1.7f).Within(1e-4f));

            Assert.That(AimSolver.ClampX(1.8f, MinX, MaxX, 0.18f, out _), Is.EqualTo(1.8f).Within(1e-4f),
                "半径更小时可放得更靠边");
        }

        [Test]
        public void ClampX_WhenBoundsInvert_ReturnsMidpoint()
        {
            var value = AimSolver.ClampX(0.2f, 0f, 0.5f, 1f, out var inverted);

            Assert.That(inverted, Is.True);
            Assert.That(value, Is.EqualTo(0.25f).Within(1e-4f), "区间反转取中点，不产生非法区间");
        }

        [Test]
        public void ClampX_WithoutRadius_MatchesRawBounds()
        {
            Assert.That(AimSolver.ClampX(5f, MinX, MaxX, 0f, out _), Is.EqualTo(MaxX).Within(1e-4f));
        }

        [Test]
        public void NormalizeX_IsWithinZeroToOne()
        {
            Assert.That(AimSolver.NormalizeX(MinX, MinX, MaxX), Is.EqualTo(0f).Within(1e-4f));
            Assert.That(AimSolver.NormalizeX(MaxX, MinX, MaxX), Is.EqualTo(1f).Within(1e-4f));
            Assert.That(AimSolver.NormalizeX(0f, MinX, MaxX), Is.EqualTo(0.5f).Within(1e-4f));
            Assert.That(AimSolver.NormalizeX(100f, MinX, MaxX), Is.EqualTo(1f).Within(1e-4f), "越界被夹取");
            Assert.That(AimSolver.NormalizeX(0f, 1f, 1f), Is.EqualTo(0.5f).Within(1e-4f), "退化区间返回中点");
        }

        [Test]
        public void SampleTrajectory_ReturnsOrderedPoints_AndRespectsMaxTime()
        {
            var points = new List<Vector2>();
            const float maxTime = 0.8f;
            const float step = 0.04f;

            var count = AimSolver.SampleTrajectory(
                new Vector2(0.5f, 5.2f), Vector2.zero, -9.81f, -4.2f, MinX, MaxX, 0.35f, maxTime, step, points);

            Assert.That(count, Is.GreaterThanOrEqualTo(2));
            Assert.That(count, Is.LessThanOrEqualTo(Mathf.CeilToInt(maxTime / step) + 2), "采样不超过时间上限");
            Assert.That(points.Count, Is.EqualTo(count));

            for (var i = 0; i < points.Count; i++)
            {
                Assert.That(points[i].x, Is.InRange(MinX - 1e-3f, MaxX + 1e-3f), "反弹不得越出左右墙");
                Assert.That(points[i].y, Is.GreaterThanOrEqualTo(-4.2f - 1e-3f), "不得穿透地面");
            }

            Assert.That(points[0].y, Is.EqualTo(5.2f).Within(1e-4f), "起点为生成点");
            Assert.That(points[1].y, Is.LessThan(points[0].y), "首个采样点应已下落");
        }

        [Test]
        public void SampleTrajectory_WithZeroGravity_ReturnsTwoPoints()
        {
            var points = new List<Vector2>();

            var count = AimSolver.SampleTrajectory(
                new Vector2(0f, 5f), Vector2.zero, 0f, -4.2f, MinX, MaxX, 0.35f, 0.8f, 0.04f, points);

            Assert.That(count, Is.EqualTo(2), "退化输入返回直线，不死循环");
            Assert.That(points[1].x, Is.EqualTo(0f).Within(1e-4f));
            Assert.That(points[1].y, Is.EqualTo(-4.2f).Within(1e-4f));
        }

        [Test]
        public void SampleTrajectory_WithInitialHorizontalVelocity_ProducesParabola()
        {
            var points = new List<Vector2>();

            AimSolver.SampleTrajectory(
                new Vector2(-2f, 0f), new Vector2(8f, 4f), -9.81f, -4.2f, MinX, MaxX, 0.5f, 0.8f, 0.04f, points);

            var sawPositiveHorizontalProgress = false;
            var sawHorizontalReversal = false;

            for (var i = 1; i < points.Count; i++)
            {
                var dx = points[i].x - points[i - 1].x;
                if (dx > 0f)
                    sawPositiveHorizontalProgress = true;
                if (sawPositiveHorizontalProgress && dx < 0f)
                    sawHorizontalReversal = true;
            }

            Assert.That(sawPositiveHorizontalProgress, Is.True, "存在水平速度时应产生可见抛物线");
            Assert.That(sawHorizontalReversal, Is.True, "触墙后应镜像反射");
        }

        [Test]
        public void SampleTrajectory_WithNullOutput_ReturnsZero()
        {
            Assert.That(AimSolver.SampleTrajectory(Vector2.zero, Vector2.zero, -9.81f, -4f, MinX, MaxX, 0.3f, 0.8f,
                0.04f, null), Is.EqualTo(0));
        }
    }
}
