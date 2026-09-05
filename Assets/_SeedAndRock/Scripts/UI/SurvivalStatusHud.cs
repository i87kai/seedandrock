using SeedAndRock.Survival;
using SeedAndRock.World;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SeedAndRock.UI
{
    /// <summary>Bottom-right survival readout built at runtime on the game-flow canvas.</summary>
    public sealed class SurvivalStatusHud : MonoBehaviour
    {
        private readonly Color panelColor = new Color(0.02f, 0.07f, 0.08f, 0.86f);
        private readonly Color trackColor = new Color(0.035f, 0.07f, 0.08f, 0.95f);
        private readonly Color pale = new Color(0.88f, 0.96f, 0.92f, 1f);
        private readonly Color muted = new Color(0.62f, 0.76f, 0.73f, 1f);
        private readonly Color healthColor = new Color(0.78f, 0.18f, 0.20f, 1f);
        private readonly Color hungerColor = new Color(0.55f, 0.32f, 0.16f, 1f);
        private readonly Color thirstColor = new Color(0.18f, 0.48f, 0.82f, 1f);
        private readonly Color coldColor = new Color(0.55f, 0.82f, 0.95f, 1f);
        private readonly Color hotColor = new Color(0.95f, 0.42f, 0.16f, 1f);

        private RectTransform root;
        private GameObject warningRow;
        private Image warningIcon;
        private TMP_Text warningLabel;
        private TMP_Text bodyTemperatureLabel;
        private TMP_Text airTemperatureLabel;
        private Image healthFill;
        private Image hungerFill;
        private Image thirstFill;
        private TMP_Text healthValue;
        private TMP_Text hungerValue;
        private TMP_Text thirstValue;
        private Sprite snowSprite;
        private Sprite flameSprite;

        public static SurvivalStatusHud Create(Transform canvasParent)
        {
            GameObject root = new GameObject("SurvivalStatusHud", typeof(RectTransform), typeof(SurvivalStatusHud));
            root.transform.SetParent(canvasParent, false);
            SurvivalStatusHud hud = root.GetComponent<SurvivalStatusHud>();
            hud.Build();
            hud.SetVisible(false);
            return hud;
        }

        public void SetVisible(bool visible)
        {
            if (root != null)
                root.gameObject.SetActive(visible);
            enabled = visible;
        }

        private void Update()
        {
            Refresh(PlayerSurvival.Active);
        }

        private void OnDestroy()
        {
            if (snowSprite != null)
            {
                Destroy(snowSprite.texture);
                Destroy(snowSprite);
            }

            if (flameSprite != null)
            {
                Destroy(flameSprite.texture);
                Destroy(flameSprite);
            }
        }

        private void Build()
        {
            snowSprite = Sprite.Create(CreateSnowflakeTexture(), new Rect(0f, 0f, 24f, 24f), new Vector2(0.5f, 0.5f), 24f);
            flameSprite = Sprite.Create(CreateFlameTexture(), new Rect(0f, 0f, 24f, 24f), new Vector2(0.5f, 0.5f), 24f);

            root = GetComponent<RectTransform>();
            root.anchorMin = new Vector2(1f, 0f);
            root.anchorMax = new Vector2(1f, 0f);
            root.pivot = new Vector2(1f, 0f);
            root.anchoredPosition = new Vector2(-24f, 24f);
            root.sizeDelta = new Vector2(320f, 0f);

            Image background = root.gameObject.AddComponent<Image>();
            background.color = panelColor;
            background.raycastTarget = false;

            VerticalLayoutGroup layout = root.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(16, 16, 14, 14);
            layout.spacing = 6f;
            layout.childAlignment = TextAnchor.UpperRight;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            ContentSizeFitter fitter = root.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            warningRow = CreateRow(root, "WarningRow", 28f);
            HorizontalLayoutGroup warningLayout = warningRow.AddComponent<HorizontalLayoutGroup>();
            warningLayout.spacing = 8f;
            warningLayout.childAlignment = TextAnchor.MiddleRight;
            warningLayout.childControlHeight = true;
            warningLayout.childForceExpandWidth = false;
            warningLayout.childForceExpandHeight = true;
            warningIcon = CreateImage(warningRow.transform, "WarningIcon", Color.white, new Vector2(22f, 22f));
            warningIcon.preserveAspect = true;
            warningLabel = CreateLabel(warningRow.transform, "WarningLabel", 18f, pale, FontStyles.Bold, TextAlignmentOptions.MidlineRight, new Vector2(0f, 28f));
            warningLabel.text = "COLD";
            warningRow.SetActive(false);

            bodyTemperatureLabel = CreateLabel(root, "BodyTemperature", 27f, pale, FontStyles.Bold, TextAlignmentOptions.MidlineRight, new Vector2(0f, 34f));
            airTemperatureLabel = CreateLabel(root, "AirTemperature", 14f, muted, FontStyles.Normal, TextAlignmentOptions.MidlineRight, new Vector2(0f, 20f));

            CreateBar(root, "Health", "HEALTH", healthColor, out healthFill, out healthValue);
            CreateBar(root, "Hunger", "HUNGER", hungerColor, out hungerFill, out hungerValue);
            CreateBar(root, "Thirst", "THIRST", thirstColor, out thirstFill, out thirstValue);
        }

        private void Refresh(PlayerSurvival survival)
        {
            if (survival == null)
            {
                bodyTemperatureLabel.text = "-- °C";
                airTemperatureLabel.text = "Air -- °C";
                SetFill(healthFill, healthValue, 0f, 100f);
                SetFill(hungerFill, hungerValue, 0f, 100f);
                SetFill(thirstFill, thirstValue, 0f, 100f);
                warningRow.SetActive(false);
                return;
            }

            float body = survival.BodyTemperatureCelsius;
            bodyTemperatureLabel.text = body.ToString("0.0") + " °C";
            bodyTemperatureLabel.color = TemperatureColor(body, survival);
            airTemperatureLabel.text = "Air " + survival.AmbientCelsius.ToString("0") + " °C  •  " + FormatBiome(survival.CurrentClimate.Biome);

            SetFill(healthFill, healthValue, survival.Health, survival.MaxHealth);
            SetFill(hungerFill, hungerValue, survival.Hunger, survival.MaxHunger);
            SetFill(thirstFill, thirstValue, survival.Thirst, survival.MaxThirst);

            switch (survival.Warning)
            {
                case SurvivalWarning.Cold:
                    warningRow.SetActive(true);
                    warningIcon.sprite = snowSprite;
                    warningIcon.color = coldColor;
                    warningLabel.text = "COLD";
                    warningLabel.color = coldColor;
                    break;
                case SurvivalWarning.Hot:
                    warningRow.SetActive(true);
                    warningIcon.sprite = flameSprite;
                    warningIcon.color = hotColor;
                    warningLabel.text = "HOT";
                    warningLabel.color = hotColor;
                    break;
                default:
                    warningRow.SetActive(false);
                    break;
            }
        }

        private Color TemperatureColor(float body, PlayerSurvival survival)
        {
            if (survival.Warning == SurvivalWarning.Cold)
                return coldColor;
            if (survival.Warning == SurvivalWarning.Hot)
                return hotColor;
            return pale;
        }

        private static string FormatBiome(SeedAndRockBiome biome)
        {
            switch (biome)
            {
                case SeedAndRock.World.SeedAndRockBiome.Plains: return "Plains";
                case SeedAndRock.World.SeedAndRockBiome.Grassland: return "Grassland";
                case SeedAndRock.World.SeedAndRockBiome.Forest: return "Forest";
                case SeedAndRock.World.SeedAndRockBiome.Desert: return "Desert";
                case SeedAndRock.World.SeedAndRockBiome.Snow: return "Snow";
                case SeedAndRock.World.SeedAndRockBiome.Mountains: return "Mountains";
                default: return "Wilds";
            }
        }

        private void CreateBar(Transform parent, string name, string caption, Color fillColor, out Image fill, out TMP_Text value)
        {
            GameObject block = CreateRow(parent, name + "Block", 43f);
            VerticalLayoutGroup blockLayout = block.AddComponent<VerticalLayoutGroup>();
            blockLayout.spacing = 3f;
            blockLayout.childAlignment = TextAnchor.UpperLeft;
            blockLayout.childControlWidth = true;
            blockLayout.childControlHeight = true;
            blockLayout.childForceExpandWidth = true;
            blockLayout.childForceExpandHeight = false;

            GameObject header = CreateRow(block.transform, "Header", 18f);
            HorizontalLayoutGroup headerLayout = header.AddComponent<HorizontalLayoutGroup>();
            headerLayout.spacing = 8f;
            headerLayout.childAlignment = TextAnchor.MiddleLeft;
            headerLayout.childControlWidth = true;
            headerLayout.childControlHeight = true;
            headerLayout.childForceExpandWidth = false;
            headerLayout.childForceExpandHeight = true;

            TMP_Text label = CreateLabel(header.transform, "Label", 13f, muted, FontStyles.Bold, TextAlignmentOptions.MidlineLeft, new Vector2(0f, 18f));
            label.text = caption;
            LayoutElement labelLayout = label.GetComponent<LayoutElement>();
            labelLayout.flexibleWidth = 1f;
            label.textWrappingMode = TextWrappingModes.NoWrap;

            value = CreateLabel(header.transform, "Value", 13f, pale, FontStyles.Bold, TextAlignmentOptions.MidlineRight, new Vector2(48f, 18f));
            LayoutElement valueLayout = value.GetComponent<LayoutElement>();
            valueLayout.preferredWidth = 48f;
            valueLayout.minWidth = 48f;
            value.textWrappingMode = TextWrappingModes.NoWrap;

            GameObject track = new GameObject("Track", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            track.transform.SetParent(block.transform, false);
            Image trackImage = track.GetComponent<Image>();
            trackImage.color = trackColor;
            trackImage.raycastTarget = false;
            LayoutElement trackLayout = track.GetComponent<LayoutElement>();
            trackLayout.flexibleWidth = 1f;
            trackLayout.minWidth = 120f;
            trackLayout.minHeight = 16f;
            trackLayout.preferredHeight = 16f;

            fill = CreateImage(track.transform, "Fill", fillColor, Vector2.zero);
            RectTransform fillRect = fill.rectTransform;
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = new Vector2(2f, 2f);
            fillRect.offsetMax = new Vector2(-2f, -2f);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 1f;
        }

        private static void SetFill(Image fill, TMP_Text value, float current, float max)
        {
            float safeMax = max <= 0f ? 1f : max;
            fill.fillAmount = Mathf.Clamp01(current / safeMax);
            value.text = Mathf.RoundToInt(current).ToString();
        }

        private GameObject CreateRow(Transform parent, string name, float height)
        {
            GameObject row = new GameObject(name, typeof(RectTransform), typeof(LayoutElement));
            row.transform.SetParent(parent, false);
            LayoutElement layout = row.GetComponent<LayoutElement>();
            layout.preferredHeight = height;
            layout.minHeight = height;
            return row;
        }

        private Image CreateImage(Transform parent, string name, Color color, Vector2 size)
        {
            GameObject rootObject = new GameObject(name, typeof(RectTransform), typeof(Image));
            rootObject.transform.SetParent(parent, false);
            Image image = rootObject.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            if (size.sqrMagnitude > 0f)
            {
                LayoutElement layout = rootObject.AddComponent<LayoutElement>();
                layout.preferredWidth = size.x;
                layout.preferredHeight = size.y;
                layout.minWidth = size.x;
                layout.minHeight = size.y;
            }

            return image;
        }

        private TMP_Text CreateLabel(Transform parent, string name, float size, Color color, FontStyles style, TextAlignmentOptions alignment, Vector2 dimensions)
        {
            GameObject rootObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
            rootObject.transform.SetParent(parent, false);
            TextMeshProUGUI text = rootObject.GetComponent<TextMeshProUGUI>();
            text.font = TMP_Settings.defaultFontAsset;
            text.fontSize = size;
            text.color = color;
            text.fontStyle = style;
            text.alignment = alignment;
            text.raycastTarget = false;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            LayoutElement layout = rootObject.GetComponent<LayoutElement>();
            layout.preferredHeight = dimensions.y;
            if (dimensions.x > 0f)
            {
                layout.preferredWidth = dimensions.x;
                layout.minWidth = dimensions.x;
            }
            else
                layout.flexibleWidth = 1f;
            return text;
        }

        private static Texture2D CreateSnowflakeTexture()
        {
            const int size = 24;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = "SR_SnowflakeIcon"
            };

            Color clear = new Color(0f, 0f, 0f, 0f);
            Color ink = Color.white;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                    texture.SetPixel(x, y, clear);
            }

            int mid = size / 2;
            for (int i = 3; i < size - 3; i++)
            {
                Plot(texture, i, mid, ink);
                Plot(texture, mid, i, ink);
                Plot(texture, i, i, ink);
                Plot(texture, i, size - 1 - i, ink);
            }

            texture.Apply(false, false);
            return texture;
        }

        private static Texture2D CreateFlameTexture()
        {
            const int size = 24;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = "SR_FlameIcon"
            };

            Color clear = new Color(0f, 0f, 0f, 0f);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float nx = (x - 11.5f) / 8.5f;
                    float ny = (y - 4f) / 17f;
                    float width = Mathf.Lerp(0.95f, 0.18f, Mathf.Clamp01(ny));
                    float wobble = Mathf.Sin(ny * 6.2f) * 0.12f;
                    bool inside = ny >= 0f && ny <= 1f && Mathf.Abs(nx - wobble) < width * (1f - ny * 0.15f);
                    texture.SetPixel(x, y, inside ? Color.white : clear);
                }
            }

            texture.Apply(false, false);
            return texture;
        }

        private static void Plot(Texture2D texture, int x, int y, Color color)
        {
            if (x < 0 || y < 0 || x >= texture.width || y >= texture.height)
                return;
            texture.SetPixel(x, y, color);
            if (x + 1 < texture.width)
                texture.SetPixel(x + 1, y, color);
            if (y + 1 < texture.height)
                texture.SetPixel(x, y + 1, color);
        }
    }
}
