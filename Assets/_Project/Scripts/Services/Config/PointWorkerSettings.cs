using System;
using AutoService.Domain.Common;

namespace AutoService.Services.Config
{
    /// <summary>Immutable hiring settings of the worker of a service type ("Washer", "Parking Attendant"...).</summary>
    public sealed class PointWorkerSettings
    {
        /// <summary>Creates and validates the settings.</summary>
        /// <param name="title">Player-facing job title (English), non-empty.</param>
        /// <param name="hireCost">One-time hiring price (&gt;= 0).</param>
        /// <param name="requiredLevel">Player level needed to hire (&gt;= 0).</param>
        /// <exception cref="ArgumentException">Thrown for an empty title, a negative cost or level.</exception>
        public PointWorkerSettings(string title, Money hireCost, int requiredLevel)
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                throw new ArgumentException("Worker title must not be empty.", nameof(title));
            }

            if (hireCost < Money.Zero)
            {
                throw new ArgumentException("Hire cost must be non-negative, got " + hireCost + ".", nameof(hireCost));
            }

            if (requiredLevel < 0)
            {
                throw new ArgumentException("Required level must be non-negative, got " + requiredLevel + ".", nameof(requiredLevel));
            }

            Title = title;
            HireCost = hireCost;
            RequiredLevel = requiredLevel;
        }

        /// <summary>Player-facing job title.</summary>
        public string Title { get; }

        /// <summary>One-time hiring price.</summary>
        public Money HireCost { get; }

        /// <summary>Player level needed to hire.</summary>
        public int RequiredLevel { get; }
    }
}
