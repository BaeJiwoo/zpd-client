using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using Zpd.Lobby;

[InitializeOnLoad]
public static class LobbyPointerChecks
{
    static LobbyPointerChecks() { EditorApplication.playModeStateChanged += State; }
    public static void Run() => Start("Assets/Scenes/Lobby.unity");
    public static void Snapshot() => Start("Assets/Scenes/UserLobbySnapshot.unity");
    static void Start(string path)
    {
        EditorSceneManager.OpenScene(path);
        var lobby=UnityEngine.Object.FindFirstObjectByType<LobbyController>();
        foreach(var name in new[]{"Profile","Inventory Button","Friends"})
        {
            var b=lobby.home.transform.Find(name).GetComponent<Button>();
            Debug.Log("Authored "+name+" calls="+b.onClick.GetPersistentEventCount()+" target="+b.onClick.GetPersistentTarget(0)+" method="+b.onClick.GetPersistentMethodName(0));
        }
        if(path.Contains("Snapshot")) { EditorSettings.serializationMode=SerializationMode.ForceText; EditorSceneManager.MarkSceneDirty(lobby.gameObject.scene); EditorSceneManager.SaveScene(lobby.gameObject.scene); }
        SessionState.SetBool("LobbyPointerChecks",true); EditorApplication.EnterPlaymode();
    }
    static void State(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool("LobbyPointerChecks",false)) return;
        SessionState.SetBool("LobbyPointerChecks",false); EditorApplication.update += Check;
        deadline=EditorApplication.timeSinceStartup+20;
    }
    static LobbyController c;
    static Mouse mouse;
    static InputSystemUIInputModule module;
    static int step;
    static double next,deadline;
    static Vector2 pos;
    static void Check()
    {
        if(EditorApplication.timeSinceStartup<next) return;
        next=EditorApplication.timeSinceStartup+.12;
        try
        {
            if(EditorApplication.timeSinceStartup>deadline) throw new Exception("Pointer test timed out");
            if(c==null)
            {
                c=UnityEngine.Object.FindFirstObjectByType<LobbyController>();
                Application.runInBackground=true;
                InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
                InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                mouse=InputSystem.AddDevice<Mouse>();
                module=EventSystem.current.GetComponent<InputSystemUIInputModule>();
                module.actionsAsset.devices=new InputDevice[]{mouse};
                Debug.Log("Input enabled: point="+module.point.action.enabled+" click="+module.leftClick.action.enabled);
                return;
            }
            EditorApplication.isPaused = false;
            EditorApplication.QueuePlayerLoopUpdate();

            module.Process();
            if(step==0) Move(c.home.transform.Find("Profile").GetComponent<Button>());
            if(step==1) { Debug.Log("Pointer value="+module.point.action.ReadValue<Vector2>()+" frame="+Time.frameCount); Press(); }
            if(step==2) Release();
            if(step==3)
            {
                Assert(c.profilePanel.activeInHierarchy,"PlayerInfo did not open from mouse click");
                Move(c.profilePanel.transform.Find("Close").GetComponent<Button>());
            }
            if(step==4) Press(); if(step==5) Release();
            if(step==6)
            {
                Assert(!c.sections.gameObject.activeSelf,"PlayerInfo did not close");
                Move(c.home.transform.Find("Inventory Button").GetComponent<Button>());
            }
            if(step==7) Press(); if(step==8) Release();
            if(step==9)
            {
                Assert(c.inventoryPanel.activeInHierarchy,"Inventory did not open from mouse click");
                Move(c.inventoryPanel.transform.Find("Close").GetComponent<Button>());
            }
            if(step==10) Press(); if(step==11) Release();
            if(step==12) { Assert(!c.sections.gameObject.activeSelf,"Inventory did not close"); Finish("PASS: PlayerInfo and Inventory mouse clicks and close buttons through the Input System UI module",0); }
            step++;
        }
        catch(Exception e) { Finish("FAIL: "+e,1); }
    }
    static void Move(Button button)
    {
        Canvas.ForceUpdateCanvases(); pos=RectTransformUtility.WorldToScreenPoint(null,button.transform.position);
        var hits=new List<RaycastResult>(); EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=pos},hits);
        Debug.Log("Click "+button.name+" at "+pos+" interactable="+button.IsInteractable()+" hits="+string.Join(",",hits.Take(8).Select(h=>h.gameObject.name)));
        Assert(hits.Count>0 && ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject)==button.gameObject,"Pointer blocked for "+button.name);
        Send(new MouseState{position=pos});
    }
    static void Press() { Send(new MouseState{position=pos}.WithButton(MouseButton.Left,true)); }
    static void Release() { Send(new MouseState{position=pos}); }
    static void Send(MouseState state)
    {
        InputSystem.QueueStateEvent(mouse,state);
        // A headless editor has no focused Game View, so the public Update() selects Editor input.
        // Explicitly tick the package's player update for this isolated input regression check.
        typeof(InputSystem).GetMethod("Update",System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic,
            null,new[]{typeof(InputUpdateType)},null).Invoke(null,new object[]{InputUpdateType.Dynamic});
        module.Process();
    }
    static void Assert(bool value,string message) { if(!value) throw new Exception(message); }
    static void Finish(string message,int code) { EditorApplication.update-=Check; File.WriteAllText(Path.Combine(Directory.GetParent(Application.dataPath).FullName,"pointer-result.txt"),message); Debug.Log(message); EditorApplication.Exit(code); }
}
