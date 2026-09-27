using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Zpd.Lobby
{
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class LegacyLobbyView : MonoBehaviour
    {
        [FormerlySerializedAs("profile")]
        public LobbyPanel lobby_panel_profile;

        [FormerlySerializedAs("friends")]
        public LobbyPanel lobby_panel_friends;

        [FormerlySerializedAs("inventory")]
        public LobbyPanel lobby_panel_inventory;

        [FormerlySerializedAs("characters")]
        public LobbyPanel lobby_panel_characters;

        [FormerlySerializedAs("characterPicker")]
        public LobbyCharacterPicker lobby_character_picker;

        [FormerlySerializedAs("backdrop")]
        public GameObject game_object_backdrop;

        [FormerlySerializedAs("friendPages")]
        public GameObject[] game_object_friend_pages;

        [FormerlySerializedAs("friendTabs")]
        public Button[] btn_friend_tabs;

        [FormerlySerializedAs("searchInput")]
        public InputField input_search;

        [FormerlySerializedAs("searchStatus")]
        public Text txt_search_status;

        [FormerlySerializedAs("status")]
        public Text txt_status;

        [FormerlySerializedAs("itemDetails")]
        public Text txt_item_details;

        [FormerlySerializedAs("socialStatus")]
        public Text txt_social_status;
        private LobbyPanel lobby_panel_visible;
        private GameObject game_object_previous_selection;

        public string SearchQuery => input_search.text;

        public void Initialize()
        {
            lobby_panel_profile.Hide();
            lobby_panel_friends.Hide();
            lobby_panel_inventory.Hide();
            lobby_panel_characters.Hide();
            lobby_character_picker.ResetServerState();

            foreach (var row in lobby_panel_friends.GetComponentsInChildren<LobbySocialSlot>(true))
            {
                row.Clear();
            }

            game_object_backdrop.SetActive(false);
        }

        public void ShowSection(LobbySection section)
        {
            LobbyPanel panel = section == LobbySection.Profile
                ? lobby_panel_profile
                : section == LobbySection.Friends
                ? lobby_panel_friends
                : section == LobbySection.Inventory
                ? lobby_panel_inventory
                : section == LobbySection.Characters ? lobby_panel_characters : null;

            if (lobby_panel_visible == panel)
            {
                return;
            }

            if (lobby_panel_visible != null)
            {
                lobby_panel_visible.Hide(panel == null);
            }
            else if (EventSystem.current != null)
            {
                game_object_previous_selection = EventSystem.current.currentSelectedGameObject;
            }

            lobby_panel_visible = panel;
            GetComponent<CanvasGroup>().interactable = panel == null;
            game_object_backdrop.SetActive(panel != null);

            if (panel != null)
            {
                panel.Show();
            }

            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(panel == null ? game_object_previous_selection : null);
            }
        }

        public void ShowSocialPage(int index)
        {
            for (int i = 0; i < game_object_friend_pages.Length; i++)
            {
                game_object_friend_pages[i].SetActive(i == index);
                btn_friend_tabs[i].interactable = i != index;
            }
        }

        public void ClearSocialPage(int index)
        {
            foreach (var row in game_object_friend_pages[index].GetComponentsInChildren<LobbySocialSlot>(true))
            {
                row.Clear();
            }
        }

        public void ShowStatus(string message) => txt_status.text = message;

        public void ShowSearchStatus(string message) => txt_search_status.text = message;

        public void ShowItemDetails(string message) => txt_item_details.text = message;
    }
}
