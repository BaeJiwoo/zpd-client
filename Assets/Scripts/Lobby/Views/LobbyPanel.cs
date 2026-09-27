using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Zpd.Lobby
{
    [RequireComponent(typeof(RectTransform), typeof(CanvasGroup))]
    public sealed class LobbyPanel : MonoBehaviour
    {
        [FormerlySerializedAs("hiddenOffset")]
        public Vector2 hidden_offset;

        [FormerlySerializedAs("duration")]
        [Min(0.01f)]
        public float transition_duration_seconds = 0.25f;
        private Coroutine coroutine_transition;
        private Vector2 visible_anchored_position;
        private bool is_initialized;

        private void Initialize()
        {
            if (is_initialized)
            {
                return;
            }

            visible_anchored_position = ((RectTransform)transform).anchoredPosition;
            is_initialized = true;
        }

        public void Show()
        {
            Initialize();
            bool wasActive = gameObject.activeSelf;
            gameObject.SetActive(true);

            if (coroutine_transition != null)
            {
                StopCoroutine(coroutine_transition);
            }

            if (!wasActive)
            {
                ((RectTransform)transform).anchoredPosition = visible_anchored_position + hidden_offset;
                GetComponent<CanvasGroup>().alpha = 0;
            }

            coroutine_transition = StartCoroutine(Slide(true));
        }

        public void Hide(bool animate = false)
        {
            Initialize();

            if (coroutine_transition != null)
            {
                StopCoroutine(coroutine_transition);
            }

            if (animate && gameObject.activeSelf)
            {
                coroutine_transition = StartCoroutine(Slide(false));
                return;
            }

            coroutine_transition = null;
            ((RectTransform)transform).anchoredPosition = visible_anchored_position;
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
            Vector2 to = opening ? visible_anchored_position : visible_anchored_position + hidden_offset;

            for (float elapsed = 0; elapsed < transition_duration_seconds; elapsed += Time.unscaledDeltaTime)
            {
                float t = Mathf.Clamp01(elapsed / transition_duration_seconds);
                t = 1 - Mathf.Pow(1 - t, 3);
                rect.anchoredPosition = Vector2.LerpUnclamped(from, to, t);
                group.alpha = Mathf.Lerp(alpha, opening ? 1 : 0, t);
                yield return null;
            }

            rect.anchoredPosition = opening ? visible_anchored_position : to;
            group.alpha = opening ? 1 : 0;
            group.interactable = opening;
            coroutine_transition = null;

            if (!opening)
            {
                gameObject.SetActive(false);
            }
            else if (EventSystem.current != null)
            {
                var first = GetComponentInChildren<Selectable>();

                if (first != null)
                {
                    EventSystem.current.SetSelectedGameObject(first.gameObject);
                }
            }
        }
    }
}

