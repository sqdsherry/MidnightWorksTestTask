using UnityEngine;

namespace AutoService.Presentation.Traffic.Routing
{
    /// <summary>
    /// A node of the location's one-way road graph: queue and parking slots, point car spots, lane corners, spawn and exit.
    /// Its forward (blue Z axis) is the driving direction; a car that ends its route here aligns with it.
    /// </summary>
    /// <remarks>
    /// Cars drive node to node (NavMesh only between neighbours), so they keep to their lanes instead of cutting across
    /// the lot. On an intermediate node a car does not stop: it switches to the next node once it is within
    /// <see cref="PassRadius"/>. Nodes are pure markup; <see cref="LocationLayout"/> indexes them into a route graph.
    /// </remarks>
    public sealed class RoadNode : MonoBehaviour
    {
        /// <summary>Value of <see cref="Index"/> before the layout has built its graph.</summary>
        public const int NoIndex = -1;

        private const float GizmoRadius = 0.35f;
        private const float ArrowHeadLength = 0.6f;
        private const float ArrowHeadWidth = 0.35f;

        [SerializeField]
        [Tooltip("One-way connections: nodes a car may drive to from here. On equally short routes earlier entries win, " +
                 "so list the through lane before a branch.")]
        private RoadNode[] _next = new RoadNode[0];

        [SerializeField]
        [Tooltip("Merge zone this node belongs to (one car at a time), or empty.")]
        private TrafficZone _zone;

        [SerializeField, Min(0.1f)]
        [Tooltip("Distance at which a car passing through switches to the next node instead of stopping (m).")]
        private float _passRadius = 1.5f;

        /// <summary>Index in the location's route graph (set by <see cref="LocationLayout"/>), or <see cref="NoIndex"/>.</summary>
        public int Index { get; private set; } = NoIndex;

        /// <summary>Directed connections (elements may be null if left empty in the Inspector).</summary>
        public RoadNode[] Next => _next;

        /// <summary>Merge zone of the node, or null.</summary>
        public TrafficZone Zone => _zone;

        /// <summary>Distance at which a passing car switches to the next node (m).</summary>
        public float PassRadius => _passRadius;

        /// <summary>Called by <see cref="LocationLayout"/> while building the graph.</summary>
        internal void SetIndex(int index)
        {
            Index = index;
        }

        private void OnDrawGizmos()
        {
            Vector3 position = transform.position;
            Gizmos.color = _zone != null ? _zone.GizmoColor : Color.white;
            Gizmos.DrawSphere(position, GizmoRadius);

            Gizmos.color = Color.cyan;
            for (int i = 0; i < _next.Length; i++)
            {
                if (_next[i] != null)
                {
                    DrawArrow(position, _next[i].transform.position);
                }
            }

#if UNITY_EDITOR
            UnityEditor.Handles.Label(position + Vector3.up * 0.8f, name);
#endif
        }

        private static void DrawArrow(Vector3 from, Vector3 to)
        {
            Vector3 direction = to - from;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f)
            {
                return;
            }

            Gizmos.DrawLine(from, to);
            Vector3 forward = direction.normalized;
            Vector3 right = new Vector3(forward.z, 0f, -forward.x);

            // Why: the head sits at the middle of the edge, so arrows of two opposite edges never overlap at a node.
            Vector3 tip = Vector3.Lerp(from, to, 0.5f);
            Gizmos.DrawLine(tip, tip - forward * ArrowHeadLength + right * ArrowHeadWidth);
            Gizmos.DrawLine(tip, tip - forward * ArrowHeadLength - right * ArrowHeadWidth);
        }
    }
}
