using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Zpd.Gameplay;

namespace Zpd.Defense
{
    public enum DefenseState { Ready, Playing, Paused, Ended }

    /// <summary>Simulates only pre-authored actors/projectiles. No Instantiate or runtime scene construction.</summary>
    public sealed class DefenseGame : MonoBehaviour, IDefenseEnemyCombat
    {
        [Serializable]
        public sealed class EnemySlot
        {
            public Transform root;
            [NonSerialized] public int health;
            [NonSerialized] public float nextAttack;
            [NonSerialized] public int level;
            public SpriteRenderer roleMarker, attackMarker, healthBar;
            public SpriteRenderer aimMarker;
            [NonSerialized] public DefenseEnemyRole role;
            [NonSerialized] public int maxHealth;
            [NonSerialized] public float windup, stunnedUntil, showHealthUntil;
            [NonSerialized] public bool targetingPlayer;
            [NonSerialized] public Vector2 knockback;
            [NonSerialized] public int lastVolley;
            [NonSerialized] public Vector2 aimDirection;
        }
        [Serializable]
        public sealed class BulletSlot
        {
            public Transform root;
            [NonSerialized] public Vector2 direction;
            [NonSerialized] public float life;
            [NonSerialized] public int damage;
            [NonSerialized] public int volley;
        }

        public Camera worldCamera;
        public Transform player;
        public Transform playerArt;
        public Transform weapon;
        public Transform crosshair;
        public Transform beacon;
        public EnemySlot[] enemies;
        public BulletSlot[] bullets;
        public GameObject readyPanel;
        public GameObject pausePanel;
        public GameObject helpPanel;
        public Button helpButton;
        public Button helpBackButton;
        public GameObject resultPanel;
        public Text healthText;
        public Text waveText;
        public Text scoreText;
        public Text hintText;
        public Text resultStats;
        public Button startButton;
        public Button resumeButton;
        public Button restartButton;
        public DefenseRewardClient rewards;
        public GameResultUploadClient resultUpload;
        public DefenseSupplies supplies;
        public DefenseAudio audioEffects;
        public SpriteRenderer[] feedbackParticles = new SpriteRenderer[0];
        [Range(0, 1)] public float screenShake = 0.65f;
        private DefenseFeedback feedback;
        public DefenseEnemyProjectile[] enemyBullets = new DefenseEnemyProjectile[0];
        private DefenseEnemyProjectilePool enemyProjectilePool;
        public DefenseWaveRules waveRules = new DefenseWaveRules();
        public SpriteRenderer[] entryMarkers = new SpriteRenderer[0];
        public Image beaconHealthBar, dashBar;
        public Text dashText;
        public Button nextWaveButton;
        public DefensePhase Phase { get; private set; }
        public float PhaseRemaining { get; private set; }
        public int AliveCount { get { int count = 0; foreach (var enemy in enemies) if (enemy.root.gameObject.activeSelf) count++; return count; } }
        public int PendingEnemies => remaining;
        public float DashReady => Mathf.Clamp01(1 - (nextDash - elapsed) / 2);
        public Vector2 arenaHalfSize = new Vector2(11.3f, 5.0f);
        public float moveSpeed = 5;
        public float bulletSpeed = 19;
        public int Threat => waveRules.Threat(Wave);
        public DefenseState State { get; private set; }
        public int Kills { get; private set; }
        public int Wave { get; private set; }
        public int PlayerHealth { get; private set; }
        public int BeaconHealth { get; private set; }
        public string RunId { get; private set; }
        private float elapsed;
        private float fireAt;
        private float spawnIn;
        private float hurtUntil;
        private float dashUntil;
        private float nextDash;
        private int remaining;
        private int spawned, volleyId, entranceCount;
        private readonly Vector2[] entrances = new Vector2[2];

        private void Awake()
        {
            feedback = new DefenseFeedback(this);
            enemyProjectilePool = new DefenseEnemyProjectilePool(enemyBullets);
            HidePool();
            State = DefenseState.Ready;
            readyPanel.SetActive(true);
            pausePanel.SetActive(false);
            helpPanel.SetActive(false);
            resultPanel.SetActive(false);
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
                GameSessionTracker.Instance.Finish("restarted", PlayerHealth, BeaconHealth);
            HidePool();
            RunId = GameSessionTracker.Instance.Begin(GameMode.SoloDefense);
            Kills = 0;
            Wave = 1;
            elapsed = fireAt = hurtUntil = dashUntil = nextDash = 0;
            volleyId = 0;
            PlayerHealth = BeaconHealth = 100;
            supplies.ResetRun();
            player.position = new Vector3(0, -2.2f, 0);
            playerArt.localScale = Vector3.one;
            State = DefenseState.Playing;
            BeginWave();
            readyPanel.SetActive(false);
            pausePanel.SetActive(false);
            helpPanel.SetActive(false);
            resultPanel.SetActive(false);
            crosshair.gameObject.SetActive(true);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            UpdateHud();
        }

        private void Update()
        {
            if (State == DefenseState.Ended)
                rewards.retryButton.interactable = !rewards.IsBusy && !resultUpload.IsBusy && (!rewards.Succeeded || !resultUpload.Succeeded);
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.mKey.wasPressedThisFrame) audioEffects.ToggleMute();
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
                HandleEscape();
            if (State != DefenseState.Playing) return;
            if (keyboard != null)
            {
                if (keyboard.digit1Key.wasPressedThisFrame) supplies.ChooseCard(0);
                if (keyboard.digit2Key.wasPressedThisFrame) supplies.ChooseCard(1);
                if (keyboard.digit3Key.wasPressedThisFrame) supplies.ChooseCard(2);
                if (keyboard.digit4Key.wasPressedThisFrame) supplies.BuyHeal();
                if (keyboard.digit5Key.wasPressedThisFrame) supplies.BuyRepair();
                if (keyboard.enterKey.wasPressedThisFrame) StartNextWave();
            }
            Vector2 movement = Vector2.zero;
            if (keyboard != null)
            {
                movement.x = (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed ? 1 : 0) - (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed ? 1 : 0);
                movement.y = (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed ? 1 : 0) - (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed ? 1 : 0);
            }
            Vector2 aim = (Vector2)player.position + Vector2.right;
            var mouse = Mouse.current;
            if (mouse != null) aim = worldCamera.ScreenToWorldPoint(new Vector3(mouse.position.x.ReadValue(), mouse.position.y.ReadValue(), -worldCamera.transform.position.z)) - feedback.CameraOffset;
            bool overUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            Simulate(Time.deltaTime, movement, aim, mouse != null && mouse.leftButton.isPressed && !overUi,
                keyboard != null && keyboard.spaceKey.wasPressedThisFrame);
        }

        public void Simulate(float delta, Vector2 movement, Vector2 aim, bool fire, bool dash)
        {
            if (State != DefenseState.Playing) return;
            float dt = Mathf.Clamp(delta, 0, 0.05f);
            elapsed += dt;
            movement = Vector2.ClampMagnitude(movement, 1);
            if (dash && movement.sqrMagnitude > 0 && elapsed >= nextDash)
            {
                dashUntil = elapsed + 0.16f;
                nextDash = elapsed + 2;
                hurtUntil = Mathf.Max(hurtUntil, dashUntil);
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
            if (direction.sqrMagnitude < 0.0001f) direction = Vector2.right;
            direction.Normalize();
            crosshair.position = new Vector3(aim.x, aim.y, 0);
            weapon.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
            playerArt.localScale = new Vector3(direction.x < 0 ? -1 : 1, 1, 1);
            if (fire && elapsed >= fireAt && Fire(direction)) fireAt = elapsed + supplies.FireInterval;

            AdvanceWave(dt);
            MoveBullets(dt);
            for (int index = 0; index < enemies.Length; index++)
            {
                var enemy = enemies[index];
                if (!enemy.root.gameObject.activeSelf) continue;
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
                if (enemy.targetingPlayer != attackPlayer) enemy.windup = 0;
                enemy.targetingPlayer = attackPlayer;
                Vector2 target = attackPlayer ? p : (Vector2)beacon.position;
                float distance = Vector2.Distance(ep, target);
                if (distance > attack.Range)
                {
                    enemy.windup = 0;
                    Vector2 separation = Vector2.zero;
                    for (int other = 0; other < enemies.Length; other++)
                    {
                        if (other == index || !enemies[other].root.gameObject.activeSelf) continue;
                        Vector2 away = ep - (Vector2)enemies[other].root.position;
                        if (away.sqrMagnitude < 0.45f * 0.45f)
                            separation += away.sqrMagnitude > 0.0001f ? away.normalized : (index < other ? Vector2.left : Vector2.right);
                    }
                    float speed = Mathf.Min(3.5f, behavior.MoveSpeed + enemy.level * 0.12f);
                    Vector2 approach = attackPlayer ? target : target + (Vector2)(Quaternion.Euler(0, 0, index * 137.5f) * Vector2.right) * 0.65f;
                    enemy.root.position = ClampToArena(ep + ((approach - ep).normalized * speed + Vector2.ClampMagnitude(separation, 1) * 0.65f) * dt);
                }
                else if (elapsed >= enemy.nextAttack)
                {
                    if (enemy.windup <= 0)
                    {
                        enemy.aimDirection = (target - ep).sqrMagnitude > 0.0001f ? (target - ep).normalized : Vector2.right;
                    }
                    enemy.windup += dt;
                    if (enemy.windup < attack.WindupSeconds) continue;
                    if (attack.Execute(this, enemy))
                    {
                        enemy.windup = 0;
                        enemy.nextAttack = elapsed + attack.CooldownSeconds;
                    }
                }
            }
            if (Phase == DefensePhase.Combat) enemyProjectilePool.Tick(dt, p, beacon.position, arenaHalfSize, this);
            if (PlayerHealth <= 0 || BeaconHealth <= 0) EndRun(PlayerHealth <= 0 ? "death" : "beacon_destroyed");
            else supplies.Tick(dt);
            UpdateHud();
        }

        private bool Fire(Vector2 direction)
        {
            int available = 0;
            foreach (var slot in bullets) if (!slot.root.gameObject.activeSelf) available++;
            int pellets = supplies.PelletCount;
            if (available < pellets) return false;
            int shot = 0;
            int volley = ++volleyId;
            foreach (var bullet in bullets)
            {
                if (bullet.root.gameObject.activeSelf) continue;
                float angle = pellets == 1 ? 0 : (shot - (pellets - 1) * 0.5f) * 4;
                bullet.direction = Quaternion.Euler(0, 0, angle) * direction;
                bullet.damage = supplies.Damage;
                bullet.volley = volley;
                bullet.life = 1.25f;
                bullet.root.position = (Vector2)player.position + bullet.direction * 0.65f;
                bullet.root.rotation = weapon.rotation * Quaternion.Euler(0, 0, angle);
                bullet.root.gameObject.SetActive(true);
                GameSessionTracker.Instance.Shot(player.position);
                if (++shot == pellets) break;
            }
            audioEffects.Play(pellets > 1 ? DefenseCue.Scatter : DefenseCue.Shot);
            feedback.Shot(direction, pellets > 1);
            return true;
        }

        private void BeginWave()
        {
            Phase = DefensePhase.Warning;
            PhaseRemaining = Mathf.Max(0.5f, waveRules.warningSeconds);
            remaining = waveRules.Count(Wave);
            spawned = 0;
            spawnIn = 0;
            entranceCount = Wave >= waveRules.twoEntrancesFromWave ? 2 : 1;
            int edge = (Wave - 1) % 4;
            for (int i = 0; i < entranceCount; i++)
            {
                int side = i == 0 ? edge : edge ^ 1;
                entrances[i] = side < 2 ? new Vector2(side == 0 ? -arenaHalfSize.x : arenaHalfSize.x, 0)
                    : new Vector2(0, side == 2 ? -arenaHalfSize.y : arenaHalfSize.y);
            }
            GameSessionTracker.Instance.WaveStarted(Wave);
            audioEffects.Play(DefenseCue.Warning);
            UpdateIndicators();
        }

        public void StartNextWave()
        {
            if (State != DefenseState.Playing || Phase != DefensePhase.Preparation || !supplies.CardChosen) return;
            supplies.CloseShop();
            Wave++;
            BeginWave();
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            UpdateHud();
        }

        private void AdvanceWave(float dt)
        {
            if (Phase == DefensePhase.Preparation)
            {
                if (supplies.AwaitingCard) return;
                PhaseRemaining = Mathf.Max(0, PhaseRemaining - dt);
                if (PhaseRemaining <= 0) StartNextWave();
                return;
            }
            if (Phase == DefensePhase.Warning)
            {
                PhaseRemaining = Mathf.Max(0, PhaseRemaining - dt);
                if (PhaseRemaining <= 0) Phase = DefensePhase.Combat;
                return;
            }
            if (remaining == 0 && AliveCount == 0)
            {
                Phase = DefensePhase.Preparation;
                PhaseRemaining = Mathf.Max(1, waveRules.preparationSeconds);
                foreach (var bullet in bullets) bullet.root.gameObject.SetActive(false);
                enemyProjectilePool.Reset();
                supplies.OpenShop();
                GameSessionTracker.Instance.Record("wave_cleared", Wave, beacon.position);
                return;
            }
            if (remaining <= 0) return;
            spawnIn -= dt;
            if (spawnIn > 0) return;
            for (int enemyIndex = 0; enemyIndex < enemies.Length; enemyIndex++)
            {
                var enemy = enemies[enemyIndex];
                if (enemy.root.gameObject.activeSelf) continue;
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
                if (!bullet.root.gameObject.activeSelf) continue;
                Vector2 from = bullet.root.position;
                Vector2 to = from + bullet.direction * bulletSpeed * Mathf.Min(dt, Mathf.Max(0, bullet.life));
                EnemySlot hit = null;
                int hitIndex = -1;
                float nearest = float.MaxValue;
                for (int enemyIndex = 0; enemyIndex < enemies.Length; enemyIndex++)
                {
                    var enemy = enemies[enemyIndex];
                    if (!enemy.root.gameObject.activeSelf) continue;
                    // Swept segment test avoids tunnelling through enemies on slower frames.
                    if (DefenseCollision.SegmentCircle(from, to, enemy.root.position, 0.42f, out float t) && t < nearest)
                    { hit = enemy; hitIndex = enemyIndex; nearest = t; }
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
                        hit.root.gameObject.SetActive(false); Kills++; GameSessionTracker.Instance.Kill(hit.root.position);
                        supplies.EnemyKilled(hit.root.position);
                        audioEffects.Play(DefenseCue.Kill);
                    }
                    bullet.root.gameObject.SetActive(false);
                }
                else if (bullet.life <= 0 || Mathf.Abs(to.x) > arenaHalfSize.x + 1 || Mathf.Abs(to.y) > arenaHalfSize.y + 1)
                    bullet.root.gameObject.SetActive(false);
                else bullet.root.position = to;
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
            resultPanel.SetActive(true);
            resultStats.text = (reason == "death" ? "YOU WERE DEFEATED" : "THE BEACON WAS DESTROYED") +
                "\n" + Kills + " KILLS   /   WAVE " + Wave + "   /   " + Mathf.FloorToInt(elapsed) + " SECONDS";
            var snapshot = GameSessionTracker.Instance.Finish(reason, PlayerHealth, BeaconHealth);
            resultUpload.Submit(snapshot, rewards.apiBaseUrl);
            rewards.Submit(new DefenseRunReport { runId = snapshot.runId, mode = snapshot.gameMode, reason = snapshot.endReason,
                claimedKills = snapshot.kills, reachedWave = snapshot.reachedWave, survivalSeconds = snapshot.playedSeconds });
            Select(restartButton);
        }

        public void RetryRequests()
        {
            resultUpload.Retry();
            rewards.Retry();
        }

        public int Heal(int amount)
        {
            if (State != DefenseState.Playing) return 0;
            int restored = Mathf.Clamp(amount, 0, 100 - PlayerHealth);
            PlayerHealth += restored;
            UpdateHud();
            return restored;
        }

        public int RepairBeacon(int amount)
        {
            if (State != DefenseState.Playing || Phase != DefensePhase.Preparation) return 0;
            int restored = Mathf.Clamp(amount, 0, 100 - BeaconHealth);
            BeaconHealth += restored;
            feedback.Pickup(beacon.position);
            UpdateHud();
            return restored;
        }

        public void ApplyEnemyDamage(bool playerTarget, int amount)
        {
            if (State != DefenseState.Playing || amount <= 0 || (playerTarget && elapsed < hurtUntil)) return;
            int damage = Mathf.Min(playerTarget ? PlayerHealth : BeaconHealth, amount);
            if (damage <= 0) return;
            if (playerTarget) { PlayerHealth -= damage; hurtUntil = elapsed + 0.35f; }
            else BeaconHealth -= damage;
            GameSessionTracker.Instance.Damage(!playerTarget, damage, playerTarget ? player.position : beacon.position);
            audioEffects.Play(DefenseCue.Hurt);
            feedback.Hurt(!playerTarget);
        }

        public bool TryFireEnemyProjectile(EnemySlot enemy)
        {
            if (State != DefenseState.Playing || Phase != DefensePhase.Combat || !enemy.root.gameObject.activeSelf) return false;
            Vector2 muzzle = (Vector2)enemy.root.position + enemy.aimDirection * 0.55f;
            if (!enemyProjectilePool.TryFire(muzzle, enemy.aimDirection, 6 + enemy.level)) return false;
            feedback.EnemyShot(muzzle, enemy.aimDirection);
            return true;
        }

        private Vector2 ClampToArena(Vector2 position) => new Vector2(
            Mathf.Clamp(position.x, -arenaHalfSize.x, arenaHalfSize.x),
            Mathf.Clamp(position.y, -arenaHalfSize.y, arenaHalfSize.y));

        public void Pause()
        {
            if (State != DefenseState.Playing) return;
            State = DefenseState.Paused;
            feedback.SuspendCamera();
            audioEffects.Stop();
            supplies.shopPanel.SetActive(false);
            supplies.Refresh();
            pausePanel.SetActive(true);
            helpPanel.SetActive(false);
            crosshair.gameObject.SetActive(false);
            GameSessionTracker.Instance.Record("paused", 0, player.position);
            Select(resumeButton);
        }
        public void Resume()
        {
            if (State != DefenseState.Paused) return;
            State = DefenseState.Playing;
            supplies.shopPanel.SetActive(supplies.ShopOpen);
            supplies.Refresh();
            pausePanel.SetActive(false);
            helpPanel.SetActive(false);
            crosshair.gameObject.SetActive(true);
            GameSessionTracker.Instance.Record("resumed", 0, player.position);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        }
        private void OnApplicationFocus(bool focused) { if (!focused) Pause(); }
        private void LateUpdate()
        {
            if (State == DefenseState.Playing) feedback.Tick(Time.unscaledDeltaTime, elapsed < dashUntil);
            UpdateIndicators();
        }
        private void OnDisable() { feedback?.Reset(); }
        public void PickupFeedback(Vector2 position) { feedback?.Pickup(position); }
        public void HandleEscape()
        {
            if (State == DefenseState.Playing) Pause();
            else if (State == DefenseState.Paused)
            {
                if (helpPanel.activeSelf) CloseHelp();
                else Resume();
            }
        }
        public void OpenHelp()
        {
            if (State != DefenseState.Paused) return;
            pausePanel.SetActive(false);
            helpPanel.SetActive(true);
            Select(helpBackButton);
        }
        public void CloseHelp()
        {
            if (State != DefenseState.Paused) return;
            helpPanel.SetActive(false);
            pausePanel.SetActive(true);
            Select(helpButton);
        }
        private void HidePool()
        {
            foreach (var enemy in enemies) enemy.root.gameObject.SetActive(false);
            foreach (var bullet in bullets) bullet.root.gameObject.SetActive(false);
            enemyProjectilePool.Reset();
        }
        private void UpdateHud()
        {
            healthText.text = "YOU " + PlayerHealth + " / 100     BEACON " + BeaconHealth + " / 100";
            waveText.text = "WAVE " + Wave + "   적 " + AliveCount + " / 대기 " + remaining;
            scoreText.text = Mathf.FloorToInt(elapsed) + "s   /   THREAT " + Mathf.Max(1, Threat);
            hintText.text = State == DefenseState.Ready ? "" : Phase == DefensePhase.Preparation
                ? supplies.AwaitingCard ? "강화 카드 1장을 선택하세요" : "다음 웨이브까지 " + Mathf.CeilToInt(PhaseRemaining) + "초"
                : Phase == DefensePhase.Warning ? "진입 예고 · " + Mathf.CeilToInt(PhaseRemaining) + "초" : "";
            if (beaconHealthBar != null) { beaconHealthBar.fillAmount = BeaconHealth / 100f; beaconHealthBar.color = BeaconHealth <= 30 ? new Color(1, 0.3f, 0.25f) : new Color(0.35f, 0.9f, 0.75f); }
            if (dashBar != null) dashBar.fillAmount = DashReady;
            if (dashText != null) dashText.text = DashReady >= 1 ? "DASH READY" : "DASH " + Mathf.Max(0, nextDash - elapsed).ToString("0.0") + "s";
            if (nextWaveButton != null) nextWaveButton.gameObject.SetActive(State == DefenseState.Playing && Phase == DefensePhase.Preparation);
        }

        private void UpdateIndicators()
        {
            bool playing = State == DefenseState.Playing;
            for (int i = 0; i < entryMarkers.Length; i++)
            {
                var marker = entryMarkers[i];
                if (marker == null) continue;
                bool visible = playing && Phase != DefensePhase.Preparation && remaining > 0 && i < entranceCount;
                marker.gameObject.SetActive(visible);
                if (!visible) continue;
                marker.transform.position = entrances[i] * 0.97f;
                marker.color = new Color(1, 0.35f, 0.12f, Phase == DefensePhase.Warning ? 0.65f + 0.3f * Mathf.Sin(elapsed * 14) : 0.45f);
            }
            foreach (var enemy in enemies)
            {
                bool active = enemy.root.gameObject.activeSelf;
                var behavior = DefenseEnemyFactory.For(enemy.role);
                if (enemy.roleMarker != null)
                {
                    enemy.roleMarker.enabled = active;
                    enemy.roleMarker.color = behavior.MarkerColor;
                    enemy.roleMarker.transform.localRotation = Quaternion.Euler(0, 0, behavior.MarkerAngle);
                    enemy.roleMarker.transform.localScale = behavior.MarkerScale;
                }
                if (enemy.attackMarker != null)
                {
                    enemy.attackMarker.enabled = active && enemy.windup > 0;
                    enemy.attackMarker.color = new Color(1, 0.15f, 0.1f);
                    enemy.attackMarker.transform.localScale = new Vector3(8 * Mathf.Clamp01(enemy.windup / behavior.Attack.WindupSeconds), 1, 1);
                }
                if (enemy.aimMarker != null)
                {
                    enemy.aimMarker.enabled = active && enemy.windup > 0 && behavior.Attack.IsRanged;
                    enemy.aimMarker.color = new Color(1, 0.2f, 0.65f, 0.4f);
                    float length = behavior.Attack.Range;
                    enemy.aimMarker.transform.localPosition = enemy.aimDirection * length * 0.5f;
                    enemy.aimMarker.transform.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(enemy.aimDirection.y, enemy.aimDirection.x) * Mathf.Rad2Deg);
                    enemy.aimMarker.transform.localScale = new Vector3(length / enemy.aimMarker.sprite.bounds.size.x, 0.045f / enemy.aimMarker.sprite.bounds.size.y, 1);
                }
                if (enemy.healthBar != null)
                {
                    enemy.healthBar.enabled = active && (enemy.role != DefenseEnemyRole.Hunter || elapsed < enemy.showHealthUntil);
                    enemy.healthBar.color = new Color(0.4f, 1, 0.5f);
                    enemy.healthBar.transform.localScale = new Vector3(8 * Mathf.Clamp01((float)enemy.health / Mathf.Max(1, enemy.maxHealth)), 0.7f, 1);
                }
            }
            if (nextWaveButton != null) nextWaveButton.gameObject.SetActive(playing && Phase == DefensePhase.Preparation);
        }
        private static void Select(Button button) { if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(button.gameObject); }
    }
}
