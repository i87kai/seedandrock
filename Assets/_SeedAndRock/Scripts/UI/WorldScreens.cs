using System;
using System.Collections.Generic;
using System.Globalization;
using SeedAndRock.Saves;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SeedAndRock.UI
{
    /// <summary>Human-friendly time formatting for world cards.</summary>
    public static class TimeFormat
    {
        public static string Relative(DateTime? utc, DateTime nowUtc)
        {
            if (!utc.HasValue) return "never";
            TimeSpan span = nowUtc - utc.Value;
            if (span.TotalSeconds < 45) return "just now";
            if (span.TotalMinutes < 60) return Mathf.Max(1, (int)span.TotalMinutes) + " min ago";
            if (span.TotalHours < 24) return (int)span.TotalHours + (span.TotalHours < 2 ? " hour ago" : " hours ago");
            if (span.TotalDays < 7) return (int)span.TotalDays + (span.TotalDays < 2 ? " day ago" : " days ago");
            return utc.Value.ToLocalTime().ToString("d MMM yyyy", CultureInfo.InvariantCulture);
        }

        public static string Date(DateTime? utc) => utc.HasValue ? utc.Value.ToLocalTime().ToString("d MMM yyyy", CultureInfo.InvariantCulture) : "unknown";
    }

    /// <summary>Saved-world browser with rich cards, delete confirmation and creation entry point.</summary>
    public sealed class WorldBrowserScreen : UiScreen
    {
        private readonly RectTransform list;
        private readonly TMP_Text hint;
        private readonly TMP_Text summary;

        public WorldBrowserScreen(SeedAndRockGameFlow flow, Transform parent) : base(flow, parent, "WorldBrowser")
        {
            RectTransform header = UiKit.RectOf(UiKit.CreateColumn(Content, "Header", 6f, new RectOffset(0, 0, 0, 0)).gameObject);
            UiKit.Anchor(header, new Vector2(0.5f, 1f), new Vector2(900f, 110f), new Vector2(0f, -54f));
            UiKit.Size(UiKit.CreateLabel(header, "Saved worlds", SeedAndRockTheme.Teal, TextAlignmentOptions.Center), null, 22f);
            UiKit.Size(UiKit.CreateText(header, "Heading", "YOUR WORLDS", SeedAndRockTheme.HeadingSize, SeedAndRockTheme.Pale, FontStyles.Bold), null, 50f);
            hint = UiKit.CreateText(header, "Hint", "Choose a landscape to continue your journey.", SeedAndRockTheme.BodySize, SeedAndRockTheme.Muted);
            UiKit.Size(hint, null, 28f);

            RectTransform card = UiKit.CreateCard(Content, "ListCard", new Vector2(900f, 560f), new Vector2(0.5f, 0.5f), null, new Vector2(0f, -10f));
            list = UiKit.CreateScrollList(card, "WorldList");
            UiKit.Stretch(UiKit.RectOf(list.parent.parent.gameObject), 14f, 14f, 14f, 14f);

            RectTransform footer = UiKit.RectOf(UiKit.CreateRow(Content, "Footer", 14f, new RectOffset(0, 0, 0, 0)).gameObject);
            UiKit.Anchor(footer, new Vector2(0.5f, 0f), new Vector2(900f, SeedAndRockTheme.ButtonHeight), new Vector2(0f, 46f));
            Button back = UiKit.CreateButton(footer, "BackButton", "BACK", UiKit.ButtonStyle.Ghost, flow.ShowMainMenu);
            UiKit.Size(back, 200f, SeedAndRockTheme.ButtonHeight);
            summary = UiKit.CreateText(footer, "Summary", "", SeedAndRockTheme.SmallSize, SeedAndRockTheme.Faint);
            UiKit.Size(summary, 300f, SeedAndRockTheme.ButtonHeight, 1f);
            Button create = UiKit.CreateButton(footer, "CreateButton", "+  CREATE NEW WORLD", UiKit.ButtonStyle.Accent, flow.ShowCreateWorld);
            UiKit.Size(create, 330f, SeedAndRockTheme.ButtonHeight);
        }

        public void SetHint(string message) => hint.text = message;

        public void Populate(List<SavedWorld> worlds)
        {
            for (int i = list.childCount - 1; i >= 0; i--) UnityEngine.Object.Destroy(list.GetChild(i).gameObject);
            DateTime now = DateTime.UtcNow;
            worlds.Sort((left, right) => Nullable.Compare(right.LastPlayedUtc, left.LastPlayedUtc));
            summary.text = worlds.Count == 0 ? "" : worlds.Count == 1 ? "1 world" : worlds.Count + " worlds";

            if (worlds.Count == 0)
            {
                RectTransform empty = UiKit.RectOf(UiKit.CreateColumn(list, "Empty", 8f, new RectOffset(0, 0, 90, 0)).gameObject);
                UiKit.Size(empty.GetComponent<VerticalLayoutGroup>(), null, 260f);
                UiKit.Size(UiKit.CreateText(empty, "Title", "No worlds yet", SeedAndRockTheme.SubheadingSize + 2f, SeedAndRockTheme.Pale, FontStyles.Bold), null, 36f);
                UiKit.Size(UiKit.CreateText(empty, "Body", "Create one and make it your own. Each seed grows a unique, enduring landscape.", SeedAndRockTheme.BodySize - 1f, SeedAndRockTheme.Muted), null, 60f);
                return;
            }

            foreach (SavedWorld world in worlds)
                BuildCard(world, now);
        }

        private void BuildCard(SavedWorld world, DateTime now)
        {
            SavedWorld captured = world;
            Image card = UiKit.CreatePanel(list, "World_" + world.id, SeedAndRockTheme.PanelRaised, true, true);
            UiKit.Size(card, null, 118f);

            Image accent = UiKit.CreatePanel(card.transform, "Accent", world.hasVisited ? SeedAndRockTheme.Teal : SeedAndRockTheme.Gold, true, false);
            accent.rectTransform.anchorMin = new Vector2(0f, 0.18f);
            accent.rectTransform.anchorMax = new Vector2(0f, 0.82f);
            accent.rectTransform.sizeDelta = new Vector2(5f, 0f);
            accent.rectTransform.anchoredPosition = new Vector2(12f, 0f);

            TMP_Text name = UiKit.CreateText(card.transform, "Name", Escape(world.worldName), SeedAndRockTheme.SubheadingSize + 2f, SeedAndRockTheme.Pale, FontStyles.Bold, TextAlignmentOptions.Left);
            name.textWrappingMode = TextWrappingModes.NoWrap;
            UiKit.Anchor(name.rectTransform, new Vector2(0f, 1f), new Vector2(470f, 34f), new Vector2(30f, -16f));

            string meta = "<color=#8FC5BC>Seed</color> " + world.seed +
                          "     <color=#8FC5BC>Difficulty</color> " + Escape(world.difficulty) +
                          "     " + (world.hasVisited ? "<color=#5FD3B8>Explored</color>" : "<color=#F2B84B>New</color>");
            TMP_Text details = UiKit.CreateText(card.transform, "Meta", meta, SeedAndRockTheme.SmallSize, SeedAndRockTheme.Muted, FontStyles.Normal, TextAlignmentOptions.Left);
            details.textWrappingMode = TextWrappingModes.NoWrap;
            UiKit.Anchor(details.rectTransform, new Vector2(0f, 1f), new Vector2(560f, 24f), new Vector2(30f, -54f));

            string dates = "<color=#8FC5BC>Last played</color> " + TimeFormat.Relative(world.LastPlayedUtc, now) + "     <color=#8FC5BC>Created</color> " + TimeFormat.Date(world.CreatedUtc);
            TMP_Text timing = UiKit.CreateText(card.transform, "Dates", dates, SeedAndRockTheme.LabelSize, SeedAndRockTheme.Faint, FontStyles.Normal, TextAlignmentOptions.Left);
            timing.textWrappingMode = TextWrappingModes.NoWrap;
            UiKit.Anchor(timing.rectTransform, new Vector2(0f, 1f), new Vector2(560f, 22f), new Vector2(30f, -82f));

            Button play = UiKit.CreateButton(card.transform, "PlayButton", "PLAY", UiKit.ButtonStyle.Primary, () => Flow.EnterWorld(captured), 46f, SeedAndRockTheme.SmallSize + 1f);
            UiKit.Anchor(play.GetComponent<RectTransform>(), new Vector2(1f, 0.5f), new Vector2(120f, 46f), new Vector2(-130f, 0f));
            Button delete = UiKit.CreateButton(card.transform, "DeleteButton", "DELETE", UiKit.ButtonStyle.Ghost, () => Flow.RequestDeleteWorld(captured), 46f, SeedAndRockTheme.LabelSize);
            UiKit.Anchor(delete.GetComponent<RectTransform>(), new Vector2(1f, 0.5f), new Vector2(96f, 46f), new Vector2(-24f, 0f));
        }

        private static string Escape(string value) => string.IsNullOrEmpty(value) ? "" : value.Replace("<", "&lt;").Replace(">", "&gt;");
    }

    /// <summary>World creation form with live validation.</summary>
    public sealed class CreateWorldScreen : UiScreen
    {
        private readonly TMP_InputField nameInput;
        private readonly TMP_InputField seedInput;
        private readonly Button difficultyButton;
        private readonly Button createButton;
        private readonly TMP_Text feedback;
        private readonly TMP_Text seedPreview;
        private string difficulty = WorldValidation.DefaultDifficulty;

        public CreateWorldScreen(SeedAndRockGameFlow flow, Transform parent) : base(flow, parent, "CreateWorld")
        {
            RectTransform card = UiKit.CreateCard(Content, "CreateCard", new Vector2(640f, 640f), new Vector2(0.5f, 0.5f));
            RectTransform column = UiKit.RectOf(UiKit.CreateColumn(card, "Column", 8f, new RectOffset(40, 40, 30, 26)).gameObject);
            UiKit.Stretch(column);

            UiKit.Size(UiKit.CreateLabel(column, "New journey", SeedAndRockTheme.Teal, TextAlignmentOptions.Center), null, 22f);
            UiKit.Size(UiKit.CreateText(column, "Heading", "CREATE NEW WORLD", SeedAndRockTheme.HeadingSize - 4f, SeedAndRockTheme.Pale, FontStyles.Bold), null, 46f);
            UiKit.CreateSpacer(column, 8f);

            UiKit.Size(UiKit.CreateLabel(column, "World name"), null, 20f);
            nameInput = UiKit.CreateInput(column, "WorldNameInput", "e.g. Misty Vale", TMP_InputField.ContentType.Standard, WorldValidation.MaxNameLength);
            nameInput.onValueChanged.AddListener(_ => Validate());
            UiKit.CreateSpacer(column, 6f);

            UiKit.Size(UiKit.CreateLabel(column, "World seed"), null, 20f);
            RectTransform seedRow = UiKit.RectOf(UiKit.CreateRow(column, "SeedRow", 10f, new RectOffset(0, 0, 0, 0)).gameObject);
            UiKit.Size(seedRow.GetComponent<HorizontalLayoutGroup>(), null, 54f);
            seedInput = UiKit.CreateInput(seedRow, "WorldSeedInput", "Leave empty for a random seed", TMP_InputField.ContentType.Standard, 64);
            UiKit.Size(seedInput, 380f, 54f, 1f);
            seedInput.onValueChanged.AddListener(_ => Validate());
            Button random = UiKit.CreateButton(seedRow, "RandomSeedButton", "RANDOMIZE", UiKit.ButtonStyle.Ghost, () => { seedInput.text = Flow.NewUniqueSeed().ToString(); Validate(); }, 54f, SeedAndRockTheme.SmallSize);
            UiKit.Size(random, 150f, 54f);
            seedPreview = UiKit.CreateText(column, "SeedPreview", "", SeedAndRockTheme.LabelSize, SeedAndRockTheme.Faint, FontStyles.Normal, TextAlignmentOptions.Left);
            UiKit.Size(seedPreview, null, 18f);
            UiKit.CreateSpacer(column, 6f);

            UiKit.Size(UiKit.CreateLabel(column, "Difficulty"), null, 20f);
            difficultyButton = UiKit.CreateButton(column, "DifficultyButton", "", UiKit.ButtonStyle.Card, () =>
            {
                difficulty = WorldValidation.NextDifficulty(difficulty);
                UiKit.SetButtonLabel(difficultyButton, DifficultyLabel());
            }, 52f);
            UiKit.CreateSpacer(column, 4f);

            feedback = UiKit.CreateText(column, "Feedback", "", SeedAndRockTheme.SmallSize, SeedAndRockTheme.Muted);
            UiKit.Size(feedback, null, 40f);

            RectTransform buttons = UiKit.RectOf(UiKit.CreateRow(column, "Buttons", 12f, new RectOffset(0, 0, 0, 0)).gameObject);
            UiKit.Size(buttons.GetComponent<HorizontalLayoutGroup>(), null, SeedAndRockTheme.ButtonHeight);
            Button back = UiKit.CreateButton(buttons, "BackButton", "BACK", UiKit.ButtonStyle.Ghost, flow.ShowWorldBrowser);
            UiKit.Size(back, 180f, SeedAndRockTheme.ButtonHeight);
            createButton = UiKit.CreateButton(buttons, "ConfirmCreateWorldButton", "CREATE WORLD", UiKit.ButtonStyle.Primary, Submit);
            UiKit.Size(createButton, 300f, SeedAndRockTheme.ButtonHeight, 1f);
        }

        public void Reset(int suggestedSeed)
        {
            difficulty = WorldValidation.DefaultDifficulty;
            nameInput.SetTextWithoutNotify("New World");
            seedInput.SetTextWithoutNotify(suggestedSeed.ToString());
            UiKit.SetButtonLabel(difficultyButton, DifficultyLabel());
            Validate();
        }

        private string DifficultyLabel() => "DIFFICULTY:  " + difficulty.ToUpperInvariant() + "   <color=#7FB5AE><size=70%>(click to change)</size></color>";

        /// <summary>Validates the form and returns whether it may be submitted; also updates the feedback text.</summary>
        private bool Validate()
        {
            bool valid = true;
            string message;
            if (!WorldValidation.ValidateName(nameInput.text, out string nameError))
            {
                message = nameError;
                valid = false;
            }
            else
            {
                switch (WorldValidation.TryParseSeed(seedInput.text, out int seed))
                {
                    case SeedParseStatus.Invalid:
                        message = "Seeds must be whole numbers (or short text, which is hashed).";
                        valid = false;
                        break;
                    case SeedParseStatus.Empty:
                        message = "A random unique seed will be generated.";
                        break;
                    default:
                        message = Flow.IsSeedTaken(seed) ? "That seed already belongs to another saved world." : "Ready to grow a new world.";
                        valid = !Flow.IsSeedTaken(seed);
                        break;
                }
            }

            SeedParseStatus status = WorldValidation.TryParseSeed(seedInput.text, out int preview);
            seedPreview.text = status == SeedParseStatus.Text ? "Text seed = " + preview : status == SeedParseStatus.Numeric ? "Numeric seed " + preview : "";
            feedback.text = message;
            feedback.color = valid ? SeedAndRockTheme.Muted : SeedAndRockTheme.Gold;
            createButton.interactable = valid;
            return valid;
        }

        private void Submit()
        {
            if (!Validate()) return;
            Flow.CreateWorld(nameInput.text, seedInput.text, difficulty);
        }

        public void ShowError(string message)
        {
            feedback.text = message;
            feedback.color = SeedAndRockTheme.Gold;
        }
    }

    /// <summary>Loading state between world selection and gameplay. Shows real stage names and only genuine progress.</summary>
    public sealed class LoadingScreen : UiScreen
    {
        private readonly TMP_Text worldTitle;
        private readonly TMP_Text stageLabel;
        private readonly TMP_Text detail;
        private readonly ProgressBar bar;
        private readonly TMP_Text[] stageRows;
        private readonly Button cancelButton;
        private readonly Button backButton;
        private readonly Image spinner;
        private float spin;

        public LoadingScreen(SeedAndRockGameFlow flow, Transform parent) : base(flow, parent, "Loading")
        {
            Image sky = UiKit.CreatePanel(Root, "Sky", SeedAndRockTheme.Night, false, true);
            UiKit.Stretch(sky.rectTransform);
            sky.transform.SetAsFirstSibling();

            RectTransform card = UiKit.CreateCard(Content, "LoadingCard", new Vector2(620f, 520f), new Vector2(0.5f, 0.5f));
            RectTransform column = UiKit.RectOf(UiKit.CreateColumn(card, "Column", 8f, new RectOffset(40, 40, 30, 26)).gameObject);
            UiKit.Stretch(column);

            UiKit.Size(UiKit.CreateLabel(column, "Shaping your world", SeedAndRockTheme.Teal, TextAlignmentOptions.Center), null, 22f);
            worldTitle = UiKit.CreateText(column, "WorldTitle", "", SeedAndRockTheme.HeadingSize - 4f, SeedAndRockTheme.Pale, FontStyles.Bold);
            UiKit.Size(worldTitle, null, 46f);
            UiKit.CreateSpacer(column, 10f);

            RectTransform stageRow = UiKit.RectOf(UiKit.CreateRow(column, "StageRow", 12f, new RectOffset(0, 0, 0, 0), TextAnchor.MiddleLeft).gameObject);
            UiKit.Size(stageRow.GetComponent<HorizontalLayoutGroup>(), null, 30f);
            spinner = UiKit.CreatePanel(stageRow, "Spinner", SeedAndRockTheme.Teal, true, false);
            UiKit.Size(spinner, 14f, 14f);
            stageLabel = UiKit.CreateText(stageRow, "Stage", "Preparing world", SeedAndRockTheme.SubheadingSize, SeedAndRockTheme.Pale, FontStyles.Bold, TextAlignmentOptions.Left);
            UiKit.Size(stageLabel, 400f, 30f, 1f);

            bar = UiKit.CreateProgress(column, "Progress", 8f);
            detail = UiKit.CreateText(column, "Detail", "", SeedAndRockTheme.SmallSize, SeedAndRockTheme.Muted, FontStyles.Normal, TextAlignmentOptions.Left);
            UiKit.Size(detail, null, 22f);
            UiKit.CreateSpacer(column, 8f);
            UiKit.CreateDivider(column, 0.25f);
            UiKit.CreateSpacer(column, 4f);

            int stageCount = (int)World.WorldGenerationStage.Complete;
            stageRows = new TMP_Text[stageCount];
            for (int i = 0; i < stageCount; i++)
            {
                stageRows[i] = UiKit.CreateText(column, "StageRow" + i, "", SeedAndRockTheme.SmallSize, SeedAndRockTheme.Faint, FontStyles.Normal, TextAlignmentOptions.Left);
                UiKit.Size(stageRows[i], null, 20f);
            }

            UiKit.CreateSpacer(column, 6f);
            RectTransform buttons = UiKit.RectOf(UiKit.CreateRow(column, "Buttons", 12f, new RectOffset(0, 0, 0, 0)).gameObject);
            UiKit.Size(buttons.GetComponent<HorizontalLayoutGroup>(), null, SeedAndRockTheme.SmallButtonHeight);
            cancelButton = UiKit.CreateButton(buttons, "CancelButton", "CANCEL", UiKit.ButtonStyle.Ghost, flow.CancelLoading, SeedAndRockTheme.SmallButtonHeight, SeedAndRockTheme.SmallSize);
            UiKit.Size(cancelButton, 200f, SeedAndRockTheme.SmallButtonHeight);
            // Leaving a load must cancel both the scene-preparation coroutine and any worker task;
            // routing through CancelLoading keeps the persistent browser from racing a stale build.
            backButton = UiKit.CreateButton(buttons, "BackButton", "BACK TO WORLDS", UiKit.ButtonStyle.Primary, flow.CancelLoading, SeedAndRockTheme.SmallButtonHeight, SeedAndRockTheme.SmallSize);
            UiKit.Size(backButton, 240f, SeedAndRockTheme.SmallButtonHeight);
        }

        public void Begin(string worldName, int seed)
        {
            worldTitle.text = worldName + "  <size=55%><color=#7FB5AE>seed " + seed + "</color></size>";
            detail.text = "";
            bar.SetProgress(null);
            cancelButton.gameObject.SetActive(true);
            backButton.gameObject.SetActive(false);
            spinner.gameObject.SetActive(true);
            Report(new World.WorldGenerationReport(World.WorldGenerationStage.PreparingWorld, null));
        }

        public void Report(World.WorldGenerationReport report)
        {
            stageLabel.text = report.Label + (report.Fraction.HasValue ? "  <size=70%><color=#7FB5AE>" + Mathf.RoundToInt(report.Fraction.Value * 100f) + "%</color></size>" : "...");
            bar.SetProgress(report.Fraction);
            for (int i = 0; i < stageRows.Length; i++)
            {
                World.WorldGenerationReport row = new World.WorldGenerationReport((World.WorldGenerationStage)i, null);
                bool done = i < report.StageIndex;
                bool current = i == report.StageIndex;
                string marker = done ? "<color=#5FD3B8>•</color>" : current ? "<color=#F2B84B>›</color>" : "<color=#3E5A5A>–</color>";
                stageRows[i].text = marker + "  " + row.Label;
                stageRows[i].color = done ? SeedAndRockTheme.Muted : current ? SeedAndRockTheme.Pale : SeedAndRockTheme.Faint;
            }
        }

        public void SetDetail(string message) => detail.text = message;

        public void ShowError(string message)
        {
            stageLabel.text = "Generation failed";
            stageLabel.color = SeedAndRockTheme.Gold;
            detail.text = message;
            bar.SetProgress(0f);
            spinner.gameObject.SetActive(false);
            cancelButton.gameObject.SetActive(false);
            backButton.gameObject.SetActive(true);
        }

        protected override void OnBeforeShow()
        {
            stageLabel.color = SeedAndRockTheme.Pale;
        }

        /// <summary>Called by the flow each frame while visible to animate the spinner.</summary>
        public void Tick()
        {
            if (!spinner.gameObject.activeSelf) return;
            spin += Time.unscaledDeltaTime * 180f;
            spinner.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -spin);
            float pulse = 0.85f + 0.15f * Mathf.Sin(Time.unscaledTime * 5f);
            spinner.rectTransform.localScale = new Vector3(pulse, pulse, 1f);
        }
    }
}
