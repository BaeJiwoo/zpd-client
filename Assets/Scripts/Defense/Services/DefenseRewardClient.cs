using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Zpd.Networking;
using Zpd.Networking.DTO;
using Zpd.Lobby;

namespace Zpd.Defense
{
    /// <summary>Authenticated prototype report; local statistics are never proof of rewards.</summary>
    public sealed class DefenseRewardClient : MonoBehaviour
    {
        [Range(1, 30)]
        public int timeoutSeconds = 5;
        public event Action Changed;

        public string Status { get; private set; } = "";
        public string Detail { get; private set; } = "";
        public bool IsBusy { get; private set; }
        public bool Succeeded { get; private set; }
        public string RunId { get; private set; }

        private string payload;
        private AccountSession session;
        private CancellationTokenSource lifetime;

        public void Submit(DefenseRunReport report)
        {
            Cancel();
            session = AuthManager.Instance.Current;

            if (!AuthManager.Instance.IsCurrent(session) || report.ownerPlayerId != session.PlayerId || report.accountApiRoot != session.ApiRoot)
            {
                Fail("Sign in with the account that started this run.");
                return;
            }

            RunId = report.runId;
            payload = JsonUtility.ToJson(report);
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
            Status = "REQUESTING REWARD...";
            Detail = "Waiting for the reward service.\nExperience has not been awarded.";
            Changed?.Invoke();
            _ = SendAsync(lifetime);
        }

        private async Task SendAsync(CancellationTokenSource attempt)
        {
            var token = attempt.Token;

            try
            {
                var api = AuthManager.Instance.CreateClient(session, timeoutSeconds);
                var response = await api.PostJsonAsync<DefenseRewardResponse>(
                    "/prototype/defense-runs/" + Uri.EscapeDataString(RunId) + "/rewards",
                    payload,
                    token,
                    authenticated: true,
                    operationId: RunId);

                if (token.IsCancellationRequested || this == null)
                {
                    return;
                }

                if (!response.IsSuccess)
                {
                    Fail(response.Error);
                    return;
                }

                var result = response.Response;

                if (result.status != "settled" || result.runId != RunId || string.IsNullOrWhiteSpace(result.settlementId) || result.earnedExperience < 0)
                {
                    Fail(ApiErrorMessages.Get(ApiErrorCode.InvalidRewardResponse));
                    return;
                }

                IsBusy = false;
                Succeeded = true;
                Status = "REWARD CONFIRMED";
                Detail = "Service result: +" + result.earnedExperience + " EXP\nPrototype display only. No local profile is modified.";
                Changed?.Invoke();
            }
            catch (Exception)
            {
                if (!token.IsCancellationRequested && this != null)
                {
                    Fail("Unable to confirm the reward. Please try again.");
                }
            }
        }

        private void Fail(string message)
        {
            IsBusy = false;
            Succeeded = false;
            Status = "REWARD REQUEST FAILED";
            Detail = message;
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
            Fail("The account changed. The reward request was cancelled.");
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
