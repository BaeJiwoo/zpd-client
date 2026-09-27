using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Zpd.Lobby;
using Zpd.Networking;
using Zpd.Gameplay;
using Zpd.Defense;

[InitializeOnLoad]
public static class LoginChecks
{
    static LoginChecks() { EditorApplication.playModeStateChanged += State; }
    public static void Run()
    {
        Begin("Assets/Scenes/Login.unity");
    }
    public static void RunRecovered()
    {
        Begin("Assets/Scenes/LoginRecovered.unity");
    }
    static void Begin(string scenePath)
    {
        PlayerSettings.insecureHttpOption=InsecureHttpOption.DevelopmentOnly;
        PlayerSettings.companyName="ZpdVerification"; PlayerSettings.productName="LoginValidation";
        EditorSceneManager.OpenScene(scenePath);
        SessionState.SetBool("LoginChecks",true); EditorApplication.EnterPlaymode();
    }
    static void State(PlayModeStateChange state)
    {
        if(state!=PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool("LoginChecks",false)) return;
        SessionState.SetBool("LoginChecks",false); EditorApplication.update+=Pump; Check();
    }
    static void Pump() { EditorApplication.QueuePlayerLoopUpdate(); }
    static int count;
    static int runtimeErrors;
    static void Log(string message, string stack, LogType type)
    { if (type == LogType.Exception && stack.Contains("Assets/Scripts/")) runtimeErrors++; }
    static void Require(bool value,string message) { if(!value) throw new Exception(message); count++; }
    static async Task Until(Func<bool> predicate)
    {
        var end=DateTime.UtcNow.AddSeconds(8);
        while(!predicate()) { if(DateTime.UtcNow>end) throw new Exception("Timed out waiting for operation"); await Task.Delay(20); }
    }
    static Task SignIn(LoginController login)
    {
        login.View.password.text = AuthTestServer.Password;
        return login.LoginAsync();
    }
    static async void Check()
    {
        try
        {
            await Task.Delay(300);
            Application.logMessageReceived += Log;
            Require(!AuthManager.Instance.IsSignedIn,"Fresh session");
            foreach(var input in new[]{"", " ", "abc\n123", new string('a',129)}) Require(!AuthValidation.TryNormalizePlayerId(input,out _),"Reject invalid ID");
            Require(AuthValidation.TryNormalizePlayerId(" 00042 ",out var id) && id=="00042","Preserve leading zeroes");
            Require(AuthValidation.TryNormalizePlayerId("player-A_01",out id) && id=="player-A_01","Opaque account ID");
            Require(AuthValidation.TryNormalizePlayerId("플레이어",out _),"Unicode account ID");
            try { new AccountSession("a","bad\r\ntoken",DateTimeOffset.UtcNow.AddHours(1),ApiClient.DefaultRoot); throw new Exception("Unsafe token accepted"); }
            catch(ArgumentException) { count++; }
            try { ApiClient.NormalizeRoot("http://example.com/api/v1"); throw new Exception("Remote HTTP accepted"); }
            catch(ArgumentException) { count++; }
            using(var server=new AuthTestServer())
            {
                var login=UnityEngine.Object.FindFirstObjectByType<LoginController>(); login.apiRoot=server.Root;
                Require(login.enabled && login.View.IsConfigured, "Authored controls recover missing serialized bindings at startup");
                var expectedId = login.View.loginId;
                var expectedPassword = login.View.password;
                login.View.loginId = null;
                login.View.password = null;
                Require(login.View.TryBindControls() && login.View.loginId == expectedId && login.View.password == expectedPassword,
                    "Missing bindings resolve to the correct existing controls");
                string originalName = expectedId.name;
                expectedId.name = "Custom ID";
                Require(login.View.TryBindControls() && login.View.loginId == expectedId,
                    "Recovery preserves explicitly assigned custom controls");
                expectedId.name = originalName;
                login.View.loginId.text=" "; await SignIn(login);
                Require(server.Requests.IsEmpty && !AuthManager.Instance.IsSignedIn,"Invalid input never calls API");
                login.View.loginId.text="player-0007";
                login.View.password.text=""; await login.LoginAsync();
                Require(server.Requests.IsEmpty, "Empty password never calls the API");
                Require(login.View.password.contentType == UnityEngine.UI.InputField.ContentType.Password, "Password input is masked");
                login.View.password.text="incorrect"; await login.LoginAsync();
                Require(!AuthManager.Instance.IsSignedIn && login.View.status.text == "Invalid ID or password.", "Invalid credentials have a clear message");
                Require(login.View.password.text == "", "Submitted password is cleared");
                server.LoginStatus=401; await SignIn(login);
                Require(!AuthManager.Instance.IsSignedIn && login.View.loginId.interactable,"Rejected login remains editable");
                server.LoginStatus=200; server.LoginBody="{\"data\":{\"playerId\":\"player-0007\"}}"; await SignIn(login);
                Require(!AuthManager.Instance.IsSignedIn,"Missing credentials rejected");
                server.LoginBody="{\"data\":{\"playerId\":\" \",\"accessToken\":\"token\",\"expiresAtUtc\":\""+DateTime.UtcNow.AddHours(1).ToString("O")+"\"}}";
                await SignIn(login); Require(!AuthManager.Instance.IsSignedIn,"Empty server account rejected");
                server.LoginBody="{\"data\":{\"playerId\":\"player-0007\",\"accessToken\":\"token\",\"expiresAtUtc\":\"2000-01-01T00:00:00Z\"}}";
                await SignIn(login); Require(!AuthManager.Instance.IsSignedIn,"Expired login response rejected");
                server.LoginBody=null; server.LoginDelay=150;
                int before=server.Requests.Count;
                var entering=SignIn(login); await SignIn(login);
                Require(login.IsBusy && !login.View.login.interactable,"Duplicate submission disabled while waiting");
                await entering;
                await Until(()=>UnityEngine.Object.FindFirstObjectByType<LobbyController>()?.Model.Profile!=null);
                var session=AuthManager.Instance.Current;
                Require(session.PlayerId=="player-0007","Server-confirmed session saved");
                Require(server.Requests.Skip(before).Count(r=>r.Path.EndsWith("/auth/login"))==1,"One login request");
                Require(server.Requests.Any(r=>r.Path.EndsWith("/auth/login") && r.Method=="POST" && r.Body.Contains("player-0007") && r.Body.Contains("password") && r.Body.Contains(AuthTestServer.Password) && r.Authorization==null),"Login route and body");
                Require(server.Requests.Any(r=>r.Path.EndsWith("/me") && r.Authorization=="Bearer token-player-0007"),"Lobby uses login token");
                SceneNavigation.Load(SceneNavigation.SoloDefense);
                await Until(()=>UnityEngine.Object.FindFirstObjectByType<DefenseGame>()!=null);
                await Task.Delay(200);
                var tracker=GameSessionTracker.Instance;
                tracker.Begin(GameMode.SoloDefense);
                var snapshot=tracker.Finish("test",100,100);
                Require(snapshot.ownerPlayerId==session.PlayerId && snapshot.accountApiRoot==server.Root,"Run bound to starting account");
                Require(!tracker.CompletedJson.Contains("token-player"),"No token in persisted result");
                var upload=UnityEngine.Object.FindFirstObjectByType<GameResultUploadClient>();
                var reward=UnityEngine.Object.FindFirstObjectByType<DefenseRewardClient>();
                upload.Submit(snapshot);
                reward.Submit(new DefenseRunReport { runId=snapshot.runId,ownerPlayerId=snapshot.ownerPlayerId,accountApiRoot=snapshot.accountApiRoot });
                await Until(()=>!upload.IsBusy && !reward.IsBusy);
                Require(upload.Succeeded && reward.Succeeded,"Authenticated result and reward complete");
                Require(server.Requests.Any(r=>r.Path.EndsWith("/me/game-results") && r.Authorization=="Bearer token-player-0007" && r.Key==snapshot.runId+":game-result"),"Result authentication and idempotency");
                Require(server.Requests.Any(r=>r.Path.EndsWith("/rewards") && r.Authorization=="Bearer token-player-0007" && r.Key==snapshot.runId),"Reward authentication and idempotency");
                SceneNavigation.Load(SceneNavigation.Lobby);
                await Until(()=>UnityEngine.Object.FindFirstObjectByType<LobbyController>()?.Model.Profile!=null);
                Require(ReferenceEquals(AuthManager.Instance.Current,session),"Scene round trip retains session and restores API");
                var lobby=UnityEngine.Object.FindFirstObjectByType<LobbyController>(); lobby.ChangePlayer();
                await Until(()=>UnityEngine.Object.FindFirstObjectByType<LoginController>()!=null);
                Require(!AuthManager.Instance.IsSignedIn,"Logout clears shared session");
                login=UnityEngine.Object.FindFirstObjectByType<LoginController>(); login.apiRoot=server.Root;
                login.View.loginId.text="other-player";
                var pending=SignIn(login); login.enabled=false; await pending;
                Require(!AuthManager.Instance.IsSignedIn,"Disabled login ignores late response");
                login.enabled=true;
                Require(login.View.loginId.interactable && login.View.password.interactable && login.View.login.interactable,
                    "Re-enabling a cancelled screen restores its input controls");
                await SignIn(login);
                await Until(()=>UnityEngine.Object.FindFirstObjectByType<LobbyController>()?.Model.Profile!=null);
                Require(AuthManager.Instance.PlayerId=="other-player","Account switch authenticates new ID");
                var testObject=new GameObject("Upload checks"); upload=testObject.AddComponent<GameResultUploadClient>(); reward=testObject.AddComponent<DefenseRewardClient>();
                before=server.Requests.Count; upload.Submit(snapshot);
                reward.Submit(new DefenseRunReport {runId=snapshot.runId,ownerPlayerId=snapshot.ownerPlayerId,accountApiRoot=snapshot.accountApiRoot});
                Require(!upload.IsBusy && !reward.IsBusy && !upload.Succeeded && !reward.Succeeded && server.Requests.Count==before,"Old account reports never sent by new account");
                UnityEngine.Object.Destroy(testObject);
                lobby=UnityEngine.Object.FindFirstObjectByType<LobbyController>(); server.ProfileStatus=401;
                await lobby.RefreshProfileAsync();
                Require(!AuthManager.Instance.IsSignedIn && lobby.Model.Profile==null && lobby.Model.Items.Count==0,"401 clears session and private UI");
                lobby.enabled=false;
                server.ProfileStatus=401; server.ProfileDelay=400;
                AuthManager.Instance.SetSession(new AccountSession("old","old-token",DateTimeOffset.UtcNow.AddMinutes(1),server.Root));
                var oldRead=new LobbyApiService(AuthManager.Instance.Current).GetProfileAsync(CancellationToken.None);
                await Task.Delay(50);
                var newer=new AccountSession("new","new-token",DateTimeOffset.UtcNow.AddMinutes(1),server.Root);
                AuthManager.Instance.SetSession(newer);
                try { await oldRead; throw new Exception("Old account response accepted"); } catch(LobbyServiceException) { count++; }
                Require(ReferenceEquals(AuthManager.Instance.Current,newer),"Stale response cannot clear new account");
                AuthManager.Instance.SetSession(new AccountSession("expiring","expires-token",DateTimeOffset.UtcNow.AddMilliseconds(100),server.Root));
                await Task.Delay(160); AuthManager.Instance.CheckExpiry();
                Require(!AuthManager.Instance.IsSignedIn && AuthManager.Instance.Current==null,"Expired session cleared");
                before=server.Requests.Count;
                try { await new LobbyApiService(newer).GetProfileAsync(CancellationToken.None); throw new Exception("Unauthenticated request accepted"); } catch(LobbyServiceException) { count++; }
                Require(before==server.Requests.Count,"Invalid session sends no request");
            }
            Require(runtimeErrors == 0,"No runtime exceptions during authenticated scene transitions");
            Finish("PASS: "+count+" authenticated login/API checks",0);
        }
        catch(Exception e) { Finish("FAIL: "+e,1); }
    }
    static void Finish(string result,int code) { Application.logMessageReceived-=Log; EditorApplication.update-=Pump; File.WriteAllText(Path.Combine(Directory.GetParent(Application.dataPath).FullName,"login-result.txt"),result); Debug.Log(result); EditorApplication.Exit(code); }
}
