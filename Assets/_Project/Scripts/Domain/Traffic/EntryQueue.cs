using System;

namespace AutoService.Domain.Traffic
{
    /// <summary>
    /// The entry lane in front of the fork: a fixed number of slots, slot 0 is the head at the fork.
    /// Cars always occupy slots 0..Count-1 contiguously; removing the head moves everybody one slot forward.
    /// </summary>
    /// <remarks>Backed by a preallocated array: no operation allocates.</remarks>
    public sealed class EntryQueue
    {
        /// <summary>Value returned for an empty slot / missing car.</summary>
        public const int None = -1;

        private readonly int[] _slots;

        /// <summary>Creates an empty queue.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="capacity"/> is not positive.</exception>
        public EntryQueue(int capacity)
        {
            if (capacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "Queue capacity must be positive.");
            }

            _slots = new int[capacity];
            for (int i = 0; i < capacity; i++)
            {
                _slots[i] = None;
            }
        }

        /// <summary>Raised after <see cref="RemoveHead"/> moved the remaining cars forward; listeners re-target them.</summary>
        public event Action<EntryQueue> Shifted;

        /// <summary>Number of slots.</summary>
        public int Capacity => _slots.Length;

        /// <summary>Number of cars in the queue.</summary>
        public int Count { get; private set; }

        /// <summary>True when every slot is taken.</summary>
        public bool IsFull => Count == _slots.Length;

        /// <summary>Car id at the head (slot 0), or <see cref="None"/>.</summary>
        public int Head => _slots[0];

        /// <summary>Adds a car to the tail.</summary>
        /// <param name="carId">Non-negative car id.</param>
        /// <param name="slotIndex">Slot the car takes, or <see cref="None"/> when the queue is full.</param>
        /// <returns>False when the queue is full.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown for a negative id.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the car is already queued.</exception>
        public bool TryEnqueue(int carId, out int slotIndex)
        {
            if (carId < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(carId), carId, "Car id must be non-negative.");
            }

            if (SlotOf(carId) != None)
            {
                throw new InvalidOperationException("Car " + carId + " is already in the queue.");
            }

            if (IsFull)
            {
                slotIndex = None;
                return false;
            }

            slotIndex = Count;
            _slots[slotIndex] = carId;
            Count++;
            return true;
        }

        /// <summary>Slot of <paramref name="carId"/>, or <see cref="None"/>.</summary>
        public int SlotOf(int carId)
        {
            for (int i = 0; i < Count; i++)
            {
                if (_slots[i] == carId)
                {
                    return i;
                }
            }

            return None;
        }

        /// <summary>Car id in <paramref name="slotIndex"/>, or <see cref="None"/> for an empty slot.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the index is outside 0..Capacity-1.</exception>
        public int GetAt(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= _slots.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(slotIndex), slotIndex, "Slot index is out of range.");
            }

            return _slots[slotIndex];
        }

        /// <summary>Removes the head; the other cars move one slot forward, then <see cref="Shifted"/> is raised.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the queue is empty.</exception>
        public void RemoveHead()
        {
            if (Count == 0)
            {
                throw new InvalidOperationException("The queue is empty.");
            }

            Count--;
            Array.Copy(_slots, 1, _slots, 0, Count);
            _slots[Count] = None;
            Shifted?.Invoke(this);
        }
    }
}
