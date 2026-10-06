using System;
using System.Collections.Generic;
using AutoService.Services.Traffic;

namespace AutoService.Tests.EditMode
{
    /// <summary>Test double of the physical cars: records commands; arrivals are raised explicitly by the test.</summary>
    public sealed class FakeCarAgents : ICarAgents
    {
        /// <inheritdoc />
        public event Action<int> Arrived;

        /// <summary>Ids passed to <see cref="Spawn"/>, in order.</summary>
        public List<int> Spawned { get; } = new List<int>();

        /// <summary>Ids passed to <see cref="Despawn"/>, in order.</summary>
        public List<int> Despawned { get; } = new List<int>();

        /// <summary>Latest destination per car.</summary>
        public Dictionary<int, CarDestination> Destinations { get; } = new Dictionary<int, CarDestination>();

        /// <summary>Number of <see cref="Arrived"/> subscribers (to check unsubscription).</summary>
        public int SubscriberCount => Arrived == null ? 0 : Arrived.GetInvocationList().Length;

        /// <inheritdoc />
        public void Spawn(int carId, string carTypeId) => Spawned.Add(carId);

        /// <inheritdoc />
        public void MoveTo(int carId, CarDestination destination) => Destinations[carId] = destination;

        /// <inheritdoc />
        public void Despawn(int carId) => Despawned.Add(carId);

        /// <summary>Simulates the car reaching its latest destination.</summary>
        public void Arrive(int carId) => Arrived?.Invoke(carId);
    }
}
