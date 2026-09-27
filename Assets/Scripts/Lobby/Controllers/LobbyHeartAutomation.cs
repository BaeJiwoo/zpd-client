using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace Zpd.Lobby
{
    /// <summary>Automatic heart workflow, independent of visible rows. Transport is still log-only.</summary>
    public sealed class LobbyHeartAutomation : MonoBehaviour
    {
        [FormerlySerializedAs("feedback")]
        public Text txt_feedback;
        private readonly HashSet<string> attempted_receive_operation_ids = new HashSet<string>();
        private readonly HashSet<string> attempted_send_operation_ids = new HashSet<string>();
        private bool is_window_open;
        private int eligibility_revision;

        public int CurrentRevision => eligibility_revision;

        public void OpenWindow()
        {
            is_window_open = true;
            Refresh();
        }

        public void CloseWindow()
        {
            is_window_open = false;
            eligibility_revision++; // Reject a late eligibility result after the drawer closes.
        }

        public void Refresh()
        {
            if (!is_window_open)
            {
                return;
            }

            eligibility_revision++;
            Debug.Log(
                "[Lobby API stub] hearts.sync revision=" + eligibility_revision + " / receive eligible hearts, then send to eligible friends (log only; no request sent)");
            txt_feedback.text = "Hearts sync automatically here. Service not connected.";
        }

        // Binding seam for a future service, not a response parser. Do not infer eligibility from UI rows.

        public void ApplyEligibility(int requestRevision, EligibleHeart[] inbox, EligibleHeart[] friends)
        {
            if (!is_window_open || requestRevision != eligibility_revision)
            {
                return;
            }

            Process(inbox, attempted_receive_operation_ids, "hearts.receive");
            Process(friends, attempted_send_operation_ids, "hearts.send");
            txt_feedback.text = "Heart service not connected. No hearts were transferred.";
        }

        private static void Process(EligibleHeart[] entries, HashSet<string> attempted, string operation)
        {
            if (entries == null)
            {
                return;
            }

            foreach (var entry in entries)
            {
                if (entry == null || !entry.allowed || string.IsNullOrWhiteSpace(entry.userId) || string.IsNullOrWhiteSpace(entry.operationId) || !attempted.Add(entry.operationId))
                {
                    continue;
                }

                Debug.Log(
                    "[Lobby API stub] " + operation + " targetUserId=" + entry.userId + " operationId=" + entry.operationId + " (automatic; log only; no request sent)");
            }
        }

        public void ResetForAccount()
        {
            CloseWindow();
            attempted_receive_operation_ids.Clear();
            attempted_send_operation_ids.Clear();
            txt_feedback.text = "Heart data: --";
        }
    }
}
