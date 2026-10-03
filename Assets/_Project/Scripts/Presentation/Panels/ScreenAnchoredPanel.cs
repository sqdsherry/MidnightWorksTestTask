using UnityEngine;

namespace AutoService.Presentation.Panels
{
    /// <summary>
    /// Screen-space panel that sticks next to a world object and stays inside the screen: shows/hides its root, follows
    /// the object's screen point and hides itself while the object is behind the camera. Shared by every "panel next to
    /// an object" (build/hire offers, the point panel).
    /// </summary>
    /// <remarks>
    /// Put it on the panel's container (the object that stays active); <see cref="_root"/> is the moved and hidden child.
    /// <see cref="Follow"/> runs every frame while open and allocates nothing.
    /// </remarks>
    public sealed class ScreenAnchoredPanel : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("The panel itself (shown/hidden and moved). Pivot (0, 0) puts it to the right of and above the object.")]
        private RectTransform _root;

        [SerializeField]
        [Tooltip("Offset from the object's screen point, in canvas units (right/up).")]
        private Vector2 _screenOffset = new Vector2(40f, 40f);

        [SerializeField, Min(0f)]
        [Tooltip("Minimum distance to the screen edges, in canvas units.")]
        private float _screenMargin = 16f;

        private Canvas _canvas;
        private bool _onScreen = true;

        /// <summary>True while the panel is shown (and its object is in front of the camera).</summary>
        public bool IsVisible => Root.gameObject.activeInHierarchy;

        private RectTransform Root => _root != null ? _root : (RectTransform)transform;

        // Why: looked up once, on first use — the panel may start inactive, so Awake is not a reliable place.
        private Canvas Canvas => _canvas != null ? _canvas : (_canvas = GetComponentInParent<Canvas>(true));

        /// <summary>Shows the panel however its objects were left in the scene.</summary>
        public void Show()
        {
            _onScreen = true;
            ActivateChainToRoot();
        }

        public void Hide()
        {
            UiVisibility.Hide(Root.gameObject);
        }

        /// <summary>Places the panel next to <paramref name="worldAnchor"/>; hides it while the point is behind the camera.</summary>
        public void Follow(Camera camera, Vector3 worldAnchor)
        {
            Vector3 screen = camera.WorldToScreenPoint(worldAnchor);

            // Why: a point behind the camera projects mirrored onto the screen; the panel would point at nothing.
            bool onScreen = screen.z > 0f;
            SetOnScreen(onScreen);
            if (onScreen)
            {
                SetScreenPosition(new Vector2(screen.x, screen.y));
            }
        }

        private void SetOnScreen(bool onScreen)
        {
            if (onScreen == _onScreen)
            {
                return;
            }

            _onScreen = onScreen;
            if (onScreen)
            {
                ActivateChainToRoot();
            }
            else
            {
                Root.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// Places the panel next to <paramref name="screenPoint"/> (pixels, origin bottom-left), shifted by the offset
        /// and clamped so the whole panel stays inside the canvas.
        /// </summary>
        private void SetScreenPosition(Vector2 screenPoint)
        {
            RectTransform root = Root;
            if (!(root.parent is RectTransform area))
            {
                return;
            }

            // Why: an overlay canvas maps screen pixels without a camera; camera/world canvases need theirs.
            Canvas canvas = Canvas;
            Camera uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(area, screenPoint, uiCamera, out Vector2 local))
            {
                return;
            }

            Rect bounds = area.rect;
            Vector2 size = root.rect.size;
            Vector2 pivot = root.pivot;
            Vector2 position = local + _screenOffset;
            float minX = bounds.xMin + size.x * pivot.x + _screenMargin;
            float maxX = bounds.xMax - size.x * (1f - pivot.x) - _screenMargin;
            float minY = bounds.yMin + size.y * pivot.y + _screenMargin;
            float maxY = bounds.yMax - size.y * (1f - pivot.y) - _screenMargin;

            // Why: on a screen smaller than the panel the min bound wins, so its top-left part stays readable.
            position.x = Mathf.Max(minX, Mathf.Min(position.x, maxX));
            position.y = Mathf.Min(maxY, Mathf.Max(position.y, minY));
            root.localPosition = new Vector3(position.x, position.y, 0f);
        }

        private void ActivateChainToRoot()
        {
            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }

            // Let UiVisibility handle the animation if present
            UiVisibility.ShowChain(Root.gameObject);
        }
    }
}
