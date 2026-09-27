using System;
using UnityEngine;
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
        private DefenseView view;

        public DefenseView View => view != null ? view : (view = GetComponent<DefenseView>());

        [Serializable]
        public sealed class EnemySlot
        {
            public Transform root;

            [NonSerialized]
            public int health;

            [NonSerialized]
            public float nextAttack;

            [NonSerialized]
            public int level;
            public SpriteRenderer roleMarker, attackMarker, healthBar;
            public SpriteRenderer aimMarker;

            [NonSerialized]
            public DefenseEnemyRole role;

            [NonSerialized]
            public int maxHealth;

            [NonSerialized]
            public float windup, stunnedUntil, showHealthUntil;

            [NonSerialized]
            public bool targetingPlayer;

            [NonSerialized]
            public Vector2 knockback;

            [NonSerialized]
            public int lastVolley;

            [NonSerialized]
            public Vector2 aimDirection;
        }

        [Serializable]
        public sealed class BulletSlot
        {
            public Transform root;

            [NonSerialized]
            public Vector2 direction;

            [NonSerialized]
            public float life;

            [NonSerialized]
            public int damage;

            [NonSerialized]
            public int volley;
        }

        public Camera worldCamera;
        public Transform player;
        public Transform playerArt;
        public Transform weapon;
        public Transform crosshair;
        public Transform beacon;
        public EnemySlot[] enemies;
        public BulletSlot[] bullets;

        public GameObject readyPanel { get => View.readyPanel; set => View.readyPanel = value; }
        public GameObject pausePanel { get => View.pausePanel; set => View.pausePanel = value; }
        public GameObject helpPanel { get => View.helpPanel; set => View.helpPanel = value; }
        public Button helpButton { get => View.helpButton; set => View.helpButton = value; }
        public Button helpBackButton { get => View.helpBackButton; set => View.helpBackButton = value; }
        public GameObject resultPanel { get => View.resultPanel; set => View.resultPanel = value; }
        public Text healthText { get => View.healthText; set => View.healthText = value; }
        public Text waveText { get => View.waveText; set => View.waveText = value; }
        public Text scoreText { get => View.scoreText; set => View.scoreText = value; }
        public Text hintText { get => View.hintText; set => View.hintText = value; }
        public Text resultStats { get => View.resultStats; set => View.resultStats = value; }
        public Button startButton { get => View.startButton; set => View.startButton = value; }
        public Button resumeButton { get => View.resumeButton; set => View.resumeButton = value; }
        public Button restartButton { get => View.restartButton; set => View.restartButton = value; }

        public DefenseRewardClient rewards;
        public GameResultUploadClient resultUpload;
        public DefenseSupplies supplies;
        public DefenseAudio audioEffects;
        public SpriteRenderer[] feedbackParticles = new SpriteRenderer[0];

        [Range(0, 1)]
        public float screenShake = 0.65f;
        private DefenseFeedback feedback;
        public DefenseEnemyProjectile[] enemyBullets = new DefenseEnemyProjectile[0];
        private DefenseEnemyProjectilePool enemyProjectilePool;
        public DefenseWaveRules waveRules = new DefenseWaveRules();

        public SpriteRenderer[] entryMarkers { get => View.entryMarkers; set => View.entryMarkers = value; }
        public Image beaconHealthBar { get => View.beaconHealthBar; set => View.beaconHealthBar = value; }
        public Image dashBar { get => View.dashBar; set => View.dashBar = value; }
        public Text dashText { get => View.dashText; set => View.dashText = value; }
        public Button nextWaveButton { get => View.nextWaveButton; set => View.nextWaveButton = value; }
        public DefensePhase Phase { get => Model.Phase; private set => Model.Phase = value; }
        public float PhaseRemaining { get => Model.PhaseRemaining; private set => Model.PhaseRemaining = value; }

        public int AliveCount
        {
            get
            {
                int count = 0;

                foreach (var enemy in enemies)
                {
                    if (enemy.root.gameObject.activeSelf)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        public int PendingEnemies => remaining;
        public float DashReady => Model.DashReady;

        public Vector2 arenaHalfSize = new Vector2(11.3f, 5.0f);
        public float moveSpeed = 5;
        public float bulletSpeed = 19;

        public int Threat => waveRules.Threat(Wave);
        public DefenseState State { get => Model.State; private set => Model.State = value; }
        public int Kills { get => Model.Kills; private set => Model.Kills = value; }
        public int Wave { get => Model.Wave; private set => Model.Wave = value; }
        public int PlayerHealth { get => Model.PlayerHealth; private set => Model.PlayerHealth = value; }
        public int BeaconHealth { get => Model.BeaconHealth; private set => Model.BeaconHealth = value; }
        public string RunId { get => Model.RunId; private set => Model.RunId = value; }
        private float elapsed { get => Model.Elapsed; set => Model.Elapsed = value; }
        private float fireAt { get => Model.FireAt; set => Model.FireAt = value; }
        private float spawnIn { get => Model.SpawnIn; set => Model.SpawnIn = value; }
        private float hurtUntil { get => Model.HurtUntil; set => Model.HurtUntil = value; }
        private float dashUntil { get => Model.DashUntil; set => Model.DashUntil = value; }
        private float nextDash { get => Model.NextDash; set => Model.NextDash = value; }
        private int remaining { get => Model.Remaining; set => Model.Remaining = value; }
        private int spawned { get => Model.Spawned; set => Model.Spawned = value; }
        private int volleyId { get => Model.VolleyId; set => Model.VolleyId = value; }
        private int entranceCount { get => Model.EntranceCount; set => Model.EntranceCount = value; }
        private Vector2[] entrances => Model.Entrances;
        public DefenseModel Model { get; } = new DefenseModel();

        private void OnEnable()
        {
            rewards.Changed += RenderRequests;
            resultUpload.Changed += RenderRequests;
        }

        private void RenderRequests() => View.RenderRequests(
            rewards.Status,
            rewards.Detail,
            resultUpload.Status,
            !rewards.IsBusy && !resultUpload.IsBusy && (!rewards.Succeeded || !resultUpload.Succeeded));

        private void Awake()
        {
            feedback = new DefenseFeedback(this);
            enemyProjectilePool = new DefenseEnemyProjectilePool(enemyBullets);
            HidePool();
            State = DefenseState.Ready;
            View.ShowState(DefenseState.Ready);
            crosshair.gameObject.SetActive(false);
            PlayerHealth = BeaconHealth = 100;
            supplies.ResetRun();
            UpdateHud();
            Select(startButton);
        }

        public void StartRun()
        {
            feedback.Reset();
            rewards.Cancel();
            resultUpload.Cancel();
            audioEffects.Stop();

            if (GameSessionTracker.Instance.IsRunning)
            {
                GameSessionTracker.Instance.Finish("restarted", PlayerHealth, BeaconHealth);
            }

            HidePool();
            RunId = GameSessionTracker.Instance.Begin(GameMode.SoloDefense);
            Model.Start(RunId);
            supplies.ResetRun();
            player.position = new Vector3(0, -2.2f, 0);
            playerArt.localScale = Vector3.one;
            State = DefenseState.Playing;
            BeginWave();
            View.ShowState(DefenseState.Playing);
            crosshair.gameObject.SetActive(true);

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
                audioEffects.ToggleMute();
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
                    supplies.ChooseCard(0);
                }

                if (keyboard.digit2Key.wasPressedThisFrame)
                {
                    supplies.ChooseCard(1);
                }

                if (keyboard.digit3Key.wasPressedThisFrame)
                {
                    supplies.ChooseCard(2);
                }

                if (keyboard.digit4Key.wasPressedThisFrame)
                {
                    supplies.BuyHeal();
                }

                if (keyboard.digit5Key.wasPressedThisFrame)
                {
                    supplies.BuyRepair();
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

            Vector2 aim = (Vector2)player.position + Vector2.right;
            var mouse = Mouse.current;

            if (mouse != null)
            {
                aim = worldCamera.ScreenToWorldPoint(
                    new Vector3(
                    mouse.position.x.ReadValue(),
                    mouse.position.y.ReadValue(),
                    -worldCamera.transform.position.z)) - feedback.CameraOffset;
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
            elapsed += dt;
            movement = Vector2.ClampMagnitude(movement, 1);

            if (Model.TryDash(dash && movement.sqrMagnitude > 0))
            {
                GameSessionTracker.Instance.Record("dash", 1, player.position);
                audioEffects.Play(DefenseCue.Dash);
                feedback.Dash(movement.normalized);
            }

            Vector2 p = (Vector2)player.position + movement * moveSpeed * (elapsed < dashUntil ? 2.7f : 1) * dt;
            p.x = Mathf.Clamp(p.x, -arenaHalfSize.x + 0.3f, arenaHalfSize.x - 0.3f);
            p.y = Mathf.Clamp(p.y, -arenaHalfSize.y + 0.3f, arenaHalfSize.y - 0.3f);
            player.position = p;
            GameSessionTracker.Instance.Advance(dt, p, PlayerHealth, BeaconHealth);
            Vector2 direction = aim - p;

            if (direction.sqrMagnitude < 0.0001f)
            {
                direction = Vector2.right;
            }

            direction.Normalize();
            crosshair.position = new Vector3(aim.x, aim.y, 0);
            weapon.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
            playerArt.localScale = new Vector3(direction.x < 0 ? -1 : 1, 1, 1);

            if (fire && elapsed >= fireAt && Fire(direction))
            {
                fireAt = elapsed + supplies.FireInterval;
            }

            AdvanceWave(dt);
            MoveBullets(dt);

            for (int index = 0; index < enemies.Length; index++)
            {
                var enemy = enemies[index];

                if (!enemy.root.gameObject.activeSelf)
                {
                    continue;
                }

                if (elapsed < enemy.stunnedUntil)
                {
                    enemy.root.position = ClampToArena((Vector2)enemy.root.position + enemy.knockback * dt);
                    enemy.knockback *= Mathf.Exp(-8 * dt);
                    continue;
                }

                Vector2 ep = enemy.root.position;
                var behavior = DefenseEnemyFactory.For(enemy.role);
                var attack = behavior.Attack;
                bool attackPlayer = behavior.Target.TargetsPlayer(ep, p);

                if (enemy.targetingPlayer != attackPlayer)
                {
                    enemy.windup = 0;
                }

                enemy.targetingPlayer = attackPlayer;
                Vector2 target = attackPlayer ? p : (Vector2)beacon.position;
                float distance = Vector2.Distance(ep, target);

                if (distance > attack.Range)
                {
                    enemy.windup = 0;
                    Vector2 separation = Vector2.zero;

                    for (int other = 0; other < enemies.Length; other++)
                    {
                        if (other == index || !enemies[other].root.gameObject.activeSelf)
                        {
                            continue;
                        }

                        Vector2 away = ep - (Vector2)enemies[other].root.position;

                        if (away.sqrMagnitude < 0.45f * 0.45f)
                        {
                            separation += away.sqrMagnitude > 0.0001f
                                ? away.normalized
                                : (index < other ? Vector2.left : Vector2.right);
                        }
                    }

                    float speed = Mathf.Min(3.5f, behavior.MoveSpeed + enemy.level * 0.12f);
                    Vector2 approach = attackPlayer
                        ? target
                        : target + (Vector2)(Quaternion.Euler(0, 0, index * 137.5f) * Vector2.right) * 0.65f;
                    enemy.root.position = ClampToArena(
                        ep + ((approach - ep).normalized * speed + Vector2.ClampMagnitude(separation, 1) * 0.65f) * dt);
                }
                else if (elapsed >= enemy.nextAttack)
                {
                    if (enemy.windup <= 0)
                    {
                        enemy.aimDirection = (target - ep).sqrMagnitude > 0.0001f
                            ? (target - ep).normalized
                            : Vector2.right;
                    }

                    enemy.windup += dt;

                    if (enemy.windup < attack.WindupSeconds)
                    {
                        continue;
                    }

                    if (attack.Execute(this, enemy))
                    {
                        enemy.windup = 0;
                        enemy.nextAttack = elapsed + attack.CooldownSeconds;
                    }
                }
            }

            if (Phase == DefensePhase.Combat)
            {
                enemyProjectilePool.Tick(dt, p, beacon.position, arenaHalfSize, this);
            }

            if (PlayerHealth <= 0 || BeaconHealth <= 0)
            {
                EndRun(PlayerHealth <= 0 ? "death" : "beacon_destroyed");
            }
            else
            {
                supplies.Tick(dt);
            }

            UpdateHud();
        }

        private bool Fire(Vector2 direction)
        {
            int available = 0;

            foreach (var slot in bullets)
            {
                if (!slot.root.gameObject.activeSelf)
                {
                    available++;
                }
            }

            int pellets = supplies.PelletCount;

            if (available < pellets)
            {
                return false;
            }

            int shot = 0;
            int volley = ++volleyId;

            foreach (var bullet in bullets)
            {
                if (bullet.root.gameObject.activeSelf)
                {
                    continue;
                }

                float angle = pellets == 1 ? 0 : (shot - (pellets - 1) * 0.5f) * 4;
                bullet.direction = Quaternion.Euler(0, 0, angle) * direction;
                bullet.damage = supplies.Damage;
                bullet.volley = volley;
                bullet.life = 1.25f;
                bullet.root.position = (Vector2)player.position + bullet.direction * 0.65f;
                bullet.root.rotation = weapon.rotation * Quaternion.Euler(0, 0, angle);
                bullet.root.gameObject.SetActive(true);
                GameSessionTracker.Instance.Shot(player.position);

                if (++shot == pellets)
                {
                    break;
                }
            }

            audioEffects.Play(pellets > 1 ? DefenseCue.Scatter : DefenseCue.Shot);
            feedback.Shot(direction, pellets > 1);
            return true;
        }

        private void BeginWave()
        {
            Model.BeginWave(waveRules, arenaHalfSize);
            GameSessionTracker.Instance.WaveStarted(Wave);
            audioEffects.Play(DefenseCue.Warning);
            UpdateIndicators();
        }

        public void StartNextWave()
        {
            if (!Model.CanAdvanceWave(supplies.CardChosen))
            {
                return;
            }

            supplies.CloseShop();
            Model.TryAdvanceWave(supplies.CardChosen);
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
                if (supplies.AwaitingCard)
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

            if (remaining == 0 && AliveCount == 0)
            {
                Model.BeginPreparation(waveRules);

                foreach (var bullet in bullets)
                {
                    bullet.root.gameObject.SetActive(false);
                }

                enemyProjectilePool.Reset();
                supplies.OpenShop();
                GameSessionTracker.Instance.Record("wave_cleared", Wave, beacon.position);
                return;
            }

            if (remaining <= 0)
            {
                return;
            }

            spawnIn -= dt;

            if (spawnIn > 0)
            {
                return;
            }

            for (int enemyIndex = 0; enemyIndex < enemies.Length; enemyIndex++)
            {
                var enemy = enemies[enemyIndex];

                if (enemy.root.gameObject.activeSelf)
                {
                    continue;
                }

                Vector2 pos = entrances[spawned % entranceCount];
                enemy.root.position = pos;
                enemy.level = Threat;
                enemy.role = waveRules.Role(Wave, spawned);
                enemy.health = enemy.maxHealth = Threat + DefenseEnemyFactory.For(enemy.role).BonusHealth;
                enemy.windup = enemy.stunnedUntil = enemy.showHealthUntil = 0;
                enemy.lastVolley = -1;
                enemy.knockback = Vector2.zero;
                enemy.aimDirection = Vector2.zero;
                enemy.targetingPlayer = false;
                enemy.nextAttack = elapsed + 0.5f;
                feedback.Spawn(enemyIndex);
                enemy.root.gameObject.SetActive(true);
                GameSessionTracker.Instance.Record("enemy_spawned", Wave, pos);
                remaining--;
                spawned++;
                spawnIn = waveRules.Interval(Wave, Vector2.Distance(pos, beacon.position));
                break;
            }
        }

        private void MoveBullets(float dt)
        {
            foreach (var bullet in bullets)
            {
                if (!bullet.root.gameObject.activeSelf)
                {
                    continue;
                }

                Vector2 from = bullet.root.position;
                Vector2 to = from + bullet.direction * bulletSpeed * Mathf.Min(dt, Mathf.Max(0, bullet.life));
                EnemySlot hit = null;
                int hitIndex = -1;
                float nearest = float.MaxValue;

                for (int enemyIndex = 0; enemyIndex < enemies.Length; enemyIndex++)
                {
                    var enemy = enemies[enemyIndex];

                    if (!enemy.root.gameObject.activeSelf)
                    {
                        continue;
                    }

                    // Swept segment test avoids tunnelling through enemies on slower frames.

                    if (DefenseCollision.SegmentCircle(from, to, enemy.root.position, 0.42f, out float t) && t < nearest)
                    {
                        hit = enemy;
                        hitIndex = enemyIndex;
                        nearest = t;
                    }
                }

                bullet.life -= dt;

                if (hit != null)
                {
                    hit.health -= bullet.damage;
                    hit.showHealthUntil = elapsed + 2;

                    if (hit.lastVolley != bullet.volley)
                    {
                        hit.lastVolley = bullet.volley;
                        hit.windup = 0;
                        hit.stunnedUntil = elapsed + 0.12f;
                        hit.nextAttack = Mathf.Max(hit.nextAttack, hit.stunnedUntil);
                        hit.knockback = bullet.direction * 4;
                    }

                    feedback.Hit(hitIndex, hit.root.position, bullet.direction, hit.health <= 0);
                    GameSessionTracker.Instance.Hit(hit.root.position);
                    audioEffects.Play(DefenseCue.Hit);

                    if (hit.health <= 0)
                    {
                        hit.root.gameObject.SetActive(false);
                        Kills++;
                        GameSessionTracker.Instance.Kill(hit.root.position);
                        supplies.EnemyKilled(hit.root.position);
                        audioEffects.Play(DefenseCue.Kill);
                    }

                    bullet.root.gameObject.SetActive(false);
                }
                else if (bullet.life <= 0 || Mathf.Abs(to.x) > arenaHalfSize.x + 1 || Mathf.Abs(to.y) > arenaHalfSize.y + 1)
                {
                    bullet.root.gameObject.SetActive(false);
                }
                else
                {
                    bullet.root.position = to;
                }
            }
        }

        private void EndRun(string reason)
        {
            State = DefenseState.Ended;
            UpdateIndicators();
            feedback.Reset();
            enemyProjectilePool.Reset();
            supplies.CloseForEnd();
            audioEffects.Stop();
            audioEffects.Play(DefenseCue.Defeat);
            crosshair.gameObject.SetActive(false);
            View.ShowResult(Model, reason);
            var snapshot = GameSessionTracker.Instance.Finish(reason, PlayerHealth, BeaconHealth);
            resultUpload.Submit(snapshot);
            rewards.Submit(
                new DefenseRunReport { runId = snapshot.runId, ownerPlayerId = snapshot.ownerPlayerId, accountApiRoot = snapshot.accountApiRoot, mode = snapshot.gameMode, reason = snapshot.endReason, claimedKills = snapshot.kills, reachedWave = snapshot.reachedWave, survivalSeconds = snapshot.playedSeconds });
            Select(restartButton);
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

            rewards.Cancel();
            resultUpload.Cancel();
            audioEffects.Stop();
            feedback.Reset();
            SceneNavigation.Load(SceneNavigation.Lobby);
        }

        public void RetryRequests()
        {
            resultUpload.Retry();
            rewards.Retry();
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
                feedback.Pickup(beacon.position);
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

            GameSessionTracker.Instance.Damage(!playerTarget, damage, playerTarget ? player.position : beacon.position);
            audioEffects.Play(DefenseCue.Hurt);
            feedback.Hurt(!playerTarget);
        }

        public bool TryFireEnemyProjectile(EnemySlot enemy)
        {
            if (State != DefenseState.Playing || Phase != DefensePhase.Combat || !enemy.root.gameObject.activeSelf)
            {
                return false;
            }

            Vector2 muzzle = (Vector2)enemy.root.position + enemy.aimDirection * 0.55f;

            if (!enemyProjectilePool.TryFire(muzzle, enemy.aimDirection, 6 + enemy.level))
            {
                return false;
            }

            feedback.EnemyShot(muzzle, enemy.aimDirection);
            return true;
        }

        private Vector2 ClampToArena(Vector2 position) => new Vector2(
            Mathf.Clamp(position.x, -arenaHalfSize.x, arenaHalfSize.x),
            Mathf.Clamp(position.y, -arenaHalfSize.y, arenaHalfSize.y));

        public void Pause()
        {
            if (!Model.Pause())
            {
                return;
            }

            feedback.SuspendCamera();
            audioEffects.Stop();
            supplies.View.ShowShop(false);
            supplies.Refresh();
            View.ShowState(DefenseState.Paused);
            crosshair.gameObject.SetActive(false);
            GameSessionTracker.Instance.Record("paused", 0, player.position);
            Select(resumeButton);
        }

        public void Resume()
        {
            if (!Model.Resume())
            {
                return;
            }

            supplies.View.ShowShop(supplies.ShopOpen);
            supplies.Refresh();
            View.ShowState(DefenseState.Playing);
            crosshair.gameObject.SetActive(true);
            GameSessionTracker.Instance.Record("resumed", 0, player.position);

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
                feedback.Tick(Time.unscaledDeltaTime, elapsed < dashUntil);
            }

            UpdateIndicators();
        }

        private void OnDisable()
        {
            rewards.Changed -= RenderRequests;
            resultUpload.Changed -= RenderRequests;
            feedback?.Reset();
        }

        public void PickupFeedback(Vector2 position)
        {
            feedback?.Pickup(position);
        }

        public void HandleEscape()
        {
            if (State == DefenseState.Playing)
            {
                Pause();
            }
            else if (State == DefenseState.Paused)
            {
                if (helpPanel.activeSelf)
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
            foreach (var enemy in enemies)
            {
                enemy.root.gameObject.SetActive(false);
            }

            foreach (var bullet in bullets)
            {
                bullet.root.gameObject.SetActive(false);
            }

            enemyProjectilePool.Reset();
        }

        private void UpdateHud() => View.RenderHud(Model, AliveCount, Threat, supplies.AwaitingCard);

        private void UpdateIndicators() => View.RenderIndicators(Model, enemies);

        private static void Select(Button button) => DefenseView.Select(button);
    }
}
