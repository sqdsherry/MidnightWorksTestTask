using System;
using AutoService.Presentation.Ui;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AutoService.Presentation.Menu
{
    /// <summary>
    /// Reusable Yes / No dialog over a dimmed screen. Passive view: the message comes from the caller, the answers go out
    /// as events.
    /// </summary>
    /// <remarks>Put it on the dialog's root (the dimmer); showing and hiding switch that object.</remarks>
    public sealed class ConfirmDialogView : MonoBehaviour
    {
        [SerializeField]
        private TMP_Text _message;

        [SerializeField]
        private Button _yesButton;

        [SerializeField]
        private Button _noButton;

        /// <summary>Raised when Yes is clicked.</summary>
        public event Action YesClicked;

        /// <summary>Raised when No is clicked.</summary>
        public event Action NoClicked;

        /// <summary>True while the dialog is shown.</summary>
        public bool IsVisible => gameObject.activeInHierarchy;

        // Why: Awake runs once, on the first activation — a dialog that starts hidden subscribes when it is first shown.
        private void Awake()
        {
            if (_yesButton != null)
            {
                _yesButton.onClick.AddListener(OnYes);
            }

            if (_noButton != null)
            {
                _noButton.onClick.AddListener(OnNo);
            }
        }

        private void OnDestroy()
        {
            if (_yesButton != null)
            {
                _yesButton.onClick.RemoveListener(OnYes);
            }

            if (_noButton != null)
            {
                _noButton.onClick.RemoveListener(OnNo);
            }
        }

        /// <summary>Shows the dialog with <paramref name="message"/>.</summary>
        public void Show(string message)
        {
            if (_message != null)
            {
                _message.text = message ?? string.Empty;
            }

            UiVisibility.ShowChain(gameObject);
        }

        /// <summary>Hides the dialog.</summary>
        public void Hide() => UiVisibility.Hide(gameObject);

        private void OnYes() => YesClicked?.Invoke();

        private void OnNo() => NoClicked?.Invoke();
    }
}
