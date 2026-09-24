using UnityEngine;

namespace Zpd.Defense
{
    [RequireComponent(typeof(Camera))]
    public sealed class DefenseCameraFit : MonoBehaviour
    {
        public float minimumHalfWidth = 12.8f;
        public float minimumHalfHeight = 7.2f;
        public SpriteRenderer meadow;
        private Camera target;
        private void Awake() { target = GetComponent<Camera>(); }
        private void LateUpdate()
        {
            target.orthographicSize = Mathf.Max(minimumHalfHeight, minimumHalfWidth / Mathf.Max(0.1f, target.aspect));
            if (meadow != null)
            {
                Vector2 size = meadow.sprite.bounds.size;
                float cover = Mathf.Max(target.orthographicSize * 2 * target.aspect / size.x, target.orthographicSize * 2 / size.y);
                meadow.transform.localScale = Vector3.one * cover * 1.01f;
            }
        }
    }
}
