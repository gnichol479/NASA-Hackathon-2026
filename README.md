# NASA Space Apps 2026: Abandoned but not Forgotten

Team: Yayo, Gibson, Dominic, Susan

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
- **Arrival** (Yayo): Opportunity in Perseverance Valley, Mars sky, 4 info hotspots, story panel, "See how I got here" button that triggers the flashback and loads Origin
- **Legacy** (Yayo): back in the present, 4 story beats, "Send wake-up command" interaction (it never answers, like the real 2018 dust storm), "Return to Earth" goes back to the title screen
- **Launch** (Dominic): Delta II night launch, detailed Spirit/Opportunity model with textures + unfold animation
- **Flashback/Origin** (Gibson): rover assembly experience in the facility
- Shared scripts anyone can reuse (see below)

**Your part**
- Gibson: Origin, built as `Opportunity-Flashback` (assemble the rover, systems check, ready for launch)
- Dominic: Launch
- Susan: Landing + hands-on task (rock grinder revealing hematite "blueberries")
- Yayo: Arrival + Legacy

**Open decisions**
- ~~Origin folder~~ Decided: Gibson's `Opportunity-Flashback` (in `NASA-Headquarters/LastSignalFlashback/`) is the Origin part. Arrival links to it and it's in Build Settings. The empty `Opportunity/Origin/` folder can be deleted
- **Gibson:** hook "Ready for Launch" to the chain. Add a Scene Flow object (LoadNextScene, Next Scene = `Opportunity-Launch`) and add `LoadNextScene.Load` to On Launch Handoff Requested
- The real rovers were built at JPL (Jet Propulsion Laboratory), not NASA HQ. A JPL clean room is the accurate setting for Origin
- Quest 2: Quest Link needs a Windows PC with a decent GPU. Who has one for the demo?
- Who owns the pitch slides?

## Scene flow

```
TitleScreen → Opportunity-Arrival → Opportunity-Flashback → Opportunity-Launch → Opportunity-Landing → Opportunity-Legacy → TitleScreen
                 (present)            (flashback)          (flashback)          (flashback)            (present)
```

Every scene has a **Scene Flow** object already pointing to the next scene. When your part is done, call `Load()` on it. That's the only hook you need:
- From a 3D object: add **XR Simple Interactable**, then in its **Select Entered** event drag in Scene Flow → `LoadNextScene.Load`
- From a UI button: **OnClick** → Scene Flow → `LoadNextScene.Load`
- After a video or narration: set **Auto Load After** on Scene Flow (seconds)

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

## Known harmless errors

- **"The only standalone targets supported are Windows x64 and OSX with OpenXR"**: shows on Linux. The simulator still works
- **Unity Connect 404 / Project ID request failed**: cloud services link only, doesn't affect the build
