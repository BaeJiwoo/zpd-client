using System;
using System.IO;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Zpd.Defense;

[InitializeOnLoad]
public static class PresentationCapture
{
    static PresentationCapture()
    {
        EditorApplication.playModeStateChanged += OnStateChanged;
    }

    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Login.unity");
        SessionState.SetBool("PresentationCapture", true);
        EditorApplication.EnterPlaymode();
    }

    private static void OnStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool("PresentationCapture", false))
        {
            SessionState.SetBool("PresentationCapture", false);
            EditorApplication.update += Pump;
            CaptureScreens();
        }
    }

    private static void Pump()
    {
        EditorApplication.QueuePlayerLoopUpdate();
    }

    private static async void CaptureScreens()
    {
        try
        {
            await Task.Delay(500);
            await Capture("login-english.png", UnityEngine.Object.FindFirstObjectByType<Canvas>(), null);
            UnityEngine.SceneManagement.SceneManager.LoadScene("SoloDefense");
            await Task.Delay(600);
            var game = UnityEngine.Object.FindFirstObjectByType<DefenseGame>();
            game.enabled = false;
            await Capture("defense-english.png", game.txt_health.canvas, game.camera_world);
            game.game_object_ready_panel.SetActive(false);
            game.game_object_help_panel.SetActive(true);
            await Capture("help-english.png", game.txt_health.canvas, game.camera_world);
            game.game_object_help_panel.SetActive(false);
            game.StartRun();
            game.Model.BeginPreparation(game.wave_rules);
            game.defense_supplies.OpenShop();
            await Capture("upgrades-english.png", game.txt_health.canvas, game.camera_world);
            UnityEngine.SceneManagement.SceneManager.LoadScene("Lobby");
            await Task.Delay(500);
            await Capture("lobby-english.png", UnityEngine.Object.FindFirstObjectByType<Canvas>(), null);
            EditorApplication.Exit(0);
        }
        catch (Exception error)
        {
            Debug.LogException(error);
            EditorApplication.Exit(1);
        }
    }

    private static async Task Capture(string path, Canvas canvas, Camera camera)
    {
        if (camera == null)
        {
            camera = new GameObject("Capture Camera").AddComponent<Camera>();
            camera.orthographic = true;
        }

        var texture = new RenderTexture(1280, 720, 24);
        texture.Create();
        camera.targetTexture = texture;
        var canvases = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);

        foreach (var root in canvases)
        {
            if (!root.isRootCanvas)
            {
                continue;
            }

            root.renderMode = RenderMode.ScreenSpaceCamera;
            root.worldCamera = camera;
            root.planeDistance = 1;
            root.sortingOrder += 100;
        }

        Canvas.ForceUpdateCanvases();
        await Task.Delay(200);
        Canvas.ForceUpdateCanvases();
        RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = texture });
        var previous = RenderTexture.active;
        RenderTexture.active = texture;
        var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
        image.Apply();
        File.WriteAllBytes(path, image.EncodeToPNG());
        RenderTexture.active = previous;
        camera.targetTexture = null;
        foreach (var root in canvases)
        {
            if (root.isRootCanvas)
            {
                root.renderMode = RenderMode.ScreenSpaceOverlay;
                root.sortingOrder -= 100;
            }
        }
        UnityEngine.Object.DestroyImmediate(image);
        UnityEngine.Object.DestroyImmediate(texture);
    }
}
