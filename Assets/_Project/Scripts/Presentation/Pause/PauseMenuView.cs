using System;
using AutoService.Presentation.Ui;
using UnityEngine;
using UnityEngine.UI;

namespace AutoService.Presentation.Pause
{
    /// <summary>
    /// Pause menu: dimmed screen with the Resume / Settings / Main Menu / Quit panel. Passive view: clicks go out as events.
    /// </summary>
    /// <remarks>
    /// Put it on the menu's root (the dimmer); showing and hiding switch that object. The button panel can be hidden on
    /// its own while the settings screen is on top, keeping the dimmer.
    /// </remarks>
    public sealed class PauseMenuView : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("The panel with the buttons (hidden while the settings are open).")]
        private GameObject _panel;

        [SerializeField]
        private Button _resumeButton;

        [SerializeField]
        private Button _settingsButton;

        [SerializeField]
        private Button _mainMenuButton;

        [SerializeField]
        private Button _quitButton;

        /// <summary>Raised when Resume is clicked.</summary>
        public event Action ResumeClicked;

        /// <summary>Raised when Settings is clicked.</summary>
        public event Action SettingsClicked;

        /// <summary>Raised when Main Menu is clicked.</summary>
        public event Action MainMenuClicked;

        /// <summary>Raised when Quit is clicked.</summary>
        public event Action QuitClicked;

        // Why: Awake runs once, on the first activation, so the listeners are never doubled.
        private void Awake()
        {
            AddListener(_resumeButton, OnResume);
            AddListener(_settingsButton, OnSettings);
            AddListener(_mainMenuButton, OnMainMenu);
            AddListener(_quitButton, OnQuit);
        }

        private void OnDestroy()
        {
            RemoveListener(_resumeButton, OnResume);
            RemoveListener(_settingsButton, OnSettings);
            RemoveListener(_mainMenuButton, OnMainMenu);
            RemoveListener(_quitButton, OnQuit);
        }

        /// <summary>Shows the dimmer and the button panel.</summary>
        public void Show()
        {
            UiVisibility.ShowChain(gameObject);
            SetPanelVisible(true);
        }

        /// <summary>Hides the whole menu.</summary>
        public void Hide() => UiVisibility.Hide(gameObject);

        /// <summary>Shows or hides only the button panel (the dimmer stays).</summary>
        public void SetPanelVisible(bool visible)
        {
            if (_panel == null)
            {
                return;
            }

            if (visible)
            {
                UiVisibility.ShowChain(_panel);
            }
            else
            {
                UiVisibility.Hide(_panel);
            }
        }

        private static void AddListener(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button != null)
            {
                button.onClick.AddListener(action);
            }
        }

        private static void RemoveListener(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button != null)
            {
                button.onClick.RemoveListener(action);
            }
        }

        private void OnResume() => ResumeClicked?.Invoke();

        private void OnSettings() => SettingsClicked?.Invoke();

        private void OnMainMenu() => MainMenuClicked?.Invoke();

        private void OnQuit() => QuitClicked?.Invoke();
    }
}
