using System;
using Zpd.Networking;

namespace Zpd.Lobby
{
    /// <summary>Adapts API failures for callers of ILobbyService.</summary>
    public sealed class LobbyServiceException : Exception
    {
        public ApiErrorCode Code { get; }
        public string ServerCode { get; }
        public long StatusCode { get; }
        public bool OutcomeUnknown { get; }

        public LobbyServiceException(
            ApiErrorCode code,
            bool outcomeUnknown = false,
            long statusCode = 0,
            string serverCode = null)
            : base(ApiErrorMessages.Get(code, statusCode))
        {
            Code = code;
            ServerCode = serverCode;
            StatusCode = statusCode;
            OutcomeUnknown = outcomeUnknown;
        }

        internal static LobbyServiceException FromResult<T>(
            ApiResult<T> result,
            ApiErrorCode invalidResponseCode = ApiErrorCode.InvalidResponse)
        {
            ApiErrorCode code = result.ErrorCode == ApiErrorCode.InvalidResponse
                ? invalidResponseCode
                : result.ErrorCode;

            return new LobbyServiceException(
                code, result.OutcomeUnknown, result.StatusCode, result.ServerCode);
        }
    }
}
