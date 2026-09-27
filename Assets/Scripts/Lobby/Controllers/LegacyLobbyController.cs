using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Zpd.Lobby
{
    /// <summary>Operates editor-authored objects only. No scene creation or network calls.</summary>
    [RequireComponent(typeof(CanvasGroup))]
    [RequireComponent(typeof(LegacyLobbyView))]
    public sealed class LegacyLobbyController : MonoBehaviour
    {
        private LegacyLobbyView legacy_lobby_view;

        public LegacyLobbyView View => legacy_lobby_view != null ? legacy_lobby_view : (legacy_lobby_view = GetComponent<LegacyLobbyView>());

        public LobbyPanel lobby_panel_profile { get => View.lobby_panel_profile; set => View.lobby_panel_profile = value; }
        public LobbyPanel lobby_panel_friends { get => View.lobby_panel_friends; set => View.lobby_panel_friends = value; }
        public LobbyPanel lobby_panel_inventory { get => View.lobby_panel_inventory; set => View.lobby_panel_inventory = value; }
        public LobbyPanel lobby_panel_characters { get => View.lobby_panel_characters; set => View.lobby_panel_characters = value; }
        public LobbyCharacterPicker lobby_character_picker { get => View.lobby_character_picker; set => View.lobby_character_picker = value; }
        public GameObject game_object_backdrop { get => View.game_object_backdrop; set => View.game_object_backdrop = value; }
        public GameObject[] game_object_friend_pages { get => View.game_object_friend_pages; set => View.game_object_friend_pages = value; }
        public Button[] btn_friend_tabs { get => View.btn_friend_tabs; set => View.btn_friend_tabs = value; }
        public InputField input_search { get => View.input_search; set => View.input_search = value; }
        public Text txt_search_status { get => View.txt_search_status; set => View.txt_search_status = value; }
        public Text txt_status { get => View.txt_status; set => View.txt_status = value; }
        public Text txt_item_details { get => View.txt_item_details; set => View.txt_item_details = value; }
        public Text txt_social_status { get => View.txt_social_status; set => View.txt_social_status = value; }

        [FormerlySerializedAs("heartAutomation")]
        public LobbyHeartAutomation lobby_heart_automation;

        public LegacyLobbyModel Model { get; } = new LegacyLobbyModel();

        private void Awake()
        {
            View.Initialize();
            SetFriendPage(0);
            lobby_heart_automation.ResetForAccount();
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                ClosePanel();
            }
        }

        private void Open(LobbySection section)
        {
            var previous = Model.Section;
            Model.Toggle(section);

            if (previous == LobbySection.Friends && Model.Section != LobbySection.Friends)
            {
                lobby_heart_automation.CloseWindow();
            }

            View.ShowSection(Model.Section);
        }

        public void ClosePanel()
        {
            if (Model.Section == LobbySection.Friends)
            {
                lobby_heart_automation.CloseWindow();
            }

            Model.Close();
            View.ShowSection(Model.Section);
        }

        public void OpenProfile()
        {
            Open(LobbySection.Profile);
            LogRequest("profile.get / self");
        }

        public void OpenFriends()
        {
            Open(LobbySection.Friends);

            if (Model.Section != LobbySection.Friends)
            {
                return;
            }

            ShowMyFriends();
            lobby_heart_automation.OpenWindow();
        }

        public void OpenInventory()
        {
            Open(LobbySection.Inventory);
            LogRequest("inventory.get / self");
        }

        public void OpenCharacters()
        {
            Open(LobbySection.Characters);
            LogRequest("characters.catalog / ownership / current");
        }

        // Preserve the existing persistent button callback.
        public void EnterBattle() => EnterSoloDefense();

        public void EnterSoloDefense()
        {
            if (Model.IsNavigating)
            {
                return;
            }

            if (!Zpd.Gameplay.SceneNavigation.CanLoad(Zpd.Gameplay.SceneNavigation.SoloDefense))
            {
                View.ShowStatus("Solo Defense is unavailable. Add its scene to the build.");
                return;
            }

            Model.IsNavigating = true;
            lobby_heart_automation.CloseWindow();

            if (!Zpd.Gameplay.SceneNavigation.Load(Zpd.Gameplay.SceneNavigation.SoloDefense))
            {
                Model.IsNavigating = false;
            }
        }

        public void ShowMyFriends()
        {
            SetFriendPage(0);
            LogRequest("friends.list");
        }

        public void ShowSearch() => SetFriendPage(1);

        public void ShowRecent()
        {
            SetFriendPage(2);
            LogRequest("friends.suggestions / recently-online");
        }

        public void ShowHearts()
        {
            SetFriendPage(3);
            LogRequest("hearts.balance / history");
        }

        public void RefreshSocial()
        {
            View.ClearSocialPage(Model.SocialPage);

            switch (Model.SocialPage)
            {
                case 0:
                    ShowMyFriends();
                    break;
                case 1:
                    SearchFriends();
                    break;
                case 2:
                    ShowRecent();
                    break;
                case 3:
                    ShowHearts();
                    break;
            }

            lobby_heart_automation.Refresh();
        }

        private void SetFriendPage(int index)
        {
            Model.SelectSocialPage(index);
            View.ShowSocialPage(Model.SocialPage);
        }

        public void SearchFriends()
        {
            Model.SetSearch(View.SearchQuery);
            View.ClearSocialPage(1);

            if (Model.SearchQuery.Length == 0)
            {
                View.ShowSearchStatus("Enter a player name first.");
                return;
            }

            LogRequest("friends.search / query=" + Model.SearchQuery);
            View.ShowSearchStatus("Service not connected. Search results are unavailable.");
        }

        public void InspectItem(string itemId) => View.ShowItemDetails("Item details unavailable until inventory data arrives.");

        private static void LogRequest(string operation) => Debug.Log(
            "[Lobby API stub] " + operation + " (log only; no request sent)");
    }
}
