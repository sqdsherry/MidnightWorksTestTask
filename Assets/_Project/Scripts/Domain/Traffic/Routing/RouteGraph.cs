using System;
using System.Collections.Generic;

namespace AutoService.Domain.Traffic.Routing
{
    /// <summary>
    /// Directed graph of road nodes with precomputed shortest routes (fewest hops).
    /// Nodes are plain indices 0..NodeCount-1; the scene assigns them (Presentation knows the positions, this class does not).
    /// </summary>
    /// <remarks>
    /// <para>Usage: <see cref="AddEdge"/> for every one-way connection, then <see cref="Build"/> once. Build runs a BFS from
    /// every node and stores a next-hop table (NodeCount × NodeCount ints), so queries are O(path length) and allocation-free.</para>
    /// <para><b>Ties</b> between equally short routes are broken by edge insertion order: edges added earlier win.
    /// Layouts rely on it to prefer the through lane over a branch of the same length (e.g. driving past a bay, not through it).</para>
    /// </remarks>
    public sealed class RouteGraph
    {
        private const int NoNode = -1;

        private readonly List<int>[] _edges;
        private int[] _nextHop;

        /// <summary>Creates a graph without edges.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown for a negative node count.</exception>
        public RouteGraph(int nodeCount)
        {
            if (nodeCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(nodeCount), nodeCount, "Node count must be non-negative.");
            }

            _edges = new List<int>[nodeCount];
            for (int i = 0; i < nodeCount; i++)
            {
                _edges[i] = new List<int>();
            }
        }

        /// <summary>Number of nodes.</summary>
        public int NodeCount => _edges.Length;

        /// <summary>True once <see cref="Build"/> has run after the last <see cref="AddEdge"/>.</summary>
        public bool IsBuilt => _nextHop != null;

        /// <summary>Adds a one-way connection <paramref name="from"/> → <paramref name="to"/>. Duplicates are ignored.</summary>
        /// <remarks>Invalidates a previous <see cref="Build"/>.</remarks>
        /// <exception cref="ArgumentOutOfRangeException">Thrown for an index outside 0..NodeCount-1.</exception>
        /// <exception cref="ArgumentException">Thrown for a self-loop.</exception>
        public void AddEdge(int from, int to)
        {
            RequireNode(from, nameof(from));
            RequireNode(to, nameof(to));
            if (from == to)
            {
                throw new ArgumentException("A node cannot connect to itself (" + from + ").", nameof(to));
            }

            List<int> edges = _edges[from];
            if (!edges.Contains(to))
            {
                edges.Add(to);
                _nextHop = null;
            }
        }

        /// <summary>Computes the next-hop table. Call once after all edges are added (startup only: it allocates).</summary>
        public void Build()
        {
            int count = _edges.Length;
            var nextHop = new int[count * count];
            var firstHop = new int[count];
            var frontier = new Queue<int>(count);

            for (int source = 0; source < count; source++)
            {
                for (int i = 0; i < count; i++)
                {
                    firstHop[i] = NoNode;
                }

                // Why: the first hop of every node reached from the source is inherited along the BFS tree,
                // so one pass per source fills its whole row of the table.
                firstHop[source] = source;
                frontier.Enqueue(source);
                while (frontier.Count > 0)
                {
                    int node = frontier.Dequeue();
                    List<int> edges = _edges[node];
                    for (int e = 0; e < edges.Count; e++)
                    {
                        int neighbour = edges[e];
                        if (firstHop[neighbour] != NoNode)
                        {
                            continue;
                        }

                        firstHop[neighbour] = node == source ? neighbour : firstHop[node];
                        frontier.Enqueue(neighbour);
                    }
                }

                Array.Copy(firstHop, 0, nextHop, source * count, count);
            }

            _nextHop = nextHop;
        }

        /// <summary>True when <paramref name="to"/> can be reached from <paramref name="from"/> (always true for the same node).</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown for an index outside 0..NodeCount-1.</exception>
        /// <exception cref="InvalidOperationException">Thrown before <see cref="Build"/>.</exception>
        public bool HasPath(int from, int to)
        {
            RequireBuilt();
            RequireNode(from, nameof(from));
            RequireNode(to, nameof(to));
            return _nextHop[from * _edges.Length + to] != NoNode;
        }

        /// <summary>
        /// Fills <paramref name="pathBuffer"/> with the nodes AFTER <paramref name="from"/> up to and including
        /// <paramref name="to"/>. Allocation-free as long as the buffer's capacity suffices.
        /// </summary>
        /// <returns>False (empty buffer) when there is no route; true with an empty buffer when <c>from == to</c>.</returns>
        /// <exception cref="ArgumentNullException">Thrown for a null buffer.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown for an index outside 0..NodeCount-1.</exception>
        /// <exception cref="InvalidOperationException">Thrown before <see cref="Build"/>.</exception>
        public bool TryGetPath(int from, int to, List<int> pathBuffer)
        {
            if (pathBuffer == null)
            {
                throw new ArgumentNullException(nameof(pathBuffer));
            }

            pathBuffer.Clear();
            if (!HasPath(from, to))
            {
                return false;
            }

            int count = _edges.Length;
            int node = from;

            // Why: every hop is one step closer on a shortest route, so the walk ends within NodeCount steps;
            // the bound only protects against a corrupted table.
            for (int guard = 0; node != to && guard < count; guard++)
            {
                node = _nextHop[node * count + to];
                pathBuffer.Add(node);
            }

            return node == to;
        }

        private void RequireNode(int node, string parameterName)
        {
            if (node < 0 || node >= _edges.Length)
            {
                throw new ArgumentOutOfRangeException(parameterName, node, "Node index is out of range (0.." + (_edges.Length - 1) + ").");
            }
        }

        private void RequireBuilt()
        {
            if (_nextHop == null)
            {
                throw new InvalidOperationException("Route graph is not built: call Build() after adding edges.");
            }
        }
    }
}
