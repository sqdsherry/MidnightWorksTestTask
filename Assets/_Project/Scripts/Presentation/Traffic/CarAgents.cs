using System;
using System.Collections.Generic;
using AutoService.Services.Core;
using AutoService.Services.Traffic;
using UnityEngine;
using UnityEngine.Pool;

namespace AutoService.Presentation.Traffic
{
    /// <summary>
    /// <see cref="ICarAgents"/> of one location: pooled <see cref="CarView"/>s (one <see cref="ObjectPool{T}"/> per car type)
    /// driven by NavMesh, destinations resolved through the <see cref="LocationLayout"/>.
    /// </summary>
    /// <remarks>
    /// <para><b>Arrivals</b> are collected into a buffer during the tick and raised after the pass over the cars, because
    /// handlers react with <see cref="MoveTo"/>/<see cref="Despawn"/> and would otherwise modify the collection being iterated.
    /// An arrival is dropped if, by the time it is raised, the car got a new target or was despawned.</para>
    /// <para><b>Failures never lose a car:</b> an unknown car type (no prefab) spawns an invisible "ghost" and an unresolvable
    /// destination is reported as reached on the next tick. Both log an error, and the domain flow keeps moving instead of
    /// stalling the queue behind the broken car.</para>
    /// <para>Steady state is allocation-free; instances are only created when a pool runs dry.</para>
    /// </remarks>
    public sealed class CarAgents : ICarAgents, ITickable, IDisposable
    {
        private const int DefaultPoolCapacity = 4;
        private const int MaxPoolSize = 32;

        private readonly LocationLayout _layout;
        private readonly Transform _poolRoot;
        private readonly Dictionary<string, ObjectPool<CarView>> _pools = new Dictionary<string, ObjectPool<CarView>>(StringComparer.Ordinal);
        private readonly Dictionary<int, ActiveCar> _cars = new Dictionary<int, ActiveCar>();
        private readonly List<int> _carIds = new List<int>();

        // Why: two buffers swapped per tick — arrivals queued by handlers while raising go to the next tick's batch.
        private List<int> _pendingArrivals = new List<int>();
        private List<int> _raisingArrivals = new List<int>();

        private bool _disposed;

        /// <summary>Creates one pool per catalog entry.</summary>
        /// <param name="layout">Markup of the location (spawn point, slots, points).</param>
        /// <param name="catalog">Car type id → prefab.</param>
        /// <param name="poolRoot">Parent of the car instances; may be null (scene root).</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="layout"/> or <paramref name="catalog"/> is null.</exception>
        public CarAgents(LocationLayout layout, CarVisualCatalog catalog, Transform poolRoot)
        {
            if (layout == null)
            {
                throw new ArgumentNullException(nameof(layout));
            }

            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }

            _layout = layout;
            _poolRoot = poolRoot;

            for (int i = 0; i < catalog.Count; i++)
            {
                string carTypeId = catalog.GetCarTypeId(i);
                CarView prefab = catalog.GetPrefab(i);
                if (string.IsNullOrWhiteSpace(carTypeId) || prefab == null)
                {
                    Debug.LogError("[CarAgents] Car Visual Catalog '" + catalog.name + "' entry " + i + " has an empty id or prefab.", catalog);
                    continue;
                }

                if (_pools.ContainsKey(carTypeId))
                {
                    Debug.LogError("[CarAgents] Car Visual Catalog '" + catalog.name + "' has a duplicate id '" + carTypeId + "'.", catalog);
                    continue;
                }

                _pools.Add(carTypeId, CreatePool(prefab));
            }
        }

        /// <inheritdoc />
        public event Action<int> Arrived;

        /// <inheritdoc />
        public void Spawn(int carId, string carTypeId)
        {
            if (_cars.ContainsKey(carId))
            {
                Debug.LogError("[CarAgents] Car " + carId + " is already spawned.");
                return;
            }

            CarView view = null;
            if (carTypeId != null && _pools.TryGetValue(carTypeId, out ObjectPool<CarView> pool))
            {
                view = pool.Get();
                Transform spawn = _layout.SpawnPoint;
                view.Place(spawn.position, spawn.rotation);
            }
            else
            {
                pool = null;
                Debug.LogError("[CarAgents] No prefab for car type '" + carTypeId + "' in the Car Visual Catalog; car " + carId + " is invisible.");
            }

            _cars.Add(carId, new ActiveCar(view, pool));
            _carIds.Add(carId);
        }

        /// <inheritdoc />
        public void MoveTo(int carId, CarDestination destination)
        {
            if (!_cars.TryGetValue(carId, out ActiveCar car))
            {
                Debug.LogError("[CarAgents] MoveTo for unknown car " + carId + ".");
                return;
            }

            if (car.View == null)
            {
                // Ghost car (no prefab): it "arrives" on the next tick.
                _pendingArrivals.Add(carId);
                return;
            }

            if (!_layout.TryResolve(destination, out Transform target))
            {
                Debug.LogError("[CarAgents] Location '" + _layout.LocationId + "' cannot resolve " + destination + " for car " + carId + "; reporting it as reached.", _layout);
                car.View.Halt();
                _pendingArrivals.Add(carId);
                return;
            }

            car.View.Drive(target);
        }

        /// <inheritdoc />
        public void Despawn(int carId)
        {
            if (!_cars.TryGetValue(carId, out ActiveCar car))
            {
                return;
            }

            _cars.Remove(carId);
            _carIds.Remove(carId);
            if (car.View != null)
            {
                car.Pool.Release(car.View);
            }
        }

        /// <inheritdoc />
        public void Tick(float deltaTime)
        {
            if (_disposed)
            {
                return;
            }

            for (int i = 0; i < _carIds.Count; i++)
            {
                int carId = _carIds[i];
                CarView view = _cars[carId].View;
                if (view != null && view.TickArrival(deltaTime))
                {
                    _pendingArrivals.Add(carId);
                }
            }

            if (_pendingArrivals.Count == 0)
            {
                return;
            }

            List<int> raising = _pendingArrivals;
            _pendingArrivals = _raisingArrivals;
            _raisingArrivals = raising;

            for (int i = 0; i < raising.Count; i++)
            {
                int carId = raising[i];

                // Why: an earlier handler in this batch may have despawned the car or given it a new target already.
                if (_cars.TryGetValue(carId, out ActiveCar car) && (car.View == null || !car.View.IsDriving))
                {
                    Arrived?.Invoke(carId);
                }
            }

            raising.Clear();
        }

        /// <summary>Destroys the pooled (inactive) instances. Active cars are left to the scene unload. Safe to call repeatedly.</summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            foreach (ObjectPool<CarView> pool in _pools.Values)
            {
                pool.Dispose();
            }

            _pools.Clear();
            _cars.Clear();
            _carIds.Clear();
        }

        private ObjectPool<CarView> CreatePool(CarView prefab)
        {
            // Why: the closure is allocated once per car type at construction, not per spawn.
            return new ObjectPool<CarView>(
                () => CreateInstance(prefab),
                null,
                OnReleased,
                OnDestroyed,
                true,
                DefaultPoolCapacity,
                MaxPoolSize);
        }

        private CarView CreateInstance(CarView prefab)
        {
            // Why: instantiated at the spawn point so the NavMeshAgent is created on the NavMesh (no "not close enough" warning).
            Transform spawn = _layout.SpawnPoint;
            CarView instance = UnityEngine.Object.Instantiate(prefab, spawn.position, spawn.rotation, _poolRoot);
            instance.name = prefab.name;
            return instance;
        }

        private static void OnReleased(CarView view)
        {
            view.ResetForPool();
            view.gameObject.SetActive(false);
        }

        private static void OnDestroyed(CarView view)
        {
            // Why: during scene unload the instance may already be destroyed.
            if (view != null)
            {
                UnityEngine.Object.Destroy(view.gameObject);
            }
        }

        /// <summary>A spawned car: its visual (null for a ghost) and the pool it returns to.</summary>
        private readonly struct ActiveCar
        {
            public ActiveCar(CarView view, ObjectPool<CarView> pool)
            {
                View = view;
                Pool = pool;
            }

            public CarView View { get; }

            public ObjectPool<CarView> Pool { get; }
        }
    }
}
