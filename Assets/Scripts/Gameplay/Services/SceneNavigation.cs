using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Zpd.Gameplay
{
    /// <summary>Shared scene navigation; full paths avoid ambiguous scene names.</summary>
    public static class SceneNavigation
    {
        public const string Login = "Assets/Scenes/Login.unity";
        public const string Lobby = "Assets/Scenes/Lobby.unity";
        public const string LegacyLobby = "Assets/Scenes/LegacyLobby.unity";
        public const string SoloDefense = "Assets/Scenes/SoloDefense.unity";

        public static bool CanLoad(string path) => Application.CanStreamedLevelBeLoaded(path);

        public static bool Load(string path)
        {
            if (!CanLoad(path))
            {
                Debug.LogError("Scene is not enabled in the build: " + path);
                return false;
            }

            try
            {
                SceneManager.LoadScene(path, LoadSceneMode.Single);
                return true;
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                return false;
            }
        }
    }
}
