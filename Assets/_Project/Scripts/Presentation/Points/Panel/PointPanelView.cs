using System;
using AutoService.Domain.Upgrades;
using AutoService.Presentation.Panels;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AutoService.Presentation.Points.Panel
{
    /// <summary>
    /// Screen-space management panel of a service point (one per scene), opened from the point's blue pad: title, status
    /// (supply, worker), income per minute, the Speed and Price upgrade rows and the hire row. Passive view: texts come
    /// from <see cref="PointPanelPresenter"/>, clicks go out as events.
    /// </summary>
    /// <remarks>
    /// Every player-facing format lives here (prefab data), not in the presenter. Numbers that change while the panel is
    /// open (income) are written with <see cref="TMP_Text.SetText(string, float)"/>, which does not allocate.
    /// </remarks>
    public sealed class PointPanelView : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Shows, hides and moves the panel. Defaults to the component on this object.")]
        private ScreenAnchoredPanel _anchor;

        [SerializeField]
        private TMP_Text _title;

        [SerializeField]
        [Tooltip("Supply and worker line.")]
        private TMP_Text _status;

        [SerializeField]
        private TMP_Text _income;

        [SerializeField]
        private UpgradeRowView _speedRow;

        [SerializeField]
        private UpgradeRowView _priceRow;

        [SerializeField]
        private HireRowView _hireRow;

        [SerializeField]
        private Button _closeButton;

        [Header("Formats")]
        [SerializeField]
        [Tooltip("Income line; {0} = dollars per minute (TMP SetText format, {0:0} = no decimals).")]
        private string _incomeFormat = "${0:0}/min";

        [SerializeField]
        [Tooltip("Supply part of the status; {0} = current, {1} = capacity.")]
        private string _supplyFormat = "Supply {0}/{1}";

        [SerializeField]
        [Tooltip("Worker part of the status; {0} = job title, {1} = hired / not hired text.")]
        private string _workerFormat = "{0}: {1}";

        [SerializeField]
        private string _workerHiredText = "hired";

        [SerializeField]
        private string _workerNotHiredText = "not hired";

        [SerializeField]
        [Tooltip("Between the parts of the status line.")]
        private string _statusSeparator = " · ";

        [SerializeField]
        [Tooltip("Upgrade button when affordable; {0} = price.")]
        private string _upgradeLabelFormat = "Upgrade {0}";

        [SerializeField]
        [Tooltip("Upgrade / hire button when the balance is too low; {0} = price.")]
        private string _needLabelFormat = "Need {0}";

        [SerializeField]
        [Tooltip("Upgrade button at the max level.")]
        private string _maxLabel = "MAX";

        [SerializeField]
        [Tooltip("Upgrade / hire button when the level requirement is not met; {0} = level.")]
        private string _lockedLabelFormat = "Locked (Lv {0})";

        [SerializeField]
        [Tooltip("Hire button when affordable; {0} = job title, {1} = price.")]
        private string _hireLabelFormat = "Hire {0} {1}";

        [SerializeField]
        [Tooltip("Hire button once the worker is hired.")]
        private string _hiredLabel = "Hired";

        private bool _anchorResolved;

        /// <summary>Raised when an upgrade's buy button is clicked.</summary>
        public event Action<UpgradeKind> UpgradeClicked;

        /// <summary>Raised when the hire button is clicked.</summary>
        public event Action HireClicked;

        /// <summary>Raised when the Close button is clicked.</summary>
        public event Action CloseClicked;

        /// <summary>Supply part of the status; {0} = current, {1} = capacity.</summary>
        public string SupplyFormat => _supplyFormat;

        /// <summary>Worker part of the status; {0} = title, {1} = hired text.</summary>
        public string WorkerFormat => _workerFormat;

        /// <summary>"hired" text of the status.</summary>
        public string WorkerHiredText => _workerHiredText;

        /// <summary>"not hired" text of the status.</summary>
        public string WorkerNotHiredText => _workerNotHiredText;

        /// <summary>Between the parts of the status line.</summary>
        public string StatusSeparator => _statusSeparator;

        /// <summary>Upgrade button when affordable; {0} = price.</summary>
        public string UpgradeLabelFormat => _upgradeLabelFormat;

        /// <summary>Button when the balance is too low; {0} = price.</summary>
        public string NeedLabelFormat => _needLabelFormat;

        /// <summary>Upgrade button at the max level.</summary>
        public string MaxLabel => _maxLabel;

        /// <summary>Button when locked; {0} = level.</summary>
        public string LockedLabelFormat => _lockedLabelFormat;

        /// <summary>Hire button when affordable; {0} = title, {1} = price.</summary>
        public string HireLabelFormat => _hireLabelFormat;

        /// <summary>Hire button once hired.</summary>
        public string HiredLabel => _hiredLabel;

        /// <summary>True while the panel is on screen (false while closed or while its object is behind the camera).</summary>
        public bool IsVisible => Anchor != null ? Anchor.IsVisible : gameObject.activeInHierarchy;

        private ScreenAnchoredPanel Anchor
        {
            get
            {
                // Why: resolved once, on first use (the panel may be hidden before it ever ran Awake).
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
            // Why: no Hide() here, see OfferPanelView — the presenter hides the panel on start.
            if (_speedRow != null)
            {
                _speedRow.Clicked += OnSpeedClicked;
            }

            if (_priceRow != null)
            {
                _priceRow.Clicked += OnPriceClicked;
            }

            if (_hireRow != null)
            {
                _hireRow.Clicked += OnHireClicked;
            }

            if (_closeButton != null)
            {
                _closeButton.onClick.AddListener(OnCloseClicked);
            }
        }

        private void OnDestroy()
        {
            if (_speedRow != null)
            {
                _speedRow.Clicked -= OnSpeedClicked;
            }

            if (_priceRow != null)
            {
                _priceRow.Clicked -= OnPriceClicked;
            }

            if (_hireRow != null)
            {
                _hireRow.Clicked -= OnHireClicked;
            }

            if (_closeButton != null)
            {
                _closeButton.onClick.RemoveListener(OnCloseClicked);
            }
        }

        /// <summary>Shows the panel.</summary>
        public void Show()
        {
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

        /// <summary>Keeps the open panel next to <paramref name="worldAnchor"/> (hidden while it is behind the camera).</summary>
        public void Follow(Camera camera, Vector3 worldAnchor)
        {
            if (Anchor != null)
            {
                Anchor.Follow(camera, worldAnchor);
            }
        }

        /// <summary>Writes the title.</summary>
        public void SetTitle(string title) => SetText(_title, title);

        /// <summary>Writes the status line.</summary>
        public void SetStatus(string status) => SetText(_status, status);

        /// <summary>Writes the status line from a cached format and two numbers without allocating.</summary>
        /// <param name="format">TMP SetText format with {0} and {1}.</param>
        /// <param name="arg0">Value of {0}.</param>
        /// <param name="arg1">Value of {1}.</param>
        public void SetStatus(string format, float arg0, float arg1)
        {
            if (_status != null)
            {
                _status.SetText(format, arg0, arg1);
            }
        }

        /// <summary>Writes the income per minute without allocating.</summary>
        public void SetIncome(float dollarsPerMinute)
        {
            if (_income != null)
            {
                _income.SetText(_incomeFormat, dollarsPerMinute);
            }
        }

        /// <summary>The row of <paramref name="kind"/>, or null if not assigned.</summary>
        public UpgradeRowView RowOf(UpgradeKind kind) => kind == UpgradeKind.Speed ? _speedRow : _priceRow;

        /// <summary>The hire row, or null if not assigned.</summary>
        public HireRowView HireRow => _hireRow;

        private void OnSpeedClicked() => UpgradeClicked?.Invoke(UpgradeKind.Speed);

        private void OnPriceClicked() => UpgradeClicked?.Invoke(UpgradeKind.Price);

        private void OnHireClicked() => HireClicked?.Invoke();

        private void OnCloseClicked() => CloseClicked?.Invoke();

        private static void SetText(TMP_Text label, string text)
        {
            if (label != null)
            {
                label.text = text ?? string.Empty;
            }
        }
    }
}
