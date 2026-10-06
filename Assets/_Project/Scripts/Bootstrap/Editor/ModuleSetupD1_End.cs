using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using AutoService.Bootstrap.Editor;
using AutoService.Presentation.Building;
using AutoService.Presentation.Points;
using AutoService.Presentation.Traffic;
using AutoService.Presentation.Player;
using AutoService.Presentation.Staff;

namespace AutoService.Bootstrap.Editor
{
    public static partial class ModuleSetupD1
    {
        private static void SetupDecorAndLight(LocationLayout layout)
        {
            Transform decorGrp = layout.transform.Find("Decor");
            if (decorGrp == null) decorGrp = new GameObject("Decor").transform;
            decorGrp.SetParent(layout.transform, false);

            GameObject billboard = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Art/billboard.prefab");
            if (billboard != null)
            {
                var bb = (GameObject)PrefabUtility.InstantiatePrefab(billboard, decorGrp);
                bb.transform.position = new Vector3(-3f, 0, 15f);
                bb.transform.rotation = Quaternion.Euler(0, 180, 0);

                var signObj = new GameObject("Sign");
                signObj.transform.SetParent(bb.transform, false);
                signObj.transform.localPosition = new Vector3(0, 4.5f, 0.5f);
                var txt = signObj.AddComponent<TMPro.TextMeshPro>();
                txt.text = "AUTO SERVICE\nTYCOON";
                txt.fontSize = 8;
                txt.alignment = TMPro.TextAlignmentOptions.Center;
                txt.color = Color.black;
            }

            GameObject tree = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Art/treeLarge.prefab");
            if (tree != null)
            {
                var t = (GameObject)PrefabUtility.InstantiatePrefab(tree, decorGrp);
                t.transform.position = new Vector3(-25f, 0, 15f);
                var t2 = (GameObject)PrefabUtility.InstantiatePrefab(tree, decorGrp);
                t2.transform.position = new Vector3(-15f, 0, 15f);
                var t3 = (GameObject)PrefabUtility.InstantiatePrefab(tree, decorGrp);
                t3.transform.position = new Vector3(20f, 0, 12f);
            }

            // Light
            var lights = Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Light dirLight = null;
            foreach (var l in lights) if (l.type == LightType.Directional) { dirLight = l; break; }
            if (dirLight != null)
            {
                dirLight.color = GetColor("#FFF4E0");
                dirLight.transform.rotation = Quaternion.Euler(50, -30, 0);
                dirLight.shadows = LightShadows.Soft;
                dirLight.intensity = 0.6f;
            }

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = GetColor("#A7D8FF");
            RenderSettings.ambientEquatorColor = GetColor("#DDE7EE");
            RenderSettings.ambientGroundColor = GetColor("#8D9A7A");

            Camera.main.backgroundColor = GetColor("#A7D8FF");
            Camera.main.clearFlags = CameraClearFlags.SolidColor;

            var volume = Object.FindAnyObjectByType<Volume>();
            if (volume != null && volume.profile != null)
            {
                if (!volume.profile.TryGet<Tonemapping>(out var tone))
                    tone = volume.profile.Add<Tonemapping>();
                tone.active = true;
                tone.mode.value = TonemappingMode.Neutral;

                if (!volume.profile.TryGet<ColorAdjustments>(out var color))
                    color = volume.profile.Add<ColorAdjustments>();
                color.active = true;
                color.saturation.value = 10f;
            }
        }

        private static void SetupCharacters()
        {
            string path = "Assets/_Project/Art/Characters/AC_Character.controller";
            Directory.CreateDirectory("Assets/_Project/Art/Characters");
            var controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Carrying", AnimatorControllerParameterType.Bool);

            var root = controller.layers[0].stateMachine;

            var idle = root.AddState("Idle");
            var walk = root.AddState("Walk");
            var carryIdle = root.AddState("CarryIdle");
            var carryWalk = root.AddState("CarryWalk");

            // Assuming motions exist, but for now we just create the states and transitions.
            // Importer maps animations to names 'idle', 'walk', 'holding-both', 'interact'
            
            // Transitions
            var i2w = idle.AddTransition(walk);
            i2w.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
            var w2i = walk.AddTransition(idle);
            w2i.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");

            var i2ci = idle.AddTransition(carryIdle);
            i2ci.AddCondition(AnimatorConditionMode.If, 0, "Carrying");
            var ci2i = carryIdle.AddTransition(idle);
            ci2i.AddCondition(AnimatorConditionMode.IfNot, 0, "Carrying");

            var w2cw = walk.AddTransition(carryWalk);
            w2cw.AddCondition(AnimatorConditionMode.If, 0, "Carrying");
            var cw2w = carryWalk.AddTransition(walk);
            cw2w.AddCondition(AnimatorConditionMode.IfNot, 0, "Carrying");

            var ci2cw = carryIdle.AddTransition(carryWalk);
            ci2cw.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
            var cw2ci = carryWalk.AddTransition(carryIdle);
            cw2ci.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");

            // We must assign this to prefabs.
            GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Characters/Player.prefab");
            if (playerPrefab != null) ReplaceCharacterVisual(playerPrefab, "character-male-a.fbx", controller);

            GameObject staffPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Characters/Staff.prefab");
            if (staffPrefab != null) ReplaceCharacterVisual(staffPrefab, "character-male-e.fbx", controller);
        }

        private static void ReplaceCharacterVisual(GameObject prefab, string fbxName, AnimatorController controller)
        {
            using (var editScope = new PrefabUtility.EditPrefabContentsScope(AssetDatabase.GetAssetPath(prefab)))
            {
                var root = editScope.prefabContentsRoot;
                Transform oldVis = root.transform.Find("Visual");
                if (oldVis != null) Object.DestroyImmediate(oldVis.gameObject);

                var vis = new GameObject("Visual");
                vis.transform.SetParent(root.transform, false);

                if (prefab.name == "Staff")
                {
                    var worker = InstantiateCharacter(vis.transform, "character-male-e.fbx", controller);
                    worker.name = "Worker";
                    var sk = InstantiateCharacter(vis.transform, "character-female-b.fbx", controller);
                    sk.name = "Storekeeper";
                    sk.gameObject.SetActive(false);
                }
                else
                {
                    var p = InstantiateCharacter(vis.transform, fbxName, controller);
                    p.name = "PlayerVisual";
                }

                // Move CarrySocket
                Transform sock = root.transform.Find("CarrySocket");
                if (sock != null)
                {
                    sock.SetParent(vis.transform, false); // Make sure it's inside Visual
                    sock.localPosition = new Vector3(0, 1.0f, 0.6f);
                    Transform oldBox = sock.Find("Box");
                    if (oldBox != null) Object.DestroyImmediate(oldBox.gameObject);

                    GameObject boxFbx = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Art/box-small.prefab");
                    if (boxFbx != null)
                    {
                        var boxInst = (GameObject)PrefabUtility.InstantiatePrefab(boxFbx, sock);
                        boxInst.name = "Box";
                        boxInst.transform.localPosition = Vector3.zero;
                        
                        if (root.TryGetComponent<StaffView>(out var staffView))
                        {
                            var serialized = new SerializedObject(staffView);
                            serialized.FindProperty("_boxRenderer").objectReferenceValue = boxInst.GetComponentInChildren<Renderer>();
                            serialized.ApplyModifiedProperties();
                        }
                    }
                }
            }
        }

        private static GameObject InstantiateCharacter(Transform parent, string fbxName, AnimatorController controller)
        {
            string prefabName = fbxName.Replace(".fbx", ".prefab");
            string path = "Assets/_Project/Prefabs/Art/" + prefabName;
            GameObject fbx = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (fbx == null) return new GameObject("Empty");

            var inst = (GameObject)PrefabUtility.InstantiatePrefab(fbx, parent);
            inst.transform.localPosition = Vector3.zero;

            // Wait, the prefab HAS a child named 'Model' because we wrapped it.
            // But if the child has an Animator, we should use it.
            var anim = inst.GetComponentInChildren<Animator>();
            if (anim != null) anim.runtimeAnimatorController = controller;
            return inst;
        }

        private static void SetupCars()
        {
            string[] cars = { "Sedan", "Suv", "Sport" };
            string[] fbxs = { "sedan.fbx", "suv.fbx", "sedan-sports.fbx" };
            float[] lengths = { 4.0f, 4.3f, 4.2f };

            for (int i=0; i<cars.Length; i++)
            {
                string path = "Assets/_Project/Prefabs/Cars/Car_" + cars[i] + ".prefab";
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;

                using (var editScope = new PrefabUtility.EditPrefabContentsScope(path))
                {
                    var root = editScope.prefabContentsRoot;
                    Transform oldVis = root.transform.Find("Visual");
                    if (oldVis != null) Object.DestroyImmediate(oldVis.gameObject);

                    string prefabName = fbxs[i].Replace(".fbx", ".prefab");
                    GameObject fbx = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Art/" + prefabName);
                    if (fbx != null)
                    {
                        var inst = (GameObject)PrefabUtility.InstantiatePrefab(fbx, root.transform);
                        inst.name = "Visual";
                        inst.transform.localPosition = Vector3.zero;
                        
                        // Wait, I already did FitToSize on rotation and scale inside CreateWrappers!
                        // The wrapper is already facing -Z (well, localRotation Euler(0,180,0)) and scaled correctly.
                        // I just need to instantiate it!
                    }
                }
            }
        }
    }
}
