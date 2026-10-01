using System;

namespace AutoService.Services.Traffic
{
    /// <summary>
    /// Port to the physical cars of one location. Implemented in Presentation (NavMesh agents, pooled visuals);
    /// the traffic logic only talks to it by car id and <see cref="CarDestination"/>.
    /// </summary>
    public interface ICarAgents
    {
        /// <summary>Raised once per <see cref="MoveTo"/> when the car has reached the destination and is aligned with it.</summary>
        /// <remarks>
        /// Must never be raised synchronously from inside <see cref="Spawn"/>/<see cref="MoveTo"/>/<see cref="Despawn"/>:
        /// the traffic logic calls them in the middle of its own bookkeeping. Raise it from the implementation's tick instead.
        /// </remarks>
        event Action<int> Arrived;

        /// <summary>Spawns a car visual at the location's spawn point.</summary>
        void Spawn(int carId, string carTypeId);

        /// <summary>Drives the car to the destination; raises Arrived once it is there and aligned.</summary>
        /// <remarks>A new call replaces the previous destination; the previous one never raises <see cref="Arrived"/>.</remarks>
        void MoveTo(int carId, CarDestination destination);

        /// <summary>Returns the car visual to the pool.</summary>
        void Despawn(int carId);
    }
}
