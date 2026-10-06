using System;
using System.Collections.Generic;
using AutoService.Services.Progression;

namespace AutoService.Presentation.Popups
{
    /// <summary>
    /// Listens to player level-up events and drives the <see cref="LevelUpPopupView"/>.
    /// Queues multiple level gains so no congratulatory popup is lost.
    /// </summary>
    public sealed class LevelUpPresenter : IDisposable
    {
        private readonly IProgressionService _progression;
        private readonly LevelUpPopupView _view;
        private readonly Queue<int> _pendingLevels = new Queue<int>();

        private bool _isShowing;
        private bool _disposed;

        public LevelUpPresenter(IProgressionService progression, LevelUpPopupView view)
        {
            _progression = progression ?? throw new ArgumentNullException(nameof(progression));
            _view = view ?? throw new ArgumentNullException(nameof(view));

            _progression.LeveledUp += OnLeveledUp;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _progression.LeveledUp -= OnLeveledUp;
        }

        private void OnLeveledUp(int newLevel)
        {
            if (_isShowing)
            {
                _pendingLevels.Enqueue(newLevel);
                return;
            }

            ShowLevel(newLevel);
        }

        private void ShowLevel(int level)
        {
            _isShowing = true;
            string description = GetUnlockedDescription(level);
            _view.Show(level, description, OnPopupClosed);
        }

        private void OnPopupClosed()
        {
            _isShowing = false;
            if (_pendingLevels.Count > 0)
            {
                int nextLevel = _pendingLevels.Dequeue();
                ShowLevel(nextLevel);
            }
        }

        /// <summary>Returns localized unlock description for the given level.</summary>
        public static string GetUnlockedDescription(int level)
        {
            switch (level)
            {
                case 2:
                    return "Разблокирована Автомойка 2 и Замена масла!\n(Новые боксы на Локации 1)";
                case 3:
                    return "Разблокирован второй бокс замены масла!\n(Увеличение потока клиентов)";
                case 4:
                    return "Разблокирован переезд на Локацию 2 — Тюнинг-Центр!\n(Доступен шлагбаум переезда)";
                case 5:
                    return "Максимальный уровень мастерской!\nВсе сервисы разблокированы!";
                default:
                    return $"Уровень {level} достигнут!\nМастерская расширяется!";
            }
        }
    }
}
