using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using AutoService.Bootstrap.Editor;
using AutoService.Presentation.Building;
using AutoService.Presentation.Points;
using AutoService.Presentation.Traffic;

namespace AutoService.Bootstrap.Editor
{
    public static partial class ModuleSetupD1
    {
        private static void SetupBarriers(LocationLayout layout)
        {
            var barriers = layout.GetComponentsInChildren<BarrierArm>(true);
            Material stripeMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Generated/M_ArmStripe.mat");

            foreach (var b in barriers)
            {
                Transform arm = b.transform.Find("ArmPivot/Arm");
                if (arm != null && arm.TryGetComponent<Renderer>(out var r))
                {
                    r.sharedMaterial = stripeMat;
                }

                Transform visual = b.transform.Find("Visual");
                if (visual == null)
                {
                    visual = new GameObject("Visual").transform;
                    visual.SetParent(b.transform, false);
                }

                // Clear old visual (Post)
                Transform oldPost = b.transform.Find("Post");
                if (oldPost != null) Object.DestroyImmediate(oldPost.gameObject);
            }
        }

        private static void SetupBuildings(LocationLayout layout)
        {
            Transform warehouse = layout.transform.Find("Warehouse_1");
            if (warehouse != null)
            {
                Transform vis = warehouse.Find("Visual");
                if (vis == null) { vis = new GameObject("Visual").transform; vis.SetParent(warehouse, false); }
                else foreach(Transform child in vis) Object.DestroyImmediate(child.gameObject);

                // Disable old renderer
                if (warehouse.TryGetComponent<Renderer>(out var r)) r.enabled = false;

                GameObject wall = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Art/structure-wall.prefab");
                GameObject door = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Art/door-wide-open.prefab");
                GameObject box = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Art/box-large.prefab");

                if (door != null)
                {
                    var d = (GameObject)PrefabUtility.InstantiatePrefab(door, vis);
                    d.transform.localPosition = new Vector3(0, 0, -1.5f);
                }
                if (wall != null)
                {
                    var w = (GameObject)PrefabUtility.InstantiatePrefab(wall, vis);
                    w.transform.localPosition = new Vector3(0, 0, 1.5f);
                    
                    var w2 = (GameObject)PrefabUtility.InstantiatePrefab(wall, vis);
                    w2.transform.localPosition = new Vector3(2f, 0, 0);
                    w2.transform.localRotation = Quaternion.Euler(0, 90, 0);
                    
                    var w3 = (GameObject)PrefabUtility.InstantiatePrefab(wall, vis);
                    w3.transform.localPosition = new Vector3(-2f, 0, 0);
                    w3.transform.localRotation = Quaternion.Euler(0, 90, 0);
                }
                if (box != null)
                {
                    var b1 = (GameObject)PrefabUtility.InstantiatePrefab(box, vis);
                    b1.transform.localPosition = new Vector3(0, 0, 0);
                }
            }

            Transform staffRoom = layout.transform.Find("StaffRoom_1");
            if (staffRoom != null)
            {
                Transform vis = staffRoom.Find("Visual");
                if (vis == null) { vis = new GameObject("Visual").transform; vis.SetParent(staffRoom, false); }
                else foreach(Transform child in vis) Object.DestroyImmediate(child.gameObject);

                if (staffRoom.TryGetComponent<Renderer>(out var r)) r.enabled = false;

                GameObject wall = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Art/structure-wall.prefab");
                GameObject door = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Art/structure-doorway-wide.prefab");
                GameObject parasol = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Art/detail-parasol-a.prefab");

                if (door != null)
                {
                    var d = (GameObject)PrefabUtility.InstantiatePrefab(door, vis);
                    d.transform.localPosition = new Vector3(0, 0, -1.5f);
                }
                if (wall != null)
                {
                    var w = (GameObject)PrefabUtility.InstantiatePrefab(wall, vis);
                    w.transform.localPosition = new Vector3(0, 0, 1.5f);
                    
                    var w2 = (GameObject)PrefabUtility.InstantiatePrefab(wall, vis);
                    w2.transform.localPosition = new Vector3(1.5f, 0, 0);
                    w2.transform.localRotation = Quaternion.Euler(0, 90, 0);
                    
                    var w3 = (GameObject)PrefabUtility.InstantiatePrefab(wall, vis);
                    w3.transform.localPosition = new Vector3(-1.5f, 0, 0);
                    w3.transform.localRotation = Quaternion.Euler(0, 90, 0);
                }
                if (parasol != null)
                {
                    var p = (GameObject)PrefabUtility.InstantiatePrefab(parasol, vis);
                    p.transform.localPosition = new Vector3(2.5f, 0, -2f);
                }
            }
        }
    }
}
