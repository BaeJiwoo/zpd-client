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
                Require(game.player_bullet_slots.Length >= 128 && game.enemy_projectile_slots.Length == 48, "Separate authored projectile pools");
                Require(game.defense_supplies.btn_upgrade_cards.Length == 3 && game.defense_supplies.txt_upgrade_cards.Length == 3, "Three card choices bound");
                foreach (var enemy in game.enemy_slots) Require(enemy.sprite_renderer_aim_marker != null && enemy.sprite_renderer_attack_marker != null, "Enemy aim/attack indicators bound");
                game.StartRun();
                Require(game.Phase == DefensePhase.Warning && game.AliveCount == 0, "Warning precedes spawning");
                Advance(game, 1);
                Require(game.AliveCount == 0, "No premature spawns");
                float left = game.PhaseRemaining;
                game.Pause(); Advance(game, 2);
                Require(game.PhaseRemaining == left, "Pause freezes warning");
                game.Resume(); Advance(game, 0.7f);
                Require(game.AliveCount > 0, "Spawn follows warning");
                var enemySlot = Array.Find(game.enemy_slots, x => x.transform_root.gameObject.activeSelf);
                int health = enemySlot.health, threat = game.Threat;
                Set(game, "elapsed_seconds", 180f); game.Simulate(0, Vector2.zero, Vector2.right, false, false);
                Require(enemySlot.health == health && game.Threat == threat, "Existing enemy stats do not grow with time");

                ClearWave(game);
                Require(game.defense_supplies.AwaitingCard, "Wave clear grants a free card choice");
                left = game.PhaseRemaining; int wave = game.Wave;
                Advance(game, 20); game.StartNextWave();
                Require(game.PhaseRemaining == left && game.Wave == wave && game.Phase == DefensePhase.Preparation, "Card choice cannot expire or be skipped");
                Require(!game.defense_supplies.ChooseCard(-1) && !game.defense_supplies.ChooseCard(3), "Invalid card indices rejected");
                game.Pause(); Require(!game.defense_supplies.ChooseCard(0), "Paused card selection rejected"); game.Resume();
                Require(game.defense_supplies.ChooseCard(1), "Damage card selectable without gold");
                Require(game.defense_supplies.Damage == 2 && game.defense_supplies.Gold == 0, "Damage card changes stats for free");
                Require(game.defense_supplies.txt_upgrade_cards[1].text.Contains("1 → 2") && game.defense_supplies.txt_upgrade_cards[1].text.Contains("SELECTED"), "Selected card keeps its original preview and confirmation");
                Require(!game.defense_supplies.ChooseCard(0) && game.defense_supplies.PelletCount == 1, "One card per wave");
                game.defense_supplies.OpenShop(); Require(!game.defense_supplies.ChooseCard(0), "Repeated open cannot grant extra card");
                var drop = game.defense_supplies.gold_drop_slots[0]; drop.gold_amount = 72; drop.transform_root.position = game.transform_player.position; drop.transform_root.gameObject.SetActive(true);
                game.Simulate(0, Vector2.zero, Vector2.right, false, false);
                Require(game.defense_supplies.Gold == 72, "Gold still collected for supplies");
                game.defense_supplies.BuyRepair(); Require(game.defense_supplies.Gold == 72, "Full beacon blocks spending");
                SetProperty(game, "BeaconHealth", 85); game.defense_supplies.BuyRepair();
                Require(game.BeaconHealth == 100 && game.defense_supplies.Gold == 47, "Repair restores health and charges gold");
                SetProperty(game, "BeaconHealth", 85); game.defense_supplies.BuyRepair();
                Require(game.BeaconHealth == 85 && game.defense_supplies.Gold == 47, "Repair limited to once per preparation");
                game.StartNextWave(); game.StartNextWave();
                Require(game.Wave == wave + 1 && !game.defense_supplies.ShopOpen, "Manual start advances once");
                Require(!game.defense_supplies.ChooseCard(2), "Combat card selection rejected");
                ClearWave(game); Require(game.defense_supplies.ChooseCard(0), "Next wave offers another card");
                Require(game.defense_supplies.Damage == 2 && game.defense_supplies.PelletCount == 2, "Different card upgrades accumulate");
                ClearWave(game); Require(game.defense_supplies.ChooseCard(2), "Fire rate card selectable");
                Require(game.defense_supplies.FireInterval < 0.24f && game.defense_supplies.Damage == 2, "Fire rate stacks independently");
                wave = game.Wave; Advance(game, 8.1f);
                Require(game.Wave == wave + 1 && game.Phase == DefensePhase.Warning, "Countdown resumes after selecting card");

                CombatFixture(game); game.transform_player.position = new Vector2(0, -2);
                enemySlot = game.enemy_slots[0]; SpawnFixture(enemySlot, DefenseEnemyRole.Siege, new Vector2(1.4f, -2));
                enemySlot.attack_windup_elapsed_seconds = 0.5f; Set(game, "next_fire_at_seconds", 0f);
                game.Simulate(0.05f, Vector2.zero, new Vector2(5, -2), true, false);
                Require(enemySlot.health == 96, "Card stats create two projectiles with two damage each");
                Require(enemySlot.attack_windup_elapsed_seconds == 0 && enemySlot.transform_root.position.x <= 1.61f, "Volley interrupts once without stacking knockback");
                Vector3 stagger = enemySlot.transform_root.position; game.Pause(); Advance(game, 1);
                Require(enemySlot.transform_root.position == stagger, "Pause freezes stagger"); game.Resume();

                CombatFixture(game); SpawnFixture(enemySlot, DefenseEnemyRole.Siege, new Vector2(0.9f, -2));
                var expired = game.player_bullet_slots[0]; expired.transform_root.position = new Vector2(0, -2); expired.travel_direction = Vector2.right;
                expired.remaining_lifetime_seconds = 0.001f; expired.damage = 1; expired.transform_root.gameObject.SetActive(true);
                game.Simulate(0.05f, Vector2.zero, Vector2.right, false, false);
                Require(enemySlot.health == 100 && !expired.transform_root.gameObject.activeSelf, "Lifetime clips swept projectile range");
                CombatFixture(game); SpawnFixture(enemySlot, DefenseEnemyRole.Siege, new Vector2(0.8f, 0)); game.transform_player.position = new Vector2(2, 0);
                health = game.BeaconHealth; Advance(game, 0.5f);
                Require(!enemySlot.is_targeting_player && game.BeaconHealth == health, "Siege keeps beacon target and telegraphs");
                Advance(game, 0.15f); Require(game.BeaconHealth < health, "Melee strategy executes after windup");
                enemySlot.role = DefenseEnemyRole.Hunter; Advance(game, 0.05f); Require(enemySlot.is_targeting_player, "Hunter targeting strategy differs");

                CombatFixture(game); game.transform_player.position = new Vector2(-3, 1);
                SpawnFixture(enemySlot, DefenseEnemyRole.Shooter, new Vector2(3, 1));
                Advance(game, 0.5f); Require(ActiveEnemyBolts(game) == 0 && enemySlot.attack_windup_elapsed_seconds > 0, "Shooter telegraphs before emitting");
                Vector2 locked = enemySlot.aim_direction; game.transform_player.position = new Vector2(-3, 2);
                Advance(game, 0.5f);
                Require(ActiveEnemyBolts(game) == 1, "Projectile strategy emits a bolt");
                var bolt = Array.Find(game.enemy_projectile_slots, x => x.transform_root.gameObject.activeSelf);
                Require(Vector2.Distance(bolt.travel_direction, locked) < 0.0001f && Mathf.Abs(bolt.travel_direction.y) < 0.001f, "Aim locks during windup, no homing");
                Vector3 boltPosition = bolt.transform_root.position; float boltLife = bolt.remaining_lifetime_seconds;
                game.Pause(); Advance(game, 1);
                Require(!game.TryFireEnemyProjectile(enemySlot), "Paused enemy cannot launch projectile");
                Require(bolt.transform_root.position == boltPosition && bolt.remaining_lifetime_seconds == boltLife, "Pause freezes hostile projectile and lifetime"); game.Resume();
                health = game.PlayerHealth; Advance(game, 1.2f);
                Require(game.PlayerHealth == health, "Moving off locked aim avoids damage");

                CombatFixture(game); game.transform_player.position = new Vector2(-3, -2);
                SpawnFixture(enemySlot, DefenseEnemyRole.Shooter, new Vector2(-1.8f, -2));
                enemySlot.aim_direction = Vector2.left; enemySlot.next_attack_at_seconds = 10000;
                Require(game.TryFireEnemyProjectile(enemySlot), "Enemy projectile launch succeeds");
                enemySlot.threat_level = 50; health = game.PlayerHealth;
                Advance(game, 0.15f);
                Require(game.PlayerHealth == health - 7, "Enemy projectile damage is snapshotted and hits player");

                CombatFixture(game); health = game.PlayerHealth;
                bolt = game.enemy_projectile_slots[0]; bolt.transform_root.position = (Vector2)game.transform_player.position + Vector2.right * 0.1f;
                bolt.travel_direction = Vector2.right; bolt.damage = 20; bolt.remaining_lifetime_seconds = 1; bolt.transform_root.gameObject.SetActive(true);
                game.Simulate(0.05f, Vector2.right, new Vector2(5, -2), false, true);
                Require(game.PlayerHealth == health && !bolt.transform_root.gameObject.activeSelf, "Dash invulnerability blocks and consumes projectile");

                var recorder = new CombatRecorder();
                var pool = new DefenseEnemyProjectilePool(game.enemy_projectile_slots);
                Require(pool.TryFire(new Vector2(-1.5f, 0), Vector2.right, 9), "Pool acquires authored slot");
                pool.Tick(0.5f, new Vector2(0.6f, 0), Vector2.zero, game.arena_half_size, recorder);
                Require(recorder.Hits == 1 && !recorder.PlayerTarget && recorder.Damage == 9, "Swept collision hits nearer beacon before player");
                pool.Reset();
                for (int i = 0; i < game.enemy_projectile_slots.Length; i++) Require(pool.TryFire(new Vector2(8, 4), Vector2.left, 1), "Pool capacity usable");
                Require(!pool.TryFire(Vector2.zero, Vector2.right, 1), "Pool exhaustion is bounded");
                game.enemy_projectile_slots[0].transform_root.gameObject.SetActive(false);
                Require(pool.TryFire(new Vector2(8, 4), Vector2.left, 1), "Pool reuses released slot");
                ClearWave(game); Require(ActiveEnemyBolts(game) == 0, "Wave clear removes hostile projectiles before cards");

                for (int i = 0; i < 100; i++) for (int card = 0; card < 3; card++) DefenseUpgradeCatalog.At(card).Apply(game.defense_supplies.Stats);
                Require(game.defense_supplies.PelletCount == 7 && game.defense_supplies.Damage == 50 && game.defense_supplies.Stats.FireRateLevel == 13, "Upgrade caps enforced");
                Require(game.player_bullet_slots.Length >= Mathf.CeilToInt(1.25f / game.defense_supplies.FireInterval + 1) * game.defense_supplies.PelletCount, "Player pool supports maximum build without dropping volleys");
                ClearWave(game); Require(game.defense_supplies.CardChosen && !game.defense_supplies.AwaitingCard, "Fully upgraded build cannot softlock choice");
                CombatFixture(game); game.transform_player.position = Vector2.zero; Set(game, "next_fire_at_seconds", 0f);
                game.Simulate(0.01f, Vector2.zero, Vector2.right, true, false);
                int activeShots = 0; foreach (var shot in game.player_bullet_slots) if (shot.transform_root.gameObject.activeSelf) { activeShots++; Require(shot.damage == 50, "Maximum damage snapshot"); }
                Require(activeShots == 7, "Maximum volley emitted completely");

                var feedback = Get<DefenseFeedback>(game, "defense_feedback"); Vector3 cameraRest = game.camera_world.transform.position;
                feedback.Shot(Vector2.right, true); for (int i = 0; i < 30; i++) feedback.Tick(0.016f, false); feedback.SuspendCamera();
                Require(Vector3.Distance(cameraRest, game.camera_world.transform.position) < 0.0001f, "Camera shake restores base position");
                if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null)
                {
                    game.defense_supplies.Stats.Reset(); DefenseUpgradeCatalog.upgrade_damage.Apply(game.defense_supplies.Stats); DefenseUpgradeCatalog.upgrade_projectiles.Apply(game.defense_supplies.Stats);
                    ClearWave(game); Capture(game, "cards.png");
                    CombatFixture(game); game.transform_player.position = new Vector2(-3, -1);
                    SpawnFixture(game.enemy_slots[0], DefenseEnemyRole.Hunter, new Vector2(-1, -2));
                    SpawnFixture(game.enemy_slots[1], DefenseEnemyRole.Siege, new Vector2(1.5f, 0));
                    SpawnFixture(game.enemy_slots[2], DefenseEnemyRole.Shooter, new Vector2(2, 2));
                    game.enemy_slots[2].is_targeting_player = true;
                    game.enemy_slots[2].attack_windup_elapsed_seconds = 0.6f; game.enemy_slots[2].aim_direction = ((Vector2)game.transform_player.position - (Vector2)game.enemy_slots[2].transform_root.position).normalized;
                    game.TryFireEnemyProjectile(game.enemy_slots[2]);
                    Array.Find(game.enemy_projectile_slots, x => x.transform_root.gameObject.activeSelf).transform_root.position += (Vector3)game.enemy_slots[2].aim_direction * 2;
                    Capture(game, "combat.png");
                    Require(game.enemy_slots[2].sprite_renderer_aim_marker.enabled, "Ranged aim warning is visible during windup");
                }
                string previous = game.RunId; game.StartRun(); GameSessionTracker.MarkStored(previous);
                Require(game.defense_supplies.PelletCount == 1 && game.defense_supplies.Damage == 1 && Mathf.Approximately(game.defense_supplies.FireInterval, 0.24f), "Restart resets all card stats");
                Require(!game.defense_supplies.ShopOpen && game.defense_supplies.Gold == 0 && ActiveEnemyBolts(game) == 0, "Restart resets supplies and hostile projectiles");
                foreach (var particle in game.sprite_renderer_feedback_particles) Require(!particle.gameObject.activeSelf, "Restart clears feedback");
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
            var canvas = game.txt_health.canvas;
            int previousOrder = canvas.sortingOrder;
            var camera = game.camera_world;
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
        private static int ActiveEnemyBolts(DefenseGame game) { int count = 0; foreach (var bolt in game.enemy_projectile_slots) if (bolt.transform_root.gameObject.activeSelf) count++; return count; }
        private static void CombatFixture(DefenseGame game)
        {
            game.defense_supplies.CloseShop();
            foreach (var enemy in game.enemy_slots) enemy.transform_root.gameObject.SetActive(false);
            foreach (var bullet in game.player_bullet_slots) bullet.transform_root.gameObject.SetActive(false);
            foreach (var bullet in game.enemy_projectile_slots) bullet.transform_root.gameObject.SetActive(false);
            SetProperty(game, "Phase", DefensePhase.Combat); Set(game, "pending_enemy_count", 1); Set(game, "spawn_delay_seconds", 10000f);
        }
        private static void SpawnFixture(DefenseGame.EnemySlot enemy, DefenseEnemyRole role, Vector2 position)
        {
            enemy.transform_root.gameObject.SetActive(true); enemy.transform_root.position = position; enemy.role = role;
            enemy.health = enemy.max_health = 100; enemy.threat_level = 1; enemy.is_targeting_player = false;
            enemy.next_attack_at_seconds = enemy.stunned_until_seconds = enemy.attack_windup_elapsed_seconds = 0; enemy.last_hit_volley_id = -1;
        }
        private static void ClearWave(DefenseGame game)
        {
            CombatFixture(game); Set(game, "pending_enemy_count", 0); game.Simulate(0.05f, Vector2.zero, Vector2.right, false, false);
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
                    lobby.OpenFriends(); Require(lobby.legacy_lobby_controller.Model.Section == LobbySection.Friends, "Lobby input updates social model");
                    lobby.legacy_lobby_controller.ClosePanel(); Require(!lobby.legacy_lobby_controller.game_object_backdrop.activeSelf, "Lobby view reflects closed social panel");
                    navigationStep++;
                    lobby.View.btn_solo_defense.onClick.Invoke();
                }
                else
                {
                    if (scene != SceneNavigation.SoloDefense) return;
                    var game = UnityEngine.Object.FindFirstObjectByType<DefenseGame>();
                    Require(game != null && game.State == DefenseState.Ready && game.View.game_object_ready_panel.activeSelf, "Defense opens in ready state with migrated view");
                    if (navigationStep == 1) { navigationStep++; ClickReturn(game, game.game_object_ready_panel); }
                    else if (navigationStep == 3)
                    {
                        game.StartRun(); navigationRun = game.RunId; game.Pause();
                        Require(game.State == DefenseState.Paused, "Can pause after entering from lobby");
                        navigationStep++; ClickReturn(game, game.game_object_pause_panel);
                    }
                    else
                    {
                        game.StartRun(); navigationRun = game.RunId;
                        AuthManager.Instance.Logout(); // Unauthenticated uploads must fail locally.
                        game.ApplyEnemyDamage(false, 100); game.Simulate(0, Vector2.zero, Vector2.right, false, false);
                        Require(game.State == DefenseState.Ended && game.game_object_result_panel.activeSelf, "Defeat renders results");
                        Require(game.View.txt_upload_status.text.Contains("FAILED") && game.View.txt_reward_title.text.Contains("FAILED"), "Service failures render through View");
                        Require(game.View.btn_retry.interactable, "Failed requests enable retry through View");
                        game.RetryRequests();
                        Require(game.View.btn_retry.interactable && !game.defense_reward_client.Succeeded && !game.game_result_upload_client.Succeeded, "Retry preserves failure without fabricating success");
                        navigationStep++; ClickReturn(game, game.game_object_result_panel);
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
