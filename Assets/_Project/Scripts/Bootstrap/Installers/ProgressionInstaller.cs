using AutoService.Domain.Progression;
using AutoService.Services.Config;
using AutoService.Services.EventBus;
using AutoService.Services.Progression;

namespace AutoService.Bootstrap.Installers
{
    /// <summary>Player progression tracking: <see cref="IProgressionService"/> over a domain <see cref="PlayerProgress"/>.</summary>
    internal sealed class ProgressionInstaller : IGameplayInstaller
    {
        /// <inheritdoc />
        public void Install(GameplayContext context)
        {
            IConfigProvider config = context.Resolve<IConfigProvider>();
            
            var progress = new PlayerProgress(config.Progression.Table);
            var service = new ProgressionService(progress, config, context.Resolve<IEventBus>());
            
            context.Register<IProgressionService>(service);
            context.Track(service);
        }
    }
}
