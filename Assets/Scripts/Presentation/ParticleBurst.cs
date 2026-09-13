using UnityEngine;

namespace MergeWater.Presentation
{
    /// <summary>
    /// 合成粒子爆发（R22）。用一个常驻 <see cref="ParticleSystem"/>（发射率为 0）按需 Emit，
    /// 避免每次合成实例化对象。粒子贴图由场景里这个常驻对象指派（在编辑器里手工换）。
    /// </summary>
    public sealed class ParticleBurst : MonoBehaviour
    {
        [SerializeField] private ParticleSystem system;

        public void Configure(ParticleSystem particleSystem)
        {
            system = particleSystem;
            if (system == null)
                return;

            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            system.Play();
        }

        public void Play(Vector2 worldPosition, Color color, int count, float sizeMultiplier = 1f)
        {
            if (system == null || count <= 0)
                return;

            transform.position = new Vector3(worldPosition.x, worldPosition.y, 0f);

            var main = system.main;
            main.startColor = color;
            main.startSize = Mathf.Max(0.02f, 0.1f * sizeMultiplier);

            system.Emit(count);
        }
    }
}
