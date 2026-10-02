using System;
using AutoService.Presentation.Controls;
using AutoService.Presentation.Menu;
using AutoService.Presentation.Settings;
using AutoService.Services.Core;
using AutoService.Services.Menu;
using AutoService.Services.Save;
using AutoService.Services.Scenes;
using AutoService.Services.Settings;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AutoService.Bootstrap
{
    /// <summary>
    /// Composition Root of the <c>MainMenu</c> scene: the menu, its New Game confirmation, the settings screen and Esc.
    /// </summary>
    /// <remarks>Nothing here ticks, so the scene has no game loop.</remarks>
    public sealed class MainMenuEntryPoint : MonoBehaviour, ISceneEntryPoint
    {
        [SerializeField]
        [Tooltip("Continue / New Game / Settings / Quit.")]
        private MainMenuView _menu;

        [SerializeField]
        [Tooltip("Asks before a new game erases the save.")]
        private ConfirmDialogView _confirmDialog;

        [SerializeField]
        [Tooltip("Settings screen (Prefabs/UI/SettingsPanel).")]
        private SettingsView _settings;

        [SerializeField]
        [Tooltip("GameControls asset; only its Esc (Gameplay/Cancel) is used here. Optional: no Esc without it.")]
        private InputActionAsset _inputActions;

        private ServiceContainer _container;

        /// <inheritdoc />
        /// <exception cref="InvalidOperationException">Thrown if the scene was already entered.</exception>
        public void Enter(ServiceContainer projectServices)
        {
            if (projectServices == null)
            {
                throw new ArgumentNullException(nameof(projectServices));
            }

            if (_container != null)
            {
                throw new InvalidOperationException("Main menu scene has already been entered.");
            }

            _container = new ServiceContainer(projectServices);
            var logger = _container.Resolve<IGameLogger>();

            // Why: non-short-circuit `|` so every missing reference is reported at once, not one per Play.
            if (!HasReference(_menu, "_menu", logger)
                | !HasReference(_confirmDialog, "_confirmDialog", logger)
                | !HasReference(_settings, "_settings", logger))
            {
                return;
            }

            EscapeRouter escape = CreateEscape(logger);
            var model = new MainMenuModel(_container.Resolve<ISaveService>(), _container.Resolve<ISceneLoader>());
            var settings = new SettingsPresenter(_container.Resolve<ISettingsService>(), _settings);

            // Registered so the container disposes them on unload (menu presenter first, reverse order).
            _container.Register(model);
            _container.Register(settings);
            _container.Register(new MainMenuPresenter(model, _menu, _confirmDialog, settings, escape));
        }

        // Why: the gameplay input wrapper is reused for its Cancel action rather than adding a second wrapper for one key;
        // the other gameplay actions have no listeners in this scene.
        private EscapeRouter CreateEscape(IGameLogger logger)
        {
            if (_inputActions == null)
            {
                logger.Warning("[MainMenu] _inputActions is not assigned on " + name + "; Esc does nothing in the menu.");
                return null;
            }

            GameplayInput input;
            try
            {
                input = new GameplayInput(_inputActions);
            }
            catch (InvalidOperationException exception)
            {
                logger.Error("[MainMenu] Esc disabled: " + exception.Message);
                return null;
            }

            var escape = new EscapeRouter();
            _container.Register(input);
            _container.Register(new EscapeInputBinding(input, escape));
            input.Enable();
            return escape;
        }

        private void OnDestroy()
        {
            _container?.Dispose();
            _container = null;
        }

        private bool HasReference(UnityEngine.Object reference, string fieldName, IGameLogger logger)
        {
            if (reference != null)
            {
                return true;
            }

            logger.Error("[MainMenu] " + fieldName + " is not assigned on " + name + ".");
            return false;
        }
    }
}
