using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AutoService.Presentation.Ui
{
    /// <summary>
    /// Press feedback of a button: shrinks it slightly while the pointer is held down. The click sound is added by
    /// <see cref="ButtonClickSounds"/>.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public sealed class ButtonAnimator : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        [SerializeField]
        [Tooltip("Scale while pressed, relative to the scale the button has in the scene.")]
        private float _pressedScale = 0.95f;

        private Button _button;
        private Vector3 _originalScale;

        private void Awake()
        {
            _button = GetComponent<Button>();
            _originalScale = transform.localScale;
        }

        private void OnDisable()
        {
            // Why: a button hidden while pressed (its panel closed by the click) must come back at its normal size.
            transform.localScale = _originalScale;
        }

        /// <inheritdoc />
        public void OnPointerDown(PointerEventData eventData)
        {
            if (_button.IsInteractable())
            {
                transform.localScale = _originalScale * _pressedScale;
            }
        }

        /// <inheritdoc />
        public void OnPointerUp(PointerEventData eventData)
        {
            transform.localScale = _originalScale;
        }
    }
}
