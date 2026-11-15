# Landing: rover mission and Legacy environment

## Current behavior

- Landing contains copies of Legacy's Mars Terrain, Mars Rocks, Directional Light, and RenderSettings. Asset references, terrain placement, terrain data, rock transforms, lighting and sky settings are shared or copied exactly. The prior Mars_EagleCrater_Terrain and original light are disabled, not deleted. No runtime environment recoloring/scattering is applied.
- This intentionally uses the final-location Perseverance Valley scenery for continuity; it is not a geographically accurate reconstruction of the Eagle Crater landing site.
- The player first watches the existing four-second parachute/airbag landing from outside. The detailed rover then plays its imported solar-panel unfold clip. A fade hands control to an observer above and behind Opportunity. The detailed model matches the asset used in Arrival and Legacy, including hidden leftover parts. Its wheels turn during short driving movements, its mast scans gently, and the arm reaches toward each selected rock, holds tool contact, and retracts. The tracked camera is not animated. Existing XR locomotion is retained.
- A briefing canvas introduces rover mode. Three independent stationary task canvases guide microscopic imaging, mineral analysis, and evidence interpretation. Each has its own concise instructions, instrument action and result. Completed task findings remain at their own stations.
- Sample 1 reveals enlarged hematite spheres. Sample 2 provides a prewritten jarosite instrument reading. Sample 3 examines inconclusive red dust and asks which conclusion is supported. Wrong answers permit retry; correct completion unlocks Legacy.

## Test in Unity 2022.3.62f1

1. Stop Play and let compilation finish. Reopen Opportunity-Landing to load the changed scene from disk. If Unity asks about unsaved scene edits, preserve any newer manual work before reloading.
2. Verify there is one active Mars Terrain and active copied Directional Light; the old Mars_EagleCrater_Terrain is disabled. Compare the sky, rock layout, terrain and light against Opportunity-Legacy.
3. Press Play. Verify observer view, parachute landing, four-second reveal, solar-array deployment, fade, and third-person rover view. Check for geometry near the eyes. LandingAnimation > Third Person Distance defaults to 3 metres and Third Person Height to 2.5 metres. Deployment Seconds defaults to 4. Headset tracking stays active; camera comfort and framing require device testing.
4. Look down to see the rover body; move/turn/teleport and verify body alignment, terrain grounding, headset comfort and free look. This is a simplified rover embodiment with existing locomotion, not wheel physics.
5. Select Begin mission on Rover Briefing Canvas, or press Enter. Approach Sample 1, then use its panel's Focus microscopic imager button, select the rock, or press E with the rover within 1.8 metres. Wait for the reading, then Record spheres / next task (desktop Enter).
6. Approach Sample 2. Run mineral analysis, then record the mineral and advance. Verify Sample 3 cannot be completed early, repeated selections do not double-count, and each result remains on its separate canvas.
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
The imported panel unfold is reused. Wheel spin, mast sweep, and arm contact/retraction are procedural illustrative motions, not mechanically constrained robotics. The arm solver uses existing joints without stretching them. A small illustrative abrasion-tool head is added. Driving providers pause during instrument actions and resume afterward. Joint limits, reach at each sample, wheel axes, near-camera geometry and terrain camera framing still need Play-mode verification. No new packages were added.

