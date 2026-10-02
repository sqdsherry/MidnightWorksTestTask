using AutoService.Services.Supplies;
using UnityEngine;

namespace AutoService.Presentation.Supplies
{
    /// <summary>
    /// Shows the box the player carries (<see cref="IPlayerCarry"/>) in the character's hands, colored by its consumable.
    /// Lives on the player object; holds no state of its own.
    /// </summary>
    public sealed class PlayerCarryView : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Where the box is held (in front of the character).")]
        private Transform _boxSocket;

        [SerializeField]
        [Tooltip("Renderer of the box (a 0.5 m cube without collider under the socket).")]
        private Renderer _boxRenderer;

        private IPlayerCarry _carry;
        private SupplyVisualCatalog _visuals;
        private CarriedBoxView _box;

        /// <summary>Where the box is held (may be null if not assigned).</summary>
        public Transform BoxSocket => _boxSocket;

        /// <summary>Starts showing <paramref name="carry"/>. Called once by the scene entry point.</summary>
        /// <param name="carry">What the player holds.</param>
        /// <param name="visuals">Box colors; may be null (white boxes).</param>
        public void Construct(IPlayerCarry carry, SupplyVisualCatalog visuals)
        {
            if (_carry != null)
            {
                _carry.Changed -= Refresh;
            }

            _carry = carry;
            _visuals = visuals;
            _box ??= new CarriedBoxView(_boxRenderer);
            if (_carry != null)
            {
                _carry.Changed += Refresh;
            }

            Refresh();
        }

        private void OnDestroy()
        {
            if (_carry != null)
            {
                _carry.Changed -= Refresh;
                _carry = null;
            }
        }

        private void Refresh()
        {
            if (_box == null)
            {
                return;
            }

            if (_carry == null || !_carry.HasBox)
            {
                _box.Hide();
                return;
            }

            _box.Show(_visuals != null ? _visuals.ColorOf(_carry.Box.SupplyTypeId) : Color.white);
        }
    }
}
