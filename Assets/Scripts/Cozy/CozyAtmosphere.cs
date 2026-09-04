using UnityEngine;
using UnityEngine.Rendering;

namespace Cozy.Rendering
{
    /// <summary>
    /// Drives the whole "cozy" atmosphere from a single time-of-day value:
    /// sun/moon light direction, colour and intensity, sky colours, ambient
    /// tri-light, URP fog, and the shader globals consumed by every Cozy shader
    /// (see Assets/Shaders/Cozy/CozyCommon.hlsl).
    ///
    /// Runs in edit mode too so the Scene view always matches. All colours are
    /// gradients over a normalized day (0 = midnight, 0.25 = sunrise, 0.5 = noon,
    /// 0.75 = sunset) so artists can retune the look without touching code.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("Cozy Rendering/Cozy Atmosphere")]
    public sealed class CozyAtmosphere : MonoBehaviour
    {
        // Shader global ids (kept in sync with CozyCommon.hlsl).
        private static readonly int AtmosphereParamsId = Shader.PropertyToID("_CozyAtmosphereParams");
        private static readonly int SunDirectionId = Shader.PropertyToID("_CozySunDirection");
        private static readonly int SunColorId = Shader.PropertyToID("_CozySunColor");
        private static readonly int SkyZenithId = Shader.PropertyToID("_CozySkyZenithColor");
        private static readonly int SkyHorizonId = Shader.PropertyToID("_CozySkyHorizonColor");
        private static readonly int SkyGroundId = Shader.PropertyToID("_CozySkyGroundColor");
        private static readonly int FogColorId = Shader.PropertyToID("_CozyFogColor");
        private static readonly int FogSunColorId = Shader.PropertyToID("_CozyFogSunColor");
        private static readonly int FogParamsId = Shader.PropertyToID("_CozyFogParams");

        public static CozyAtmosphere Active { get; private set; }

        [Header("Time")]
        [Tooltip("Hour of the day, 0..24. 12 = noon.")]
        [Range(0f, 24f)] public float timeOfDay = 15.5f;
        [Tooltip("Real-time minutes for a full 24h cycle while playing. 0 keeps the time fixed.")]
        [Min(0f)] public float dayLengthMinutes = 0f;
        [Tooltip("Also advance time in the editor (Scene view) when dayLengthMinutes > 0.")]
        public bool animateInEditMode = false;

        [Header("Sun")]
        [Tooltip("Directional light to drive. Defaults to RenderSettings.sun or the first directional light.")]
        public Light sun;
        public bool driveSunLight = true;
        [Range(-180f, 180f)] public float sunAzimuth = 35f;
        [Tooltip("Highest elevation the sun reaches at noon, in degrees.")]
        [Range(20f, 90f)] public float sunMaxElevation = 62f;
        public Gradient sunColor = DefaultSunColor();
        [Tooltip("Sun light intensity over the day (0 = midnight, 0.5 = noon).")]
        public AnimationCurve sunIntensity = DefaultSunIntensity();
        public Color moonColor = new Color(0.55f, 0.66f, 0.95f);
        [Range(0f, 1f)] public float moonIntensity = 0.16f;

        [Header("Sky")]
        public Gradient zenithColor = DefaultZenith();
        public Gradient horizonColor = DefaultHorizon();
        [Tooltip("Colour of the sky gradient below the horizon line.")]
        public Gradient groundColor = DefaultGround();
        [Tooltip("Optional: assign the Cozy/Sky material so it is set as the scene skybox automatically.")]
        public Material skyboxMaterial;

        [Header("Ambient")]
        public bool driveAmbient = true;
        [Range(0f, 2f)] public float ambientIntensity = 1.0f;
        [Tooltip("How much the ambient light follows the sky colours vs. a neutral warm/cool split.")]
        [Range(0f, 1f)] public float ambientSkyInfluence = 0.75f;

        [Header("Fog / Atmosphere")]
        public bool driveFog = true;
        public Gradient fogColor = DefaultFog();
        [Tooltip("URP exponential-squared fog density used for distance fog.")]
        [Range(0f, 0.05f)] public float fogDensity = 0.0032f;
        [Tooltip("Extra low-lying height fog (Cozy shaders only). 0 disables it.")]
        [Range(0f, 0.2f)] public float heightFogDensity = 0.018f;
        [Range(0.005f, 0.5f)] public float heightFogFalloff = 0.07f;
        [Tooltip("World Y where the height fog is densest (roughly the water level).")]
        public float heightFogBase = 3.5f;
        [Tooltip("How strongly fog picks up the sun colour when looking towards the sun.")]
        [Range(0f, 1f)] public float fogSunInscatter = 0.6f;

        private float lastEditorTime;

        /// <summary>Normalized time of day, 0..1.</summary>
        public float NormalizedTime => Mathf.Repeat(timeOfDay / 24f, 1f);

        /// <summary>World direction pointing towards the sun (may be below the horizon).</summary>
        public Vector3 SunDirection { get; private set; } = Vector3.up;

        /// <summary>0 = night, 1 = day.</summary>
        public float DayFactor { get; private set; } = 1f;

        private void OnEnable()
        {
            Active = this;
            ResolveSun();
            Apply();
        }

        private void OnDisable()
        {
            if (Active == this) Active = null;
            // Let the shaders fall back to their material/URP defaults.
            Shader.SetGlobalVector(AtmosphereParamsId, Vector4.zero);
        }

        private void OnValidate()
        {
            if (isActiveAndEnabled) Apply();
        }

        private void Update()
        {
            if (dayLengthMinutes > 0f)
            {
                if (Application.isPlaying)
                    timeOfDay = Mathf.Repeat(timeOfDay + Time.deltaTime * 24f / (dayLengthMinutes * 60f), 24f);
                else if (animateInEditMode)
                {
                    float now = Time.realtimeSinceStartup;
                    float delta = Mathf.Clamp(now - lastEditorTime, 0f, 0.1f);
                    lastEditorTime = now;
                    timeOfDay = Mathf.Repeat(timeOfDay + delta * 24f / (dayLengthMinutes * 60f), 24f);
                }
            }
            Apply();
        }

        private void ResolveSun()
        {
            if (sun != null) return;
            sun = RenderSettings.sun;
            if (sun != null) return;
            foreach (Light light in FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (light.type == LightType.Directional) { sun = light; break; }
            }
        }

        /// <summary>Evaluates every derived value for the current time and pushes it to Unity + shaders.</summary>
        public void Apply()
        {
            float t = NormalizedTime;

            // --- Sun geometry --------------------------------------------------
            // X rotation: 0 at sunrise (t=0.25), 90 at noon (t=0.5), 180 at sunset.
            float xRot = t * 360f - 90f;
            float tilt = 90f - sunMaxElevation;
            Quaternion orbit = Quaternion.Euler(0f, sunAzimuth, 0f) * Quaternion.Euler(0f, 0f, tilt) * Quaternion.Euler(xRot, 0f, 0f);
            Vector3 sunForward = orbit * Vector3.forward;   // light direction (from sun to ground)
            Vector3 sunDir = -sunForward;                    // towards the sun
            SunDirection = sunDir;

            float elevation = sunDir.y;                                          // -1..1
            float day = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.08f, 0.22f, elevation));
            float sunset = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, 0.32f, Mathf.Abs(elevation)));
            sunset *= Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.3f, -0.05f, elevation)); // fade once well below horizon
            DayFactor = day;

            // --- Colours ---------------------------------------------------------
            Color sunCol = sunColor.Evaluate(t);
            float sunInt = Mathf.Max(0f, sunIntensity.Evaluate(t));
            Color zenith = zenithColor.Evaluate(t);
            Color horizon = horizonColor.Evaluate(t);
            Color ground = groundColor.Evaluate(t);
            Color fog = fogColor.Evaluate(t);

            // Cross-fade between sun and moon so the light never pops.
            float sunWeight = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.12f, 0.04f, elevation));
            Vector3 lightDir = Vector3.Slerp(sunDir, -sunDir, 1f - sunWeight); // moon is opposite the sun
            if (lightDir.y < 0.05f) lightDir = Vector3.Slerp(lightDir, Vector3.up, 0.5f).normalized;
            Color lightColor = Color.Lerp(moonColor, sunCol, sunWeight);
            float lightIntensity = Mathf.Lerp(moonIntensity, sunInt, sunWeight);

            // --- Directional light ---------------------------------------------
            if (driveSunLight && sun != null)
            {
                sun.transform.rotation = Quaternion.LookRotation(-lightDir, Vector3.up);
                sun.color = lightColor;
                sun.intensity = lightIntensity;
                sun.useColorTemperature = false;
            }

            // --- Ambient -------------------------------------------------------------
            if (driveAmbient)
            {
                Color nightAmbient = new Color(0.10f, 0.13f, 0.22f);
                Color skyAmbient = Color.Lerp(nightAmbient, Color.Lerp(new Color(0.62f, 0.72f, 0.88f), horizon, ambientSkyInfluence), day);
                Color equatorAmbient = Color.Lerp(nightAmbient * 0.8f, Color.Lerp(new Color(0.60f, 0.58f, 0.52f), fog, ambientSkyInfluence * 0.8f), day);
                Color groundAmbient = Color.Lerp(nightAmbient * 0.5f, Color.Lerp(new Color(0.30f, 0.26f, 0.22f), ground * 0.7f, ambientSkyInfluence * 0.6f), day);
                RenderSettings.ambientMode = AmbientMode.Trilight;
                RenderSettings.ambientSkyColor = skyAmbient * ambientIntensity;
                RenderSettings.ambientEquatorColor = equatorAmbient * ambientIntensity;
                RenderSettings.ambientGroundColor = groundAmbient * ambientIntensity;
            }

            if (skyboxMaterial != null && RenderSettings.skybox != skyboxMaterial)
                RenderSettings.skybox = skyboxMaterial;

            // --- Fog -------------------------------------------------------------------
            if (driveFog)
            {
                RenderSettings.fog = true;
                RenderSettings.fogMode = FogMode.ExponentialSquared;
                RenderSettings.fogColor = fog;
                RenderSettings.fogDensity = fogDensity;
            }

            // --- Shader globals -----------------------------------------------------
            Color sunGlobal = lightColor * lightIntensity;
            Color fogSun = Color.Lerp(fog, sunCol * Mathf.Clamp01(sunInt), 0.7f * sunWeight);
            Shader.SetGlobalVector(SunDirectionId, new Vector4(lightDir.x, lightDir.y, lightDir.z, elevation));
            Shader.SetGlobalColor(SunColorId, sunGlobal);
            Shader.SetGlobalColor(SkyZenithId, zenith);
            Shader.SetGlobalColor(SkyHorizonId, horizon);
            Shader.SetGlobalColor(SkyGroundId, ground);
            Shader.SetGlobalColor(FogColorId, fog);
            Shader.SetGlobalColor(FogSunColorId, fogSun);
            Shader.SetGlobalVector(FogParamsId, new Vector4(heightFogDensity, heightFogFalloff, heightFogBase, fogSunInscatter));
            Shader.SetGlobalVector(AtmosphereParamsId, new Vector4(1f, day, sunset, 1f - day));
        }

        private void Reset()
        {
            sunColor = DefaultSunColor();
            sunIntensity = DefaultSunIntensity();
            zenithColor = DefaultZenith();
            horizonColor = DefaultHorizon();
            groundColor = DefaultGround();
            fogColor = DefaultFog();
            ResolveSun();
        }

        // ------------------------------------------------------------------------------
        // Default look: vibrant, soft, warm afternoons; blue-violet dusk; deep cool night.
        // Keys are at normalized time (0 midnight, .25 sunrise, .5 noon, .75 sunset).
        // ------------------------------------------------------------------------------
        private static Gradient MakeGradient(params (float time, Color color)[] keys)
        {
            GradientColorKey[] colorKeys = new GradientColorKey[keys.Length];
            for (int i = 0; i < keys.Length; i++) colorKeys[i] = new GradientColorKey(keys[i].color, keys[i].time);
            Gradient gradient = new Gradient();
            gradient.SetKeys(colorKeys, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            return gradient;
        }

        private static Gradient DefaultSunColor() => MakeGradient(
            (0.00f, new Color(0.55f, 0.66f, 0.95f)),
            (0.22f, new Color(1.00f, 0.55f, 0.32f)),
            (0.28f, new Color(1.00f, 0.80f, 0.58f)),
            (0.50f, new Color(1.00f, 0.96f, 0.88f)),
            (0.70f, new Color(1.00f, 0.86f, 0.62f)),
            (0.77f, new Color(1.00f, 0.52f, 0.28f)),
            (0.82f, new Color(0.60f, 0.45f, 0.70f)),
            (1.00f, new Color(0.55f, 0.66f, 0.95f)));

        private static AnimationCurve DefaultSunIntensity()
        {
            AnimationCurve curve = new AnimationCurve(
                new Keyframe(0.00f, 0.0f),
                new Keyframe(0.21f, 0.0f),
                new Keyframe(0.27f, 1.1f),
                new Keyframe(0.50f, 1.55f),
                new Keyframe(0.72f, 1.25f),
                new Keyframe(0.79f, 0.0f),
                new Keyframe(1.00f, 0.0f));
            for (int i = 0; i < curve.length; i++) curve.SmoothTangents(i, 0f);
            return curve;
        }

        private static Gradient DefaultZenith() => MakeGradient(
            (0.00f, new Color(0.02f, 0.04f, 0.10f)),
            (0.20f, new Color(0.05f, 0.08f, 0.20f)),
            (0.27f, new Color(0.28f, 0.44f, 0.80f)),
            (0.50f, new Color(0.22f, 0.50f, 0.92f)),
            (0.73f, new Color(0.26f, 0.42f, 0.82f)),
            (0.80f, new Color(0.14f, 0.12f, 0.36f)),
            (1.00f, new Color(0.02f, 0.04f, 0.10f)));

        private static Gradient DefaultHorizon() => MakeGradient(
            (0.00f, new Color(0.08f, 0.10f, 0.18f)),
            (0.20f, new Color(0.30f, 0.20f, 0.30f)),
            (0.26f, new Color(1.00f, 0.72f, 0.52f)),
            (0.35f, new Color(0.78f, 0.90f, 1.00f)),
            (0.50f, new Color(0.72f, 0.88f, 1.00f)),
            (0.68f, new Color(0.86f, 0.86f, 0.92f)),
            (0.76f, new Color(1.00f, 0.60f, 0.42f)),
            (0.82f, new Color(0.42f, 0.24f, 0.40f)),
            (1.00f, new Color(0.08f, 0.10f, 0.18f)));

        private static Gradient DefaultGround() => MakeGradient(
            (0.00f, new Color(0.04f, 0.05f, 0.08f)),
            (0.25f, new Color(0.40f, 0.34f, 0.36f)),
            (0.50f, new Color(0.48f, 0.56f, 0.62f)),
            (0.75f, new Color(0.46f, 0.34f, 0.36f)),
            (1.00f, new Color(0.04f, 0.05f, 0.08f)));

        private static Gradient DefaultFog() => MakeGradient(
            (0.00f, new Color(0.07f, 0.09f, 0.16f)),
            (0.22f, new Color(0.34f, 0.26f, 0.36f)),
            (0.28f, new Color(0.92f, 0.78f, 0.66f)),
            (0.50f, new Color(0.76f, 0.87f, 0.96f)),
            (0.70f, new Color(0.86f, 0.82f, 0.82f)),
            (0.77f, new Color(0.88f, 0.58f, 0.48f)),
            (0.84f, new Color(0.26f, 0.20f, 0.34f)),
            (1.00f, new Color(0.07f, 0.09f, 0.16f)));
    }
}
