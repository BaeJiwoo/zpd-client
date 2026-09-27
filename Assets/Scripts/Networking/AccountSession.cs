using System;
using System.Linq;

namespace Zpd.Networking
{
    public sealed class AccountSession
    {
        public string PlayerId { get; }
        public string ApiRoot { get; }
        public DateTimeOffset ExpiresAtUtc { get; }
        internal string AccessToken { get; }
        public bool IsExpired => DateTimeOffset.UtcNow >= ExpiresAtUtc;

        public AccountSession(string playerId, string accessToken, DateTimeOffset expiresAtUtc, string apiRoot)
        {
            if (!AuthValidation.TryNormalizePlayerId(playerId, out var id) ||
                id != playerId ||
                string.IsNullOrWhiteSpace(accessToken) ||
                accessToken.Any(c => c <= 32 || c >= 127) ||
                expiresAtUtc <= DateTimeOffset.UtcNow)
            {
                throw new ArgumentException("Invalid login response.");
            }

            PlayerId = id;
            AccessToken = accessToken;
            ExpiresAtUtc = expiresAtUtc;
            ApiRoot = ApiClient.NormalizeRoot(apiRoot);
        }
    }
}
