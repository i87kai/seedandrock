using System;
using System.Collections;
using UnityEngine;

namespace SeedAndRock.UI
{
    /// <summary>
    /// Base class for a full-screen UI state. Screens are plain objects that build their hierarchy once
    /// and animate visibility through a CanvasGroup, keeping the game-flow component free of layout code.
    /// </summary>
    public abstract class UiScreen
    {
        protected readonly SeedAndRockGameFlow Flow;
        protected readonly RectTransform Root;
        protected readonly RectTransform Content;
        private readonly CanvasGroup group;
        private Coroutine transition;

        protected UiScreen(SeedAndRockGameFlow flow, Transform parent, string name, bool dimBackdrop = true)
        {
            Flow = flow;
            GameObject root = UiKit.CreateObject(name, parent, typeof(CanvasGroup));
            Root = UiKit.RectOf(root);
            UiKit.Stretch(Root);
            group = root.GetComponent<CanvasGroup>();
            if (dimBackdrop)
            {
                UnityEngine.UI.Image backdrop = UiKit.CreatePanel(Root, "Backdrop", SeedAndRockTheme.Backdrop, false, true);
                UiKit.Stretch(backdrop.rectTransform);
            }

            Content = UiKit.RectOf(UiKit.CreateObject("Content", Root));
            UiKit.Stretch(Content);
            root.SetActive(false);
        }

        public bool IsVisible => Root.gameObject.activeSelf;

        public void Show(bool instant = false)
        {
            OnBeforeShow();
            Root.gameObject.SetActive(true);
            Root.SetAsLastSibling();
            StartTransition(true, instant);
        }

        public void Hide(bool instant = false)
        {
            if (!Root.gameObject.activeSelf) return;
            StartTransition(false, instant);
        }

        protected virtual void OnBeforeShow() { }

        private void StartTransition(bool visible, bool instant)
        {
            if (transition != null) Flow.StopCoroutine(transition);
            if (instant || !Flow.isActiveAndEnabled)
            {
                group.alpha = visible ? 1f : 0f;
                group.interactable = group.blocksRaycasts = visible;
                Content.anchoredPosition = Vector2.zero;
                if (!visible) Root.gameObject.SetActive(false);
                return;
            }

            transition = Flow.StartCoroutine(Fade(visible));
        }

        private IEnumerator Fade(bool visible)
        {
            float duration = SeedAndRockTheme.FadeDuration;
            float start = group.alpha;
            float end = visible ? 1f : 0f;
            group.interactable = group.blocksRaycasts = visible;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
                group.alpha = Mathf.Lerp(start, end, t);
                float slide = visible ? Mathf.Lerp(-18f, 0f, t) : Mathf.Lerp(0f, 10f, t);
                Content.anchoredPosition = new Vector2(0f, slide);
                yield return null;
            }

            group.alpha = end;
            Content.anchoredPosition = Vector2.zero;
            if (!visible) Root.gameObject.SetActive(false);
            transition = null;
        }
    }

    /// <summary>Full-screen fade used when entering gameplay.</summary>
    public sealed class ScreenFader
    {
        private readonly CanvasGroup group;
        private readonly MonoBehaviour host;
        private Coroutine routine;

        public ScreenFader(MonoBehaviour host, Transform parent)
        {
            this.host = host;
            UnityEngine.UI.Image image = UiKit.CreatePanel(parent, "ScreenFader", Color.black, false, false);
            UiKit.Stretch(image.rectTransform);
            group = image.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;
            image.transform.SetAsLastSibling();
        }

        public void SetOpaque() { Stop(); group.alpha = 1f; group.transform.SetAsLastSibling(); }
        public void SetClear() { Stop(); group.alpha = 0f; }

        public void FadeIn(float duration, Action onComplete = null)
        {
            Stop();
            group.transform.SetAsLastSibling();
            routine = host.StartCoroutine(Animate(0f, duration, onComplete));
        }

        public void FadeOut(float duration, Action onComplete = null)
        {
            Stop();
            group.transform.SetAsLastSibling();
            routine = host.StartCoroutine(Animate(1f, duration, onComplete));
        }

        private void Stop()
        {
            if (routine != null) host.StopCoroutine(routine);
            routine = null;
        }

        private IEnumerator Animate(float target, float duration, Action onComplete)
        {
            float start = group.alpha;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                group.alpha = Mathf.Lerp(start, target, Mathf.SmoothStep(0f, 1f, elapsed / duration));
                yield return null;
            }

            group.alpha = target;
            routine = null;
            onComplete?.Invoke();
        }
    }
}
