# Opportunity Flashback

This scene is only the flashback development facility. It does not contain the Mars planet or the “See how I got here” choice; the Mars scene owns that interaction and can enter here through `OpportunityFlashbackEntry.EnterFlashback()`.

## What the scene does

- Fades into a NASA/JPL-inspired rover integration bay, using the updated Opportunity FBX as its chassis and three named component groups.
- Uses the shared `XR Rig` prefab from the project XR setup commit. Its tracked camera, XRI controller rays and direct interactors, grab input, continuous movement, and snap-turn are reused. The scene references the prefab by GUID, so moving its folder does not require changing the interactions.
- Starts with the solar arrays, camera mast, and instrument arm detached as grabbable FBX parts on the three numbered bench trays. The matching chassis mounts remain clearly marked. Each part snaps into its original mount with a chime, checklist update, caption, and optional narration clip.
- Snapping a tray copy immediately turns its original FBX group back on at the authored rover transform and hides the copy. Systems Check unlocks only after all three parts are secured; a passing check unlocks Ready for Launch. Reset hides the original groups and returns the grabbable copies to their trays.
- Emits `OpportunityFlashbackSceneController.LaunchHandoffRequested` and the Inspector **On Launch Handoff Requested** event. It does not load or implement the launch scene.
- Keeps `OPPORTUNITY MODEL ANCHOR` separate from the temporary primitive assembly scaffold. The scene assigns the updated FBX prefab there; the existing snap targets remain stable while detached copies are grabbed from the trays.

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
4. The updated FBX is assigned to **Final Opportunity Model Prefab**. Keep the model anchor as the placement point; another model can replace it later if it retains separate `panels_left`, `panels_right`, `mast`, and `arm_mount` groups.
5. To connect launch, add the existing `NasaHack.LoadNextScene` (or the launch-flow component) as a listener on **On Launch Handoff Requested**. Set its destination there; this flashback only emits the handoff.
6. Import Brian's generated clips and assign the five narration fields. Captions work without audio.

## Editor verification

With the Unity project open, choose **Last Signal → Opportunity Flashback → Verify Assembly Flow**. It checks the saved XR camera, ray/direct interactors, movement and snap-turn providers, imported model parts, immediate model restoration on each snap, near/far snap behavior, assembly progression, Systems Check and launch locks, handoff emission, and reset in a temporary scene. This deterministic check does not require a headset.
