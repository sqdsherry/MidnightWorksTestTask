using System;
using AutoService.Services.Audio;
using AutoService.Services.Core;
using UnityEngine;

namespace AutoService.Infrastructure.Audio
{
    /// <summary>
    /// <see cref="IAudioService"/> on a Unity <see cref="AudioSource"/> attached to a persistent host object.
    /// Clips are loaded once from <c>Resources/Audio</c>.
    /// </summary>
    public sealed class UnityAudioService : IAudioService
    {
        // Why: indexed by (int)SfxKind — keep in the enum's order.
        private static readonly string[] ClipPaths =
        {
            "Audio/sfx_click",
            "Audio/sfx_money",
            "Audio/sfx_service_complete",
            "Audio/sfx_level_up",
        };

        private readonly AudioSource _sfxSource;
        private readonly AudioClip[] _clips;
        private float _sfxVolume = 1f;

        /// <summary>Adds the SFX source to <paramref name="host"/> and loads the clips.</summary>
        /// <param name="host">Object that lives for the whole session (the project entry point).</param>
        /// <param name="logger">Reports clips missing from Resources.</param>
        /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
        public UnityAudioService(GameObject host, IGameLogger logger)
        {
            if (host == null)
            {
                throw new ArgumentNullException(nameof(host));
            }

            if (logger == null)
            {
                throw new ArgumentNullException(nameof(logger));
            }

            _sfxSource = host.AddComponent<AudioSource>();
            _sfxSource.playOnAwake = false;
            _sfxSource.spatialBlend = 0f;

            _clips = new AudioClip[ClipPaths.Length];
            for (int i = 0; i < ClipPaths.Length; i++)
            {
                _clips[i] = Resources.Load<AudioClip>(ClipPaths[i]);
                if (_clips[i] == null)
                {
                    logger.Warning("[Audio] Clip not found in Resources: " + ClipPaths[i]);
                }
            }
        }

        /// <inheritdoc />
        public void PlaySfx(SfxKind kind)
        {
            int index = (int)kind;
            if (index < 0 || index >= _clips.Length || _clips[index] == null || _sfxVolume <= 0f)
            {
                return;
            }

            // Why: the source stays at volume 1 and the level is passed per shot, so it is applied exactly once.
            _sfxSource.PlayOneShot(_clips[index], _sfxVolume);
        }

        /// <inheritdoc />
        public void SetVolumes(float music, float sfx)
        {
            // Why: the game has no music track yet; the music level is only kept in settings for when one is added.
            _sfxVolume = Mathf.Clamp01(sfx);
        }
    }
}
