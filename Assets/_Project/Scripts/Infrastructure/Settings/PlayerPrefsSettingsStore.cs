using System;
using AutoService.Services.Settings;
using UnityEngine;

namespace AutoService.Infrastructure.Settings
{
    /// <summary>
    /// <see cref="ISettingsStore"/> backed by <see cref="PlayerPrefs"/>. Kept apart from the save file,
    /// so deleting the save does not reset the player's settings.
    /// </summary>
    public sealed class PlayerPrefsSettingsStore : ISettingsStore
    {
        private const string MusicKey = "settings.music";
        private const string SfxKey = "settings.sfx";
        private const string QualityKey = "settings.quality";
        private const string FullscreenKey = "settings.fullscreen";
        private const string ResolutionWidthKey = "settings.resW";
        private const string ResolutionHeightKey = "settings.resH";

        /// <inheritdoc />
        public bool TryLoad(out GameSettings settings)
        {
            // Why: Save writes all keys together, so the music key alone tells whether settings were ever saved.
            if (!PlayerPrefs.HasKey(MusicKey))
            {
                settings = null;
                return false;
            }

            settings = new GameSettings(
                PlayerPrefs.GetFloat(MusicKey),
                PlayerPrefs.GetFloat(SfxKey),
                PlayerPrefs.GetInt(QualityKey),
                PlayerPrefs.GetInt(FullscreenKey) != 0,
                PlayerPrefs.GetInt(ResolutionWidthKey),
                PlayerPrefs.GetInt(ResolutionHeightKey));
            return true;
        }

        /// <inheritdoc />
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="settings"/> is null.</exception>
        public void Save(GameSettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            PlayerPrefs.SetFloat(MusicKey, settings.MusicVolume);
            PlayerPrefs.SetFloat(SfxKey, settings.SfxVolume);
            PlayerPrefs.SetInt(QualityKey, settings.QualityLevel);
            PlayerPrefs.SetInt(FullscreenKey, settings.Fullscreen ? 1 : 0);
            PlayerPrefs.SetInt(ResolutionWidthKey, settings.ResolutionWidth);
            PlayerPrefs.SetInt(ResolutionHeightKey, settings.ResolutionHeight);

            // Why: PlayerPrefs is otherwise flushed only on a clean quit; a crash would lose the change.
            PlayerPrefs.Save();
        }
    }
}
