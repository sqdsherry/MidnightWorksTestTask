using UnityEngine;

namespace AutoService.Presentation.Interaction
{
    /// <summary>
    /// Result of <see cref="PointerRaycaster.TryRaycast"/>: what the nearest collider under the pointer is.
    /// At most one of <see cref="Interactable"/> / <see cref="HasGroundPoint"/> is set; both are empty when the nearest hit
    /// is an occluder.
    /// </summary>
    public readonly struct PointerHit
    {
        /// <summary>Creates a hit.</summary>
        /// <param name="interactable">The interactable owning the nearest collider, or null.</param>
        /// <param name="hasGroundPoint">True if the nearest collider is ground.</param>
        /// <param name="groundPoint">World-space point on the ground (meaningful only with <paramref name="hasGroundPoint"/>).</param>
        public PointerHit(IInteractable interactable, bool hasGroundPoint, Vector3 groundPoint)
        {
            Interactable = interactable;
            HasGroundPoint = hasGroundPoint;
            GroundPoint = groundPoint;
        }

        /// <summary>
        /// The interactable whose collider is nearest to the camera, or null. Its <see cref="IInteractable.IsInteractable"/>
        /// flag is not checked.
        /// </summary>
        public IInteractable Interactable { get; }

        /// <summary>True if the nearest collider is on a ground layer.</summary>
        public bool HasGroundPoint { get; }

        /// <summary>World-space hit point on the ground; <see cref="Vector3.zero"/> when <see cref="HasGroundPoint"/> is false.</summary>
        public Vector3 GroundPoint { get; }
    }
}
