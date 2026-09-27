using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Zpd.Lobby;
using Zpd.Networking;

[InitializeOnLoad]
public static class LobbyApiChecks
{
    static LobbyApiChecks() { EditorApplication.playModeStateChanged += State; }
    public static void Run()
    {
        PlayerSettings.insecureHttpOption = InsecureHttpOption.DevelopmentOnly;
        EditorSceneManager.OpenScene("Assets/Scenes/Lobby.unity");
        SessionState.SetBool("LobbyApiChecks", true); EditorApplication.EnterPlaymode();
    }
    static void State(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool("LobbyApiChecks", false)) return;
        SessionState.SetBool("LobbyApiChecks", false); EditorApplication.update += Pump; Check();
    }
    static void Pump() { EditorApplication.isPaused = false; EditorApplication.QueuePlayerLoopUpdate(); }
    const string Profile = "{\"revision\":\"3\",\"displayName\":\"HTTP Player\",\"level\":12,\"stats\":{\"wins\":7,\"losses\":3},\"recentMatches\":[\"WIN\"]}";
    static string Inventory(int quantity) => "{\"revision\":\"4\",\"items\":[{\"instanceId\":\"potion\",\"name\":\"Potion\",\"kind\":\"consumable\",\"quantity\":" + quantity + ",\"capacity\":20}],\"nextCursor\":null}";
    static int assertions;
    static void Require(bool condition, string message) { if (!condition) throw new Exception(message); assertions++; }
    sealed class Server : IDisposable
    {
        readonly TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
        public readonly ConcurrentQueue<string> requests = new ConcurrentQueue<string>();
        public string bodyOverride;
        public int status = 200, delay;
        public string Root { get; }
        public Server()
        {
            listener.Start(); Root = "http://127.0.0.1:" + ((IPEndPoint)listener.LocalEndpoint).Port + "/api/v1";
            _ = Task.Run(async () => {
                try { while (true) { var client = await listener.AcceptTcpClientAsync(); _ = Task.Run(() => Serve(client)); } }
                catch (SocketException) { } catch (ObjectDisposedException) { }
            });
        }
        async Task Serve(TcpClient client)
        {
            using (client)
            try
            {
                var stream = client.GetStream(); var reader = new StreamReader(stream, Encoding.UTF8, false, 1024, true);
                string line = await reader.ReadLineAsync(); var request = new StringBuilder(line); int length = 0;
                for (string header; !string.IsNullOrEmpty(header = await reader.ReadLineAsync()); )
                {
                    request.Append('\n').Append(header);
                    if (header.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase)) length = int.Parse(header.Substring(15).Trim());
                }
                var chars = new char[length]; int offset = 0;
                while (offset < length) offset += await reader.ReadAsync(chars, offset, length - offset);
                request.Append('\n').Append(chars); requests.Enqueue(request.ToString());
                string body = bodyOverride ?? (line.StartsWith("POST") ? "{\"data\":{\"inventory\":" + Inventory(4) + "}}" :
                    line.Contains("/me/inventory") ? "{\"data\":" + Inventory(5) + "}" : "{\"data\":" + Profile + "}");
                int responseStatus = status, responseDelay = delay;
                if (responseDelay > 0) await Task.Delay(responseDelay);
                var bytes = Encoding.UTF8.GetBytes(body);
                var headers = Encoding.ASCII.GetBytes("HTTP/1.1 " + responseStatus + " Result\r\nContent-Type: application/json\r\nContent-Length: " + bytes.Length + "\r\nConnection: close\r\n\r\n");
                await stream.WriteAsync(headers, 0, headers.Length); await stream.WriteAsync(bytes, 0, bytes.Length);
            }
            catch (IOException) { } catch (ObjectDisposedException) { }
        }
        public void Dispose() { listener.Stop(); }
    }
    static async Task<LobbyServiceException> Failure(Func<Task> call)
    {
        try { await call(); } catch (LobbyServiceException error) { return error; }
        throw new Exception("Expected API failure");
    }
    static async void Check()
    {
        try
        {
            using (var server = new Server())
            {
                var c = UnityEngine.Object.FindFirstObjectByType<LobbyController>(); c.enabled = false;
                AuthManager.Instance.SetSession(new AccountSession("test-player", "test-token", DateTimeOffset.UtcNow.AddHours(1), server.Root));
                var api = new LobbyApiService(AuthManager.Instance.Current, 1);
                var profile = await api.GetProfileAsync(CancellationToken.None);
                Require(profile.nickname == "HTTP Player" && profile.wins == 7 && profile.recentMatches.Length == 1, "GET profile mapping");
                var inventory = await api.GetInventoryAsync(CancellationToken.None);
                Require(inventory.items.Length == 1 && inventory.items[0].quantity == 5, "GET inventory mapping");
                var used = await api.UseItemAsync("potion", "stable-key", CancellationToken.None);
                Require(used.inventory.items[0].quantity == 4, "POST authoritative inventory");
                var requests = server.requests.ToArray();
                Require(requests[0].StartsWith("GET /api/v1/me HTTP"), "Profile route");
                Require(requests[1].StartsWith("GET /api/v1/me/inventory HTTP"), "Inventory route");
                Require(requests[2].StartsWith("POST /api/v1/me/inventory/potion/use HTTP") && requests[2].Contains("{\"quantity\":1}"), "Use route/body");
                Require(requests[0].Contains("Authorization: Bearer test-token") && requests[2].Contains("Idempotency-Key: stable-key"), "Authentication and idempotency headers");
                c.enabled = true; c.ConfigureService(api); await c.RefreshAsync();
                c.OpenProfile(); Require(c.Model.Profile.Nickname == "HTTP Player" && c.nickname.text.Contains("HTTP Player"), "HTTP response rendered in profile");
                c.OpenInventory(); await c.RefreshInventoryAsync(); c.InspectItem("potion"); await c.UseSelectedItemAsync();
                Require(c.Model.Items[0].Quantity == 4, "Controller uses HTTP mutation response");
                server.status = 401; server.bodyOverride = "{\"error\":{\"code\":\"UNAUTHORIZED\"}}";
                var unauthorized = await Failure(async () => await api.GetProfileAsync(CancellationToken.None));
                Require(unauthorized.Code == ApiErrorCode.Unauthorized, "401 enum classification");
                Require(unauthorized.ServerCode == "UNAUTHORIZED" && unauthorized.StatusCode == 401,
                    "Server code and HTTP status survive lobby exception conversion");
                c.enabled = false;
                AuthManager.Instance.SetSession(new AccountSession("test-player", "test-token", DateTimeOffset.UtcNow.AddHours(1), server.Root));
                api = new LobbyApiService(AuthManager.Instance.Current, 1);
                server.status = 403;
                server.bodyOverride = "{\"error\":{\"code\":\"FUTURE_SERVER_CODE\"}}";
                var forbidden = await Failure(async () => await api.GetProfileAsync(CancellationToken.None));
                Require(forbidden.Code == ApiErrorCode.Forbidden && forbidden.ServerCode == "FUTURE_SERVER_CODE",
                    "Unknown server codes are preserved without losing HTTP classification");
                Require(forbidden.Message == "You do not have permission to make this request.", "Enum selects user-facing message");

                server.status = 404;
                server.bodyOverride = "not-json";
                var notFound = await Failure(async () => await api.GetProfileAsync(CancellationToken.None));
                Require(notFound.Code == ApiErrorCode.NotFound && notFound.ServerCode == null,
                    "Non-JSON error keeps HTTP classification");

                server.status = 408;
                var timeout = await Failure(async () => await api.UseItemAsync("potion", "stable-key", CancellationToken.None));
                Require(timeout.Code == ApiErrorCode.RequestTimeout && timeout.OutcomeUnknown,
                    "Request timeout preserves uncertain mutation outcome");

                server.status = 500;
                Require((await Failure(async () => await api.UseItemAsync("potion", "stable-key", CancellationToken.None))).OutcomeUnknown, "5xx mutation uncertain");
                server.status = 200; server.bodyOverride = "{\"data\":{\"revision\":\"1\"}}";
                Require((await Failure(async () => await api.GetInventoryAsync(CancellationToken.None))).Code == ApiErrorCode.InvalidResponse, "Missing items rejected");
                server.bodyOverride = "{\"data\":" + Inventory(5).Replace(",\"quantity\":5", "") + "}";
                await Failure(async () => await api.GetInventoryAsync(CancellationToken.None)); assertions++;
                server.bodyOverride = "{\"data\":" + Profile.Replace("\"level\":12,", "") + "}";
                await Failure(async () => await api.GetProfileAsync(CancellationToken.None)); assertions++;
                server.bodyOverride = "broken";
                var invalidUse = await Failure(async () => await api.UseItemAsync("potion", "stable-key", CancellationToken.None));
                Require(invalidUse.Code == ApiErrorCode.InvalidItemUseResponse && invalidUse.OutcomeUnknown,
                    "Invalid item use result has its own enum and preserves mutation key");
                server.bodyOverride = null; server.delay = 2500;
                await Failure(async () => await api.GetInventoryAsync(CancellationToken.None)); assertions++;
                using (var cancellation = new CancellationTokenSource(80))
                {
                    try { await api.GetProfileAsync(cancellation.Token); throw new Exception("Cancellation ignored"); }
                    catch (OperationCanceledException) { assertions++; }
                }
                c.ConfigureService(null);
            }
            File.WriteAllText("api-result.txt", "PASS: " + assertions + " HTTP/API assertions"); EditorApplication.Exit(0);
        }
        catch (Exception error) { File.WriteAllText("api-result.txt", error.ToString()); Debug.LogException(error); EditorApplication.Exit(1); }
    }
}

