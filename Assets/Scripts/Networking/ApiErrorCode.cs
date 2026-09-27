namespace Zpd.Networking
{
    /// <summary>Client error categories. Server-specific codes remain separate strings.</summary>
    public enum ApiErrorCode
    {
        Unknown = 0,
        // Authentication and session lifetime.

        SessionExpired,
        InvalidLoginId,
        InvalidPassword,
        InvalidCredentials,
        InvalidLoginResponse,
        // HTTP failures.

        ConnectionFailed,
        Unauthorized,
        Forbidden,
        NotFound,
        RequestTimeout,
        HttpFailure,
        // Successful HTTP responses with invalid or incomplete data.

        InvalidResponse,
        InvalidItemUseResponse,
        InvalidGameResultResponse,
        InvalidRewardResponse
    }
}
