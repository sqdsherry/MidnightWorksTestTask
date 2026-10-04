using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using AutoService.Bootstrap.Editor;
using AutoService.Presentation.Building;
using AutoService.Presentation.Points;
using AutoService.Presentation.Traffic;
using TMPro;

namespace AutoService.Bootstrap.Editor
{
    public class ArtD1Generated : MonoBehaviour { }

    public static partial class ModuleSetupD1
    {
        [MenuItem("AutoService/Setup/Run D1 Setup")]
        public static void Run()
        {
            var layout = WhiteboxLocationBuilder.FindLayout();
            if (layout == null) return;
            
            if (!layout.TryGetComponent<ArtD1Generated>(out _))
                layout.gameObject.AddComponent<ArtD1Generated>();

            EnsureMaterials();
            // SetupRoads(layout.transform);
            SetupBays(layout);
            SetupBarriers(layout);
            SetupBuildings(layout);
            SetupDecorAndLight(layout);
            SetupCharacters();
            SetupCars();
            
            var problems = new List<string>();
            ModuleSetupA2.BakeNavMeshes(problems);
            
            string layoutProblem = ModuleSetupA2.ValidateLayout(layout);
            if (layoutProblem != null) problems.Add(layoutProblem);
            
            if (problems.Count > 0)
            {
                Debug.LogWarning("[D1 Setup] Completed with problems: " + string.Join(", ", problems));
            }
            else
            {
                Debug.Log("[D1 Setup] Completed successfully.");
            }
        }

        private static void EnsureMaterials()
        {
            Directory.CreateDirectory("Assets/_Project/Art/Generated");
            
            void CreateMat(string name, Material mat)
            {
                if (AssetDatabase.LoadAssetAtPath<Material>(name) == null)
                    AssetDatabase.CreateAsset(mat, name);
            }

            // Grass
            Material grass = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            grass.color = GetColor("#7CB342");
            grass.SetFloat("_Smoothness", 0.1f);
            CreateMat("Assets/_Project/Art/Generated/M_Grass.mat", grass);

            // LineWhite
            Material line = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            line.color = Color.white;
            CreateMat("Assets/_Project/Art/Generated/M_LineWhite.mat", line);

            // Stripe texture
            if (AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Project/Art/Generated/T_Stripe.png") == null)
            {
                Texture2D tex = new Texture2D(64, 8);
                for(int y=0; y<8; y++)
                    for(int x=0; x<64; x++)
                        tex.SetPixel(x, y, (x/8)%2 == 0 ? Color.red : Color.white);
                tex.Apply();
                byte[] bytes = tex.EncodeToPNG();
                System.IO.File.WriteAllBytes("Assets/_Project/Art/Generated/T_Stripe.png", bytes);
                AssetDatabase.Refresh();
            }
            
            Material stripe = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            stripe.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Project/Art/Generated/T_Stripe.png");
            CreateMat("Assets/_Project/Art/Generated/M_ArmStripe.mat", stripe);
            
            // Brush
            Material brush = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            brush.color = GetColor("#29B6F6");
            CreateMat("Assets/_Project/Art/Generated/M_Brush.mat", brush);
            
            // Metal
            Material metal = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            metal.color = GetColor("#9E9E9E");
            metal.SetFloat("_Metallic", 0.8f);
            metal.SetFloat("_Smoothness", 0.5f);
            CreateMat("Assets/_Project/Art/Generated/M_Metal.mat", metal);
            
            AssetDatabase.SaveAssets();
        }

        private static Color GetColor(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out Color c);
            return c;
        }

        private static void SetupRoads(Transform root)
        {
            Transform ground = root.Find("Ground");
            if (ground != null && ground.TryGetComponent<Renderer>(out var gr))
            {
                gr.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Generated/M_Grass.mat");
            }

            GameObject tilePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Art/road_square.prefab");
            GameObject walkPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Art/tile_low.prefab");

            foreach (var r in root.GetComponentsInChildren<Renderer>())
            {
                if (r.gameObject.layer == LayerMask.NameToLayer("Road"))
                {
                    r.enabled = false;
                    Bounds b = r.bounds;
                    GameObject prefab = r.name.Contains("ServiceArea") ? walkPrefab : tilePrefab;
                    
                    // Create grid parent (unscaled, attached to layout root)
                    Transform gridRoot = new GameObject("RoadGrid_" + r.name).transform;
                    gridRoot.SetParent(root, false);
                    gridRoot.localPosition = Vector3.zero;
                    gridRoot.localRotation = Quaternion.identity;
                    gridRoot.localScale = Vector3.one;
                    
                    float size = 4f;
                    int xCount = Mathf.CeilToInt(b.size.x / size);
                    int zCount = Mathf.CeilToInt(b.size.z / size);
                    
                    float startX = b.center.x - b.size.x / 2f + size / 2f;
                    float startZ = b.center.z - b.size.z / 2f + size / 2f;

                    for (int x = 0; x < xCount; x++)
                    {
                        for (int z = 0; z < zCount; z++)
                        {
                            Vector3 pos = new Vector3(startX + x * size, 0.05f, startZ + z * size);
                            if (prefab != null)
                            {
                                var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, gridRoot);
                                inst.transform.position = pos;
                            }
                        }
                    }

                    if (r.name.Contains("Parking"))
                    {
                        // Lines
                        Material lineMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Generated/M_LineWhite.mat");
                        for (int i = 0; i <= 4; i++)
                        {
                            GameObject line = GameObject.CreatePrimitive(PrimitiveType.Quad);
                            line.transform.SetParent(gridRoot, true);
                            // Enclose the 4 parking slots which are at X=14, 18, 22, 26 and Z=-8
                            line.transform.position = new Vector3(12f + i * 4f, 0.06f, -8f);
                            line.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                            line.transform.localScale = new Vector3(0.1f, 4.4f, 1f);
                            line.GetComponent<Renderer>().sharedMaterial = lineMat;
                            Object.DestroyImmediate(line.GetComponent<Collider>());
                        }
                    }
                }
            }
        }
    }
}
