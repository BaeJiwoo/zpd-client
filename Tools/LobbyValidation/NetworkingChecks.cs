using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Zpd.Networking;

[InitializeOnLoad]
public static class NetworkingChecks
{
    [Serializable]
    public sealed class ProfileEnvelope
    {
        public ProfileData data;
    }

    [Serializable]
    public sealed class ProfileData
    {
        public string displayName;
    }

    [Serializable]
    public sealed class SentinelResponse
    {
        public int earnedExperience = -1;
    }

    private static int _assertions;

    static NetworkingChecks()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    public static void Run()
    {
        PlayerSettings.insecureHttpOption = InsecureHttpOption.DevelopmentOnly;
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool("NetworkingChecks", true);
        EditorApplication.EnterPlaymode();
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode ||
            !SessionState.GetBool("NetworkingChecks", false))
        {
            return;
        }

        SessionState.SetBool("NetworkingChecks", false);
        EditorApplication.update += Pump;
        Check();
    }

    private static void Pump()
    {
        EditorApplication.QueuePlayerLoopUpdate();
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new Exception(message);
        }

        _assertions++;
    }

    private static async Task WaitForRequest(AuthTestServer server, int previousCount)
    {
        DateTime deadline = DateTime.UtcNow.AddSeconds(5);

        while (server.Requests.Count <= previousCount)
        {
            if (DateTime.UtcNow >= deadline)
            {
                throw new TimeoutException("The test peer did not receive a request.");
            }

            await Task.Delay(10);
        }
    }

    private static async void Check()
    {
        try
        {
            var auth = AuthManager.Instance;
            var states = new List<AuthState>();
            auth.StateChanged += states.Add;

            using (var server = new AuthTestServer())
            {
                var invalid = await auth.LoginAsync(server.Root, " ", AuthTestServer.Password, CancellationToken.None);
                Require(!invalid.IsSuccess && invalid.ErrorCode == ApiErrorCode.InvalidLoginId,
                    "Invalid input returns a typed failure");
                Require(server.Requests.IsEmpty && auth.State == AuthState.SignedOut,
                    "Invalid input neither sends a request nor changes auth state");

                var login = await auth.LoginAsync(server.Root, "first", AuthTestServer.Password, CancellationToken.None);
                Require(login.IsSuccess && ReferenceEquals(auth.Current, login.Response),
                    "AuthManager commits the validated account");
                Require(states.SequenceEqual(new[] { AuthState.Busy, AuthState.SignedIn }),
                    "Login publishes busy and signed-in states");
                Require(auth.Tokens.Revision == 1, "Token storage owns the session revision");

                var api = auth.CreateClient(auth.Current);
                var profile = await api.GetAsync<ProfileEnvelope>("/me", CancellationToken.None, true);
                Require(profile.IsSuccess && profile.Response.data.displayName == "API Player",
                    "ApiClient returns a typed response");
                Require(profile.StatusCode == 200 && profile.Error == null,
                    "Successful results preserve the HTTP status without an error");
                Require(server.Requests.Last().Authorization == "Bearer token-first",
                    "The bound session supplies the authorization header");

                server.ProfileStatus = 403;
                server.ProfileBody = "{\"error\":{\"code\":\"FUTURE_SERVER_CODE\"}}";
                var forbidden = await api.GetAsync<ProfileEnvelope>("/me", CancellationToken.None, true);
                Require(!forbidden.IsSuccess && forbidden.ErrorCode == ApiErrorCode.Forbidden &&
                    forbidden.ServerCode == "FUTURE_SERVER_CODE" && forbidden.StatusCode == 403,
                    "Unknown server codes survive enum classification");
                var converted = forbidden.ConvertFailure<SentinelResponse>();
                Require(converted.ServerCode == forbidden.ServerCode && converted.StatusCode == 403,
                    "Failure conversion preserves metadata");

                server.ProfileStatus = 200;
                server.ProfileBody = "{}";
                var sentinel = await api.GetAsync<SentinelResponse>("/me", CancellationToken.None, true);
                Require(sentinel.Response.earnedExperience == -1,
                    "Deserialization preserves missing-field sentinels");

                server.ProfileBody = "broken";
                var malformed = await api.PostJsonAsync<ProfileEnvelope>(
                    "/me", "{}", CancellationToken.None, true, "stable-operation");
                Require(!malformed.IsSuccess && malformed.ErrorCode == ApiErrorCode.InvalidResponse &&
                    malformed.OutcomeUnknown,
                    "Malformed mutation responses remain uncertain");
                Require(server.Requests.Last().Key == "stable-operation" &&
                    server.Requests.Last().Body == "{}",
                    "JSON retries retain their body and idempotency key");

                server.ProfileBody = "";
                var empty = await api.GetAsync<ProfileEnvelope>("/me", CancellationToken.None, true);
                Require(!empty.IsSuccess && empty.ErrorCode == ApiErrorCode.InvalidResponse,
                    "An empty response is not invented data");

                server.ProfileBody = null;
                server.ProfileDelay = 300;
                using (var cancellation = new CancellationTokenSource(30))
                {
                    try
                    {
                        await api.GetAsync<ProfileEnvelope>("/me", cancellation.Token, true);
                        throw new Exception("Cancellation was ignored.");
                    }
                    catch (OperationCanceledException)
                    {
                        Require(auth.IsSignedIn, "Cancellation does not clear the account");
                    }
                }


                int before = server.Requests.Count;
                var oldRead = api.GetAsync<ProfileEnvelope>("/me", CancellationToken.None, true);
                await WaitForRequest(server, before);
                var newer = new AccountSession("new", "new-token", DateTimeOffset.UtcNow.AddHours(1), server.Root);
                auth.SetSession(newer);
                var stale = await oldRead;
                Require(!stale.IsSuccess && stale.ErrorCode == ApiErrorCode.SessionExpired &&
                    ReferenceEquals(auth.Current, newer),
                    "A stale response cannot replace or clear a new account");

                before = server.Requests.Count;
                var blocked = await api.GetAsync<ProfileEnvelope>("/me", CancellationToken.None, true);
                Require(!blocked.IsSuccess && server.Requests.Count == before,
                    "An old client cannot send using the new account's credentials");

                var currentApi = auth.CreateClient(newer);
                server.ProfileDelay = 0;
                server.ProfileStatus = 401;
                var unauthorized = await currentApi.GetAsync<ProfileEnvelope>("/me", CancellationToken.None, true);
                Require(unauthorized.ErrorCode == ApiErrorCode.Unauthorized &&
                    !auth.IsSignedIn && auth.State == AuthState.SignedOut,
                    "401 invalidates only the request's account");

                server.LoginDelay = 200;
                before = server.Requests.Count;
                var olderLogin = auth.LoginAsync(server.Root, "older", AuthTestServer.Password, CancellationToken.None);
                await WaitForRequest(server, before);
                server.LoginDelay = 0;
                var latestLogin = await auth.LoginAsync(server.Root, "latest", AuthTestServer.Password, CancellationToken.None);
                var superseded = await olderLogin;
                Require(latestLogin.IsSuccess && !superseded.IsSuccess &&
                    auth.PlayerId == "latest" && auth.State == AuthState.SignedIn,
                    "A delayed login cannot overwrite the latest account");

                server.LoginDelay = 200;
                before = server.Requests.Count;
                var loggedOutLogin = auth.LoginAsync(server.Root, "after-logout", AuthTestServer.Password, CancellationToken.None);
                await WaitForRequest(server, before);
                auth.Logout();
                var loggedOut = await loggedOutLogin;
                Require(!loggedOut.IsSuccess && !auth.IsSignedIn,
                    "Logout prevents a pending login from restoring credentials");

                using (var cancellation = new CancellationTokenSource(30))
                {
                    try
                    {
                        await auth.LoginAsync(server.Root, "cancelled", AuthTestServer.Password, cancellation.Token);
                        throw new Exception("Login cancellation was ignored.");
                    }
                    catch (OperationCanceledException)
                    {
                        Require(auth.State == AuthState.SignedOut && !auth.IsSignedIn,
                            "Cancelled login leaves no busy state or credentials");
                    }
                }
                server.LoginDelay = 0;
                server.LoginBody = "{\"data\":{\"playerId\":\"canonical-account\",\"accessToken\":\"canonical-token\",\"expiresAtUtc\":\"2099-01-01T00:00:00Z\"}}";
                var canonical = await auth.LoginAsync(server.Root, "different-login-id", AuthTestServer.Password, CancellationToken.None);
                Require(canonical.IsSuccess && auth.PlayerId == "canonical-account",
                    "Session identity comes from the server, not the entered login ID");
                Require(server.Requests.Last().Body.Contains("\"password\":\"" + AuthTestServer.Password + "\""),
                    "Password whitespace reaches the server unchanged");
                auth.Logout();
            }

            Finish("PASS: " + _assertions + " networking architecture checks", 0);
        }
        catch (Exception error)
        {
            Debug.LogException(error);
            Finish(error.ToString(), 1);
        }
    }

    private static void Finish(string result, int exitCode)
    {
        EditorApplication.update -= Pump;
        File.WriteAllText("networking-result.txt", result);
        EditorApplication.Exit(exitCode);
    }
}
