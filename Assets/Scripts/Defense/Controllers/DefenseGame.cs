using System;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Zpd.Gameplay;

namespace Zpd.Defense
{
    /// <summary>Simulates only pre-authored actors/projectiles. No Instantiate or runtime scene construction.</summary>
    [RequireComponent(typeof(DefenseView))]
    public sealed class DefenseGame : MonoBehaviour, IDefenseEnemyCombat
    {
        private DefenseView defense_view;

        public DefenseView View => defense_view != null ? defense_view : (defense_view = GetComponent<DefenseView>());

        [Serializable]
        public sealed class EnemySlot
        {
            [FormerlySerializedAs("root")]
            public Transform transform_root;

            [NonSerialized]
            public int health;

            [NonSerialized]
            public float next_attack_at_seconds;

            [NonSerialized]
            public int threat_level;

            [FormerlySerializedAs("roleMarker")]
            public SpriteRenderer sprite_renderer_role_marker;

            [FormerlySerializedAs("attackMarker")]
            public SpriteRenderer sprite_renderer_attack_marker;

            [FormerlySerializedAs("healthBar")]
            public SpriteRenderer sprite_renderer_health_bar;

            [FormerlySerializedAs("aimMarker")]
            public SpriteRenderer sprite_renderer_aim_marker;

            [NonSerialized]
            public DefenseEnemyRole role;

            [NonSerialized]
            public int max_health;

            [NonSerialized]
            public float attack_windup_elapsed_seconds;

            [NonSerialized]
            public float stunned_until_seconds;

            [NonSerialized]
            public float health_visible_until_seconds;

            [NonSerialized]
            public bool is_targeting_player;

            [NonSerialized]
            public Vector2 knockback_velocity;

            [NonSerialized]
            public int last_hit_volley_id;

            [NonSerialized]
            public Vector2 aim_direction;
        }

        [Serializable]
        public sealed class BulletSlot
        {
            [FormerlySerializedAs("root")]
            public Transform transform_root;

            [NonSerialized]
            public Vector2 travel_direction;

            [NonSerialized]
            public float remaining_lifetime_seconds;

            [NonSerialized]
            public int damage;

            [NonSerialized]
            public int volley_id;
        }

        [FormerlySerializedAs("worldCamera")]
        public Camera camera_world;

        [FormerlySerializedAs("player")]
        public Transform transform_player;

        [FormerlySerializedAs("playerArt")]
        public Transform transform_player_art;

        [FormerlySerializedAs("weapon")]
        public Transform transform_weapon;

        [FormerlySerializedAs("crosshair")]
        public Transform transform_crosshair;

        [FormerlySerializedAs("beacon")]
        public Transform transform_beacon;

        [FormerlySerializedAs("enemies")]
        public EnemySlot[] enemy_slots;

        [FormerlySerializedAs("bullets")]
        public BulletSlot[] player_bullet_slots;

        public GameObject game_object_ready_panel { get => View.game_object_ready_panel; set => View.game_object_ready_panel = value; }
        public GameObject game_object_pause_panel { get => View.game_object_pause_panel; set => View.game_object_pause_panel = value; }
        public GameObject game_object_help_panel { get => View.game_object_help_panel; set => View.game_object_help_panel = value; }
        public Button btn_help { get => View.btn_help; set => View.btn_help = value; }
        public Button btn_help_back { get => View.btn_help_back; set => View.btn_help_back = value; }
        public GameObject game_object_result_panel { get => View.game_object_result_panel; set => View.game_object_result_panel = value; }
        public Text txt_health { get => View.txt_health; set => View.txt_health = value; }
        public Text txt_wave { get => View.txt_wave; set => View.txt_wave = value; }
        public Text txt_score { get => View.txt_score; set => View.txt_score = value; }
        public Text txt_hint { get => View.txt_hint; set => View.txt_hint = value; }
        public Text txt_result_stats { get => View.txt_result_stats; set => View.txt_result_stats = value; }
        public Button btn_start { get => View.btn_start; set => View.btn_start = value; }
        public Button btn_resume { get => View.btn_resume; set => View.btn_resume = value; }
        public Button btn_restart { get => View.btn_restart; set => View.btn_restart = value; }

        [FormerlySerializedAs("rewards")]
        public DefenseRewardClient defense_reward_client;

        [FormerlySerializedAs("resultUpload")]
        public GameResultUploadClient game_result_upload_client;

        [FormerlySerializedAs("supplies")]
        public DefenseSupplies defense_supplies;

        [FormerlySerializedAs("audioEffects")]
        public DefenseAudio defense_audio;

        [FormerlySerializedAs("feedbackParticles")]
        public SpriteRenderer[] sprite_renderer_feedback_particles = new SpriteRenderer[0];

        [FormerlySerializedAs("screenShake")]
        [Range(0, 1)]
        public float screen_shake_strength = 0.65f;
        private DefenseFeedback defense_feedback;

        [FormerlySerializedAs("enemyBullets")]
        public DefenseEnemyProjectile[] enemy_projectile_slots = new DefenseEnemyProjectile[0];
        private DefenseEnemyProjectilePool enemy_projectile_pool;

        [FormerlySerializedAs("waveRules")]
        public DefenseWaveRules wave_rules = new DefenseWaveRules();

        public SpriteRenderer[] sprite_renderer_entry_markers { get => View.sprite_renderer_entry_markers; set => View.sprite_renderer_entry_markers = value; }
        public Image img_beacon_health_bar { get => View.img_beacon_health_bar; set => View.img_beacon_health_bar = value; }
        public Image img_dash_bar { get => View.img_dash_bar; set => View.img_dash_bar = value; }
        public Text txt_dash { get => View.txt_dash; set => View.txt_dash = value; }
        public Button btn_next_wave { get => View.btn_next_wave; set => View.btn_next_wave = value; }
        public DefensePhase Phase { get => Model.Phase; private set => Model.Phase = value; }
        public float PhaseRemaining { get => Model.PhaseRemaining; private set => Model.PhaseRemaining = value; }

        public int AliveCount
        {
            get
            {
                int count = 0;

                foreach (var enemy in enemy_slots)
                {
                    if (enemy.transform_root.gameObject.activeSelf)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        public int PendingEnemies => pending_enemy_count;
        public float DashReady => Model.DashReady;

        [FormerlySerializedAs("arenaHalfSize")]
        public Vector2 arena_half_size = new Vector2(11.3f, 5.0f);

        [FormerlySerializedAs("moveSpeed")]
        public float move_speed = 5;

        [FormerlySerializedAs("bulletSpeed")]
        public float bullet_speed = 19;

        public int Threat => wave_rules.Threat(Wave);
        public DefenseState State { get => Model.State; private set => Model.State = value; }
        public int Kills { get => Model.Kills; private set => Model.Kills = value; }
        public int Wave { get => Model.Wave; private set => Model.Wave = value; }
        public int PlayerHealth { get => Model.PlayerHealth; private set => Model.PlayerHealth = value; }
        public int BeaconHealth { get => Model.BeaconHealth; private set => Model.BeaconHealth = value; }
        public string RunId { get => Model.RunId; private set => Model.RunId = value; }
        private float elapsed_seconds { get => Model.Elapsed; set => Model.Elapsed = value; }
        private float next_fire_at_seconds { get => Model.FireAt; set => Model.FireAt = value; }
        private float spawn_delay_seconds { get => Model.SpawnIn; set => Model.SpawnIn = value; }
        private float invulnerable_until_seconds { get => Model.HurtUntil; set => Model.HurtUntil = value; }
        private float dash_until_seconds { get => Model.DashUntil; set => Model.DashUntil = value; }
        private float next_dash_at_seconds { get => Model.NextDash; set => Model.NextDash = value; }
        private int pending_enemy_count { get => Model.Remaining; set => Model.Remaining = value; }
        private int spawned_enemy_count { get => Model.Spawned; set => Model.Spawned = value; }
        private int volley_id { get => Model.VolleyId; set => Model.VolleyId = value; }
        private int entrance_count { get => Model.EntranceCount; set => Model.EntranceCount = value; }
        private Vector2[] entrance_positions => Model.Entrances;
        public DefenseModel Model { get; } = new DefenseModel();

        private void OnEnable()
        {
            defense_reward_client.Changed += RenderRequests;
            game_result_upload_client.Changed += RenderRequests;
        }

        private void RenderRequests() => View.RenderRequests(
            defense_reward_client.Status,
            defense_reward_client.Detail,
            game_result_upload_client.Status,
            !defense_reward_client.IsBusy && !game_result_upload_client.IsBusy && (!defense_reward_client.Succeeded || !game_result_upload_client.Succeeded));

        private void Awake()
        {
            defense_feedback = new DefenseFeedback(this);
            enemy_projectile_pool = new DefenseEnemyProjectilePool(enemy_projectile_slots);
            HidePool();
            State = DefenseState.Ready;
            View.ShowState(DefenseState.Ready);
            transform_crosshair.gameObject.SetActive(false);
            PlayerHealth = BeaconHealth = 100;
            defense_supplies.ResetRun();
            UpdateHud();
            Select(btn_start);
        }

        public void StartRun()
        {
            defense_feedback.Reset();
            defense_reward_client.Cancel();
            game_result_upload_client.Cancel();
            defense_audio.Stop();

            if (GameSessionTracker.Instance.IsRunning)
            {
                GameSessionTracker.Instance.Finish("restarted", PlayerHealth, BeaconHealth);
            }

            HidePool();
            RunId = GameSessionTracker.Instance.Begin(GameMode.SoloDefense);
            Model.Start(RunId);
            defense_supplies.ResetRun();
            transform_player.position = new Vector3(0, -2.2f, 0);
            transform_player_art.localScale = Vector3.one;
            State = DefenseState.Playing;
            BeginWave();
            View.ShowState(DefenseState.Playing);
            transform_crosshair.gameObject.SetActive(true);

            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(null);
            }

            UpdateHud();
        }

        private void Update()
        {

            var keyboard = Keyboard.current;

            if (keyboard != null && keyboard.mKey.wasPressedThisFrame)
            {
                defense_audio.ToggleMute();
            }

            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                HandleEscape();
            }

            if (State != DefenseState.Playing)
            {
                return;
            }

            if (keyboard != null)
            {
                if (keyboard.digit1Key.wasPressedThisFrame)
                {
                    defense_supplies.ChooseCard(0);
                }

                if (keyboard.digit2Key.wasPressedThisFrame)
                {
                    defense_supplies.ChooseCard(1);
                }

                if (keyboard.digit3Key.wasPressedThisFrame)
                {
                    defense_supplies.ChooseCard(2);
                }

                if (keyboard.digit4Key.wasPressedThisFrame)
                {
                    defense_supplies.BuyHeal();
                }

                if (keyboard.digit5Key.wasPressedThisFrame)
                {
                    defense_supplies.BuyRepair();
                }

                if (keyboard.enterKey.wasPressedThisFrame)
                {
                    StartNextWave();
                }
            }

            Vector2 movement = Vector2.zero;

            if (keyboard != null)
            {
                movement.x = (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed ? 1 : 0) - (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed
                    ? 1
                    : 0);
                movement.y = (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed ? 1 : 0) - (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed
                    ? 1
                    : 0);
            }

            Vector2 aim = (Vector2)transform_player.position + Vector2.right;
            var mouse = Mouse.current;

            if (mouse != null)
            {
                aim = camera_world.ScreenToWorldPoint(
                    new Vector3(
                    mouse.position.x.ReadValue(),
                    mouse.position.y.ReadValue(),
                    -camera_world.transform.position.z)) - defense_feedback.CameraOffset;
            }

            bool overUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            Simulate(
                Time.deltaTime,
                movement,
                aim,
                mouse != null && mouse.leftButton.isPressed && !overUi,
                keyboard != null && keyboard.spaceKey.wasPressedThisFrame);
        }

        public void Simulate(float delta, Vector2 movement, Vector2 aim, bool fire, bool dash)
        {
            if (State != DefenseState.Playing)
            {
                return;
            }

            float dt = Mathf.Clamp(delta, 0, 0.05f);
            elapsed_seconds += dt;
            movement = Vector2.ClampMagnitude(movement, 1);

            if (Model.TryDash(dash && movement.sqrMagnitude > 0))
            {
                GameSessionTracker.Instance.Record("dash", 1, transform_player.position);
                defense_audio.Play(DefenseCue.Dash);
                defense_feedback.Dash(movement.normalized);
            }

            Vector2 p = (Vector2)transform_player.position + movement * move_speed * (elapsed_seconds < dash_until_seconds ? 2.7f : 1) * dt;
            p.x = Mathf.Clamp(p.x, -arena_half_size.x + 0.3f, arena_half_size.x - 0.3f);
            p.y = Mathf.Clamp(p.y, -arena_half_size.y + 0.3f, arena_half_size.y - 0.3f);
            transform_player.position = p;
            GameSessionTracker.Instance.Advance(dt, p, PlayerHealth, BeaconHealth);
            Vector2 direction = aim - p;

            if (direction.sqrMagnitude < 0.0001f)
            {
                direction = Vector2.right;
            }

            direction.Normalize();
            transform_crosshair.position = new Vector3(aim.x, aim.y, 0);
            transform_weapon.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
            transform_player_art.localScale = new Vector3(direction.x < 0 ? -1 : 1, 1, 1);

            if (fire && elapsed_seconds >= next_fire_at_seconds && Fire(direction))
            {
                next_fire_at_seconds = elapsed_seconds + defense_supplies.FireInterval;
            }

            AdvanceWave(dt);
            MoveBullets(dt);

            for (int index = 0; index < enemy_slots.Length; index++)
            {
                var enemy = enemy_slots[index];

                if (!enemy.transform_root.gameObject.activeSelf)
                {
                    continue;
                }

                if (elapsed_seconds < enemy.stunned_until_seconds)
                {
                    enemy.transform_root.position = ClampToArena((Vector2)enemy.transform_root.position + enemy.knockback_velocity * dt);
                    enemy.knockback_velocity *= Mathf.Exp(-8 * dt);
                    continue;
                }

                Vector2 ep = enemy.transform_root.position;
                var behavior = DefenseEnemyFactory.For(enemy.role);
                var attack = behavior.attack_strategy;
                bool attackPlayer = behavior.target_strategy.TargetsPlayer(ep, p);

                if (enemy.is_targeting_player != attackPlayer)
                {
                    enemy.attack_windup_elapsed_seconds = 0;
                }

                enemy.is_targeting_player = attackPlayer;
                Vector2 target = attackPlayer ? p : (Vector2)transform_beacon.position;
                float distance = Vector2.Distance(ep, target);

                if (distance > attack.Range)
                {
                    enemy.attack_windup_elapsed_seconds = 0;
                    Vector2 separation = Vector2.zero;

                    for (int other = 0; other < enemy_slots.Length; other++)
                    {
                        if (other == index || !enemy_slots[other].transform_root.gameObject.activeSelf)
                        {
                            continue;
                        }

                        Vector2 away = ep - (Vector2)enemy_slots[other].transform_root.position;

                        if (away.sqrMagnitude < 0.45f * 0.45f)
                        {
                            separation += away.sqrMagnitude > 0.0001f
                                ? away.normalized
                                : (index < other ? Vector2.left : Vector2.right);
                        }
                    }

                    float speed = Mathf.Min(3.5f, behavior.move_speed + enemy.threat_level * 0.12f);
                    Vector2 approach = attackPlayer
                        ? target
                        : target + (Vector2)(Quaternion.Euler(0, 0, index * 137.5f) * Vector2.right) * 0.65f;
                    enemy.transform_root.position = ClampToArena(
                        ep + ((approach - ep).normalized * speed + Vector2.ClampMagnitude(separation, 1) * 0.65f) * dt);
                }
                else if (elapsed_seconds >= enemy.next_attack_at_seconds)
                {
                    if (enemy.attack_windup_elapsed_seconds <= 0)
                    {
                        enemy.aim_direction = (target - ep).sqrMagnitude > 0.0001f
                            ? (target - ep).normalized
                            : Vector2.right;
                    }

                    enemy.attack_windup_elapsed_seconds += dt;

                    if (enemy.attack_windup_elapsed_seconds < attack.WindupSeconds)
                    {
                        continue;
                    }

                    if (attack.Execute(this, enemy))
                    {
                        enemy.attack_windup_elapsed_seconds = 0;
                        enemy.next_attack_at_seconds = elapsed_seconds + attack.CooldownSeconds;
                    }
                }
            }

            if (Phase == DefensePhase.Combat)
            {
                enemy_projectile_pool.Tick(dt, p, transform_beacon.position, arena_half_size, this);
            }

            if (PlayerHealth <= 0 || BeaconHealth <= 0)
            {
                EndRun(PlayerHealth <= 0 ? "death" : "beacon_destroyed");
            }
            else
            {
                defense_supplies.Tick(dt);
            }

            UpdateHud();
        }

        private bool Fire(Vector2 direction)
        {
            int available = 0;

            foreach (var slot in player_bullet_slots)
            {
                if (!slot.transform_root.gameObject.activeSelf)
                {
                    available++;
                }
            }

            int pellets = defense_supplies.PelletCount;

            if (available < pellets)
            {
                return false;
            }

            int shot = 0;
            int volley = ++volley_id;

            foreach (var bullet in player_bullet_slots)
            {
                if (bullet.transform_root.gameObject.activeSelf)
                {
                    continue;
                }

                float angle = pellets == 1 ? 0 : (shot - (pellets - 1) * 0.5f) * 4;
                bullet.travel_direction = Quaternion.Euler(0, 0, angle) * direction;
                bullet.damage = defense_supplies.Damage;
                bullet.volley_id = volley;
                bullet.remaining_lifetime_seconds = 1.25f;
                bullet.transform_root.position = (Vector2)transform_player.position + bullet.travel_direction * 0.65f;
                bullet.transform_root.rotation = transform_weapon.rotation * Quaternion.Euler(0, 0, angle);
                bullet.transform_root.gameObject.SetActive(true);
                GameSessionTracker.Instance.Shot(transform_player.position);

                if (++shot == pellets)
                {
                    break;
                }
            }

            defense_audio.Play(pellets > 1 ? DefenseCue.Scatter : DefenseCue.Shot);
            defense_feedback.Shot(direction, pellets > 1);
            return true;
        }

        private void BeginWave()
        {
            Model.BeginWave(wave_rules, arena_half_size);
            GameSessionTracker.Instance.WaveStarted(Wave);
            defense_audio.Play(DefenseCue.Warning);
            UpdateIndicators();
        }

        public void StartNextWave()
        {
            if (!Model.CanAdvanceWave(defense_supplies.CardChosen))
            {
                return;
            }

            defense_supplies.CloseShop();
            Model.TryAdvanceWave(defense_supplies.CardChosen);
            BeginWave();

            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(null);
            }

            UpdateHud();
        }

        private void AdvanceWave(float dt)
        {
            if (Phase == DefensePhase.Preparation)
            {
                if (defense_supplies.AwaitingCard)
                {
                    return;
                }

                PhaseRemaining = Mathf.Max(0, PhaseRemaining - dt);

                if (PhaseRemaining <= 0)
                {
                    StartNextWave();
                }

                return;
            }

            if (Phase == DefensePhase.Warning)
            {
                PhaseRemaining = Mathf.Max(0, PhaseRemaining - dt);

                if (PhaseRemaining <= 0)
                {
                    Phase = DefensePhase.Combat;
                }

                return;
            }

            if (pending_enemy_count == 0 && AliveCount == 0)
            {
                Model.BeginPreparation(wave_rules);

                foreach (var bullet in player_bullet_slots)
                {
                    bullet.transform_root.gameObject.SetActive(false);
                }

                enemy_projectile_pool.Reset();
                defense_supplies.OpenShop();
                GameSessionTracker.Instance.Record("wave_cleared", Wave, transform_beacon.position);
                return;
            }

            if (pending_enemy_count <= 0)
            {
                return;
            }

            spawn_delay_seconds -= dt;

            if (spawn_delay_seconds > 0)
            {
                return;
            }

            for (int enemyIndex = 0; enemyIndex < enemy_slots.Length; enemyIndex++)
            {
                var enemy = enemy_slots[enemyIndex];

                if (enemy.transform_root.gameObject.activeSelf)
                {
                    continue;
                }

                Vector2 pos = entrance_positions[spawned_enemy_count % entrance_count];
                enemy.transform_root.position = pos;
                enemy.threat_level = Threat;
                enemy.role = wave_rules.Role(Wave, spawned_enemy_count);
                enemy.health = enemy.max_health = Threat + DefenseEnemyFactory.For(enemy.role).bonus_health;
                enemy.attack_windup_elapsed_seconds = enemy.stunned_until_seconds = enemy.health_visible_until_seconds = 0;
                enemy.last_hit_volley_id = -1;
                enemy.knockback_velocity = Vector2.zero;
                enemy.aim_direction = Vector2.zero;
                enemy.is_targeting_player = false;
                enemy.next_attack_at_seconds = elapsed_seconds + 0.5f;
                defense_feedback.Spawn(enemyIndex);
                enemy.transform_root.gameObject.SetActive(true);
                GameSessionTracker.Instance.Record("enemy_spawned", Wave, pos);
                pending_enemy_count--;
                spawned_enemy_count++;
                spawn_delay_seconds = wave_rules.Interval(Wave, Vector2.Distance(pos, transform_beacon.position));
                break;
            }
        }

        private void MoveBullets(float dt)
        {
            foreach (var bullet in player_bullet_slots)
            {
                if (!bullet.transform_root.gameObject.activeSelf)
                {
                    continue;
                }

                Vector2 from = bullet.transform_root.position;
                Vector2 to = from + bullet.travel_direction * bullet_speed * Mathf.Min(dt, Mathf.Max(0, bullet.remaining_lifetime_seconds));
                EnemySlot hit = null;
                int hitIndex = -1;
                float nearest = float.MaxValue;

                for (int enemyIndex = 0; enemyIndex < enemy_slots.Length; enemyIndex++)
                {
                    var enemy = enemy_slots[enemyIndex];

                    if (!enemy.transform_root.gameObject.activeSelf)
                    {
                        continue;
                    }

                    // Swept segment test avoids tunnelling through enemies on slower frames.

                    if (DefenseCollision.SegmentCircle(from, to, enemy.transform_root.position, 0.42f, out float t) && t < nearest)
                    {
                        hit = enemy;
                        hitIndex = enemyIndex;
                        nearest = t;
                    }
                }

                bullet.remaining_lifetime_seconds -= dt;

                if (hit != null)
                {
                    hit.health -= bullet.damage;
                    hit.health_visible_until_seconds = elapsed_seconds + 2;

                    if (hit.last_hit_volley_id != bullet.volley_id)
                    {
                        hit.last_hit_volley_id = bullet.volley_id;
                        hit.attack_windup_elapsed_seconds = 0;
                        hit.stunned_until_seconds = elapsed_seconds + 0.12f;
                        hit.next_attack_at_seconds = Mathf.Max(hit.next_attack_at_seconds, hit.stunned_until_seconds);
                        hit.knockback_velocity = bullet.travel_direction * 4;
                    }

                    defense_feedback.Hit(hitIndex, hit.transform_root.position, bullet.travel_direction, hit.health <= 0);
                    GameSessionTracker.Instance.Hit(hit.transform_root.position);
                    defense_audio.Play(DefenseCue.Hit);

                    if (hit.health <= 0)
                    {
                        hit.transform_root.gameObject.SetActive(false);
                        Kills++;
                        GameSessionTracker.Instance.Kill(hit.transform_root.position);
                        defense_supplies.EnemyKilled(hit.transform_root.position);
                        defense_audio.Play(DefenseCue.Kill);
                    }

                    bullet.transform_root.gameObject.SetActive(false);
                }
                else if (bullet.remaining_lifetime_seconds <= 0 || Mathf.Abs(to.x) > arena_half_size.x + 1 || Mathf.Abs(to.y) > arena_half_size.y + 1)
                {
                    bullet.transform_root.gameObject.SetActive(false);
                }
                else
                {
                    bullet.transform_root.position = to;
                }
            }
        }

        private void EndRun(string reason)
        {
            State = DefenseState.Ended;
            UpdateIndicators();
            defense_feedback.Reset();
            enemy_projectile_pool.Reset();
            defense_supplies.CloseForEnd();
            defense_audio.Stop();
            defense_audio.Play(DefenseCue.Defeat);
            transform_crosshair.gameObject.SetActive(false);
            View.ShowResult(Model, reason);
            var snapshot = GameSessionTracker.Instance.Finish(reason, PlayerHealth, BeaconHealth);
            game_result_upload_client.Submit(snapshot);
            defense_reward_client.Submit(
                new DefenseRunReport { runId = snapshot.runId, ownerPlayerId = snapshot.ownerPlayerId, accountApiRoot = snapshot.accountApiRoot, mode = snapshot.gameMode, reason = snapshot.endReason, claimedKills = snapshot.kills, reachedWave = snapshot.reachedWave, survivalSeconds = snapshot.playedSeconds });
            Select(btn_restart);
        }

        public void ReturnToLobby()
        {
            if (!SceneNavigation.CanLoad(SceneNavigation.Lobby))
            {
                return;
            }

            if (GameSessionTracker.Instance != null && GameSessionTracker.Instance.IsRunning)
            {
                GameSessionTracker.Instance.Finish("returned_to_lobby", PlayerHealth, BeaconHealth);
            }

            defense_reward_client.Cancel();
            game_result_upload_client.Cancel();
            defense_audio.Stop();
            defense_feedback.Reset();
            SceneNavigation.Load(SceneNavigation.Lobby);
        }

        public void RetryRequests()
        {
            game_result_upload_client.Retry();
            defense_reward_client.Retry();
        }

        public int Heal(int amount)
        {
            int restored = Model.Heal(amount);
            UpdateHud();
            return restored;
        }

        public int RepairBeacon(int amount)
        {
            int restored = Model.RepairBeacon(amount);

            if (restored > 0)
            {
                defense_feedback.Pickup(transform_beacon.position);
            }

            UpdateHud();
            return restored;
        }

        public void ApplyEnemyDamage(bool playerTarget, int amount)
        {
            int damage = Model.ApplyDamage(playerTarget, amount);

            if (damage <= 0)
            {
                return;
            }

            GameSessionTracker.Instance.Damage(!playerTarget, damage, playerTarget ? transform_player.position : transform_beacon.position);
            defense_audio.Play(DefenseCue.Hurt);
            defense_feedback.Hurt(!playerTarget);
        }

        public bool TryFireEnemyProjectile(EnemySlot enemy)
        {
            if (State != DefenseState.Playing || Phase != DefensePhase.Combat || !enemy.transform_root.gameObject.activeSelf)
            {
                return false;
            }

            Vector2 muzzle = (Vector2)enemy.transform_root.position + enemy.aim_direction * 0.55f;

            if (!enemy_projectile_pool.TryFire(muzzle, enemy.aim_direction, 6 + enemy.threat_level))
            {
                return false;
            }

            defense_feedback.EnemyShot(muzzle, enemy.aim_direction);
            return true;
        }

        private Vector2 ClampToArena(Vector2 position) => new Vector2(
            Mathf.Clamp(position.x, -arena_half_size.x, arena_half_size.x),
            Mathf.Clamp(position.y, -arena_half_size.y, arena_half_size.y));

        public void Pause()
        {
            if (!Model.Pause())
            {
                return;
            }

            defense_feedback.SuspendCamera();
            defense_audio.Stop();
            defense_supplies.View.ShowShop(false);
            defense_supplies.Refresh();
            View.ShowState(DefenseState.Paused);
            transform_crosshair.gameObject.SetActive(false);
            GameSessionTracker.Instance.Record("paused", 0, transform_player.position);
            Select(btn_resume);
        }

        public void Resume()
        {
            if (!Model.Resume())
            {
                return;
            }

            defense_supplies.View.ShowShop(defense_supplies.ShopOpen);
            defense_supplies.Refresh();
            View.ShowState(DefenseState.Playing);
            transform_crosshair.gameObject.SetActive(true);
            GameSessionTracker.Instance.Record("resumed", 0, transform_player.position);

            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(null);
            }
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused)
            {
                Pause();
            }
        }

        private void LateUpdate()
        {
            if (State == DefenseState.Playing)
            {
                defense_feedback.Tick(Time.unscaledDeltaTime, elapsed_seconds < dash_until_seconds);
            }

            UpdateIndicators();
        }

        private void OnDisable()
        {
            defense_reward_client.Changed -= RenderRequests;
            game_result_upload_client.Changed -= RenderRequests;
            defense_feedback?.Reset();
        }

        public void PickupFeedback(Vector2 position)
        {
            defense_feedback?.Pickup(position);
        }

        public void HandleEscape()
        {
            if (State == DefenseState.Playing)
            {
                Pause();
            }
            else if (State == DefenseState.Paused)
            {
                if (game_object_help_panel.activeSelf)
                {
                    CloseHelp();
                }
                else
                {
                    Resume();
                }
            }
        }

        public void OpenHelp()
        {
            if (State != DefenseState.Paused)
            {
                return;
            }

            View.ShowHelp(true);
        }

        public void CloseHelp()
        {
            if (State != DefenseState.Paused)
            {
                return;
            }

            View.ShowHelp(false);
        }

        private void HidePool()
        {
            foreach (var enemy in enemy_slots)
            {
                enemy.transform_root.gameObject.SetActive(false);
            }

            foreach (var bullet in player_bullet_slots)
            {
                bullet.transform_root.gameObject.SetActive(false);
            }

            enemy_projectile_pool.Reset();
        }

        private void UpdateHud() => View.RenderHud(Model, AliveCount, Threat, defense_supplies.AwaitingCard);

        private void UpdateIndicators() => View.RenderIndicators(Model, enemy_slots);

        private static void Select(Button button) => DefenseView.Select(button);
    }
}
