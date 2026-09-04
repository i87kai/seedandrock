using UnityEngine;

namespace SeedAndRock.UI
{
    /// <summary>Single source of truth for the SeedAndRock look: a calm night-teal palette with warm gold accents.</summary>
    public static class SeedAndRockTheme
    {
        public static readonly Color Night = new Color(0.030f, 0.062f, 0.078f, 0.985f);
        public static readonly Color Backdrop = new Color(0.012f, 0.035f, 0.045f, 0.86f);
        public static readonly Color Panel = new Color(0.060f, 0.120f, 0.135f, 0.97f);
        public static readonly Color PanelRaised = new Color(0.085f, 0.165f, 0.180f, 1f);
        public static readonly Color Border = new Color(0.18f, 0.40f, 0.40f, 0.55f);
        public static readonly Color Teal = new Color(0.16f, 0.72f, 0.62f, 1f);
        public static readonly Color TealDeep = new Color(0.11f, 0.48f, 0.43f, 1f);
        public static readonly Color Gold = new Color(0.95f, 0.70f, 0.26f, 1f);
        public static readonly Color Danger = new Color(0.78f, 0.30f, 0.26f, 1f);
        public static readonly Color Pale = new Color(0.90f, 0.96f, 0.93f, 1f);
        public static readonly Color Muted = new Color(0.60f, 0.73f, 0.71f, 1f);
        public static readonly Color Faint = new Color(0.44f, 0.56f, 0.56f, 1f);
        public static readonly Color Field = new Color(0.028f, 0.070f, 0.080f, 1f);
        public static readonly Color Ghost = new Color(0.14f, 0.25f, 0.27f, 1f);

        public const float TitleSize = 68f;
        public const float HeadingSize = 38f;
        public const float SubheadingSize = 22f;
        public const float BodySize = 18f;
        public const float SmallSize = 14f;
        public const float LabelSize = 13f;

        public const float ButtonHeight = 58f;
        public const float SmallButtonHeight = 46f;
        public const float CardCorner = 6f;
        public const float Spacing = 14f;
        public const float FadeDuration = 0.22f;
    }
}
