using System.Collections.Generic;
using AutoService.Domain.Building;
using AutoService.Domain.Points;
using AutoService.Domain.Upgrades;
using AutoService.Infrastructure.Config;
using UnityEditor;
using UnityEngine;

namespace AutoService.Bootstrap.Editor
{
    /// <summary>
    /// Editor tool: creates the missing config assets of location 1 (service types of the new bays, buildables of the
    /// build plots, supply types, upgrades) next to the existing <see cref="GameConfig"/> and adds them to it. Existing assets
    /// are never overwritten: A2 only fills the supply / worker / storekeeper fields that are still empty, so balance tweaks
    /// made in the inspector survive a re-run.
    /// </summary>
    internal static class Location1ConfigCreator
    {
        // ── Location 1 content (GDD §6, §8.2). Prices in whole dollars, durations in seconds. ─────────────────────────

        private const string ServiceTypesFolder = "ServiceTypes";
        private const string BuildablesFolder = "Buildables";
        private const string WashTypeId = "wash";
        private const string SuppliesFolder = "Supplies";
        private const string UpgradesFolder = "Upgrades";

        // ── A2: supplies, workers, upgrades (GDD §7, §8; prompt 05 §4.5) ──────────────────────────────────────────────

        private const int SupplyCapacity = 10;

        private static readonly SupplySpec[] SupplyTypes =
        {
            new SupplySpec("SUP_Shampoo", "shampoo", "Shampoo", 5, 5),
            new SupplySpec("SUP_Oil", "oil", "Motor Oil", 15, 5),
            new SupplySpec("SUP_Tires", "tires", "Tires", 25, 5),
        };

        // Service type id → consumable (empty for the parking barriers) and its worker.
        private static readonly PointStaffSpec[] PointStaff =
        {
            new PointStaffSpec("parking", string.Empty, "Parking Attendant", 150, 1, 0),
            new PointStaffSpec(WashTypeId, "shampoo", "Washer", 300, 2, 5),
            new PointStaffSpec("oil", "oil", "Oil Mechanic", 400, 3, 10),
            new PointStaffSpec("tires", "tires", "Tire Mechanic", 700, 4, 15),
        };

        // Why: an ASCII minus — the default TMP font has no U+2212 glyph.
        private static readonly UpgradeSpec[] Upgrades =
        {
            new UpgradeSpec("UPG_Speed", UpgradeKind.Speed, "Speed", "-10% service time", 0.10f),
            new UpgradeSpec("UPG_Price", UpgradeKind.Price, "Price", "+15% price", 0.15f),
        };

        private const long UpgradeBaseCost = 100;
        private const float UpgradeGrowth = 1.35f;
        private const int UpgradeMaxLevel = 10;
        private const string StorekeeperTitle = "Storekeeper";
        private const string StorekeeperDescription = "Carries boxes to the hungriest bay";
        private const int RestockAtOrBelow = 5;
        private const int MaxStorekeepers = 3;
        private const float StorekeeperCostGrowth = 1.5f;

        private static readonly ServiceTypeSpec[] ServiceTypes =
        {
            new ServiceTypeSpec("ST_Oil", "oil", "Oil Change", 25, 9f),
            new ServiceTypeSpec("ST_Tires", "tires", "Tire Service", 35, 12f),
        };

        private static readonly BuildableSpec[] Buildables =
        {
            new BuildableSpec("B_Wash1", "loc1_build_wash_1", "Wash Bay 1", "First wash bay.",
                BuildableKind.ServicePoint, "loc1_wash_1", 200, 1, 0.2f),
            new BuildableSpec("B_Wash2", "loc1_build_wash_2", "Wash Bay 2", "Second wash bay: serve two cars at once.",
                BuildableKind.ServicePoint, "loc1_wash_2", 400, 2, 0.2f),
            new BuildableSpec("B_Oil1", "loc1_build_oil_1", "Oil Change 1", "Oil change bay: a new service, more customers.",
                BuildableKind.ServicePoint, "loc1_oil_1", 500, 3, 0.2f),
            new BuildableSpec("B_Oil2", "loc1_build_oil_2", "Oil Change 2", "Second oil change bay.",
                BuildableKind.ServicePoint, "loc1_oil_2", 800, 4, 0.2f),
            new BuildableSpec("B_Parking3", "loc1_build_parking_3", "Parking Spot 3", "One more parking spot: more paying guests.",
                BuildableKind.ParkingSlot, "2", 200, 2, 0.05f),
            new BuildableSpec("B_Parking4", "loc1_build_parking_4", "Parking Spot 4", "One more parking spot: more paying guests.",
                BuildableKind.ParkingSlot, "3", 350, 3, 0.05f),
        };

        // ──────────────────────────────────────────────────────────────────────────────────────────────────────────────

        [MenuItem("AutoService/Whitebox/Create Location 1 Configs")]
        private static void CreateLocation1Configs() => Run();

        /// <summary>Creates / completes the configs of location 1. Also step 1 of <see cref="ModuleSetupA2"/>.</summary>
        /// <returns>False when there is no <see cref="GameConfig"/> to fill.</returns>
        internal static bool Run()
        {
            string[] guids = AssetDatabase.FindAssets("t:" + nameof(GameConfig));
            if (guids.Length == 0)
            {
                Debug.LogError("[Whitebox] No GameConfig asset found; create one (Create → AutoService → Game Config) and run again.");
                return false;
            }

            if (guids.Length > 1)
            {
                Debug.LogWarning("[Whitebox] Several GameConfig assets found; using the first one.");
            }

            string configPath = AssetDatabase.GUIDToAssetPath(guids[0]);
            var gameConfig = AssetDatabase.LoadAssetAtPath<GameConfig>(configPath);
            string root = System.IO.Path.GetDirectoryName(configPath)?.Replace('\\', '/');
            string typesFolder = EnsureFolder(root, ServiceTypesFolder);
            string buildablesFolder = EnsureFolder(root, BuildablesFolder);

            ServiceTypeConfig wash = FindById(gameConfig.ServiceTypes, WashTypeId);
            int created = 0;

            var serviceTypes = new List<ServiceTypeConfig>();
            for (int i = 0; i < ServiceTypes.Length; i++)
            {
                serviceTypes.Add(GetOrCreate<ServiceTypeConfig>(typesFolder, ServiceTypes[i].AssetName, ref created,
                    asset => FillServiceType(asset, ServiceTypes[i], wash)));
            }

            var buildables = new List<BuildableConfig>();
            for (int i = 0; i < Buildables.Length; i++)
            {
                buildables.Add(GetOrCreate<BuildableConfig>(buildablesFolder, Buildables[i].AssetName, ref created,
                    asset => FillBuildable(asset, Buildables[i])));
            }

            string suppliesFolder = EnsureFolder(root, SuppliesFolder);
            var supplyTypes = new List<SupplyTypeConfig>();
            for (int i = 0; i < SupplyTypes.Length; i++)
            {
                supplyTypes.Add(GetOrCreate<SupplyTypeConfig>(suppliesFolder, SupplyTypes[i].AssetName, ref created,
                    asset => FillSupplyType(asset, SupplyTypes[i])));
            }

            string upgradesFolder = EnsureFolder(root, UpgradesFolder);
            var upgrades = new List<UpgradeConfig>();
            for (int i = 0; i < Upgrades.Length; i++)
            {
                upgrades.Add(GetOrCreate<UpgradeConfig>(upgradesFolder, Upgrades[i].AssetName, ref created,
                    asset => FillUpgrade(asset, Upgrades[i])));
            }

            var serializedConfig = new SerializedObject(gameConfig);
            int added = AddMissing(serializedConfig.FindProperty("_serviceTypes"), serviceTypes, asset => asset.Id)
                + AddMissing(serializedConfig.FindProperty("_buildables"), buildables, asset => asset.Id)
                + AddMissing(serializedConfig.FindProperty("_supplyTypes"), supplyTypes, asset => asset.Id)
                + AddMissing(serializedConfig.FindProperty("_upgrades"), upgrades, asset => asset.Kind.ToString());
            int completed = FillEmptyString(serializedConfig.FindProperty("_staff._storekeeperTitle"), StorekeeperTitle)
                + FillEmptyString(serializedConfig.FindProperty("_staff._storekeeperDescription"), StorekeeperDescription)
                + FillInvalidInt(serializedConfig.FindProperty("_staff._restockAtOrBelow"), RestockAtOrBelow, 0)
                + FillInvalidInt(serializedConfig.FindProperty("_staff._maxStorekeepers"), MaxStorekeepers, 1)
                + FillInvalidFloat(serializedConfig.FindProperty("_staff._storekeeperCostGrowth"), StorekeeperCostGrowth, 1f);
            serializedConfig.ApplyModifiedProperties();

            ServiceTypeConfig[] allTypes = gameConfig.ServiceTypes;
            for (int i = 0; allTypes != null && i < allTypes.Length; i++)
            {
                completed += CompleteStaffSupplies(allTypes[i]);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[Whitebox] Location 1 configs: " + created + " asset(s) created, " + added + " added to '"
                + gameConfig.name + "', " + completed + " empty A2 field group(s) filled (Service Types: "
                + gameConfig.ServiceTypes.Length + ", Buildables: " + gameConfig.Buildables.Length + ", Supply Types: "
                + gameConfig.SupplyTypes.Length + ", Upgrades: " + gameConfig.Upgrades.Length + ").", gameConfig);
            return true;
        }

        /// <summary>
        /// Fills the consumable and the worker of a service type, each only while still empty (an old asset has no value
        /// there yet): consumable id + capacity together, worker title + price + level together.
        /// </summary>
        /// <returns>Number of field groups filled.</returns>
        private static int CompleteStaffSupplies(ServiceTypeConfig asset)
        {
            if (asset == null)
            {
                return 0;
            }

            PointStaffSpec spec = default;
            bool found = false;
            for (int i = 0; i < PointStaff.Length && !found; i++)
            {
                if (PointStaff[i].ServiceTypeId == asset.Id)
                {
                    spec = PointStaff[i];
                    found = true;
                }
            }

            if (!found)
            {
                return 0;
            }

            var serialized = new SerializedObject(asset);
            int filled = 0;
            if (!string.IsNullOrEmpty(spec.SupplyTypeId) && string.IsNullOrEmpty(asset.SupplyTypeId))
            {
                serialized.FindProperty("_supplyTypeId").stringValue = spec.SupplyTypeId;
                serialized.FindProperty("_supplyCapacity").intValue = SupplyCapacity;
                filled++;
            }

            if (string.IsNullOrEmpty(asset.WorkerTitle))
            {
                serialized.FindProperty("_workerTitle").stringValue = spec.WorkerTitle;
                serialized.FindProperty("_workerHireCost").longValue = spec.WorkerHireCost;
                serialized.FindProperty("_workerRequiredLevel").intValue = spec.WorkerRequiredLevel;
                filled++;
            }

            if (spec.XpReward > 0 && asset.XpReward != spec.XpReward)
            {
                serialized.FindProperty("_xpReward").intValue = spec.XpReward;
                filled++;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            return filled;
        }

        // Why: an old asset gets the field initializers (5 / 3 / 1.5) on load; only a value that is out of range is replaced,
        // so a balance tweak made in the inspector survives.
        private static int FillInvalidInt(SerializedProperty property, int value, int minimum)
        {
            if (property == null || property.intValue >= minimum)
            {
                return 0;
            }

            property.intValue = value;
            return 1;
        }

        private static int FillInvalidFloat(SerializedProperty property, float value, float minimum)
        {
            if (property == null || property.floatValue >= minimum)
            {
                return 0;
            }

            property.floatValue = value;
            return 1;
        }

        private static int FillEmptyString(SerializedProperty property, string value)
        {
            if (property == null || !string.IsNullOrEmpty(property.stringValue))
            {
                return 0;
            }

            property.stringValue = value;
            return 1;
        }

        private static void FillSupplyType(SupplyTypeConfig asset, SupplySpec spec)
        {
            var serialized = new SerializedObject(asset);
            serialized.FindProperty("_id").stringValue = spec.Id;
            serialized.FindProperty("_displayName").stringValue = spec.DisplayName;
            serialized.FindProperty("_boxPrice").longValue = spec.BoxPrice;
            serialized.FindProperty("_unitsPerBox").intValue = spec.UnitsPerBox;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void FillUpgrade(UpgradeConfig asset, UpgradeSpec spec)
        {
            var serialized = new SerializedObject(asset);
            serialized.FindProperty("_kind").intValue = (int)spec.Kind;
            serialized.FindProperty("_displayName").stringValue = spec.DisplayName;
            serialized.FindProperty("_effectFormat").stringValue = spec.EffectFormat;
            serialized.FindProperty("_baseCost").longValue = UpgradeBaseCost;
            serialized.FindProperty("_growth").floatValue = UpgradeGrowth;
            serialized.FindProperty("_maxLevel").intValue = UpgradeMaxLevel;
            serialized.FindProperty("_effectPerLevel").floatValue = spec.EffectPerLevel;
            serialized.FindProperty("_requiredLevel").intValue = 1;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static string EnsureFolder(string parent, string name)
        {
            string path = parent + "/" + name;
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, name);
            }

            return path;
        }

        private static T GetOrCreate<T>(string folder, string assetName, ref int created, System.Action<T> fill) where T : ScriptableObject
        {
            string path = folder + "/" + assetName + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null)
            {
                return existing;
            }

            var asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            fill(asset);
            created++;
            return asset;
        }

        // Why: accept/clear delays are copied from the wash, so every bay feels the same; only price and time differ.
        private static void FillServiceType(ServiceTypeConfig asset, ServiceTypeSpec spec, ServiceTypeConfig wash)
        {
            var serialized = new SerializedObject(asset);
            serialized.FindProperty("_id").stringValue = spec.Id;
            serialized.FindProperty("_displayName").stringValue = spec.DisplayName;
            serialized.FindProperty("_kind").intValue = (int)PointKind.Service;
            serialized.FindProperty("_basePrice").longValue = spec.BasePrice;
            serialized.FindProperty("_pricePerSecond").floatValue = 0f;
            serialized.FindProperty("_serviceDuration").floatValue = spec.Duration;
            if (wash != null)
            {
                serialized.FindProperty("_acceptDelay").floatValue = wash.AcceptDelay;
                serialized.FindProperty("_clearDelay").floatValue = wash.ClearDelay;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void FillBuildable(BuildableConfig asset, BuildableSpec spec)
        {
            var serialized = new SerializedObject(asset);
            serialized.FindProperty("_id").stringValue = spec.Id;
            serialized.FindProperty("_displayName").stringValue = spec.DisplayName;
            serialized.FindProperty("_description").stringValue = spec.Description;
            serialized.FindProperty("_kind").intValue = (int)spec.Kind;
            serialized.FindProperty("_targetId").stringValue = spec.TargetId;
            serialized.FindProperty("_cost").longValue = spec.Cost;
            serialized.FindProperty("_requiredLevel").intValue = spec.RequiredLevel;
            serialized.FindProperty("_flowBonus").floatValue = spec.FlowBonus;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <returns>Number of assets appended to <paramref name="array"/>.</returns>
        private static int AddMissing<T>(SerializedProperty array, List<T> assets, System.Func<T, string> idOf) where T : Object
        {
            int added = 0;
            for (int i = 0; i < assets.Count; i++)
            {
                T asset = assets[i];
                bool present = false;
                for (int e = 0; e < array.arraySize && !present; e++)
                {
                    Object element = array.GetArrayElementAtIndex(e).objectReferenceValue;
                    if (element == asset)
                    {
                        present = true;
                    }
                    else if (element is T other && idOf(other) == idOf(asset))
                    {
                        // Why: a hand-made asset with the same id already plays this role; adding ours would duplicate the id.
                        Debug.LogWarning("[Whitebox] '" + other.name + "' already uses id '" + idOf(asset) + "'; '" + asset.name
                            + "' is not added.", other);
                        present = true;
                    }
                }

                if (present)
                {
                    continue;
                }

                array.arraySize++;
                array.GetArrayElementAtIndex(array.arraySize - 1).objectReferenceValue = asset;
                added++;
            }

            return added;
        }

        private static ServiceTypeConfig FindById(ServiceTypeConfig[] assets, string id)
        {
            for (int i = 0; assets != null && i < assets.Length; i++)
            {
                if (assets[i] != null && assets[i].Id == id)
                {
                    return assets[i];
                }
            }

            return null;
        }

        private readonly struct SupplySpec
        {
            public SupplySpec(string assetName, string id, string displayName, long boxPrice, int unitsPerBox)
            {
                AssetName = assetName;
                Id = id;
                DisplayName = displayName;
                BoxPrice = boxPrice;
                UnitsPerBox = unitsPerBox;
            }

            public string AssetName { get; }

            public string Id { get; }

            public string DisplayName { get; }

            public long BoxPrice { get; }

            public int UnitsPerBox { get; }
        }

        private readonly struct PointStaffSpec
        {
            public PointStaffSpec(string serviceTypeId, string supplyTypeId, string workerTitle, long workerHireCost, int workerRequiredLevel, int xpReward)
            {
                ServiceTypeId = serviceTypeId;
                SupplyTypeId = supplyTypeId;
                WorkerTitle = workerTitle;
                WorkerHireCost = workerHireCost;
                WorkerRequiredLevel = workerRequiredLevel;
                XpReward = xpReward;
            }

            public string ServiceTypeId { get; }

            public string SupplyTypeId { get; }

            public string WorkerTitle { get; }

            public long WorkerHireCost { get; }

            public int WorkerRequiredLevel { get; }

            public int XpReward { get; }
        }

        private readonly struct UpgradeSpec
        {
            public UpgradeSpec(string assetName, UpgradeKind kind, string displayName, string effectFormat, float effectPerLevel)
            {
                AssetName = assetName;
                Kind = kind;
                DisplayName = displayName;
                EffectFormat = effectFormat;
                EffectPerLevel = effectPerLevel;
            }

            public string AssetName { get; }

            public UpgradeKind Kind { get; }

            public string DisplayName { get; }

            public string EffectFormat { get; }

            public float EffectPerLevel { get; }
        }

        private readonly struct ServiceTypeSpec
        {
            public ServiceTypeSpec(string assetName, string id, string displayName, long basePrice, float duration)
            {
                AssetName = assetName;
                Id = id;
                DisplayName = displayName;
                BasePrice = basePrice;
                Duration = duration;
            }

            public string AssetName { get; }

            public string Id { get; }

            public string DisplayName { get; }

            public long BasePrice { get; }

            public float Duration { get; }
        }

        private readonly struct BuildableSpec
        {
            public BuildableSpec(
                string assetName,
                string id,
                string displayName,
                string description,
                BuildableKind kind,
                string targetId,
                long cost,
                int requiredLevel,
                float flowBonus)
            {
                AssetName = assetName;
                Id = id;
                DisplayName = displayName;
                Description = description;
                Kind = kind;
                TargetId = targetId;
                Cost = cost;
                RequiredLevel = requiredLevel;
                FlowBonus = flowBonus;
            }

            public string AssetName { get; }

            public string Id { get; }

            public string DisplayName { get; }

            public string Description { get; }

            public BuildableKind Kind { get; }

            public string TargetId { get; }

            public long Cost { get; }

            public int RequiredLevel { get; }

            public float FlowBonus { get; }
        }
    }
}
