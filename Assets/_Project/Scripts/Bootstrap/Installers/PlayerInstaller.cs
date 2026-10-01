using System;
using AutoService.Presentation.Controls;
using AutoService.Presentation.Interaction;
using AutoService.Presentation.Player;
using AutoService.Services.Core;

namespace AutoService.Bootstrap.Installers
{
    /// <summary>Input, pointer raycasts, the character's movement FSM and the camera rig.</summary>
    internal sealed class PlayerInstaller : IGameplayInstaller
    {
        /// <summary>Gameplay input (Esc for panels), or null when the player module was skipped.</summary>
        public GameplayInput Input { get; private set; }

        /// <inheritdoc />
        // Why: a scene with missing references should still run the rest of the game and say exactly what is missing,
        // instead of failing with a NullReferenceException deep inside a constructor.
        public void Install(GameplayContext context)
        {
            GameplaySceneRefs scene = context.Scene;

            // Why: non-short-circuit `|` so every missing reference is reported at once, not one per Play.
            if (!context.HasReference(scene.InputActions, "_inputActions")
                | !context.HasReference(scene.Camera, "_camera")
                | !context.HasReference(scene.CameraRig, "_cameraRig")
                | !context.HasReference(scene.Player, "_player"))
            {
                context.Logger.Error("[Gameplay] Player module skipped: assign the missing references on " + context.OwnerName + ".");
                return;
            }

            if (scene.Player.Agent == null)
            {
                context.Logger.Error("[Gameplay] Player module skipped: PlayerView '" + scene.Player.name + "' has no NavMeshAgent assigned.");
                return;
            }

            if (scene.InteractableMask.value == 0)
            {
                context.Logger.Warning("[Gameplay] _interactableMask is empty; interactables cannot be clicked.");
            }

            if (scene.GroundMask.value == 0)
            {
                context.Logger.Warning("[Gameplay] _groundMask is empty; clicks on the ground are ignored.");
            }

            GameplayInput input;
            try
            {
                input = new GameplayInput(scene.InputActions);
            }
            catch (InvalidOperationException exception)
            {
                context.Logger.Error("[Gameplay] Player module skipped: " + exception.Message);
                return;
            }

            // Why: Presentation classes have no contracts of their own; they are registered under their concrete types
            // so the container disposes them (presenter and motor first, input last — reverse registration order).
            context.Register(input, TickPhase.Input);
            input.Enable();
            Input = input;

            var raycaster = new PointerRaycaster(scene.Camera, scene.InteractableMask, scene.GroundMask, scene.OccluderMask);
            context.Register(new PlayerMotor(scene.Player), TickPhase.Input);
            context.Register(
                new PlayerInputPresenter(input, raycaster, scene.Player, context.Resolve<IPauseService>(), scene.ClickMarker),
                TickPhase.Input);
            scene.CameraRig.Construct(input, scene.Player);
        }
    }
}
