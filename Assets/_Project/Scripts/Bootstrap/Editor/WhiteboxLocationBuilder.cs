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
    /// Editor tool: builds the whitebox road layout v2 of location 1 (GDD §3) under its <see cref="LocationLayout"/> —
    /// road surfaces, the one-way <see cref="RoadNode"/> graph, merge zones with the automatic entry gate — moves the wash
    /// and the parking exit barrier into place and fills the layout's references. One undoable step; safe to re-run.
    /// </summary>
    /// <remarks>
    /// Why a script: ~30 linked nodes by hand is an hour of clicking and typos; this is one click and reproducible.
    /// Once real assets replace the whitebox, nodes are simply moved by hand. All numbers live in the tables below.
    /// </remarks>
    internal static class WhiteboxLocationBuilder
    {
        // ── Whitebox layout v2, see GDD §3. World coordinates (the location root sits at the origin). ──────────────────

        private const string LocationObjectName = "Location_1";
        private const string RootName = "Roads_v2";
        private const string RoadLayerName = "Road";

        private const string WashPointId = "loc1_wash_1";
        private const string OldParkingExitPointId = "loc1_barrier";
        private const string ParkingExitPointId = "loc1_parking_exit";

        // Tokens in NodeSpec.Next that stand for the car spots of the points (they carry their own RoadNode).
        private const string WashSpot = "<wash>";
        private const string ParkingExitSpot = "<parking_exit>";

        private const string ZoneS = "Zone_S";
        private const string ZoneJ = "Zone_J";
        private const string ZoneG = "Zone_G";

        private static readonly SurfaceSpec[] Surfaces =
        {
            new SurfaceSpec("Surface_Road", new Vector3(0f, 0f, -21.5f), new Vector3(72f, 0.1f, 8f)),
            new SurfaceSpec("Surface_LaneA", new Vector3(-3f, 0f, -11f), new Vector3(5f, 0.1f, 13f)),
            new SurfaceSpec("Surface_ServiceLane", new Vector3(11.5f, 0f, -6f), new Vector3(34f, 0.1f, 5f)),
            new SurfaceSpec("Surface_Bays", new Vector3(16f, 0f, 1.5f), new Vector3(24f, 0.1f, 10f)),
            new SurfaceSpec("Surface_LaneD", new Vector3(26f, 0f, -12f), new Vector3(5f, 0.1f, 13f)),
            new SurfaceSpec("Surface_Parking", new Vector3(11f, 0f, -13f), new Vector3(22f, 0.1f, 9f)),
        };

        private static readonly ZoneSpec[] Zones =
        {
            new ZoneSpec(ZoneS, new Vector3(2f, 0f, -6f), new Color(1f, 0.55f, 0f)),
            new ZoneSpec(ZoneJ, new Vector3(26f, 0f, -6f), new Color(0.9f, 0.2f, 0.9f)),
            new ZoneSpec(ZoneG, new Vector3(20f, 0f, -16f), new Color(0.3f, 0.9f, 0.3f)),
        };

        // Why: on equally short routes the graph prefers earlier connections, so the through lane is always listed first
        // (L6 → L12 before the wash: a parking-only car must drive past the bay, not through it).
        private static readonly NodeSpec[] Nodes =
        {
            new NodeSpec("N_Spawn", -34f, -19f, 90f, null, "Q3"),
            new NodeSpec("Q3", -24f, -19f, 90f, null, "Q2"),
            new NodeSpec("Q2", -18f, -19f, 90f, null, "Q1"),
            new NodeSpec("Q1", -12f, -19f, 90f, null, "Q0"),
            new NodeSpec("Q0", -6f, -19f, 90f, null, "F1", "R1"),
            new NodeSpec("F1", -3f, -15f, 0f, null, "A1"),
            new NodeSpec("A1", -3f, -9f, 0f, null, "S0"),
            new NodeSpec("S0", -1f, -6f, 90f, null, "S1"),
            new NodeSpec("S1", 2f, -6f, 90f, ZoneS, "L6"),
            new NodeSpec("L6", 6f, -6f, 90f, null, "L12", WashSpot),
            new NodeSpec("L12", 12f, -6f, 90f, null, "L18"),
            new NodeSpec("L18", 18f, -6f, 90f, null, "L24"),
            new NodeSpec("L24", 24f, -6f, 90f, null, "J1"),
            new NodeSpec("E6", 6f, 4f, 90f, null, "E26"),
            new NodeSpec("E26", 26f, 4f, 180f, null, "J1"),
            new NodeSpec("J1", 26f, -6f, 180f, ZoneJ, "D1"),
            new NodeSpec("D1", 26f, -14f, 180f, null, "D2"),
            new NodeSpec("D2", 26f, -24f, 90f, null, "N_Exit"),
            new NodeSpec("N_Exit", 34f, -24f, 90f, null),
            new NodeSpec("R1", 4f, -19f, 90f, null, "R2"),
            new NodeSpec("R2", 18f, -19f, 90f, null, "G1"),
            new NodeSpec("G1", 20f, -16f, 0f, ZoneG, "K18"),
            new NodeSpec("K18", 18f, -14f, 270f, null, "K14", "P3"),
            new NodeSpec("K14", 14f, -14f, 270f, null, "K10", "P2"),
            new NodeSpec("K10", 10f, -14f, 270f, null, "K6", "P1"),
            new NodeSpec("K6", 6f, -14f, 270f, null, "K2", "P0"),
            new NodeSpec("K2", 2f, -14f, 0f, null, ParkingExitSpot),
            new NodeSpec("P3", 18f, -11f, 0f, null, "K14"),
            new NodeSpec("P2", 14f, -11f, 0f, null, "K10"),
            new NodeSpec("P1", 10f, -11f, 0f, null, "K6"),
            new NodeSpec("P0", 6f, -11f, 0f, null, "K2"),
        };

        private const string SpawnNodeName = "N_Spawn";
        private const string ExitNodeName = "N_Exit";
        private static readonly string[] QueueSlotNames = { "Q0", "Q1", "Q2", "Q3" };
        private static readonly string[] ParkingSlotNames = { "P0", "P1", "P2", "P3" };

        // Wash: the car spot lands here (XZ; the bay keeps its height), facing north.
        private static readonly Vector3 WashSpotPosition = new Vector3(6f, 0f, -1f);
        private const float WashSpotYaw = 0f;
        private static readonly string[] WashSpotNext = { "E6" };

        // Parking exit barrier: post, car spot (facing north), work spot (facing east), arm along +X lifted around Z.
        private static readonly Vector3 ExitPostPosition = new Vector3(0f, 0.5f, -10f);
        private static readonly Vector3 ExitSpotPosition = new Vector3(2f, 0f, -10f);
        private const float ExitSpotYaw = 0f;
        private static readonly Vector3 ExitWorkSpotPosition = new Vector3(-1f, 0f, -10f);
        private const float ExitWorkSpotYaw = 90f;
        private static readonly Vector3 ExitArmLocalPosition = new Vector3(2f, 0f, 0f);
        private static readonly string[] ExitSpotNext = { "S1" };

        // Automatic entry gate of zone G: post with an arm along -X over G1.
        private static readonly Vector3 GatePostPosition = new Vector3(22.5f, 0.5f, -16f);
        private static readonly Vector3 GateArmLocalPosition = new Vector3(-2f, 0f, 0f);

        // Why: the arm points along -X, so a positive turn around +Z would swing it down into the road.
        private const float GateOpenAngle = -80f;

        private static readonly Vector3 PostScale = new Vector3(0.5f, 1f, 0.5f);
        private static readonly Vector3 PivotLocalPosition = new Vector3(0f, 0.5f, 0f);
        private static readonly Vector3 PivotLocalScale = new Vector3(2f, 1f, 2f); // undoes the post's scale
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

            Transform previous = layout.transform.Find(RootName);
            if (previous != null)
            {
                Undo.DestroyObjectImmediate(previous.gameObject);
            }

            GameObject root = CreateChild(RootName, layout.transform);
            Undo.RegisterCreatedObjectUndo(root, "Create " + RootName);

            BuildSurfaces(root.transform);
            Dictionary<string, TrafficZone> zones = BuildZones(root.transform);
            Dictionary<string, RoadNode> nodes = BuildNodes(root.transform, zones);

            ServicePointView wash = FindPoint(layout, WashPointId);
            ServicePointView parkingExit = FindPoint(layout, ParkingExitPointId) ?? FindPoint(layout, OldParkingExitPointId);
            int problems = 0;

            if (wash != null && wash.CarSpot != null)
            {
                nodes[WashSpot] = PlaceWash(wash, nodes);
            }
            else
            {
                Debug.LogError("[Whitebox] Wash point '" + WashPointId + "' (with a Car Spot) not found; L6 has no bay.");
                problems++;
            }

            if (parkingExit != null && parkingExit.CarSpot != null)
            {
                nodes[ParkingExitSpot] = PlaceParkingExit(parkingExit, nodes);
            }
            else
            {
                Debug.LogError("[Whitebox] Parking exit point '" + OldParkingExitPointId + "' / '" + ParkingExitPointId
                    + "' (with a Car Spot) not found; K2 leads nowhere.");
                problems++;
            }

            LinkNodes(nodes);
            FillLayout(layout, nodes, parkingExit, wash);

            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(layout.gameObject.scene);
            Selection.activeGameObject = root;

            // Why: no LocationLayout.Validate here — it caches the graph on the instance, and a second run of this tool
            // would then validate against the destroyed nodes. GameplayEntryPoint validates the layout on Play.
            Debug.Log("[Whitebox] Location 1 roads built: " + Nodes.Length + " nodes, " + Zones.Length + " zones"
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
                if (spec.Name == ZoneG)
                {
                    serialized.FindProperty("_gate").objectReferenceValue = BuildGate(zoneObject.transform);
                }

                serialized.ApplyModifiedPropertiesWithoutUndo();
                zones.Add(spec.Name, zone);
            }

            return zones;
        }

        private static BarrierArm BuildGate(Transform zone)
        {
            GameObject post = CreateCube("GatePost", zone);
            post.transform.SetPositionAndRotation(GatePostPosition, Quaternion.identity);
            post.transform.localScale = PostScale;

            GameObject pivot = CreateChild("ArmPivot", post.transform);
            pivot.transform.localPosition = PivotLocalPosition;
            pivot.transform.localScale = PivotLocalScale;

            GameObject arm = CreateCube("Arm", pivot.transform);
            arm.transform.localPosition = GateArmLocalPosition;
            arm.transform.localScale = ArmScale;

            var barrierArm = post.AddComponent<BarrierArm>();
            var serialized = new SerializedObject(barrierArm);
            serialized.FindProperty("_arm").objectReferenceValue = pivot.transform;
            serialized.FindProperty("_openAxis").vector3Value = Vector3.forward;
            serialized.FindProperty("_openAngle").floatValue = GateOpenAngle;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return barrierArm;
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

            RoadNode node = GetOrAddNode(spot.gameObject);
            SetNext(node, WashSpotNext, nodes);
            return node;
        }

        /// <returns>The road node on the parking exit's car spot.</returns>
        private static RoadNode PlaceParkingExit(ServicePointView exit, Dictionary<string, RoadNode> nodes)
        {
            var view = new SerializedObject(exit);
            view.FindProperty("_pointId").stringValue = ParkingExitPointId;
            view.ApplyModifiedProperties();

            Transform post = exit.transform;
            Undo.RecordObject(post, "Move parking exit");
            post.SetPositionAndRotation(ExitPostPosition, Quaternion.identity);

            Transform spot = exit.CarSpot;
            Undo.RecordObject(spot, "Move parking exit car spot");
            spot.SetPositionAndRotation(ExitSpotPosition, Quaternion.Euler(0f, ExitSpotYaw, 0f));

            Object workSpot = view.FindProperty("_approachPoint").objectReferenceValue;
            if (workSpot is Transform work)
            {
                Undo.RecordObject(work, "Move parking exit work spot");
                work.SetPositionAndRotation(ExitWorkSpotPosition, Quaternion.Euler(0f, ExitWorkSpotYaw, 0f));
            }
            else
            {
                Debug.LogWarning("[Whitebox] Parking exit '" + exit.name + "' has no Approach Point; the worker stands at the post.", exit);
            }

            TurnExitArm(exit);

            RoadNode node = GetOrAddNode(spot.gameObject);
            SetNext(node, ExitSpotNext, nodes);
            return node;
        }

        private static void TurnExitArm(ServicePointView exit)
        {
            if (!exit.TryGetComponent(out BarrierArm barrierArm))
            {
                Debug.LogWarning("[Whitebox] Parking exit '" + exit.name + "' has no BarrierArm; the arm was not turned.", exit);
                return;
            }

            var serialized = new SerializedObject(barrierArm);
            serialized.FindProperty("_openAxis").vector3Value = Vector3.forward;
            serialized.ApplyModifiedProperties();

            var pivot = serialized.FindProperty("_arm").objectReferenceValue as Transform;
            if (pivot == null || pivot.childCount == 0)
            {
                Debug.LogWarning("[Whitebox] BarrierArm of '" + exit.name + "' has no pivot with an arm child; the arm was not turned.", exit);
                return;
            }

            Undo.RecordObject(pivot, "Reset parking exit arm pivot");
            pivot.localRotation = Quaternion.identity;

            Transform arm = pivot.GetChild(0);
            Undo.RecordObject(arm, "Turn parking exit arm");
            arm.localPosition = ExitArmLocalPosition;
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
                else if (next[i] != WashSpot && next[i] != ParkingExitSpot)
                {
                    Debug.LogError("[Whitebox] Node table: '" + node.name + "' links to unknown node '" + next[i] + "'.");
                }
            }

            var serialized = new SerializedObject(node);
            SetArray(serialized.FindProperty("_next"), targets);
            serialized.ApplyModifiedProperties();
        }

        private static void FillLayout(
            LocationLayout layout, Dictionary<string, RoadNode> nodes, ServicePointView parkingExit, ServicePointView wash)
        {
            var serialized = new SerializedObject(layout);
            serialized.FindProperty("_spawnNode").objectReferenceValue = nodes[SpawnNodeName];
            serialized.FindProperty("_exitNode").objectReferenceValue = nodes[ExitNodeName];
            SetArray(serialized.FindProperty("_queueSlots"), Lookup(nodes, QueueSlotNames));
            SetArray(serialized.FindProperty("_parkingSlots"), Lookup(nodes, ParkingSlotNames));
            if (parkingExit != null)
            {
                serialized.FindProperty("_parkingExit").objectReferenceValue = parkingExit;
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

        // Why: gate parts are visuals only — a collider would block clicks and, on the Road layer, the car NavMesh.
        private static GameObject CreateCube(string name, Transform parent)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            Object.DestroyImmediate(cube.GetComponent<Collider>());
            cube.transform.SetParent(parent, false);
            return cube;
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

            /// <summary>Names of the next nodes (or <c>WashSpot</c>/<c>ParkingExitSpot</c> tokens), through lane first.</summary>
            public string[] Next { get; }
        }
    }
}
