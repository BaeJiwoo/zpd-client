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
        private static Sprite solid;
        private static Sprite[] ui;
        private static Font font;
        private static Material spriteMaterial;
        private static readonly Color Ink = Hex("24192F");

        [MenuItem("ZPD/Defense/Create Solo Defense Scene")]
        public static void CreateScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            ui = AssetDatabase.LoadAllAssetsAtPath("Assets/Resources/UI/Lobby/cartoon-ui-atlas.png").OfType<Sprite>().ToArray();
            if (!ui.Any(s => s.name == "Panel")) throw new InvalidOperationException("Import the lobby UI atlas before building Defense.");
            Load("Full body animated characters/Char 1/with hands/idle_0.png");
            PrepareAssets();
            const string fontPath = "Assets/Resources/Fonts/NotoSansKR/NotoSansKR-Regular.otf";
            AssetDatabase.ImportAsset(fontPath, ImportAssetOptions.ForceSynchronousImport);
            font = AssetDatabase.LoadAssetAtPath<Font>(fontPath);
            if (font == null) throw new InvalidOperationException("Import the bundled Noto Sans KR font first.");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("Solo Defense");
            new GameObject("Game Session Tracker", typeof(GameSessionTracker));
            var game = root.AddComponent<DefenseGame>();
            game.rewards = root.AddComponent<DefenseRewardClient>();
            game.resultUpload = root.AddComponent<GameResultUploadClient>();
            game.supplies = root.AddComponent<DefenseSupplies>();
            game.supplies.game = game;
            game.audioEffects = root.AddComponent<DefenseAudio>();
            game.audioEffects.combat = new GameObject("Combat Audio", typeof(AudioSource)).GetComponent<AudioSource>();
            game.audioEffects.feedback = new GameObject("Feedback Audio", typeof(AudioSource)).GetComponent<AudioSource>();
            foreach (var source in new[] { game.audioEffects.combat, game.audioEffects.feedback })
            {
                source.transform.SetParent(root.transform, false); source.playOnAwake = false; source.spatialBlend = 0;
            }
            game.audioEffects.clips = DefenseSoundBuilder.CreateClips();
            var camera = new GameObject("Arena Camera", typeof(Camera)).GetComponent<Camera>();
            camera.tag = "MainCamera";
            camera.gameObject.AddComponent<AudioListener>();
            camera.transform.position = new Vector3(0, 0, -20);
            camera.orthographic = true;
            camera.orthographicSize = 7.2f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Hex("77914A");
            game.worldCamera = camera;
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
            var meadow = WorldSprite("Last Signal Meadow - Evacuation Trail", arena,
                AssetDatabase.LoadAssetAtPath<Sprite>(meadowPath), 14.6f, -20);
            game.beacon = new GameObject("Defense Beacon").transform;
            game.beacon.SetParent(root.transform);
            BuildBeaconVisual(game);
            game.player = new GameObject("Player").transform;
            game.player.SetParent(root.transform);
            game.player.position = new Vector2(0, -2.2f);
            game.playerArt = Composite("Character Art", game.player, "Full body animated characters/Char 1/with hands/idle_0.png", 1.25f, 10);
            game.weapon = new GameObject("Aim Pivot").transform;
            game.weapon.SetParent(game.player, false);
            var gun = WorldSprite("Weapon", game.weapon, Load("Weapons/weaponR1.png")[0], 0.38f, 15);
            gun.transform.localPosition = new Vector2(0.57f, -0.06f);
            game.supplies.weaponRenderer = gun;
            game.supplies.baseWeaponSprite = Load("Weapons/weaponR1.png")[0];
            game.crosshair = WorldSprite("Aim Reticle", root.transform, Load("Extras/crosshair.png")[0], 0.4f, 30).transform;

            var enemyPool = new GameObject("Enemy Pool - 48 authored slots").transform;
            enemyPool.SetParent(root.transform);
            game.enemies = new DefenseGame.EnemySlot[48];
            string[] enemies = { "Enemy 1", "Enemy 2", "Enemy 4" };
            for (int i = 0; i < game.enemies.Length; i++)
            {
                var actor = Composite("Enemy " + i.ToString("00"), enemyPool, "Full body animated characters/Enemies/" + enemies[i % 3] + "/idle_0.png", 1.05f, 9);
                actor.gameObject.SetActive(false);
                game.enemies[i] = new DefenseGame.EnemySlot { root = actor };
            }
            var bulletPool = new GameObject("Projectile Pool - 128 authored slots").transform;
            bulletPool.SetParent(root.transform);
            game.bullets = new DefenseGame.BulletSlot[128];
            var bulletSprite = Load("Extras/bullet.png")[0];
            for (int i = 0; i < game.bullets.Length; i++)
            {
                var bullet = WorldSprite("Bullet " + i.ToString("00"), bulletPool, bulletSprite, 0.14f, 20);
                bullet.color = Hex("FFD077");
                bullet.gameObject.SetActive(false);
                game.bullets[i] = new DefenseGame.BulletSlot { root = bullet.transform };
            }
            var goldPool = new GameObject("Gold Pool - 96 authored piles").transform;
            goldPool.SetParent(root.transform, false);
            game.supplies.drops = new DefenseSupplies.GoldSlot[96];
            for (int i = 0; i < game.supplies.drops.Length; i++)
            {
                var coin = new GameObject("Gold " + i.ToString("00")).transform;
                coin.SetParent(goldPool, false);
                Block("Outline", coin, Vector2.zero, new Vector2(0.34f, 0.34f), Color.black, 3).transform.localRotation = Quaternion.Euler(0, 0, 45);
                Block("Gold", coin, Vector2.zero, new Vector2(0.25f, 0.25f), Hex("FFC64B"), 4).transform.localRotation = Quaternion.Euler(0, 0, 45);
                Block("Glint", coin, new Vector2(-0.04f, 0.04f), new Vector2(0.06f, 0.15f), Hex("FFF3AF"), 5);
                game.supplies.drops[i] = new DefenseSupplies.GoldSlot { root = coin };
                coin.gameObject.SetActive(false);
            }
            var feedbackPool = new GameObject("Feedback Pool - 96 authored sparks").transform;
            feedbackPool.SetParent(root.transform, false);
            game.feedbackParticles = new SpriteRenderer[96];
            for (int i = 0; i < game.feedbackParticles.Length; i++)
            {
                var spark = Block("Spark " + i.ToString("00"), feedbackPool, Vector2.zero, Vector2.one * 0.1f, Color.white, 25);
                spark.gameObject.SetActive(false);
                game.feedbackParticles[i] = spark;
            }
            BuildGameplayActors(game);
            BuildUi(game);
            var events = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            events.GetComponent<EventSystem>().firstSelectedGameObject = game.startButton.gameObject;
            var fit = camera.gameObject.AddComponent<DefenseCameraFit>();
            fit.minimumHalfHeight = 7.2f;
            fit.minimumHalfWidth = 12.8f;
            fit.meadow = meadow;
            EditorSceneManager.SaveScene(scene, AssetDatabase.GenerateUniqueAssetPath("Assets/Scenes/SoloDefense.unity"));
            Selection.activeGameObject = root;
            Debug.Log("[Defense Builder] Created wave defense with enemy roles, warnings, preparation supplies and feedback.");
        }

        private static void BuildUi(DefenseGame game)
        {
            var canvas = new GameObject("Defense Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            var root = Rect("Layout", canvas.transform, 0, 0, 1280, 720);
            var header = Panel("HUD", root, 0, 316, 1230, 70, "Card");
            header.GetComponent<Image>().raycastTarget = false;
            game.healthText = Text("Health", header, "YOU 100 / 100     BEACON 100 / 100", -330, 0, 530, 40, 21);
            game.waveText = Text("Score", header, "WAVE 1     KILLS 0", 136, 0, 360, 40, 23);
            game.scoreText = Text("Timer", header, "0s / EXP: --", 442, 0, 220, 40, 19);
            game.hintText = Text("Wave Status", root, "", 0, -315, 800, 38, 23, Color.white);
            var wallet = Panel("Supply Status", root, -401, 232, 460, 72, "Card");
            wallet.GetComponent<Image>().raycastTarget = false;
            game.supplies.walletText = Text("Gold and Weapon", wallet, "GOLD 0 / PISTOL", 0, 0, 440, 60, 18);
            BuildShop(game, root);
            BuildStatusBars(game, root);
            game.readyPanel = Modal("Start Overlay", root, out var ready);
            Text("Title", ready, "THE LAST SIGNAL", 0, 177, 700, 64, 43);
            Text("Subtitle", ready, "마지막 신호", 0, 108, 730, 48, 27);
            game.startButton = Button("Start Defense", ready, "전투 시작", 0, -90, 370, 66, game.StartRun, "Battle");

            game.pausePanel = Modal("Pause Overlay", root, out var pause);
            Text("Title", pause, "일시정지", 0, 110, 650, 64, 42);
            game.resumeButton = Button("Resume", pause, "계속하기", 0, -75, 350, 64, game.Resume, "Battle");
            game.helpButton = Button("Help", pause, "?", 310, 200, 72, 66, game.OpenHelp, "Button");
            game.pausePanel.SetActive(false);

            game.helpPanel = Modal("Help Overlay", root, out var help);
            Text("Title", help, "게임 안내", 0, 220, 700, 48, 30);
            var explanation = Text("Description", help,
                HelpCopy,
                0, 15, 745, 350, 19);
            explanation.alignment = TextAnchor.UpperLeft;
            Text("Credits", help, "Sprites: Rgsdev (CC0) | Art: OpenAI / Codex | SFX: project originals\nFont: Noto Sans KR (SIL OFL 1.1)", 0, -191, 745, 43, 12);
            game.helpBackButton = Button("Back to Pause", help, "돌아가기", 0, -240, 280, 52, game.CloseHelp, "Button");
            game.helpPanel.SetActive(false);

            game.resultPanel = Modal("Result Overlay", root, out var result);
            game.resultStats = Text("Run Stats", result, "", 0, 177, 735, 98, 24);
            game.rewards.title = Text("Reward Status", result, "REWARD REQUEST FAILED", 0, 74, 735, 56, 28);
            game.rewards.detail = Text("Reward Detail", result, "No experience was awarded.", 0, -3, 730, 80, 18);
            game.resultUpload.statusText = Text("Game Log Status", result, "GAME LOG: --", 0, -92, 735, 65, 17);
            game.rewards.retryButton = Button("Retry Reward", result, "RETRY FAILED REQUESTS", -190, -178, 328, 62, game.RetryRequests, "Button");
            game.restartButton = Button("New Run", result, "NEW RUN", 190, -178, 328, 62, game.StartRun, "Battle");
            game.resultPanel.SetActive(false);
        }

        private static void BuildShop(DefenseGame game, Transform root)
        {
            var shop = Panel("Upgrade Cards and Supplies", root, 0, -30, 920, 440, "Panel");
            game.supplies.shopPanel = shop.gameObject;
            game.supplies.shopTitle = Text("Countdown", shop, "강화 카드 1장 선택 · 무료", 0, 180, 840, 40, 26);
            Text("Card Hint", shop, "선택 전에는 시간이 멈춥니다 · 강화는 이번 판에 누적됩니다", 0, 142, 840, 30, 17);
            game.supplies.cardButtons = new Button[3];
            game.supplies.cardLabels = new Text[3];
            UnityAction[] choices = { game.supplies.ChooseProjectiles, game.supplies.ChooseDamage, game.supplies.ChooseFireRate };
            var preview = new DefenseCombatStats();
            for (int i = 0; i < 3; i++)
            {
                var card = Button("Upgrade Card " + (i + 1), shop, (i + 1) + "  " + DefenseUpgradeCatalog.At(i).Describe(preview), (i - 1) * 280, 25, 258, 172, choices[i], "Card");
                game.supplies.cardButtons[i] = card;
                game.supplies.cardLabels[i] = card.GetComponentInChildren<Text>();
                game.supplies.cardLabels[i].fontSize = 19;
            }
            game.supplies.healButton = Button("Heal", shop, "4  회복 +35 / 15G", -214, -95, 400, 46, game.supplies.BuyHeal, "Battle");
            game.supplies.healLabel = game.supplies.healButton.GetComponentInChildren<Text>();
            game.supplies.healLabel.fontSize = 18;
            game.supplies.repairButton = Button("Repair Beacon", shop, "5  비콘 +15 / 25G", 214, -95, 400, 46, game.supplies.BuyRepair, "Battle");
            game.supplies.repairLabel = game.supplies.repairButton.GetComponentInChildren<Text>();
            game.supplies.repairLabel.fontSize = 17;
            game.supplies.shopMessage = Text("Feedback", shop, "", 0, -137, 840, 28, 15);
            game.nextWaveButton = Button("Next Wave", shop, "다음 웨이브  [ENTER]", 0, -183, 380, 48, game.StartNextWave, "Button");
            shop.gameObject.SetActive(false);
        }

        private const string HelpCopy = "마지막 신호석을 지켜 주세요. 플레이어 또는 비콘의 체력이 0이면 종료합니다.\n\n" +
            "WASD / 방향키  이동     마우스  조준 · 왼쪽 버튼 사격\nSpace  대시     M  효과음 음소거     Esc  일시정지\n1 · 2 · 3  카드 선택     4  회복     5  비콘 수리\nEnter  다음 웨이브\n\n" +
            "청록 막대: 추격병 / 주황 마름모: 비콘 공격병\n보라 세로 막대: 사격병. 조준선을 보고 투사체를 피하세요.\n명중하면 적의 공격 준비를 끊을 수 있습니다.\n" +
            "웨이브 종료 후 무료 강화 카드 1장을 선택하세요. 선택 후 8초간 준비합니다.\n골드는 회복·수리에 사용하며, 비콘 수리는 보급당 1회입니다.\n카드 강화와 골드는 이번 판에서만 유지됩니다.";

        private static void BuildGameplayActors(DefenseGame game)
        {
            if (game.bullets.Length < 128)
            {
                int oldCount = game.bullets.Length;
                Array.Resize(ref game.bullets, 128);
                for (int i = oldCount; i < game.bullets.Length; i++)
                {
                    var bullet = WorldSprite("Bullet " + i, game.bullets[0].root.parent, Load("Extras/bullet.png")[0], 0.14f, 20);
                    bullet.color = Hex("FFD077"); bullet.gameObject.SetActive(false);
                    game.bullets[i] = new DefenseGame.BulletSlot { root = bullet.transform };
                }
            }
            if (game.enemyBullets.Length == 0)
            {
                var pool = new GameObject("Enemy Projectile Pool - 48 authored slots").transform;
                pool.SetParent(game.transform, false);
                game.enemyBullets = new DefenseEnemyProjectile[48];
                for (int i = 0; i < game.enemyBullets.Length; i++)
                {
                    var projectile = Block("Enemy Bolt " + i, pool, Vector2.zero, new Vector2(0.32f, 0.18f), Hex("FF438A"), 24);
                    projectile.gameObject.SetActive(false);
                    game.enemyBullets[i] = new DefenseEnemyProjectile { root = projectile.transform };
                }
            }
            if (game.entryMarkers.Length == 0)
            {
                game.entryMarkers = new SpriteRenderer[2];
                for (int i = 0; i < 2; i++)
                {
                    var marker = Block("Entry Warning " + i, game.transform, Vector2.zero, Vector2.one * 0.8f, Hex("FF6429"), 6);
                    marker.transform.localRotation = Quaternion.Euler(0, 0, 45);
                    marker.gameObject.SetActive(false);
                    game.entryMarkers[i] = marker;
                }
            }
            foreach (var enemy in game.enemies)
            {
                if (enemy.roleMarker == null) enemy.roleMarker = Block("Role Marker", enemy.root, new Vector2(0, 0.78f), Vector2.one * 0.24f, Color.white, 16);
                if (enemy.attackMarker == null) enemy.attackMarker = Block("Attack Windup", enemy.root, new Vector2(0, -0.67f), new Vector2(0.64f, 0.08f), Color.red, 16);
                if (enemy.healthBar == null) enemy.healthBar = Block("Enemy Health", enemy.root, new Vector2(0, 0.6f), new Vector2(0.64f, 0.056f), Color.green, 16);
                if (enemy.aimMarker == null) enemy.aimMarker = Block("Ranged Aim Warning", enemy.root, Vector2.zero, new Vector2(6.2f, 0.045f), Hex("FF438A"), 5);
                enemy.attackMarker.enabled = enemy.healthBar.enabled = enemy.aimMarker.enabled = false;
            }
        }

        private static void BuildStatusBars(DefenseGame game, Transform root)
        {
            if (game.beaconHealthBar == null) game.beaconHealthBar = StatusBar("Beacon Health Bar", root, -330, 282, 480, 7, Hex("62D9B0"));
            if (game.dashBar == null) game.dashBar = StatusBar("Dash Cooldown", root, -500, -285, 180, 10, Hex("72E0FF"));
            if (game.dashText == null) game.dashText = Text("Dash Status", root, "DASH READY", -500, -310, 210, 28, 17, Color.white);
            game.waveText.fontSize = 18;
        }

        private static Image StatusBar(string name, Transform root, float x, float y, float width, float height, Color color)
        {
            var background = Rect(name + " Background", root, x, y, width, height).gameObject.AddComponent<Image>();
            background.color = Ink; background.raycastTarget = false;
            var fill = Rect(name, background.transform, 0, 0, width, height).gameObject.AddComponent<Image>();
            fill.sprite = solid; fill.type = Image.Type.Filled; fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = 0; fill.fillAmount = 1; fill.color = color; fill.raycastTarget = false;
            return fill;
        }

        [MenuItem("ZPD/Defense/Upgrade Open Defense Scene")]
        public static void UpgradeOpenScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var game = UnityEngine.Object.FindFirstObjectByType<DefenseGame>();
            if (game == null) throw new InvalidOperationException("Open SoloDefense before upgrading.");
            ui = AssetDatabase.LoadAllAssetsAtPath("Assets/Resources/UI/Lobby/cartoon-ui-atlas.png").OfType<Sprite>().ToArray();
            font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Resources/Fonts/NotoSansKR/NotoSansKR-Regular.otf");
            PrepareAssets();
            BuildBeaconVisual(game);
            BuildGameplayActors(game);
            var layout = game.healthText.transform.parent.parent;
            if (game.supplies.cardButtons.Length != 3)
            {
                UnityEngine.Object.DestroyImmediate(game.supplies.shopPanel);
                BuildShop(game, layout);
            }
            game.supplies.baseWeaponSprite = Load("Weapons/weaponR1.png")[0];
            BuildStatusBars(game, layout);
            foreach (var label in game.helpPanel.GetComponentsInChildren<Text>(true))
                if (label.name == "Description") label.text = HelpCopy;
            EditorSceneManager.MarkSceneDirty(game.gameObject.scene);
        }

        private static void BuildBeaconVisual(DefenseGame game)
        {
            const string path = "Assets/Resources/Art/Defense/defense-crystal.png";
            AssetDatabase.ImportAsset(path);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("Missing defense crystal: " + path);
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
            if (sprite == null) throw new InvalidOperationException("Defense crystal sprite import failed: " + path);
            // Keep the gameplay root and its hit feedback; replace only the authored artwork.
            foreach (var name in new[] { "Outline", "Crystal", "Core", "Defense Crystal" })
            {
                var child = game.beacon.Find(name);
                if (child != null) UnityEngine.Object.DestroyImmediate(child.gameObject);
            }
            WorldSprite("Defense Crystal", game.beacon, sprite, 2.4f, 1);
        }

        private static GameObject Modal(string name, Transform parent, out RectTransform panel)
        {
            var overlay = Rect(name, parent, 0, 0, 1280, 720);
            var shade = overlay.gameObject.AddComponent<Image>();
            shade.color = new Color(0.02f, 0.015f, 0.04f, 0.85f);
            panel = Panel("Panel", overlay, 0, 0, 850, 570, "Panel");
            return overlay.gameObject;
        }
        private static RectTransform Panel(string name, Transform parent, float x, float y, float w, float h, string skin)
        {
            var rect = Rect(name, parent, x, y, w, h);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = ui.Single(s => s.name == skin);
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 2.5f;
            return rect;
        }
        private static Button Button(string name, Transform parent, string label, float x, float y, float w, float h, UnityAction callback, string skin)
        {
            var rect = Panel(name, parent, x, y, w, h, skin);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = rect.GetComponent<Image>();
            UnityEventTools.AddPersistentListener(button.onClick, callback);
            Text("Label", rect, label, 0, 2, w - 40, h - 12, 21);
            return button;
        }
        private static Text Text(string name, Transform parent, string value, float x, float y, float w, float h, int size, Color? color = null)
        {
            var text = Rect(name, parent, x, y, w, h).gameObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.fontStyle = size >= 23 ? FontStyle.Bold : FontStyle.Normal;
            text.color = color ?? Ink;
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
            renderer.sharedMaterial = spriteMaterial;
            renderer.sortingOrder = order;
            renderer.transform.localScale = Vector3.one * height / sprite.bounds.size.y;
            return renderer;
        }
        private static Transform Composite(string name, Transform parent, string path, float height, int order)
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            var sprites = Load(path);
            if (sprites.Length == 0) throw new InvalidOperationException("Missing art: " + path);
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
        private static SpriteRenderer Block(string name, Transform parent, Vector2 position, Vector2 size, Color color, int order)
        {
            var renderer = WorldSprite(name, parent, solid, 1, order);
            renderer.transform.localScale = new Vector3(size.x / solid.bounds.size.x, size.y / solid.bounds.size.y, 1);
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
            solid = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            const string materialPath = folder + "/DefenseUnlit.mat";
            spriteMaterial = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (spriteMaterial == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
                if (shader == null) throw new InvalidOperationException("URP 2D unlit shader missing.");
                spriteMaterial = new Material(shader);
                AssetDatabase.CreateAsset(spriteMaterial, materialPath);
            }
        }
        private static Color Hex(string value) { ColorUtility.TryParseHtmlString("#" + value, out var color); return color; }
    }
}
