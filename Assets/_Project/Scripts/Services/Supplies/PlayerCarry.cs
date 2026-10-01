using System;
using AutoService.Domain.Supplies;

namespace AutoService.Services.Supplies
{
    /// <summary>Default <see cref="IPlayerCarry"/>: one box slot.</summary>
    public sealed class PlayerCarry : IPlayerCarry
    {
        /// <inheritdoc />
        public event Action Changed;

        /// <inheritdoc />
        public bool HasBox => !Box.IsNone;

        /// <inheritdoc />
        public SupplyBox Box { get; private set; } = SupplyBox.None;

        /// <inheritdoc />
        public bool TryPick(SupplyBox box)
        {
            if (HasBox || box.IsNone)
            {
                return false;
            }

            Box = box;
            Changed?.Invoke();
            return true;
        }

        /// <inheritdoc />
        public SupplyBox Drop()
        {
            SupplyBox box = Box;
            if (box.IsNone)
            {
                return box;
            }

            Box = SupplyBox.None;
            Changed?.Invoke();
            return box;
        }
    }
}
