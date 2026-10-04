using AutoService.Domain.Common;
using UnityEngine;

namespace AutoService.Presentation.Interaction
{
    [RequireComponent(typeof(Collider))]
    public class TravelPoint : MonoBehaviour
    {
        [SerializeField] private Transform _targetTransform;
        [SerializeField] private float _dwellSeconds = 1.5f;
        [SerializeField] private DwellRingView _ring;

        private DwellProgress _dwell;
        private CharacterController _playerInZone;

        public Transform TargetTransform
        {
            get => _targetTransform;
            set => _targetTransform = value;
        }

        private void Awake()
        {
            _dwell = new DwellProgress(_dwellSeconds);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.TryGetComponent(out CharacterController cc))
            {
                _playerInZone = cc;
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
            
            _playerInZone.enabled = false;
            _playerInZone.transform.position = _targetTransform.position;
            _playerInZone.enabled = true;
            
            _playerInZone = null; 
        }
    }
}
