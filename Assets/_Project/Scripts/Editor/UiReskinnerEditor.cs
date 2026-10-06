using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using AutoService.Presentation.Ui;
using AutoService.Presentation.Panels;

namespace AutoService.Editor
{
    public class UiReskinnerEditor : UnityEditor.Editor
    {
        private const string KenneyUiRoot = "Assets/_Project/Art/Kenney/kenney_ui-pack";

        [MenuItem("Tools/Reskin UI (12b)")]
        [MenuItem("Tools/Reskin UI")]
        public static void ReskinUI()
        {
            Sprite panelSprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{KenneyUiRoot}/Grey/Default/button_rectangle_depth_flat.png")
                ?? AssetDatabase.LoadAssetAtPath<Sprite>($"{KenneyUiRoot}/Grey/Default/button_rectangle_border.png");
            
            Sprite greenBtnSprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{KenneyUiRoot}/Green/Default/button_rectangle_depth_flat.png");
            Sprite blueBtnSprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{KenneyUiRoot}/Blue/Default/button_rectangle_depth_flat.png");
            Sprite redBtnSprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{KenneyUiRoot}/Red/Default/button_rectangle_depth_flat.png");
            Sprite greyBtnSprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{KenneyUiRoot}/Grey/Default/button_rectangle_depth_flat.png");

            Sprite redSquareBtnSprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{KenneyUiRoot}/Red/Default/button_square_depth_flat.png")
                ?? AssetDatabase.LoadAssetAtPath<Sprite>($"{KenneyUiRoot}/Red/Default/button_square_flat.png")
                ?? redBtnSprite;

            if (panelSprite == null || greenBtnSprite == null)
            {
                Debug.LogError("[UiReskinner] Kenney UI sprites are not imported as Sprites! Run 'AutoService -> Setup -> Configure Kenney UI Sprites' first.");
                return;
            }

            Sprite fallbackBg = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");

            // Process prefabs in Assets/_Project
            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Project" });
            foreach (string guid in prefabGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;

                if (ReskinGameObject(prefab, panelSprite, greenBtnSprite, blueBtnSprite, redBtnSprite, redSquareBtnSprite, greyBtnSprite, fallbackBg))
                {
                    EditorUtility.SetDirty(prefab);
                    Debug.Log($"[UiReskinner] Reskinned prefab: {prefab.name}");
                }
            }

            // Also reskin any active/inactive UI panels in currently opened scenes (PointPanel, StorekeeperPanel, BuildPanel, PauseMenu, SettingsPanel)
            Canvas[] sceneCanvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            bool sceneDirty = false;
            foreach (Canvas canvas in sceneCanvases)
            {
                if (ReskinGameObject(canvas.gameObject, panelSprite, greenBtnSprite, blueBtnSprite, redBtnSprite, redSquareBtnSprite, greyBtnSprite, fallbackBg))
                {
                    sceneDirty = true;
                }
            }

            if (sceneDirty)
            {
                UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[UiReskinner] UI Reskin with Kenney UI completed successfully!");
        }

        private static bool ReskinGameObject(
            GameObject root,
            Sprite panelSprite,
            Sprite greenBtn,
            Sprite blueBtn,
            Sprite redBtn,
            Sprite redSquareBtn,
            Sprite greyBtn,
            Sprite fallbackBg)
        {
            bool changed = false;

            // 1. Buttons
            Button[] buttons = root.GetComponentsInChildren<Button>(true);
            foreach (var btn in buttons)
            {
                if (btn.GetComponent<ButtonAnimator>() == null)
                {
                    btn.gameObject.AddComponent<ButtonAnimator>();
                    changed = true;
                }

                Image img = btn.GetComponent<Image>();
                if (img != null)
                {
                    string btnName = btn.name.ToLowerInvariant();
                    Sprite chosenSprite = greenBtn ?? fallbackBg;

                    if (btnName.Contains("close") || btnName.Contains("cancel") || btnName.Contains("quit") || btnName.Contains("exit"))
                    {
                        chosenSprite = redSquareBtn ?? redBtn ?? greyBtn ?? fallbackBg;
                        RectTransform rt = (RectTransform)btn.transform;
                        if (rt.sizeDelta.x > 36f || rt.sizeDelta.y > 36f)
                        {
                            rt.sizeDelta = new Vector2(32f, 32f);
                            changed = true;
                        }
                    }
                    else if (btnName.Contains("settings") || btnName.Contains("pause") || btnName.Contains("back") || btnName.Contains("menu"))
                    {
                        chosenSprite = greyBtn ?? blueBtn ?? fallbackBg;
                    }
                    else if (btnName.Contains("buy") || btnName.Contains("upgrade") || btnName.Contains("hire") || btnName.Contains("build"))
                    {
                        chosenSprite = greenBtn ?? blueBtn ?? fallbackBg;
                    }
                    else if (blueBtn != null)
                    {
                        chosenSprite = blueBtn;
                    }

                    if (chosenSprite != null && img.sprite != chosenSprite)
                    {
                        img.sprite = chosenSprite;
                        img.type = Image.Type.Sliced;
                        img.color = Color.white;
                        changed = true;
                    }
                }
            }

            // 2. Windows / Panels
            CanvasGroup[] groups = root.GetComponentsInChildren<CanvasGroup>(true);
            foreach (var group in groups)
            {
                string groupName = group.name.ToLowerInvariant();
                if (group.GetComponent<UiWindowAnimator>() == null &&
                    (groupName.Contains("panel") || groupName.Contains("window") || groupName.Contains("menu") || groupName.Contains("dialog")))
                {
                    group.gameObject.AddComponent<UiWindowAnimator>();
                    changed = true;
                }

                Image img = group.GetComponent<Image>();
                if (img != null)
                {
                    Sprite chosenPanel = panelSprite ?? fallbackBg;
                    if (chosenPanel != null && img.sprite != chosenPanel)
                    {
                        img.sprite = chosenPanel;
                        img.type = Image.Type.Sliced;
                        img.color = new Color(0.95f, 0.95f, 0.98f, 0.98f);
                        changed = true;
                    }
                }
            }

            // 3. Child named "Panel" or "Background" inside dialog roots
            Image[] allImages = root.GetComponentsInChildren<Image>(true);
            foreach (var img in allImages)
            {
                string name = img.name.ToLowerInvariant();
                if ((name == "panel" || name == "background" || name == "window") && img.GetComponent<Button>() == null)
                {
                    Sprite chosenPanel = panelSprite ?? fallbackBg;
                    if (chosenPanel != null && img.sprite != chosenPanel)
                    {
                        img.sprite = chosenPanel;
                        img.type = Image.Type.Sliced;
                        img.color = new Color(0.95f, 0.95f, 0.98f, 0.98f);
                        changed = true;
                    }
                }
            }

            // 4. Offer Panels (BuildPanel, StorekeeperPanel) uniform styling
            var offerPanels = root.GetComponentsInChildren<OfferPanelView>(true);
            foreach (var offer in offerPanels)
            {
                Transform panelTrans = offer.transform.Find("Panel");
                if (panelTrans != null)
                {
                    RectTransform panelRt = (RectTransform)panelTrans;
                    panelRt.sizeDelta = new Vector2(360f, 180f);

                    Transform titleTr = panelTrans.Find("Title");
                    if (titleTr != null)
                    {
                        RectTransform tr = (RectTransform)titleTr;
                        tr.anchoredPosition = new Vector2(0f, -18f);
                        tr.sizeDelta = new Vector2(-60f, 36f);
                        var t = titleTr.GetComponent<TMP_Text>();
                        if (t != null)
                        {
                            t.color = new Color32(0x1E, 0x24, 0x30, 0xFF);
                            t.fontSize = 24f;
                            t.fontStyle = FontStyles.Bold;
                        }
                    }

                    // Hide description completely for a clean minimalistic buy window
                    Transform descTr = panelTrans.Find("Description");
                    if (descTr != null && descTr.gameObject.activeSelf)
                    {
                        descTr.gameObject.SetActive(false);
                        changed = true;
                    }

                    // Hide separate cost and requirement to keep panel uncluttered and clean
                    Transform costTr = panelTrans.Find("Cost");
                    if (costTr != null && costTr.gameObject.activeSelf)
                    {
                        costTr.gameObject.SetActive(false);
                        changed = true;
                    }

                    Transform reqTr = panelTrans.Find("Requirement");
                    if (reqTr != null)
                    {
                        RectTransform rr = (RectTransform)reqTr;
                        rr.anchorMin = new Vector2(0f, 1f);
                        rr.anchorMax = new Vector2(1f, 1f);
                        rr.pivot = new Vector2(0.5f, 1f);
                        rr.anchoredPosition = new Vector2(0f, -68f);
                        rr.sizeDelta = new Vector2(-40f, 28f);
                        var rt = reqTr.GetComponent<TMP_Text>();
                        if (rt != null)
                        {
                            rt.color = new Color(0.12f, 0.14f, 0.19f, 1f);
                            rt.alignment = TextAlignmentOptions.Center;
                        }
                        changed = true;
                    }

                    Transform btnTr = panelTrans.Find("BuildButton") ?? panelTrans.Find("BuildButton ");
                    if (btnTr != null)
                    {
                        RectTransform br = (RectTransform)btnTr;
                        br.anchorMin = new Vector2(0.5f, 0f);
                        br.anchorMax = new Vector2(0.5f, 0f);
                        br.pivot = new Vector2(0.5f, 0f);
                        br.anchoredPosition = new Vector2(0f, 16f);
                        br.sizeDelta = new Vector2(320f, 48f);
                    }

                    Transform closeTr = panelTrans.Find("CloseButton");
                    if (closeTr != null)
                    {
                        RectTransform cr = (RectTransform)closeTr;
                        cr.anchorMin = new Vector2(1f, 1f);
                        cr.anchorMax = new Vector2(1f, 1f);
                        cr.pivot = new Vector2(1f, 1f);
                        cr.anchoredPosition = new Vector2(-4f, -4f);
                        cr.sizeDelta = new Vector2(32f, 32f);
                    }
                    changed = true;
                }
            }

            // 5. Fonts & Text readability
            TMP_Text[] texts = root.GetComponentsInChildren<TMP_Text>(true);
            foreach (var txt in texts)
            {
                if (txt.font == null)
                {
                    TMP_FontAsset defaultFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
                    if (defaultFont != null)
                    {
                        txt.font = defaultFont;
                        changed = true;
                    }
                }

                // If text is sitting on a light panel background and is white/light, make it dark readable
                if (txt.transform.parent != null && 
                    (txt.transform.parent.name == "Panel" || txt.transform.parent.name == "Background") &&
                    txt.GetComponentInParent<Button>() == null)
                {
                    if (txt.name.ToLowerInvariant().Contains("desc") || txt.name.ToLowerInvariant().Contains("status"))
                    {
                        txt.color = new Color32(0x22, 0x2B, 0x38, 0xFF);
                        changed = true;
                    }
                    else if (txt.name.ToLowerInvariant().Contains("title"))
                    {
                        txt.color = new Color32(0x1E, 0x24, 0x30, 0xFF);
                        changed = true;
                    }
                }
            }

            return changed;
        }
    }
}
