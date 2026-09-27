using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Zpd.Networking;
using Zpd.Networking.DTO;
using Zpd.Lobby;

namespace Zpd.Gameplay
{
    public sealed class GameResultUploadClient : MonoBehaviour
    {
        public event Action Changed;

        public string Status { get; private set; } = "";
        public bool IsBusy { get; private set; }
        public bool Succeeded { get; private set; }
        public string RunId { get; private set; }

        private string payload;
        private AccountSession session;
        private CancellationTokenSource lifetime;

        public void Submit(GameRunSnapshot snapshot)
        {
            Cancel();
            session = AuthManager.Instance.Current;

            if (!AuthManager.Instance.IsCurrent(session) || snapshot.ownerPlayerId != session.PlayerId || snapshot.accountApiRoot != session.ApiRoot)
            {
                Fail("Sign in with the account that started this run.");
                return;
            }

            RunId = snapshot.runId;
            payload = JsonUtility.ToJson(snapshot);
            lifetime = new CancellationTokenSource();
            Retry();
        }

        public void Retry()
        {
            if (IsBusy || Succeeded || string.IsNullOrEmpty(payload))
            {
                return;
            }

            if (!AuthManager.Instance.IsCurrent(session))
            {
                Fail("Please sign in again.");
                return;
            }

            IsBusy = true;
            Status = "GAME LOG: SAVING...";
            Changed?.Invoke();
            _ = SendAsync(lifetime);
        }

        private async Task SendAsync(CancellationTokenSource attempt)
        {
            var token = attempt.Token;

            try
            {
                var api = AuthManager.Instance.CreateClient(session, timeoutSeconds: 5);
                var result = await api.PostJsonAsync<GameResultResponse>(
                    "/me/game-results",
                    payload,
                    token,
                    authenticated: true,
                    operationId: RunId + ":game-result");

                if (token.IsCancellationRequested || this == null)
                {
                    return;
                }

                if (!result.IsSuccess)
                {
                    Fail(result.Error);
                    return;
                }

                var response = result.Response;

                if (response == null || response.status != "stored" || response.runId != RunId || string.IsNullOrWhiteSpace(response.resultId))
                {
                    Fail(ApiErrorMessages.Get(ApiErrorCode.InvalidGameResultResponse));
                    return;
                }

                IsBusy = false;
                Succeeded = true;
                Status = "GAME LOG: SAVED";
                GameSessionTracker.MarkStored(RunId);
                Changed?.Invoke();
            }
            catch (Exception)
            {
                if (!token.IsCancellationRequested && this != null)
                {
                    Fail("Unable to confirm the upload. Please try again.");
                }
            }
        }

        private void Fail(string reason)
        {
            IsBusy = false;
            Succeeded = false;
            Status = "GAME LOG SAVE FAILED / " + reason;
            Changed?.Invoke();
        }

        public void Cancel()
        {
            lifetime?.Cancel();
            lifetime?.Dispose();
            lifetime = null;
            IsBusy = false;
            Succeeded = false;
            payload = null;
            RunId = null;
            session = null;
        }

        private void AccountChanged()
        {
            if (payload == null)
            {
                return;
            }

            Cancel();
            Fail("The account changed. The upload was cancelled and the record remains saved locally.");
        }

        private void OnEnable()
        {
            AuthManager.Instance.Changed += AccountChanged;
        }

        private void OnDisable()
        {
            AuthManager.Instance.Changed -= AccountChanged;
            Cancel();
        }
    }
}
