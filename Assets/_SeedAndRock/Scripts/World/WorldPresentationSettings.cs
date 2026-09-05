using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SeedAndRock.World
{
    /// <summary>Calm, readable atmosphere defaults shared by the scene light, fog, ambient light and camera.</summary>
    [CreateAssetMenu(fileName = "SR_WorldPresentationSettings", menuName = "SeedAndRock/World Presentation Settings")]
    public sealed class WorldPresentationSettings : ScriptableObject
    {
        [Header("Sun")]
        public Color sunColor = new Color(1.0f, 0.93f, 0.82f);
        [Range(0f, 4f)] public float sunIntensity = 1.35f;
        public Vector3 sunEulerAngles = new Vector3(42f, -32f, 0f);
        public LightShadows sunShadows = LightShadows.Soft;
        [Range(0f, 1f)] public float shadowStrength = 0.82f;

        [Header("Ambient")]
        public Color ambientSky = new Color(0.52f, 0.66f, 0.82f);
        public Color ambientEquator = new Color(0.50f, 0.55f, 0.46f);
        public Color ambientGround = new Color(0.20f, 0.22f, 0.17f);

        [Header("Fog")]
        public bool fogEnabled = true;
        public Color fogColor = new Color(0.68f, 0.78f, 0.86f);
        [Range(0f, 0.02f)] public float fogDensity = 0.0028f;

        [Header("Camera")]
        public AntialiasingMode antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
        public AntialiasingQuality antialiasingQuality = AntialiasingQuality.High;
        [Range(100f, 5000f)] public float farClipPlane = 1500f;
    }
}
