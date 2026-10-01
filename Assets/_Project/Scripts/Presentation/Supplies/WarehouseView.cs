using System.Collections;
using AutoService.Domain.Points;
using AutoService.Domain.Supplies;
using AutoService.Presentation.Interaction;
using AutoService.Services.Formatting;
using AutoService.Services.Supplies;
using TMPro;
using UnityEngine;

namespace AutoService.Presentation.Supplies
{
    /// <summary>
    /// The warehouse of a location: the character walks up and immediately takes (and pays for) a box for the hungriest
    /// point. Failures ("Hands full", "All stocked", "Need $15") flash above the warehouse for a moment.
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

        [SerializeField, Min(0.1f)]
        [Tooltip("Seconds a message stays visible (unscaled time).")]
        private float _messageSeconds = 1.5f;

        [SerializeField]
        [Tooltip("Shown when the character already carries a box.")]
        private string _handsFullText = "Hands full";

        [SerializeField]
        [Tooltip("Shown when no point can take a box.")]
        private string _allStockedText = "All stocked";

        [SerializeField]
        [Tooltip("Shown when the balance does not cover the box; {0} = price.")]
        private string _needFormat = "Need {0}";

        private ISupplyService _supplies;
        private IPlayerCarry _carry;
        private Coroutine _message;
        private WaitForSecondsRealtime _messageWait;

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
            HideMessage();
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

            if (_supplies.TryBuyBoxForHungriest(_locationId, true, out SupplyBox box, out ServicePoint target))
            {
                _carry.TryPick(box);
                return;
            }

            // Why: the purchase fails either because nothing needs a box or because the balance is too low — say which.
            ShowMessage(target == null
                ? _allStockedText
                : string.Format(_needFormat, MoneyFormatter.Format(_supplies.GetBoxPrice(target.Supply.SupplyTypeId))));
        }

        /// <inheritdoc />
        public void EndInteraction()
        {
        }

        private void ShowMessage(string text)
        {
            if (_messageLabel == null)
            {
                return;
            }

            _messageLabel.text = text;
            _messageLabel.gameObject.SetActive(true);
            if (_message != null)
            {
                StopCoroutine(_message);
            }

            // Why: a coroutine needs an active object; without one the message simply stays until the next interaction.
            if (isActiveAndEnabled)
            {
                _message = StartCoroutine(HideMessageLater());
            }
        }

        private IEnumerator HideMessageLater()
        {
            // Why: unscaled — the message is UI feedback and should disappear even while the game is paused.
            _messageWait ??= new WaitForSecondsRealtime(_messageSeconds);
            _messageWait.Reset();
            yield return _messageWait;
            _message = null;
            HideMessage();
        }

        private void HideMessage()
        {
            if (_messageLabel != null)
            {
                _messageLabel.gameObject.SetActive(false);
            }
        }
    }
}
