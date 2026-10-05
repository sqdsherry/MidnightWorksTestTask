using AutoService.Presentation.Building;
using AutoService.Presentation.Interaction;
using AutoService.Presentation.Traffic;
using AutoService.Presentation.Supplies;
using UnityEditor;
using UnityEngine;

namespace AutoService.Bootstrap.Editor
{
    internal static class WhiteboxLocation2Builder
    {
        private const string Loc2Name = "Location_2";
        private const string Loc1Name = "Location_1";
        private const float Loc2Offset = 200f;

        [MenuItem("AutoService/Whitebox/Build Location 2 (and Travel Points)")]
        private static void BuildLocation2()
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

            BuildLoc2Whitebox(loc2);

            // Travel point on Loc 1 to Loc 2
            Transform travelSpawnLoc2 = CreateChild("SpawnLoc2", loc2.transform).transform;
            travelSpawnLoc2.position = loc2.transform.position + new Vector3(0, 0, -10f); // just a safe spot

            BuildPlotView travel1To2 = CreateTravelPlot(loc1.transform, "b_travel_to_loc2", "Travel_To_Loc2", new Vector3(-35f, 0f, 0f), travelSpawnLoc2);

            // Travel point on Loc 2 to Loc 1
            Transform travelSpawnLoc1 = CreateChild("SpawnLoc1", loc1.transform).transform;
            travelSpawnLoc1.position = loc1.transform.position + new Vector3(-34f, 0, -19f); // near Loc 1 spawn

            BuildPlotView travel2To1 = CreateTravelPlot(loc2.transform, "b_travel_to_loc1", "Travel_To_Loc1", new Vector3(-5f, 0f, -5f), travelSpawnLoc1);

            // Add travel plot to Loc 1 LocationLayout
            var loc1So = new SerializedObject(loc1);
            AddPlotToLayout(loc1So, travel1To2);
            loc1So.ApplyModifiedPropertiesWithoutUndo();

            // Set up Loc 2 LocationLayout
            var loc2So = new SerializedObject(loc2);
            loc2So.FindProperty("_locationId").stringValue = "loc2";
            
            // Collect all build plots from Loc2
            var loc2Plots = loc2.GetComponentsInChildren<BuildPlotView>(true);
            var buildPlotsProp = loc2So.FindProperty("_buildPlots");
            buildPlotsProp.ClearArray();
            for (int i = 0; i < loc2Plots.Length; i++)
            {
                buildPlotsProp.InsertArrayElementAtIndex(i);
                buildPlotsProp.GetArrayElementAtIndex(i).objectReferenceValue = loc2Plots[i];
            }

            // Find warehouse
            var warehouse = loc2.GetComponentInChildren<WarehouseView>(true);
            if (warehouse != null)
            {
                loc2So.FindProperty("_warehouse").objectReferenceValue = warehouse;
            }

            loc2So.ApplyModifiedPropertiesWithoutUndo();

            Undo.CollapseUndoOperations(undoGroup);
            Debug.Log("[Whitebox] Location 2 and Travel Points built.");
        }

        private static void AddPlotToLayout(SerializedObject layoutSo, BuildPlotView plot)
        {
            var buildPlotsProp = layoutSo.FindProperty("_buildPlots");
            bool contains = false;
            for (int i = 0; i < buildPlotsProp.arraySize; i++)
            {
                if (buildPlotsProp.GetArrayElementAtIndex(i).objectReferenceValue == plot)
                {
                    contains = true;
                    break;
                }
            }
            if (!contains)
            {
                buildPlotsProp.InsertArrayElementAtIndex(buildPlotsProp.arraySize);
                buildPlotsProp.GetArrayElementAtIndex(buildPlotsProp.arraySize - 1).objectReferenceValue = plot;
            }
        }

        private static void BuildLoc2Whitebox(LocationLayout loc2)
        {
            Transform root = loc2.transform;
            for (int i = root.childCount - 1; i >= 0; i--)
            {
                Transform child = root.GetChild(i);
                if (child.name == "Whitebox") Undo.DestroyObjectImmediate(child.gameObject);
            }

            GameObject wb = CreateChild("Whitebox", root);
            Undo.RegisterCreatedObjectUndo(wb, "Loc2 Whitebox");

            // Road
            GameObject road = GameObject.CreatePrimitive(PrimitiveType.Cube);
            road.name = "Surface_Road";
            road.transform.SetParent(wb.transform, false);
            road.transform.localPosition = new Vector3(0f, 0f, 0f);
            road.transform.localScale = new Vector3(30f, 0.1f, 10f);

            // Parking
            GameObject parking = GameObject.CreatePrimitive(PrimitiveType.Cube);
            parking.name = "ParkingSpots";
            parking.transform.SetParent(wb.transform, false);
            parking.transform.localPosition = new Vector3(0f, 0.05f, -5f);
            parking.transform.localScale = new Vector3(15f, 0.1f, 4f);

            // Spawn/Despawn
            GameObject spawn = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            spawn.name = "SpawnPoint";
            spawn.transform.SetParent(wb.transform, false);
            spawn.transform.localPosition = new Vector3(-15f, 0.5f, 0f);
            
            GameObject despawn = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            despawn.name = "DespawnPoint";
            despawn.transform.SetParent(wb.transform, false);
            despawn.transform.localPosition = new Vector3(15f, 0.5f, 0f);

            // Services
            CreateServicePlot(wb.transform, "loc2_build_tires_1", "Tires1", new Vector3(-10f, 0f, 10f));
            CreateServicePlot(wb.transform, "loc2_build_tuning_1", "Tuning1", new Vector3(0f, 0f, 10f));
            CreateServicePlot(wb.transform, "loc2_build_paint_1", "Paint1", new Vector3(10f, 0f, 10f));

            // Warehouse
            GameObject warehouse = GameObject.CreatePrimitive(PrimitiveType.Cube);
            warehouse.name = "Warehouse_Loc2";
            warehouse.transform.SetParent(wb.transform, false);
            warehouse.transform.localPosition = new Vector3(0f, 1.25f, -10f);
            warehouse.transform.localScale = new Vector3(5f, 2.5f, 5f);
        }

        private static void CreateServicePlot(Transform parent, string plotId, string name, Vector3 localPos)
        {
            GameObject plotObj = CreateChild("Plot_" + name, parent);
            plotObj.transform.localPosition = localPos;

            GameObject ghost = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ghost.name = "Ghost";
            ghost.transform.SetParent(plotObj.transform, false);
            ghost.GetComponent<Collider>().isTrigger = true;
            ghost.transform.localScale = new Vector3(4f, 2f, 4f);
            ghost.transform.localPosition = new Vector3(0, 1f, 0);
            ghost.layer = LayerMask.NameToLayer("Interactable");
            
            var highlight1 = ghost.AddComponent<InteractableHighlight>();
            var hlSo1 = new SerializedObject(highlight1);
            hlSo1.FindProperty("_renderers").InsertArrayElementAtIndex(0);
            hlSo1.FindProperty("_renderers").GetArrayElementAtIndex(0).objectReferenceValue = ghost.GetComponent<Renderer>();
            hlSo1.ApplyModifiedPropertiesWithoutUndo();

            GameObject target = GameObject.CreatePrimitive(PrimitiveType.Cube);
            target.name = "Service_" + name;
            target.transform.SetParent(plotObj.transform, false);
            target.transform.localScale = new Vector3(4f, 2f, 4f);
            target.transform.localPosition = new Vector3(0, 1f, 0);
            target.SetActive(false);

            Transform approach = CreateChild("Approach", plotObj.transform).transform;
            approach.localPosition = new Vector3(0, 0, 0f);

            var view = plotObj.AddComponent<BuildPlotView>();
            var so = new SerializedObject(view);
            so.FindProperty("_plotId").stringValue = plotId;
            so.FindProperty("_ghost").objectReferenceValue = ghost;
            so.FindProperty("_target").objectReferenceValue = target;
            so.FindProperty("_approachPoint").objectReferenceValue = approach;
            so.FindProperty("_highlight").objectReferenceValue = highlight1;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static BuildPlotView CreateTravelPlot(Transform parent, string plotId, string name, Vector3 localPos, Transform teleportTarget)
        {
            GameObject plotObj = CreateChild("TravelPlot_" + name, parent);
            plotObj.transform.localPosition = localPos;

            GameObject ghost = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ghost.name = "Ghost";
            ghost.transform.SetParent(plotObj.transform, false);
            ghost.GetComponent<Collider>().isTrigger = true;
            ghost.transform.localScale = new Vector3(2f, 0.1f, 2f);
            ghost.layer = LayerMask.NameToLayer("Interactable");
            
            var highlight2 = ghost.AddComponent<InteractableHighlight>();
            var hlSo2 = new SerializedObject(highlight2);
            hlSo2.FindProperty("_renderers").InsertArrayElementAtIndex(0);
            hlSo2.FindProperty("_renderers").GetArrayElementAtIndex(0).objectReferenceValue = ghost.GetComponent<Renderer>();
            hlSo2.ApplyModifiedPropertiesWithoutUndo();

            GameObject target = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            target.name = "TravelPoint";
            target.transform.SetParent(plotObj.transform, false);
            target.transform.localScale = new Vector3(2f, 0.1f, 2f);
            target.GetComponent<Collider>().isTrigger = true;
            var renderer = target.GetComponent<Renderer>();
            renderer.sharedMaterial = new Material(renderer.sharedMaterial) { color = Color.green };
            target.SetActive(false);

            var tp = target.AddComponent<TravelPoint>();
            tp.TargetTransform = teleportTarget;

            Transform approach = CreateChild("Approach", plotObj.transform).transform;
            approach.localPosition = new Vector3(0, 0, 0f);

            var view = plotObj.AddComponent<BuildPlotView>();
            var so = new SerializedObject(view);
            so.FindProperty("_plotId").stringValue = plotId;
            so.FindProperty("_ghost").objectReferenceValue = ghost;
            so.FindProperty("_target").objectReferenceValue = target;
            so.FindProperty("_approachPoint").objectReferenceValue = approach;
            so.FindProperty("_highlight").objectReferenceValue = highlight2;
            so.ApplyModifiedPropertiesWithoutUndo();
            
            return view;
        }

        private static GameObject CreateChild(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go;
        }

        private static LocationLayout FindLayout(string name)
        {
            LocationLayout[] layouts = Object.FindObjectsByType<LocationLayout>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < layouts.Length; i++)
            {
                if (layouts[i].name == name) return layouts[i];
            }
            return null;
        }
    }
}
