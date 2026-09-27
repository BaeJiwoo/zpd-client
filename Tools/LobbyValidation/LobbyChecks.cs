using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Zpd.Lobby;
using Zpd.Networking;

[InitializeOnLoad]
public static class LobbyChecks
{
    static LobbyChecks() { EditorApplication.playModeStateChanged+=State; }
    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Lobby.unity");
        SessionState.SetBool("LobbyMvcChecks",true); EditorApplication.EnterPlaymode();
    }
    static void State(PlayModeStateChange state)
    {
        if(state!=PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool("LobbyMvcChecks",false)) return;
        SessionState.SetBool("LobbyMvcChecks",false); EditorApplication.update+=Pump; Check();
    }
    static void Pump() { EditorApplication.isPaused=false; EditorApplication.QueuePlayerLoopUpdate(); }
    static int assertions;
    static void Require(bool yes,string message) { if(!yes) throw new Exception(message); assertions++; }
    static LobbyInventoryData Inventory(long revision,int amount=5) => new LobbyInventoryData {revision=revision,items=new[]{new LobbyItemData{id="potion",name="Recovery",quantity=amount,capacity=20,kind=LobbyItemKind.Consumable},new LobbyItemData{id="armor",name="Armor",quantity=1,kind=LobbyItemKind.Equipment}}};
    static LobbyProfileData Profile(string name,long revision=1) => new LobbyProfileData{nickname=name,revision=revision,level=12,wins=7,losses=3,recentMatches=new[]{"WIN / Solo Defense"}};
    sealed class FakeService : ILobbyService
    {
        public Func<Task<LobbyProfileData>> profile=()=>Task.FromResult(Profile("Account A"));
        public Func<Task<LobbyInventoryData>> inventory=()=>Task.FromResult(Inventory(1));
        public Func<Task<LobbyUseItemResult>> use;
        public readonly List<string> operations=new List<string>();
        public Task<LobbyProfileData> GetProfileAsync(CancellationToken token)=>profile();
        public Task<LobbyInventoryData> GetInventoryAsync(CancellationToken token)=>inventory();
        public Task<LobbyUseItemResult> UseItemAsync(string id,string operation,CancellationToken token) {operations.Add(operation);return use();}
    }
    static async void Check()
    {
        try
        {
            var m=new LobbyModel(); Require(m.InventoryState==LobbyLoadState.Unavailable,"Unknown inventory is not empty");
            var dto=Inventory(3); m.ApplyInventory(dto); dto.items[0].quantity=999;
            Require(m.Items[0].Quantity==5,"Model copies response data");
            Require(!m.ApplyInventory(Inventory(2)) && m.InventoryRevision==3,"Older resource revision rejected");
            try { m.ApplyInventory(new LobbyInventoryData{revision=4,items=new[]{dto.items[0],dto.items[0]}}); throw new Exception("Duplicate IDs accepted"); } catch(ArgumentException) { assertions++; }
            Require(m.InventoryRevision==3,"Invalid response cannot partially replace snapshot");
            m.ApplyInventory(new LobbyInventoryData{revision=4,items=Array.Empty<LobbyItemData>()}); Require(m.InventoryState==LobbyLoadState.Ready && m.Items.Count==0,"Confirmed empty state");
            m.ApplyProfile(Profile("Player")); Require(m.Profile.WinRate==70,"Derived win rate");
            var c=UnityEngine.Object.FindFirstObjectByType<LobbyController>();
            Require(c.View!=null && !c.sections.gameObject.activeSelf,"Saved MVC view initialized");
            c.OpenProfile(); Require(c.profilePanel.activeInHierarchy,"Profile opens without API"); c.CloseTopmost();
            c.OpenInventory(); Require(c.inventoryPanel.activeInHierarchy,"Inventory opens without API"); c.CloseTopmost();
            var api=new FakeService(); c.ConfigureService(api);
            Require(c.Model.Profile.Nickname=="Account A" && c.Model.Items.Count==2,"Service responses populate model");
            c.OpenInventory(); c.InspectItem("potion");
            var mutation=new TaskCompletionSource<LobbyUseItemResult>(); api.use=()=>mutation.Task;
            var use=c.UseSelectedItemAsync(); await c.UseSelectedItemAsync();
            Require(api.operations.Count==1 && c.Model.Items[0].Quantity==5,"Pending request suppresses duplicate and no optimistic decrement");
            c.CloseModal(); c.InspectItem("potion"); Require(!c.useButton.interactable,"Reopening does not unlock pending use");
            c.CloseModal(); mutation.SetResult(new LobbyUseItemResult{inventory=Inventory(2,4)}); await use;
            Require(c.Model.Items[0].Quantity==4 && !c.modal.activeSelf,"Server result updates quantity without reopening popup");
            var old=new TaskCompletionSource<LobbyInventoryData>(); var recent=new TaskCompletionSource<LobbyInventoryData>();
            var reads=new Queue<Task<LobbyInventoryData>>(new[]{old.Task,recent.Task}); api.inventory=()=>reads.Dequeue();
            var a=c.RefreshInventoryAsync(); var b=c.RefreshInventoryAsync(); recent.SetResult(Inventory(4,3)); await b;
            old.SetResult(Inventory(99,99)); await a;
            Require(c.Model.InventoryRevision==4 && c.Model.Items[0].Quantity==3,"Out-of-order reads ignored even when transport ignores cancellation");
            api.inventory=()=>Task.FromResult(Inventory(4,3));
            c.InspectItem("potion"); api.use=()=>Task.FromException<LobbyUseItemResult>(new LobbyServiceException(ApiErrorCode.RequestTimeout, outcomeUnknown: true));
            await c.UseSelectedItemAsync(); var operation=c.Model.OperationId;
            Require(operation!=null && !c.Model.IsUsingItem && c.Model.Items[0].Quantity==3,"Unknown result retains operation identity and quantity");
            api.use=()=>Task.FromResult(new LobbyUseItemResult{inventory=Inventory(5,2)}); await c.UseSelectedItemAsync();
            Require(api.operations[api.operations.Count-1]==operation && c.Model.OperationId==null,"Retry reuses idempotency key");
            c.InspectItem("armor"); Require(!c.useButton.gameObject.activeSelf && !c.quantityRoot.activeSelf,"Equipment is informational");
            c.CloseSection();
            var stale=new TaskCompletionSource<LobbyProfileData>(); api.profile=()=>stale.Task; var read=c.RefreshProfileAsync();
            var accountB=new FakeService {profile=()=>Task.FromResult(Profile("Account B"))}; c.ConfigureService(accountB);
            stale.SetResult(Profile("Old account",100)); await read;
            Require(c.Model.Profile.Nickname=="Account B","Previous account response ignored");
            var disabled=new TaskCompletionSource<LobbyInventoryData>(); accountB.inventory=()=>disabled.Task;
            var pending=c.RefreshInventoryAsync(); c.enabled=false; disabled.SetResult(Inventory(100)); await pending;
            Require(c.Model.InventoryState==LobbyLoadState.Unavailable,"Disabled controller ignores late response and clears account snapshot");
            accountB.inventory=()=>Task.FromResult(Inventory(1)); c.enabled=true;
            Require(c.Model.Items.Count==2,"Reenabled controller refreshes through service");
            c.ConfigureService(null); Require(c.Model.Profile==null && c.Model.Items.Count==0,"Logout removes prior account data");
            Finish("PASS: "+assertions+" MVC/service assertions (snapshots, revisions, stale replies, account changes, pending/retry, lifecycle, UI)",0);
        }
        catch(Exception e) { Finish("FAIL: "+e,1); }
    }
    static void Finish(string message,int code) {EditorApplication.update-=Pump;File.WriteAllText(Path.Combine(Directory.GetParent(Application.dataPath).FullName,"validation-result.txt"),message);Debug.Log(message);EditorApplication.Exit(code);}
}
