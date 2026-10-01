using UnityEngine;
using UnityEngine.AI;

namespace AutoService.Presentation.Staff
{
    /// <summary>
    /// Body of a hired NPC (prefab): a NavMesh agent that walks to a target transform, turns to face it and reports the
    /// arrival once; plus the body renderer (colored by role) and the box in its hands.
    /// </summary>
    /// <remarks>
    /// No <c>Update</c>: <see cref="StaffAgents"/> ticks <see cref="TickArrival"/> from the game loop. Same arrival rule as
    /// the player's motor: the path is complete and the remaining distance is within the stopping distance + tolerance.
    /// </remarks>
    public sealed class StaffView : MonoBehaviour
    {
        // Why: targets sit on the floor next to walls; the closest NavMesh point may be a little off.
        private const float NavMeshSampleRadius = 1.5f;
        private const float FacingToleranceDegrees = 1f;

        [SerializeField]
        private NavMeshAgent _agent;

        [SerializeField]
        [Tooltip("Body renderer, recolored by role.")]
        private Renderer _body;

        [SerializeField]
        [Tooltip("Where a carried box is held.")]
        private Transform _carrySocket;

        [SerializeField]
        [Tooltip("Renderer of the carried box (under the socket, without collider).")]
        private Renderer _boxRenderer;

        [SerializeField, Min(0f)]
        [Tooltip("Extra distance on top of the agent's Stopping Distance at which the target counts as reached (m).")]
        private float _arrivalTolerance = 0.15f;

        [SerializeField, Min(1f)]
        [Tooltip("Turn speed towards the target after arriving (degrees per second).")]
        private float _turnSpeed = 540f;

        private Transform _target;
        private Phase _phase;

        private enum Phase
        {
            Idle,
            Walking,
            Turning,
        }

        /// <summary>Renderer of the carried box (may be null).</summary>
        public Renderer BoxRenderer => _boxRenderer;

        /// <summary>True while walking or turning towards a target.</summary>
        public bool IsMoving => _phase != Phase.Idle;

        /// <summary>Puts the NPC at <paramref name="position"/> on the NavMesh, facing <paramref name="rotation"/>.</summary>
        public void Place(Vector3 position, Quaternion rotation)
        {
            transform.rotation = YawOnly(rotation, transform.rotation);
            if (_agent != null && NavMesh.SamplePosition(position, out NavMeshHit hit, NavMeshSampleRadius, _agent.areaMask))
            {
                _agent.Warp(hit.position);
            }
            else
            {
                transform.position = position;
            }
        }

        /// <summary>Sets the body material (null keeps the prefab's).</summary>
        public void SetBodyMaterial(Material material)
        {
            if (_body != null && material != null)
            {
                _body.sharedMaterial = material;
            }
        }

        /// <summary>Walks to <paramref name="target"/> and faces its forward on arrival.</summary>
        /// <returns>False when the target is not reachable on the NavMesh (the NPC is then snapped onto it).</returns>
        public bool WalkTo(Transform target)
        {
            _target = target;
            if (_agent != null
                && _agent.isActiveAndEnabled
                && _agent.isOnNavMesh
                && NavMesh.SamplePosition(target.position, out NavMeshHit hit, NavMeshSampleRadius, _agent.areaMask)
                && _agent.SetDestination(hit.position))
            {
                _agent.updateRotation = true;
                _phase = Phase.Walking;
                return true;
            }

            Place(target.position, target.rotation);
            _phase = Phase.Idle;
            return false;
        }

        /// <summary>Advances arrival and turning.</summary>
        /// <param name="deltaTime">Scaled seconds (0 while paused).</param>
        /// <returns>True exactly once per <see cref="WalkTo"/>: when the NPC has arrived and faces the target.</returns>
        public bool TickArrival(float deltaTime)
        {
            switch (_phase)
            {
                case Phase.Walking:
                    if (_agent.pathPending || _agent.remainingDistance > _agent.stoppingDistance + _arrivalTolerance)
                    {
                        return false;
                    }

                    // Why: a partial path (target inside an obstacle) still ends at the closest reachable point — stop there.
                    _agent.ResetPath();
                    _agent.updateRotation = false;
                    _phase = Phase.Turning;
                    return false;
                case Phase.Turning:
                    Quaternion goal = YawOnly(_target != null ? _target.rotation : transform.rotation, transform.rotation);
                    Quaternion rotation = Quaternion.RotateTowards(transform.rotation, goal, _turnSpeed * deltaTime);
                    if (Quaternion.Angle(rotation, goal) > FacingToleranceDegrees)
                    {
                        transform.rotation = rotation;
                        return false;
                    }

                    transform.rotation = goal;
                    _phase = Phase.Idle;
                    return true;
                default:
                    return false;
            }
        }

        // Why: target points may be tilted in the scene; the NPC only ever rotates around the vertical axis.
        private static Quaternion YawOnly(Quaternion rotation, Quaternion fallback)
        {
            Vector3 forward = rotation * Vector3.forward;
            forward.y = 0f;
            return forward.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(forward) : fallback;
        }
    }
}
