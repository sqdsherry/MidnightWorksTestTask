using System;

namespace AutoService.Domain.Supplies
{
    /// <summary>
    /// Consumable stock of one service point (shampoo of a wash, oil of an oil bay): every accepted order uses one unit,
    /// boxes from the warehouse refill it.
    /// </summary>
    /// <remarks>
    /// The stock never overflows: <see cref="Add"/> throws instead of clamping, so a box can never vanish silently —
    /// callers check <see cref="CanAdd"/> and keep the box when it does not fit.
    /// </remarks>
    public sealed class SupplyStock
    {
        /// <summary>Creates a full stock.</summary>
        /// <param name="supplyTypeId">Id of the consumable (from config).</param>
        /// <param name="capacity">Maximum units (&gt; 0).</param>
        /// <exception cref="ArgumentException">Thrown for an empty type id.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown for a non-positive capacity.</exception>
        public SupplyStock(string supplyTypeId, int capacity)
        {
            if (string.IsNullOrWhiteSpace(supplyTypeId))
            {
                throw new ArgumentException("Supply type id must not be empty.", nameof(supplyTypeId));
            }

            if (capacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "Supply capacity must be positive.");
            }

            SupplyTypeId = supplyTypeId;
            Capacity = capacity;

            // Why: GDD §8 — a point starts with a full stock, so the first minutes need no warehouse runs.
            Current = capacity;
        }

        /// <summary>Raised after every change of <see cref="Current"/>.</summary>
        public event Action<SupplyStock> Changed;

        /// <summary>Id of the consumable.</summary>
        public string SupplyTypeId { get; }

        /// <summary>Units left.</summary>
        public int Current { get; private set; }

        /// <summary>Maximum units.</summary>
        public int Capacity { get; }

        /// <summary>True when no unit is left: the point cannot accept orders.</summary>
        public bool IsEmpty => Current == 0;

        /// <summary>Fill level 0..1.</summary>
        public float Fill01 => (float)Current / Capacity;

        /// <summary>True when <paramref name="units"/> more units fit.</summary>
        public bool CanAdd(int units) => units >= 0 && Capacity - Current >= units;

        /// <summary>Uses one unit.</summary>
        /// <returns>False (nothing changes) when the stock is empty.</returns>
        public bool TryConsume()
        {
            if (IsEmpty)
            {
                return false;
            }

            Current--;
            Changed?.Invoke(this);
            return true;
        }

        /// <summary>Adds <paramref name="units"/> (a delivered box).</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown for a negative amount.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the units do not fit; check <see cref="CanAdd"/> first.</exception>
        public void Add(int units)
        {
            if (units < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(units), units, "Units must be non-negative.");
            }

            if (!CanAdd(units))
            {
                throw new InvalidOperationException(
                    "Stock of '" + SupplyTypeId + "' holds " + Current + "/" + Capacity + "; " + units + " more units do not fit.");
            }

            if (units == 0)
            {
                return;
            }

            Current += units;
            Changed?.Invoke(this);
        }

        /// <summary>Sets the saved amount, clamped to 0..<see cref="Capacity"/> (a save may come from another config).</summary>
        public void Restore(int current)
        {
            int clamped = Math.Max(0, Math.Min(Capacity, current));
            if (clamped == Current)
            {
                return;
            }

            Current = clamped;
            Changed?.Invoke(this);
        }
    }
}
