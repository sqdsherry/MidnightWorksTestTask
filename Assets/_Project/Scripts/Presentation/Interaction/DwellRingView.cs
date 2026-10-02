using UnityEngine;
using UnityEngine.UI;

namespace AutoService.Presentation.Interaction
{
    /// <summary>
    /// World-space "stand here" ring above an object: a radial filled image that shows a dwell progress 0..1 and hides
    /// itself at 0, over an optional background (ring + icon) that stays visible, so the spot is recognisable before
    /// anyone stands on it. Passive view.
    /// </summary>
    /// <remarks>
    /// <see cref="Render"/> may be called every frame; it only touches Unity objects when the value changed.
    /// </remarks>
    public sealed class DwellRingView : MonoBehaviour
    {
        // Why: below this the ring would not visibly change; skipping the write avoids re-meshing the Image every frame.
        private const float FillEpsilon = 0.001f;

        [SerializeField]
        [Tooltip("Image with Image Type = Filled, Fill Method = Radial 360.")]
        private Image _fill;

        [SerializeField]
        [Tooltip("Object shown while the progress is above zero. Defaults to the fill image's object.")]
        private GameObject _root;

        [SerializeField]
        [Tooltip("Optional background (ring, icon) that stays visible while the object is active, also at 0.")]
        private GameObject _background;

        private bool _initialized;
        private bool _visible;
        private float _shownFill;

        /// <summary>Shows <paramref name="progress01"/>; hides the ring at 0.</summary>
        public void Render(float progress01)
        {
            bool visible = progress01 > 0f;
            if (!_initialized && _background != null && !_background.activeSelf)
            {
                _background.SetActive(true);
            }

            GameObject root = _root != null ? _root : _fill != null ? _fill.gameObject : null;
            if (root != null && (!_initialized || visible != _visible))
            {
                root.SetActive(visible);
            }

            _visible = visible;
            if (visible && _fill != null && (!_initialized || Mathf.Abs(progress01 - _shownFill) > FillEpsilon))
            {
                _fill.fillAmount = progress01;
                _shownFill = progress01;
            }

            _initialized = true;
        }
    }
}
