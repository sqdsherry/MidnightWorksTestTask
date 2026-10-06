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

        public NavMeshAgent Agent => _agent;

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

        public void SetBodyMaterial(Material material)
        {
            if (_body != null && material != null)
            {
                _body.sharedMaterial = material;
            }
        }

        public void SetRole(AutoService.Domain.Staff.StaffRole role)
        {
            Transform vis = transform.Find("Visual");
            if (vis != null)
            {
                Transform worker = vis.Find("Worker");
                Transform storekeeper = vis.Find("Storekeeper");
                if (worker != null && storekeeper != null)
                {
                    worker.gameObject.SetActive(role == AutoService.Domain.Staff.StaffRole.PointWorker);
                    storekeeper.gameObject.SetActive(role == AutoService.Domain.Staff.StaffRole.Storekeeper);
                    Renderer r = role == AutoService.Domain.Staff.StaffRole.PointWorker 
                        ? worker.GetComponentInChildren<Renderer>() 
                        : storekeeper.GetComponentInChildren<Renderer>();
                    if (r != null)
                    {
                        _body = r;
                    }
                    return;
                }
            }

            // Фоллбэк: если используется базовый префаб с Body/Head
            Transform fallbackBody = transform.Find("Body");
            if (fallbackBody != null)
            {
                fallbackBody.gameObject.SetActive(true);
                if (_body == null)
                {
                    _body = fallbackBody.GetComponent<Renderer>();
                }
            }
            Transform fallbackHead = transform.Find("Head");
            if (fallbackHead != null)
            {
                fallbackHead.gameObject.SetActive(true);
            }
        }

        /// <summary>Walks to <paramref name="target"/> and faces its forward on arrival.</summary>
        /// <returns>False when the target is not reachable on the NavMesh (the NPC is then snapped onto it).</returns>
        public bool WalkTo(Transform target)
        {
            _target = target;
            if (_agent != null && _agent.isActiveAndEnabled)
            {
                if (!_agent.isOnNavMesh)
                {
                    if (NavMesh.SamplePosition(transform.position, out NavMeshHit curHit, 3.0f, _agent.areaMask))
                    {
                        _agent.Warp(curHit.position);
                    }
                }

                if (_agent.isOnNavMesh
                    && NavMesh.SamplePosition(target.position, out NavMeshHit hit, 3.0f, _agent.areaMask))
                {
                    if (_agent.SetDestination(hit.position))
                    {
                        _agent.updateRotation = true;
                        _phase = Phase.Walking;
                        return true;
                    }
                }
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
                    if (!_agent.isOnNavMesh)
                    {
                        SnapToTarget("is not on the NavMesh");
                        return false;
                    }

                    // Если путь еще рассчитывается в фоне — ждем завершения расчета
                    if (_agent.pathPending)
                    {
                        return false;
                    }

                    // Если путь вообще недостижим (нет проходимой сетки)
                    if (_agent.pathStatus == NavMeshPathStatus.PathInvalid)
                    {
                        SnapToTarget("has invalid path");
                        return false;
                    }

                    // Если у агента еще нет пути — ждем инициализации NavMesh
                    if (!_agent.hasPath)
                    {
                        return false;
                    }

                    // Проверяем реальное приближение к цели
                    bool closeToTarget = _target != null && Vector3.Distance(transform.position, _target.position) <= 1.8f;
                    bool reachedPathEnd = _agent.remainingDistance <= Mathf.Max(_agent.stoppingDistance + _arrivalTolerance, 0.5f);

                    if (closeToTarget)
                    {
                        StartTurning();
                        return false;
                    }

                    if (reachedPathEnd)
                    {
                        // Дошел до конца доступного пути NavMesh.
                        // Если остался небольшой разрыв до объекта из-за obstacle, дотягиваем до точки
                        if (_target != null && Vector3.Distance(transform.position, _target.position) > 2.0f)
                        {
                            SnapToTarget("path ended before target; snapping to work spot");
                        }
                        else
                        {
                            StartTurning();
                        }
                        return false;
                    }

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

        private void StartTurning()
        {
            if (_agent.isOnNavMesh)
            {
                _agent.ResetPath();
            }

            _agent.updateRotation = false;
            _phase = Phase.Turning;
        }

        private void SnapToTarget(string reason)
        {
            Debug.LogWarning("[StaffView] '" + name + "' " + reason + " to '" + (_target != null ? _target.name : "nothing")
                + "'; teleported there.", this);
            if (_target != null)
            {
                if (_agent.isOnNavMesh)
                {
                    _agent.ResetPath();
                }

                Place(_target.position, _target.rotation);
            }

            StartTurning();
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
