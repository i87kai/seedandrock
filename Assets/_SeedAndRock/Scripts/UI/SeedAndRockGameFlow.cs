using System;
using System.Collections;
using System.Collections.Generic;
using SeedAndRock.Player;
using SeedAndRock.Saves;
using SeedAndRock.World;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace SeedAndRock.UI
{
    public enum GameFlowState
    {
        MainMenu,
        WorldBrowser,
        CreateWorld,
        Loading,
        Playing,
        Paused,
        Settings
    }

    /// <summary>
    /// State machine for the runtime flow: Main Menu -> World Browser -> Create/Load -> Loading -> Gameplay
    /// -> Pause -> Save. Screens build and own their widgets; persistence lives in <see cref="WorldSaveService"/>;
    /// generation lives in <see cref="WorldGenerator"/>. This component only coordinates.
    /// </summary>
    public sealed class SeedAndRockGameFlow : MonoBehaviour
    {
        public static SeedAndRockGameFlow Instance { get; private set; }

        private const string CanvasName = "SeedAndRock_GameFlowCanvas";

        private WorldGenerator generator;
        private WorldPresentationController presentation;
        private WorldSaveService saves;

        private MainMenuScreen mainMenu;
        private WorldBrowserScreen browser;
        private CreateWorldScreen creation;
        private LoadingScreen loading;
        private PauseMenuScreen pause;
        private SettingsScreen settings;
        private ConfirmDialog confirm;
        private HudOverlay hud;
        private DeveloperOverlay developerOverlay;
        private ScreenFader fader;

        private GameFlowState state = GameFlowState.MainMenu;
        private GameFlowState settingsReturnState = GameFlowState.MainMenu;
        private SavedWorld currentWorld;
        private bool worldLoaded;

        public GameFlowState State => state;
        public SavedWorld CurrentWorld => currentWorld;

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private IEnumerator Start()
        {
            yield return null;
            generator = FindAnyObjectByType<WorldGenerator>();
            presentation = FindAnyObjectByType<WorldPresentationController>();
            saves = new WorldSaveService();
            BuildCanvas();
            GameSettings.Apply();
            SetPlayerControl(false);
            ShowMainMenu(true);
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.escapeKey.wasPressedThisFrame) HandleEscape();
            if (keyboard.f3Key.wasPressedThisFrame && (state == GameFlowState.Playing || state == GameFlowState.Paused) && developerOverlay != null)
                developerOverlay.Toggle();

            if (state == GameFlowState.Loading) loading.Tick();
            hud?.Tick();
        }

        private void HandleEscape()
        {
            if (confirm != null && confirm.IsVisible) { confirm.Hide(); return; }
            switch (state)
            {
                case GameFlowState.Playing: PauseGame(); break;
                case GameFlowState.Paused: ResumeGame(); break;
                case GameFlowState.Settings: CloseSettings(); break;
                case GameFlowState.CreateWorld: ShowWorldBrowser(); break;
                case GameFlowState.WorldBrowser: ShowMainMenu(); break;
            }
        }

        private void OnApplicationPause(bool paused) { if (paused) SaveCurrentWorld(); }
        private void OnApplicationQuit() { SaveCurrentWorld(); }

        // ------------------------------------------------------------------ menus

        public void ShowMainMenu() => ShowMainMenu(false);

        private void ShowMainMenu(bool instant)
        {
            HideAll();
            state = GameFlowState.MainMenu;
            SetPlayerControl(false);
            mainMenu.Show(instant);
        }

        public void ShowWorldBrowser()
        {
            HideAll();
            state = GameFlowState.WorldBrowser;
            SetPlayerControl(false);
            browser.SetHint("Choose a landscape to continue your journey.");
            browser.Populate(saves.LoadAll());
            browser.Show();
        }

        public void ShowCreateWorld()
        {
            HideAll();
            state = GameFlowState.CreateWorld;
            creation.Reset(NewUniqueSeed());
            creation.Show();
        }

        public void ShowSettings()
        {
            if (state == GameFlowState.Settings) return;
            settingsReturnState = state;
            if (state == GameFlowState.Playing) PauseGame();
            HideAll(keepPause: state == GameFlowState.Paused);
            state = GameFlowState.Settings;
            settings.Show();
        }

        public void CloseSettings()
        {
            GameSettings.Apply();
            settings.Hide();
            switch (settingsReturnState)
            {
                case GameFlowState.Paused:
                case GameFlowState.Playing:
                    state = GameFlowState.Paused;
                    pause.Show();
                    break;
                default:
                    ShowMainMenu();
                    break;
            }
        }

        public int NewUniqueSeed() => saves.GenerateUniqueSeed();
        public bool IsSeedTaken(int seed) => saves.ContainsSeed(seed);

        public void RequestDeleteWorld(SavedWorld world)
        {
            if (world == null) return;
            confirm.Ask("Delete \"" + world.worldName + "\"?",
                "This removes the saved world entry and its player progress. The seed " + world.seed + " can always recreate the same landscape.",
                "DELETE", () =>
                {
                    saves.Delete(world.id);
                    browser.SetHint("\"" + world.worldName + "\" was deleted.");
                    browser.Populate(saves.LoadAll());
                });
        }

        public void CreateWorld(string nameText, string seedText, string difficulty)
        {
            if (!WorldValidation.ValidateName(nameText, out string nameError))
            {
                creation.ShowError(nameError);
                return;
            }

            int seed;
            switch (WorldValidation.TryParseSeed(seedText, out seed))
            {
                case SeedParseStatus.Invalid:
                    creation.ShowError("Seeds must be whole numbers or short text.");
                    return;
                case SeedParseStatus.Empty:
                    seed = NewUniqueSeed();
                    break;
            }

            if (saves.ContainsSeed(seed))
            {
                creation.ShowError("That seed already belongs to another saved world.");
                return;
            }

            SavedWorld world = saves.CreateRecord(nameText, seed, difficulty);
            if (!saves.TrySave(world, out string error))
            {
                creation.ShowError("Could not save the new world: " + error);
                return;
            }

            EnterWorld(world);
        }

        // ------------------------------------------------------------------ loading and gameplay

        public void EnterWorld(SavedWorld world)
        {
            if (world == null) return;
            // WorldSceneBootstrap and this persistent UI can initialize in either order after a scene load.
            // Resolve again at the point it is needed so a valid saved world never gets stranded in Create World.
            if (generator == null) generator = FindAnyObjectByType<WorldGenerator>();
            if (presentation == null) presentation = FindAnyObjectByType<WorldPresentationController>();
            if (generator == null)
            {
                browser.SetHint("World generator is not present in this scene.");
                return;
            }

            HideAll();
            currentWorld = world;
            worldLoaded = false;
            state = GameFlowState.Loading;
            SetPlayerControl(false);
            loading.Begin(world.worldName, world.seed);
            loading.Show();
            if (developerOverlay != null) { developerOverlay.Generator = generator; developerOverlay.WorldName = world.worldName; }
            generator.GenerateWorldAsync(world.seed, report => { if (state == GameFlowState.Loading) loading.Report(report); }, OnWorldGenerated, OnWorldGenerationFailed);
        }

        public void CancelLoading()
        {
            if (state != GameFlowState.Loading) return;
            generator.CancelGeneration();
            currentWorld = null;
            ShowWorldBrowser();
        }

        private void OnWorldGenerated(WorldBuildResult result)
        {
            if (state != GameFlowState.Loading || currentWorld == null) return;
            FirstPersonExplorerController player = PlayerSpawner.Find();
            if (player != null)
            {
                RestorePlayer(player, result);
                presentation?.ApplyToCamera(player.ViewCamera);
            }

            GameSettings.Apply();
            loading.SetDetail("Generated " + result.TotalTriangles.ToString("N0") + " triangles in " + result.Seconds.ToString("0.0") + " s.");
            currentWorld.lastPlayedUtc = SavedWorld.FormatUtc(DateTime.UtcNow);
            saves.TrySave(currentWorld, out _);
            worldLoaded = true;
            StartCoroutine(FadeIntoGameplay());
        }

        private void RestorePlayer(FirstPersonExplorerController player, WorldBuildResult result)
        {
            Vector3 target = result.SpawnPosition;
            float yaw = 0f, pitch = 0f;
            if (currentWorld.hasVisited)
            {
                PlayerStateData saved = currentWorld.GetPlayerState();
                Vector3 savedPosition = new Vector3(saved.x, saved.y, saved.z);
                float half = result.Sampler.Settings.HalfSize;
                bool inside = saved.IsFinite && Mathf.Abs(saved.x) < half && Mathf.Abs(saved.z) < half;
                if (inside)
                {
                    // Saved positions come from the same seed, but settings may have changed since; snap to the current surface when far off.
                    float ground = result.Sampler.GetHeightAt(saved.x, saved.z);
                    if (Mathf.Abs(saved.y - ground) > 6f) savedPosition.y = ground + 0.2f;
                    target = savedPosition;
                    yaw = saved.yaw;
                    pitch = saved.pitch;
                }
            }

            PlayerSpawner.Teleport(player, target, yaw, pitch);
        }

        private IEnumerator FadeIntoGameplay()
        {
            fader.SetOpaque();
            loading.Hide(true);
            yield return null;
            state = GameFlowState.Playing;
            SetPlayerControl(true);
            hud.SetVisible(true);
            hud.Toast(currentWorld.worldName + "  •  seed " + currentWorld.seed, 3.5f);
            bool done = false;
            fader.FadeIn(0.9f, () => done = true);
            while (!done) yield return null;
        }

        private void OnWorldGenerationFailed(Exception exception)
        {
            if (exception is OperationCanceledException)
            {
                if (state == GameFlowState.Loading) ShowWorldBrowser();
                return;
            }

            Debug.LogException(exception);
            if (state != GameFlowState.Loading) return;
            currentWorld = null;
            loading.ShowError("Something went wrong while shaping this world: " + exception.Message);
        }

        public void PauseGame()
        {
            if (state != GameFlowState.Playing) return;
            state = GameFlowState.Paused;
            SetPlayerControl(false);
            Time.timeScale = 0f;
            pause.SetWorld(currentWorld.worldName, currentWorld.seed);
            pause.SetStatus("Last saved " + TimeFormat.Relative(currentWorld.LastPlayedUtc, DateTime.UtcNow));
            pause.Show();
        }

        public void ResumeGame()
        {
            if (state != GameFlowState.Paused) return;
            pause.Hide();
            Time.timeScale = 1f;
            state = GameFlowState.Playing;
            SetPlayerControl(true);
        }

        public void SaveFromPause()
        {
            if (SaveCurrentWorld(out string error))
                pause.SetStatus("World saved just now.");
            else
                pause.SetStatus("Save failed: " + error);
        }

        public void SaveAndReturnToMainMenu()
        {
            SaveCurrentWorld();
            Time.timeScale = 1f;
            UnloadWorld();
            ShowMainMenu();
        }

        public void QuitGame()
        {
            SaveCurrentWorld();
            Time.timeScale = 1f;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void UnloadWorld()
        {
            worldLoaded = false;
            currentWorld = null;
            hud.SetVisible(false);
            if (developerOverlay != null && developerOverlay.IsVisible) developerOverlay.Toggle();
            generator?.ClearGeneratedWorld();
        }

        private void SaveCurrentWorld() => SaveCurrentWorld(out _);

        private bool SaveCurrentWorld(out string error)
        {
            error = null;
            if (currentWorld == null || !worldLoaded) return true;
            WorldSaveService.CapturePlayer(currentWorld, PlayerSpawner.Find());
            bool ok = saves.TrySave(currentWorld, out error);
            if (ok && state == GameFlowState.Playing) hud.Toast("World saved");
            return ok;
        }

        private void SetPlayerControl(bool enabled)
        {
            FirstPersonExplorerController controller = PlayerSpawner.Find();
            if (controller != null) controller.enabled = enabled;
            Cursor.lockState = enabled ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !enabled;
        }

        private void HideAll(bool keepPause = false)
        {
            mainMenu.Hide();
            browser.Hide();
            creation.Hide();
            loading.Hide();
            settings.Hide();
            confirm.Hide(true);
            if (!keepPause) pause.Hide();
        }

        // ------------------------------------------------------------------ canvas

        private void BuildCanvas()
        {
            GameObject existing = GameObject.Find(CanvasName);
            if (existing != null) Destroy(existing);

            GameObject root = new GameObject(CanvasName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.layer = LayerMask.NameToLayer("UI");
            DontDestroyOnLoad(root);
            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;
            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            if (FindAnyObjectByType<EventSystem>() == null)
            {
                GameObject events = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                DontDestroyOnLoad(events);
            }

            Transform parent = root.transform;
            hud = new HudOverlay(parent);
            mainMenu = new MainMenuScreen(this, parent);
            browser = new WorldBrowserScreen(this, parent);
            creation = new CreateWorldScreen(this, parent);
            loading = new LoadingScreen(this, parent);
            pause = new PauseMenuScreen(this, parent);
            settings = new SettingsScreen(this, parent);
            confirm = new ConfirmDialog(this, parent);
            developerOverlay = DeveloperOverlay.Create(parent);
            fader = new ScreenFader(this, parent);
        }
    }

    internal static class SeedAndRockGameBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (UnityEngine.Object.FindAnyObjectByType<SeedAndRockGameFlow>() != null) return;
            new GameObject("SeedAndRock_GameFlow").AddComponent<SeedAndRockGameFlow>();
        }
    }
}
