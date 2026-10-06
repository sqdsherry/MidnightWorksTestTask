namespace AutoService.Services.Settings
{
    /// <summary>
    /// Immutable player settings (audio, quality, display). Values are validated on construction;
    /// the <c>With…</c> methods return a modified copy.
    /// </summary>
    public sealed class GameSettings
    {
        /// <summary>Creates settings, clamping every value into its valid range.</summary>
        /// <param name="musicVolume">Music volume, clamped to [0, 1]; NaN becomes 0.</param>
        /// <param name="sfxVolume">Sound effects volume, clamped to [0, 1]; NaN becomes 0.</param>
        /// <param name="qualityLevel">Quality preset index; negative becomes 0 (the upper bound is checked by the applier).</param>
        /// <param name="fullscreen">True for fullscreen, false for a window.</param>
        /// <param name="resolutionWidth">Screen width in pixels; 0 keeps the current resolution.</param>
        /// <param name="resolutionHeight">Screen height in pixels; 0 keeps the current resolution.</param>
        public GameSettings(float musicVolume, float sfxVolume, int qualityLevel, bool fullscreen, int resolutionWidth, int resolutionHeight)
        {
            MusicVolume = ClampVolume(musicVolume);
            SfxVolume = ClampVolume(sfxVolume);
            QualityLevel = qualityLevel < 0 ? 0 : qualityLevel;
            Fullscreen = fullscreen;

            // Why: a resolution with only one dimension set is meaningless, so it falls back to "keep current" as a whole.
            bool hasResolution = resolutionWidth > 0 && resolutionHeight > 0;
            ResolutionWidth = hasResolution ? resolutionWidth : 0;
            ResolutionHeight = hasResolution ? resolutionHeight : 0;
        }

        /// <summary>Music volume in [0, 1].</summary>
        public float MusicVolume { get; }

        /// <summary>Sound effects volume in [0, 1].</summary>
        public float SfxVolume { get; }

        /// <summary>Quality preset index (non-negative).</summary>
        public int QualityLevel { get; }

        /// <summary>True for fullscreen, false for a window.</summary>
        public bool Fullscreen { get; }

        /// <summary>Screen width in pixels, or 0 to keep the current resolution.</summary>
        public int ResolutionWidth { get; }

        /// <summary>Screen height in pixels, or 0 to keep the current resolution.</summary>
        public int ResolutionHeight { get; }

        /// <summary>Returns a copy with another music volume.</summary>
        public GameSettings WithMusicVolume(float value) =>
            new GameSettings(value, SfxVolume, QualityLevel, Fullscreen, ResolutionWidth, ResolutionHeight);

        /// <summary>Returns a copy with another sound effects volume.</summary>
        public GameSettings WithSfxVolume(float value) =>
            new GameSettings(MusicVolume, value, QualityLevel, Fullscreen, ResolutionWidth, ResolutionHeight);

        /// <summary>Returns a copy with another quality preset.</summary>
        public GameSettings WithQualityLevel(int value) =>
            new GameSettings(MusicVolume, SfxVolume, value, Fullscreen, ResolutionWidth, ResolutionHeight);

        /// <summary>Returns a copy with another fullscreen flag.</summary>
        public GameSettings WithFullscreen(bool value) =>
            new GameSettings(MusicVolume, SfxVolume, QualityLevel, value, ResolutionWidth, ResolutionHeight);

        /// <summary>Returns a copy with another resolution (0 × 0 keeps the current one).</summary>
        public GameSettings WithResolution(int width, int height) =>
            new GameSettings(MusicVolume, SfxVolume, QualityLevel, Fullscreen, width, height);

        private static float ClampVolume(float value)
        {
            // Why: NaN fails every comparison, so it is handled explicitly instead of slipping through the clamp.
            if (float.IsNaN(value) || value < 0f)
            {
                return 0f;
            }

            return value > 1f ? 1f : value;
        }
    }
}
