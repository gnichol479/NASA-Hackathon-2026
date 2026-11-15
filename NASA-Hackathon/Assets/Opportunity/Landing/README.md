# Landing demo

Changed `Scripts/LandingSequenceController.cs` and `Animations/Opertunity_LandingSequence.anim`. Existing scene references, terrain assets, packages and shared scripts are preserved.

The controller builds the airbag payload, orange/white open parachute and suspension lines at runtime. A terrain-sampled anchor aligns the existing position animation with the surface. Y keys at 0, 2, 2.5, 3, 3.5, 4 seconds are 25, 1, 5, 1, 2.5, 1 metres above the anchor. Airbag bottoms sit one metre below the animated pivot. The obsolete renderer-enable animation track was removed. Canopy detaches before the first bounce; the rover still appears after four seconds.

After reveal, the shared ScreenFader masks an XR Origin reposition. Runtime objects provide a stationary XR science panel, an XR Simple Interactable rock, enlarged hematite spherules, and a button to the existing Legacy scene. Terrain receives TeleportationArea when absent; warm fog and sky tint provide a simple horizon. Original authored objects return when Play stops.

## Unity verification still required

1. Open `Assets/Opportunity/Landing/Scenes/Opportunity-Landing.unity` in Unity 2022.3.62f1. Finish any active Play session before scripts recompile.
2. Select `LandingAnimation`. Verify LandingSequenceController fields: Lander = `Oppertunity_Lander`, Opportunity Rover = `Opportunity-NASA`, Landing Duration = `4`. No reassignment should be necessary.
3. Press Play. Check outside-rover starting view, parachute descent, canopy separation, terrain contact, two bounces, and rover reveal at four seconds.
4. Check fade teleport, standing height, free movement/turning and terrain ray teleportation using the XR Device Simulator and headset.
5. Approach `Investigate Water Evidence Rock` beside `Landing Science Panel`. Point the controller ray and select within 3 metres. Desktop alternative: approach and press E. Confirm spheres appear once and discovery text replaces instructions.
6. Select `Continue to Opportunity's legacy` and confirm `Opportunity-Legacy` loads. Verify panel readability, XR UI input, rover wheel placement and no Console errors.

## Validation and limits

Runtime C# compiled successfully with Unity's installed Roslyn compiler and this project's existing assembly references. Separate Unity batch validation could not open the project because an existing Editor holds its lock. Play mode, visual layout, terrain slopes and headset comfort remain unverified. No packages or external assets were added. The four-second landing is a compressed illustration; the brush-to-reveal rock is a simplified activity rather than a simulated grinder or measurement. Hematite dots are enlarged and evidence of water is distinguished from proof of life.

Science checked against NASA:
- https://science.nasa.gov/mission/mer-opportunity/
- https://science.nasa.gov/resource/martian-blueberries-2/
- https://science.nasa.gov/resource/discovery-served-up-in-a-bowl/
