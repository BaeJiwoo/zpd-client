# ID and password login

The first build scene is `Assets/Scenes/Login.unity`. Enter an ID and password,
then select **SIGN IN** or press Enter. Tab switches between fields. Passwords
are masked and cleared when submitted or when the screen is disabled.
The lobby opens only after a successful, validated server response.

## Server integration required

The client implements the contract below. The adjacent `zpd-server` currently
provides TCP matchmaking, not an HTTP account service. Configure
`LoginController.api_root` with an account service implementing this contract.
There is no local login fallback or built-in account/password database.

The default root is `https://127.0.0.1:18080/api/v1`; timeout is 15 seconds.
HTTPS is required except for loopback HTTP in editor/development builds.
Certificate validation is never bypassed.

## API contract

`POST /api/v1/auth/login`

Headers: `Content-Type: application/json`, `Accept: application/json`.
No Bearer token is sent for login.

```json
{ "loginId": "player-0007", "password": "user-entered-password" }
```

Successful response (`200`):

```json
{
  "data": {
    "playerId": "account-42",
    "accessToken": "server-issued-token",
    "expiresAtUtc": "2099-01-01T00:00:00Z"
  }
}
```

- Login IDs have 1-128 characters after trimming surrounding whitespace.
  Case, leading zeroes and Unicode are preserved; control characters are rejected.
- Passwords must be nonempty. Spaces and case are preserved exactly.
  Login does not impose registration/password-strength rules.
- The response `playerId` is the canonical account identity. It may differ from
  `loginId`; account-bound game data always uses this response ID.
- An invalid/empty ID or token, malformed response, or expired session is rejected.
  Expiry must be a future UTC timestamp ending in `Z`.
- Invalid credentials return `401`, optionally with
  `{ "error": { "code": "INVALID_CREDENTIALS" } }`.
  The client shows **Invalid ID or password.** for either incorrect credential.
- The server must verify passwords and issue tokens. Do not log request bodies
  containing passwords. The client cannot authenticate an account by itself.

## Session lifetime

`AuthManager` validates the response and commits it through `TokenStorage`.
Only the server-confirmed player ID, token, expiry and API root enter the session.
Passwords are never written to sessions, PlayerPrefs, scenes, game reports or files.
Credentials survive scene changes in memory and reset when the app restarts.

On activation, `LoginView` restores missing references from the existing authored
ID, Password, Login and Status controls. Explicitly assigned controls are retained.
If a required control is absent, the controller disables submission and reports
the scene configuration error instead of throwing repeated null-reference errors.
Re-enabling a cancelled login screen restores editable controls.

Pending login responses cannot override a later login, logout or cancellation.
Account requests use the session's API root and Bearer token. Requests remain
bound to their original account; a stale `401` cannot clear a newer session.
**Sign out** clears private account state and returns to the login screen.
Refresh tokens and automatic renewal are not implemented; expiry requires sign-in.

## Verification

`Tools/LobbyValidation/Run.ps1 -Login` checks masked input, empty passwords,
incorrect credentials, exact password whitespace, response validation, duplicate
submissions, cancellation, scene transitions, account-bound uploads and logout.
`-Networking` checks the shared typed API and authentication state directly.
The test-only HTTP peer does not ship with the game or replace backend integration.

See [Networking architecture](NETWORKING.md) for the shared request/response flow.
