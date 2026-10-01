using System;
using UnityEngine;

namespace AutoService.Presentation.Interaction
{
    /// <summary>
    /// Casts a ray from the camera through a screen position and reports what the nearest collider is:
    /// an interactable, the ground, or an occluder (walls, roofs) that hides whatever is behind it.
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
        private readonly int _combinedMask;
        private readonly float _maxDistance;
        private readonly RaycastHit[] _hits = new RaycastHit[BufferSize];

        // Why: hover queries run every frame; GetComponentInParent is only repeated when the pointer moves to another collider.
        private Collider _cachedCollider;
        private IInteractable _cachedInteractable;

        /// <summary>Creates the raycaster.</summary>
        /// <param name="camera">Camera the screen positions belong to.</param>
        /// <param name="interactableMask">Layers of interactable colliders. Wins if a layer is also in another mask.</param>
        /// <param name="groundMask">Layers of walkable ground.</param>
        /// <param name="occluderMask">Layers that only block the ray (neither clickable nor walkable). Empty by default.</param>
        /// <param name="maxDistance">Ray length in meters.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="camera"/> is null.</exception>
        public PointerRaycaster(
            Camera camera,
            LayerMask interactableMask,
            LayerMask groundMask,
            LayerMask occluderMask = default,
            float maxDistance = 200f)
        {
            if (camera == null)
            {
                throw new ArgumentNullException(nameof(camera));
            }

            _camera = camera;
            _interactableMask = interactableMask.value;
            _groundMask = groundMask.value;
            _combinedMask = _interactableMask | _groundMask | occluderMask.value;
            _maxDistance = maxDistance;
        }

        /// <summary>
        /// Casts one ray through <paramref name="screenPosition"/> and classifies the nearest collider, so an object
        /// behind another collider (or behind the ground) is never picked.
        /// </summary>
        /// <param name="screenPosition">Screen position in pixels.</param>
        /// <param name="hit">The classified nearest hit; <c>default</c> when nothing usable was hit.</param>
        /// <returns>True if the nearest hit is an interactable or the ground; false for an occluder or no hit.</returns>
        public bool TryRaycast(Vector2 screenPosition, out PointerHit hit)
        {
            Ray ray = _camera.ScreenPointToRay(screenPosition);

            // Why: triggers count, so large invisible click zones can be added around small interactables later.
            int count = Physics.RaycastNonAlloc(ray, _hits, _maxDistance, _combinedMask, QueryTriggerInteraction.Collide);

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

            hit = default;
            if (nearestIndex < 0)
            {
                return false;
            }

            Collider nearestCollider = _hits[nearestIndex].collider;
            int layerBit = 1 << nearestCollider.gameObject.layer;

            if ((layerBit & _interactableMask) != 0)
            {
                IInteractable interactable = ResolveInteractable(nearestCollider);
                if (!interactable.IsAlive())
                {
                    return false;
                }

                hit = new PointerHit(interactable, false, Vector3.zero);
                return true;
            }

            if ((layerBit & _groundMask) != 0)
            {
                hit = new PointerHit(null, true, _hits[nearestIndex].point);
                return true;
            }

            // Occluder: blocks both hover and clicks behind it.
            return false;
        }

        private IInteractable ResolveInteractable(Collider hitCollider)
        {
            if (hitCollider != _cachedCollider)
            {
                _cachedCollider = hitCollider;
                _cachedInteractable = hitCollider.GetComponentInParent<IInteractable>();
            }

            return _cachedInteractable;
        }
    }
}
