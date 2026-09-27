using System;
using System.Globalization;
using System.Linq;
using static Zpd.Networking.DTO.LoginApiDtos;

namespace Zpd.Networking
{
    public static class AuthValidation
    {
        public static bool TryNormalizeLoginId(string input, out string loginId)
        {
            return TryNormalizePlayerId(input, out loginId);
        }

        public static bool IsValidPassword(string password)
        {
            // Login does not impose registration rules or trim significant whitespace.
            return !string.IsNullOrEmpty(password);
        }

        public static bool TryNormalizePlayerId(string input, out string playerId)
        {
            playerId = input?.Trim();

            if (string.IsNullOrEmpty(playerId) ||
                playerId.Length > 128 ||
                playerId.Any(char.IsControl))
            {
                playerId = null;
                return false;
            }

            // Account IDs are opaque: preserve case, leading zeroes and Unicode.
            return true;
        }

        internal static bool TryCreateSession(
            LoginData data,
            string apiRoot,
            out AccountSession session)
        {
            session = null;

            if (data == null ||
                string.IsNullOrEmpty(data.expiresAtUtc) ||
                !data.expiresAtUtc.EndsWith("Z", StringComparison.Ordinal) ||
                !DateTimeOffset.TryParse(
                    data.expiresAtUtc,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal,
                    out var expires))
            {
                return false;
            }

            try
            {
                session = new AccountSession(data.playerId, data.accessToken, expires, apiRoot);
                return true;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }
    }
}
