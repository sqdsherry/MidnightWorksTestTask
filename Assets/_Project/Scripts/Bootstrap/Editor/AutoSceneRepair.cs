using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AutoService.Bootstrap.Editor
{
    [InitializeOnLoad]
    public static class AutoSceneRepair
    {
        static AutoSceneRepair()
        {
            EditorApplication.delayCall += Repair;
        }

        private static void Repair()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
            
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "Gameplay") return;
            
            Debug.Log("[AutoSceneRepair] Ensuring restored Gameplay.unity has all Task 20 and 21 setups applied...");
            try
            {
                Location2SceneUpdater.ApplyFixes();
                PopupsAndCheatsSetup.RunSetup();
                AudioSetup.ConfigureAudio();
                Debug.Log("[AutoSceneRepair] Successfully auto-repaired Gameplay.unity with Task 20, 21 & 22 setups!");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[AutoSceneRepair] Exception during setup: {ex}");
            }
        }
    }
}
