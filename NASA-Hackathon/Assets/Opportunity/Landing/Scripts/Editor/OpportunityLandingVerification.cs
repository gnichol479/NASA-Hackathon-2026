using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.XR.CoreUtils;

// Runs the real scene and coroutine interactions; does not replace headset/input testing.
[InitializeOnLoad]
public static class OpportunityLandingVerification
{
    const string Key = "NASA.LandingVerification.Running";
    static LandingSequenceController controller;
    static int stage, sample;
    static double started, next;
    static readonly List<string> checks = new List<string>();
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static OpportunityLandingVerification() { EditorApplication.update += Tick; }

    [MenuItem("NasaHack/Landing/Verify playable mission")]
    public static void Run()
    {
        if (EditorApplication.isPlaying || SceneManager.GetActiveScene().name != "Opportunity-Landing")
        { Debug.LogWarning("Open Opportunity-Landing outside Play mode before verification."); return; }
        SessionState.SetBool(Key, true);
        EditorApplication.isPlaying = true;
    }
    static T Read<T>(string field) => (T)typeof(LandingSequenceController).GetField(field, Private).GetValue(controller);
    static void Call(string method, params object[] args) => typeof(LandingSequenceController).GetMethod(method, Private).Invoke(controller,args);
    static void Check(bool condition, string message)
    { if (!condition) throw new Exception(message); checks.Add(message); }
    static void Tick()
    {
        if (!SessionState.GetBool(Key, false) || EditorApplication.isCompiling || EditorApplication.isPaused) return;
        if (!EditorApplication.isPlaying) return;
        if (started == 0) started = EditorApplication.timeSinceStartup;
        try
        {
            if (EditorApplication.timeSinceStartup - started > 100) throw new Exception("Mission verification timed out at stage " + stage);
            if (EditorApplication.timeSinceStartup < next) return;
            if (stage == 0)
            {
                controller = UnityEngine.Object.FindObjectOfType<LandingSequenceController>();
                if (controller == null || !Read<bool>("ready")) return;
                Check(controller.opportunityRover.name.Contains("Detailed"), "Detailed rover instantiated and deployment/handoff completed");
                Check(Read<List<Transform>>("armJoints").Count >= 3, "Imported arm joints available");
                Check(Read<List<Transform>>("wheels").Count >= 6, "Six animated wheel parts available");
                var panels = Read<RectTransform[]>("taskPanels");
                Check(panels.Length == 3 && panels[0] != panels[1] && panels[1] != panels[2], "Three separate task canvases created");
                controller.SubmitConclusion(true);
                Check(!Read<bool>("discovered"), "Conclusion locked before samples are completed");
                Call("BeginMission"); stage = 1;
            }
            else if (stage == 1)
            {
                var target = Read<GameObject[]>("studyRocks")[sample].transform.position;
                var origin = UnityEngine.Object.FindObjectOfType<XROrigin>();
                var heading = Vector3.ProjectOnPlane(origin.transform.forward, Vector3.up).normalized;
                var eye = target - heading * (controller.thirdPersonDistance + 0.8f);
                eye.y = target.y + controller.thirdPersonHeight;
                origin.MoveCameraToWorldLocation(eye);
                stage = 2; next = EditorApplication.timeSinceStartup + 0.6;
            }
            else if (stage == 2)
            {
                Call("ScanRock",sample);
                Check(Read<bool>("scanning"), "Sample " + (sample+1) + " starts its instrument animation");
                stage = 3; next = EditorApplication.timeSinceStartup + 0.3;
            }
            else if (stage == 3)
            {
                if (Read<bool>("scanning")) return;
                Check(Read<bool[]>("examined")[sample], "Sample " + (sample+1) + " animation finishes and result is recorded");
                int count = Read<int>("examinedCount"); Call("ScanRock",sample);
                Check(Read<int>("examinedCount") == count && !Read<bool>("scanning"), "Sample " + (sample+1) + " cannot be counted twice");
                if (sample < 2) {Call("AdvanceTask",sample); sample++; stage=1;}
                else
                {
                    controller.SubmitConclusion(false);
                    Check(!Read<bool>("discovered"), "Incorrect conclusion permits retry");
                    controller.SubmitConclusion(true);
                    Check(Read<bool>("discovered"), "Correct water conclusion completes mission");
                    Call("ContinueToLegacy"); stage=4;
                }
            }
            else if (SceneManager.GetActiveScene().name == "Opportunity-Legacy")
            {Check(true,"Completed mission loads Opportunity-Legacy"); Finish(true,null);}
        }
        catch(Exception e) {Finish(false,e.GetBaseException().Message);}
    }
    static void Finish(bool passed, string error)
    {
        SessionState.SetBool(Key,false);
        string report = (passed ? "PASS" : "FAIL") + "\n" + string.Join("\n",checks) + (error == null ? "" : "\nERROR: " + error);
        File.WriteAllText(Path.GetFullPath("../landing-playmode-verification.txt"),report);
        if(passed) Debug.Log(report); else Debug.LogError(report);
        EditorApplication.isPlaying=false;
        started=0;stage=0;sample=0;checks.Clear();
    }
}
