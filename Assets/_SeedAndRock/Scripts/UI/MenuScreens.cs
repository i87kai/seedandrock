using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SeedAndRock.UI
{
    /// <summary>Title screen with a dedicated, softly animated backdrop.</summary>
    public sealed class MainMenuScreen : UiScreen
    {
        public MainMenuScreen(SeedAndRockGameFlow flow, Transform parent) : base(flow, parent, "MainMenu", false)
        {
            BuildBackdrop();

            RectTransform column = UiKit.RectOf(UiKit.CreateColumn(Content, "Column", 12f, new RectOffset(0, 0, 0, 0)).gameObject);
            UiKit.Anchor(column, new Vector2(0.5f, 0.5f), new Vector2(460f, 640f), new Vector2(0f, 10f));

            TMP_Text eyebrow = UiKit.CreateLabel(column, "A world made your way", SeedAndRockTheme.Teal, TextAlignmentOptions.Center);
            UiKit.Size(eyebrow, null, 24f);
            TMP_Text title = UiKit.CreateText(column, "Title", "SEED & ROCK", SeedAndRockTheme.TitleSize, SeedAndRockTheme.Pale, FontStyles.Bold);
            title.characterSpacing = 4f;
            UiKit.Size(title, null, 92f);
            TMP_Text tagline = UiKit.CreateText(column, "Tagline", "Explore one enduring world, shaped by your seed.", SeedAndRockTheme.SubheadingSize, SeedAndRockTheme.Muted);
            UiKit.Size(tagline, null, 40f);
            UiKit.CreateSpacer(column, 26f);

            UiKit.CreateButton(column, "PlayButton", "PLAY", UiKit.ButtonStyle.Primary, flow.ShowWorldBrowser, 66f, 22f);
            UiKit.CreateButton(column, "SettingsButton", "SETTINGS", UiKit.ButtonStyle.Ghost, () => flow.ShowSettings());
            UiKit.CreateButton(column, "QuitButton", "QUIT", UiKit.ButtonStyle.Ghost, flow.QuitGame, SeedAndRockTheme.SmallButtonHeight, SeedAndRockTheme.SmallSize + 1f);

            TMP_Text footer = UiKit.CreateText(Content, "Footer", "Every saved world is a deterministic landscape — your seed always leads home.", SeedAndRockTheme.SmallSize, SeedAndRockTheme.Faint);
            UiKit.Anchor(footer.rectTransform, new Vector2(0.5f, 0f), new Vector2(900f, 30f), new Vector2(0f, 34f));
            TMP_Text version = UiKit.CreateText(Content, "Version", "Unity " + Application.unityVersion + "  •  " + Application.version, SeedAndRockTheme.LabelSize, SeedAndRockTheme.Faint, FontStyles.Normal, TextAlignmentOptions.Right);
            UiKit.Anchor(version.rectTransform, new Vector2(1f, 0f), new Vector2(360f, 24f), new Vector2(-18f, 14f));
        }

        private void BuildBackdrop()
        {
            Image sky = UiKit.CreatePanel(Root, "Sky", SeedAndRockTheme.Night, false, true);
            UiKit.Stretch(sky.rectTransform);
            sky.transform.SetAsFirstSibling();

            Image glow = UiKit.CreatePanel(Root, "Glow", new Color(0.16f, 0.52f, 0.50f, 0.28f), true, false);
            glow.sprite = UiKit.Soft;
            UiKit.Anchor(glow.rectTransform, new Vector2(0.5f, 0.62f), new Vector2(1500f, 900f));
            glow.transform.SetSiblingIndex(1);
            glow.gameObject.AddComponent<DriftMotion>().Configure(new Vector2(30f, 12f), 0.11f);

            Color[] hillColors =
            {
                new Color(0.05f, 0.12f, 0.14f, 1f), new Color(0.04f, 0.10f, 0.12f, 1f), new Color(0.03f, 0.075f, 0.09f, 1f)
            };
            for (int layer = 0; layer < hillColors.Length; layer++)
            {
                for (int i = 0; i < 5; i++)
                {
                    Image hill = UiKit.CreatePanel(Root, "Hill_" + layer + "_" + i, hillColors[layer], true, false);
                    hill.sprite = UiKit.Soft;
                    float x = -960f + i * 480f + layer * 150f;
                    float width = 900f - layer * 120f;
                    float height = 420f - layer * 60f;
                    UiKit.Anchor(hill.rectTransform, new Vector2(0.5f, 0f), new Vector2(width, height), new Vector2(x, -120f - layer * 40f));
                    hill.transform.SetSiblingIndex(2 + layer);
                    hill.gameObject.AddComponent<DriftMotion>().Configure(new Vector2(10f + layer * 6f, 3f), 0.06f + layer * 0.02f, i * 1.7f);
                }
            }

            Image mist = UiKit.CreatePanel(Root, "Mist", new Color(0.55f, 0.75f, 0.75f, 0.06f), true, false);
            mist.sprite = UiKit.Soft;
            UiKit.Anchor(mist.rectTransform, new Vector2(0.5f, 0.12f), new Vector2(2200f, 380f));
            mist.transform.SetSiblingIndex(2 + hillColors.Length);
            mist.gameObject.AddComponent<DriftMotion>().Configure(new Vector2(60f, 6f), 0.05f);
        }
    }

    /// <summary>In-game pause menu (ESC).</summary>
    public sealed class PauseMenuScreen : UiScreen
    {
        private readonly TMP_Text status;
        private readonly TMP_Text worldLabel;

        public PauseMenuScreen(SeedAndRockGameFlow flow, Transform parent) : base(flow, parent, "PauseMenu")
        {
            RectTransform card = UiKit.CreateCard(Content, "PauseCard", new Vector2(440f, 560f), new Vector2(0.5f, 0.5f));
            RectTransform column = UiKit.RectOf(UiKit.CreateColumn(card, "Column", 10f, new RectOffset(32, 32, 30, 26)).gameObject);
            UiKit.Stretch(column);

            UiKit.Size(UiKit.CreateLabel(column, "Paused", SeedAndRockTheme.Teal, TextAlignmentOptions.Center), null, 22f);
            worldLabel = UiKit.CreateText(column, "World", "", SeedAndRockTheme.HeadingSize - 6f, SeedAndRockTheme.Pale, FontStyles.Bold);
            UiKit.Size(worldLabel, null, 44f);
            status = UiKit.CreateText(column, "Status", "", SeedAndRockTheme.SmallSize, SeedAndRockTheme.Muted);
            UiKit.Size(status, null, 22f);
            UiKit.CreateSpacer(column, 8f);

            UiKit.CreateButton(column, "ResumeButton", "RESUME", UiKit.ButtonStyle.Primary, flow.ResumeGame);
            UiKit.CreateButton(column, "SaveButton", "SAVE WORLD", UiKit.ButtonStyle.Card, flow.SaveFromPause);
            UiKit.CreateButton(column, "SettingsButton", "SETTINGS", UiKit.ButtonStyle.Card, () => flow.ShowSettings());
            UiKit.CreateButton(column, "SaveAndMenuButton", "SAVE & MAIN MENU", UiKit.ButtonStyle.Ghost, flow.SaveAndReturnToMainMenu);
            UiKit.CreateButton(column, "QuitButton", "QUIT", UiKit.ButtonStyle.Danger, flow.QuitGame, SeedAndRockTheme.SmallButtonHeight, SeedAndRockTheme.SmallSize + 1f);

            TMP_Text hint = UiKit.CreateText(column, "Hint", "ESC to resume  •  F3 for developer overlay", SeedAndRockTheme.LabelSize, SeedAndRockTheme.Faint);
            UiKit.Size(hint, null, 20f);
        }

        public void SetWorld(string name, int seed) => worldLabel.text = name + "  <size=60%><color=#7FB5AE>seed " + seed + "</color></size>";

        public void SetStatus(string message) => status.text = message;
    }

    /// <summary>Player preferences: mouse sensitivity, field of view, quality, fullscreen.</summary>
    public sealed class SettingsScreen : UiScreen
    {
        private readonly TMP_Text sensitivityValue;
        private readonly TMP_Text fovValue;
        private readonly Button qualityButton;
        private readonly Button fullscreenButton;
        private readonly Slider sensitivitySlider;
        private readonly Slider fovSlider;

        public SettingsScreen(SeedAndRockGameFlow flow, Transform parent) : base(flow, parent, "Settings")
        {
            RectTransform card = UiKit.CreateCard(Content, "SettingsCard", new Vector2(560f, 560f), new Vector2(0.5f, 0.5f));
            RectTransform column = UiKit.RectOf(UiKit.CreateColumn(card, "Column", 10f, new RectOffset(36, 36, 30, 26)).gameObject);
            UiKit.Stretch(column);

            UiKit.Size(UiKit.CreateLabel(column, "Preferences", SeedAndRockTheme.Teal, TextAlignmentOptions.Center), null, 22f);
            UiKit.Size(UiKit.CreateText(column, "Heading", "SETTINGS", SeedAndRockTheme.HeadingSize - 6f, SeedAndRockTheme.Pale, FontStyles.Bold), null, 44f);
            UiKit.CreateSpacer(column, 6f);

            sensitivityValue = AddRowHeader(column, "Mouse sensitivity");
            sensitivitySlider = UiKit.CreateSlider(column, "SensitivitySlider", 0.2f, 3f, GameSettings.MouseSensitivity, value =>
            {
                GameSettings.MouseSensitivity = value;
                sensitivityValue.text = value.ToString("0.00");
                GameSettings.Apply();
            });

            fovValue = AddRowHeader(column, "Field of view");
            fovSlider = UiKit.CreateSlider(column, "FovSlider", 55f, 100f, GameSettings.FieldOfView, value =>
            {
                GameSettings.FieldOfView = Mathf.Round(value);
                fovValue.text = Mathf.Round(value).ToString("0") + "°";
                GameSettings.Apply();
            });

            UiKit.CreateSpacer(column, 6f);
            qualityButton = UiKit.CreateButton(column, "QualityButton", "", UiKit.ButtonStyle.Card, () =>
            {
                int count = Mathf.Max(1, QualitySettings.names.Length);
                GameSettings.QualityLevel = (GameSettings.QualityLevel + 1) % count;
                GameSettings.Apply();
                RefreshLabels();
            }, SeedAndRockTheme.SmallButtonHeight);
            fullscreenButton = UiKit.CreateButton(column, "FullscreenButton", "", UiKit.ButtonStyle.Card, () =>
            {
                GameSettings.Fullscreen = !GameSettings.Fullscreen;
                GameSettings.Apply();
                RefreshLabels();
            }, SeedAndRockTheme.SmallButtonHeight);

            UiKit.CreateSpacer(column, 10f);
            UiKit.CreateButton(column, "BackButton", "BACK", UiKit.ButtonStyle.Primary, flow.CloseSettings);
            RefreshLabels();
        }

        private static TMP_Text AddRowHeader(Transform parent, string title)
        {
            RectTransform row = UiKit.RectOf(UiKit.CreateRow(parent, title + "Row", 8f, new RectOffset(0, 0, 0, 0)).gameObject);
            UiKit.Size(row.GetComponent<HorizontalLayoutGroup>(), null, 22f);
            TMP_Text label = UiKit.CreateLabel(row, title);
            UiKit.Size(label, 320f, 22f, 1f);
            TMP_Text value = UiKit.CreateText(row, "Value", "", SeedAndRockTheme.SmallSize, SeedAndRockTheme.Pale, FontStyles.Bold, TextAlignmentOptions.Right);
            UiKit.Size(value, 120f, 22f);
            return value;
        }

        protected override void OnBeforeShow()
        {
            sensitivitySlider.SetValueWithoutNotify(GameSettings.MouseSensitivity);
            fovSlider.SetValueWithoutNotify(GameSettings.FieldOfView);
            RefreshLabels();
        }

        private void RefreshLabels()
        {
            sensitivityValue.text = GameSettings.MouseSensitivity.ToString("0.00");
            fovValue.text = GameSettings.FieldOfView.ToString("0") + "°";
            string[] names = QualitySettings.names;
            string quality = names.Length > 0 ? names[Mathf.Clamp(GameSettings.QualityLevel, 0, names.Length - 1)] : "Default";
            UiKit.SetButtonLabel(qualityButton, "QUALITY:  " + quality.ToUpperInvariant());
            UiKit.SetButtonLabel(fullscreenButton, "FULLSCREEN:  " + (GameSettings.Fullscreen ? "ON" : "OFF"));
        }
    }

    /// <summary>Modal yes/no dialog used for destructive actions.</summary>
    public sealed class ConfirmDialog : UiScreen
    {
        private readonly TMP_Text title;
        private readonly TMP_Text message;
        private readonly Button confirmButton;
        private Action onConfirm;

        public ConfirmDialog(SeedAndRockGameFlow flow, Transform parent) : base(flow, parent, "ConfirmDialog")
        {
            RectTransform card = UiKit.CreateCard(Content, "ConfirmCard", new Vector2(520f, 300f), new Vector2(0.5f, 0.5f), SeedAndRockTheme.PanelRaised);
            RectTransform column = UiKit.RectOf(UiKit.CreateColumn(card, "Column", 12f, new RectOffset(34, 34, 30, 26)).gameObject);
            UiKit.Stretch(column);

            title = UiKit.CreateText(column, "Title", "", SeedAndRockTheme.SubheadingSize + 4f, SeedAndRockTheme.Pale, FontStyles.Bold);
            UiKit.Size(title, null, 38f);
            message = UiKit.CreateText(column, "Message", "", SeedAndRockTheme.BodySize - 1f, SeedAndRockTheme.Muted);
            UiKit.Size(message, null, 84f);
            UiKit.CreateSpacer(column, 4f);

            RectTransform buttons = UiKit.RectOf(UiKit.CreateRow(column, "Buttons", 12f, new RectOffset(0, 0, 0, 0)).gameObject);
            UiKit.Size(buttons.GetComponent<HorizontalLayoutGroup>(), null, SeedAndRockTheme.ButtonHeight);
            Button cancel = UiKit.CreateButton(buttons, "CancelButton", "CANCEL", UiKit.ButtonStyle.Ghost, () => Hide());
            UiKit.Size(cancel, 200f, SeedAndRockTheme.ButtonHeight, 1f);
            confirmButton = UiKit.CreateButton(buttons, "ConfirmButton", "", UiKit.ButtonStyle.Danger, () =>
            {
                Action action = onConfirm;
                Hide();
                action?.Invoke();
            });
            UiKit.Size(confirmButton, 200f, SeedAndRockTheme.ButtonHeight, 1f);
        }

        public void Ask(string heading, string body, string confirmLabel, Action confirmed)
        {
            title.text = heading;
            message.text = body;
            UiKit.SetButtonLabel(confirmButton, confirmLabel);
            onConfirm = confirmed;
            Show();
        }
    }
}
