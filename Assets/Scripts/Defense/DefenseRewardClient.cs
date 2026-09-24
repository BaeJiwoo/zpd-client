using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace Zpd.Defense
{
    [Serializable]
    public sealed class DefenseRunReport
    {
        public string runId;
        public string mode = "solo_defense";
        public string reason;
        public int claimedKills;
        public int reachedWave;
        public float survivalSeconds;
    }

    /// <summary>Real HTTP prototype request. Local run statistics are untrusted, not proof of rewards.</summary>
    public sealed class DefenseRewardClient : MonoBehaviour
    {
        [Tooltip("Prototype endpoint. Default intentionally points to an unimplemented local service.")]
        public string apiBaseUrl = "https://127.0.0.1:18080";
        [Range(1, 30)] public int timeoutSeconds = 5;
        public Text title;
        public Text detail;
        public Button retryButton;
        public bool IsBusy { get; private set; }
        public bool Succeeded { get; private set; }
        public string RunId { get; private set; }
        private string payload;
        private string endpoint;
        private UnityWebRequest request;
        private Coroutine routine;

        [Serializable]
        private sealed class RewardResult
        {
            public string status;
            public string runId;
            public string settlementId;
            public int earnedExperience = -1;
        }

        public void Submit(DefenseRunReport report)
        {
            Cancel();
            RunId = report.runId;
            payload = JsonUtility.ToJson(report); // Immutable across retries; a new round gets a new ID.
            endpoint = apiBaseUrl.TrimEnd('/') + "/api/v1/prototype/defense-runs/" + UnityWebRequest.EscapeURL(RunId) + "/rewards";
            Retry();
        }

        public void Retry()
        {
            if (IsBusy || Succeeded || string.IsNullOrEmpty(payload)) return;
            if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) || (uri.Scheme != "http" && uri.Scheme != "https"))
            {
                Fail("The reward API address is invalid.\nNo experience was awarded.");
                return;
            }
            IsBusy = true;
            retryButton.interactable = false;
            title.text = "REQUESTING REWARD...";
            detail.text = "Waiting for the reward service.\nExperience has not been awarded.";
            routine = StartCoroutine(Send());
        }

        private IEnumerator Send()
        {
            request = new UnityWebRequest(endpoint, UnityWebRequest.kHttpVerbPOST);
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(payload));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Idempotency-Key", RunId);
            request.timeout = timeoutSeconds;
            request.redirectLimit = 0;
            Debug.Log("[Defense Reward] POST " + endpoint + " runId=" + RunId);
            UnityWebRequestAsyncOperation operation = null;
            string startupError = null;
            try { operation = request.SendWebRequest(); }
            catch (InvalidOperationException error) { startupError = error.Message; }
            if (operation == null)
            {
                Fail("Unable to start the reward request.\nNo experience was awarded.");
                Debug.LogWarning("[Defense Reward] Request could not start: " + startupError);
                request.Dispose(); request = null; routine = null;
                yield break;
            }
            yield return operation;

            if (request.result != UnityWebRequest.Result.Success)
            {
                string cause = request.responseCode > 0 ? "HTTP " + request.responseCode : "Connection failed or timed out";
                Fail(cause + ".\nNo experience was awarded. Retry or start a new run.");
                Debug.LogWarning("[Defense Reward] Request failed: " + request.error);
            }
            else
            {
                RewardResult result = null;
                try
                {
                    var parsed = new RewardResult();
                    JsonUtility.FromJsonOverwrite(request.downloadHandler.text, parsed);
                    result = parsed;
                }
                catch (ArgumentException) { }
                if (result == null || result.status != "settled" || result.runId != RunId ||
                    string.IsNullOrWhiteSpace(result.settlementId) || result.earnedExperience < 0)
                    Fail("The service returned an invalid or unconfirmed result.\nNo experience was awarded.");
                else
                {
                    Succeeded = true;
                    IsBusy = false;
                    title.text = "REWARD CONFIRMED";
                    detail.text = "Service result: +" + result.earnedExperience + " EXP\nPrototype display only. No local profile is modified.";
                    retryButton.interactable = false;
                }
            }
            request.Dispose();
            request = null;
            routine = null;
        }

        private void Fail(string message)
        {
            IsBusy = false;
            Succeeded = false;
            title.text = "REWARD REQUEST FAILED";
            detail.text = message;
            retryButton.interactable = true;
        }

        public void Cancel()
        {
            if (routine != null) StopCoroutine(routine);
            routine = null;
            if (request != null) { request.Abort(); request.Dispose(); request = null; }
            IsBusy = false;
            Succeeded = false;
            payload = null;
            RunId = null;
            if (retryButton != null) retryButton.interactable = false;
        }

        private void OnDisable() { Cancel(); }
    }
}
