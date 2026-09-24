using System;
using UnityEngine;
using UnityEngine.UI;

namespace Zpd.Lobby
{
    public sealed class LobbyCharacterPicker : MonoBehaviour
    {
        [Serializable]
        public sealed class Slot
        {
            public string artKey;
            public Text state;
            public Text previewLabel;
            public GameObject lobbyArt;
            public GameObject profileArt;
            [NonSerialized] public string serverId;
            [NonSerialized] public bool owned;
        }
        public Slot[] slots;
        public Button applyButton;
        public Text feedback;
        public Text currentCharacter;
        public Text homeTag;
        private int selected = -1;
        private string confirmedId;

        public void ResetServerState()
        {
            selected = -1;
            confirmedId = null;
            foreach (var slot in slots)
            {
                slot.serverId = null;
                slot.owned = false;
                slot.state.text = "Ownership: --";
                slot.lobbyArt.SetActive(false);
                slot.profileArt.SetActive(false);
            }
            // This image is explicitly a local art preview, never the player's selected character.
            slots[0].lobbyArt.SetActive(true);
            slots[0].profileArt.SetActive(true);
            currentCharacter.text = "CHARACTER: --";
            homeTag.text = "LOCAL ART PREVIEW";
            feedback.text = "Preview artwork locally. Ownership and current character: --";
            RefreshApply();
        }

        public void Preview(int index)
        {
            if (index < 0 || index >= slots.Length) return;
            selected = index;
            feedback.text = "Preview " + (index + 1).ToString("00") + ": " +
                (string.IsNullOrEmpty(slots[index].serverId) ? "ownership is unknown." : slots[index].owned ? "owned character." : "not owned.") +
                "\nYour active character has not changed.";
            RefreshApply();
        }

        // Future service maps server IDs to local art keys; local art availability never implies ownership.
        public void BindOwnership(string artKey, string serverCharacterId, bool isOwned)
        {
            var slot = Array.Find(slots, s => s.artKey == artKey);
            if (slot == null) return;
            slot.serverId = string.IsNullOrWhiteSpace(serverCharacterId) ? null : serverCharacterId;
            slot.owned = slot.serverId != null && isOwned;
            slot.state.text = slot.serverId == null ? "Ownership: --" : slot.owned ? "OWNED" : "NOT OWNED";
            RefreshApply();
        }

        public void RequestChange()
        {
            if (!CanApply()) { feedback.text = "A server-confirmed owned character is required."; return; }
            Debug.Log("[Lobby API stub] characters.change characterId=" + slots[selected].serverId + " (log only; no request sent)");
            feedback.text = "Service not connected. Character change was not sent.\nYour active character has not changed.";
        }

        // Called only with a successful authoritative profile/change result by the future service adapter.
        // A rejected change never calls this method and therefore leaves both portraits untouched.
        public bool ApplyConfirmedCharacter(string serverCharacterId)
        {
            if (string.IsNullOrWhiteSpace(serverCharacterId)) return false;
            int index = Array.FindIndex(slots, s => s.serverId == serverCharacterId && s.owned);
            if (index < 0) return false;
            confirmedId = serverCharacterId;
            for (int i = 0; i < slots.Length; i++)
            {
                slots[i].lobbyArt.SetActive(i == index);
                slots[i].profileArt.SetActive(i == index);
            }
            currentCharacter.text = "CHARACTER " + (index + 1).ToString("00");
            homeTag.text = "CURRENT CHARACTER";
            feedback.text = "Active character confirmed by the service.";
            RefreshApply();
            return true;
        }

        private bool CanApply() => selected >= 0 && slots[selected].owned &&
            !string.IsNullOrWhiteSpace(slots[selected].serverId) && slots[selected].serverId != confirmedId;
        private void RefreshApply()
        {
            applyButton.interactable = CanApply();
            for (int i = 0; i < slots.Length; i++)
                slots[i].previewLabel.text = i == selected ? "SELECTED PREVIEW" : "PREVIEW " + (i + 1).ToString("00");
        }
    }
}
