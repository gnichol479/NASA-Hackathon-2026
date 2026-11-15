using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;

namespace NasaHack
{
    // Runs a rocket launch: countdown, ignition, liftoff, pitch-over, booster jettison and
    // air-lit ignition (if the rocket has boosters), then hands off to LoadNextScene.
    // The real flight is compressed so the whole show takes about 40 seconds.
    // Built for Opportunity's Delta II 7925H; InSight's Atlas V 401 uses it with its own messages.
    public class DeltaIILaunch : MonoBehaviour
    {
        [Header("Rocket")]
        public Transform rocket;
        public ParticleSystem[] mainEngineFlames;   // RS-27A + verniers
        public ParticleSystem[] groundLitFlames;    // 6 GEM-46, lit on the pad
        public ParticleSystem[] airLitFlames;       // 3 GEM-46, lit in flight
        public ParticleSystem[] liftoffEffects;     // exhaust trail and glow, started at liftoff
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

        [Header("Flight")]
        [Tooltip("Acceleration at liftoff and late in the flight, in m/s². A heavy rocket starts slowly.")]
        public float liftoffAcceleration = 5f;
        public float maxAcceleration = 30f;

        [Header("Messages on the countdown sign")]
        [TextArea(2, 5)] public string idleMessage =
            "DELTA II 7925H  •  MARS EXPLORATION ROVER-B \"OPPORTUNITY\"\n" +
            "Cape Canaveral, Space Launch Complex 17B  •  July 7, 2003, 11:18 p.m. EDT\n\n" +
            "Press the red LAUNCH button to start the countdown.";
        [TextArea(1, 3)] public string ignitionMessage = "Main engine start!";
        [TextArea(1, 3)] public string liftoffMessage = "LIFTOFF!\nDelta II lifts off carrying the Opportunity rover to Mars.";
        [TextArea(1, 3)] public string pitchMessage = "Pitching over and heading east, out over the Atlantic Ocean.\nSix ground-lit GEM-46 boosters at full thrust.";
        [TextArea(1, 3)] public string stagingMessage = "The six ground-lit boosters burn out and drop away.\nThe three air-lit boosters ignite!";
        [TextArea(1, 4)] public string cruiseMessage = "Opportunity is on its way to Mars!\nLanding: Meridiani Planum, January 25, 2004 UTC (January 24 local time).\n\n(Real launch timeline sped up.)";

        [Header("Narration")]
        [TextArea(2, 6)] public string briefingNarrationText;
        [TextArea(1, 3)] public string countdownNarrationText;
        [TextArea(1, 2)] public string internalPowerNarrationText;
        [TextArea(1, 2)] public string ignitionNarrationText;
        [TextArea(1, 3)] public string liftoffNarrationText;
        [TextArea(1, 3)] public string pitchNarrationText;
        [TextArea(1, 3)] public string stagingNarrationText;
        [TextArea(1, 4)] public string cruiseNarrationText;
        public AudioClip briefingNarration;
        public AudioClip countdownNarration;
        public AudioClip internalPowerNarration;
        public AudioClip ignitionNarration;
        public AudioClip liftoffNarration;
        public AudioClip pitchNarration;
        public AudioClip stagingNarration;
        public AudioClip cruiseNarration;

        public bool Launched { get; private set; }

        float velocity;
        float flightTime;
        bool flying;
        float ignitionTime = -1f;
        Quaternion padRotation;
        AudioSource narrationSource;

        void Start()
        {
            EnsureNarrationText();
            // RocketRumble uses OnAudioFilterRead on the Launch Complex object. Keep narration
            // on a child source so Unity does not invoke that stateful filter for two sources.
            narrationSource = NarrationPlayback.CreateSource(this);
            SetClock("T-" + countdownSeconds.ToString("00"));
            SetStatus(idleMessage);
            PlayNarration(briefingNarration);
            if (engineLight) engineLight.intensity = 0f;
            if (rocket) padRotation = rocket.rotation;
            DynamicGI.UpdateEnvironment(); // refresh sky reflections (ocean, metal) from the skybox
        }

        /// <summary>Fills scene-specific, NASA-sourced briefing copy and milestone captions for the editor generator.</summary>
        public void EnsureNarrationText()
        {
            if (string.IsNullOrWhiteSpace(briefingNarrationText))
            {
                if (idleMessage.IndexOf("OPPORTUNITY", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    briefingNarrationText = "NASA's Mars Exploration Rover-B, Opportunity, launched aboard a Delta II 7925H from Cape Canaveral Air Force Station on July 7, 2003, at 11:18 p.m. Eastern Daylight Time. Its mission was to investigate evidence of past water on Mars. Press the red launch button when you are ready. This is a shortened educational reenactment.";
                else if (idleMessage.IndexOf("INSIGHT", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    briefingNarrationText = "NASA's InSight lander launched aboard an Atlas V 401 from Vandenberg Air Force Base in California on May 5, 2018. The launch window opened at 4:05 a.m. Pacific Daylight Time. InSight was sent to study the deep interior of Mars. Press the red launch button when you are ready. This is a shortened educational reenactment.";
                else
                    briefingNarrationText = idleMessage;
            }

            // The visual launch clock still counts down, but the numbers are intentionally
            // not spoken. A fast compressed voice clip was difficult to understand.
            if (string.IsNullOrWhiteSpace(internalPowerNarrationText))
                internalPowerNarrationText = "Launch vehicle on internal power.";
            if (string.IsNullOrWhiteSpace(ignitionNarrationText)) ignitionNarrationText = ignitionMessage;
            if (string.IsNullOrWhiteSpace(liftoffNarrationText)) liftoffNarrationText = liftoffMessage;
            if (string.IsNullOrWhiteSpace(pitchNarrationText)) pitchNarrationText = pitchMessage;
            if (string.IsNullOrWhiteSpace(stagingNarrationText)) stagingNarrationText = stagingMessage;
            if (string.IsNullOrWhiteSpace(cruiseNarrationText)) cruiseNarrationText = cruiseMessage;
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
                if (t == countdownSeconds)
                {
                    SetStatus("Countdown started. All systems go.");
                }
                if (t == 6)
                {
                    SetStatus("Launch vehicle on internal power.");
                    PlayNarration(internalPowerNarration);
                }
                if (t == 3)
                {
                    SetStatus(ignitionMessage);
                    PlayNarration(ignitionNarration);
                    Play(mainEngineFlames);
                    ignitionTime = Time.time;
                    if (rumble) rumble.target = 0.35f;
                }
                yield return new WaitForSeconds(1f);
            }

            // ---- Liftoff
            SetClock("T+00");
            SetStatus(liftoffMessage);
            PlayNarration(liftoffNarration);
            Play(groundLitFlames);
            Play(liftoffEffects);
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
                    SetStatus(pitchMessage);
                    PlayNarration(pitchNarration);
                }
                if (!jettisoned && flightTime > 17f)
                {
                    jettisoned = true;
                    JettisonGroundLitBoosters();
                    Play(airLitFlames);
                    SetStatus(stagingMessage);
                    PlayNarration(stagingNarration);
                }
                if (!cruising && flightTime > 25f)
                {
                    cruising = true;
                    if (rumble) rumble.target = 0.3f;
                    SetStatus(cruiseMessage);
                    PlayNarration(cruiseNarration);
                }
                yield return null;
            }

            if (rumble) rumble.target = 0f;
            // The educational flight is compressed, but the final explanation must finish.
            while (narrationSource != null && narrationSource.isPlaying) yield return null;
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

            // Slow, heavy start that builds up.
            float accel = Mathf.Lerp(liftoffAcceleration, maxAcceleration, Mathf.Clamp01(flightTime / 25f));
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

        void PlayNarration(AudioClip clip)
        {
            if (clip == null || narrationSource == null) return;
            NarrationPlayback.Play(narrationSource, clip);
        }
    }
}
