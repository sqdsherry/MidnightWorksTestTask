using AutoService.Domain.Upgrades;
using UnityEngine;

namespace AutoService.Infrastructure.Config
{
    /// <summary>
    /// Authoring asset of one point upgrade (speed or price): texts, cost curve and effect per level.
    /// Mapped into <c>UpgradeSettings</c> by <see cref="ScriptableObjectConfigProvider"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "AutoService/Upgrade", fileName = "UPG_New")]
    public sealed class UpgradeConfig : ScriptableObject
    {
        [SerializeField]
        [Tooltip("What the upgrade improves. One asset per kind in GameConfig.")]
        private UpgradeKind _kind = UpgradeKind.Speed;

        [SerializeField]
        [Tooltip("Player-facing name (English), e.g. \"Speed\".")]
        private string _displayName = string.Empty;

        [SerializeField]
        [Tooltip("Player-facing effect per level (English), e.g. \"−10% service time\".")]
        private string _effectFormat = string.Empty;

        [SerializeField, Min(0)]
        [Tooltip("Cost of the first level, in whole dollars.")]
        private long _baseCost = 100;

        [SerializeField, Min(1f)]
        [Tooltip("Cost multiplier per level: cost = base × growth^level.")]
        private float _growth = 1.35f;

        [SerializeField, Min(0)]
        [Tooltip("Highest level.")]
        private int _maxLevel = 10;

        [SerializeField, Min(0f)]
        [Tooltip("Speed: share of service time removed per level (0.10 → ×0.9 per level, below 1). Price: added per level (0.15 → +15%).")]
        private float _effectPerLevel = 0.1f;

        [SerializeField, Min(0)]
        [Tooltip("Player level needed to buy it.")]
        private int _requiredLevel = 1;

        /// <summary>What the upgrade improves.</summary>
        public UpgradeKind Kind => _kind;

        /// <summary>Player-facing name.</summary>
        public string DisplayName => _displayName;

        /// <summary>Player-facing effect per level.</summary>
        public string EffectFormat => _effectFormat;

        /// <summary>Cost of the first level, in whole dollars.</summary>
        public long BaseCost => _baseCost;

        /// <summary>Cost multiplier per level.</summary>
        public float Growth => _growth;

        /// <summary>Highest level.</summary>
        public int MaxLevel => _maxLevel;

        /// <summary>Effect per level.</summary>
        public float EffectPerLevel => _effectPerLevel;

        /// <summary>Player level needed to buy it.</summary>
        public int RequiredLevel => _requiredLevel;

        private void OnValidate()
        {
            _baseCost = _baseCost < 0 ? 0 : _baseCost;
            _growth = Mathf.Max(1f, _growth);
            _maxLevel = Mathf.Max(0, _maxLevel);
            _effectPerLevel = _kind == UpgradeKind.Speed ? Mathf.Clamp(_effectPerLevel, 0f, 0.95f) : Mathf.Max(0f, _effectPerLevel);
            _requiredLevel = Mathf.Max(0, _requiredLevel);
        }
    }
}
