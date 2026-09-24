using UnityEngine;

namespace Zpd.Defense
{
    /// <summary>Presentation only. Uses scene-authored sprites; never changes combat positions or clocks.</summary>
    public sealed class DefenseFeedback
    {
        private sealed class Actor
        {
            public Transform root;
            public Vector3 scale;
            public SpriteRenderer[] sprites;
            public Color[] colors;
            public float pulse;
            public Actor(Transform transform)
            {
                root = transform;
                scale = root.localScale;
                sprites = root.GetComponentsInChildren<SpriteRenderer>(true);
                colors = new Color[sprites.Length];
                for (int i = 0; i < sprites.Length; i++) colors[i] = sprites[i].color;
            }
            public void Tick(float dt, Color tint, bool stretch)
            {
                if (root == null) return;
                pulse = Mathf.Max(0, pulse - dt);
                float weight = Mathf.Clamp01(pulse / 0.14f);
                if (stretch) root.localScale = Vector3.Scale(scale, new Vector3(1 + weight * 0.22f, 1 - weight * 0.16f, 1));
                for (int i = 0; i < sprites.Length; i++)
                    if (sprites[i] != null) sprites[i].color = Color.Lerp(colors[i], tint, weight * 0.85f);
            }
        }

        private readonly DefenseGame game;
        private readonly Actor[] enemies;
        private readonly Actor player, beacon;
        private readonly Vector2[] velocities;
        private readonly float[] lives, durations;
        private readonly Color[] colors;
        private readonly Vector3[] scales;
        private readonly Vector3 weaponRest, reticleRest;
        private Vector3 cameraOffset;
        private Vector2 recoilDirection;
        private float recoil, shake, phase, reticlePulse, trailIn;
        private int cursor;
        public Vector3 CameraOffset => cameraOffset;

        public DefenseFeedback(DefenseGame owner)
        {
            game = owner;
            enemies = new Actor[game.enemies.Length];
            for (int i = 0; i < enemies.Length; i++) enemies[i] = new Actor(game.enemies[i].root);
            player = new Actor(game.playerArt);
            beacon = new Actor(game.beacon);
            weaponRest = game.weapon.localPosition;
            reticleRest = game.crosshair.localScale;
            int count = game.feedbackParticles.Length;
            velocities = new Vector2[count]; lives = new float[count]; durations = new float[count];
            colors = new Color[count]; scales = new Vector3[count];
            Reset();
        }

        public void Reset()
        {
            SuspendCamera();
            recoil = shake = phase = reticlePulse = trailIn = 0;
            cursor = 0;
            foreach (var actor in enemies) { actor.pulse = 0; actor.Tick(0, Color.white, true); }
            player.pulse = beacon.pulse = 0;
            player.Tick(0, Color.white, false); beacon.Tick(0, Color.white, true);
            if (game.weapon != null) game.weapon.localPosition = weaponRest;
            if (game.crosshair != null) game.crosshair.localScale = reticleRest;
            for (int i = 0; i < lives.Length; i++)
            {
                lives[i] = 0;
                if (game.feedbackParticles[i] != null) game.feedbackParticles[i].gameObject.SetActive(false);
            }
        }

        public void SuspendCamera()
        {
            if (game.worldCamera != null) game.worldCamera.transform.position -= cameraOffset;
            cameraOffset = Vector3.zero;
        }

        public void Shot(Vector2 direction, bool scatter)
        {
            recoilDirection = direction;
            recoil = scatter ? 0.19f : 0.09f;
            reticlePulse = Mathf.Max(reticlePulse, scatter ? 0.22f : 0.1f);
            shake = Mathf.Max(shake, scatter ? 0.045f : 0.012f);
            Vector2 muzzle = (Vector2)game.player.position + direction * 0.95f;
            Emit(muzzle, direction * 1.5f, new Color(1, 0.85f, 0.32f), scatter ? 0.3f : 0.19f, 0.055f);
            Emit(muzzle, direction * 3, Color.white, 0.1f, 0.04f);
        }

        public void Hit(int index, Vector2 position, Vector2 direction, bool killed)
        {
            enemies[index].pulse = 0.14f;
            reticlePulse = Mathf.Max(reticlePulse, killed ? 0.3f : 0.16f);
            Burst(position, direction, killed ? 9 : 3, killed ? new Color(1, 0.65f, 0.18f) : new Color(1, 0.95f, 0.7f), killed ? 3.8f : 2.1f);
            if (killed) shake = Mathf.Max(shake, 0.065f);
        }

        public void Spawn(int index) { enemies[index].pulse = 0; enemies[index].Tick(0, Color.white, true); }

        public void Hurt(bool isBeacon)
        {
            (isBeacon ? beacon : player).pulse = 0.24f;
            shake = Mathf.Max(shake, isBeacon ? 0.16f : 0.12f);
            Burst(isBeacon ? (Vector2)game.beacon.position : (Vector2)game.player.position,
                Vector2.up, 8, new Color(1, 0.3f, 0.25f), 3);
        }

        public void Dash(Vector2 direction)
        {
            Burst(game.player.position, -direction, 7, new Color(0.45f, 0.95f, 1), 3);
            trailIn = 0;
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
                Emit(position, velocity * speed * (0.6f + (i % 3) * 0.2f), color, 0.09f + (i % 3) * 0.035f, 0.2f + (i % 4) * 0.055f);
            }
        }

        private void Emit(Vector2 position, Vector2 velocity, Color color, float size, float duration)
        {
            if (lives.Length == 0) return;
            int slot = cursor++ % lives.Length;
            var sprite = game.feedbackParticles[slot];
            velocities[slot] = velocity; colors[slot] = color; lives[slot] = durations[slot] = duration;
            scales[slot] = new Vector3(size / sprite.sprite.bounds.size.x, size / sprite.sprite.bounds.size.y, 1);
            sprite.transform.position = position;
            sprite.transform.rotation = Quaternion.Euler(0, 0, cursor * 137.5f);
            sprite.transform.localScale = scales[slot];
            sprite.color = color;
            sprite.gameObject.SetActive(true);
        }

        public void Tick(float dt, bool dashing)
        {
            SuspendCamera();
            dt = Mathf.Clamp(dt, 0, 0.05f);
            phase += dt;
            recoil = Mathf.MoveTowards(recoil, 0, dt * 1.8f);
            game.weapon.localPosition = weaponRest - (Vector3)recoilDirection * recoil;
            reticlePulse = Mathf.MoveTowards(reticlePulse, 0, dt * 2.5f);
            game.crosshair.localScale = reticleRest * (1 + reticlePulse);
            foreach (var actor in enemies) actor.Tick(dt, new Color(1, 0.65f, 0.35f), true);
            player.Tick(dt, new Color(1, 0.25f, 0.25f), false);
            beacon.Tick(dt, new Color(1, 0.3f, 0.2f), true);
            if (dashing && (trailIn -= dt) <= 0)
            {
                trailIn = 0.025f;
                Emit(game.player.position, Vector2.zero, new Color(0.45f, 0.95f, 1, 0.6f), 0.3f, 0.18f);
            }
            for (int i = 0; i < lives.Length; i++)
            {
                if (lives[i] <= 0) continue;
                lives[i] -= dt;
                var sprite = game.feedbackParticles[i];
                if (lives[i] <= 0) { sprite.gameObject.SetActive(false); continue; }
                sprite.transform.position += (Vector3)velocities[i] * dt;
                velocities[i] *= Mathf.Exp(-7 * dt);
                float remaining = lives[i] / durations[i];
                sprite.transform.localScale = scales[i] * (0.35f + remaining * 0.65f);
                var color = colors[i]; color.a *= remaining; sprite.color = color;
            }
            shake = Mathf.MoveTowards(shake, 0, dt * 0.8f);
            cameraOffset = new Vector3(Mathf.Sin(phase * 113), Mathf.Sin(phase * 157), 0) * shake * game.screenShake;
            game.worldCamera.transform.position += cameraOffset;
        }
    }
}
