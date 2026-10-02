using System;
using AutoService.Services.Core;
using AutoService.Services.Scenes;

namespace AutoService.Services.Menu
{
    /// <summary>
    /// Logic of the in-game pause menu: holds exactly one pause request while open and leaves to the main menu.
    /// </summary>
    public sealed class PauseMenuModel
    {
        private readonly IPauseService _pause;
        private readonly ISceneLoader _scenes;

        /// <summary>Creates the model.</summary>
        /// <param name="pause">Pause requests.</param>
        /// <param name="scenes">Loads the main menu.</param>
        /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
        public PauseMenuModel(IPauseService pause, ISceneLoader scenes)
        {
            _pause = pause ?? throw new ArgumentNullException(nameof(pause));
            _scenes = scenes ?? throw new ArgumentNullException(nameof(scenes));
        }

        /// <summary>True while the pause menu is open (and holds its pause request).</summary>
        public bool IsOpen { get; private set; }

        /// <summary>Opens the menu and pauses the game. Does nothing if already open.</summary>
        public void Open()
        {
            if (IsOpen)
            {
                return;
            }

            IsOpen = true;
            _pause.Push();
        }

        /// <summary>Closes the menu and releases its pause request. Safe to call when closed.</summary>
        public void Close()
        {
            // Why: guarded, so a second Close (Resume clicked and Esc pressed in one frame) cannot pop someone else's request.
            if (!IsOpen)
            {
                return;
            }

            IsOpen = false;
            _pause.Pop();
        }

        /// <summary>Closes the menu and loads the main menu.</summary>
        public void ToMainMenu()
        {
            // TODO(08b-save): save before leaving to the menu.
            Close();
            _scenes.Load(GameScene.MainMenu);
        }
    }
}
