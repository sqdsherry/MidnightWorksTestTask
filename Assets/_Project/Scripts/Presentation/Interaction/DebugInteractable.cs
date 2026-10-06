using UnityEngine;

namespace AutoService.Presentation.Interaction
{
    /// <summary>
    /// Whitebox test object: logs interaction start/end and shows an "occupied" color while the character interacts.
    /// Useful for debugging movement and interaction without real service points.
    /// </summary>
    public sealed class DebugInteractable : MonoBehaviour, IInteractable
    {
        [SerializeField]
        [Tooltip("Where the character stands; its forward (blue Z axis) is the direction the character faces. Falls back to this transform.")]
        private Transform _approachPoint;

        [SerializeField]
        [Tooltip("Optional hover/occupied feedback.")]
        private InteractableHighlight _highlight;

        [SerializeField]
        [Tooltip("When off, clicks are ignored and the object is not highlighted.")]
        private bool _isInteractable = true;

        [SerializeField]
        [Tooltip("Highlight color while the character is interacting.")]
        private Color _occupiedColor = new Color(0.2f, 1f, 0.4f, 1f);

        private bool _isHovered;
        private bool _isOccupied;

        /// <inheritdoc />
        public Vector3 ApproachPosition => ApproachTransform.position;

        /// <inheritdoc />
        public Quaternion ApproachRotation => ApproachTransform.rotation;

        /// <inheritdoc />
        public bool IsInteractable => _isInteractable;

        private Transform ApproachTransform => _approachPoint != null ? _approachPoint : transform;

        /// <inheritdoc />
        public void SetHighlighted(bool highlighted)
        {
            _isHovered = highlighted;
            RefreshHighlight();
        }

        /// <inheritdoc />
        public void BeginInteraction()
        {
            _isOccupied = true;
            RefreshHighlight();
            Debug.Log("[DebugInteractable] BeginInteraction: " + name, this);
        }

        /// <inheritdoc />
        public void EndInteraction()
        {
            _isOccupied = false;
            RefreshHighlight();
            Debug.Log("[DebugInteractable] EndInteraction: " + name, this);
        }

        // Why: "occupied" wins over hover so the interaction state stays visible while the cursor moves around.
        private void RefreshHighlight()
        {
            if (_highlight == null)
            {
                return;
            }

            if (_isOccupied)
            {
                _highlight.SetHighlighted(true, _occupiedColor);
            }
            else
            {
                _highlight.SetHighlighted(_isHovered);
            }
        }

        private void OnDrawGizmos()
        {
            Transform approach = ApproachTransform;
            Vector3 position = approach.position;
            Vector3 forward = approach.forward;
            Vector3 right = approach.right;

            Gizmos.color = _isInteractable ? Color.cyan : Color.gray;
            Gizmos.DrawWireSphere(position, 0.25f);

            Vector3 tip = position + forward * 0.8f;
            Gizmos.DrawLine(position, tip);
            Gizmos.DrawLine(tip, tip - forward * 0.25f + right * 0.15f);
            Gizmos.DrawLine(tip, tip - forward * 0.25f - right * 0.15f);
        }
    }
}
