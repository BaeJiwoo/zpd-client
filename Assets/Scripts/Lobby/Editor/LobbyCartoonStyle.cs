using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Zpd.Lobby.Editor
{
    /// <summary>Uses the original lobby's illustrated atlas, palette and button states.</summary>
    public static class LobbyCartoonStyle
    {
        private static readonly Color Ink = C("24192F"), Cream = C("FFF2D8"), Muted = C("665274");

        [MenuItem("ZPD/Lobby/Apply Cartoon Style to Open Lobby")]
        public static void ApplyOpen()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            var lobby = UnityEngine.Object.FindFirstObjectByType<LobbyController>();

            if (lobby == null)
            {
                throw new InvalidOperationException("Open Lobby first.");
            }

            Undo.RegisterFullObjectHierarchyUndo(lobby.gameObject, "Apply original lobby style");
            Apply(lobby);
            EditorSceneManager.MarkSceneDirty(lobby.gameObject.scene);
        }

        public static void Apply(LobbyController lobby)
        {
            var sprites = AssetDatabase.LoadAllAssetsAtPath(LegacyLobbyCartoonStyle.AtlasPath).OfType<Sprite>().ToArray();

            if (sprites.Length < 6)
            {
                throw new InvalidOperationException("Original lobby UI atlas is missing.");
            }

            foreach (var image in lobby.GetComponentsInChildren<Image>(true))
            {
                string name = image.name;

                if (name == "Dismiss" || name == "Section Dismiss" || name == "Icon" || name == "Drawn Icon")
                {
                    continue;
                }

                var button = image.GetComponent<Button>();

                if (button != null)
                {
                    Skin(
                        image,
                        sprites,
                        name == "Solo Defense" || name == "Use Item"
                        ? "Battle"
                        : name == "Item Template" ? "Card" : "Button",
                        name == "Close" ? 3.5f : name == "Item Template" ? 3 : 2.2f);
                    var colors = button.colors;
                    colors.normalColor = Color.white;
                    colors.highlightedColor = colors.selectedColor = new Color(1, 1, .82f);
                    colors.pressedColor = new Color(.77f, .69f, .9f);
                    colors.disabledColor = new Color(.70f, .60f, .83f);
                    button.colors = colors;
                }
                else if (name == "Inventory" || name == "Details")
                {
                    Skin(image, sprites, "Panel", 1.8f);
                }
                else if (name == "Player Profile")
                {
                    Skin(image, sprites, "Card", 2.2f);
                }
                else if (name == "Background")
                {
                    image.color = C("251D35");
                }
                else if (name == "Rule")
                {
                    image.color = C("715090");
                }
                else if (name == "Inventory Viewport")
                {
                    image.color = new Color(.25f, .12f, .38f, .08f);
                }
                else if (name == "History Viewport")
                {
                    image.color = Color.clear;
                }
                else if (name == "Owned Amount")
                {
                    image.color = C("CDBDDB");
                }
                else if (name == "Fill")
                {
                    image.color = C("8951C9");
                }
            }

            foreach (var text in lobby.GetComponentsInChildren<Text>(true))
            {
                text.color = Ink;

                if (text.fontSize >= 19)
                {
                    text.fontStyle = FontStyle.Bold;
                }

                if (text.name == "History" || text.name == "History Title" || text.name == "Inventory Status" || text.name == "Hint" || text.name == "Category" || text.name == "Description" || text.name == "Empty State")
                {
                    text.color = Muted;
                }
            }

            foreach (var name in new[]
            {
                "Brand",
                "Version",
                "Deploy",
                "Hub",
                "Status",
                "Art Credit",
                "Character Tag",
                "Character Name"
            }

            )
            {
                var text = lobby.home.transform.Find(name)?.GetComponent<Text>();

                if (text == null)
                {
                    continue;
                }

                text.color = Cream;

                if (name == "Brand")
                {
                    var outline = text.GetComponent<Outline>() ?? text.gameObject.AddComponent<Outline>();
                    outline.effectColor = Color.black;
                    outline.effectDistance = new Vector2(2, -2);
                }
            }

            var inventory = lobby.inventoryPanel.transform;
            AddIcon(inventory, sprites, "Backpack", new Vector2(-332, 218), new Vector2(40, 40));
            var heading = inventory.Find("Inventory Title").GetComponent<RectTransform>();
            heading.anchoredPosition = new Vector2(-148, 218);
            heading.sizeDelta = new Vector2(316, 36);
            var friends = lobby.home.transform.Find("Friends");
            AddIcon(friends, sprites, "Friends", new Vector2(-112, 2), new Vector2(40, 40));
            var friendLabel = friends.Find("Label").GetComponent<Text>();
            friendLabel.rectTransform.anchoredPosition = new Vector2(22, 2);
            friendLabel.rectTransform.sizeDelta = new Vector2(230, 44);
            friendLabel.fontSize = 20;
            var inventoryButton = lobby.home.transform.Find("Inventory Button");
            AddIcon(inventoryButton, sprites, "Backpack", new Vector2(-112, 2), new Vector2(40, 40));
            var inventoryLabel = inventoryButton.Find("Label").GetComponent<Text>();
            inventoryLabel.rectTransform.anchoredPosition = new Vector2(22, 2);
            inventoryLabel.rectTransform.sizeDelta = new Vector2(230, 44);

            if (lobby.home.transform.Find("Comic Backdrop") == null)
            {
                var source = lobby.social.transform.Find("Comic Backdrop");

                if (source != null)
                {
                    var backdrop = UnityEngine.Object.Instantiate(source.gameObject, lobby.home.transform, false);
                    backdrop.name = "Comic Backdrop";
                    backdrop.SetActive(true);
                    backdrop.transform.SetAsFirstSibling();
                }
            }

            Canvas.ForceUpdateCanvases();
        }

        private static void AddIcon(Transform parent, Sprite[] sprites, string name, Vector2 position, Vector2 size)
        {
            var image = parent.Find("Drawn Icon")?.GetComponent<Image>();

            if (image == null)
            {
                image = new GameObject("Drawn Icon", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                image.transform.SetParent(parent, false);
            }

            image.sprite = sprites.Single(s => s.name == name);
            image.color = Color.white;
            image.preserveAspect = true;
            image.raycastTarget = false;
            image.rectTransform.sizeDelta = size;
            image.rectTransform.anchoredPosition = position;
        }

        private static void Skin(Image image, Sprite[] sprites, string name, float density)
        {
            image.sprite = sprites.Single(s => s.name == name);
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = density;
            image.color = Color.white;
        }

        private static Color C(string hex)
        {
            ColorUtility.TryParseHtmlString("#" + hex, out var color);
            return color;
        }
    }
}
