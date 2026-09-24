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
    public static class LobbySceneBuilder
    {
        private const string Art = "Assets/Resources/Art/Rgsdev/";
        private static readonly Color Background = Hex("121C2B");
        private static readonly Color Surface = Hex("1D2C40");
        private static readonly Color Card = Hex("293C53");
        private static readonly Color Muted = Hex("A7B8CA");
        private static readonly Color Accent = Hex("83DFC9");
        private static Font font;

        [MenuItem("ZPD/Lobby/Create Lobby Scene")]
        public static void CreateLobbyScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            // Resolve required art before touching the open scene.
            LoadSprites("Full body animated characters/Char 1/with hands/idle_0.png");
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cameraObject = new GameObject("Lobby Camera", typeof(Camera));
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Background;
            camera.orthographic = true;
            camera.transform.position = new Vector3(0, 0, -10);

            var canvasObject = new GameObject("Lobby Canvas", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            var root = Rect("Lobby", canvas.transform, 0, 0, 1280, 720);
            var controller = root.gameObject.AddComponent<LobbyController>();
            var background = Box("Background", canvas.transform, 0, 0, 0, 0, Background);
            Stretch(background.rectTransform);
            background.transform.SetAsFirstSibling();
            BuildHome(root, controller);

            var overlay = Button("Modal Backdrop", root, "", 0, 0, 1280, 720,
                new Color(0.02f, 0.04f, 0.08f, 0.8f), controller.ClosePanel);
            // Cover the entire canvas, including margins on wider/taller displays.
            overlay.transform.SetParent(canvas.transform, false);
            Stretch((RectTransform)overlay.transform);
            controller.backdrop = overlay.gameObject;
            var modalRoot = Rect("Panels", canvas.transform, 0, 0, 1280, 720);
            BuildProfile(modalRoot, controller);
            BuildFriends(modalRoot, controller);
            BuildInventory(modalRoot, controller);
            BuildCharacters(modalRoot, controller);
            controller.profile.gameObject.SetActive(false);
            controller.friends.gameObject.SetActive(false);
            controller.inventory.gameObject.SetActive(false);
            controller.characters.gameObject.SetActive(false);
            overlay.gameObject.SetActive(false);
            var eventObject = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            eventObject.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            eventObject.GetComponent<EventSystem>().firstSelectedGameObject = root.Find("Enter Battle").gameObject;

            LobbyCartoonStyle.Apply(controller);

            string path = AssetDatabase.GenerateUniqueAssetPath("Assets/Scenes/Lobby.unity");
            EditorSceneManager.SaveScene(scene, path);
            Selection.activeGameObject = canvasObject;
            ValidateLobbyScene();
            Debug.Log("[Lobby] Saved editor-authored scene: " + path);
        }

        private static void BuildHome(RectTransform root, LobbyController c)
        {
            Box("Header Rule", root, 0, 264, 1184, 2, Card);
            Label("Brand", root, "ZPD / ARENA", -440, 306, 305, 40, 28, Color.white);
            Label("Mode", root, "LOBBY", 395, 306, 395, 36, 16, Muted, TextAnchor.MiddleRight);
            Button("Profile", root, "PLAYER: --     /     LEVEL: --\nView profile & record", -411, 208, 360, 72, Surface, c.OpenProfile);
            Button("Friends", root, "FRIENDS   /   SOCIAL  >", 437, 208, 310, 72, Surface, c.OpenFriends);
            Label("Heading", root, "READY FOR\nTHE ARENA?", -402, 76, 380, 130, 42, Color.white);
            Label("Intro", root, "Your next match starts here.\nGear up and make it count.", -412, -31, 360, 66, 18, Muted);
            Label("Character Tag", root, "LOCAL ART PREVIEW", 40, 200, 300, 40, 18, Accent, TextAnchor.MiddleCenter);
            Box("Character Backplate", root, 40, -22, 320, 372, Surface);
            Box("Character Accent", root, 40, -206, 320, 4, Accent);
            Rect("Hero", root, 40, -12, 240, 300);
            Label("Character Name", root, "CHARACTER: --", 40, -243, 300, 38, 22, Color.white, TextAnchor.MiddleCenter);
            var loadout = Box("Current Loadout", root, 442, -22, 300, 300, Surface).transform;
            Label("Title", loadout, "CURRENT LOADOUT", 0, 113, 260, 30, 17, Accent);
            Label("Weapon Placeholder", loadout, "--", 0, 36, 206, 102, 42, Muted, TextAnchor.MiddleCenter);
            Label("Weapon Name", loadout, "EQUIPPED ITEM: --", 0, -45, 260, 32, 17, Color.white, TextAnchor.MiddleCenter);
            Label("Loadout Info", loadout, "Equipment data unavailable", 0, -98, 260, 55, 16, Muted, TextAnchor.MiddleCenter);
            Button("Inventory", root, "INVENTORY   ^", -411, -286, 360, 62, Card, c.OpenInventory);
            Button("Enter Battle", root, "ENTER BATTLE   >", 437, -270, 310, 82, Hex("EAB574"), c.EnterBattle, Background, 25);
            Button("Change Character", root, "CHANGE CHARACTER", 40, -292, 280, 48, Card, c.OpenCharacters, null, 17);
            c.status = Label("Status", root, "Service not connected. Player data: --", -252, -337, 680, 26, 13, Muted);
            Label("Art Credit", root, "Character art: Rgsdev / CC0", 437, -337, 310, 26, 12, Muted, TextAnchor.MiddleRight);
        }

        private static void BuildProfile(Transform root, LobbyController c)
        {
            var panel = Panel("User Profile", root, 0, 0, 650, 476, new Vector2(0, -60), out c.profile);
            Label("Title", panel, "PLAYER PROFILE", -60, 186, 440, 44, 28, Color.white);
            Button("Close", panel, "X", 271, 186, 44, 44, Card, c.ClosePanel);
            Rect("Avatar", panel, -191, 21, 130, 164);
            Label("Player", panel, "PLAYER: --", 80, 89, 310, 45, 30, Color.white);
            Label("Level", panel, "LEVEL: --", 80, 43, 310, 34, 18, Accent);
            Label("Record", panel, "MATCHES: --\nWINS: --   /   LOSSES: --\nWIN RATE: --", 80, -43, 310, 112, 20, Color.white);
            Label("Data Notice", panel, "Profile data unavailable. Portrait is a local art preview.\nNo player record has been received.", 0, -169, 560, 66, 16, Muted, TextAnchor.MiddleCenter);
        }

        private static void BuildFriends(Transform root, LobbyController c)
        {
            var panel = Panel("Friends Drawer", root, 347, 0, 570, 704, new Vector2(600, 0), out c.friends);
            Label("Title", panel, "SOCIAL", -47, 302, 420, 42, 30, Color.white);
            Button("Close", panel, "X", 239, 302, 44, 44, Card, c.ClosePanel);
            Label("Subtitle", panel, "FRIENDS: --   /   HEARTS: --", -83, 253, 348, 30, 15, Muted);
            Button("Refresh Social", panel, "REFRESH", 186, 253, 142, 40, Card, c.RefreshSocial, null, 15);
            c.friendTabs = new[] {
                Button("My Friends Tab", panel, "FRIENDS", -195, 196, 122, 48, Card, c.ShowMyFriends, null, 13),
                Button("Search Tab", panel, "SEARCH", -65, 196, 122, 48, Card, c.ShowSearch, null, 13),
                Button("Recent Tab", panel, "SUGGESTED", 65, 196, 122, 48, Card, c.ShowRecent, null, 13),
                Button("Hearts Tab", panel, "HEARTS", 195, 196, 122, 48, Card, c.ShowHearts, null, 13)
            };
            var mine = Rect("My Friends Page", panel, 0, -50, 514, 414);
            var search = Rect("Search Page", panel, 0, -50, 514, 414);
            var recent = Rect("Friend Suggestions Page", panel, 0, -50, 514, 414);
            var hearts = Rect("Heart Inbox Page", panel, 0, -50, 514, 414);
            c.friendPages = new[] { mine.gameObject, search.gameObject, recent.gameObject, hearts.gameObject };
            c.socialStatus = Label("Preview Notice", panel, "Service not connected. Player data and eligibility: --", 0, -300, 504, 40, 13, Muted);
            c.heartAutomation = c.gameObject.AddComponent<LobbyHeartAutomation>();
            c.heartAutomation.feedback = c.socialStatus;
            c.socialStatus.text = "Hearts sync automatically here. Service not connected.";
            Label("Data State", mine, "Available hearts are received and sent automatically.\nFriend and heart data: --", 0, 178, 496, 52, 15, Muted);
            SocialRows(mine, c.socialStatus, LobbySocialAction.SendHeart, "AUTO", 88);
            Label("Data State", recent, "Recently online players: --\nRefresh to find players and send a friend request.", 0, 175, 496, 58, 15, Muted);
            SocialRows(recent, c.socialStatus, LobbySocialAction.SendFriendRequest, "ADD FRIEND", 88);
            Label("Data State", hearts, "Automatic receive / send history: --\nEligible hearts sync when you open or refresh Social.", 0, 175, 496, 58, 15, Muted);
            SocialRows(hearts, c.socialStatus, LobbySocialAction.ReceiveHeart, "AUTO", 88);
            var inputBox = Box("Player Search", search, -61, 174, 378, 50, Background);
            c.searchInput = inputBox.gameObject.AddComponent<InputField>();
            c.searchInput.characterLimit = 32;
            c.searchInput.lineType = InputField.LineType.SingleLine;
            var inputText = Label("Text", inputBox.transform, "", 0, 0, 334, 42, 17, Color.white);
            inputText.supportRichText = false;
            var placeholder = Label("Placeholder", inputBox.transform, "Enter a player name...", 0, 0, 334, 42, 16, Muted);
            c.searchInput.textComponent = inputText;
            c.searchInput.placeholder = placeholder;
            c.searchInput.targetGraphic = inputBox;
            Button("Search Players", search, "GO", 194, 174, 112, 50, Accent, c.SearchFriends, Background);
            c.searchStatus = Label("Search Status", search, "Search results unavailable until connected.", 0, 110, 496, 42, 14, Muted);
            SocialRows(search, c.searchStatus, LobbySocialAction.SendFriendRequest, "ADD FRIEND", 33, 2);
            search.gameObject.SetActive(false);
            recent.gameObject.SetActive(false);
            hearts.gameObject.SetActive(false);
        }

        private static void SocialRows(Transform parent, Text feedback, LobbySocialAction action, string actionLabel, float top, int count = 3)
        {
            for (int i = 0; i < count; i++)
            {
                var row = Box("Player Placeholder " + (i + 1), parent, 0, top - i * 96, 498, 86, Card).rectTransform;
                var slot = row.gameObject.AddComponent<LobbySocialSlot>();
                slot.action = action;
                slot.feedback = feedback;
                slot.playerName = Label("Player Name", row, "--", -81, 13, 270, 26, 18, Color.white);
                slot.detail = Label("Player Detail", row, "Player data: --", -81, -14, 270, 24, 13, Muted);
                if (action == LobbySocialAction.SendFriendRequest)
                    slot.actionButton = Button("Player Action", row, actionLabel, 160, 0, 148, 48, Card, slot.RequestAction, null, 12);
                else
                    Label("Automatic Heart Status", row, "AUTO / --", 160, 0, 148, 48, 13, Muted, TextAnchor.MiddleCenter);
                slot.Clear();
            }
        }
        private static void BuildInventory(Transform root, LobbyController c)
        {
            var panel = Panel("Inventory Drawer", root, 0, -84, 1264, 536, new Vector2(0, -560), out c.inventory);
            Label("Title", panel, "INVENTORY", -340, 218, 520, 44, 30, Color.white);
            Button("Close", panel, "X", 586, 218, 44, 44, Card, c.ClosePanel);
            Label("Equipped Title", panel, "EQUIPPED: --", -439, 153, 322, 32, 17, Accent);
            var equipment = Box("Equipped Weapon", panel, -439, -10, 322, 270, Background).transform;
            Label("Weapon Placeholder", equipment, "--", 0, 39, 256, 126, 42, Muted, TextAnchor.MiddleCenter);
            Label("Name", equipment, "ITEM: --", 0, -52, 270, 35, 23, Color.white, TextAnchor.MiddleCenter);
            Label("Slot", equipment, "EQUIPMENT DATA UNAVAILABLE", 0, -96, 290, 30, 14, Accent, TextAnchor.MiddleCenter);
            Label("Items Title", panel, "COLLECTION: -- / DATA UNAVAILABLE", 188, 153, 790, 32, 17, Accent);
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
                var item = Button("Item " + id, content, "", 0, 0, 184, 132, Card, null);
                UnityEventTools.AddStringPersistentListener(item.onClick, c.InspectItem, "");
                item.interactable = false;
                Label("Icon", item.transform, "--", 0, 13, 154, 70, 28, Muted, TextAnchor.MiddleCenter);
                Label("Name", item.transform, "ITEM: --", 0, -39, 166, 26, 14, Color.white, TextAnchor.MiddleCenter);

            }
            c.itemDetails = Label("Item Details", panel, "Inventory data unavailable. Item slots do not indicate owned items.", 80, -211, 1040, 40, 16, Muted);
        }

        private static void BuildCharacters(Transform root, LobbyController c)
        {
            var panel = Panel("Character Selection", root, 0, 0, 1080, 620, new Vector2(0, -100), out c.characters);
            var picker = panel.gameObject.AddComponent<LobbyCharacterPicker>();
            c.characterPicker = picker;
            Label("Title", panel, "CHANGE CHARACTER", -115, 255, 770, 44, 30, Color.white);
            Button("Close", panel, "X", 490, 255, 44, 44, Card, c.ClosePanel);
            Label("Subtitle", panel, "Local artwork previews / ownership and active character require service data.", 0, 199, 996, 42, 17, Muted);
            picker.slots = new LobbyCharacterPicker.Slot[4];
            var hero = c.transform.Find("Hero");
            var avatar = c.profile.transform.Find("Avatar");
            for (int i = 0; i < 4; i++)
            {
                string path = "Full body animated characters/Char " + (i + 1) + "/with hands/idle_0.png";
                var card = Button("Character Option " + (i + 1), panel, "", -375 + i * 250, 22, 226, 270, Card, null);
                UnityEventTools.AddIntPersistentListener(card.onClick, picker.Preview, i);
                Composite("Character Art", card.transform, path, 0, 23, 162, 170);
                var previewLabel = Label("Preview Label", card.transform, "PREVIEW " + (i + 1).ToString("00"), 0, -78, 190, 28, 15, Color.white, TextAnchor.MiddleCenter);
                var ownership = Label("Ownership", card.transform, "Ownership: --", 0, -105, 190, 24, 14, Muted, TextAnchor.MiddleCenter);
                string child = "Character " + (i + 1);
                Composite(child, hero, path, 0, 0, 240, 300);
                Composite(child, avatar, path, 0, 0, 130, 164);
                picker.slots[i] = new LobbyCharacterPicker.Slot {
                    artKey = "char-" + (i + 1), state = ownership, previewLabel = previewLabel,
                    lobbyArt = hero.Find(child).gameObject, profileArt = avatar.Find(child).gameObject
                };
            }
            picker.feedback = Label("Character Status", panel, "Ownership and current character: --", -170, -197, 660, 88, 17, Muted);
            picker.applyButton = Button("Apply Character", panel, "APPLY CHARACTER", 348, -251, 300, 48, Accent, picker.RequestChange, null, 17);
            picker.currentCharacter = c.transform.Find("Character Name").GetComponent<Text>();
            picker.homeTag = c.transform.Find("Character Tag").GetComponent<Text>();
            picker.ResetServerState();
        }
        private static RectTransform Panel(string name, Transform parent, float x, float y, float w, float h, Vector2 offset, out LobbyPanel behavior)
        {
            var rect = Box(name, parent, x, y, w, h, Surface).rectTransform;
            behavior = rect.gameObject.AddComponent<LobbyPanel>();
            behavior.hiddenOffset = offset;
            return rect;
        }

        private static RectTransform ScrollContent(string name, Transform parent, float x, float y, float w, float h, float contentW, float contentH)
        {
            var scrollRect = Rect(name, parent, x, y, w, h);
            var scroll = scrollRect.gameObject.AddComponent<ScrollRect>();
            var viewport = Box("Viewport", scrollRect, 0, 0, w, h, Background).rectTransform;
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
            if (sprites.Length == 0) throw new InvalidOperationException("Missing sprite: " + Art + relativePath);
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
                var part = Box(sprite.name, holder, pos.x, pos.y, sprite.rect.width * scale, sprite.rect.height * scale, Color.white);
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

        private static Text Label(string name, Transform parent, string value, float x, float y, float w, float h, int size, Color color, TextAnchor alignment = TextAnchor.MiddleLeft)
        {
            var text = Rect(name, parent, x, y, w, h).gameObject.AddComponent<Text>();
            text.font = font;
            text.text = value;
            text.fontSize = size;
            text.color = color;
            text.alignment = alignment;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            return text;
        }

        private static Button Button(string name, Transform parent, string label, float x, float y, float w, float h, Color color, UnityAction action, Color? textColor = null, int size = 19)
        {
            var image = Box(name, parent, x, y, w, h, color);
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f);
            colors.selectedColor = new Color(1.15f, 1.15f, 1.15f);
            colors.pressedColor = new Color(0.75f, 0.85f, 0.9f);
            colors.disabledColor = Accent;
            button.colors = colors;
            if (label.Length > 0) Label("Label", image.transform, label, 0, 0, w - 24, h - 8, size, textColor ?? Color.white, TextAnchor.MiddleCenter);
            if (action != null) UnityEventTools.AddPersistentListener(button.onClick, action);
            return button;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private static Color Hex(string value) { ColorUtility.TryParseHtmlString("#" + value, out var color); return color; }

        [MenuItem("ZPD/Lobby/Validate Open Lobby Scene")]
        public static void ValidateLobbyScene()
        {
            var c = UnityEngine.Object.FindFirstObjectByType<LobbyController>();
            if (c == null || c.profile == null || c.friends == null || c.inventory == null || c.characters == null || c.characterPicker == null || c.backdrop == null)
                throw new InvalidOperationException("Lobby scene has missing references.");
            var canvas = c.GetComponentInParent<Canvas>();
            foreach (var button in canvas.GetComponentsInChildren<Button>(true))
                if (button.onClick.GetPersistentEventCount() == 0 || button.onClick.GetPersistentTarget(0) == null)
                    throw new InvalidOperationException("Unwired button: " + button.name);
            if (c.friendPages.Length != 4 || c.friendTabs.Length != 4 || c.searchInput == null || c.searchStatus == null || c.itemDetails == null)
                throw new InvalidOperationException("Incomplete lobby panels.");
            if (c.inventory.GetComponentInChildren<GridLayoutGroup>(true).transform.childCount != 12)
                throw new InvalidOperationException("Expected 12 authored item slots.");
            if (c.characterPicker.slots.Length != 4 || c.characterPicker.applyButton.interactable)
                throw new InvalidOperationException("Expected four local previews and disabled character apply before server data.");
            if (c.heartAutomation == null || c.friends.GetComponentsInChildren<LobbySocialSlot>(true).Any(s => s.actionButton != null && s.actionButton.interactable))
                throw new InvalidOperationException("Social actions must start disabled without service data.");
            if (UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length != 1)
                throw new InvalidOperationException("Expected one EventSystem.");
            Debug.Log("[Lobby] Validation passed: references, persistent buttons, four social pages, 12 placeholder item slots, character picker, EventSystem.");
        }
    }
}

