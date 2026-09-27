using Zpd.Networking;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Zpd.Lobby
{
    /// <summary>Authored UI references, rendering and focus. Never changes account data or calls services.</summary>
    public sealed class LobbyView : MonoBehaviour
    {
        public CanvasGroup home, sections;
        public GameObject profilePanel, inventoryPanel;
        public Text nickname, level, record, history, inventoryStatus, status, emptyState;
        public Transform content;
        public LobbyItemCard cardTemplate;
        public Button[] filters;
        public GameObject modal, quantityRoot;
        public Text modalTitle, modalDescription, modalQuantity;
        public Slider quantitySlider;
        public Button useButton, soloButton;

        [Serializable]
        public sealed class Icon
        {
            public string key;
            public Sprite sprite;
        }

        public Icon[] icons = Array.Empty<Icon>();
        public event Action<string> ItemSelected;
        private readonly List<LobbyItemCard> cards = new List<LobbyItemCard>();
        private GameObject previousSelection, sectionSelection;

        public bool HasModal => modal.activeSelf;
        public bool HasSection => sections.gameObject.activeSelf;

        private void OnValidate() => ResolveReferences();

        // Also repairs references after a script reload in an already-open pre-MVC scene.

        public void ResolveReferences()
        {
            if (home == null)
            {
                home = Find<CanvasGroup>("Home");
            }

            if (sections == null)
            {
                sections = Find<CanvasGroup>("Feature Windows");
            }

            if (profilePanel == null)
            {
                profilePanel = transform.Find("Feature Windows/Player Profile")?.gameObject;
            }

            if (inventoryPanel == null)
            {
                inventoryPanel = transform.Find("Feature Windows/Inventory")?.gameObject;
            }

            if (modal == null)
            {
                modal = transform.Find("Item Modal")?.gameObject;
            }

            string p = "Feature Windows/Player Profile/", i = "Feature Windows/Inventory/", d = "Item Modal/Details/";

            if (nickname == null)
            {
                nickname = Find<Text>(p + "Nickname");
            }

            if (level == null)
            {
                level = Find<Text>(p + "Level");
            }

            if (record == null)
            {
                record = Find<Text>(p + "Record");
            }

            if (history == null)
            {
                history = Find<Text>(p + "History Viewport/History");
            }

            if (inventoryStatus == null)
            {
                inventoryStatus = Find<Text>(i + "Inventory Status");
            }

            if (status == null)
            {
                status = Find<Text>("Home/Status");
            }

            if (emptyState == null)
            {
                emptyState = Find<Text>(i + "Inventory Viewport/Empty State");
            }

            if (content == null)
            {
                content = transform.Find(i + "Inventory Viewport/Items");
            }

            if (cardTemplate == null)
            {
                cardTemplate = Find<LobbyItemCard>(i + "Inventory Viewport/Items/Item Template");
            }

            if (filters == null || filters.Length != 3 || filters.Any(b => b == null))
            {
                filters = new[]
                {
                    Find<Button>(i + "All"),
                    Find<Button>(i + "Consumables"),
                    Find<Button>(i + "Equipment")
                };
            }

            if (quantityRoot == null)
            {
                quantityRoot = transform.Find(d + "Owned Quantity")?.gameObject;
            }

            if (modalTitle == null)
            {
                modalTitle = Find<Text>(d + "Title");
            }

            if (modalDescription == null)
            {
                modalDescription = Find<Text>(d + "Description");
            }

            if (modalQuantity == null)
            {
                modalQuantity = Find<Text>(d + "Owned Quantity/Quantity");
            }

            if (quantitySlider == null)
            {
                quantitySlider = Find<Slider>(d + "Owned Quantity/Owned Amount");
            }

            if (useButton == null)
            {
                useButton = Find<Button>(d + "Use Item");
            }

            if (soloButton == null)
            {
                soloButton = Find<Button>("Home/Solo Defense");
            }
        }

        private T Find<T>(string path)
            where T : Component => transform.Find(path)?.GetComponent<T>();

        public void Initialize()
        {
            ResolveReferences();
            sections.gameObject.SetActive(false);
            profilePanel.SetActive(false);
            inventoryPanel.SetActive(false);
            modal.SetActive(false);
            cardTemplate.gameObject.SetActive(false);

            foreach (var label in GetComponentsInChildren<Text>(true))
            {
                label.supportRichText = false;
            }
        }

        public void RenderProfile(LobbyModel model)
        {
            var p = model.Profile;
            nickname.text = p?.Nickname ?? (AuthManager.Instance.IsSignedIn ? "PLAYER " + AuthManager.Instance.PlayerId : "PLAYER --");
            level.text = p == null ? "LEVEL --" : "LEVEL " + p.Level;
            record.text = p == null
                ? "MATCHES -- / WIN RATE --"
                : p.Matches == 0
                ? "0 MATCHES / WIN RATE --"
                : $"{p.Matches} MATCHES / {p.Wins}W {p.Losses}L / WIN RATE {p.WinRate:0.#}%";
            history.text = model.ProfileState == LobbyLoadState.Loading
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
            foreach (var card in cards)
            {
                card.gameObject.SetActive(false);
                Destroy(card.gameObject);
            }

            cards.Clear();

            foreach (var item in model.VisibleItems)
            {
                var card = Instantiate(cardTemplate, content);
                card.gameObject.SetActive(true);
                card.Bind(
                    item,
                    icons.FirstOrDefault(i => i.key == item.IconKey)?.sprite,
                    id => ItemSelected?.Invoke(id));
                cards.Add(card);
            }

            for (int i = 0; i < filters.Length; i++)
            {
                filters[i].interactable = i != model.Filter;
            }

            inventoryStatus.text = model.InventoryState == LobbyLoadState.Loading
                ? "Loading inventory..."
                : model.InventoryState == LobbyLoadState.Error
                ? model.InventoryError
                : model.InventoryState == LobbyLoadState.Unavailable
                ? "Inventory service not connected."
                : $"{cards.Count} ITEMS";
            emptyState.gameObject.SetActive(cards.Count == 0);
            emptyState.text = model.InventoryState == LobbyLoadState.Ready
                ? "No owned items in this category."
                : inventoryStatus.text;
        }

        public void ShowSection(LobbyWindow window)
        {
            CloseModal();

            if (!HasSection)
            {
                sectionSelection = EventSystem.current?.currentSelectedGameObject;
            }

            profilePanel.SetActive(window == LobbyWindow.Profile);
            inventoryPanel.SetActive(window == LobbyWindow.Inventory);
            sections.gameObject.SetActive(true);
            SyncInteraction(false);
            Select(
                (window == LobbyWindow.Profile ? profilePanel : inventoryPanel).GetComponentInChildren<Button>().gameObject);
        }

        public void CloseSection()
        {
            CloseModal();

            if (!HasSection)
            {
                return;
            }

            sections.gameObject.SetActive(false);
            profilePanel.SetActive(false);
            inventoryPanel.SetActive(false);
            SyncInteraction(false);
            Select(sectionSelection);
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
            modalTitle.text = item.Name;
            bool consumable = item.Kind == LobbyItemKind.Consumable;
            modalDescription.text = (consumable ? "CONSUMABLE" : "EQUIPMENT") + "\n\n" + item.Description;

            if (consumable && !serviceAvailable)
            {
                modalDescription.text += "\n\nItem service not connected.";
            }

            if (model.IsUsingItem)
            {
                modalDescription.text = "Requesting item use...";
            }
            else if (consumable && !string.IsNullOrEmpty(model.UseError))
            {
                modalDescription.text += "\n\n" + model.UseError;
            }

            quantityRoot.SetActive(consumable);
            useButton.gameObject.SetActive(consumable);
            quantitySlider.maxValue = Mathf.Max(1, item.Capacity);
            quantitySlider.value = item.Quantity;
            modalQuantity.text = "OWNED  " + item.Quantity;
            useButton.interactable = serviceAvailable && model.CanUseSelected;
        }

        public void ShowMultiPlay()
        {
            OpenModal();
            modalTitle.text = "MULTI PLAY";
            modalDescription.text = "Multiplayer gameplay is not available yet.";
            quantityRoot.SetActive(false);
            useButton.gameObject.SetActive(false);
        }

        private void OpenModal()
        {
            if (!HasModal)
            {
                previousSelection = EventSystem.current?.currentSelectedGameObject;
            }

            modal.SetActive(true);
            SyncInteraction(false);
            Select(modal.GetComponentInChildren<Button>().gameObject);
        }

        public void CloseModal()
        {
            if (!HasModal)
            {
                return;
            }

            modal.SetActive(false);
            SyncInteraction(false);
            Select(previousSelection);
        }

        public void SyncInteraction(bool socialVisible)
        {
            home.interactable = !HasSection && !HasModal && !socialVisible;
            sections.interactable = !HasModal;
        }

        public void ShowStatus(string message) => status.text = message;

        private static void Select(GameObject target)
        {
            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(target);
            }
        }
    }
}
