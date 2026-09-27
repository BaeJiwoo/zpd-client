using System;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using static Zpd.Networking.DTO.ApiErrorDtos;

namespace Zpd.Networking
{
    /// <summary>Typed HTTP requests. Construct and call on Unity's main thread.</summary>
    public sealed class ApiClient
    {
        public const string DefaultRoot = "https://127.0.0.1:18080/api/v1";

        public string BaseUrl { get; }

        private readonly TokenStorage token_storage;
        private readonly AccountSession account_session;
        private readonly int request_timeout_seconds;

        public ApiClient(
            string baseUrl,
            TokenStorage tokens = null,
            int timeoutSeconds = 15,
            AccountSession session = null)
        {
            BaseUrl = NormalizeRoot(baseUrl);
            token_storage = tokens;
            account_session = session ?? tokens?.Current;
            request_timeout_seconds = Math.Max(1, timeoutSeconds);

            if (account_session != null && account_session.ApiRoot != BaseUrl)
            {
                throw new ArgumentException("Request origin must match the authenticated API.");
            }
        }

        public Task<ApiResult<T>> GetAsync<T>(
            string path,
            CancellationToken cancellation,
            bool authenticated = false)
            where T : class, new()
        {
            return SendAsync<T>(path, UnityWebRequest.kHttpVerbGET, null, null, cancellation, authenticated);
        }

        public Task<ApiResult<TResponse>> PostAsync<TRequest, TResponse>(
            string path,
            TRequest body,
            CancellationToken cancellation,
            bool authenticated = false,
            string operationId = null)
            where TResponse : class, new()
        {
            return PostJsonAsync<TResponse>(
                path, JsonUtility.ToJson(body), cancellation, authenticated, operationId);
        }

        /// <summary>Send a frozen JSON snapshot again without changing its body or idempotency key.</summary>
        public Task<ApiResult<T>> PostJsonAsync<T>(
            string path,
            string json,
            CancellationToken cancellation,
            bool authenticated = false,
            string operationId = null)
            where T : class, new()
        {
            return SendAsync<T>(path, UnityWebRequest.kHttpVerbPOST, json, operationId, cancellation, authenticated);
        }

        public static string NormalizeRoot(string root)
        {
            if (!Uri.TryCreate(root, UriKind.Absolute, out var uri))
            {
                throw new ArgumentException("An absolute API URL is required.", nameof(root));
            }

            bool developmentLoopback = uri.Scheme == "http" &&
                uri.IsLoopback &&
                (Application.isEditor || Debug.isDebugBuild);

            if ((uri.Scheme != "https" && !developmentLoopback) ||
                !string.IsNullOrEmpty(uri.UserInfo) ||
                !string.IsNullOrEmpty(uri.Query) ||
                !string.IsNullOrEmpty(uri.Fragment))
            {
                throw new ArgumentException("API root must use HTTPS. Loopback HTTP is for development only.");
            }

            return uri.AbsoluteUri.TrimEnd('/');
        }

        private async Task<ApiResult<T>> SendAsync<T>(
            string path,
            string method,
            string json,
            string operationId,
            CancellationToken cancellation,
            bool authenticated)
            where T : class, new()
        {
            cancellation.ThrowIfCancellationRequested();

            if (authenticated && !HasCurrentSession())
            {
                return ApiResult<T>.Failure(ApiErrorCode.SessionExpired);
            }

            using (var request = CreateRequest(path, method, json, operationId, authenticated))
            {
                var pending = request.SendWebRequest();

                while (!pending.isDone)
                {
                    if (cancellation.IsCancellationRequested)
                    {
                        request.Abort();
                        cancellation.ThrowIfCancellationRequested();
                    }

                    if (authenticated && !HasCurrentSession())
                    {
                        request.Abort();
                        return ApiResult<T>.Failure(ApiErrorCode.SessionExpired, outcomeUnknown: json != null);
                    }

                    await Task.Yield();
                }

                cancellation.ThrowIfCancellationRequested();

                if (authenticated && !HasCurrentSession())
                {
                    return ApiResult<T>.Failure(ApiErrorCode.SessionExpired, outcomeUnknown: json != null);
                }

                if (authenticated && request.responseCode == (long)HttpStatusCode.Unauthorized)
                {
                    token_storage.Invalidate(account_session);
                }

                return CreateResult<T>(request, json != null);
            }
        }

        private bool HasCurrentSession()
        {
            if (account_session != null && account_session.IsExpired)
            {
                token_storage?.Invalidate(account_session);
            }

            return token_storage != null && token_storage.IsCurrent(account_session);
        }

        private UnityWebRequest CreateRequest(
            string path, string method, string json, string operationId, bool authenticated)
        {
            var request = new UnityWebRequest(BaseUrl + "/" + path.TrimStart('/'), method);

            try
            {
                request.downloadHandler = new DownloadHandlerBuffer();
                request.timeout = request_timeout_seconds;
                request.redirectLimit = 0;
                request.SetRequestHeader("Accept", "application/json");

                if (authenticated)
                {
                    request.SetRequestHeader("Authorization", "Bearer " + account_session.AccessToken);
                }

                if (json != null)
                {
                    request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                    request.SetRequestHeader("Content-Type", "application/json");
                }

                if (!string.IsNullOrEmpty(operationId))
                {
                    request.SetRequestHeader("Idempotency-Key", operationId);
                }

                return request;
            }
            catch
            {
                request.Dispose();
                throw;
            }
        }

        private static ApiResult<T> CreateResult<T>(UnityWebRequest request, bool isMutation)
            where T : class, new()
        {
            long status = request.responseCode;
            string json = request.downloadHandler.text;

            if (request.result != UnityWebRequest.Result.Success)
            {
                bool outcomeUnknown = isMutation &&
                    (status == 0 ||
                     status == (long)HttpStatusCode.RequestTimeout ||
                     status >= (long)HttpStatusCode.InternalServerError);

                return ApiResult<T>.Failure(
                    ClassifyHttpStatus(status), status, ReadServerCode(json), outcomeUnknown);
            }

            if (string.IsNullOrWhiteSpace(json))
            {
                return ApiResult<T>.Failure(ApiErrorCode.InvalidResponse, status, outcomeUnknown: isMutation);
            }

            try
            {
                // Preserve sentinel field initializers used to detect missing response values.
                var response = new T();
                JsonUtility.FromJsonOverwrite(json, response);
                return ApiResult<T>.Success(response, status);
            }
            catch (ArgumentException)
            {
                return ApiResult<T>.Failure(ApiErrorCode.InvalidResponse, status, outcomeUnknown: isMutation);
            }
        }

        private static ApiErrorCode ClassifyHttpStatus(long status)
        {
            if (status == 0)
            {
                return ApiErrorCode.ConnectionFailed;
            }

            switch ((HttpStatusCode)status)
            {
                case HttpStatusCode.Unauthorized:
                    return ApiErrorCode.Unauthorized;
                case HttpStatusCode.Forbidden:
                    return ApiErrorCode.Forbidden;
                case HttpStatusCode.NotFound:
                    return ApiErrorCode.NotFound;
                case HttpStatusCode.RequestTimeout:
                    return ApiErrorCode.RequestTimeout;
                default:
                    return ApiErrorCode.HttpFailure;
            }
        }

        private static string ReadServerCode(string json)
        {
            try
            {
                var error = JsonUtility.FromJson<ErrorEnvelope>(json)?.error;
                return string.IsNullOrEmpty(error?.code) ? null : error.code;
            }
            catch (ArgumentException)
            {
                return null;
            }
        }
    }
}
