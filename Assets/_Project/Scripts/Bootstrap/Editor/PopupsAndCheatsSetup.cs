using System.Collections.Generic;
using AutoService.Presentation.Hud;
using AutoService.Presentation.Points;
using AutoService.Presentation.Popups;
using AutoService.Presentation.Ui;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AutoService.Bootstrap.Editor
{
    /// <summary>
    /// Editor setup tool for Task 21: builds the LevelUpPopup, Location2WelcomePopup, and DebugCheatView
    /// inside the Gameplay scene HUD canvas and wires them to <see cref="GameplayEntryPoint"/>.
    /// </summary>
    public static class PopupsAndCheatsSetup
    {
        private const string GameplayScenePath = "Assets/_Project/Scenes/Gameplay.unity";
        private const string GameplaySceneName = "Gameplay";
        private const string ScreenHudName = "ScreenHud";

        private const string StarSpritePath = "Assets/_Project/Art/Kenney/kenney_ui-pack/Yellow/Default/star.png";
        private const string GreenButtonSpritePath = "Assets/_Project/Art/Kenney/kenney_ui-pack/Green/Default/button_rectangle_depth_flat.png";
        private const string GreyButtonSpritePath = "Assets/_Project/Art/Kenney/kenney_ui-pack/Grey/Default/button_rectangle_depth_flat.png";
        private const string BlueButtonSpritePath = "Assets/_Project/Art/Kenney/kenney_ui-pack/Blue/Default/button_rectangle_depth_flat.png";
        private const string RedButtonSpritePath = "Assets/_Project/Art/Kenney/kenney_ui-pack/Red/Default/button_rectangle_depth_flat.png";
        private const string DefaultCardSpritePath = "Assets/_Project/Art/Kenney/kenney_ui-pack/Default/button_rectangle_depth_flat.png";
        private const string SquareButtonSpritePath = "Assets/_Project/Art/Kenney/kenney_ui-pack/Grey/Default/button_square_depth_flat.png";

        private static readonly Color32 DimColor = new Color32(0, 0, 0, 153); // RGBA(0, 0, 0, 0.6)
        private static readonly Color32 CardColor = new Color32(0xF5, 0xF6, 0xFA, 0xFF); // #F5F6FA
        private static readonly Color32 DarkTitleColor = new Color32(0x1E, 0x24, 0x30, 0xFF); // #1E2430
        private static readonly Color32 LevelBadgeColor = new Color32(0x2E, 0xCC, 0x71, 0xFF); // #2ECC71
        private static readonly Color32 DescriptionTextColor = new Color32(0x4A, 0x55, 0x68, 0xFF); // #4A5568
        private static readonly Color32 CheatPanelColor = new Color32(0x1E, 0x24, 0x30, 0xF5);

        [InitializeOnLoadMethod]
        private static void OnEditorLoad()
        {
            EditorApplication.delayCall += () =>
            {
                if (!EditorPrefs.GetBool("Task21_Setup_V2_Completed", false))
                {
                    EditorPrefs.SetBool("Task21_Setup_V2_Completed", true);
                    RunSetup();
                }
            };
        }

        [MenuItem("AutoService/Setup/Setup Task 21 Popups and Debug Cheats")]
        public static void RunSetup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            {
                Debug.LogError("[Setup Task 21] Wait until Play Mode is stopped and scripts have finished compiling.");
                return;
            }

            Scene scene = SceneManager.GetActiveScene();
            if (scene.name != GameplaySceneName)
            {
                scene = EditorSceneManager.OpenScene(GameplayScenePath, OpenSceneMode.Single);
            }

            GameplayEntryPoint entryPoint = Object.FindFirstObjectByType<GameplayEntryPoint>(FindObjectsInactive.Include);
            if (entryPoint == null)
            {
                Debug.LogError("[Setup Task 21] GameplayEntryPoint not found in Gameplay scene.");
                return;
            }

            var serializedEntry = new SerializedObject(entryPoint);
            var balanceProp = serializedEntry.FindProperty("_balanceView");
            BalanceView balance = balanceProp != null ? balanceProp.objectReferenceValue as BalanceView : null;

            Transform hud = FindScreenHud(scene, balance);
            if (hud == null)
            {
                Debug.LogError("[Setup Task 21] ScreenHud canvas not found in Gameplay scene.");
                return;
            }

            var problems = new List<string>();

            LevelUpPopupView levelUpView = EnsureLevelUpPopup(hud, problems);
            Location2WelcomePopupView welcomeView = EnsureLocation2WelcomePopup(hud, problems);
            DebugCheatView cheatView = EnsureDebugCheatPanel(hud, problems);

            SavePrefabs(levelUpView.gameObject, welcomeView.gameObject, cheatView.gameObject);

            serializedEntry.FindProperty("_levelUpPopup").objectReferenceValue = levelUpView;
            serializedEntry.FindProperty("_loc2WelcomePopup").objectReferenceValue = welcomeView;
            serializedEntry.FindProperty("_debugCheatView").objectReferenceValue = cheatView;
            serializedEntry.ApplyModifiedPropertiesWithoutUndo();

            FixWashBaysFoam();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            Debug.Log("[Setup Task 21] Successfully created Popups and Debug Cheats in Gameplay scene!");
        }

        private static void SavePrefabs(GameObject levelUpGo, GameObject welcomeGo, GameObject cheatGo)
        {
            const string prefabsFolder = "Assets/_Project/Prefabs/UI";
            const string resourcesFolder = "Assets/_Project/Resources/UI";

            PrefabUtility.SaveAsPrefabAsset(levelUpGo, prefabsFolder + "/LevelUpPopup.prefab");
            PrefabUtility.SaveAsPrefabAsset(levelUpGo, resourcesFolder + "/LevelUpPopup.prefab");

            PrefabUtility.SaveAsPrefabAsset(welcomeGo, prefabsFolder + "/Location2WelcomePopup.prefab");
            PrefabUtility.SaveAsPrefabAsset(welcomeGo, resourcesFolder + "/Location2WelcomePopup.prefab");

            PrefabUtility.SaveAsPrefabAsset(cheatGo, prefabsFolder + "/DebugCheatPanel.prefab");
            PrefabUtility.SaveAsPrefabAsset(cheatGo, resourcesFolder + "/DebugCheatPanel.prefab");
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

            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                if (roots[i].name == ScreenHudName && roots[i].TryGetComponent(out Canvas c))
                {
                    return c.transform;
                }
            }

            Canvas anyCanvas = Object.FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
            return anyCanvas != null ? anyCanvas.rootCanvas.transform : null;
        }

        private static LevelUpPopupView EnsureLevelUpPopup(Transform hud, List<string> problems)
        {
            const string PopupName = "LevelUpPopup";
            Transform existing = hud.Find(PopupName);

            GameObject root;
            if (existing != null)
            {
                root = existing.gameObject;
            }
            else
            {
                RectTransform rect = SetupUi.CreateRect(PopupName, hud);
                SetupUi.Stretch(rect);
                root = rect.gameObject;
            }

            SetupUi.MarkGenerated(root);

            // Overlay
            RectTransform overlay = SetupUi.EnsureRect(root.transform, "Overlay", problems, r =>
            {
                SetupUi.Stretch(r);
                Image img = r.gameObject.AddComponent<Image>();
                img.color = DimColor;
                img.raycastTarget = true;
            });

            // Card
            Vector2 cardSize = new Vector2(460f, 340f);
            RectTransform card = SetupUi.EnsureRect(root.transform, "Card", problems, r =>
            {
                SetupUi.Place(r, new Vector2(0.5f, 0.5f), Vector2.zero, cardSize, new Vector2(0.5f, 0.5f));
                Image img = r.gameObject.AddComponent<Image>();
                img.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(DefaultCardSpritePath);
                img.type = Image.Type.Sliced;
                img.color = CardColor;
            });

            // Star Icon
            Image starIcon = SetupUi.EnsureChild(card, "StarIcon", problems, p =>
            {
                RectTransform r = SetupUi.CreateRect("StarIcon", p);
                SetupUi.Place(r, new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(56f, 56f), new Vector2(0.5f, 0.5f));
                Image img = r.gameObject.AddComponent<Image>();
                img.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(StarSpritePath);
                img.preserveAspect = true;
                return img;
            });

            // Title
            TMP_Text title = SetupUi.EnsureChild(card, "Title", problems, p =>
            {
                TMP_Text t = SetupUi.CreateText(p, "Title", "НОВЫЙ УРОВЕНЬ!", 32f, true, TextAlignmentOptions.Center);
                SetupUi.Place(t.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -85f), new Vector2(400f, 40f), new Vector2(0.5f, 0.5f));
                t.color = DarkTitleColor;
                return t;
            });

            // Level Badge
            TMP_Text levelBadge = SetupUi.EnsureChild(card, "LevelBadge", problems, p =>
            {
                TMP_Text t = SetupUi.CreateText(p, "LevelBadge", "УРОВЕНЬ 2", 24f, true, TextAlignmentOptions.Center);
                SetupUi.Place(t.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -125f), new Vector2(300f, 32f), new Vector2(0.5f, 0.5f));
                t.color = LevelBadgeColor;
                return t;
            });

            // Description
            TMP_Text description = SetupUi.EnsureChild(card, "Description", problems, p =>
            {
                TMP_Text t = SetupUi.CreateText(p, "Description", "Разблокированы новые возможности!", 18f, false, TextAlignmentOptions.Center, wrap: true);
                SetupUi.Place(t.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -185f), new Vector2(400f, 80f), new Vector2(0.5f, 0.5f));
                t.color = DescriptionTextColor;
                return t;
            });

            // Continue Button
            Button continueButton = SetupUi.EnsureChild(card, "ContinueButton", problems, p =>
            {
                Button btn = SetupUi.CreateButton(p, "ContinueButton", "ПРОДОЛЖИТЬ", Color.white, new Vector2(240f, 54f));
                SetupUi.Place((RectTransform)btn.transform, new Vector2(0.5f, 0f), new Vector2(0f, 45f), new Vector2(240f, 54f), new Vector2(0.5f, 0.5f));
                Image btnImg = btn.GetComponent<Image>();
                btnImg.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(GreenButtonSpritePath);
                btnImg.type = Image.Type.Sliced;
                return btn;
            });

            LevelUpPopupView view = SetupUi.GetOrAdd<LevelUpPopupView>(root);
            var so = new SerializedObject(view);
            so.FindProperty("_overlay").objectReferenceValue = overlay != null ? overlay.gameObject : null;
            so.FindProperty("_card").objectReferenceValue = card;
            so.FindProperty("_starIcon").objectReferenceValue = starIcon;
            so.FindProperty("_titleText").objectReferenceValue = title;
            so.FindProperty("_levelBadgeText").objectReferenceValue = levelBadge;
            so.FindProperty("_descriptionText").objectReferenceValue = description;
            so.FindProperty("_continueButton").objectReferenceValue = continueButton;
            so.ApplyModifiedPropertiesWithoutUndo();

            root.SetActive(false);
            return view;
        }

        private static Location2WelcomePopupView EnsureLocation2WelcomePopup(Transform hud, List<string> problems)
        {
            const string PopupName = "Location2WelcomePopup";
            Transform existing = hud.Find(PopupName);

            GameObject root;
            if (existing != null)
            {
                root = existing.gameObject;
            }
            else
            {
                RectTransform rect = SetupUi.CreateRect(PopupName, hud);
                SetupUi.Stretch(rect);
                root = rect.gameObject;
            }

            SetupUi.MarkGenerated(root);

            // Overlay
            RectTransform overlay = SetupUi.EnsureRect(root.transform, "Overlay", problems, r =>
            {
                SetupUi.Stretch(r);
                Image img = r.gameObject.AddComponent<Image>();
                img.color = DimColor;
                img.raycastTarget = true;
            });

            // Card
            Vector2 cardSize = new Vector2(520f, 360f);
            RectTransform card = SetupUi.EnsureRect(root.transform, "Card", problems, r =>
            {
                SetupUi.Place(r, new Vector2(0.5f, 0.5f), Vector2.zero, cardSize, new Vector2(0.5f, 0.5f));
                Image img = r.gameObject.AddComponent<Image>();
                img.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(DefaultCardSpritePath);
                img.type = Image.Type.Sliced;
                img.color = CardColor;
            });

            // Title
            TMP_Text title = SetupUi.EnsureChild(card, "Title", problems, p =>
            {
                TMP_Text t = SetupUi.CreateText(p, "Title", Location2WelcomePopupView.DefaultTitle, 30f, true, TextAlignmentOptions.Center);
                SetupUi.Place(t.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -50f), new Vector2(480f, 44f), new Vector2(0.5f, 0.5f));
                t.color = DarkTitleColor;
                return t;
            });

            // Description
            TMP_Text description = SetupUi.EnsureChild(card, "Description", problems, p =>
            {
                TMP_Text t = SetupUi.CreateText(p, "Description", Location2WelcomePopupView.DefaultDescription, 18f, false, TextAlignmentOptions.Center, wrap: true);
                SetupUi.Place(t.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -145f), new Vector2(460f, 130f), new Vector2(0.5f, 0.5f));
                t.color = DescriptionTextColor;
                return t;
            });

            // Start Button
            Button startButton = SetupUi.EnsureChild(card, "StartButton", problems, p =>
            {
                Button btn = SetupUi.CreateButton(p, "StartButton", "НАЧАТЬ РАБОТУ!", Color.white, new Vector2(250f, 54f));
                SetupUi.Place((RectTransform)btn.transform, new Vector2(0.5f, 0f), new Vector2(0f, 45f), new Vector2(250f, 54f), new Vector2(0.5f, 0.5f));
                Image btnImg = btn.GetComponent<Image>();
                btnImg.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(GreenButtonSpritePath);
                btnImg.type = Image.Type.Sliced;
                return btn;
            });

            Location2WelcomePopupView view = SetupUi.GetOrAdd<Location2WelcomePopupView>(root);
            var so = new SerializedObject(view);
            so.FindProperty("_overlay").objectReferenceValue = overlay != null ? overlay.gameObject : null;
            so.FindProperty("_card").objectReferenceValue = card;
            so.FindProperty("_titleText").objectReferenceValue = title;
            so.FindProperty("_descriptionText").objectReferenceValue = description;
            so.FindProperty("_startButton").objectReferenceValue = startButton;
            so.ApplyModifiedPropertiesWithoutUndo();

            root.SetActive(false);
            return view;
        }

        private static DebugCheatView EnsureDebugCheatPanel(Transform hud, List<string> problems)
        {
            const string DebugRootName = "DebugCheatPanel";
            Transform existing = hud.Find(DebugRootName);

            GameObject root;
            if (existing != null)
            {
                root = existing.gameObject;
            }
            else
            {
                RectTransform rect = SetupUi.CreateRect(DebugRootName, hud);
                SetupUi.Stretch(rect);
                root = rect.gameObject;
            }

            SetupUi.MarkGenerated(root);

            // Toggle Button in top right
            Button toggleButton = SetupUi.EnsureChild(root.transform, "ToggleButton", problems, p =>
            {
                Button btn = SetupUi.CreateButton(p, "ToggleButton", "F1", Color.white, new Vector2(56f, 56f));
                SetupUi.Place((RectTransform)btn.transform, new Vector2(1f, 1f), new Vector2(-24f, -24f), new Vector2(56f, 56f), new Vector2(1f, 1f));
                Image img = btn.GetComponent<Image>();
                img.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SquareButtonSpritePath);
                img.type = Image.Type.Sliced;
                return btn;
            });
            if (toggleButton != null)
            {
                TMP_Text t = toggleButton.GetComponentInChildren<TMP_Text>();
                if (t != null)
                {
                    t.text = "F1";
                    t.fontSize = 18f;
                }
            }

            // Panel Root
            Vector2 panelSize = new Vector2(380f, 480f);
            RectTransform panelRoot = SetupUi.EnsureRect(root.transform, "PanelRoot", problems, r =>
            {
                SetupUi.Place(r, new Vector2(1f, 1f), new Vector2(-24f, -90f), panelSize, new Vector2(1f, 1f));
                Image img = r.gameObject.AddComponent<Image>();
                img.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(DefaultCardSpritePath);
                img.type = Image.Type.Sliced;
                img.color = CheatPanelColor;
            });
            panelRoot.sizeDelta = panelSize;

            // Panel Header Title
            SetupUi.EnsureChild(panelRoot, "HeaderTitle", problems, p =>
            {
                TMP_Text t = SetupUi.CreateText(p, "HeaderTitle", "ДЕБАГ-ПАНЕЛЬ (F1)", 22f, true, TextAlignmentOptions.Left);
                SetupUi.Place(t.rectTransform, new Vector2(0f, 1f), new Vector2(20f, -25f), new Vector2(280f, 30f), new Vector2(0f, 1f));
                return t;
            });

            // Close Button
            Button closeButton = SetupUi.EnsureChild(panelRoot, "CloseButton", problems, p =>
            {
                Button btn = SetupUi.CreateButton(p, "CloseButton", "✕", Color.white, new Vector2(36f, 36f));
                SetupUi.Place((RectTransform)btn.transform, new Vector2(1f, 1f), new Vector2(-12f, -12f), new Vector2(36f, 36f), new Vector2(1f, 1f));
                Image img = btn.GetComponent<Image>();
                img.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SquareButtonSpritePath);
                img.type = Image.Type.Sliced;
                return btn;
            });

            // Button stack
            Vector2 actionBtnSize = new Vector2(340f, 48f);
            float startY = -75f;
            float stepY = -56f;

            Button add1000 = CreateCheatButton(panelRoot, "Add1000Btn", "+ $1,000", GreenButtonSpritePath, new Vector2(0f, startY), actionBtnSize, problems);
            Button add10000 = CreateCheatButton(panelRoot, "Add10000Btn", "+ $10,000", GreenButtonSpritePath, new Vector2(0f, startY + stepY), actionBtnSize, problems);
            Button levelUp = CreateCheatButton(panelRoot, "LevelUpBtn", "+ 1 Уровень (Level Up)", BlueButtonSpritePath, new Vector2(0f, startY + stepY * 2), actionBtnSize, problems);
            Button teleportLoc1 = CreateCheatButton(panelRoot, "TeleportLoc1Btn", "Телепорт: Локация 1", GreyButtonSpritePath, new Vector2(0f, startY + stepY * 3), actionBtnSize, problems);
            Button teleportLoc2 = CreateCheatButton(panelRoot, "TeleportLoc2Btn", "Телепорт: Локация 2", GreyButtonSpritePath, new Vector2(0f, startY + stepY * 4), actionBtnSize, problems);
            Button resetProgress = CreateCheatButton(panelRoot, "ResetProgressBtn", "Сброс прогресса", RedButtonSpritePath, new Vector2(0f, startY + stepY * 5), actionBtnSize, problems);

            DebugCheatView view = SetupUi.GetOrAdd<DebugCheatView>(root);
            var so = new SerializedObject(view);
            so.FindProperty("_panelRoot").objectReferenceValue = panelRoot.gameObject;
            so.FindProperty("_toggleButton").objectReferenceValue = toggleButton;
            so.FindProperty("_closeButton").objectReferenceValue = closeButton;
            so.FindProperty("_add1000Button").objectReferenceValue = add1000;
            so.FindProperty("_add10000Button").objectReferenceValue = add10000;
            so.FindProperty("_levelUpButton").objectReferenceValue = levelUp;
            so.FindProperty("_teleportLoc1Button").objectReferenceValue = teleportLoc1;
            so.FindProperty("_teleportLoc2Button").objectReferenceValue = teleportLoc2;
            so.FindProperty("_resetProgressButton").objectReferenceValue = resetProgress;
            so.ApplyModifiedPropertiesWithoutUndo();

            panelRoot.gameObject.SetActive(false);
            return view;
        }

        private static Button CreateCheatButton(
            Transform parent, string name, string label, string spritePath, Vector2 pos, Vector2 size, List<string> problems)
        {
            Button btn = SetupUi.EnsureChild(parent, name, problems, p =>
            {
                Button b = SetupUi.CreateButton(p, name, label, Color.white, size);
                SetupUi.Place((RectTransform)b.transform, new Vector2(0.5f, 1f), pos, size, new Vector2(0.5f, 1f));
                Image img = b.GetComponent<Image>();
                img.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
                img.type = Image.Type.Sliced;
                return b;
            });

            if (btn != null)
            {
                RectTransform r = (RectTransform)btn.transform;
                SetupUi.Place(r, new Vector2(0.5f, 1f), pos, size, new Vector2(0.5f, 1f));
                TMP_Text t = btn.GetComponentInChildren<TMP_Text>();
                if (t != null)
                {
                    t.text = label;
                    t.fontSize = 16f;
                    t.enableAutoSizing = true;
                    t.fontSizeMin = 12f;
                    t.fontSizeMax = 18f;
                    t.margin = new Vector4(8f, 2f, 8f, 2f);
                    t.textWrappingMode = TextWrappingModes.NoWrap;
                    t.overflowMode = TextOverflowModes.Ellipsis;
                }
            }

            return btn;
        }

        private static void FixWashBaysFoam()
        {
            var washFxs = Object.FindObjectsByType<WashFx>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var foamMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/M_WashFoam.mat");
            if (foamMat == null) return;

            foreach (var washFx in washFxs)
            {
                var so = new SerializedObject(washFx);
                var matProp = so.FindProperty("_foamMaterial");
                if (matProp != null)
                {
                    matProp.objectReferenceValue = foamMat;
                }

                var foamProp = so.FindProperty("_foam");
                if (foamProp != null && foamProp.objectReferenceValue is ParticleSystem ps)
                {
                    var psr = ps.GetComponent<ParticleSystemRenderer>();
                    if (psr != null)
                    {
                        psr.sharedMaterial = foamMat;
                    }
                }
                else
                {
                    var childPs = washFx.GetComponentInChildren<ParticleSystem>(true);
                    if (childPs != null)
                    {
                        var psr = childPs.GetComponent<ParticleSystemRenderer>();
                        if (psr != null)
                        {
                            psr.sharedMaterial = foamMat;
                        }
                        if (foamProp != null)
                        {
                            foamProp.objectReferenceValue = childPs;
                        }
                    }
                }
                so.ApplyModifiedProperties();
            }
        }
    }
}
