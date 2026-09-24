using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Zpd.Lobby
{
    /// <summary>Automatic heart workflow, independent of visible rows. Transport is still log-only.</summary>
    public sealed class LobbyHeartAutomation : MonoBehaviour
    {
        [Serializable]
        public sealed class EligibleHeart
        {
            public string userId;
            // Receipt ID for receiving; stable server eligibility-cycle ID for sending.
            public string operationId;
            public bool allowed;
        }

        public Text feedback;
        private readonly HashSet<string> received = new HashSet<string>();
        private readonly HashSet<string> sent = new HashSet<string>();
        private bool windowOpen;
        private int revision;
        public int CurrentRevision => revision;

        public void OpenWindow()
        {
            windowOpen = true;
            Refresh();
        }

        public void CloseWindow()
        {
            windowOpen = false;
            revision++; // Reject a late eligibility result after the drawer closes.
        }

        public void Refresh()
        {
            if (!windowOpen) return;
            revision++;
            Debug.Log("[Lobby API stub] hearts.sync revision=" + revision +
                " / receive eligible hearts, then send to eligible friends (log only; no request sent)");
            feedback.text = "Hearts sync automatically here. Service not connected.";
        }

        // Binding seam for a future service, not a response parser. Do not infer eligibility from UI rows.
        public void ApplyEligibility(int requestRevision, EligibleHeart[] inbox, EligibleHeart[] friends)
        {
            if (!windowOpen || requestRevision != revision) return;
            Process(inbox, received, "hearts.receive");
            Process(friends, sent, "hearts.send");
            feedback.text = "Heart service not connected. No hearts were transferred.";
        }

        private static void Process(EligibleHeart[] entries, HashSet<string> attempted, string operation)
        {
            if (entries == null) return;
            foreach (var entry in entries)
            {
                if (entry == null || !entry.allowed || string.IsNullOrWhiteSpace(entry.userId) ||
                    string.IsNullOrWhiteSpace(entry.operationId) || !attempted.Add(entry.operationId)) continue;
                Debug.Log("[Lobby API stub] " + operation + " targetUserId=" + entry.userId +
                    " operationId=" + entry.operationId + " (automatic; log only; no request sent)");
            }
        }

        public void ResetForAccount()
        {
            CloseWindow();
            received.Clear();
            sent.Clear();
            feedback.text = "Heart data: --";
        }
    }
}
