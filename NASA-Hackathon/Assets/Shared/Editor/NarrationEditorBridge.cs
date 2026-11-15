#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace NasaHack.EditorTools
{
    // Allows local automation to use the already-open editor. No credentials or network code.
    [InitializeOnLoad]
    static class NarrationEditorBridge
    {
        const string Folder = "Library/LastSignalNarration";
        static double nextPoll;

        static NarrationEditorBridge() => EditorApplication.update += Poll;

        static void Poll()
        {
            if (EditorApplication.timeSinceStartup < nextPoll) return;
            nextPoll = EditorApplication.timeSinceStartup + 1;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            string request = Folder + "/request.txt";
            if (!File.Exists(request)) return;
            string command = File.ReadAllText(request).Trim();
            // Import changed scripts before dispatching, including across a domain reload.
            if (!command.EndsWith(".ready", StringComparison.Ordinal))
            {
                File.WriteAllText(request, command + ".ready");
                AssetDatabase.Refresh();
                return;
            }
            command = command.Substring(0, command.Length - ".ready".Length);
            File.Delete(request);
            try
            {
                if (EditorUtility.scriptCompilationFailed) throw new InvalidOperationException("Scripts have compiler errors.");
                if (!EditorSceneManager.SaveOpenScenes()) throw new InvalidOperationException("Could not save open scenes.");
                switch (command)
                {
                    case "export": SceneNarrationGeneratorWindow.ExportManifest(); break;
                    case "assign": SceneNarrationGeneratorWindow.AssignManifest(); break;
                    case "validate": SceneNarrationGeneratorWindow.ValidateAllSceneAudio(); break;
                    case "playback": SceneNarrationPlaybackVerification.Start(); break;
                    default: throw new InvalidOperationException("Unknown narration command: " + command);
                }
                File.WriteAllText(Folder + "/result.txt", "PASS " + command);
            }
            catch (Exception exception)
            {
                File.WriteAllText(Folder + "/result.txt", "FAIL " + command + "\n" + exception);
                UnityEngine.Debug.LogException(exception);
            }
        }
    }
}
#endif
