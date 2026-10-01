using System;
using UnityEngine;

namespace AutoService.Presentation.Interaction
{
    /// <summary>
    /// Casts rays from the camera through a screen position to find interactables and points on the ground.
    /// Allocation-free: uses <see cref="Physics.RaycastNonAlloc(Ray, RaycastHit[], float, int, QueryTriggerInteraction)"/>
    /// with a preallocated buffer.
    /// </summary>
    /// <remarks>Does not know about UI; blocking clicks over UI is the caller's job.</remarks>
    public sealed class PointerRaycaster
    {
        private const int BufferSize = 8;

        private readonly Camera _camera;
        private readonly int _interactableMask;
        private readonly int _groundMask;
        private readonly float _maxDistance;
        private readonly RaycastHit[] _hits = new RaycastHit[BufferSize];

        // Why: hover queries run every frame; GetComponentInParent is only repeated when the pointer moves to another collider.
        private Collider _cachedCollider;
        private IInteractable _cachedInteractable;

        /// <summary>Creates the raycaster.</summary>
        /// <param name="camera">Camera the screen positions belong to.</param>
        /// <param name="interactableMask">Layers of interactable colliders.</param>
        /// <param name="groundMask">Layers of walkable ground.</param>
        /// <param name="maxDistance">Ray length in meters.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="camera"/> is null.</exception>
        public PointerRaycaster(Camera camera, LayerMask interactableMask, LayerMask groundMask, float maxDistance = 200f)
        {
            if (camera == null)
            {
                throw new ArgumentNullException(nameof(camera));
            }

            _camera = camera;
            _interactableMask = interactableMask.value;
            _groundMask = groundMask.value;
            _maxDistance = maxDistance;
        }

        /// <summary>Finds the nearest interactable under <paramref name="screenPosition"/>.</summary>
        /// <param name="screenPosition">Screen position in pixels.</param>
        /// <param name="interactable">The interactable owning the nearest hit collider (searched up the hierarchy), or null.</param>
        /// <returns>True if an interactable was found. Its <see cref="IInteractable.IsInteractable"/> flag is not checked.</returns>
        public bool TryGetInteractable(Vector2 screenPosition, out IInteractable interactable)
        {
            // Why: triggers count here, so large invisible click zones can be added around small objects later.
            if (!TryGetNearestHit(screenPosition, _interactableMask, QueryTriggerInteraction.Collide, out RaycastHit hit))
            {
                interactable = null;
                return false;
            }

            Collider hitCollider = hit.collider;
            if (hitCollider != _cachedCollider)
            {
                _cachedCollider = hitCollider;
                _cachedInteractable = hitCollider.GetComponentInParent<IInteractable>();
            }

            interactable = _cachedInteractable;
            return interactable.IsAlive();
        }

        /// <summary>Finds the nearest point on the ground under <paramref name="screenPosition"/>.</summary>
        /// <param name="screenPosition">Screen position in pixels.</param>
        /// <param name="point">The world-space hit point, or <see cref="Vector3.zero"/>.</param>
        /// <returns>True if the ground was hit.</returns>
        public bool TryGetGroundPoint(Vector2 screenPosition, out Vector3 point)
        {
            if (!TryGetNearestHit(screenPosition, _groundMask, QueryTriggerInteraction.Ignore, out RaycastHit hit))
            {
                point = Vector3.zero;
                return false;
            }

            point = hit.point;
            return true;
        }

        private bool TryGetNearestHit(Vector2 screenPosition, int mask, QueryTriggerInteraction triggers, out RaycastHit nearest)
        {
            Ray ray = _camera.ScreenPointToRay(screenPosition);
            int count = Physics.RaycastNonAlloc(ray, _hits, _maxDistance, mask, triggers);

            // Why: RaycastNonAlloc returns hits in no particular order.
            int nearestIndex = -1;
            float nearestDistance = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                if (_hits[i].distance < nearestDistance)
                {
                    nearestDistance = _hits[i].distance;
                    nearestIndex = i;
                }
            }

            nearest = nearestIndex >= 0 ? _hits[nearestIndex] : default;
            return nearestIndex >= 0;
        }
    }
}
