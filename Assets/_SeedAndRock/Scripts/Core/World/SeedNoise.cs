namespace SeedAndRock.World
{
    /// <summary>
    /// Deterministic value-noise utility used by world generation. All functions are pure and
    /// depend only on their arguments, which is what makes seed reproducibility possible.
    /// </summary>
    public static class SeedNoise
    {
        public static float Fractal(int seed, float x, float z, int octaves, float frequency, float lacunarity, float gain)
        {
            float sum = 0f;
            float amplitude = 1f;
            float normalizer = 0f;
            for (int i = 0; i < octaves; i++)
            {
                sum += Value(seed + i * 1013, x * frequency, z * frequency) * amplitude;
                normalizer += amplitude;
                amplitude *= gain;
                frequency *= lacunarity;
            }

            return normalizer > 0f ? sum / normalizer : 0f;
        }

        /// <summary>Fractal noise remapped to the 0..1 range.</summary>
        public static float Fractal01(int seed, float x, float z, int octaves, float frequency, float lacunarity = 2f, float gain = 0.5f) =>
            Fractal(seed, x, z, octaves, frequency, lacunarity, gain) * 0.5f + 0.5f;

        /// <summary>Ridged multifractal: sharp crests suitable for mountain ranges. Returns 0..1.</summary>
        public static float Ridged(int seed, float x, float z, int octaves, float frequency, float lacunarity, float gain, float sharpness)
        {
            float sum = 0f;
            float amplitude = 1f;
            float normalizer = 0f;
            float weight = 1f;
            for (int i = 0; i < octaves; i++)
            {
                float n = 1f - SRMath.Abs(Value(seed + i * 1013, x * frequency, z * frequency));
                n = SRMath.Pow(SRMath.Clamp01(n), sharpness) * weight;
                weight = SRMath.Clamp01(n * 1.6f);
                sum += n * amplitude;
                normalizer += amplitude;
                amplitude *= gain;
                frequency *= lacunarity;
            }

            return normalizer > 0f ? SRMath.Clamp01(sum / normalizer) : 0f;
        }

        public static float Value(int seed, float x, float z)
        {
            int x0 = SRMath.FloorToInt(x);
            int z0 = SRMath.FloorToInt(z);
            float tx = Smooth(x - x0);
            float tz = Smooth(z - z0);

            float a = Hash01(seed, x0, z0);
            float b = Hash01(seed, x0 + 1, z0);
            float c = Hash01(seed, x0, z0 + 1);
            float d = Hash01(seed, x0 + 1, z0 + 1);
            return SRMath.LerpUnclamped(SRMath.LerpUnclamped(a, b, tx), SRMath.LerpUnclamped(c, d, tx), tz) * 2f - 1f;
        }

        public static float Hash01(int seed, int x, int z)
        {
            unchecked
            {
                uint h = (uint)seed;
                h ^= (uint)x * 0x9E3779B9u;
                h = (h << 13) | (h >> 19);
                h ^= (uint)z * 0x85EBCA6Bu;
                h *= 0xC2B2AE35u;
                h ^= h >> 16;
                return (h & 0x00FFFFFFu) / 16777215f;
            }
        }

        /// <summary>Three-argument hash for per-instance variation (e.g. cell x/z plus a channel id).</summary>
        public static float Hash01(int seed, int x, int z, int channel) => Hash01(seed + channel * 7919, x, z);

        /// <summary>Stable 32-bit hash of a string (FNV-1a) so text seeds are reproducible across platforms.</summary>
        public static int HashString(string text)
        {
            unchecked
            {
                uint h = 2166136261u;
                for (int i = 0; i < text.Length; i++)
                {
                    h ^= text[i];
                    h *= 16777619u;
                }

                return (int)(h & 0x7FFFFFFFu);
            }
        }

        public static float Smooth(float value) => value * value * (3f - 2f * value);
    }
}
