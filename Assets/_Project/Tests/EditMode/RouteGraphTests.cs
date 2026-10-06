using System;
using System.Collections.Generic;
using AutoService.Domain.Traffic.Routing;
using NUnit.Framework;

namespace AutoService.Tests.EditMode
{
    /// <summary>Tests for <see cref="RouteGraph"/>.</summary>
    public sealed class RouteGraphTests
    {
        private readonly List<int> _path = new List<int>();

        [Test]
        public void LinearChain_ReturnsNodesAfterStartUpToTarget()
        {
            RouteGraph graph = Chain(4);

            Assert.IsTrue(graph.TryGetPath(0, 3, _path));

            CollectionAssert.AreEqual(new[] { 1, 2, 3 }, _path);
        }

        [Test]
        public void Fork_TakesTheBranchLeadingToTheTarget()
        {
            // 0 → 1 → 2 (left), 0 → 3 → 4 (right)
            var graph = new RouteGraph(5);
            graph.AddEdge(0, 1);
            graph.AddEdge(1, 2);
            graph.AddEdge(0, 3);
            graph.AddEdge(3, 4);
            graph.Build();

            Assert.IsTrue(graph.TryGetPath(0, 4, _path));
            CollectionAssert.AreEqual(new[] { 3, 4 }, _path);

            Assert.IsTrue(graph.TryGetPath(0, 2, _path));
            CollectionAssert.AreEqual(new[] { 1, 2 }, _path);
        }

        [Test]
        public void ShorterRoute_WinsOverLongerOne()
        {
            // 0 → 1 → 2 → 3 and the shortcut 0 → 3
            RouteGraph graph = Chain(4, build: false);
            graph.AddEdge(0, 3);
            graph.Build();

            Assert.IsTrue(graph.TryGetPath(0, 3, _path));

            CollectionAssert.AreEqual(new[] { 3 }, _path);
        }

        [Test]
        public void EqualRoutes_PreferTheEarlierEdge()
        {
            // 0 → 1 → 3 and 0 → 2 → 3: same length, the edge 0 → 1 was added first.
            var graph = new RouteGraph(4);
            graph.AddEdge(0, 1);
            graph.AddEdge(0, 2);
            graph.AddEdge(1, 3);
            graph.AddEdge(2, 3);
            graph.Build();

            Assert.IsTrue(graph.TryGetPath(0, 3, _path));

            CollectionAssert.AreEqual(new[] { 1, 3 }, _path);
        }

        [Test]
        public void UnreachableNode_HasNoPathAndClearsBuffer()
        {
            var graph = new RouteGraph(3);
            graph.AddEdge(0, 1);
            graph.Build();
            _path.Add(42);

            Assert.IsFalse(graph.TryGetPath(0, 2, _path));
            Assert.IsFalse(graph.HasPath(0, 2));
            Assert.AreEqual(0, _path.Count);
        }

        [Test]
        public void SameNode_IsAnEmptyPath()
        {
            RouteGraph graph = Chain(2);
            _path.Add(42);

            Assert.IsTrue(graph.TryGetPath(1, 1, _path));
            Assert.IsTrue(graph.HasPath(1, 1));
            Assert.AreEqual(0, _path.Count);
        }

        [Test]
        public void OneWayEdge_IsNotUsedBackwards()
        {
            RouteGraph graph = Chain(3);

            Assert.IsTrue(graph.HasPath(0, 2));
            Assert.IsFalse(graph.HasPath(2, 0));
            Assert.IsFalse(graph.TryGetPath(1, 0, _path));
        }

        [Test]
        public void Cycle_IsWalkedForward()
        {
            // 0 → 1 → 2 → 0
            RouteGraph graph = Chain(3, build: false);
            graph.AddEdge(2, 0);
            graph.Build();

            Assert.IsTrue(graph.TryGetPath(2, 1, _path));

            CollectionAssert.AreEqual(new[] { 0, 1 }, _path);
        }

        [Test]
        public void AddEdge_AfterBuild_RequiresRebuild()
        {
            RouteGraph graph = Chain(3);
            Assert.IsTrue(graph.IsBuilt);

            graph.AddEdge(2, 0);

            Assert.IsFalse(graph.IsBuilt);
            Assert.Throws<InvalidOperationException>(() => graph.HasPath(0, 1));
            graph.Build();
            Assert.IsTrue(graph.HasPath(2, 0));
        }

        [Test]
        public void InvalidArguments_Throw()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new RouteGraph(-1));

            var graph = new RouteGraph(2);
            Assert.Throws<ArgumentOutOfRangeException>(() => graph.AddEdge(0, 2));
            Assert.Throws<ArgumentOutOfRangeException>(() => graph.AddEdge(-1, 0));
            Assert.Throws<ArgumentException>(() => graph.AddEdge(1, 1));
            Assert.Throws<InvalidOperationException>(() => graph.TryGetPath(0, 1, _path), "Not built yet.");

            graph.Build();
            Assert.Throws<ArgumentNullException>(() => graph.TryGetPath(0, 1, null));
            Assert.Throws<ArgumentOutOfRangeException>(() => graph.HasPath(0, 5));
        }

        private static RouteGraph Chain(int count, bool build = true)
        {
            var graph = new RouteGraph(count);
            for (int i = 0; i + 1 < count; i++)
            {
                graph.AddEdge(i, i + 1);
            }

            if (build)
            {
                graph.Build();
            }

            return graph;
        }
    }
}
