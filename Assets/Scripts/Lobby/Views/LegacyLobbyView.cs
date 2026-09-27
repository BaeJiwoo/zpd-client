using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Zpd.Lobby
{
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class LegacyLobbyView : MonoBehaviour
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
        private LobbyPanel visible;
        private GameObject previousSelection;

        public string SearchQuery => searchInput.text;

        public void Initialize()
        {
            profile.Hide();
            friends.Hide();
            inventory.Hide();
            characters.Hide();
            characterPicker.ResetServerState();

            foreach (var row in friends.GetComponentsInChildren<LobbySocialSlot>(true))
            {
                row.Clear();
            }

            backdrop.SetActive(false);
        }

        public void ShowSection(LobbySection section)
        {
            LobbyPanel panel = section == LobbySection.Profile
                ? profile
                : section == LobbySection.Friends
                ? friends
                : section == LobbySection.Inventory
                ? inventory
                : section == LobbySection.Characters ? characters : null;

            if (visible == panel)
            {
                return;
            }

            if (visible != null)
            {
                visible.Hide(panel == null);
            }
            else if (EventSystem.current != null)
            {
                previousSelection = EventSystem.current.currentSelectedGameObject;
            }

            visible = panel;
            GetComponent<CanvasGroup>().interactable = panel == null;
            backdrop.SetActive(panel != null);

            if (panel != null)
            {
                panel.Show();
            }

            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(panel == null ? previousSelection : null);
            }
        }

        public void ShowSocialPage(int index)
        {
            for (int i = 0; i < friendPages.Length; i++)
            {
                friendPages[i].SetActive(i == index);
                friendTabs[i].interactable = i != index;
            }
        }

        public void ClearSocialPage(int index)
        {
            foreach (var row in friendPages[index].GetComponentsInChildren<LobbySocialSlot>(true))
            {
                row.Clear();
            }
        }

        public void ShowStatus(string message) => status.text = message;

        public void ShowSearchStatus(string message) => searchStatus.text = message;

        public void ShowItemDetails(string message) => itemDetails.text = message;
    }
}
