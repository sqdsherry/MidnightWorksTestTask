using System.Collections;
using AutoService.Domain.Points;
using AutoService.Domain.Supplies;
using AutoService.Presentation.Interaction;
using AutoService.Services.Formatting;
using AutoService.Services.Supplies;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AutoService.Presentation.Supplies
{
    /// <summary>
    /// The warehouse of a location: the character walks up and takes (and pays for) a box for the hungriest
    /// point after a short pickup dwell with visual progress. Failures ("Hands full", "All stocked", "Need $15") flash above.
    /// </summary>
    /// <remarks>
    /// The view holds no game state: buying is <see cref="ISupplyService.TryBuyBoxForHungriest"/>, the hands are
    /// <see cref="IPlayerCarry"/>. Hiring the storekeeper happens on the warehouse's own blue pad, not here.
    /// </remarks>
    public sealed class WarehouseView : MonoBehaviour, IInteractable
    {
        [SerializeField]
        [Tooltip("Id of the location this warehouse serves, e.g. \"loc1\".")]
        private string _locationId = string.Empty;

        [SerializeField]
        [Tooltip("Where the character (and the storekeeper) stands; its forward faces the warehouse.")]
        private Transform _approachPoint;

        [SerializeField]
        [Tooltip("Optional hover feedback.")]
        private InteractableHighlight _highlight;

        [SerializeField]
        [Tooltip("Optional world-space text for short failure messages.")]
        private TMP_Text _messageLabel;

        [SerializeField]
        [Tooltip("Optional message container/badge for punch and fade animations.")]
        private GameObject _messageRoot;

        [SerializeField, Min(0.1f)]
        [Tooltip("Seconds a message stays visible (unscaled time).")]
        private float _messageSeconds = 2.0f;

        [SerializeField]
        [Tooltip("Shown when the character already carries a box.")]
        private string _handsFullText = "Hands full";

        [SerializeField]
        [Tooltip("Shown when no point can take a box.")]
        private string _allStockedText = "All stocked";

        [SerializeField]
        [Tooltip("Shown when the balance does not cover the box; {0} = price.")]
        private string _needFormat = "Need {0}";

        [Header("Pickup Progress")]
        [SerializeField, Min(0.1f)]
        [Tooltip("Seconds required to pick up a box from the warehouse.")]
        private float _dwellSeconds = 1.2f;

        [SerializeField]
        [Tooltip("Parent of the green progress bar (hidden when idle).")]
        private GameObject _progressRoot;

        [SerializeField]
        [Tooltip("Horizontal fill image for pickup progress.")]
        private Image _progressBarFill;

        private ISupplyService _supplies;
        private IPlayerCarry _carry;
        private Coroutine _message;
        private WaitForSecondsRealtime _messageWait;
        private bool _isInteracting;
        private float _dwellTimer;

        /// <summary>Id of the location this warehouse serves.</summary>
        public string LocationId => _locationId;

        /// <summary>Where the character and the storekeeper stand.</summary>
        public Transform ApproachPoint => _approachPoint != null ? _approachPoint : transform;

        /// <inheritdoc />
        public Vector3 ApproachPosition => ApproachPoint.position;

        /// <inheritdoc />
        public Quaternion ApproachRotation => ApproachPoint.rotation;

        /// <inheritdoc />
        /// <remarks>Not interactable before <see cref="Construct"/>.</remarks>
        public bool IsInteractable => _supplies != null && _carry != null;

        /// <summary>Injects the supply service and the player's hands. Called once by the scene entry point.</summary>
        public void Construct(ISupplyService supplies, IPlayerCarry carry)
        {
            _supplies = supplies;
            _carry = carry;
            _isInteracting = false;
            _dwellTimer = 0f;
            if (_progressRoot != null)
            {
                _progressRoot.SetActive(false);
            }
            if (_progressBarFill != null)
            {
                _progressBarFill.fillAmount = 0f;
            }
            HideMessage();
        }

        private void OnDisable()
        {
            EndInteraction();
        }

        /// <inheritdoc />
        public void SetHighlighted(bool highlighted)
        {
            if (_highlight != null)
            {
                _highlight.SetHighlighted(highlighted);
            }
        }

        /// <inheritdoc />
        public void BeginInteraction()
        {
            if (!IsInteractable)
            {
                return;
            }

            if (_carry.HasBox)
            {
                ShowMessage(_handsFullText);
                return;
            }

            if (_supplies.FindHungriest(_locationId) == null)
            {
                ShowMessage(_allStockedText);
                return;
            }

            _isInteracting = true;
            _dwellTimer = 0f;
            if (_progressRoot != null)
            {
                _progressRoot.SetActive(true);
            }
            if (_progressBarFill != null)
            {
                _progressBarFill.fillAmount = 0f;
            }
        }

        private void Update()
        {
            if (!_isInteracting)
            {
                return;
            }

            _dwellTimer += Time.deltaTime;
            float progress = Mathf.Clamp01(_dwellTimer / _dwellSeconds);
            if (_progressBarFill != null)
            {
                _progressBarFill.fillAmount = progress;
            }

            if (_dwellTimer >= _dwellSeconds)
            {
                _isInteracting = false;
                if (_progressRoot != null)
                {
                    _progressRoot.SetActive(false);
                }

                if (_supplies.TryBuyBoxForHungriest(_locationId, true, out SupplyBox box, out ServicePoint target))
                {
                    _carry.TryPick(box);
                }
                else
                {
                    // Why: the purchase fails either because nothing needs a box or because the balance is too low — say which.
                    ShowMessage(target == null
                        ? _allStockedText
                        : string.Format(_needFormat, MoneyFormatter.Format(_supplies.GetBoxPrice(target.Supply.SupplyTypeId))));
                }
            }
        }

        /// <inheritdoc />
        public void EndInteraction()
        {
            _isInteracting = false;
            _dwellTimer = 0f;
            if (_progressRoot != null)
            {
                _progressRoot.SetActive(false);
            }
        }

        private void ShowMessage(string text)
        {
            if (_messageLabel == null && _messageRoot == null)
            {
                return;
            }

            if (_messageLabel != null)
            {
                _messageLabel.text = text;
            }

            GameObject activeTarget = _messageRoot != null ? _messageRoot : _messageLabel.gameObject;
            activeTarget.SetActive(true);

            if (_message != null)
            {
                StopCoroutine(_message);
            }

            if (isActiveAndEnabled)
            {
                _message = StartCoroutine(AnimateMessage(activeTarget));
            }
        }

        private IEnumerator AnimateMessage(GameObject target)
        {
            Transform t = target.transform;
            Vector3 originalScale = Vector3.one;
            Vector3 baseLocalPos = t.localPosition;

            float elapsed = 0f;
            float totalDuration = _messageSeconds;

            CanvasGroup group = target.GetComponent<CanvasGroup>();

            while (elapsed < totalDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / totalDuration);

                // Punch scale: quick zoom 1.0 -> 1.18 -> 1.0 in first 0.35s
                float scaleMod = 1f;
                if (progress < 0.2f)
                {
                    scaleMod = Mathf.Lerp(1.0f, 1.18f, progress / 0.2f);
                }
                else if (progress < 0.35f)
                {
                    scaleMod = Mathf.Lerp(1.18f, 1.0f, (progress - 0.2f) / 0.15f);
                }

                t.localScale = originalScale * scaleMod;

                // Subtle shake while visible
                if (progress < 0.35f)
                {
                    float shake = Mathf.Sin(elapsed * 45f) * 4f * (1f - progress / 0.35f);
                    t.localPosition = baseLocalPos + new Vector3(shake, 0f, 0f);
                }
                else
                {
                    t.localPosition = baseLocalPos;
                }

                // Smooth fade out in the last 0.4s
                if (progress > 0.8f && group != null)
                {
                    group.alpha = Mathf.Lerp(1f, 0f, (progress - 0.8f) / 0.2f);
                }
                else if (group != null)
                {
                    group.alpha = 1f;
                }

                yield return null;
            }

            t.localScale = originalScale;
            t.localPosition = baseLocalPos;
            if (group != null) group.alpha = 1f;

            _message = null;
            HideMessage();
        }

        private void HideMessage()
        {
            if (_messageRoot != null)
            {
                _messageRoot.SetActive(false);
            }
            if (_messageLabel != null)
            {
                _messageLabel.gameObject.SetActive(false);
            }
        }
    }
}
