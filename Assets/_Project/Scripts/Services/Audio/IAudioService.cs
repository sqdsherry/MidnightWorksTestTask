namespace AutoService.Services.Audio
{
    /// <summary>
    /// Project-wide sound output: one-shot effects and the volume levels taken from the player settings.
    /// </summary>
    public interface IAudioService
    {
        /// <summary>Plays <paramref name="kind"/> once at the current SFX volume. Missing clips are ignored.</summary>
        void PlaySfx(SfxKind kind);

        /// <summary>Sets music and SFX volumes, both in [0, 1].</summary>
        void SetVolumes(float music, float sfx);
    }
}
