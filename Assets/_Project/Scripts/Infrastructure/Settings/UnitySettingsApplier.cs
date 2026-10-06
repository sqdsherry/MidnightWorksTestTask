using System;
using AutoService.Services.Settings;
using UnityEngine;

namespace AutoService.Infrastructure.Settings
{
    /// <summary>
    /// <see cref="ISettingsApplier"/> that pushes settings into <see cref="QualitySettings"/> and <see cref="Screen"/>.
    /// </summary>
    public sealed class UnitySettingsApplier : ISettingsApplier
    {
        /// <inheritdoc />
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="settings"/> is null.</exception>
        public void Apply(GameSettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            ApplyQuality(settings.QualityLevel);
            ApplyScreen(settings);

            if (AutoService.Infrastructure.Services.Audio.AudioService.Instance != null)
            {
                AutoService.Infrastructure.Services.Audio.AudioService.Instance.ApplyVolumes(1f, settings.SfxVolume, settings.MusicVolume);
            }
        }

        private static void ApplyQuality(int level)
        {
            int count = QualitySettings.names.Length;
            if (count == 0)
            {
                return;
            }

            // Why: a saved index may point past the list if quality presets were removed in a later build.
            int clamped = level >= count ? count - 1 : level;
            if (clamped != QualitySettings.GetQualityLevel())
            {
                QualitySettings.SetQualityLevel(clamped, applyExpensiveChanges: true);
            }
        }

        private static void ApplyScreen(GameSettings settings)
        {
            FullScreenMode mode = settings.Fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;

            if (settings.ResolutionWidth > 0 && settings.ResolutionHeight > 0)
            {
                // Why: only on a real change — the applier also runs for volume changes, and SetResolution re-creates
                // the swap chain (a visible flicker) even when the size is the same.
                if (Screen.width != settings.ResolutionWidth
                    || Screen.height != settings.ResolutionHeight
                    || Screen.fullScreenMode != mode)
                {
                    Screen.SetResolution(settings.ResolutionWidth, settings.ResolutionHeight, mode);
                }

                return;
            }

            // 0 x 0 means "keep the current resolution": only the window mode changes.
            if (Screen.fullScreenMode != mode)
            {
                Screen.fullScreenMode = mode;
            }
        }
    }
}
