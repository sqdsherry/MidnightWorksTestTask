using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace AutoService.Bootstrap.Editor
{
    public static class KenneyImporter
    {
        private static readonly (string pack, string regex)[] Whitelist = new[]
        {
            (""kenney_car-kit"", ""^(sedan|suv|sedan-sports|wheel-default|wheel-dark|debris-tire|cone|box)\\.fbx$""),
            (""kenney_cityKitRoads_1.1"", ""^(road_straight|road_bend|road_curve|road_crossroad|road_intersection|road_sideEntry|road_sideExit|road_drivewayDouble|road_drivewaySingle|road_square|tile_low|light_square|light_curved)\\.fbx$""),
            (""kenney_city-kit-commercial_2.1"", ""^(building-[a-n]|low-detail-building-.*|building-skyscraper-.*|detail-awning(-wide)?|detail-overhang|detail-parasol-a|cover-window)\\.fbx$""),
            (""kenney_conveyor-kit"", ""^(structure-doorway-wide|door-wide-open|structure-wall|structure-window(-wide)?|structure-corner-.*|top(-large)?|floor(-large)?|scanner-high|cover|cover-hopper|robot-arm-a|box-small|box-long|box-wide|box-large|conveyor-long|structure-yellow-.*)\\.fbx$""),
            (""kenney_racing-kit"", ""^(barrierRed|barrierWhite|fenceStraight|pylon|lightPostModern|treeLarge|treeSmall|billboard)\\.fbx$""),
            (""kenney_cityKitSuburban"", ""^(tree_large|tree_small|fence_.*|path_.*)\\.fbx$""),
            (""kenney_mini-characters"", ""^character-(male|female)-.*\\.fbx$"")
        };

        [MenuItem(""AutoService/Setup/Import Kenney Assets"")]
        public static void ImportAssets()
        {
            string projectDir = Directory.GetParent(Application.dataPath).FullName;
            string downloadDir = Path.Combine(projectDir, ""Donwload"");
            if (!Directory.Exists(downloadDir))
            {
                Debug.LogError($""[KenneyImporter] Download directory not found at {downloadDir}. Please download the assets."");
                return;
            }

            string targetRoot = Path.Combine(Application.dataPath, ""_Project/Art/Kenney"");
            Directory.CreateDirectory(targetRoot);

            bool importedAny = false;

            foreach (var group in Whitelist)
            {
                string packDir = Path.Combine(downloadDir, group.pack);
                if (!Directory.Exists(packDir))
                {
                    Debug.LogWarning($""[KenneyImporter] Pack {group.pack} not found in {downloadDir}."");
                    continue;
                }

                string modelsDir = Path.Combine(packDir, ""Models"", ""FBX format"");
                if (!Directory.Exists(modelsDir)) continue;

                string targetPackDir = Path.Combine(targetRoot, group.pack);
                Directory.CreateDirectory(targetPackDir);

                // Copy texture if exists
                string texDir = Path.Combine(packDir, ""Models"", ""Textures"");
                string texFile = Path.Combine(texDir, ""colormap.png"");
                if (File.Exists(texFile))
                {
                    string targetTexDir = Path.Combine(targetPackDir, ""Textures"");
                    Directory.CreateDirectory(targetTexDir);
                    string targetTexFile = Path.Combine(targetTexDir, ""colormap.png"");
                    if (!File.Exists(targetTexFile)) File.Copy(texFile, targetTexFile);
                }
                
                // Copy license
                string licenseFile = Path.Combine(packDir, ""License.txt"");
                if (File.Exists(licenseFile))
                {
                    string targetLicense = Path.Combine(targetPackDir, ""License.txt"");
                    if (!File.Exists(targetLicense)) File.Copy(licenseFile, targetLicense);
                }

                var regex = new Regex(group.regex, RegexOptions.IgnoreCase);
                foreach (string fbx in Directory.GetFiles(modelsDir, ""*.fbx""))
                {
                    string fileName = Path.GetFileName(fbx);
                    if (regex.IsMatch(fileName))
                    {
                        string targetPath = Path.Combine(targetPackDir, fileName);
                        if (!File.Exists(targetPath))
                        {
                            File.Copy(fbx, targetPath);
                            importedAny = true;
                        }
                    }
                }
            }

            if (importedAny)
            {
                AssetDatabase.Refresh();
                ConfigureImports();
                FixMaterials();
                CreateWrappers();
                Debug.Log(""[KenneyImporter] Imported Kenney assets successfully."");
            }
            else
            {
                Debug.Log(""[KenneyImporter] Kenney assets already imported or missing."");
            }
        }

        private static void ConfigureImports()
        {
            string searchDir = ""Assets/_Project/Art/Kenney"";
            string[] guids = AssetDatabase.FindAssets(""t:Model"", new[] { searchDir });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer == null) continue;

                bool isCharacter = path.Contains(""kenney_mini-characters"");
                bool changed = false;

                if (Mathf.Abs(importer.globalScale - 1f) > 0.001f)
                {
                    importer.globalScale = 1f;
                    changed = true;
                }

                if (importer.materialImportMode != ModelImporterMaterialImportMode.ImportViaMaterialDescription)
                {
                    importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
                    changed = true;
                }

                if (isCharacter)
                {
                    if (!importer.importAnimation)
                    {
                        importer.importAnimation = true;
                        changed = true;
                    }
                    if (importer.animationType != ModelImporterAnimationType.Generic)
                    {
                        importer.animationType = ModelImporterAnimationType.Generic;
                        changed = true;
                    }

                    // Set up clips
                    var clips = importer.defaultClipAnimations;
                    bool clipsChanged = false;
                    for (int i = 0; i < clips.Length; i++)
                    {
                        var clip = clips[i];
                        string name = clip.name.ToLower();
                        if (name.Contains(""idle"") || name.Contains(""walk"") || name.Contains(""holding"") || name.Contains(""interact""))
                        {
                            if (name.Contains(""idle"")) clip.name = ""idle"";
                            else if (name.Contains(""walk"")) clip.name = ""walk"";
                            else if (name.Contains(""holding"")) clip.name = ""holding-both"";
                            else if (name.Contains(""interact"")) clip.name = ""interact"";

                            if (clip.name != ""interact"" && !clip.loopTime)
                            {
                                clip.loopTime = true;
                                clipsChanged = true;
                            }
                        }
                    }
                    if (clipsChanged)
                    {
                        importer.clipAnimations = clips;
                        changed = true;
                    }
                }
                else
                {
                    if (importer.importAnimation)
                    {
                        importer.importAnimation = false;
                        changed = true;
                    }
                }

                if (changed)
                {
                    importer.SaveAndReimport();
                }
            }
        }

        private static void FixMaterials()
        {
            string searchDir = ""Assets/_Project/Art/Kenney"";
            string[] guids = AssetDatabase.FindAssets(""t:Material"", new[] { searchDir });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat != null)
                {
                    bool dirty = false;
                    if (mat.HasProperty(""_Smoothness"") && Mathf.Abs(mat.GetFloat(""_Smoothness"") - 0.1f) > 0.001f)
                    {
                        mat.SetFloat(""_Smoothness"", 0.1f);
                        dirty = true;
                    }
                    if (mat.HasProperty(""_Metallic"") && mat.GetFloat(""_Metallic"") != 0f)
                    {
                        mat.SetFloat(""_Metallic"", 0f);
                        dirty = true;
                    }
                    if (mat.HasProperty(""_SpecularHighlights"") && mat.GetFloat(""_SpecularHighlights"") != 0f)
                    {
                        mat.SetFloat(""_SpecularHighlights"", 0f);
                        dirty = true;
                    }
                    if (dirty)
                    {
                        EditorUtility.SetDirty(mat);
                    }
                }
            }
            AssetDatabase.SaveAssets();
        }
    }
}
