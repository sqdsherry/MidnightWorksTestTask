using System;
using AutoService.Domain.Progression;
using AutoService.Services.Config;
using AutoService.Services.Events;
using AutoService.Services.Points;

namespace AutoService.Services.Progression
{
    /// <summary>
    /// Implements progression tracking and rewards XP for completed services.
    /// </summary>
    public sealed class ProgressionService : IProgressionService, IDisposable
    {
        private readonly PlayerProgress _progress;
        private readonly IConfigProvider _config;
        private readonly IEventBus _eventBus;

        public ProgressionService(PlayerProgress progress, IConfigProvider config, IEventBus eventBus)
        {
            _progress = progress ?? throw new ArgumentNullException(nameof(progress));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));

            _progress.Changed += OnProgressChanged;
            _progress.LeveledUp += OnLeveledUp;
            _eventBus.Subscribe<ServiceCompletedEvent>(OnServiceCompleted);
        }

        public int Xp => _progress.Xp;
        public int Level => _progress.Level;
        public float LevelProgress01 => _progress.LevelProgress01;
        public int XpToNextLevel => _progress.XpToNextLevel;

        public event Action Changed;
        public event Action<int> LeveledUp;

        public void Restore(int xp)
        {
            _progress.Restore(xp);
        }

        public void AddExperience(int amount)
        {
            _progress.AddXp(amount);
        }

        private void OnProgressChanged(PlayerProgress progress)
        {
            Changed?.Invoke();
        }

        private void OnLeveledUp(PlayerProgress progress, int newLevel)
        {
            LeveledUp?.Invoke(newLevel);
            _eventBus.Publish(new LevelUpEvent(newLevel));
        }

        private void OnServiceCompleted(ServiceCompletedEvent evt)
        {
            if (_config.TryGetServiceType(evt.ServiceTypeId, out var type))
            {
                if (type.XpReward > 0)
                {
                    _progress.AddXp(type.XpReward);
                }
            }
        }

        public void Dispose()
        {
            _progress.Changed -= OnProgressChanged;
            _progress.LeveledUp -= OnLeveledUp;
            _eventBus.Unsubscribe<ServiceCompletedEvent>(OnServiceCompleted);
        }
    }
}
