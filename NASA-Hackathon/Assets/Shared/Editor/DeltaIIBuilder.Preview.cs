using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace NasaHack.EditorTools
{
    // Preview renders of the launch scene, without entering Play mode or putting on the headset.
    //
    //   NasaHack > Opportunity > Capture Launch Previews   ->  Library/LaunchPreview/*.png
    //
    // It also watches for Library/LaunchPreview/request ("capture" or "build"), so a rebuild and
    // new renders can be asked for from outside Unity. Nothing here runs unless that file exists.
    public static partial class DeltaIIBuilder
    {
        const string PreviewDir = "Library/LaunchPreview";
        static string RequestFile => PreviewDir + "/request";
        static string DoneFile => PreviewDir + "/done.txt";
        static double nextPoll;

        static void StartRequestWatcher()
        {
            EditorApplication.update -= PollRequest;
            EditorApplication.update += PollRequest;
        }

        static void PollRequest()
        {
            if (EditorApplication.timeSinceStartup < nextPoll) return;
            nextPoll = EditorApplication.timeSinceStartup + 1.0;
            if (!File.Exists(RequestFile)) return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;

            string request = File.ReadAllText(RequestFile).Trim();
            try
            {
                if (request == "build")
                {
                    // Pick up changed scripts first; a recompile reloads this code, then "build2" continues.
                    File.WriteAllText(RequestFile, "build2");
                    AssetDatabase.Refresh();
                    return;
                }
                File.Delete(RequestFile);
                if (request == "build2")
                {
                    if (EditorUtility.scriptCompilationFailed) { File.WriteAllText(DoneFile, "COMPILE FAILED"); return; }
                    Build();
                }
                string result = CapturePreviews();
                File.WriteAllText(DoneFile, result);
            }
            catch (System.Exception e)
            {
                File.WriteAllText(DoneFile, "ERROR: " + e);
            }
        }

        [MenuItem("NasaHack/Opportunity/Capture Launch Previews")]
        static void CapturePreviewsMenu() => Debug.Log("[Delta II] " + CapturePreviews());

        static string CapturePreviews()
        {
            if (EditorSceneManager.GetActiveScene().path != ScenePath) return "SKIPPED: Opportunity-Launch is not the open scene";
            Directory.CreateDirectory(PreviewDir);
            foreach (var f in Directory.GetFiles(PreviewDir, "*.png")) File.Delete(f);

            var launch = Object.FindObjectOfType<DeltaIILaunch>();
            if (launch == null) return "SKIPPED: no DeltaIILaunch in the scene";

            var camGo = new GameObject("Preview Camera") { hideFlags = HideFlags.HideAndDontSave };
            var cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 12000f;
            cam.allowHDR = true;
            cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;

            var rocket = launch.rocket;
            Vector3 padPos = rocket.position;
            Quaternion padRot = rocket.rotation;
            float lightWas = launch.engineLight ? launch.engineLight.intensity : 0f;
            var eye = new Vector3(0f, 1.65f, 0f);
            Vector3 mid = padPos + Vector3.up * 16f;

            try
            {
                // ---- On the pad
                Shot(cam, "1-deck-idle", eye, mid, 70f);
                Shot(cam, "2-deck-wide", eye + new Vector3(0f, 0f, -3f), new Vector3(0f, 6f, 40f), 95f);
                Shot(cam, "3-rocket-close", new Vector3(26f, 5f, 52f), padPos + Vector3.up * 20f, 62f);
                Shot(cam, "4-console", new Vector3(-0.5f, 1.65f, -0.9f), new Vector3(0.75f, 0.95f, 1.0f), 60f);
                Shot(cam, "5-moon-behind", eye, eye + MoonDir * 100f + Vector3.down * 12f, 75f);
                Shot(cam, "6-aerial", new Vector3(-150f, 90f, -70f), new Vector3(10f, 5f, 110f), 55f);

                // ---- Liftoff: flames, smoke and engine glow simulated to a few seconds in
                SetFlight(launch, padPos, padRot, height: 7f, pitch: 0f, glow: 1f, flameTime: 1.2f, smokeTime: 4f, airLit: false);
                Shot(cam, "7-liftoff", eye, mid, 70f);
                Shot(cam, "8-liftoff-close", new Vector3(26f, 5f, 52f), padPos + Vector3.up * 14f, 62f);

                // ---- Climbing
                SetFlight(launch, padPos, padRot, height: 150f, pitch: 6f, glow: 0.45f, flameTime: 1.2f, smokeTime: 9f, airLit: false);
                Shot(cam, "9-climb", eye, rocket.position + Vector3.down * 20f, 80f);
            }
            finally
            {
                rocket.SetPositionAndRotation(padPos, padRot);
                if (launch.engineLight) launch.engineLight.intensity = lightWas;
                foreach (var ps in launch.GetComponentsInChildren<ParticleSystem>(true))
                    ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                Object.DestroyImmediate(camGo);
            }
            return "OK " + string.Join(", ", Directory.GetFiles(PreviewDir, "*.png").Select(Path.GetFileName));
        }

        static void SetFlight(DeltaIILaunch launch, Vector3 padPos, Quaternion padRot, float height, float pitch,
                              float glow, float flameTime, float smokeTime, bool airLit)
        {
            var rot = padRot * Quaternion.Euler(pitch, 0f, 0f);
            if (launch.engineLight) launch.engineLight.intensity = launch.engineLightIntensity * glow;

            var systems = launch.mainEngineFlames.Concat(launch.groundLitFlames).Concat(launch.liftoffEffects ?? new ParticleSystem[0]);
            if (airLit) systems = systems.Concat(launch.airLitFlames);
            var list = systems.Where(ps => ps).ToList();
            if (launch.padSmoke) list.Add(launch.padSmoke);

            // Fly the last few seconds of the climb in small steps, so trails and plumes form as in Play mode.
            float seconds = Mathf.Max(flameTime, smokeTime);
            const int Steps = 90;
            float speed = Mathf.Max(6f, height / 4f);          // rough speed at this point of the flight
            float startHeight = Mathf.Max(0f, height - speed * seconds);
            launch.rocket.SetPositionAndRotation(padPos + rot * Vector3.up * startHeight, rot);
            foreach (var ps in list) ps.Simulate(0f, true, true);
            for (int i = 1; i <= Steps; i++)
            {
                float h = Mathf.Lerp(startHeight, height, i / (float)Steps);
                launch.rocket.position = padPos + rot * Vector3.up * h;
                foreach (var ps in list)
                {
                    if (ps == launch.padSmoke && h > 60f) continue;
                    ps.Simulate(seconds / Steps, true, false);
                }
            }
        }

        static void Shot(Camera cam, string name, Vector3 from, Vector3 lookAt, float fov)
        {
            const int W = 1600, H = 900;
            cam.transform.position = from;
            cam.transform.rotation = Quaternion.LookRotation(lookAt - from);
            cam.fieldOfView = fov;

            var rt = RenderTexture.GetTemporary(W, H, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            cam.targetTexture = rt;
            cam.Render();
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            cam.targetTexture = null;
            RenderTexture.ReleaseTemporary(rt);
            File.WriteAllBytes($"{PreviewDir}/{name}.png", tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }
    }
}
