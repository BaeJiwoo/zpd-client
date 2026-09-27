using System;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace Zpd.Lobby
{
    public sealed class LobbyCharacterPicker : MonoBehaviour
    {
        [Serializable]
        public sealed class Slot
        {
            [FormerlySerializedAs("artKey")]
            public string art_key;

            [FormerlySerializedAs("state")]
            public Text txt_state;

            [FormerlySerializedAs("previewLabel")]
            public Text txt_preview;

            [FormerlySerializedAs("lobbyArt")]
            public GameObject game_object_lobby_art;

            [FormerlySerializedAs("profileArt")]
            public GameObject game_object_profile_art;

            [NonSerialized]
            public string server_character_id;

            [NonSerialized]
            public bool is_owned;
        }

        [FormerlySerializedAs("slots")]
        public Slot[] character_slots;

        [FormerlySerializedAs("applyButton")]
        public Button btn_apply;

        [FormerlySerializedAs("feedback")]
        public Text txt_feedback;

        [FormerlySerializedAs("currentCharacter")]
        public Text txt_current_character;

        [FormerlySerializedAs("homeTag")]
        public Text txt_home_tag;
        private int selected_slot_index = -1;
        private string confirmed_character_id;

        public void ResetServerState()
        {
            selected_slot_index = -1;
            confirmed_character_id = null;

            foreach (var slot in character_slots)
            {
                slot.server_character_id = null;
                slot.is_owned = false;
                slot.txt_state.text = "Ownership: --";
                slot.game_object_lobby_art.SetActive(false);
                slot.game_object_profile_art.SetActive(false);
            }

            // This image is explicitly a local art preview, never the player's selected character.

            character_slots[0].game_object_lobby_art.SetActive(true);
            character_slots[0].game_object_profile_art.SetActive(true);
            txt_current_character.text = "CHARACTER: --";
            txt_home_tag.text = "LOCAL ART PREVIEW";
            txt_feedback.text = "Preview artwork locally. Ownership and current character: --";
            RefreshApply();
        }

        public void Preview(int index)
        {
            if (index < 0 || index >= character_slots.Length)
            {
                return;
            }

            selected_slot_index = index;
            txt_feedback.text = "Preview " + (index + 1).ToString("00") + ": " + (string.IsNullOrEmpty(character_slots[index].server_character_id)
                ? "ownership is unknown."
                : character_slots[index].is_owned ? "owned character." : "not owned.") + "\nYour active character has not changed.";
            RefreshApply();
        }

        // Future service maps server IDs to local art keys; local art availability never implies ownership.

        public void BindOwnership(string artKey, string serverCharacterId, bool isOwned)
        {
            var slot = Array.Find(character_slots, s => s.art_key == artKey);

            if (slot == null)
            {
                return;
            }

            slot.server_character_id = string.IsNullOrWhiteSpace(serverCharacterId) ? null : serverCharacterId;
            slot.is_owned = slot.server_character_id != null && isOwned;
            slot.txt_state.text = slot.server_character_id == null ? "Ownership: --" : slot.is_owned ? "OWNED" : "NOT OWNED";
            RefreshApply();
        }

        public void RequestChange()
        {
            if (!CanApply())
            {
                txt_feedback.text = "A server-confirmed owned character is required.";
                return;
            }

            Debug.Log(
                "[Lobby API stub] characters.change characterId=" + character_slots[selected_slot_index].server_character_id + " (log only; no request sent)");
            txt_feedback.text = "Service not connected. Character change was not sent.\nYour active character has not changed.";
        }

        // Called only with a successful authoritative profile/change result by the future service adapter.
        // A rejected change never calls this method and therefore leaves both portraits untouched.

        public bool ApplyConfirmedCharacter(string serverCharacterId)
        {
            if (string.IsNullOrWhiteSpace(serverCharacterId))
            {
                return false;
            }

            int index = Array.FindIndex(character_slots, s => s.server_character_id == serverCharacterId && s.is_owned);

            if (index < 0)
            {
                return false;
            }

            confirmed_character_id = serverCharacterId;

            for (int i = 0; i < character_slots.Length; i++)
            {
                character_slots[i].game_object_lobby_art.SetActive(i == index);
                character_slots[i].game_object_profile_art.SetActive(i == index);
            }

            txt_current_character.text = "CHARACTER " + (index + 1).ToString("00");
            txt_home_tag.text = "CURRENT CHARACTER";
            txt_feedback.text = "Active character confirmed by the service.";
            RefreshApply();
            return true;
        }

        private bool CanApply() => selected_slot_index >= 0 && character_slots[selected_slot_index].is_owned && !string.IsNullOrWhiteSpace(character_slots[selected_slot_index].server_character_id) && character_slots[selected_slot_index].server_character_id != confirmed_character_id;

        private void RefreshApply()
        {
            btn_apply.interactable = CanApply();

            for (int i = 0; i < character_slots.Length; i++)
            {
                character_slots[i].txt_preview.text = i == selected_slot_index ? "SELECTED PREVIEW" : "PREVIEW " + (i + 1).ToString("00");
            }
        }
    }
}
