using UnityEngine;

namespace AutoService.Presentation.Ui
{
    /// <summary>Quits the game from the menus.</summary>
    public static class GameQuitter
    {
        /// <summary>Stops Play Mode in the editor; quits the application in a build.</summary>
        public static void Quit()
        {
#if UNITY_EDITOR
            // Why: Application.Quit is ignored in the editor; the Quit button should still visibly do something there.
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
