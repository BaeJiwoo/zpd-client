using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace Zpd.Lobby
{
    public enum LobbySocialAction
    {
        SendHeart,
        SendFriendRequest,
        ReceiveHeart
    }

    /// <summary>A fixed, editor-authored row. Empty until a future service binds authoritative data.</summary>
    public sealed class LobbySocialSlot : MonoBehaviour
    {
        [FormerlySerializedAs("action")]
        public LobbySocialAction social_action;

        [FormerlySerializedAs("playerName")]
        public Text txt_player_name;

        [FormerlySerializedAs("detail")]
        public Text txt_detail;

        [FormerlySerializedAs("actionButton")]
        public Button btn_action;

        [FormerlySerializedAs("feedback")]
        public Text txt_feedback;
        private string target_user_id;
        private string receipt_id;
        private bool is_action_allowed;

        public void Clear()
        {
            target_user_id = null;
            receipt_id = null;
            is_action_allowed = false;
            txt_player_name.text = "--";
            txt_detail.text = social_action == LobbySocialAction.ReceiveHeart ? "Sender / received time: --" : "Player data: --";

            if (btn_action != null)
            {
                btn_action.interactable = false;
            }
        }

        // UI binding seam only; no API transport, mock response or client-side eligibility calculation.

        public void Bind(
            string userId,
            string displayName,
            string detailText,
            bool canAct,
            string heartReceiptId = null)
        {
            Clear();

            if (string.IsNullOrWhiteSpace(userId))
            {
                return;
            }

            target_user_id = userId;
            receipt_id = heartReceiptId;
            txt_player_name.text = displayName;
            txt_detail.text = detailText;
            is_action_allowed = canAct && (social_action != LobbySocialAction.ReceiveHeart || !string.IsNullOrWhiteSpace(receipt_id));

            if (btn_action != null)
            {
                btn_action.interactable = is_action_allowed && social_action == LobbySocialAction.SendFriendRequest;
            }
        }

        public void RequestAction()
        {
            // Heart rows are informational. Automation consumes the complete service eligibility list.

            if (social_action != LobbySocialAction.SendFriendRequest)
            {
                return;
            }

            if (!is_action_allowed || string.IsNullOrWhiteSpace(target_user_id))
            {
                txt_feedback.text = "Player data and eligibility are not available yet.";
                return;
            }

            Debug.Log(
                "[Lobby API stub] friends.request.send targetUserId=" + target_user_id + " (log only; no request sent)");
            txt_feedback.text = "Service not connected. Nothing was sent or received.";
            // Never change relationship, eligibility, inbox or heart balance without a server response.
        }
    }
}
