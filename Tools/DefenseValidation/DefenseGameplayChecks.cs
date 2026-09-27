using Zpd.Networking;
using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Zpd.Gameplay;
using Zpd.Lobby;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Zpd.Defense.Editor
{
    [InitializeOnLoad]
    public static class DefenseGameplayChecks
    {
        private const string Pending = "Zpd.DefenseValidation.Pending";
        private static string ResultPath => Path.Combine(Directory.GetParent(Application.dataPath).FullName, "validation-result.txt");
        private static int assertions;
        static DefenseGameplayChecks() { EditorApplication.playModeStateChanged += OnState; }
        public static void Run()
        {
            try
            {
                PlayerSettings.companyName = "ZpdVerification";
                PlayerSettings.productName = "SoloDefenseValidation";
                EditorSceneManager.OpenScene("Assets/Scenes/SoloDefense.unity");
                SessionState.SetBool(Pending, true);
                EditorApplication.EnterPlaymode();
            }
            catch (Exception error) { Fail(error); }
        }
        private static void OnState(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Pending, false)) return;
            SessionState.SetBool(Pending, false); Check();
        }
        private static void Check()
        {
            try
            {
                var game = UnityEngine.Object.FindFirstObjectByType<DefenseGame>();
                Require(game != null, "Defense controller exists"); game.enabled = false;
                Require(game.bullets.Length >= 128 && game.enemyBullets.Length == 48, "Separate authored projectile pools");
                Require(game.supplies.cardButtons.Length == 3 && game.supplies.cardLabels.Length == 3, "Three card choices bound");
                foreach (var enemy in game.enemies) Require(enemy.aimMarker != null && enemy.attackMarker != null, "Enemy aim/attack indicators bound");
                game.StartRun();
                Require(game.Phase == DefensePhase.Warning && game.AliveCount == 0, "Warning precedes spawning");
                Advance(game, 1);
                Require(game.AliveCount == 0, "No premature spawns");
                float left = game.PhaseRemaining;
                game.Pause(); Advance(game, 2);
                Require(game.PhaseRemaining == left, "Pause freezes warning");
                game.Resume(); Advance(game, 0.7f);
                Require(game.AliveCount > 0, "Spawn follows warning");
                var enemySlot = Array.Find(game.enemies, x => x.root.gameObject.activeSelf);
                int health = enemySlot.health, threat = game.Threat;
                Set(game, "elapsed", 180f); game.Simulate(0, Vector2.zero, Vector2.right, false, false);
                Require(enemySlot.health == health && game.Threat == threat, "Existing enemy stats do not grow with time");

                ClearWave(game);
                Require(game.supplies.AwaitingCard, "Wave clear grants a free card choice");
                left = game.PhaseRemaining; int wave = game.Wave;
                Advance(game, 20); game.StartNextWave();
                Require(game.PhaseRemaining == left && game.Wave == wave && game.Phase == DefensePhase.Preparation, "Card choice cannot expire or be skipped");
                Require(!game.supplies.ChooseCard(-1) && !game.supplies.ChooseCard(3), "Invalid card indices rejected");
                game.Pause(); Require(!game.supplies.ChooseCard(0), "Paused card selection rejected"); game.Resume();
                Require(game.supplies.ChooseCard(1), "Damage card selectable without gold");
                Require(game.supplies.Damage == 2 && game.supplies.Gold == 0, "Damage card changes stats for free");
                Require(game.supplies.cardLabels[1].text.Contains("1 → 2") && game.supplies.cardLabels[1].text.Contains("SELECTED"), "Selected card keeps its original preview and confirmation");
                Require(!game.supplies.ChooseCard(0) && game.supplies.PelletCount == 1, "One card per wave");
                game.supplies.OpenShop(); Require(!game.supplies.ChooseCard(0), "Repeated open cannot grant extra card");
                var drop = game.supplies.drops[0]; drop.amount = 72; drop.root.position = game.player.position; drop.root.gameObject.SetActive(true);
                game.Simulate(0, Vector2.zero, Vector2.right, false, false);
                Require(game.supplies.Gold == 72, "Gold still collected for supplies");
                game.supplies.BuyRepair(); Require(game.supplies.Gold == 72, "Full beacon blocks spending");
                SetProperty(game, "BeaconHealth", 85); game.supplies.BuyRepair();
                Require(game.BeaconHealth == 100 && game.supplies.Gold == 47, "Repair restores health and charges gold");
                SetProperty(game, "BeaconHealth", 85); game.supplies.BuyRepair();
                Require(game.BeaconHealth == 85 && game.supplies.Gold == 47, "Repair limited to once per preparation");
                game.StartNextWave(); game.StartNextWave();
                Require(game.Wave == wave + 1 && !game.supplies.ShopOpen, "Manual start advances once");
                Require(!game.supplies.ChooseCard(2), "Combat card selection rejected");
                ClearWave(game); Require(game.supplies.ChooseCard(0), "Next wave offers another card");
                Require(game.supplies.Damage == 2 && game.supplies.PelletCount == 2, "Different card upgrades accumulate");
                ClearWave(game); Require(game.supplies.ChooseCard(2), "Fire rate card selectable");
                Require(game.supplies.FireInterval < 0.24f && game.supplies.Damage == 2, "Fire rate stacks independently");
                wave = game.Wave; Advance(game, 8.1f);
                Require(game.Wave == wave + 1 && game.Phase == DefensePhase.Warning, "Countdown resumes after selecting card");

                CombatFixture(game); game.player.position = new Vector2(0, -2);
                enemySlot = game.enemies[0]; SpawnFixture(enemySlot, DefenseEnemyRole.Siege, new Vector2(1.4f, -2));
                enemySlot.windup = 0.5f; Set(game, "fireAt", 0f);
                game.Simulate(0.05f, Vector2.zero, new Vector2(5, -2), true, false);
                Require(enemySlot.health == 96, "Card stats create two projectiles with two damage each");
                Require(enemySlot.windup == 0 && enemySlot.root.position.x <= 1.61f, "Volley interrupts once without stacking knockback");
                Vector3 stagger = enemySlot.root.position; game.Pause(); Advance(game, 1);
                Require(enemySlot.root.position == stagger, "Pause freezes stagger"); game.Resume();

                CombatFixture(game); SpawnFixture(enemySlot, DefenseEnemyRole.Siege, new Vector2(0.9f, -2));
                var expired = game.bullets[0]; expired.root.position = new Vector2(0, -2); expired.direction = Vector2.right;
                expired.life = 0.001f; expired.damage = 1; expired.root.gameObject.SetActive(true);
                game.Simulate(0.05f, Vector2.zero, Vector2.right, false, false);
                Require(enemySlot.health == 100 && !expired.root.gameObject.activeSelf, "Lifetime clips swept projectile range");
                CombatFixture(game); SpawnFixture(enemySlot, DefenseEnemyRole.Siege, new Vector2(0.8f, 0)); game.player.position = new Vector2(2, 0);
                health = game.BeaconHealth; Advance(game, 0.5f);
                Require(!enemySlot.targetingPlayer && game.BeaconHealth == health, "Siege keeps beacon target and telegraphs");
                Advance(game, 0.15f); Require(game.BeaconHealth < health, "Melee strategy executes after windup");
                enemySlot.role = DefenseEnemyRole.Hunter; Advance(game, 0.05f); Require(enemySlot.targetingPlayer, "Hunter targeting strategy differs");

                CombatFixture(game); game.player.position = new Vector2(-3, 1);
                SpawnFixture(enemySlot, DefenseEnemyRole.Shooter, new Vector2(3, 1));
                Advance(game, 0.5f); Require(ActiveEnemyBolts(game) == 0 && enemySlot.windup > 0, "Shooter telegraphs before emitting");
                Vector2 locked = enemySlot.aimDirection; game.player.position = new Vector2(-3, 2);
                Advance(game, 0.5f);
                Require(ActiveEnemyBolts(game) == 1, "Projectile strategy emits a bolt");
                var bolt = Array.Find(game.enemyBullets, x => x.root.gameObject.activeSelf);
                Require(Vector2.Distance(bolt.direction, locked) < 0.0001f && Mathf.Abs(bolt.direction.y) < 0.001f, "Aim locks during windup, no homing");
                Vector3 boltPosition = bolt.root.position; float boltLife = bolt.life;
                game.Pause(); Advance(game, 1);
                Require(!game.TryFireEnemyProjectile(enemySlot), "Paused enemy cannot launch projectile");
                Require(bolt.root.position == boltPosition && bolt.life == boltLife, "Pause freezes hostile projectile and lifetime"); game.Resume();
                health = game.PlayerHealth; Advance(game, 1.2f);
                Require(game.PlayerHealth == health, "Moving off locked aim avoids damage");

                CombatFixture(game); game.player.position = new Vector2(-3, -2);
                SpawnFixture(enemySlot, DefenseEnemyRole.Shooter, new Vector2(-1.8f, -2));
                enemySlot.aimDirection = Vector2.left; enemySlot.nextAttack = 10000;
                Require(game.TryFireEnemyProjectile(enemySlot), "Enemy projectile launch succeeds");
                enemySlot.level = 50; health = game.PlayerHealth;
                Advance(game, 0.15f);
                Require(game.PlayerHealth == health - 7, "Enemy projectile damage is snapshotted and hits player");

                CombatFixture(game); health = game.PlayerHealth;
                bolt = game.enemyBullets[0]; bolt.root.position = (Vector2)game.player.position + Vector2.right * 0.1f;
                bolt.direction = Vector2.right; bolt.damage = 20; bolt.life = 1; bolt.root.gameObject.SetActive(true);
                game.Simulate(0.05f, Vector2.right, new Vector2(5, -2), false, true);
                Require(game.PlayerHealth == health && !bolt.root.gameObject.activeSelf, "Dash invulnerability blocks and consumes projectile");

                var recorder = new CombatRecorder();
                var pool = new DefenseEnemyProjectilePool(game.enemyBullets);
                Require(pool.TryFire(new Vector2(-1.5f, 0), Vector2.right, 9), "Pool acquires authored slot");
                pool.Tick(0.5f, new Vector2(0.6f, 0), Vector2.zero, game.arenaHalfSize, recorder);
                Require(recorder.Hits == 1 && !recorder.PlayerTarget && recorder.Damage == 9, "Swept collision hits nearer beacon before player");
                pool.Reset();
                for (int i = 0; i < game.enemyBullets.Length; i++) Require(pool.TryFire(new Vector2(8, 4), Vector2.left, 1), "Pool capacity usable");
                Require(!pool.TryFire(Vector2.zero, Vector2.right, 1), "Pool exhaustion is bounded");
                game.enemyBullets[0].root.gameObject.SetActive(false);
                Require(pool.TryFire(new Vector2(8, 4), Vector2.left, 1), "Pool reuses released slot");
                ClearWave(game); Require(ActiveEnemyBolts(game) == 0, "Wave clear removes hostile projectiles before cards");

                for (int i = 0; i < 100; i++) for (int card = 0; card < 3; card++) DefenseUpgradeCatalog.At(card).Apply(game.supplies.Stats);
                Require(game.supplies.PelletCount == 7 && game.supplies.Damage == 50 && game.supplies.Stats.FireRateLevel == 13, "Upgrade caps enforced");
                Require(game.bullets.Length >= Mathf.CeilToInt(1.25f / game.supplies.FireInterval + 1) * game.supplies.PelletCount, "Player pool supports maximum build without dropping volleys");
                ClearWave(game); Require(game.supplies.CardChosen && !game.supplies.AwaitingCard, "Fully upgraded build cannot softlock choice");
                CombatFixture(game); game.player.position = Vector2.zero; Set(game, "fireAt", 0f);
                game.Simulate(0.01f, Vector2.zero, Vector2.right, true, false);
                int activeShots = 0; foreach (var shot in game.bullets) if (shot.root.gameObject.activeSelf) { activeShots++; Require(shot.damage == 50, "Maximum damage snapshot"); }
                Require(activeShots == 7, "Maximum volley emitted completely");

                var feedback = Get<DefenseFeedback>(game, "feedback"); Vector3 cameraRest = game.worldCamera.transform.position;
                feedback.Shot(Vector2.right, true); for (int i = 0; i < 30; i++) feedback.Tick(0.016f, false); feedback.SuspendCamera();
                Require(Vector3.Distance(cameraRest, game.worldCamera.transform.position) < 0.0001f, "Camera shake restores base position");
                if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null)
                {
                    game.supplies.Stats.Reset(); DefenseUpgradeCatalog.Damage.Apply(game.supplies.Stats); DefenseUpgradeCatalog.Projectiles.Apply(game.supplies.Stats);
                    ClearWave(game); Capture(game, "cards.png");
                    CombatFixture(game); game.player.position = new Vector2(-3, -1);
                    SpawnFixture(game.enemies[0], DefenseEnemyRole.Hunter, new Vector2(-1, -2));
                    SpawnFixture(game.enemies[1], DefenseEnemyRole.Siege, new Vector2(1.5f, 0));
                    SpawnFixture(game.enemies[2], DefenseEnemyRole.Shooter, new Vector2(2, 2));
                    game.enemies[2].targetingPlayer = true;
                    game.enemies[2].windup = 0.6f; game.enemies[2].aimDirection = ((Vector2)game.player.position - (Vector2)game.enemies[2].root.position).normalized;
                    game.TryFireEnemyProjectile(game.enemies[2]);
                    Array.Find(game.enemyBullets, x => x.root.gameObject.activeSelf).root.position += (Vector3)game.enemies[2].aimDirection * 2;
                    Capture(game, "combat.png");
                    Require(game.enemies[2].aimMarker.enabled, "Ranged aim warning is visible during windup");
                }
                string previous = game.RunId; game.StartRun(); GameSessionTracker.MarkStored(previous);
                Require(game.supplies.PelletCount == 1 && game.supplies.Damage == 1 && Mathf.Approximately(game.supplies.FireInterval, 0.24f), "Restart resets all card stats");
                Require(!game.supplies.ShopOpen && game.supplies.Gold == 0 && ActiveEnemyBolts(game) == 0, "Restart resets supplies and hostile projectiles");
                foreach (var particle in game.feedbackParticles) Require(!particle.gameObject.activeSelf, "Restart clears feedback");
                var snapshot = GameSessionTracker.Instance.Finish("verification", game.PlayerHealth, game.BeaconHealth); GameSessionTracker.MarkStored(snapshot.runId);
                BeginNavigationChecks();
            }
            catch (Exception error) { Fail(error); }
        }
        private sealed class CombatRecorder : IDefenseEnemyCombat
        {
            public int Hits, Damage; public bool PlayerTarget;
            public void ApplyEnemyDamage(bool playerTarget, int damage) { Hits++; PlayerTarget = playerTarget; Damage = damage; }
            public bool TryFireEnemyProjectile(DefenseGame.EnemySlot enemy) => false;
        }
        private static void Capture(DefenseGame game, string name)
        {
            game.Simulate(0, Vector2.zero, Vector2.right, false, false);
            typeof(DefenseGame).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(game, null);
            var canvas = game.healthText.canvas;
            int previousOrder = canvas.sortingOrder;
            var camera = game.worldCamera;
            var texture = new RenderTexture(1280, 720, 24);
            texture.Create();
            camera.targetTexture = texture;
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1; canvas.sortingOrder = 100;
            Canvas.ForceUpdateCanvases();
            RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = texture });
            var previous = RenderTexture.active; RenderTexture.active = texture;
            var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); image.Apply();
            File.WriteAllBytes(Path.Combine(Directory.GetParent(Application.dataPath).FullName, name), image.EncodeToPNG());
            RenderTexture.active = previous; camera.targetTexture = null; canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = previousOrder;
            UnityEngine.Object.DestroyImmediate(image); UnityEngine.Object.DestroyImmediate(texture);
        }
        private static int ActiveEnemyBolts(DefenseGame game) { int count = 0; foreach (var bolt in game.enemyBullets) if (bolt.root.gameObject.activeSelf) count++; return count; }
        private static void CombatFixture(DefenseGame game)
        {
            game.supplies.CloseShop();
            foreach (var enemy in game.enemies) enemy.root.gameObject.SetActive(false);
            foreach (var bullet in game.bullets) bullet.root.gameObject.SetActive(false);
            foreach (var bullet in game.enemyBullets) bullet.root.gameObject.SetActive(false);
            SetProperty(game, "Phase", DefensePhase.Combat); Set(game, "remaining", 1); Set(game, "spawnIn", 10000f);
        }
        private static void SpawnFixture(DefenseGame.EnemySlot enemy, DefenseEnemyRole role, Vector2 position)
        {
            enemy.root.gameObject.SetActive(true); enemy.root.position = position; enemy.role = role;
            enemy.health = enemy.maxHealth = 100; enemy.level = 1; enemy.targetingPlayer = false;
            enemy.nextAttack = enemy.stunnedUntil = enemy.windup = 0; enemy.lastVolley = -1;
        }
        private static void ClearWave(DefenseGame game)
        {
            CombatFixture(game); Set(game, "remaining", 0); game.Simulate(0.05f, Vector2.zero, Vector2.right, false, false);
        }
        private static void Advance(DefenseGame game, float seconds)
        { for (int i = 0; i < Mathf.CeilToInt(seconds / 0.05f); i++) game.Simulate(0.05f, Vector2.zero, Vector2.right, false, false); }
        private static void Set(object target, string field, object value)
        {
            var flags = BindingFlags.NonPublic | BindingFlags.Instance;
            var member = target.GetType().GetField(field, flags);
            if (member != null) member.SetValue(target, value);
            else target.GetType().GetProperty(field, flags).SetValue(target, value);
        }
        private static int navigationStep;
        private static double navigationDeadline;
        private static string navigationRun;
        private static void BeginNavigationChecks()
        {
            var model = new DefenseModel();
            model.Start("model-only");
            Require(model.ApplyDamage(true, 30) == 30 && model.PlayerHealth == 70, "Model applies bounded damage without a scene");
            Require(model.ApplyDamage(true, 30) == 0, "Model enforces invulnerability");
            Require(model.Pause() && model.Heal(20) == 0 && !model.TryDash(true), "Model blocks actions while paused");
            Require(model.Resume() && model.Heal(100) == 30, "Model caps restored health");
            var lobby = new LegacyLobbyModel();
            lobby.Toggle(LobbySection.Friends); lobby.SelectSocialPage(2); lobby.SelectSocialPage(9);
            Require(lobby.Section == LobbySection.Friends && lobby.SocialPage == 2, "Lobby model validates navigation");
            lobby.Toggle(LobbySection.Friends); Require(lobby.Section == LobbySection.None, "Same panel toggles closed");
            lobby.SetSearch("  alice\nbob  "); Require(lobby.SearchQuery == "alice bob", "Search normalization belongs to model");
            Require(SceneNavigation.CanLoad(SceneNavigation.Lobby) && SceneNavigation.CanLoad(SceneNavigation.SoloDefense), "Both navigation scenes are enabled");
            navigationStep = 0;
            navigationDeadline = EditorApplication.timeSinceStartup + 60;
            EditorApplication.update += CheckNavigation;
            SceneNavigation.Load(SceneNavigation.Lobby);
        }
        private static void ClickReturn(DefenseGame game, GameObject overlay)
        {
            var button = overlay.transform.Find("Panel/Return to Lobby").GetComponent<Button>();
            Require(button.onClick.GetPersistentMethodName(0) == nameof(DefenseGame.ReturnToLobby), "Authored return button targets controller");
            button.onClick.Invoke();
        }
        private static void CheckNavigation()
        {
            try
            {
                if (EditorApplication.timeSinceStartup > navigationDeadline) throw new Exception("Scene navigation timed out at step " + navigationStep);
                string scene = SceneManager.GetActiveScene().path;
                if (navigationStep % 2 == 0)
                {
                    if (scene != SceneNavigation.Lobby) return;
                    var lobby = UnityEngine.Object.FindFirstObjectByType<LobbyController>();
                    Require(lobby != null && lobby.View != null, "Lobby controller and migrated view survive scene load");
                    Require(!GameSessionTracker.Instance.IsRunning, "Lobby has no active defense run");
                    if (navigationStep == 4)
                    {
                        Require(GameSessionTracker.Instance.CompletedJson.Contains("returned_to_lobby"), "Mid-run exit persists an explicit end reason");
                        GameSessionTracker.MarkStored(navigationRun);
                    }
                    if (navigationStep == 6)
                    {
                        Require(UnityEngine.Object.FindObjectsByType<GameSessionTracker>(FindObjectsSortMode.None).Length == 1, "Repeated navigation keeps one tracker");
                        GameSessionTracker.MarkStored(navigationRun);
                        EditorApplication.update -= CheckNavigation;
                        string result = "PASS: " + assertions + " assertions; MVC models, combat regression and Lobby/SoloDefense round trips (ready, paused, ended).";
                        File.WriteAllText(ResultPath, result); Debug.Log(result); EditorApplication.Exit(0); return;
                    }
                    lobby.OpenFriends(); Require(lobby.social.Model.Section == LobbySection.Friends, "Lobby input updates social model");
                    lobby.social.ClosePanel(); Require(!lobby.social.backdrop.activeSelf, "Lobby view reflects closed social panel");
                    navigationStep++;
                    lobby.View.soloButton.onClick.Invoke();
                }
                else
                {
                    if (scene != SceneNavigation.SoloDefense) return;
                    var game = UnityEngine.Object.FindFirstObjectByType<DefenseGame>();
                    Require(game != null && game.State == DefenseState.Ready && game.View.readyPanel.activeSelf, "Defense opens in ready state with migrated view");
                    if (navigationStep == 1) { navigationStep++; ClickReturn(game, game.readyPanel); }
                    else if (navigationStep == 3)
                    {
                        game.StartRun(); navigationRun = game.RunId; game.Pause();
                        Require(game.State == DefenseState.Paused, "Can pause after entering from lobby");
                        navigationStep++; ClickReturn(game, game.pausePanel);
                    }
                    else
                    {
                        game.StartRun(); navigationRun = game.RunId;
                        AuthManager.Instance.Logout(); // Unauthenticated uploads must fail locally.
                        game.ApplyEnemyDamage(false, 100); game.Simulate(0, Vector2.zero, Vector2.right, false, false);
                        Require(game.State == DefenseState.Ended && game.resultPanel.activeSelf, "Defeat renders results");
                        Require(game.View.uploadStatus.text.Contains("FAILED") && game.View.rewardTitle.text.Contains("FAILED"), "Service failures render through View");
                        Require(game.View.retryButton.interactable, "Failed requests enable retry through View");
                        game.RetryRequests();
                        Require(game.View.retryButton.interactable && !game.rewards.Succeeded && !game.resultUpload.Succeeded, "Retry preserves failure without fabricating success");
                        navigationStep++; ClickReturn(game, game.resultPanel);
                    }
                }
            }
            catch (Exception error) { EditorApplication.update -= CheckNavigation; Fail(error); }
        }
        private static T Get<T>(object target, string field) => (T)target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(target);
        private static void SetProperty(object target, string property, object value) => target.GetType().GetProperty(property).SetValue(target, value);
        private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); assertions++; }
        private static void Fail(Exception error) { File.WriteAllText(ResultPath, "FAIL: " + error); Debug.LogException(error); EditorApplication.Exit(1); }
    }
}
