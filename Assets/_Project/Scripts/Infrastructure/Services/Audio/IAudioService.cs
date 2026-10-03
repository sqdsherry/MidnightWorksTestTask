using UnityEngine;

namespace AutoService.Infrastructure.Services.Audio
{
    /// <summary>
    /// Service for playing sound effects and music.
    /// Placed in Infrastructure because it directly references Unity's AudioClip.
    /// </summary>
    public interface IAudioService
    {
        void PlaySfx(AudioClip clip);
        void PlayMusic(AudioClip clip);
    }
}
