using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using Zpd.Gameplay;

namespace Zpd.Defense.Editor
{
    public static class DefenseSceneBuilder
    {
        private const string Art = "Assets/Resources/Art/Rgsdev/";
        private static Sprite sprite_solid;
        private static Sprite[] sprite_ui_atlas;
        private static Font font_ui;
        private static Material material_sprite;
        private static readonly Color color_color_ink = Hex("24192F");

        [MenuItem("ZPD/Defense/Create Solo Defense Scene")]
        public static void CreateScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            sprite_ui_atlas = AssetDatabase.LoadAllAssetsAtPath("Assets/Resources/UI/Lobby/cartoon-ui-atlas.png").OfType<Sprite>().ToArray();

            if (!sprite_ui_atlas.Any(s => s.name == "Panel"))
            {
                throw new InvalidOperationException("Import the lobby UI atlas before building Defense.");
            }

            Load("Full body animated characters/Char 1/with hands/idle_0.png");
            PrepareAssets();
            const string fontPath = "Assets/Resources/Fonts/NexonLv1/NEXONLv1GothicRegular.ttf";
            AssetDatabase.ImportAsset(fontPath, ImportAssetOptions.ForceSynchronousImport);
            font_ui = AssetDatabase.LoadAssetAtPath<Font>(fontPath);

            if (font_ui == null)
            {
                throw new InvalidOperationException("Import the bundled NEXON Lv.1 Gothic font first.");
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("Solo Defense");
            new GameObject("Game Session Tracker", typeof(GameSessionTracker));
            var game = root.AddComponent<DefenseGame>();
            game.defense_reward_client = root.AddComponent<DefenseRewardClient>();
            game.game_result_upload_client = root.AddComponent<GameResultUploadClient>();
            game.defense_supplies = root.AddComponent<DefenseSupplies>();
            game.defense_supplies.defense_game = game;
            game.defense_audio = root.AddComponent<DefenseAudio>();
            game.defense_audio.audio_source_combat = new GameObject("Combat Audio", typeof(AudioSource)).GetComponent<AudioSource>();
            game.defense_audio.audio_source_feedback = new GameObject("Feedback Audio", typeof(AudioSource)).GetComponent<AudioSource>();

            foreach (var source in new[]
            {
                game.defense_audio.audio_source_combat,
                game.defense_audio.audio_source_feedback
            }

            )
            {
                source.transform.SetParent(root.transform, false);
                source.playOnAwake = false;
                source.spatialBlend = 0;
            }

            game.defense_audio.audio_clip_cues = DefenseSoundBuilder.CreateClips();
            var camera = new GameObject("Arena Camera", typeof(Camera)).GetComponent<Camera>();
            camera.tag = "MainCamera";
            camera.gameObject.AddComponent<AudioListener>();
            camera.transform.position = new Vector3(0, 0, -20);
            camera.orthographic = true;
            camera.orthographicSize = 7.2f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Hex("77914A");
            game.camera_world = camera;
            var arena = new GameObject("Arena").transform;
            arena.SetParent(root.transform);
            const string meadowPath = "Assets/Resources/Art/Defense/last-signal-meadow.png";
            AssetDatabase.ImportAsset(meadowPath, ImportAssetOptions.ForceSynchronousImport);
            var meadowImporter = (TextureImporter)AssetImporter.GetAtPath(meadowPath);
            meadowImporter.textureType = TextureImporterType.Sprite;
            meadowImporter.textureShape = TextureImporterShape.Texture2D;
            meadowImporter.spriteImportMode = SpriteImportMode.Single;
            meadowImporter.mipmapEnabled = false;
            meadowImporter.textureCompression = TextureImporterCompression.Uncompressed;
            meadowImporter.maxTextureSize = 2048;
            meadowImporter.SaveAndReimport();
            var meadow = WorldSprite(
                "Last Signal Meadow - Evacuation Trail",
                arena,
                AssetDatabase.LoadAssetAtPath<Sprite>(meadowPath),
                14.6f,
                -20);
            game.transform_beacon = new GameObject("Defense Beacon").transform;
            game.transform_beacon.SetParent(root.transform);
            BuildBeaconVisual(game);
            game.transform_player = new GameObject("Player").transform;
            game.transform_player.SetParent(root.transform);
            game.transform_player.position = new Vector2(0, -2.2f);
            game.transform_player_art = Composite(
                "Character Art",
                game.transform_player,
                "Full body animated characters/Char 1/with hands/idle_0.png",
                1.25f,
                10);
            game.transform_weapon = new GameObject("Aim Pivot").transform;
            game.transform_weapon.SetParent(game.transform_player, false);
            var gun = WorldSprite("Weapon", game.transform_weapon, Load("Weapons/weaponR1.png")[0], 0.38f, 15);
            gun.transform.localPosition = new Vector2(0.57f, -0.06f);
            game.defense_supplies.sprite_renderer_weapon = gun;
            game.defense_supplies.sprite_base_weapon = Load("Weapons/weaponR1.png")[0];
            game.transform_crosshair = WorldSprite("Aim Reticle", root.transform, Load("Extras/crosshair.png")[0], 0.4f, 30).transform;

            var enemyPool = new GameObject("Enemy Pool - 48 authored slots").transform;
            enemyPool.SetParent(root.transform);
            game.enemy_slots = new DefenseGame.EnemySlot[48];
            string[] enemies =
            {
                "Enemy 1",
                "Enemy 2",
                "Enemy 4"
            };

            for (int i = 0; i < game.enemy_slots.Length; i++)
            {
                var actor = Composite(
                    "Enemy " + i.ToString("00"),
                    enemyPool,
                    "Full body animated characters/Enemies/" + enemies[i % 3] + "/idle_0.png",
                    1.05f,
                    9);
                actor.gameObject.SetActive(false);
                game.enemy_slots[i] = new DefenseGame.EnemySlot
                {
                    transform_root = actor
                };
            }

            var bulletPool = new GameObject("Projectile Pool - 128 authored slots").transform;
            bulletPool.SetParent(root.transform);
            game.player_bullet_slots = new DefenseGame.BulletSlot[128];
            var bulletSprite = Load("Extras/bullet.png")[0];

            for (int i = 0; i < game.player_bullet_slots.Length; i++)
            {
                var bullet = WorldSprite("Bullet " + i.ToString("00"), bulletPool, bulletSprite, 0.14f, 20);
                bullet.color = Hex("FFD077");
                bullet.gameObject.SetActive(false);
                game.player_bullet_slots[i] = new DefenseGame.BulletSlot
                {
                    transform_root = bullet.transform
                };
            }

            var goldPool = new GameObject("Gold Pool - 96 authored piles").transform;
            goldPool.SetParent(root.transform, false);
            game.defense_supplies.gold_drop_slots = new DefenseSupplies.GoldSlot[96];

            for (int i = 0; i < game.defense_supplies.gold_drop_slots.Length; i++)
            {
                var coin = new GameObject("Gold " + i.ToString("00")).transform;
                coin.SetParent(goldPool, false);
                Block("Outline", coin, Vector2.zero, new Vector2(0.34f, 0.34f), Color.black, 3).transform.localRotation = Quaternion.Euler(0, 0, 45);
                Block("Gold", coin, Vector2.zero, new Vector2(0.25f, 0.25f), Hex("FFC64B"), 4).transform.localRotation = Quaternion.Euler(0, 0, 45);
                Block("Glint", coin, new Vector2(-0.04f, 0.04f), new Vector2(0.06f, 0.15f), Hex("FFF3AF"), 5);
                game.defense_supplies.gold_drop_slots[i] = new DefenseSupplies.GoldSlot
                {
                    transform_root = coin
                };
                coin.gameObject.SetActive(false);
            }

            var feedbackPool = new GameObject("Feedback Pool - 96 authored sparks").transform;
            feedbackPool.SetParent(root.transform, false);
            game.sprite_renderer_feedback_particles = new SpriteRenderer[96];

            for (int i = 0; i < game.sprite_renderer_feedback_particles.Length; i++)
            {
                var spark = Block(
                    "Spark " + i.ToString("00"),
                    feedbackPool,
                    Vector2.zero,
                    Vector2.one * 0.1f,
                    Color.white,
                    25);
                spark.gameObject.SetActive(false);
                game.sprite_renderer_feedback_particles[i] = spark;
            }

            BuildGameplayActors(game);
            BuildUi(game);
            var events = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            events.GetComponent<EventSystem>().firstSelectedGameObject = game.btn_start.gameObject;
            var fit = camera.gameObject.AddComponent<DefenseCameraFit>();
            fit.minimum_half_height = 7.2f;
            fit.minimum_half_width = 12.8f;
            fit.sprite_renderer_meadow = meadow;
            EditorSceneManager.SaveScene(
                scene,
                AssetDatabase.GenerateUniqueAssetPath("Assets/Scenes/SoloDefense.unity"));
            Selection.activeGameObject = root;
            Debug.Log(
                "[Defense Builder] Created wave defense with enemy roles, warnings, preparation supplies and feedback.");
        }

        private static void BuildUi(DefenseGame game)
        {
            var canvas = new GameObject(
                "Defense Canvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            var root = Rect("Layout", canvas.transform, 0, 0, 1280, 720);
            var header = Panel("HUD", root, 0, 316, 1230, 70, "Card");
            header.GetComponent<Image>().raycastTarget = false;
            game.txt_health = Text("Health", header, "YOU 100 / 100     BEACON 100 / 100", -330, 0, 530, 40, 21);
            game.txt_wave = Text("Score", header, "WAVE 1     KILLS 0", 136, 0, 360, 40, 23);
            game.txt_score = Text("Timer", header, "0s / EXP: --", 442, 0, 220, 40, 19);
            game.txt_hint = Text("Wave Status", root, "", 0, -315, 800, 38, 23, Color.white);
            var wallet = Panel("Supply Status", root, -401, 232, 460, 72, "Card");
            wallet.GetComponent<Image>().raycastTarget = false;
            game.defense_supplies.txt_wallet = Text("Gold and Weapon", wallet, "GOLD 0 / PISTOL", 0, 0, 440, 60, 18);
            BuildShop(game, root);
            BuildStatusBars(game, root);
            game.game_object_ready_panel = Modal("Start Overlay", root, out var ready);
            Text("Title", ready, "THE LAST SIGNAL", 0, 177, 700, 64, 43);
            Text("Subtitle", ready, "The Last Signal", 0, 108, 730, 48, 27);
            game.btn_start = Button("Start Defense", ready, "START DEFENSE", 0, -90, 370, 66, game.StartRun, "Battle");

            game.game_object_pause_panel = Modal("Pause Overlay", root, out var pause);
            Text("Title", pause, "PAUSED", 0, 110, 650, 64, 42);
            game.btn_resume = Button("Resume", pause, "RESUME", 0, -75, 350, 64, game.Resume, "Battle");
            game.btn_help = Button("Help", pause, "?", 310, 200, 72, 66, game.OpenHelp, "Button");
            game.game_object_pause_panel.SetActive(false);

            game.game_object_help_panel = Modal("Help Overlay", root, out var help);
            Text("Title", help, "HOW TO PLAY", 0, 220, 700, 48, 30);
            var explanation = Text("Description", help, HelpCopy, 0, 15, 745, 350, 19);
            explanation.alignment = TextAnchor.UpperLeft;
            Text(
                "Credits",
                help,
                "Sprites: Rgsdev (CC0) | Art: OpenAI / Codex | SFX: project originals\nFont: NEXON Lv.1 Gothic / Copyright NEXON Korea Corporation",
                0,
                -191,
                745,
                43,
                12);
            game.btn_help_back = Button("Back to Pause", help, "BACK", 0, -240, 280, 52, game.CloseHelp, "Button");
            game.game_object_help_panel.SetActive(false);

            game.game_object_result_panel = Modal("Result Overlay", root, out var result);
            game.txt_result_stats = Text("Run Stats", result, "", 0, 177, 735, 98, 24);
            game.View.txt_reward_title = Text("Reward Status", result, "REWARD REQUEST FAILED", 0, 74, 735, 56, 28);
            game.View.txt_reward_detail = Text("Reward Detail", result, "No experience was awarded.", 0, -3, 730, 80, 18);
            game.View.txt_upload_status = Text("Game Log Status", result, "GAME LOG: --", 0, -92, 735, 65, 17);
            game.View.btn_retry = Button(
                "Retry Reward",
                result,
                "RETRY FAILED REQUESTS",
                -190,
                -178,
                328,
                62,
                game.RetryRequests,
                "Button");
            game.btn_restart = Button("New Run", result, "NEW RUN", 190, -178, 328, 62, game.StartRun, "Battle");
            game.game_object_result_panel.SetActive(false);
            BuildNavigation(game);
        }

        private static void BuildShop(DefenseGame game, Transform root)
        {
            var shop = Panel("Upgrade Cards and Supplies", root, 0, -30, 920, 440, "Panel");
            game.defense_supplies.game_object_shop_panel = shop.gameObject;
            game.defense_supplies.txt_shop_title = Text("Countdown", shop, "CHOOSE ONE FREE UPGRADE", 0, 180, 840, 40, 26);
            Text("Card Hint", shop, "Time pauses until you choose. Upgrades last for this run.", 0, 142, 840, 30, 17);
            game.defense_supplies.btn_upgrade_cards = new Button[3];
            game.defense_supplies.txt_upgrade_cards = new Text[3];
            UnityAction[] choices =
            {
                game.defense_supplies.ChooseProjectiles,
                game.defense_supplies.ChooseDamage,
                game.defense_supplies.ChooseFireRate
            };
            var preview = new DefenseCombatStats();

            for (int i = 0; i < 3; i++)
            {
                var card = Button(
                    "Upgrade Card " + (i + 1),
                    shop,
                    (i + 1) + "  " + DefenseUpgradeCatalog.At(i).Describe(preview),
                    (i - 1) * 280,
                    25,
                    258,
                    172,
                    choices[i],
                    "Card");
                game.defense_supplies.btn_upgrade_cards[i] = card;
                game.defense_supplies.txt_upgrade_cards[i] = card.GetComponentInChildren<Text>();
                game.defense_supplies.txt_upgrade_cards[i].fontSize = 19;
            }

            game.defense_supplies.btn_heal = Button(
                "Heal",
                shop,
                "4  HEAL +35 / 15G",
                -214,
                -95,
                400,
                46,
                game.defense_supplies.BuyHeal,
                "Battle");
            game.defense_supplies.txt_heal = game.defense_supplies.btn_heal.GetComponentInChildren<Text>();
            game.defense_supplies.txt_heal.fontSize = 18;
            game.defense_supplies.btn_repair = Button(
                "Repair Beacon",
                shop,
                "5  REPAIR +15 / 25G",
                214,
                -95,
                400,
                46,
                game.defense_supplies.BuyRepair,
                "Battle");
            game.defense_supplies.txt_repair = game.defense_supplies.btn_repair.GetComponentInChildren<Text>();
            game.defense_supplies.txt_repair.fontSize = 17;
            game.defense_supplies.txt_shop_message = Text("Feedback", shop, "", 0, -137, 840, 28, 15);
            game.btn_next_wave = Button(
                "Next Wave",
                shop,
                "NEXT WAVE  [ENTER]",
                0,
                -183,
                380,
                48,
                game.StartNextWave,
                "Button");
            shop.gameObject.SetActive(false);
        }

        private const string HelpCopy = "Protect the beacon. The run ends if you or the beacon fall.\n\n" + "WASD / Arrows: Move     Mouse: Aim / Left click: Shoot\nSpace: Dash     M: Mute sound     Esc: Pause\n1 / 2 / 3: Pick upgrade     4: Heal     5: Repair beacon\nEnter: Next wave\n\n" + "Teal: Chasers. Orange: Beacon attackers.\nPurple: Shooters. Watch their aim and dodge projectiles.\nHit enemies to interrupt their attacks.\n" + "Choose one free upgrade after each wave, then prepare for 8 seconds.\nSpend gold to heal or repair. Repair once per supply break.\nUpgrades and gold last for this run only.";

        private static void BuildGameplayActors(DefenseGame game)
        {
            if (game.player_bullet_slots.Length < 128)
            {
                int oldCount = game.player_bullet_slots.Length;
                Array.Resize(ref game.player_bullet_slots, 128);

                for (int i = oldCount; i < game.player_bullet_slots.Length; i++)
                {
                    var bullet = WorldSprite(
                        "Bullet " + i,
                        game.player_bullet_slots[0].transform_root.parent,
                        Load("Extras/bullet.png")[0],
                        0.14f,
                        20);
                    bullet.color = Hex("FFD077");
                    bullet.gameObject.SetActive(false);
                    game.player_bullet_slots[i] = new DefenseGame.BulletSlot
                    {
                        transform_root = bullet.transform
                    };
                }
            }

            if (game.enemy_projectile_slots.Length == 0)
            {
                var pool = new GameObject("Enemy Projectile Pool - 48 authored slots").transform;
                pool.SetParent(game.transform, false);
                game.enemy_projectile_slots = new DefenseEnemyProjectile[48];

                for (int i = 0; i < game.enemy_projectile_slots.Length; i++)
                {
                    var projectile = Block(
                        "Enemy Bolt " + i,
                        pool,
                        Vector2.zero,
                        new Vector2(0.32f, 0.18f),
                        Hex("FF438A"),
                        24);
                    projectile.gameObject.SetActive(false);
                    game.enemy_projectile_slots[i] = new DefenseEnemyProjectile
                    {
                        transform_root = projectile.transform
                    };
                }
            }

            if (game.sprite_renderer_entry_markers.Length == 0)
            {
                game.sprite_renderer_entry_markers = new SpriteRenderer[2];

                for (int i = 0; i < 2; i++)
                {
                    var marker = Block(
                        "Entry Warning " + i,
                        game.transform,
                        Vector2.zero,
                        Vector2.one * 0.8f,
                        Hex("FF6429"),
                        6);
                    marker.transform.localRotation = Quaternion.Euler(0, 0, 45);
                    marker.gameObject.SetActive(false);
                    game.sprite_renderer_entry_markers[i] = marker;
                }
            }

            foreach (var enemy in game.enemy_slots)
            {
                if (enemy.sprite_renderer_role_marker == null)
                {
                    enemy.sprite_renderer_role_marker = Block(
                        "Role Marker",
                        enemy.transform_root,
                        new Vector2(0, 0.78f),
                        Vector2.one * 0.24f,
                        Color.white,
                        16);
                }

                if (enemy.sprite_renderer_attack_marker == null)
                {
                    enemy.sprite_renderer_attack_marker = Block(
                        "Attack Windup",
                        enemy.transform_root,
                        new Vector2(0, -0.67f),
                        new Vector2(0.64f, 0.08f),
                        Color.red,
                        16);
                }

                if (enemy.sprite_renderer_health_bar == null)
                {
                    enemy.sprite_renderer_health_bar = Block(
                        "Enemy Health",
                        enemy.transform_root,
                        new Vector2(0, 0.6f),
                        new Vector2(0.64f, 0.056f),
                        Color.green,
                        16);
                }

                if (enemy.sprite_renderer_aim_marker == null)
                {
                    enemy.sprite_renderer_aim_marker = Block(
                        "Ranged Aim Warning",
                        enemy.transform_root,
                        Vector2.zero,
                        new Vector2(6.2f, 0.045f),
                        Hex("FF438A"),
                        5);
                }

                enemy.sprite_renderer_attack_marker.enabled = enemy.sprite_renderer_health_bar.enabled = enemy.sprite_renderer_aim_marker.enabled = false;
            }
        }

        private static void BuildStatusBars(DefenseGame game, Transform root)
        {
            if (game.img_beacon_health_bar == null)
            {
                game.img_beacon_health_bar = StatusBar("Beacon Health Bar", root, -330, 282, 480, 7, Hex("62D9B0"));
            }

            if (game.img_dash_bar == null)
            {
                game.img_dash_bar = StatusBar("Dash Cooldown", root, -500, -285, 180, 10, Hex("72E0FF"));
            }

            if (game.txt_dash == null)
            {
                game.txt_dash = Text("Dash Status", root, "DASH READY", -500, -310, 210, 28, 17, Color.white);
            }

            game.txt_wave.fontSize = 18;
        }

        private static Image StatusBar(
            string name,
            Transform root,
            float x,
            float y,
            float width,
            float height,
            Color color)
        {
            var background = Rect(name + " Background", root, x, y, width, height).gameObject.AddComponent<Image>();
            background.color = color_color_ink;
            background.raycastTarget = false;
            var fill = Rect(name, background.transform, 0, 0, width, height).gameObject.AddComponent<Image>();
            fill.sprite = sprite_solid;
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = 0;
            fill.fillAmount = 1;
            fill.color = color;
            fill.raycastTarget = false;
            return fill;
        }

        [MenuItem("ZPD/Defense/Upgrade Open Defense Scene")]
        public static void UpgradeOpenScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            var game = UnityEngine.Object.FindFirstObjectByType<DefenseGame>();

            if (game == null)
            {
                throw new InvalidOperationException("Open SoloDefense before upgrading.");
            }

            sprite_ui_atlas = AssetDatabase.LoadAllAssetsAtPath("Assets/Resources/UI/Lobby/cartoon-ui-atlas.png").OfType<Sprite>().ToArray();
            font_ui = AssetDatabase.LoadAssetAtPath<Font>("Assets/Resources/Fonts/NexonLv1/NEXONLv1GothicRegular.ttf");
            PrepareAssets();
            BuildBeaconVisual(game);
            BuildGameplayActors(game);
            var layout = game.txt_health.transform.parent.parent;

            if (game.defense_supplies.btn_upgrade_cards.Length != 3)
            {
                UnityEngine.Object.DestroyImmediate(game.defense_supplies.game_object_shop_panel);
                BuildShop(game, layout);
            }

            game.defense_supplies.sprite_base_weapon = Load("Weapons/weaponR1.png")[0];
            BuildStatusBars(game, layout);
            BuildNavigation(game);

            foreach (var label in game.game_object_help_panel.GetComponentsInChildren<Text>(true))
            {
                if (label.name == "Description")
                {
                    label.text = HelpCopy;
                }
            }

            EditorSceneManager.MarkSceneDirty(game.gameObject.scene);
        }

        [MenuItem("ZPD/Defense/Add Lobby Return Buttons")]
        public static void AddLobbyReturnButtons()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            var game = UnityEngine.Object.FindFirstObjectByType<DefenseGame>();

            if (game == null)
            {
                throw new InvalidOperationException("Open SoloDefense before adding return buttons.");
            }

            sprite_ui_atlas = AssetDatabase.LoadAllAssetsAtPath("Assets/Resources/UI/Lobby/cartoon-ui-atlas.png").OfType<Sprite>().ToArray();
            font_ui = AssetDatabase.LoadAssetAtPath<Font>("Assets/Resources/Fonts/NexonLv1/NEXONLv1GothicRegular.ttf");
            BuildNavigation(game);
            EditorSceneManager.MarkSceneDirty(game.gameObject.scene);
        }

        private static void BuildNavigation(DefenseGame game)
        {
            foreach (var overlay in new[]
            {
                game.game_object_ready_panel,
                game.game_object_pause_panel,
                game.game_object_result_panel
            }

            )
            {
                var panel = overlay.transform.Find("Panel");

                if (panel.Find("Return to Lobby") == null)
                {
                    Button("Return to Lobby", panel, "RETURN TO LOBBY", 0, -246, 350, 46, game.ReturnToLobby, "Button");
                }
            }
        }

        private static void BuildBeaconVisual(DefenseGame game)
        {
            const string path = "Assets/Resources/Art/Defense/defense-crystal.png";
            AssetDatabase.ImportAsset(path);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;

            if (importer == null)
            {
                throw new InvalidOperationException("Missing defense crystal: " + path);
            }

            importer.textureShape = TextureImporterShape.Texture2D;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePivot = new Vector2(0.5f, 0.5f);
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 1024;
            importer.SaveAndReimport();
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);

            if (sprite == null)
            {
                throw new InvalidOperationException("Defense crystal sprite import failed: " + path);
            }

            // Keep the gameplay root and its hit feedback; replace only the authored artwork.

            foreach (var name in new[]
            {
                "Outline",
                "Crystal",
                "Core",
                "Defense Crystal"
            }

            )
            {
                var child = game.transform_beacon.Find(name);

                if (child != null)
                {
                    UnityEngine.Object.DestroyImmediate(child.gameObject);
                }
            }

            WorldSprite("Defense Crystal", game.transform_beacon, sprite, 2.4f, 1);
        }

        private static GameObject Modal(string name, Transform parent, out RectTransform panel)
        {
            var overlay = Rect(name, parent, 0, 0, 1280, 720);
            var shade = overlay.gameObject.AddComponent<Image>();
            shade.color = new Color(0.02f, 0.015f, 0.04f, 0.85f);
            panel = Panel("Panel", overlay, 0, 0, 850, 570, "Panel");
            return overlay.gameObject;
        }

        private static RectTransform Panel(
            string name,
            Transform parent,
            float x,
            float y,
            float w,
            float h,
            string skin)
        {
            var rect = Rect(name, parent, x, y, w, h);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite_ui_atlas.Single(s => s.name == skin);
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 2.5f;
            return rect;
        }

        private static Button Button(
            string name,
            Transform parent,
            string label,
            float x,
            float y,
            float w,
            float h,
            UnityAction callback,
            string skin)
        {
            var rect = Panel(name, parent, x, y, w, h, skin);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = rect.GetComponent<Image>();
            UnityEventTools.AddPersistentListener(button.onClick, callback);
            Text("Label", rect, label, 0, 2, w - 40, h - 12, 21);
            return button;
        }

        private static Text Text(
            string name,
            Transform parent,
            string value,
            float x,
            float y,
            float w,
            float h,
            int size,
            Color? color = null)
        {
            var text = Rect(name, parent, x, y, w, h).gameObject.AddComponent<Text>();
            text.font = font_ui;
            text.fontSize = size;
            text.fontStyle = size >= 23 ? FontStyle.Bold : FontStyle.Normal;
            text.color = color ?? color_color_ink;
            text.text = value;
            text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false;
            return text;
        }

        private static RectTransform Rect(string name, Transform parent, float x, float y, float w, float h)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(w, h);
            rect.anchoredPosition = new Vector2(x, y);
            return rect;
        }

        private static Sprite[] Load(string path) => AssetDatabase.LoadAllAssetsAtPath(Art + path).OfType<Sprite>().OrderBy(s => s.name).ToArray();

        private static SpriteRenderer WorldSprite(string name, Transform parent, Sprite sprite, float height, int order)
        {
            var renderer = new GameObject(name, typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
            renderer.transform.SetParent(parent, false);
            renderer.sprite = sprite;
            renderer.sharedMaterial = material_sprite;
            renderer.sortingOrder = order;
            renderer.transform.localScale = Vector3.one * height / sprite.bounds.size.y;
            return renderer;
        }

        private static Transform Composite(string name, Transform parent, string path, float height, int order)
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            var sprites = Load(path);

            if (sprites.Length == 0)
            {
                throw new InvalidOperationException("Missing art: " + path);
            }

            float minX = sprites.Min(s => s.rect.xMin), maxX = sprites.Max(s => s.rect.xMax);
            float minY = sprites.Min(s => s.rect.yMin), maxY = sprites.Max(s => s.rect.yMax);
            Vector2 center = new Vector2((minX + maxX) / 2, (minY + maxY) / 2);
            float scale = height / (maxY - minY);

            foreach (var sprite in sprites)
            {
                var renderer = WorldSprite(sprite.name, root, sprite, sprite.rect.height * scale, order);
                // Imported sprites may have bottom-left pivots; correct to the actual sheet position.

                renderer.transform.localPosition = (sprite.rect.position + sprite.pivot - center) * scale;
            }

            return root;
        }

        private static SpriteRenderer Block(
            string name,
            Transform parent,
            Vector2 position,
            Vector2 size,
            Color color,
            int order)
        {
            var renderer = WorldSprite(name, parent, sprite_solid, 1, order);
            renderer.transform.localScale = new Vector3(size.x / sprite_solid.bounds.size.x, size.y / sprite_solid.bounds.size.y, 1);
            renderer.transform.localPosition = position;
            renderer.color = color;
            return renderer;
        }

        private static void PrepareAssets()
        {
            const string folder = "Assets/Resources/UI/Defense";
            Directory.CreateDirectory(folder);
            const string path = folder + "/solid.png";

            if (!File.Exists(path))
            {
                var texture = new Texture2D(8, 8);
                texture.SetPixels(Enumerable.Repeat(Color.white, 64).ToArray());
                texture.Apply();
                File.WriteAllBytes(path, texture.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(texture);
            }

            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
            sprite_solid = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            const string materialPath = folder + "/DefenseUnlit.mat";
            material_sprite = AssetDatabase.LoadAssetAtPath<Material>(materialPath);

            if (material_sprite == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");

                if (shader == null)
                {
                    throw new InvalidOperationException("URP 2D unlit shader missing.");
                }

                material_sprite = new Material(shader);
                AssetDatabase.CreateAsset(material_sprite, materialPath);
            }
        }

        private static Color Hex(string value)
        {
            ColorUtility.TryParseHtmlString("#" + value, out var color);
            return color;
        }
    }
}
