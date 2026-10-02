using System;
using System.Collections.Generic;
using AutoService.Presentation.Hud;
using AutoService.Presentation.Loading;
using AutoService.Presentation.Menu;
using AutoService.Presentation.Pause;
using AutoService.Presentation.Settings;
using AutoService.Presentation.Ui;
using AutoService.Services.Scenes;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
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
    /// <para><b>Safe to re-run.</b> Nothing is rebuilt: objects this tool made (marked with
    /// <see cref="Presentation.Building.WhiteboxGenerated"/>) and the settings prefab are reused — only missing children and
    /// components are added and the references are assigned again. Labels, colors and layout already in place are kept.
    /// Same-named objects made by hand are not touched and are reported.</para>
    /// <para>Why no Undo: the tool opens scenes with <c>OpenScene</c>/<c>NewScene</c>, which clears the undo stack, and saves
    /// each scene right away — undo records would be thrown away anyway. Use git to revert a run.</para>
    /// <para>Editor-only, so scene searches are fine here (CLAUDE.md rule 3).</para>
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
        private const string InputActionsName = "GameControls";

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
        private const string PanelName = "Panel";
        private const string TitleName = "Title";

        private const int LoadingSortingOrder = 100;
        private static readonly Color LoadingBackground = new Color32(0x14, 0x18, 0x20, 0xFF);
        private static readonly Vector2 SettingsPanelSize = new Vector2(760f, 640f);
        private static readonly Vector2 DropdownSize = new Vector2(400f, 48f);

        [MenuItem("AutoService/Setup/Run C1 Setup")]
        private static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            {
                Debug.LogError(Tag + "Wait until Play Mode is stopped and scripts have finished compiling.");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.LogWarning(Tag + "Cancelled: the open scenes were not saved.");
                return;
            }

            SetupUi.WarnIfNoTmpFont(Tag);
            SceneSetup[] originalSetup = EditorSceneManager.GetSceneManagerSetup();
            var problems = new List<string>();

            EnsureFolder(PrefabsFolder, "UI");
            SettingsView settingsPrefab = SetupSettingsPrefab(problems);
            SetupMainMenuScene(settingsPrefab, problems);
            SetupBootScene(problems);
            SetupGameplayScene(settingsPrefab, problems);
            SetupBuildScenes();
            AssetDatabase.SaveAssets();
            RestoreScenes(originalSetup);

            const string summary = "settings prefab, MainMenu scene, Boot loading screen + game loop, Gameplay pause + settings, "
                + "Build Profile scenes (Boot, MainMenu, Gameplay); scenes were saved, use git to revert";
            if (problems.Count == 0)
            {
                Debug.Log(Tag + "Done: " + summary + ". Press Play.");
            }
            else
            {
                Debug.LogError(Tag + "Finished with " + problems.Count + " problem(s): " + string.Join("; ", problems) + ". (" + summary + ")");
            }
        }

        // Why: an untitled scene cannot be restored (it has no path); Boot is where Play starts anyway.
        private static void RestoreScenes(SceneSetup[] setup)
        {
            bool restorable = setup.Length > 0 && Array.TrueForAll(setup, scene => !string.IsNullOrEmpty(scene.path));
            if (restorable)
            {
                EditorSceneManager.RestoreSceneManagerSetup(setup);
            }
            else if (AssetDatabase.LoadAssetAtPath<SceneAsset>(BootScenePath) != null)
            {
                EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            }
        }

        // ── Settings prefab ──────────────────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Creates <c>Prefabs/UI/SettingsPanel.prefab</c> (prompt 09a §4.3, §4.5), or completes the existing one in place:
        /// only missing parts are added, so edits made to the prefab survive and its object ids stay the same.
        /// </summary>
        private static SettingsView SetupSettingsPrefab(List<string> problems)
        {
            bool exists = AssetDatabase.LoadAssetAtPath<GameObject>(SettingsPrefabPath) != null;
            GameObject root = exists ? PrefabUtility.LoadPrefabContents(SettingsPrefabPath) : CreateSettingsRoot();
            try
            {
                CompleteSettings(root, problems);

                // Why: SaveAsPrefabAsset overwrites an existing prefab in place, so its GUID (and every instance) is kept.
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, SettingsPrefabPath);
                return prefab != null ? prefab.GetComponent<SettingsView>() : null;
            }
            finally
            {
                if (exists)
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
                else
                {
                    Object.DestroyImmediate(root);
                }
            }
        }

        private static GameObject CreateSettingsRoot()
        {
            var root = new GameObject(SettingsPanelName, typeof(RectTransform));
            root.layer = 5;
            SetupUi.Stretch((RectTransform)root.transform);
            root.AddComponent<Image>().color = SetupUi.DimColor;
            SetupUi.MarkGenerated(root);
            return root;
        }

        private static void CompleteSettings(GameObject root, List<string> problems)
        {
            var view = SetupUi.GetOrAdd<SettingsView>(root);
            RectTransform panel = SetupUi.EnsureChild(root.transform, PanelName, problems,
                parent => SetupUi.CreatePanel(PanelName, parent, SettingsPanelSize, Vector2.zero));
            if (panel == null)
            {
                return;
            }

            EnsureTitle(panel, "Settings", -28f, 40f, problems);

            RectTransform music = EnsureSettingsRow(panel, "Music", "Music", -120f, problems);
            Slider musicSlider = EnsureVolumeSlider(music, problems, out TMP_Text musicValue);
            RectTransform sfx = EnsureSettingsRow(panel, "Sfx", "SFX", -192f, problems);
            Slider sfxSlider = EnsureVolumeSlider(sfx, problems, out TMP_Text sfxValue);
            RectTransform quality = EnsureSettingsRow(panel, "Quality", "Quality", -264f, problems);
            TMP_Dropdown qualityDropdown = EnsureDropdown(quality, "QualityDropdown", problems);
            RectTransform fullscreen = EnsureSettingsRow(panel, "Fullscreen", "Fullscreen", -336f, problems);
            Toggle fullscreenToggle = fullscreen == null ? null : SetupUi.EnsureChild(fullscreen, "FullscreenToggle", problems, parent =>
            {
                Toggle toggle = SetupUi.CreateToggle(parent, "FullscreenToggle");
                PlaceControl((RectTransform)toggle.transform, new Vector2(44f, 44f));
                return toggle;
            });
            RectTransform resolution = EnsureSettingsRow(panel, "Resolution", "Resolution", -408f, problems);
            TMP_Dropdown resolutionDropdown = EnsureDropdown(resolution, "ResolutionDropdown", problems);

            Button back = SetupUi.EnsureChild(panel, "BackButton", problems, parent =>
            {
                Button button = SetupUi.CreateButton(parent, "BackButton", "Back", SetupUi.PrimaryColor, SetupUi.ButtonSize);
                SetupUi.Place((RectTransform)button.transform, new Vector2(0.5f, 0f), new Vector2(0f, 40f), SetupUi.ButtonSize, new Vector2(0.5f, 0f));
                return button;
            });

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
        }

        /// <summary>A row of the settings panel: the label on the left, the control is added on the right.</summary>
        private static RectTransform EnsureSettingsRow(RectTransform panel, string name, string label, float top, List<string> problems)
        {
            RectTransform row = SetupUi.EnsureRect(panel, name + "Row", problems, rect => SetupUi.TopBand(rect, top, 56f, 48f));
            if (row == null)
            {
                return null;
            }

            SetupUi.EnsureChild(row, "Label", problems, parent =>
            {
                TMP_Text text = SetupUi.CreateText(parent, "Label", label, SetupUi.TextFont, false, TextAlignmentOptions.Left);
                SetupUi.Place(text.rectTransform, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(240f, 56f), new Vector2(0f, 0.5f));
                return text;
            });
            return row;
        }

        // Controls start 260 units from the row's left edge.
        private static void PlaceControl(RectTransform control, Vector2 size)
        {
            SetupUi.Place(control, new Vector2(0f, 0.5f), new Vector2(260f, 0f), size, new Vector2(0f, 0.5f));
        }

        private static Slider EnsureVolumeSlider(RectTransform row, List<string> problems, out TMP_Text value)
        {
            value = null;
            if (row == null)
            {
                return null;
            }

            Slider slider = SetupUi.EnsureChild(row, "Slider", problems, parent =>
            {
                Slider created = SetupUi.CreateSlider(parent, "Slider");
                PlaceControl((RectTransform)created.transform, new Vector2(300f, 28f));
                return created;
            });
            if (slider != null)
            {
                // Why: added to older panels too — the volume is saved when the slider is released.
                SetupUi.GetOrAdd<SliderCommit>(slider.gameObject);
            }

            value = SetupUi.EnsureChild(row, "Value", problems, parent =>
            {
                TMP_Text text = SetupUi.CreateText(parent, "Value", "100%", SetupUi.TextFont, true, TextAlignmentOptions.Right);
                SetupUi.Place(text.rectTransform, new Vector2(1f, 0.5f), Vector2.zero, new Vector2(90f, 56f), new Vector2(1f, 0.5f));
                return text;
            });
            return slider;
        }

        private static TMP_Dropdown EnsureDropdown(RectTransform row, string name, List<string> problems)
        {
            if (row == null)
            {
                return null;
            }

            return SetupUi.EnsureChild(row, name, problems, parent =>
            {
                TMP_Dropdown dropdown = SetupUi.CreateDropdown(parent, name);
                PlaceControl((RectTransform)dropdown.transform, DropdownSize);
                return dropdown;
            });
        }

        private static void EnsureTitle(RectTransform panel, string text, float top, float inset, List<string> problems)
        {
            SetupUi.EnsureChild(panel, TitleName, problems, parent =>
            {
                TMP_Text title = SetupUi.CreateText(parent, TitleName, text, SetupUi.TitleFont, true, TextAlignmentOptions.Center);
                SetupUi.TopBand(title.rectTransform, top, 64f, inset);
                return title;
            });
        }

        /// <summary>Uses the settings instance already under <paramref name="canvas"/>, or adds one of the prefab.</summary>
        private static SettingsView EnsureSettingsInstance(SettingsView prefab, Transform canvas, List<string> problems, string sceneName)
        {
            Transform existing = canvas.Find(SettingsPanelName);
            if (existing != null)
            {
                if (existing.TryGetComponent(out SettingsView found))
                {
                    return found;
                }

                problems.Add(sceneName + ": '" + SettingsPanelName + "' has no SettingsView and was left as is");
                return null;
            }

            if (prefab == null)
            {
                problems.Add(sceneName + ": no settings panel (the settings prefab could not be saved)");
                return null;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab.gameObject, canvas);
            instance.name = SettingsPanelName;
            instance.transform.SetAsLastSibling();
            instance.SetActive(false);
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
            var entryPoint = SetupUi.GetOrAdd<MainMenuEntryPoint>(entryObject);
            var entry = new SerializedObject(entryPoint);

            GameObject canvasObject = FindRoot(scene, MenuCanvasName);
            if (canvasObject != null && !SetupUi.IsGenerated(canvasObject))
            {
                problems.Add("MainMenu: '" + MenuCanvasName + "' was not made by this tool and was left as is (menu references not changed)");
            }
            else
            {
                if (canvasObject == null)
                {
                    Canvas canvas = SetupUi.CreateCanvas(MenuCanvasName, null, 0);
                    canvasObject = canvas.gameObject;
                    SceneManager.MoveGameObjectToScene(canvasObject, scene);
                    SetupUi.MarkGenerated(canvasObject);
                }

                Transform canvasTransform = canvasObject.transform;
                entry.FindProperty("_menu").objectReferenceValue = EnsureMainMenu(canvasTransform, problems);
                entry.FindProperty("_confirmDialog").objectReferenceValue = EnsureConfirmDialog(canvasTransform, problems);
                entry.FindProperty("_settings").objectReferenceValue = EnsureSettingsInstance(settingsPrefab, canvasTransform, problems, "MainMenu");
            }

            SerializedProperty input = entry.FindProperty("_inputActions");
            if (input.objectReferenceValue == null)
            {
                input.objectReferenceValue = FindInputActions(problems);
            }

            entry.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, MainMenuScenePath);
        }

        private static InputActionAsset FindInputActions(List<string> problems)
        {
            string[] guids = AssetDatabase.FindAssets(InputActionsName + " t:" + nameof(InputActionAsset));
            for (int i = 0; i < guids.Length; i++)
            {
                var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(AssetDatabase.GUIDToAssetPath(guids[i]));
                if (asset != null && asset.name == InputActionsName)
                {
                    return asset;
                }
            }

            problems.Add("MainMenu: input actions '" + InputActionsName + "' not found (Esc will not work in the menu)");
            return null;
        }

        private static MainMenuView EnsureMainMenu(Transform canvas, List<string> problems)
        {
            MainMenuView view = SetupUi.EnsureChild(canvas, MainMenuName, problems, parent =>
            {
                RectTransform root = SetupUi.CreateRect(MainMenuName, parent);
                SetupUi.Stretch(root);
                return root.gameObject.AddComponent<MainMenuView>();
            });
            if (view == null)
            {
                return null;
            }

            Transform root = view.transform;
            SetupUi.EnsureChild(root, TitleName, problems, parent =>
            {
                TMP_Text title = SetupUi.CreateText(parent, TitleName, GameTitle, 96f, true, TextAlignmentOptions.Center);
                SetupUi.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -140f), new Vector2(1400f, 120f), new Vector2(0.5f, 1f));
                return title;
            });

            float columnHeight = 4f * SetupUi.ButtonSize.y + 3f * SetupUi.ButtonSpacing;
            RectTransform panel = SetupUi.EnsureChild(root, PanelName, problems,
                parent => SetupUi.CreatePanel(PanelName, parent, new Vector2(SetupUi.ButtonSize.x + 64f, columnHeight + 64f), new Vector2(0f, -80f)));
            Button[] buttons = panel != null
                ? SetupUi.EnsureButtonColumn(panel, Vector2.zero, problems, "Continue", "New Game", "Settings", "Quit")
                : new Button[4];

            var serialized = new SerializedObject(view);
            serialized.FindProperty("_continueButton").objectReferenceValue = buttons[0];
            serialized.FindProperty("_newGameButton").objectReferenceValue = buttons[1];
            serialized.FindProperty("_settingsButton").objectReferenceValue = buttons[2];
            serialized.FindProperty("_quitButton").objectReferenceValue = buttons[3];
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return view;
        }

        private static ConfirmDialogView EnsureConfirmDialog(Transform canvas, List<string> problems)
        {
            ConfirmDialogView view = SetupUi.EnsureChild(canvas, ConfirmDialogName, problems, parent =>
            {
                RectTransform root = SetupUi.CreateDimmedRoot(ConfirmDialogName, parent);
                root.gameObject.SetActive(false);
                return root.gameObject.AddComponent<ConfirmDialogView>();
            });
            if (view == null)
            {
                return null;
            }

            RectTransform panel = SetupUi.EnsureChild(view.transform, PanelName, problems,
                parent => SetupUi.CreatePanel(PanelName, parent, new Vector2(760f, 320f), Vector2.zero));
            if (panel == null)
            {
                return view;
            }

            TMP_Text message = SetupUi.EnsureChild(panel, "Message", problems, parent =>
            {
                TMP_Text text = SetupUi.CreateText(parent, "Message", "Are you sure?", 32f, false, TextAlignmentOptions.Center, wrap: true);
                SetupUi.TopBand(text.rectTransform, -40f, 140f, 48f);
                return text;
            });

            var buttonSize = new Vector2(260f, SetupUi.ButtonSize.y);
            Button yes = SetupUi.EnsureChild(panel, "YesButton", problems, parent =>
            {
                Button button = SetupUi.CreateButton(parent, "YesButton", "Yes", SetupUi.PrimaryColor, buttonSize);
                SetupUi.Place((RectTransform)button.transform, new Vector2(0.5f, 0f), new Vector2(-145f, 40f), buttonSize, new Vector2(0.5f, 0f));
                return button;
            });
            Button no = SetupUi.EnsureChild(panel, "NoButton", problems, parent =>
            {
                Button button = SetupUi.CreateButton(parent, "NoButton", "No", SetupUi.SecondaryColor, buttonSize);
                SetupUi.Place((RectTransform)button.transform, new Vector2(0.5f, 0f), new Vector2(145f, 40f), buttonSize, new Vector2(0.5f, 0f));
                return button;
            });

            var serialized = new SerializedObject(view);
            serialized.FindProperty("_message").objectReferenceValue = message;
            serialized.FindProperty("_yesButton").objectReferenceValue = yes;
            serialized.FindProperty("_noButton").objectReferenceValue = no;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return view;
        }

        private static void EnsureCamera(Scene scene)
        {
            if (FindInScene<Camera>(scene) != null)
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
            if (FindInScene<Light>(scene) != null)
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
            if (FindInScene<EventSystem>(scene) != null)
            {
                return;
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

            GameLoop loop = SetupUi.GetOrAdd<GameLoop>(entryPoint.gameObject);
            LoadingScreenView loadingScreen = EnsureLoadingScreen(entryPoint.transform, problems);

            var serialized = new SerializedObject(entryPoint);
            serialized.FindProperty("_loadingScreen").objectReferenceValue = loadingScreen;
            serialized.FindProperty("_gameLoop").objectReferenceValue = loop;
            serialized.FindProperty("_firstScene").enumValueIndex =
                Array.IndexOf(Enum.GetNames(typeof(GameScene)), nameof(GameScene.MainMenu));
            serialized.FindProperty("_mainMenuSceneName").stringValue = MainMenuSceneName;
            serialized.FindProperty("_gameplaySceneName").stringValue = GameplaySceneName;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        /// <summary>The persistent loading screen: own canvas above everything, backdrop, title, bar and tip.</summary>
        private static LoadingScreenView EnsureLoadingScreen(Transform parent, List<string> problems)
        {
            Transform existing = parent.Find(LoadingScreenName);
            if (existing != null && !SetupUi.IsGenerated(existing.gameObject))
            {
                problems.Add("Boot: '" + LoadingScreenName + "' was not made by this tool and was left as is");
                return existing.GetComponent<LoadingScreenView>();
            }

            GameObject root;
            if (existing != null)
            {
                root = existing.gameObject;
            }
            else
            {
                root = SetupUi.CreateCanvas(LoadingScreenName, parent, LoadingSortingOrder).gameObject;
                SetupUi.MarkGenerated(root);
            }

            var canvas = SetupUi.GetOrAdd<Canvas>(root);
            var group = SetupUi.GetOrAdd<CanvasGroup>(root);
            var view = SetupUi.GetOrAdd<LoadingScreenView>(root);
            Transform rect = root.transform;

            SetupUi.EnsureChild(rect, "Background", problems, p =>
            {
                RectTransform background = SetupUi.CreateRect("Background", p);
                SetupUi.Stretch(background);
                var image = background.gameObject.AddComponent<Image>();
                image.color = LoadingBackground;
                return image;
            });
            SetupUi.EnsureChild(rect, TitleName, problems, p =>
            {
                TMP_Text title = SetupUi.CreateText(p, TitleName, GameTitle, 96f, true, TextAlignmentOptions.Center);
                SetupUi.Place(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 140f), new Vector2(1400f, 120f), new Vector2(0.5f, 0.5f));
                return title;
            });

            RectTransform bar = SetupUi.EnsureRect(rect, "ProgressBar", problems, created =>
            {
                SetupUi.Place(created, new Vector2(0.5f, 0.5f), new Vector2(0f, -40f), new Vector2(900f, 28f), new Vector2(0.5f, 0.5f));
                SetupUi.AddImage(created, SetupUi.SecondaryColor);
            });
            Image fill = bar == null ? null : SetupUi.EnsureChild(bar, "Fill", problems, p =>
            {
                RectTransform fillRect = SetupUi.CreateRect("Fill", p);
                SetupUi.Stretch(fillRect);
                var image = fillRect.gameObject.AddComponent<Image>();
                image.color = SetupUi.PrimaryColor;
                image.type = Image.Type.Filled;
                image.fillMethod = Image.FillMethod.Horizontal;
                image.fillOrigin = (int)Image.OriginHorizontal.Left;
                image.fillAmount = 0f;
                return image;
            });

            TMP_Text tip = SetupUi.EnsureChild(rect, "Tip", problems, p =>
            {
                TMP_Text text = SetupUi.CreateText(p, "Tip", "Tip", 30f, false, TextAlignmentOptions.Center, wrap: true);
                SetupUi.Place(text.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -130f), new Vector2(1200f, 100f), new Vector2(0.5f, 0.5f));
                text.fontStyle = FontStyles.Italic;
                return text;
            });

            var serialized = new SerializedObject(view);
            serialized.FindProperty("_canvas").objectReferenceValue = canvas;
            serialized.FindProperty("_group").objectReferenceValue = group;
            serialized.FindProperty("_progressFill").objectReferenceValue = fill;
            serialized.FindProperty("_tipLabel").objectReferenceValue = tip;
            serialized.ApplyModifiedPropertiesWithoutUndo();
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

            entry.FindProperty("_pauseButton").objectReferenceValue = EnsurePauseButton(hud, problems);
            entry.FindProperty("_pauseMenu").objectReferenceValue = EnsurePauseMenu(hud, problems);
            entry.FindProperty("_settingsPanel").objectReferenceValue = EnsureSettingsInstance(settingsPrefab, hud, problems, "Gameplay");
            entry.ApplyModifiedPropertiesWithoutUndo();

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
        private static PauseButtonView EnsurePauseButton(Transform hud, List<string> problems)
        {
            Transform existing = hud.Find(PauseButtonName);
            if (existing != null && !SetupUi.IsGenerated(existing.gameObject))
            {
                problems.Add("Gameplay: '" + PauseButtonName + "' was not made by this tool and was left as is");
                return existing.GetComponent<PauseButtonView>();
            }

            Button button;
            if (existing != null)
            {
                button = SetupUi.GetOrAdd<Button>(existing.gameObject);
            }
            else
            {
                var size = new Vector2(72f, 72f);
                button = SetupUi.CreateButton(hud, PauseButtonName, "II", SetupUi.SecondaryColor, size);
                SetupUi.Place((RectTransform)button.transform, new Vector2(0f, 1f), new Vector2(24f, -24f), size, new Vector2(0f, 1f));
                SetupUi.MarkGenerated(button.gameObject);
            }

            SetupUi.GetOrAdd<ButtonJuice>(button.gameObject);
            var view = SetupUi.GetOrAdd<PauseButtonView>(button.gameObject);
            var serialized = new SerializedObject(view);
            serialized.FindProperty("_button").objectReferenceValue = button;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return view;
        }

        private static PauseMenuView EnsurePauseMenu(Transform hud, List<string> problems)
        {
            Transform existing = hud.Find(PauseMenuName);
            if (existing != null && !SetupUi.IsGenerated(existing.gameObject))
            {
                problems.Add("Gameplay: '" + PauseMenuName + "' was not made by this tool and was left as is");
                return existing.GetComponent<PauseMenuView>();
            }

            GameObject root;
            if (existing != null)
            {
                root = existing.gameObject;
            }
            else
            {
                RectTransform created = SetupUi.CreateDimmedRoot(PauseMenuName, hud);
                SetupUi.MarkGenerated(created.gameObject);
                created.SetAsLastSibling();
                created.gameObject.SetActive(false);
                root = created.gameObject;
            }

            var view = SetupUi.GetOrAdd<PauseMenuView>(root);
            float columnHeight = 4f * SetupUi.ButtonSize.y + 3f * SetupUi.ButtonSpacing;
            const float titleHeight = 64f;
            const float padding = 32f;
            var panelSize = new Vector2(SetupUi.ButtonSize.x + 2f * padding, padding + titleHeight + padding + columnHeight + padding);
            RectTransform panel = SetupUi.EnsureChild(root.transform, PanelName, problems,
                parent => SetupUi.CreatePanel(PanelName, parent, panelSize, Vector2.zero));

            Button[] buttons = new Button[4];
            if (panel != null)
            {
                EnsureTitle(panel, "Paused", -padding, padding, problems);
                float columnCenter = panelSize.y * 0.5f - padding - titleHeight - padding - columnHeight * 0.5f;
                buttons = SetupUi.EnsureButtonColumn(panel, new Vector2(0f, columnCenter), problems, "Resume", "Settings", "Main Menu", "Quit");
            }

            var serialized = new SerializedObject(view);
            serialized.FindProperty("_panel").objectReferenceValue = panel != null ? panel.gameObject : null;
            serialized.FindProperty("_resumeButton").objectReferenceValue = buttons[0];
            serialized.FindProperty("_settingsButton").objectReferenceValue = buttons[1];
            serialized.FindProperty("_mainMenuButton").objectReferenceValue = buttons[2];
            serialized.FindProperty("_quitButton").objectReferenceValue = buttons[3];
            serialized.ApplyModifiedPropertiesWithoutUndo();
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
                if (Array.IndexOf(ordered, current[i].path) < 0)
                {
                    scenes.Add(current[i]);
                }
            }

            EditorBuildSettings.scenes = scenes.ToArray();
        }

        // ── Helpers ──────────────────────────────────────────────────────────────────────────────────────────────────

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
