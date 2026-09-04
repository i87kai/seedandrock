using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SeedAndRock.UI
{
    /// <summary>Minimal gameplay HUD: a soft crosshair dot and a transient toast (e.g. "World saved").</summary>
    public sealed class HudOverlay
    {
        private readonly RectTransform root;
        private readonly CanvasGroup toastGroup;
        private readonly TMP_Text toastText;
        private float toastUntil;

        public HudOverlay(Transform parent)
        {
            GameObject rootObject = UiKit.CreateObject("Hud", parent);
            root = UiKit.RectOf(rootObject);
            UiKit.Stretch(root);

            Image dot = UiKit.CreatePanel(root, "Crosshair", new Color(1f, 1f, 1f, 0.55f), true, false);
            dot.sprite = UiKit.Soft;
            UiKit.Anchor(dot.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(10f, 10f));

            Image toast = UiKit.CreatePanel(root, "Toast", new Color(SeedAndRockTheme.Panel.r, SeedAndRockTheme.Panel.g, SeedAndRockTheme.Panel.b, 0.85f), true, false);
            UiKit.Anchor(toast.rectTransform, new Vector2(0.5f, 1f), new Vector2(420f, 42f), new Vector2(0f, -28f));
            toastGroup = toast.gameObject.AddComponent<CanvasGroup>();
            toastGroup.alpha = 0f;
            toastGroup.blocksRaycasts = false;
            toastText = UiKit.CreateText(toast.transform, "Text", "", SeedAndRockTheme.SmallSize + 1f, SeedAndRockTheme.Pale, FontStyles.Bold);
            UiKit.Stretch(toastText.rectTransform, 14f, 0f, 14f, 0f);
            toastText.textWrappingMode = TextWrappingModes.NoWrap;
            rootObject.SetActive(false);
        }

        public void SetVisible(bool visible) => root.gameObject.SetActive(visible);

        public void Toast(string message, float seconds = 2.4f)
        {
            toastText.text = message;
            toastUntil = Time.unscaledTime + seconds;
            toastGroup.alpha = 1f;
        }

        public void Tick()
        {
            if (!root.gameObject.activeSelf || toastGroup.alpha <= 0f) return;
            float remaining = toastUntil - Time.unscaledTime;
            toastGroup.alpha = Mathf.Clamp01(remaining / 0.4f);
        }
    }
}
