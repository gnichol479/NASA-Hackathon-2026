#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using NasaHack;

namespace LastSignal.IngenuityFlashback.EditorTools
{
    /// <summary>
    /// Generates offline, assignable Brian voice clips in the Editor. The API key lives only in
    /// this window's memory and is sent to ElevenLabs only when the user explicitly generates audio.
    /// </summary>
    public sealed class IngenuityElevenLabsWindow : EditorWindow
    {
        private const string VoiceId = IngenuityFlashbackSceneController.ElevenLabsBrianVoiceId;
        private const string OutputFolder = "Assets/Ingenuity/NASA-Headquarters/LastSignalFlashback/Audio/ElevenLabsBrian";

        private static readonly Cue[] Cues =
        {
            new Cue("Facility introduction", "introNarrationText", "introNarration", "01_Introduction"),
            new Cue("Solar arrays", "solarPanelNarrationText", "solarPanelNarration", "02_Solar_Arrays"),
            new Cue("Camera mast", "cameraMastNarrationText", "cameraMastNarration", "03_Camera_Mast"),
            new Cue("Instrument arm", "instrumentArmNarrationText", "instrumentArmNarration", "04_Instrument_Arm"),
            new Cue("Systems check", "systemsCheckNarrationText", "systemsCheckNarration", "05_Systems_Check")
        };

        // Deliberately not serialized to EditorPrefs, the scene, or this project.
        private string apiKey = string.Empty;
        private string status = "Enter your ElevenLabs API key to generate clips.";
        private Queue<Cue> generateQueue;
        private string batchKey;
        private UnityWebRequest activeRequest;
        private bool busy;
        private Vector2 scroll;

        [MenuItem("Last Signal/Ingenuity Flashback/Narration/ElevenLabs Brian")]
        public static void Open()
        {
            IngenuityElevenLabsWindow window = GetWindow<IngenuityElevenLabsWindow>("Brian Narration");
            window.minSize = new Vector2(520f, 640f);
            window.position = new Rect(window.position.x, window.position.y, Mathf.Max(560f, window.position.width), Mathf.Max(760f, window.position.height));
        }

        private void OnDisable()
        {
            EditorUtility.ClearProgressBar();
            if (activeRequest != null && !activeRequest.isDone)
                activeRequest.Abort();
            activeRequest?.Dispose();
            activeRequest = null;
            apiKey = string.Empty;
            batchKey = string.Empty;
            busy = false;
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("ElevenLabs · Brian", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Voice ID: " + VoiceId, EditorStyles.miniLabel);
            EditorGUILayout.HelpBox(
                "Generate clips in the Editor and assign them to this scene. The API key stays in this window's memory and is never saved with the project. Generation sends the selected caption text to ElevenLabs.",
                MessageType.Info);
            EditorGUILayout.HelpBox(
                "This window generates the five flashback clips only. For the complete Ingenuity scenario, use Last Signal > Ingenuity > ElevenLabs Brian - Flashback + Launch to include the seven launch clips too.",
                MessageType.None);
            if (!ElevenLabsFeatureSettings.IsEnabled)
                EditorGUILayout.HelpBox("ElevenLabs is temporarily disabled. Captions remain available; no requests or audio previews will run until it is re-enabled from Last Signal > ElevenLabs.", MessageType.Warning);

            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorGUILayout.HelpBox("Exit Play mode to generate or assign narration clips.", MessageType.Warning);
                return;
            }

            IngenuityFlashbackSceneController controller = FindController();
            if (controller == null)
            {
                EditorGUILayout.HelpBox("Open the Ingenuity-Flashback scene to edit its caption script and audio slots.", MessageType.Warning);
                return;
            }

            using (new EditorGUI.DisabledScope(busy || !ElevenLabsFeatureSettings.IsEnabled))
            {
                apiKey = EditorGUILayout.PasswordField("ElevenLabs API key", apiKey);
                EditorGUILayout.Space(6);

                SerializedObject serialized = new SerializedObject(controller);
                serialized.Update();
                scroll = EditorGUILayout.BeginScrollView(scroll);
                foreach (Cue cue in Cues)
                    DrawCue(serialized, cue);
                EditorGUILayout.Space(8);
                using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(apiKey)))
                {
                    if (GUILayout.Button("Generate and assign all five clips", GUILayout.Height(30)))
                    {
                        serialized.ApplyModifiedProperties();
                        BeginGenerateAll(serialized, controller);
                    }
                }
                EditorGUILayout.EndScrollView();
                serialized.ApplyModifiedProperties();
            }

            EditorGUILayout.HelpBox(status, busy ? MessageType.Info : MessageType.None);
        }

        private void DrawCue(SerializedObject serialized, Cue cue)
        {
            SerializedProperty text = serialized.FindProperty(cue.TextProperty);
            SerializedProperty clip = serialized.FindProperty(cue.ClipProperty);
            if (text == null || clip == null)
            {
                EditorGUILayout.HelpBox("Narration fields are missing for " + cue.Label + ".", MessageType.Error);
                return;
            }

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(cue.Label, EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            string edited = EditorGUILayout.TextArea(text.stringValue, GUILayout.MinHeight(44));
            if (EditorGUI.EndChangeCheck())
                text.stringValue = edited;

            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.ObjectField("Assigned clip", clip.objectReferenceValue, typeof(AudioClip), false);
            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(busy || !ElevenLabsFeatureSettings.IsEnabled || string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(text.stringValue)))
            {
                if (GUILayout.Button("Generate", GUILayout.Width(90)))
                {
                    serialized.ApplyModifiedProperties();
                    GenerateOne(cue, text.stringValue, apiKey, FindController());
                }
            }
            using (new EditorGUI.DisabledScope(!ElevenLabsFeatureSettings.IsEnabled || clip.objectReferenceValue == null))
            {
                if (GUILayout.Button("Play", GUILayout.Width(60)))
                    PlayPreview((AudioClip)clip.objectReferenceValue);
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        private void BeginGenerateAll(SerializedObject serialized, IngenuityFlashbackSceneController controller)
        {
            serialized.ApplyModifiedProperties();
            SerializedObject fresh = new SerializedObject(controller);
            fresh.Update();
            generateQueue = new Queue<Cue>();
            foreach (Cue cue in Cues)
            {
                SerializedProperty text = fresh.FindProperty(cue.TextProperty);
                if (text == null || string.IsNullOrWhiteSpace(text.stringValue))
                {
                    status = "Add narration text for " + cue.Label + " before generating the full set.";
                    Repaint();
                    return;
                }
                generateQueue.Enqueue(cue);
            }
            batchKey = apiKey;
            busy = true;
            GenerateNext(controller);
        }

        private void GenerateNext(IngenuityFlashbackSceneController controller)
        {
            if (generateQueue == null || generateQueue.Count == 0)
            {
                busy = false;
                apiKey = string.Empty;
                batchKey = string.Empty;
                status = "All five Brian clips are imported and assigned. Save the scene, then press Play to hear them in context.";
                EditorUtility.ClearProgressBar();
                Repaint();
                return;
            }

            Cue cue = generateQueue.Dequeue();
            SerializedObject serialized = new SerializedObject(controller);
            string text = serialized.FindProperty(cue.TextProperty).stringValue;
            status = "Generating " + cue.Label + " (" + (Cues.Length - generateQueue.Count) + " of " + Cues.Length + ")…";
            Generate(cue, text, batchKey, controller, clip =>
            {
                if (clip == null)
                {
                    busy = false;
                    apiKey = string.Empty;
                    batchKey = string.Empty;
                    generateQueue.Clear();
                    EditorUtility.ClearProgressBar();
                    Repaint();
                    return;
                }
                GenerateNext(controller);
            });
        }

        private void GenerateOne(Cue cue, string text, string key, IngenuityFlashbackSceneController controller)
        {
            if (controller == null)
            {
                status = "Open the flashback scene before generating audio.";
                return;
            }
            busy = true;
            Generate(cue, text, key, controller, clip =>
            {
                busy = false;
                apiKey = string.Empty;
                status = clip != null
                    ? cue.Label + " was generated, assigned, and previewed. Save the scene to keep the assignment."
                    : "Generation failed. See the Unity Console for the ElevenLabs response.";
                Repaint();
            });
        }

        private void Generate(Cue cue, string text, string key,
            IngenuityFlashbackSceneController controller, Action<AudioClip> completed)
        {
            if (!ElevenLabsFeatureSettings.IsEnabled)
            {
                status = "ElevenLabs is temporarily disabled. No request was sent.";
                completed(null);
                return;
            }

            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(text))
            {
                status = "An API key and narration text are required.";
                completed(null);
                return;
            }

            var payload = new SpeechRequest
            {
                text = text,
                model_id = "eleven_multilingual_v2",
                voice_settings = new VoiceSettings { stability = 0.5f, similarity_boost = 0.75f }
            };
            string json = JsonUtility.ToJson(payload);
            string url = "https://api.elevenlabs.io/v1/text-to-speech/" + VoiceId + "?output_format=mp3_44100_128";
            activeRequest = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST)
            {
                uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json)),
                downloadHandler = new DownloadHandlerBuffer(),
                timeout = 120
            };
            activeRequest.SetRequestHeader("xi-api-key", key);
            activeRequest.SetRequestHeader("Content-Type", "application/json");
            activeRequest.SetRequestHeader("Accept", "audio/mpeg");

            UnityWebRequest request = activeRequest;
            UnityWebRequestAsyncOperation operation = request.SendWebRequest();
            EditorUtility.DisplayProgressBar("ElevenLabs Brian", "Generating " + cue.Label + "…", 0f);
            operation.completed += _ =>
            {
                try
                {
                    if (request.result != UnityWebRequest.Result.Success)
                    {
                        string details = request.downloadHandler != null ? request.downloadHandler.text : request.error;
                        Debug.LogError("[ElevenLabs] " + cue.Label + " failed (HTTP " + request.responseCode + "): " + details);
                        status = "ElevenLabs returned an error for " + cue.Label + " (HTTP " + request.responseCode + "). See Console.";
                        completed(null);
                        return;
                    }

                    byte[] bytes = request.downloadHandler.data;
                    if (bytes == null || bytes.Length < 128)
                    {
                        Debug.LogError("[ElevenLabs] The response for " + cue.Label + " contained no audio.");
                        status = "ElevenLabs returned an empty audio file for " + cue.Label + ".";
                        completed(null);
                        return;
                    }

                    string absoluteFolder = Path.Combine(Application.dataPath,
                        OutputFolder.Substring("Assets/".Length).Replace('/', Path.DirectorySeparatorChar));
                    Directory.CreateDirectory(absoluteFolder);
                    string assetPath = OutputFolder + "/" + cue.FileName + ".mp3";
                    string absolutePath = Path.Combine(Directory.GetParent(Application.dataPath).FullName,
                        assetPath.Replace('/', Path.DirectorySeparatorChar));
                    File.WriteAllBytes(absolutePath, bytes);
                    AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);

                    AudioImporter importer = AssetImporter.GetAtPath(assetPath) as AudioImporter;
                    if (importer != null)
                    {
                        importer.forceToMono = true;
                        importer.loadInBackground = false;
                        AudioImporterSampleSettings settings = importer.defaultSampleSettings;
                        settings.preloadAudioData = true;
                        settings.loadType = AudioClipLoadType.DecompressOnLoad;
                        settings.compressionFormat = AudioCompressionFormat.Vorbis;
                        settings.quality = 1f;
                        importer.defaultSampleSettings = settings;
                        importer.SaveAndReimport();
                    }

                    AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(assetPath);
                    if (clip == null)
                    {
                        Debug.LogError("[ElevenLabs] Unity could not import the returned audio as an AudioClip: " + assetPath);
                        completed(null);
                        return;
                    }

                    SerializedObject serialized = new SerializedObject(controller);
                    SerializedProperty slot = serialized.FindProperty(cue.ClipProperty);
                    if (slot == null)
                    {
                        Debug.LogError("[ElevenLabs] Scene audio slot is missing: " + cue.ClipProperty);
                        completed(null);
                        return;
                    }
                    slot.objectReferenceValue = clip;
                    serialized.ApplyModifiedProperties();
                    EditorUtility.SetDirty(controller);
                    EditorSceneManager.MarkSceneDirty(controller.gameObject.scene);
                    AssetDatabase.SaveAssets();
                    PlayPreview(clip);
                    status = cue.Label + " generated and assigned.";
                    Repaint();
                    completed(clip);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                    completed(null);
                }
                finally
                {
                    EditorUtility.ClearProgressBar();
                    request.Dispose();
                    if (ReferenceEquals(activeRequest, request))
                        activeRequest = null;
                }
            };
        }

        private static IngenuityFlashbackSceneController FindController()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded) return null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                IngenuityFlashbackSceneController controller =
                    root.GetComponentInChildren<IngenuityFlashbackSceneController>(true);
                if (controller != null) return controller;
            }
            return null;
        }

        private static void PlayPreview(AudioClip clip)
        {
            if (clip == null || !ElevenLabsFeatureSettings.IsEnabled) return;
            Type audioUtil = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType("UnityEditor.AudioUtil", false))
                .FirstOrDefault(type => type != null);
            MethodInfo play = audioUtil?.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .FirstOrDefault(method =>
                {
                    if (method.Name != "PlayPreviewClip") return false;
                    ParameterInfo[] parameters = method.GetParameters();
                    return parameters.Length >= 3 && parameters[0].ParameterType == typeof(AudioClip) &&
                        parameters[1].ParameterType == typeof(int) && parameters[2].ParameterType == typeof(bool);
                });
            if (play == null)
            {
                Debug.LogWarning("[ElevenLabs] Unity's Editor audio preview API was not found. Select the imported clip and use its Inspector preview.");
                return;
            }

            try
            {
                ParameterInfo[] parameters = play.GetParameters();
                object[] arguments = new object[parameters.Length];
                arguments[0] = clip;
                arguments[1] = 0;
                arguments[2] = false;
                for (int i = 3; i < arguments.Length; i++)
                    arguments[i] = parameters[i].HasDefaultValue ? parameters[i].DefaultValue :
                        (parameters[i].ParameterType.IsValueType ? Activator.CreateInstance(parameters[i].ParameterType) : null);
                play.Invoke(null, arguments);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[ElevenLabs] Could not preview the clip: " + exception.GetBaseException().Message);
            }
        }

        [Serializable]
        private sealed class SpeechRequest
        {
            public string text;
            public string model_id;
            public VoiceSettings voice_settings;
        }

        [Serializable]
        private sealed class VoiceSettings
        {
            public float stability;
            public float similarity_boost;
        }

        private sealed class Cue
        {
            public readonly string Label;
            public readonly string TextProperty;
            public readonly string ClipProperty;
            public readonly string FileName;

            public Cue(string label, string textProperty, string clipProperty, string fileName)
            {
                Label = label;
                TextProperty = textProperty;
                ClipProperty = clipProperty;
                FileName = fileName;
            }
        }
    }
}
#endif
