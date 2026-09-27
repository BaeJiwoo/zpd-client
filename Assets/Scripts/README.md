# Script navigation

Scripts are grouped by feature. Follow the [C# style guide](../../Docs/CODE_STYLE.md) and the root `.editorconfig`.

## Networking

HTTP and authentication follow the [Sweeper networking structure](https://github.com/J-sGames/sweeper-client/tree/main/Assets/Sweeper/Scripts/Networking). Read [Networking architecture](../../Docs/NETWORKING.md) for the mapping and request flow.

- `Networking/ApiClient.cs`: typed GET/POST requests, JSON handling, HTTP errors, timeout and cancellation.
- `Networking/ApiResult.cs`: success or failure, response, status, enum error, raw server code and mutation uncertainty.
- `Networking/AuthManager.cs`: login, logout, authentication state and session-change events.
- `Networking/TokenStorage.cs`: current credentials and session revision, kept only in memory.
- `Networking/AuthValidation.cs`: login ID, password and login response validation.
- `Networking/AccountSession.cs`: immutable server-issued account credentials.
- `Networking/ApiErrorCode.cs` and `ApiErrorMessages.cs`: error categories and English messages.
- `Networking/DTO`: HTTP request and response shapes.
- `Networking/Tcp`: engine-independent TCP matchmaking assembly. Do not manually edit its `Generated` files.
- `Development`: TCP connection and matchmaking test clients.

## Features

| Responsibility | Lobby | Defense | Gameplay |
| --- | --- | --- | --- |
| Models | Lobby state and validated snapshots | Combat, wave, supply and upgrade rules | Game modes |
| Views | Login, lobby, item and social UI | Combat, supplies, visual and audio feedback | — |
| Controllers | Input, service orchestration and rendering | Combat, enemies, projectiles and supplies | Game session tracking |
| DTOs | Validated data exchanged with the lobby model | Run reports | Frozen run snapshots and log events |
| Services | Adapt typed API results to `ILobbyService` | Reward submission | Game result upload and scene navigation |

Feature services use `AuthManager.Instance.CreateClient(session)` to bind requests to their original account. They never switch an existing request to newly signed-in credentials.

The lobby retains `ILobbyService` for model/controller tests. `LobbyApiService` converts HTTP results into domain data; `LobbyResponseParser` validates that data. Its service exception is an adapter boundary, not a second HTTP transport.

Each feature's `Editor` folder contains Unity scene-authoring tools. DTOs contain no UI references or API calls.
