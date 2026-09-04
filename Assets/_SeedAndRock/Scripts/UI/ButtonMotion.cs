using UnityEngine;
using UnityEngine.EventSystems;

namespace SeedAndRock.UI
{
    /// <summary>Subtle scale response for pointer hover and press, driven by unscaled time so it works while paused.</summary>
    public sealed class ButtonMotion : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        private float target = 1f;
        private bool hovered;
        private bool pressed;
        private RectTransform rect;

        private void Awake()
        {
            rect = GetComponent<RectTransform>();
        }

        private void OnEnable()
        {
            hovered = pressed = false;
            target = 1f;
            if (rect != null) rect.localScale = Vector3.one;
        }

        private void Update()
        {
            float current = rect.localScale.x;
            float next = Mathf.Lerp(current, target, 1f - Mathf.Exp(-Time.unscaledDeltaTime * 18f));
            rect.localScale = new Vector3(next, next, 1f);
        }

        private void Refresh() => target = pressed ? 0.975f : hovered ? 1.025f : 1f;

        public void OnPointerEnter(PointerEventData eventData) { hovered = true; Refresh(); }
        public void OnPointerExit(PointerEventData eventData) { hovered = false; pressed = false; Refresh(); }
        public void OnPointerDown(PointerEventData eventData) { pressed = true; Refresh(); }
        public void OnPointerUp(PointerEventData eventData) { pressed = false; Refresh(); }
    }
}
