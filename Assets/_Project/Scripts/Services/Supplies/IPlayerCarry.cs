using System;
using AutoService.Domain.Supplies;

namespace AutoService.Services.Supplies
{
    /// <summary>What the player character holds: at most one <see cref="SupplyBox"/>. Not saved — the character starts empty-handed.</summary>
    /// <remarks>The state lives here, in Services; Presentation only shows it (the box in the character's hands).</remarks>
    public interface IPlayerCarry
    {
        /// <summary>Raised after the box was picked up or dropped.</summary>
        event Action Changed;

        /// <summary>True while a box is carried.</summary>
        bool HasBox { get; }

        /// <summary>The carried box, or <see cref="SupplyBox.None"/>.</summary>
        SupplyBox Box { get; }

        /// <summary>Takes <paramref name="box"/>.</summary>
        /// <returns>False (nothing changes) when the hands are full or the box is <see cref="SupplyBox.None"/>.</returns>
        bool TryPick(SupplyBox box);

        /// <summary>Empties the hands.</summary>
        /// <returns>The box that was carried, or <see cref="SupplyBox.None"/>.</returns>
        SupplyBox Drop();
    }
}
