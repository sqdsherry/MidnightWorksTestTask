using System;
using System.Collections.Generic;
using AutoService.Domain.Common;
using AutoService.Presentation.Building;
using AutoService.Presentation.Interaction;
using AutoService.Presentation.Points;
using AutoService.Presentation.Supplies;
using AutoService.Presentation.Traffic;
using AutoService.Presentation.Traffic.Routing;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace AutoService.Bootstrap.Editor
{
    internal static class WhiteboxLocation2Builder
    {
        private const string Loc2Name = "Location_2";
        private const string Loc1Name = "Location_1";
        private const float Loc2Offset = 200f;

        private const string MtbRoadPath = "Assets/_Project/Materials/M_RoadGray.mat";
        private const string MtbGhostPath = "Assets/_Project/Materials/M_Ghost.mat";
        private const string MtbManagePadPath = "Assets/_Project/Materials/M_ManagePad.mat";
        private const string MtbWarehousePath = "Assets/_Project/Materials/M_Warehouse.mat";
        private const string MtbStaffRoomPath = "Assets/_Project/Materials/M_StaffRoom.mat";
        private const string MtbWorkPadPath = "Assets/_Project/Materials/M_WorkPad.mat";

        private static readonly Color GhostColor = new Color(0.55f, 0.75f, 1f, 0.35f);
        private static readonly Color ManagePadColor = new Color(0.2f, 0.5f, 1f, 1f);
        private static readonly Color WarehouseColor = new Color(0.6f, 0.45f, 0.3f, 1f);
        private static readonly Color StaffRoomColor = new Color(0.45f, 0.55f, 0.65f, 1f);
        private static readonly Color RoadColor = new Color(0.34f, 0.34f, 0.34f, 1f);
        private static readonly Color WorkPadColor = new Color(0.95f, 0.75f, 0.1f, 1f);


        [MenuItem("AutoService/Whitebox/Build Location 2 (and Travel Points)")]
        public static void BuildLocation2()
        {
            LocationLayout loc1 = FindLayout(Loc1Name);
            if (loc1 == null)
            {
                Debug.LogError("[Whitebox] Location_1 not found.");
                return;
            }

            LocationLayout loc2 = FindLayout(Loc2Name);
            if (loc2 == null)
            {
                GameObject obj = new GameObject(Loc2Name);
                obj.transform.position = new Vector3(Loc2Offset, 0, 0);
                loc2 = obj.AddComponent<LocationLayout>();
                Undo.RegisterCreatedObjectUndo(obj, "Create Location_2");
            }

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Build Location 2 and Travel");

            // Clean up existing generated objects in Loc2
            Transform root2 = loc2.transform;
            for (int i = root2.childCount - 1; i >= 0; i--)
            {
                Transform child = root2.GetChild(i);
                Undo.DestroyObjectImmediate(child.gameObject);
            }
            loc2.ResetGraphCache();

            // Clean up old travel plots on Loc1
            DestroyChildren(loc1.transform, "SpawnLoc1");
            DestroyChildren(loc1.transform, "TravelPlot_Travel_To_Loc2");

            // Build Location 2 content
            BuildLocation2Content(loc2, loc1);

            Undo.CollapseUndoOperations(undoGroup);
        }

        private const string RoadLayerName = "Road";

        private static void BuildLocation2Content(LocationLayout loc2, LocationLayout loc1)
        {
            Transform root = loc2.transform;
            int roadLayer = LayerMask.NameToLayer(RoadLayerName);
            if (roadLayer < 0) roadLayer = 0;
            int interactableLayer = LayerMask.NameToLayer("Interactable");
            if (interactableLayer < 0) interactableLayer = 0;

            Material roadMat = GetOrCreateMaterial(MtbRoadPath, "M_RoadGray", RoadColor);
            Material ghostMat = GetOrCreateGhostMaterial();
            Material managePadMat = GetOrCreateMaterial(MtbManagePadPath, "M_ManagePad", ManagePadColor);
            Material warehouseMat = GetOrCreateMaterial(MtbWarehousePath, "M_Warehouse", WarehouseColor);
            Material staffMat = GetOrCreateMaterial(MtbStaffRoomPath, "M_StaffRoom", StaffRoomColor);
            Material workPadMat = GetOrCreateMaterial(MtbWorkPadPath, "M_WorkPad", WorkPadColor);

            // 1. Ground Plates
            GameObject surfacesObj = CreateChild("Surfaces", root);
            CreatePlate(surfacesObj.transform, "Surface_EntryRoad", new Vector3(25f, -0.05f, -12f), new Vector3(24f, 0.1f, 6f), roadMat, roadLayer);
            CreatePlate(surfacesObj.transform, "Surface_ServiceArea", new Vector3(-5f, -0.05f, 0f), new Vector3(42f, 0.1f, 18f), roadMat, roadLayer);
            CreatePlate(surfacesObj.transform, "Surface_ExitRoad", new Vector3(-10f, -0.05f, 12f), new Vector3(56f, 0.1f, 6f), roadMat, roadLayer);
            CreatePlate(surfacesObj.transform, "Surface_WarehouseArea", new Vector3(25f, -0.05f, 6f), new Vector3(14f, 0.1f, 14f), roadMat, roadLayer);

            // 2. Road Nodes container
            GameObject nodesObj = CreateChild("RoadNodes", root);

            RoadNode nSpawn = CreateNode(nodesObj.transform, "N_Spawn", new Vector3(35f, 0f, -12f), -90f);
            RoadNode q3 = CreateNode(nodesObj.transform, "Q3", new Vector3(31f, 0f, -12f), -90f);
            RoadNode q2 = CreateNode(nodesObj.transform, "Q2", new Vector3(26f, 0f, -12f), -90f);
            RoadNode q1 = CreateNode(nodesObj.transform, "Q1", new Vector3(21f, 0f, -12f), -90f);
            RoadNode q0 = CreateNode(nodesObj.transform, "Q0", new Vector3(15f, 0f, -12f), -90f);

            // Box buffers (Z=-6)
            RoadNode b0Tires = CreateNode(nodesObj.transform, "B0_Tires", new Vector3(10f, 0f, -6f), 0f);
            RoadNode b0Tuning = CreateNode(nodesObj.transform, "B0_Tuning", new Vector3(-5f, 0f, -6f), 0f);
            RoadNode b0Paint = CreateNode(nodesObj.transform, "B0_Paint", new Vector3(-20f, 0f, -6f), 0f);

            // Exits (Z=6)
            RoadNode exitTires = CreateNode(nodesObj.transform, "Exit_Tires", new Vector3(10f, 0f, 6f), 0f);
            RoadNode exitTuning = CreateNode(nodesObj.transform, "Exit_Tuning", new Vector3(-5f, 0f, 6f), 0f);
            RoadNode exitPaint = CreateNode(nodesObj.transform, "Exit_Paint", new Vector3(-20f, 0f, 6f), 0f);

            // Top Road (Z=12)
            RoadNode topTires = CreateNode(nodesObj.transform, "Top_Tires", new Vector3(10f, 0f, 12f), -90f);
            RoadNode topTuning = CreateNode(nodesObj.transform, "Top_Tuning", new Vector3(-5f, 0f, 12f), -90f);
            RoadNode topPaint = CreateNode(nodesObj.transform, "Top_Paint", new Vector3(-20f, 0f, 12f), -90f);
            RoadNode nExit = CreateNode(nodesObj.transform, "N_Exit", new Vector3(-35f, 0f, 12f), -90f);

            // 3. Connect Nodes
            Link(nSpawn, q3);
            Link(q3, q2);
            Link(q2, q1);
            Link(q1, q0);
            Link(q0, b0Tires, b0Tuning, b0Paint);

            Link(exitTires, topTires);
            Link(exitTuning, topTuning);
            Link(exitPaint, topPaint);

            Link(topTires, topTuning);
            Link(topTuning, topPaint);
            Link(topPaint, nExit);

            // 4. Manage Pads List
            GameObject padsObj = CreateChild("ManagePads", root);
            var managePads = new List<ManagePadView>();

            // 5. Service Bays
            GameObject plotsObj = CreateChild("BuildPlots", root);
            var buildPlots = new List<BuildPlotView>();
            var servicePoints = new List<ServicePointView>();

            ServicePointView washRef = WhiteboxLocationBuilder.FindPoint(loc1, "loc1_wash_1");
            if (washRef == null)
            {
                Debug.LogError("[Whitebox] Reference bay 'loc1_wash_1' not found on Location_1!");
                return;
            }

            // Tires Bay
            BuildPlotView tiresPlot = CreateClonedServiceBay(
                washRef, plotsObj.transform, "loc2_build_tires_1", "loc2_tires_1", "tires_loc2",
                new Vector3(10f, 0f, 0f), b0Tires, exitTires,
                ghostMat, workPadMat, managePadMat,
                out ServicePointView tiresPoint, out ManagePadView tiresPad);
            buildPlots.Add(tiresPlot);
            servicePoints.Add(tiresPoint);
            managePads.Add(tiresPad);

            // Tuning Bay
            BuildPlotView tuningPlot = CreateClonedServiceBay(
                washRef, plotsObj.transform, "loc2_build_tuning_1", "loc2_tuning_1", "tuning_loc2",
                new Vector3(-5f, 0f, 0f), b0Tuning, exitTuning,
                ghostMat, workPadMat, managePadMat,
                out ServicePointView tuningPoint, out ManagePadView tuningPad);
            buildPlots.Add(tuningPlot);
            servicePoints.Add(tuningPoint);
            managePads.Add(tuningPad);

            // Paint Bay
            BuildPlotView paintPlot = CreateClonedServiceBay(
                washRef, plotsObj.transform, "loc2_build_paint_1", "loc2_paint_1", "paint_loc2",
                new Vector3(-20f, 0f, 0f), b0Paint, exitPaint,
                ghostMat, workPadMat, managePadMat,
                out ServicePointView paintPoint, out ManagePadView paintPad);
            buildPlots.Add(paintPlot);
            servicePoints.Add(paintPoint);
            managePads.Add(paintPad);

            // 6. Warehouse & Staff Room
            GameObject warehouseArea = CreateChild("WarehouseArea", root);
            WarehouseView warehouse = CreateWarehouse(
                warehouseArea.transform, "loc2", new Vector3(25f, 0f, 6f),
                warehouseMat, managePadMat, interactableLayer,
                out ManagePadView warehousePad, out List<Transform> storekeeperSpots);

            Transform staffRoomDoor = CreateStaffRoom(
                warehouseArea.transform, new Vector3(25f, 0f, 10f), staffMat);

            // 7. Travel Points
            Transform travelSpawnLoc2 = CreateChild("SpawnLoc2", root).transform;
            travelSpawnLoc2.position = root.position + new Vector3(30f, 0f, -15f);

            BuildPlotView travel1To2 = CreateTravelPlot(
                loc1.transform, "b_travel_to_loc2", "Travel_To_Loc2",
                new Vector3(-3f, 0f, 12f), travelSpawnLoc2);

            Transform travelSpawnLoc1 = CreateChild("SpawnLoc1", loc1.transform).transform;
            travelSpawnLoc1.position = loc1.transform.position + new Vector3(-34f, 0f, -19f);

            BuildPlotView travel2To1 = CreateTravelPlot(
                plotsObj.transform, "b_travel_to_loc1", "Travel_To_Loc1",
                new Vector3(-5f, 0f, -12f), travelSpawnLoc1);
            buildPlots.Add(travel2To1);

            var loc1So = new SerializedObject(loc1);
            AddPlotToLayout(loc1So, travel1To2);
            loc1So.ApplyModifiedPropertiesWithoutUndo();

            // 8. Bind Location 2
            var loc2So = new SerializedObject(loc2);
            loc2So.FindProperty("_locationId").stringValue = "loc2";
            loc2So.FindProperty("_mainEntrance").objectReferenceValue = null;
            loc2So.FindProperty("_serviceEntrance").objectReferenceValue = null;
            loc2So.FindProperty("_spawnNode").objectReferenceValue = nSpawn;
            loc2So.FindProperty("_exitNode").objectReferenceValue = nExit;

            SetArrayProp(loc2So.FindProperty("_queueSlots"), new UnityEngine.Object[] { q0, q1, q2, q3 });
            SetArrayProp(loc2So.FindProperty("_parkingSlots"), new UnityEngine.Object[0]);
            SetArrayProp(loc2So.FindProperty("_servicePoints"), servicePoints.ToArray());
            SetArrayProp(loc2So.FindProperty("_buildPlots"), buildPlots.ToArray());
            SetArrayProp(loc2So.FindProperty("_managePads"), managePads.ToArray());
            SetArrayProp(loc2So.FindProperty("_storekeeperSpots"), storekeeperSpots.ToArray());

            loc2So.FindProperty("_warehouse").objectReferenceValue = warehouse;
            loc2So.FindProperty("_warehousePad").objectReferenceValue = warehousePad;
            loc2So.FindProperty("_staffRoom").objectReferenceValue = staffRoomDoor;

            loc2So.ApplyModifiedPropertiesWithoutUndo();

            loc2.ResetGraphCache();
            if (!loc2.Validate(out string problem))
            {
                Debug.LogError("[Whitebox] Location_2 Validate failed: " + (problem ?? "<null>"), loc2);
            }
            else
            {
                Debug.Log("[Whitebox] Location_2 Validate succeeded! 0 errors.");
            }

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
        }

        private static BuildPlotView CreateClonedServiceBay(
            ServicePointView washRef,
            Transform parent,
            string plotId,
            string pointId,
            string serviceTypeId,
            Vector3 localPos,
            RoadNode bufferNode,
            RoadNode exitNode,
            Material ghostMat,
            Material workPadMat,
            Material managePadMat,
            out ServicePointView bay,
            out ManagePadView pad)
        {
            GameObject copy = UnityEngine.Object.Instantiate(washRef.gameObject, parent);
            copy.name = "Bay_" + pointId;
            copy.transform.localPosition = localPos;
            copy.transform.localRotation = Quaternion.identity;

            // Убираем визуальные вращающиеся щетки мойки (на сервисах 2 локации они не нужны)
            WashFx fx = copy.GetComponent<WashFx>();
            if (fx != null)
            {
                UnityEngine.Object.DestroyImmediate(fx);
            }

            // Remove existing pads from the cloned reference before adding fresh ones for this bay
            for (int i = copy.transform.childCount - 1; i >= 0; i--)
            {
                Transform child = copy.transform.GetChild(i);
                if (child.name.StartsWith("ManagePad", StringComparison.Ordinal) || child.name == "WorkPad")
                {
                    UnityEngine.Object.DestroyImmediate(child.gameObject);
                }
            }

            Transform fxTransform = copy.transform.Find("Visual/Fx");
            if (fxTransform != null)
            {
                UnityEngine.Object.DestroyImmediate(fxTransform.gameObject);
            }

            Transform signTransform = copy.transform.Find("Visual/Sign");
            if (signTransform != null)
            {
                var txt = signTransform.GetComponent<TextMeshPro>();
                if (txt != null) txt.text = serviceTypeId.ToUpper();
            }

            bay = copy.GetComponent<ServicePointView>();

            // Конфигурируем ServicePointView
            var viewSo = new SerializedObject(bay);
            viewSo.FindProperty("_pointId").stringValue = pointId;
            viewSo.FindProperty("_serviceTypeId").stringValue = serviceTypeId;
            SetArrayProp(viewSo.FindProperty("_bufferSlots"), new UnityEngine.Object[] { bufferNode });
            viewSo.ApplyModifiedPropertiesWithoutUndo();

            // Соединяем CarSpot в дорожный граф
            RoadNode carSpotNode = bay.CarSpot.GetComponent<RoadNode>();
            if (carSpotNode == null)
            {
                carSpotNode = bay.CarSpot.gameObject.AddComponent<RoadNode>();
            }
            Link(bufferNode, carSpotNode);
            Link(carSpotNode, exitNode);

            // Ставим рабочую точку и площадки (1-в-1 как на Локации 1: желтый квадрат и синий круг с иконкой апгрейда)
            WhiteboxLocationBuilder.PlaceBayWorkSpot(bay);
            pad = WhiteboxLocationBuilder.AddBayPads(bay, workPadMat, managePadMat, washRef.Hud);

            // Создаем призрак и BuildPlotView через проверенный метод Локации 1
            BuildPlotView plot = WhiteboxLocationBuilder.CreateBayGhost(washRef, bay, plotId, pointId, Vector3.zero, ghostMat, washRef.Hud);

            // Целевой бокс скрыт до покупки участка
            bay.gameObject.SetActive(false);

            return plot;
        }

        private static RoadNode CreateNode(Transform parent, string name, Vector3 localPos, float yaw)
        {
            GameObject obj = CreateChild(name, parent);
            obj.transform.localPosition = localPos;
            obj.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            return obj.AddComponent<RoadNode>();
        }

        private static void Link(RoadNode from, params RoadNode[] to)
        {
            var so = new SerializedObject(from);
            var nextProp = so.FindProperty("_next");
            nextProp.ClearArray();
            for (int i = 0; i < to.Length; i++)
            {
                if (to[i] == null) continue;
                nextProp.InsertArrayElementAtIndex(i);
                nextProp.GetArrayElementAtIndex(i).objectReferenceValue = to[i];
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void CreatePlate(Transform parent, string name, Vector3 localPos, Vector3 scale, Material mat, int layer)
        {
            GameObject plate = GameObject.CreatePrimitive(PrimitiveType.Cube);
            plate.name = name;
            plate.layer = layer;
            plate.transform.SetParent(parent, false);
            plate.transform.localPosition = localPos;
            plate.transform.localScale = scale;
            var r = plate.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
        }


        private readonly struct GhostTag
        {
            public GhostTag(TMP_Text label, DwellRingView ring)
            {
                Label = label;
                Ring = ring;
            }
            public TMP_Text Label { get; }
            public DwellRingView Ring { get; }
        }

        private static GhostTag CreateTagCanvas(Transform parent, Vector3 localPos, string placeholder)
        {
            var canvasObj = new GameObject("TagCanvas", typeof(RectTransform), typeof(Canvas));
            canvasObj.transform.SetParent(parent, false);
            canvasObj.transform.localPosition = localPos;
            canvasObj.transform.localRotation = Quaternion.Euler(30f, 0f, 0f);
            canvasObj.transform.localScale = Vector3.one * 0.01f;

            var canvas = canvasObj.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var rect = canvasObj.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(320f, 180f);

            var labelObj = new GameObject("PriceTag", typeof(RectTransform));
            labelObj.transform.SetParent(canvasObj.transform, false);
            var labelRect = (RectTransform)labelObj.transform;
            labelRect.anchoredPosition = new Vector2(0f, 45f);
            labelRect.sizeDelta = new Vector2(320f, 90f);
            var label = labelObj.AddComponent<TextMeshProUGUI>();
            label.text = placeholder;
            label.fontSize = 36f;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;
            label.raycastTarget = false;

            var ringObj = new GameObject("Ring", typeof(RectTransform));
            ringObj.transform.SetParent(canvasObj.transform, false);
            var ringRect = (RectTransform)ringObj.transform;
            ringRect.anchoredPosition = new Vector2(0f, -40f);
            ringRect.sizeDelta = new Vector2(80f, 80f);
            var ringImg = ringObj.AddComponent<Image>();
            ringImg.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            ringImg.type = Image.Type.Filled;
            ringImg.fillMethod = Image.FillMethod.Radial360;
            ringImg.fillOrigin = (int)Image.Origin360.Top;
            ringImg.fillClockwise = true;
            ringImg.fillAmount = 0f;
            ringImg.color = new Color(0.2f, 0.8f, 1f, 1f);
            ringImg.raycastTarget = false;

            var ringView = canvasObj.AddComponent<DwellRingView>();
            var rvSo = new SerializedObject(ringView);
            rvSo.FindProperty("_fill").objectReferenceValue = ringImg;
            rvSo.FindProperty("_root").objectReferenceValue = ringObj;
            rvSo.ApplyModifiedPropertiesWithoutUndo();

            return new GhostTag(label, ringView);
        }

        private static ManagePadView CreateManagePad(
            Transform parent, string targetId, ManagePadTarget target,
            Vector3 worldPos, Vector3 faceTowards,
            Material mat, int interactableLayer)
        {
            Vector3 ground = new Vector3(worldPos.x, 0f, worldPos.z);
            Vector3 facing = faceTowards - ground;
            facing.y = 0f;
            Quaternion rotation = facing.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(facing) : Quaternion.identity;

            GameObject root = CreateChild("ManagePad_" + targetId, parent);
            root.transform.position = ground;
            root.transform.rotation = rotation;
            root.layer = interactableLayer;

            GameObject disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            disc.name = "Disc";
            UnityEngine.Object.DestroyImmediate(disc.GetComponent<Collider>());
            disc.transform.SetParent(root.transform, false);
            disc.transform.localPosition = new Vector3(0f, 0.06f, 0f);
            disc.transform.localScale = new Vector3(1.2f, 0.02f, 1.2f);
            var discRenderer = disc.GetComponent<Renderer>();
            discRenderer.sharedMaterial = mat;
            discRenderer.shadowCastingMode = ShadowCastingMode.Off;

            var box = root.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 0.25f, 0f);
            box.size = new Vector3(1.2f, 0.5f, 1.2f);

            Transform approach = CreateChild("ApproachPoint", root.transform).transform;
            approach.SetPositionAndRotation(ground, rotation);

            Transform anchor = CreateChild("PanelAnchor", root.transform).transform;
            anchor.position = ground + Vector3.up * 1f;

            var highlight = root.AddComponent<InteractableHighlight>();
            var hlSo = new SerializedObject(highlight);
            hlSo.FindProperty("_renderers").InsertArrayElementAtIndex(0);
            hlSo.FindProperty("_renderers").GetArrayElementAtIndex(0).objectReferenceValue = discRenderer;
            hlSo.ApplyModifiedPropertiesWithoutUndo();

            var pad = root.AddComponent<ManagePadView>();
            var padSo = new SerializedObject(pad);
            padSo.FindProperty("_targetId").stringValue = targetId;
            padSo.FindProperty("_target").intValue = (int)target;
            padSo.FindProperty("_approachPoint").objectReferenceValue = approach;
            padSo.FindProperty("_panelAnchor").objectReferenceValue = anchor;
            padSo.FindProperty("_highlight").objectReferenceValue = highlight;
            padSo.ApplyModifiedPropertiesWithoutUndo();

            return pad;
        }

        private static WarehouseView CreateWarehouse(
            Transform parent, string locationId, Vector3 localPos,
            Material warehouseMat, Material padMat, int interactableLayer,
            out ManagePadView warehousePad, out List<Transform> storekeeperSpots)
        {
            GameObject root = CreateChild("Warehouse_" + locationId, parent);
            root.transform.localPosition = localPos;
            root.layer = interactableLayer;

            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Body";
            body.transform.SetParent(root.transform, false);
            body.transform.localPosition = new Vector3(0f, 1.25f, 0f);
            body.transform.localScale = new Vector3(4f, 2.5f, 3f);
            UnityEngine.Object.DestroyImmediate(body.GetComponent<Collider>());
            var bodyR = body.GetComponent<Renderer>();
            bodyR.sharedMaterial = warehouseMat;

            var box = root.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 1.25f, 0f);
            box.size = new Vector3(4f, 2.5f, 3f);

            Transform approach = CreateChild("ApproachPoint", root.transform).transform;
            approach.localPosition = new Vector3(0f, 0f, -2.5f);
            approach.localRotation = Quaternion.LookRotation(Vector3.forward);

            var highlight = root.AddComponent<InteractableHighlight>();
            var hlSo = new SerializedObject(highlight);
            hlSo.FindProperty("_renderers").InsertArrayElementAtIndex(0);
            hlSo.FindProperty("_renderers").GetArrayElementAtIndex(0).objectReferenceValue = bodyR;
            hlSo.ApplyModifiedPropertiesWithoutUndo();

            var warehouse = root.AddComponent<WarehouseView>();
            var wSo = new SerializedObject(warehouse);
            wSo.FindProperty("_locationId").stringValue = locationId;
            wSo.FindProperty("_approachPoint").objectReferenceValue = approach;
            wSo.FindProperty("_highlight").objectReferenceValue = highlight;
            wSo.ApplyModifiedPropertiesWithoutUndo();

            storekeeperSpots = new List<Transform>();
            for (int i = 0; i < 3; i++)
            {
                Transform spot = CreateChild("StorekeeperSpot_" + i, root.transform).transform;
                spot.localPosition = new Vector3((i - 1) * 1.2f, 0f, -2.2f);
                spot.localRotation = Quaternion.LookRotation(Vector3.back);
                storekeeperSpots.Add(spot);
            }

            warehousePad = CreateManagePad(
                root.transform, locationId, ManagePadTarget.Warehouse,
                root.transform.position + new Vector3(0f, 0f, -3.8f), root.transform.position,
                padMat, interactableLayer);

            return warehouse;
        }

        private static Transform CreateStaffRoom(Transform parent, Vector3 localPos, Material staffMat)
        {
            GameObject root = CreateChild("StaffRoom", parent);
            root.transform.localPosition = localPos;

            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Body";
            body.transform.SetParent(root.transform, false);
            body.transform.localPosition = new Vector3(0f, 1.25f, 0f);
            body.transform.localScale = new Vector3(3f, 2.5f, 3f);
            UnityEngine.Object.DestroyImmediate(body.GetComponent<Collider>());
            var r = body.GetComponent<Renderer>();
            r.sharedMaterial = staffMat;

            Transform door = CreateChild("Door", root.transform).transform;
            door.localPosition = new Vector3(0f, 0f, -2f);
            door.localRotation = Quaternion.LookRotation(Vector3.back);
            return door;
        }

        private static BuildPlotView CreateTravelPlot(Transform parent, string plotId, string name, Vector3 localPos, Transform teleportTarget)
        {
            GameObject plotObj = CreateChild("TravelPlot_" + name, parent);
            plotObj.transform.localPosition = localPos;

            int interactableLayer = LayerMask.NameToLayer("Interactable");
            if (interactableLayer < 0) interactableLayer = 0;

            GameObject ghost = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ghost.name = "Visual";
            ghost.transform.SetParent(plotObj.transform, false);
            ghost.GetComponent<Collider>().isTrigger = true;
            ghost.transform.localScale = new Vector3(2f, 0.1f, 2f);
            ghost.layer = interactableLayer;

            var highlight = ghost.AddComponent<InteractableHighlight>();
            var hlSo = new SerializedObject(highlight);
            hlSo.FindProperty("_renderers").InsertArrayElementAtIndex(0);
            hlSo.FindProperty("_renderers").GetArrayElementAtIndex(0).objectReferenceValue = ghost.GetComponent<Renderer>();
            hlSo.ApplyModifiedPropertiesWithoutUndo();

            GameObject target = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            target.name = "TravelPoint";
            target.transform.SetParent(plotObj.transform, false);
            target.transform.localScale = new Vector3(2f, 0.1f, 2f);
            target.GetComponent<Collider>().isTrigger = true;
            
            var rb = target.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            
            var renderer = target.GetComponent<Renderer>();
            renderer.sharedMaterial = new Material(renderer.sharedMaterial) { color = Color.green };
            target.layer = interactableLayer;
            
            var targetHighlight = target.AddComponent<InteractableHighlight>();
            var tHlSo = new SerializedObject(targetHighlight);
            tHlSo.FindProperty("_renderers").InsertArrayElementAtIndex(0);
            tHlSo.FindProperty("_renderers").GetArrayElementAtIndex(0).objectReferenceValue = renderer;
            tHlSo.ApplyModifiedPropertiesWithoutUndo();

            target.SetActive(false);

            var tp = target.AddComponent<TravelPoint>();
            tp.TargetTransform = teleportTarget;

            GhostTag tpTag = CreateTagCanvas(target.transform, new Vector3(0f, 38f, 0f), "Travel");
            var tpSo = new SerializedObject(tp);
            tpSo.FindProperty("_ring").objectReferenceValue = tpTag.Ring;
            tpSo.FindProperty("_highlight").objectReferenceValue = targetHighlight;
            tpSo.ApplyModifiedPropertiesWithoutUndo();

            Transform plotApproach = CreateChild("ApproachPoint", plotObj.transform).transform;
            plotApproach.localPosition = new Vector3(0f, 0f, -2.5f);

            Transform plotAnchor = CreateChild("PanelAnchor", plotObj.transform).transform;
            plotAnchor.localPosition = new Vector3(0f, 1.5f, 0f);

            var plotView = plotObj.AddComponent<BuildPlotView>();
            var plotSo = new SerializedObject(plotView);
            plotSo.FindProperty("_plotId").stringValue = plotId;
            plotSo.FindProperty("_ghost").objectReferenceValue = ghost;
            plotSo.FindProperty("_target").objectReferenceValue = target;
            plotSo.FindProperty("_approachPoint").objectReferenceValue = plotApproach;
            plotSo.FindProperty("_panelAnchor").objectReferenceValue = plotAnchor;
            plotSo.FindProperty("_highlight").objectReferenceValue = highlight;
            plotSo.ApplyModifiedPropertiesWithoutUndo();

            return plotView;
        }

        private static GameObject CreateChild(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go;
        }

        private static void DestroyChildren(Transform parent, string namePrefix)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                Transform child = parent.GetChild(i);
                if (child.name.StartsWith(namePrefix, StringComparison.Ordinal))
                {
                    Undo.DestroyObjectImmediate(child.gameObject);
                }
            }
        }

        private static LocationLayout FindLayout(string objName)
        {
            var go = GameObject.Find(objName);
            return go != null ? go.GetComponent<LocationLayout>() : null;
        }

        private static Material GetOrCreateMaterial(string path, string defaultName, Color color)
        {
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Standard"));
                mat.color = color;
                AssetDatabase.CreateAsset(mat, path);
            }
            return mat;
        }

        private static Material GetOrCreateGhostMaterial()
        {
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(MtbGhostPath);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Standard"));
                mat.color = GhostColor;
                mat.SetFloat("_Mode", 3); // Transparent
                mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.DisableKeyword("_ALPHATEST_ON");
                mat.EnableKeyword("_ALPHABLEND_ON");
                mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                mat.renderQueue = 3000;
                AssetDatabase.CreateAsset(mat, MtbGhostPath);
            }
            return mat;
        }

        private static void AddPlotToLayout(SerializedObject layoutSo, BuildPlotView plot)
        {
            var buildPlotsProp = layoutSo.FindProperty("_buildPlots");
            
            // Clean up nulls and existing references with the same plot ID
            for (int i = buildPlotsProp.arraySize - 1; i >= 0; i--)
            {
                var element = buildPlotsProp.GetArrayElementAtIndex(i).objectReferenceValue as BuildPlotView;
                if (element == null || element.PlotId == plot.PlotId)
                {
                    buildPlotsProp.DeleteArrayElementAtIndex(i);
                    // Deleting an element that is NOT null actually sets it to null in Unity serialization first, so we delete again
                    if (buildPlotsProp.arraySize > i && buildPlotsProp.GetArrayElementAtIndex(i).objectReferenceValue == null)
                        buildPlotsProp.DeleteArrayElementAtIndex(i);
                }
            }
            
            buildPlotsProp.InsertArrayElementAtIndex(buildPlotsProp.arraySize);
            buildPlotsProp.GetArrayElementAtIndex(buildPlotsProp.arraySize - 1).objectReferenceValue = plot;
        }

        private static void SetArrayProp(SerializedProperty prop, UnityEngine.Object[] items)
        {
            prop.ClearArray();
            for (int i = 0; i < items.Length; i++)
            {
                if (items[i] == null) continue;
                prop.InsertArrayElementAtIndex(i);
                prop.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
            }
        }
    }
}
