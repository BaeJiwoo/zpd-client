using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Zpd.Lobby;
using Zpd.Lobby.Editor;

public static class PresentationChecks
{
    public static void Apply()
    {
        LoginSceneBuilder.Build();
        var font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Resources/Fonts/NexonLv1/NEXONLv1GothicRegular.ttf");

        if (font == null)
        {
            throw new InvalidOperationException("The bundled NEXON font is missing.");
        }

        int count = 0;

        foreach (string path in Directory.GetFiles("Assets/Scenes", "*.unity"))
        {
            var scene = EditorSceneManager.OpenScene(path);

            foreach (var label in scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Text>(true)))
            {
                if (Regex.IsMatch(label.text, "[\uac00-\ud7a3]"))
                {
                    throw new InvalidOperationException(path + ": untranslated text on " + label.name + ": " + label.text);
                }

                label.font = font;
                EditorUtility.SetDirty(label);
                count++;
            }

            EditorSceneManager.SaveScene(scene);
        }

        AssetDatabase.SaveAssets();
        File.WriteAllText("presentation-result.txt", "PASS: " + count + " scene labels use English and NEXON Lv.1 Gothic.");
        EditorApplication.Exit(0);
    }
}
