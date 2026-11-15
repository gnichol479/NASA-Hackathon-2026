> **InSight version.** Rebuilt from Gibson's Opportunity flashback (`Assets/Opportunity/NASA-Headquarters`) to assemble the InSight lander: solar arrays, SEIS seismometer and robotic arm from `Assets/InSight/Models/InSight-Lander.glb`. Classes, namespace (`LastSignal.InSightFlashback`) and menus (**Last Signal > InSight Flashback**) are renamed. Ready for Launch loads `InSight-Launch`. No narration MP3s yet. The text below is the original Opportunity documentation.

# Opportunity Flashback

This scene is only the flashback development facility. It does not contain the Mars planet or the “See how I got here” choice; the Mars scene owns that interaction and can enter here through `OpportunityFlashbackEntry.EnterFlashback()`.

## What the scene does

- Fades into a NASA/JPL-inspired rover integration bay, using the updated Opportunity FBX as its chassis and three named component groups.
- Uses the shared `XR Rig` prefab from the project XR setup commit. Its tracked camera, XRI controller rays and direct interactors, grab input, continuous movement, and snap-turn are reused. The scene references the prefab by GUID, so moving its folder does not require changing the interactions.
- On scene entry, a flashback-local setup requests floor tracking and waits for a valid position-and-rotation head pose before enabling locomotion and fading in. It aligns the player's horizontal position once at the arrival point, preserving their measured standing/seated height. The existing Input System TrackedPoseDriver owns the camera; XRI owns movement, turning, grabbing and UI. There is no competing raw-input or camera-pose writer.
- Starts with the solar arrays, camera mast, and instrument arm detached as grabbable FBX parts on the three numbered bench trays. The matching chassis mounts remain clearly marked. Each part snaps into its original mount with a chime, checklist update, caption, and optional narration clip.
- Snapping a tray copy immediately turns its original FBX group back on at the authored rover transform and hides the copy. Systems Check unlocks only after all three parts are secured; a passing check unlocks Ready for Launch. Reset hides the original groups and returns the grabbable copies to their trays.
- After Systems Check, **Ready for Launch** fades to black and loads the existing `Opportunity-Launch` scene. It also emits `OpportunityFlashbackSceneController.LaunchHandoffRequested` and the Inspector **On Launch Handoff Requested** event so the rest of the project can observe the handoff.
- Keeps `OPPORTUNITY MODEL ANCHOR` separate from the temporary primitive assembly scaffold. The scene assigns the updated FBX prefab there; the existing snap targets remain stable while detached copies are grabbed from the trays.

The educational captions are short and factual:

| Moment | Caption / narration line |
| --- | --- |
| Entry | “Opportunity was built to study Martian rocks and soils for clues that water once flowed there.” |
| Solar panel | “The solar arrays collected sunlight and converted it into electricity for the rover's batteries.” |
| Camera mast | “The camera mast raised stereo cameras for navigation and panoramic views of the terrain.” |
| Instrument arm | “The instrument arm placed science tools against rocks so Opportunity could examine their texture and chemistry.” |
| Systems check | “Power, vision, and science tools are accounted for. Opportunity is ready for launch.” |

The caption script and assignable audio slots are on the scene controller. Use **Last Signal → Opportunity Flashback → Narration → ElevenLabs Brian** to generate the current five lines with voice ID `nPczcJzI2devNBz1zQrb`, assign the imported MP3 clips, and preview each line in the Editor. The API key is entered into a password field, kept in window memory only, and sent only when you choose Generate. Generated audio is saved under `Audio/ElevenLabsBrian`; captions and narration read the same editable text fields, so later script revisions stay aligned when regenerated. A short generated chime provides immediate snap feedback when no custom snap clip is assigned.

## Hookup

1. Open `Scenes/Opportunity-Flashback.unity`. Its serialized controller already references the committed shared XR rig prefab by GUID.
2. Add the scene to **File → Build Settings** for player builds. This section stays in `NASA-Headquarters`, so project-wide build settings are left to the integrator.
3. In the Mars scene, call `OpportunityFlashbackEntry.EnterFlashback()` from the “See how I got here” action. Do not place that choice or Mars environment in this scene.
4. The updated FBX is assigned to **Final Opportunity Model Prefab**. Keep the model anchor as the placement point; another model can replace it later if it retains separate `panels_left`, `panels_right`, `mast`, and `arm_mount` groups.
5. Run the systems check, then select **Ready for Launch**. The scene fades through black and loads `Opportunity-Launch`; the event remains available for additional launch-flow listeners.
6. Open the ElevenLabs Brian window, enter the API key, and generate/preview the lines. Captions continue to display when no clips are assigned.

## Editor verification

With the Unity project open, choose **Last Signal → Opportunity Flashback → Verify Assembly Flow**. It checks the saved XR camera, ray/direct interactors, movement and snap-turn providers, imported model parts, immediate model restoration on each snap, near/far snap behavior, assembly progression, Systems Check and launch locks, handoff emission, and reset in a temporary scene. This deterministic check does not require a headset.

## Quest 2 and Link

The saved project enables OpenXR and **Oculus Touch Controller Profile** for both Windows Standalone (Link / Editor Play) and Android. Android also enables Meta Quest support, ARM64, IL2CPP and Vulkan. Automatic XR Device Simulator startup is disabled so it cannot compete with the real headset. These required XR/project settings are shared; the rig overrides, setup tooling and new code remain in NASA-Headquarters.

1. Open the flashback scene. **Last Signal → Opportunity Flashback → VR → Configure Quest 2 and Link** reapplies and saves these settings if another setup tool overwrites them. It preserves the shared rig prefab.
2. For Link, connect the Quest through Meta Horizon Link, make Meta Link the active OpenXR runtime, enter Link in the headset, then press Play and focus Unity's **Game** view. Put on the headset to start tracking. Editor Play uses the Standalone OpenXR settings even with Android selected as the build target.
3. Look and lean naturally. **Left stick:** head-relative walking/strafe. **Right stick left/right:** 30-degree snap turns. **Grip:** grab/release a component. **Trigger:** ray selection and physical display buttons. The starter rig's action mediation suspends locomotion during conflicting interactions.
4. **VR → Verify VR Setup** checks loaders, profiles, camera/pose actions, floor position and locomotion bindings. **VR → Log Live XR Status** logs the tracked height, tracking mode and connected devices during Play. A black startup fade means no valid headset pose has arrived; the Console reports this after eight seconds.
5. **VR → Build Quest 2 Flashback APK** switches to Android and creates a development APK containing the flashback and its launch destination at `Builds/Quest2/LastSignal-Flashback.apk`. Android Build Support (SDK/NDK/OpenJDK) is required. This leaves the team's multi-scene Build Settings list intact. The default isolated app id is `com.lastsignal.opportunity.flashback`; a previously configured team id is preserved.

For a desktop simulation, explicitly add the existing XR Device Simulator prefab to a temporary test scene with no real headset connected. Keep automatic simulator startup off for Link. The fallback editor rig is for code verification, not headset deployment.

Headset acceptance check: enter at measured eye height above the floor; look/lean without dragging the world; walk and snap-turn; grip each of the three tray parts and release near its matching mount; complete Systems Check; confirm Ready for Launch stays locked until then; reset and repeat. Link rendering is a PC test and does not establish native Quest frame rate.
