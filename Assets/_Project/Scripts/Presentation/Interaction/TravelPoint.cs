using AutoService.Domain.Common;
using AutoService.Presentation.CameraControl;
using AutoService.Presentation.Player;
using UnityEngine;
using UnityEngine.AI;

namespace AutoService.Presentation.Interaction
{
    /// <summary>
    /// Bidirectional teleport point between locations: standing on it or clicking it starts a dwell timer
    /// with radial progress visualization; on completion the player is warped to the destination point.
    /// </summary>
    public class TravelPoint : MonoBehaviour, IInteractable
    {
        private const float TeleportCooldown = 2.0f;
        private const float ProximityEnterRadius = 1.1f;
        private const float ProximityExitRadius = 1.4f;
        private const float MaxVerticalDistance = 1.5f;

        private static float _lastTeleportTime = -10f;

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

        private DwellProgress _dwell;
        private bool _isInteracting;
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

        private void OnEnable()
        {
            _dwell = new DwellProgress(_dwellSeconds);
            _hasExitedSinceWarp = true;
            if (_ring != null)
            {
                _ring.Render(0f);
            }
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
            if (Time.time < _lastTeleportTime + TeleportCooldown)
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
            if (_ring != null)
            {
                _ring.Render(0f);
            }
        }

        private void Update()
        {
            if (Time.time < _lastTeleportTime + TeleportCooldown)
            {
                if (_isInteracting)
                {
                    EndInteraction();
                }
                else if (_ring != null)
                {
                    _ring.Render(0f);
                }
                return;
            }

            var player = Object.FindFirstObjectByType<PlayerView>();
            bool playerNearby = false;
            if (player != null)
            {
                Vector3 pPos = player.transform.position;
                Vector3 tPos = transform.position;
                float horizontalDist = Vector2.Distance(new Vector2(pPos.x, pPos.z), new Vector2(tPos.x, tPos.z));
                float verticalDist = Mathf.Abs(pPos.y - tPos.y);

                if (horizontalDist > ProximityExitRadius)
                {
                    _hasExitedSinceWarp = true;
                }

                if (horizontalDist <= ProximityEnterRadius && verticalDist <= MaxVerticalDistance)
                {
                    playerNearby = true;
                }
            }

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
                if (_dwell != null && _dwell.Progress01 > 0f)
                {
                    _dwell.Tick(Time.deltaTime);
                    if (_ring != null)
                    {
                        _ring.Render(_dwell.Progress01);
                    }
                }
                return;
            }

            bool completed = _dwell.Tick(Time.deltaTime);
            if (_ring != null)
            {
                _ring.Render(_dwell.Progress01);
            }

            if (completed)
            {
                TeleportPlayer();
                EndInteraction();
            }
        }

        private void TeleportPlayer()
        {
            if (_targetTransform == null)
            {
                return;
            }

            _lastTeleportTime = Time.time;

            var player = Object.FindFirstObjectByType<PlayerView>();
            if (player != null && player.TryGetComponent(out NavMeshAgent agent))
            {
                Vector3 targetPos = _targetTransform.position;
                if (NavMesh.SamplePosition(targetPos, out NavMeshHit hit, 5.0f, NavMesh.AllAreas))
                {
                    agent.Warp(hit.position);
                }
                else
                {
                    agent.Warp(targetPos);
                }

                player.Stop();

                var cameraRig = Object.FindFirstObjectByType<CameraRig>();
                if (cameraRig != null)
                {
                    // If target X > 100, we are on Loc2, otherwise Loc1
                    bool isLoc2 = targetPos.x > 100f;
                    Vector2 minBounds = isLoc2 ? new Vector2(175f, -25f) : new Vector2(-25f, -25f);
                    Vector2 maxBounds = isLoc2 ? new Vector2(225f, 25f) : new Vector2(25f, 25f);

                    cameraRig.SnapTo(agent.transform.position, minBounds, maxBounds);
                }
            }
        }
    }
}
