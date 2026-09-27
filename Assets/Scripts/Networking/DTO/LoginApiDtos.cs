using System;

namespace Zpd.Networking.DTO
{
    internal static class LoginApiDtos
    {
        [Serializable]
        public sealed class LoginRequest
        {
            public string loginId;
            public string password;
        }

        [Serializable]
        public sealed class LoginEnvelope
        {
            public LoginData data;
        }

        [Serializable]
        public sealed class LoginData
        {
            public string playerId;
            public string accessToken;
            public string expiresAtUtc;
        }
    }
}
