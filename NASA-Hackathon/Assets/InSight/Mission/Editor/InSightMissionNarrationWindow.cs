#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using LastSignal.InSightFlashback;
using LastSignal.InSightMission;
using NasaHack;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

namespace LastSignal.InSightMission.EditorTools
{
    /// <summary>Generates offline Brian clips for InSight only; the key remains in window memory.</summary>
    public sealed class InSightMissionNarrationWindow : EditorWindow
    {
        const string FlashbackScene = "Assets/InSight/NASA-Headquarters/LastSignalFlashback/Scenes/InSight-Flashback.unity";
        const string LandingScene = "Assets/InSight/Mission/Scenes/InSight-Landing.unity";
        const string ArrivalScene = "Assets/InSight/Mission/Scenes/InSight-Arrival.unity";
        const string LegacyScene = "Assets/InSight/Mission/Scenes/InSight-Legacy.unity";
        const string FlashbackAudioFolder = "Assets/InSight/NASA-Headquarters/LastSignalFlashback/Audio/ElevenLabsBrian";
        const string MissionAudioFolder = "Assets/InSight/Audio/ElevenLabsBrian";

        static readonly string[] ScenePaths = { FlashbackScene, LandingScene, ArrivalScene, LegacyScene };
        static readonly string[] HqLabels = { "Facility introduction", "Solar arrays", "SEIS seismometer", "Robotic arm", "Systems check" };
        static readonly string[] HqTextFields = { "introNarrationText", "solarPanelNarrationText", "cameraMastNarrationText", "instrumentArmNarrationText", "systemsCheckNarrationText" };
        static readonly string[] HqClipFields = { "introNarration", "solarPanelNarration", "cameraMastNarration", "instrumentArmNarration", "systemsCheckNarration" };
        static readonly string[] HqFileNames = { "01_Introduction", "02_Solar_Arrays", "03_SEIS", "04_Robotic_Arm", "05_Systems_Check" };

        sealed class ClipJob
        {
            public Scene scene;
            public UnityEngine.Object controller;
            public bool isFlashback;
            public int cueIndex;
            public string text;
            public string clipPropertyPath;
            public string outputAssetPath;
            public string label;
        }

        string apiKey = string.Empty;
        string status = "Enter your ElevenLabs secret key, then generate missing InSight clips.";
        string originalScenePath;
        readonly List<Scene> openedScenes = new List<Scene>();
        Queue<ClipJob> jobs;
        UnityWebRequest activeRequest;
        bool busy;
        Vector2 scroll;

        [MenuItem("Last Signal/InSight/Narration/Generate Brian Clips for All InSight Scenes")]
        public static void Open()
        {
            InSightMissionNarrationWindow window = GetWindow<InSightMissionNarrationWindow>("InSight Narration");
            window.minSize = new Vector2(520f, 250f);
            window.Show();
        }

        void OnGUI()
        {
            EditorGUILayout.LabelField("ElevenLabs Brian — InSight", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Voice ID: " + InSightMissionSceneController.BrianVoiceId, EditorStyles.miniLabel);
            EditorGUILayout.HelpBox("Generates only clips with empty scene slots. The secret key is kept in this window's memory and is not saved to the project or Editor preferences.", MessageType.Info);
            if (!ElevenLabsFeatureSettings.IsEnabled)
                EditorGUILayout.HelpBox("ElevenLabs is temporarily disabled. Captions remain available and no clips will be generated until it is re-enabled from Last Signal > ElevenLabs.", MessageType.Warning);
            apiKey = EditorGUILayout.PasswordField("ElevenLabs secret key", apiKey);

            using (new EditorGUILayout.HorizontalScope())
            {
                GUI.enabled = !busy && ElevenLabsFeatureSettings.IsEnabled;
                if (GUILayout.Button("Generate missing InSight clips", GUILayout.Height(32f))) BeginGenerate();
                GUI.enabled = busy;
                if (GUILayout.Button("Cancel", GUILayout.Width(92f), GUILayout.Height(32f))) CancelGeneration();
                GUI.enabled = true;
            }

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField(status, EditorStyles.wordWrappedLabel);
            EditorGUILayout.Space(6f);
            scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.MinHeight(72f));
            EditorGUILayout.LabelField("Coverage", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Headquarters assembly • landing sequence • SEIS, HP³ and RISE science tour • mission legacy. Existing InSight launch narration remains assigned in the launch scene.", EditorStyles.wordWrappedLabel);
            EditorGUILayout.EndScrollView();
        }

        void BeginGenerate()
        {
            if (!ElevenLabsFeatureSettings.IsEnabled)
            {
                status = "ElevenLabs is temporarily disabled. No request was sent.";
                Repaint();
                return;
            }

            // Copying a key from the dashboard often includes a trailing line break; HTTP
            // headers reject those characters, so normalize it before sending the request.
            apiKey = NormalizeHeaderValue(apiKey);
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                status = "Paste an ElevenLabs secret key into the password field. It will not be stored.";
                Repaint();
                return;
            }
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                status = "Stop Play mode before generating clips.";
                Repaint();
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            originalScenePath = SceneManager.GetActiveScene().path;
            openedScenes.Clear();
            jobs = new Queue<ClipJob>();
            try
            {
                foreach (string path in ScenePaths)
                {
                    if (!File.Exists(Path.GetFullPath(path)))
                    {
                        if (path != FlashbackScene) throw new FileNotFoundException("Build InSight mission scenes first.", path);
                        throw new FileNotFoundException("InSight flashback scene is missing.", path);
                    }

                    Scene scene = FindLoadedScene(path);
                    if (!scene.IsValid())
                    {
                        scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                        openedScenes.Add(scene);
                    }
                    CollectJobs(scene);
                }
            }
            catch (Exception exception)
            {
                CloseOpenedScenes();
                Debug.LogException(exception);
                status = exception.Message;
                apiKey = string.Empty;
                Repaint();
                return;
            }

            if (jobs.Count == 0)
            {
                CloseOpenedScenes();
                apiKey = string.Empty;
                status = "All InSight narration slots already contain audio clips.";
                Repaint();
                return;
            }

            busy = true;
            status = jobs.Count + " missing Brian clips queued.";
            GenerateNext();
        }

        void CollectJobs(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                InSightFlashbackSceneController flashback = root.GetComponentInChildren<InSightFlashbackSceneController>(true);
                if (flashback != null)
                {
                    var serialized = new SerializedObject(flashback);
                    for (int i = 0; i < HqLabels.Length; i++)
                    {
                        SerializedProperty text = serialized.FindProperty(HqTextFields[i]);
                        SerializedProperty clip = serialized.FindProperty(HqClipFields[i]);
                        AddJob(scene, flashback, true, i, text, clip, HqLabels[i], FlashbackAudioFolder + "/" + HqFileNames[i] + ".mp3");
                    }
                }

                InSightMissionSceneController mission = root.GetComponentInChildren<InSightMissionSceneController>(true);
                if (mission == null) continue;
                var missionSerialized = new SerializedObject(mission);
                SerializedProperty cues = missionSerialized.FindProperty("voiceCues");
                if (cues == null || !cues.isArray) continue;
                for (int i = 0; i < cues.arraySize; i++)
                {
                    SerializedProperty cue = cues.GetArrayElementAtIndex(i);
                    SerializedProperty caption = cue.FindPropertyRelative("caption");
                    SerializedProperty clip = cue.FindPropertyRelative("audioClip");
                    SerializedProperty id = cue.FindPropertyRelative("id");
                    string cueId = id != null ? id.stringValue : (i + 1).ToString("00");
                    string label = scene.name + " / " + cueId;
                    string fileName = SafeFileName(scene.name + "_" + cueId);
                    AddJob(scene, mission, false, i, caption, clip, label, MissionAudioFolder + "/" + fileName + ".mp3");
                }
            }
        }

        void AddJob(Scene scene, UnityEngine.Object controller, bool isFlashback, int index,
            SerializedProperty textProperty, SerializedProperty clipProperty, string label, string assetPath)
        {
            if (textProperty == null || clipProperty == null) return;
            if (clipProperty.objectReferenceValue != null) return;

            AudioClip existing = AssetDatabase.LoadAssetAtPath<AudioClip>(assetPath);
            if (existing != null)
            {
                clipProperty.objectReferenceValue = existing;
                clipProperty.serializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(controller);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                return;
            }

            string text = textProperty.stringValue;
            if (string.IsNullOrWhiteSpace(text)) return;
            string clipPropertyPath = isFlashback ? HqClipFields[index] : "voiceCues.Array.data[" + index + "].audioClip";
            jobs.Enqueue(new ClipJob
            {
                scene = scene,
                controller = controller,
                isFlashback = isFlashback,
                cueIndex = index,
                text = text.Trim(),
                clipPropertyPath = clipPropertyPath,
                outputAssetPath = assetPath,
                label = label
            });
        }

        void GenerateNext()
        {
            if (!ElevenLabsFeatureSettings.IsEnabled)
            {
                CancelGeneration();
                status = "ElevenLabs was disabled. Any completed clips were kept; no further requests were sent.";
                return;
            }

            if (jobs == null || jobs.Count == 0)
            {
                busy = false;
                apiKey = string.Empty;
                status = "All missing InSight Brian clips were generated and assigned to their scenes.";
                CloseOpenedScenes();
                EditorUtility.ClearProgressBar();
                Repaint();
                return;
            }

            ClipJob job = jobs.Dequeue();
            status = "Generating " + job.label + " (" + (openedScenes.Count > 0 ? "see progress bar" : "").Trim() + ")…";
            Repaint();

            var payload = new SpeechRequest
            {
                text = job.text,
                model_id = "eleven_multilingual_v2",
                voice_settings = new VoiceSettings { stability = 0.5f, similarity_boost = 0.75f }
            };
            string url = "https://api.elevenlabs.io/v1/text-to-speech/" + InSightMissionSceneController.BrianVoiceId + "?output_format=mp3_44100_128";
            UnityWebRequest request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST)
            {
                uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(JsonUtility.ToJson(payload))),
                downloadHandler = new DownloadHandlerBuffer(),
                timeout = 120
            };
            activeRequest = request;
            try
            {
                request.SetRequestHeader("xi-api-key", apiKey);
                request.SetRequestHeader("Content-Type", "application/json");
                request.SetRequestHeader("Accept", "audio/mpeg");
            }
            catch (Exception exception)
            {
                request.Dispose();
                activeRequest = null;
                Debug.LogException(exception);
                status = "The pasted key contained unsupported header characters. Paste the secret key again without labels.";
                FinishFailedBatch();
                return;
            }
            EditorUtility.DisplayProgressBar("ElevenLabs Brian", "Generating " + job.label + "…", 0f);

            request.SendWebRequest().completed += _ =>
            {
                try
                {
                    if (request.result != UnityWebRequest.Result.Success)
                    {
                        string detail = request.downloadHandler != null ? request.downloadHandler.text : request.error;
                        if (request.responseCode == 401)
                        {
                            Debug.LogWarning("[InSight ElevenLabs] ElevenLabs rejected the provided key (HTTP 401). No audio was generated. The key may be an ID rather than its secret value.");
                            status = "ElevenLabs rejected the key (HTTP 401 Invalid API Key). No audio was generated. Paste the secret API key value, not the key ID.";
                        }
                        else if (request.responseCode == 404)
                        {
                            Debug.LogWarning("[InSight ElevenLabs] Brian voice was not found or is unavailable to this key (HTTP 404). Requested voice ID: " + InSightMissionSceneController.BrianVoiceId + ".");
                            status = "ElevenLabs could not find the Brian voice ID " + InSightMissionSceneController.BrianVoiceId + " (HTTP 404). Check the ID or voice access. No audio was generated for this batch.";
                        }
                        else
                        {
                            Debug.LogWarning("[InSight ElevenLabs] " + job.label + " failed (HTTP " + request.responseCode + "): " + detail);
                            status = "Generation stopped at " + job.label + " (HTTP " + request.responseCode + "). No audio was generated for this batch; previously assigned clips remain saved.";
                        }
                        FinishFailedBatch();
                        return;
                    }

                    byte[] audio = request.downloadHandler.data;
                    if (audio == null || audio.Length < 128)
                    {
                        status = "ElevenLabs returned an empty clip for " + job.label + ".";
                        FinishFailedBatch();
                        return;
                    }

                    string absolutePath = Path.Combine(Directory.GetParent(Application.dataPath).FullName,
                        job.outputAssetPath.Replace('/', Path.DirectorySeparatorChar));
                    Directory.CreateDirectory(Path.GetDirectoryName(absolutePath));
                    File.WriteAllBytes(absolutePath, audio);
                    AssetDatabase.ImportAsset(job.outputAssetPath, ImportAssetOptions.ForceSynchronousImport);
                    AudioImporter importer = AssetImporter.GetAtPath(job.outputAssetPath) as AudioImporter;
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

                    AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(job.outputAssetPath);
                    if (clip == null) throw new InvalidDataException("Unity could not import the ElevenLabs response: " + job.outputAssetPath);
                    SerializedObject serialized = new SerializedObject(job.controller);
                    SerializedProperty slot = serialized.FindProperty(job.clipPropertyPath);
                    if (slot == null) throw new InvalidOperationException("Could not find audio slot " + job.clipPropertyPath + " on " + job.label);
                    slot.objectReferenceValue = clip;
                    serialized.ApplyModifiedProperties();
                    EditorUtility.SetDirty(job.controller);
                    EditorSceneManager.MarkSceneDirty(job.scene);
                    EditorSceneManager.SaveScene(job.scene);
                    AssetDatabase.SaveAssets();
                    GenerateNext();
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                    status = "Could not import or assign " + job.label + ". See Console.";
                    FinishFailedBatch();
                }
                finally
                {
                    EditorUtility.ClearProgressBar();
                    request.Dispose();
                    if (ReferenceEquals(activeRequest, request)) activeRequest = null;
                }
            };
        }

        void FinishFailedBatch()
        {
            busy = false;
            apiKey = string.Empty;
            jobs?.Clear();
            CloseOpenedScenes();
            Repaint();
        }

        void CancelGeneration()
        {
            if (activeRequest != null) activeRequest.Abort();
            busy = false;
            apiKey = string.Empty;
            jobs?.Clear();
            CloseOpenedScenes();
            EditorUtility.ClearProgressBar();
            status = "Generation canceled. Successfully assigned clips remain saved.";
            Repaint();
        }

        void CloseOpenedScenes()
        {
            foreach (Scene scene in openedScenes.ToArray())
                if (scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
            openedScenes.Clear();
            if (!string.IsNullOrWhiteSpace(originalScenePath))
            {
                Scene active = FindLoadedScene(originalScenePath);
                if (active.IsValid()) SceneManager.SetActiveScene(active);
            }
        }

        static Scene FindLoadedScene(string path)
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene.isLoaded && scene.path == path) return scene;
            }
            return default;
        }

        static string SafeFileName(string value)
        {
            foreach (char invalid in Path.GetInvalidFileNameChars()) value = value.Replace(invalid, '_');
            return value.Replace(' ', '_').Replace('/', '_').Replace('-', '_');
        }

        static string NormalizeHeaderValue(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            var clean = new StringBuilder(value.Length);
            foreach (char character in value)
                if (character >= 0x21 && character <= 0x7e) clean.Append(character);
            return clean.ToString();
        }

        void OnDisable()
        {
            if (activeRequest != null) activeRequest.Abort();
            if (busy) FinishFailedBatch();
        }

        [Serializable] sealed class SpeechRequest { public string text; public string model_id; public VoiceSettings voice_settings; }
        [Serializable] sealed class VoiceSettings { public float stability; public float similarity_boost; }
    }
}
#endif
