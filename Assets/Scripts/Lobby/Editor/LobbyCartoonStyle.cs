using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.UI;

namespace Zpd.Lobby.Editor
{
    /// <summary>Imports the drawn UI kit and applies it to authored scene objects in the editor.</summary>
    public static class LobbyCartoonStyle
    {
        public const string AtlasPath = "Assets/Resources/UI/Lobby/cartoon-ui-atlas.png";
        private static readonly string[] Names = { "Panel", "Card", "Button", "Battle", "Backpack", "Friends" };
        private static readonly Color Ink = ColorOf("24192F");
        private static readonly Color Cream = ColorOf("FFF2D8");
        private static Sprite[] sprites;

        [MenuItem("ZPD/Lobby/Apply Cartoon Style to Open Lobby")]
        public static void ApplyToOpenScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var lobby = UnityEngine.Object.FindFirstObjectByType<LobbyController>();
            if (lobby == null) throw new InvalidOperationException("Open a lobby scene first.");
            // A full hierarchy snapshot keeps repeated styling reversible in the editor.
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.RegisterFullObjectHierarchyUndo(lobby.GetComponentInParent<Canvas>().gameObject, "Apply lobby cartoon art");
            Apply(lobby);
            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(lobby.gameObject.scene);
            Debug.Log("[Lobby] Cartoon UI applied. Save the scene to keep the changes.");
        }

        public static void Apply(LobbyController lobby)
        {
            ImportAtlas();
            var canvas = lobby.GetComponentInParent<Canvas>();
            foreach (var image in canvas.GetComponentsInChildren<Image>(true))
            {
                string name = image.name;
                var button = image.GetComponent<Button>();
                if (name == "Modal Backdrop") continue;
                if (button != null)
                {
                    bool card = name.StartsWith("Item ") || name.StartsWith("Character Option ");
                    Skin(image, name == "Enter Battle" ? "Battle" : card ? "Card" : "Button", name == "Close" ? 3.5f : card ? 3 : 2.2f);
                    var colors = button.colors;
                    colors.normalColor = Color.white;
                    colors.highlightedColor = new Color(1, 1, 0.82f);
                    colors.selectedColor = new Color(1, 1, 0.82f);
                    colors.pressedColor = new Color(0.77f, 0.69f, 0.9f);
                    colors.disabledColor = new Color(0.70f, 0.60f, 0.83f);
                    button.colors = colors;
                }
                else if (image.GetComponent<LobbyPanel>() != null) Skin(image, "Panel", 1.8f);
                else if (name == "Character Backplate" || name == "Current Loadout" || name == "Equipped Weapon" || name == "Player Search" || name.StartsWith("Player "))
                    Skin(image, "Card", name.StartsWith("Player ") ? 3.0f : 2.2f);
                else if (name == "Background") image.color = ColorOf("251D35");
                else if (name == "Header Rule") image.color = ColorOf("715090");
                else if (name == "Character Accent") image.color = Color.clear;
                else if (name == "Viewport") image.color = new Color(0.25f, 0.12f, 0.38f, 0.08f);
            }
            foreach (var text in canvas.GetComponentsInChildren<Text>(true))
            {
                text.color = Ink;
                if (text.fontSize >= 19) text.fontStyle = FontStyle.Bold;
                if (text.name == "Presence" || text.name == "Data Notice" || text.name == "Preview Notice" || text.name == "Item Details" || text.name == "Subtitle")
                    text.color = ColorOf("665274");
            }
            foreach (string name in new[] { "Brand", "Mode", "Heading", "Intro", "Character Tag", "Character Name", "Status", "Art Credit" })
            {
                var text = lobby.transform.Find(name)?.GetComponent<Text>();
                if (text == null) continue;
                text.color = Cream;
                if (name == "Heading" || name == "Brand" || name == "Character Name")
                {
                    var outline = text.GetComponent<Outline>() ?? text.gameObject.AddComponent<Outline>();
                    outline.effectColor = Color.black;
                    outline.effectDistance = new Vector2(2, -2);
                }
            }
            lobby.transform.Find("Art Credit").GetComponent<Text>().text = "Art: Rgsdev (CC0)  |  UI: OpenAI / Codex";
            lobby.transform.Find("Art Credit").GetComponent<Text>().fontSize = 11;
            AddButtonIcon(lobby.transform.Find("Friends"), "Friends");
            AddButtonIcon(lobby.transform.Find("Inventory"), "Backpack");
            var profileLabel = lobby.transform.Find("Profile/Label").GetComponent<Text>();
            profileLabel.fontSize = 17;
            profileLabel.rectTransform.sizeDelta = new Vector2(336, 44);
            profileLabel.rectTransform.anchoredPosition = new Vector2(0, 3);
            foreach (var text in lobby.friends.GetComponentsInChildren<Text>(true))
            {
                if (text.name == "Name") text.rectTransform.anchoredPosition = new Vector2(-32, 10);
                if (text.name == "Presence")
                {
                    text.rectTransform.anchoredPosition = new Vector2(-32, -12);
                    text.fontSize = 13;
                }
                if (text.name == "Preview Notice") text.rectTransform.anchoredPosition = new Vector2(0, -300);
            }
            foreach (var item in lobby.inventory.GetComponentsInChildren<Button>(true).Where(b => b.name.StartsWith("Item ")))
            {
                var icon = (RectTransform)item.transform.Find("Icon");
                icon.sizeDelta = new Vector2(148, 60);
                icon.anchoredPosition = new Vector2(0, 15);
                ((RectTransform)item.transform.Find("Name")).anchoredPosition = new Vector2(0, -31);
            }
            AddBackdrop(lobby.transform);
            Canvas.ForceUpdateCanvases();
        }

        private static void Skin(Image image, string name, float pixelsPerUnit)
        {
            image.sprite = sprites.Single(s => s.name == name);
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = pixelsPerUnit;
            image.color = Color.white;
        }

        private static void AddButtonIcon(Transform button, string name)
        {
            var icon = button.Find("Drawn Icon")?.GetComponent<Image>();
            if (icon == null)
            {
                var go = new GameObject("Drawn Icon", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(button, false);
                Undo.RegisterCreatedObjectUndo(go, "Add drawn UI icon");
                icon = go.GetComponent<Image>();
            }
            icon.sprite = sprites.Single(s => s.name == name);
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            icon.rectTransform.sizeDelta = new Vector2(46, 46);
            icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = new Vector2(0, 0.5f);
            icon.rectTransform.anchoredPosition = new Vector2(43, 2);
            var label = button.Find("Label").GetComponent<RectTransform>();
            label.sizeDelta = new Vector2(((RectTransform)button).sizeDelta.x - 100, label.sizeDelta.y);
            label.anchoredPosition = new Vector2(29, 2);
            label.GetComponent<Text>().fontSize = 17;
        }

        private static void AddBackdrop(Transform parent)
        {
            if (parent.Find("Comic Backdrop") != null) return;
            var backdrop = new GameObject("Comic Backdrop", typeof(RectTransform)).GetComponent<RectTransform>();
            backdrop.SetParent(parent, false);
            backdrop.SetAsFirstSibling();
            backdrop.sizeDelta = new Vector2(1280, 720);
            // Simple authored graphic shapes support the illustrated UI; no runtime drawing.
            for (int i = 0; i < 5; i++)
            {
                var stripe = new GameObject("Ink Streak " + i, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                stripe.transform.SetParent(backdrop, false);
                stripe.rectTransform.sizeDelta = new Vector2(72 + 12 * i, 490);
                stripe.rectTransform.anchoredPosition = new Vector2(-160 + i * 105, 0);
                stripe.rectTransform.localRotation = Quaternion.Euler(0, 0, -20);
                stripe.color = new Color(0.42f, 0.28f, 0.64f, 0.10f);
                stripe.raycastTarget = false;
            }
            Undo.RegisterCreatedObjectUndo(backdrop.gameObject, "Add comic backdrop");
        }

        private static void ImportAtlas()
        {
            AssetDatabase.ImportAsset(AtlasPath);
            var importer = AssetImporter.GetAtPath(AtlasPath) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("Missing illustrated UI atlas: " + AtlasPath);
            sprites = AssetDatabase.LoadAllAssetsAtPath(AtlasPath).OfType<Sprite>().ToArray();
            if (sprites.Length == Names.Length && Names.All(n => sprites.Any(s => s.name == n))) return;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.isReadable = true;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 2048;
            importer.filterMode = FilterMode.Bilinear;
            importer.SaveAndReimport();
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(AtlasPath);
            var pixels = texture.GetPixels32();
            var rects = new SpriteRect[6];
            int cellW = texture.width / 3, cellH = texture.height / 2;
            for (int i = 0; i < 6; i++)
            {
                int left = (i % 3) * cellW, bottom = (1 - i / 3) * cellH;
                int minX = left + cellW, minY = bottom + cellH, maxX = left, maxY = bottom;
                for (int y = bottom; y < bottom + cellH; y++)
                for (int x = left; x < left + cellW; x++)
                    if (pixels[y * texture.width + x].a > 200)
                    { minX = Mathf.Min(minX, x); minY = Mathf.Min(minY, y); maxX = Mathf.Max(maxX, x); maxY = Mathf.Max(maxY, y); }
                if (minX > maxX || minY > maxY) throw new InvalidOperationException("Empty UI sprite cell: " + Names[i]);
                minX = Mathf.Max(left, minX - 2); minY = Mathf.Max(bottom, minY - 2);
                maxX = Mathf.Min(left + cellW - 1, maxX + 2); maxY = Mathf.Min(bottom + cellH - 1, maxY + 2);
                rects[i] = new SpriteRect {
                    name = Names[i], spriteID = GUID.Generate(), alignment = SpriteAlignment.Center,
                    pivot = new Vector2(0.5f, 0.5f), rect = new Rect(minX, minY, maxX - minX + 1, maxY - minY + 1),
                    border = i < 4 ? new Vector4(64, 64, 64, 64) : Vector4.zero
                };
            }
            var factory = new SpriteDataProviderFactories();
            factory.Init();
            var provider = factory.GetSpriteEditorDataProviderFromObject(importer);
            provider.InitSpriteEditorDataProvider();
            provider.SetSpriteRects(rects);
            provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)));
            provider.Apply();
            importer.isReadable = false;
            importer.SaveAndReimport();
            sprites = AssetDatabase.LoadAllAssetsAtPath(AtlasPath).OfType<Sprite>().ToArray();
        }

        private static Color ColorOf(string hex) { ColorUtility.TryParseHtmlString("#" + hex, out var value); return value; }
    }
}
