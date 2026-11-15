#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace NasaHack.EditorTools
{
    // Offline manifest bridge: credentials never enter a scene or a manifest.
    public sealed partial class SceneNarrationGeneratorWindow
    {
        const string ManifestPath = "Library/LastSignalNarration/manifest.json";

        [Serializable]
        public sealed class NarrationManifest
        {
            public string voiceId = VoiceId;
            public string modelId = "eleven_multilingual_v2";
            public List<NarrationEntry> cues = new List<NarrationEntry>();
        }

        [Serializable]
        public sealed class NarrationEntry
        {
            public string scenePath;
            public string objectId;
            public string propertyPath;
            public string text;
            public string assetPath;
            public string existingClipPath;
            public string label;
        }

        // -batchmode -executeMethod NasaHack.EditorTools.SceneNarrationGeneratorWindow.ExportManifest -quit
        public static void ExportManifest()
        {
            var manifest = new NarrationManifest();
            var scanner = CreateInstance<SceneNarrationGeneratorWindow>();
            scanner.includeAssignedCues = true;
            var setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                foreach (var buildScene in EditorBuildSettings.scenes.Where(s => s.enabled))
                {
                    var scene = EditorSceneManager.OpenScene(buildScene.path, OpenSceneMode.Single);
                    scanner.PrepareTitleCards(scene);
                    scanner.cueQueue.Clear();
                    scanner.CollectSceneCues(scene);
                    // Save added components before recording their persistent IDs.
                    if (scene.isDirty) EditorSceneManager.SaveScene(scene);
                    foreach (var cue in scanner.cueQueue)
                    {
                        var slot = new SerializedObject(cue.Owner).FindProperty(cue.AudioProperty);
                        manifest.cues.Add(new NarrationEntry
                        {
                            scenePath = scene.path,
                            objectId = GlobalObjectId.GetGlobalObjectIdSlow(cue.Owner).ToString(),
                            propertyPath = cue.AudioProperty,
                            text = cue.Text,
                            assetPath = cue.AssetPath,
                            existingClipPath = AssetDatabase.GetAssetPath(slot.objectReferenceValue),
                            label = cue.Label
                        });
                    }
                }
                Directory.CreateDirectory(Path.GetDirectoryName(ManifestPath));
                File.WriteAllText(ManifestPath, JsonUtility.ToJson(manifest, true));
                var missing = manifest.cues.Where(c => string.IsNullOrEmpty(c.existingClipPath)).ToArray();
                Debug.Log($"[Narration] Exported {manifest.cues.Count} cues in {manifest.cues.Select(c => c.scenePath).Distinct().Count()} scenes; {missing.Length} missing, {missing.Sum(c => c.text.Length)} characters.");
            }
            finally
            {
                DestroyImmediate(scanner);
                EditorSceneManager.RestoreSceneManagerSetup(setup);
            }
        }

        public static void AssignManifest()
        {
            var manifest = JsonUtility.FromJson<NarrationManifest>(File.ReadAllText(ManifestPath));
            var setup = EditorSceneManager.GetSceneManagerSetup();
            int assigned = 0;
            int missing = 0;
            try
            {
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                foreach (var group in manifest.cues.GroupBy(c => c.scenePath))
                {
                    var scene = EditorSceneManager.OpenScene(group.Key, OpenSceneMode.Single);
                    foreach (var cue in group)
                    {
                        if (!GlobalObjectId.TryParse(cue.objectId, out var id))
                            throw new InvalidOperationException("Invalid narration owner: " + cue.label);
                        var owner = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(id);
                        if (owner == null) throw new InvalidOperationException("Narration owner missing: " + cue.label);
                        var serialized = new SerializedObject(owner);
                        var slot = serialized.FindProperty(cue.propertyPath);
                        if (slot == null) throw new InvalidOperationException("Narration slot missing: " + cue.label);
                        if (slot.objectReferenceValue != null) continue;
                        if (!File.Exists(cue.assetPath)) { missing++; continue; }
                        var importer = AssetImporter.GetAtPath(cue.assetPath) as AudioImporter;
                        if (importer == null) throw new InvalidOperationException("Audio import failed: " + cue.assetPath);
                        importer.forceToMono = true;
                        var settings = importer.defaultSampleSettings;
                        settings.loadType = AudioClipLoadType.DecompressOnLoad;
                        settings.compressionFormat = AudioCompressionFormat.Vorbis;
                        settings.quality = 0.8f;
                        importer.defaultSampleSettings = settings;
                        importer.SaveAndReimport();
                        var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(cue.assetPath);
                        if (clip == null || clip.length <= 0f)
                            throw new InvalidOperationException("Empty audio: " + cue.assetPath);
                        slot.objectReferenceValue = clip;
                        serialized.ApplyModifiedPropertiesWithoutUndo();
                        EditorUtility.SetDirty(owner);
                        EditorSceneManager.MarkSceneDirty(scene);
                        assigned++;
                    }
                    if (scene.isDirty) EditorSceneManager.SaveScene(scene);
                }
                AssetDatabase.SaveAssets();
                Debug.Log($"[Narration] Assigned {assigned} clips; {missing} files still missing.");
            }
            finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
        }

        [MenuItem("Last Signal/Narration/Validate All Scene Audio")]
        public static void ValidateAllSceneAudio()
        {
            var scanner = CreateInstance<SceneNarrationGeneratorWindow>();
            scanner.includeAssignedCues = true;
            var setup = EditorSceneManager.GetSceneManagerSetup();
            var report = new List<string>();
            var failures = new List<string>();
            var checkedClips = new HashSet<AudioClip>();
            try
            {
                foreach (var buildScene in EditorBuildSettings.scenes.Where(s => s.enabled))
                {
                    var scene = EditorSceneManager.OpenScene(buildScene.path, OpenSceneMode.Single);
                    scanner.cueQueue.Clear();
                    scanner.CollectSceneCues(scene);
                    int listeners = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<AudioListener>())
                        .Count(l => l.enabled && l.gameObject.activeInHierarchy);
                    if (listeners != 1) failures.Add(scene.name + ": expected one active AudioListener, found " + listeners);
                    if (scanner.cueQueue.Count == 0) failures.Add(scene.name + ": no narration cues.");
                    foreach (var cue in scanner.cueQueue)
                    {
                        var clip = new SerializedObject(cue.Owner).FindProperty(cue.AudioProperty).objectReferenceValue as AudioClip;
                        if (clip == null) { failures.Add("Unassigned: " + cue.Label); continue; }
                        if (!checkedClips.Add(clip)) continue;
                        clip.LoadAudioData();
                        var samples = new float[clip.samples * clip.channels];
                        if (clip.length < 0.1f || !clip.GetData(samples, 0) || !samples.Any(s => Mathf.Abs(s) > 0.001f))
                            failures.Add("Empty or silent: " + AssetDatabase.GetAssetPath(clip));
                        report.Add($"CLIP {clip.name}: {clip.length:F2}s, {clip.frequency}Hz, {clip.channels} channel(s)");
                    }
                    report.Add($"SCENE {scene.name}: {scanner.cueQueue.Count} cues, {listeners} active listener(s)");
                }
                report.AddRange(failures);
                report.Add($"RESULT: {checkedClips.Count} unique clips, {failures.Count} failures.");
                Directory.CreateDirectory(Path.GetDirectoryName(ManifestPath));
                File.WriteAllLines("Library/LastSignalNarration/validation.txt", report);
                if (failures.Count > 0) throw new InvalidOperationException(string.Join("\n", failures));
                Debug.Log("[Narration] " + report[report.Count - 1]);
            }
            finally
            {
                DestroyImmediate(scanner);
                EditorSceneManager.RestoreSceneManagerSetup(setup);
            }
        }
    }
}
#endif
