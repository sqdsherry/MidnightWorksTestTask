using System;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace AutoService.Presentation.Panels
{
    /// <summary>
    /// Screen-space "offer" panel that sticks next to an object: title, description, cost, level requirement and one action
    /// button (Build a plot, Hire the storekeeper) plus Close. Passive view: texts come from the presenter, clicks go out as
    /// events. One instance per kind of offer in the scene.
    /// </summary>
    /// <remarks>
    /// Player-facing label formats live here (prefab data), not in the presenters' logic.
    /// Positioning next to the object is done by the <see cref="ScreenAnchoredPanel"/> on the same object.
    /// <para>Renamed from <c>BuildPanelView</c> (the script GUID is kept, so scene references survive); the old field names
    /// are mapped with <see cref="FormerlySerializedAsAttribute"/>.</para>
    /// </remarks>
    public sealed class OfferPanelView : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Shows, hides and moves the panel. Defaults to the component on this object.")]
        private ScreenAnchoredPanel _anchor;

        [SerializeField]
        private TMP_Text _title;

        [SerializeField]
        private TMP_Text _description;

        [SerializeField]
        private TMP_Text _cost;

        [SerializeField]
        [Tooltip("Level requirement; hidden when unlocked.")]
        private TMP_Text _requirement;

        [SerializeField, FormerlySerializedAs("_buildButton")]
        private Button _actionButton;

        [SerializeField, FormerlySerializedAs("_buildButtonLabel")]
        private TMP_Text _actionButtonLabel;

        [SerializeField]
        private Button _closeButton;

        [SerializeField, FormerlySerializedAs("_buildLabelFormat")]
        [Tooltip("Action button label when affordable; {0} = price, {1} = name. E.g. \"Build {0}\", \"Hire {1} {0}\".")]
        private string _actionLabelFormat = "Build {0}";

        [SerializeField]
        [Tooltip("Action button label when the balance is too low; {0} = price.")]
        private string _needLabelFormat = "Need {0}";

        [SerializeField]
        [Tooltip("Action button label when the level requirement is not met.")]
        private string _lockedLabel = "Locked";

        [SerializeField]
        [Tooltip("Action button label once the offer was taken (e.g. \"Hired\").")]
        private string _completedLabel = "Done";

        [SerializeField]
        [Tooltip("Action button label when no more can be taken (e.g. every storekeeper is hired).")]
        private string _maxLabel = "Max staff";

        [SerializeField]
        [Tooltip("Title of an offer taken several times; {0} = name, {1} = taken, {2} = maximum. E.g. \"Storekeepers 1/3\".")]
        private string _countTitleFormat = "{0}s {1}/{2}";

        [SerializeField]
        [Tooltip("Requirement text when locked; {0} = level.")]
        private string _requirementFormat = "Requires level {0}";

        // Why: legacy fields of BuildPanelView, kept only so the A2 setup can read the scene's old values and move them to
        // ScreenAnchoredPanel (a removed field's data cannot be read). Not used at runtime; delete after the migration.
        [SerializeField, HideInInspector]
        private RectTransform _root;

        [SerializeField, HideInInspector]
        private Vector2 _screenOffset = new Vector2(40f, 40f);

        private bool _anchorResolved;

        /// <summary>Raised when the action button is clicked.</summary>
        public event Action ActionClicked;

        /// <summary>Raised when the Close button is clicked.</summary>
        public event Action CloseClicked;

        /// <summary>Action button label when affordable; {0} = price, {1} = name (optional).</summary>
        public string ActionLabelFormat => _actionLabelFormat;

        /// <summary>Action button label when the balance is too low; {0} = price.</summary>
        public string NeedLabelFormat => _needLabelFormat;

        /// <summary>Action button label when locked.</summary>
        public string LockedLabel => _lockedLabel;

        /// <summary>Action button label once the offer was taken.</summary>
        public string CompletedLabel => _completedLabel;

        /// <summary>Action button label when no more can be taken.</summary>
        public string MaxLabel => _maxLabel;

        /// <summary>Title of an offer taken several times; {0} = name, {1} = taken, {2} = maximum.</summary>
        public string CountTitleFormat => _countTitleFormat;

        /// <summary>Requirement text when locked; {0} = level.</summary>
        public string RequirementFormat => _requirementFormat;

        /// <summary>True while the panel is on screen (false while closed or while its object is behind the camera).</summary>
        public bool IsVisible => Anchor != null ? Anchor.IsVisible : gameObject.activeInHierarchy;

        // Why: resolved once, on first use — the presenter may hide the panel before this object ever ran Awake, and a
        // missing component must not be searched for again every frame.
        private ScreenAnchoredPanel Anchor
        {
            get
            {
                if (_anchor == null && !_anchorResolved)
                {
                    _anchorResolved = true;
                    TryGetComponent(out _anchor);
                }

                return _anchor;
            }
        }

        private void Awake()
        {
            if (_actionButton != null)
            {
                _actionButton.onClick.AddListener(OnActionButton);
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
            if (_actionButton != null)
            {
                _actionButton.onClick.RemoveListener(OnActionButton);
            }

            if (_closeButton != null)
            {
                _closeButton.onClick.RemoveListener(OnCloseButton);
            }
        }

        /// <summary>Fills the texts and shows the panel.</summary>
        /// <param name="title">Offer name.</param>
        /// <param name="description">Offer description.</param>
        /// <param name="cost">Formatted price, or null/empty to hide it.</param>
        /// <param name="requirement">Requirement text, or null/empty to hide it.</param>
        public void Show(string title, string description, string cost, string requirement)
        {
            SetText(_title, title);
            SetText(_description, description);
            if (_description != null)
            {
                _description.gameObject.SetActive(false);
            }

            SetText(_cost, cost);
            if (_cost != null)
            {
                _cost.gameObject.SetActive(false);
            }

            SetText(_requirement, requirement);
            if (_requirement != null)
            {
                _requirement.gameObject.SetActive(!string.IsNullOrEmpty(requirement));
            }

            if (Anchor != null)
            {
                Anchor.Show();
            }
        }

        /// <summary>Hides the panel (only its root; this container stays active).</summary>
        public void Hide()
        {
            if (Anchor != null)
            {
                Anchor.Hide();
            }
        }

        /// <summary>Enables the action button and sets its label.</summary>
        public void SetAffordable(bool affordable, string label)
        {
            if (_actionButton != null)
            {
                _actionButton.interactable = affordable;
            }

            SetText(_actionButtonLabel, label);
        }

        /// <summary>Keeps the open panel next to <paramref name="worldAnchor"/> (hidden while it is behind the camera).</summary>
        public void Follow(Camera camera, Vector3 worldAnchor)
        {
            if (Anchor != null)
            {
                Anchor.Follow(camera, worldAnchor);
            }
        }

        private void OnActionButton() => ActionClicked?.Invoke();

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
