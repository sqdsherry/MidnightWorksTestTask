using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace AutoService.Bootstrap.Editor
{
    public static partial class KenneyImporter
    {
        private static void CreateWrappers()
        {
            string searchDir = "Assets/_Project/Art/Kenney";
            string outDir = "Assets/_Project/Prefabs/Art";
            Directory.CreateDirectory(outDir);

            string[] guids = AssetDatabase.FindAssets("t:Model", new[] { searchDir });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string name = Path.GetFileNameWithoutExtension(path);
                
                GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (model == null) continue;

                GameObject wrapper = new GameObject(name);
                GameObject inst = (GameObject)PrefabUtility.InstantiatePrefab(model, wrapper.transform);
                inst.name = "Model";
                inst.transform.localPosition = Vector3.zero;
                
                // Fit to size
                if (path.Contains("kenney_car-kit") && (name.Contains("sedan") || name.Contains("suv") || name.Contains("hatchback") || name.Contains("race") || name.Contains("taxi") || name.Contains("police") || name.Contains("ambulance") || name.Contains("truck") || name.Contains("van")))
                {
                    inst.transform.localRotation = Quaternion.Euler(0, 180, 0); // Kenney cars face -Z
                    float targetZ = name.Contains("suv") ? 4.3f : (name.Contains("sports") || name.Contains("race") ? 4.2f : 4.0f);
                    FitToSize(inst, new Vector3(0, 0, targetZ), 2);
                }
                else if (path.Contains("kenney_mini-characters"))
                {
                    FitToSize(inst, new Vector3(0, 1.8f, 0), 1);
                }
                else if (path.Contains("kenney_cityKitRoads"))
                {
                    FitToSize(inst, new Vector3(4f, 0, 4f), 0); // approx
                }
                else if (path.Contains("kenney_conveyor-kit") && name.Contains("structure"))
                {
                    FitToSize(inst, new Vector3(0, 3f, 0), 1);
                }

                PrefabUtility.SaveAsPrefabAsset(wrapper, Path.Combine(outDir, name + ".prefab"));
                Object.DestroyImmediate(wrapper);
            }
        }

        private static void FitToSize(GameObject inst, Vector3 targetSize, int axis)
        {
            Renderer[] rs = inst.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return;
            
            Bounds b = rs[0].bounds;
            for(int i=1; i<rs.Length; i++) b.Encapsulate(rs[i].bounds);

            float currentSize = axis == 0 ? b.size.x : (axis == 1 ? b.size.y : b.size.z);
            float target = axis == 0 ? targetSize.x : (axis == 1 ? targetSize.y : targetSize.z);
            
            if (currentSize > 0.001f)
            {
                float scale = target / currentSize;
                inst.transform.localScale = Vector3.one * scale;
                Debug.Log($"[KenneyImporter] FitToSize {inst.transform.parent.name} axis {axis}: coeff {scale}");
            }
        }
    }
}
