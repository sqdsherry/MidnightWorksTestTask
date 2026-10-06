using System;
using AutoService.Presentation.Player;
using AutoService.Services.Core;
using UnityEngine;

namespace AutoService.Presentation.Popups
{
    /// <summary>
    /// Shows the <see cref="Location2WelcomePopupView"/> the first time the character reaches the second location.
    /// </summary>
    /// <remarks>
    /// "Reached" means the character is nearer to the second location's origin than to the first one's. The shown flag is
    /// kept in PlayerPrefs (a UI hint, not game progress).
    /// </remarks>
    public sealed class Location2WelcomePresenter : ITickable
    {
        /// <summary>PlayerPrefs key of the "already shown" flag.</summary>
        public const string Loc2WelcomeShownKey = "loc2_welcome_shown";

        private readonly PlayerView _player;
        private readonly Location2WelcomePopupView _view;
        private readonly Vector3 _firstOrigin;
        private readonly Vector3 _secondOrigin;
        private bool _shown;

        /// <summary>Creates the presenter.</summary>
        /// <param name="player">The character.</param>
        /// <param name="view">The welcome popup.</param>
        /// <param name="firstOrigin">World origin of the first location.</param>
        /// <param name="secondOrigin">World origin of the second location.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="player"/> or <paramref name="view"/> is null.</exception>
        public Location2WelcomePresenter(PlayerView player, Location2WelcomePopupView view, Vector3 firstOrigin, Vector3 secondOrigin)
        {
            if (player == null)
            {
                throw new ArgumentNullException(nameof(player));
            }

            if (view == null)
            {
                throw new ArgumentNullException(nameof(view));
            }

            _player = player;
            _view = view;
            _firstOrigin = firstOrigin;
            _secondOrigin = secondOrigin;
            _shown = PlayerPrefs.GetInt(Loc2WelcomeShownKey, 0) == 1;
        }

        /// <summary>Forgets that the popup was shown, so it appears again on the next arrival (progress reset).</summary>
        public static void ResetShownFlag()
        {
            PlayerPrefs.DeleteKey(Loc2WelcomeShownKey);
        }

        /// <inheritdoc />
        public void Tick(float deltaTime)
        {
            if (_shown)
            {
                return;
            }

            Vector3 position = _player.transform.position;
            if (SqrDistanceXz(position, _secondOrigin) < SqrDistanceXz(position, _firstOrigin))
            {
                _shown = true;
                PlayerPrefs.SetInt(Loc2WelcomeShownKey, 1);
                PlayerPrefs.Save();
                _view.Show();
            }
        }

        private static float SqrDistanceXz(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x;
            float dz = a.z - b.z;
            return dx * dx + dz * dz;
        }
    }
}
