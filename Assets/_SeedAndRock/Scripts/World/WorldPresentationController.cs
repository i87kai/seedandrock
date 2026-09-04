using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SeedAndRock.World
{
    /// <summary>Applies <see cref="WorldPresentationSettings"/> to the scene once at start and whenever a camera appears.</summary>
    [DisallowMultipleComponent]
    public sealed class WorldPresentationController : MonoBehaviour
    {
        [SerializeField] private WorldPresentationSettings presentation;
        [SerializeField] private Light sun;

        public WorldPresentationSettings Presentation => presentation;

        private void Start()
        {
            Apply();
        }

        public void Apply()
        {
            if (presentation == null) return;

            if (sun == null) sun = RenderSettings.sun;
            if (sun != null)
            {
                sun.color = presentation.sunColor;
                sun.intensity = presentation.sunIntensity;
                sun.shadows = presentation.sunShadows;
                sun.shadowStrength = presentation.shadowStrength;
                sun.transform.rotation = Quaternion.Euler(presentation.sunEulerAngles);
            }

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = presentation.ambientSky;
            RenderSettings.ambientEquatorColor = presentation.ambientEquator;
            RenderSettings.ambientGroundColor = presentation.ambientGround;
            RenderSettings.fog = presentation.fogEnabled;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = presentation.fogColor;
            RenderSettings.fogDensity = presentation.fogDensity;
        }

        /// <summary>Configures a gameplay camera for the chosen anti-aliasing and clip planes.</summary>
        public void ApplyToCamera(Camera camera)
        {
            if (camera == null || presentation == null) return;
            camera.farClipPlane = presentation.farClipPlane;
            UniversalAdditionalCameraData data = camera.GetUniversalAdditionalCameraData();
            if (data == null) return;
            data.renderPostProcessing = true;
            data.antialiasing = presentation.antialiasing;
            data.antialiasingQuality = presentation.antialiasingQuality;
        }
    }
}
