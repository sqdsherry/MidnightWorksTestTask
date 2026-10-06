using System;
using System.Collections;
using AutoService.Presentation.Controls;
using AutoService.Presentation.Ui;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AutoService.Presentation.Popups
{
    /// <summary>
    /// Modal welcome window introducing Location 2 (Tuning Center) upon the player's first arrival.
    /// </summary>
    public sealed class Location2WelcomePopupView : MonoBehaviour, IEscapeHandler
    {
        public const string DefaultTitle = "ТЮНИНГ-ЦЕНТР ОТКРЫТ!";
        public const string DefaultDescription =
            "Добро пожаловать на вторую локацию!\nЗдесь доступны премиальные сервисы: Замена шин, Тюнинг и Покраска кузова с визуальным изменением авто!";

        [SerializeField]
        [Tooltip("Full-screen dimmer blocking clicks behind the modal.")]
        private GameObject _overlay;

        [SerializeField]
        [Tooltip("The centered card transformed during pop-in animation.")]
        private RectTransform _card;

        [SerializeField]
        [Tooltip("Title label ('ТЮНИНГ-ЦЕНТР ОТКРЫТ!').")]
        private TMP_Text _titleText;

        [SerializeField]
        [Tooltip("Description text detailing Location 2 features.")]
        private TMP_Text _descriptionText;

        [SerializeField]
        [Tooltip("Button to dismiss the popup ('НАЧАТЬ РАБОТУ!').")]
        private Button _startButton;

        private Action _onClose;
        private Coroutine _animationCoroutine;
        private EscapeRouter _escapeRouter;

        /// <summary>True if the popup is currently visible.</summary>
        public bool IsOpen => gameObject.activeSelf;

        private void Awake()
        {
            if (_startButton != null)
            {
                _startButton.onClick.AddListener(OnStartClicked);
            }
        }

        private void OnDestroy()
        {
            if (_startButton != null)
            {
                _startButton.onClick.RemoveListener(OnStartClicked);
            }

            _escapeRouter?.Remove(this);
        }

        /// <summary>Connects the escape key handler.</summary>
        public void SetEscapeRouter(EscapeRouter escapeRouter)
        {
            _escapeRouter = escapeRouter;
        }

        /// <summary>Displays the welcome popup.</summary>
        public void Show(Action onClose = null)
        {
            _onClose = onClose;

            if (_titleText != null && string.IsNullOrEmpty(_titleText.text))
            {
                _titleText.text = DefaultTitle;
            }

            if (_descriptionText != null && string.IsNullOrEmpty(_descriptionText.text))
            {
                _descriptionText.text = DefaultDescription;
            }

            UiVisibility.ShowChain(gameObject);
            if (_overlay != null)
            {
                _overlay.SetActive(true);
            }

            _escapeRouter?.Push(this);

            if (_card != null)
            {
                if (_animationCoroutine != null)
                {
                    StopCoroutine(_animationCoroutine);
                }
                _animationCoroutine = StartCoroutine(AnimateCardIn());
            }
        }

        /// <summary>Hides the popup and invokes the close callback.</summary>
        public void Hide()
        {
            _escapeRouter?.Remove(this);

            if (_animationCoroutine != null)
            {
                StopCoroutine(_animationCoroutine);
                _animationCoroutine = null;
            }

            UiVisibility.Hide(gameObject);

            Action callback = _onClose;
            _onClose = null;
            callback?.Invoke();
        }

        /// <inheritdoc />
        public bool TryHandleEscape()
        {
            if (IsOpen)
            {
                Hide();
                return true;
            }

            return false;
        }

        private void OnStartClicked()
        {
            Hide();
        }

        private IEnumerator AnimateCardIn()
        {
            const float duration = 0.25f;
            float time = 0f;
            Vector3 startScale = Vector3.one * 0.7f;
            Vector3 targetScale = Vector3.one;

            _card.localScale = startScale;

            while (time < duration)
            {
                time += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(time / duration);
                float smoothT = Mathf.SmoothStep(0f, 1f, t);
                _card.localScale = Vector3.Lerp(startScale, targetScale, smoothT);
                yield return null;
            }

            _card.localScale = targetScale;
            _animationCoroutine = null;
        }
    }
}
