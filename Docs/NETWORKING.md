# Networking architecture

The HTTP layer follows the responsibilities of [Sweeper's Networking folder](https://github.com/J-sGames/sweeper-client/tree/main/Assets/Sweeper/Scripts/Networking), while retaining this project's server contract and C# style.

## Layout

```text
Assets/Scripts/Networking/
    ApiClient.cs
    ApiResult.cs
    AuthManager.cs
    AuthState.cs
    TokenStorage.cs
    AuthValidation.cs
    AccountSession.cs
    ApiErrorCode.cs
    ApiErrorMessages.cs
    DTO/
        LoginApiDtos.cs
        ApiErrorDtos.cs
        LobbyApiDtos.cs
        GameResultResponse.cs
        DefenseRewardResponse.cs
    Tcp/
        Zpd.Networking.asmdef
        NetworkClient.cs
        MatchmakingClient.cs
        TcpTransport.cs
        Packet.cs
        PacketCodec.cs
        NetworkEvent.cs
        NetworkSettings.cs
        Generated/
```

The old authentication transport, login service/parser and player-session facade have been removed. Consumers use the new classes directly.

## Login flow

```text
LoginController
    -> AuthManager.LoginAsync
    -> AuthValidation validates the login ID and password
    -> ApiClient.PostAsync<LoginRequest, LoginEnvelope>
    -> ApiResult<LoginEnvelope>
    -> AuthValidation validates credentials and expiry
    -> TokenStorage.Replace
    -> AuthManager publishes the session change
    -> LoginController opens the lobby
```

`AuthManager.Instance` is a plain C# singleton with a Unity subsystem reset hook. It survives scene transitions without adding a scene component. `StateChanged` reports busy/signed-in/signed-out transitions. `Changed` reports credential changes so existing feature controllers can discard private account data.

A login attempt is invalidated by a later login, logout or session replacement. Cancelled requests cannot commit credentials.

## Feature requests

```csharp
var api = AuthManager.Instance.CreateClient(session);
var result = await api.GetAsync<MyResponse>(
    "/me/example",
    cancellation,
    authenticated: true);

if (!result.IsSuccess)
{
    // Display result.Error or inspect result.ErrorCode.
    return;
}

var response = result.Response;
```

`ApiClient` performs HTTP and JSON processing. Domain services validate required fields, revisions and operation identities after deserialization. Response envelopes are explicit DTOs; the client does not guess their shape by searching JSON text.

`PostAsync<TRequest, TResponse>` serializes a request DTO. `PostJsonAsync<TResponse>` sends an already frozen snapshot; game-result and reward retries keep the same body and idempotency key.

`ApiResult<T>` carries `IsSuccess`, `Response`, `StatusCode`, `ErrorCode`, `Error`, `ServerCode` and `OutcomeUnknown`. HTTP errors and malformed responses return failures. Caller cancellation still throws `OperationCanceledException`. Invalid programmer configuration, such as an unsafe base URL, still throws `ArgumentException`.

## Preserved guarantees

- A client captures its account session. It never sends a new account's token for an old operation.
- Account changes, expiry and cancellation stop pending requests.
- A 401 clears only the session that issued the request.
- HTTPS is required except for development loopback HTTP. Redirects are disabled.
- Malformed or empty response bodies do not become confirmed domain data.
- Field sentinel defaults survive deserialization.
- Uncertain mutations retain their original idempotency key.
- Server error strings are preserved separately from the client error enum.

## Deliberate differences from the reference

- Async methods return `Task<ApiResult<T>>` to retain the existing cancellation and controller lifecycle behavior; the reference uses coroutine callbacks.
- The client sends ID/password credentials to `/auth/login`; the adjacent TCP server still needs a separate HTTP account service. See [the login contract](LOGIN.md). Refresh-token, registration and Google-login endpoints are not implemented.
- `TokenStorage` holds access credentials in memory. Persistent refresh-token storage and automatic refresh require a server contract first.
- UI rendering stays in existing feature views. No unused generic view interface is added.
- HTTP is compiled with the Unity feature code. TCP retains its separate engine-free assembly under `Tcp`.

## Verification

Run `Tools/LobbyValidation/Run.ps1 -Networking` for direct API-result and authentication-state tests. Existing `-Api`, `-Login` and lobby checks cover feature integration. TCP protocol generation and standalone checks target `Networking/Tcp`.

