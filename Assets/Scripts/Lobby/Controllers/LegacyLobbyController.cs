using UnityEngine;
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
        private LegacyLobbyView view;

        public LegacyLobbyView View => view != null ? view : (view = GetComponent<LegacyLobbyView>());

        public LobbyPanel profile { get => View.profile; set => View.profile = value; }
        public LobbyPanel friends { get => View.friends; set => View.friends = value; }
        public LobbyPanel inventory { get => View.inventory; set => View.inventory = value; }
        public LobbyPanel characters { get => View.characters; set => View.characters = value; }
        public LobbyCharacterPicker characterPicker { get => View.characterPicker; set => View.characterPicker = value; }
        public GameObject backdrop { get => View.backdrop; set => View.backdrop = value; }
        public GameObject[] friendPages { get => View.friendPages; set => View.friendPages = value; }
        public Button[] friendTabs { get => View.friendTabs; set => View.friendTabs = value; }
        public InputField searchInput { get => View.searchInput; set => View.searchInput = value; }
        public Text searchStatus { get => View.searchStatus; set => View.searchStatus = value; }
        public Text status { get => View.status; set => View.status = value; }
        public Text itemDetails { get => View.itemDetails; set => View.itemDetails = value; }
        public Text socialStatus { get => View.socialStatus; set => View.socialStatus = value; }

        public LobbyHeartAutomation heartAutomation;

        public LegacyLobbyModel Model { get; } = new LegacyLobbyModel();

        private void Awake()
        {
            View.Initialize();
            SetFriendPage(0);
            heartAutomation.ResetForAccount();
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
                heartAutomation.CloseWindow();
            }

            View.ShowSection(Model.Section);
        }

        public void ClosePanel()
        {
            if (Model.Section == LobbySection.Friends)
            {
                heartAutomation.CloseWindow();
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
            heartAutomation.OpenWindow();
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
            heartAutomation.CloseWindow();

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

            heartAutomation.Refresh();
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
