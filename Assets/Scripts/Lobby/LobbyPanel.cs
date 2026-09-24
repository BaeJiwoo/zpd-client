using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Zpd.Lobby
{
    [RequireComponent(typeof(RectTransform), typeof(CanvasGroup))]
    public sealed class LobbyPanel : MonoBehaviour
    {
        public Vector2 hiddenOffset;
        [Min(0.01f)] public float duration = 0.25f;
        private Coroutine transition;
        private Vector2 home;
        private bool initialized;

        private void Initialize()
        {
            if (initialized) return;
            home = ((RectTransform)transform).anchoredPosition;
            initialized = true;
        }

        public void Show()
        {
            Initialize();
            bool wasActive = gameObject.activeSelf;
            gameObject.SetActive(true);
            if (transition != null) StopCoroutine(transition);
            if (!wasActive)
            {
                ((RectTransform)transform).anchoredPosition = home + hiddenOffset;
                GetComponent<CanvasGroup>().alpha = 0;
            }
            transition = StartCoroutine(Slide(true));
        }

        public void Hide(bool animate = false)
        {
            Initialize();
            if (transition != null) StopCoroutine(transition);
            if (animate && gameObject.activeSelf)
            {
                transition = StartCoroutine(Slide(false));
                return;
            }
            transition = null;
            ((RectTransform)transform).anchoredPosition = home;
            gameObject.SetActive(false);
        }

        private IEnumerator Slide(bool opening)
        {
            var rect = (RectTransform)transform;
            var group = GetComponent<CanvasGroup>();
            Vector2 from = rect.anchoredPosition;
            float alpha = group.alpha;
            group.interactable = false;
            group.blocksRaycasts = opening;
            Vector2 to = opening ? home : home + hiddenOffset;
            for (float elapsed = 0; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                float t = Mathf.Clamp01(elapsed / duration);
                t = 1 - Mathf.Pow(1 - t, 3);
                rect.anchoredPosition = Vector2.LerpUnclamped(from, to, t);
                group.alpha = Mathf.Lerp(alpha, opening ? 1 : 0, t);
                yield return null;
            }
            rect.anchoredPosition = opening ? home : to;
            group.alpha = opening ? 1 : 0;
            group.interactable = opening;
            transition = null;
            if (!opening) gameObject.SetActive(false);
            else if (EventSystem.current != null)
            {
                var first = GetComponentInChildren<Selectable>();
                if (first != null) EventSystem.current.SetSelectedGameObject(first.gameObject);
            }
        }
    }
}

