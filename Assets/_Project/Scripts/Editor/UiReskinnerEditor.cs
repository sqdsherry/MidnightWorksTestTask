using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using AutoService.Presentation.Ui;

namespace AutoService.Editor
{
    public class UiReskinnerEditor : UnityEditor.Editor
    {
        [MenuItem("Tools/Reskin UI (12b)")]
        public static void ReskinUI()
        {
            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Project" });
            
            foreach (string guid in prefabGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;

                bool changed = false;

                // 1. Buttons
                Button[] buttons = prefab.GetComponentsInChildren<Button>(true);
                foreach (var btn in buttons)
                {
                    if (btn.GetComponent<ButtonAnimator>() == null)
                    {
                        btn.gameObject.AddComponent<ButtonAnimator>();
                        changed = true;
                    }

                    // Use Unity's built-in rounded Background sprite instead of default UISprite
                    Image img = btn.GetComponent<Image>();
                    if (img != null && img.sprite != null && img.sprite.name == "UISprite")
                    {
                        Sprite bgSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");
                        if (bgSprite != null)
                        {
                            img.sprite = bgSprite;
                            img.type = Image.Type.Sliced;
                        }
                        img.color = new Color(0.9f, 0.9f, 0.92f); // Slightly cool off-white
                        changed = true;
                    }
                }

                // 2. Windows / Panels
                // Usually Panels have CanvasGroup, or they are named *Panel
                CanvasGroup[] groups = prefab.GetComponentsInChildren<CanvasGroup>(true);
                foreach (var group in groups)
                {
                    if (group.GetComponent<UiWindowAnimator>() == null && 
                        (group.name.Contains("Panel") || group.name.Contains("Window") || group.name.Contains("Menu")))
                    {
                        group.gameObject.AddComponent<UiWindowAnimator>();
                        
                        Image img = group.GetComponent<Image>();
                        if (img != null && img.sprite != null && (img.sprite.name == "UISprite" || img.sprite.name == "Background"))
                        {
                            Sprite bgSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");
                            if (bgSprite != null)
                            {
                                img.sprite = bgSprite;
                                img.type = Image.Type.Sliced;
                            }
                            img.color = new Color(0.95f, 0.95f, 0.95f, 0.98f);
                        }
                        
                        changed = true;
                    }
                }

                // 3. Fonts
                // Usually TMP fonts are used, let's just make sure all TMP texts have default font or style
                TMP_Text[] texts = prefab.GetComponentsInChildren<TMP_Text>(true);
                foreach (var txt in texts)
                {
                    // If we want to assign a specific font, we'd load it here.
                    // For now, we just ensure it's not missing.
                    if (txt.font == null)
                    {
                        TMP_FontAsset defaultFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
                        if (defaultFont != null)
                        {
                            txt.font = defaultFont;
                            changed = true;
                        }
                    }
                }

                if (changed)
                {
                    EditorUtility.SetDirty(prefab);
                    Debug.Log($"[UiReskinner] Reskinned prefab: {prefab.name}");
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[UiReskinner] UI Reskin completed successfully!");
        }
    }
}
