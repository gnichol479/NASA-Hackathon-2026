#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace NasaHack.EditorTools
{
    /// <summary>
    /// Generates offline ElevenLabs clips for authored scene narration and assigns them to the
    /// relevant scene fields. The API key is held in memory only and is never written to assets.
    /// </summary>
    public sealed partial class SceneNarrationGeneratorWindow : EditorWindow
    {
        const string VoiceId = "nPczCjzI2devNBz1zQrb";
        const string OutputRoot = "Assets/Shared/Audio/Narration";

        string apiKey = string.Empty;
        string activeKey = string.Empty;
        string status = "Enter an ElevenLabs API key, then generate missing narration for all project scenes.";
        string originalScenePath = string.Empty;
        string currentScenePath = string.Empty;
        readonly List<string> scenePaths = new List<string>();
        readonly Queue<Cue> cueQueue = new Queue<Cue>();
        UnityWebRequest activeRequest;
        bool busy;
        int sceneIndex;
        int totalGenerated;
        int totalSkipped;
        int totalFailed;
        int maxCharactersPerRun = 15000;
        bool includeAssignedCues;
        int charactersThisRun;
        int sceneGenerated;
        int sceneTotal;

        [MenuItem("Last Signal/Narration/ElevenLabs Brian - All Scenes")]
        public static void Open()
        {
            SceneNarrationGeneratorWindow window = GetWindow<SceneNarrationGeneratorWindow>("Brian Narration");
            window.minSize = new Vector2(500f, 360f);
        }

        void OnDisable()
        {
            bool wasBusy = busy;
            busy = false;
            EditorUtility.ClearProgressBar();
            if (activeRequest != null && !activeRequest.isDone) activeRequest.Abort();
            activeRequest?.Dispose();
            activeRequest = null;
            if (wasBusy) SaveCurrentAndRestoreOriginalScene();
            apiKey = string.Empty;
            activeKey = string.Empty;
        }

        void OnGUI()
        {
            EditorGUILayout.LabelField("ElevenLabs · Brian", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Voice ID: " + VoiceId, EditorStyles.miniLabel);
            EditorGUILayout.HelpBox(
                "Creates offline, assignable voice clips for the authored story panels, mission cards, rover hotspots, landing discoveries, and launch milestones. The API key is kept only in this window's memory. Each generated caption is sent to ElevenLabs; no live API call is made in the game.",
                MessageType.Info);

            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorGUILayout.HelpBox("Exit Play mode before generating or assigning narration.", MessageType.Warning);
                return;
            }

            using (new EditorGUI.DisabledScope(busy))
            {
                apiKey = EditorGUILayout.PasswordField("ElevenLabs API key", apiKey);
                maxCharactersPerRun = EditorGUILayout.IntField("Character budget this run", maxCharactersPerRun);
                maxCharactersPerRun = Mathf.Clamp(maxCharactersPerRun, 100, 100000);
                EditorGUILayout.HelpBox("Set this budget within your available ElevenLabs credits and key limit. Assigned clips are skipped. If a run stops, completed clips stay assigned and the next run resumes the missing lines.", MessageType.None);
                EditorGUILayout.Space(8);
                using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(apiKey)))
                {
                    if (GUILayout.Button("Generate missing narration for all scenes", GUILayout.Height(34)))
                        BeginBatch();
                }
            }

            if (busy && GUILayout.Button("Cancel batch now"))
            {
                StopBatch("Cancelled. Any clips generated so far were kept and assigned.");
                return;
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.HelpBox(status, busy ? MessageType.Info : MessageType.None);
        }

        void BeginBatch()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            originalScenePath = SceneManager.GetActiveScene().path;
            scenePaths.Clear();
            List<string> foundScenes = AssetDatabase.FindAssets("t:Scene", new[] { "Assets" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
                .Where(path => !path.Contains("/Samples/") && !path.Contains("/DemoScene.unity"))
                .ToList();
            const string opportunityLaunchScene = "Assets/Opportunity/Launch/Scenes/Opportunity-Launch.unity";
            if (foundScenes.Contains(opportunityLaunchScene)) scenePaths.Add(opportunityLaunchScene);
            scenePaths.AddRange(EditorBuildSettings.scenes
                .Where(scene => scene.enabled && scene.path != opportunityLaunchScene && foundScenes.Contains(scene.path))
                .Select(scene => scene.path));
            scenePaths.AddRange(foundScenes.Except(scenePaths)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase));

            if (scenePaths.Count == 0)
            {
                status = "No project scenes were found.";
                Repaint();
                return;
            }

            activeKey = apiKey;
            busy = true;
            sceneIndex = 0;
            totalGenerated = totalSkipped = totalFailed = 0;
            charactersThisRun = 0;
            status = "Preparing " + scenePaths.Count + " scenes…";
            OpenNextScene();
        }

        void OpenNextScene()
        {
            if (!busy) return;
            cueQueue.Clear();
            if (sceneIndex >= scenePaths.Count)
            {
                FinishBatch();
                return;
            }

            currentScenePath = scenePaths[sceneIndex];
            status = "Scanning " + Path.GetFileNameWithoutExtension(currentScenePath) + " (" + (sceneIndex + 1) + " of " + scenePaths.Count + ")…";
            Repaint();

            Scene scene;
            try
            {
                scene = EditorSceneManager.OpenScene(currentScenePath, OpenSceneMode.Single);
                PrepareTitleCards(scene);
                CollectSceneCues(scene);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                totalFailed++;
                sceneIndex++;
                OpenNextScene();
                return;
            }

            sceneTotal = cueQueue.Count;
            sceneGenerated = 0;
            if (scene.isDirty) EditorSceneManager.SaveScene(scene);
            GenerateNextCue();
        }

        void PrepareTitleCards(Scene scene)
        {
            if (scene.name == "TitleScreen-Night")
            {
                Transform tagline = FindTransform(scene, "Title/Tagline");
                if (tagline != null)
                {
                    SceneIntroNarrator intro = tagline.GetComponent<SceneIntroNarrator>();
                    if (intro == null) intro = Undo.AddComponent<SceneIntroNarrator>(tagline.gameObject);
                    intro.narrationText = "Abandoned, but not forgotten. A VR field trip to the machines NASA left behind. Choose a mission to begin.";
                    EditorUtility.SetDirty(intro);
                    EditorSceneManager.MarkSceneDirty(scene);
                }
            }

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Button button in root.GetComponentsInChildren<Button>(true))
                {
                    Transform card = button.transform.parent;
                    while (card != null && !card.name.EndsWith(" Card", StringComparison.Ordinal))
                        card = card.parent;
                    if (card == null || !card.name.EndsWith(" Card", StringComparison.Ordinal)) continue;

                    TMP_Text name = FindNamedText(card, "Name");
                    TMP_Text kind = FindNamedText(card, "Kind");
                    TMP_Text blurb = FindNamedText(card, "Blurb");
                    TMP_Text buttonLabel = button.GetComponentInChildren<TMP_Text>(true);
                    if (name == null && blurb == null) continue;

                    TitleCardNarrator narrator = button.GetComponent<TitleCardNarrator>();
                    if (narrator == null) narrator = Undo.AddComponent<TitleCardNarrator>(button.gameObject);
                    narrator.narrationText = JoinNonEmpty(name != null ? name.text : card.name,
                        kind != null ? kind.text : string.Empty,
                        blurb != null ? blurb.text : string.Empty,
                        buttonLabel != null ? buttonLabel.text : string.Empty);
                    EditorUtility.SetDirty(narrator);
                    EditorSceneManager.MarkSceneDirty(scene);
                }
            }
        }

        static TMP_Text FindNamedText(Transform root, string childName)
        {
            Transform child = root.Find(childName);
            return child != null ? child.GetComponent<TMP_Text>() : null;
        }

        static Transform FindTransform(Scene scene, string path)
        {
            string[] parts = path.Split('/');
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name != parts[0]) continue;
                Transform current = root.transform;
                for (int i = 1; i < parts.Length && current != null; i++)
                    current = current.Find(parts[i]);
                return current;
            }
            return null;
        }

        void CollectSceneCues(Scene scene)
        {
            string sceneName = Path.GetFileNameWithoutExtension(scene.path);
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (NarrationPanel panel in root.GetComponentsInChildren<NarrationPanel>(true))
                {
                    if (panel.beats == null) continue;
                    for (int i = 0; i < panel.beats.Length; i++)
                    {
                        NarrationPanel.Beat beat = panel.beats[i];
                        if (beat == null) continue;
                        AddCue(panel, "beats.Array.data[" + i + "].voice",
                            JoinNonEmpty(beat.title, beat.text), sceneName,
                            GetTransformPath(panel.transform) + "_Beat_" + (i + 1) + "_" + beat.title);
                    }
                }

                foreach (InfoHotspot hotspot in root.GetComponentsInChildren<InfoHotspot>(true))
                    AddCue(hotspot, "narrationClip", JoinNonEmpty(hotspot.title, hotspot.body), sceneName,
                        GetTransformPath(hotspot.transform) + "_" + hotspot.title);

                foreach (TitleCardNarrator card in root.GetComponentsInChildren<TitleCardNarrator>(true))
                    AddCue(card, "narrationClip", card.narrationText, sceneName,
                        GetTransformPath(card.transform) + "_MissionCard");

                foreach (SceneIntroNarrator intro in root.GetComponentsInChildren<SceneIntroNarrator>(true))
                    AddCue(intro, "narrationClip", intro.narrationText, sceneName,
                        GetTransformPath(intro.transform) + "_Scene_Introduction");

                foreach (DeltaIILaunch launch in root.GetComponentsInChildren<DeltaIILaunch>(true))
                {
                    launch.EnsureNarrationText();
                    EditorUtility.SetDirty(launch);
                    EditorSceneManager.MarkSceneDirty(scene);
                    AddCue(launch, "briefingNarration", launch.briefingNarrationText, sceneName,
                        GetTransformPath(launch.transform) + "_Briefing");
                    AddCue(launch, "countdownNarration", launch.countdownNarrationText, sceneName,
                        GetTransformPath(launch.transform) + "_Countdown");
                    AddCue(launch, "internalPowerNarration", launch.internalPowerNarrationText, sceneName,
                        GetTransformPath(launch.transform) + "_Internal_Power");
                    AddCue(launch, "ignitionNarration", launch.ignitionNarrationText, sceneName,
                        GetTransformPath(launch.transform) + "_Ignition");
                    AddCue(launch, "liftoffNarration", launch.liftoffNarrationText, sceneName,
                        GetTransformPath(launch.transform) + "_Liftoff");
                    AddCue(launch, "pitchNarration", launch.pitchNarrationText, sceneName,
                        GetTransformPath(launch.transform) + "_Pitch");
                    AddCue(launch, "stagingNarration", launch.stagingNarrationText, sceneName,
                        GetTransformPath(launch.transform) + "_Staging");
                    AddCue(launch, "cruiseNarration", launch.cruiseNarrationText, sceneName,
                        GetTransformPath(launch.transform) + "_Cruise");
                }

                foreach (LandingSequenceController landing in root.GetComponentsInChildren<LandingSequenceController>(true))
                {
                    AddCue(landing, "landingIntroductionNarration", landing.landingIntroductionText, sceneName,
                        GetTransformPath(landing.transform) + "_Landing_Introduction");
                    if (landing.sampleNarrationText != null)
                    {
                        for (int i = 0; i < landing.sampleNarrationText.Length; i++)
                            AddCue(landing, "sampleNarrationClips.Array.data[" + i + "]", landing.sampleNarrationText[i], sceneName,
                                GetTransformPath(landing.transform) + "_Sample_" + (i + 1));
                    }
                    AddCue(landing, "incorrectConclusionNarration", landing.incorrectConclusionText, sceneName,
                        GetTransformPath(landing.transform) + "_Incorrect_Conclusion");
                    AddCue(landing, "conclusionNarration", landing.conclusionNarrationText, sceneName,
                        GetTransformPath(landing.transform) + "_Conclusion");
                }

                foreach (Component component in root.GetComponentsInChildren<Component>(true))
                {
                    if (component == null || component.GetType().Name != "OpportunityFlashbackSceneController") continue;
                    CollectFlashbackCues(component, sceneName);
                }
            }
        }

        void CollectFlashbackCues(Component controller, string sceneName)
        {
            string path = GetTransformPath(controller.transform);
            AddCue(controller, "introNarration", ReadString(controller, "introNarrationText"), sceneName, path + "_Briefing");
            AddCue(controller, "solarPanelNarration", ReadString(controller, "solarPanelNarrationText"), sceneName, path + "_Solar_Panels");
            AddCue(controller, "cameraMastNarration", ReadString(controller, "cameraMastNarrationText"), sceneName, path + "_Camera_Mast");
            AddCue(controller, "instrumentArmNarration", ReadString(controller, "instrumentArmNarrationText"), sceneName, path + "_Instrument_Arm");
            AddCue(controller, "systemsCheckNarration", ReadString(controller, "systemsCheckNarrationText"), sceneName, path + "_Systems_Check");
        }

        static string ReadString(Component component, string propertyPath)
        {
            var serialized = new SerializedObject(component);
            SerializedProperty property = serialized.FindProperty(propertyPath);
            return property != null && property.propertyType == SerializedPropertyType.String ? property.stringValue : string.Empty;
        }

        void AddCue(Component owner, string audioProperty, string text, string sceneName, string fileSlug)
        {
            if (owner == null || string.IsNullOrWhiteSpace(text)) return;
            var serialized = new SerializedObject(owner);
            SerializedProperty clip = serialized.FindProperty(audioProperty);
            if (clip == null || clip.propertyType != SerializedPropertyType.ObjectReference)
            {
                Debug.LogWarning("[Narration] Missing AudioClip slot '" + audioProperty + "' on " + owner.name, owner);
                return;
            }
            if (clip.objectReferenceValue != null && !includeAssignedCues)
            {
                totalSkipped++;
                return;
            }

            cueQueue.Enqueue(new Cue
            {
                Owner = owner,
                AudioProperty = audioProperty,
                Text = Regex.Replace(Regex.Replace(text, "<[^>]+>", string.Empty), @"\s+", " ").Trim(),
                AssetPath = OutputRoot + "/" + sceneName + "/" + SafeName(fileSlug) + ".mp3",
                Label = sceneName + " / " + owner.name + " / " + fileSlug
            });
        }

        void GenerateNextCue()
        {
            if (!busy) return;
            if (cueQueue.Count == 0)
            {
                Scene scene = SceneManager.GetActiveScene();
                if (scene.IsValid() && scene.isDirty) EditorSceneManager.SaveScene(scene);
                sceneIndex++;
                OpenNextScene();
                return;
            }

            Cue cue = cueQueue.Peek();
            if (charactersThisRun + cue.Text.Length > maxCharactersPerRun)
            {
                Scene scene = SceneManager.GetActiveScene();
                if (scene.IsValid() && scene.isDirty) EditorSceneManager.SaveScene(scene);
                FinishAtCharacterBudget();
                return;
            }

            cueQueue.Dequeue();
            charactersThisRun += cue.Text.Length;
            sceneGenerated++;
            status = "Generating " + cue.Label + " (" + sceneGenerated + " of " + sceneTotal + " in this scene)…";
            Repaint();
            Generate(cue, completed =>
            {
                if (completed) totalGenerated++;
                else totalFailed++;
                GenerateNextCue();
            });
        }

        void Generate(Cue cue, Action<bool> completed)
        {
            string folder = Path.GetDirectoryName(cue.AssetPath)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(folder))
            {
                Debug.LogError("[Narration] Could not determine an output folder for " + cue.Label);
                completed(false);
                return;
            }

            string absoluteFolder = Path.Combine(Directory.GetParent(Application.dataPath).FullName,
                folder.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(absoluteFolder);

            var payload = new SpeechRequest
            {
                text = cue.Text,
                model_id = "eleven_multilingual_v2",
                voice_settings = new VoiceSettings { stability = 0.5f, similarity_boost = 0.75f }
            };
            activeRequest = new UnityWebRequest(
                "https://api.elevenlabs.io/v1/text-to-speech/" + VoiceId + "?output_format=mp3_44100_128",
                UnityWebRequest.kHttpVerbPOST)
            {
                uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(JsonUtility.ToJson(payload))),
                downloadHandler = new DownloadHandlerBuffer(),
                timeout = 120
            };
            activeRequest.SetRequestHeader("xi-api-key", activeKey);
            activeRequest.SetRequestHeader("Content-Type", "application/json");
            activeRequest.SetRequestHeader("Accept", "audio/mpeg");

            UnityWebRequest request = activeRequest;
            EditorUtility.DisplayProgressBar("ElevenLabs Brian", cue.Label, 0f);
            request.SendWebRequest().completed += _ =>
            {
                try
                {
                    if (request.result != UnityWebRequest.Result.Success)
                    {
                        string details = request.downloadHandler != null ? request.downloadHandler.text : request.error;
                        if (details == null) details = string.Empty;
                        Debug.LogError("[ElevenLabs] " + cue.Label + " failed (HTTP " + request.responseCode + "): " + details);
                        if (request.responseCode == 401 || request.responseCode == 402 ||
                            request.responseCode == 403 || request.responseCode == 429 ||
                            details.IndexOf("insufficient", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            details.IndexOf("quota", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            totalFailed++;
                            StopBatch("ElevenLabs stopped the batch (HTTP " + request.responseCode + "). Generated clips so far were saved. Check the key or wait for its credit limit to refresh, then run again to continue.");
                            return;
                        }
                        completed(false);
                        return;
                    }

                    byte[] bytes = request.downloadHandler.data;
                    if (bytes == null || bytes.Length < 128)
                    {
                        Debug.LogError("[ElevenLabs] Empty audio response for " + cue.Label);
                        completed(false);
                        return;
                    }

                    string absolutePath = Path.Combine(Directory.GetParent(Application.dataPath).FullName,
                        cue.AssetPath.Replace('/', Path.DirectorySeparatorChar));
                    File.WriteAllBytes(absolutePath, bytes);
                    AssetDatabase.ImportAsset(cue.AssetPath, ImportAssetOptions.ForceSynchronousImport);

                    AudioImporter importer = AssetImporter.GetAtPath(cue.AssetPath) as AudioImporter;
                    if (importer != null)
                    {
                        importer.forceToMono = true;
                        AudioImporterSampleSettings settings = importer.defaultSampleSettings;
                        settings.loadType = AudioClipLoadType.DecompressOnLoad;
                        settings.compressionFormat = AudioCompressionFormat.Vorbis;
                        settings.quality = 1f;
                        importer.defaultSampleSettings = settings;
                        importer.SaveAndReimport();
                    }

                    AudioClip clipAsset = AssetDatabase.LoadAssetAtPath<AudioClip>(cue.AssetPath);
                    if (clipAsset == null)
                    {
                        Debug.LogError("[Narration] Unity could not import " + cue.AssetPath + " as an AudioClip.");
                        completed(false);
                        return;
                    }

                    SerializedObject serialized = new SerializedObject(cue.Owner);
                    SerializedProperty slot = serialized.FindProperty(cue.AudioProperty);
                    if (slot == null)
                    {
                        Debug.LogError("[Narration] AudioClip slot disappeared: " + cue.AudioProperty, cue.Owner);
                        completed(false);
                        return;
                    }
                    slot.objectReferenceValue = clipAsset;
                    serialized.ApplyModifiedProperties();
                    EditorUtility.SetDirty(cue.Owner);
                    EditorSceneManager.MarkSceneDirty(cue.Owner.gameObject.scene);
                    AssetDatabase.SaveAssets();
                    completed(true);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                    completed(false);
                }
                finally
                {
                    EditorUtility.ClearProgressBar();
                    request.Dispose();
                    if (ReferenceEquals(activeRequest, request)) activeRequest = null;
                    Repaint();
                }
            };
        }

        void FinishBatch()
        {
            busy = false;
            apiKey = string.Empty;
            activeKey = string.Empty;
            EditorUtility.ClearProgressBar();

            if (!string.IsNullOrEmpty(originalScenePath) && File.Exists(originalScenePath))
                EditorSceneManager.OpenScene(originalScenePath, OpenSceneMode.Single);

            status = "Finished. Generated " + totalGenerated + " clips; kept " + totalSkipped +
                " existing clips; " + totalFailed + " failed. Review the Unity Console for any generation errors.";
            Repaint();
        }

        void FinishAtCharacterBudget()
        {
            busy = false;
            apiKey = string.Empty;
            activeKey = string.Empty;
            cueQueue.Clear();
            EditorUtility.ClearProgressBar();
            if (!string.IsNullOrEmpty(originalScenePath) && File.Exists(originalScenePath))
                EditorSceneManager.OpenScene(originalScenePath, OpenSceneMode.Single);
            status = "Paused at the character budget. Generated " + totalGenerated + " clips using about " + charactersThisRun +
                " characters; kept " + totalSkipped + " existing clips; " + totalFailed + " failed. Set a new run budget within your remaining ElevenLabs credits and run again to continue; completed clips are skipped.";
            Repaint();
        }

        void StopBatch(string message)
        {
            if (activeRequest != null && !activeRequest.isDone) activeRequest.Abort();
            activeRequest?.Dispose();
            activeRequest = null;
            cueQueue.Clear();
            busy = false;
            activeKey = string.Empty;
            apiKey = string.Empty;
            EditorUtility.ClearProgressBar();
            SaveCurrentAndRestoreOriginalScene();
            status = message;
            Repaint();
        }

        void SaveCurrentAndRestoreOriginalScene()
        {
            Scene current = SceneManager.GetActiveScene();
            if (current.IsValid() && current.isLoaded && current.isDirty)
                EditorSceneManager.SaveScene(current);
            if (!string.IsNullOrEmpty(originalScenePath) && File.Exists(originalScenePath))
                EditorSceneManager.OpenScene(originalScenePath, OpenSceneMode.Single);
        }

        static string JoinNonEmpty(params string[] parts)
        {
            return string.Join(" ", parts.Where(part => !string.IsNullOrWhiteSpace(part))
                .Select(part => Regex.Replace(part.Trim(), @"\s+", " ")));
        }

        static string GetTransformPath(Transform transform)
        {
            var names = new Stack<string>();
            for (Transform current = transform; current != null; current = current.parent)
                names.Push(current.name);
            return string.Join("_", names.ToArray());
        }

        static string SafeName(string value)
        {
            string safe = Regex.Replace(value ?? string.Empty, @"[^A-Za-z0-9_-]+", "_").Trim('_', '-');
            return string.IsNullOrEmpty(safe) ? "Narration" : safe;
        }

        [Serializable]
        sealed class SpeechRequest
        {
            public string text;
            public string model_id;
            public VoiceSettings voice_settings;
        }

        [Serializable]
        sealed class VoiceSettings
        {
            public float stability;
            public float similarity_boost;
        }

        sealed class Cue
        {
            public Component Owner;
            public string AudioProperty;
            public string Text;
            public string AssetPath;
            public string Label;
        }
    }
}
#endif
