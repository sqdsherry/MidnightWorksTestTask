using System;
using AutoService.Presentation.Ui;
using UnityEngine;
using UnityEngine.UI;

namespace AutoService.Presentation.Menu
{
    /// <summary>
    /// Main menu: game title and the Continue / New Game / Settings / Quit column. Passive view: clicks go out as events.
    /// </summary>
    /// <remarks>Put it on the menu's root; showing and hiding switch that object. Texts live on the prefab/scene.</remarks>
    public sealed class MainMenuView : MonoBehaviour
    {
        [SerializeField]
        private Button _continueButton;

        [SerializeField]
        private Button _newGameButton;

        [SerializeField]
        private Button _settingsButton;

        [SerializeField]
        private Button _quitButton;

        [SerializeField]
        [Tooltip("Question asked before a new game erases the save.")]
        private string _newGameConfirmText = "Start a new game? Your progress will be lost.";

        /// <summary>Raised when Continue is clicked.</summary>
        public event Action ContinueClicked;

        /// <summary>Raised when New Game is clicked.</summary>
        public event Action NewGameClicked;

        /// <summary>Raised when Settings is clicked.</summary>
        public event Action SettingsClicked;

        /// <summary>Raised when Quit is clicked.</summary>
        public event Action QuitClicked;

        /// <summary>Question asked before a new game erases the save.</summary>
        public string NewGameConfirmText => _newGameConfirmText;

        // Why: Awake runs once, on the first activation, so the listeners are never doubled.
        private void Awake()
        {
            AddListener(_continueButton, OnContinue);
            AddListener(_newGameButton, OnNewGame);
            AddListener(_settingsButton, OnSettings);
            AddListener(_quitButton, OnQuit);
        }

        private void OnDestroy()
        {
            RemoveListener(_continueButton, OnContinue);
            RemoveListener(_newGameButton, OnNewGame);
            RemoveListener(_settingsButton, OnSettings);
            RemoveListener(_quitButton, OnQuit);
        }

        /// <summary>Shows the menu.</summary>
        public void Show() => UiVisibility.ShowChain(gameObject);

        /// <summary>Hides the menu.</summary>
        public void Hide() => UiVisibility.Hide(gameObject);

        /// <summary>Enables or greys out Continue.</summary>
        public void SetContinueAvailable(bool available)
        {
            if (_continueButton != null)
            {
                _continueButton.interactable = available;
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

        private void OnContinue() => ContinueClicked?.Invoke();

        private void OnNewGame() => NewGameClicked?.Invoke();

        private void OnSettings() => SettingsClicked?.Invoke();

        private void OnQuit() => QuitClicked?.Invoke();
    }
}
