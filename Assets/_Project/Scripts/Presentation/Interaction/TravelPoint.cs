using AutoService.Domain.Common;
using UnityEngine;
using UnityEngine.AI;

namespace AutoService.Presentation.Interaction
{
    [RequireComponent(typeof(Collider))]
    public class TravelPoint : MonoBehaviour
    {
        [SerializeField] private Transform _targetTransform;
        [SerializeField] private float _dwellSeconds = 1.5f;
        [SerializeField] private DwellRingView _ring;

        private DwellProgress _dwell;
        private NavMeshAgent _playerInZone;

        public Transform TargetTransform
        {
            get => _targetTransform;
            set => _targetTransform = value;
        }

        private void OnEnable()
        {
            _dwell = new DwellProgress(_dwellSeconds);
        }

        private void OnTriggerStay(Collider other)
        {
            if (_playerInZone == null && other.TryGetComponent(out NavMeshAgent agent))
            {
                _playerInZone = agent;
                _dwell.Begin();
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (_playerInZone != null && other.gameObject == _playerInZone.gameObject)
            {
                _playerInZone = null;
                _dwell.End();
                if (_ring != null)
                {
                    _ring.Render(0f);
                }
            }
        }

        private void Update()
        {
            if (_playerInZone == null)
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
                _dwell.End();
                if (_ring != null)
                {
                    _ring.Render(0f);
                }
            }
        }

        private void TeleportPlayer()
        {
            if (_targetTransform == null || _playerInZone == null) return;
            
            _playerInZone.Warp(_targetTransform.position);
            
            _playerInZone = null; 
        }
    }
}
