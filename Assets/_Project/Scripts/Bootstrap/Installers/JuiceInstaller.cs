using AutoService.Presentation.Ui.FloatingText;
using AutoService.Services.Events;
using UnityEngine;

namespace AutoService.Bootstrap.Installers
{
    internal sealed class JuiceInstaller : IGameplayInstaller
    {
        public void Install(GameplayContext context)
        {
            var spawnerGo = new GameObject("FloatingTextSpawner");
            var spawner = spawnerGo.AddComponent<FloatingTextSpawner>();
            
            spawner.Initialize(context.Resolve<IEventBus>(), context.Scene.Locations);
            
            // To ensure it gets cleaned up on reload/dispose:
            context.Track(spawner);
        }
    }
}
