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

        [Header("Supplies")]
        [SerializeField]
        [Tooltip("Id of the Supply Type every accepted order uses, e.g. \"shampoo\". Empty = no consumable (parking barriers).")]
        private string _supplyTypeId = string.Empty;

        [SerializeField, Min(1)]
        [Tooltip("Units a point's stock holds; it starts full. Ignored without a supply type.")]
        private int _supplyCapacity = 10;

        [Header("Worker")]
        [SerializeField]
        [Tooltip("Job title of the hired point worker (English), e.g. \"Washer\". Empty = points of this type cannot hire one.")]
        private string _workerTitle = string.Empty;

        [SerializeField, Min(0)]
        [Tooltip("One-time hiring price of the worker, in whole dollars.")]
        private long _workerHireCost = 150;

        [SerializeField, Min(0)]
        [Tooltip("Player level needed to hire the worker.")]
        private int _workerRequiredLevel = 1;

        [SerializeField, Min(0)]
        [Tooltip("XP granted to the player when this service is completed.")]
        private int _xpReward;

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

        /// <summary>Id of the consumable; empty when none.</summary>
        public string SupplyTypeId => _supplyTypeId;

        /// <summary>Units a point's stock holds.</summary>
        public int SupplyCapacity => _supplyCapacity;

        /// <summary>Job title of the point worker; empty when none can be hired.</summary>
        public string WorkerTitle => _workerTitle;

        /// <summary>Hiring price of the worker, in whole dollars.</summary>
        public long WorkerHireCost => _workerHireCost;

        /// <summary>Player level needed to hire the worker.</summary>
        public int WorkerRequiredLevel => _workerRequiredLevel;

        /// <summary>XP granted to the player when this service is completed.</summary>
        public int XpReward => _xpReward;

        private void OnValidate()
        {
            _id = _id == null ? string.Empty : _id.Trim();
            _basePrice = _basePrice < 0 ? 0 : _basePrice;
            _pricePerSecond = Mathf.Max(0f, _pricePerSecond);
            _serviceDuration = Mathf.Max(0f, _serviceDuration);
            _acceptDelay = Mathf.Max(0f, _acceptDelay);
            _clearDelay = Mathf.Max(0f, _clearDelay);
            _supplyTypeId = _supplyTypeId == null ? string.Empty : _supplyTypeId.Trim();
            _supplyCapacity = Mathf.Max(1, _supplyCapacity);
            _workerTitle = _workerTitle == null ? string.Empty : _workerTitle.Trim();
            _workerHireCost = _workerHireCost < 0 ? 0 : _workerHireCost;
            _workerRequiredLevel = Mathf.Max(0, _workerRequiredLevel);
            _xpReward = Mathf.Max(0, _xpReward);
        }
    }
}
