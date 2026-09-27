using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using static Zpd.Networking.DTO.LoginApiDtos;

namespace Zpd.Networking
{
    /// <summary>Owns authentication across scenes without requiring a scene component.</summary>
    public sealed class AuthManager
    {
        public static AuthManager Instance { get; private set; } = new AuthManager();

        public TokenStorage Tokens { get; } = new TokenStorage();
        public AuthState State { get; private set; } = AuthState.SignedOut;
        public AccountSession Current => Tokens.Current;
        public bool IsSignedIn => Tokens.HasSession;
        public string PlayerId => IsSignedIn ? Current.PlayerId : null;

        public event Action Changed;
        public event Action<AuthState> StateChanged;

        private int _loginAttempt;
        private int _activeLogin;

        private AuthManager()
        {
            Tokens.Changed += OnSessionChanged;
        }

        public ApiClient CreateClient(AccountSession session, int timeoutSeconds = 15)
        {
            if (session == null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            return new ApiClient(session.ApiRoot, Tokens, timeoutSeconds, session);
        }

        public async Task<ApiResult<AccountSession>> LoginAsync(
            string apiRoot,
            string loginId,
            string password,
            CancellationToken cancellation,
            int timeoutSeconds = 15)
        {
            cancellation.ThrowIfCancellationRequested();

            if (!AuthValidation.TryNormalizeLoginId(loginId, out string id))
            {
                return ApiResult<AccountSession>.Failure(ApiErrorCode.InvalidLoginId);
            }

            if (!AuthValidation.IsValidPassword(password))
            {
                return ApiResult<AccountSession>.Failure(ApiErrorCode.InvalidPassword);
            }

            var api = new ApiClient(apiRoot, timeoutSeconds: timeoutSeconds);
            int attempt = ++_loginAttempt;
            _activeLogin = attempt;
            SetState(AuthState.Busy);

            try
            {
                var result = await api.PostAsync<LoginRequest, LoginEnvelope>(
                    "/auth/login",
                    new LoginRequest { loginId = id, password = password },
                    cancellation);

                cancellation.ThrowIfCancellationRequested();

                if (attempt != _loginAttempt)
                {
                    return ApiResult<AccountSession>.Failure(ApiErrorCode.SessionExpired);
                }

                if (!result.IsSuccess)
                {
                    if (result.StatusCode == 401)
                    {
                        return ApiResult<AccountSession>.Failure(
                            ApiErrorCode.InvalidCredentials, result.StatusCode, result.ServerCode);
                    }

                    if (result.ErrorCode == ApiErrorCode.InvalidResponse)
                    {
                        return ApiResult<AccountSession>.Failure(
                            ApiErrorCode.InvalidLoginResponse, result.StatusCode);
                    }

                    return result.ConvertFailure<AccountSession>();
                }

                if (!AuthValidation.TryCreateSession(result.Response.data, api.BaseUrl, out var session))
                {
                    return ApiResult<AccountSession>.Failure(
                        ApiErrorCode.InvalidLoginResponse, result.StatusCode);
                }

                SetSession(session);
                return ApiResult<AccountSession>.Success(session, result.StatusCode);
            }
            finally
            {
                if (_activeLogin == attempt)
                {
                    _activeLogin = 0;
                    SetState(IsSignedIn ? AuthState.SignedIn : AuthState.SignedOut);
                }
            }
        }

        public void SetSession(AccountSession session)
        {
            Tokens.Replace(session);
        }

        public void Logout()
        {
            Tokens.Clear();
        }

        public void Invalidate(AccountSession expected)
        {
            Tokens.Invalidate(expected);
        }

        public bool IsCurrent(AccountSession session)
        {
            return Tokens.IsCurrent(session);
        }

        public void CheckExpiry()
        {
            if (Current != null && Current.IsExpired)
            {
                Tokens.Invalidate(Current);
            }
        }

        private void OnSessionChanged()
        {
            // A logout or account change invalidates every older pending login.
            _loginAttempt++;
            SetState(IsSignedIn ? AuthState.SignedIn : AuthState.SignedOut);
            Changed?.Invoke();
        }

        private void SetState(AuthState state)
        {
            if (State == state)
            {
                return;
            }

            State = state;
            StateChanged?.Invoke(state);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            Instance = new AuthManager();
        }
    }
}
