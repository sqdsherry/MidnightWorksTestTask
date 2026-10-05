using System.Collections.Generic;
using AutoService.Domain.Building;
using AutoService.Domain.Points;
using AutoService.Domain.Upgrades;
using AutoService.Infrastructure.Config;
using UnityEditor;
using UnityEngine;

namespace AutoService.Bootstrap.Editor
{
    internal static class Location2ConfigCreator
    {
        private const string ServiceTypesFolder = "ServiceTypes";
        private const string BuildablesFolder = "Buildables";
        private const string SuppliesFolder = "Supplies";
        private const string UpgradesFolder = "Upgrades";

        private static readonly SupplySpec[] SupplyTypes =
        {
            new SupplySpec("SUP_TiresBox", "tires_box", "Tires Box", 30, 5),
            new SupplySpec("SUP_TuningParts", "tuning_parts", "Tuning Parts", 50, 5),
            new SupplySpec("SUP_PaintCans", "paint_cans", "Paint Cans", 80, 5),
        };

        private static readonly ServiceTypeSpec[] ServiceTypes =
        {
            new ServiceTypeSpec("ST_TiresLoc2", "tires_loc2", "Tires", 35, 12f),
            new ServiceTypeSpec("ST_TuningLoc2", "tuning_loc2", "Tuning", 60, 18f),
            new ServiceTypeSpec("ST_PaintLoc2", "paint_loc2", "Paint", 90, 24f),
        };

        private static readonly BuildableSpec[] Buildables =
        {
            new BuildableSpec("B_Loc2_Tires1", "loc2_build_tires_1", "Tires 1", "Tires bay.",
                BuildableKind.ServicePoint, "loc2_tires_1", 100, 1, 0.2f),
            new BuildableSpec("B_Loc2_Tuning1", "loc2_build_tuning_1", "Tuning 1", "Tuning bay.",
                BuildableKind.ServicePoint, "loc2_tuning_1", 100, 1, 0.2f),
            new BuildableSpec("B_Loc2_Paint1", "loc2_build_paint_1", "Paint 1", "Paint bay.",
                BuildableKind.ServicePoint, "loc2_paint_1", 100, 1, 0.2f),
            new BuildableSpec("B_TravelToLoc2", "b_travel_to_loc2", "Travel to Location 2", "Go to Location 2.",
                BuildableKind.TravelPoint, "travel_to_loc2", 0, 1, 0f),
            new BuildableSpec("B_TravelToLoc1", "b_travel_to_loc1", "Travel to Location 1", "Return to Location 1.",
                BuildableKind.TravelPoint, "travel_to_loc1", 0, 1, 0f),
        };

        [MenuItem("AutoService/Whitebox/Create Location 2 Configs")]
        private static void CreateLocation2Configs() => Run();

        internal static bool Run()
        {
            string[] guids = AssetDatabase.FindAssets("t:" + nameof(GameConfig));
            if (guids.Length == 0) return false;

            string configPath = AssetDatabase.GUIDToAssetPath(guids[0]);
            var gameConfig = AssetDatabase.LoadAssetAtPath<GameConfig>(configPath);
            string root = System.IO.Path.GetDirectoryName(configPath)?.Replace('\\', '/');
            string typesFolder = EnsureFolder(root, ServiceTypesFolder);
            string buildablesFolder = EnsureFolder(root, BuildablesFolder);
            string suppliesFolder = EnsureFolder(root, SuppliesFolder);

            int created = 0;

            var serviceTypes = new List<ServiceTypeConfig>();
            for (int i = 0; i < ServiceTypes.Length; i++)
            {
                serviceTypes.Add(GetOrCreate<ServiceTypeConfig>(typesFolder, ServiceTypes[i].AssetName, ref created,
                    asset => FillServiceType(asset, ServiceTypes[i])));
            }

            var buildables = new List<BuildableConfig>();
            for (int i = 0; i < Buildables.Length; i++)
            {
                buildables.Add(GetOrCreate<BuildableConfig>(buildablesFolder, Buildables[i].AssetName, ref created,
                    asset => FillBuildable(asset, Buildables[i])));
            }

            var supplyTypes = new List<SupplyTypeConfig>();
            for (int i = 0; i < SupplyTypes.Length; i++)
            {
                supplyTypes.Add(GetOrCreate<SupplyTypeConfig>(suppliesFolder, SupplyTypes[i].AssetName, ref created,
                    asset => FillSupplyType(asset, SupplyTypes[i])));
            }

            var serializedConfig = new SerializedObject(gameConfig);
            int added = AddMissing(serializedConfig.FindProperty("_serviceTypes"), serviceTypes, asset => asset.Id)
                + AddMissing(serializedConfig.FindProperty("_buildables"), buildables, asset => asset.Id)
                + AddMissing(serializedConfig.FindProperty("_supplyTypes"), supplyTypes, asset => asset.Id);

            serializedConfig.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();
            Debug.Log($"[Whitebox] Location 2 configs: {created} asset(s) created, {added} added.");
            return true;
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

        private static string EnsureFolder(string parent, string name)
        {
            string path = parent + "/" + name;
            if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, name);
            return path;
        }

        private static T GetOrCreate<T>(string folder, string assetName, ref int created, System.Action<T> fill) where T : ScriptableObject
        {
            string path = folder + "/" + assetName + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null)
            {
                fill(existing);
                return existing;
            }

            var asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            fill(asset);
            created++;
            return asset;
        }

        private static void FillServiceType(ServiceTypeConfig asset, ServiceTypeSpec spec)
        {
            var serialized = new SerializedObject(asset);
            serialized.FindProperty("_id").stringValue = spec.Id;
            serialized.FindProperty("_displayName").stringValue = spec.DisplayName;
            serialized.FindProperty("_kind").intValue = (int)PointKind.Service;
            serialized.FindProperty("_basePrice").longValue = spec.BasePrice;
            serialized.FindProperty("_pricePerSecond").floatValue = 0f;
            serialized.FindProperty("_serviceDuration").floatValue = spec.Duration;
            serialized.FindProperty("_acceptDelay").floatValue = 1f;
            serialized.FindProperty("_clearDelay").floatValue = 1f;
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
                    if (element == asset || (element is T other && idOf(other) == idOf(asset))) present = true;
                }
                if (present) continue;
                array.arraySize++;
                array.GetArrayElementAtIndex(array.arraySize - 1).objectReferenceValue = asset;
                added++;
            }
            return added;
        }

        private readonly struct SupplySpec
        {
            public SupplySpec(string assetName, string id, string displayName, long boxPrice, int unitsPerBox)
            {
                AssetName = assetName; Id = id; DisplayName = displayName; BoxPrice = boxPrice; UnitsPerBox = unitsPerBox;
            }
            public string AssetName { get; } public string Id { get; } public string DisplayName { get; }
            public long BoxPrice { get; } public int UnitsPerBox { get; }
        }

        private readonly struct ServiceTypeSpec
        {
            public ServiceTypeSpec(string assetName, string id, string displayName, long basePrice, float duration)
            {
                AssetName = assetName; Id = id; DisplayName = displayName; BasePrice = basePrice; Duration = duration;
            }
            public string AssetName { get; } public string Id { get; } public string DisplayName { get; }
            public long BasePrice { get; } public float Duration { get; }
        }

        private readonly struct BuildableSpec
        {
            public BuildableSpec(string assetName, string id, string displayName, string description, BuildableKind kind, string targetId, long cost, int requiredLevel, float flowBonus)
            {
                AssetName = assetName; Id = id; DisplayName = displayName; Description = description; Kind = kind;
                TargetId = targetId; Cost = cost; RequiredLevel = requiredLevel; FlowBonus = flowBonus;
            }
            public string AssetName { get; } public string Id { get; } public string DisplayName { get; }
            public string Description { get; } public BuildableKind Kind { get; } public string TargetId { get; }
            public long Cost { get; } public int RequiredLevel { get; } public float FlowBonus { get; }
        }
    }
}
