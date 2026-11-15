# Landing: rover mission and Legacy environment

## Current behavior

- Landing contains copies of Legacy's Mars Terrain, Mars Rocks, Directional Light, and RenderSettings. Asset references, terrain placement, terrain data, rock transforms, lighting and sky settings are shared or copied exactly. The prior Mars_EagleCrater_Terrain and original light are disabled, not deleted. No runtime environment recoloring/scattering is applied.
- This intentionally uses the final-location Perseverance Valley scenery for continuity; it is not a geographically accurate reconstruction of the Eagle Crater landing site.
- The near Mars terrain uses a balanced heightmap LOD (pixel error 12) for VR rendering; the distant terrain stays at its coarser LOD. The inactive Eagle Crater terrain remains disabled.
- The player first watches the existing four-second parachute/airbag landing from outside. The detailed rover then plays its imported solar-panel unfold clip. A fade hands control to an observer about 1.8 m above the ground and 3.5 m from Opportunity. Head tracking and room-scale movement stay independent; the rover no longer follows the player's head or body. After selecting Begin Mission, the left Touch stick drives forward/reverse and the right stick steers. WASD/arrow keys provide the same controls in Editor. Landing's terrain teleport targets, teleport provider and teleport-stick mode are disabled so a ray trigger click stays a UI selection. The detailed model matches the asset used in Arrival and Legacy, including hidden leftover parts. Wheels turn while driving, the mast scans gently, and the arm points toward the selected sample. The model's front direction is derived from its front arm mount for steering and sampling.
- A briefing canvas introduces rover mode. Three independent stationary task canvases guide microscopic imaging, mineral analysis, and evidence interpretation. Each has a clear Scan Sample action and result. The first task explains both controller mappings; completed findings remain at their stations.
- Drive to within 2.4 m of each marked rock, then press Scan Sample with the trigger or point at the rock and select it with grip. Each scan now takes about two seconds, with visible arm-positioning and readout feedback; rover driving pauses while the arm is working.
- Sample 1 reveals enlarged hematite spheres. Sample 2 provides a prewritten jarosite instrument reading. Sample 3 examines inconclusive red dust and asks which conclusion is supported. Wrong answers permit retry; correct completion unlocks Legacy.

## Test in Unity 2022.3.62f1

1. Stop Play and let compilation finish. Reopen Opportunity-Landing to load the changed scene from disk. If Unity asks about unsaved scene edits, preserve any newer manual work before reloading.
2. Verify there is one active Mars Terrain and active copied Directional Light; the old Mars_EagleCrater_Terrain is disabled. Compare the sky, rock layout, terrain and light against Opportunity-Legacy.
3. Press Play. Verify observer view, parachute landing, four-second reveal, solar-array deployment, fade, and third-person rover view. Check for geometry near the eyes. LandingAnimation > Third Person Distance defaults to 3.5 metres and Third Person Height to 1.8 metres. Deployment Seconds defaults to 4. The camera remains tracked; comfort and framing still need headset inspection.
4. Select Begin Mission. Use left stick forward/back to drive and right stick left/right to steer. Head movement remains tracked and doesn't steer or move the rover. Teleport is disabled in this section. Editor controls: WASD/arrows drive and steer; E scans; Enter advances; 1/2 select a conclusion.
5. Drive to each marked sample and press Scan Sample with the trigger, or point at the rock and select it with grip. The scan animation now runs in about two seconds. Check that it turns toward the sample and shows progress before the result appears.
6. Verify Sample 3 cannot be completed early, repeated selections do not double-count, and each result remains on its separate canvas.
7. Inspect Sample 3. Choose water (desktop 1); verify life (desktop 2) gives retry feedback. Continue to Legacy with its button or Enter after successful completion.
8. Check each canvas in the headset for readability, placement and ray input, confirm the sample-recorded chime, and verify no Console errors. Test Launch -> Landing -> Legacy.

## Verification

Runtime C# compilation passed using Unity's installed Roslyn compiler and project assembly references, including the new teammate XR player setup source. Copied environment block IDs were checked for uniqueness. This does not verify scene rendering, Unity import, Play-mode interaction or headset comfort. Those checks remain necessary in the open Editor.

## Science and credits

This is a simplified educational activity with scripted readings, not simulated physical instruments. Evidence of water is distinguished from proof of life.

- https://www.jpl.nasa.gov/news/opportunity-rover-finds-strong-evidence-meridiani-planum-was-wet/
- https://science.nasa.gov/resource/martian-blueberries-2/
- https://science.nasa.gov/mission/mer-opportunity/

Legacy data credits also apply to Landing: HiRISE terrain NASA/JPL/University of Arizona/USGS; PIA22909 panorama NASA/JPL-Caltech/Cornell/ASU. See the repository README for source links.

## Rover animation limits
The imported panel unfold is reused. Wheel spin, mast sweep, and arm contact/retraction are procedural illustrative motions, not mechanically constrained robotics. The arm solver uses existing joints without stretching them. A small illustrative abrasion-tool head is added. Rover controls pause during instrument actions. Joint limits, reach at each sample, wheel axes, near-camera geometry and terrain camera framing still need Play-mode verification. No new packages were added.


## Automated Play-mode verification (2026-11-15)

The Mars Landscape 3D pack is imported locally. Unity's environment builder completed for Landing, including Mars Far Terrain; the downloaded pack remains excluded from git under the team's licensing workflow.

NasaHack > Landing > Verify playable mission runs the real scene, checks deployment/handoff, arm joints and six wheels, three separate canvases, all three instrument coroutine completions, duplicate prevention, wrong-answer retry, and successful transition to Opportunity-Legacy. Latest run: PASS. Report: landing-playmode-verification.txt in the repository root.

The existing check moves the XR origin programmatically and invokes the same task actions used by the controls. That PASS predates the recent rover-control, scan, XR handoff, and terrain LOD changes above; it does not validate physical controller input, animation-facing, reach/contact appearance, wheel axes, visual panel readability or headset comfort. An unrelated existing Failed to load Editor window message appeared during Game view maximization; this was not a mission-script failure.

Surface grounding now chooses the highest TerrainCollider intersection when the near and far terrains overlap, keeping the rover and task objects on the visible surface.
