using UnityEngine;

namespace Zpd.Defense
{
    /// <summary>Presentation only. Uses scene-authored sprites; never changes combat positions or clocks.</summary>
    public sealed class DefenseFeedback
    {
        private sealed class Actor
        {
            public Transform transform_root;
            public Vector3 original_scale;
            public SpriteRenderer[] sprite_renderer_parts;
            public Color[] original_colors;
            public float pulse_remaining_seconds;

            public Actor(Transform transform)
            {
                transform_root = transform;
                original_scale = transform_root.localScale;
                sprite_renderer_parts = transform_root.GetComponentsInChildren<SpriteRenderer>(true);
                original_colors = new Color[sprite_renderer_parts.Length];

                for (int i = 0; i < sprite_renderer_parts.Length; i++)
                {
                    original_colors[i] = sprite_renderer_parts[i].color;
                }
            }

            public void Tick(float dt, Color tint, bool stretch)
            {
                if (transform_root == null)
                {
                    return;
                }

                pulse_remaining_seconds = Mathf.Max(0, pulse_remaining_seconds - dt);
                float weight = Mathf.Clamp01(pulse_remaining_seconds / 0.14f);

                if (stretch)
                {
                    transform_root.localScale = Vector3.Scale(original_scale, new Vector3(1 + weight * 0.22f, 1 - weight * 0.16f, 1));
                }

                for (int i = 0; i < sprite_renderer_parts.Length; i++)
                {
                    if (sprite_renderer_parts[i] != null)
                    {
                        sprite_renderer_parts[i].color = Color.Lerp(original_colors[i], tint, weight * 0.85f);
                    }
                }
            }
        }

        private readonly DefenseGame defense_game;
        private readonly Actor[] enemy_actors;
        private readonly Actor player_actor;

        private readonly Actor beacon_actor;
        private readonly Vector2[] particle_velocities;
        private readonly float[] particle_remaining_seconds;

        private readonly float[] particle_duration_seconds;
        private readonly Color[] particle_colors;
        private readonly Vector3[] particle_scales;
        private readonly Vector3 weapon_rest_position;

        private readonly Vector3 crosshair_rest_scale;
        private Vector3 camera_offset;
        private Vector2 recoil_direction;
        private float recoil_distance;

        private float shake_amplitude;

        private float effect_elapsed_seconds;

        private float crosshair_pulse_scale;

        private float trail_delay_seconds;
        private int next_particle_index;

        public Vector3 CameraOffset => camera_offset;

        public DefenseFeedback(DefenseGame owner)
        {
            defense_game = owner;
            enemy_actors = new Actor[defense_game.enemy_slots.Length];

            for (int i = 0; i < enemy_actors.Length; i++)
            {
                enemy_actors[i] = new Actor(defense_game.enemy_slots[i].transform_root);
            }

            player_actor = new Actor(defense_game.transform_player_art);
            beacon_actor = new Actor(defense_game.transform_beacon);
            weapon_rest_position = defense_game.transform_weapon.localPosition;
            crosshair_rest_scale = defense_game.transform_crosshair.localScale;
            int count = defense_game.sprite_renderer_feedback_particles.Length;
            particle_velocities = new Vector2[count];
            particle_remaining_seconds = new float[count];
            particle_duration_seconds = new float[count];
            particle_colors = new Color[count];
            particle_scales = new Vector3[count];
            Reset();
        }

        public void Reset()
        {
            SuspendCamera();
            recoil_distance = shake_amplitude = effect_elapsed_seconds = crosshair_pulse_scale = trail_delay_seconds = 0;
            next_particle_index = 0;

            foreach (var actor in enemy_actors)
            {
                actor.pulse_remaining_seconds = 0;
                actor.Tick(0, Color.white, true);
            }

            player_actor.pulse_remaining_seconds = beacon_actor.pulse_remaining_seconds = 0;
            player_actor.Tick(0, Color.white, false);
            beacon_actor.Tick(0, Color.white, true);

            if (defense_game.transform_weapon != null)
            {
                defense_game.transform_weapon.localPosition = weapon_rest_position;
            }

            if (defense_game.transform_crosshair != null)
            {
                defense_game.transform_crosshair.localScale = crosshair_rest_scale;
            }

            for (int i = 0; i < particle_remaining_seconds.Length; i++)
            {
                particle_remaining_seconds[i] = 0;

                if (defense_game.sprite_renderer_feedback_particles[i] != null)
                {
                    defense_game.sprite_renderer_feedback_particles[i].gameObject.SetActive(false);
                }
            }
        }

        public void SuspendCamera()
        {
            if (defense_game.camera_world != null)
            {
                defense_game.camera_world.transform.position -= camera_offset;
            }

            camera_offset = Vector3.zero;
        }

        public void Shot(Vector2 direction, bool scatter)
        {
            recoil_direction = direction;
            recoil_distance = scatter ? 0.19f : 0.09f;
            crosshair_pulse_scale = Mathf.Max(crosshair_pulse_scale, scatter ? 0.22f : 0.1f);
            shake_amplitude = Mathf.Max(shake_amplitude, scatter ? 0.045f : 0.012f);
            Vector2 muzzle = (Vector2)defense_game.transform_player.position + direction * 0.95f;
            Emit(muzzle, direction * 1.5f, new Color(1, 0.85f, 0.32f), scatter ? 0.3f : 0.19f, 0.055f);
            Emit(muzzle, direction * 3, Color.white, 0.1f, 0.04f);
        }

        public void Hit(int index, Vector2 position, Vector2 direction, bool killed)
        {
            enemy_actors[index].pulse_remaining_seconds = 0.14f;
            crosshair_pulse_scale = Mathf.Max(crosshair_pulse_scale, killed ? 0.3f : 0.16f);
            Burst(
                position,
                direction,
                killed ? 9 : 3,
                killed ? new Color(1, 0.65f, 0.18f) : new Color(1, 0.95f, 0.7f),
                killed ? 3.8f : 2.1f);

            if (killed)
            {
                shake_amplitude = Mathf.Max(shake_amplitude, 0.065f);
            }
        }

        public void Spawn(int index)
        {
            enemy_actors[index].pulse_remaining_seconds = 0;
            enemy_actors[index].Tick(0, Color.white, true);
        }

        public void Hurt(bool isBeacon)
        {
            (isBeacon ? beacon_actor : player_actor).pulse_remaining_seconds = 0.24f;
            shake_amplitude = Mathf.Max(shake_amplitude, isBeacon ? 0.16f : 0.12f);
            Burst(
                isBeacon ? (Vector2)defense_game.transform_beacon.position : (Vector2)defense_game.transform_player.position,
                Vector2.up,
                8,
                new Color(1, 0.3f, 0.25f),
                3);
        }

        public void Dash(Vector2 direction)
        {
            Burst(defense_game.transform_player.position, -direction, 7, new Color(0.45f, 0.95f, 1), 3);
            trail_delay_seconds = 0;
        }

        public void Pickup(Vector2 position)
        {
            Burst(position, Vector2.up, 3, new Color(1, 0.83f, 0.2f), 1.6f);
        }

        public void EnemyShot(Vector2 position, Vector2 direction)
        {
            Burst(position, direction, 4, new Color(1, 0.2f, 0.65f), 2);
        }

        private void Burst(Vector2 position, Vector2 direction, int count, Color color, float speed)
        {
            // Deterministic visual variation does not consume the enemy spawn RNG.

            float baseAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

            for (int i = 0; i < count; i++)
            {
                Vector2 velocity = Quaternion.Euler(0, 0, baseAngle + i * 137.5f) * Vector2.right;
                Emit(
                    position,
                    velocity * speed * (0.6f + (i % 3) * 0.2f),
                    color,
                    0.09f + (i % 3) * 0.035f,
                    0.2f + (i % 4) * 0.055f);
            }
        }

        private void Emit(Vector2 position, Vector2 velocity, Color color, float size, float duration)
        {
            if (particle_remaining_seconds.Length == 0)
            {
                return;
            }

            int slot = next_particle_index++ % particle_remaining_seconds.Length;
            var sprite = defense_game.sprite_renderer_feedback_particles[slot];
            particle_velocities[slot] = velocity;
            particle_colors[slot] = color;
            particle_remaining_seconds[slot] = particle_duration_seconds[slot] = duration;
            particle_scales[slot] = new Vector3(size / sprite.sprite.bounds.size.x, size / sprite.sprite.bounds.size.y, 1);
            sprite.transform.position = position;
            sprite.transform.rotation = Quaternion.Euler(0, 0, next_particle_index * 137.5f);
            sprite.transform.localScale = particle_scales[slot];
            sprite.color = color;
            sprite.gameObject.SetActive(true);
        }

        public void Tick(float dt, bool dashing)
        {
            SuspendCamera();
            dt = Mathf.Clamp(dt, 0, 0.05f);
            effect_elapsed_seconds += dt;
            recoil_distance = Mathf.MoveTowards(recoil_distance, 0, dt * 1.8f);
            defense_game.transform_weapon.localPosition = weapon_rest_position - (Vector3)recoil_direction * recoil_distance;
            crosshair_pulse_scale = Mathf.MoveTowards(crosshair_pulse_scale, 0, dt * 2.5f);
            defense_game.transform_crosshair.localScale = crosshair_rest_scale * (1 + crosshair_pulse_scale);

            foreach (var actor in enemy_actors)
            {
                actor.Tick(dt, new Color(1, 0.65f, 0.35f), true);
            }

            player_actor.Tick(dt, new Color(1, 0.25f, 0.25f), false);
            beacon_actor.Tick(dt, new Color(1, 0.3f, 0.2f), true);

            if (dashing && (trail_delay_seconds -= dt) <= 0)
            {
                trail_delay_seconds = 0.025f;
                Emit(defense_game.transform_player.position, Vector2.zero, new Color(0.45f, 0.95f, 1, 0.6f), 0.3f, 0.18f);
            }

            for (int i = 0; i < particle_remaining_seconds.Length; i++)
            {
                if (particle_remaining_seconds[i] <= 0)
                {
                    continue;
                }

                particle_remaining_seconds[i] -= dt;
                var sprite = defense_game.sprite_renderer_feedback_particles[i];

                if (particle_remaining_seconds[i] <= 0)
                {
                    sprite.gameObject.SetActive(false);
                    continue;
                }

                sprite.transform.position += (Vector3)particle_velocities[i] * dt;
                particle_velocities[i] *= Mathf.Exp(-7 * dt);
                float remaining = particle_remaining_seconds[i] / particle_duration_seconds[i];
                sprite.transform.localScale = particle_scales[i] * (0.35f + remaining * 0.65f);
                var color = particle_colors[i];
                color.a *= remaining;
                sprite.color = color;
            }

            shake_amplitude = Mathf.MoveTowards(shake_amplitude, 0, dt * 0.8f);
            camera_offset = new Vector3(Mathf.Sin(effect_elapsed_seconds * 113), Mathf.Sin(effect_elapsed_seconds * 157), 0) * shake_amplitude * defense_game.screen_shake_strength;
            defense_game.camera_world.transform.position += camera_offset;
        }
    }
}
