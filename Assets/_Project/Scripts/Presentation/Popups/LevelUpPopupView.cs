using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using AutoService.Presentation.Controls;
using AutoService.Presentation.Ui;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AutoService.Presentation.Popups
{
    /// <summary>
    /// Modal popup displayed upon leveling up, highlighting newly unlocked features and services.
    /// </summary>
    public sealed class LevelUpPopupView : MonoBehaviour, IEscapeHandler
    {
        [SerializeField]
        [Tooltip("Full-screen dimmer blocking clicks behind the modal.")]
        private GameObject _overlay;

        [SerializeField]
        [Tooltip("The centered card transformed during pop-in animation.")]
        private RectTransform _card;

        [SerializeField]
        [Tooltip("Icon displayed on top of the card.")]
        private Image _starIcon;

        [SerializeField]
        [Tooltip("Title label; its text is set in the scene.")]
        private TMP_Text _titleText;

        [SerializeField]
        [Tooltip("Level indicator, filled from the level format.")]
        private TMP_Text _levelBadgeText;

        [SerializeField]
        [Tooltip("Description text detailing unlocked features.")]
        private TMP_Text _descriptionText;

        [SerializeField]
        [Tooltip("Button to dismiss the popup.")]
        private Button _continueButton;

        [Header("Texts")]
        [SerializeField]
        [Tooltip("Level badge text; {0} is the new level.")]
        private string _levelFormat = "LEVEL {0}";

        [SerializeField]
        [Tooltip("Line above the list of buildables the new level unlocks.")]
        private string _unlocksHeader = "Now available to build:";

        [SerializeField]
        [Tooltip("Shown when the new level unlocks nothing.")]
        private string _noUnlocksText = "Keep growing your auto service!";

        private readonly StringBuilder _builder = new StringBuilder();

        private Action _onClose;
        private Coroutine _animationCoroutine;
        private EscapeRouter _escapeRouter;

        /// <summary>True if the popup is currently visible.</summary>
        public bool IsOpen => gameObject.activeSelf;

        private void Awake()
        {
            if (_continueButton != null)
            {
                _continueButton.onClick.AddListener(OnContinueClicked);
            }
        }

        private void OnDestroy()
        {
            if (_continueButton != null)
            {
                _continueButton.onClick.RemoveListener(OnContinueClicked);
            }

            _escapeRouter?.Remove(this);
        }

        /// <summary>Connects the escape key handler.</summary>
        public void SetEscapeRouter(EscapeRouter escapeRouter)
        {
            _escapeRouter = escapeRouter;
        }

        /// <summary>Shows the popup for <paramref name="newLevel"/> with the names of what it unlocks.</summary>
        /// <param name="newLevel">The level just reached.</param>
        /// <param name="unlocks">Display names of the unlocked buildables; may be empty.</param>
        /// <param name="onClose">Called once when the popup is closed.</param>
        public void Show(int newLevel, IReadOnlyList<string> unlocks, Action onClose = null)
        {
            _onClose = onClose;

            if (_levelBadgeText != null)
            {
                _levelBadgeText.text = string.Format(_levelFormat, newLevel);
            }

            if (_descriptionText != null)
            {
                _descriptionText.text = BuildDescription(unlocks);
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

        private string BuildDescription(IReadOnlyList<string> unlocks)
        {
            if (unlocks == null || unlocks.Count == 0)
            {
                return _noUnlocksText;
            }

            _builder.Clear();
            _builder.Append(_unlocksHeader);
            for (int i = 0; i < unlocks.Count; i++)
            {
                _builder.Append('\n').Append("- ").Append(unlocks[i]);
            }

            return _builder.ToString();
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

        private void OnContinueClicked()
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
