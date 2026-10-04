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
    public static partial class ModuleSetupD1
    {
        private static void SetupBays(LocationLayout layout)
        {
            var bays = layout.GetComponentsInChildren<ServicePointView>(true);
            Material ghostMat = WhiteboxLocationBuilder.GetOrCreateGhostMaterial();
            
            // Clean up old ghosts
            var childrenToDestroy = new List<Transform>();
            foreach (Transform t in layout.transform)
            {
                if (t.name.StartsWith("Ghost_loc1_")) childrenToDestroy.Add(t);
            }
            foreach (Transform t in childrenToDestroy)
            {
                Object.DestroyImmediate(t.gameObject);
            }
            
            foreach (var bay in bays)
            {
                if (!bay.name.StartsWith("Bay_") && bay.PointId != "loc1_wash_1") continue;

                // Rebuild visuals
                Transform oldVisual = bay.transform.Find("Visual");
                if (oldVisual != null) Object.DestroyImmediate(oldVisual.gameObject);

                GameObject visual = new GameObject("Visual");
                visual.transform.SetParent(bay.transform, false);
                visual.transform.localPosition = Vector3.zero;

                GameObject wallPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Art/structure-wall.prefab");
                GameObject doorwayPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Art/structure-doorway-wide.prefab");
                GameObject roofPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Art/top-large.prefab");

                // Add TMP Sign
                var signObj = new GameObject("Sign");
                signObj.transform.SetParent(visual.transform, false);
                signObj.transform.localPosition = new Vector3(0, 3.5f, 3f);
                var txt = signObj.AddComponent<TextMeshPro>();
                txt.text = bay.ServiceTypeId.ToUpper();
                txt.fontSize = 5;
                txt.alignment = TextAlignmentOptions.Center;
                txt.color = Color.white;

                // Add Fx
                var fxObj = new GameObject("Fx");
                fxObj.transform.SetParent(visual.transform, false);

                if (bay.ServiceTypeId == "wash")
                {
                    var washFx = bay.GetComponent<WashFx>();
                    if (washFx == null) washFx = bay.gameObject.AddComponent<WashFx>();

                    var foamObj = new GameObject("Foam");
                    foamObj.transform.SetParent(fxObj.transform, false);
                    foamObj.transform.localPosition = new Vector3(0, 2f, 0);
                    var ps = foamObj.AddComponent<ParticleSystem>();
                    var main = ps.main;
                    main.useUnscaledTime = false;
                    main.startLifetime = 1f;
                    main.startSpeed = 2f;
                    var em = ps.emission;
                    em.rateOverTime = 30;
                    var shape = ps.shape;
                    shape.shapeType = ParticleSystemShapeType.Box;
                    shape.scale = new Vector3(3f, 1f, 3f);
                    
                    var bMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Generated/M_Brush.mat");
                    var bl = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    bl.transform.SetParent(fxObj.transform, false);
                    bl.transform.localPosition = new Vector3(-1.5f, 1f, 0);
                    bl.transform.localScale = new Vector3(0.6f, 1f, 0.6f);
                    bl.GetComponent<Renderer>().sharedMaterial = bMat;
                    Object.DestroyImmediate(bl.GetComponent<Collider>());
                    
                    var br = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    br.transform.SetParent(fxObj.transform, false);
                    br.transform.localPosition = new Vector3(1.5f, 1f, 0);
                    br.transform.localScale = new Vector3(0.6f, 1f, 0.6f);
                    br.GetComponent<Renderer>().sharedMaterial = bMat;
                    Object.DestroyImmediate(br.GetComponent<Collider>());

                    var serialized = new SerializedObject(washFx);
                    serialized.FindProperty("_leftBrush").objectReferenceValue = bl.transform;
                    serialized.FindProperty("_rightBrush").objectReferenceValue = br.transform;
                    serialized.FindProperty("_foam").objectReferenceValue = ps;
                    serialized.ApplyModifiedProperties();
                }
                else if (bay.ServiceTypeId == "oil")
                {
                    var liftFx = bay.GetComponent<LiftFx>();
                    if (liftFx == null) liftFx = bay.gameObject.AddComponent<LiftFx>();
                    var mMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Generated/M_Metal.mat");
                    
                    var pl = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    pl.transform.SetParent(fxObj.transform, false);
                    pl.transform.localPosition = new Vector3(-1f, 0, 0);
                    pl.transform.localScale = new Vector3(0.5f, 0.15f, 3f);
                    pl.GetComponent<Renderer>().sharedMaterial = mMat;
                    
                    var pr = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    pr.transform.SetParent(fxObj.transform, false);
                    pr.transform.localPosition = new Vector3(1f, 0, 0);
                    pr.transform.localScale = new Vector3(0.5f, 0.15f, 3f);
                    pr.GetComponent<Renderer>().sharedMaterial = mMat;

                    GameObject cover = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Art/cover-hopper.prefab");
                    if (cover != null)
                    {
                        var c = (GameObject)PrefabUtility.InstantiatePrefab(cover, fxObj.transform);
                        c.transform.localPosition = new Vector3(2f, 0, 0);
                    }

                    var serialized = new SerializedObject(liftFx);
                    serialized.FindProperty("_plates").arraySize = 2;
                    serialized.FindProperty("_plates").GetArrayElementAtIndex(0).objectReferenceValue = pl.transform;
                    serialized.FindProperty("_plates").GetArrayElementAtIndex(1).objectReferenceValue = pr.transform;
                    serialized.ApplyModifiedProperties();
                }
                else if (bay.ServiceTypeId == "tires")
                {
                    // Tires removed to second location.
                }

                ServicePointView washBay = null;
                foreach (var b in bays) if (b.PointId == "loc1_wash_1") { washBay = b; break; }

                string plotId = null;
                if (bay.PointId == "loc1_wash_1") plotId = "loc1_build_wash_1";
                else if (bay.PointId == "loc1_wash_2") plotId = "loc1_build_wash_2";
                else if (bay.PointId == "loc1_oil_1") plotId = "loc1_build_oil_1";
                else if (bay.PointId == "loc1_oil_2") plotId = "loc1_build_oil_2";

                if (plotId != null && washBay != null)
                {
                    Vector3 shift = bay.transform.position - washBay.transform.position;
                    WhiteboxLocationBuilder.CreateBayGhost(washBay, bay, plotId, bay.PointId, shift, ghostMat, washBay.Hud);
                    bay.gameObject.SetActive(false);
                }
            }

            // Sync build plots on LocationLayout to avoid null references
            var allPlots = layout.GetComponentsInChildren<BuildPlotView>(true);
            var validPlots = new List<BuildPlotView>();
            foreach (var p in allPlots)
            {
                if (p != null) validPlots.Add(p);
            }
            
            var serializedLayout = new SerializedObject(layout);
            var plotsProp = serializedLayout.FindProperty("_buildPlots");
            plotsProp.arraySize = validPlots.Count;
            for (int i = 0; i < validPlots.Count; i++)
            {
                plotsProp.GetArrayElementAtIndex(i).objectReferenceValue = validPlots[i];
            }
            serializedLayout.ApplyModifiedProperties();
            EditorUtility.SetDirty(layout);
        }
    }
}
