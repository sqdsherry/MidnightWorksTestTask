using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AutoService.Presentation.Hud
{
    /// <summary>
    /// Passive view displaying player progression: level label, XP text, and XP progress bar fill.
    /// Updated strictly through events by <see cref="ProgressionPresenter"/>.
    /// </summary>
    public sealed class ProgressionView : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Label showing current level, e.g. 'Lvl 1'.")]
        private TMP_Text _levelText;

        [SerializeField]
        [Tooltip("Label showing XP progress, e.g. '15 / 50 XP'.")]
        private TMP_Text _xpText;

        [SerializeField]
        [Tooltip("Fill image or bar indicating progress towards next level.")]
        private Image _progressBarFill;

        /// <summary>Updates the level text label.</summary>
        public void SetLevelText(string text)
        {
            if (_levelText != null)
            {
                _levelText.text = text;
            }
        }

        /// <summary>Updates the XP text label.</summary>
        public void SetXpText(string text)
        {
            if (_xpText != null)
            {
                _xpText.text = text;
            }
        }

        /// <summary>Updates the progress bar fill amount (0..1).</summary>
        public void SetProgress(float progress01)
        {
            if (_progressBarFill != null)
            {
                _progressBarFill.fillAmount = Mathf.Clamp01(progress01);
            }
        }
    }
}
