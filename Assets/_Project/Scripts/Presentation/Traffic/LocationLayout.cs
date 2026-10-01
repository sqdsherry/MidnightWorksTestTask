using System;
using AutoService.Presentation.Points;
using AutoService.Services.Traffic;
using UnityEngine;

namespace AutoService.Presentation.Traffic
{
    /// <summary>
    /// Scene markup of one location: its points, spawn/exit, entry queue slots and parking slots.
    /// Resolves engine-agnostic <see cref="CarDestination"/>s into transforms.
    /// </summary>
    public sealed class LocationLayout : MonoBehaviour
    {
        private const float SlotGizmoRadius = 0.6f;

        [SerializeField]
        [Tooltip("Location id; every point of this location gets it as LocationId, e.g. \"loc1\".")]
        private string _locationId = string.Empty;

        [SerializeField]
        [Tooltip("The parking barrier point.")]
        private ServicePointView _barrier;

        [SerializeField]
        [Tooltip("Service points of the location (wash...). Not the barrier.")]
        private ServicePointView[] _servicePoints = new ServicePointView[0];

        [SerializeField]
        [Tooltip("Where cars appear; its forward is their initial heading.")]
        private Transform _spawnPoint;

        [SerializeField]
        [Tooltip("Where served cars drive to and disappear.")]
        private Transform _exitPoint;

        [SerializeField]
        [Tooltip("Entry queue lane slots; element 0 is the head at the fork.")]
        private Transform[] _queueSlots = new Transform[0];

        [SerializeField]
        [Tooltip("Parking slots; forward = the direction a parked car faces.")]
        private Transform[] _parkingSlots = new Transform[0];

        /// <summary>Location id.</summary>
        public string LocationId => _locationId;

        /// <summary>The parking barrier point (may be null if not assigned).</summary>
        public ServicePointView Barrier => _barrier;

        /// <summary>Service points of the location (elements may be null if left empty).</summary>
        public ServicePointView[] ServicePoints => _servicePoints;

        /// <summary>Spawn point (may be null if not assigned).</summary>
        public Transform SpawnPoint => _spawnPoint;

        /// <summary>Number of entry queue slots.</summary>
        public int QueueSlotCount => _queueSlots.Length;

        /// <summary>Number of parking slots.</summary>
        public int ParkingSlotCount => _parkingSlots.Length;

        /// <summary>
        /// Checks that every reference needed at runtime is assigned.
        /// </summary>
        /// <param name="problem">Description of the first problem found, or null.</param>
        /// <returns>True when the layout is complete.</returns>
        public bool Validate(out string problem)
        {
            problem = null;
            if (string.IsNullOrWhiteSpace(_locationId))
            {
                problem = "Location Id is empty";
            }
            else if (_barrier == null)
            {
                problem = "Barrier is not assigned";
            }
            else if (_barrier.CarSpot == null)
            {
                problem = "Barrier '" + _barrier.name + "' has no Car Spot";
            }
            else if (_spawnPoint == null)
            {
                problem = "Spawn Point is not assigned";
            }
            else if (_exitPoint == null)
            {
                problem = "Exit Point is not assigned";
            }
            else if (_queueSlots.Length == 0)
            {
                problem = "Queue Slots is empty";
            }
            else if (IndexOfNull(_queueSlots) >= 0)
            {
                problem = "Queue Slots element " + IndexOfNull(_queueSlots) + " is empty";
            }
            else if (IndexOfNull(_parkingSlots) >= 0)
            {
                problem = "Parking Slots element " + IndexOfNull(_parkingSlots) + " is empty";
            }
            else
            {
                for (int i = 0; i < _servicePoints.Length; i++)
                {
                    if (_servicePoints[i] == null)
                    {
                        problem = "Service Points element " + i + " is empty";
                        break;
                    }

                    if (_servicePoints[i].CarSpot == null)
                    {
                        problem = "Service point '" + _servicePoints[i].name + "' has no Car Spot";
                        break;
                    }
                }
            }

            return problem == null;
        }

        /// <summary>Resolves a destination into the transform the car should stop at (position + heading).</summary>
        /// <returns>False when the destination does not exist in this layout (bad index, unknown point, unassigned reference).</returns>
        public bool TryResolve(in CarDestination destination, out Transform target)
        {
            switch (destination.Kind)
            {
                case CarDestinationKind.QueueSlot:
                    target = ElementOrNull(_queueSlots, destination.Index);
                    break;
                case CarDestinationKind.ParkingSlot:
                    target = ElementOrNull(_parkingSlots, destination.Index);
                    break;
                case CarDestinationKind.Barrier:
                    target = _barrier != null ? _barrier.CarSpot : null;
                    break;
                case CarDestinationKind.Point:
                    target = FindCarSpot(destination.PointId);
                    break;
                case CarDestinationKind.Exit:
                    target = _exitPoint;
                    break;
                default:
                    target = null;
                    break;
            }

            return target != null;
        }

        // Why: a linear scan over a handful of points with ordinal comparison — no dictionary to keep in sync, no allocations.
        private Transform FindCarSpot(string pointId)
        {
            if (_barrier != null && string.Equals(_barrier.PointId, pointId, StringComparison.Ordinal))
            {
                return _barrier.CarSpot;
            }

            for (int i = 0; i < _servicePoints.Length; i++)
            {
                ServicePointView point = _servicePoints[i];
                if (point != null && string.Equals(point.PointId, pointId, StringComparison.Ordinal))
                {
                    return point.CarSpot;
                }
            }

            return null;
        }

        private static Transform ElementOrNull(Transform[] slots, int index)
        {
            return index >= 0 && index < slots.Length ? slots[index] : null;
        }

        private static int IndexOfNull(Transform[] slots)
        {
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] == null)
                {
                    return i;
                }
            }

            return -1;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.yellow;
            for (int i = 0; i < _queueSlots.Length; i++)
            {
                Transform slot = _queueSlots[i];
                if (slot == null)
                {
                    continue;
                }

                Gizmos.DrawWireSphere(slot.position, SlotGizmoRadius);
                Gizmos.DrawLine(slot.position, slot.position + slot.forward * 2f);
                if (i > 0 && _queueSlots[i - 1] != null)
                {
                    Gizmos.DrawLine(_queueSlots[i - 1].position, slot.position);
                }

                DrawLabel(slot.position, "Q" + i);
            }

            Gizmos.color = Color.green;
            for (int i = 0; i < _parkingSlots.Length; i++)
            {
                Transform slot = _parkingSlots[i];
                if (slot == null)
                {
                    continue;
                }

                Gizmos.DrawWireSphere(slot.position, SlotGizmoRadius);
                Gizmos.DrawLine(slot.position, slot.position + slot.forward * 2f);
                DrawLabel(slot.position, "P" + i);
            }

            DrawEndpoint(_spawnPoint, Color.blue, "Spawn");
            DrawEndpoint(_exitPoint, Color.red, "Exit");
        }

        private static void DrawEndpoint(Transform point, Color color, string label)
        {
            if (point == null)
            {
                return;
            }

            Gizmos.color = color;
            Gizmos.DrawSphere(point.position, 0.4f);
            Gizmos.DrawLine(point.position, point.position + point.forward * 2f);
            DrawLabel(point.position, label);
        }

        private static void DrawLabel(Vector3 position, string text)
        {
#if UNITY_EDITOR
            UnityEditor.Handles.Label(position + Vector3.up * 1.2f, text);
#endif
        }
    }
}
