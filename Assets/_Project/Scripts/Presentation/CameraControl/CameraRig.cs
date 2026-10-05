using AutoService.Presentation.Controls;
using AutoService.Presentation.Player;
using UnityEngine;
using UnityEngine.EventSystems;

namespace AutoService.Presentation.CameraControl
{
    /// <summary>
    /// Isometric camera: a pivot on the ground that follows the character, with a child camera orbiting it at a fixed
    /// pitch/yaw. Supports manual pan (WASD/arrows, screen edge), mouse-wheel zoom and XZ bounds.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Manual pan switches follow off; <c>Space</c> or any new character command switches it back on.</item>
    /// <item>Runs on unscaled time, so the camera stays controllable while the game is paused.</item>
    /// </list>
    /// </remarks>
    public sealed class CameraRig : MonoBehaviour
    {
        private const float InputDeadZone = 0.01f;

        [SerializeField]
        [Tooltip("The camera, a child of this rig. Positioned and rotated by the rig every frame.")]
        private Transform _cameraTransform;

        [Header("Angle")]
        [SerializeField]
        [Range(10f, 89f)]
        [Tooltip("Downward tilt of the camera (degrees).")]
        private float _pitch = 50f;

        [SerializeField]
        [Tooltip("Rotation around the vertical axis (degrees).")]
        private float _yaw = 45f;

        [Header("Zoom")]
        [SerializeField]
        [Tooltip("Starting distance from the pivot (m).")]
        private float _distance = 18f;

        [SerializeField]
        [Tooltip("Min/max distance from the pivot (m).")]
        private Vector2 _distanceRange = new Vector2(10f, 28f);

        [SerializeField]
        [Min(0.1f)]
        [Tooltip("Distance change per wheel notch (m).")]
        private float _zoomStep = 2f;

        [SerializeField]
        [Min(0.1f)]
        [Tooltip("How fast the distance reaches its target (1/s).")]
        private float _zoomSmoothing = 10f;

        [Header("Follow & Pan")]
        [SerializeField]
        [Min(0.1f)]
        [Tooltip("How fast the pivot catches up with the character (1/s).")]
        private float _followSmoothing = 8f;

        [SerializeField]
        [Min(0f)]
        [Tooltip("Manual pan speed (m/s).")]
        private float _panSpeed = 15f;

        [SerializeField]
        [Tooltip("Pan when the cursor is near the screen edge.")]
        private bool _edgePanEnabled = true;

        [SerializeField]
        [Min(1f)]
        [Tooltip("Width of the edge-pan zone (px).")]
        private float _edgePanMargin = 12f;

        [Header("Bounds (pivot, world XZ)")]
        [SerializeField]
        private Vector2 _boundsMin = new Vector2(-25f, -25f);

        [SerializeField]
        private Vector2 _boundsMax = new Vector2(25f, 25f);

        private GameplayInput _input;
        private PlayerView _player;
        private Transform _followTarget;
        private Vector3 _pivot;
        private float _targetDistance;
        private float _currentDistance;
        private bool _isFollowing = true;

        /// <summary>True while the pivot follows the character (false after manual pan).</summary>
        public bool IsFollowing => _isFollowing;

        /// <summary>Injects dependencies and snaps the camera to the character.</summary>
        /// <param name="input">Gameplay input (pan, zoom, pointer, recenter).</param>
        /// <param name="player">The character to follow; its new commands re-enable follow.</param>
        public void Construct(GameplayInput input, PlayerView player)
        {
            Unsubscribe();

            _input = input;
            _player = player;
            _followTarget = player != null ? player.transform : null;

            if (_input != null)
            {
                _input.RecenterPressed += Recenter;
            }

            if (_player != null)
            {
                _player.CommandIssued += Recenter;
            }

            _isFollowing = true;
            if (_followTarget != null)
            {
                _pivot = ClampToBounds(FollowPoint());
            }

            ApplyTransform();
        }

        /// <summary>Resumes following the character (the pivot glides back to it).</summary>
        public void Recenter() => _isFollowing = true;

        /// <summary>Sets the XZ area the pivot is kept in (e.g. when switching locations).</summary>
        /// <param name="min">Minimum world X (x) and Z (y).</param>
        /// <param name="max">Maximum world X (x) and Z (y).</param>
        public void SetBounds(Vector2 min, Vector2 max)
        {
            _boundsMin = Vector2.Min(min, max);
            _boundsMax = Vector2.Max(min, max);
            _pivot = ClampToBounds(_pivot);
            ApplyTransform();
        }

        /// <summary>Instantly moves the pivot to the specified world position and optionally updates bounds.</summary>
        public void SnapTo(Vector3 worldPosition, Vector2? newBoundsMin = null, Vector2? newBoundsMax = null)
        {
            if (newBoundsMin.HasValue && newBoundsMax.HasValue)
            {
                _boundsMin = Vector2.Min(newBoundsMin.Value, newBoundsMax.Value);
                _boundsMax = Vector2.Max(newBoundsMin.Value, newBoundsMax.Value);
            }
            
            _pivot = new Vector3(worldPosition.x, _pivot.y, worldPosition.z);
            _pivot = ClampToBounds(_pivot);
            ApplyTransform();
            
            // Re-enable follow mode seamlessly if they were moving
            _isFollowing = true;
        }

        private void Awake()
        {
            _pivot = transform.position;
            _targetDistance = Mathf.Clamp(_distance, _distanceRange.x, _distanceRange.y);
            _currentDistance = _targetDistance;
        }

        // Why: the only Update-family method besides GameLoop. LateUpdate runs after the NavMeshAgent has moved
        // the character this frame; following it from a regular tick would lag a frame behind and jitter.
        private void LateUpdate()
        {
            if (_input == null || _cameraTransform == null)
            {
                return;
            }

            float deltaTime = Time.unscaledDeltaTime;

            Vector2 pan = _input.Pan + EdgePanDirection();
            if (pan.sqrMagnitude > InputDeadZone)
            {
                _isFollowing = false;
                pan = Vector2.ClampMagnitude(pan, 1f);
                Quaternion yaw = Quaternion.Euler(0f, _yaw, 0f);
                Vector3 move = yaw * new Vector3(pan.x, 0f, pan.y);
                _pivot += move * (_panSpeed * deltaTime);
            }
            else if (_isFollowing && _followTarget != null)
            {
                _pivot = Vector3.Lerp(_pivot, FollowPoint(), Damping(_followSmoothing, deltaTime));
            }

            float zoom = _input.Zoom;
            if (Mathf.Abs(zoom) > InputDeadZone && !IsPointerOverUi())
            {
                // Why: only the sign is used — the wheel's magnitude per notch differs between platforms and settings.
                _targetDistance = Mathf.Clamp(
                    _targetDistance - Mathf.Sign(zoom) * _zoomStep, _distanceRange.x, _distanceRange.y);
            }

            _currentDistance = Mathf.Lerp(_currentDistance, _targetDistance, Damping(_zoomSmoothing, deltaTime));
            _pivot = ClampToBounds(_pivot);
            ApplyTransform();
        }

        private void OnDestroy() => Unsubscribe();

        private void Unsubscribe()
        {
            if (_input != null)
            {
                _input.RecenterPressed -= Recenter;
            }

            if (_player != null)
            {
                _player.CommandIssued -= Recenter;
            }
        }

        private Vector2 EdgePanDirection()
        {
            if (!_edgePanEnabled || !Application.isFocused)
            {
                return Vector2.zero;
            }

            Vector2 pointer = _input.PointerPosition;
            float width = Screen.width;
            float height = Screen.height;

            // Why: outside the window (e.g. over the Editor's Inspector) the pointer must not keep the camera sliding.
            if (pointer.x < 0f || pointer.y < 0f || pointer.x > width || pointer.y > height)
            {
                return Vector2.zero;
            }

            Vector2 direction = Vector2.zero;
            if (pointer.x <= _edgePanMargin)
            {
                direction.x = -1f;
            }
            else if (pointer.x >= width - _edgePanMargin)
            {
                direction.x = 1f;
            }

            if (pointer.y <= _edgePanMargin)
            {
                direction.y = -1f;
            }
            else if (pointer.y >= height - _edgePanMargin)
            {
                direction.y = 1f;
            }

            return direction;
        }

        // Why: the wheel over a scrollable panel must scroll the panel, not zoom the camera behind it.
        // Explicit null check instead of `?.`: the null-conditional operator bypasses Unity's destroyed-object check.
        private static bool IsPointerOverUi()
        {
            EventSystem eventSystem = EventSystem.current;
            return eventSystem != null && eventSystem.IsPointerOverGameObject();
        }

        // Why: the pivot stays on its own ground height; only the character's XZ is followed.
        private Vector3 FollowPoint()
        {
            Vector3 target = _followTarget.position;
            return new Vector3(target.x, _pivot.y, target.z);
        }

        private Vector3 ClampToBounds(Vector3 point)
        {
            point.x = Mathf.Clamp(point.x, _boundsMin.x, _boundsMax.x);
            point.z = Mathf.Clamp(point.z, _boundsMin.y, _boundsMax.y);
            return point;
        }

        private void ApplyTransform()
        {
            transform.position = _pivot;
            if (_cameraTransform == null)
            {
                return;
            }

            Quaternion rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            _cameraTransform.SetPositionAndRotation(_pivot + rotation * (Vector3.back * _currentDistance), rotation);
        }

        // Why: frame-rate independent exponential smoothing — the same feel at 30 and 144 FPS, unlike a fixed lerp factor.
        private static float Damping(float sharpness, float deltaTime) => 1f - Mathf.Exp(-sharpness * deltaTime);

        private void OnDrawGizmosSelected()
        {
            float y = transform.position.y;
            var a = new Vector3(_boundsMin.x, y, _boundsMin.y);
            var b = new Vector3(_boundsMax.x, y, _boundsMin.y);
            var c = new Vector3(_boundsMax.x, y, _boundsMax.y);
            var d = new Vector3(_boundsMin.x, y, _boundsMax.y);

            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(a, b);
            Gizmos.DrawLine(b, c);
            Gizmos.DrawLine(c, d);
            Gizmos.DrawLine(d, a);
        }
    }
}
