using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace AutoService.Bootstrap.Editor
{
    public static class CharacterSetupHelper
    {
        private const string ControllerPath = "Assets/_Project/Art/Characters/AC_Character.controller";
        private const string MaleAFbx = "Assets/_Project/Art/Kenney/kenney_mini-characters/character-male-a.fbx";
        private const string MaleBFbx = "Assets/_Project/Art/Kenney/kenney_mini-characters/character-male-b.fbx";
        private const string MaleDFbx = "Assets/_Project/Art/Kenney/kenney_mini-characters/character-male-d.fbx";
        private const string BoxSmallPrefab = "Assets/_Project/Prefabs/Art/box-small.prefab";

        [MenuItem("AutoService/Setup/Configure Characters And Animations")]
        public static void ConfigureAll()
        {
            SetupAnimatorController();
            SetupStaffPrefabs();
            SetupPlayerInScene();
            AssetDatabase.SaveAssets();
            Debug.Log("[CharacterSetupHelper] Character setup and animations completed successfully!");
        }

        private static void SetupAnimatorController()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
            {
                Debug.LogError("[CharacterSetupHelper] Controller not found at " + ControllerPath);
                return;
            }

            // Find clips from character-male-a
            AnimationClip idleClip = null;
            AnimationClip walkClip = null;
            AnimationClip carryIdleClip = null;
            AnimationClip carryWalkClip = null;

            var assets = AssetDatabase.LoadAllAssetsAtPath(MaleAFbx);
            foreach (var asset in assets)
            {
                if (asset is AnimationClip clip)
                {
                    if (clip.name == "idle") idleClip = clip;
                    else if (clip.name == "walk") walkClip = clip;
                    else if (clip.name == "holding-both" || clip.name.Contains("holding"))
                    {
                        if (carryIdleClip == null) carryIdleClip = clip;
                    }
                }
            }

            if (carryIdleClip == null) carryIdleClip = idleClip;
            carryWalkClip = walkClip; // Kenney uses walk while holding or base walk

            foreach (var layer in controller.layers)
            {
                foreach (var state in layer.stateMachine.states)
                {
                    if (state.state.name == "Idle") state.state.motion = idleClip;
                    else if (state.state.name == "Walk") state.state.motion = walkClip;
                    else if (state.state.name == "CarryIdle") state.state.motion = carryIdleClip;
                    else if (state.state.name == "CarryWalk") state.state.motion = carryWalkClip;
                }
            }

            EditorUtility.SetDirty(controller);
            Debug.Log("[CharacterSetupHelper] AC_Character controller wired with clips from " + MaleAFbx);
        }

        private static void SetupStaffPrefabs()
        {
            string[] staffPrefabPaths = { "Assets/_Project/Prefabs/Staff.prefab", "Assets/_Project/Prefabs/Staff_0.prefab" };
            var maleBPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(MaleBFbx);
            var maleDPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(MaleDFbx);
            var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
            var boxSmall = AssetDatabase.LoadAssetAtPath<GameObject>(BoxSmallPrefab);

            foreach (var path in staffPrefabPaths)
            {
                if (!File.Exists(path)) continue;
                using (var scope = new PrefabUtility.EditPrefabContentsScope(path))
                {
                    GameObject root = scope.prefabContentsRoot;
                    var staffView = root.GetComponent<AutoService.Presentation.Staff.StaffView>();

                    // 1. Disable primitive Capsule / Head / Body
                    Transform oldHead = root.transform.Find("Head");
                    if (oldHead != null) oldHead.gameObject.SetActive(false);
                    Transform oldBody = root.transform.Find("Body");
                    if (oldBody != null) oldBody.gameObject.SetActive(false);

                    // 2. Setup Visual root with Worker and Storekeeper
                    Transform visualRoot = root.transform.Find("Visual");
                    if (visualRoot == null)
                    {
                        var visGo = new GameObject("Visual");
                        visGo.transform.SetParent(root.transform, false);
                        visualRoot = visGo.transform;
                    }

                    // Worker model (male-b)
                    Transform worker = visualRoot.Find("Worker");
                    if (worker == null)
                    {
                        var wGo = (GameObject)PrefabUtility.InstantiatePrefab(maleBPrefab, visualRoot);
                        wGo.name = "Worker";
                        worker = wGo.transform;
                        worker.localPosition = Vector3.zero;
                        worker.localRotation = Quaternion.identity;
                    }

                    // Storekeeper model (male-d)
                    Transform storekeeper = visualRoot.Find("Storekeeper");
                    if (storekeeper == null)
                    {
                        var sGo = (GameObject)PrefabUtility.InstantiatePrefab(maleDPrefab, visualRoot);
                        sGo.name = "Storekeeper";
                        storekeeper = sGo.transform;
                        storekeeper.localPosition = Vector3.zero;
                        storekeeper.localRotation = Quaternion.identity;
                    }

                    // Ensure Animator on root or children
                    var anim = root.GetComponent<Animator>();
                    if (anim == null) anim = root.AddComponent<Animator>();
                    anim.runtimeAnimatorController = controller;
                    anim.applyRootMotion = false;

                    // Ensure Avatar from male-b if available
                    var avatar = AssetDatabase.LoadAssetAtPath<Avatar>(MaleBFbx);
                    if (avatar != null) anim.avatar = avatar;

                    // Also remove or disable primitive Box under CarrySocket and replace with box-small
                    Transform carrySocket = root.transform.Find("CarrySocket");
                    Renderer boxRen = null;
                    if (carrySocket != null)
                    {
                        carrySocket.localPosition = new Vector3(0f, 0.7f, 0.45f);
                        Transform oldBox = carrySocket.Find("Box");
                        if (oldBox != null) oldBox.gameObject.SetActive(false);

                        Transform newBox = carrySocket.Find("BoxVisual");
                        if (newBox == null && boxSmall != null)
                        {
                            var bGo = (GameObject)PrefabUtility.InstantiatePrefab(boxSmall, carrySocket);
                            bGo.name = "BoxVisual";
                            bGo.transform.localPosition = Vector3.zero;
                            bGo.transform.localRotation = Quaternion.identity;
                            bGo.transform.localScale = new Vector3(1f, 1f, 1f);
                            newBox = bGo.transform;
                        }

                        if (newBox != null)
                        {
                            boxRen = newBox.GetComponentInChildren<Renderer>();
                        }
                    }

                    // Serialized object update for StaffView
                    if (staffView != null)
                    {
                        var so = new SerializedObject(staffView);
                        var boxProp = so.FindProperty("_boxRenderer");
                        if (boxProp != null && boxRen != null)
                        {
                            boxProp.objectReferenceValue = boxRen;
                        }
                        var bodyProp = so.FindProperty("_body");
                        if (bodyProp != null)
                        {
                            bodyProp.objectReferenceValue = worker.GetComponentInChildren<Renderer>();
                        }
                        so.ApplyModifiedProperties();
                    }

                    // Initial state: Worker active, Storekeeper inactive
                    worker.gameObject.SetActive(true);
                    storekeeper.gameObject.SetActive(false);
                }
                Debug.Log("[CharacterSetupHelper] Configured staff prefab: " + path);
            }
        }

        private static void SetupPlayerInScene()
        {
            var maleAPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(MaleAFbx);
            var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
            var boxSmall = AssetDatabase.LoadAssetAtPath<GameObject>(BoxSmallPrefab);

            var player = GameObject.Find("Player");
            if (player == null)
            {
                Debug.LogWarning("[CharacterSetupHelper] Player GameObject not found in scene.");
                return;
            }

            // Enforce Player position Y = 0 and NavMeshAgent BaseOffset = 0
            Vector3 playerPos = player.transform.position;
            playerPos.y = 0f;
            player.transform.position = playerPos;

            var agent = player.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (agent != null)
            {
                agent.baseOffset = 0f;
                EditorUtility.SetDirty(agent);
            }

            // Disable player capsule renderer & filter
            var meshFilter = player.GetComponent<MeshFilter>();
            if (meshFilter != null) UnityEngine.Object.DestroyImmediate(meshFilter);
            var meshRenderer = player.GetComponent<MeshRenderer>();
            if (meshRenderer != null) UnityEngine.Object.DestroyImmediate(meshRenderer);

            // Add Kenney 3D model under Visual child
            Transform visual = player.transform.Find("Visual");
            if (visual == null)
            {
                var vGo = (GameObject)PrefabUtility.InstantiatePrefab(maleAPrefab, player.transform);
                vGo.name = "Visual";
                visual = vGo.transform;
            }
            visual.localPosition = Vector3.zero;
            visual.localRotation = Quaternion.identity;

            // Ensure Animator on Player
            var anim = player.GetComponent<Animator>();
            if (anim == null) anim = player.AddComponent<Animator>();
            anim.runtimeAnimatorController = controller;
            anim.applyRootMotion = false;
            var avatar = AssetDatabase.LoadAssetAtPath<Avatar>(MaleAFbx);
            if (avatar != null) anim.avatar = avatar;

            // CarrySocket and Box for PlayerCarryView
            var playerCarry = player.GetComponent<AutoService.Presentation.Supplies.PlayerCarryView>();
            Transform socket = player.transform.Find("CarrySocket");
            Renderer boxRen = null;
            if (socket != null)
            {
                socket.localPosition = new Vector3(0f, 0.7f, 0.45f);
                Transform oldBox = socket.Find("Box");
                if (oldBox != null) oldBox.gameObject.SetActive(false);

                Transform newBox = socket.Find("BoxVisual");
                if (newBox == null && boxSmall != null)
                {
                    var bGo = (GameObject)PrefabUtility.InstantiatePrefab(boxSmall, socket);
                    bGo.name = "BoxVisual";
                    bGo.transform.localPosition = Vector3.zero;
                    bGo.transform.localRotation = Quaternion.identity;
                    newBox = bGo.transform;
                }
                if (newBox != null)
                {
                    boxRen = newBox.GetComponentInChildren<Renderer>();
                }
            }

            if (playerCarry != null && boxRen != null)
            {
                var so = new SerializedObject(playerCarry);
                var boxProp = so.FindProperty("_boxRenderer");
                if (boxProp != null)
                {
                    boxProp.objectReferenceValue = boxRen;
                }
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(playerCarry);
            }

            EditorUtility.SetDirty(player);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(player.scene);
            Debug.Log("[CharacterSetupHelper] Configured Player in scene with 3D model and Animator.");
        }
    }
}
