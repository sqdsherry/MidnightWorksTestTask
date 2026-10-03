using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AutoService.Presentation.Ui
{
    [RequireComponent(typeof(Button))]
    public class ButtonAnimator : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        [SerializeField] private float _pressedScale = 0.95f;
        
        private Vector3 _originalScale;

        private void Awake()
        {
            _originalScale = transform.localScale;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (GetComponent<Button>().interactable)
            {
                transform.localScale = _originalScale * _pressedScale;
                AutoService.Services.Events.UiEvents.OnAnyButtonClicked?.Invoke();
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            transform.localScale = _originalScale;
        }
    }
}
