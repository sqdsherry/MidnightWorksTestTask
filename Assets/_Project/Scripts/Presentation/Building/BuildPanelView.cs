using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AutoService.Presentation.Building
{
    /// <summary>
    /// Screen-space build panel (one per scene) that sticks next to the plot it describes: name, description, cost,
    /// level requirement and the Build / Close buttons. Passive view: texts come from the presenter, clicks go out as events.
    /// </summary>
    /// <remarks>
    /// Player-facing label formats live here (prefab data), not in the presenter's logic.
    /// <see cref="SetScreenPosition"/> runs every frame while open and allocates nothing.
    /// </remarks>
    public sealed class BuildPanelView : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("The panel itself (shown/hidden and moved). Pivot (0, 0) puts it to the right of and above the plot.")]
        private RectTransform _root;

        [SerializeField]
        private TMP_Text _title;

        [SerializeField]
        private TMP_Text _description;

        [SerializeField]
        private TMP_Text _cost;

        [SerializeField]
        [Tooltip("Level requirement; hidden when the plot is unlocked.")]
        private TMP_Text _requirement;

        [SerializeField]
        private Button _buildButton;

        [SerializeField]
        private TMP_Text _buildButtonLabel;

        [SerializeField]
        private Button _closeButton;

        [SerializeField]
        [Tooltip("Offset from the plot's screen point, in canvas units (right/up).")]
        private Vector2 _screenOffset = new Vector2(40f, 40f);

        [SerializeField, Min(0f)]
        [Tooltip("Minimum distance to the screen edges, in canvas units.")]
        private float _screenMargin = 16f;

        [SerializeField]
        [Tooltip("Build button label when affordable; {0} = price.")]
        private string _buildLabelFormat = "Build {0}";

        [SerializeField]
        [Tooltip("Build button label when the balance is too low; {0} = price.")]
        private string _needLabelFormat = "Need {0}";

        [SerializeField]
        [Tooltip("Build button label when the level requirement is not met.")]
        private string _lockedLabel = "Locked";

        [SerializeField]
        [Tooltip("Requirement text when locked; {0} = level.")]
        private string _requirementFormat = "Requires level {0}";

        private Canvas _canvas;
        private bool _onScreen = true;

        /// <summary>Raised when the Build button is clicked.</summary>
        public event Action BuildClicked;

        /// <summary>Raised when the Close button is clicked.</summary>
        public event Action CloseClicked;

        /// <summary>Build button label when affordable; {0} = price.</summary>
        public string BuildLabelFormat => _buildLabelFormat;

        /// <summary>Build button label when the balance is too low; {0} = price.</summary>
        public string NeedLabelFormat => _needLabelFormat;

        /// <summary>Build button label when locked.</summary>
        public string LockedLabel => _lockedLabel;

        /// <summary>Requirement text when locked; {0} = level.</summary>
        public string RequirementFormat => _requirementFormat;

        private RectTransform Root => _root != null ? _root : (RectTransform)transform;

        private void Awake()
        {
            _canvas = GetComponentInParent<Canvas>();
            if (_buildButton != null)
            {
                _buildButton.onClick.AddListener(OnBuildButton);
            }

            if (_closeButton != null)
            {
                _closeButton.onClick.AddListener(OnCloseButton);
            }

            // Why: no Hide() here — when the panel object itself starts inactive, Awake runs inside Show() and would
            // hide it right away. The presenter hides the panel on start instead.
            // Unity calls Awake once per object lifetime (on its first activation), so the listeners are never doubled.
        }

        private void OnDestroy()
        {
            if (_buildButton != null)
            {
                _buildButton.onClick.RemoveListener(OnBuildButton);
            }

            if (_closeButton != null)
            {
                _closeButton.onClick.RemoveListener(OnCloseButton);
            }
        }

        /// <summary>Fills the texts and shows the panel.</summary>
        /// <param name="title">Buildable name.</param>
        /// <param name="description">Buildable description.</param>
        /// <param name="cost">Formatted price.</param>
        /// <param name="requirement">Requirement text, or null/empty to hide it.</param>
        public void Show(string title, string description, string cost, string requirement)
        {
            SetText(_title, title);
            SetText(_description, description);
            SetText(_cost, cost);
            SetText(_requirement, requirement);
            if (_requirement != null)
            {
                _requirement.gameObject.SetActive(!string.IsNullOrEmpty(requirement));
            }

            _onScreen = true;
            ActivateChainToRoot();
        }

        /// <summary>Hides the panel (only its root; this container stays active).</summary>
        public void Hide()
        {
            // Why: the container must stay active, otherwise Awake (button listeners, the canvas lookup) would not run
            // before the next Show and SetScreenPosition would work without a canvas.
            Root.gameObject.SetActive(false);
        }

        /// <summary>Enables the Build button and sets its label.</summary>
        public void SetAffordable(bool affordable, string label)
        {
            if (_buildButton != null)
            {
                _buildButton.interactable = affordable;
            }

            SetText(_buildButtonLabel, label);
        }

        /// <summary>Temporarily hides the open panel while its plot is behind the camera, or shows it again.</summary>
        public void SetOnScreen(bool onScreen)
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
        public void SetScreenPosition(Vector2 screenPoint)
        {
            RectTransform root = Root;
            if (!(root.parent is RectTransform area))
            {
                return;
            }

            // Why: an overlay canvas maps screen pixels without a camera; camera/world canvases need theirs.
            Camera uiCamera = _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay ? _canvas.worldCamera : null;
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

        // Why: the panel must show however the objects were left in the scene — this view, the root and every object
        // between them are switched on (activating this object first also runs Awake if it never ran).
        private void ActivateChainToRoot()
        {
            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }

            for (Transform current = Root; current != null && current != transform; current = current.parent)
            {
                if (!current.gameObject.activeSelf)
                {
                    current.gameObject.SetActive(true);
                }
            }
        }

        private void OnBuildButton() => BuildClicked?.Invoke();

        private void OnCloseButton() => CloseClicked?.Invoke();

        private static void SetText(TMP_Text label, string text)
        {
            if (label != null)
            {
                label.text = text ?? string.Empty;
            }
        }
    }
}
