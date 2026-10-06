using AutoService.Presentation.Ui;
using AutoService.Services.Audio;
using AutoService.Services.Events;
using UnityEngine;

namespace AutoService.Bootstrap.Installers
{
    /// <summary>
    /// Gameplay sounds: bus events to SFX and the click sound on every button of the scene.
    /// </summary>
    /// <remarks>Must run after <see cref="HudInstaller"/>, so the popups it instantiates already exist.</remarks>
    internal sealed class AudioInstaller : IGameplayInstaller
    {
        private readonly GameObject[] _sceneRoots;

        /// <param name="sceneRoots">Root objects of the gameplay scene.</param>
        public AudioInstaller(GameObject[] sceneRoots)
        {
            _sceneRoots = sceneRoots;
        }

        /// <inheritdoc />
        public void Install(GameplayContext context)
        {
            var audio = context.Resolve<IAudioService>();
            context.Track(new GameplaySfx(context.Resolve<IEventBus>(), audio));

            var clicks = new ButtonClickSounds(audio);
            clicks.Bind(_sceneRoots);
            context.Track(clicks);
        }
    }
}
