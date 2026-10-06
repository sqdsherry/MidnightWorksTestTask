using System;
using AutoService.Presentation.Controls;
using AutoService.Presentation.Settings;
using AutoService.Presentation.Ui;
using AutoService.Services.Menu;

namespace AutoService.Presentation.Menu
{
    /// <summary>
    /// Connects the <see cref="MainMenuView"/> and its confirmation dialog to the <see cref="MainMenuModel"/>:
    /// Continue only with a save, New Game asks before erasing one, Settings swaps the menu for the settings screen,
    /// Quit leaves the game.
    /// </summary>
    /// <remarks>
    /// Esc closes the settings, then the confirmation dialog; on the bare menu it does nothing (no accidental quit).
    /// </remarks>
    public sealed class MainMenuPresenter : IEscapeHandler, IDisposable
    {
        private readonly MainMenuModel _model;
        private readonly MainMenuView _view;
        private readonly ConfirmDialogView _confirm;
        private readonly SettingsPresenter _settings;
        private readonly EscapeRouter _escape;
        private bool _disposed;

        /// <summary>Creates the presenter, subscribes to the views and shows the menu.</summary>
        /// <param name="model">Main menu logic.</param>
        /// <param name="view">The menu.</param>
        /// <param name="confirm">Dialog asking before a new game erases the save.</param>
        /// <param name="settings">Settings screen of the menu scene.</param>
        /// <param name="escape">The scene's Esc router; may be null (no Esc then).</param>
        /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
        public MainMenuPresenter(MainMenuModel model, MainMenuView view, ConfirmDialogView confirm, SettingsPresenter settings, EscapeRouter escape)
        {
            _model = model ?? throw new ArgumentNullException(nameof(model));
            _view = view != null ? view : throw new ArgumentNullException(nameof(view));
            _confirm = confirm != null ? confirm : throw new ArgumentNullException(nameof(confirm));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _escape = escape;

            _view.ContinueClicked += OnContinue;
            _view.NewGameClicked += OnNewGame;
            _view.SettingsClicked += OnSettings;
            _view.QuitClicked += OnQuit;
            _confirm.YesClicked += OnConfirmed;
            _confirm.NoClicked += OnDeclined;
            _settings.Closed += OnSettingsClosed;

            // Why: one permanent handler for the whole scene — it declines Esc when nothing is open on top of the menu.
            _escape?.Push(this);
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
                _view.SettingsClicked -= OnSettings;
                _view.QuitClicked -= OnQuit;
            }

            if (_confirm != null)
            {
                _confirm.YesClicked -= OnConfirmed;
                _confirm.NoClicked -= OnDeclined;
            }

            _settings.Closed -= OnSettingsClosed;
            _escape?.Remove(this);
        }

        /// <inheritdoc />
        public bool TryHandleEscape()
        {
            if (_settings.IsOpen)
            {
                _settings.Close();
                return true;
            }

            if (_confirm != null && _confirm.IsVisible)
            {
                _confirm.Hide();
                return true;
            }

            return false;
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

        private void OnSettings()
        {
            _view.Hide();
            _settings.Open();
        }

        private void OnSettingsClosed() => _view.Show();

        private static void OnQuit() => GameQuitter.Quit();
    }
}
