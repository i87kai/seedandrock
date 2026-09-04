using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SeedAndRock.UI
{
    /// <summary>
    /// Runtime UI factory. Builds consistently styled widgets (rounded cards, buttons with hover/pressed
    /// motion, inputs, scroll views, progress bars) so screens only describe layout and behaviour.
    /// </summary>
    public static class UiKit
    {
        private static Sprite roundedSprite;
        private static Sprite softSprite;

        /// <summary>9-sliced rounded rectangle generated once at runtime, so no texture assets are required.</summary>
        public static Sprite Rounded
        {
            get
            {
                if (roundedSprite == null) roundedSprite = BuildRoundedSprite(48, 12, false);
                return roundedSprite;
            }
        }

        /// <summary>Rounded rectangle with a soft alpha falloff, used for glows and shadows.</summary>
        public static Sprite Soft
        {
            get
            {
                if (softSprite == null) softSprite = BuildRoundedSprite(64, 26, true);
                return softSprite;
            }
        }

        private static Sprite BuildRoundedSprite(int size, int radius, bool soft)
        {
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = soft ? "SR_UI_Soft" : "SR_UI_Rounded", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            Color32[] pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Max(radius - x - 0.5f, x + 0.5f - (size - radius), 0f);
                    float dy = Mathf.Max(radius - y - 0.5f, y + 0.5f - (size - radius), 0f);
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = soft ? Mathf.Clamp01(1f - distance / radius) : Mathf.Clamp01(radius - distance + 0.5f);
                    if (soft) alpha *= alpha;
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            float border = radius + 1;
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(border, border, border, border));
        }

        // ------------------------------------------------------------------ rect helpers

        public static RectTransform RectOf(GameObject gameObject) => gameObject.GetComponent<RectTransform>();

        public static void Stretch(RectTransform rect, float left = 0f, float bottom = 0f, float right = 0f, float top = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }

        public static void Anchor(RectTransform rect, Vector2 anchor, Vector2 size, Vector2 offset = default)
        {
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.sizeDelta = size;
            rect.anchoredPosition = offset;
        }

        public static GameObject CreateObject(string name, Transform parent, params System.Type[] components)
        {
            GameObject gameObject = new GameObject(name, components);
            gameObject.layer = LayerMask.NameToLayer("UI");
            gameObject.transform.SetParent(parent, false);
            if (gameObject.GetComponent<RectTransform>() == null) gameObject.AddComponent<RectTransform>();
            return gameObject;
        }

        // ------------------------------------------------------------------ containers

        public static Image CreatePanel(Transform parent, string name, Color color, bool rounded = true, bool raycast = false)
        {
            GameObject gameObject = CreateObject(name, parent, typeof(Image));
            Image image = gameObject.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = raycast;
            if (rounded)
            {
                image.sprite = Rounded;
                image.type = Image.Type.Sliced;
                image.pixelsPerUnitMultiplier = 1f;
            }

            return image;
        }

        /// <summary>A raised card with a faint border and drop shadow.</summary>
        public static RectTransform CreateCard(Transform parent, string name, Vector2 size, Vector2 anchor, Color? fill = null, Vector2 offset = default)
        {
            Image shadow = CreatePanel(parent, name + "_Shadow", new Color(0f, 0f, 0f, 0.35f));
            shadow.sprite = Soft;
            Anchor(shadow.rectTransform, anchor, size + new Vector2(28f, 28f), offset + new Vector2(0f, -8f));

            Image border = CreatePanel(parent, name + "_Border", SeedAndRockTheme.Border);
            Anchor(border.rectTransform, anchor, size + new Vector2(2f, 2f), offset);

            Image card = CreatePanel(parent, name, fill ?? SeedAndRockTheme.Panel, true, true);
            Anchor(card.rectTransform, anchor, size, offset);
            return card.rectTransform;
        }

        public static VerticalLayoutGroup CreateColumn(Transform parent, string name, float spacing, RectOffset padding, TextAnchor alignment = TextAnchor.UpperCenter)
        {
            GameObject gameObject = CreateObject(name, parent, typeof(VerticalLayoutGroup));
            VerticalLayoutGroup group = gameObject.GetComponent<VerticalLayoutGroup>();
            group.spacing = spacing;
            group.padding = padding;
            group.childAlignment = alignment;
            group.childControlWidth = true;
            group.childControlHeight = true;
            group.childForceExpandWidth = true;
            group.childForceExpandHeight = false;
            return group;
        }

        public static HorizontalLayoutGroup CreateRow(Transform parent, string name, float spacing, RectOffset padding, TextAnchor alignment = TextAnchor.MiddleCenter)
        {
            GameObject gameObject = CreateObject(name, parent, typeof(HorizontalLayoutGroup));
            HorizontalLayoutGroup group = gameObject.GetComponent<HorizontalLayoutGroup>();
            group.spacing = spacing;
            group.padding = padding;
            group.childAlignment = alignment;
            group.childControlWidth = true;
            group.childControlHeight = true;
            group.childForceExpandWidth = false;
            group.childForceExpandHeight = false;
            return group;
        }

        public static LayoutElement Size(Component component, float? width = null, float? height = null, float flexibleWidth = 0f)
        {
            LayoutElement element = component.GetComponent<LayoutElement>() ?? component.gameObject.AddComponent<LayoutElement>();
            if (width.HasValue) element.preferredWidth = width.Value;
            if (height.HasValue) { element.preferredHeight = height.Value; element.minHeight = height.Value; }
            element.flexibleWidth = flexibleWidth;
            return element;
        }

        public static RectTransform CreateSpacer(Transform parent, float height)
        {
            GameObject gameObject = CreateObject("Spacer", parent, typeof(LayoutElement));
            LayoutElement element = gameObject.GetComponent<LayoutElement>();
            element.minHeight = height;
            element.preferredHeight = height;
            return RectOf(gameObject);
        }

        public static Image CreateDivider(Transform parent, float alpha = 0.35f)
        {
            Image image = CreatePanel(parent, "Divider", new Color(SeedAndRockTheme.Teal.r, SeedAndRockTheme.Teal.g, SeedAndRockTheme.Teal.b, alpha), false);
            Size(image, null, 1f);
            return image;
        }

        // ------------------------------------------------------------------ text

        public static TMP_Text CreateText(Transform parent, string name, string content, float size, Color color, FontStyles style = FontStyles.Normal, TextAlignmentOptions alignment = TextAlignmentOptions.Center)
        {
            GameObject gameObject = CreateObject(name, parent, typeof(TextMeshProUGUI));
            TextMeshProUGUI text = gameObject.GetComponent<TextMeshProUGUI>();
            text.font = TMP_Settings.defaultFontAsset;
            text.text = content;
            text.fontSize = size;
            text.color = color;
            text.fontStyle = style;
            text.alignment = alignment;
            text.raycastTarget = false;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.textWrappingMode = TextWrappingModes.Normal;
            return text;
        }

        public static TMP_Text CreateLabel(Transform parent, string content, Color? color = null, TextAlignmentOptions alignment = TextAlignmentOptions.Left)
        {
            TMP_Text text = CreateText(parent, "Label", content.ToUpperInvariant(), SeedAndRockTheme.LabelSize, color ?? SeedAndRockTheme.Teal, FontStyles.Bold, alignment);
            text.characterSpacing = 6f;
            return text;
        }

        // ------------------------------------------------------------------ buttons

        public enum ButtonStyle { Primary, Accent, Ghost, Danger, Card }

        public static Button CreateButton(Transform parent, string name, string caption, ButtonStyle style, UnityAction onClick, float height = SeedAndRockTheme.ButtonHeight, float fontSize = SeedAndRockTheme.BodySize)
        {
            Color fill;
            Color textColor = SeedAndRockTheme.Pale;
            switch (style)
            {
                case ButtonStyle.Primary: fill = SeedAndRockTheme.Teal; textColor = SeedAndRockTheme.Night; break;
                case ButtonStyle.Accent: fill = SeedAndRockTheme.Gold; textColor = SeedAndRockTheme.Night; break;
                case ButtonStyle.Danger: fill = SeedAndRockTheme.Danger; break;
                case ButtonStyle.Card: fill = SeedAndRockTheme.PanelRaised; break;
                default: fill = SeedAndRockTheme.Ghost; break;
            }

            GameObject gameObject = CreateObject(name, parent, typeof(Image), typeof(Button), typeof(ButtonMotion));
            Image image = gameObject.GetComponent<Image>();
            image.sprite = Rounded;
            image.type = Image.Type.Sliced;
            image.color = fill;

            Button button = gameObject.GetComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.ColorTint;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.12f, 1.12f, 1.12f, 1f);
            colors.pressedColor = new Color(0.82f, 0.82f, 0.82f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.6f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            if (onClick != null) button.onClick.AddListener(onClick);

            Size(button, null, height);
            if (!string.IsNullOrEmpty(caption))
            {
                TMP_Text label = CreateText(gameObject.transform, "Label", caption, fontSize, textColor, FontStyles.Bold);
                Stretch(label.rectTransform, 18f, 0f, 18f, 0f);
                label.textWrappingMode = TextWrappingModes.NoWrap;
            }

            return button;
        }

        public static void SetButtonLabel(Button button, string caption)
        {
            TMP_Text label = button.GetComponentInChildren<TMP_Text>();
            if (label != null) label.text = caption;
        }

        // ------------------------------------------------------------------ inputs

        public static TMP_InputField CreateInput(Transform parent, string name, string placeholder, TMP_InputField.ContentType type = TMP_InputField.ContentType.Standard, int characterLimit = 0)
        {
            GameObject gameObject = CreateObject(name, parent, typeof(Image), typeof(TMP_InputField));
            Image image = gameObject.GetComponent<Image>();
            image.sprite = Rounded;
            image.type = Image.Type.Sliced;
            image.color = SeedAndRockTheme.Field;
            Size(image, null, 54f);

            GameObject viewport = CreateObject("TextArea", gameObject.transform, typeof(RectMask2D));
            Stretch(RectOf(viewport), 16f, 6f, 16f, 6f);

            TMP_Text text = CreateText(viewport.transform, "Text", string.Empty, SeedAndRockTheme.BodySize, SeedAndRockTheme.Pale, FontStyles.Normal, TextAlignmentOptions.Left);
            Stretch(text.rectTransform);
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;

            TMP_Text hint = CreateText(viewport.transform, "Placeholder", placeholder, SeedAndRockTheme.BodySize, SeedAndRockTheme.Faint, FontStyles.Italic, TextAlignmentOptions.Left);
            Stretch(hint.rectTransform);
            hint.textWrappingMode = TextWrappingModes.NoWrap;

            TMP_InputField input = gameObject.GetComponent<TMP_InputField>();
            input.targetGraphic = image;
            input.textViewport = RectOf(viewport);
            input.textComponent = text;
            input.placeholder = hint;
            input.contentType = type;
            input.characterLimit = characterLimit;
            input.caretColor = SeedAndRockTheme.Pale;
            input.selectionColor = new Color(SeedAndRockTheme.Teal.r, SeedAndRockTheme.Teal.g, SeedAndRockTheme.Teal.b, 0.35f);
            input.customCaretColor = true;

            ColorBlock colors = input.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
            colors.selectedColor = new Color(1.25f, 1.25f, 1.25f, 1f);
            colors.pressedColor = Color.white;
            input.colors = colors;
            return input;
        }

        // ------------------------------------------------------------------ scroll view

        public static RectTransform CreateScrollList(Transform parent, string name, float spacing = SeedAndRockTheme.Spacing)
        {
            GameObject root = CreateObject(name, parent, typeof(ScrollRect));
            Stretch(RectOf(root));

            GameObject viewport = CreateObject("Viewport", root.transform, typeof(Image), typeof(Mask));
            Stretch(RectOf(viewport));
            viewport.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.02f);
            viewport.GetComponent<Mask>().showMaskGraphic = false;

            GameObject content = CreateObject("Content", viewport.transform, typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            RectTransform contentRect = RectOf(content);
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = new Vector2(0f, 0f);
            VerticalLayoutGroup group = content.GetComponent<VerticalLayoutGroup>();
            group.padding = new RectOffset(6, 14, 6, 6);
            group.spacing = spacing;
            group.childControlWidth = true;
            group.childControlHeight = true;
            group.childForceExpandWidth = true;
            group.childForceExpandHeight = false;
            content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            ScrollRect scroll = root.GetComponent<ScrollRect>();
            scroll.viewport = RectOf(viewport);
            scroll.content = contentRect;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 32f;
            scroll.inertia = true;
            scroll.decelerationRate = 0.12f;
            return contentRect;
        }

        // ------------------------------------------------------------------ slider

        public static Slider CreateSlider(Transform parent, string name, float min, float max, float value, UnityAction<float> onChanged)
        {
            GameObject root = CreateObject(name, parent, typeof(Slider));
            Size(root.GetComponent<Slider>(), null, 28f);

            Image track = CreatePanel(root.transform, "Track", new Color(1f, 1f, 1f, 0.10f), true, false);
            track.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            track.rectTransform.anchorMax = new Vector2(1f, 0.5f);
            track.rectTransform.sizeDelta = new Vector2(0f, 6f);
            track.rectTransform.anchoredPosition = Vector2.zero;

            GameObject fillArea = CreateObject("FillArea", root.transform);
            RectTransform fillAreaRect = RectOf(fillArea);
            fillAreaRect.anchorMin = new Vector2(0f, 0.5f);
            fillAreaRect.anchorMax = new Vector2(1f, 0.5f);
            fillAreaRect.sizeDelta = new Vector2(-12f, 6f);
            fillAreaRect.anchoredPosition = Vector2.zero;
            Image fill = CreatePanel(fillArea.transform, "Fill", SeedAndRockTheme.Teal, true, false);
            Stretch(fill.rectTransform);

            GameObject handleArea = CreateObject("HandleArea", root.transform);
            RectTransform handleAreaRect = RectOf(handleArea);
            Stretch(handleAreaRect, 8f, 0f, 8f, 0f);
            Image handle = CreatePanel(handleArea.transform, "Handle", SeedAndRockTheme.Pale, true, true);
            handle.rectTransform.sizeDelta = new Vector2(20f, 20f);

            Slider slider = root.GetComponent<Slider>();
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = min;
            slider.maxValue = max;
            slider.SetValueWithoutNotify(value);
            ColorBlock colors = slider.colors;
            colors.highlightedColor = new Color(1.1f, 1.1f, 1.1f, 1f);
            colors.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
            slider.colors = colors;
            if (onChanged != null) slider.onValueChanged.AddListener(onChanged);
            return slider;
        }

        // ------------------------------------------------------------------ progress

        public static ProgressBar CreateProgress(Transform parent, string name, float height = 8f)
        {
            Image track = CreatePanel(parent, name, new Color(1f, 1f, 1f, 0.08f));
            Size(track, null, height);
            Image fill = CreatePanel(track.transform, "Fill", SeedAndRockTheme.Teal);
            RectTransform fillRect = fill.rectTransform;
            fillRect.anchorMin = new Vector2(0f, 0f);
            fillRect.anchorMax = new Vector2(0.3f, 1f);
            fillRect.offsetMin = fillRect.offsetMax = Vector2.zero;
            ProgressBar bar = track.gameObject.AddComponent<ProgressBar>();
            bar.Initialize(fillRect);
            return bar;
        }
    }
}
