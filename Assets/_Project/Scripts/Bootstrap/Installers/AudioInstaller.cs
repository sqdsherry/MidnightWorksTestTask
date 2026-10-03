using AutoService.Infrastructure.Services.Audio;
using AutoService.Services.Events;
using UnityEngine;

namespace AutoService.Bootstrap.Installers
{
    internal sealed class AudioInstaller : IGameplayInstaller
    {
        public void Install(GameplayContext context)
        {
            var audioGo = new GameObject("AudioService");
            var audioService = audioGo.AddComponent<AudioService>();
            
            audioService.Initialize(context.Resolve<IEventBus>());
            
            context.Register<IAudioService>(audioService);
            context.Track(audioService);
        }
    }
}
