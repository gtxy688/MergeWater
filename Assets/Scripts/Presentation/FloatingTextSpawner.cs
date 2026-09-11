using UnityEngine;

namespace MergeWater.Presentation
{
    /// <summary>飘字（R22）：连击/得分飘字，punch 缩放后上浮淡出。</summary>
    public sealed class FloatingTextSpawner : MonoBehaviour
    {
        [SerializeField] private Font font;
        [SerializeField] private float defaultCharacterSize = 0.14f;

        public void SetFont(Font textFont) => font = textFont;

        public void Spawn(Vector2 worldPosition, string text, Color color, float sizeMultiplier = 1f)
        {
            if (string.IsNullOrEmpty(text))
                return;

            var go = new GameObject("FloatingText");
            go.transform.SetParent(transform, false);
            go.transform.position = new Vector3(worldPosition.x, worldPosition.y, 0f);

            var mesh = go.AddComponent<TextMesh>();
            mesh.font = font != null ? font : UiFontProvider.Resolve();
            mesh.text = text;
            mesh.color = color;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.fontSize = 72;
            mesh.characterSize = defaultCharacterSize * Mathf.Max(0.1f, sizeMultiplier);

            var renderer = go.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                if (mesh.font != null)
                    renderer.sharedMaterial = mesh.font.material;

                renderer.sortingOrder = 600;
            }

            var item = go.AddComponent<FloatingTextItem>();
            item.Play(duration: 0.8f, rise: 1.0f * Mathf.Max(0.1f, sizeMultiplier));
        }
    }

    /// <summary>单条飘字的生命周期：上浮 + 淡出 + punch 缩放。</summary>
    public sealed class FloatingTextItem : MonoBehaviour
    {
        private TextMesh _mesh;
        private Color _baseColor;
        private float _duration = 0.8f;
        private float _remaining;
        private float _rise;
        private float _scaleBase = 1f;

        public void Play(float duration, float rise)
        {
            _mesh = GetComponent<TextMesh>();
            _baseColor = _mesh != null ? _mesh.color : Color.white;
            _duration = Mathf.Max(0.05f, duration);
            _remaining = _duration;
            _rise = rise;
        }

        private void Update()
        {
            if (_remaining <= 0f)
                return;

            var unscaled = Time.unscaledDeltaTime;
            _remaining = Mathf.Max(0f, _remaining - unscaled);
            var progress = 1f - _remaining / _duration;

            transform.position += Vector3.up * (_rise * unscaled / _duration);

            if (_mesh != null)
            {
                var color = _baseColor;
                color.a = Mathf.Clamp01(1f - progress * progress);
                _mesh.color = color;
            }

            var punch = 1f + Mathf.Sin(Mathf.Clamp01(progress * 3f) * Mathf.PI) * 0.35f;
            transform.localScale = Vector3.one * punch;

            if (_remaining <= 0f)
                Destroy(gameObject);
        }
    }
}
