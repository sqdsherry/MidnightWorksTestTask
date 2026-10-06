using System.Collections;
using UnityEngine;

namespace AutoService.Presentation.Player
{
    /// <summary>
    /// Ground click feedback: a single reusable ring/disc that pops at the clicked point and fades out.
    /// </summary>
    /// <remarks>
    /// Runs on unscaled time so it still plays while the game is paused. The fade drives the alpha of <c>_BaseColor</c>
    /// through a <see cref="MaterialPropertyBlock"/>, so the material must be transparent to actually fade.
    /// </remarks>
    public sealed class ClickMarkerView : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField]
        [Tooltip("Renderer of the marker; hidden while the marker is idle.")]
        private Renderer _renderer;

        [SerializeField]
        [Tooltip("Scale multiplier over normalized time (0..1).")]
        private AnimationCurve _scaleCurve = new AnimationCurve(
            new Keyframe(0f, 0.3f), new Keyframe(0.3f, 1.1f), new Keyframe(1f, 1f));

        [SerializeField]
        [Tooltip("Alpha over normalized time (0..1).")]
        private AnimationCurve _alphaCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);

        [SerializeField]
        [Min(0.01f)]
        [Tooltip("Animation length in seconds (unscaled).")]
        private float _duration = 0.4f;

        [SerializeField]
        [Tooltip("Lift above the clicked point to avoid z-fighting with the ground (m).")]
        private float _heightOffset = 0.02f;

        private MaterialPropertyBlock _block;
        private Vector3 _baseScale;
        private Color _baseColor = Color.white;
        private Coroutine _animation;

        /// <summary>Moves the marker to <paramref name="worldPosition"/> and (re)starts its animation.</summary>
        public void Show(Vector3 worldPosition)
        {
            if (_renderer == null || !isActiveAndEnabled)
            {
                return;
            }

            if (_animation != null)
            {
                StopCoroutine(_animation);
            }

            transform.position = worldPosition + Vector3.up * _heightOffset;
            _renderer.enabled = true;
            _animation = StartCoroutine(Animate());
        }

        private void Awake()
        {
            _block = new MaterialPropertyBlock();
            _baseScale = transform.localScale;

            if (_renderer != null)
            {
                Material material = _renderer.sharedMaterial;
                if (material != null && material.HasProperty(BaseColorId))
                {
                    _baseColor = material.GetColor(BaseColorId);
                }

                _renderer.enabled = false;
            }
        }

        private void OnDisable()
        {
            // Why: Unity stops coroutines on disable; reset so the marker does not stay frozen mid-animation.
            _animation = null;
            Hide();
        }

        // Why: `yield return null` (next frame) allocates nothing, unlike a fresh WaitForSeconds per loop;
        // the only allocation is the enumerator once per click.
        private IEnumerator Animate()
        {
            float elapsed = 0f;
            while (elapsed < _duration)
            {
                Apply(elapsed / _duration);
                yield return null;
                elapsed += Time.unscaledDeltaTime;
            }

            Hide();
            _animation = null;
        }

        private void Apply(float normalizedTime)
        {
            transform.localScale = _baseScale * _scaleCurve.Evaluate(normalizedTime);

            Color color = _baseColor;
            color.a *= _alphaCurve.Evaluate(normalizedTime);
            _block.SetColor(BaseColorId, color);
            _renderer.SetPropertyBlock(_block);
        }

        private void Hide()
        {
            transform.localScale = _baseScale;
            if (_renderer != null)
            {
                _renderer.enabled = false;
                _renderer.SetPropertyBlock(null);
            }
        }
    }
}
