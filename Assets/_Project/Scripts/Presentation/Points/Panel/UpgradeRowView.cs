using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AutoService.Presentation.Points.Panel
{
    /// <summary>One upgrade row of the point panel: name, level, effect and the buy button. Passive view.</summary>
    public sealed class UpgradeRowView : MonoBehaviour
    {
        [SerializeField]
        private TMP_Text _name;

        [SerializeField]
        private TMP_Text _level;

        [SerializeField]
        private TMP_Text _effect;

        [SerializeField]
        private Button _buy;

        [SerializeField]
        private TMP_Text _buyLabel;

        [SerializeField]
        [Tooltip("Level text; {0} = level.")]
        private string _levelFormat = "Lv {0}";

        /// <summary>Raised when the buy button is clicked.</summary>
        public event Action Clicked;

        private void Awake()
        {
            if (_buy != null)
            {
                _buy.onClick.AddListener(OnBuy);
            }
        }

        private void OnDestroy()
        {
            if (_buy != null)
            {
                _buy.onClick.RemoveListener(OnBuy);
            }
        }

        /// <summary>Shows the row or hides it (an upgrade missing from the config).</summary>
        public void SetVisible(bool visible)
        {
            if (gameObject.activeSelf != visible)
            {
                gameObject.SetActive(visible);
            }
        }

        /// <summary>Writes the static texts of the row.</summary>
        public void SetInfo(string displayName, string effect)
        {
            SetText(_name, displayName);
            SetText(_effect, effect);
        }

        /// <summary>Writes the level without allocating.</summary>
        public void SetLevel(int level)
        {
            if (_level != null)
            {
                _level.SetText(_levelFormat, level);
            }
        }

        /// <summary>Enables the buy button and sets its label.</summary>
        public void SetButton(bool interactable, string label)
        {
            if (_buy != null)
            {
                _buy.interactable = interactable;
            }

            SetText(_buyLabel, label);
        }

        private void OnBuy() => Clicked?.Invoke();

        private static void SetText(TMP_Text label, string text)
        {
            if (label != null)
            {
                label.text = text ?? string.Empty;
            }
        }
    }
}
