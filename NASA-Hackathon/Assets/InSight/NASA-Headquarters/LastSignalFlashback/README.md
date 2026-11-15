# InSight mission flow

This InSight-only flow is `TitleScreen-Night → InSight-Flashback → InSight-Launch → InSight-Landing → InSight-Arrival → InSight-Legacy → TitleScreen-Night`. The flashback introduces assembly, the launch scene covers launch and countdown, the landing chapter explains entry/descent/landing, the arrival chapter explores SEIS, HP³ and RISE, and the legacy chapter lets the player identify why the lander lost power.

## Build and narrate

1. Open the Unity project and wait for scripts to compile.
2. Choose **Last Signal → InSight → Build Mission Flow Scenes**. The generated Landing, Arrival and Legacy scenes reference the existing XR rig, InSight model, Mars materials and facility materials. Source assets remain unchanged.
3. To generate narration, choose **Last Signal → InSight → Narration → Generate Brian Clips for All InSight Scenes**. Paste an ElevenLabs secret key into the password field and select **Generate missing InSight clips**. The key stays in the window's memory; generated MP3 clips are imported and assigned to the flashback and mission scenes. The existing InSight Launch clips remain assigned in the launch scene.
4. For the full headset build, open `InSight-Flashback` and choose **Last Signal → InSight Flashback → VR → Build Quest 2 InSight Demo APK**. The build contains the title, InSight flashback, launch, landing, science and legacy scenes.

The mission chapters use a floor-tracked shared XR rig, controller movement and rays, a large high-contrast world-space caption panel, and desktop keyboard controls (WASD/arrows, 1–4, Enter). No vignette/tunnel mask is used in InSight scenes. The lander is an instance of the imported model; the source model and shared assets are not edited.

## NASA sources used for narration

- [NASA/JPL InSight landing mission overview](https://www.jpl.nasa.gov/news/press_kits/insight/landing/mission/)
- [NASA/JPL Elysium Planitia landing site](https://www.jpl.nasa.gov/news/press_kits/insight/landing/mission/landing-site/)
- [NASA Science: InSight spacecraft](https://science.nasa.gov/mission/insight/spacecraft/)
- [NASA Science: InSight instruments](https://science.nasa.gov/mission/insight/science-instruments/)
- [NASA/JPL mission end and results](https://www.jpl.nasa.gov/news/nasa-retires-insight-mars-lander-mission-after-years-of-science/)
