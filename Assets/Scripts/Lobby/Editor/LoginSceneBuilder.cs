using System.Linq;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using Zpd.Gameplay;

namespace Zpd.Lobby.Editor
{
    public static class LoginSceneBuilder
    {
        private static Font Font => AssetDatabase.LoadAssetAtPath<Font>("Assets/Resources/Fonts/NexonLv1/NEXONLv1GothicRegular.ttf");
        private static Color Ink => new Color32(36, 25, 47, 255);

        [MenuItem("ZPD/Lobby/Create Login Scene")]
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

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var canvas = new GameObject(
                "Login Canvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster)).GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scale = canvas.GetComponent<CanvasScaler>();
            scale.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scale.referenceResolution = new Vector2(1280, 720);
            scale.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            var bg = Box("Background", canvas.transform, 0, 0, 1280, 720, new Color32(37, 29, 53, 255));
            bg.rectTransform.anchorMin = Vector2.zero;
            bg.rectTransform.anchorMax = Vector2.one;
            bg.rectTransform.offsetMin = bg.rectTransform.offsetMax = Vector2.zero;
            Label("Brand", canvas.transform, "ZPD / ARENA", 0, 288, 600, 60, 36, Color.white);
            var panel = Box("Login Card", canvas.transform, 0, -6, 560, 510, Color.white);
            Skin(panel, "Panel");
            Label("Title", panel.transform, "Sign in", 0, 193, 480, 54, 32, Ink);
            Label("Subtitle", panel.transform, "Enter your ID and password to play.", 0, 148, 480, 32, 18, Ink);

            var controller = canvas.gameObject.AddComponent<LoginController>();
            var view = controller.View;
            view.input_login_id = CreateInput(panel.transform, "ID", "Enter your ID", 65, false);
            view.input_password = CreateInput(panel.transform, "Password", "Enter your password", -40, true);
            view.txt_status = Label("Status", panel.transform, "Enter your ID and password.", 0, -105, 470, 40, 15, Ink);

            var button = Box("Login", panel.transform, 0, -175, 440, 66, Color.white);
            Skin(button, "Battle");
            view.btn_login = button.gameObject.AddComponent<Button>();
            view.btn_login.targetGraphic = button;
            Label("Label", button.transform, "SIGN IN", 0, 0, 400, 56, 24, Ink);
            UnityEventTools.AddPersistentListener(view.btn_login.onClick, controller.Login);

            Label("Footer", canvas.transform, "ZPD ACCOUNT", 0, -294, 600, 30, 14, new Color32(199, 180, 219, 255));
            var events = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule)).GetComponent<EventSystem>();
            events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            events.firstSelectedGameObject = view.input_login_id.gameObject;
            EditorSceneManager.SaveScene(scene, SceneNavigation.Login);
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(SceneNavigation.Login, true)
            }.Concat(EditorBuildSettings.scenes.Where(s => s.path != SceneNavigation.Login)).ToArray();
        }

        public static void Install()
        {
            Build();
            var scene = EditorSceneManager.OpenScene(SceneNavigation.Lobby);
            AddChangePlayer(Object.FindFirstObjectByType<LobbyController>());
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }

        public static void AddChangePlayer(LobbyController controller)
        {
            if (controller.canvas_group_home.transform.Find("Change Player") != null)
            {
                return;
            }

            var box = Box("Change Player", controller.canvas_group_home.transform, 430, -282, 280, 42, Color.white);
            Skin(box, "Button");
            var button = box.gameObject.AddComponent<Button>();
            button.targetGraphic = box;
            Label("Label", button.transform, "Sign out", 0, 0, 250, 36, 16, Ink);
            UnityEventTools.AddPersistentListener(button.onClick, controller.ChangePlayer);
        }

        private static InputField CreateInput(Transform parent, string name, string placeholder, float y, bool secret)
        {
            Label(name + " Label", parent, name.ToUpperInvariant(), 0, y + 43, 440, 26, 15, Ink).alignment = TextAnchor.MiddleLeft;

            var box = Box(name, parent, 0, y, 440, 58, new Color32(255, 248, 231, 255));
            var input = box.gameObject.AddComponent<InputField>();
            input.targetGraphic = box;
            input.textComponent = Label("Text", input.transform, "", 0, 0, 400, 54, 24, Ink);
            input.textComponent.alignment = TextAnchor.MiddleLeft;
            input.placeholder = Label("Placeholder", input.transform, placeholder, 0, 0, 400, 54, 20, new Color32(132, 118, 144, 255));
            ((Text)input.placeholder).alignment = TextAnchor.MiddleLeft;
            input.contentType = secret ? InputField.ContentType.Password : InputField.ContentType.Standard;
            input.asteriskChar = '*';
            input.lineType = InputField.LineType.SingleLine;
            input.characterLimit = 0;
            return input;
        }

        private static RectTransform Rect(string name, Transform parent, float x, float y, float w, float h)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(w, h);
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
            Color color)
        {
            var text = Rect(name, parent, x, y, w, h).gameObject.AddComponent<Text>();
            text.font = Font;
            text.fontSize = size;
            text.text = value;
            text.color = color;
            text.alignment = TextAnchor.MiddleCenter;
            text.supportRichText = false;
            text.raycastTarget = false;
            return text;
        }

        private static void Skin(Image image, string name)
        {
            image.sprite = AssetDatabase.LoadAllAssetsAtPath(LegacyLobbyCartoonStyle.AtlasPath).OfType<Sprite>().Single(s => s.name == name);
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 2;
        }
    }
}
