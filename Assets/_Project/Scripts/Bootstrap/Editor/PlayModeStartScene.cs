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
            EditorApplication.delayCall += Apply;
        }

        private static void Apply()
        {
            var bootScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(BootScenePath);
            if (bootScene == null)
            {
                Debug.LogWarning("[PlayModeStartScene] Boot scene not found at '" + BootScenePath +
                                 "'. Create it (see PR 01 Editor setup) so Play Mode starts through the Composition Root.");
                return;
            }

            EditorSceneManager.playModeStartScene = bootScene;
        }
    }
}
