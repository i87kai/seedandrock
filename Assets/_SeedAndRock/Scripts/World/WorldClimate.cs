using UnityEngine;

namespace SeedAndRock.World
{
    /// <summary>
    /// Maps the world's continuous climate field onto Celsius.
    /// Keypoints match the snow (0.28) and desert (0.68) gates in biome classification.
    /// </summary>
    public static class WorldClimate
    {
        public const float SnowThreshold = 0.28f;
        public const float TemperateMid = 0.50f;
        public const float DesertThreshold = 0.68f;

        public const float DefaultMinCelsius = -15f;
        public const float DefaultSnowLineCelsius = 0f;
        public const float DefaultTemperateCelsius = 16f;
        public const float DefaultHotFringeCelsius = 32f;
        public const float DefaultMaxCelsius = 46f;

        public static float Temperature01ToCelsius(float temperature01)
        {
            return Temperature01ToCelsius(
                temperature01,
                DefaultMinCelsius,
                DefaultSnowLineCelsius,
                DefaultTemperateCelsius,
                DefaultHotFringeCelsius,
                DefaultMaxCelsius);
        }

        public static float Temperature01ToCelsius(
            float temperature01,
            float minCelsius,
            float snowLineCelsius,
            float temperateCelsius,
            float hotFringeCelsius,
            float maxCelsius)
        {
            float t = Mathf.Clamp01(temperature01);
            if (t <= SnowThreshold)
                return Mathf.Lerp(minCelsius, snowLineCelsius, t / SnowThreshold);
            if (t <= TemperateMid)
                return Mathf.Lerp(snowLineCelsius, temperateCelsius, (t - SnowThreshold) / (TemperateMid - SnowThreshold));
            if (t <= DesertThreshold)
                return Mathf.Lerp(temperateCelsius, hotFringeCelsius, (t - TemperateMid) / (DesertThreshold - TemperateMid));
            return Mathf.Lerp(hotFringeCelsius, maxCelsius, (t - DesertThreshold) / (1f - DesertThreshold));
        }
    }
}
