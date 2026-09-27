# Lobby

`Assets/Scenes/Lobby.unity` is the main lobby, used after login and when returning from Solo Defense. Login remains the first enabled build scene. Open Lobby directly to edit the screen. `ZPD > Lobby > Create Lobby Scene` creates a uniquely named copy using `LegacyLobby.unity` as its source. The legacy scene is retained for reused social/character widgets and excluded from the build; its tools are under `ZPD > Lobby > Legacy`.

The new layout reuses the original `cartoon-ui-atlas.png`: purple illustrated panels/buttons, cream cards, orange primary actions, original friends/backpack icons, comic background streaks, dark ink and cream typography. `ZPD > Lobby > Apply Cartoon Style to Open Lobby` reapplies the style without rebuilding the scene.

## UI

- Home: a large centered character using the original authored artwork. Player Info / Inventory / Friends buttons open their respective windows. Solo Defense and Multi Play remain directly accessible on the right.
- Player Info window: nickname, level, matches, wins/losses, calculated win rate and a scrolling recent-play history.
- Inventory window: scrolling three-column button grid; All / Consumables / Equipment filters.
- Consumables: owned quantity text and read-only slider in both the card and detail popup. The slider displays stock, not the amount to consume. Capacity is supplied by item data and never shown below the owned quantity.
- Equipment: informational popup with no quantity control or use/equip action.
- Solo Defense: enters the existing game preparation scene.
- Multi Play: opens a preparation/unavailable popup until the multiplayer game flow is connected. The connection-test scene is not a multiplayer game.
- Friends: the existing four-tab animated social drawer and heart automation are retained in the copied scene.
- Windows close by X, background click or Escape. Closing item details returns to the inventory; closing the inventory returns home. The underlying window and home cannot receive input through a popup.

The central character retains the existing `LobbyCharacterPicker` references and confirmed-character binding. Until account data arrives it is explicitly labeled as local artwork preview.

Text currently uses English with the existing built-in Unity font. No player data is invented. Unknown inventory and a confirmed empty inventory have different messages.

## MVC and services

Account data is managed through `ILobbyService` responses → `LobbyModel` snapshots → `LobbyView`. `LobbyController` coordinates input and asynchronous requests. The service adapter is injected on Unity's main thread with `ConfigureService(authenticatedService)`; use `AuthManager.Instance.Logout()` on logout. `LobbyApiService` now sends real GET profile/inventory and POST item-use requests. ID/password login calls `/auth/login` and stores a server-issued AccountSession. Lobby initialization restores the shared session across scene changes. All account requests use its API root and Bearer token. See [login contract](../../../Docs/LOGIN.md). See [HTTP contract](../../../Docs/LOBBY_API_CONTRACT.md) for request/response JSON, configuration and server integration requirements.

See [MVC/service implementation plan](../../../Docs/LOBBY_SERVICES.md) for contracts, responsibilities, authentication/bootstrap wiring, response revisions, cancellation, retries/idempotency and the plan for social/character/matchmaking services. The previous direct `BindProfile`/`BindInventory` and `UseItemRequested` integration API has been replaced by this service contract.

UI references are serialized on `LobbyView`. Missing references in already-open pre-MVC scenes are recovered from the authored hierarchy during editor validation and initialization. Controller button callbacks and compatibility accessors retain the existing editor builder bindings.

## Verification

`Tools/LobbyValidation/Run.ps1` verifies immutable snapshots, invalid/older responses, account changes, pending-use guards, uncertain retry identity, disable/enable lifecycle and UI bindings with a test-only service. `-Pointer` verifies menu and close buttons via raycasts and the Input System UI module. No test data is saved into the production scene. These focused checks do not enter Solo Defense.

The existing SoloDefense scene has a separate pre-existing DefenseView serialization issue, which is not changed by this lobby refactor.

