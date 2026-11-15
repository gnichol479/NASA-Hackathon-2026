# NASA Space Apps 2026: Abandoned but not Forgotten

Team: Yarden, Gibson, Dominic, Susan

A VR experience in Unity for students aged 12-18. You arrive on Mars next to a rover NASA left behind, explore it, relive its mission in a flashback through its eyes, then come back to the present to learn why it matters.

**First rover: Opportunity.** InSight and Ingenuity come after the pitch if there's time.

## Deadlines (Sun Nov 15)

| Time | What |
|---|---|
| 12:00 pm | Full Opportunity chain plays start to finish |
| **3:00 pm** | Submit pitch (due 3:30 pm) |
| 4:00-6:00 pm | Pitch session |
| 9:30 pm | Feature freeze. Only bug fixes after this |
| **11:00 pm** | Submit project (due 11:59 pm) |

## Team update (Sat Nov 14)

Hey team, here's where we are.

**Done**
- Project setup: URP, OpenXR, XR Interaction Toolkit, glTFast + Draco (for NASA models), TextMeshPro
- One shared XR Rig used in every scene. XR Device Simulator lets you test without a headset
- All 5 Opportunity scenes exist, are in Build Settings, and are linked in order
- **Arrival** (Yarden): Opportunity in Perseverance Valley, Mars sky, 4 info hotspots, story panel, "See how I got here" button that triggers the flashback and loads Origin
- **Legacy** (Yarden): back in the present, 4 story beats, "Send wake-up command" interaction (it never answers, like the real 2018 dust storm), "Return to Earth" goes back to the title screen
- **Launch** (Dominic): press the red LAUNCH button and watch a Delta II 7925H lift off at night from Cape Canaveral, then it loads Landing. Details in [Launch scene](#launch-scene-dominic) below
- **Rover model** (Dominic): detailed Spirit/Opportunity model with textures and its built-in unfold animation, in `Assets/Opportunity/Models/`
- **Flashback/Origin** (Gibson): rover assembly experience in the facility
- Shared scripts anyone can reuse (see below)

**Your part**
- Gibson: Origin, built as `Opportunity-Flashback` (assemble the rover, systems check, ready for launch)
- Dominic: Launch
- Susan: Landing + hands-on task (rock grinder revealing hematite "blueberries")
- Yarden: Arrival + Legacy

**Open decisions**
- ~~Origin folder~~ Decided: Gibson's `Opportunity-Flashback` (in `NASA-Headquarters/LastSignalFlashback/`) is the Origin part. Arrival links to it and it's in Build Settings. The empty `Opportunity/Origin/` folder can be deleted
- **Gibson:** hook "Ready for Launch" to the chain. Add a Scene Flow object (LoadNextScene, Next Scene = `Opportunity-Launch`) and add `LoadNextScene.Load` to On Launch Handoff Requested
- The real rovers were built at JPL (Jet Propulsion Laboratory), not NASA HQ. A JPL clean room is the accurate setting for Origin
- Quest 2: Quest Link needs a Windows PC with a decent GPU. Who has one for the demo?
- Who owns the pitch slides?

## Scene flow

```
TitleScreen-Night ─ Opportunity → Opportunity-Arrival → Opportunity-Flashback → Opportunity-Launch → Opportunity-Landing → Opportunity-Legacy → TitleScreen-Night
 (mission choice)                    (present)            (flashback)          (flashback)          (flashback)            (present)
                  ├ InSight     → InSight-Launch → TitleScreen-Night
                  └ Ingenuity   → coming soon
```

Every scene has a **Scene Flow** object already pointing to the next scene. When your part is done, call `Load()` on it. That's the only hook you need:
- From a 3D object: add **XR Simple Interactable**, then in its **Select Entered** event drag in Scene Flow → `LoadNextScene.Load`
- From a UI button: **OnClick** → Scene Flow → `LoadNextScene.Load`
- After a video or narration: set **Auto Load After** on Scene Flow (seconds)

## Launch scene (Dominic)

`Assets/Opportunity/Launch/Scenes/Opportunity-Launch.unity`. You stand on a viewing deck facing east, with the rocket on its pad 90 m ahead and the Atlantic behind it.

**What happens**
1. Press the red **LAUNCH** button on the console (poke it, grab it, or point and pull the trigger). Keyboard: **L**
2. T-10 countdown on the big sign to the front-left. Main engine lights at T-3
3. Liftoff: six boosters fire, smoke covers the pad, the engine glow lights up the whole area, controllers rumble
4. The rocket pitches east, drops the six spent boosters, and the three air-lit boosters ignite
5. After about 45 seconds it fades to `Opportunity-Landing` through Scene Flow

**What's in it**
- **Rocket:** a Delta II 7925H built from generated meshes at real size (about 39 m tall), because there was no free Delta II model. Teal first stage, nine white strap-on boosters, white nose fairing
- **Surroundings:** launch pad and umbilical tower, the twin pad next door, blockhouse, hangars, water tower, roads with lamps, palms, scrub, beach and ocean
- **Night sky:** generated stars, Milky Way and a half moon. Floodlights with visible beams light the rocket
- **Sound:** the engine roar is generated in code, so there is no audio file

**Files**
- Scripts in `Assets/Shared/Scripts/Launch/`: `DeltaIILaunch` (countdown and flight), `LaunchButton`, `RocketRumble` (the roar), `FallingDebris` (dropped boosters)
- Everything else is in `Assets/Opportunity/Launch/`: rocket prefab, meshes, materials, textures, post-processing profile

**Rebuilding it**
- The scene is made by a script, `Assets/Shared/Editor/DeltaIIBuilder*.cs`. Menu: **NasaHack > Opportunity > Build Delta II Launch**. It replaces what it built before and saves the scene, so hand edits to those objects are lost on a rebuild
- For a daytime launch, set `NightLaunch = false` in `DeltaIIBuilder.Night.cs` and rebuild
- **NasaHack > Opportunity > Capture Launch Previews** renders stills of the scene to `Library/LaunchPreview/` without entering Play mode

**Heads up for everyone**
- `Assets/Shared/Settings/URP-Asset.asset`: the per-object light limit went from 4 to 8. The launch needs it so the engine glow reaches the ground. It applies to every scene
- The rover model's **Import Visibility** is off. The FBX marks all its parts as hidden, so with it on the rover imports invisible
- The rover itself is not shown in the Launch scene. It is inside the fairing

**Launch facts used in the scene.** Check these against a NASA page before the pitch, as the rule below says:
- Launched July 7, 2003 at 11:18 p.m. EDT (a night launch) from Cape Canaveral, Space Launch Complex 17B
- Rocket: Delta II 7925H ("Heavy"), nine strap-on solid boosters, six lit on the pad and three in flight
- The flight in the scene is sped up. The real timeline is much longer

## InSight launch scene (Dominic)

`Assets/InSight/Launch/Scenes/InSight-Launch.unity`. Same idea as the Opportunity launch, built with the same scripts, for the InSight lander.

- **Rocket:** an Atlas V 401 at real size (about 58 m tall): bronze booster, white Centaur upper stage and nose fairing, no strap-on boosters
- **Setting:** Vandenberg, California, before dawn. Fog, rolling hills, the mobile service tower rolled back from the pad, a gibbous moon
- **Lander on show:** the InSight lander model stands on a lit display beside the viewing deck with a sign, because in the real launch it is hidden inside the nose fairing
- **Model:** `Assets/InSight/Models/InSight-Lander.glb` (imported at real size, about 6 m across)
- **Rebuild:** **NasaHack > InSight > Build Atlas V Launch**. Code is in `Assets/Shared/Editor/DeltaIIBuilder.InSight.cs`
- **In the chain:** the InSight button on the title screen loads it, and it returns to the title screen when the launch ends
- `DeltaIILaunch` now has the sign messages and liftoff acceleration as Inspector fields, so both launches share it. The Opportunity scene keeps its old values

**InSight facts used in the scene.** Check these against a NASA page before showing them:
- Launched May 5, 2018 at 4:05 a.m. PDT from Vandenberg Air Force Base, Space Launch Complex 3E, on an Atlas V 401
- First launch to another planet from the U.S. West Coast. Thick fog hid the rocket from most people watching
- Landed at Elysium Planitia on November 26, 2018. It listened for marsquakes and measured heat from inside Mars

## Title screen with mission choice (Dominic)

`Assets/Shared/Scenes/TitleScreen-Night.unity` is now the first scene in Build Settings, so the game starts there. Yarden's `TitleScreen.unity` is unchanged and still in the project and in Build Settings.

- Dusk on Mars: a blue glow where the sun set, stars, Earth labelled in the sky, Opportunity in the middle distance, and "ABANDONED / BUT NOT FORGOTTEN" above the horizon
- **Three mission cards**, each with its own button:

| Card | Button loads | Keyboard |
|---|---|---|
| Opportunity | `Opportunity-Arrival` | **1** |
| InSight | `InSight-Launch` | **2** |
| Ingenuity | nothing yet: shows "Coming soon" | |

- **Adding Ingenuity (or more InSight parts):** create a scene named `Ingenuity-<Part>.unity`, then run **NasaHack > Build Night Title Screen**. The button picks the mission's `-Arrival` scene if there is one, else `-Launch`, else the first by name, and adds it to Build Settings
- The builder is `Assets/Shared/Editor/TitleScreenBuilder.cs`. It rebuilds the whole scene, so hand edits to it are lost. Its assets are in `Assets/Shared/Title/`

**Changes outside my own scenes, so everyone knows**
- **Build Settings:** `TitleScreen-Night` added first, `InSight-Launch` added last
- **`Opportunity-Legacy`:** the Scene Flow's Next Scene changed from `TitleScreen` to `TitleScreen-Night`, so "Return to Earth" goes back to the mission choice. That one line is the only change
- `InSight-Launch` also returns to `TitleScreen-Night`

## Unity tip

**Edit > Preferences > General > Script Changes While Playing → Recompile After Finished Playing.** Otherwise pulling or editing code while in Play mode breaks the running scene with a wall of errors.

## Getting started

1. `git pull`
2. Open `NASA-Hackathon/` in Unity Hub with **Unity 2022.3.62f1**. First open takes a few minutes while packages import
3. If Unity offers to upgrade anything, say **no**
4. Open **only your own scene** and press Play

**Testing without a headset**
- The XR Device Simulator loads automatically in Play mode. Expand its panel (bottom left of the Game view) to see the controls
- **N**: skip to the next scene
- **Enter**: Next on story panels. **U**: send the wake-up command in Legacy
- In the headset you point the controller ray and pull the trigger instead

## Folder structure

```
Assets/
  Shared/                 used by everyone
    Prefabs/XR Rig.prefab
    Scripts/              LoadNextScene, ScreenFader, FlashbackTransition, NarrationPanel,
                          InfoHotspot, DustCover, Billboard, UiFactory
      Legacy/             WakeUpCommand
    Scenes/TitleScreen.unity
    Settings/             URP asset, Mars Sky, Hotspot material
  Opportunity/
    Models/               rover models, imported once, used by every part
    Arrival/ Origin/ Launch/ Landing/ Legacy/
      Scenes/ Prefabs/ Audio/ Textures/
```

**Rules that keep us from breaking each other's work**
1. **Only edit your own scene.** Two people editing the same scene = merge conflict nobody can fix
2. **Scene names are unique:** `<Rover>-<Part>.unity`. Scenes load by name
3. **Don't copy a script into each rover folder.** Two scripts with the same class name break compilation for everyone. Reusable scripts go in `Assets/Shared/Scripts/`
4. **Models live once** in `Assets/Opportunity/Models/`. Drag them into your scene, don't duplicate them
5. **Move or rename files inside Unity**, never in your file browser (keeps the `.meta` links)
6. **Don't commit `ProjectSettings/` or `Packages/` changes** unless the team agreed. Package installs touch everyone

## Reusable pieces

| Script | What it does | How to use |
|---|---|---|
| `LoadNextScene` | Fades to black and loads the next scene | Already in every scene as Scene Flow. Call `Load()` |
| `FlashbackTransition` | The "memory" look: warm, faded, vignette | Add to any object. **Origin, Launch, Landing: set Start Mode = In Flashback** so the whole flashback looks like a memory |
| `NarrationPanel` | Story text one beat at a time, optional voice clip per beat | Edit the Beats list in the Inspector |
| `InfoHotspot` | Glowing dot that shows an info card when you point at it | Edit Title/Body in the Inspector |
| `DustCover` | Tints a model with Mars dust | `Amount` 0 = clean (flashback), 0.5+ = years on Mars |

**Models**
- `Mars Exploration Rover - Spirit and Opportunity.fbx`: detailed, textured (from Dominic). Best for close-ups
- Arrival and Legacy now use the detailed model, posed deployed (end of its unfold clip), with 19 leftover Blender parts hidden. Hide those same parts if you use the model elsewhere (list in `OpportunitySceneBuilder.cs`, `KnownHiddenParts`)
- `Opportunity-NASA.glb`: low-poly backup

## Git workflow

1. `git pull` before you start and before you push
2. Commit only files in your own folder (and your scene)
3. Keep every file under 50 MB. Compress videos to 720p `.mp4`
4. The `.gitattributes ... not allowed` warnings on every git command are harmless

## Facts and sources

Every fact shown to players must come from a NASA page. Don't guess dates or numbers. Key ones already checked:

- Landed Jan 25, 2004, Meridiani Planum. Planned 90 sols, lasted almost 15 years
- Drove 45.16 km, the first marathon on another world. Climbed a 32-degree slope
- Last contact June 10, 2018 (global dust storm). Team sent more than a thousand commands over 8 months, last on Feb 12, 2019. Mission declared complete Feb 13, 2019
- Final spot: Perseverance Valley, Endeavour Crater rim
- Perseverance passed Opportunity's driving record on Oct 11, 2026
- **Don't use** "my battery is low and it's getting dark". A reporter paraphrased it from the rover's data. Opportunity never sent those words

Sources:
- https://science.nasa.gov/mission/mer-opportunity/
- https://www.jpl.nasa.gov/news/nasas-opportunity-rover-mission-on-mars-comes-to-end/
- https://science.nasa.gov/blogs/perseverance-mars-rover/2026/10/12/nasas-perseverance-sets-off-world-record-for-total-distance-driven/
- 3D models: https://science.nasa.gov/3d-resources/

## Textures and rocks: Mars Landscape 3D (free Asset Store pack)

Not in git (store license). Each person imports it once: open https://assetstore.unity.com/packages/3d/environments/landscapes/mars-landscape-3d-175814, sign in, **Add to My Assets**; in Unity **Window > Package Manager > My Assets > Mars Landscape 3D > Download > Import**. Then **NasaHack > Mars Environment > NASA terrain + Mars Landscape 3D textures and rocks**. Without it the builder falls back to generated textures. Credit: Evgenii Nikolskii.

## Data credits (must appear on the project page)

The Mars environment in Title, Arrival and Legacy is built from real NASA data:

- **Terrain:** HiRISE digital terrain model `DTEEC_018701_1775_018846_1775_U01` (1 m posts), cropped 3.2 km around Opportunity's final position (2.336 S, 354.651 E, Perseverance Valley). Credit: **NASA/JPL/University of Arizona/USGS**. https://www.uahirise.org/dtm/ESP_018701_1775
- **Sky:** Opportunity's last 360-degree panorama, PIA22909 (true color), taken May 13 to June 10, 2018 from the same spot. Credit: **NASA/JPL-Caltech/Cornell/ASU**. https://www.jpl.nasa.gov/images/pia22909-opportunity-legacy-pan-true-color/
- **Rover models:** NASA 3D Resources. https://science.nasa.gov/3d-resources/

Rebuild the environment any time with **NasaHack > Build Mars Environment (Title, Arrival, Legacy)**. Source data and the generated heightmap live in `Assets/Shared/Mars/`. To turn the panorama so it lines up with the terrain, change **Rotation** on `Assets/Shared/Mars/Mars Sky (PIA22909).mat`.

## Known harmless errors

- **"The only standalone targets supported are Windows x64 and OSX with OpenXR"**: shows on Linux. The simulator still works
- **Unity Connect 404 / Project ID request failed**: cloud services link only, doesn't affect the build
