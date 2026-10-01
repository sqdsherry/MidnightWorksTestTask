using AutoService.Domain.Points;
using UnityEngine;

namespace AutoService.Infrastructure.Config
{
    /// <summary>
    /// Authoring asset of one service type (parking barrier, wash...). Scene points reference it by <see cref="Id"/>.
    /// Mapped into <c>ServiceTypeSettings</c> by <see cref="ScriptableObjectConfigProvider"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "AutoService/Service Type", fileName = "ST_New")]
    public sealed class ServiceTypeConfig : ScriptableObject
    {
        [SerializeField]
        [Tooltip("Unique id referenced by ServicePointView.serviceTypeId, e.g. \"wash\". Keep it stable: saves will use it.")]
        private string _id = string.Empty;

        [SerializeField]
        [Tooltip("Player-facing name (English).")]
        private string _displayName = string.Empty;

        [SerializeField]
        [Tooltip("Barrier = parking entrance (fee for the planned stay); Service = a real service after which the car leaves.")]
        private PointKind _kind = PointKind.Service;

        [SerializeField, Min(0)]
        [Tooltip("Price in whole dollars before the car type multiplier.")]
        private long _basePrice = 10;

        [SerializeField, Min(0f)]
        [Tooltip("Extra dollars per second of the planned stay (parking entrance fee: (Base Price + this × seconds) × car multiplier). 0 = flat price.")]
        private float _pricePerSecond;

        [SerializeField, Min(0f)]
        [Tooltip("Seconds of occupied work needed per car.")]
        private float _serviceDuration = 5f;

        [SerializeField, Min(0f)]
        [Tooltip("Seconds the work spot must be occupied before the order is accepted.")]
        private float _acceptDelay = 0.4f;

        [SerializeField, Min(0f)]
        [Tooltip("Seconds the point stays unavailable after a car was served (it drives away).")]
        private float _clearDelay = 1.5f;

        /// <summary>Unique id.</summary>
        public string Id => _id;

        /// <summary>Player-facing name.</summary>
        public string DisplayName => _displayName;

        /// <summary>Barrier or real service.</summary>
        public PointKind Kind => _kind;

        /// <summary>Price in whole dollars before the car type multiplier.</summary>
        public long BasePrice => _basePrice;

        /// <summary>Extra dollars per second of the car's stay.</summary>
        public float PricePerSecond => _pricePerSecond;

        /// <summary>Seconds of occupied work needed per car.</summary>
        public float ServiceDuration => _serviceDuration;

        /// <summary>Seconds of presence before the order is accepted.</summary>
        public float AcceptDelay => _acceptDelay;

        /// <summary>Seconds the point stays unavailable after a car was served.</summary>
        public float ClearDelay => _clearDelay;

        private void OnValidate()
        {
            _id = _id == null ? string.Empty : _id.Trim();
            _basePrice = _basePrice < 0 ? 0 : _basePrice;
            _pricePerSecond = Mathf.Max(0f, _pricePerSecond);
            _serviceDuration = Mathf.Max(0f, _serviceDuration);
            _acceptDelay = Mathf.Max(0f, _acceptDelay);
            _clearDelay = Mathf.Max(0f, _clearDelay);
        }
    }
}
