using System;
using UnityEngine;

namespace AutoService.Presentation.Supplies
{
    /// <summary>
    /// Presentation-side lookup: consumable id → box color and icon. Kept out of the game config so visuals can change
    /// without touching the rules.
    /// </summary>
    [CreateAssetMenu(menuName = "AutoService/Supply Visual Catalog", fileName = "SupplyVisualCatalog")]
    public sealed class SupplyVisualCatalog : ScriptableObject
    {
        [SerializeField]
        [Tooltip("One entry per supply type id.")]
        private Entry[] _entries = new Entry[0];

        [SerializeField]
        [Tooltip("Color of a box whose supply type has no entry.")]
        private Color _fallbackColor = Color.white;

        /// <summary>Box color of <paramref name="supplyTypeId"/>, or the fallback color.</summary>
        public Color ColorOf(string supplyTypeId)
        {
            for (int i = 0; i < _entries.Length; i++)
            {
                if (string.Equals(_entries[i].SupplyTypeId, supplyTypeId, StringComparison.Ordinal))
                {
                    return _entries[i].Color;
                }
            }

            return _fallbackColor;
        }

        /// <summary>Icon of <paramref name="supplyTypeId"/>, or null.</summary>
        public Sprite IconOf(string supplyTypeId)
        {
            for (int i = 0; i < _entries.Length; i++)
            {
                if (string.Equals(_entries[i].SupplyTypeId, supplyTypeId, StringComparison.Ordinal))
                {
                    return _entries[i].Icon;
                }
            }

            return null;
        }

        /// <summary>One consumable's visuals.</summary>
        [Serializable]
        private struct Entry
        {
            [SerializeField]
            [Tooltip("Supply type id, e.g. \"shampoo\".")]
            private string _supplyTypeId;

            [SerializeField]
            private Color _color;

            [SerializeField]
            [Tooltip("Optional icon (HUD, bubbles).")]
            private Sprite _icon;

            public string SupplyTypeId => _supplyTypeId;

            public Color Color => _color;

            public Sprite Icon => _icon;
        }
    }
}
