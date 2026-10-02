using System;
using AutoService.Domain.Common;
using AutoService.Presentation.Interaction;
using UnityEngine;

namespace AutoService.Presentation.Points
{
    /// <summary>
    /// Blue "purchases" pad next to a point or the warehouse (yellow = work, blue = buying): the character walks onto it,
    /// stands for a moment (ring) and the management panel of its target opens next to it.
    /// </summary>
    /// <remarks>
    /// Same "walk up and stand" mechanics as a build plot (<see cref="DwellProgress"/>): the owning presenter ticks the dwell
    /// (<see cref="TickDwell"/>) and listens to <see cref="DwellCompleted"/> / <see cref="Left"/>. The rule is the same whether
    /// the point has a worker or not. A pad of a bay that is still a build plot sits inside the plot's Target, so it only
    /// appears once the bay is built.
    /// </remarks>
    public sealed class ManagePadView : MonoBehaviour, IInteractable
    {
        [SerializeField]
        [Tooltip("Point id (Target = Service Point) or location id (Target = Warehouse).")]
        private string _targetId = string.Empty;

        [SerializeField]
        [Tooltip("What the pad manages.")]
        private ManagePadTarget _target = ManagePadTarget.ServicePoint;

        [SerializeField]
        [Tooltip("Where the character stands (on the pad), facing the target. Also where the storekeeper hands over boxes.")]
        private Transform _approachPoint;

        [SerializeField]
        [Tooltip("Optional hover feedback.")]
        private InteractableHighlight _highlight;

        [SerializeField]
        [Tooltip("Optional world-space dwell ring.")]
        private DwellRingView _ring;

        [SerializeField, Min(0f)]
        [Tooltip("Seconds the character has to stand on the pad before the panel opens.")]
        private float _dwellSeconds = 1.5f;

        [SerializeField]
        [Tooltip("World point the panel sticks to. Defaults to this object.")]
        private Transform _panelAnchor;

        private DwellProgress _dwell;

        /// <summary>Raised once per visit when the character has stood on the pad long enough.</summary>
        public event Action<ManagePadView> DwellCompleted;

        /// <summary>Raised when the character stops interacting with the pad (walked away / new command).</summary>
        public event Action<ManagePadView> Left;

        /// <summary>Point id or location id, see <see cref="Target"/>.</summary>
        public string TargetId => _targetId;

        /// <summary>What the pad manages.</summary>
        public ManagePadTarget Target => _target;

        /// <summary>Where the character (and the storekeeper with a box) stands.</summary>
        public Transform ApproachPoint => _approachPoint != null ? _approachPoint : transform;

        /// <summary>World point the panel sticks to.</summary>
        public Transform PanelAnchor => _panelAnchor != null ? _panelAnchor : transform;

        /// <inheritdoc />
        public Vector3 ApproachPosition => ApproachPoint.position;

        /// <inheritdoc />
        public Quaternion ApproachRotation => ApproachPoint.rotation;

        /// <inheritdoc />
        /// <remarks>Clickable while active (a pad of an unbuilt bay is inactive with the bay).</remarks>
        public bool IsInteractable => isActiveAndEnabled;

        // Why: created on first use, not in Awake — the presenter may tick the pad before it was ever enabled.
        private DwellProgress Dwell => _dwell ??= new DwellProgress(_dwellSeconds);

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
            Dwell.Begin();
        }

        /// <inheritdoc />
        public void EndInteraction()
        {
            Dwell.End();
            Left?.Invoke(this);
        }

        /// <summary>Advances the dwell timer and the ring; raises <see cref="DwellCompleted"/> when it fills up.</summary>
        /// <param name="deltaTime">Scaled seconds (0 while paused).</param>
        public void TickDwell(float deltaTime)
        {
            DwellProgress dwell = Dwell;
            bool completed = dwell.Tick(deltaTime);
            if (_ring != null)
            {
                _ring.Render(dwell.Progress01);
            }

            if (completed)
            {
                DwellCompleted?.Invoke(this);
            }
        }

        private void OnDrawGizmos()
        {
            Transform approach = ApproachPoint;
            Gizmos.color = new Color(0.25f, 0.55f, 1f, 1f);
            Gizmos.DrawWireSphere(approach.position, 0.4f);
            Gizmos.DrawLine(approach.position, approach.position + approach.forward * 0.8f);
        }
    }
}
