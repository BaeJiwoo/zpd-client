using UnityEngine;
using UnityEngine.UI;

namespace Zpd.Lobby
{
    public enum LobbySocialAction { SendHeart, SendFriendRequest, ReceiveHeart }

    /// <summary>A fixed, editor-authored row. Empty until a future service binds authoritative data.</summary>
    public sealed class LobbySocialSlot : MonoBehaviour
    {
        public LobbySocialAction action;
        public Text playerName;
        public Text detail;
        public Button actionButton;
        public Text feedback;
        private string targetUserId;
        private string receiptId;
        private bool allowed;

        public void Clear()
        {
            targetUserId = null;
            receiptId = null;
            allowed = false;
            playerName.text = "--";
            detail.text = action == LobbySocialAction.ReceiveHeart ? "Sender / received time: --" : "Player data: --";
            if (actionButton != null) actionButton.interactable = false;
        }

        // UI binding seam only; no API transport, mock response or client-side eligibility calculation.
        public void Bind(string userId, string displayName, string detailText, bool canAct, string heartReceiptId = null)
        {
            Clear();
            if (string.IsNullOrWhiteSpace(userId)) return;
            targetUserId = userId;
            receiptId = heartReceiptId;
            playerName.text = displayName;
            detail.text = detailText;
            allowed = canAct && (action != LobbySocialAction.ReceiveHeart || !string.IsNullOrWhiteSpace(receiptId));
            if (actionButton != null) actionButton.interactable = allowed && action == LobbySocialAction.SendFriendRequest;
        }

        public void RequestAction()
        {
            // Heart rows are informational. Automation consumes the complete service eligibility list.
            if (action != LobbySocialAction.SendFriendRequest) return;
            if (!allowed || string.IsNullOrWhiteSpace(targetUserId))
            {
                feedback.text = "Player data and eligibility are not available yet.";
                return;
            }
            Debug.Log("[Lobby API stub] friends.request.send targetUserId=" + targetUserId + " (log only; no request sent)");
            feedback.text = "Service not connected. Nothing was sent or received.";
            // Never change relationship, eligibility, inbox or heart balance without a server response.
        }
    }
}
