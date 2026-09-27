using UnityEngine;
using UnityEngine.Serialization;

namespace Zpd.Defense
{
    [RequireComponent(typeof(Camera))]
    public sealed class DefenseCameraFit : MonoBehaviour
    {
        [FormerlySerializedAs("minimumHalfWidth")]
        public float minimum_half_width = 12.8f;

        [FormerlySerializedAs("minimumHalfHeight")]
        public float minimum_half_height = 7.2f;

        [FormerlySerializedAs("meadow")]
        public SpriteRenderer sprite_renderer_meadow;
        private Camera camera_target;

        private void Awake()
        {
            camera_target = GetComponent<Camera>();
        }

        private void LateUpdate()
        {
            camera_target.orthographicSize = Mathf.Max(minimum_half_height, minimum_half_width / Mathf.Max(0.1f, camera_target.aspect));

            if (sprite_renderer_meadow != null)
            {
                Vector2 size = sprite_renderer_meadow.sprite.bounds.size;
                float cover = Mathf.Max(
                    camera_target.orthographicSize * 2 * camera_target.aspect / size.x,
                    camera_target.orthographicSize * 2 / size.y);
                sprite_renderer_meadow.transform.localScale = Vector3.one * cover * 1.01f;
            }
        }
    }
}
