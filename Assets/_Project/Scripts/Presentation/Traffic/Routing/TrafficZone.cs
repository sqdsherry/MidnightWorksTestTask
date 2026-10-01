using AutoService.Presentation.Points;
using UnityEngine;

namespace AutoService.Presentation.Traffic.Routing
{
    /// <summary>
    /// A merge zone of the road graph: at most one car at a time may drive on the <see cref="RoadNode"/>s that reference it;
    /// the others wait on the node before the zone. Optionally drives an automatic gate arm (the parking entrance).
    /// </summary>
    /// <remarks>
    /// Pure presentation: the traffic logic never sees zones, it only decides where cars go. Occupancy is runtime state
    /// of the scene object and starts empty on every Play. Has no <c>Update</c>; <see cref="CarAgents"/> ticks it.
    /// </remarks>
    public sealed class TrafficZone : MonoBehaviour
    {
        /// <summary>Value of <see cref="OccupantCarId"/> when the zone is free.</summary>
        public const int NoCar = -1;

        [SerializeField]
        [Tooltip("Colour of the zone's road nodes in the Scene view.")]
        private Color _gizmoColor = new Color(1f, 0.5f, 0f, 1f);

        [SerializeField]
        [Tooltip("Optional automatic gate: open while a car is in the zone (parking entrance).")]
        private BarrierArm _gate;

        private int _occupantCarId = NoCar;

        /// <summary>Colour of the zone's nodes in the Scene view.</summary>
        public Color GizmoColor => _gizmoColor;

        /// <summary>True while a car holds the zone.</summary>
        public bool IsOccupied => _occupantCarId != NoCar;

        /// <summary>Id of the car holding the zone, or <see cref="NoCar"/>.</summary>
        public int OccupantCarId => _occupantCarId;

        /// <summary>Lets <paramref name="carId"/> into the zone.</summary>
        /// <returns>True when the zone was free or already held by this car; false when another car holds it.</returns>
        public bool TryEnter(int carId)
        {
            if (_occupantCarId == carId)
            {
                return true;
            }

            if (_occupantCarId != NoCar)
            {
                return false;
            }

            _occupantCarId = carId;
            return true;
        }

        /// <summary>Frees the zone if <paramref name="carId"/> holds it; otherwise does nothing.</summary>
        public void Exit(int carId)
        {
            if (_occupantCarId == carId)
            {
                _occupantCarId = NoCar;
            }
        }

        /// <summary>Opens or closes the gate (if any) to match the occupancy and animates it.</summary>
        /// <param name="unscaledDeltaTime">Unscaled frame time: the arm finishes moving even while the game is paused.</param>
        public void TickVisual(float unscaledDeltaTime)
        {
            if (_gate == null)
            {
                return;
            }

            _gate.SetOpen(IsOccupied);
            _gate.Animate(unscaledDeltaTime);
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = _gizmoColor;
            Gizmos.DrawWireCube(transform.position, new Vector3(0.6f, 0.6f, 0.6f));
#if UNITY_EDITOR
            UnityEditor.Handles.Label(transform.position + Vector3.up * 1.5f, name);
#endif
        }
    }
}
