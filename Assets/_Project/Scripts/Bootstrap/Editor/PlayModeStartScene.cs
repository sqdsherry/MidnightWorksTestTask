using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AutoService.Bootstrap.Editor
{
    /// <summary>
    /// Makes Play Mode always start from the Boot scene, whichever scene is open in the editor,
    /// so the project Composition Root is always built first.
    /// </summary>
    [InitializeOnLoad]
    internal static class PlayModeStartScene
    {
        private const string BootScenePath = "Assets/_Project/Scenes/Boot.unity";

        static PlayModeStartScene()
        {
            // Why: deferred — during the very first domain reload after import the AssetDatabase may not be ready yet.
            EditorApplication.delayCall += OnEditorReady;

            // Why: picks up Boot.unity as soon as it is created/moved/renamed, without waiting for a recompile.
            EditorApplication.projectChanged += OnProjectChanged;
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
        }

        private static void OnEditorReady()
        {
            if (!TryApply())
            {
                Debug.LogWarning("[PlayModeStartScene] Boot scene not found at '" + BootScenePath +
                                 "'. Create it (see PR 01 Editor setup) so Play Mode starts through the Composition Root.");
            }
        }

        private static void OnProjectChanged()
        {
            // Why: silent here — projectChanged fires on every asset change, a warning each time would flood the console.
            TryApply();
        }

        private static void OnBeforeAssemblyReload()
        {
            EditorApplication.projectChanged -= OnProjectChanged;
            AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;
        }

        private static bool TryApply()
        {
            var bootScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(BootScenePath);
            if (bootScene == null)
            {
                // Boot was deleted or moved: fall back to the default behaviour (play the open scene).
                if (EditorSceneManager.playModeStartScene != null)
                {
                    EditorSceneManager.playModeStartScene = null;
                }

                return false;
            }

            if (EditorSceneManager.playModeStartScene != bootScene)
            {
                EditorSceneManager.playModeStartScene = bootScene;
            }

            return true;
        }
    }
}
