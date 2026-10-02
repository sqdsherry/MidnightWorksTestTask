using System;
using AutoService.Presentation.Ui;
using AutoService.Services.Menu;

namespace AutoService.Presentation.Menu
{
    /// <summary>
    /// Connects the <see cref="MainMenuView"/> and its confirmation dialog to the <see cref="MainMenuModel"/>:
    /// Continue only with a save, New Game asks before erasing one, Quit leaves the game.
    /// </summary>
    public sealed class MainMenuPresenter : IDisposable
    {
        private readonly MainMenuModel _model;
        private readonly MainMenuView _view;
        private readonly ConfirmDialogView _confirm;
        private bool _disposed;

        /// <summary>Creates the presenter, subscribes to the views and shows the menu.</summary>
        /// <param name="model">Main menu logic.</param>
        /// <param name="view">The menu.</param>
        /// <param name="confirm">Dialog asking before a new game erases the save.</param>
        /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
        public MainMenuPresenter(MainMenuModel model, MainMenuView view, ConfirmDialogView confirm)
        {
            _model = model ?? throw new ArgumentNullException(nameof(model));
            _view = view != null ? view : throw new ArgumentNullException(nameof(view));
            _confirm = confirm != null ? confirm : throw new ArgumentNullException(nameof(confirm));

            _view.ContinueClicked += OnContinue;
            _view.NewGameClicked += OnNewGame;
            _view.QuitClicked += OnQuit;
            _confirm.YesClicked += OnConfirmed;
            _confirm.NoClicked += OnDeclined;

            _confirm.Hide();
            _view.SetContinueAvailable(_model.CanContinue);
            _view.Show();
        }

        /// <summary>Unsubscribes from the views. Safe to call repeatedly.</summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            // Why: Unity's == — the views may already be destroyed while the scene unloads.
            if (_view != null)
            {
                _view.ContinueClicked -= OnContinue;
                _view.NewGameClicked -= OnNewGame;
                _view.QuitClicked -= OnQuit;
            }

            if (_confirm != null)
            {
                _confirm.YesClicked -= OnConfirmed;
                _confirm.NoClicked -= OnDeclined;
            }
        }

        private void OnContinue()
        {
            if (_model.CanContinue)
            {
                _model.Continue();
            }
        }

        private void OnNewGame()
        {
            if (_model.NewGame() == NewGameResult.NeedsConfirmation)
            {
                _confirm.Show(_view.NewGameConfirmText);
            }
        }

        private void OnConfirmed()
        {
            _confirm.Hide();
            _model.ConfirmNewGame();
        }

        private void OnDeclined() => _confirm.Hide();

        private static void OnQuit() => GameQuitter.Quit();
    }
}
