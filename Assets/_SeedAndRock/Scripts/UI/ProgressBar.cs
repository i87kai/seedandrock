using UnityEngine;

namespace SeedAndRock.UI
{
    /// <summary>Progress bar that shows a determinate fill only when real progress exists; otherwise an animated sweep.</summary>
    public sealed class ProgressBar : MonoBehaviour
    {
        private RectTransform fill;
        private float? fraction;
        private float displayed;

        public void Initialize(RectTransform fillRect)
        {
            fill = fillRect;
            fraction = null;
        }

        /// <summary>Pass null for indeterminate.</summary>
        public void SetProgress(float? value)
        {
            if (value.HasValue && !fraction.HasValue) displayed = 0f;
            fraction = value.HasValue ? Mathf.Clamp01(value.Value) : (float?)null;
        }

        private void Update()
        {
            if (fill == null) return;
            if (fraction.HasValue)
            {
                displayed = Mathf.MoveTowards(displayed, fraction.Value, Time.unscaledDeltaTime * 1.5f);
                fill.anchorMin = new Vector2(0f, 0f);
                fill.anchorMax = new Vector2(Mathf.Max(displayed, 0.01f), 1f);
            }
            else
            {
                float t = (Time.unscaledTime * 0.55f) % 1f;
                float width = 0.28f;
                float start = Mathf.Lerp(-width, 1f, t);
                fill.anchorMin = new Vector2(Mathf.Clamp01(start), 0f);
                fill.anchorMax = new Vector2(Mathf.Clamp01(start + width), 1f);
            }

            fill.offsetMin = fill.offsetMax = Vector2.zero;
        }
    }
}
