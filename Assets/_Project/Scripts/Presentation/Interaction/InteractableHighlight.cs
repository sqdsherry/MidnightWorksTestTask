using UnityEngine;

namespace AutoService.Presentation.Interaction
{
    /// <summary>
    /// Reusable hover/selection highlight for interactable objects. Drives one material property
    /// (see <see cref="HighlightProperty"/>) through a single cached <see cref="MaterialPropertyBlock"/>.
    /// </summary>
    /// <remarks>
    /// Why a property block: it never creates material instances (no leaks, shared materials stay shared).
    /// Renderers with a block opt out of SRP Batcher batching only while highlighted, which is negligible for a few objects.
    /// </remarks>
    public sealed class InteractableHighlight : MonoBehaviour
    {
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField]
        [Tooltip("Renderers that light up together.")]
        private Renderer[] _renderers = new Renderer[0];

        [SerializeField]
        [Tooltip("Default highlight color (hover).")]
        private Color _highlightColor = new Color(0.6f, 1f, 0.4f, 1f);

        [SerializeField]
        [Range(0f, 1f)]
        [Tooltip("Emission: multiplier of the color. Tint: how far the base color moves towards the highlight color.")]
        private float _intensity = 0.6f;

        [SerializeField]
        [Tooltip("Emission needs Emission enabled on the material; Base Color Tint works with any URP material.")]
        private HighlightProperty _property = HighlightProperty.BaseColorTint;

        private MaterialPropertyBlock _block;

        // Why: cached once — the tint lerps from the material's own base color, and reading it per call would touch materials.
        private Color[] _baseColors;
        private Color? _overrideBaseColor;
        private bool _isHighlighted;

        /// <summary>The default highlight color used by <see cref="SetHighlighted(bool)"/>.</summary>
        public Color HighlightColor => _highlightColor;

        public void SetOverrideBaseColor(Color? color)
        {
            _overrideBaseColor = color;
            SetHighlighted(_isHighlighted); // Re-render the current state.
        }

        private void Awake()
        {
            EnsureRenderers();
        }

        private void EnsureRenderers()
        {
            if (_block == null)
            {
                _block = new MaterialPropertyBlock();
            }

            bool hasValid = false;
            if (_renderers != null && _renderers.Length > 0)
            {
                for (int i = 0; i < _renderers.Length; i++)
                {
                    if (_renderers[i] != null && _renderers[i].gameObject.activeInHierarchy)
                    {
                        hasValid = true;
                        break;
                    }
                }
            }

            if (!hasValid)
            {
                var found = GetComponentsInChildren<MeshRenderer>(false);
                if (found != null && found.Length > 0)
                {
                    _renderers = found;
                }
            }

            _baseColors = new Color[_renderers != null ? _renderers.Length : 0];
            for (int i = 0; i < _baseColors.Length; i++)
            {
                Material material = _renderers[i] != null ? _renderers[i].sharedMaterial : null;
                _baseColors[i] = material != null && material.HasProperty(BaseColorId) ? material.GetColor(BaseColorId) : Color.white;
            }
        }

        /// <summary>Turns the highlight on with the default color, or off.</summary>
        public void SetHighlighted(bool highlighted) => SetHighlighted(highlighted, _highlightColor);

        /// <summary>Turns the highlight on with <paramref name="color"/>, or off (the color is then ignored).</summary>
        public void SetHighlighted(bool highlighted, Color color)
        {
            if (_block == null || _baseColors == null || _baseColors.Length != (_renderers != null ? _renderers.Length : 0))
            {
                EnsureRenderers();
            }

            _isHighlighted = highlighted;

            for (int i = 0; i < _renderers.Length; i++)
            {
                Renderer target = _renderers[i];
                if (target == null)
                {
                    continue;
                }

                if (!highlighted && _overrideBaseColor == null)
                {
                    // No hover and no color override: back to the default material.
                    target.SetPropertyBlock(null);
                    continue;
                }

                _block.Clear();
                if (highlighted)
                {
                    if (_property == HighlightProperty.Emission)
                    {
                        _block.SetColor(EmissionColorId, color * _intensity);
                    }
                    else
                    {
                        Color baseCol = _overrideBaseColor ?? _baseColors[i];
                        // A locked (yellow) object brightens towards white on hover instead of turning green.
                        Color highlightCol = _overrideBaseColor.HasValue ? Color.white : color;
                        _block.SetColor(BaseColorId, Color.Lerp(baseCol, highlightCol, _intensity));
                    }
                }
                else if (_overrideBaseColor.HasValue)
                {
                    // No hover, but a color override is set (yellow ghost).
                    _block.SetColor(BaseColorId, _overrideBaseColor.Value);
                }

                target.SetPropertyBlock(_block);
            }
        }
    }
}
