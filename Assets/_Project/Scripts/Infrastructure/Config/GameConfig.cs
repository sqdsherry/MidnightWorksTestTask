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

        /// <summary>Global economy settings.</summary>
        public EconomySection Economy => _economy;
    }
}
