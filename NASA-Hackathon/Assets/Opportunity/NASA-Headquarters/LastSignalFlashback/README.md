# Opportunity Flashback

This scene is only the flashback development facility. It does not contain the Mars planet or the “See how I got here” choice; the Mars scene owns that interaction and can enter here through `OpportunityFlashbackEntry.EnterFlashback()`.

## What the scene does

- Fades into a NASA/JPL-inspired rover integration bay, with a temporary primitive rover scaffold and three marked sockets.
- Uses the shared `XR Rig` prefab from the project XR setup commit. Its XRI controller rays, direct interactors, grab input, locomotion, snap-turn, and Editor XR Device Simulator are reused. The scene references the prefab by GUID, so moving its folder does not require changing the interactions.
- Lets the player grab the solar panel, camera mast, and instrument arm and release each within its forgiving snap radius. Each successful snap gives a chime, updates the checklist, and presents its matching caption and optional narration clip.
- Unlocks Systems Check only when all three parts are secured. A passing check reveals the optional final Opportunity model and unlocks Ready for Launch.
- Emits `OpportunityFlashbackSceneController.LaunchHandoffRequested` and the Inspector **On Launch Handoff Requested** event. It does not load or implement the launch scene.
- Keeps an empty `OPPORTUNITY MODEL ANCHOR` separate from the educational scaffold. Assign the imported model prefab to **Final Opportunity Model Prefab**; the existing interactions remain attached to the scaffold sockets.

The educational captions are short and factual:

| Moment | Caption / narration line |
| --- | --- |
| Entry | “Opportunity was built to study Martian rocks and soils for clues that water once flowed there.” |
| Solar panel | “The solar arrays collected sunlight and converted it into electricity for the rover's batteries.” |
| Camera mast | “The camera mast raised stereo cameras for navigation and panoramic views of the terrain.” |
| Instrument arm | “The instrument arm placed science tools against rocks so Opportunity could examine their texture and chemistry.” |
| Systems check | “Power, vision, and science tools are accounted for. Opportunity is ready for launch.” |

The assignable narration clip slots are on the scene controller. Import ElevenLabs Brian audio separately and assign the clips; the configured voice ID is `nPczcJzI2devNBz1zQrb`. There are no API keys or live voice requests. A short generated chime provides immediate snap feedback when no custom snap clip is assigned.

## Hookup

1. Open `Scenes/Opportunity-Flashback.unity`. Its serialized controller already references the committed shared XR rig prefab by GUID.
2. Add the scene to **File → Build Settings** for player builds. This section stays in `NASA-Headquarters`, so project-wide build settings are left to the integrator.
3. In the Mars scene, call `OpportunityFlashbackEntry.EnterFlashback()` from the “See how I got here” action. Do not place that choice or Mars environment in this scene.
4. When the final rover model arrives, assign its prefab to **Final Opportunity Model Prefab**. Keep the empty model anchor as the placement point.
5. To connect launch, add the existing `NasaHack.LoadNextScene` (or the launch-flow component) as a listener on **On Launch Handoff Requested**. Set its destination there; this flashback only emits the handoff.
6. Import Brian's generated clips and assign the five narration fields. Captions work without audio.

## Editor verification

With the Unity project open, choose **Last Signal → Opportunity Flashback → Verify Assembly Flow**. It checks far-drop rejection, near-drop snapping, all three progression steps, initial launch lock, Systems Check gating, handoff emission, and reset/re-lock behavior in a temporary scene. The committed project also configures the XR Device Simulator for Editor interaction.
