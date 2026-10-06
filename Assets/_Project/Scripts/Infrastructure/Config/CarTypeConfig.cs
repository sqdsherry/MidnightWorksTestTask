using UnityEngine;

namespace AutoService.Infrastructure.Config
{
    /// <summary>
    /// Authoring asset of one car type. Mapped into the domain <c>CarType</c> by <see cref="ScriptableObjectConfigProvider"/>;
    /// the visual prefab is looked up by <see cref="Id"/> in the Presentation-side car visual catalog.
    /// </summary>
    [CreateAssetMenu(menuName = "AutoService/Car Type", fileName = "CT_New")]
    public sealed class CarTypeConfig : ScriptableObject
    {
        private const float MinPriceMultiplier = 0.01f;
        private const float MinPatience = 1f;

        [SerializeField]
        [Tooltip("Unique id, e.g. \"sedan\". Also the key of the prefab in the Car Visual Catalog.")]
        private string _id = string.Empty;

        [SerializeField, Min(1)]
        [Tooltip("Relative spawn chance: weight / sum of all weights.")]
        private int _spawnWeight = 1;

        [SerializeField, Min(MinPriceMultiplier)]
        [Tooltip("Multiplier applied to every price this car pays.")]
        private float _priceMultiplier = 1f;

        [SerializeField, Min(MinPatience)]
        [Tooltip("Seconds the car is willing to wait in total.")]
        private float _patience = 60f;

        /// <summary>Unique id.</summary>
        public string Id => _id;

        /// <summary>Relative spawn chance.</summary>
        public int SpawnWeight => _spawnWeight;

        /// <summary>Multiplier applied to every price this car pays.</summary>
        public float PriceMultiplier => _priceMultiplier;

        /// <summary>Seconds the car is willing to wait in total.</summary>
        public float Patience => _patience;

        private void OnValidate()
        {
            _id = _id == null ? string.Empty : _id.Trim();
            _spawnWeight = Mathf.Max(1, _spawnWeight);
            _priceMultiplier = Mathf.Max(MinPriceMultiplier, _priceMultiplier);
            _patience = Mathf.Max(MinPatience, _patience);
        }
    }
}
