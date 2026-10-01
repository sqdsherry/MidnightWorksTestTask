using System.Collections.Generic;
using AutoService.Presentation.Traffic.Routing;
using UnityEngine;
using UnityEngine.AI;

namespace AutoService.Presentation.Traffic
{
    /// <summary>
    /// A pooled car visual driven by a <see cref="NavMeshAgent"/> along a route of <see cref="RoadNode"/>s: passes through
    /// intermediate nodes, stops on the last one, turns in place to its heading and reports the arrival once.
    /// </summary>
    /// <remarks>
    /// <para>Has no <c>Update</c>: <see cref="CarAgents"/> calls <see cref="TickArrival"/> from the game loop.</para>
    /// <para><b>Merge zones.</b> Before heading for a node whose <see cref="TrafficZone"/> it does not hold yet, the car asks
    /// the zone to let it in; if another car holds it, the car stops and asks again every tick. It releases the zone once it
    /// reaches a node outside of it, so the next car only starts when this one has cleared the merge.</para>
    /// <para>When the agent cannot path (no NavMesh, partial path) the car is snapped to the node with a warning, so a layout
    /// mistake shows up in the Console instead of freezing the whole car flow.</para>
    /// </remarks>
    public sealed class CarView : MonoBehaviour
    {
        private const float FacingToleranceDegrees = 1f;
        private const int InitialPathCapacity = 32;
        private const int NoCar = TrafficZone.NoCar;

        [SerializeField]
        [Tooltip("Agent that moves the car (Agent Type: Car).")]
        private NavMeshAgent _agent;

        [SerializeField, Min(1f)]
        [Tooltip("Turn speed when aligning with the target after arrival (degrees per second).")]
        private float _alignSpeed = 360f;

        [SerializeField, Min(0f)]
        [Tooltip("Extra distance on top of the agent's Stopping Distance at which the final node counts as reached (m).")]
        private float _arrivalTolerance = 0.3f;

        // Why: preallocated and reused for every route; a route never has more nodes than the graph, so it rarely grows.
        private readonly List<RoadNode> _path = new List<RoadNode>(InitialPathCapacity);

        private int _pathIndex;
        private int _carId = NoCar;
        private RoadNode _currentNode;
        private TrafficZone _heldZone;
        private bool _waitingForZone;
        private bool _aligning;
        private bool _snapToNode;

        /// <summary>True while the car has a route it has not reported finishing yet.</summary>
        public bool IsDriving => _pathIndex < _path.Count;

        /// <summary>Last node the car has reached (the start node right after <see cref="Place"/>); routes start from it.</summary>
        public RoadNode CurrentNode => _currentNode;

        private bool AgentUsable => _agent != null && _agent.isActiveAndEnabled && _agent.isOnNavMesh;

        /// <summary>Activates the car (if pooled) and teleports it onto <paramref name="startNode"/>, with no route.</summary>
        /// <param name="carId">Runtime id; the car enters merge zones under it.</param>
        /// <param name="startNode">Node the car appears on (the spawn); its forward is the initial heading.</param>
        public void Place(int carId, RoadNode startNode)
        {
            ReleaseZone();
            _carId = carId;
            ClearRoute();

            Transform start = startNode.transform;
            Vector3 position = start.position;
            Quaternion rotation = YawOnly(start.rotation, transform.rotation);

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

            _currentNode = startNode;
        }

        /// <summary>
        /// Starts driving along <paramref name="path"/> (nodes after <see cref="CurrentNode"/>, the last one is the target),
        /// replacing any previous route. The nodes are copied, so the caller may reuse its buffer.
        /// </summary>
        public void FollowPath(IReadOnlyList<RoadNode> path)
        {
            ClearRoute();
            for (int i = 0; i < path.Count; i++)
            {
                if (path[i] != null)
                {
                    _path.Add(path[i]);
                }
            }

            if (_path.Count == 0)
            {
                StopAgent();
                return;
            }

            if (!AgentUsable)
            {
                Debug.LogWarning("[CarView] '" + name + "' is not on a NavMesh; it will be snapped along its route.", this);
            }
            else
            {
                _agent.updateRotation = true;
            }

            StartLeg();
        }

        /// <summary>Advances the route, merge-zone waiting, arrival detection and the final turn.</summary>
        /// <param name="deltaTime">Scaled frame time in seconds.</param>
        /// <returns>True exactly once per <see cref="FollowPath"/>: when the car is on the last node and aligned with it.</returns>
        public bool TickArrival(float deltaTime)
        {
            if (!IsDriving)
            {
                return false;
            }

            RoadNode node = _path[_pathIndex];
            if (_waitingForZone)
            {
                if (TryEnterZone(node))
                {
                    _waitingForZone = false;
                    DriveTo(node);
                }

                return false;
            }

            if (!_aligning)
            {
                if (_pathIndex < _path.Count - 1)
                {
                    if (TryPass(node))
                    {
                        ReachNode(node);
                        _pathIndex++;
                        StartLeg();
                    }

                    return false;
                }

                if (!TryReachFinal(node))
                {
                    return false;
                }

                ReachNode(node);
                _aligning = true;

                // Why: during the final turn the view owns the rotation; the agent would otherwise fight it.
                if (_agent != null)
                {
                    _agent.updateRotation = false;
                }
            }

            Quaternion goal = YawOnly(node.transform.rotation, transform.rotation);
            Quaternion rotation = Quaternion.RotateTowards(transform.rotation, goal, _alignSpeed * deltaTime);
            if (Quaternion.Angle(rotation, goal) > FacingToleranceDegrees)
            {
                transform.rotation = rotation;
                return false;
            }

            transform.rotation = goal;
            ClearRoute();
            return true;
        }

        /// <summary>Teleports the car onto <paramref name="node"/> (position and heading) and drops the route; no arrival is reported.</summary>
        public void SnapTo(RoadNode node)
        {
            ClearRoute();
            StopAgent();
            SnapPosition(node.transform.position);
            transform.rotation = YawOnly(node.transform.rotation, transform.rotation);
            ReachNode(node);
        }

        /// <summary>Drops the current route and stops the agent; no arrival will be reported for it.</summary>
        public void Halt()
        {
            ClearRoute();
            StopAgent();

            // Why: a halted car will not move on by itself; holding a zone would block that merge for everybody forever.
            ReleaseZone();
        }

        /// <summary>Clears all runtime state (route, zone) before the car goes back to the pool.</summary>
        public void ResetForPool()
        {
            Halt();
            _currentNode = null;
            _carId = NoCar;
        }

        private void StartLeg()
        {
            RoadNode node = _path[_pathIndex];
            if (!TryEnterZone(node))
            {
                _waitingForZone = true;
                PauseAgent();
                return;
            }

            DriveTo(node);
        }

        private void DriveTo(RoadNode node)
        {
            _snapToNode = false;
            if (!AgentUsable)
            {
                _snapToNode = true;
                return;
            }

            _agent.isStopped = false;

            // Why: after a failed SetDestination the agent still reports the previous path's status and distance,
            // which could fake an instant arrival; snapping is explicit instead.
            if (!_agent.SetDestination(node.transform.position))
            {
                Debug.LogWarning("[CarView] SetDestination failed for '" + name + "'; it will be snapped to '" + node.name + "'.", this);
                _snapToNode = true;
            }
        }

        /// <returns>True once a passing car is close enough to switch to the next node.</returns>
        private bool TryPass(RoadNode node)
        {
            Vector3 target = node.transform.position;
            if (_snapToNode || !AgentUsable || IsPathBroken(node))
            {
                SnapPosition(target);
                return true;
            }

            Vector3 offset = target - transform.position;
            offset.y = 0f;
            float radius = node.PassRadius;
            return offset.sqrMagnitude <= radius * radius;
        }

        /// <returns>True once the car stands on the last node (the alignment phase begins).</returns>
        private bool TryReachFinal(RoadNode node)
        {
            if (_snapToNode || !AgentUsable || IsPathBroken(node))
            {
                SnapPosition(node.transform.position);
                return true;
            }

            if (_agent.pathPending || _agent.remainingDistance > _agent.stoppingDistance + _arrivalTolerance)
            {
                return false;
            }

            StopAgent();
            return true;
        }

        // Why: a partial path would stop the car short of the node forever and stall every car behind it.
        private bool IsPathBroken(RoadNode node)
        {
            if (_agent.pathPending || _agent.pathStatus == NavMeshPathStatus.PathComplete)
            {
                return false;
            }

            Debug.LogWarning("[CarView] No complete path for '" + name + "' to '" + node.name + "' (" + _agent.pathStatus + "); snapping it.", this);
            return true;
        }

        private void ReachNode(RoadNode node)
        {
            _currentNode = node;
            if (_heldZone != null && node.Zone != _heldZone)
            {
                ReleaseZone();
            }
        }

        /// <returns>True when the node is outside any zone or its zone now belongs to this car.</returns>
        private bool TryEnterZone(RoadNode node)
        {
            TrafficZone zone = node.Zone;
            if (zone == null || zone == _heldZone)
            {
                return true;
            }

            if (!zone.TryEnter(_carId))
            {
                return false;
            }

            // Why: one zone at a time — layouts keep a plain node between two zones, so this only triggers on a layout
            // where zones touch, and there the old one is already behind the car.
            ReleaseZone();
            _heldZone = zone;
            return true;
        }

        private void ReleaseZone()
        {
            if (_heldZone == null)
            {
                return;
            }

            _heldZone.Exit(_carId);
            _heldZone = null;
        }

        private void ClearRoute()
        {
            _path.Clear();
            _pathIndex = 0;
            _waitingForZone = false;
            _aligning = false;
            _snapToNode = false;
        }

        private void SnapPosition(Vector3 position)
        {
            if (AgentUsable)
            {
                _agent.Warp(position);
                return;
            }

            transform.position = position;
        }

        private void PauseAgent()
        {
            if (AgentUsable)
            {
                // Why: the car is already near the node before the zone; braking smoothly would roll it into the merge.
                _agent.isStopped = true;
                _agent.velocity = Vector3.zero;
            }
        }

        private void StopAgent()
        {
            if (AgentUsable)
            {
                _agent.ResetPath();
            }
        }

        // Why: nodes may be tilted in the scene; a car only ever rotates around the vertical axis.
        private static Quaternion YawOnly(Quaternion rotation, Quaternion fallback)
        {
            Vector3 forward = rotation * Vector3.forward;
            forward.y = 0f;
            return forward.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(forward) : fallback;
        }
    }
}
