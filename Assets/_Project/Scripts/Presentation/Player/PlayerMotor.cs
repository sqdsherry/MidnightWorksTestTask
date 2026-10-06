using System;
using AutoService.Presentation.Interaction;
using AutoService.Services.Core;
using UnityEngine;
using UnityEngine.AI;

namespace AutoService.Presentation.Player
{
    /// <summary>
    /// Movement FSM of the character (<see cref="PlayerMotionState"/>): executes <see cref="PlayerView"/> commands
    /// and, every tick, checks arrival, turns to the target and begins the interaction.
    /// </summary>
    /// <remarks>
    /// All state changes go through <see cref="TransitionTo"/>, which is also the only place that calls
    /// <see cref="IInteractable.BeginInteraction"/> / <see cref="IInteractable.EndInteraction"/>, so the two always pair up.
    /// Ticked by the game loop with scaled time: while paused the character neither turns nor starts interacting.
    /// </remarks>
    public sealed class PlayerMotor : ITickable, IDisposable
    {
        private const float FacingToleranceDegrees = 0.5f;

        private readonly PlayerView _view;
        private readonly NavMeshAgent _agent;
        private readonly Transform _transform;
        private bool _disposed;

        /// <summary>Creates the motor and starts listening to <paramref name="view"/>'s commands.</summary>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="view"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the view has no <see cref="NavMeshAgent"/> assigned.</exception>
        public PlayerMotor(PlayerView view)
        {
            if (view == null)
            {
                throw new ArgumentNullException(nameof(view));
            }

            if (view.Agent == null)
            {
                throw new ArgumentException("PlayerView '" + view.name + "' has no NavMeshAgent assigned.", nameof(view));
            }

            _view = view;
            _agent = view.Agent;
            _transform = view.transform;
            _view.CommandRequested += OnCommandRequested;
        }

        /// <inheritdoc />
        public void Tick(float deltaTime)
        {
            switch (_view.State)
            {
                case PlayerMotionState.MovingToPoint:
                    TickMoving(PlayerMotionState.Idle);
                    break;
                case PlayerMotionState.MovingToTarget:
                    if (!_view.CurrentTarget.IsAlive())
                    {
                        StopAgent();
                        TransitionTo(PlayerMotionState.Idle, null);
                        break;
                    }

                    TickMoving(PlayerMotionState.Turning);
                    break;
                case PlayerMotionState.Turning:
                    TickTurning(deltaTime);
                    break;
            }
        }

        /// <summary>Stops listening to the view's commands. Safe to call repeatedly.</summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _view.CommandRequested -= OnCommandRequested;
        }

        private void OnCommandRequested(PlayerCommand command)
        {
            if (command.Kind == PlayerCommandKind.Stop)
            {
                StopAgent();
                TransitionTo(PlayerMotionState.Idle, null);
                return;
            }

            if (!_agent.isActiveAndEnabled || !_agent.isOnNavMesh || !_agent.SetDestination(command.Destination))
            {
                Debug.LogWarning("[PlayerMotor] Cannot set destination " + command.Destination + "; is the agent placed on a baked NavMesh?", _view);
                StopAgent();
                TransitionTo(PlayerMotionState.Idle, null);
                return;
            }

            if (command.Kind == PlayerCommandKind.Approach)
            {
                TransitionTo(PlayerMotionState.MovingToTarget, command.Target);
            }
            else
            {
                TransitionTo(PlayerMotionState.MovingToPoint, null);
            }
        }

        /// <summary>Stops on an unreachable destination; enters <paramref name="arrivedState"/> once the destination is reached.</summary>
        /// <param name="arrivedState">State to enter on arrival (target is kept).</param>
        private void TickMoving(PlayerMotionState arrivedState)
        {
            if (!_agent.isOnNavMesh)
            {
                TransitionTo(PlayerMotionState.Idle, null);
                return;
            }

            if (_agent.pathPending)
            {
                return;
            }

            if (_agent.pathStatus == NavMeshPathStatus.PathInvalid)
            {
                // Why: an invalid path means no route can be calculated at all (e.g. completely disconnected mesh).
                StopAgent();
                TransitionTo(PlayerMotionState.Idle, null);
                return;
            }

            if (_agent.remainingDistance <= _agent.stoppingDistance + _view.ArrivalTolerance)
            {
                StopAgent();
                TransitionTo(arrivedState, _view.CurrentTarget);
            }
        }

        private void TickTurning(float deltaTime)
        {
            IInteractable target = _view.CurrentTarget;
            if (!target.IsAlive())
            {
                TransitionTo(PlayerMotionState.Idle, null);
                return;
            }

            Quaternion goal = YawOnly(target.ApproachRotation, _transform.rotation);
            Quaternion rotation = Quaternion.RotateTowards(_transform.rotation, goal, _view.TurnSpeed * deltaTime);
            if (Quaternion.Angle(rotation, goal) <= FacingToleranceDegrees)
            {
                _transform.rotation = goal;
                TransitionTo(PlayerMotionState.Interacting, target);
                return;
            }

            _transform.rotation = rotation;
        }

        /// <summary>The single place where the FSM changes state.</summary>
        private void TransitionTo(PlayerMotionState next, IInteractable target)
        {
            PlayerMotionState previous = _view.State;
            IInteractable previousTarget = _view.CurrentTarget;

            // Why: any command leaving an active interaction must end it first (GDD §4.1: a new click interrupts).
            if (previous == PlayerMotionState.Interacting)
            {
                if (previousTarget.IsAlive())
                {
                    previousTarget.EndInteraction();
                }

                _view.RaiseInteractionEnded(previousTarget);
            }

            _view.State = next;
            _view.CurrentTarget = target;

            // Why: while walking the agent faces its velocity; once arrived the motor owns the rotation,
            // otherwise the agent could fight the turn towards the target.
            _agent.updateRotation = next == PlayerMotionState.MovingToPoint || next == PlayerMotionState.MovingToTarget;

            if (next == PlayerMotionState.Interacting)
            {
                target.BeginInteraction();
                _view.RaiseInteractionStarted(target);
            }
        }

        private void StopAgent()
        {
            if (_agent.isActiveAndEnabled && _agent.isOnNavMesh)
            {
                _agent.ResetPath();
            }
        }

        // Why: approach points may be tilted in the scene; the character only ever rotates around the vertical axis.
        private static Quaternion YawOnly(Quaternion rotation, Quaternion fallback)
        {
            Vector3 forward = rotation * Vector3.forward;
            forward.y = 0f;
            return forward.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(forward) : fallback;
        }
    }
}
