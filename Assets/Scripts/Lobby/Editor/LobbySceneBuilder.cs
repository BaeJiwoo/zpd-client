using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Zpd.Lobby.Editor
{
    public static class LobbySceneBuilder
    {
        private static readonly Color color_background = C("101A25");

        private static readonly Color color_color_surface = C("1A2938");

        private static readonly Color color_color_card = C("26394A");

        private static readonly Color color_color_muted = C("A3B6C5");

        private static readonly Color color_color_accent = C("83DFC9");
        private static Font font_ui;

        [MenuItem("ZPD/Lobby/Create Lobby Scene")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            font_ui = Resources.Load<Font>("Fonts/NexonLv1/NEXONLv1GothicRegular");
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/LegacyLobby.unity");
            var legacy = UnityEngine.Object.FindFirstObjectByType<LegacyLobbyController>();
            var oldCanvas = legacy.GetComponentInParent<Canvas>();
            oldCanvas.name = "Social Canvas";
            oldCanvas.sortingOrder = 1;

            foreach (Transform child in legacy.transform)
            {
                child.gameObject.SetActive(false);
            }

            foreach (Transform child in oldCanvas.transform)
            {
                if (child != legacy.transform && child != legacy.game_object_backdrop.transform && child != legacy.lobby_panel_friends.transform.parent)
                {
                    child.gameObject.SetActive(false);
                }
            }

            var canvas = new GameObject(
                "Lobby Canvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster)).GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            var bg = Box("Background", canvas.transform, 0, 0, 1280, 720, color_background);
            Stretch(bg.rectTransform);
            var main = Rect("Home", canvas.transform, 0, 0, 1280, 720);
            var c = canvas.gameObject.AddComponent<LobbyController>();
            c.legacy_lobby_controller = legacy;
            c.canvas_group_home = main.gameObject.AddComponent<CanvasGroup>();
            Label("Brand", main, "ZPD / ARENA", -340, 316, 504, 40, 25, Color.white);
            Label("Version", main, "LOBBY", 346, 316, 180, 40, 14, color_color_muted);
            Button("Profile", main, "PLAYER INFO    >", -425, 140, 300, 76, color_color_card, c.OpenProfile, Color.white, 20);
            Button(
                "Inventory Button",
                main,
                "INVENTORY    >",
                -425,
                36,
                300,
                76,
                color_color_card,
                c.OpenInventory,
                Color.white,
                20);
            Button("Friends", main, "FRIENDS    >", -425, -68, 300, 76, color_color_card, c.OpenFriends, Color.white, 20);
            Label("Hub", main, "YOUR HUB", -425, 216, 300, 28, 14, color_color_muted);
            Box("Rule", main, 0, 278, 1184, 2, color_color_card);
            // Keep the existing authored character art and its server binding references.

            var hero = (RectTransform)legacy.transform.Find("Hero");
            hero.SetParent(main, false);
            hero.anchoredPosition = new Vector2(0, 0);
            hero.localScale = Vector3.one * 1.45f;
            hero.gameObject.SetActive(true);
            var characterTag = (RectTransform)legacy.lobby_character_picker.txt_home_tag.transform;
            characterTag.SetParent(main, false);
            characterTag.anchoredPosition = new Vector2(0, 236);
            characterTag.gameObject.SetActive(true);
            var characterName = (RectTransform)legacy.lobby_character_picker.txt_current_character.transform;
            characterName.SetParent(main, false);
            characterName.anchoredPosition = new Vector2(0, -248);
            characterName.gameObject.SetActive(true);
            Label(
                "Art Credit",
                main,
                "Character art: Rgsdev / CC0",
                400,
                -332,
                390,
                24,
                11,
                color_color_muted,
                TextAnchor.MiddleRight);
            var sectionRoot = Rect("Feature Windows", canvas.transform, 0, 0, 1280, 720);
            Stretch(sectionRoot);
            c.canvas_group_sections = sectionRoot.gameObject.AddComponent<CanvasGroup>();
            var sectionDim = Button(
                "Section Dismiss",
                sectionRoot,
                "",
                0,
                0,
                1280,
                720,
                new Color(0, 0, 0, .72f),
                c.CloseSection);
            Stretch((RectTransform)sectionDim.transform);
            var profile = Box("Player Profile", sectionRoot, 0, 0, 520, 410, color_color_surface).transform;
            c.game_object_profile_panel = profile.gameObject;
            Button("Close", profile, "X", 213, 158, 40, 40, color_color_card, c.CloseSection);
            Label("Heading", profile, "PLAYER RECORD", 0, 140, 328, 28, 14, color_color_accent);
            c.txt_nickname = Label("Nickname", profile, "PLAYER --", 0, 91, 328, 42, 28, Color.white);
            c.txt_nickname.supportRichText = false;
            c.txt_level = Label("Level", profile, "LEVEL --", 0, 51, 328, 28, 16, color_color_accent);
            c.txt_record = Label("Record", profile, "MATCHES --  /  WIN RATE --", 0, 5, 328, 52, 16, Color.white);
            Label("History Title", profile, "RECENT PLAY HISTORY", 0, -48, 328, 24, 13, color_color_muted);
            var historyViewport = Box("History Viewport", profile, 0, -112, 328, 94, color_color_surface).rectTransform;
            historyViewport.gameObject.AddComponent<RectMask2D>();
            var historyScroll = historyViewport.gameObject.AddComponent<ScrollRect>();
            historyScroll.horizontal = false;
            c.txt_history = Label("History", historyViewport, "Play history has not been loaded.", 0, 0, 312, 94, 15, color_color_muted);
            c.txt_history.supportRichText = false;
            c.txt_history.alignment = TextAnchor.UpperLeft;
            var hr = c.txt_history.rectTransform;
            hr.anchorMin = new Vector2(0, 1);
            hr.anchorMax = Vector2.one;
            hr.pivot = new Vector2(.5f, 1);
            hr.anchoredPosition = Vector2.zero;
            hr.sizeDelta = new Vector2(-16, 94);
            c.txt_history.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            historyScroll.viewport = historyViewport;
            historyScroll.content = hr;
            var inv = Box("Inventory", sectionRoot, 0, 0, 776, 512, color_color_surface).transform;
            c.game_object_inventory_panel = inv.gameObject;
            Button("Close", inv, "X", 339, 218, 40, 40, color_color_card, c.CloseSection);
            Label("Inventory Title", inv, "INVENTORY", -173, 218, 366, 36, 26, Color.white);
            c.txt_inventory_status = Label(
                "Inventory Status",
                inv,
                "Inventory has not been loaded.",
                152,
                218,
                300,
                30,
                13,
                color_color_muted,
                TextAnchor.MiddleRight);
            c.btn_inventory_filters = new[]
            {
                Button("All", inv, "ALL", -280, 161, 152, 40, color_color_card, c.ShowAll),
                Button("Consumables", inv, "CONSUMABLES", -96, 161, 200, 40, color_color_card, c.ShowConsumables),
                Button("Equipment", inv, "EQUIPMENT", 116, 161, 200, 40, color_color_card, c.ShowEquipment)
            };
            var viewport = Box("Inventory Viewport", inv, 0, -32, 720, 310, color_background).rectTransform;
            viewport.gameObject.AddComponent<RectMask2D>();
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 28;
            var content = Rect("Items", viewport, 0, 0, 720, 330);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = Vector2.one;
            content.pivot = new Vector2(.5f, 1);
            content.sizeDelta = new Vector2(0, 330);
            var grid = content.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(224, 154);
            grid.spacing = new Vector2(12, 12);
            grid.padding = new RectOffset(12, 12, 12, 12);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 3;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport;
            scroll.content = content;
            c.transform_inventory_content = content;
            var item = Box("Item Template", content, 0, 0, 224, 154, color_color_card).gameObject.AddComponent<LobbyItemCard>();
            item.btn_select_item = item.gameObject.AddComponent<Button>();
            item.btn_select_item.targetGraphic = item.GetComponent<Image>();
            item.txt_category = Label("Category", item.transform, "CONSUMABLE", 0, 55, 196, 20, 11, color_color_accent);
            item.txt_title = Label("Name", item.transform, "ITEM", 18, 19, 148, 46, 19, Color.white);
            item.txt_title.supportRichText = false;
            item.img_icon = Box("Icon", item.transform, -80, 19, 36, 36, Color.white);
            item.txt_quantity = Label("Quantity", item.transform, "OWNED --", 0, -26, 196, 22, 13, color_color_muted);
            item.slider_owned_quantity = Amount(item.transform, 0, -53, 196);
            c.lobby_item_card_template = item;
            item.gameObject.SetActive(false);
            c.txt_empty_state = Label(
                "Empty State",
                viewport,
                "YOUR INVENTORY\n\nOwned items will appear here once loaded.",
                0,
                0,
                640,
                140,
                20,
                color_color_muted,
                TextAnchor.MiddleCenter);
            Label("Hint", inv, "Select an item to inspect its details.", 0, -207, 720, 22, 13, color_color_muted);
            Label("Deploy", main, "BATTLE MODES", 425, 216, 300, 28, 13, color_color_muted);
            c.btn_solo_defense = Button(
                "Solo Defense",
                main,
                "SOLO DEFENSE    >",
                425,
                140,
                320,
                80,
                color_color_accent,
                c.EnterSoloDefense,
                color_background,
                23);
            Button("Multi Play", main, "MULTI PLAY    >", 425, 36, 320, 76, color_color_card, c.OpenMultiPlay, Color.white, 21);
            c.txt_status = Label(
                "Status",
                main,
                "Select a menu to view your profile, items or friends.",
                -200,
                -332,
                784,
                24,
                13,
                color_color_muted);
            // New modal sits above the home. The retained friends canvas has its own overlay.

            var modal = Rect("Item Modal", canvas.transform, 0, 0, 1280, 720);
            Stretch(modal);
            c.game_object_modal = modal.gameObject;
            var dim = Button("Dismiss", modal, "", 0, 0, 1280, 720, new Color(0, 0, 0, .8f), c.CloseModal);
            Stretch((RectTransform)dim.transform);
            var panel = Box("Details", modal, 0, 0, 560, 466, color_color_surface).transform;
            c.txt_modal_title = Label("Title", panel, "ITEM DETAILS", -20, 181, 448, 42, 27, Color.white);
            c.txt_modal_title.supportRichText = false;
            Button("Close", panel, "X", 236, 181, 40, 40, color_color_card, c.CloseModal);
            c.txt_modal_description = Label("Description", panel, "", 0, 52, 480, 186, 18, color_color_muted);
            c.txt_modal_description.supportRichText = false;
            var qty = Rect("Owned Quantity", panel, 0, -91, 480, 66);
            c.game_object_quantity_root = qty.gameObject;
            c.txt_modal_quantity = Label("Quantity", qty, "OWNED --", 0, 19, 480, 28, 16, Color.white);
            c.slider_quantity = Amount(qty, 0, -15, 480);
            c.btn_use_item = Button("Use Item", panel, "USE ITEM", 0, -176, 480, 56, color_color_accent, c.UseItem, color_background, 20);
            modal.gameObject.SetActive(false);
            profile.gameObject.SetActive(false);
            inv.gameObject.SetActive(false);
            sectionRoot.gameObject.SetActive(false);
            UnityEngine.Object.FindFirstObjectByType<EventSystem>().firstSelectedGameObject = c.btn_solo_defense.gameObject;
            LobbyCartoonStyle.Apply(c);
            LoginSceneBuilder.AddChangePlayer(c);
            var path = AssetDatabase.GenerateUniqueAssetPath("Assets/Scenes/Lobby.unity");
            EditorSceneManager.SaveScene(scene, path);
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(path, true)
            }.Concat(EditorBuildSettings.scenes.Where(s => s.path != path)).ToArray();

            if (EditorBuildSettings.scenes.Any(s => s.path == Zpd.Gameplay.SceneNavigation.Login))
            {
                EditorBuildSettings.scenes = EditorBuildSettings.scenes.OrderBy(s => s.path == Zpd.Gameplay.SceneNavigation.Login ? 0 : 1).ToArray();
            }

            Debug.Log("[Lobby] Created " + path);
        }

        private static Slider Amount(Transform parent, float x, float y, float width)
        {
            var track = Box("Owned Amount", parent, x, y, width, 8, color_background);
            var slider = track.gameObject.AddComponent<Slider>();
            slider.interactable = false;
            slider.wholeNumbers = true;
            slider.navigation = new Navigation
            {
                mode = Navigation.Mode.None
            };
            var fill = Box("Fill", track.transform, 0, 0, width, 8, color_color_accent);
            Stretch(fill.rectTransform);
            fill.raycastTarget = false;
            track.raycastTarget = false;
            slider.fillRect = fill.rectTransform;
            slider.maxValue = 100;
            return slider;
        }

        private static RectTransform Rect(string n, Transform p, float x, float y, float w, float h)
        {
            var r = new GameObject(n, typeof(RectTransform)).GetComponent<RectTransform>();
            r.SetParent(p, false);
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(.5f, .5f);
            r.sizeDelta = new Vector2(w, h);
            r.anchoredPosition = new Vector2(x, y);
            return r;
        }

        private static Image Box(string n, Transform p, float x, float y, float w, float h, Color color)
        {
            var i = Rect(n, p, x, y, w, h).gameObject.AddComponent<Image>();
            i.color = color;
            return i;
        }

        private static Text Label(
            string n,
            Transform p,
            string value,
            float x,
            float y,
            float w,
            float h,
            int size,
            Color color,
            TextAnchor align = TextAnchor.MiddleLeft)
        {
            var t = Rect(n, p, x, y, w, h).gameObject.AddComponent<Text>();
            t.font = font_ui;
            t.text = value;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.raycastTarget = false;
            return t;
        }

        private static Button Button(
            string n,
            Transform p,
            string value,
            float x,
            float y,
            float w,
            float h,
            Color color,
            UnityAction action,
            Color? ink = null,
            int size = 15)
        {
            var i = Box(n, p, x, y, w, h, color);
            var b = i.gameObject.AddComponent<Button>();
            b.targetGraphic = i;

            if (value.Length > 0)
            {
                Label(
                    "Label",
                    i.transform,
                    value,
                    0,
                    0,
                    w - 20,
                    h - 6,
                    size,
                    ink ?? Color.white,
                    TextAnchor.MiddleCenter);
            }

            UnityEventTools.AddPersistentListener(b.onClick, action);
            return b;
        }

        private static void Stretch(RectTransform r)
        {
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = r.offsetMax = Vector2.zero;
        }

        private static Color C(string hex)
        {
            ColorUtility.TryParseHtmlString("#" + hex, out var c);
            return c;
        }
    }
}
