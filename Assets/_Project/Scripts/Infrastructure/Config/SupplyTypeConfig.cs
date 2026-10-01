using UnityEngine;

namespace AutoService.Infrastructure.Config
{
    /// <summary>
    /// Authoring asset of one consumable (shampoo, oil, tires). Service types reference it by <see cref="Id"/>.
    /// Mapped into <c>SupplyTypeSettings</c> by <see cref="ScriptableObjectConfigProvider"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "AutoService/Supply Type", fileName = "SUP_New")]
    public sealed class SupplyTypeConfig : ScriptableObject
    {
        [SerializeField]
        [Tooltip("Unique id referenced by Service Type → Supply Type Id, e.g. \"shampoo\". Keep it stable: saves will use it.")]
        private string _id = string.Empty;

        [SerializeField]
        [Tooltip("Player-facing name (English).")]
        private string _displayName = string.Empty;

        [SerializeField, Min(0)]
        [Tooltip("Price of one box at the warehouse, in whole dollars.")]
        private long _boxPrice = 5;

        [SerializeField, Min(1)]
        [Tooltip("Units in one box (one unit per accepted order).")]
        private int _unitsPerBox = 5;

        /// <summary>Unique id.</summary>
        public string Id => _id;

        /// <summary>Player-facing name.</summary>
        public string DisplayName => _displayName;

        /// <summary>Price of one box, in whole dollars.</summary>
        public long BoxPrice => _boxPrice;

        /// <summary>Units in one box.</summary>
        public int UnitsPerBox => _unitsPerBox;

        private void OnValidate()
        {
            _id = _id == null ? string.Empty : _id.Trim();
            _boxPrice = _boxPrice < 0 ? 0 : _boxPrice;
            _unitsPerBox = Mathf.Max(1, _unitsPerBox);
        }
    }
}
