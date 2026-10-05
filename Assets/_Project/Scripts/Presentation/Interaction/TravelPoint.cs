using AutoService.Domain.Common;
using AutoService.Presentation.Interaction;
using AutoService.Presentation.Player;
using AutoService.Presentation.CameraControl;
using UnityEngine;
using UnityEngine.AI;

namespace AutoService.Presentation.Interaction
{
    public class TravelPoint : MonoBehaviour, IInteractable
    {
        [SerializeField] private Transform _targetTransform;
        [SerializeField] private float _dwellSeconds = 1.5f;
        [SerializeField] private DwellRingView _ring;
        [SerializeField] private InteractableHighlight _highlight;

        private DwellProgress _dwell;
        private bool _isInteracting;

        public Transform TargetTransform
        {
            get => _targetTransform;
            set => _targetTransform = value;
        }

        public Vector3 ApproachPosition => transform.position;
        public Quaternion ApproachRotation => transform.rotation;
        public bool IsInteractable => isActiveAndEnabled;

        private void OnEnable()
        {
            _dwell = new DwellProgress(_dwellSeconds);
        }

        public void SetHighlighted(bool highlighted)
        {
            if (_highlight != null)
            {
                _highlight.SetHighlighted(highlighted);
            }
        }

        public void BeginInteraction()
        {
            _isInteracting = true;
            _dwell.Begin();
        }

        public void EndInteraction()
        {
            _isInteracting = false;
            _dwell.End();
            if (_ring != null)
            {
                _ring.Render(0f);
            }
        }

        private void Update()
        {
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
            if (_targetTransform == null) return;
            
            var player = Object.FindFirstObjectByType<PlayerView>();
            if (player != null && player.TryGetComponent(out NavMeshAgent agent))
            {
                if (NavMesh.SamplePosition(_targetTransform.position, out NavMeshHit hit, 5.0f, NavMesh.AllAreas))
                {
                    agent.Warp(hit.position);
                }
                else
                {
                    agent.Warp(_targetTransform.position);
                }
                
                var cameraRig = Object.FindFirstObjectByType<CameraRig>();
                if (cameraRig != null)
                {
                    // If target X > 100, we are on Loc2, otherwise Loc1
                    bool isLoc2 = _targetTransform.position.x > 100f;
                    Vector2 minBounds = isLoc2 ? new Vector2(175f, -25f) : new Vector2(-25f, -25f);
                    Vector2 maxBounds = isLoc2 ? new Vector2(225f, 25f) : new Vector2(25f, 25f);
                    
                    cameraRig.SnapTo(agent.transform.position, minBounds, maxBounds);
                }
            }
        }
    }
}
