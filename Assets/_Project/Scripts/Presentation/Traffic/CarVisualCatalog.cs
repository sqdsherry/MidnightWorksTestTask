using System;
using UnityEngine;

namespace AutoService.Presentation.Traffic
{
    /// <summary>
    /// Maps car type ids (from the game config) to car prefabs. Lives in Presentation: the gameplay config
    /// knows nothing about visuals.
    /// </summary>
    [CreateAssetMenu(menuName = "AutoService/Car Visual Catalog", fileName = "CarVisualCatalog")]
    public sealed class CarVisualCatalog : ScriptableObject
    {
        [SerializeField]
        [Tooltip("One entry per car type id.")]
        private Entry[] _entries = new Entry[0];

        /// <summary>Number of entries (for iteration in setup code).</summary>
        public int Count => _entries.Length;

        /// <summary>Car type id of entry <paramref name="index"/>.</summary>
        public string GetCarTypeId(int index) => _entries[index].CarTypeId;

        /// <summary>Prefab of entry <paramref name="index"/> (may be null if not assigned).</summary>
        public CarView GetPrefab(int index) => _entries[index].Prefab;

        /// <summary>Finds the prefab of a car type.</summary>
        /// <returns>False when no entry with a prefab matches <paramref name="carTypeId"/>.</returns>
        public bool TryGetPrefab(string carTypeId, out CarView prefab)
        {
            for (int i = 0; i < _entries.Length; i++)
            {
                Entry entry = _entries[i];
                if (entry != null && entry.Prefab != null && string.Equals(entry.CarTypeId, carTypeId, StringComparison.Ordinal))
                {
                    prefab = entry.Prefab;
                    return true;
                }
            }

            prefab = null;
            return false;
        }

        /// <summary>One car type → prefab pair.</summary>
        [Serializable]
        private sealed class Entry
        {
            [SerializeField]
            [Tooltip("Id of a Car Type asset, e.g. \"sedan\".")]
            private string _carTypeId = string.Empty;

            [SerializeField]
            private CarView _prefab;

            public string CarTypeId => _carTypeId;

            public CarView Prefab => _prefab;
        }
    }
}
