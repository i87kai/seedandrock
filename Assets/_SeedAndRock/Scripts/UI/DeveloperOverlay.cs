using System.Text;
using SeedAndRock.Player;
using SeedAndRock.World;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SeedAndRock.UI
{
    /// <summary>F3 developer overlay: frame time, position, surface sample and generation statistics. Hidden by default.</summary>
    public sealed class DeveloperOverlay : MonoBehaviour
    {
        private TMP_Text text;
        private readonly StringBuilder builder = new StringBuilder(512);
        private float smoothedDelta = 1f / 60f;
        private float nextRefresh;

        public WorldGenerator Generator { get; set; }
        public string WorldName { get; set; }

        public static DeveloperOverlay Create(Transform parent)
        {
            Image panel = UiKit.CreatePanel(parent, "DeveloperOverlay", new Color(0f, 0f, 0f, 0.62f), true, false);
            UiKit.Anchor(panel.rectTransform, new Vector2(0f, 1f), new Vector2(430f, 250f), new Vector2(16f, -16f));
            DeveloperOverlay overlay = panel.gameObject.AddComponent<DeveloperOverlay>();
            overlay.text = UiKit.CreateText(panel.transform, "Text", "", 13f, new Color(0.85f, 0.95f, 0.9f), FontStyles.Normal, TextAlignmentOptions.TopLeft);
            UiKit.Stretch(overlay.text.rectTransform, 12f, 10f, 12f, 10f);
            overlay.text.overflowMode = TextOverflowModes.Overflow;
            panel.gameObject.SetActive(false);
            return overlay;
        }

        public bool IsVisible => gameObject.activeSelf;

        public void Toggle() => gameObject.SetActive(!gameObject.activeSelf);

        private void Update()
        {
            smoothedDelta = Mathf.Lerp(smoothedDelta, Time.unscaledDeltaTime, 0.08f);
            if (Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + 0.15f;

            builder.Clear();
            builder.Append("<b>SeedAndRock developer overlay</b>  (F3)\n");
            builder.Append((1f / Mathf.Max(smoothedDelta, 1e-5f)).ToString("0")).Append(" fps   ").Append((smoothedDelta * 1000f).ToString("0.0")).Append(" ms\n");

            FirstPersonExplorerController player = PlayerSpawner.Find();
            WorldSampler sampler = Generator != null ? Generator.Sampler : null;
            if (Generator != null)
                builder.Append("World: ").Append(WorldName).Append("   seed ").Append(Generator.CurrentSeed).Append('\n');

            if (player != null)
            {
                Vector3 p = player.transform.position;
                builder.Append("Position ").Append(p.x.ToString("0.0")).Append(", ").Append(p.y.ToString("0.0")).Append(", ").Append(p.z.ToString("0.0"));
                builder.Append("   yaw ").Append(player.Yaw.ToString("0")).Append("   grounded ").Append(player.IsGrounded ? "yes" : "no").Append('\n');
                if (sampler != null)
                {
                    SurfaceSample s = sampler.SampleSurface(p.x, p.z);
                    builder.Append("Biome ").Append(s.biome).Append("   height ").Append(s.height.ToString("0.0")).Append("   slope ").Append(s.slope.ToString("0.00")).Append('\n');
                    builder.Append("Moisture ").Append(s.moisture.ToString("0.00")).Append("   temp ").Append(s.temperature.ToString("0.00")).Append("   water dist ").Append(s.waterDistance.ToString("0.0")).Append("m\n");
                    builder.Append("Wet ").Append(s.wetness.ToString("0.00")).Append("   snow ").Append(s.snow.ToString("0.00")).Append("   sand ").Append(s.sand.ToString("0.00")).Append("   river ").Append(s.riverStrength.ToString("0.00")).Append('\n');
                }
            }

            WorldBuildResult result = Generator != null ? Generator.LastResult : null;
            if (result != null)
            {
                builder.Append("Triangles ").Append(result.TotalTriangles.ToString("N0")).Append("  (terrain ").Append(result.TerrainTriangles.ToString("N0")).Append(", water ").Append(result.WaterTriangles.ToString("N0")).Append(", props ").Append(result.PropTriangles.ToString("N0")).Append(")\n");
                builder.Append("Trees ").Append(result.TreeCount).Append("   rocks ").Append(result.RockCount).Append("   grass ").Append(result.GrassCount).Append("   renderers ").Append(result.RendererCount).Append('\n');
                builder.Append("Generated in ").Append(result.Seconds.ToString("0.00")).Append(" s");
                if (sampler != null)
                    builder.Append("   lakes ").Append(sampler.Hydrology.Lakes.Count).Append("   river cells ").Append(sampler.Hydrology.RiverCellCount);
            }

            text.text = builder.ToString();
        }
    }
}
