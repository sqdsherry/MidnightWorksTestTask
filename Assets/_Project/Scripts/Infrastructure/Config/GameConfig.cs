using UnityEngine;

namespace AutoService.Infrastructure.Config
{
    /// <summary>
    /// Root configuration asset. Holds global sections directly and, as the game grows,
    /// references to the other configuration assets (service points, cars, levels...).
    /// Read only by <see cref="ScriptableObjectConfigProvider"/>; the rest of the game never sees ScriptableObjects.
    /// </summary>
    [CreateAssetMenu(menuName = "AutoService/Game Config", fileName = "GameConfig")]
    public sealed class GameConfig : ScriptableObject
    {
        [SerializeField]
        [Tooltip("Global economy settings.")]
        private EconomySection _economy = new EconomySection();

        [SerializeField]
        [Tooltip("Every service type used by scene points (parking barrier, wash...). Ids must be unique.")]
        private ServiceTypeConfig[] _serviceTypes = new ServiceTypeConfig[0];

        [SerializeField]
        [Tooltip("Every car type that can spawn. Ids must be unique.")]
        private CarTypeConfig[] _carTypes = new CarTypeConfig[0];

        [SerializeField]
        [Tooltip("Car flow settings.")]
        private TrafficSection _traffic = new TrafficSection();

        [SerializeField]
        [Tooltip("Every buildable referenced by scene build plots (bays, extra parking slots). Ids must be unique.")]
        private BuildableConfig[] _buildables = new BuildableConfig[0];

        /// <summary>Global economy settings.</summary>
        public EconomySection Economy => _economy;

        /// <summary>Service type assets (may contain nulls if left empty in the inspector).</summary>
        public ServiceTypeConfig[] ServiceTypes => _serviceTypes;

        /// <summary>Car type assets (may contain nulls if left empty in the inspector).</summary>
        public CarTypeConfig[] CarTypes => _carTypes;

        /// <summary>Car flow settings.</summary>
        public TrafficSection Traffic => _traffic;

        /// <summary>Buildable assets (may contain nulls if left empty in the inspector).</summary>
        public BuildableConfig[] Buildables => _buildables;

        private void OnValidate()
        {
            _traffic?.Sanitize();
        }
    }
}
