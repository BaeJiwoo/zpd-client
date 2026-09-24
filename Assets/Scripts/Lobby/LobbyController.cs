using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Zpd.Lobby
{
    /// <summary>Operates editor-authored objects only. No scene creation or network calls.</summary>
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class LobbyController : MonoBehaviour
    {
        public LobbyPanel profile;
        public LobbyPanel friends;
        public LobbyPanel inventory;
        public LobbyPanel characters;
        public LobbyCharacterPicker characterPicker;
        public GameObject backdrop;
        public GameObject[] friendPages;
        public Button[] friendTabs;
        public InputField searchInput;
        public Text searchStatus;
        public Text status;
        public Text itemDetails;
        public Text socialStatus;
        public LobbyHeartAutomation heartAutomation;
        private int selectedSocialPage;
        private LobbyPanel current;
        private GameObject previousSelection;

        private void Awake()
        {
            profile.Hide();
            friends.Hide();
            inventory.Hide();
            characters.Hide();
            characterPicker.ResetServerState();
            foreach (var row in friends.GetComponentsInChildren<LobbySocialSlot>(true)) row.Clear();
            backdrop.SetActive(false);
            SetFriendPage(0);
            heartAutomation.ResetForAccount();
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                ClosePanel();
        }

        private void Open(LobbyPanel panel)
        {
            if (current == panel) { ClosePanel(); return; }
            if (current == friends && panel != friends) heartAutomation.CloseWindow();
            if (current != null) current.Hide();
            else previousSelection = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            if (panel != profile) profile.Hide();
            if (panel != friends) friends.Hide();
            if (panel != inventory) inventory.Hide();
            if (panel != characters) characters.Hide();
            current = panel;
            GetComponent<CanvasGroup>().interactable = false;
            backdrop.SetActive(true);
            panel.Show();
            // Focus moves after the opening tween; disabled controls cannot receive submit events.
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        }

        public void ClosePanel()
        {
            if (current == null) return;
            if (current == friends) heartAutomation.CloseWindow();
            current.Hide(true);
            current = null;
            GetComponent<CanvasGroup>().interactable = true;
            backdrop.SetActive(false);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(previousSelection);
        }

        public void OpenProfile() { Open(profile); LogRequest("profile.get / self"); }
        public void OpenFriends()
        {
            Open(friends);
            if (current != friends) return;
            ShowMyFriends();
            heartAutomation.OpenWindow();
        }
        public void OpenInventory() { Open(inventory); LogRequest("inventory.get / self"); }
        public void OpenCharacters()
        {
            Open(characters);
            LogRequest("characters.catalog / ownership / current");
        }
        public void EnterBattle()
        {
            LogRequest("battle.enter / mode=dedicated_battle");
            status.text = "Service not connected. Battle entry was not sent.";
        }
        public void ShowMyFriends() { SetFriendPage(0); LogRequest("friends.list"); }
        public void ShowSearch() { SetFriendPage(1); }
        public void ShowRecent() { SetFriendPage(2); LogRequest("friends.suggestions / recently-online"); }
        public void ShowHearts() { SetFriendPage(3); LogRequest("hearts.balance / history"); }

        public void RefreshSocial()
        {
            foreach (var row in friendPages[selectedSocialPage].GetComponentsInChildren<LobbySocialSlot>(true)) row.Clear();
            switch (selectedSocialPage)
            {
                case 0: ShowMyFriends(); break;
                case 1: SearchFriends(); break;
                case 2: ShowRecent(); break;
                case 3: ShowHearts(); break;
            }
            heartAutomation.Refresh();
        }

        private void SetFriendPage(int index)
        {
            selectedSocialPage = index;
            for (int i = 0; i < friendPages.Length; i++)
            {
                friendPages[i].SetActive(i == index);
                friendTabs[i].interactable = i != index;
            }
        }

        public void SearchFriends()
        {
            string query = searchInput.text.Trim();
            foreach (var row in friendPages[1].GetComponentsInChildren<LobbySocialSlot>(true)) row.Clear();
            if (query.Length == 0) { searchStatus.text = "Enter a player name first."; return; }
            LogRequest("friends.search / query=" + query.Replace("\n", " ").Replace("\r", " "));
            searchStatus.text = "Service not connected. Search results are unavailable.";
        }

        public void InspectItem(string itemId)
        {
            itemDetails.text = "Item details unavailable until inventory data arrives.";
        }

        private static void LogRequest(string operation)
        {
            Debug.Log("[Lobby API stub] " + operation + " (log only; no request sent)");
        }
    }
}
