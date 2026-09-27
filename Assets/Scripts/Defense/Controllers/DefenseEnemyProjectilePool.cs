using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace Zpd.Defense
{
    [Serializable]
    public sealed class DefenseEnemyProjectile
    {
        [FormerlySerializedAs("root")]
        public Transform transform_root;

        [NonSerialized]
        public Vector2 travel_direction;

        [NonSerialized]
        public float remaining_lifetime_seconds;

        [NonSerialized]
        public int damage;
    }

    /// <summary>Object Pool: authored slots, bounded capacity, no Instantiate/Destroy during combat.</summary>
    public sealed class DefenseEnemyProjectilePool
    {
        public const float Speed = 5.5f;
        private readonly DefenseEnemyProjectile[] projectile_slots;

        public DefenseEnemyProjectilePool(DefenseEnemyProjectile[] slots)
        {
            this.projectile_slots = slots;
            Reset();
        }

        public bool TryFire(Vector2 origin, Vector2 direction, int damage)
        {
            if (direction.sqrMagnitude < 0.0001f)
            {
                return false;
            }

            foreach (var slot in projectile_slots)
            {
                if (slot.transform_root.gameObject.activeSelf)
                {
                    continue;
                }

                slot.travel_direction = direction.normalized;
                slot.transform_root.position = origin;
                slot.transform_root.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
                slot.damage = damage;
                slot.remaining_lifetime_seconds = 4.5f;
                slot.transform_root.gameObject.SetActive(true);
                return true;
            }

            return false;
        }

        public void Reset()
        {
            foreach (var slot in projectile_slots)
            {
                slot.remaining_lifetime_seconds = 0;
                slot.transform_root.gameObject.SetActive(false);
            }
        }

        public void Tick(float dt, Vector2 player, Vector2 beacon, Vector2 bounds, IDefenseEnemyCombat combat)
        {
            foreach (var slot in projectile_slots)
            {
                if (!slot.transform_root.gameObject.activeSelf)
                {
                    continue;
                }

                Vector2 from = slot.transform_root.position;
                Vector2 to = from + slot.travel_direction * Speed * Mathf.Min(dt, Mathf.Max(0, slot.remaining_lifetime_seconds));
                bool hitPlayer = DefenseCollision.SegmentCircle(from, to, player, 0.42f, out float playerTime);
                bool hitBeacon = DefenseCollision.SegmentCircle(from, to, beacon, 0.7f, out float beaconTime);
                slot.remaining_lifetime_seconds -= dt;

                if (hitPlayer || hitBeacon)
                {
                    combat.ApplyEnemyDamage(hitPlayer && (!hitBeacon || playerTime <= beaconTime), slot.damage);
                    slot.transform_root.gameObject.SetActive(false);
                }
                else if (slot.remaining_lifetime_seconds <= 0 || Mathf.Abs(to.x) > bounds.x + 1 || Mathf.Abs(to.y) > bounds.y + 1)
                {
                    slot.transform_root.gameObject.SetActive(false);
                }
                else
                {
                    slot.transform_root.position = to;
                }
            }
        }
    }

    public static class DefenseCollision
    {
        public static bool SegmentCircle(Vector2 from, Vector2 to, Vector2 center, float radius, out float time)
        {
            time = 0;
            Vector2 offset = from - center, step = to - from;
            float c = offset.sqrMagnitude - radius * radius;

            if (c <= 0)
            {
                return true;
            }

            float a = step.sqrMagnitude;

            if (a < 0.0000001f)
            {
                return false;
            }

            float b = Vector2.Dot(offset, step);
            float discriminant = b * b - a * c;

            if (discriminant < 0)
            {
                return false;
            }

            time = (-b - Mathf.Sqrt(discriminant)) / a;
            return time >= 0 && time <= 1;
        }
    }
}
