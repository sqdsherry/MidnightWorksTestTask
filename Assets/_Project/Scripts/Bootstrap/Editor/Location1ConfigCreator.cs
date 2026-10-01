using System.Collections.Generic;
using AutoService.Domain.Building;
using AutoService.Domain.Points;
using AutoService.Infrastructure.Config;
using UnityEditor;
using UnityEngine;

namespace AutoService.Bootstrap.Editor
{
    /// <summary>
    /// Editor tool: creates the missing config assets of location 1 (service types of the new bays, buildables of the
    /// build plots) next to the existing <see cref="GameConfig"/> and adds them to it. Existing assets are never changed,
    /// so balance tweaks made in the inspector survive a re-run.
    /// </summary>
    internal static class Location1ConfigCreator
    {
        // ── Location 1 content (GDD §6, §8.2). Prices in whole dollars, durations in seconds. ─────────────────────────

        private const string ServiceTypesFolder = "ServiceTypes";
        private const string BuildablesFolder = "Buildables";
        private const string WashTypeId = "wash";

        private static readonly ServiceTypeSpec[] ServiceTypes =
        {
            new ServiceTypeSpec("ST_Oil", "oil", "Oil Change", 25, 9f),
            new ServiceTypeSpec("ST_Tires", "tires", "Tire Service", 35, 12f),
        };

        private static readonly BuildableSpec[] Buildables =
        {
            new BuildableSpec("B_Wash2", "loc1_build_wash_2", "Wash Bay 2", "Second wash bay: serve two cars at once.",
                BuildableKind.ServicePoint, "loc1_wash_2", 400, 2, 0.2f),
            new BuildableSpec("B_Oil", "loc1_build_oil", "Oil Change", "Oil change bay: a new service, more customers.",
                BuildableKind.ServicePoint, "loc1_oil", 500, 3, 0.2f),
            new BuildableSpec("B_Tires", "loc1_build_tires", "Tire Service", "Tire bay: the priciest service in the garage.",
                BuildableKind.ServicePoint, "loc1_tires", 900, 4, 0.2f),
            new BuildableSpec("B_Parking3", "loc1_build_parking_3", "Parking Spot 3", "One more parking spot: more paying guests.",
                BuildableKind.ParkingSlot, "2", 200, 2, 0.05f),
            new BuildableSpec("B_Parking4", "loc1_build_parking_4", "Parking Spot 4", "One more parking spot: more paying guests.",
                BuildableKind.ParkingSlot, "3", 350, 3, 0.05f),
        };

        // ──────────────────────────────────────────────────────────────────────────────────────────────────────────────

        [MenuItem("AutoService/Whitebox/Create Location 1 Configs")]
        private static void CreateLocation1Configs()
        {
            string[] guids = AssetDatabase.FindAssets("t:" + nameof(GameConfig));
            if (guids.Length == 0)
            {
                Debug.LogError("[Whitebox] No GameConfig asset found; create one (Create → AutoService → Game Config) and run again.");
                return;
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

            var serializedConfig = new SerializedObject(gameConfig);
            int added = AddMissing(serializedConfig.FindProperty("_serviceTypes"), serviceTypes, asset => asset.Id)
                + AddMissing(serializedConfig.FindProperty("_buildables"), buildables, asset => asset.Id);
            serializedConfig.ApplyModifiedProperties();

            AssetDatabase.SaveAssets();
            Debug.Log("[Whitebox] Location 1 configs: " + created + " asset(s) created, " + added + " added to '"
                + gameConfig.name + "' (Service Types: " + gameConfig.ServiceTypes.Length + ", Buildables: "
                + gameConfig.Buildables.Length + ").", gameConfig);
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
