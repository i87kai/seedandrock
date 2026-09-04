using System;
using System.Globalization;
using SeedAndRock.World;

namespace SeedAndRock.Saves
{
    public enum SeedParseStatus
    {
        Empty,
        Numeric,
        Text,
        Invalid
    }

    /// <summary>Pure validation rules shared by the UI and the save repository.</summary>
    public static class WorldValidation
    {
        public const int MaxNameLength = 32;
        public const string DefaultDifficulty = "Normal";
        public static readonly string[] Difficulties = { "Peaceful", "Easy", "Normal", "Hard" };

        private static readonly char[] ForbiddenNameCharacters = { '<', '>', ':', '"', '/', '\\', '|', '?', '*' };
        private static readonly string[] ReservedNames =
        {
            "CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
            "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
        };

        /// <summary>Collapses whitespace and trims so cosmetic differences do not create odd duplicates.</summary>
        public static string NormalizeName(string name)
        {
            if (name == null) return string.Empty;
            char[] buffer = new char[name.Length];
            int length = 0;
            bool pendingSpace = false;
            foreach (char c in name)
            {
                if (char.IsWhiteSpace(c) || char.IsControl(c))
                {
                    pendingSpace = length > 0;
                    continue;
                }

                if (pendingSpace) buffer[length++] = ' ';
                pendingSpace = false;
                buffer[length++] = c;
            }

            return new string(buffer, 0, length);
        }

        public static bool ValidateName(string name, out string error)
        {
            string normalized = NormalizeName(name);
            if (normalized.Length == 0)
            {
                error = "Choose a name for this world.";
                return false;
            }

            if (normalized.Length > MaxNameLength)
            {
                error = "World names must be " + MaxNameLength + " characters or fewer.";
                return false;
            }

            if (normalized.IndexOfAny(ForbiddenNameCharacters) >= 0)
            {
                error = "World names cannot contain < > : \" / \\ | ? or *.";
                return false;
            }

            if (Array.IndexOf(ReservedNames, normalized.ToUpperInvariant()) >= 0 || normalized.EndsWith(".", StringComparison.Ordinal))
            {
                error = "That name is reserved by the file system.";
                return false;
            }

            error = null;
            return true;
        }

        /// <summary>
        /// Parses a seed field. Whole numbers are used directly, other text is hashed deterministically so
        /// players can share memorable seeds. Empty input reports <see cref="SeedParseStatus.Empty"/>.
        /// </summary>
        public static SeedParseStatus TryParseSeed(string text, out int seed)
        {
            seed = 0;
            string trimmed = text == null ? string.Empty : text.Trim();
            if (trimmed.Length == 0) return SeedParseStatus.Empty;

            if (long.TryParse(trimmed, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long numeric))
            {
                if (numeric < int.MinValue || numeric > int.MaxValue) return SeedParseStatus.Invalid;
                seed = (int)numeric;
                return SeedParseStatus.Numeric;
            }

            if (trimmed.Length > 64) return SeedParseStatus.Invalid;
            seed = SeedNoise.HashString(trimmed);
            return SeedParseStatus.Text;
        }

        public static bool IsValidDifficulty(string difficulty) => Array.IndexOf(Difficulties, difficulty) >= 0;

        public static string NextDifficulty(string current)
        {
            int index = Array.IndexOf(Difficulties, current);
            return Difficulties[(index + 1 + Difficulties.Length) % Difficulties.Length];
        }

        public static bool IsSafeId(string id)
        {
            if (string.IsNullOrEmpty(id) || id.Length > 64) return false;
            foreach (char c in id)
            {
                bool ok = (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F') || c == '-';
                if (!ok) return false;
            }

            return true;
        }

        /// <summary>
        /// Returns true when a loaded record is safe to use. Repairable fields (difficulty, timestamps,
        /// non-finite player state) are fixed in place; unrepairable records are rejected.
        /// </summary>
        public static bool SanitizeRecord(SavedWorld record, DateTime nowUtc)
        {
            if (record == null || !IsSafeId(record.id)) return false;
            if (!ValidateName(record.worldName, out _)) return false;
            record.worldName = NormalizeName(record.worldName);
            if (!IsValidDifficulty(record.difficulty)) record.difficulty = DefaultDifficulty;
            if (record.CreatedUtc == null) record.createdUtc = SavedWorld.FormatUtc(nowUtc);
            if (record.LastPlayedUtc == null) record.lastPlayedUtc = record.createdUtc;
            if (!record.GetPlayerState().IsFinite)
            {
                record.hasVisited = false;
                record.SetPlayerState(default);
                record.hasVisited = false;
            }

            if (record.version <= 0) record.version = 1;
            return true;
        }
    }
}
