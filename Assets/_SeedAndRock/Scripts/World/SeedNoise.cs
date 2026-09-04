using UnityEngine;

namespace SeedAndRock.World
{
    /// <summary>Small deterministic value-noise utility used by world generation.</summary>
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

        public static float Value(int seed, float x, float z)
        {
            int x0 = Mathf.FloorToInt(x);
            int z0 = Mathf.FloorToInt(z);
            float tx = Smooth(x - x0);
            float tz = Smooth(z - z0);

            float a = Hash01(seed, x0, z0);
            float b = Hash01(seed, x0 + 1, z0);
            float c = Hash01(seed, x0, z0 + 1);
            float d = Hash01(seed, x0 + 1, z0 + 1);
            return Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), tz) * 2f - 1f;
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

        public static float Smooth(float value) => value * value * (3f - 2f * value);
    }
}