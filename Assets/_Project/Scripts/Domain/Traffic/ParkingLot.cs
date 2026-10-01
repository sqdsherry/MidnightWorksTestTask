using System;

namespace AutoService.Domain.Traffic
{
    /// <summary>
    /// Parking slots of a location. A slot is reserved when a car is sent to the barrier and released when the car
    /// leaves for a service point, so a car driving in always has its place.
    /// </summary>
    public sealed class ParkingLot
    {
        /// <summary>Value of a free slot / failed reservation.</summary>
        public const int None = -1;

        private int[] _slots;

        /// <summary>Creates an empty lot.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown for a negative capacity.</exception>
        public ParkingLot(int capacity)
        {
            if (capacity < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "Parking capacity must be non-negative.");
            }

            _slots = new int[capacity];
            MarkFree(_slots, 0);
            FreeCount = capacity;
        }

        /// <summary>Number of slots.</summary>
        public int Capacity => _slots.Length;

        /// <summary>Number of free slots.</summary>
        public int FreeCount { get; private set; }

        /// <summary>Reserves the first free slot for <paramref name="carId"/>.</summary>
        /// <param name="carId">Non-negative car id.</param>
        /// <param name="slotIndex">Reserved slot, or <see cref="None"/> when the lot is full.</param>
        /// <returns>False when the lot is full.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown for a negative id.</exception>
        public bool TryReserve(int carId, out int slotIndex)
        {
            if (carId < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(carId), carId, "Car id must be non-negative.");
            }

            for (int i = 0; i < _slots.Length; i++)
            {
                if (_slots[i] == None)
                {
                    _slots[i] = carId;
                    FreeCount--;
                    slotIndex = i;
                    return true;
                }
            }

            slotIndex = None;
            return false;
        }

        /// <summary>Frees <paramref name="slotIndex"/>. Releasing a free slot does nothing.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the index is outside 0..Capacity-1.</exception>
        public void Release(int slotIndex)
        {
            RequireIndex(slotIndex);
            if (_slots[slotIndex] == None)
            {
                return;
            }

            _slots[slotIndex] = None;
            FreeCount++;
        }

        /// <summary>Car id in <paramref name="slotIndex"/>, or <see cref="None"/>.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the index is outside 0..Capacity-1.</exception>
        public int CarAt(int slotIndex)
        {
            RequireIndex(slotIndex);
            return _slots[slotIndex];
        }

        /// <summary>Grows the lot (module 05 builds extra slots). Existing reservations keep their indices.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="capacity"/> is smaller than the current one.</exception>
        public void SetCapacity(int capacity)
        {
            if (capacity < _slots.Length)
            {
                // Why: shrinking would have to evict cars that may already be driving to their slot.
                throw new ArgumentOutOfRangeException(
                    nameof(capacity), capacity, "Parking capacity can only grow (current " + _slots.Length + ").");
            }

            if (capacity == _slots.Length)
            {
                return;
            }

            int previous = _slots.Length;
            Array.Resize(ref _slots, capacity);
            MarkFree(_slots, previous);
            FreeCount += capacity - previous;
        }

        private void RequireIndex(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= _slots.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(slotIndex), slotIndex, "Parking slot index is out of range.");
            }
        }

        private static void MarkFree(int[] slots, int from)
        {
            for (int i = from; i < slots.Length; i++)
            {
                slots[i] = None;
            }
        }
    }
}
