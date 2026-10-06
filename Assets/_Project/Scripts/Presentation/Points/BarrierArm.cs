using UnityEngine;

namespace AutoService.Presentation.Points
{
    /// <summary>
    /// Visual of the parking barrier arm: rotates it between closed and open.
    /// Has no <c>Update</c>; <see cref="ServicePointPresenter"/> drives it through <see cref="Animate"/>.
    /// </summary>
    public sealed class BarrierArm : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Pivot of the arm (an empty parent at the post); rotated around Open Axis.")]
        private Transform _arm;

        [SerializeField]
        [Tooltip("Rotation of the open arm relative to its closed pose, in degrees.")]
        private float _openAngle = 80f;

        [SerializeField, Min(1f)]
        [Tooltip("Rotation speed in degrees per second (unscaled time).")]
        private float _speed = 240f;

        [SerializeField]
        [Tooltip("Local axis of the pivot the arm rotates around (Z lifts an arm that points along X).")]
        private Vector3 _openAxis = Vector3.forward;

        private Quaternion _closedRotation;
        private bool _hasClosedRotation;
        private bool _isOpen;
        private float _angle;

        /// <summary>Sets the target pose; the arm moves there over the next <see cref="Animate"/> calls.</summary>
        public void SetOpen(bool open)
        {
            _isOpen = open;
        }

        /// <summary>Moves the arm towards the target pose.</summary>
        /// <param name="unscaledDeltaTime">Unscaled frame time in seconds.</param>
        public void Animate(float unscaledDeltaTime)
        {
            if (_arm == null)
            {
                return;
            }

            if (!_hasClosedRotation)
            {
                // Why: captured lazily, so the closed pose is whatever was authored in the scene.
                _closedRotation = _arm.localRotation;
                _hasClosedRotation = true;
            }

            float target = _isOpen ? _openAngle : 0f;
            if (Mathf.Approximately(_angle, target))
            {
                return;
            }

            _angle = Mathf.MoveTowards(_angle, target, _speed * unscaledDeltaTime);
            _arm.localRotation = _closedRotation * Quaternion.AngleAxis(_angle, _openAxis);
        }
    }
}
