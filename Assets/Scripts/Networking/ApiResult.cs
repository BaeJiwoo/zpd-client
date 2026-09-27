using System;

namespace Zpd.Networking
{
    /// <summary>A typed HTTP result. Cancellation remains an OperationCanceledException.</summary>
    public sealed class ApiResult<T>
    {
        public bool IsSuccess { get; }
        public long StatusCode { get; }
        public T Response { get; }
        public ApiErrorCode ErrorCode { get; }
        public string ServerCode { get; }
        public bool OutcomeUnknown { get; }
        public string Error => IsSuccess ? null : ApiErrorMessages.Get(ErrorCode, StatusCode);

        private ApiResult(
            bool isSuccess,
            T response,
            long statusCode,
            ApiErrorCode errorCode,
            string serverCode,
            bool outcomeUnknown)
        {
            IsSuccess = isSuccess;
            Response = response;
            StatusCode = statusCode;
            ErrorCode = errorCode;
            ServerCode = serverCode;
            OutcomeUnknown = outcomeUnknown;
        }

        public static ApiResult<T> Success(T response, long statusCode)
        {
            return new ApiResult<T>(true, response, statusCode, ApiErrorCode.Unknown, null, false);
        }

        public static ApiResult<T> Failure(
            ApiErrorCode code,
            long statusCode = 0,
            string serverCode = null,
            bool outcomeUnknown = false)
        {
            return new ApiResult<T>(false, default, statusCode, code, serverCode, outcomeUnknown);
        }

        public ApiResult<TResponse> ConvertFailure<TResponse>()
        {
            if (IsSuccess)
            {
                throw new InvalidOperationException("A successful response cannot be converted to a failure.");
            }

            return ApiResult<TResponse>.Failure(ErrorCode, StatusCode, ServerCode, OutcomeUnknown);
        }
    }
}
