using System.Collections.Generic;
using UnityEngine;

namespace MergeWater.Aim
{
    /// <summary>
    /// 瞄准相关的纯计算（可脱离 MonoBehaviour 单测）。
    /// 投放本身为垂直下落（决策 D8），预览线在触及堆叠后按镜像反射预测反弹路径（V2.26）。
    /// </summary>
    public static class AimSolver
    {
        private const int MaxSamples = 128;

        /// <summary>落点夹取：保证水果完整落在场地内。区间因半径反转时返回中点并置 <paramref name="inverted"/>。</summary>
        public static float ClampX(float rawX, float minX, float maxX, float radius, out bool inverted)
        {
            var low = minX + radius;
            var high = maxX - radius;
            inverted = low > high;

            if (inverted)
                return (minX + maxX) * 0.5f;

            return Mathf.Clamp(rawX, low, high);
        }

        /// <summary>落点在场地内的归一化位置，用于表现层定位。</summary>
        public static float NormalizeX(float x, float minX, float maxX)
        {
            if (maxX - minX <= 0f)
                return 0.5f;

            return Mathf.Clamp01((x - minX) / (maxX - minX));
        }

        /// <summary>
        /// 采样预测路径。重力 ≤0 且初速为 0（退化输入）时返回两点直线，绝不进入死循环。
        /// </summary>
        public static int SampleTrajectory(
            Vector2 start,
            Vector2 velocity,
            float gravity,
            float floorY,
            float minX,
            float maxX,
            float restitution,
            float maxTime,
            float step,
            List<Vector2> output)
        {
            if (output == null)
                return 0;

            output.Clear();
            output.Add(start);

            if (step <= 0f)
                step = 0.02f;

            if (maxTime <= 0f)
            {
                output.Add(start);
                return output.Count;
            }

            if (gravity >= -0.0001f && Mathf.Abs(velocity.y) < 0.0001f)
            {
                output.Add(new Vector2(start.x, Mathf.Min(start.y, floorY)));
                return output.Count;
            }

            restitution = Mathf.Clamp01(restitution);
            var position = start;
            var currentVelocity = velocity;
            var elapsed = 0f;
            var guard = 0;

            while (elapsed < maxTime && output.Count < MaxSamples && guard++ < 10000)
            {
                var dt = Mathf.Min(step, maxTime - elapsed);

                currentVelocity.y += gravity * dt;
                position += currentVelocity * dt;

                if (position.y <= floorY)
                {
                    position.y = floorY;
                    currentVelocity.y = -currentVelocity.y * restitution;
                    currentVelocity.x *= 0.98f;
                }

                if (position.x < minX)
                {
                    position.x = minX;
                    currentVelocity.x = -currentVelocity.x * restitution;
                }
                else if (position.x > maxX)
                {
                    position.x = maxX;
                    currentVelocity.x = -currentVelocity.x * restitution;
                }

                output.Add(position);
                elapsed += dt;
            }

            return output.Count;
        }
    }
}
