using System;
using System.Collections.Generic;
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
        private readonly List<RaycastResult> _uiRaycastResults = new List<RaycastResult>();

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
            bool blocked = _pause.IsPaused || IsPointerOverUi(_input.PointerPosition);

            // Why: one raycast per tick feeds both hover and the click, so they can never disagree about the target.
            PointerHit hit = default;
            bool hasHit = !blocked && _raycaster.TryRaycast(_input.PointerPosition, out hit);

            IInteractable underPointer = null;
            if (hasHit && hit.Interactable != null && hit.Interactable.IsInteractable)
            {
                underPointer = hit.Interactable;
            }

            SetHovered(underPointer);

            if (_clickPending)
            {
                _clickPending = false;
                if (hasHit)
                {
                    HandleClick(underPointer, hit);
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

        // Why: a click on a non-interactable (e.g. locked) object does nothing — the ground behind it is occluded,
        // so the character must not walk "through" the object to the floor point.
        private void HandleClick(IInteractable underPointer, in PointerHit hit)
        {
            if (underPointer != null)
            {
                _player.ApproachAndInteract(underPointer);
                return;
            }

            if (hit.HasGroundPoint)
            {
                _player.MoveTo(hit.GroundPoint);
                if (_clickMarker != null)
                {
                    _clickMarker.Show(hit.GroundPoint);
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

        private bool IsPointerOverUi(Vector2 pointerPosition)
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null || !eventSystem.IsPointerOverGameObject())
            {
                return false;
            }

            // Why: EventSystem.IsPointerOverGameObject() returns true even when the pointer is over World Space canvases
            // (such as in-world floating bay HUDs, indicators, or name plates). Gameplay ground clicks must only be
            // blocked when the pointer is over Screen Space UI (ScreenHud, popups, menus).
            var pointerData = new PointerEventData(eventSystem)
            {
                position = pointerPosition
            };

            _uiRaycastResults.Clear();
            eventSystem.RaycastAll(pointerData, _uiRaycastResults);

            for (int i = 0; i < _uiRaycastResults.Count; i++)
            {
                GameObject go = _uiRaycastResults[i].gameObject;
                if (go == null)
                {
                    continue;
                }

                Canvas canvas = go.GetComponentInParent<Canvas>();
                if (canvas != null && canvas.renderMode != RenderMode.WorldSpace)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
