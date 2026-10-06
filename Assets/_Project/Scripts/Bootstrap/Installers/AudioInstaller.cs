using AutoService.Infrastructure.Services.Audio;
using AutoService.Services.Events;
using UnityEngine;

namespace AutoService.Bootstrap.Installers
{
    internal sealed class AudioInstaller : IGameplayInstaller
    {
        public void Install(GameplayContext context)
        {
            var audioService = Object.FindFirstObjectByType<AudioService>(FindObjectsInactive.Include);
            if (audioService == null)
            {
                var audioGo = new GameObject("AudioService");
                audioService = audioGo.AddComponent<AudioService>();
            }
            
            audioService.Initialize(context.Resolve<IEventBus>());
            
            context.Register<IAudioService>(audioService);
            context.Track(audioService);
        }
    }
}
