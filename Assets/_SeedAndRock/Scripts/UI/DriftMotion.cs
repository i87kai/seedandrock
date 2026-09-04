using UnityEngine;

namespace SeedAndRock.UI
{
    /// <summary>Slow sinusoidal drift for decorative menu shapes. Uses unscaled time so it keeps moving while paused.</summary>
    public sealed class DriftMotion : MonoBehaviour
    {
        private RectTransform rect;
        private Vector2 origin;
        private Vector2 amplitude = new Vector2(20f, 8f);
        private float speed = 0.1f;
        private float phase;

        public void Configure(Vector2 driftAmplitude, float driftSpeed, float phaseOffset = 0f)
        {
            amplitude = driftAmplitude;
            speed = driftSpeed;
            phase = phaseOffset;
        }

        private void Awake()
        {
            rect = GetComponent<RectTransform>();
            origin = rect.anchoredPosition;
        }

        private void OnDisable()
        {
            if (rect != null) rect.anchoredPosition = origin;
        }

        private void Update()
        {
            float t = Time.unscaledTime * speed * Mathf.PI * 2f + phase;
            rect.anchoredPosition = origin + new Vector2(Mathf.Sin(t) * amplitude.x, Mathf.Cos(t * 0.7f) * amplitude.y);
        }
    }
}
