using AutoService.Domain.Common;
using AutoService.Presentation.Player;
using AutoService.Services.Core;
using UnityEngine;

namespace AutoService.Presentation.Interaction
{
    /// <summary>
    /// Travel pad between locations: standing on it (or walking to it by a click) fills a dwell ring; when it is full
    /// the character is teleported to <see cref="TargetTransform"/>.
    /// </summary>
    /// <remarks>Ticked by the game loop once <see cref="Construct"/> has been called; inactive pads (not built yet) skip the tick.</remarks>
    public sealed class TravelPoint : MonoBehaviour, IInteractable, ITickable
    {
        private const float ProximityEnterRadius = 1.1f;
        private const float ProximityExitRadius = 1.4f;
        private const float MaxVerticalDistance = 1.5f;

        [SerializeField]
        [Tooltip("Destination transform where the player is teleported to.")]
        private Transform _targetTransform;

        [SerializeField, Min(0.1f)]
        [Tooltip("Seconds the player must stand on the pad before teleportation occurs.")]
        private float _dwellSeconds = 1.5f;

        [SerializeField]
        [Tooltip("World-space dwell ring indicating interaction progress.")]
        private DwellRingView _ring;

        [SerializeField]
        [Tooltip("Optional hover highlight renderer.")]
        private InteractableHighlight _highlight;

        private PlayerTeleporter _teleporter;
        private DwellProgress _dwell;
        private bool _isInteracting;

        // Why: after arriving on a pad the character must step off it once before it can fire again.
        private bool _hasExitedSinceWarp = true;

        /// <summary>The target transform where the player arrives after teleportation.</summary>
        public Transform TargetTransform
        {
            get => _targetTransform;
            set => _targetTransform = value;
        }

        /// <inheritdoc />
        public Vector3 ApproachPosition => transform.position;

        /// <inheritdoc />
        public Quaternion ApproachRotation => transform.rotation;

        /// <inheritdoc />
        public bool IsInteractable => isActiveAndEnabled;

        /// <summary>Injects the teleporter shared by all pads.</summary>
        public void Construct(PlayerTeleporter teleporter)
        {
            _teleporter = teleporter;
        }

        private void OnEnable()
        {
            _dwell = new DwellProgress(_dwellSeconds);
            _hasExitedSinceWarp = true;
            RenderRing(0f);
        }

        private void OnDisable()
        {
            EndInteraction();
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
            if (_teleporter == null || _teleporter.IsCoolingDown)
            {
                return;
            }

            _isInteracting = true;
            _hasExitedSinceWarp = true;
            _dwell.Begin();
        }

        /// <inheritdoc />
        public void EndInteraction()
        {
            _isInteracting = false;
            _dwell?.End();
            RenderRing(0f);
        }

        /// <inheritdoc />
        public void Tick(float deltaTime)
        {
            if (_teleporter == null || !isActiveAndEnabled)
            {
                return;
            }

            if (_teleporter.IsCoolingDown)
            {
                if (_isInteracting)
                {
                    EndInteraction();
                }

                return;
            }

            bool playerNearby = IsPlayerOnPad();
            if (!_isInteracting && _hasExitedSinceWarp && playerNearby)
            {
                BeginInteraction();
            }
            else if (_isInteracting && !playerNearby)
            {
                EndInteraction();
                return;
            }

            if (!_isInteracting)
            {
                return;
            }

            bool completed = _dwell.Tick(deltaTime);
            RenderRing(_dwell.Progress01);
            if (completed)
            {
                EndInteraction();
                if (_targetTransform != null)
                {
                    _teleporter.Teleport(_targetTransform.position);
                }
            }
        }

        private bool IsPlayerOnPad()
        {
            Vector3 player = _teleporter.PlayerPosition;
            Vector3 pad = transform.position;
            float dx = player.x - pad.x;
            float dz = player.z - pad.z;
            float horizontalSqr = dx * dx + dz * dz;

            if (horizontalSqr > ProximityExitRadius * ProximityExitRadius)
            {
                _hasExitedSinceWarp = true;
            }

            return horizontalSqr <= ProximityEnterRadius * ProximityEnterRadius
                && Mathf.Abs(player.y - pad.y) <= MaxVerticalDistance;
        }

        private void RenderRing(float progress01)
        {
            if (_ring != null)
            {
                _ring.Render(progress01);
            }
        }
    }
}
