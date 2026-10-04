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
                if (vis != null) Object.DestroyImmediate(vis.gameObject);
            }

            Transform staffRoom = layout.transform.Find("StaffRoom_1");
            if (staffRoom != null)
            {
                Transform vis = staffRoom.Find("Visual");
                if (vis != null) Object.DestroyImmediate(vis.gameObject);
            }
        }
    }
}
