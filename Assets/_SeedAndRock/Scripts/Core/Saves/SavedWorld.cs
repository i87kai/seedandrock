using System;
using System.Collections.Generic;
using System.Globalization;

namespace SeedAndRock.Saves
{
    /// <summary>
    /// Persistent metadata for one world. Terrain is recreated from the seed; only progress that cannot
    /// be derived (player position, timestamps) is stored. Field names are kept stable for JSON compatibility.
    /// </summary>
    [Serializable]
    public sealed class SavedWorld
    {
        public const int CurrentVersion = 3;
        public ExpeditionState expedition;
        public string worldBackend;
        public int graphVersion;

        public int version = CurrentVersion;
        public string id;
        public string worldName;
        public int seed;
        public string difficulty = WorldValidation.DefaultDifficulty;
        public string createdUtc;
        public string lastPlayedUtc;
        public bool hasVisited;
        public float playerX;
        public float playerY;
        public float playerZ;
        public float playerYaw;
        public float playerPitch;
        public bool hasSurvivalState;
        public float health;
        public float hunger;
        public float thirst;
        public float bodyTemperature;

        public PlayerStateData GetPlayerState() => new PlayerStateData(playerX, playerY, playerZ, playerYaw, playerPitch);

        public void SetPlayerState(PlayerStateData state)
        {
            playerX = state.x;
            playerY = state.y;
            playerZ = state.z;
            playerYaw = state.yaw;
            playerPitch = state.pitch;
            hasVisited = true;
        }

        public DateTime? CreatedUtc => ParseUtc(createdUtc);
        public DateTime? LastPlayedUtc => ParseUtc(lastPlayedUtc);

        public static string FormatUtc(DateTime time) => time.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

        public static DateTime? ParseUtc(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;
            if (!DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime value)) return null;
            return value.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(value, DateTimeKind.Utc) : value.ToUniversalTime();
        }

        public SavedWorld Clone() { var copy = (SavedWorld)MemberwiseClone(); copy.expedition = expedition?.Copy(); return copy; }
    }

    [Serializable]
    public sealed class SavedWorldCollection
    {
        public List<SavedWorld> worlds = new List<SavedWorld>();
    }

    /// <summary>Position and view orientation restored when a world is re-entered.</summary>
    [Serializable]
    public struct PlayerStateData
    {
        public float x, y, z;
        public float yaw, pitch;

        public PlayerStateData(float x, float y, float z, float yaw, float pitch)
        {
            this.x = x; this.y = y; this.z = z; this.yaw = yaw; this.pitch = pitch;
        }

        public bool IsFinite =>
            !float.IsNaN(x) && !float.IsInfinity(x) &&
            !float.IsNaN(y) && !float.IsInfinity(y) &&
            !float.IsNaN(z) && !float.IsInfinity(z) &&
            !float.IsNaN(yaw) && !float.IsInfinity(yaw) &&
            !float.IsNaN(pitch) && !float.IsInfinity(pitch);
    }
}
