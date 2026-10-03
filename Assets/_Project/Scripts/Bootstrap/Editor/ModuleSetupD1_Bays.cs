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

                if (doorwayPrefab != null)
                {
                    var front = (GameObject)PrefabUtility.InstantiatePrefab(doorwayPrefab, visual.transform);
                    front.transform.localPosition = new Vector3(0, 0, 3f);
                    
                    var back = (GameObject)PrefabUtility.InstantiatePrefab(doorwayPrefab, visual.transform);
                    back.transform.localPosition = new Vector3(0, 0, -3f);
                    back.transform.localRotation = Quaternion.Euler(0, 180, 0);

                    // Add TMP Sign
                    var signObj = new GameObject("Sign");
                    signObj.transform.SetParent(front.transform, false);
                    signObj.transform.localPosition = new Vector3(0, 3.5f, 0);
                    var txt = signObj.AddComponent<TextMeshPro>();
                    txt.text = bay.ServiceTypeId.ToUpper();
                    txt.fontSize = 5;
                    txt.alignment = TextAlignmentOptions.Center;
                    txt.color = Color.white;
                }

                if (wallPrefab != null)
                {
                    var left1 = (GameObject)PrefabUtility.InstantiatePrefab(wallPrefab, visual.transform);
                    left1.transform.localPosition = new Vector3(-2.5f, 0, 1.5f);
                    left1.transform.localRotation = Quaternion.Euler(0, -90, 0);
                    
                    var left2 = (GameObject)PrefabUtility.InstantiatePrefab(wallPrefab, visual.transform);
                    left2.transform.localPosition = new Vector3(-2.5f, 0, -1.5f);
                    left2.transform.localRotation = Quaternion.Euler(0, -90, 0);

                    var right1 = (GameObject)PrefabUtility.InstantiatePrefab(wallPrefab, visual.transform);
                    right1.transform.localPosition = new Vector3(2.5f, 0, 1.5f);
                    right1.transform.localRotation = Quaternion.Euler(0, 90, 0);

                    var right2 = (GameObject)PrefabUtility.InstantiatePrefab(wallPrefab, visual.transform);
                    right2.transform.localPosition = new Vector3(2.5f, 0, -1.5f);
                    right2.transform.localRotation = Quaternion.Euler(0, 90, 0);
                }

                if (roofPrefab != null)
                {
                    var roof = (GameObject)PrefabUtility.InstantiatePrefab(roofPrefab, visual.transform);
                    roof.transform.localPosition = new Vector3(0, 3f, 0);
                    roof.transform.localScale = new Vector3(1.2f, 1f, 1.2f);
                }

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
                    
                    GameObject scanner = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Art/scanner-high.prefab");
                    if (scanner != null) PrefabUtility.InstantiatePrefab(scanner, fxObj.transform);

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
                    var tireFx = bay.GetComponent<TireFx>();
                    if (tireFx == null) tireFx = bay.gameObject.AddComponent<TireFx>();
                    GameObject arm = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Art/robot-arm-a.prefab");
                    GameObject tirePref = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Art/wheel-dark.prefab");
                    
                    Transform rArm = null;
                    if (arm != null)
                    {
                        var a = (GameObject)PrefabUtility.InstantiatePrefab(arm, fxObj.transform);
                        a.transform.localPosition = new Vector3(-2f, 0, 0);
                        rArm = a.transform;
                    }

                    var stack = new GameObject("TireStack");
                    stack.transform.SetParent(fxObj.transform, false);
                    stack.transform.localPosition = new Vector3(2f, 0, 0);
                    
                    if (tirePref != null)
                    {
                        for (int i=0; i<3; i++)
                        {
                            var t = (GameObject)PrefabUtility.InstantiatePrefab(tirePref, stack.transform);
                            t.transform.localPosition = new Vector3(0, i * 0.3f, 0);
                            t.transform.localRotation = Quaternion.Euler(90, 0, 0);
                        }
                    }

                    var serialized = new SerializedObject(tireFx);
                    serialized.FindProperty("_robotArm").objectReferenceValue = rArm;
                    serialized.FindProperty("_tires").objectReferenceValue = stack.transform;
                    serialized.ApplyModifiedProperties();
                }

                // If ghost exists, rebuild it
                string ghostName = "Ghost_" + bay.PointId;
                Transform ghost = layout.transform.Find(ghostName);
                if (ghost != null)
                {
                    string plotId = ghost.GetComponent<BuildPlotView>().PlotId;
                    ServicePointView washBay = null;
                    foreach (var b in bays) if (b.PointId == "loc1_wash_1") { washBay = b; break; }
                    
                    if (washBay != null)
                    {
                        Vector3 shift = bay.transform.position - washBay.transform.position;
                        Object.DestroyImmediate(ghost.gameObject);
                        WhiteboxLocationBuilder.CreateBayGhost(washBay, bay, plotId, bay.PointId, shift, ghostMat, washBay.Hud);
                    }
                }
            }
        }
    }
}
