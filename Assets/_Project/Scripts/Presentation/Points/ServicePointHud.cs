using AutoService.Domain.Points;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AutoService.Presentation.Points
{
    /// <summary>
    /// World-space indicators above a service point: progress bar while servicing, "!" while a car waits for someone
    /// to accept its order, optional "occupied" icon, the supply counter ("7/10") and the box icon of an empty stock.
    /// </summary>
    /// <remarks>
    /// <see cref="Render"/> is called every frame by the presenter, so it only touches Unity objects when a value
    /// actually changed (no allocations, no redundant <c>SetActive</c>). The canvas is not billboarded: the camera angle
    /// is fixed, so the canvas is simply tilted in the scene.
    /// </remarks>
    public sealed class ServicePointHud : MonoBehaviour
    {
        // Why: below this the bar would not visibly change; skipping the write avoids re-meshing the Image every frame.
        private const float FillEpsilon = 0.001f;

        [SerializeField]
        [Tooltip("Image with Image Type = Filled.")]
        private Image _progressFill;

        [SerializeField]
        [Tooltip("Shown while servicing (parent of the progress bar).")]
        private GameObject _progressRoot;

        [SerializeField]
        [Tooltip("\"!\" shown while a car waits and nobody is on the work spot.")]
        private GameObject _awaitingIcon;

        [SerializeField]
        [Tooltip("Optional icon shown while someone is on the work spot.")]
        private GameObject _occupiedIcon;

        [SerializeField]
        [Tooltip("Optional supply counter; hidden for points without a consumable.")]
        private TMP_Text _supplyLabel;

        [SerializeField]
        [Tooltip("Supply counter; {0} = current, {1} = capacity (TMP SetText format).")]
        private string _supplyFormat = "{0}/{1}";

        [SerializeField]
        [Tooltip("Optional box icon shown while the stock is empty (the point needs a box, not a worker).")]
        private GameObject _noSupplyIcon;

        private bool _initialized;
        private bool _progressVisible;
        private bool _awaitingVisible;
        private bool _occupiedVisible;
        private bool _noSupplyVisible;
        private float _fill;

        /// <summary>Updates the indicators.</summary>
        /// <param name="state">Point state.</param>
        /// <param name="progress">Service progress 0..1.</param>
        /// <param name="occupied">True while someone stands on the work spot.</param>
        /// <param name="supplyEmpty">True while the point's stock is empty.</param>
        public void Render(ServicePointState state, float progress, bool occupied, bool supplyEmpty)
        {
            bool progressVisible = state == ServicePointState.Servicing;

            // Why: once someone is on the spot the order is being taken; the "!" only calls for help. With an empty stock
            // the point needs a box, not a person — the box icon replaces the "!".
            bool awaitingVisible = state == ServicePointState.AwaitingAccept && !occupied && !supplyEmpty;
            if (!_initialized || supplyEmpty != _noSupplyVisible)
            {
                SetActive(_noSupplyIcon, supplyEmpty);
                _noSupplyVisible = supplyEmpty;
            }

            if (!_initialized || progressVisible != _progressVisible)
            {
                SetActive(_progressRoot, progressVisible);
                _progressVisible = progressVisible;
            }

            if (!_initialized || awaitingVisible != _awaitingVisible)
            {
                SetActive(_awaitingIcon, awaitingVisible);
                _awaitingVisible = awaitingVisible;
            }

            if (!_initialized || occupied != _occupiedVisible)
            {
                SetActive(_occupiedIcon, occupied);
                _occupiedVisible = occupied;
            }

            if (_progressFill != null && (!_initialized || Mathf.Abs(progress - _fill) > FillEpsilon))
            {
                _progressFill.fillAmount = progress;
                _fill = progress;
            }

            _initialized = true;
        }

        /// <summary>Shows "current/capacity" without allocating. Call it when the stock changes, not every frame.</summary>
        public void SetSupply(int current, int capacity)
        {
            if (_supplyLabel == null)
            {
                return;
            }

            if (!_supplyLabel.gameObject.activeSelf)
            {
                _supplyLabel.gameObject.SetActive(true);
            }

            _supplyLabel.SetText(_supplyFormat, current, capacity);
        }

        /// <summary>Hides the supply counter (points without a consumable).</summary>
        public void HideSupply()
        {
            SetActive(_supplyLabel != null ? _supplyLabel.gameObject : null, false);
        }

        private static void SetActive(GameObject target, bool active)
        {
            if (target != null)
            {
                target.SetActive(active);
            }
        }
    }
}
