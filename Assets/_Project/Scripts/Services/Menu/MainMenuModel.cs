using System;
using AutoService.Services.Save;
using AutoService.Services.Scenes;

namespace AutoService.Services.Menu
{
    /// <summary>
    /// Logic of the main menu: whether there is a game to continue, and starting a new one only after the player agreed
    /// to lose the old progress.
    /// </summary>
    public sealed class MainMenuModel
    {
        private readonly ISaveService _save;
        private readonly ISceneLoader _scenes;

        /// <summary>Creates the model.</summary>
        /// <param name="save">The save slot.</param>
        /// <param name="scenes">Loads the gameplay scene.</param>
        /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
        public MainMenuModel(ISaveService save, ISceneLoader scenes)
        {
            _save = save ?? throw new ArgumentNullException(nameof(save));
            _scenes = scenes ?? throw new ArgumentNullException(nameof(scenes));
        }

        /// <summary>True when a save exists, so Continue makes sense.</summary>
        public bool CanContinue => _save.HasSave;

        /// <summary>Loads the gameplay scene, which restores the save.</summary>
        public void Continue()
        {
            _scenes.Load(GameScene.Gameplay);
        }

        /// <summary>
        /// Starts a new game, or asks for confirmation first when that would erase a save.
        /// </summary>
        /// <returns><see cref="NewGameResult.NeedsConfirmation"/> (nothing changed) when a save exists; otherwise
        /// <see cref="NewGameResult.Started"/>.</returns>
        public NewGameResult NewGame()
        {
            if (_save.HasSave)
            {
                return NewGameResult.NeedsConfirmation;
            }

            StartFresh();
            return NewGameResult.Started;
        }

        /// <summary>The player agreed to lose the progress: deletes the save and loads the gameplay scene.</summary>
        public void ConfirmNewGame()
        {
            StartFresh();
        }

        private void StartFresh()
        {
            // Why: deleted even when HasSave reported nothing — cheap insurance that a new game never picks up leftovers
            // of an old one.
            _save.Delete();
            _scenes.Load(GameScene.Gameplay);
        }
    }
}
