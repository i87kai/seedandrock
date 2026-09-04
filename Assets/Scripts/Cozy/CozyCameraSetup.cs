using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Cozy.Rendering
{
    /// <summary>
    /// Put this on any camera that should render the Cozy look.
    ///  * Makes sure the URP camera data has post-processing enabled (runtime
    ///    created cameras often miss this) and the depth/opaque textures the
    ///    water shader needs are requested.
    ///  * Drives the underwater state: when the camera goes below the water
    ///    surface it publishes _CozyUnderwaterParams/_CozyUnderwaterColor (used
    ///    by every Cozy shader for absorption) and optionally fades an
    ///    underwater Volume in, so swimming looks right without any extra
    ///    render features.
    /// Water level: assign a Transform (e.g. the water plane) or a fixed height.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    [AddComponentMenu("Cozy Rendering/Cozy Camera Setup")]
    public sealed class CozyCameraSetup : MonoBehaviour
    {
        private static readonly int UnderwaterParamsId = Shader.PropertyToID("_CozyUnderwaterParams");
        private static readonly int UnderwaterColorId = Shader.PropertyToID("_CozyUnderwaterColor");

        [Header("URP Camera")]
        public bool enablePostProcessing = true;
        [Tooltip("Cozy/Water refraction and depth foam need these. Also enabled on the active URP asset by the editor setup tool.")]
        public bool requestDepthAndOpaqueTextures = true;

        [Header("Underwater")]
        public bool underwaterEnabled = true;
        [Tooltip("Water surface reference. If null, Water Level is used.")]
        public Transform waterSurface;
        public float waterLevel = 0f;
        [Tooltip("Extra offset below the surface before the effect kicks in (avoids flicker at the waterline).")]
        public float surfaceOffset = 0.08f;
        public Color underwaterColor = new Color(0.08f, 0.36f, 0.50f, 1f);
        [Tooltip("Absorption per metre. Higher = murkier.")]
        [Range(0.005f, 0.5f)] public float absorption = 0.09f;
        [Tooltip("Optional Volume faded in while submerged (e.g. blue tint, more vignette, less bloom).")]
        public Volume underwaterVolume;
        [Range(1f, 30f)] public float blendSpeed = 10f;

        /// <summary>0 above water .. 1 fully submerged (smoothed).</summary>
        public float Submerged { get; private set; }
        public bool IsUnderwater => Submerged > 0.5f;

        private Camera cam;

        private void OnEnable()
        {
            cam = GetComponent<Camera>();
            ApplyCameraData();
            Publish(0f);
        }

        private void OnDisable()
        {
            Publish(0f);
            if (underwaterVolume != null) underwaterVolume.weight = 0f;
        }

        private void OnValidate()
        {
            if (isActiveAndEnabled) ApplyCameraData();
        }

        private void LateUpdate()
        {
            if (!underwaterEnabled)
            {
                Publish(0f);
                return;
            }

            float level = waterSurface != null ? waterSurface.position.y : waterLevel;
            float target = transform.position.y < level - surfaceOffset ? 1f : 0f;
            float dt = Application.isPlaying ? Time.deltaTime : 1f / 30f;
            Submerged = Mathf.MoveTowards(Submerged, target, dt * blendSpeed);
            Publish(Submerged);
        }

        private void Publish(float submerged)
        {
            float level = waterSurface != null ? waterSurface.position.y : waterLevel;
            Shader.SetGlobalVector(UnderwaterParamsId, new Vector4(submerged, absorption, level, 0f));
            Shader.SetGlobalColor(UnderwaterColorId, underwaterColor);
            if (underwaterVolume != null) underwaterVolume.weight = submerged;
        }

        private void ApplyCameraData()
        {
            if (cam == null) cam = GetComponent<Camera>();
            var data = cam.GetUniversalAdditionalCameraData();
            if (data == null) return;
            if (enablePostProcessing) data.renderPostProcessing = true;
            if (requestDepthAndOpaqueTextures)
            {
                data.requiresDepthOption = CameraOverrideOption.On;
                data.requiresColorOption = CameraOverrideOption.On;
            }
        }
    }
}
