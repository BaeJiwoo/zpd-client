namespace Zpd.Networking
{
    /// <summary>User-facing text belongs here, not in request or response processing.</summary>
    internal static class ApiErrorMessages
    {
        public static string Get(ApiErrorCode code, long statusCode = 0)
        {
            switch (code)
            {
                case ApiErrorCode.InvalidLoginId:
                    return "Enter an ID between 1 and 128 characters.";

                case ApiErrorCode.InvalidPassword:
                    return "Enter your password.";

                case ApiErrorCode.InvalidCredentials:
                    return "Invalid ID or password.";

                case ApiErrorCode.SessionExpired:
                    return "Please sign in again.";

                case ApiErrorCode.InvalidLoginResponse:
                    return "The login response is invalid. Please try again.";

                case ApiErrorCode.ConnectionFailed:
                    return "Unable to connect to the server. Please try again.";

                case ApiErrorCode.Unauthorized:
                    return "Your session has expired or is invalid. Please sign in again.";

                case ApiErrorCode.Forbidden:
                    return "You do not have permission to make this request.";

                case ApiErrorCode.NotFound:
                    return "The player or API endpoint was not found.";

                case ApiErrorCode.RequestTimeout:
                    return "The request timed out. Please try again.";

                case ApiErrorCode.HttpFailure:
                    return "Unable to complete the request. (HTTP " + statusCode + ")";

                case ApiErrorCode.InvalidResponse:
                    return "The server response is invalid.";

                case ApiErrorCode.InvalidItemUseResponse:
                    return "Unable to confirm item use. Retry the same request.";

                case ApiErrorCode.InvalidGameResultResponse:
                    return "Unable to confirm that the game result was saved. Retry the same request.";

                case ApiErrorCode.InvalidRewardResponse:
                    return "Unable to confirm the reward. Retry the same request.";

                default:
                    return "Unable to complete the request. Please try again.";
            }
        }
    }
}
