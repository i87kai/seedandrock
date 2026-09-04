using System;

namespace SeedAndRock.World
{
    /// <summary>
    /// Engine-independent float helpers used by the deterministic world core.
    /// Keeping these out of UnityEngine lets the generation code run and be tested anywhere.
    /// </summary>
    public static class SRMath
    {
        public const float Pi = 3.14159265358979f;
        public const float Deg2Rad = Pi / 180f;

        public static float Clamp01(float value) => value < 0f ? 0f : value > 1f ? 1f : value;

        public static float Clamp(float value, float min, float max) => value < min ? min : value > max ? max : value;

        public static int Clamp(int value, int min, int max) => value < min ? min : value > max ? max : value;

        public static float Lerp(float a, float b, float t) => a + (b - a) * Clamp01(t);

        public static float LerpUnclamped(float a, float b, float t) => a + (b - a) * t;

        public static float InverseLerp(float a, float b, float value) => Math.Abs(b - a) < 1e-8f ? 0f : Clamp01((value - a) / (b - a));

        /// <summary>Hermite smoothstep between two edges; behaves like Mathf.SmoothStep(edge0, edge1, x) but with the GLSL argument order.</summary>
        public static float SmoothStep(float edge0, float edge1, float x)
        {
            float t = InverseLerp(edge0, edge1, x);
            return t * t * (3f - 2f * t);
        }

        public static float Smooth01(float t)
        {
            t = Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        public static float Remap(float value, float fromMin, float fromMax, float toMin, float toMax) =>
            LerpUnclamped(toMin, toMax, InverseLerp(fromMin, fromMax, value));

        public static int FloorToInt(float value) => (int)Math.Floor(value);

        public static int CeilToInt(float value) => (int)Math.Ceiling(value);

        public static int RoundToInt(float value) => (int)Math.Round(value, MidpointRounding.AwayFromZero);

        public static float Sqrt(float value) => (float)Math.Sqrt(value);

        public static float Abs(float value) => value < 0f ? -value : value;

        public static float Min(float a, float b) => a < b ? a : b;

        public static float Max(float a, float b) => a > b ? a : b;

        public static float Pow(float value, float power) => (float)Math.Pow(value, power);

        public static float Exp(float value) => (float)Math.Exp(value);

        public static float Sin(float value) => (float)Math.Sin(value);

        public static float Cos(float value) => (float)Math.Cos(value);

        public static float Length(float x, float y) => (float)Math.Sqrt(x * x + y * y);

        /// <summary>Bilinear sample of a row-major grid with clamped coordinates. gx/gz are in grid units.</summary>
        public static float SampleBilinear(float[] grid, int width, int height, float gx, float gz)
        {
            gx = Clamp(gx, 0f, width - 1.0001f);
            gz = Clamp(gz, 0f, height - 1.0001f);
            int x0 = (int)gx;
            int z0 = (int)gz;
            int x1 = Math.Min(x0 + 1, width - 1);
            int z1 = Math.Min(z0 + 1, height - 1);
            float tx = gx - x0;
            float tz = gz - z0;
            float a = grid[z0 * width + x0];
            float b = grid[z0 * width + x1];
            float c = grid[z1 * width + x0];
            float d = grid[z1 * width + x1];
            return LerpUnclamped(LerpUnclamped(a, b, tx), LerpUnclamped(c, d, tx), tz);
        }
    }
}
