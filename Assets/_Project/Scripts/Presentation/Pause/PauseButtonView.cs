using System;
using UnityEngine;
using UnityEngine.UI;

namespace AutoService.Presentation.Pause
{
    /// <summary>The HUD's pause button (top left). Passive view.</summary>
    public sealed class PauseButtonView : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Defaults to the Button on this object.")]
        private Button _button;

        /// <summary>Raised when the button is clicked.</summary>
        public event Action Clicked;

        private void Awake()
        {
            if (_button == null)
            {
                TryGetComponent(out _button);
            }

            if (_button != null)
            {
                _button.onClick.AddListener(OnClick);
            }
        }

        private void OnDestroy()
        {
            if (_button != null)
            {
                _button.onClick.RemoveListener(OnClick);
            }
        }

        private void OnClick() => Clicked?.Invoke();
    }
}
