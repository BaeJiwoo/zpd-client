using Zpd.Networking;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Zpd.Lobby
{
    /// <summary>Authored UI references, rendering and focus. Never changes account data or calls services.</summary>
    public sealed class LobbyView : MonoBehaviour
    {
        [FormerlySerializedAs("home")]
        public CanvasGroup canvas_group_home;

        [FormerlySerializedAs("sections")]
        public CanvasGroup canvas_group_sections;

        [FormerlySerializedAs("profilePanel")]
        public GameObject game_object_profile_panel;

        [FormerlySerializedAs("inventoryPanel")]
        public GameObject game_object_inventory_panel;

        [FormerlySerializedAs("nickname")]
        public Text txt_nickname;

        [FormerlySerializedAs("level")]
        public Text txt_level;

        [FormerlySerializedAs("record")]
        public Text txt_record;

        [FormerlySerializedAs("history")]
        public Text txt_history;

        [FormerlySerializedAs("inventoryStatus")]
        public Text txt_inventory_status;

        [FormerlySerializedAs("status")]
        public Text txt_status;

        [FormerlySerializedAs("emptyState")]
        public Text txt_empty_state;

        [FormerlySerializedAs("content")]
        public Transform transform_inventory_content;

        [FormerlySerializedAs("cardTemplate")]
        public LobbyItemCard lobby_item_card_template;

        [FormerlySerializedAs("filters")]
        public Button[] btn_inventory_filters;

        [FormerlySerializedAs("modal")]
        public GameObject game_object_modal;

        [FormerlySerializedAs("quantityRoot")]
        public GameObject game_object_quantity_root;

        [FormerlySerializedAs("modalTitle")]
        public Text txt_modal_title;

        [FormerlySerializedAs("modalDescription")]
        public Text txt_modal_description;

        [FormerlySerializedAs("modalQuantity")]
        public Text txt_modal_quantity;

        [FormerlySerializedAs("quantitySlider")]
        public Slider slider_quantity;

        [FormerlySerializedAs("useButton")]
        public Button btn_use_item;

        [FormerlySerializedAs("soloButton")]
        public Button btn_solo_defense;

        [Serializable]
        public sealed class Icon
        {
            [FormerlySerializedAs("key")]
            public string icon_key;

            [FormerlySerializedAs("sprite")]
            public Sprite sprite_icon;
        }

        [FormerlySerializedAs("icons")]
        public Icon[] item_icons = Array.Empty<Icon>();
        public event Action<string> ItemSelected;
        private readonly List<LobbyItemCard> item_cards = new List<LobbyItemCard>();
        private GameObject game_object_previous_selection;

        private GameObject game_object_section_selection;

        public bool HasModal => game_object_modal.activeSelf;
        public bool HasSection => canvas_group_sections.gameObject.activeSelf;

        private void OnValidate() => ResolveReferences();

        // Also repairs references after a script reload in an already-open pre-MVC scene.

        public void ResolveReferences()
        {
            if (canvas_group_home == null)
            {
                canvas_group_home = Find<CanvasGroup>("Home");
            }

            if (canvas_group_sections == null)
            {
                canvas_group_sections = Find<CanvasGroup>("Feature Windows");
            }

            if (game_object_profile_panel == null)
            {
                game_object_profile_panel = transform.Find("Feature Windows/Player Profile")?.gameObject;
            }

            if (game_object_inventory_panel == null)
            {
                game_object_inventory_panel = transform.Find("Feature Windows/Inventory")?.gameObject;
            }

            if (game_object_modal == null)
            {
                game_object_modal = transform.Find("Item Modal")?.gameObject;
            }

            string p = "Feature Windows/Player Profile/", i = "Feature Windows/Inventory/", d = "Item Modal/Details/";

            if (txt_nickname == null)
            {
                txt_nickname = Find<Text>(p + "Nickname");
            }

            if (txt_level == null)
            {
                txt_level = Find<Text>(p + "Level");
            }

            if (txt_record == null)
            {
                txt_record = Find<Text>(p + "Record");
            }

            if (txt_history == null)
            {
                txt_history = Find<Text>(p + "History Viewport/History");
            }

            if (txt_inventory_status == null)
            {
                txt_inventory_status = Find<Text>(i + "Inventory Status");
            }

            if (txt_status == null)
            {
                txt_status = Find<Text>("Home/Status");
            }

            if (txt_empty_state == null)
            {
                txt_empty_state = Find<Text>(i + "Inventory Viewport/Empty State");
            }

            if (transform_inventory_content == null)
            {
                transform_inventory_content = transform.Find(i + "Inventory Viewport/Items");
            }

            if (lobby_item_card_template == null)
            {
                lobby_item_card_template = Find<LobbyItemCard>(i + "Inventory Viewport/Items/Item Template");
            }

            if (btn_inventory_filters == null || btn_inventory_filters.Length != 3 || btn_inventory_filters.Any(b => b == null))
            {
                btn_inventory_filters = new[]
                {
                    Find<Button>(i + "All"),
                    Find<Button>(i + "Consumables"),
                    Find<Button>(i + "Equipment")
                };
            }

            if (game_object_quantity_root == null)
            {
                game_object_quantity_root = transform.Find(d + "Owned Quantity")?.gameObject;
            }

            if (txt_modal_title == null)
            {
                txt_modal_title = Find<Text>(d + "Title");
            }

            if (txt_modal_description == null)
            {
                txt_modal_description = Find<Text>(d + "Description");
            }

            if (txt_modal_quantity == null)
            {
                txt_modal_quantity = Find<Text>(d + "Owned Quantity/Quantity");
            }

            if (slider_quantity == null)
            {
                slider_quantity = Find<Slider>(d + "Owned Quantity/Owned Amount");
            }

            if (btn_use_item == null)
            {
                btn_use_item = Find<Button>(d + "Use Item");
            }

            if (btn_solo_defense == null)
            {
                btn_solo_defense = Find<Button>("Home/Solo Defense");
            }
        }

        private T Find<T>(string path)
            where T : Component => transform.Find(path)?.GetComponent<T>();

        public void Initialize()
        {
            ResolveReferences();
            canvas_group_sections.gameObject.SetActive(false);
            game_object_profile_panel.SetActive(false);
            game_object_inventory_panel.SetActive(false);
            game_object_modal.SetActive(false);
            lobby_item_card_template.gameObject.SetActive(false);

            foreach (var label in GetComponentsInChildren<Text>(true))
            {
                label.supportRichText = false;
            }
        }

        public void RenderProfile(LobbyModel model)
        {
            var p = model.Profile;
            txt_nickname.text = p?.Nickname ?? (AuthManager.Instance.IsSignedIn ? "PLAYER " + AuthManager.Instance.PlayerId : "PLAYER --");
            txt_level.text = p == null ? "LEVEL --" : "LEVEL " + p.Level;
            txt_record.text = p == null
                ? "MATCHES -- / WIN RATE --"
                : p.Matches == 0
                ? "0 MATCHES / WIN RATE --"
                : $"{p.Matches} MATCHES / {p.Wins}W {p.Losses}L / WIN RATE {p.WinRate:0.#}%";
            txt_history.text = model.ProfileState == LobbyLoadState.Loading
                ? "Loading profile..."
                : model.ProfileState == LobbyLoadState.Error
                ? model.ProfileError
                : p == null
                ? "Profile service not connected."
                : p.History.Count == 0
                ? "No recent matches."
                : string.Join("\n", p.History);
        }

        public void RenderInventory(LobbyModel model)
        {
            foreach (var card in item_cards)
            {
                card.gameObject.SetActive(false);
                Destroy(card.gameObject);
            }

            item_cards.Clear();

            foreach (var item in model.VisibleItems)
            {
                var card = Instantiate(lobby_item_card_template, transform_inventory_content);
                card.gameObject.SetActive(true);
                card.Bind(
                    item,
                    item_icons.FirstOrDefault(i => i.icon_key == item.IconKey)?.sprite_icon,
                    id => ItemSelected?.Invoke(id));
                item_cards.Add(card);
            }

            for (int i = 0; i < btn_inventory_filters.Length; i++)
            {
                btn_inventory_filters[i].interactable = i != model.Filter;
            }

            txt_inventory_status.text = model.InventoryState == LobbyLoadState.Loading
                ? "Loading inventory..."
                : model.InventoryState == LobbyLoadState.Error
                ? model.InventoryError
                : model.InventoryState == LobbyLoadState.Unavailable
                ? "Inventory service not connected."
                : $"{item_cards.Count} ITEMS";
            txt_empty_state.gameObject.SetActive(item_cards.Count == 0);
            txt_empty_state.text = model.InventoryState == LobbyLoadState.Ready
                ? "No owned items in this category."
                : txt_inventory_status.text;
        }

        public void ShowSection(LobbyWindow window)
        {
            CloseModal();

            if (!HasSection)
            {
                game_object_section_selection = EventSystem.current?.currentSelectedGameObject;
            }

            game_object_profile_panel.SetActive(window == LobbyWindow.Profile);
            game_object_inventory_panel.SetActive(window == LobbyWindow.Inventory);
            canvas_group_sections.gameObject.SetActive(true);
            SyncInteraction(false);
            Select(
                (window == LobbyWindow.Profile ? game_object_profile_panel : game_object_inventory_panel).GetComponentInChildren<Button>().gameObject);
        }

        public void CloseSection()
        {
            CloseModal();

            if (!HasSection)
            {
                return;
            }

            canvas_group_sections.gameObject.SetActive(false);
            game_object_profile_panel.SetActive(false);
            game_object_inventory_panel.SetActive(false);
            SyncInteraction(false);
            Select(game_object_section_selection);
        }

        public void ShowItem(LobbyModel model, bool serviceAvailable)
        {
            var item = model.SelectedItem;

            if (item == null)
            {
                CloseModal();
                return;
            }

            OpenModal();
            txt_modal_title.text = item.Name;
            bool consumable = item.Kind == LobbyItemKind.Consumable;
            txt_modal_description.text = (consumable ? "CONSUMABLE" : "EQUIPMENT") + "\n\n" + item.Description;

            if (consumable && !serviceAvailable)
            {
                txt_modal_description.text += "\n\nItem service not connected.";
            }

            if (model.IsUsingItem)
            {
                txt_modal_description.text = "Requesting item use...";
            }
            else if (consumable && !string.IsNullOrEmpty(model.UseError))
            {
                txt_modal_description.text += "\n\n" + model.UseError;
            }

            game_object_quantity_root.SetActive(consumable);
            btn_use_item.gameObject.SetActive(consumable);
            slider_quantity.maxValue = Mathf.Max(1, item.Capacity);
            slider_quantity.value = item.Quantity;
            txt_modal_quantity.text = "OWNED  " + item.Quantity;
            btn_use_item.interactable = serviceAvailable && model.CanUseSelected;
        }

        public void ShowMultiPlay()
        {
            OpenModal();
            txt_modal_title.text = "MULTI PLAY";
            txt_modal_description.text = "Multiplayer gameplay is not available yet.";
            game_object_quantity_root.SetActive(false);
            btn_use_item.gameObject.SetActive(false);
        }

        private void OpenModal()
        {
            if (!HasModal)
            {
                game_object_previous_selection = EventSystem.current?.currentSelectedGameObject;
            }

            game_object_modal.SetActive(true);
            SyncInteraction(false);
            Select(game_object_modal.GetComponentInChildren<Button>().gameObject);
        }

        public void CloseModal()
        {
            if (!HasModal)
            {
                return;
            }

            game_object_modal.SetActive(false);
            SyncInteraction(false);
            Select(game_object_previous_selection);
        }

        public void SyncInteraction(bool socialVisible)
        {
            canvas_group_home.interactable = !HasSection && !HasModal && !socialVisible;
            canvas_group_sections.interactable = !HasModal;
        }

        public void ShowStatus(string message) => txt_status.text = message;

        private static void Select(GameObject target)
        {
            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(target);
            }
        }
    }
}
