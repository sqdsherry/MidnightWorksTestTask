using System;
using AutoService.Presentation.Controls;
using AutoService.Presentation.Settings;
using AutoService.Presentation.Ui;
using AutoService.Services.Menu;

namespace AutoService.Presentation.Pause
{
    /// <summary>
    /// The in-game pause: opens from the HUD button or an Esc nobody else took, pauses the game while open, and offers
    /// Resume / Settings / Main Menu / Quit.
    /// </summary>
    /// <remarks>
    /// While open it is the top Esc handler: Esc closes the settings first, then the pause menu itself.
    /// Clicks on the world are already ignored while paused (<c>PlayerInputPresenter</c> checks the pause).
    /// </remarks>
    public sealed class PauseMenuPresenter : IEscapeHandler, IDisposable
    {
        private readonly PauseMenuModel _model;
        private readonly PauseMenuView _view;
        private readonly PauseButtonView _button;
        private readonly SettingsPresenter _settings;
        private readonly EscapeRouter _escape;
        private bool _disposed;

        /// <summary>Creates the presenter, subscribes to the views, the settings and the router, and hides the menu.</summary>
        /// <param name="model">Pause menu logic.</param>
        /// <param name="view">The pause menu.</param>
        /// <param name="button">HUD pause button; may be null (Esc only).</param>
        /// <param name="settings">Settings screen of the scene; may be null (the Settings button does nothing).</param>
        /// <param name="escape">The scene's Esc router; may be null (button only).</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="model"/> or <paramref name="view"/> is null.</exception>
        public PauseMenuPresenter(
            PauseMenuModel model,
            PauseMenuView view,
            PauseButtonView button,
            SettingsPresenter settings,
            EscapeRouter escape)
        {
            _model = model ?? throw new ArgumentNullException(nameof(model));
            _view = view != null ? view : throw new ArgumentNullException(nameof(view));
            _button = button;
            _settings = settings;
            _escape = escape;

            _view.ResumeClicked += Close;
            _view.SettingsClicked += OnSettingsClicked;
            _view.MainMenuClicked += OnMainMenuClicked;
            _view.QuitClicked += OnQuitClicked;
            if (_button != null)
            {
                _button.Clicked += Open;
            }

            if (_settings != null)
            {
                _settings.Closed += OnSettingsClosed;
            }

            if (_escape != null)
            {
                _escape.Unhandled += Open;
            }

            _view.Hide();
        }

        /// <summary>Opens the menu and pauses the game. Does nothing if already open.</summary>
        public void Open()
        {
            if (_model.IsOpen)
            {
                return;
            }

            _model.Open();
            _view.Show();
            _escape?.Push(this);
        }

        /// <summary>Closes the settings and the menu and resumes the game. Does nothing if closed.</summary>
        public void Close()
        {
            if (!_model.IsOpen)
            {
                return;
            }

            _settings?.Close();
            _model.Close();
            _escape?.Remove(this);
            if (_view != null)
            {
                _view.Hide();
            }
        }

        /// <inheritdoc />
        public bool TryHandleEscape()
        {
            if (!_model.IsOpen)
            {
                return false;
            }

            if (_settings != null && _settings.IsOpen)
            {
                _settings.Close();
            }
            else
            {
                Close();
            }

            return true;
        }

        /// <summary>Unsubscribes and releases the pause if the menu is still open. Safe to call repeatedly.</summary>
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
                _view.ResumeClicked -= Close;
                _view.SettingsClicked -= OnSettingsClicked;
                _view.MainMenuClicked -= OnMainMenuClicked;
                _view.QuitClicked -= OnQuitClicked;
            }

            if (_button != null)
            {
                _button.Clicked -= Open;
            }

            if (_settings != null)
            {
                _settings.Closed -= OnSettingsClosed;
            }

            if (_escape != null)
            {
                _escape.Unhandled -= Open;
                _escape.Remove(this);
            }

            // Why: the pause request is project-wide; a scene unloaded with the menu open must not leave the game paused.
            _model.Close();
        }

        private void OnSettingsClicked()
        {
            if (_settings == null || !_model.IsOpen)
            {
                return;
            }

            _view.SetPanelVisible(false);
            _settings.Open();
        }

        private void OnSettingsClosed()
        {
            if (_model.IsOpen && _view != null)
            {
                _view.SetPanelVisible(true);
            }
        }

        private void OnMainMenuClicked()
        {
            Close();
            _model.ToMainMenu();
        }

        // TODO(08b-save): save before quitting.
        private static void OnQuitClicked() => GameQuitter.Quit();
    }
}
