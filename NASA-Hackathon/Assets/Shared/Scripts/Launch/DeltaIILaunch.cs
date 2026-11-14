using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;

namespace NasaHack
{
    // Runs the Delta II 7925H launch: countdown, ignition, liftoff, pitch-over,
    // booster jettison and air-lit ignition, then hands off to LoadNextScene.
    // The real flight is compressed so the whole show takes about 40 seconds.
    public class DeltaIILaunch : MonoBehaviour
    {
        [Header("Rocket")]
        public Transform rocket;
        public ParticleSystem[] mainEngineFlames;   // RS-27A + verniers
        public ParticleSystem[] groundLitFlames;    // 6 GEM-46, lit on the pad
        public ParticleSystem[] airLitFlames;       // 3 GEM-46, lit in flight
        public Transform[] groundLitBoosters;       // dropped after burnout
        public Light engineLight;
        [Tooltip("Peak engine glow. URP lights fall off with distance squared, so this is large: it lights up the whole pad at night.")]
        public float engineLightIntensity = 4000f;

        [Header("Pad")]
        public ParticleSystem padSmoke;

        [Header("Display")]
        public Text clockText;
        public Text statusText;

        [Header("Flow")]
        public LoadNextScene sceneFlow;
        public int countdownSeconds = 10;
        public float flightSeconds = 32f;

        [Header("Audio")]
        public RocketRumble rumble;

        public bool Launched { get; private set; }

        const string IdleMessage =
            "DELTA II 7925H  •  MARS EXPLORATION ROVER-B \"OPPORTUNITY\"\n" +
            "Cape Canaveral, Space Launch Complex 17B  •  July 7, 2003, 11:18 p.m. EDT\n\n" +
            "Press the red LAUNCH button to start the countdown.";

        float velocity;
        float flightTime;
        bool flying;
        float ignitionTime = -1f;
        Quaternion padRotation;

        void Start()
        {
            SetClock("T-" + countdownSeconds.ToString("00"));
            SetStatus(IdleMessage);
            if (engineLight) engineLight.intensity = 0f;
            if (rocket) padRotation = rocket.rotation;
            DynamicGI.UpdateEnvironment(); // refresh sky reflections (ocean, metal) from the skybox
        }

#if ENABLE_LEGACY_INPUT_MANAGER
        void Update()
        {
            // Desktop testing without a headset.
            if (Input.GetKeyDown(KeyCode.L)) BeginLaunch();
        }
#endif

        public void BeginLaunch()
        {
            if (Launched) return;
            Launched = true;
            // A climbing rocket gets far away fast; don't let the camera clip it.
            if (Camera.main) Camera.main.farClipPlane = Mathf.Max(Camera.main.farClipPlane, 30000f);
            StartCoroutine(Sequence());
        }

        IEnumerator Sequence()
        {
            // ---- Countdown
            for (int t = countdownSeconds; t > 0; t--)
            {
                SetClock("T-" + t.ToString("00"));
                if (t == countdownSeconds) SetStatus("Countdown started. All systems go.");
                if (t == 6) SetStatus("Launch vehicle on internal power.");
                if (t == 3)
                {
                    SetStatus("Main engine start!");
                    Play(mainEngineFlames);
                    ignitionTime = Time.time;
                    if (rumble) rumble.target = 0.35f;
                }
                yield return new WaitForSeconds(1f);
            }

            // ---- Liftoff
            SetClock("T+00");
            SetStatus("LIFTOFF!\nDelta II lifts off carrying the Opportunity rover to Mars.");
            Play(groundLitFlames);
            if (padSmoke) padSmoke.Play();
            if (rumble) rumble.target = 1f;
            flying = true;

            float next = 1f;
            bool pitched = false, jettisoned = false, cruising = false;
            while (flightTime < flightSeconds)
            {
                if (flightTime >= next) { SetClock("T+" + Mathf.FloorToInt(flightTime).ToString("00")); next += 1f; }

                if (flightTime > 4f && padSmoke && padSmoke.isEmitting) padSmoke.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                if (!pitched && flightTime > 9f)
                {
                    pitched = true;
                    SetStatus("Pitching over and heading east, out over the Atlantic Ocean.\nSix ground-lit GEM-46 boosters at full thrust.");
                }
                if (!jettisoned && flightTime > 17f)
                {
                    jettisoned = true;
                    JettisonGroundLitBoosters();
                    Play(airLitFlames);
                    SetStatus("The six ground-lit boosters burn out and drop away.\nThe three air-lit boosters ignite!");
                }
                if (!cruising && flightTime > 25f)
                {
                    cruising = true;
                    if (rumble) rumble.target = 0.3f;
                    SetStatus("Opportunity is on its way to Mars!\nLanding: Meridiani Planum, January 25, 2004.\n\n(Real launch timeline sped up.)");
                }
                yield return null;
            }

            if (rumble) rumble.target = 0f;
            if (sceneFlow) sceneFlow.Load();
        }

        void LateUpdate()
        {
            // Engine glow: builds up from main-engine start, flares at liftoff, fades as the rocket climbs away.
            if (engineLight && ignitionTime >= 0f)
            {
                float build = Mathf.Clamp01((Time.time - ignitionTime) / 3f);
                float level = flying ? Mathf.Lerp(1f, 0.08f, Mathf.Clamp01(flightTime / 20f)) : 0.35f * build;
                engineLight.intensity = engineLightIntensity * level * Random.Range(0.85f, 1.15f);
            }

            if (!flying || !rocket) return;
            float dt = Time.deltaTime;
            flightTime += dt;

            // Slow, heavy start that builds up: ~5 m/s² at liftoff rising to ~30 m/s².
            float accel = Mathf.Lerp(5f, 30f, Mathf.Clamp01(flightTime / 25f));
            velocity += accel * dt;

            // Gravity turn: tilt away from the viewer (+Z, "east") after clearing the tower.
            float pitch = Mathf.SmoothStep(0f, 40f, Mathf.InverseLerp(5f, 28f, flightTime));
            rocket.rotation = padRotation * Quaternion.Euler(pitch, 0f, 0f);
            rocket.position += rocket.up * velocity * dt;

            // Controller rumble while the engines are loud.
            if (Time.frameCount % 6 == 0)
            {
                float amp = Mathf.Clamp01(0.6f - flightTime / 40f);
                if (amp > 0.02f)
                    foreach (var c in FindObjectsOfType<XRBaseController>())
                        c.SendHapticImpulse(amp, 0.12f);
            }
        }

        void JettisonGroundLitBoosters()
        {
            if (groundLitBoosters == null) return;
            Vector3 v = rocket.up * velocity;
            foreach (var b in groundLitBoosters)
            {
                if (!b) continue;
                foreach (var ps in b.GetComponentsInChildren<ParticleSystem>())
                    ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                Vector3 outward = Vector3.ProjectOnPlane(b.position - rocket.position, rocket.up).normalized;
                b.SetParent(null, true);
                var fall = b.gameObject.AddComponent<FallingDebris>();
                fall.velocity = v * 0.9f + outward * 6f;
                fall.spin = outward * 40f;
            }
        }

        static void Play(ParticleSystem[] systems)
        {
            if (systems == null) return;
            foreach (var ps in systems) if (ps) ps.Play();
        }

        void SetClock(string s) { if (clockText) clockText.text = s; }
        void SetStatus(string s) { if (statusText) statusText.text = s; }
    }
}
