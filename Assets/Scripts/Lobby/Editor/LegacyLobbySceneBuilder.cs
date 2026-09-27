using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Zpd.Lobby.Editor
{
    public static class LegacyLobbySceneBuilder
    {
        private const string Art = "Assets/Resources/Art/Rgsdev/";
        private static readonly Color color_color_background = Hex("121C2B");
        private static readonly Color color_color_surface = Hex("1D2C40");
        private static readonly Color color_color_card = Hex("293C53");
        private static readonly Color color_color_muted = Hex("A7B8CA");
        private static readonly Color color_color_accent = Hex("83DFC9");
        private static Font font_ui;

        [MenuItem("ZPD/Lobby/Legacy/Create Lobby Scene")]
        public static void CreateLobbyScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            // Resolve required art before touching the open scene.

            LoadSprites("Full body animated characters/Char 1/with hands/idle_0.png");
            font_ui = Resources.Load<Font>("Fonts/NexonLv1/NEXONLv1GothicRegular");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cameraObject = new GameObject("Lobby Camera", typeof(Camera));
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = color_color_background;
            camera.orthographic = true;
            camera.transform.position = new Vector3(0, 0, -10);

            var canvasObject = new GameObject(
                "Lobby Canvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            var root = Rect("Lobby", canvas.transform, 0, 0, 1280, 720);
            var controller = root.gameObject.AddComponent<LegacyLobbyController>();
            var background = Box("Background", canvas.transform, 0, 0, 0, 0, color_color_background);
            Stretch(background.rectTransform);
            background.transform.SetAsFirstSibling();
            BuildHome(root, controller);

            var overlay = Button(
                "Modal Backdrop",
                root,
                "",
                0,
                0,
                1280,
                720,
                new Color(0.02f, 0.04f, 0.08f, 0.8f),
                controller.ClosePanel);
            // Cover the entire canvas, including margins on wider/taller displays.

            overlay.transform.SetParent(canvas.transform, false);
            Stretch((RectTransform)overlay.transform);
            controller.game_object_backdrop = overlay.gameObject;
            var modalRoot = Rect("Panels", canvas.transform, 0, 0, 1280, 720);
            BuildProfile(modalRoot, controller);
            BuildFriends(modalRoot, controller);
            BuildInventory(modalRoot, controller);
            BuildCharacters(modalRoot, controller);
            controller.lobby_panel_profile.gameObject.SetActive(false);
            controller.lobby_panel_friends.gameObject.SetActive(false);
            controller.lobby_panel_inventory.gameObject.SetActive(false);
            controller.lobby_panel_characters.gameObject.SetActive(false);
            overlay.gameObject.SetActive(false);
            var eventObject = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            eventObject.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            eventObject.GetComponent<EventSystem>().firstSelectedGameObject = root.Find("Enter Battle").gameObject;

            LegacyLobbyCartoonStyle.Apply(controller);

            string path = AssetDatabase.GenerateUniqueAssetPath("Assets/Scenes/LegacyLobby.unity");
            EditorSceneManager.SaveScene(scene, path);
            Selection.activeGameObject = canvasObject;
            ValidateLobbyScene();
            Debug.Log("[Lobby] Saved editor-authored scene: " + path);
        }

        private static void BuildHome(RectTransform root, LegacyLobbyController c)
        {
            Box("Header Rule", root, 0, 264, 1184, 2, color_color_card);
            Label("Brand", root, "ZPD / ARENA", -440, 306, 305, 40, 28, Color.white);
            Label("Mode", root, "LOBBY", 395, 306, 395, 36, 16, color_color_muted, TextAnchor.MiddleRight);
            Button(
                "Profile",
                root,
                "PLAYER: --     /     LEVEL: --\nView profile & record",
                -411,
                208,
                360,
                72,
                color_color_surface,
                c.OpenProfile);
            Button("Friends", root, "FRIENDS   /   SOCIAL  >", 437, 208, 310, 72, color_color_surface, c.OpenFriends);
            Label("Heading", root, "READY FOR\nTHE ARENA?", -402, 76, 380, 130, 42, Color.white);
            Label(
                "Intro",
                root,
                "Your next match starts here.\nGear up and make it count.",
                -412,
                -31,
                360,
                66,
                18,
                color_color_muted);
            Label("Character Tag", root, "LOCAL ART PREVIEW", 40, 200, 300, 40, 18, color_color_accent, TextAnchor.MiddleCenter);
            Box("Character Backplate", root, 40, -22, 320, 372, color_color_surface);
            Box("Character Accent", root, 40, -206, 320, 4, color_color_accent);
            Rect("Hero", root, 40, -12, 240, 300);
            Label("Character Name", root, "CHARACTER: --", 40, -243, 300, 38, 22, Color.white, TextAnchor.MiddleCenter);
            var loadout = Box("Current Loadout", root, 442, -22, 300, 300, color_color_surface).transform;
            Label("Title", loadout, "CURRENT LOADOUT", 0, 113, 260, 30, 17, color_color_accent);
            Label("Weapon Placeholder", loadout, "--", 0, 36, 206, 102, 42, color_color_muted, TextAnchor.MiddleCenter);
            Label(
                "Weapon Name",
                loadout,
                "EQUIPPED ITEM: --",
                0,
                -45,
                260,
                32,
                17,
                Color.white,
                TextAnchor.MiddleCenter);
            Label(
                "Loadout Info",
                loadout,
                "Equipment data unavailable",
                0,
                -98,
                260,
                55,
                16,
                color_color_muted,
                TextAnchor.MiddleCenter);
            Button("Inventory", root, "INVENTORY   ^", -411, -286, 360, 62, color_color_card, c.OpenInventory);
            Button(
                "Enter Battle",
                root,
                "SOLO DEFENSE   >",
                437,
                -270,
                310,
                82,
                Hex("EAB574"),
                c.EnterBattle,
                color_color_background,
                25);
            Button("Change Character", root, "CHANGE CHARACTER", 40, -292, 280, 48, color_color_card, c.OpenCharacters, null, 17);
            c.txt_status = Label("Status", root, "Service not connected. Player data: --", -252, -337, 680, 26, 13, color_color_muted);
            Label(
                "Art Credit",
                root,
                "Character art: Rgsdev / CC0",
                437,
                -337,
                310,
                26,
                12,
                color_color_muted,
                TextAnchor.MiddleRight);
        }

        private static void BuildProfile(Transform root, LegacyLobbyController c)
        {
            var panel = Panel("User Profile", root, 0, 0, 650, 476, new Vector2(0, -60), out c.View.lobby_panel_profile);
            Label("Title", panel, "PLAYER PROFILE", -60, 186, 440, 44, 28, Color.white);
            Button("Close", panel, "X", 271, 186, 44, 44, color_color_card, c.ClosePanel);
            Rect("Avatar", panel, -191, 21, 130, 164);
            Label("Player", panel, "PLAYER: --", 80, 89, 310, 45, 30, Color.white);
            Label("Level", panel, "LEVEL: --", 80, 43, 310, 34, 18, color_color_accent);
            Label(
                "Record",
                panel,
                "MATCHES: --\nWINS: --   /   LOSSES: --\nWIN RATE: --",
                80,
                -43,
                310,
                112,
                20,
                Color.white);
            Label(
                "Data Notice",
                panel,
                "Profile data unavailable. Portrait is a local art preview.\nNo player record has been received.",
                0,
                -169,
                560,
                66,
                16,
                color_color_muted,
                TextAnchor.MiddleCenter);
        }

        private static void BuildFriends(Transform root, LegacyLobbyController c)
        {
            var panel = Panel("Friends Drawer", root, 347, 0, 570, 704, new Vector2(600, 0), out c.View.lobby_panel_friends);
            Label("Title", panel, "SOCIAL", -47, 302, 420, 42, 30, Color.white);
            Button("Close", panel, "X", 239, 302, 44, 44, color_color_card, c.ClosePanel);
            Label("Subtitle", panel, "FRIENDS: --   /   HEARTS: --", -83, 253, 348, 30, 15, color_color_muted);
            Button("Refresh Social", panel, "REFRESH", 186, 253, 142, 40, color_color_card, c.RefreshSocial, null, 15);
            c.btn_friend_tabs = new[]
            {
                Button("My Friends Tab", panel, "FRIENDS", -195, 196, 122, 48, color_color_card, c.ShowMyFriends, null, 13),
                Button("Search Tab", panel, "SEARCH", -65, 196, 122, 48, color_color_card, c.ShowSearch, null, 13),
                Button("Recent Tab", panel, "SUGGESTED", 65, 196, 122, 48, color_color_card, c.ShowRecent, null, 13),
                Button("Hearts Tab", panel, "HEARTS", 195, 196, 122, 48, color_color_card, c.ShowHearts, null, 13)
            };
            var mine = Rect("My Friends Page", panel, 0, -50, 514, 414);
            var search = Rect("Search Page", panel, 0, -50, 514, 414);
            var recent = Rect("Friend Suggestions Page", panel, 0, -50, 514, 414);
            var hearts = Rect("Heart Inbox Page", panel, 0, -50, 514, 414);
            c.game_object_friend_pages = new[]
            {
                mine.gameObject,
                search.gameObject,
                recent.gameObject,
                hearts.gameObject
            };
            c.txt_social_status = Label(
                "Preview Notice",
                panel,
                "Service not connected. Player data and eligibility: --",
                0,
                -300,
                504,
                40,
                13,
                color_color_muted);
            c.lobby_heart_automation = c.gameObject.AddComponent<LobbyHeartAutomation>();
            c.lobby_heart_automation.txt_feedback = c.txt_social_status;
            c.txt_social_status.text = "Hearts sync automatically here. Service not connected.";
            Label(
                "Data State",
                mine,
                "Available hearts are received and sent automatically.\nFriend and heart data: --",
                0,
                178,
                496,
                52,
                15,
                color_color_muted);
            SocialRows(mine, c.txt_social_status, LobbySocialAction.SendHeart, "AUTO", 88);
            Label(
                "Data State",
                recent,
                "Recently online players: --\nRefresh to find players and send a friend request.",
                0,
                175,
                496,
                58,
                15,
                color_color_muted);
            SocialRows(recent, c.txt_social_status, LobbySocialAction.SendFriendRequest, "ADD FRIEND", 88);
            Label(
                "Data State",
                hearts,
                "Automatic receive / send history: --\nEligible hearts sync when you open or refresh Social.",
                0,
                175,
                496,
                58,
                15,
                color_color_muted);
            SocialRows(hearts, c.txt_social_status, LobbySocialAction.ReceiveHeart, "AUTO", 88);
            var inputBox = Box("Player Search", search, -61, 174, 378, 50, color_color_background);
            c.input_search = inputBox.gameObject.AddComponent<InputField>();
            c.input_search.characterLimit = 32;
            c.input_search.lineType = InputField.LineType.SingleLine;
            var inputText = Label("Text", inputBox.transform, "", 0, 0, 334, 42, 17, Color.white);
            inputText.supportRichText = false;
            var placeholder = Label(
                "Placeholder",
                inputBox.transform,
                "Enter a player name...",
                0,
                0,
                334,
                42,
                16,
                color_color_muted);
            c.input_search.textComponent = inputText;
            c.input_search.placeholder = placeholder;
            c.input_search.targetGraphic = inputBox;
            Button("Search Players", search, "GO", 194, 174, 112, 50, color_color_accent, c.SearchFriends, color_color_background);
            c.txt_search_status = Label(
                "Search Status",
                search,
                "Search results unavailable until connected.",
                0,
                110,
                496,
                42,
                14,
                color_color_muted);
            SocialRows(search, c.txt_search_status, LobbySocialAction.SendFriendRequest, "ADD FRIEND", 33, 2);
            search.gameObject.SetActive(false);
            recent.gameObject.SetActive(false);
            hearts.gameObject.SetActive(false);
        }

        private static void SocialRows(
            Transform parent,
            Text feedback,
            LobbySocialAction action,
            string actionLabel,
            float top,
            int count = 3)
        {
            for (int i = 0; i < count; i++)
            {
                var row = Box("Player Placeholder " + (i + 1), parent, 0, top - i * 96, 498, 86, color_color_card).rectTransform;
                var slot = row.gameObject.AddComponent<LobbySocialSlot>();
                slot.social_action = action;
                slot.txt_feedback = feedback;
                slot.txt_player_name = Label("Player Name", row, "--", -81, 13, 270, 26, 18, Color.white);
                slot.txt_detail = Label("Player Detail", row, "Player data: --", -81, -14, 270, 24, 13, color_color_muted);

                if (action == LobbySocialAction.SendFriendRequest)
                {
                    slot.btn_action = Button(
                        "Player Action",
                        row,
                        actionLabel,
                        160,
                        0,
                        148,
                        48,
                        color_color_card,
                        slot.RequestAction,
                        null,
                        12);
                }
                else
                {
                    Label(
                        "Automatic Heart Status",
                        row,
                        "AUTO / --",
                        160,
                        0,
                        148,
                        48,
                        13,
                        color_color_muted,
                        TextAnchor.MiddleCenter);
                }

                slot.Clear();
            }
        }

        private static void BuildInventory(Transform root, LegacyLobbyController c)
        {
            var panel = Panel("Inventory Drawer", root, 0, -84, 1264, 536, new Vector2(0, -560), out c.View.lobby_panel_inventory);
            Label("Title", panel, "INVENTORY", -340, 218, 520, 44, 30, Color.white);
            Button("Close", panel, "X", 586, 218, 44, 44, color_color_card, c.ClosePanel);
            Label("Equipped Title", panel, "EQUIPPED: --", -439, 153, 322, 32, 17, color_color_accent);
            var equipment = Box("Equipped Weapon", panel, -439, -10, 322, 270, color_color_background).transform;
            Label("Weapon Placeholder", equipment, "--", 0, 39, 256, 126, 42, color_color_muted, TextAnchor.MiddleCenter);
            Label("Name", equipment, "ITEM: --", 0, -52, 270, 35, 23, Color.white, TextAnchor.MiddleCenter);
            Label("Slot", equipment, "EQUIPMENT DATA UNAVAILABLE", 0, -96, 290, 30, 14, color_color_accent, TextAnchor.MiddleCenter);
            Label("Items Title", panel, "COLLECTION: -- / DATA UNAVAILABLE", 188, 153, 790, 32, 17, color_color_accent);
            var content = ScrollContent("Item Grid", panel, 188, -20, 800, 296, 800, 440);
            var grid = content.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(184, 132);
            grid.spacing = new Vector2(14, 14);
            grid.padding = new RectOffset(2, 2, 2, 2);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 4;

            for (int i = 0; i < 12; i++)
            {
                string id = "Placeholder " + (i + 1).ToString("00");
                var item = Button("Item " + id, content, "", 0, 0, 184, 132, color_color_card, null);
                UnityEventTools.AddStringPersistentListener(item.onClick, c.InspectItem, "");
                item.interactable = false;
                Label("Icon", item.transform, "--", 0, 13, 154, 70, 28, color_color_muted, TextAnchor.MiddleCenter);
                Label("Name", item.transform, "ITEM: --", 0, -39, 166, 26, 14, Color.white, TextAnchor.MiddleCenter);
            }

            c.txt_item_details = Label(
                "Item Details",
                panel,
                "Inventory data unavailable. Item slots do not indicate owned items.",
                80,
                -211,
                1040,
                40,
                16,
                color_color_muted);
        }

        private static void BuildCharacters(Transform root, LegacyLobbyController c)
        {
            var panel = Panel("Character Selection", root, 0, 0, 1080, 620, new Vector2(0, -100), out c.View.lobby_panel_characters);
            var picker = panel.gameObject.AddComponent<LobbyCharacterPicker>();
            c.lobby_character_picker = picker;
            Label("Title", panel, "CHANGE CHARACTER", -115, 255, 770, 44, 30, Color.white);
            Button("Close", panel, "X", 490, 255, 44, 44, color_color_card, c.ClosePanel);
            Label(
                "Subtitle",
                panel,
                "Local artwork previews / ownership and active character require service data.",
                0,
                199,
                996,
                42,
                17,
                color_color_muted);
            picker.character_slots = new LobbyCharacterPicker.Slot[4];
            var hero = c.transform.Find("Hero");
            var avatar = c.lobby_panel_profile.transform.Find("Avatar");

            for (int i = 0; i < 4; i++)
            {
                string path = "Full body animated characters/Char " + (i + 1) + "/with hands/idle_0.png";
                var card = Button("Character Option " + (i + 1), panel, "", -375 + i * 250, 22, 226, 270, color_color_card, null);
                UnityEventTools.AddIntPersistentListener(card.onClick, picker.Preview, i);
                Composite("Character Art", card.transform, path, 0, 23, 162, 170);
                var previewLabel = Label(
                    "Preview Label",
                    card.transform,
                    "PREVIEW " + (i + 1).ToString("00"),
                    0,
                    -78,
                    190,
                    28,
                    15,
                    Color.white,
                    TextAnchor.MiddleCenter);
                var ownership = Label(
                    "Ownership",
                    card.transform,
                    "Ownership: --",
                    0,
                    -105,
                    190,
                    24,
                    14,
                    color_color_muted,
                    TextAnchor.MiddleCenter);
                string child = "Character " + (i + 1);
                Composite(child, hero, path, 0, 0, 240, 300);
                Composite(child, avatar, path, 0, 0, 130, 164);
                picker.character_slots[i] = new LobbyCharacterPicker.Slot
                {
                    art_key = "char-" + (i + 1),
                    txt_state = ownership,
                    txt_preview = previewLabel,
                    game_object_lobby_art = hero.Find(child).gameObject,
                    game_object_profile_art = avatar.Find(child).gameObject
                };
            }

            picker.txt_feedback = Label(
                "Character Status",
                panel,
                "Ownership and current character: --",
                -170,
                -197,
                660,
                88,
                17,
                color_color_muted);
            picker.btn_apply = Button(
                "Apply Character",
                panel,
                "APPLY CHARACTER",
                348,
                -251,
                300,
                48,
                color_color_accent,
                picker.RequestChange,
                null,
                17);
            picker.txt_current_character = c.transform.Find("Character Name").GetComponent<Text>();
            picker.txt_home_tag = c.transform.Find("Character Tag").GetComponent<Text>();
            picker.ResetServerState();
        }

        private static RectTransform Panel(
            string name,
            Transform parent,
            float x,
            float y,
            float w,
            float h,
            Vector2 offset,
            out LobbyPanel behavior)
        {
            var rect = Box(name, parent, x, y, w, h, color_color_surface).rectTransform;
            behavior = rect.gameObject.AddComponent<LobbyPanel>();
            behavior.hidden_offset = offset;
            return rect;
        }

        private static RectTransform ScrollContent(
            string name,
            Transform parent,
            float x,
            float y,
            float w,
            float h,
            float contentW,
            float contentH)
        {
            var scrollRect = Rect(name, parent, x, y, w, h);
            var scroll = scrollRect.gameObject.AddComponent<ScrollRect>();
            var viewport = Box("Viewport", scrollRect, 0, 0, w, h, color_color_background).rectTransform;
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = Rect("Content", viewport, 0, 0, contentW, contentH);
            content.anchorMin = content.anchorMax = new Vector2(0.5f, 1);
            content.pivot = new Vector2(0.5f, 1);
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 28;
            return content;
        }

        private static Sprite[] LoadSprites(string relativePath)
        {
            var sprites = AssetDatabase.LoadAllAssetsAtPath(Art + relativePath).OfType<Sprite>().OrderBy(s => s.name).ToArray();

            if (sprites.Length == 0)
            {
                throw new InvalidOperationException("Missing sprite: " + Art + relativePath);
            }

            return sprites;
        }

        private static void Composite(string name, Transform parent, string path, float x, float y, float w, float h)
        {
            var holder = Rect(name, parent, x, y, w, h);
            var sprites = LoadSprites(path);
            // The source sheet has separate body/feet slices. Preserve their sheet-relative placement.

            float minX = sprites.Min(s => s.rect.xMin), maxX = sprites.Max(s => s.rect.xMax);
            float minY = sprites.Min(s => s.rect.yMin), maxY = sprites.Max(s => s.rect.yMax);
            Vector2 center = new Vector2((minX + maxX) / 2, (minY + maxY) / 2);
            float scale = Mathf.Min(w / (maxX - minX), h / (maxY - minY));

            foreach (var sprite in sprites)
            {
                Vector2 pos = (sprite.rect.center - center) * scale;
                var part = Box(
                    sprite.name,
                    holder,
                    pos.x,
                    pos.y,
                    sprite.rect.width * scale,
                    sprite.rect.height * scale,
                    Color.white);
                part.sprite = sprite;
                part.raycastTarget = false;
            }
        }

        private static void SpriteImage(string name, Transform parent, string path, float x, float y, float w, float h)
        {
            var img = Box(name, parent, x, y, w, h, Color.white);
            img.sprite = LoadSprites(path)[0];
            img.preserveAspect = true;
            img.raycastTarget = false;
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

        private static Image Box(string name, Transform parent, float x, float y, float w, float h, Color color)
        {
            var image = Rect(name, parent, x, y, w, h).gameObject.AddComponent<Image>();
            image.color = color;
            return image;
        }

        private static Text Label(
            string name,
            Transform parent,
            string value,
            float x,
            float y,
            float w,
            float h,
            int size,
            Color color,
            TextAnchor alignment = TextAnchor.MiddleLeft)
        {
            var text = Rect(name, parent, x, y, w, h).gameObject.AddComponent<Text>();
            text.font = font_ui;
            text.text = value;
            text.fontSize = size;
            text.color = color;
            text.alignment = alignment;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            return text;
        }

        private static Button Button(
            string name,
            Transform parent,
            string label,
            float x,
            float y,
            float w,
            float h,
            Color color,
            UnityAction action,
            Color? textColor = null,
            int size = 19)
        {
            var image = Box(name, parent, x, y, w, h, color);
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f);
            colors.selectedColor = new Color(1.15f, 1.15f, 1.15f);
            colors.pressedColor = new Color(0.75f, 0.85f, 0.9f);
            colors.disabledColor = color_color_accent;
            button.colors = colors;

            if (label.Length > 0)
            {
                Label(
                    "Label",
                    image.transform,
                    label,
                    0,
                    0,
                    w - 24,
                    h - 8,
                    size,
                    textColor ?? Color.white,
                    TextAnchor.MiddleCenter);
            }

            if (action != null)
            {
                UnityEventTools.AddPersistentListener(button.onClick, action);
            }

            return button;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private static Color Hex(string value)
        {
            ColorUtility.TryParseHtmlString("#" + value, out var color);
            return color;
        }

        [MenuItem("ZPD/Lobby/Legacy/Validate Open Lobby Scene")]
        public static void ValidateLobbyScene()
        {
            var c = UnityEngine.Object.FindFirstObjectByType<LegacyLobbyController>();

            if (c == null || c.lobby_panel_profile == null || c.lobby_panel_friends == null || c.lobby_panel_inventory == null || c.lobby_panel_characters == null || c.lobby_character_picker == null || c.game_object_backdrop == null)
            {
                throw new InvalidOperationException("Lobby scene has missing references.");
            }

            var canvas = c.GetComponentInParent<Canvas>();

            foreach (var button in canvas.GetComponentsInChildren<Button>(true))
            {
                if (button.onClick.GetPersistentEventCount() == 0 || button.onClick.GetPersistentTarget(0) == null)
                {
                    throw new InvalidOperationException("Unwired button: " + button.name);
                }
            }

            if (c.game_object_friend_pages.Length != 4 || c.btn_friend_tabs.Length != 4 || c.input_search == null || c.txt_search_status == null || c.txt_item_details == null)
            {
                throw new InvalidOperationException("Incomplete lobby panels.");
            }

            if (c.lobby_panel_inventory.GetComponentInChildren<GridLayoutGroup>(true).transform.childCount != 12)
            {
                throw new InvalidOperationException("Expected 12 authored item slots.");
            }

            if (c.lobby_character_picker.character_slots.Length != 4 || c.lobby_character_picker.btn_apply.interactable)
            {
                throw new InvalidOperationException(
                    "Expected four local previews and disabled character apply before server data.");
            }

            if (c.lobby_heart_automation == null || c.lobby_panel_friends.GetComponentsInChildren<LobbySocialSlot>(true).Any(s => s.btn_action != null && s.btn_action.interactable))
            {
                throw new InvalidOperationException("Social actions must start disabled without service data.");
            }

            if (UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length != 1)
            {
                throw new InvalidOperationException("Expected one EventSystem.");
            }

            Debug.Log(
                "[Lobby] Validation passed: references, persistent buttons, four social pages, 12 placeholder item slots, character picker, EventSystem.");
        }
    }
}
