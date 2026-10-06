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
            UpdateWarehouses(facing);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            Debug.Log("[Location2SceneUpdater] Successfully applied Location 2 and Warehouse fixes to " + scene.name + "!");
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

                // 2. Ensure overhead HUD Canvas (Progress bar + Message)
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
                Vector3 hudPos = workSpot + Vector3.up * 2.4f + towardsCamera * 0.3f;

                var hudGo = new GameObject("HUD", typeof(RectTransform), typeof(Canvas));
                hudGo.transform.SetParent(wh.transform, false);
                var canvas = hudGo.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;

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

                // Message text
                var msgGo = new GameObject("Message", typeof(RectTransform));
                msgGo.transform.SetParent(hudRect, false);
                var msgRect = (RectTransform)msgGo.transform;
                msgRect.anchoredPosition = new Vector2(0f, 32f);
                msgRect.sizeDelta = new Vector2(400f, 60f);
                var msgTmp = msgGo.AddComponent<TextMeshProUGUI>();
                msgTmp.text = "Need $0";
                msgTmp.fontSize = 28f;
                msgTmp.fontStyle = FontStyles.Bold;
                msgTmp.alignment = TextAlignmentOptions.Center;
                msgTmp.color = new Color(1f, 0.84f, 0.31f, 1f);
                msgTmp.raycastTarget = false;
                msgGo.SetActive(false);

                // Wire to WarehouseView
                var wSo = new SerializedObject(wh);
                wSo.FindProperty("_progressRoot").objectReferenceValue = progressRoot;
                wSo.FindProperty("_progressBarFill").objectReferenceValue = fillImg;
                wSo.FindProperty("_messageLabel").objectReferenceValue = msgTmp;
                wSo.FindProperty("_dwellSeconds").floatValue = 1.2f;
                wSo.ApplyModifiedProperties();

                Undo.RegisterCreatedObjectUndo(hudGo, "Create warehouse HUD");
            }

            Debug.Log("[Location2SceneUpdater] Configured WorkPad and HUD for " + warehouses.Length + " warehouse(s).");
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
                    if (ringSo.FindProperty("_fill").objectReferenceValue != null &&
                        ringSo.FindProperty("_background").objectReferenceValue != null)
                    {
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
            Sprite arrowSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>(ArrowSpritePath);

            var bgGo = new GameObject("Background", typeof(RectTransform));
            bgGo.transform.SetParent(rect, false);
            var bgRect = (RectTransform)bgGo.transform;
            bgRect.sizeDelta = PadCanvasSize;
            var bgImg = bgGo.AddComponent<Image>();
            bgImg.sprite = knobSprite;
            bgImg.color = PadRingBackground;
            bgImg.raycastTarget = false;

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

            var arrowGo = new GameObject("Arrow", typeof(RectTransform));
            arrowGo.transform.SetParent(rect, false);
            var arrowRect = (RectTransform)arrowGo.transform;
            arrowRect.sizeDelta = new Vector2(90f, 90f);
            arrowRect.localRotation = Quaternion.Euler(0f, 0f, 180f);
            var arrowImg = arrowGo.AddComponent<Image>();
            arrowImg.sprite = arrowSprite;
            arrowImg.color = Color.white;
            arrowImg.raycastTarget = false;

            var ringView = canvasGo.AddComponent<DwellRingView>();
            var serialized = new SerializedObject(ringView);
            serialized.FindProperty("_fill").objectReferenceValue = fillImg;
            serialized.FindProperty("_root").objectReferenceValue = fillGo;
            serialized.FindProperty("_background").objectReferenceValue = bgGo;
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
