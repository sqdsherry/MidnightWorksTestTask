using System;
using System.Collections;
using AutoService.Domain.Common;
using AutoService.Presentation.Interaction;
using TMPro;
using UnityEngine;

namespace AutoService.Presentation.Building
{
    /// <summary>
    /// Scene side of one build plot: the translucent ghost with its price tag and dwell ring, the real object that is
    /// switched on once built, and the "walk up and stand" interaction that opens the build panel.
    /// </summary>
    /// <remarks>
    /// The view keeps only presentation state. Whether the plot is built is decided by <c>IBuildService</c>;
    /// <see cref="BuildableBinder"/> calls <see cref="SetBuilt"/>, and <see cref="BuildPanelPresenter"/> ticks the dwell
    /// (<see cref="TickDwell"/>) and listens to <see cref="DwellCompleted"/> / <see cref="Left"/>.
    /// <para>Keep this component on an object that stays active (the ghost is a child): the grow animation runs on it.</para>
    /// </remarks>
    public sealed class BuildPlotView : MonoBehaviour, IInteractable
    {
        [SerializeField]
        [Tooltip("Id of the Buildable asset in GameConfig, e.g. \"loc1_build_oil\".")]
        private string _plotId = string.Empty;

        [SerializeField]
        [Tooltip("Translucent preview with its collider, price tag and ring; hidden once built.")]
        private GameObject _ghost;

        [SerializeField]
        [Tooltip("The real object (a bay with its ServicePointView); inactive until built. May be empty for parking slots.")]
        private GameObject _target;

        [SerializeField]
        [Tooltip("Where the character stands; its forward is the direction they face.")]
        private Transform _approachPoint;

        [SerializeField]
        [Tooltip("World point the build panel sticks to. Defaults to this object.")]
        private Transform _panelAnchor;

        [SerializeField]
        [Tooltip("Optional hover feedback of the ghost.")]
        private InteractableHighlight _highlight;

        [SerializeField]
        [Tooltip("Optional world-space dwell ring.")]
        private DwellRingView _ring;

        [SerializeField]
        [Tooltip("Optional world-space price tag.")]
        private TMP_Text _priceTag;

        [SerializeField]
        [Tooltip("Price tag text; {0} = name, {1} = price.")]
        private string _priceTagFormat = "{0} · {1}";

        [SerializeField, Min(0f)]
        [Tooltip("Seconds the character has to stand at the plot before the build panel opens.")]
        private float _dwellSeconds = 1.5f;

        [SerializeField]
        [Tooltip("Optional particles played when the plot is built (not on load).")]
        private ParticleSystem _buildFx;

        [SerializeField]
        [Tooltip("Scale of the built object over the grow animation (0..1 time), with an overshoot.")]
        private AnimationCurve _growCurve = new AnimationCurve(
            new Keyframe(0f, 0f), new Keyframe(0.6f, 1.15f), new Keyframe(0.8f, 0.95f), new Keyframe(1f, 1f));

        [SerializeField, Min(0.01f)]
        [Tooltip("Seconds of the grow animation (unscaled time).")]
        private float _growDuration = 0.5f;

        private DwellProgress _dwell;
        private bool _built;
        private bool _offered = true;
        private Coroutine _grow;

        /// <summary>Raised once per visit when the character has stood at the plot long enough.</summary>
        public event Action<BuildPlotView> DwellCompleted;

        /// <summary>Raised when the character stops interacting with the plot (walked away / new command).</summary>
        public event Action<BuildPlotView> Left;

        /// <summary>Id of the plot (= the buildable id).</summary>
        public string PlotId => _plotId;

        /// <summary>The real object switched on when built (may be null).</summary>
        public GameObject Target => _target;

        /// <summary>World point the build panel sticks to.</summary>
        public Transform PanelAnchor => _panelAnchor != null ? _panelAnchor : transform;

        /// <summary>True after <see cref="SetBuilt"/>.</summary>
        public bool IsBuilt => _built;

        /// <inheritdoc />
        public Vector3 ApproachPosition => ApproachTransform.position;

        /// <inheritdoc />
        public Quaternion ApproachRotation => ApproachTransform.rotation;

        /// <inheritdoc />
        /// <remarks>Only an unbuilt plot that is currently offered (see <see cref="SetOffered"/>) can be clicked.</remarks>
        public bool IsInteractable => !_built && _offered;

        private Transform ApproachTransform => _approachPoint != null ? _approachPoint : transform;

        // Why: created on first use, not in Awake — the binder may touch the view before it was ever enabled.
        private DwellProgress Dwell => _dwell ??= new DwellProgress(_dwellSeconds);

        /// <inheritdoc />
        public void SetHighlighted(bool highlighted)
        {
            if (_highlight != null)
            {
                _highlight.SetHighlighted(highlighted);
            }
        }

        /// <inheritdoc />
        public void BeginInteraction()
        {
            if (!_built)
            {
                Dwell.Begin();
            }
        }

        /// <inheritdoc />
        public void EndInteraction()
        {
            Dwell.End();
            Left?.Invoke(this);
        }

        /// <summary>Advances the dwell timer and the ring; raises <see cref="DwellCompleted"/> when it fills up.</summary>
        /// <param name="deltaTime">Scaled seconds (0 while paused).</param>
        public void TickDwell(float deltaTime)
        {
            DwellProgress dwell = Dwell;
            bool completed = dwell.Tick(deltaTime);
            if (_ring != null)
            {
                _ring.Render(dwell.Progress01);
            }

            if (completed && !_built)
            {
                DwellCompleted?.Invoke(this);
            }
        }

        /// <summary>Writes "name · price" on the world-space price tag.</summary>
        public void SetPriceTag(string displayName, string price)
        {
            if (_priceTag != null)
            {
                _priceTag.text = string.Format(_priceTagFormat, displayName, price);
            }
        }

        /// <summary>Shows the plot as not built yet: ghost on (if offered), real object off.</summary>
        public void ShowUnbuilt()
        {
            _built = false;
            SetActive(_ghost, _offered);
            SetActive(_target, false);
        }

        /// <summary>
        /// Offers the plot for building or hides its ghost (e.g. parking slot 4 until slot 3 is bought).
        /// Ignored once built.
        /// </summary>
        public void SetOffered(bool offered)
        {
            _offered = offered;
            if (_built)
            {
                return;
            }

            SetActive(_ghost, offered);
            if (!offered)
            {
                Dwell.Reset();
                if (_ring != null)
                {
                    _ring.Render(0f);
                }
            }
        }

        /// <summary>Hides the ghost and switches the real object on.</summary>
        /// <param name="animate">True for a fresh construction (grow + particles), false when restored from a save.</param>
        public void SetGhostLockedVisual(bool isLocked)
        {
            if (_highlight != null)
            {
                _highlight.SetOverrideBaseColor(isLocked ? new Color(1f, 0.9f, 0.1f, 0.4f) : (Color?)null);
            }
        }

        public void SetBuilt(bool animate)
        {
            _built = true;
            Dwell.Reset();
            if (_ring != null)
            {
                _ring.Render(0f);
            }

            SetHighlighted(false);
            SetActive(_ghost, false);
            if (_target == null)
            {
                return;
            }

            _target.SetActive(true);
            if (!animate)
            {
                return;
            }

            if (_buildFx != null)
            {
                _buildFx.Play();
            }

            if (_grow != null)
            {
                StopCoroutine(_grow);
            }

            // Why: a coroutine needs an active object; if this one is off, the object simply appears without the animation.
            if (isActiveAndEnabled)
            {
                _grow = StartCoroutine(Grow(_target.transform));
            }
        }

        private IEnumerator Grow(Transform target)
        {
            Vector3 finalScale = target.localScale;
            float elapsed = 0f;
            while (elapsed < _growDuration)
            {
                target.localScale = finalScale * _growCurve.Evaluate(elapsed / _growDuration);

                // Why: unscaled — the construction is UI feedback and should finish even if the game gets paused.
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            target.localScale = finalScale;
            _grow = null;
        }

        private static void SetActive(GameObject target, bool active)
        {
            if (target != null && target.activeSelf != active)
            {
                target.SetActive(active);
            }
        }

        private void OnDrawGizmos()
        {
            Transform approach = ApproachTransform;
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(approach.position, 0.3f);
            Gizmos.DrawLine(approach.position, approach.position + approach.forward * 0.8f);
        }
    }
}
