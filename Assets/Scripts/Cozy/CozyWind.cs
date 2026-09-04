using UnityEngine;

namespace Cozy.Rendering
{
    /// <summary>
    /// Publishes the global wind state consumed by CozyWind.hlsl (Cozy/Lit,
    /// Cozy/Foliage, Cozy/Grass). One instance per scene. All values are safe to
    /// animate at runtime (weather systems, storms) - shaders read them every frame.
    /// When no CozyWind is active the shaders fall back to a gentle default breeze.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("Cozy Rendering/Cozy Wind")]
    public sealed class CozyWind : MonoBehaviour
    {
        private static readonly int WindParamsId = Shader.PropertyToID("_CozyWindParams");
        private static readonly int WindParams2Id = Shader.PropertyToID("_CozyWindParams2");

        public static CozyWind Active { get; private set; }

        [Header("Direction")]
        [Tooltip("Compass direction the wind blows towards, degrees around Y. 0 = +Z.")]
        [Range(0f, 360f)] public float directionDegrees = 35f;
        [Tooltip("Slowly wander the direction over time for a less mechanical feel.")]
        [Range(0f, 90f)] public float directionWander = 12f;
        [Range(0f, 1f)] public float wanderSpeed = 0.05f;

        [Header("Strength")]
        [Tooltip("Metres of sway at the top of a ~6 m tree. 0.1 calm .. 1.5 storm.")]
        [Range(0f, 2f)] public float strength = 0.35f;
        [Tooltip("How fast waves travel through the foliage.")]
        [Range(0f, 4f)] public float speed = 1f;
        [Tooltip("Amount of travelling gust cells on top of the base sway.")]
        [Range(0f, 1f)] public float gustiness = 0.55f;
        [Tooltip("World-space size of the turbulence pattern (metres).")]
        [Range(2f, 80f)] public float turbulenceScale = 18f;
        [Tooltip("Multiplier for the small high-frequency leaf flutter.")]
        [Range(0f, 3f)] public float leafFlutter = 1f;

        /// <summary>Current wind direction (unit XZ vector) including wander.</summary>
        public Vector2 CurrentDirection { get; private set; } = Vector2.up;

        private void OnEnable()
        {
            Active = this;
            Apply();
        }

        private void OnDisable()
        {
            if (Active == this) Active = null;
            Shader.SetGlobalVector(WindParams2Id, Vector4.zero); // ready flag off -> shader defaults
        }

        private void OnValidate()
        {
            if (isActiveAndEnabled) Apply();
        }

        private void Update()
        {
            Apply();
        }

        private void Apply()
        {
            float t = Application.isPlaying ? Time.time : (float)(Time.realtimeSinceStartupAsDouble % 10000.0);
            float wander = (Mathf.PerlinNoise(t * wanderSpeed, 0.37f) - 0.5f) * 2f * directionWander;
            float rad = (directionDegrees + wander) * Mathf.Deg2Rad;
            CurrentDirection = new Vector2(Mathf.Sin(rad), Mathf.Cos(rad));

            Shader.SetGlobalVector(WindParamsId, new Vector4(CurrentDirection.x, CurrentDirection.y, strength, speed));
            Shader.SetGlobalVector(WindParams2Id, new Vector4(gustiness, turbulenceScale, leafFlutter, 1f));
        }

        /// <summary>Convenience for weather systems: blend towards a target wind over time.</summary>
        public void BlendTo(float targetStrength, float targetGustiness, float lerpFactor)
        {
            strength = Mathf.Lerp(strength, targetStrength, lerpFactor);
            gustiness = Mathf.Lerp(gustiness, targetGustiness, lerpFactor);
        }
    }
}
