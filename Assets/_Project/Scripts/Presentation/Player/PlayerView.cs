using System;
using AutoService.Presentation.Interaction;
using UnityEngine;
using UnityEngine.AI;

namespace AutoService.Presentation.Player
{
    /// <summary>
    /// The player character: Unity-side references plus the public command API (click-to-move, approach-and-interact).
    /// </summary>
    /// <remarks>
    /// Split of responsibilities:
    /// <list type="bullet">
    /// <item><see cref="PlayerView"/> validates commands (snaps positions to the NavMesh) and exposes read-only state and events.</item>
    /// <item><see cref="PlayerMotor"/> owns the movement FSM: it executes the commands, checks arrival and turning every tick
    /// and is the only writer of <see cref="State"/> / <see cref="CurrentTarget"/>.</item>
    /// </list>
    /// Why: the view has no <c>Update</c> — all per-frame work runs through the game loop.
    /// Commands do nothing until a <see cref="PlayerMotor"/> is created for this view.
    /// </remarks>
    public sealed class PlayerView : MonoBehaviour
    {
        // Why: clicks land on the ground/object surface, which can be slightly off the baked NavMesh (edges, object footprints).
        private const float NavMeshSampleRadius = 1f;

        [SerializeField]
        [Tooltip("Agent that moves the character.")]
        private NavMeshAgent _agent;

        [SerializeField]
        [Min(0f)]
        [Tooltip("Extra distance on top of the agent's Stopping Distance at which the destination counts as reached (m).")]
        private float _arrivalTolerance = 0.15f;

        [SerializeField]
        [Min(1f)]
        [Tooltip("Turn speed towards the interactable after arriving (degrees per second).")]
        private float _turnSpeed = 720f;

        /// <summary>Raised when the character starts interacting with a target (after arriving and turning).</summary>
        public event Action<IInteractable> InteractionStarted;

        /// <summary>Raised when an interaction that had started ends because of a new command.</summary>
        public event Action<IInteractable> InteractionEnded;

        /// <summary>
        /// Raised after a <see cref="MoveTo"/> or <see cref="ApproachAndInteract"/> command was accepted
        /// (e.g. the camera resumes following the character).
        /// </summary>
        public event Action CommandIssued;

        /// <summary>Raised for every accepted command; consumed by <see cref="PlayerMotor"/>.</summary>
        internal event Action<PlayerCommand> CommandRequested;

        /// <summary>The agent that moves the character (may be null if not assigned in the Inspector).</summary>
        public NavMeshAgent Agent => _agent;

        /// <summary>Current state of the movement FSM.</summary>
        public PlayerMotionState State { get; internal set; }

        /// <summary>True while walking (to a point or to a target). For animation later.</summary>
        public bool IsMoving => State == PlayerMotionState.MovingToPoint || State == PlayerMotionState.MovingToTarget;

        /// <summary>The interactable being approached or interacted with; null otherwise.</summary>
        public IInteractable CurrentTarget { get; internal set; }

        /// <summary>True while interacting with <see cref="CurrentTarget"/>.</summary>
        public bool IsInteracting => State == PlayerMotionState.Interacting;

        /// <summary>Arrival tolerance on top of the agent's stopping distance, in meters.</summary>
        internal float ArrivalTolerance => _arrivalTolerance;

        /// <summary>Turn speed after arrival, in degrees per second.</summary>
        internal float TurnSpeed => _turnSpeed;

        /// <summary>Walks to <paramref name="worldPosition"/>. Interrupts the current interaction.</summary>
        /// <remarks>Ignored if there is no NavMesh within ~1 m of the position.</remarks>
        public void MoveTo(Vector3 worldPosition)
        {
            if (!TrySampleNavMesh(worldPosition, out Vector3 destination))
            {
                return;
            }

            Issue(PlayerCommand.MoveTo(destination));
        }

        /// <summary>
        /// Walks to <paramref name="target"/>'s approach position, turns to its approach rotation and begins the interaction.
        /// Interrupts the current interaction.
        /// </summary>
        /// <remarks>
        /// Ignored if the target is null/destroyed, not interactable, already being interacted with,
        /// or its approach position is not near the NavMesh.
        /// </remarks>
        public void ApproachAndInteract(IInteractable target)
        {
            if (!target.IsAlive() || !target.IsInteractable)
            {
                return;
            }

            if (IsInteracting && ReferenceEquals(CurrentTarget, target))
            {
                return;
            }

            if (!TrySampleNavMesh(target.ApproachPosition, out Vector3 destination))
            {
                return;
            }

            Issue(PlayerCommand.Approach(destination, target));
        }

        /// <summary>Stops where the character stands and ends any interaction.</summary>
        public void Stop() => CommandRequested?.Invoke(PlayerCommand.Stop());

        /// <summary>Raises <see cref="InteractionStarted"/>. Called by <see cref="PlayerMotor"/>.</summary>
        internal void RaiseInteractionStarted(IInteractable target) => InteractionStarted?.Invoke(target);

        /// <summary>Raises <see cref="InteractionEnded"/>. Called by <see cref="PlayerMotor"/>.</summary>
        internal void RaiseInteractionEnded(IInteractable target) => InteractionEnded?.Invoke(target);

        private void Issue(PlayerCommand command)
        {
            CommandRequested?.Invoke(command);
            CommandIssued?.Invoke();
        }

        private bool TrySampleNavMesh(Vector3 position, out Vector3 sampled)
        {
            int areaMask = _agent != null ? _agent.areaMask : NavMesh.AllAreas;
            if (NavMesh.SamplePosition(position, out NavMeshHit hit, NavMeshSampleRadius, areaMask))
            {
                sampled = hit.position;
                return true;
            }

            sampled = position;
            return false;
        }
    }
}
