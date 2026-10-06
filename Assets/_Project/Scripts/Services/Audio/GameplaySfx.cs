using System;
using AutoService.Services.Economy;
using AutoService.Services.Events;
using AutoService.Services.Points;
using AutoService.Services.Progression;

namespace AutoService.Services.Audio
{
    /// <summary>
    /// Turns gameplay bus events into sound effects: income, finished services and level-ups.
    /// </summary>
    public sealed class GameplaySfx : IDisposable
    {
        private readonly IEventBus _eventBus;
        private readonly IAudioService _audio;

        // Why: cached so subscribing does not allocate and Unsubscribe receives the same delegate instance.
        private readonly Action<BalanceChangedEvent> _onBalanceChanged;
        private readonly Action<ServiceCompletedEvent> _onServiceCompleted;
        private readonly Action<LevelUpEvent> _onLevelUp;

        /// <summary>Subscribes to the bus right away.</summary>
        /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
        public GameplaySfx(IEventBus eventBus, IAudioService audio)
        {
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            _audio = audio ?? throw new ArgumentNullException(nameof(audio));

            _onBalanceChanged = OnBalanceChanged;
            _onServiceCompleted = OnServiceCompleted;
            _onLevelUp = OnLevelUp;

            _eventBus.Subscribe(_onBalanceChanged);
            _eventBus.Subscribe(_onServiceCompleted);
            _eventBus.Subscribe(_onLevelUp);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            _eventBus.Unsubscribe(_onBalanceChanged);
            _eventBus.Unsubscribe(_onServiceCompleted);
            _eventBus.Unsubscribe(_onLevelUp);
        }

        private void OnBalanceChanged(BalanceChangedEvent gameEvent)
        {
            // Why: spending already has the button click; only income gets the cash sound.
            if (gameEvent.Delta > 0)
            {
                _audio.PlaySfx(SfxKind.Cash);
            }
        }

        private void OnServiceCompleted(ServiceCompletedEvent gameEvent) => _audio.PlaySfx(SfxKind.ServiceCompleted);

        private void OnLevelUp(LevelUpEvent gameEvent) => _audio.PlaySfx(SfxKind.LevelUp);
    }
}
