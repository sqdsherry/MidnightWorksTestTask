using System;
using AutoService.Presentation.Player;
using AutoService.Services.Core;
using UnityEngine;

namespace AutoService.Presentation.Popups
{
    /// <summary>
    /// Tracks player entry into Location 2 and shows the <see cref="Location2WelcomePopupView"/> once per game profile.
    /// </summary>
    public sealed class Location2WelcomePresenter : ITickable, IDisposable
    {
        public const string Loc2WelcomeShownKey = "loc2_welcome_shown";
        private const float Loc2ThresholdX = 100f;

        private readonly PlayerView _player;
        private readonly Location2WelcomePopupView _view;
        private bool _shown;
        private bool _disposed;

        public Location2WelcomePresenter(PlayerView player, Location2WelcomePopupView view)
        {
            _player = player;
            _view = view;
            _shown = PlayerPrefs.GetInt(Loc2WelcomeShownKey, 0) == 1;
        }

        /// <inheritdoc />
        public void Tick(float deltaTime)
        {
            if (_shown || _player == null || _view == null)
            {
                return;
            }

            // Location 2 is positioned at X ~ 200, Location 1 at X ~ 0
            if (_player.transform.position.x > Loc2ThresholdX)
            {
                _shown = true;
                PlayerPrefs.SetInt(Loc2WelcomeShownKey, 1);
                PlayerPrefs.Save();
                _view.Show();
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
        }
    }
}
