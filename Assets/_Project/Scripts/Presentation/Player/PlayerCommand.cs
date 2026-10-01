using AutoService.Presentation.Interaction;
using UnityEngine;

namespace AutoService.Presentation.Player
{
    /// <summary>A validated movement command from <see cref="PlayerView"/> to <see cref="PlayerMotor"/>.</summary>
    /// <remarks>A struct so raising it through <c>Action&lt;PlayerCommand&gt;</c> does not allocate.</remarks>
    internal readonly struct PlayerCommand
    {
        private PlayerCommand(PlayerCommandKind kind, Vector3 destination, IInteractable target)
        {
            Kind = kind;
            Destination = destination;
            Target = target;
        }

        /// <summary>What to do.</summary>
        public PlayerCommandKind Kind { get; }

        /// <summary>Destination already snapped to the NavMesh (unused for <see cref="PlayerCommandKind.Stop"/>).</summary>
        public Vector3 Destination { get; }

        /// <summary>The interactable for <see cref="PlayerCommandKind.Approach"/>; otherwise null.</summary>
        public IInteractable Target { get; }

        /// <summary>Creates a "walk to point" command.</summary>
        public static PlayerCommand MoveTo(Vector3 destination) => new PlayerCommand(PlayerCommandKind.MoveTo, destination, null);

        /// <summary>Creates a "walk to target and interact" command.</summary>
        public static PlayerCommand Approach(Vector3 destination, IInteractable target) =>
            new PlayerCommand(PlayerCommandKind.Approach, destination, target);

        /// <summary>Creates a "stop" command.</summary>
        public static PlayerCommand Stop() => new PlayerCommand(PlayerCommandKind.Stop, Vector3.zero, null);
    }
}
