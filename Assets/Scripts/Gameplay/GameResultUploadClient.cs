using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace Zpd.Gameplay
{
    public sealed class GameResultUploadClient : MonoBehaviour
    {
        public Text statusText;
        public bool IsBusy { get; private set; }
        public bool Succeeded { get; private set; }
        public string RunId { get; private set; }
        private string payload;
        private string endpoint;
        private UnityWebRequest request;
        private Coroutine routine;
        [Serializable] private sealed class SaveResult { public string status; public string runId; public string resultId; }

        public void Submit(GameRunSnapshot snapshot, string apiBaseUrl)
        {
            Cancel();
            RunId = snapshot.runId;
            payload = JsonUtility.ToJson(snapshot);
            endpoint = apiBaseUrl.TrimEnd('/') + "/api/v1/me/game-results";
            Retry();
        }
        public void Retry()
        {
            if (IsBusy || Succeeded || string.IsNullOrEmpty(payload)) return;
            IsBusy = true;
            statusText.text = "GAME LOG: SAVING...";
            routine = StartCoroutine(Send());
        }
        private IEnumerator Send()
        {
            if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) || (uri.Scheme != "https" && uri.Scheme != "http"))
            { Fail("Invalid API address"); yield break; }
            request = new UnityWebRequest(endpoint, UnityWebRequest.kHttpVerbPOST);
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(payload));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Idempotency-Key", RunId + ":game-result");
            request.timeout = 5;
            request.redirectLimit = 0;
            UnityWebRequestAsyncOperation operation = null;
            try { operation = request.SendWebRequest(); }
            catch (InvalidOperationException error) { Debug.LogWarning("[Game Result] " + error.Message); }
            Debug.Log("[Game Result] POST " + endpoint + " runId=" + RunId);
            if (operation == null) Fail("Request could not start");
            else
            {
                yield return operation;
                SaveResult response = null;
                if (request.result == UnityWebRequest.Result.Success)
                {
                    try { response = JsonUtility.FromJson<SaveResult>(request.downloadHandler.text); }
                    catch (ArgumentException) { }
                }
                if (response != null && response.status == "stored" && response.runId == RunId && !string.IsNullOrWhiteSpace(response.resultId))
                {
                    IsBusy = false; Succeeded = true;
                    statusText.text = "GAME LOG: SAVED";
                    GameSessionTracker.MarkStored(RunId);
                }
                else Fail(request.responseCode > 0 ? "HTTP " + request.responseCode + " / unconfirmed result" : "Connection failed or timed out");
            }
            request.Dispose(); request = null; routine = null;
        }
        private void Fail(string reason)
        {
            IsBusy = false; Succeeded = false;
            statusText.text = "GAME LOG SAVE FAILED / " + reason;
            Debug.LogWarning("[Game Result] Save failed: " + reason);
        }
        public void Cancel()
        {
            if (routine != null) StopCoroutine(routine);
            routine = null;
            if (request != null) { request.Abort(); request.Dispose(); request = null; }
            IsBusy = false; Succeeded = false; payload = null; RunId = null;
        }
        private void OnDisable() { Cancel(); }
    }
}
