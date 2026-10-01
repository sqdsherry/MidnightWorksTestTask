using UnityEngine;
using UnityEngine.AI;

namespace AutoService.Presentation.Traffic
{
    /// <summary>
    /// A pooled car visual driven by a <see cref="NavMeshAgent"/>. Drives to a target transform, then turns in place
    /// to the target's heading and reports the arrival once.
    /// </summary>
    /// <remarks>
    /// Has no <c>Update</c>: <see cref="CarAgents"/> calls <see cref="TickArrival"/> from the game loop.
    /// When the agent cannot path to the target (no NavMesh, partial path) the car is snapped to the target with a warning,
    /// so a layout mistake shows up in the Console instead of freezing the whole car flow.
    /// </remarks>
    public sealed class CarView : MonoBehaviour
    {
        private const float FacingToleranceDegrees = 1f;

        [SerializeField]
        [Tooltip("Agent that moves the car (Agent Type: Car).")]
        private NavMeshAgent _agent;

        [SerializeField, Min(1f)]
        [Tooltip("Turn speed when aligning with the target after arrival (degrees per second).")]
        private float _alignSpeed = 360f;

        [SerializeField, Min(0f)]
        [Tooltip("Extra distance on top of the agent's Stopping Distance at which the target counts as reached (m).")]
        private float _arrivalTolerance = 0.3f;

        private Transform _target;
        private bool _aligning;
        private bool _snapToTarget;

        /// <summary>True while the car has a target it has not reported reaching yet.</summary>
        public bool IsDriving => _target != null;

        private bool AgentUsable => _agent != null && _agent.isActiveAndEnabled && _agent.isOnNavMesh;

        /// <summary>Activates the car (if pooled) and teleports it to the pose, with no target.</summary>
        public void Place(Vector3 position, Quaternion rotation)
        {
            // Why: positioned before activation so the agent snaps onto the NavMesh at the spawn point, not at the pool root.
            transform.SetPositionAndRotation(position, rotation);
            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }

            if (_agent != null && _agent.isActiveAndEnabled)
            {
                _agent.Warp(position);
                transform.rotation = rotation;
            }

            _target = null;
            _aligning = false;
            _snapToTarget = false;
        }

        /// <summary>Starts driving to <paramref name="target"/>, replacing any previous target.</summary>
        public void Drive(Transform target)
        {
            _target = target;
            _aligning = false;
            _snapToTarget = false;
            if (target == null)
            {
                return;
            }

            if (!AgentUsable)
            {
                Debug.LogWarning("[CarView] '" + name + "' is not on a NavMesh; it will be snapped to its target.", this);
                _snapToTarget = true;
                return;
            }

            _agent.updateRotation = true;
            _agent.isStopped = false;

            // Why: after a failed SetDestination the agent still reports the previous path's status and distance,
            // which could fake an instant arrival; snapping is explicit instead.
            if (!_agent.SetDestination(target.position))
            {
                Debug.LogWarning("[CarView] SetDestination failed for '" + name + "'; it will be snapped to its target.", this);
                _snapToTarget = true;
            }
        }

        /// <summary>Advances arrival detection and the final turn.</summary>
        /// <param name="deltaTime">Scaled frame time in seconds.</param>
        /// <returns>True exactly once per <see cref="Drive"/>: when the car is at the target and aligned with it.</returns>
        public bool TickArrival(float deltaTime)
        {
            if (_target == null)
            {
                return false;
            }

            if (!_aligning && !TryReachTarget())
            {
                return false;
            }

            Quaternion goal = YawOnly(_target.rotation, transform.rotation);
            Quaternion rotation = Quaternion.RotateTowards(transform.rotation, goal, _alignSpeed * deltaTime);
            if (Quaternion.Angle(rotation, goal) > FacingToleranceDegrees)
            {
                transform.rotation = rotation;
                return false;
            }

            transform.rotation = goal;
            _target = null;
            _aligning = false;
            return true;
        }

        /// <summary>Drops the current target and stops the agent; no arrival will be reported for it.</summary>
        public void Halt()
        {
            _target = null;
            _aligning = false;
            _snapToTarget = false;
            StopAgent();
        }

        /// <summary>Clears all runtime state before the car goes back to the pool.</summary>
        public void ResetForPool() => Halt();

        /// <returns>True once the car stands at the target (alignment phase begins).</returns>
        private bool TryReachTarget()
        {
            if (_snapToTarget || !AgentUsable)
            {
                SnapTo(_target.position);
            }
            else
            {
                if (_agent.pathPending)
                {
                    return false;
                }

                if (_agent.pathStatus != NavMeshPathStatus.PathComplete)
                {
                    // Why: a partial path would stop the car short of the target forever and stall the queue behind it.
                    Debug.LogWarning("[CarView] No complete path for '" + name + "' (" + _agent.pathStatus + "); snapping it to its target.", this);
                    SnapTo(_target.position);
                }
                else if (_agent.remainingDistance > _agent.stoppingDistance + _arrivalTolerance)
                {
                    return false;
                }
                else
                {
                    StopAgent();
                }
            }

            _aligning = true;

            // Why: during the final turn the view owns the rotation; the agent would otherwise fight it.
            if (_agent != null)
            {
                _agent.updateRotation = false;
            }

            return true;
        }

        private void SnapTo(Vector3 position)
        {
            if (AgentUsable)
            {
                _agent.Warp(position);
                return;
            }

            transform.position = position;
        }

        private void StopAgent()
        {
            if (AgentUsable)
            {
                _agent.ResetPath();
            }
        }

        // Why: target transforms may be tilted in the scene; a car only ever rotates around the vertical axis.
        private static Quaternion YawOnly(Quaternion rotation, Quaternion fallback)
        {
            Vector3 forward = rotation * Vector3.forward;
            forward.y = 0f;
            return forward.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(forward) : fallback;
        }
    }
}
