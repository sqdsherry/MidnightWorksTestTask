using System;
using AutoService.Presentation.Characters;
using AutoService.Presentation.Controls;
using AutoService.Presentation.Interaction;
using AutoService.Presentation.Player;
using AutoService.Services.Core;
using AutoService.Services.Supplies;
using UnityEngine;

namespace AutoService.Bootstrap.Installers
{
    /// <summary>Input, the scene's Esc router, pointer raycasts, the character's movement FSM and the camera rig.</summary>
    internal sealed class PlayerInstaller : IGameplayInstaller
    {
        /// <summary>Gameplay input, or null when the player module was skipped.</summary>
        public GameplayInput Input { get; private set; }

        /// <summary>
        /// The scene's Esc router (open panels register in it, the pause menu takes the rest), or null when the player
        /// module was skipped. Also registered in the container for the HUD.
        /// </summary>
        public EscapeRouter Escape { get; private set; }

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

            var escape = new EscapeRouter();
            context.Register(escape);
            context.Register(new EscapeInputBinding(input, escape));
            Escape = escape;

            var raycaster = new PointerRaycaster(scene.Camera, scene.InteractableMask, scene.GroundMask, scene.OccluderMask);
            context.Register(new PlayerMotor(scene.Player), TickPhase.Input);
            context.Register(
                new PlayerInputPresenter(input, raycaster, scene.Player, context.Resolve<IPauseService>(), scene.ClickMarker),
                TickPhase.Input);

            var animator = scene.Player.GetComponentInChildren<Animator>();
            if (animator != null)
            {
                var charAnim = new CharacterAnimator(animator, scene.Player.Agent);
                context.Register(new PlayerAnimatorPresenter(charAnim, context.Resolve<IPlayerCarry>()), TickPhase.Presentation);
            }

            scene.CameraRig.Construct(input, scene.Player);
        }
    }
}
