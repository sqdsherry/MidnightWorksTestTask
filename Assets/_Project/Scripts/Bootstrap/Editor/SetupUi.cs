using AutoService.Presentation.Building;
using AutoService.Presentation.Ui;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace AutoService.Bootstrap.Editor
{
    /// <summary>
    /// uGUI building blocks of the setup tools in the shell's style (prompt 09a §4.5): canvases, panels, texts, buttons
    /// and the default slider / toggle / dropdown controls resized for a 1920×1080 canvas.
    /// </summary>
    internal static class SetupUi
    {
        /// <summary>Panel background.</summary>
        public static readonly Color PanelColor = new Color32(0x1E, 0x24, 0x30, 235);

        /// <summary>Full-screen dimmer behind modal panels.</summary>
        public static readonly Color DimColor = new Color32(0x00, 0x00, 0x00, 140);

        /// <summary>Main action button.</summary>
        public static readonly Color PrimaryColor = new Color32(0x43, 0xA0, 0x47, 0xFF);

        /// <summary>Secondary button.</summary>
        public static readonly Color SecondaryColor = new Color32(0x3A, 0x42, 0x50, 0xFF);

        /// <summary>Disabled button.</summary>
        public static readonly Color DisabledColor = new Color32(0x5A, 0x5F, 0x66, 0xFF);

        /// <summary>Size of a menu button.</summary>
        public static readonly Vector2 ButtonSize = new Vector2(360f, 64f);

        /// <summary>Gap between menu buttons.</summary>
        public const float ButtonSpacing = 16f;

        /// <summary>Font size of panel titles.</summary>
        public const float TitleFont = 48f;

        /// <summary>Font size of button labels.</summary>
        public const float ButtonFont = 26f;

        /// <summary>Font size of ordinary labels.</summary>
        public const float TextFont = 28f;

        private const int UiLayer = 5;
        private static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);

        /// <summary>Creates a screen-space overlay canvas (Scale With Screen Size 1920×1080, match 0.5).</summary>
        public static Canvas CreateCanvas(string name, Transform parent, int sortingOrder)
        {
            var root = new GameObject(name, typeof(RectTransform));
            root.layer = UiLayer;
            if (parent != null)
            {
                root.transform.SetParent(parent, false);
            }

            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ReferenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            root.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        /// <summary>Creates a child UI object.</summary>
        public static RectTransform CreateRect(string name, Transform parent)
        {
            var child = new GameObject(name, typeof(RectTransform));
            child.layer = UiLayer;
            child.transform.SetParent(parent, false);
            return (RectTransform)child.transform;
        }

        /// <summary>Fills the parent.</summary>
        public static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
        }

        /// <summary>Places a fixed-size rect at an anchor.</summary>
        public static void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size, Vector2 pivot)
        {
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        /// <summary>A full-width band at a fixed distance from the parent's top, inset on both sides.</summary>
        public static void TopBand(RectTransform rect, float top, float height, float inset)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, top);
            rect.sizeDelta = new Vector2(-inset * 2f, height);
        }

        /// <summary>Adds a sliced UI sprite image of one color.</summary>
        public static Image AddImage(RectTransform rect, Color color)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            image.type = Image.Type.Sliced;
            image.color = color;
            return image;
        }

        /// <summary>Creates a full-screen dimmer that also blocks clicks on everything behind it.</summary>
        public static RectTransform CreateDimmedRoot(string name, Transform parent)
        {
            RectTransform root = CreateRect(name, parent);
            Stretch(root);
            var image = root.gameObject.AddComponent<Image>();
            image.color = DimColor;
            return root;
        }

        /// <summary>Creates a centered panel with the panel background.</summary>
        public static RectTransform CreatePanel(string name, Transform parent, Vector2 size, Vector2 position)
        {
            RectTransform panel = CreateRect(name, parent);
            Place(panel, new Vector2(0.5f, 0.5f), position, size, new Vector2(0.5f, 0.5f));
            AddImage(panel, PanelColor);
            return panel;
        }

        /// <summary>Creates a TextMeshPro label.</summary>
        public static TMP_Text CreateText(
            Transform parent, string name, string text, float fontSize, bool bold, TextAlignmentOptions alignment, bool wrap = false)
        {
            RectTransform rect = CreateRect(name, parent);
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = fontSize;
            label.color = Color.white;
            label.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
            label.alignment = alignment;
            label.raycastTarget = false;
            label.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            label.overflowMode = wrap ? TextOverflowModes.Overflow : TextOverflowModes.Ellipsis;
            return label;
        }

        /// <summary>
        /// Creates a button whose tint is its whole color (lighter on hover, gray when disabled) with a bold label and
        /// <see cref="ButtonJuice"/>.
        /// </summary>
        public static Button CreateButton(Transform parent, string name, string text, Color color, Vector2 size)
        {
            RectTransform rect = CreateRect(name, parent);
            rect.sizeDelta = size;
            Image image = AddImage(rect, Color.white);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.normalColor = color;
            colors.highlightedColor = Color.Lerp(color, Color.white, 0.2f);
            colors.pressedColor = Color.Lerp(color, Color.black, 0.2f);
            colors.selectedColor = color;
            colors.disabledColor = DisabledColor;
            colors.colorMultiplier = 1f;
            button.colors = colors;

            // Why: mouse-only game — a button left "selected" after a click would otherwise react to Enter/Space.
            button.navigation = new Navigation { mode = Navigation.Mode.None };

            TMP_Text label = CreateText(rect, "Label", text, ButtonFont, true, TextAlignmentOptions.Center);
            Stretch(label.rectTransform);
            rect.gameObject.AddComponent<ButtonJuice>();
            return button;
        }

        /// <summary>
        /// Creates a vertical column of buttons centered on <paramref name="center"/> (top to bottom); the first one is
        /// primary, the others secondary.
        /// </summary>
        public static Button[] CreateButtonColumn(Transform parent, Vector2 center, params string[] labels)
        {
            var buttons = new Button[labels.Length];
            float height = labels.Length * ButtonSize.y + (labels.Length - 1) * ButtonSpacing;
            float top = center.y + height * 0.5f;
            for (int i = 0; i < labels.Length; i++)
            {
                Color color = i == 0 ? PrimaryColor : SecondaryColor;
                Button button = CreateButton(parent, labels[i].Replace(" ", string.Empty) + "Button", labels[i], color, ButtonSize);
                float y = top - ButtonSize.y * 0.5f - i * (ButtonSize.y + ButtonSpacing);
                Place((RectTransform)button.transform, new Vector2(0.5f, 0.5f), new Vector2(center.x, y), ButtonSize, new Vector2(0.5f, 0.5f));
                buttons[i] = button;
            }

            return buttons;
        }

        /// <summary>Default uGUI slider in [0, 1], resized and with the primary color fill.</summary>
        public static Slider CreateSlider(Transform parent, string name)
        {
            GameObject created = DefaultControls.CreateSlider(UguiResources());
            Adopt(created, parent, name);
            var slider = created.GetComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.navigation = new Navigation { mode = Navigation.Mode.None };
            Transform fill = created.transform.Find("Fill Area/Fill");
            if (fill != null && fill.TryGetComponent(out Image fillImage))
            {
                fillImage.color = PrimaryColor;
            }

            return slider;
        }

        /// <summary>Default uGUI toggle without its legacy text label (the row has its own), as a square box.</summary>
        public static Toggle CreateToggle(Transform parent, string name)
        {
            GameObject created = DefaultControls.CreateToggle(UguiResources());
            Adopt(created, parent, name);
            Transform label = created.transform.Find("Label");
            if (label != null)
            {
                Object.DestroyImmediate(label.gameObject);
            }

            Transform background = created.transform.Find("Background");
            if (background != null)
            {
                Stretch((RectTransform)background);
                Transform checkmark = background.Find("Checkmark");
                if (checkmark != null)
                {
                    Stretch((RectTransform)checkmark);
                }
            }

            var toggle = created.GetComponent<Toggle>();
            toggle.navigation = new Navigation { mode = Navigation.Mode.None };
            return toggle;
        }

        /// <summary>Default TextMeshPro dropdown with larger texts and rows.</summary>
        public static TMP_Dropdown CreateDropdown(Transform parent, string name)
        {
            GameObject created = TMP_DefaultControls.CreateDropdown(TmpResources());
            Adopt(created, parent, name);
            var dropdown = created.GetComponent<TMP_Dropdown>();
            dropdown.navigation = new Navigation { mode = Navigation.Mode.None };
            const float itemHeight = 40f;
            if (dropdown.captionText != null)
            {
                dropdown.captionText.fontSize = 24f;
            }

            if (dropdown.itemText != null)
            {
                dropdown.itemText.fontSize = 22f;
            }

            RectTransform template = dropdown.template;
            if (template != null)
            {
                template.sizeDelta = new Vector2(template.sizeDelta.x, itemHeight * 6f);
                if (template.Find("Viewport/Content") is RectTransform content)
                {
                    content.sizeDelta = new Vector2(content.sizeDelta.x, itemHeight);
                    if (content.Find("Item") is RectTransform item)
                    {
                        item.sizeDelta = new Vector2(item.sizeDelta.x, itemHeight);
                    }
                }
            }

            return dropdown;
        }

        /// <summary>Marks an object as created by a setup tool, so a re-run may replace it.</summary>
        public static void MarkGenerated(GameObject target)
        {
            if (!target.TryGetComponent(out WhiteboxGenerated _))
            {
                target.AddComponent<WhiteboxGenerated>();
            }
        }

        /// <summary>True when <paramref name="target"/> was created by a setup tool.</summary>
        public static bool IsGenerated(GameObject target) => target.TryGetComponent(out WhiteboxGenerated _);

        private static void Adopt(GameObject created, Transform parent, string name)
        {
            created.name = name;
            created.transform.SetParent(parent, false);
            SetLayerRecursively(created.transform, UiLayer);
        }

        private static void SetLayerRecursively(Transform root, int layer)
        {
            root.gameObject.layer = layer;
            for (int i = 0; i < root.childCount; i++)
            {
                SetLayerRecursively(root.GetChild(i), layer);
            }
        }

        // The sprites the GameObject → UI menu uses.
        private static DefaultControls.Resources UguiResources()
        {
            return new DefaultControls.Resources
            {
                standard = Builtin("UI/Skin/UISprite.psd"),
                background = Builtin("UI/Skin/Background.psd"),
                inputField = Builtin("UI/Skin/InputFieldBackground.psd"),
                knob = Builtin("UI/Skin/Knob.psd"),
                checkmark = Builtin("UI/Skin/Checkmark.psd"),
                dropdown = Builtin("UI/Skin/DropdownArrow.psd"),
                mask = Builtin("UI/Skin/UIMask.psd"),
            };
        }

        private static TMP_DefaultControls.Resources TmpResources()
        {
            return new TMP_DefaultControls.Resources
            {
                standard = Builtin("UI/Skin/UISprite.psd"),
                background = Builtin("UI/Skin/Background.psd"),
                inputField = Builtin("UI/Skin/InputFieldBackground.psd"),
                knob = Builtin("UI/Skin/Knob.psd"),
                checkmark = Builtin("UI/Skin/Checkmark.psd"),
                dropdown = Builtin("UI/Skin/DropdownArrow.psd"),
                mask = Builtin("UI/Skin/UIMask.psd"),
            };
        }

        private static Sprite Builtin(string path) => AssetDatabase.GetBuiltinExtraResource<Sprite>(path);
    }
}
