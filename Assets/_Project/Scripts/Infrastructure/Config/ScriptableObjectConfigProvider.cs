using System;
using AutoService.Domain.Common;
using AutoService.Services.Config;

namespace AutoService.Infrastructure.Config
{
    /// <summary>
    /// <see cref="IConfigProvider"/> that maps a <see cref="GameConfig"/> asset into engine-agnostic settings.
    /// </summary>
    /// <remarks>
    /// Mapping happens once in the constructor: consumers get immutable snapshots, and editing the asset
    /// during Play Mode cannot change the rules underneath running systems.
    /// </remarks>
    public sealed class ScriptableObjectConfigProvider : IConfigProvider
    {
        /// <summary>Maps <paramref name="config"/> into settings.</summary>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="config"/> is null (e.g. not assigned in the inspector).</exception>
        public ScriptableObjectConfigProvider(GameConfig config)
        {
            // Why: Unity's overloaded == also catches a missing/destroyed asset, not just a null reference.
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config), "GameConfig is not assigned.");
            }

            Economy = new EconomySettings(new Money(config.Economy.StartingMoney));
        }

        /// <inheritdoc />
        public EconomySettings Economy { get; }
    }
}
