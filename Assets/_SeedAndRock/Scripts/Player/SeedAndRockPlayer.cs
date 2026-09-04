using SeedAndRock.World;
using UnityEngine;

namespace SeedAndRock.Player
{
    /// <summary>Resolves the runtime explorer, preferring the generated world instance.</summary>
    public static class SeedAndRockPlayer
    {
        public const string ObjectName = "SeedAndRock_Player";

        public static GameObject Find()
        {
            if (WorldGenerator.Active != null)
            {
                Transform generated = WorldGenerator.Active.transform.Find("__GeneratedWorld/" + ObjectName);
                if (generated != null)
                    return generated.gameObject;
            }

            return GameObject.Find(ObjectName);
        }
    }
}
