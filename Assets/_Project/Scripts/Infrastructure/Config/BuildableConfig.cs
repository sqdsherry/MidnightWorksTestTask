using AutoService.Domain.Building;
using UnityEngine;

namespace AutoService.Infrastructure.Config
{
    /// <summary>
    /// Authoring asset of one buildable (a bay, an extra parking slot). Scene build plots reference it by <see cref="Id"/>.
    /// Mapped into <c>BuildableSettings</c> by <see cref="ScriptableObjectConfigProvider"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "AutoService/Buildable", fileName = "B_New")]
    public sealed class BuildableConfig : ScriptableObject
    {
        [SerializeField]
        [Tooltip("Unique id referenced by BuildPlotView.plotId, e.g. \"loc1_build_oil\". Keep it stable: saves will use it.")]
        private string _id = string.Empty;

        [SerializeField]
        [Tooltip("Player-facing name (English), shown on the price tag and the build panel.")]
        private string _displayName = string.Empty;

        [SerializeField, TextArea(2, 4)]
        [Tooltip("Player-facing description (English), shown on the build panel.")]
        private string _description = string.Empty;

        [SerializeField]
        [Tooltip("Service Point = a bay; Parking Slot = one extra parking slot.")]
        private BuildableKind _kind = BuildableKind.ServicePoint;

        [SerializeField]
        [Tooltip("Service Point: the point id of the bay (e.g. \"loc1_oil\"). Parking Slot: the slot index in LocationLayout (e.g. \"2\").")]
        private string _targetId = string.Empty;

        [SerializeField, Min(0)]
        [Tooltip("Construction price in whole dollars.")]
        private long _cost = 100;

        [SerializeField, Min(0)]
        [Tooltip("Player level needed to build.")]
        private int _requiredLevel = 1;

        [SerializeField, Min(0f)]
        [Tooltip("Added to the location's car flow multiplier once built (0.2 = +20% cars).")]
        private float _flowBonus;

        /// <summary>Unique id.</summary>
        public string Id => _id;

        /// <summary>Player-facing name.</summary>
        public string DisplayName => _displayName;

        /// <summary>Player-facing description.</summary>
        public string Description => _description;

        /// <summary>What gets built.</summary>
        public BuildableKind Kind => _kind;

        /// <summary>Point id or slot index.</summary>
        public string TargetId => _targetId;

        /// <summary>Construction price in whole dollars.</summary>
        public long Cost => _cost;

        /// <summary>Player level needed to build.</summary>
        public int RequiredLevel => _requiredLevel;

        /// <summary>Car flow multiplier bonus.</summary>
        public float FlowBonus => _flowBonus;

        private void OnValidate()
        {
            _id = _id == null ? string.Empty : _id.Trim();
            _targetId = _targetId == null ? string.Empty : _targetId.Trim();
            _cost = _cost < 0 ? 0 : _cost;
            _requiredLevel = Mathf.Max(0, _requiredLevel);
            _flowBonus = Mathf.Max(0f, _flowBonus);
        }
    }
}
