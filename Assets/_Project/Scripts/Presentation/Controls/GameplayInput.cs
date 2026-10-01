using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AutoService.Presentation.Controls
{
    /// <summary>
    /// Thin wrapper over the <c>Gameplay</c> action map of the game's <see cref="InputActionAsset"/>.
    /// Exposes continuous values as properties (poll them in a tick) and button presses as events.
    /// </summary>
    /// <remarks>
    /// Actions are looked up by name once in the constructor, so a renamed or missing action fails fast at startup
    /// instead of silently doing nothing. Reading the properties does not allocate.
    /// </remarks>
    public sealed class GameplayInput : IDisposable
    {
        private const string MapName = "Gameplay";
        private const string PointActionName = "Point";
        private const string ClickActionName = "Click";
        private const string PanActionName = "Pan";
        private const string ZoomActionName = "Zoom";
        private const string RecenterActionName = "Recenter";
        private const string CancelActionName = "Cancel";

        private readonly InputActionMap _map;
        private readonly InputAction _point;
        private readonly InputAction _click;
        private readonly InputAction _pan;
        private readonly InputAction _zoom;
        private readonly InputAction _recenter;
        private readonly InputAction _cancel;
        private bool _disposed;

        /// <summary>Finds the <c>Gameplay</c> map and all of its actions and subscribes to the button actions.</summary>
        /// <param name="asset">The game's input actions asset.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="asset"/> is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the map or any action is missing; the message names it.</exception>
        public GameplayInput(InputActionAsset asset)
        {
            if (asset == null)
            {
                throw new ArgumentNullException(nameof(asset));
            }

            _map = asset.FindActionMap(MapName, throwIfNotFound: false);
            if (_map == null)
            {
                throw new InvalidOperationException("Input action map '" + MapName + "' was not found in '" + asset.name + "'.");
            }

            _point = FindAction(PointActionName);
            _click = FindAction(ClickActionName);
            _pan = FindAction(PanActionName);
            _zoom = FindAction(ZoomActionName);
            _recenter = FindAction(RecenterActionName);
            _cancel = FindAction(CancelActionName);

            _click.performed += OnClickPerformed;
            _recenter.performed += OnRecenterPerformed;
            _cancel.performed += OnCancelPerformed;
        }

        /// <summary>Raised when the left mouse button is pressed.</summary>
        public event Action Clicked;

        /// <summary>Raised when the "return camera to the character" key is pressed.</summary>
        public event Action RecenterPressed;

        /// <summary>Raised when the cancel key (Esc) is pressed.</summary>
        public event Action CancelPressed;

        /// <summary>Pointer position in screen pixels (origin bottom-left).</summary>
        public Vector2 PointerPosition => _point.ReadValue<Vector2>();

        /// <summary>Keyboard camera pan direction, each axis in -1..1 (x = right, y = forward).</summary>
        public Vector2 Pan => _pan.ReadValue<Vector2>();

        /// <summary>Raw mouse wheel value for the current frame; positive = scrolled up, 0 when the wheel is idle.</summary>
        /// <remarks>The magnitude per notch is platform/settings dependent, so consumers should use only its sign.</remarks>
        public float Zoom => _zoom.ReadValue<float>();

        /// <summary>Enables the action map. Values read as zero and events are not raised while disabled.</summary>
        public void Enable()
        {
            if (!_disposed)
            {
                _map.Enable();
            }
        }

        /// <summary>Disables the action map.</summary>
        public void Disable() => _map.Disable();

        /// <summary>Unsubscribes from the actions and disables the map. Safe to call repeatedly.</summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _click.performed -= OnClickPerformed;
            _recenter.performed -= OnRecenterPerformed;
            _cancel.performed -= OnCancelPerformed;

            // Why: the asset is shared project data; leaving its map enabled would keep it live after the scene unloads
            // (and across Play Mode sessions in the Editor).
            _map.Disable();
        }

        private InputAction FindAction(string actionName)
        {
            InputAction action = _map.FindAction(actionName, throwIfNotFound: false);
            if (action == null)
            {
                throw new InvalidOperationException("Input action '" + MapName + "/" + actionName + "' was not found.");
            }

            return action;
        }

        private void OnClickPerformed(InputAction.CallbackContext context) => Clicked?.Invoke();

        private void OnRecenterPerformed(InputAction.CallbackContext context) => RecenterPressed?.Invoke();

        private void OnCancelPerformed(InputAction.CallbackContext context) => CancelPressed?.Invoke();
    }
}
