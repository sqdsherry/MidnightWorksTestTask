using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AutoService.Presentation.Points.Panel
{
    /// <summary>Hire row of the point panel: optional title and a full-width hire button. Passive view.</summary>
    public sealed class HireRowView : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Optional caption of the row.")]
        private TMP_Text _title;

        [SerializeField]
        private Button _hire;

        [SerializeField]
        private TMP_Text _hireLabel;

        /// <summary>Raised when the hire button is clicked.</summary>
        public event Action Clicked;

        private void Awake()
        {
            if (_hire != null)
            {
                _hire.onClick.AddListener(OnHire);
            }
        }

        private void OnDestroy()
        {
            if (_hire != null)
            {
                _hire.onClick.RemoveListener(OnHire);
            }
        }

        /// <summary>Shows the row or hides it (points of a type without a worker).</summary>
        public void SetVisible(bool visible)
        {
            if (gameObject.activeSelf != visible)
            {
                gameObject.SetActive(visible);
            }
        }

        /// <summary>Writes the caption (may be empty).</summary>
        public void SetTitle(string title)
        {
            if (_title != null)
            {
                _title.text = title ?? string.Empty;
            }
        }

        /// <summary>Enables the hire button and sets its label.</summary>
        public void SetButton(bool interactable, string label)
        {
            if (_hire != null)
            {
                _hire.interactable = interactable;
            }

            if (_hireLabel != null)
            {
                _hireLabel.text = label ?? string.Empty;
            }
        }

        private void OnHire() => Clicked?.Invoke();
    }
}
