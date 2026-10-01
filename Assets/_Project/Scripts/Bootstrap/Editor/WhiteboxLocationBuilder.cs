using System.Collections.Generic;
using AutoService.Presentation.Building;
using AutoService.Presentation.Interaction;
using AutoService.Presentation.Points;
using AutoService.Presentation.Traffic;
using AutoService.Presentation.Traffic.Routing;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace AutoService.Bootstrap.Editor
{
    /// <summary>
    /// Editor tool: builds the whitebox road layout v3.1 of location 1 (GDD §3) under its <see cref="LocationLayout"/> —
    /// road surfaces, the one-way <see cref="RoadNode"/> graph and the merge zones — places the row of four bays (wash 1
    /// plus three copies that start as build plots), both parking entrances (creating the second one as a copy of the
    /// first), the ghosts of the bays and of parking slots 3–4, and fills the layout's references.
    /// One undoable step; safe to re-run.
    /// </summary>
    /// <remarks>
    /// Why a script: ~50 linked nodes and five ghosts by hand is an hour of clicking and typos; this is one click and
    /// reproducible. Once real assets replace the whitebox, nodes are simply moved by hand. All numbers live in the tables below.
    /// Everything the tool copies or creates outside <c>Roads_v3*</c> carries <see cref="WhiteboxGenerated"/> and is
    /// replaced on the next run.
    /// </remarks>
    internal static class WhiteboxLocationBuilder
    {
        // ── Whitebox layout v3.1, see GDD §3. World coordinates (the location root sits at the origin). ────────────────

        private const string LocationObjectName = "Location_1";
        private const string RootName = "Roads_v3_1";
        private const string ObsoleteRootPrefix = "Roads_v3";
        private const string ObsoleteRootV2 = "Roads_v2";
        private const string RoadLayerName = "Road";
        private const string InteractableLayerName = "Interactable";

        private const string WashPointId = "loc1_wash_1";
        private const string MainEntrancePointId = "loc1_entrance_main";
        private const string ServiceEntrancePointId = "loc1_entrance_service";
        private const string ServiceEntranceObjectName = "Barrier_Service";

        // Ids the main entrance had in earlier layouts (v1 barrier, v2 parking exit); it is found and renamed.
        private static readonly string[] OldMainEntrancePointIds = { "loc1_parking_exit", "loc1_barrier" };

        // Tokens in NodeSpec.Next that stand for the car spots of the points (they carry their own RoadNode).
        private const string SpotTokenPrefix = "<";
        private const string MainEntranceSpot = "<entrance_main>";
        private const string ServiceEntranceSpot = "<entrance_service>";

        private const string ZoneM = "Zone_M";
        private const string ZoneJ = "Zone_J";
        private const string ZoneT15 = "Zone_T-15";
        private const string ZoneT9 = "Zone_T-9";
        private const string ZoneT3 = "Zone_T-3";

        private static readonly SurfaceSpec[] Surfaces =
        {
            new SurfaceSpec("Surface_Road", new Vector3(0f, 0f, -21.5f), new Vector3(72f, 0.1f, 8f)),
            new SurfaceSpec("Surface_ServiceArea", new Vector3(-12f, 0f, -4f), new Vector3(26f, 0.1f, 29f)),
            new SurfaceSpec("Surface_TopRoad", new Vector3(5f, 0f, 8f), new Vector3(58f, 0.1f, 5f)),
            new SurfaceSpec("Surface_Entrance2Lane", new Vector3(11f, 0f, 3f), new Vector3(4f, 0.1f, 6f)),
            new SurfaceSpec("Surface_Parking", new Vector3(19f, 0f, -8.25f), new Vector3(20f, 0.1f, 18.5f)),
            new SurfaceSpec("Surface_ExitRoad", new Vector3(30f, 0f, -8f), new Vector3(5f, 0.1f, 34f)),
        };

        private static readonly ZoneSpec[] Zones =
        {
            new ZoneSpec(ZoneM, new Vector3(11f, 0f, -13f), new Color(1f, 0.55f, 0f)),
            new ZoneSpec(ZoneJ, new Vector3(30f, 0f, -3f), new Color(0.9f, 0.2f, 0.9f)),
            new ZoneSpec(ZoneT15, new Vector3(-15f, 0f, 8f), new Color(0.2f, 0.8f, 1f)),
            new ZoneSpec(ZoneT9, new Vector3(-9f, 0f, 8f), new Color(0.2f, 0.8f, 1f)),
            new ZoneSpec(ZoneT3, new Vector3(-3f, 0f, 8f), new Color(0.2f, 0.8f, 1f)),
        };

        // Why: on equally short routes the graph prefers earlier connections; layout v3.1 has no such ties
        // (LocationLayout.Validate reports a route that would drive through a spot or a parking slot).
        // Bay lanes: L{x}_B1 → L{x}_B0 → the bay's car spot → WX{x} → the top road ("m" = minus in the lane names).
        private static readonly NodeSpec[] Nodes =
        {
            new NodeSpec("N_Spawn", -34f, -19f, 90f, null, "Q3"),
            new NodeSpec("Q3", -27f, -19f, 90f, null, "Q2"),
            new NodeSpec("Q2", -21f, -19f, 90f, null, "Q1"),
            new NodeSpec("Q1", -15f, -19f, 90f, null, "Q0"),
            new NodeSpec("Q0", -9f, -19f, 90f, null, "F1", "R1"),
            new NodeSpec("F1", -3f, -16f, 0f, null, "SSm3"),
            new NodeSpec("SSm3", -3f, -13f, 270f, null, "Lm3_B1", "SSm9"),
            new NodeSpec("SSm9", -9f, -13f, 270f, null, "Lm9_B1", "SSm15"),
            new NodeSpec("SSm15", -15f, -13f, 270f, null, "Lm15_B1", "SSm21"),
            new NodeSpec("SSm21", -21f, -13f, 0f, null, "Lm21_B1"),
            new NodeSpec("Lm3_B1", -3f, -11f, 0f, null, "Lm3_B0"),
            new NodeSpec("Lm3_B0", -3f, -7f, 0f, null, BaySpot(-3f)),
            new NodeSpec("Lm9_B1", -9f, -11f, 0f, null, "Lm9_B0"),
            new NodeSpec("Lm9_B0", -9f, -7f, 0f, null, BaySpot(-9f)),
            new NodeSpec("Lm15_B1", -15f, -11f, 0f, null, "Lm15_B0"),
            new NodeSpec("Lm15_B0", -15f, -7f, 0f, null, BaySpot(-15f)),
            new NodeSpec("Lm21_B1", -21f, -11f, 0f, null, "Lm21_B0"),
            new NodeSpec("Lm21_B0", -21f, -7f, 0f, null, BaySpot(-21f)),
            new NodeSpec("WX-3", -3f, 4f, 0f, null, "T-3"),
            new NodeSpec("WX-9", -9f, 4f, 0f, null, "T-9"),
            new NodeSpec("WX-15", -15f, 4f, 0f, null, "T-15"),
            new NodeSpec("WX-21", -21f, 4f, 0f, null, "T-21"),
            new NodeSpec("T-21", -21f, 8f, 90f, null, "T-15"),
            new NodeSpec("T-15", -15f, 8f, 90f, ZoneT15, "T-9"),
            new NodeSpec("T-9", -9f, 8f, 90f, ZoneT9, "T-3"),
            new NodeSpec("T-3", -3f, 8f, 90f, ZoneT3, "T2"),
            new NodeSpec("T2", 11f, 8f, 90f, null, ServiceEntranceSpot, "T3"),
            new NodeSpec("T3", 30f, 8f, 180f, null, "D1"),
            new NodeSpec("D1", 30f, -3f, 180f, ZoneJ, "D2"),
            new NodeSpec("D2", 30f, -24f, 90f, null, "N_Exit"),
            new NodeSpec("N_Exit", 35f, -24f, 90f, null),
            new NodeSpec("R1", 0f, -19f, 90f, null, MainEntranceSpot),
            new NodeSpec("B1N", 11f, -16f, 0f, null, "KIN"),
            new NodeSpec("B2S", 11f, -1f, 180f, null, "KIN"),
            new NodeSpec("KIN", 11f, -13f, 90f, ZoneM, "K14"),
            new NodeSpec("K14", 14f, -13f, 90f, null, "P0", "K18"),
            new NodeSpec("K18", 18f, -13f, 90f, null, "P1", "K22"),
            new NodeSpec("K22", 22f, -13f, 90f, null, "P2", "K26"),
            new NodeSpec("K26", 26f, -13f, 0f, null, "P3"),
            new NodeSpec("P0", 14f, -8f, 0f, null, "X14"),
            new NodeSpec("P1", 18f, -8f, 0f, null, "X18"),
            new NodeSpec("P2", 22f, -8f, 0f, null, "X22"),
            new NodeSpec("P3", 26f, -8f, 0f, null, "X26"),
            new NodeSpec("X14", 14f, -3f, 90f, null, "X18"),
            new NodeSpec("X18", 18f, -3f, 90f, null, "X22"),
            new NodeSpec("X22", 22f, -3f, 90f, null, "X26"),
            new NodeSpec("X26", 26f, -3f, 90f, null, "D1"),
        };

        private const string SpawnNodeName = "N_Spawn";
        private const string ExitNodeName = "N_Exit";
        private static readonly string[] QueueSlotNames = { "Q0", "Q1", "Q2", "Q3" };
        private static readonly string[] ParkingSlotNames = { "P0", "P1", "P2", "P3" };

        // Row of drive-through bays (z = -2, facing north). The first one is the existing wash; the others are its copies,
        // built later through their plots (ids = Buildable ids, created by "Create Location 1 Configs").
        private const float BayRowZ = -2f;
        private const float BaySpotYaw = 0f;
        private static readonly BaySpec[] Bays =
        {
            new BaySpec(-3f, WashPointId, "wash", null),
            new BaySpec(-9f, "loc1_wash_2", "wash", "loc1_build_wash_2"),
            new BaySpec(-15f, "loc1_oil", "oil", "loc1_build_oil"),
            new BaySpec(-21f, "loc1_tires", "tires", "loc1_build_tires"),
        };

        // Parking slots that are bought later: index into ParkingSlotNames → plot id.
        private static readonly ParkingPlotSpec[] ParkingPlots =
        {
            new ParkingPlotSpec(2, "loc1_build_parking_3"),
            new ParkingPlotSpec(3, "loc1_build_parking_4"),
        };

        private static readonly Vector3 ParkingGhostScale = new Vector3(2.2f, 0.05f, 4.4f);

        // Why: the pad's bottom sits above Surface_Parking (top at y = 0.05), and the clickable volume rises above the
        // road — a flat ghost level with the asphalt would lose the pointer ray to the road's collider.
        private const float ParkingPadBottom = 0.06f;
        private static readonly Vector3 ParkingColliderCenter = new Vector3(0f, 0.25f, 0f);
        private static readonly Vector3 ParkingColliderSize = new Vector3(2.2f, 0.5f, 4.4f);
        private const float RoadTop = 0.05f;
        private static readonly Vector3 ParkingApproachOffset = new Vector3(0f, 0f, -2f);

        // Ghost look and world-space price tag / dwell ring.
        private const string GhostMaterialPath = "Assets/_Project/Materials/M_Ghost.mat";
        private static readonly Color GhostColor = new Color(1f, 1f, 1f, 0.35f);
        private const float TagHeightAboveGhost = 0.8f;
        private const float ParkingTagHeight = 1.5f;
        private const float DefaultCanvasScale = 0.01f;
        private static readonly Vector2 TagCanvasSize = new Vector2(320f, 180f);
        private const float PriceTagFontSize = 40f;
        private const float RingSize = 90f;
        private const string RingSpritePath = "UI/Skin/Knob.psd";

        // Parking entrances: post, car spot (the car waits in front of the arm), work spot (facing east) and the arm.
        // Barrier 1 stands beside the road: its arm reaches south across the queue lane and lifts around X.
        // Barrier 2 stands beside the service lane: its arm reaches east across it and lifts around Z.
        // Why +80 for both: rotating (0,0,-1) by +80° around +X and (1,0,0) by +80° around +Z both raise the tip (y > 0).
        private static readonly BarrierSpec MainEntrance = new BarrierSpec(
            MainEntrancePointId, new Vector3(9f, 0.5f, -17f), new Vector3(5f, 0f, -19f), 90f, new Vector3(7.5f, 0f, -17f), "B1N",
            new Vector3(0f, 0f, -2f), new Vector3(0.15f, 0.15f, 4f), Vector3.right, 80f);

        private static readonly BarrierSpec ServiceEntrance = new BarrierSpec(
            ServiceEntrancePointId, new Vector3(9f, 0.5f, 2.5f), new Vector3(11f, 0f, 5.5f), 180f, new Vector3(7.5f, 0f, 3f), "B2S",
            new Vector3(2f, 0f, 0f), new Vector3(4f, 0.15f, 0.15f), Vector3.forward, 80f);

        private const float WorkSpotYaw = 90f;
        private const string ArmPivotName = "ArmPivot";
        private const string ArmName = "Arm";
        private static readonly Vector3 PostScale = new Vector3(0.5f, 1f, 0.5f);
        private static readonly Vector3 PivotLocalPosition = new Vector3(0f, 0.5f, 0f);
        private static readonly Vector3 PivotLocalScale = new Vector3(2f, 1f, 2f); // undoes the post's scale

        // ──────────────────────────────────────────────────────────────────────────────────────────────────────────────

        [MenuItem("AutoService/Whitebox/Build Location 1 Roads")]
        private static void BuildLocation1Roads()
        {
            LocationLayout layout = FindLayout();
            if (layout == null)
            {
                Debug.LogError("[Whitebox] Select the LocationLayout object (or name it '" + LocationObjectName + "') and run again.");
                return;
            }

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Build Location 1 Roads");

            RemovePreviousRun(layout);

            GameObject root = CreateChild(RootName, layout.transform);
            Undo.RegisterCreatedObjectUndo(root, "Create " + RootName);

            BuildSurfaces(root.transform);
            Dictionary<string, TrafficZone> zones = BuildZones(root.transform);
            Dictionary<string, RoadNode> nodes = BuildNodes(root.transform, zones);
            int problems = 0;

            var bays = new List<ServicePointView>();
            var plots = new List<BuildPlotView>();
            ServicePointView wash = FindPoint(layout, WashPointId);
            if (wash != null && wash.CarSpot != null)
            {
                Material ghostMaterial = GetOrCreateGhostMaterial();
                PlaceBays(wash, nodes, ghostMaterial, bays, plots);
                PlaceParkingGhosts(layout.transform, nodes, ghostMaterial, plots);
            }
            else
            {
                Debug.LogError("[Whitebox] Wash point '" + WashPointId + "' (with a Car Spot) not found; no bays or ghosts placed.");
                problems++;
            }

            ServicePointView main = FindMainEntrance(layout);
            ServicePointView service = null;
            if (main != null && main.CarSpot != null)
            {
                nodes[MainEntranceSpot] = PlaceBarrier(main, MainEntrance, nodes);
                service = FindPoint(layout, ServiceEntrancePointId) ?? CloneBarrier(main);
                nodes[ServiceEntranceSpot] = PlaceBarrier(service, ServiceEntrance, nodes);
            }
            else
            {
                Debug.LogError("[Whitebox] Main entrance '" + MainEntrancePointId + "' (or its older ids '"
                    + string.Join("', '", OldMainEntrancePointIds) + "') with a Car Spot not found; no parking entrances placed.");
                main = null;
                problems++;
            }

            LinkNodes(nodes);
            FillLayout(layout, nodes, main, service, bays, plots);

            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(layout.gameObject.scene);
            Selection.activeGameObject = root;

            // Why: no LocationLayout.Validate here — it caches the graph on the instance, and a second run of this tool
            // would then validate against the destroyed nodes. GameplayEntryPoint validates the layout on Play.
            Debug.Log("[Whitebox] Location 1 roads v3.1 built: " + Nodes.Length + " nodes, " + Zones.Length + " zones, "
                + bays.Count + " bays, " + plots.Count + " build plots"
                + (problems == 0 ? ". Bake both NavMesh surfaces, then press Play: the layout is validated on start."
                    : ", " + problems + " problem(s) above."), root);
        }

        private static LocationLayout FindLayout()
        {
            GameObject selected = Selection.activeGameObject;
            if (selected != null)
            {
                LocationLayout fromSelection = selected.GetComponentInParent<LocationLayout>();
                if (fromSelection != null)
                {
                    return fromSelection;
                }
            }

            LocationLayout[] layouts = Object.FindObjectsByType<LocationLayout>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < layouts.Length; i++)
            {
                if (layouts[i].name == LocationObjectName)
                {
                    return layouts[i];
                }
            }

            return null;
        }

        // Why: the road roots of older runs and every copied bay / ghost are replaced, so a re-run never stacks duplicates.
        private static void RemovePreviousRun(LocationLayout layout)
        {
            Transform location = layout.transform;
            for (int i = location.childCount - 1; i >= 0; i--)
            {
                Transform child = location.GetChild(i);
                if (child.name.StartsWith(ObsoleteRootPrefix) || child.name == ObsoleteRootV2)
                {
                    Undo.DestroyObjectImmediate(child.gameObject);
                }
            }

            WhiteboxGenerated[] generated = layout.GetComponentsInChildren<WhiteboxGenerated>(true);
            for (int i = 0; i < generated.Length; i++)
            {
                // Why: a generated object may sit inside another one that was destroyed a step earlier.
                if (generated[i] != null)
                {
                    Undo.DestroyObjectImmediate(generated[i].gameObject);
                }
            }
        }

        private static ServicePointView FindMainEntrance(LocationLayout layout)
        {
            ServicePointView main = FindPoint(layout, MainEntrancePointId);
            for (int i = 0; main == null && i < OldMainEntrancePointIds.Length; i++)
            {
                main = FindPoint(layout, OldMainEntrancePointIds[i]);
            }

            return main;
        }

        private static ServicePointView FindPoint(LocationLayout layout, string pointId)
        {
            ServicePointView[] points = layout.GetComponentsInChildren<ServicePointView>(true);
            for (int i = 0; i < points.Length; i++)
            {
                if (points[i].PointId == pointId)
                {
                    return points[i];
                }
            }

            return null;
        }

        private static void BuildSurfaces(Transform root)
        {
            int roadLayer = LayerMask.NameToLayer(RoadLayerName);
            if (roadLayer < 0)
            {
                Debug.LogWarning("[Whitebox] Layer '" + RoadLayerName + "' does not exist; surfaces stay on Default and the car NavMesh will miss them.");
                roadLayer = 0;
            }

            Transform group = CreateChild("Surfaces", root).transform;
            for (int i = 0; i < Surfaces.Length; i++)
            {
                SurfaceSpec spec = Surfaces[i];
                GameObject surface = GameObject.CreatePrimitive(PrimitiveType.Cube);
                surface.name = spec.Name;
                surface.layer = roadLayer;
                surface.transform.SetParent(group, false);
                surface.transform.SetPositionAndRotation(spec.Position, Quaternion.identity);
                surface.transform.localScale = spec.Scale;
            }
        }

        private static Dictionary<string, TrafficZone> BuildZones(Transform root)
        {
            var zones = new Dictionary<string, TrafficZone>();
            Transform group = CreateChild("Zones", root).transform;
            for (int i = 0; i < Zones.Length; i++)
            {
                ZoneSpec spec = Zones[i];
                GameObject zoneObject = CreateChild(spec.Name, group);
                zoneObject.transform.position = spec.Position;
                var zone = zoneObject.AddComponent<TrafficZone>();
                var serialized = new SerializedObject(zone);
                serialized.FindProperty("_gizmoColor").colorValue = spec.Color;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                zones.Add(spec.Name, zone);
            }

            return zones;
        }

        private static Dictionary<string, RoadNode> BuildNodes(Transform root, Dictionary<string, TrafficZone> zones)
        {
            var nodes = new Dictionary<string, RoadNode>();
            Transform group = CreateChild("Nodes", root).transform;
            for (int i = 0; i < Nodes.Length; i++)
            {
                NodeSpec spec = Nodes[i];
                GameObject nodeObject = CreateChild(spec.Name, group);
                nodeObject.transform.SetPositionAndRotation(spec.Position, Quaternion.Euler(0f, spec.Yaw, 0f));
                var node = nodeObject.AddComponent<RoadNode>();
                if (spec.Zone != null)
                {
                    var serialized = new SerializedObject(node);
                    serialized.FindProperty("_zone").objectReferenceValue = zones[spec.Zone];
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }

                nodes.Add(spec.Name, node);
            }

            return nodes;
        }

        /// <summary>
        /// Places the wash as the first bay of the row, then copies it into the other bays (inactive: their plots' targets)
        /// and creates a ghost for each of them.
        /// </summary>
        private static void PlaceBays(
            ServicePointView wash,
            Dictionary<string, RoadNode> nodes,
            Material ghostMaterial,
            List<ServicePointView> bays,
            List<BuildPlotView> plots)
        {
            BaySpec first = Bays[0];
            PlaceWash(wash, first, nodes);
            nodes[BaySpot(first.X)] = GetOrAddNode(wash.CarSpot.gameObject);
            bays.Add(wash);

            for (int i = 1; i < Bays.Length; i++)
            {
                BaySpec spec = Bays[i];
                Vector3 shift = Vector3.right * (spec.X - first.X);
                ServicePointView bay = CloneBay(wash, spec, shift, nodes);
                nodes[BaySpot(spec.X)] = GetOrAddNode(bay.CarSpot.gameObject);
                bays.Add(bay);
                plots.Add(CreateBayGhost(wash, bay, spec, shift, ghostMaterial));

                // Why: the bay is the plot's target — it only appears (and registers as a point) once built.
                bay.gameObject.SetActive(false);
            }
        }

        private static void PlaceWash(ServicePointView wash, BaySpec spec, Dictionary<string, RoadNode> nodes)
        {
            Transform root = wash.transform;
            Transform spot = wash.CarSpot;
            Undo.RecordObject(root, "Move wash");

            // Why: the root is turned so that the car spot (wherever it sits inside the bay) faces the driving direction.
            Quaternion spotInRoot = Quaternion.Inverse(root.rotation) * spot.rotation;
            root.rotation = Quaternion.Euler(0f, BaySpotYaw, 0f) * Quaternion.Inverse(spotInRoot);
            Vector3 offset = new Vector3(spec.X, 0f, BayRowZ) - spot.position;
            offset.y = 0f;
            root.position += offset;

            ConfigureBay(wash, spec, nodes);
        }

        // Why: a copy of the wash gets the same roof, pillars, HUD and click setup; only ids, lane and position differ.
        private static ServicePointView CloneBay(ServicePointView wash, BaySpec spec, Vector3 shift, Dictionary<string, RoadNode> nodes)
        {
            Transform source = wash.transform;
            GameObject copy = Object.Instantiate(source.gameObject, source.position + shift, source.rotation, source.parent);
            copy.name = "Bay_" + spec.PointId;
            copy.AddComponent<WhiteboxGenerated>();
            Undo.RegisterCreatedObjectUndo(copy, "Create bay " + spec.PointId);

            var bay = copy.GetComponent<ServicePointView>();
            ConfigureBay(bay, spec, nodes);
            return bay;
        }

        private static void ConfigureBay(ServicePointView bay, BaySpec spec, Dictionary<string, RoadNode> nodes)
        {
            var view = new SerializedObject(bay);
            view.FindProperty("_pointId").stringValue = spec.PointId;
            view.FindProperty("_serviceTypeId").stringValue = spec.ServiceTypeId;
            SetArray(view.FindProperty("_bufferSlots"), Lookup(nodes, new[] { LaneNode(spec.X, 0), LaneNode(spec.X, 1) }));
            view.ApplyModifiedProperties();

            SetNext(GetOrAddNode(bay.CarSpot.gameObject), new[] { ExitNode(spec.X) }, nodes);
        }

        /// <summary>Translucent copy of the bay (renderers only) with a clickable collider, price tag, ring and plot view.</summary>
        private static BuildPlotView CreateBayGhost(ServicePointView wash, ServicePointView bay, BaySpec spec, Vector3 shift, Material material)
        {
            Transform bayRoot = bay.transform;
            GameObject ghostRoot = CreateChild("Ghost_" + spec.PointId, bayRoot.parent);
            ghostRoot.transform.SetPositionAndRotation(bayRoot.position, bayRoot.rotation);
            ghostRoot.AddComponent<WhiteboxGenerated>();

            Transform source = wash.transform;
            GameObject visual = Object.Instantiate(source.gameObject, source.position + shift, source.rotation, ghostRoot.transform);
            visual.name = "Visual";
            StripToRenderers(visual);
            Bounds bounds = ApplyGhostLook(visual, material);
            AddGhostCollider(visual, bounds);

            Transform approach = CreateChild("ApproachPoint", ghostRoot.transform).transform;
            Transform workSpot = ApproachOf(bay);
            approach.SetPositionAndRotation(workSpot.position, workSpot.rotation);

            Transform anchor = CreateChild("PanelAnchor", ghostRoot.transform).transform;
            anchor.position = new Vector3(bounds.center.x, bounds.max.y, bounds.center.z);

            Vector3 tagPosition = new Vector3(bounds.center.x, bounds.max.y + TagHeightAboveGhost, bounds.center.z);
            GhostTag ghostTag = CreateTag(visual.transform, tagPosition, wash.Hud, spec.PlotId);

            BuildPlotView plot = AddPlotView(ghostRoot, spec.PlotId, visual, bay.gameObject, approach, anchor, ghostTag);
            Undo.RegisterCreatedObjectUndo(ghostRoot, "Create ghost " + spec.PointId);
            return plot;
        }

        private static void PlaceParkingGhosts(Transform parent, Dictionary<string, RoadNode> nodes, Material material, List<BuildPlotView> plots)
        {
            for (int i = 0; i < ParkingPlots.Length; i++)
            {
                ParkingPlotSpec spec = ParkingPlots[i];
                string slotName = ParkingSlotNames[spec.SlotIndex];
                Transform slot = nodes[slotName].transform;

                GameObject ghostRoot = CreateChild("Ghost_" + slotName, parent);
                ghostRoot.transform.SetPositionAndRotation(slot.position, slot.rotation);
                ghostRoot.AddComponent<WhiteboxGenerated>();

                // Why: the pad is scaled, the visual is not — a canvas under a non-uniformly scaled parent would be sheared.
                GameObject visual = CreateChild("Visual", ghostRoot.transform);
                GameObject pad = GameObject.CreatePrimitive(PrimitiveType.Cube);
                pad.name = "Pad";
                pad.transform.SetParent(visual.transform, false);
                pad.transform.localPosition = Vector3.up * (ParkingPadBottom + ParkingGhostScale.y * 0.5f);
                pad.transform.localScale = ParkingGhostScale;
                Object.DestroyImmediate(pad.GetComponent<Collider>());
                ApplyGhostLook(pad, material);

                // Why: on the unscaled Visual (it sits exactly on the ghost root) rather than the root itself, so the
                // collider is switched off together with the ghost once the slot is built and stops catching clicks.
                var box = visual.AddComponent<BoxCollider>();
                box.center = ParkingColliderCenter;
                box.size = ParkingColliderSize;
                SetInteractableLayer(visual);

                // Why: south of the slot, facing it (north) — where a player would stand to look at the bare asphalt.
                Transform approach = CreateChild("ApproachPoint", ghostRoot.transform).transform;
                approach.SetPositionAndRotation(slot.position + ParkingApproachOffset, Quaternion.LookRotation(Vector3.forward));

                Transform anchor = CreateChild("PanelAnchor", ghostRoot.transform).transform;
                anchor.position = slot.position + Vector3.up;

                GhostTag ghostTag = CreateTag(visual.transform, slot.position + Vector3.up * ParkingTagHeight, null, spec.PlotId);
                plots.Add(AddPlotView(ghostRoot, spec.PlotId, visual, null, approach, anchor, ghostTag));
                Undo.RegisterCreatedObjectUndo(ghostRoot, "Create ghost " + slotName);
            }
        }

        // Why: a ghost must not act — no point, node, HUD or collider of the copied bay may survive in it.
        private static void StripToRenderers(GameObject copy)
        {
            Canvas[] canvases = copy.GetComponentsInChildren<Canvas>(true);
            for (int i = 0; i < canvases.Length; i++)
            {
                if (canvases[i] != null)
                {
                    Object.DestroyImmediate(canvases[i].gameObject);
                }
            }

            MonoBehaviour[] scripts = copy.GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < scripts.Length; i++)
            {
                Object.DestroyImmediate(scripts[i]);
            }

            Collider[] colliders = copy.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                Object.DestroyImmediate(colliders[i]);
            }
        }

        /// <returns>World bounds of the ghost's renderers.</returns>
        private static Bounds ApplyGhostLook(GameObject visual, Material material)
        {
            Renderer[] renderers = visual.GetComponentsInChildren<Renderer>(true);
            var bounds = new Bounds(visual.transform.position, Vector3.zero);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer target = renderers[i];
                var materials = new Material[target.sharedMaterials.Length];
                for (int m = 0; m < materials.Length; m++)
                {
                    materials[m] = material;
                }

                target.sharedMaterials = materials;
                target.shadowCastingMode = ShadowCastingMode.Off;
                if (i == 0)
                {
                    bounds = target.bounds;
                }
                else
                {
                    bounds.Encapsulate(target.bounds);
                }
            }

            return bounds;
        }

        private static void AddGhostCollider(GameObject visual, Bounds worldBounds)
        {
            // Why: the clickable volume starts above the road surface, so the road's collider never wins the pointer ray.
            float bottom = Mathf.Max(worldBounds.min.y, RoadTop + 0.01f);
            worldBounds.SetMinMax(new Vector3(worldBounds.min.x, bottom, worldBounds.min.z),
                new Vector3(worldBounds.max.x, Mathf.Max(worldBounds.max.y, bottom + 0.5f), worldBounds.max.z));

            var box = visual.AddComponent<BoxCollider>();
            Transform transform = visual.transform;
            box.center = transform.InverseTransformPoint(worldBounds.center);
            Vector3 size = transform.InverseTransformVector(worldBounds.size);
            box.size = new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z));
            SetInteractableLayer(visual);
        }

        private static void SetInteractableLayer(GameObject target)
        {
            int layer = LayerMask.NameToLayer(InteractableLayerName);
            if (layer < 0)
            {
                Debug.LogWarning("[Whitebox] Layer '" + InteractableLayerName + "' does not exist; ghosts cannot be clicked.");
                return;
            }

            target.layer = layer;
        }

        /// <summary>World-space canvas above the ghost: price tag on top, dwell ring below it.</summary>
        /// <param name="parent">Ghost visual (the tag hides with it).</param>
        /// <param name="position">World position of the canvas centre.</param>
        /// <param name="referenceHud">HUD of the wash, whose tilt and scale the tag copies so it faces the camera the same way.</param>
        /// <param name="placeholder">Initial text (the game writes "name · price" on start).</param>
        private static GhostTag CreateTag(Transform parent, Vector3 position, ServicePointHud referenceHud, string placeholder)
        {
            var canvasObject = new GameObject("Tag", typeof(RectTransform), typeof(Canvas));
            canvasObject.transform.SetParent(parent, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;

            Transform reference = referenceHud != null ? referenceHud.transform : null;
            var rect = (RectTransform)canvasObject.transform;
            rect.sizeDelta = TagCanvasSize;
            rect.SetPositionAndRotation(position, reference != null ? reference.rotation : Quaternion.identity);
            float scale = reference != null ? reference.lossyScale.x : DefaultCanvasScale;
            Vector3 parentScale = parent.lossyScale;
            rect.localScale = new Vector3(scale / parentScale.x, scale / parentScale.y, scale / parentScale.z);

            var labelObject = new GameObject("PriceTag", typeof(RectTransform));
            labelObject.transform.SetParent(canvasObject.transform, false);
            var labelRect = (RectTransform)labelObject.transform;
            labelRect.anchoredPosition = new Vector2(0f, TagCanvasSize.y * 0.25f);
            labelRect.sizeDelta = new Vector2(TagCanvasSize.x, TagCanvasSize.y * 0.5f);
            var label = labelObject.AddComponent<TextMeshProUGUI>();
            label.text = placeholder;
            label.fontSize = PriceTagFontSize;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;
            label.raycastTarget = false;

            var ringObject = new GameObject("Ring", typeof(RectTransform));
            ringObject.transform.SetParent(canvasObject.transform, false);
            var ringRect = (RectTransform)ringObject.transform;
            ringRect.anchoredPosition = new Vector2(0f, -TagCanvasSize.y * 0.25f);
            ringRect.sizeDelta = new Vector2(RingSize, RingSize);
            var ring = ringObject.AddComponent<Image>();
            ring.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>(RingSpritePath);
            ring.type = Image.Type.Filled;
            ring.fillMethod = Image.FillMethod.Radial360;
            ring.fillOrigin = (int)Image.Origin360.Top;
            ring.fillClockwise = true;
            ring.fillAmount = 0f;
            ring.raycastTarget = false;

            var ringView = canvasObject.AddComponent<DwellRingView>();
            var serialized = new SerializedObject(ringView);
            serialized.FindProperty("_fill").objectReferenceValue = ring;
            serialized.FindProperty("_root").objectReferenceValue = ringObject;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            return new GhostTag(label, ringView);
        }

        private static BuildPlotView AddPlotView(
            GameObject ghostRoot,
            string plotId,
            GameObject visual,
            GameObject target,
            Transform approach,
            Transform anchor,
            GhostTag ghostTag)
        {
            var highlight = ghostRoot.AddComponent<InteractableHighlight>();
            var highlightObject = new SerializedObject(highlight);
            Renderer[] renderers = visual.GetComponentsInChildren<Renderer>(true);
            SetArray(highlightObject.FindProperty("_renderers"), new List<Renderer>(renderers));
            highlightObject.ApplyModifiedPropertiesWithoutUndo();

            var plot = ghostRoot.AddComponent<BuildPlotView>();
            var serialized = new SerializedObject(plot);
            serialized.FindProperty("_plotId").stringValue = plotId;
            serialized.FindProperty("_ghost").objectReferenceValue = visual;
            serialized.FindProperty("_target").objectReferenceValue = target;
            serialized.FindProperty("_approachPoint").objectReferenceValue = approach;
            serialized.FindProperty("_panelAnchor").objectReferenceValue = anchor;
            serialized.FindProperty("_highlight").objectReferenceValue = highlight;
            serialized.FindProperty("_ring").objectReferenceValue = ghostTag.Ring;
            serialized.FindProperty("_priceTag").objectReferenceValue = ghostTag.Label;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return plot;
        }

        private static Transform ApproachOf(ServicePointView point)
        {
            var view = new SerializedObject(point);
            return view.FindProperty("_approachPoint").objectReferenceValue is Transform work ? work : point.transform;
        }

        private static Material GetOrCreateGhostMaterial()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(GhostMaterialPath);
            if (existing != null)
            {
                return existing;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                Debug.LogWarning("[Whitebox] URP Lit shader not found; M_Ghost uses the default shader and will not be transparent.");
                shader = Shader.Find("Standard");
            }

            // Why: the same switches the URP material inspector sets for Surface Type = Transparent, Blend = Alpha.
            var material = new Material(shader) { name = "M_Ghost" };
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;
            material.SetColor("_BaseColor", GhostColor);
            AssetDatabase.CreateAsset(material, GhostMaterialPath);
            return material;
        }

        // Why: the second entrance is a copy of the first, so it gets the same post, arm, HUD and click setup.
        private static ServicePointView CloneBarrier(ServicePointView source)
        {
            GameObject copy = Object.Instantiate(source.gameObject, source.transform.parent);
            copy.name = ServiceEntranceObjectName;
            Undo.RegisterCreatedObjectUndo(copy, "Create service entrance");
            return copy.GetComponent<ServicePointView>();
        }

        /// <returns>The road node on the barrier's car spot.</returns>
        private static RoadNode PlaceBarrier(ServicePointView barrier, BarrierSpec spec, Dictionary<string, RoadNode> nodes)
        {
            var view = new SerializedObject(barrier);
            view.FindProperty("_pointId").stringValue = spec.PointId;
            view.ApplyModifiedProperties();

            Transform post = barrier.transform;
            Undo.RecordObject(post, "Move " + spec.PointId);
            post.SetPositionAndRotation(spec.PostPosition, Quaternion.identity);
            post.localScale = PostScale;

            Transform spot = barrier.CarSpot;
            Undo.RecordObject(spot, "Move " + spec.PointId + " car spot");
            spot.SetPositionAndRotation(spec.SpotPosition, Quaternion.Euler(0f, spec.SpotYaw, 0f));

            if (view.FindProperty("_approachPoint").objectReferenceValue is Transform work)
            {
                Undo.RecordObject(work, "Move " + spec.PointId + " work spot");
                work.SetPositionAndRotation(spec.WorkSpotPosition, Quaternion.Euler(0f, WorkSpotYaw, 0f));
            }
            else
            {
                Debug.LogWarning("[Whitebox] Barrier '" + barrier.name + "' has no Approach Point; the worker stands at the post.", barrier);
            }

            Renderer arm = RebuildArm(barrier, spec);
            HighlightArm(barrier, arm);

            RoadNode node = GetOrAddNode(spot.gameObject);
            SetNext(node, new[] { spec.Next }, nodes);
            return node;
        }

        // Why: the builder owns the arm geometry — rebuilding it every run beats patching whatever an older layout left.
        /// <returns>The renderer of the new arm.</returns>
        private static Renderer RebuildArm(ServicePointView barrier, BarrierSpec spec)
        {
            Transform post = barrier.transform;
            for (int i = post.childCount - 1; i >= 0; i--)
            {
                Transform child = post.GetChild(i);
                if (child.name == ArmPivotName || child.name == ArmName)
                {
                    Undo.DestroyObjectImmediate(child.gameObject);
                }
            }

            GameObject pivot = CreateChild(ArmPivotName, post);
            pivot.transform.localPosition = PivotLocalPosition;
            pivot.transform.localScale = PivotLocalScale;
            Undo.RegisterCreatedObjectUndo(pivot, "Create barrier arm");

            GameObject arm = GameObject.CreatePrimitive(PrimitiveType.Cube);
            arm.name = ArmName;

            // Why: a visual only — a collider would catch clicks meant for the barrier and block the player's NavMesh.
            Object.DestroyImmediate(arm.GetComponent<Collider>());
            arm.transform.SetParent(pivot.transform, false);
            arm.transform.localPosition = spec.ArmLocalPosition;
            arm.transform.localScale = spec.ArmScale;

            BarrierArm barrierArm = barrier.TryGetComponent(out BarrierArm existing) ? existing : Undo.AddComponent<BarrierArm>(barrier.gameObject);
            var serialized = new SerializedObject(barrierArm);
            serialized.FindProperty("_arm").objectReferenceValue = pivot.transform;
            serialized.FindProperty("_openAxis").vector3Value = spec.OpenAxis;
            serialized.FindProperty("_openAngle").floatValue = spec.OpenAngle;
            serialized.ApplyModifiedProperties();
            return arm.GetComponent<Renderer>();
        }

        // Why (TDD K3): the old arm was destroyed, so its slot in the highlight list is empty — the new arm takes its place.
        private static void HighlightArm(ServicePointView barrier, Renderer arm)
        {
            if (!barrier.TryGetComponent(out InteractableHighlight highlight))
            {
                return;
            }

            var serialized = new SerializedObject(highlight);
            SerializedProperty renderers = serialized.FindProperty("_renderers");
            var kept = new List<Renderer>(renderers.arraySize + 1);
            for (int i = 0; i < renderers.arraySize; i++)
            {
                if (renderers.GetArrayElementAtIndex(i).objectReferenceValue is Renderer renderer && renderer != null && renderer != arm)
                {
                    kept.Add(renderer);
                }
            }

            kept.Add(arm);
            SetArray(renderers, kept);
            serialized.ApplyModifiedProperties();
        }

        private static void LinkNodes(Dictionary<string, RoadNode> nodes)
        {
            for (int i = 0; i < Nodes.Length; i++)
            {
                SetNext(nodes[Nodes[i].Name], Nodes[i].Next, nodes);
            }
        }

        private static void SetNext(RoadNode node, string[] next, Dictionary<string, RoadNode> nodes)
        {
            var targets = new List<RoadNode>(next.Length);
            for (int i = 0; i < next.Length; i++)
            {
                if (nodes.TryGetValue(next[i], out RoadNode target))
                {
                    targets.Add(target);
                }
                else if (!next[i].StartsWith(SpotTokenPrefix))
                {
                    Debug.LogError("[Whitebox] Node table: '" + node.name + "' links to unknown node '" + next[i] + "'.");
                }
            }

            var serialized = new SerializedObject(node);
            SetArray(serialized.FindProperty("_next"), targets);
            serialized.ApplyModifiedProperties();
        }

        private static void FillLayout(
            LocationLayout layout,
            Dictionary<string, RoadNode> nodes,
            ServicePointView main,
            ServicePointView service,
            List<ServicePointView> bays,
            List<BuildPlotView> plots)
        {
            var serialized = new SerializedObject(layout);
            serialized.FindProperty("_spawnNode").objectReferenceValue = nodes[SpawnNodeName];
            serialized.FindProperty("_exitNode").objectReferenceValue = nodes[ExitNodeName];
            SetArray(serialized.FindProperty("_queueSlots"), Lookup(nodes, QueueSlotNames));
            SetArray(serialized.FindProperty("_parkingSlots"), Lookup(nodes, ParkingSlotNames));
            if (main != null)
            {
                serialized.FindProperty("_mainEntrance").objectReferenceValue = main;
                serialized.FindProperty("_serviceEntrance").objectReferenceValue = service;
            }

            if (bays.Count > 0)
            {
                SetArray(serialized.FindProperty("_servicePoints"), bays);
            }

            SetArray(serialized.FindProperty("_buildPlots"), plots);
            serialized.ApplyModifiedProperties();
        }

        private static List<RoadNode> Lookup(Dictionary<string, RoadNode> nodes, string[] names)
        {
            var result = new List<RoadNode>(names.Length);
            for (int i = 0; i < names.Length; i++)
            {
                result.Add(nodes[names[i]]);
            }

            return result;
        }

        private static void SetArray<T>(SerializedProperty array, List<T> values) where T : Object
        {
            array.arraySize = values.Count;
            for (int i = 0; i < values.Count; i++)
            {
                array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }
        }

        private static RoadNode GetOrAddNode(GameObject spot)
        {
            return spot.TryGetComponent(out RoadNode node) ? node : Undo.AddComponent<RoadNode>(spot);
        }

        private static GameObject CreateChild(string name, Transform parent)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent, false);
            return child;
        }

        // Node names of the bay at x (x is negative: -3 → "m3").
        private static string LaneNode(float x, int slot) => "Lm" + Mathf.RoundToInt(-x) + "_B" + slot;

        private static string ExitNode(float x) => "WX" + Mathf.RoundToInt(x);

        private static string BaySpot(float x) => "<bay" + Mathf.RoundToInt(x) + ">";

        private readonly struct SurfaceSpec
        {
            public SurfaceSpec(string name, Vector3 position, Vector3 scale)
            {
                Name = name;
                Position = position;
                Scale = scale;
            }

            public string Name { get; }

            public Vector3 Position { get; }

            public Vector3 Scale { get; }
        }

        private readonly struct ZoneSpec
        {
            public ZoneSpec(string name, Vector3 position, Color color)
            {
                Name = name;
                Position = position;
                Color = color;
            }

            public string Name { get; }

            public Vector3 Position { get; }

            public Color Color { get; }
        }

        private readonly struct BaySpec
        {
            public BaySpec(float x, string pointId, string serviceTypeId, string plotId)
            {
                X = x;
                PointId = pointId;
                ServiceTypeId = serviceTypeId;
                PlotId = plotId;
            }

            /// <summary>X of the bay's car spot (and of its lane).</summary>
            public float X { get; }

            public string PointId { get; }

            public string ServiceTypeId { get; }

            /// <summary>Buildable id of the bay's plot, or null for a bay built from the start.</summary>
            public string PlotId { get; }
        }

        private readonly struct ParkingPlotSpec
        {
            public ParkingPlotSpec(int slotIndex, string plotId)
            {
                SlotIndex = slotIndex;
                PlotId = plotId;
            }

            public int SlotIndex { get; }

            public string PlotId { get; }
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

        private readonly struct BarrierSpec
        {
            public BarrierSpec(
                string pointId,
                Vector3 postPosition,
                Vector3 spotPosition,
                float spotYaw,
                Vector3 workSpotPosition,
                string next,
                Vector3 armLocalPosition,
                Vector3 armScale,
                Vector3 openAxis,
                float openAngle)
            {
                ArmLocalPosition = armLocalPosition;
                ArmScale = armScale;
                OpenAxis = openAxis;
                OpenAngle = openAngle;
                PointId = pointId;
                PostPosition = postPosition;
                SpotPosition = spotPosition;
                SpotYaw = spotYaw;
                WorkSpotPosition = workSpotPosition;
                Next = next;
            }

            public string PointId { get; }

            public Vector3 PostPosition { get; }

            public Vector3 SpotPosition { get; }

            /// <summary>Heading of the car at the barrier in degrees around Y (0 = north/+Z, 180 = south).</summary>
            public float SpotYaw { get; }

            public Vector3 WorkSpotPosition { get; }

            /// <summary>Node the car drives to after paying.</summary>
            public string Next { get; }

            /// <summary>Arm cube position relative to the pivot on top of the post (its centre: half the arm's length out).</summary>
            public Vector3 ArmLocalPosition { get; }

            public Vector3 ArmScale { get; }

            /// <summary>Local axis of the pivot the arm lifts around (<c>BarrierArm._openAxis</c>).</summary>
            public Vector3 OpenAxis { get; }

            /// <summary>Opening angle in degrees (<c>BarrierArm._openAngle</c>); positive lifts the arm for the axes above.</summary>
            public float OpenAngle { get; }
        }

        private readonly struct NodeSpec
        {
            public NodeSpec(string name, float x, float z, float yaw, string zone, params string[] next)
            {
                Name = name;
                Position = new Vector3(x, 0f, z);
                Yaw = yaw;
                Zone = zone;
                Next = next;
            }

            public string Name { get; }

            public Vector3 Position { get; }

            /// <summary>Driving direction in degrees around Y (0 = north/+Z, 90 = east/+X).</summary>
            public float Yaw { get; }

            /// <summary>Merge zone name, or null.</summary>
            public string Zone { get; }

            /// <summary>Names of the next nodes (or the car spot tokens), in preference order.</summary>
            public string[] Next { get; }
        }
    }
}
