using UnityEngine;

namespace AutoService.Presentation.Supplies
{
    /// <summary>
    /// The box in someone's hands (the player, the storekeeper): a renderer that is shown in the consumable's color or
    /// hidden. Color goes through one cached <see cref="MaterialPropertyBlock"/>, so no material is ever instanced.
    /// </summary>
    public sealed class CarriedBoxView
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private readonly Renderer _renderer;
        private readonly MaterialPropertyBlock _block = new MaterialPropertyBlock();

        /// <summary>Wraps <paramref name="renderer"/> (may be null: nothing is shown then) and hides it.</summary>
        public CarriedBoxView(Renderer renderer)
        {
            _renderer = renderer;
            Hide();
        }

        /// <summary>True if the box is currently being shown.</summary>
        public bool IsVisible => _renderer != null && _renderer.gameObject.activeSelf;

        /// <summary>Shows the box in <paramref name="color"/>.</summary>
        public void Show(Color color)
        {
            if (_renderer == null)
            {
                return;
            }

            _block.Clear();
            _block.SetColor(BaseColorId, color);
            _renderer.SetPropertyBlock(_block);
            if (!_renderer.gameObject.activeSelf)
            {
                _renderer.gameObject.SetActive(true);
            }
        }

        /// <summary>Hides the box.</summary>
        public void Hide()
        {
            if (_renderer != null && _renderer.gameObject.activeSelf)
            {
                _renderer.gameObject.SetActive(false);
            }
        }
    }
}
