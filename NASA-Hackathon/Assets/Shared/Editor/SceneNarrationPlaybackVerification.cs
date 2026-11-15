#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.XR;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;
using Object = UnityEngine.Object;

namespace NasaHack.EditorTools
{
    /// <summary>Exercises authored narration triggers in Play mode and measures real mixer output.</summary>
    [InitializeOnLoad]
    public static class SceneNarrationPlaybackVerification
    {
        const string Pending = "LastSignal.NarrationPlaybackVerification";
        const string Report = "Library/LastSignalNarration/playback-validation.txt";
        static readonly Stack<IEnumerator> steps = new Stack<IEnumerator>();
        static readonly List<string> results = new List<string>();
        static readonly List<string> runtimeErrors = new List<string>();
        static readonly float[] waveform = new float[1024];
        static double deadline;
        static XRHMD simulatedHead;

        static SceneNarrationPlaybackVerification()
        {
            EditorApplication.playModeStateChanged += OnPlayMode;
        }

        [MenuItem("Last Signal/Narration/Verify Playback in All Scenes")]
        public static void Start()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play mode before starting narration verification.");
            if (!EditorSceneManager.SaveOpenScenes()) return;
            SessionState.SetBool(Pending, true);
            File.WriteAllText(Report, "RUNNING: entering Play mode.\n");
            EditorApplication.EnterPlaymode();
        }

        static void OnPlayMode(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(Pending, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                results.Clear();
                runtimeErrors.Clear();
                steps.Clear();
                deadline = EditorApplication.timeSinceStartup + 360;
                // HQ deliberately waits for tracking before its fade-in and narration. Supply
                // a test-only tracked device when running this check without a worn headset.
                if (!InputSystem.devices.OfType<XRHMD>().Any(h => h.isTracked.isPressed))
                {
                    simulatedHead = InputSystem.AddDevice<XRHMD>();
                    results.Add("Tracking: simulated Editor HMD; physical headset output is not tested.");
                }
                Application.logMessageReceived += OnLog;
                steps.Push(VerifyScenes());
                EditorApplication.update += Tick;
            }
            else if (state == PlayModeStateChange.ExitingPlayMode && steps.Count > 0)
                Finish("INTERRUPTED: Play mode ended during verification.");
        }

        static void OnLog(string message, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                runtimeErrors.Add(SceneManager.GetActiveScene().name + ": " + message);
        }

        static void Tick()
        {
            try
            {
                Require(EditorApplication.timeSinceStartup < deadline, "Playback verification timed out.");
                if (simulatedHead != null)
                {
                    using (StateEvent.From(simulatedHead, out var eventPtr))
                    {
                        simulatedHead.isTracked.WriteValueIntoEvent(1f, eventPtr);
                        simulatedHead.trackingState.WriteValueIntoEvent(3, eventPtr);
                        simulatedHead.devicePosition.WriteValueIntoEvent(Vector3.up * 1.65f, eventPtr);
                        simulatedHead.deviceRotation.WriteValueIntoEvent(Quaternion.identity, eventPtr);
                        simulatedHead.centerEyePosition.WriteValueIntoEvent(Vector3.up * 1.65f, eventPtr);
                        simulatedHead.centerEyeRotation.WriteValueIntoEvent(Quaternion.identity, eventPtr);
                        InputSystem.QueueEvent(eventPtr);
                    }
                }
                while (steps.Count > 0)
                {
                    var step = steps.Peek();
                    if (!step.MoveNext()) { (step as IDisposable)?.Dispose(); steps.Pop(); continue; }
                    if (step.Current is IEnumerator nested) { steps.Push(nested); continue; }
                    return;
                }
                Require(runtimeErrors.Count == 0, "Runtime errors: " + string.Join("\n", runtimeErrors.Distinct()));
                Finish("PASS: all eight scenes, narration triggers, listener output, and both launch handoffs.");
            }
            catch (Exception exception) { Finish("FAIL: " + exception); }
        }

        static void Finish(string outcome)
        {
            EditorApplication.update -= Tick;
            Application.logMessageReceived -= OnLog;
            steps.Clear();
            if (simulatedHead != null) InputSystem.RemoveDevice(simulatedHead);
            simulatedHead = null;
            SessionState.SetBool(Pending, false);
            results.Add(outcome);
            File.WriteAllLines(Report, results);
            Debug.Log("[Narration verification] " + outcome);
            if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
        }

        static IEnumerator VerifyScenes()
        {
            foreach (var scene in EditorBuildSettings.scenes.Where(s => s.enabled))
            {
                SceneManager.LoadScene(Path.GetFileNameWithoutExtension(scene.path));
                yield return Wait(0.6);
                Require(Object.FindObjectsOfType<AudioListener>().Count(l => l.isActiveAndEnabled) == 1,
                    scene.path + ": expected exactly one active AudioListener.");
                Require(!AudioListener.pause && AudioListener.volume > 0f, "Global audio is paused or muted.");

                var intro = Object.FindObjectOfType<SceneIntroNarrator>();
                if (intro != null) yield return CheckPlayback(intro.narrationClip, "scene introduction");
                foreach (var card in Object.FindObjectsOfType<TitleCardNarrator>())
                {
                    card.OnPointerEnter(new PointerEventData(EventSystem.current));
                    yield return CheckPlayback(card.narrationClip, "mission card " + card.name);
                }
                foreach (var panel in Object.FindObjectsOfType<NarrationPanel>())
                {
                    for (int i = 0; i < panel.beats.Length; i++)
                    {
                        if (i > 0)
                        {
                            if (panel.beats[i - 1].waitForEvent) panel.ContinueIfWaiting();
                            else panel.Continue();
                        }
                        yield return CheckPlayback(panel.beats[i].voice, "story beat " + (i + 1));
                    }
                }
                foreach (var hotspot in Object.FindObjectsOfType<InfoHotspot>())
                {
                    hotspot.GetComponent<XRSimpleInteractable>().selectEntered.Invoke(new SelectEnterEventArgs());
                    yield return CheckPlayback(hotspot.narrationClip, "hotspot " + hotspot.title);
                }

                var landing = Object.FindObjectOfType<LandingSequenceController>();
                if (landing != null) yield return VerifyLanding(landing);
                var launch = Object.FindObjectOfType<DeltaIILaunch>();
                if (launch != null) yield return VerifyLaunch(launch);

                if (scene.path.Contains("Opportunity-Flashback"))
                {
                    var controller = Object.FindObjectsOfType<MonoBehaviour>().First(c => c.GetType().Name == "OpportunityFlashbackSceneController");
                    yield return CheckPlayback(ReadField<AudioClip>(controller, "introNarration"), "HQ introduction", 10);
                }
                results.Add("SCENE PASS: " + Path.GetFileNameWithoutExtension(scene.path));
                File.WriteAllLines(Report, results);
            }
        }

        static IEnumerator VerifyLanding(LandingSequenceController landing)
        {
            yield return CheckPlayback(landing.landingIntroductionNarration, "landing introduction", 12);
            var rocks = ReadField<GameObject[]>(landing, "studyRocks");
            var origin = Object.FindObjectOfType<XROrigin>();
            Require(origin != null && origin.Camera != null, "Landing XR origin/camera missing.");
            for (int i = 0; i < rocks.Length; i++)
            {
                var controllers = origin.GetComponentsInChildren<CharacterController>().Where(c => c.enabled).ToArray();
                foreach (var controller in controllers) controller.enabled = false;
                origin.MoveCameraToWorldLocation(rocks[i].transform.position + Vector3.up * 1.6f);
                foreach (var controller in controllers) controller.enabled = true;
                rocks[i].GetComponent<XRSimpleInteractable>().selectEntered.Invoke(new SelectEnterEventArgs());
                yield return CheckPlayback(landing.sampleNarrationClips[i], "landing sample " + (i + 1), 5);
            }
            landing.SubmitConclusion(false);
            yield return CheckPlayback(landing.incorrectConclusionNarration, "incorrect conclusion");
            landing.SubmitConclusion(true);
            yield return CheckPlayback(landing.conclusionNarration, "water conclusion");
        }

        static IEnumerator VerifyLaunch(DeltaIILaunch launch)
        {
            yield return CheckPlayback(launch.briefingNarration, "launch briefing");
            var source = ReadField<AudioSource>(launch, "narrationSource");
            Require(source.gameObject != launch.gameObject, "Narration shares RocketRumble's filter object.");
            Require(launch.rumble == null || launch.rumble.GetComponents<AudioSource>().Length == 1,
                "RocketRumble has multiple AudioSources.");
            string initialScene = SceneManager.GetActiveScene().name;
            string nextScene = launch.sceneFlow.nextScene;
            var expected = new[] { launch.countdownNarration, launch.internalPowerNarration, launch.ignitionNarration,
                launch.liftoffNarration, launch.pitchNarration, launch.stagingNarration, launch.cruiseNarration };
            var seen = new HashSet<AudioClip>();
            var advanced = new HashSet<AudioClip>();
            var previous = new Dictionary<AudioClip, int>();
            float cruiseLength = launch.cruiseNarration.length;
            float cruiseProgress = 0f;
            double timeout = EditorApplication.timeSinceStartup + 90;
            launch.BeginLaunch();
            while (SceneManager.GetActiveScene().name == initialScene)
            {
                Require(EditorApplication.timeSinceStartup < timeout, "Launch did not hand off.");
                if (source != null && source.isPlaying && source.clip != null)
                {
                    seen.Add(source.clip);
                    if (previous.TryGetValue(source.clip, out int sample) && source.timeSamples > sample) advanced.Add(source.clip);
                    previous[source.clip] = source.timeSamples;
                    if (source.clip == expected[expected.Length - 1]) cruiseProgress = Mathf.Max(cruiseProgress, source.time);
                }
                yield return null;
            }
            Require(SceneManager.GetActiveScene().name == nextScene, "Launch loaded the wrong scene.");
            Require(expected.All(c => seen.Contains(c) && advanced.Contains(c)), "One or more launch milestones did not play.");
            Require(cruiseProgress >= cruiseLength - 0.35f, "Launch handoff cut off the final narration.");
            results.Add($"PASS {initialScene}: all 7 flight cues played; final voice reached {cruiseProgress:F2}/{cruiseLength:F2}s before {nextScene}.");
        }

        static IEnumerator CheckPlayback(AudioClip clip, string label, double timeout = 3)
        {
            Require(clip != null, "Missing clip: " + label);
            double until = EditorApplication.timeSinceStartup + timeout;
            AudioSource source = null;
            while (source == null)
            {
                source = Object.FindObjectsOfType<AudioSource>().FirstOrDefault(s => s.clip == clip && s.isPlaying);
                Require(EditorApplication.timeSinceStartup < until, "Narration did not start: " + label);
                if (source == null) yield return null;
            }
            Require(source.enabled && !source.mute && source.volume > 0f && source.spatialBlend == 0f,
                "Muted or spatially attenuated narration: " + label);
            int startSample = source.timeSamples;
            float peak = 0f;
            until = EditorApplication.timeSinceStartup + 0.65;
            while (EditorApplication.timeSinceStartup < until)
            {
                AudioListener.GetOutputData(waveform, 0);
                foreach (float sample in waveform) peak = Mathf.Max(peak, Mathf.Abs(sample));
                yield return null;
            }
            Require(source.timeSamples > startSample, "Audio sample position did not advance: " + label);
            Require(peak > 0.0001f, "Mixer output is silent: " + label);
            results.Add($"PASS {SceneManager.GetActiveScene().name} / {label}: samples advancing, mixer peak {peak:F4}");
        }

        static T ReadField<T>(object target, string name) => (T)target.GetType()
            .GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(target);

        static IEnumerator Wait(double seconds)
        {
            double until = EditorApplication.timeSinceStartup + seconds;
            while (EditorApplication.timeSinceStartup < until) yield return null;
        }

        static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
#endif
