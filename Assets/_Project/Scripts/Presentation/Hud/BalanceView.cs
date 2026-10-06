using TMPro;
using UnityEngine;

namespace AutoService.Presentation.Hud
{
    /// <summary>Temporary on-screen balance label (until the full HUD of module 09). Passive view.</summary>
    public sealed class BalanceView : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Label that shows the balance.")]
        private TMP_Text _label;

        /// <summary>Shows <paramref name="text"/>.</summary>
        public void SetText(string text)
        {
            if (_label != null)
            {
                _label.text = text;
            }
        }
    }
}
