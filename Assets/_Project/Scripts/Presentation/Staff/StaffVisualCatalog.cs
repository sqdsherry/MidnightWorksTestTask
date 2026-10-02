using AutoService.Domain.Staff;
using UnityEngine;

namespace AutoService.Presentation.Staff
{
    /// <summary>Presentation-side lookup: staff role → body material (workers blue, the storekeeper orange).</summary>
    [CreateAssetMenu(menuName = "AutoService/Staff Visual Catalog", fileName = "StaffVisualCatalog")]
    public sealed class StaffVisualCatalog : ScriptableObject
    {
        [SerializeField]
        [Tooltip("Body material of point workers.")]
        private Material _pointWorker;

        [SerializeField]
        [Tooltip("Body material of the storekeeper.")]
        private Material _storekeeper;

        /// <summary>Body material of <paramref name="role"/>, or null to keep the prefab's own.</summary>
        public Material MaterialOf(StaffRole role) => role == StaffRole.Storekeeper ? _storekeeper : _pointWorker;
    }
}
