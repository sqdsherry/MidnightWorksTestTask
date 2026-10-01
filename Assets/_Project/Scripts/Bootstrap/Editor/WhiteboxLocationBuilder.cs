using System.Collections.Generic;
using AutoService.Presentation.Points;
using AutoService.Presentation.Traffic;
using AutoService.Presentation.Traffic.Routing;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AutoService.Bootstrap.Editor
{
    /// <summary>
    /// Editor tool: builds the whitebox road layout v3 of location 1 (GDD §3) under its <see cref="LocationLayout"/> —
    /// road surfaces, the one-way <see cref="RoadNode"/> graph and the merge zones — places the wash and both parking
    /// entrances (creating the second one as a copy of the first) and fills the layout's references.
    /// One undoable step; safe to re-run.
    /// </summary>
    /// <remarks>
    /// Why a script: ~30 linked nodes by hand is an hour of clicking and typos; this is one click and reproducible.
    /// Once real assets replace the whitebox, nodes are simply moved by hand. All numbers live in the tables below.
    /// </remarks>
    internal static class WhiteboxLocationBuilder
    {
        // ── Whitebox layout v3, see GDD §3. World coordinates (the location root sits at the origin). ──────────────────

        private const string LocationObjectName = "Location_1";
        private const string RootName = "Roads_v3";
        private static readonly string[] ObsoleteRootNames = { "Roads_v2", RootName };
        private const string RoadLayerName = "Road";

        private const string WashPointId = "loc1_wash_1";
        private const string MainEntrancePointId = "loc1_entrance_main";
        private const string ServiceEntrancePointId = "loc1_entrance_service";
        private const string ServiceEntranceObjectName = "Barrier_Service";

        // Ids the main entrance had in earlier layouts (v1 barrier, v2 parking exit); it is found and renamed.
        private static readonly string[] OldMainEntrancePointIds = { "loc1_parking_exit", "loc1_barrier" };

        // Tokens in NodeSpec.Next that stand for the car spots of the points (they carry their own RoadNode).
        private const string WashSpot = "<wash>";
        private const string MainEntranceSpot = "<entrance_main>";
        private const string ServiceEntranceSpot = "<entrance_service>";

        private const string ZoneM = "Zone_M";
        private const string ZoneJ = "Zone_J";

        private static readonly SurfaceSpec[] Surfaces =
        {
            new SurfaceSpec("Surface_Road", new Vector3(0f, 0f, -21.5f), new Vector3(72f, 0.1f, 8f)),
            new SurfaceSpec("Surface_Driveway", new Vector3(-3f, 0f, -4f), new Vector3(5f, 0.1f, 28f)),
            new SurfaceSpec("Surface_TopRoad", new Vector3(14f, 0f, 8f), new Vector3(40f, 0.1f, 5f)),
            new SurfaceSpec("Surface_Entrance2Lane", new Vector3(11f, 0f, 3f), new Vector3(4f, 0.1f, 6f)),
            new SurfaceSpec("Surface_Parking", new Vector3(19f, 0f, -8.25f), new Vector3(20f, 0.1f, 18.5f)),
            new SurfaceSpec("Surface_ExitRoad", new Vector3(30f, 0f, -8f), new Vector3(5f, 0.1f, 34f)),
        };

        private static readonly ZoneSpec[] Zones =
        {
            new ZoneSpec(ZoneM, new Vector3(11f, 0f, -13f), new Color(1f, 0.55f, 0f)),
            new ZoneSpec(ZoneJ, new Vector3(30f, 0f, -3f), new Color(0.9f, 0.2f, 0.9f)),
        };

        // Why: on equally short routes the graph prefers earlier connections; layout v3 has no such ties
        // (LocationLayout.Validate reports a route that would drive through a spot or a parking slot).
        private static readonly NodeSpec[] Nodes =
        {
            new NodeSpec("N_Spawn", -34f, -19f, 90f, null, "Q3"),
            new NodeSpec("Q3", -27f, -19f, 90f, null, "Q2"),
            new NodeSpec("Q2", -21f, -19f, 90f, null, "Q1"),
            new NodeSpec("Q1", -15f, -19f, 90f, null, "Q0"),
            new NodeSpec("Q0", -9f, -19f, 90f, null, "F1", "R1"),
            new NodeSpec("F1", -3f, -16f, 0f, null, "WB1"),
            new NodeSpec("WB1", -3f, -12f, 0f, null, "WB0"),
            new NodeSpec("WB0", -3f, -7f, 0f, null, WashSpot),
            new NodeSpec("WX", -3f, 4f, 0f, null, "T1"),
            new NodeSpec("T1", -3f, 8f, 90f, null, "T2"),
            new NodeSpec("T2", 11f, 8f, 90f, null, ServiceEntranceSpot, "T3"),
            new NodeSpec("T3", 30f, 8f, 180f, null, "D1"),
            new NodeSpec("D1", 30f, -3f, 180f, ZoneJ, "D2"),
            new NodeSpec("D2", 30f, -24f, 90f, null, "N_Exit"),
            new NodeSpec("N_Exit", 35f, -24f, 90f, null),
            new NodeSpec("R1", 6f, -19f, 90f, null, MainEntranceSpot),
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

        // Wash (drive-through): the car spot lands here (XZ; the bay keeps its height), facing north; buffer 0 = nearest.
        private static readonly Vector3 WashSpotPosition = new Vector3(-3f, 0f, -2f);
        private const float WashSpotYaw = 0f;
        private static readonly string[] WashSpotNext = { "WX" };
        private static readonly string[] WashBufferNames = { "WB0", "WB1" };

        // Parking entrances: post, car spot, work spot (facing east), arm along +X lifted around Z.
        // Why: the car spot sits 3 m before the arm (z of the post ± 3), so a waiting car's nose stays in front of it.
        private static readonly BarrierSpec MainEntrance = new BarrierSpec(
            MainEntrancePointId, new Vector3(9f, 0.5f, -17.5f), new Vector3(11f, 0f, -20.5f), 0f, new Vector3(7.5f, 0f, -17f), "B1N");

        private static readonly BarrierSpec ServiceEntrance = new BarrierSpec(
            ServiceEntrancePointId, new Vector3(9f, 0.5f, 2.5f), new Vector3(11f, 0f, 5.5f), 180f, new Vector3(7.5f, 0f, 3f), "B2S");

        private const float WorkSpotYaw = 90f;
        private static readonly Vector3 ArmLocalPosition = new Vector3(2f, 0f, 0f);
        private static readonly Vector3 ArmScale = new Vector3(4f, 0.15f, 0.15f);

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

            for (int i = 0; i < ObsoleteRootNames.Length; i++)
            {
                Transform previous = layout.transform.Find(ObsoleteRootNames[i]);
                if (previous != null)
                {
                    Undo.DestroyObjectImmediate(previous.gameObject);
                }
            }

            GameObject root = CreateChild(RootName, layout.transform);
            Undo.RegisterCreatedObjectUndo(root, "Create " + RootName);

            BuildSurfaces(root.transform);
            Dictionary<string, TrafficZone> zones = BuildZones(root.transform);
            Dictionary<string, RoadNode> nodes = BuildNodes(root.transform, zones);
            int problems = 0;

            ServicePointView wash = FindPoint(layout, WashPointId);
            if (wash != null && wash.CarSpot != null)
            {
                nodes[WashSpot] = PlaceWash(wash, nodes);
            }
            else
            {
                Debug.LogError("[Whitebox] Wash point '" + WashPointId + "' (with a Car Spot) not found; WB0 leads nowhere.");
                wash = null;
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
            FillLayout(layout, nodes, main, service, wash);

            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(layout.gameObject.scene);
            Selection.activeGameObject = root;

            // Why: no LocationLayout.Validate here — it caches the graph on the instance, and a second run of this tool
            // would then validate against the destroyed nodes. GameplayEntryPoint validates the layout on Play.
            Debug.Log("[Whitebox] Location 1 roads v3 built: " + Nodes.Length + " nodes, " + Zones.Length + " zones"
                + (problems == 0 ? ". Press Play: the layout is validated on start." : ", " + problems + " problem(s) above."), root);
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

        /// <returns>The road node on the wash's car spot.</returns>
        private static RoadNode PlaceWash(ServicePointView wash, Dictionary<string, RoadNode> nodes)
        {
            Transform root = wash.transform;
            Transform spot = wash.CarSpot;
            Undo.RecordObject(root, "Move wash");

            // Why: the root is turned so that the car spot (wherever it sits inside the bay) faces the driving direction.
            Quaternion spotInRoot = Quaternion.Inverse(root.rotation) * spot.rotation;
            root.rotation = Quaternion.Euler(0f, WashSpotYaw, 0f) * Quaternion.Inverse(spotInRoot);
            Vector3 offset = WashSpotPosition - spot.position;
            offset.y = 0f;
            root.position += offset;

            var view = new SerializedObject(wash);
            SetArray(view.FindProperty("_bufferSlots"), Lookup(nodes, WashBufferNames));
            view.ApplyModifiedProperties();

            RoadNode node = GetOrAddNode(spot.gameObject);
            SetNext(node, WashSpotNext, nodes);
            return node;
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

            TurnArm(barrier);

            RoadNode node = GetOrAddNode(spot.gameObject);
            SetNext(node, new[] { spec.Next }, nodes);
            return node;
        }

        private static void TurnArm(ServicePointView barrier)
        {
            if (!barrier.TryGetComponent(out BarrierArm barrierArm))
            {
                Debug.LogWarning("[Whitebox] Barrier '" + barrier.name + "' has no BarrierArm; the arm was not turned.", barrier);
                return;
            }

            var serialized = new SerializedObject(barrierArm);
            serialized.FindProperty("_openAxis").vector3Value = Vector3.forward;
            serialized.ApplyModifiedProperties();

            var pivot = serialized.FindProperty("_arm").objectReferenceValue as Transform;
            if (pivot == null || pivot.childCount == 0)
            {
                Debug.LogWarning("[Whitebox] BarrierArm of '" + barrier.name + "' has no pivot with an arm child; the arm was not turned.", barrier);
                return;
            }

            Undo.RecordObject(pivot, "Reset barrier arm pivot");
            pivot.localRotation = Quaternion.identity;

            Transform arm = pivot.GetChild(0);
            Undo.RecordObject(arm, "Turn barrier arm");
            arm.localPosition = ArmLocalPosition;
            arm.localRotation = Quaternion.identity;
            arm.localScale = ArmScale;
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
                else if (next[i] != WashSpot && next[i] != MainEntranceSpot && next[i] != ServiceEntranceSpot)
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
            ServicePointView wash)
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

            if (wash != null)
            {
                SetArray(serialized.FindProperty("_servicePoints"), new List<Object> { wash });
            }

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

        private readonly struct BarrierSpec
        {
            public BarrierSpec(string pointId, Vector3 postPosition, Vector3 spotPosition, float spotYaw, Vector3 workSpotPosition, string next)
            {
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
