using System;
using System.Collections.Generic;
using AutoService.Infrastructure.Config;
using AutoService.Presentation.Panels;
using AutoService.Presentation.Player;
using AutoService.Presentation.Points;
using AutoService.Presentation.Points.Panel;
using AutoService.Presentation.Staff;
using AutoService.Presentation.Supplies;
using AutoService.Presentation.Traffic;
using TMPro;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace AutoService.Bootstrap.Editor
{
    /// <summary>
    /// One-click Editor setup of module A2 (staff, supplies, upgrades): configs, layout (pads, warehouse, staff room,
    /// K4 obstacles), the staff prefab, visual catalogs, the player's carry socket, the point and storekeeper panels,
    /// the supply indicators on every point HUD, every new reference on the entry point and the layout, NavMesh bake,
    /// layout validation and saving. Safe to re-run: everything it creates is found by name and updated, or replaced.
    /// </summary>
    /// <remarks>Editor-only, so scene searches (<c>FindObjectsByType</c>) are fine here (CLAUDE.md rule 3 exception).</remarks>
    internal static class ModuleSetupA2
    {
        private const string Tag = "[A2 Setup] ";
        private const string GameplayScenePath = "Assets/_Project/Scenes/Gameplay.unity";
        private const string PrefabsFolder = "Assets/_Project/Prefabs";
        private const string StaffPrefabPath = PrefabsFolder + "/Staff.prefab";
        private const string ConfigsFolder = "Assets/_Project/Configs";
        private const string StaffVisualsPath = ConfigsFolder + "/StaffVisualCatalog.asset";
        private const string SupplyVisualsPath = ConfigsFolder + "/SupplyVisualCatalog.asset";
        private const string WorkerMaterialPath = "Assets/_Project/Materials/M_StaffWorker.mat";
        private const string StorekeeperMaterialPath = "Assets/_Project/Materials/M_Storekeeper.mat";
        private const string StaffRootName = "[Staff]";
        private const string CarrySocketName = "CarrySocket";
        private const string BoxName = "Box";
        private const string PointPanelName = "PointPanel";
        private const string StorekeeperPanelName = "StorekeeperPanel";
        private const string PanelChildName = "Panel";
        private const string SupplyLabelName = "SupplyLabel";
        private const string NoSupplyIconName = "NoSupplyIcon";
        private const string UiSpritePath = "UI/Skin/UISprite.psd";

        // Staff body (prompt 05 §8): capsule 1.8 m, NavMeshAgent humanoid, speed 3.5, box socket at (0, 1.1, 0.5).
        private const float StaffHeight = 1.8f;
        private const float StaffRadius = 0.4f;
        private const float StaffSpeed = 3.5f;
        private static readonly Vector3 StaffCarrySocket = new Vector3(0f, 1.1f, 0.5f);
        private static readonly Vector3 PlayerCarrySocket = new Vector3(0f, 0.1f, 0.6f);
        private const float BoxSize = 0.5f;
        private static readonly Color WorkerColor = new Color(0.2f, 0.45f, 0.95f, 1f);
        private static readonly Color StorekeeperColor = new Color(1f, 0.55f, 0.1f, 1f);

        private static readonly SupplyColor[] SupplyColors =
        {
            new SupplyColor("shampoo", new Color(0.45f, 0.8f, 1f, 1f)),
            new SupplyColor("oil", new Color(0.75f, 0.6f, 0.1f, 1f)),
            new SupplyColor("tires", new Color(0.2f, 0.2f, 0.22f, 1f)),
        };

        // Panel style (prompt 05 §9, canvas 1920×1080).
        private static readonly Color PanelBackground = new Color32(0x1E, 0x24, 0x30, 235);
        private static readonly Color TextColor = new Color32(0xC8, 0xCE, 0xD8, 0xFF);
        private static readonly Color PriceColor = new Color32(0xFF, 0xD5, 0x4F, 0xFF);
        private static readonly Color ButtonColor = new Color32(0x43, 0xA0, 0x47, 0xFF);
        private static readonly Color ButtonDisabledColor = new Color32(0x5A, 0x5F, 0x66, 0xFF);
        private static readonly Vector2 PointPanelSize = new Vector2(380f, 300f);
        private const float Inset = 12f;
        private const float TitleFont = 26f;
        private const float TextFont = 18f;
        private const float PriceFont = 22f;

        // Point HUD (world canvas 200×100).
        private static readonly Vector2 SupplyLabelPosition = new Vector2(0f, -60f);
        private static readonly Vector2 SupplyLabelSize = new Vector2(200f, 50f);
        private const float SupplyLabelFont = 44f;
        private static readonly Vector2 NoSupplyIconSize = new Vector2(80f, 80f);
        private static readonly Color NoSupplyColor = new Color(0.8f, 0.55f, 0.2f, 1f);

        [MenuItem("AutoService/Setup/Run A2 Setup")]
        private static void Run()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError(Tag + "Stop Play Mode first.");
                return;
            }

            if (!EnsureGameplaySceneOpen())
            {
                return;
            }

            var problems = new List<string>();
            if (!Location1ConfigCreator.Run())
            {
                problems.Add("configs (see the [Whitebox] error above)");
            }

            if (!WhiteboxLocationBuilder.Run())
            {
                problems.Add("location layout (see the [Whitebox] errors above)");
            }

            LocationLayout layout = WhiteboxLocationBuilder.FindLayout();
            GameplayEntryPoint entryPoint = FindInScene<GameplayEntryPoint>();
            if (layout == null || entryPoint == null)
            {
                Debug.LogError(Tag + "The Gameplay scene needs a LocationLayout ('Location_1') and a GameplayEntryPoint; nothing else was set up.");
                return;
            }

            EnsureFolder("Assets/_Project", "Prefabs");
            Material workerMaterial = WhiteboxLocationBuilder.GetOrCreateColorMaterial(WorkerMaterialPath, "M_StaffWorker", WorkerColor);
            Material storekeeperMaterial = WhiteboxLocationBuilder.GetOrCreateColorMaterial(StorekeeperMaterialPath, "M_Storekeeper", StorekeeperColor);
            StaffView staffPrefab = CreateStaffPrefab(workerMaterial);
            StaffVisualCatalog staffVisuals = CreateStaffVisuals(workerMaterial, storekeeperMaterial);
            SupplyVisualCatalog supplyVisuals = CreateSupplyVisuals();

            var entry = new SerializedObject(entryPoint);
            PlayerCarryView carry = SetupPlayerCarry(entry.FindProperty("_player").objectReferenceValue as PlayerView, problems);
            var buildPanel = entry.FindProperty("_buildPanel").objectReferenceValue as OfferPanelView;
            PointPanelView pointPanel = null;
            OfferPanelView storekeeperPanel = null;
            if (buildPanel != null)
            {
                RectTransform canvas = (RectTransform)buildPanel.transform.parent;
                EnsureAnchoredPanel(buildPanel);
                storekeeperPanel = SetupStorekeeperPanel(buildPanel, canvas);
                pointPanel = BuildPointPanel(canvas);
            }
            else
            {
                problems.Add("_buildPanel is not assigned on the entry point (the panels are created next to it)");
            }

            int huds = SetupPointHuds(layout);

            entry.FindProperty("_pointPanel").objectReferenceValue = pointPanel;
            entry.FindProperty("_storekeeperPanel").objectReferenceValue = storekeeperPanel;
            entry.FindProperty("_staffPrefab").objectReferenceValue = staffPrefab;
            entry.FindProperty("_staffVisuals").objectReferenceValue = staffVisuals;
            entry.FindProperty("_supplyVisuals").objectReferenceValue = supplyVisuals;
            entry.FindProperty("_staffRoot").objectReferenceValue = GetOrCreateRoot(StaffRootName, entryPoint.gameObject.scene);
            entry.FindProperty("_playerCarry").objectReferenceValue = carry;
            entry.ApplyModifiedProperties();

            int surfaces = BakeNavMeshes(problems);
            string layoutProblem = ValidateLayout(layout);
            if (layoutProblem != null)
            {
                problems.Add("layout validation: " + layoutProblem);
            }

            Scene scene = entryPoint.gameObject.scene;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            string summary = "staff prefab, 2 catalogs, player carry, point + storekeeper panels, " + huds + " point HUDs, "
                + surfaces + " NavMesh surface(s) baked, scene saved";
            if (problems.Count == 0)
            {
                Debug.Log(Tag + "Done: " + summary + ". Press Play.");
            }
            else
            {
                Debug.LogError(Tag + "Finished with " + problems.Count + " problem(s): " + string.Join("; ", problems) + ". (" + summary + ")");
            }
        }

        // ── Scene ────────────────────────────────────────────────────────────────────────────────────────────────────

        private static bool EnsureGameplaySceneOpen()
        {
            if (SceneManager.GetActiveScene().path == GameplayScenePath)
            {
                return true;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.LogWarning(Tag + "Cancelled: the open scene was not saved.");
                return false;
            }

            EditorSceneManager.OpenScene(GameplayScenePath, OpenSceneMode.Single);
            return true;
        }

        private static T FindInScene<T>() where T : Object
        {
            T[] found = Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            return found.Length > 0 ? found[0] : null;
        }

        private static Transform GetOrCreateRoot(string name, Scene scene)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                if (roots[i].name == name)
                {
                    return roots[i].transform;
                }
            }

            var root = new GameObject(name);
            SceneManager.MoveGameObjectToScene(root, scene);
            return root.transform;
        }

        // ── Staff prefab and catalogs ────────────────────────────────────────────────────────────────────────────────

        private static StaffView CreateStaffPrefab(Material bodyMaterial)
        {
            var root = new GameObject("Staff");
            try
            {
                var agent = root.AddComponent<NavMeshAgent>();
                agent.agentTypeID = 0;
                agent.speed = StaffSpeed;
                agent.radius = StaffRadius;
                agent.height = StaffHeight;
                agent.angularSpeed = 720f;
                agent.acceleration = 16f;
                agent.stoppingDistance = 0.1f;
                agent.baseOffset = 0f;

                // Why: a 2 m capsule scaled to a 1.5 m body plus a head on top makes 1.8 m.
                Renderer body = CreateVisual(PrimitiveType.Capsule, "Body", root.transform,
                    new Vector3(0f, 0.75f, 0f), new Vector3(StaffRadius * 2f, 0.75f, StaffRadius * 2f));
                body.sharedMaterial = bodyMaterial;
                CreateVisual(PrimitiveType.Sphere, "Head", root.transform, new Vector3(0f, 1.575f, 0f), Vector3.one * 0.45f);

                Transform socket = new GameObject(CarrySocketName).transform;
                socket.SetParent(root.transform, false);
                socket.localPosition = StaffCarrySocket;
                Renderer box = CreateVisual(PrimitiveType.Cube, BoxName, socket, Vector3.zero, Vector3.one * BoxSize);

                var view = root.AddComponent<StaffView>();
                var serialized = new SerializedObject(view);
                serialized.FindProperty("_agent").objectReferenceValue = agent;
                serialized.FindProperty("_body").objectReferenceValue = body;
                serialized.FindProperty("_carrySocket").objectReferenceValue = socket;
                serialized.FindProperty("_boxRenderer").objectReferenceValue = box;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                // Why: SaveAsPrefabAsset overwrites an existing prefab in place, so its GUID (and every reference) is kept.
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, StaffPrefabPath);
                return prefab != null ? prefab.GetComponent<StaffView>() : null;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        // Why: render-only parts — a collider on the NPC would catch clicks and block the player's NavMesh agent.
        private static Renderer CreateVisual(PrimitiveType type, string name, Transform parent, Vector3 localPosition, Vector3 localScale)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            part.name = name;
            Object.DestroyImmediate(part.GetComponent<Collider>());
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = localScale;
            return part.GetComponent<Renderer>();
        }

        private static StaffVisualCatalog CreateStaffVisuals(Material worker, Material storekeeper)
        {
            StaffVisualCatalog catalog = GetOrCreateAsset<StaffVisualCatalog>(StaffVisualsPath);
            var serialized = new SerializedObject(catalog);
            SetIfEmpty(serialized.FindProperty("_pointWorker"), worker);
            SetIfEmpty(serialized.FindProperty("_storekeeper"), storekeeper);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return catalog;
        }

        private static SupplyVisualCatalog CreateSupplyVisuals()
        {
            SupplyVisualCatalog catalog = GetOrCreateAsset<SupplyVisualCatalog>(SupplyVisualsPath);
            var serialized = new SerializedObject(catalog);
            SerializedProperty entries = serialized.FindProperty("_entries");
            for (int i = 0; i < SupplyColors.Length; i++)
            {
                bool present = false;
                for (int e = 0; e < entries.arraySize && !present; e++)
                {
                    present = entries.GetArrayElementAtIndex(e).FindPropertyRelative("_supplyTypeId").stringValue == SupplyColors[i].Id;
                }

                // Why: only missing ids are added, so colors tuned in the inspector survive a re-run.
                if (present)
                {
                    continue;
                }

                entries.arraySize++;
                SerializedProperty entry = entries.GetArrayElementAtIndex(entries.arraySize - 1);
                entry.FindPropertyRelative("_supplyTypeId").stringValue = SupplyColors[i].Id;
                entry.FindPropertyRelative("_color").colorValue = SupplyColors[i].Color;
                entry.FindPropertyRelative("_icon").objectReferenceValue = null;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            return catalog;
        }

        private static T GetOrCreateAsset<T>(string path) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null)
            {
                return existing;
            }

            var asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static void SetIfEmpty(SerializedProperty property, Object value)
        {
            if (property.objectReferenceValue == null)
            {
                property.objectReferenceValue = value;
            }
        }

        private static void EnsureFolder(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder(parent + "/" + name))
            {
                AssetDatabase.CreateFolder(parent, name);
            }
        }

        // ── Player ───────────────────────────────────────────────────────────────────────────────────────────────────

        private static PlayerCarryView SetupPlayerCarry(PlayerView player, List<string> problems)
        {
            if (player == null)
            {
                problems.Add("_player is not assigned on the entry point (no carry socket)");
                return null;
            }

            Transform socket = player.transform.Find(CarrySocketName);
            if (socket == null)
            {
                socket = new GameObject(CarrySocketName).transform;
                socket.SetParent(player.transform, false);
                Undo.RegisterCreatedObjectUndo(socket.gameObject, "Create carry socket");
            }

            socket.localPosition = PlayerCarrySocket;
            socket.localRotation = Quaternion.identity;
            Transform boxTransform = socket.Find(BoxName);
            Renderer box = boxTransform != null
                ? boxTransform.GetComponent<Renderer>()
                : CreateVisual(PrimitiveType.Cube, BoxName, socket, Vector3.zero, Vector3.one * BoxSize);

            PlayerCarryView carry = player.TryGetComponent(out PlayerCarryView existing) ? existing : Undo.AddComponent<PlayerCarryView>(player.gameObject);
            var serialized = new SerializedObject(carry);
            serialized.FindProperty("_boxSocket").objectReferenceValue = socket;
            serialized.FindProperty("_boxRenderer").objectReferenceValue = box;
            serialized.ApplyModifiedProperties();
            return carry;
        }

        // ── Panels ───────────────────────────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// The build panel's positioning moved from <c>BuildPanelView</c> into <see cref="ScreenAnchoredPanel"/>: adds the
        /// component on the panel's container and points it at the moved child.
        /// </summary>
        private static void EnsureAnchoredPanel(OfferPanelView view)
        {
            ScreenAnchoredPanel anchor = view.TryGetComponent(out ScreenAnchoredPanel existing)
                ? existing
                : Undo.AddComponent<ScreenAnchoredPanel>(view.gameObject);
            var anchorObject = new SerializedObject(anchor);
            SerializedProperty root = anchorObject.FindProperty("_root");
            if (root.objectReferenceValue == null)
            {
                Transform child = view.transform.Find(PanelChildName);
                root.objectReferenceValue = child != null ? child : view.transform.childCount > 0 ? view.transform.GetChild(0) : null;
            }

            anchorObject.ApplyModifiedProperties();

            var viewObject = new SerializedObject(view);
            viewObject.FindProperty("_anchor").objectReferenceValue = anchor;
            viewObject.ApplyModifiedProperties();
        }

        // Why: the storekeeper offer is the build panel's layout with other labels, so it is a copy of that panel.
        private static OfferPanelView SetupStorekeeperPanel(OfferPanelView buildPanel, RectTransform canvas)
        {
            Transform existing = canvas.Find(StorekeeperPanelName);
            OfferPanelView panel = existing != null ? existing.GetComponent<OfferPanelView>() : null;
            if (panel == null)
            {
                GameObject copy = Object.Instantiate(buildPanel.gameObject, canvas, false);
                copy.name = StorekeeperPanelName;
                Undo.RegisterCreatedObjectUndo(copy, "Create storekeeper panel");
                panel = copy.GetComponent<OfferPanelView>();
            }

            EnsureAnchoredPanel(panel);
            var serialized = new SerializedObject(panel);
            serialized.FindProperty("_actionLabelFormat").stringValue = "Hire {0}";
            serialized.FindProperty("_completedLabel").stringValue = "Hired";
            serialized.ApplyModifiedProperties();
            return panel;
        }

        /// <summary>Builds the point panel from scratch (layout of prompt 05 §9); a previous one is replaced.</summary>
        private static PointPanelView BuildPointPanel(RectTransform canvas)
        {
            Transform previous = canvas.Find(PointPanelName);
            if (previous != null)
            {
                Undo.DestroyObjectImmediate(previous.gameObject);
            }

            RectTransform container = CreateRect(PointPanelName, canvas);
            Stretch(container);
            var anchor = container.gameObject.AddComponent<ScreenAnchoredPanel>();
            var view = container.gameObject.AddComponent<PointPanelView>();

            RectTransform panel = CreateRect(PanelChildName, container);
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.pivot = Vector2.zero;
            panel.sizeDelta = PointPanelSize;
            AddImage(panel, PanelBackground);

            TMP_Text title = CreateLine(panel, "Title", -12f, 34f, TitleFont, Color.white, true);
            TMP_Text status = CreateLine(panel, "Status", -50f, 24f, TextFont, TextColor, false);
            TMP_Text income = CreateLine(panel, "Income", -76f, 24f, PriceFont, PriceColor, true);
            UpgradeRowView speed = CreateUpgradeRow(panel, "UpgradeSpeed", -108f);
            UpgradeRowView price = CreateUpgradeRow(panel, "UpgradePrice", -166f);
            HireRowView hire = CreateHireRow(panel);
            Button close = CreateCloseButton(panel);

            var anchorObject = new SerializedObject(anchor);
            anchorObject.FindProperty("_root").objectReferenceValue = panel;
            anchorObject.ApplyModifiedPropertiesWithoutUndo();

            var serialized = new SerializedObject(view);
            serialized.FindProperty("_anchor").objectReferenceValue = anchor;
            serialized.FindProperty("_title").objectReferenceValue = title;
            serialized.FindProperty("_status").objectReferenceValue = status;
            serialized.FindProperty("_income").objectReferenceValue = income;
            serialized.FindProperty("_speedRow").objectReferenceValue = speed;
            serialized.FindProperty("_priceRow").objectReferenceValue = price;
            serialized.FindProperty("_hireRow").objectReferenceValue = hire;
            serialized.FindProperty("_closeButton").objectReferenceValue = close;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            panel.gameObject.SetActive(false);
            Undo.RegisterCreatedObjectUndo(container.gameObject, "Create point panel");
            return view;
        }

        private static UpgradeRowView CreateUpgradeRow(RectTransform panel, string name, float top)
        {
            RectTransform row = CreateRect(name, panel);
            TopBand(row, top, 52f);

            TMP_Text rowName = CreateText(row, "Name", TextFont, Color.white, true, TextAlignmentOptions.Left);
            Place(rowName.rectTransform, new Vector2(0f, 1f), new Vector2(Inset, 0f), new Vector2(110f, 26f), new Vector2(0f, 1f));
            TMP_Text level = CreateText(row, "Level", TextFont, PriceColor, true, TextAlignmentOptions.Left);
            Place(level.rectTransform, new Vector2(0f, 1f), new Vector2(Inset + 110f, 0f), new Vector2(80f, 26f), new Vector2(0f, 1f));
            TMP_Text effect = CreateText(row, "Effect", 16f, TextColor, false, TextAlignmentOptions.Left);
            Place(effect.rectTransform, new Vector2(0f, 1f), new Vector2(Inset, -26f), new Vector2(200f, 24f), new Vector2(0f, 1f));

            Button buy = CreateButton(row, "Buy", out TMP_Text label, TextFont);
            Place((RectTransform)buy.transform, new Vector2(1f, 0.5f), new Vector2(-Inset, 0f), new Vector2(140f, 40f), new Vector2(1f, 0.5f));

            var view = row.gameObject.AddComponent<UpgradeRowView>();
            var serialized = new SerializedObject(view);
            serialized.FindProperty("_name").objectReferenceValue = rowName;
            serialized.FindProperty("_level").objectReferenceValue = level;
            serialized.FindProperty("_effect").objectReferenceValue = effect;
            serialized.FindProperty("_buy").objectReferenceValue = buy;
            serialized.FindProperty("_buyLabel").objectReferenceValue = label;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return view;
        }

        private static HireRowView CreateHireRow(RectTransform panel)
        {
            RectTransform row = CreateRect("Hire", panel);
            row.anchorMin = new Vector2(0f, 0f);
            row.anchorMax = new Vector2(1f, 0f);
            row.pivot = new Vector2(0.5f, 0f);
            row.anchoredPosition = new Vector2(0f, 16f);
            row.sizeDelta = new Vector2(-Inset * 2f, 44f);

            Button hire = CreateButton(row, "HireButton", out TMP_Text label, 20f);
            Stretch((RectTransform)hire.transform);

            var view = row.gameObject.AddComponent<HireRowView>();
            var serialized = new SerializedObject(view);
            serialized.FindProperty("_hire").objectReferenceValue = hire;
            serialized.FindProperty("_hireLabel").objectReferenceValue = label;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return view;
        }

        private static Button CreateCloseButton(RectTransform panel)
        {
            Button close = CreateButton(panel, "CloseButton", out TMP_Text label, 20f);
            Place((RectTransform)close.transform, new Vector2(1f, 1f), new Vector2(-4f, -4f), new Vector2(32f, 32f), new Vector2(1f, 1f));
            label.text = "X";

            // Why: Close is never disabled; it uses the neutral color so it does not read as a purchase.
            ColorBlock colors = close.colors;
            colors.normalColor = ButtonDisabledColor;
            colors.selectedColor = ButtonDisabledColor;
            close.colors = colors;
            return close;
        }

        private static TMP_Text CreateLine(RectTransform panel, string name, float top, float height, float fontSize, Color color, bool bold)
        {
            TMP_Text text = CreateText(panel, name, fontSize, color, bold, TextAlignmentOptions.Center);
            TopBand(text.rectTransform, top, height);
            return text;
        }

        private static TMP_Text CreateText(RectTransform parent, string name, float fontSize, Color color, bool bold, TextAlignmentOptions alignment)
        {
            RectTransform rect = CreateRect(name, parent);
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.text = name;
            text.fontSize = fontSize;
            text.color = color;
            text.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
            text.alignment = alignment;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Ellipsis;
            return text;
        }

        /// <summary>Button whose tint is the whole color: green when enabled, gray when disabled (prompt 05 §9).</summary>
        private static Button CreateButton(RectTransform parent, string name, out TMP_Text label, float fontSize)
        {
            RectTransform rect = CreateRect(name, parent);
            Image image = AddImage(rect, Color.white);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.normalColor = ButtonColor;
            colors.highlightedColor = Color.Lerp(ButtonColor, Color.white, 0.2f);
            colors.pressedColor = Color.Lerp(ButtonColor, Color.black, 0.2f);
            colors.selectedColor = ButtonColor;
            colors.disabledColor = ButtonDisabledColor;
            colors.colorMultiplier = 1f;
            button.colors = colors;

            label = CreateText(rect, "Label", fontSize, Color.white, true, TextAlignmentOptions.Center);
            Stretch(label.rectTransform);
            return button;
        }

        private static Image AddImage(RectTransform rect, Color color)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>(UiSpritePath);
            image.type = Image.Type.Sliced;
            image.color = color;
            return image;
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            var child = new GameObject(name, typeof(RectTransform));
            child.layer = parent.gameObject.layer;
            child.transform.SetParent(parent, false);
            return (RectTransform)child.transform;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
        }

        // A full-width band at a fixed distance from the parent's top, inset on both sides.
        private static void TopBand(RectTransform rect, float top, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, top);
            rect.sizeDelta = new Vector2(-Inset * 2f, height);
        }

        private static void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size, Vector2 pivot)
        {
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        // ── Point HUDs ───────────────────────────────────────────────────────────────────────────────────────────────

        /// <returns>Number of HUDs that have the supply counter and the box icon.</returns>
        private static int SetupPointHuds(LocationLayout layout)
        {
            ServicePointView[] points = layout.GetComponentsInChildren<ServicePointView>(true);
            int count = 0;
            for (int i = 0; i < points.Length; i++)
            {
                ServicePointHud hud = points[i].Hud;
                if (hud == null)
                {
                    continue;
                }

                var rect = (RectTransform)hud.transform;
                Transform label = rect.Find(SupplyLabelName);
                TMP_Text supply = label != null ? label.GetComponent<TMP_Text>() : CreateHudLabel(rect);
                Transform icon = rect.Find(NoSupplyIconName);
                GameObject noSupply = icon != null ? icon.gameObject : CreateNoSupplyIcon(rect);

                var serialized = new SerializedObject(hud);
                serialized.FindProperty("_supplyLabel").objectReferenceValue = supply;
                serialized.FindProperty("_noSupplyIcon").objectReferenceValue = noSupply;
                serialized.ApplyModifiedProperties();
                count++;
            }

            return count;
        }

        private static TMP_Text CreateHudLabel(RectTransform hud)
        {
            TMP_Text text = CreateText(hud, SupplyLabelName, SupplyLabelFont, Color.white, true, TextAlignmentOptions.Center);
            text.text = "10/10";
            Place(text.rectTransform, new Vector2(0.5f, 0.5f), SupplyLabelPosition, SupplyLabelSize, new Vector2(0.5f, 0.5f));
            Undo.RegisterCreatedObjectUndo(text.gameObject, "Create supply label");
            return text;
        }

        // Why: a brown crate with "BOX" on it — in place of the "!" (they are never shown together).
        private static GameObject CreateNoSupplyIcon(RectTransform hud)
        {
            RectTransform icon = CreateRect(NoSupplyIconName, hud);
            Place(icon, new Vector2(0.5f, 0.5f), Vector2.zero, NoSupplyIconSize, new Vector2(0.5f, 0.5f));
            AddImage(icon, NoSupplyColor).raycastTarget = false;
            TMP_Text text = CreateText(icon, "Label", 26f, Color.white, true, TextAlignmentOptions.Center);
            text.text = "BOX";
            Stretch(text.rectTransform);
            icon.gameObject.SetActive(false);
            Undo.RegisterCreatedObjectUndo(icon.gameObject, "Create no-supply icon");
            return icon.gameObject;
        }

        // ── NavMesh and validation ───────────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Bakes every NavMesh surface synchronously and stores the data where the previous bake was (or next to the
        /// scene, like the surface inspector does).
        /// </summary>
        private static int BakeNavMeshes(List<string> problems)
        {
            NavMeshSurface[] surfaces = Object.FindObjectsByType<NavMeshSurface>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            if (surfaces.Length == 0)
            {
                problems.Add("no NavMeshSurface in the scene to bake");
                return 0;
            }

            for (int i = 0; i < surfaces.Length; i++)
            {
                NavMeshSurface surface = surfaces[i];
                string path = surface.navMeshData != null ? AssetDatabase.GetAssetPath(surface.navMeshData) : null;
                surface.BuildNavMesh();
                if (string.IsNullOrEmpty(path))
                {
                    string folder = GameplayScenePath.Substring(0, GameplayScenePath.Length - ".unity".Length);
                    if (!AssetDatabase.IsValidFolder(folder))
                    {
                        AssetDatabase.CreateFolder("Assets/_Project/Scenes", "Gameplay");
                    }

                    path = AssetDatabase.GenerateUniqueAssetPath(folder + "/NavMesh-" + surface.name + ".asset");
                }
                else
                {
                    AssetDatabase.DeleteAsset(path);
                }

                AssetDatabase.CreateAsset(surface.navMeshData, path);
                EditorUtility.SetDirty(surface);
            }

            return surfaces.Length;
        }

        /// <summary>Runs the runtime layout validation on a hidden copy of the location.</summary>
        /// <remarks>
        /// Why a copy: <see cref="LocationLayout.Validate"/> caches the road graph on the instance, and the next run of the
        /// builder replaces the nodes — the scene object would then keep validating against destroyed nodes.
        /// </remarks>
        /// <returns>The first problem, or null.</returns>
        private static string ValidateLayout(LocationLayout layout)
        {
            string[] guids = AssetDatabase.FindAssets("t:" + nameof(GameConfig));
            if (guids.Length == 0)
            {
                return "no GameConfig asset";
            }

            GameObject copy = Object.Instantiate(layout.gameObject);
            copy.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                var config = new ScriptableObjectConfigProvider(AssetDatabase.LoadAssetAtPath<GameConfig>(AssetDatabase.GUIDToAssetPath(guids[0])));
                var validated = copy.GetComponent<LocationLayout>();
                return validated.Validate(out string problem) && validated.ValidateBuildPlots(config, out problem) ? null : problem;
            }
            catch (Exception exception) when (exception is InvalidOperationException || exception is ArgumentException)
            {
                return exception.Message;
            }
            finally
            {
                Object.DestroyImmediate(copy);
            }
        }

        private readonly struct SupplyColor
        {
            public SupplyColor(string id, Color color)
            {
                Id = id;
                Color = color;
            }

            public string Id { get; }

            public Color Color { get; }
        }
    }
}
