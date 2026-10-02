using AutoService.Domain.Points;
using AutoService.Presentation.Interaction;
using AutoService.Presentation.Traffic.Routing;
using AutoService.Services.Points;
using AutoService.Services.Staff;
using AutoService.Services.Supplies;
using UnityEngine;

namespace AutoService.Presentation.Points
{
    /// <summary>
    /// Scene representation of a service point (wash bay, parking barrier...): its ids, where the car stands,
    /// where the worker stands, and the click interaction that occupies the work spot.
    /// </summary>
    /// <remarks>
    /// The view holds no game state: <see cref="BeginInteraction"/>/<see cref="EndInteraction"/> forward to
    /// <see cref="IServicePointService"/>, which owns the <see cref="ServicePoint"/> entity.
    /// Components live on the parent object; colliders may sit on children (the pointer raycast resolves the parent).
    /// <para>With staff and supplies (<see cref="ConstructStaffSupplies"/>): a click first hands over the box the player
    /// carries (if it fits), then takes the work spot. Once a worker is hired the point is only clickable to deliver a box,
    /// and the player then walks to the point's supply drop (its blue pad) instead of the worker's spot.</para>
    /// </remarks>
    public sealed class ServicePointView : MonoBehaviour, IInteractable
    {
        private static readonly Vector3 CarFootprint = new Vector3(2f, 0.05f, 4f);

        [SerializeField]
        [Tooltip("Unique point id, e.g. \"loc1_wash_1\".")]
        private string _pointId = string.Empty;

        [SerializeField]
        [Tooltip("Id of a Service Type asset in GameConfig, e.g. \"wash\".")]
        private string _serviceTypeId = string.Empty;

        [SerializeField]
        [Tooltip("Where the car stops; its forward (blue Z axis) is the direction the car faces.")]
        private Transform _carSpot;

        [SerializeField]
        [Tooltip("Road nodes where cars wait for this service point; element 0 is the one nearest to the point. " +
                 "Leave empty for barriers. Every service point of a location needs the same number.")]
        private RoadNode[] _bufferSlots = new RoadNode[0];

        [SerializeField]
        [Tooltip("Work spot of the player/worker; its forward is the direction they face.")]
        private Transform _approachPoint;

        [SerializeField]
        [Tooltip("Optional hover feedback.")]
        private InteractableHighlight _highlight;

        [SerializeField]
        [Tooltip("Optional world-space indicators (progress, \"!\").")]
        private ServicePointHud _hud;

        private IServicePointService _service;
        private ISupplyService _supplies;
        private IPlayerCarry _carry;
        private IStaffService _staff;
        private Transform _supplyDrop;

        /// <summary>Unique point id.</summary>
        public string PointId => _pointId;

        /// <summary>Id of the point's service type.</summary>
        public string ServiceTypeId => _serviceTypeId;

        /// <summary>Where the car stops (may be null if not assigned).</summary>
        public Transform CarSpot => _carSpot;

        /// <summary>Buffer slots in front of the point (0 = nearest to it); elements may be null if left empty.</summary>
        public RoadNode[] BufferSlots => _bufferSlots;

        /// <summary>World-space indicators, or null.</summary>
        public ServicePointHud Hud => _hud;

        /// <summary>The work spot of the player / worker (where a hired worker walks to).</summary>
        public Transform WorkSpot => _approachPoint != null ? _approachPoint : transform;

        /// <inheritdoc />
        public Vector3 ApproachPosition => ApproachTransform.position;

        /// <inheritdoc />
        public Quaternion ApproachRotation => ApproachTransform.rotation;

        /// <inheritdoc />
        /// <remarks>
        /// Not interactable before <see cref="Construct"/> and for unregistered ids. Once a worker is hired (from the moment
        /// of hiring) only while the player carries a box that fits — the asymmetry of GDD 2026-09-30: the player never
        /// helps a worker, but still brings it supplies.
        /// </remarks>
        public bool IsInteractable =>
            _service != null
            && _service.TryGet(_pointId, out ServicePoint point)
            && (_staff != null ? !_staff.HasWorker(_pointId) || CanDeliverCarriedBox : point.Occupant != OccupantKind.Worker);

        // Why: with a worker on the spot the player hands the box over from the side, never walking into the worker.
        private Transform ApproachTransform =>
            _supplyDrop != null && _staff != null && _staff.HasWorker(_pointId) ? _supplyDrop : WorkSpot;

        private bool CanDeliverCarriedBox => _carry != null && _carry.HasBox && _supplies.CanDeliver(_carry.Box, _pointId);

        /// <summary>Injects the point registry. Called by the scene entry point after the point was registered.</summary>
        public void Construct(IServicePointService service)
        {
            _service = service;
        }

        /// <summary>Injects the staff and supply services. Called once the A2 modules are installed (also for bays built later).</summary>
        /// <param name="supplies">Deliveries.</param>
        /// <param name="carry">The player's hands.</param>
        /// <param name="staff">Who works here.</param>
        /// <param name="supplyDrop">Where the player stands to hand over a box once a worker is hired; may be null (the work spot).</param>
        public void ConstructStaffSupplies(ISupplyService supplies, IPlayerCarry carry, IStaffService staff, Transform supplyDrop)
        {
            _supplies = supplies;
            _carry = supplies != null ? carry : null;
            _staff = staff;
            _supplyDrop = supplyDrop;
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
            // Why: a box of another consumable (or one that does not fit) stays in the hands; the player still works here.
            if (CanDeliverCarriedBox && _supplies.TryDeliver(_carry.Box, _pointId, true))
            {
                _carry.Drop();
            }

            // Why: the spot of a hired worker is never the player's (GDD: the player does not help a worker). Without this
            // the player could take it while the worker still walks from the staff room — after handing over a box, or by
            // arriving on a work spot it was already walking to when the worker was hired — and the worker would wait forever.
            if (_staff != null && _staff.HasWorker(_pointId))
            {
                return;
            }

            // Why: the result is ignored on purpose — if someone else holds the spot, the player just stands next to it.
            _service?.TryOccupy(_pointId, OccupantKind.Player);
        }

        /// <inheritdoc />
        public void EndInteraction()
        {
            _service?.Vacate(_pointId, OccupantKind.Player);
        }

        private void OnDrawGizmos()
        {
            if (_carSpot != null)
            {
                Gizmos.color = new Color(1f, 0.6f, 0.1f, 1f);
                Matrix4x4 previous = Gizmos.matrix;
                Gizmos.matrix = Matrix4x4.TRS(_carSpot.position, _carSpot.rotation, Vector3.one);
                Gizmos.DrawWireCube(Vector3.up * CarFootprint.y, CarFootprint);
                DrawArrow(Vector3.zero, Vector3.forward, Vector3.right, 2.5f);
                Gizmos.matrix = previous;
            }

            Transform approach = ApproachTransform;
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(approach.position, 0.3f);
            DrawArrow(approach.position, approach.forward, approach.right, 0.8f);
        }

        private static void DrawArrow(Vector3 origin, Vector3 forward, Vector3 right, float length)
        {
            Vector3 tip = origin + forward * length;
            Gizmos.DrawLine(origin, tip);
            Gizmos.DrawLine(tip, tip - forward * 0.3f + right * 0.2f);
            Gizmos.DrawLine(tip, tip - forward * 0.3f - right * 0.2f);
        }
    }
}
