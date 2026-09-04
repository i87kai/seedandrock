using SeedAndRock.Player;
using UnityEngine;

namespace SeedAndRock.UI
{
    /// <summary>Small persistent player preferences surfaced by the Settings screen. Stored in PlayerPrefs.</summary>
    public static class GameSettings
    {
        private const string SensitivityKey = "sr.settings.sensitivity";
        private const string FieldOfViewKey = "sr.settings.fov";
        private const string QualityKey = "sr.settings.quality";
        private const string FullscreenKey = "sr.settings.fullscreen";

        public static float MouseSensitivity
        {
            get => Mathf.Clamp(PlayerPrefs.GetFloat(SensitivityKey, 1f), 0.2f, 3f);
            set => PlayerPrefs.SetFloat(SensitivityKey, Mathf.Clamp(value, 0.2f, 3f));
        }

        public static float FieldOfView
        {
            get => Mathf.Clamp(PlayerPrefs.GetFloat(FieldOfViewKey, 70f), 55f, 100f);
            set => PlayerPrefs.SetFloat(FieldOfViewKey, Mathf.Clamp(value, 55f, 100f));
        }

        public static int QualityLevel
        {
            get => Mathf.Clamp(PlayerPrefs.GetInt(QualityKey, QualitySettings.GetQualityLevel()), 0, Mathf.Max(0, QualitySettings.names.Length - 1));
            set => PlayerPrefs.SetInt(QualityKey, Mathf.Clamp(value, 0, Mathf.Max(0, QualitySettings.names.Length - 1)));
        }

        public static bool Fullscreen
        {
            get => PlayerPrefs.GetInt(FullscreenKey, Screen.fullScreen ? 1 : 0) == 1;
            set => PlayerPrefs.SetInt(FullscreenKey, value ? 1 : 0);
        }

        public static void Apply()
        {
            FirstPersonExplorerController.LookSensitivityScale = MouseSensitivity;
            Camera camera = Camera.main;
            if (camera != null) camera.fieldOfView = FieldOfView;
            if (QualitySettings.GetQualityLevel() != QualityLevel) QualitySettings.SetQualityLevel(QualityLevel, true);
            if (Screen.fullScreen != Fullscreen) Screen.fullScreen = Fullscreen;
            PlayerPrefs.Save();
        }
    }
}
