using System;
using AutoService.Presentation.Controls;
using AutoService.Presentation.Interaction;
using AutoService.Services.Core;
using UnityEngine;
using UnityEngine.EventSystems;

namespace AutoService.Presentation.Player
{
    /// <summary>
    /// Turns pointer input into character commands: hover highlights interactables, a click on an interactable
    /// approaches it, a click on the ground walks there. Input is ignored while paused or when the pointer is over UI.
    /// </summary>
    public sealed class PlayerInputPresenter : ITickable, IDisposable
    {
        private readonly GameplayInput _input;
        private readonly PointerRaycaster _raycaster;
        private readonly PlayerView _player;
        private readonly IPauseService _pause;
        private readonly ClickMarkerView _clickMarker;

        private IInteractable _hovered;
        private bool _clickPending;
        private bool _disposed;

        /// <summary>Creates the presenter and subscribes to clicks.</summary>
        /// <param name="input">Gameplay input.</param>
        /// <param name="raycaster">Pointer raycaster for the gameplay camera.</param>
        /// <param name="player">The character to command.</param>
        /// <param name="pause">Pause state; input is ignored while paused.</param>
        /// <param name="clickMarker">Optional ground click feedback; may be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when a required dependency is null.</exception>
        public PlayerInputPresenter(
            GameplayInput input,
            PointerRaycaster raycaster,
            PlayerView player,
            IPauseService pause,
            ClickMarkerView clickMarker)
        {
            _input = input ?? throw new ArgumentNullException(nameof(input));
            _raycaster = raycaster ?? throw new ArgumentNullException(nameof(raycaster));
            _player = player != null ? player : throw new ArgumentNullException(nameof(player));
            _pause = pause ?? throw new ArgumentNullException(nameof(pause));
            _clickMarker = clickMarker;

            _input.Clicked += OnClicked;
        }

        /// <inheritdoc />
        public void Tick(float deltaTime)
        {
            bool blocked = _pause.IsPaused || IsPointerOverUi();

            IInteractable underPointer = null;
            Vector2 pointer = _input.PointerPosition;
            if (!blocked && _raycaster.TryGetInteractable(pointer, out IInteractable found) && found.IsInteractable)
            {
                underPointer = found;
            }

            SetHovered(underPointer);

            if (_clickPending)
            {
                _clickPending = false;
                if (!blocked)
                {
                    HandleClick(underPointer, pointer);
                }
            }
        }

        /// <summary>Unsubscribes from input and removes the hover highlight. Safe to call repeatedly.</summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _input.Clicked -= OnClicked;
            SetHovered(null);
        }

        // Why: the click is only recorded here and handled in Tick of the same frame. Querying the EventSystem from inside
        // an input callback reads last frame's UI state (Input System UI module warns about it), and Tick needs
        // the hover raycast anyway.
        private void OnClicked() => _clickPending = true;

        private void HandleClick(IInteractable underPointer, Vector2 pointer)
        {
            if (underPointer != null)
            {
                _player.ApproachAndInteract(underPointer);
                return;
            }

            if (_raycaster.TryGetGroundPoint(pointer, out Vector3 point))
            {
                _player.MoveTo(point);
                if (_clickMarker != null)
                {
                    _clickMarker.Show(point);
                }
            }
        }

        private void SetHovered(IInteractable next)
        {
            if (ReferenceEquals(next, _hovered))
            {
                return;
            }

            if (_hovered.IsAlive())
            {
                _hovered.SetHighlighted(false);
            }

            _hovered = next;
            if (_hovered != null)
            {
                _hovered.SetHighlighted(true);
            }
        }

        private static bool IsPointerOverUi()
        {
            EventSystem eventSystem = EventSystem.current;
            return eventSystem != null && eventSystem.IsPointerOverGameObject();
        }
    }
}
