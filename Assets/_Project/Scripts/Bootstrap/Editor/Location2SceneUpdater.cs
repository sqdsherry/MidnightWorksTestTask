using System;
using System.Collections.Generic;
using AutoService.Infrastructure.Config;
using AutoService.Presentation.Interaction;
using AutoService.Presentation.Points;
using AutoService.Presentation.Supplies;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AutoService.Bootstrap.Editor
{
    /// <summary>
    /// Non-destructive updater for Location 2, warehouses, travel points, and service type XP.
    /// Preserves user-adjusted coordinates and heights while wiring missing links and UI rings.
    /// </summary>
    public static class Location2SceneUpdater
    {
        private const string GameplayScenePath = "Assets/_Project/Scenes/Gameplay.unity";
        private const string ParkingConfigPath = "Assets/_Project/Configs/ServiceTypes/ST_Parking.asset";
        private const string WorkPadMaterialPath = "Assets/_Project/Materials/M_WorkPad.mat";

        private const string KnobSpritePath = "UI/Skin/Knob.psd";
        private const string ArrowSpritePath = "UI/Skin/DropdownArrow.psd";
        private const string UiSpritePath = "UI/Skin/UISprite.psd";

        private static readonly Color32 PadRingBackground = new Color32(0x1E, 0x24, 0x30, 153);
        private static readonly Color32 PadRingFill = new Color32(0x4F, 0xC3, 0xF7, 0xFF);
        private static readonly Vector2 PadCanvasSize = new Vector2(200f, 200f);

        [InitializeOnLoadMethod]
        private static void AutoSyncWarehouseVisuals()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorPrefs.GetBool("AutoService_WarehouseLoc2Visuals_V3", false)) return;
                EditorPrefs.SetBool("AutoService_WarehouseLoc2Visuals_V3", true);
                ApplyFixes();
            };
        }

        /// <summary>
        /// Applies in-place non-destructive updates to the gameplay scene and configs.
        /// </summary>
        [MenuItem("AutoService/Setup/Apply Location 2 and Warehouse Fixes")]
        public static void ApplyFixes()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != GameplayScenePath)
            {
                scene = EditorSceneManager.OpenScene(GameplayScenePath, OpenSceneMode.Single);
            }

            Quaternion facing = GetCameraFacing();

            UpdateParkingConfig();
            UpdateTravelPoints(facing);
            UpdateManagePads(facing);
            CloneWarehouse1VisualsToLoc2();
            UpdateWarehouses(facing);
            EnsureDoorNavMesh();
            AdjustWorkSpotsAndBakeNavMesh();
            CleanTagCanvases();
            EnsureServiceBayHudNames();
            CharacterSetupHelper.ConfigureAll();
            StripWorldSpaceGraphicRaycasters();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            Debug.Log("[Location2SceneUpdater] Successfully applied Location 2 and Warehouse fixes to " + scene.name + "!");
        }

        [MenuItem("AutoService/Setup/Strip WorldSpace GraphicRaycasters")]
        public static void StripWorldSpaceGraphicRaycasters()
        {
            var canvases = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            int stripped = 0;
            foreach (var canvas in canvases)
            {
                if (canvas.renderMode == RenderMode.WorldSpace)
                {
                    var raycaster = canvas.GetComponent<GraphicRaycaster>();
                    if (raycaster != null)
                    {
                        Undo.DestroyObjectImmediate(raycaster);
                        stripped++;
                    }

                    var graphics = canvas.GetComponentsInChildren<Graphic>(true);
                    foreach (var g in graphics)
                    {
                        if (g.raycastTarget)
                        {
                            g.raycastTarget = false;
                            EditorUtility.SetDirty(g);
                        }
                    }
                    EditorUtility.SetDirty(canvas.gameObject);
                }
            }
            Debug.Log("[Location2SceneUpdater] Stripped GraphicRaycaster from " + stripped + " WorldSpace Canvas(es).");
        }

        private static void CleanTagCanvases()
        {
            var rings = UnityEngine.Object.FindObjectsByType<DwellRingView>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            int cleaned = 0;
            for (int i = 0; i < rings.Length; i++)
            {
                DwellRingView ring = rings[i];
                Transform t = ring.transform;

                Transform bg = t.Find("Background");
                if (bg != null)
                {
                    Undo.DestroyObjectImmediate(bg.gameObject);
                    cleaned++;
                }

                Transform arrow = t.Find("Arrow");
                if (arrow != null)
                {
                    Undo.DestroyObjectImmediate(arrow.gameObject);
                    cleaned++;
                }

                var ringSo = new SerializedObject(ring);
                var bgProp = ringSo.FindProperty("_background");
                if (bgProp != null && bgProp.objectReferenceValue != null)
                {
                    bgProp.objectReferenceValue = null;
                    ringSo.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            Debug.Log("[Location2SceneUpdater] Cleaned Background and Arrow from Tag canvases. Objects removed: " + cleaned);
        }

        private static void EnsureDoorNavMesh()
        {
            GameObject loc2StaffRoom = GameObject.Find("StaffRoom");
            if (loc2StaffRoom != null)
            {
                Transform door = loc2StaffRoom.transform.Find("Door");
                if (door != null)
                {
                    // Ensure door is in front of the house at ground level
                    door.localPosition = new Vector3(0f, 0f, -2.2f);
                    door.localRotation = Quaternion.LookRotation(Vector3.back);
                    EditorUtility.SetDirty(door);
                    Debug.Log("[Location2SceneUpdater] Verified Location 2 StaffRoom Door position.");
                }
            }
        }

        private static void AdjustWorkSpotsAndBakeNavMesh()
        {
            // Ensure WorkSpots on ServicePointViews are offset from pillars
            var points = UnityEngine.Object.FindObjectsByType<ServicePointView>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var point in points)
            {
                Transform workSpot = point.WorkSpot;
                if (workSpot != null && workSpot != point.transform)
                {
                    // Offset by 0.5m forward away from pillar obstacle
                    Vector3 localPos = workSpot.localPosition;
                    if (Mathf.Abs(localPos.x) > 2.0f)
                    {
                        localPos.x = Mathf.Sign(localPos.x) * 2.0f;
                        workSpot.localPosition = localPos;
                        EditorUtility.SetDirty(workSpot);
                    }
                }
            }

            // Rebake NavMesh surfaces
            var surfaces = UnityEngine.Object.FindObjectsByType<Unity.AI.Navigation.NavMeshSurface>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var surface in surfaces)
            {
                surface.BuildNavMesh();
                Debug.Log("[Location2SceneUpdater] Rebaked NavMeshSurface: " + surface.name);
            }
        }

        private static void UpdateParkingConfig()
        {
            var config = AssetDatabase.LoadAssetAtPath<ServiceTypeConfig>(ParkingConfigPath);
            if (config != null)
            {
                var so = new SerializedObject(config);
                SerializedProperty xpProp = so.FindProperty("_xpReward");
                if (xpProp != null && xpProp.intValue != 5)
                {
                    xpProp.intValue = 5;
                    so.ApplyModifiedProperties();
                    EditorUtility.SetDirty(config);
                    Debug.Log("[Location2SceneUpdater] Updated ST_Parking _xpReward = 5.");
                }
            }
        }

        private static void UpdateTravelPoints(Quaternion facing)
        {
            GameObject plot1Go = GameObject.Find("TravelPlot_Travel_To_Loc2");
            GameObject plot2Go = GameObject.Find("TravelPlot_Travel_To_Loc1");

            if (plot1Go == null || plot2Go == null)
            {
                Debug.LogWarning("[Location2SceneUpdater] Travel plots not found in scene.");
                return;
            }

            TravelPoint tp1 = plot1Go.GetComponentInChildren<TravelPoint>(true);
            TravelPoint tp2 = plot2Go.GetComponentInChildren<TravelPoint>(true);

            if (tp1 == null || tp2 == null)
            {
                Debug.LogWarning("[Location2SceneUpdater] TravelPoint components not found inside travel plots.");
                return;
            }

            // 1. Cross-link targets directly without offset dummy spawns
            var so1 = new SerializedObject(tp1);
            so1.FindProperty("_targetTransform").objectReferenceValue = tp2.transform;
            so1.ApplyModifiedProperties();

            var so2 = new SerializedObject(tp2);
            so2.FindProperty("_targetTransform").objectReferenceValue = tp1.transform;
            so2.ApplyModifiedProperties();

            // 2. Remove obsolete dummy spawn transforms if present
            GameObject spawn1 = GameObject.Find("SpawnLoc1");
            if (spawn1 != null)
            {
                Undo.DestroyObjectImmediate(spawn1);
            }

            GameObject spawn2 = GameObject.Find("SpawnLoc2");
            if (spawn2 != null)
            {
                Undo.DestroyObjectImmediate(spawn2);
            }

            // 3. Create/update dwell ring visual for both travel points
            SetupTravelPointRing(tp1, facing);
            SetupTravelPointRing(tp2, facing);

            Debug.Log("[Location2SceneUpdater] Cross-linked TravelPoints and attached DwellRingViews.");
        }

        private static void SetupTravelPointRing(TravelPoint tp, Quaternion facing)
        {
            // Remove old TagCanvas if it was created by the old builder without background
            Transform oldTag = tp.transform.Find("TagCanvas");
            if (oldTag != null)
            {
                Undo.DestroyObjectImmediate(oldTag.gameObject);
            }

            DwellRingView ring = EnsureDwellRingCanvas(tp.transform, tp.transform.position, facing, 2.5f, 0.6f, "Tag");
            var so = new SerializedObject(tp);
            so.FindProperty("_ring").objectReferenceValue = ring;
            so.ApplyModifiedProperties();
        }

        private static void UpdateManagePads(Quaternion facing)
        {
            var pads = UnityEngine.Object.FindObjectsByType<ManagePadView>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            int updated = 0;

            for (int i = 0; i < pads.Length; i++)
            {
                ManagePadView pad = pads[i];
                var so = new SerializedObject(pad);
                SerializedProperty ringProp = so.FindProperty("_ring");

                if (ringProp.objectReferenceValue == null)
                {
                    DwellRingView ring = EnsureDwellRingCanvas(pad.transform, pad.transform.position, facing, 2.6f, 0.6f, "Tag");
                    ringProp.objectReferenceValue = ring;
                    so.ApplyModifiedProperties();
                    updated++;
                }
            }

            Debug.Log("[Location2SceneUpdater] Verified ManagePads; attached DwellRingView to " + updated + " pad(s).");
        }

        /// <summary>
        /// Clones the modular visual models, transforms, and UI structure from Warehouse_1 to Warehouse_loc2.
        /// </summary>
        [MenuItem("AutoService/Setup/Clone Warehouse 1 Visuals To Location 2")]
        public static void CloneWarehouse1VisualsToLoc2()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != GameplayScenePath)
            {
                scene = EditorSceneManager.OpenScene(GameplayScenePath, OpenSceneMode.Single);
            }

            GameObject wh1 = GameObject.Find("Warehouse_1");
            GameObject wh2 = GameObject.Find("Warehouse_loc2");

            if (wh1 == null || wh2 == null)
            {
                var allTransforms = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                foreach (var t in allTransforms)
                {
                    if (t.name == "Warehouse_1") wh1 = t.gameObject;
                    if (t.name == "Warehouse_loc2") wh2 = t.gameObject;
                }
            }

            if (wh1 == null || wh2 == null)
            {
                Debug.LogWarning("[Location2SceneUpdater] Warehouse_1 or Warehouse_loc2 not found in scene.");
                return;
            }

            // 1. Deactivate whitebox Body on Warehouse_loc2
            Transform body2 = wh2.transform.Find("Body");
            if (body2 != null)
            {
                body2.gameObject.SetActive(false);
                EditorUtility.SetDirty(body2.gameObject);
            }

            // 2. Remove duplicate/nested WorkPad under ApproachPoint if present
            Transform approach2 = wh2.transform.Find("ApproachPoint");
            if (approach2 != null)
            {
                Transform nestedPad = approach2.Find("WorkPad");
                if (nestedPad != null)
                {
                    Undo.DestroyObjectImmediate(nestedPad.gameObject);
                }
                approach2.localPosition = new Vector3(-2.16f, 0f, -0.53f);
                approach2.localRotation = Quaternion.Euler(0f, 90f, 0f);
                EditorUtility.SetDirty(approach2);
            }

            // 3. Align WorkPad directly under Warehouse_loc2
            Transform workPad2 = wh2.transform.Find("WorkPad");
            if (workPad2 == null)
            {
                var pad = GameObject.CreatePrimitive(PrimitiveType.Cube);
                pad.name = "WorkPad";
                UnityEngine.Object.DestroyImmediate(pad.GetComponent<Collider>());
                pad.transform.SetParent(wh2.transform, false);
                workPad2 = pad.transform;
                Material workPadMat = AssetDatabase.LoadAssetAtPath<Material>(WorkPadMaterialPath);
                if (workPadMat != null)
                {
                    pad.GetComponent<Renderer>().sharedMaterial = workPadMat;
                }
                Undo.RegisterCreatedObjectUndo(pad, "Create WorkPad");
            }
            workPad2.localPosition = new Vector3(-2.16f, 0.01f, -0.53f);
            workPad2.localRotation = Quaternion.identity;
            workPad2.localScale = new Vector3(1.6f, 0.02f, 1.6f);
            EditorUtility.SetDirty(workPad2);

            // 4. Align StorekeeperSpots
            Transform spot0 = wh2.transform.Find("StorekeeperSpot_0");
            if (spot0 != null)
            {
                spot0.localPosition = new Vector3(-1.2f, 0f, -2.3f);
                spot0.localRotation = Quaternion.Euler(0f, 180f, 0f);
                EditorUtility.SetDirty(spot0);
            }
            Transform spot1 = wh2.transform.Find("StorekeeperSpot_1");
            if (spot1 != null)
            {
                spot1.localPosition = new Vector3(0f, 0f, -2.3f);
                spot1.localRotation = Quaternion.Euler(0f, 180f, 0f);
                EditorUtility.SetDirty(spot1);
            }
            Transform spot2 = wh2.transform.Find("StorekeeperSpot_2");
            if (spot2 != null)
            {
                spot2.localPosition = new Vector3(1.2f, 0f, -2.3f);
                spot2.localRotation = Quaternion.Euler(0f, 180f, 0f);
                EditorUtility.SetDirty(spot2);
            }

            // 5. Clean existing cloned models on Warehouse_loc2
            for (int i = wh2.transform.childCount - 1; i >= 0; i--)
            {
                Transform child = wh2.transform.GetChild(i);
                string cName = child.name;
                if (cName == "floor" || cName == "conveyor-long" ||
                    cName.StartsWith("box-wide") || cName.StartsWith("box-small") ||
                    cName.StartsWith("detail-awning"))
                {
                    Undo.DestroyObjectImmediate(child.gameObject);
                }
            }

            // 6. Clone all 19 model children from Warehouse_1
            for (int i = 0; i < wh1.transform.childCount; i++)
            {
                Transform srcChild = wh1.transform.GetChild(i);
                string srcName = srcChild.name;
                if (srcName == "floor" || srcName == "conveyor-long" ||
                    srcName.StartsWith("box-wide") || srcName.StartsWith("box-small") ||
                    srcName.StartsWith("detail-awning"))
                {
                    GameObject srcPrefab = PrefabUtility.GetCorrespondingObjectFromSource(srcChild.gameObject);
                    GameObject clone;
                    if (srcPrefab != null)
                    {
                        clone = (GameObject)PrefabUtility.InstantiatePrefab(srcPrefab, wh2.transform);
                        clone.name = srcName;
                    }
                    else
                    {
                        clone = UnityEngine.Object.Instantiate(srcChild.gameObject, wh2.transform);
                        clone.name = srcName;
                    }

                    clone.transform.localPosition = srcChild.localPosition;
                    clone.transform.localRotation = srcChild.localRotation;
                    clone.transform.localScale = srcChild.localScale;

                    clone.layer = wh2.layer;
                    foreach (Transform t in clone.GetComponentsInChildren<Transform>(true))
                    {
                        t.gameObject.layer = wh2.layer;
                    }
                    Undo.RegisterCreatedObjectUndo(clone, "Clone warehouse visual part");
                }
            }

            // 7. Update BoxCollider to match Warehouse_1 bounding box
            BoxCollider col1 = wh1.GetComponent<BoxCollider>();
            BoxCollider col2 = wh2.GetComponent<BoxCollider>();
            if (col2 != null)
            {
                if (col1 != null)
                {
                    col2.center = col1.center;
                    col2.size = col1.size;
                }
                else
                {
                    col2.center = new Vector3(0.9787302f, 1.0f, -0.57f);
                    col2.size = new Vector3(5.450001f, 4.023367f, 3.6142006f);
                }
                EditorUtility.SetDirty(col2);
            }

            Debug.Log("[Location2SceneUpdater] Cloned visuals, transforms, spots, and collider to Warehouse_loc2!");
        }

        private static void UpdateWarehouses(Quaternion facing)
        {
            var warehouses = UnityEngine.Object.FindObjectsByType<WarehouseView>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Material workPadMat = AssetDatabase.LoadAssetAtPath<Material>(WorkPadMaterialPath);

            for (int i = 0; i < warehouses.Length; i++)
            {
                WarehouseView wh = warehouses[i];
                Transform approach = wh.ApproachPoint;
                Vector3 workSpot = approach.position;

                // 1. Ensure WorkPad yellow cube under ApproachPoint
                Transform existingPad = wh.transform.Find("WorkPad");
                if (existingPad == null)
                {
                    var pad = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    pad.name = "WorkPad";
                    UnityEngine.Object.DestroyImmediate(pad.GetComponent<Collider>());
                    pad.transform.SetParent(wh.transform, true);
                    pad.transform.position = new Vector3(workSpot.x, workSpot.y + 0.01f, workSpot.z);
                    pad.transform.rotation = Quaternion.identity;
                    pad.transform.localScale = new Vector3(1.6f, 0.02f, 1.6f);

                    if (workPadMat != null)
                    {
                        pad.GetComponent<Renderer>().sharedMaterial = workPadMat;
                    }
                    Undo.RegisterCreatedObjectUndo(pad, "Create warehouse work pad");
                }

                // 2. Ensure overhead HUD Canvas (Progress bar + Message + TitleBadge)
                Transform existingHud = wh.transform.Find("HUD");
                if (existingHud != null)
                {
                    Undo.DestroyObjectImmediate(existingHud.gameObject);
                }

                // Also remove obsolete separate Label canvas if present
                Transform oldLabel = wh.transform.Find("Label");
                if (oldLabel != null)
                {
                    Undo.DestroyObjectImmediate(oldLabel.gameObject);
                }

                Vector3 towardsCamera = -(facing * Vector3.forward);
                towardsCamera.y = 0f;
                towardsCamera = towardsCamera.sqrMagnitude > 0.0001f ? towardsCamera.normalized : Vector3.back;
                Vector3 hudPos = workSpot + Vector3.up * 3.0f + towardsCamera * 0.3f;

                var hudGo = new GameObject("HUD", typeof(RectTransform), typeof(Canvas));
                hudGo.transform.SetParent(wh.transform, false);
                var canvas = hudGo.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.additionalShaderChannels = AdditionalCanvasShaderChannels.TexCoord1 | 
                                                  AdditionalCanvasShaderChannels.Normal | 
                                                  AdditionalCanvasShaderChannels.Tangent;

                var hudRect = (RectTransform)hudGo.transform;
                hudRect.sizeDelta = new Vector2(300f, 120f);
                hudRect.SetPositionAndRotation(hudPos, facing);

                Vector3 parentScale = wh.transform.lossyScale;
                const float worldScale = 0.005f;
                hudRect.localScale = new Vector3(
                    parentScale.x != 0f ? worldScale / Mathf.Abs(parentScale.x) : worldScale,
                    parentScale.y != 0f ? worldScale / Mathf.Abs(parentScale.y) : worldScale,
                    parentScale.z != 0f ? worldScale / Mathf.Abs(parentScale.z) : worldScale);

                Sprite uiSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>(UiSpritePath);
                Sprite badgeSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/Kenney/kenney_ui-pack/Red/Default/button_rectangle_depth_flat.png")
                    ?? AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/Kenney/kenney_ui-pack/Yellow/Default/button_rectangle_depth_flat.png")
                    ?? uiSprite;

                Sprite titleBadgeSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/Kenney/kenney_ui-pack/Default/button_rectangle_depth_flat.png")
                    ?? AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/Kenney/kenney_ui-pack/Grey/Default/button_rectangle_depth_flat.png")
                    ?? badgeSprite;

                TMP_FontAsset fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                    "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset")
                    ?? AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                    "Assets/_Project/Art/Kenney/kenney_ui-pack/Font/LiberationSans SDF.asset");

                // Progress bar root
                var progressRoot = new GameObject("ProgressRoot", typeof(RectTransform));
                progressRoot.transform.SetParent(hudRect, false);
                var prRect = (RectTransform)progressRoot.transform;
                prRect.anchoredPosition = Vector2.zero;
                prRect.sizeDelta = new Vector2(160f, 18f);

                // Bar background
                var bgGo = new GameObject("BarBackground", typeof(RectTransform));
                bgGo.transform.SetParent(prRect, false);
                var bgRect = (RectTransform)bgGo.transform;
                bgRect.anchoredPosition = Vector2.zero;
                bgRect.sizeDelta = new Vector2(160f, 18f);
                var bgImg = bgGo.AddComponent<Image>();
                bgImg.sprite = uiSprite;
                bgImg.type = Image.Type.Sliced;
                bgImg.color = new Color(0.12f, 0.14f, 0.18f, 0.9f);
                bgImg.raycastTarget = false;

                // Bar fill
                var fillGo = new GameObject("Fill", typeof(RectTransform));
                fillGo.transform.SetParent(prRect, false);
                var fillRect = (RectTransform)fillGo.transform;
                fillRect.anchoredPosition = Vector2.zero;
                fillRect.sizeDelta = new Vector2(156f, 14f);
                var fillImg = fillGo.AddComponent<Image>();
                fillImg.sprite = uiSprite;
                fillImg.type = Image.Type.Filled;
                fillImg.fillMethod = Image.FillMethod.Horizontal;
                fillImg.fillOrigin = (int)Image.OriginHorizontal.Left;
                fillImg.fillAmount = 0f;
                fillImg.color = new Color(0.2f, 0.8f, 0.3f, 1f);
                fillImg.raycastTarget = false;

                progressRoot.SetActive(false);

                // Message Badge container (dynamic interaction feedback)
                var msgGo = new GameObject("MessageBadge", typeof(RectTransform), typeof(CanvasGroup));
                msgGo.transform.SetParent(hudRect, false);
                var msgRect = (RectTransform)msgGo.transform;
                msgRect.anchoredPosition = new Vector2(0f, 34f);
                msgRect.sizeDelta = new Vector2(260f, 62f);

                var badgeImg = msgGo.AddComponent<Image>();
                badgeImg.sprite = badgeSprite;
                badgeImg.type = Image.Type.Sliced;
                badgeImg.color = Color.white;
                badgeImg.raycastTarget = false;

                // Message text inside badge
                var textGo = new GameObject("Text", typeof(RectTransform));
                textGo.transform.SetParent(msgGo.transform, false);
                var textRect = (RectTransform)textGo.transform;
                textRect.anchorMin = Vector2.zero;
                textRect.anchorMax = Vector2.one;
                textRect.sizeDelta = Vector2.zero;
                textRect.anchoredPosition = new Vector2(0f, 2f);

                var msgTmp = textGo.AddComponent<TextMeshProUGUI>();
                if (fontAsset != null) msgTmp.font = fontAsset;
                msgTmp.text = "Need $0";
                msgTmp.fontSize = 26f;
                msgTmp.fontStyle = FontStyles.Bold;
                msgTmp.alignment = TextAlignmentOptions.Center;
                msgTmp.color = Color.white;
                msgTmp.raycastTarget = false;
                msgGo.SetActive(false);

                // Title Badge ("Warehouse" sign plate on building)
                var titleGo = new GameObject("TitleBadge", typeof(RectTransform));
                titleGo.transform.SetParent(hudRect, false);
                var titleRect = (RectTransform)titleGo.transform;
                titleRect.anchoredPosition = new Vector2(607f, -15f);
                titleRect.localPosition = new Vector3(titleRect.localPosition.x, titleRect.localPosition.y, -322f);
                titleRect.localRotation = Quaternion.Euler(30f, 0f, 0f);
                titleRect.sizeDelta = new Vector2(600f, 150f);

                var titleImg = titleGo.AddComponent<Image>();
                titleImg.sprite = titleBadgeSprite;
                titleImg.type = Image.Type.Sliced;
                titleImg.color = new Color(0.95f, 0.95f, 0.98f, 0.98f);
                titleImg.raycastTarget = false;

                var titleTextGo = new GameObject("Text", typeof(RectTransform));
                titleTextGo.transform.SetParent(titleGo.transform, false);
                var titleTextRect = (RectTransform)titleTextGo.transform;
                titleTextRect.anchorMin = Vector2.zero;
                titleTextRect.anchorMax = Vector2.one;
                titleTextRect.sizeDelta = Vector2.zero;
                titleTextRect.anchoredPosition = new Vector2(0f, 2f);

                var titleTmp = titleTextGo.AddComponent<TextMeshProUGUI>();
                if (fontAsset != null) titleTmp.font = fontAsset;
                titleTmp.text = "Warehouse";
                titleTmp.fontSize = 68.2f;
                titleTmp.fontSizeMin = 18f;
                titleTmp.fontSizeMax = 72f;
                titleTmp.enableAutoSizing = true;
                titleTmp.fontStyle = FontStyles.Bold;
                titleTmp.alignment = TextAlignmentOptions.Center;
                titleTmp.color = new Color(0.3647f, 0.3882f, 0.4784f, 1f);
                titleTmp.raycastTarget = false;

                // Wire to WarehouseView
                var wSo = new SerializedObject(wh);
                wSo.FindProperty("_progressRoot").objectReferenceValue = progressRoot;
                wSo.FindProperty("_progressBarFill").objectReferenceValue = fillImg;
                wSo.FindProperty("_messageLabel").objectReferenceValue = msgTmp;
                var msgRootProp = wSo.FindProperty("_messageRoot");
                if (msgRootProp != null)
                {
                    msgRootProp.objectReferenceValue = msgGo;
                }
                wSo.FindProperty("_dwellSeconds").floatValue = 1.2f;
                wSo.ApplyModifiedProperties();

                // 3. Re-link active renderers to InteractableHighlight (excluding WorkPad, Disc and ManagePad)
                var highlight = wh.GetComponent<InteractableHighlight>();
                if (highlight != null)
                {
                    Transform managePad = wh.transform.Find("ManagePad_loc2");
                    var activeRenderers = System.Array.FindAll(wh.GetComponentsInChildren<MeshRenderer>(false),
                        r => r.gameObject.name != "WorkPad" && r.gameObject.name != "Disc" && (managePad == null || !r.transform.IsChildOf(managePad)));
                    if (activeRenderers.Length == 0)
                    {
                        activeRenderers = wh.GetComponentsInChildren<MeshRenderer>(false);
                    }

                    if (activeRenderers.Length > 0)
                    {
                        var so = new SerializedObject(highlight);
                        var rendProp = so.FindProperty("_renderers");
                        rendProp.arraySize = activeRenderers.Length;
                        for (int r = 0; r < activeRenderers.Length; r++)
                        {
                            rendProp.GetArrayElementAtIndex(r).objectReferenceValue = activeRenderers[r];
                        }
                        so.ApplyModifiedProperties();
                    }
                }

                // 4. Update BoxCollider bounds of warehouse based on active renderers
                var collider = wh.GetComponent<BoxCollider>();
                if (collider != null)
                {
                    Transform managePad = wh.transform.Find("ManagePad_loc2");
                    var allRenderers = System.Array.FindAll(wh.GetComponentsInChildren<MeshRenderer>(false),
                        r => r.gameObject.name != "WorkPad" && r.gameObject.name != "Disc" && (managePad == null || !r.transform.IsChildOf(managePad)));
                    if (allRenderers.Length == 0)
                    {
                        allRenderers = wh.GetComponentsInChildren<MeshRenderer>(false);
                    }

                    if (allRenderers.Length > 0)
                    {
                        Bounds b = allRenderers[0].bounds;
                        for (int r = 1; r < allRenderers.Length; r++)
                        {
                            b.Encapsulate(allRenderers[r].bounds);
                        }
                        collider.center = wh.transform.InverseTransformPoint(b.center);
                        collider.size = b.size;
                        EditorUtility.SetDirty(collider);
                    }
                }

                Undo.RegisterCreatedObjectUndo(hudGo, "Create warehouse HUD");
            }

            Debug.Log("[Location2SceneUpdater] Configured WorkPad, HUD, Highlight and Collider for " + warehouses.Length + " warehouse(s).");
        }

        private static void EnsureServiceBayHudNames()
        {
            var bays = new (string BayName, string DisplayName)[]
            {
                ("Wash", "Car Wash 1"),
                ("Bay_loc1_wash_2", "Car Wash 2"),
                ("Bay_loc1_oil_1", "Oil Service 1"),
                ("Bay_loc1_oil_2", "Oil Service 2"),
                ("Bay_loc2_tires_1", "Tire Service"),
                ("Bay_loc2_tuning_1", "Tuning"),
                ("Bay_loc2_paint_1", "Paint Shop")
            };

            Sprite bgSprite = AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/_Project/Art/Kenney/kenney_ui-pack/Default/button_rectangle_depth_flat.png")
                ?? AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/_Project/Art/Kenney/kenney_ui-pack/Grey/Default/button_rectangle_depth_flat.png")
                ?? AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/_Project/Art/Kenney/kenney_ui-pack/PNG/Default/button_rectangle_depth_flat.png");

            TMP_FontAsset fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset")
                ?? AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                "Assets/_Project/Art/Kenney/kenney_ui-pack/Font/LiberationSans SDF.asset");

            foreach (var (bayName, displayName) in bays)
            {
                GameObject bayGo = GameObject.Find(bayName);
                if (bayGo == null)
                {
                    var allTransforms = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                    foreach (var t in allTransforms)
                    {
                        if (t.name == bayName && t.gameObject.scene.isLoaded)
                        {
                            bayGo = t.gameObject;
                            break;
                        }
                    }
                }

                if (bayGo == null) continue;

                Transform existing = bayGo.transform.Find("HUDNAME");
                if (existing != null)
                {
                    Undo.DestroyObjectImmediate(existing.gameObject);
                }

                var hudGo = new GameObject("HUDNAME", typeof(RectTransform), typeof(Canvas));
                hudGo.transform.SetParent(bayGo.transform, false);
                hudGo.transform.localPosition = new Vector3(0f, 3.5f, -1.55f);
                hudGo.transform.localEulerAngles = new Vector3(30f, 0f, 0f);
                hudGo.transform.localScale = new Vector3(0.006f, 0.006f, 0.006f);

                var canvas = hudGo.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.additionalShaderChannels = AdditionalCanvasShaderChannels.TexCoord1 | 
                                                  AdditionalCanvasShaderChannels.Normal | 
                                                  AdditionalCanvasShaderChannels.Tangent;

                var rt = hudGo.GetComponent<RectTransform>();
                rt.sizeDelta = new Vector2(280f, 70f);

                // Background
                var bgGo = new GameObject("Background", typeof(RectTransform), typeof(Image));
                bgGo.transform.SetParent(hudGo.transform, false);
                var bgRt = bgGo.GetComponent<RectTransform>();
                bgRt.anchorMin = Vector2.zero;
                bgRt.anchorMax = Vector2.one;
                bgRt.sizeDelta = Vector2.zero;
                var bgImg = bgGo.GetComponent<Image>();
                bgImg.sprite = bgSprite;
                bgImg.type = Image.Type.Sliced;
                bgImg.color = new Color(0.95f, 0.95f, 0.98f, 0.98f);
                bgImg.raycastTarget = false;

                // Text
                var textGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
                textGo.transform.SetParent(bgGo.transform, false);
                var textRt = textGo.GetComponent<RectTransform>();
                textRt.anchorMin = Vector2.zero;
                textRt.anchorMax = Vector2.one;
                textRt.offsetMin = new Vector2(8f, 4f);
                textRt.offsetMax = new Vector2(-8f, -4f);
                var tmp = textGo.GetComponent<TextMeshProUGUI>();
                if (fontAsset != null) tmp.font = fontAsset;
                tmp.text = displayName;
                tmp.fontSize = 26f;
                tmp.fontStyle = FontStyles.Bold;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.color = new Color(0.15f, 0.18f, 0.25f, 1f);
                tmp.raycastTarget = false;

                EditorUtility.SetDirty(bayGo);
            }

            Debug.Log("[Location2SceneUpdater] Successfully created HUDNAME plates for all service bays!");
        }

        private static DwellRingView EnsureDwellRingCanvas(Transform parent, Vector3 groundPos, Quaternion facing, float height, float forwardOffset, string canvasName)
        {
            Transform existing = parent.Find(canvasName);
            if (existing != null)
            {
                var existingRing = existing.GetComponent<DwellRingView>();
                if (existingRing != null)
                {
                    var ringSo = new SerializedObject(existingRing);
                    if (ringSo.FindProperty("_fill").objectReferenceValue != null)
                    {
                        // Remove legacy Background and Arrow if present
                        Transform oldBg = existing.Find("Background");
                        if (oldBg != null) Undo.DestroyObjectImmediate(oldBg.gameObject);
                        Transform oldArrow = existing.Find("Arrow");
                        if (oldArrow != null) Undo.DestroyObjectImmediate(oldArrow.gameObject);

                        ringSo.FindProperty("_background").objectReferenceValue = null;
                        ringSo.ApplyModifiedPropertiesWithoutUndo();
                        return existingRing;
                    }
                }
                Undo.DestroyObjectImmediate(existing.gameObject);
            }

            Vector3 towardsCamera = -(facing * Vector3.forward);
            towardsCamera.y = 0f;
            towardsCamera = towardsCamera.sqrMagnitude > 0.0001f ? towardsCamera.normalized : Vector3.back;
            Vector3 position = groundPos + Vector3.up * height + towardsCamera * forwardOffset;

            var canvasGo = new GameObject(canvasName, typeof(RectTransform), typeof(Canvas));
            canvasGo.transform.SetParent(parent, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;

            var rect = (RectTransform)canvasGo.transform;
            rect.sizeDelta = PadCanvasSize;
            rect.SetPositionAndRotation(position, facing);

            Vector3 parentScale = parent.lossyScale;
            float worldScale = 0.9f / PadCanvasSize.x;
            rect.localScale = new Vector3(
                parentScale.x != 0f ? worldScale / Mathf.Abs(parentScale.x) : worldScale,
                parentScale.y != 0f ? worldScale / Mathf.Abs(parentScale.y) : worldScale,
                parentScale.z != 0f ? worldScale / Mathf.Abs(parentScale.z) : worldScale);

            Sprite knobSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>(KnobSpritePath);

            var fillGo = new GameObject("Fill", typeof(RectTransform));
            fillGo.transform.SetParent(rect, false);
            var fillRect = (RectTransform)fillGo.transform;
            fillRect.sizeDelta = PadCanvasSize;
            var fillImg = fillGo.AddComponent<Image>();
            fillImg.sprite = knobSprite;
            fillImg.color = PadRingFill;
            fillImg.type = Image.Type.Filled;
            fillImg.fillMethod = Image.FillMethod.Radial360;
            fillImg.fillOrigin = (int)Image.Origin360.Top;
            fillImg.fillClockwise = true;
            fillImg.fillAmount = 0f;
            fillImg.raycastTarget = false;
            fillGo.SetActive(false);

            var ringView = canvasGo.AddComponent<DwellRingView>();
            var serialized = new SerializedObject(ringView);
            serialized.FindProperty("_fill").objectReferenceValue = fillImg;
            serialized.FindProperty("_root").objectReferenceValue = fillGo;
            serialized.FindProperty("_background").objectReferenceValue = null;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Undo.RegisterCreatedObjectUndo(canvasGo, "Create Dwell Ring");
            return ringView;
        }

        private static Quaternion GetCameraFacing()
        {
            Camera cam = Camera.main != null ? Camera.main : UnityEngine.Object.FindFirstObjectByType<Camera>();
            if (cam != null)
            {
                return cam.transform.rotation;
            }

            return Quaternion.Euler(50f, 45f, 0f);
        }
    }
}
