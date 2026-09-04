using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using SeedAndRock.Player;
using SeedAndRock.World;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace SeedAndRock.UI
{
    [Serializable]
    public sealed class SavedWorld
    {
        public string id;
        public string worldName;
        public int seed;
        public string difficulty;
        public string createdUtc;
        public string lastPlayedUtc;
        public bool hasVisited;
        public float playerX;
        public float playerY;
        public float playerZ;
    }

    [Serializable]
    internal sealed class SavedWorldCollection { public List<SavedWorld> worlds = new List<SavedWorld>(); }

    /// <summary>Small persistent registry. Terrain is recreated from its seed; player progress is stored as world metadata.</summary>
    public static class WorldSaveRegistry
    {
        private const string FileName = "seed-and-rock-worlds.json";
        private static string FilePath => Path.Combine(Application.persistentDataPath, FileName);

        public static List<SavedWorld> Load()
        {
            try
            {
                if (!File.Exists(FilePath)) return new List<SavedWorld>();
                SavedWorldCollection collection = JsonUtility.FromJson<SavedWorldCollection>(File.ReadAllText(FilePath));
                return collection != null && collection.worlds != null ? collection.worlds : new List<SavedWorld>();
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[SeedAndRock] Could not read saved worlds: " + exception.Message);
                return new List<SavedWorld>();
            }
        }

        public static void Save(SavedWorld world)
        {
            List<SavedWorld> worlds = Load();
            int index = worlds.FindIndex(candidate => candidate.id == world.id);
            if (index >= 0) worlds[index] = world; else worlds.Add(world);
            string json = JsonUtility.ToJson(new SavedWorldCollection { worlds = worlds }, true);
            string temporary = FilePath + ".tmp";
            Directory.CreateDirectory(Application.persistentDataPath);
            File.WriteAllText(temporary, json);
            File.Copy(temporary, FilePath, true);
            File.Delete(temporary);
        }

        public static bool ContainsSeed(int seed) => Load().Exists(world => world.seed == seed);

        public static int GenerateUniqueSeed()
        {
            int seed;
            do
            {
                seed = Guid.NewGuid().GetHashCode() & int.MaxValue;
            } while (seed == 0 || ContainsSeed(seed));
            return seed;
        }
    }

    /// <summary>Creates and owns the complete title → world picker → creation → playable-world flow.</summary>
    public sealed class SeedAndRockGameFlow : MonoBehaviour
    {
        private enum Screen { MainMenu, WorldSelection, WorldCreation, InWorld }

        public static SeedAndRockGameFlow Instance { get; private set; }

        private const string CanvasName = "SeedAndRock_GameFlowCanvas";
        private readonly Color night = new Color(0.025f, 0.055f, 0.070f, 0.97f);
        private readonly Color panel = new Color(0.055f, 0.115f, 0.125f, 0.96f);
        private readonly Color teal = new Color(0.15f, 0.72f, 0.62f, 1f);
        private readonly Color gold = new Color(0.95f, 0.68f, 0.23f, 1f);
        private readonly Color pale = new Color(0.88f, 0.96f, 0.92f, 1f);

        private WorldGenerator generator;
        private Canvas canvas;
        private UnityEngine.UI.Image dimmer;
        private RectTransform mainMenu;
        private RectTransform selectionMenu;
        private RectTransform creationMenu;
        private RectTransform inWorldMenu;
        private RectTransform explorationHud;
        private TMP_Text explorationWorldLabel;
        private RectTransform worldListContent;
        private TMP_InputField nameInput;
        private TMP_InputField seedInput;
        private TMP_Text difficultyText;
        private TMP_Text creationHint;
        private TMP_Text selectionHint;
        private string selectedDifficulty = "Normal";
        private SavedWorld currentWorld;
        private Screen currentScreen;

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private IEnumerator Start()
        {
            yield return null;
            generator = FindFirstObjectByType<WorldGenerator>();
            BuildCanvas();
            SetPlayerControl(false);
            ShowMainMenu();
        }

        private void Update()
        {
            if (!Keyboard.current.escapeKey.wasPressedThisFrame) return;
            if (currentScreen == Screen.InWorld) ShowWorldSelection();
            else if (currentScreen == Screen.WorldCreation) ShowWorldSelection();
            else if (currentScreen == Screen.WorldSelection) ShowMainMenu();
        }

        private void OnApplicationPause(bool paused) { if (paused) SaveCurrentWorld(); }
        private void OnApplicationQuit() { SaveCurrentWorld(); }

        public void ShowMainMenu()
        {
            SaveCurrentWorld();
            currentScreen = Screen.MainMenu;
            SetPlayerControl(false);
            SetScreen(mainMenu);
        }

        public void ShowWorldSelection()
        {
            SaveCurrentWorld();
            currentScreen = Screen.WorldSelection;
            SetPlayerControl(false);
            PopulateWorldList();
            SetScreen(selectionMenu);
        }

        public void ShowWorldCreation()
        {
            currentScreen = Screen.WorldCreation;
            selectedDifficulty = "Normal";
            nameInput.text = "New World";
            seedInput.text = WorldSaveRegistry.GenerateUniqueSeed().ToString();
            difficultyText.text = "Difficulty: " + selectedDifficulty;
            creationHint.text = "A unique seed is ready. You can replace it with your own number.";
            SetScreen(creationMenu);
        }

        public void GenerateAnotherSeed()
        {
            seedInput.text = WorldSaveRegistry.GenerateUniqueSeed().ToString();
            creationHint.text = "New unique seed generated.";
        }

        public void CycleDifficulty()
        {
            selectedDifficulty = selectedDifficulty == "Peaceful" ? "Easy" : selectedDifficulty == "Easy" ? "Normal" : selectedDifficulty == "Normal" ? "Hard" : "Peaceful";
            difficultyText.text = "Difficulty: " + selectedDifficulty;
        }

        public void CreateWorld()
        {
            string worldName = nameInput.text.Trim();
            if (string.IsNullOrWhiteSpace(worldName)) { creationHint.text = "Choose a name for this world."; return; }
            if (!int.TryParse(seedInput.text.Trim(), out int seed)) { creationHint.text = "Seeds must be whole numbers."; return; }
            if (WorldSaveRegistry.ContainsSeed(seed)) { creationHint.text = "That seed already belongs to another saved world."; return; }

            SavedWorld world = new SavedWorld
            {
                id = Guid.NewGuid().ToString("N"), worldName = worldName, seed = seed, difficulty = selectedDifficulty,
                createdUtc = DateTime.UtcNow.ToString("O"), lastPlayedUtc = DateTime.UtcNow.ToString("O")
            };
            WorldSaveRegistry.Save(world);
            EnterWorld(world);
        }

        public void EnterWorld(SavedWorld world)
        {
            if (generator == null) { selectionHint.text = "World generator is not present in this scene."; return; }
            currentWorld = world;
            currentWorld.lastPlayedUtc = DateTime.UtcNow.ToString("O");
            WorldSaveRegistry.Save(currentWorld);
            explorationWorldLabel.text = world.worldName + "  •  Seed " + world.seed + "  •  ESC: Save & return";
            currentScreen = Screen.InWorld;
            SetScreen(inWorldMenu);
            StartCoroutine(GenerateAndEnter());
        }

        private IEnumerator GenerateAndEnter()
        {
            yield return null;
            generator.LoadWorldSeed(currentWorld.seed);
            yield return null;
            if (currentWorld.hasVisited)
            {
                GameObject player = GameObject.Find("SeedAndRock_Player");
                if (player != null) player.transform.position = new Vector3(currentWorld.playerX, currentWorld.playerY, currentWorld.playerZ);
            }
            inWorldMenu.gameObject.SetActive(false);
            dimmer.gameObject.SetActive(false);
            explorationHud.gameObject.SetActive(true);
            SetPlayerControl(true);
        }

        private void SaveCurrentWorld()
        {
            if (currentWorld == null || currentScreen != Screen.InWorld) return;
            GameObject player = GameObject.Find("SeedAndRock_Player");
            if (player != null)
            {
                Vector3 position = player.transform.position;
                currentWorld.playerX = position.x; currentWorld.playerY = position.y; currentWorld.playerZ = position.z;
                currentWorld.hasVisited = true;
            }
            currentWorld.lastPlayedUtc = DateTime.UtcNow.ToString("O");
            WorldSaveRegistry.Save(currentWorld);
        }

        private void SetPlayerControl(bool enabled)
        {
            FirstPersonExplorerController controller = FindFirstObjectByType<FirstPersonExplorerController>(FindObjectsInactive.Include);
            if (controller != null) controller.enabled = enabled;
            Cursor.lockState = enabled ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !enabled;
        }

        private void BuildCanvas()
        {
            if (GameObject.Find(CanvasName) != null) return;
            GameObject root = new GameObject(CanvasName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 50;
            CanvasScaler scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920f, 1080f); scaler.matchWidthOrHeight = 0.5f;
            if (FindFirstObjectByType<EventSystem>() == null)
            {
                GameObject events = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                DontDestroyOnLoad(events);
            }

            dimmer = CreateImage(root.transform, "Atmosphere", new Color(0.01f, 0.035f, 0.045f, 0.83f), Stretch());
            mainMenu = BuildMainMenu(root.transform);
            selectionMenu = BuildWorldSelection(root.transform);
            creationMenu = BuildWorldCreation(root.transform);
            inWorldMenu = BuildInWorldMenu(root.transform);
            explorationHud = BuildExplorationHud(root.transform);
        }

        private RectTransform BuildMainMenu(Transform parent)
        {
            RectTransform screen = CreatePanel(parent, "MainMenu", night, Stretch());
            CreateText(screen, "SEED & ROCK", 72, pale, new Vector2(0.5f, 0.70f), new Vector2(760f, 105f), FontStyles.Bold);
            CreateText(screen, "A WORLD MADE YOUR WAY", 19, teal, new Vector2(0.5f, 0.61f), new Vector2(500f, 38f), FontStyles.Bold);
            CreateText(screen, "Explore one enduring world, shaped by your seed.", 22, new Color(0.66f, 0.77f, 0.75f), new Vector2(0.5f, 0.555f), new Vector2(620f, 40f));
            MakeButton(screen, "PlayButton", "PLAY", teal, new Vector2(0.5f, 0.405f), new Vector2(390f, 76f), ShowWorldSelection);
            MakeButton(screen, "QuitButton", "QUIT", new Color(0.16f, 0.25f, 0.27f), new Vector2(0.5f, 0.315f), new Vector2(390f, 58f), Application.Quit);
            CreateText(screen, "Each saved world is a deterministic landscape: your seed always leads home.", 15, new Color(0.50f, 0.63f, 0.62f), new Vector2(0.5f, 0.10f), new Vector2(900f, 32f));
            return screen;
        }

        private RectTransform BuildWorldSelection(Transform parent)
        {
            RectTransform screen = CreatePanel(parent, "WorldSelection", night, Stretch());
            CreateText(screen, "YOUR WORLDS", 42, pale, new Vector2(0.5f, 0.87f), new Vector2(760f, 64f), FontStyles.Bold);
            selectionHint = CreateText(screen, "Choose a landscape to continue your journey.", 18, new Color(0.62f, 0.76f, 0.73f), new Vector2(0.5f, 0.815f), new Vector2(760f, 36f));
            RectTransform list = CreatePanel(screen, "WorldListFrame", panel, Anchored(new Vector2(0.5f, 0.45f), new Vector2(860f, 560f)));
            worldListContent = CreateScrollView(list, "WorldScrollView");
            MakeButton(screen, "CreateNewWorldButton", "+  CREATE NEW WORLD", gold, new Vector2(0.5f, 0.105f), new Vector2(390f, 64f), ShowWorldCreation);
            MakeButton(screen, "BackToMainButton", "BACK", new Color(0.14f, 0.24f, 0.25f), new Vector2(0.12f, 0.105f), new Vector2(185f, 54f), ShowMainMenu);
            return screen;
        }

        private RectTransform BuildWorldCreation(Transform parent)
        {
            RectTransform screen = CreatePanel(parent, "WorldCreation", night, Stretch());
            RectTransform card = CreatePanel(screen, "CreationCard", panel, Anchored(new Vector2(0.5f, 0.50f), new Vector2(760f, 680f)));
            CreateText(card, "CREATE NEW WORLD", 38, pale, new Vector2(0.5f, 0.89f), new Vector2(670f, 55f), FontStyles.Bold);
            CreateText(card, "WORLD NAME", 15, teal, new Vector2(0.15f, 0.755f), new Vector2(250f, 28f), FontStyles.Bold, TextAlignmentOptions.Left);
            nameInput = CreateInput(card, "WorldNameInput", new Vector2(0.5f, 0.69f), "New World");
            CreateText(card, "WORLD SEED", 15, teal, new Vector2(0.15f, 0.565f), new Vector2(250f, 28f), FontStyles.Bold, TextAlignmentOptions.Left);
            seedInput = CreateInput(card, "WorldSeedInput", new Vector2(0.5f, 0.50f), "A unique seed will be generated", TMP_InputField.ContentType.IntegerNumber);
            MakeButton(card, "RandomSeedButton", "RANDOMIZE", new Color(0.13f, 0.31f, 0.31f), new Vector2(0.72f, 0.39f), new Vector2(205f, 46f), GenerateAnotherSeed, 15);
            Button difficulty = MakeButton(card, "DifficultyButton", "Difficulty: Normal", new Color(0.17f, 0.28f, 0.31f), new Vector2(0.50f, 0.29f), new Vector2(510f, 60f), CycleDifficulty, 19);
            difficultyText = difficulty.GetComponentInChildren<TMP_Text>();
            creationHint = CreateText(card, "", 14, new Color(0.78f, 0.84f, 0.74f), new Vector2(0.5f, 0.19f), new Vector2(610f, 30f));
            MakeButton(card, "ConfirmCreateWorldButton", "CREATE WORLD", teal, new Vector2(0.5f, 0.095f), new Vector2(400f, 60f), CreateWorld, 20);
            MakeButton(screen, "BackToWorldSelectionButton", "BACK", new Color(0.14f, 0.24f, 0.25f), new Vector2(0.12f, 0.105f), new Vector2(185f, 54f), ShowWorldSelection);
            return screen;
        }

        private RectTransform BuildInWorldMenu(Transform parent)
        {
            RectTransform screen = CreatePanel(parent, "GeneratingWorld", new Color(0.01f, 0.035f, 0.045f, 0.93f), Stretch());
            CreateText(screen, "SHAPING YOUR WORLD", 40, pale, new Vector2(0.5f, 0.54f), new Vector2(700f, 60f), FontStyles.Bold);
            CreateText(screen, "Reading the seed and growing its landscape…", 20, new Color(0.66f, 0.78f, 0.76f), new Vector2(0.5f, 0.47f), new Vector2(700f, 35f));
            return screen;
        }

        private RectTransform BuildExplorationHud(Transform parent)
        {
            RectTransform hud = CreatePanel(parent, "ExplorationHud", new Color(0.02f, 0.07f, 0.08f, 0.80f), Anchored(new Vector2(0.5f, 0.965f), new Vector2(720f, 44f)));
            explorationWorldLabel = CreateText(hud, "WorldLabel", 15, new Color(0.80f, 0.92f, 0.88f), new Vector2(0.5f, 0.5f), new Vector2(690f, 38f), FontStyles.Bold);
            hud.gameObject.SetActive(false);
            return hud;
        }

        private void PopulateWorldList()
        {
            for (int i = worldListContent.childCount - 1; i >= 0; i--) Destroy(worldListContent.GetChild(i).gameObject);
            List<SavedWorld> worlds = WorldSaveRegistry.Load();
            worlds.Sort((left, right) => string.Compare(right.lastPlayedUtc, left.lastPlayedUtc, StringComparison.Ordinal));
            if (worlds.Count == 0)
            {
                CreateText(worldListContent, "No worlds yet — create one and make it your own.", 19, new Color(0.62f, 0.72f, 0.70f), Vector2.zero, new Vector2(760f, 88f));
                return;
            }
            foreach (SavedWorld world in worlds)
            {
                SavedWorld captured = world;
                Button button = MakeButton(worldListContent, "World_" + world.id, "", new Color(0.09f, 0.20f, 0.21f), Vector2.zero, new Vector2(760f, 94f), () => EnterWorld(captured));
                LayoutElement layout = button.gameObject.AddComponent<LayoutElement>(); layout.preferredHeight = 94f;
                TMP_Text label = button.GetComponentInChildren<TMP_Text>();
                label.alignment = TextAlignmentOptions.Left;
                label.margin = new Vector4(28f, 0f, 16f, 0f);
                label.text = "<b>" + Escape(world.worldName) + "</b>\n<size=15><color=#9DC9C0>Seed " + world.seed + "   •   " + Escape(world.difficulty) + "   •   " + (world.hasVisited ? "Explored" : "New") + "</color></size>";
            }
        }

        private void SetScreen(RectTransform active)
        {
            dimmer.gameObject.SetActive(active != inWorldMenu || inWorldMenu.gameObject.activeSelf);
            if (active != inWorldMenu) explorationHud.gameObject.SetActive(false);
            mainMenu.gameObject.SetActive(active == mainMenu);
            selectionMenu.gameObject.SetActive(active == selectionMenu);
            creationMenu.gameObject.SetActive(active == creationMenu);
            inWorldMenu.gameObject.SetActive(active == inWorldMenu);
        }

        private RectTransform CreateScrollView(Transform parent, string name)
        {
            GameObject root = new GameObject(name, typeof(RectTransform), typeof(ScrollRect));
            root.transform.SetParent(parent, false); RectTransform rootRect = root.GetComponent<RectTransform>(); ApplyRect(rootRect, Stretch(new Vector2(24f, 24f), new Vector2(-24f, -24f)));
            GameObject viewport = new GameObject("Viewport", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(Mask));
            viewport.transform.SetParent(root.transform, false); RectTransform viewportRect = viewport.GetComponent<RectTransform>(); ApplyRect(viewportRect, Stretch()); viewport.GetComponent<UnityEngine.UI.Image>().color = new Color(0f, 0f, 0f, 0.01f); viewport.GetComponent<Mask>().showMaskGraphic = false;
            GameObject content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            content.transform.SetParent(viewport.transform, false); RectTransform contentRect = content.GetComponent<RectTransform>(); contentRect.anchorMin = new Vector2(0f, 1f); contentRect.anchorMax = new Vector2(1f, 1f); contentRect.pivot = new Vector2(0.5f, 1f); contentRect.anchoredPosition = Vector2.zero; contentRect.sizeDelta = new Vector2(0f, 1f);
            VerticalLayoutGroup group = content.GetComponent<VerticalLayoutGroup>(); group.padding = new RectOffset(4, 4, 4, 4); group.spacing = 12f; group.childControlWidth = true; group.childControlHeight = false; group.childForceExpandWidth = true; group.childForceExpandHeight = false;
            content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            ScrollRect scroll = root.GetComponent<ScrollRect>(); scroll.viewport = viewportRect; scroll.content = contentRect; scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 28f;
            return contentRect;
        }

        private TMP_InputField CreateInput(Transform parent, string name, Vector2 anchor, string placeholder, TMP_InputField.ContentType type = TMP_InputField.ContentType.Standard)
        {
            GameObject root = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(TMP_InputField)); root.transform.SetParent(parent, false);
            RectTransform rootRect = root.GetComponent<RectTransform>(); rootRect.anchorMin = rootRect.anchorMax = anchor; rootRect.pivot = new Vector2(0.5f, 0.5f); rootRect.sizeDelta = new Vector2(510f, 62f);
            root.GetComponent<UnityEngine.UI.Image>().color = new Color(0.025f, 0.070f, 0.078f, 1f);
            TMP_InputField input = root.GetComponent<TMP_InputField>(); input.contentType = type; input.caretColor = pale; input.selectionColor = new Color(teal.r, teal.g, teal.b, 0.35f);
            RectTransform textRect = CreateText(root.transform, "Text", 20, pale, Vector2.zero, new Vector2(480f, 45f), FontStyles.Normal, TextAlignmentOptions.Left).rectTransform; textRect.anchorMin = textRect.anchorMax = new Vector2(0.5f, 0.5f); textRect.offsetMin = new Vector2(-230f, -22f); textRect.offsetMax = new Vector2(230f, 22f);
            TMP_Text text = textRect.GetComponent<TMP_Text>(); text.textWrappingMode = TextWrappingModes.NoWrap;
            TMP_Text hint = CreateText(root.transform, "Placeholder", 19, new Color(0.44f, 0.57f, 0.56f), Vector2.zero, new Vector2(480f, 45f), FontStyles.Italic, TextAlignmentOptions.Left); hint.text = placeholder; RectTransform hintRect = hint.rectTransform; hintRect.anchorMin = hintRect.anchorMax = new Vector2(0.5f, 0.5f); hintRect.offsetMin = new Vector2(-230f, -22f); hintRect.offsetMax = new Vector2(230f, 22f);
            input.textComponent = text; input.placeholder = hint; input.textViewport = rootRect;
            return input;
        }

        private Button MakeButton(Transform parent, string name, string caption, Color color, Vector2 anchor, Vector2 size, UnityEngine.Events.UnityAction action, int fontSize = 22)
        {
            GameObject root = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(Button)); root.transform.SetParent(parent, false);
            RectTransform rect = root.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = anchor; rect.pivot = new Vector2(0.5f, 0.5f); rect.sizeDelta = size;
            UnityEngine.UI.Image image = root.GetComponent<UnityEngine.UI.Image>(); image.color = color;
            Button button = root.GetComponent<Button>(); button.targetGraphic = image; button.onClick.AddListener(action);
            ColorBlock colors = button.colors; colors.normalColor = Color.white; colors.highlightedColor = new Color(1.08f, 1.08f, 1.08f, 1f); colors.pressedColor = new Color(0.78f, 0.78f, 0.78f, 1f); colors.selectedColor = colors.highlightedColor; button.colors = colors;
            TMP_Text label = CreateText(root.transform, "Label", fontSize, color.grayscale > 0.6f ? night : pale, Vector2.zero, size, FontStyles.Bold); label.text = caption; label.raycastTarget = false;
            return button;
        }

        private TMP_Text CreateText(Transform parent, string name, float size, Color color, Vector2 anchor, Vector2 dimensions, FontStyles style = FontStyles.Normal, TextAlignmentOptions alignment = TextAlignmentOptions.Center)
        {
            GameObject root = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)); root.transform.SetParent(parent, false);
            RectTransform rect = root.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = anchor; rect.pivot = new Vector2(0.5f, 0.5f); rect.sizeDelta = dimensions;
            TextMeshProUGUI text = root.GetComponent<TextMeshProUGUI>(); text.font = TMP_Settings.defaultFontAsset; text.fontSize = size; text.color = color; text.fontStyle = style; text.alignment = alignment; text.raycastTarget = false; text.overflowMode = TextOverflowModes.Ellipsis;
            return text;
        }

        private UnityEngine.UI.Image CreateImage(Transform parent, string name, Color color, RectSetup setup)
        {
            GameObject root = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Image)); root.transform.SetParent(parent, false); ApplyRect(root.GetComponent<RectTransform>(), setup); UnityEngine.UI.Image image = root.GetComponent<UnityEngine.UI.Image>(); image.color = color; image.raycastTarget = false; return image;
        }

        private RectTransform CreatePanel(Transform parent, string name, Color color, RectSetup setup)
        {
            GameObject root = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Image)); root.transform.SetParent(parent, false); ApplyRect(root.GetComponent<RectTransform>(), setup); UnityEngine.UI.Image image = root.GetComponent<UnityEngine.UI.Image>(); image.color = color; image.raycastTarget = false; return root.GetComponent<RectTransform>();
        }

        private readonly struct RectSetup { public readonly Vector2 min, max, offsetMin, offsetMax; public RectSetup(Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax) { this.min = min; this.max = max; this.offsetMin = offsetMin; this.offsetMax = offsetMax; } }
        private static RectSetup Stretch(Vector2? min = null, Vector2? max = null) => new RectSetup(Vector2.zero, Vector2.one, min ?? Vector2.zero, max ?? Vector2.zero);
        private static RectSetup Anchored(Vector2 anchor, Vector2 size) => new RectSetup(anchor, anchor, -size * 0.5f, size * 0.5f);
        private static void ApplyRect(RectTransform rect, RectSetup setup) { rect.anchorMin = setup.min; rect.anchorMax = setup.max; rect.offsetMin = setup.offsetMin; rect.offsetMax = setup.offsetMax; }
        private static string Escape(string value) => value.Replace("<", "&lt;").Replace(">", "&gt;");
    }

    internal static class SeedAndRockGameBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (UnityEngine.Object.FindFirstObjectByType<SeedAndRockGameFlow>() != null) return;
            new GameObject("SeedAndRock_GameFlow").AddComponent<SeedAndRockGameFlow>();
        }
    }
}
