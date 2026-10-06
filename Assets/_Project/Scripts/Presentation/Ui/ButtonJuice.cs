using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AutoService.Presentation.Ui
{
    /// <summary>
    /// Hover feedback of a menu button: scales it up slightly while the pointer is over it. The color highlight comes from
    /// the button's own <see cref="ColorBlock"/>.
    /// </summary>
    /// <remarks>Runs on unscaled time, so it also works in the pause menu. Disabled buttons do not react.</remarks>
    [DisallowMultipleComponent]
    public sealed class ButtonJuice : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField, Min(1f)]
        [Tooltip("Scale while hovered, relative to the scale the button has in the scene.")]
        private float _hoverScale = 1.05f;

        [SerializeField, Min(0f)]
        [Tooltip("Duration of the scale change, in real seconds.")]
        private float _seconds = 0.08f;

        private Selectable _selectable;
        private Vector3 _baseScale;
        private Coroutine _tween;

        private void Awake()
        {
            _baseScale = transform.localScale;
            TryGetComponent(out _selectable);
        }

        private void OnDisable()
        {
            // Why: a button hidden while hovered (its panel closed by the click) must come back at its normal size.
            _tween = null;
            transform.localScale = _baseScale;
        }

        /// <inheritdoc />
        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_selectable != null && !_selectable.IsInteractable())
            {
                return;
            }

            ScaleTo(_baseScale * _hoverScale);
        }

        /// <inheritdoc />
        public void OnPointerExit(PointerEventData eventData)
        {
            ScaleTo(_baseScale);
        }

        private void ScaleTo(Vector3 target)
        {
            if (_tween != null)
            {
                StopCoroutine(_tween);
                _tween = null;
            }

            if (_seconds <= 0f || !isActiveAndEnabled)
            {
                transform.localScale = target;
                return;
            }

            _tween = StartCoroutine(Scale(target));
        }

        private IEnumerator Scale(Vector3 target)
        {
            Vector3 start = transform.localScale;
            float speed = 1f / _seconds;
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime * speed)
            {
                transform.localScale = Vector3.LerpUnclamped(start, target, t);
                yield return null;
            }

            transform.localScale = target;
            _tween = null;
        }
    }
}
