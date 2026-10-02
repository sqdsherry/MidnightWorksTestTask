using System.Collections.Generic;
using AutoService.Presentation.Hud;
using AutoService.Presentation.Loading;
using AutoService.Presentation.Menu;
using AutoService.Presentation.Pause;
using AutoService.Presentation.Settings;
using AutoService.Services.Scenes;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace AutoService.Bootstrap.Editor
{
    /// <summary>
    /// One-click Editor setup of module C1 (the shell): the shared settings prefab, the <c>MainMenu</c> scene (camera,
    /// light, placeholder backdrop, entry point, menu, New Game confirmation, settings), the loading screen and game loop
    /// in <c>Boot</c>, the pause button / pause menu / settings in <c>Gameplay</c>, every reference on the entry points,
    /// and the Build Profile scene list (Boot, MainMenu, Gameplay).
    /// </summary>
    /// <remarks>
    /// Safe to re-run: UI objects it created carry <see cref="Presentation.Building.WhiteboxGenerated"/> and are
    /// replaced; objects with the same name made by hand are kept and reported. The settings prefab is rebuilt in place
    /// (its GUID, and so every instance, is kept). Editor-only, so scene searches are fine here (CLAUDE.md rule 3).
    /// </remarks>
    internal static class ModuleSetupC1
    {
        private const string Tag = "[C1 Setup] ";
        private const string ScenesFolder = "Assets/_Project/Scenes";
        private const string BootScenePath = ScenesFolder + "/Boot.unity";
        private const string MainMenuScenePath = ScenesFolder + "/MainMenu.unity";
        private const string GameplayScenePath = ScenesFolder + "/Gameplay.unity";
        private const string MainMenuSceneName = "MainMenu";
        private const string GameplaySceneName = "Gameplay";
        private const string PrefabsFolder = "Assets/_Project/Prefabs";
        private const string UiPrefabsFolder = PrefabsFolder + "/UI";
        private const string SettingsPrefabPath = UiPrefabsFolder + "/SettingsPanel.prefab";
        private const string MaterialsFolder = "Assets/_Project/Materials";

        private const string GameTitle = "AUTO SERVICE TYCOON";
        private const string EntryPointName = "[EntryPoint]";
        private const string BackgroundName = "[Background]";
        private const string CameraName = "Main Camera";
        private const string LightName = "Directional Light";
        private const string EventSystemName = "EventSystem";
        private const string MenuCanvasName = "MenuCanvas";
        private const string MainMenuName = "MainMenu";
        private const string ConfirmDialogName = "ConfirmDialog";
        private const string SettingsPanelName = "SettingsPanel";
        private const string LoadingScreenName = "LoadingScreen";
        private const string ScreenHudName = "ScreenHud";
        private const string PauseButtonName = "PauseButton";
        private const string PauseMenuName = "PauseMenu";

        private const int LoadingSortingOrder = 100;
        private static readonly Color LoadingBackground = new Color32(0x14, 0x18, 0x20, 0xFF);

        [MenuItem("AutoService/Setup/Run C1 Setup")]
        private static void Run()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError(Tag + "Stop Play Mode first.");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.LogWarning(Tag + "Cancelled: the open scenes were not saved.");
                return;
            }

            string originalScene = SceneManager.GetActiveScene().path;
            var problems = new List<string>();

            EnsureFolder(PrefabsFolder, "UI");
            SettingsView settingsPrefab = BuildSettingsPrefab();
            SetupMainMenuScene(settingsPrefab, problems);
            SetupBootScene(problems);
            SetupGameplayScene(settingsPrefab, problems);
            SetupBuildScenes();
            AssetDatabase.SaveAssets();

            if (!string.IsNullOrEmpty(originalScene) && AssetDatabase.LoadAssetAtPath<SceneAsset>(originalScene) != null)
            {
                EditorSceneManager.OpenScene(originalScene, OpenSceneMode.Single);
            }

            const string summary = "settings prefab, MainMenu scene, Boot loading screen + game loop, Gameplay pause + settings, "
                + "Build Profile scenes (Boot, MainMenu, Gameplay)";
            if (problems.Count == 0)
            {
                Debug.Log(Tag + "Done: " + summary + ". Press Play.");
            }
            else
            {
                Debug.LogError(Tag + "Finished with " + problems.Count + " problem(s): " + string.Join("; ", problems) + ". (" + summary + ")");
            }
        }

        // ── Settings prefab ──────────────────────────────────────────────────────────────────────────────────────────

        /// <summary>Builds <c>Prefabs/UI/SettingsPanel.prefab</c> (prompt 09a §4.3, §4.5); an existing one is overwritten in place.</summary>
        private static SettingsView BuildSettingsPrefab()
        {
            var root = new GameObject(SettingsPanelName, typeof(RectTransform));
            try
            {
                root.layer = 5;
                var rect = (RectTransform)root.transform;
                SetupUi.Stretch(rect);
                root.AddComponent<Image>().color = SetupUi.DimColor;
                SetupUi.MarkGenerated(root);
                var view = root.AddComponent<SettingsView>();

                RectTransform panel = SetupUi.CreatePanel("Panel", rect, new Vector2(760f, 640f), Vector2.zero);
                TMP_Text title = SetupUi.CreateText(panel, "Title", "Settings", SetupUi.TitleFont, true, TextAlignmentOptions.Center);
                SetupUi.TopBand(title.rectTransform, -28f, 64f, 40f);

                RectTransform music = CreateSettingsRow(panel, "Music", "Music", -120f);
                Slider musicSlider = CreateVolumeSlider(music, out TMP_Text musicValue);
                RectTransform sfx = CreateSettingsRow(panel, "Sfx", "SFX", -192f);
                Slider sfxSlider = CreateVolumeSlider(sfx, out TMP_Text sfxValue);
                RectTransform quality = CreateSettingsRow(panel, "Quality", "Quality", -264f);
                TMP_Dropdown qualityDropdown = SetupUi.CreateDropdown(quality, "QualityDropdown");
                PlaceControl((RectTransform)qualityDropdown.transform, new Vector2(400f, 48f));
                RectTransform fullscreen = CreateSettingsRow(panel, "Fullscreen", "Fullscreen", -336f);
                Toggle fullscreenToggle = SetupUi.CreateToggle(fullscreen, "FullscreenToggle");
                PlaceControl((RectTransform)fullscreenToggle.transform, new Vector2(44f, 44f));
                RectTransform resolution = CreateSettingsRow(panel, "Resolution", "Resolution", -408f);
                TMP_Dropdown resolutionDropdown = SetupUi.CreateDropdown(resolution, "ResolutionDropdown");
                PlaceControl((RectTransform)resolutionDropdown.transform, new Vector2(400f, 48f));

                Button back = SetupUi.CreateButton(panel, "BackButton", "Back", SetupUi.PrimaryColor, SetupUi.ButtonSize);
                SetupUi.Place((RectTransform)back.transform, new Vector2(0.5f, 0f), new Vector2(0f, 40f), SetupUi.ButtonSize, new Vector2(0.5f, 0f));

                var serialized = new SerializedObject(view);
                serialized.FindProperty("_musicSlider").objectReferenceValue = musicSlider;
                serialized.FindProperty("_musicValue").objectReferenceValue = musicValue;
                serialized.FindProperty("_sfxSlider").objectReferenceValue = sfxSlider;
                serialized.FindProperty("_sfxValue").objectReferenceValue = sfxValue;
                serialized.FindProperty("_qualityDropdown").objectReferenceValue = qualityDropdown;
                serialized.FindProperty("_fullscreenToggle").objectReferenceValue = fullscreenToggle;
                serialized.FindProperty("_resolutionDropdown").objectReferenceValue = resolutionDropdown;
                serialized.FindProperty("_backButton").objectReferenceValue = back;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                // Why: SaveAsPrefabAsset overwrites an existing prefab in place, so its GUID (and every instance) is kept.
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, SettingsPrefabPath);
                return prefab != null ? prefab.GetComponent<SettingsView>() : null;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>A row of the settings panel: the label on the left, the control is added on the right.</summary>
        private static RectTransform CreateSettingsRow(RectTransform panel, string name, string label, float top)
        {
            RectTransform row = SetupUi.CreateRect(name + "Row", panel);
            SetupUi.TopBand(row, top, 56f, 48f);
            TMP_Text text = SetupUi.CreateText(row, "Label", label, SetupUi.TextFont, false, TextAlignmentOptions.Left);
            SetupUi.Place(text.rectTransform, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(240f, 56f), new Vector2(0f, 0.5f));
            return row;
        }

        // Controls start 260 units from the row's left edge.
        private static void PlaceControl(RectTransform control, Vector2 size)
        {
            SetupUi.Place(control, new Vector2(0f, 0.5f), new Vector2(260f, 0f), size, new Vector2(0f, 0.5f));
        }

        private static Slider CreateVolumeSlider(RectTransform row, out TMP_Text value)
        {
            Slider slider = SetupUi.CreateSlider(row, "Slider");
            PlaceControl((RectTransform)slider.transform, new Vector2(300f, 28f));
            value = SetupUi.CreateText(row, "Value", "100%", SetupUi.TextFont, true, TextAlignmentOptions.Right);
            SetupUi.Place(value.rectTransform, new Vector2(1f, 0.5f), Vector2.zero, new Vector2(90f, 56f), new Vector2(1f, 0.5f));
            return slider;
        }

        private static SettingsView InstantiateSettings(SettingsView prefab, Transform canvas, List<string> problems, string sceneName)
        {
            if (prefab == null)
            {
                problems.Add("the settings prefab could not be saved (" + sceneName + " has no settings)");
                return null;
            }

            if (!ClearGenerated(canvas, SettingsPanelName, problems, sceneName))
            {
                Transform kept = canvas.Find(SettingsPanelName);
                return kept != null ? kept.GetComponent<SettingsView>() : null;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab.gameObject, canvas);
            instance.name = SettingsPanelName;
            instance.transform.SetAsLastSibling();
            instance.SetActive(false);
            Undo.RegisterCreatedObjectUndo(instance, "Create settings panel");
            return instance.GetComponent<SettingsView>();
        }

        // ── MainMenu scene ───────────────────────────────────────────────────────────────────────────────────────────

        private static void SetupMainMenuScene(SettingsView settingsPrefab, List<string> problems)
        {
            bool exists = AssetDatabase.LoadAssetAtPath<SceneAsset>(MainMenuScenePath) != null;
            Scene scene = exists
                ? EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Single)
                : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            EnsureCamera(scene);
            EnsureLight(scene);
            EnsureBackdrop(scene);
            EnsureEventSystem(scene);
            GameObject entryObject = FindRoot(scene, EntryPointName) ?? CreateRoot(scene, EntryPointName);
            MainMenuEntryPoint entryPoint = entryObject.TryGetComponent(out MainMenuEntryPoint found)
                ? found
                : Undo.AddComponent<MainMenuEntryPoint>(entryObject);

            MainMenuView menu = null;
            ConfirmDialogView dialog = null;
            SettingsView settings = null;
            GameObject existingCanvas = FindRoot(scene, MenuCanvasName);
            if (existingCanvas != null && !SetupUi.IsGenerated(existingCanvas))
            {
                problems.Add("MainMenu: '" + MenuCanvasName + "' was not made by this tool and was left as is (menu references not changed)");
            }
            else
            {
                if (existingCanvas != null)
                {
                    Undo.DestroyObjectImmediate(existingCanvas);
                }

                Canvas canvas = SetupUi.CreateCanvas(MenuCanvasName, null, 0);
                SceneManager.MoveGameObjectToScene(canvas.gameObject, scene);
                SetupUi.MarkGenerated(canvas.gameObject);
                menu = BuildMainMenu(canvas.transform);
                dialog = BuildConfirmDialog(canvas.transform);
                settings = InstantiateSettings(settingsPrefab, canvas.transform, problems, "MainMenu");
                Undo.RegisterCreatedObjectUndo(canvas.gameObject, "Create menu canvas");
            }

            if (menu != null)
            {
                var serialized = new SerializedObject(entryPoint);
                serialized.FindProperty("_menu").objectReferenceValue = menu;
                serialized.FindProperty("_confirmDialog").objectReferenceValue = dialog;
                serialized.FindProperty("_settings").objectReferenceValue = settings;
                serialized.ApplyModifiedProperties();
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, MainMenuScenePath);
        }

        private static MainMenuView BuildMainMenu(Transform canvas)
        {
            RectTransform root = SetupUi.CreateRect(MainMenuName, canvas);
            SetupUi.Stretch(root);
            var view = root.gameObject.AddComponent<MainMenuView>();

            TMP_Text title = SetupUi.CreateText(root, "Title", GameTitle, 96f, true, TextAlignmentOptions.Center);
            SetupUi.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -140f), new Vector2(1400f, 120f), new Vector2(0.5f, 1f));

            float columnHeight = 4f * SetupUi.ButtonSize.y + 3f * SetupUi.ButtonSpacing;
            RectTransform panel = SetupUi.CreatePanel("Panel", root, new Vector2(SetupUi.ButtonSize.x + 64f, columnHeight + 64f), new Vector2(0f, -80f));
            Button[] buttons = SetupUi.CreateButtonColumn(panel, Vector2.zero, "Continue", "New Game", "Settings", "Quit");

            var serialized = new SerializedObject(view);
            serialized.FindProperty("_continueButton").objectReferenceValue = buttons[0];
            serialized.FindProperty("_newGameButton").objectReferenceValue = buttons[1];
            serialized.FindProperty("_settingsButton").objectReferenceValue = buttons[2];
            serialized.FindProperty("_quitButton").objectReferenceValue = buttons[3];
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return view;
        }

        private static ConfirmDialogView BuildConfirmDialog(Transform canvas)
        {
            RectTransform root = SetupUi.CreateDimmedRoot(ConfirmDialogName, canvas);
            var view = root.gameObject.AddComponent<ConfirmDialogView>();

            RectTransform panel = SetupUi.CreatePanel("Panel", root, new Vector2(760f, 320f), Vector2.zero);
            TMP_Text message = SetupUi.CreateText(panel, "Message", "Are you sure?", 32f, false, TextAlignmentOptions.Center, wrap: true);
            SetupUi.TopBand(message.rectTransform, -40f, 140f, 48f);

            var buttonSize = new Vector2(260f, SetupUi.ButtonSize.y);
            Button yes = SetupUi.CreateButton(panel, "YesButton", "Yes", SetupUi.PrimaryColor, buttonSize);
            SetupUi.Place((RectTransform)yes.transform, new Vector2(0.5f, 0f), new Vector2(-145f, 40f), buttonSize, new Vector2(0.5f, 0f));
            Button no = SetupUi.CreateButton(panel, "NoButton", "No", SetupUi.SecondaryColor, buttonSize);
            SetupUi.Place((RectTransform)no.transform, new Vector2(0.5f, 0f), new Vector2(145f, 40f), buttonSize, new Vector2(0.5f, 0f));

            var serialized = new SerializedObject(view);
            serialized.FindProperty("_message").objectReferenceValue = message;
            serialized.FindProperty("_yesButton").objectReferenceValue = yes;
            serialized.FindProperty("_noButton").objectReferenceValue = no;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            root.gameObject.SetActive(false);
            return view;
        }

        private static void EnsureCamera(Scene scene)
        {
            if (FindRoot(scene, CameraName) != null)
            {
                return;
            }

            GameObject cameraObject = CreateRoot(scene, CameraName);
            cameraObject.tag = "MainCamera";
            cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<AudioListener>();
            cameraObject.transform.position = new Vector3(0f, 7f, -12f);
            cameraObject.transform.LookAt(new Vector3(0f, 0.5f, 2f));
        }

        private static void EnsureLight(Scene scene)
        {
            if (FindRoot(scene, LightName) != null)
            {
                return;
            }

            GameObject lightObject = CreateRoot(scene, LightName);
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            light.shadows = LightShadows.Soft;
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        /// <summary>
        /// Placeholder backdrop behind the menu (a ground plane and a few blocks in the project palette); replaced with
        /// real assets in D1, so an existing one is never touched.
        /// </summary>
        private static void EnsureBackdrop(Scene scene)
        {
            if (FindRoot(scene, BackgroundName) != null)
            {
                return;
            }

            GameObject root = CreateRoot(scene, BackgroundName);
            SetupUi.MarkGenerated(root);
            Material ground = WhiteboxLocationBuilder.GetOrCreateColorMaterial(MaterialsFolder + "/M_RoadGray.mat", "M_RoadGray", new Color(0.35f, 0.37f, 0.4f));
            Material blue = WhiteboxLocationBuilder.GetOrCreateColorMaterial(MaterialsFolder + "/M_CarBlue.mat", "M_CarBlue", new Color(0.2f, 0.45f, 0.95f));
            Material red = WhiteboxLocationBuilder.GetOrCreateColorMaterial(MaterialsFolder + "/M_CarRed.mat", "M_CarRed", new Color(0.85f, 0.2f, 0.2f));
            Material orange = WhiteboxLocationBuilder.GetOrCreateColorMaterial(MaterialsFolder + "/M_Storekeeper.mat", "M_Storekeeper", new Color(1f, 0.55f, 0.1f));

            CreateBlock(root.transform, PrimitiveType.Plane, "Ground", Vector3.zero, new Vector3(4f, 1f, 4f), ground);
            CreateBlock(root.transform, PrimitiveType.Cube, "Garage", new Vector3(-5f, 1.5f, 6f), new Vector3(6f, 3f, 4f), blue);
            CreateBlock(root.transform, PrimitiveType.Cube, "Warehouse", new Vector3(5.5f, 1f, 5f), new Vector3(3f, 2f, 3f), orange);
            CreateBlock(root.transform, PrimitiveType.Cube, "Car", new Vector3(0.5f, 0.5f, 1f), new Vector3(1.8f, 1f, 4f), red);
        }

        // Why: no colliders — nothing in the menu is clicked in the world.
        private static void CreateBlock(Transform parent, PrimitiveType type, string name, Vector3 position, Vector3 scale, Material material)
        {
            GameObject block = GameObject.CreatePrimitive(type);
            block.name = name;
            Object.DestroyImmediate(block.GetComponent<Collider>());
            block.transform.SetParent(parent, false);
            block.transform.localPosition = position;
            block.transform.localScale = scale;
            block.GetComponent<Renderer>().sharedMaterial = material;
        }

        private static void EnsureEventSystem(Scene scene)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                if (roots[i].GetComponentInChildren<EventSystem>(true) != null)
                {
                    return;
                }
            }

            GameObject eventSystem = CreateRoot(scene, EventSystemName);
            eventSystem.AddComponent<EventSystem>();
            var module = eventSystem.AddComponent<InputSystemUIInputModule>();
            module.AssignDefaultActions();
        }

        // ── Boot scene ───────────────────────────────────────────────────────────────────────────────────────────────

        private static void SetupBootScene(List<string> problems)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(BootScenePath) == null)
            {
                problems.Add("Boot scene not found at " + BootScenePath);
                return;
            }

            Scene scene = EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            ProjectEntryPoint entryPoint = FindInScene<ProjectEntryPoint>(scene);
            if (entryPoint == null)
            {
                problems.Add("Boot has no ProjectEntryPoint (loading screen not created)");
                return;
            }

            GameLoop loop = entryPoint.TryGetComponent(out GameLoop existingLoop)
                ? existingLoop
                : Undo.AddComponent<GameLoop>(entryPoint.gameObject);

            LoadingScreenView loadingScreen = null;
            if (ClearGenerated(entryPoint.transform, LoadingScreenName, problems, "Boot"))
            {
                loadingScreen = BuildLoadingScreen(entryPoint.transform);
            }
            else
            {
                Transform kept = entryPoint.transform.Find(LoadingScreenName);
                loadingScreen = kept != null ? kept.GetComponent<LoadingScreenView>() : null;
            }

            var serialized = new SerializedObject(entryPoint);
            serialized.FindProperty("_loadingScreen").objectReferenceValue = loadingScreen;
            serialized.FindProperty("_gameLoop").objectReferenceValue = loop;
            serialized.FindProperty("_firstScene").intValue = (int)GameScene.MainMenu;
            serialized.FindProperty("_mainMenuSceneName").stringValue = MainMenuSceneName;
            serialized.FindProperty("_gameplaySceneName").stringValue = GameplaySceneName;
            serialized.ApplyModifiedProperties();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        /// <summary>The persistent loading screen: own canvas above everything, backdrop, title, bar and tip.</summary>
        private static LoadingScreenView BuildLoadingScreen(Transform parent)
        {
            Canvas canvas = SetupUi.CreateCanvas(LoadingScreenName, parent, LoadingSortingOrder);
            GameObject root = canvas.gameObject;
            SetupUi.MarkGenerated(root);
            var group = root.AddComponent<CanvasGroup>();
            var view = root.AddComponent<LoadingScreenView>();
            var rect = (RectTransform)root.transform;

            RectTransform background = SetupUi.CreateRect("Background", rect);
            SetupUi.Stretch(background);
            background.gameObject.AddComponent<Image>().color = LoadingBackground;

            TMP_Text title = SetupUi.CreateText(rect, "Title", GameTitle, 96f, true, TextAlignmentOptions.Center);
            SetupUi.Place(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 140f), new Vector2(1400f, 120f), new Vector2(0.5f, 0.5f));

            RectTransform bar = SetupUi.CreateRect("ProgressBar", rect);
            SetupUi.Place(bar, new Vector2(0.5f, 0.5f), new Vector2(0f, -40f), new Vector2(900f, 28f), new Vector2(0.5f, 0.5f));
            SetupUi.AddImage(bar, SetupUi.SecondaryColor);
            RectTransform fill = SetupUi.CreateRect("Fill", bar);
            SetupUi.Stretch(fill);
            var fillImage = fill.gameObject.AddComponent<Image>();
            fillImage.color = SetupUi.PrimaryColor;
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Horizontal;
            fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
            fillImage.fillAmount = 0f;

            TMP_Text tip = SetupUi.CreateText(rect, "Tip", "Tip", 30f, false, TextAlignmentOptions.Center, wrap: true);
            SetupUi.Place(tip.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -130f), new Vector2(1200f, 100f), new Vector2(0.5f, 0.5f));
            tip.fontStyle = FontStyles.Italic;

            var serialized = new SerializedObject(view);
            serialized.FindProperty("_canvas").objectReferenceValue = canvas;
            serialized.FindProperty("_group").objectReferenceValue = group;
            serialized.FindProperty("_progressFill").objectReferenceValue = fillImage;
            serialized.FindProperty("_tipLabel").objectReferenceValue = tip;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Undo.RegisterCreatedObjectUndo(root, "Create loading screen");
            return view;
        }

        // ── Gameplay scene ───────────────────────────────────────────────────────────────────────────────────────────

        private static void SetupGameplayScene(SettingsView settingsPrefab, List<string> problems)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(GameplayScenePath) == null)
            {
                problems.Add("Gameplay scene not found at " + GameplayScenePath);
                return;
            }

            Scene scene = EditorSceneManager.OpenScene(GameplayScenePath, OpenSceneMode.Single);
            GameplayEntryPoint entryPoint = FindInScene<GameplayEntryPoint>(scene);
            if (entryPoint == null)
            {
                problems.Add("Gameplay has no GameplayEntryPoint (pause not created)");
                return;
            }

            var entry = new SerializedObject(entryPoint);
            Transform hud = FindScreenHud(scene, entry.FindProperty("_balanceView").objectReferenceValue as BalanceView);
            if (hud == null)
            {
                problems.Add("Gameplay has no screen HUD canvas ('" + ScreenHudName + "' or the canvas of _balanceView)");
                return;
            }

            PauseButtonView pauseButton = ClearGenerated(hud, PauseButtonName, problems, "Gameplay")
                ? BuildPauseButton(hud)
                : FindComponent<PauseButtonView>(hud, PauseButtonName);
            PauseMenuView pauseMenu = ClearGenerated(hud, PauseMenuName, problems, "Gameplay")
                ? BuildPauseMenu(hud)
                : FindComponent<PauseMenuView>(hud, PauseMenuName);
            SettingsView settings = InstantiateSettings(settingsPrefab, hud, problems, "Gameplay");

            entry.FindProperty("_pauseButton").objectReferenceValue = pauseButton;
            entry.FindProperty("_pauseMenu").objectReferenceValue = pauseMenu;
            entry.FindProperty("_settingsPanel").objectReferenceValue = settings;
            entry.ApplyModifiedProperties();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static Transform FindScreenHud(Scene scene, BalanceView balance)
        {
            if (balance != null)
            {
                Canvas canvas = balance.GetComponentInParent<Canvas>(true);
                if (canvas != null)
                {
                    return canvas.rootCanvas.transform;
                }
            }

            GameObject named = FindRoot(scene, ScreenHudName);
            return named != null && named.TryGetComponent(out Canvas hud) ? hud.transform : null;
        }

        // Top left, per the brief: pause on the left, money on the right.
        private static PauseButtonView BuildPauseButton(Transform hud)
        {
            var size = new Vector2(72f, 72f);
            Button button = SetupUi.CreateButton(hud, PauseButtonName, "II", SetupUi.SecondaryColor, size);
            SetupUi.Place((RectTransform)button.transform, new Vector2(0f, 1f), new Vector2(24f, -24f), size, new Vector2(0f, 1f));
            SetupUi.MarkGenerated(button.gameObject);
            var view = button.gameObject.AddComponent<PauseButtonView>();
            var serialized = new SerializedObject(view);
            serialized.FindProperty("_button").objectReferenceValue = button;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Undo.RegisterCreatedObjectUndo(button.gameObject, "Create pause button");
            return view;
        }

        private static PauseMenuView BuildPauseMenu(Transform hud)
        {
            RectTransform root = SetupUi.CreateDimmedRoot(PauseMenuName, hud);
            SetupUi.MarkGenerated(root.gameObject);
            var view = root.gameObject.AddComponent<PauseMenuView>();

            float columnHeight = 4f * SetupUi.ButtonSize.y + 3f * SetupUi.ButtonSpacing;
            const float titleHeight = 64f;
            const float padding = 32f;
            var panelSize = new Vector2(SetupUi.ButtonSize.x + 2f * padding, padding + titleHeight + padding + columnHeight + padding);
            RectTransform panel = SetupUi.CreatePanel("Panel", root, panelSize, Vector2.zero);
            TMP_Text title = SetupUi.CreateText(panel, "Title", "Paused", SetupUi.TitleFont, true, TextAlignmentOptions.Center);
            SetupUi.TopBand(title.rectTransform, -padding, titleHeight, padding);

            float columnCenter = panelSize.y * 0.5f - padding - titleHeight - padding - columnHeight * 0.5f;
            Button[] buttons = SetupUi.CreateButtonColumn(panel, new Vector2(0f, columnCenter), "Resume", "Settings", "Main Menu", "Quit");

            var serialized = new SerializedObject(view);
            serialized.FindProperty("_panel").objectReferenceValue = panel.gameObject;
            serialized.FindProperty("_resumeButton").objectReferenceValue = buttons[0];
            serialized.FindProperty("_settingsButton").objectReferenceValue = buttons[1];
            serialized.FindProperty("_mainMenuButton").objectReferenceValue = buttons[2];
            serialized.FindProperty("_quitButton").objectReferenceValue = buttons[3];
            serialized.ApplyModifiedPropertiesWithoutUndo();

            root.SetAsLastSibling();
            root.gameObject.SetActive(false);
            Undo.RegisterCreatedObjectUndo(root.gameObject, "Create pause menu");
            return view;
        }

        // ── Build Profile ────────────────────────────────────────────────────────────────────────────────────────────

        /// <summary>Puts Boot, MainMenu and Gameplay first, in this order; any other listed scenes stay after them.</summary>
        private static void SetupBuildScenes()
        {
            string[] ordered = { BootScenePath, MainMenuScenePath, GameplayScenePath };
            var scenes = new List<EditorBuildSettingsScene>();
            for (int i = 0; i < ordered.Length; i++)
            {
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ordered[i]) != null)
                {
                    scenes.Add(new EditorBuildSettingsScene(ordered[i], true));
                }
            }

            EditorBuildSettingsScene[] current = EditorBuildSettings.scenes;
            for (int i = 0; i < current.Length; i++)
            {
                if (System.Array.IndexOf(ordered, current[i].path) < 0)
                {
                    scenes.Add(current[i]);
                }
            }

            EditorBuildSettings.scenes = scenes.ToArray();
        }

        // ── Helpers ──────────────────────────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Removes the child <paramref name="name"/> of <paramref name="parent"/> if this tool created it.
        /// </summary>
        /// <returns>True when the name is free now (create it); false when a hand-made object of that name was kept.</returns>
        private static bool ClearGenerated(Transform parent, string name, List<string> problems, string sceneName)
        {
            Transform existing = parent.Find(name);
            if (existing == null)
            {
                return true;
            }

            if (!SetupUi.IsGenerated(existing.gameObject))
            {
                problems.Add(sceneName + ": '" + name + "' was not made by this tool and was left as is");
                return false;
            }

            Undo.DestroyObjectImmediate(existing.gameObject);
            return true;
        }

        private static T FindComponent<T>(Transform parent, string name) where T : Component
        {
            Transform child = parent.Find(name);
            return child != null ? child.GetComponent<T>() : null;
        }

        private static T FindInScene<T>(Scene scene) where T : Component
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                var found = roots[i].GetComponentInChildren<T>(true);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private static GameObject FindRoot(Scene scene, string name)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                if (roots[i].name == name)
                {
                    return roots[i];
                }
            }

            return null;
        }

        private static GameObject CreateRoot(Scene scene, string name)
        {
            var root = new GameObject(name);
            SceneManager.MoveGameObjectToScene(root, scene);
            Undo.RegisterCreatedObjectUndo(root, "Create " + name);
            return root;
        }

        private static void EnsureFolder(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder(parent + "/" + name))
            {
                AssetDatabase.CreateFolder(parent, name);
            }
        }
    }
}
