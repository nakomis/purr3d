# 03 · Build the corridor (PURR3D-4)

The code is done: at startup `PurrService` fetches every project and all their tasks, sorts the projects by their most recent task, and `CorridorBuilder` lays out one door per project. This guide covers the editor half: a scene, a placeholder door prefab, and wiring it together. Allow about 30 minutes.

**Before you start:** pull this branch (`purr3d-4-corridor`) into your Unity checkout and let Unity recompile. The Console should show no red errors. The `PurrService` on the Playground's `Purr` object loses its *Log Projects On Start* tick box and gains *Load Corridor On Start* and *Max Concurrent Requests*. That's expected.

## 1. Import TextMeshPro's resources

The door labels are TextMeshPro. Its fonts and shaders need importing once per project:

**Window → TextMeshPro → Import TMP Essential Resources**, then **Import**. It creates `Assets/TextMesh Pro/`; commit that folder.

(If you skip this, Unity offers the same import the first time you create a TMP object.)

## 2. Make the Corridor scene

Starting from the Playground keeps the player, cameras and input that already work.

1. Open the Playground scene, then **File → Save As…** to `Assets/Purr3D/Scenes/Corridor.unity` (create the `Scenes` folder).
2. In the Corridor scene's Hierarchy, delete the playground level: the floor, ramps, steps and walls. Keep:
   - `PlayerArmature`
   - `MainCamera` and `PlayerFollowCamera`
   - the `Purr` object from guide 02
   - the Directional Light and any Global Volume
   - anything to do with input or the UI canvas (`UI_Canvas_StarterAssetsInputs_Joysticks` and similar)

   If you're not sure about an object, disable it rather than deleting it. Press Play: you should fall forever. That proves the floor is gone.
3. Put `PlayerArmature` at position **(0, 0, -1)** with rotation **(0, 0, 0)**, so it faces +Z, down the corridor.

## 3. Build a placeholder door prefab

Art comes later (PURR3D-8). For now, cubes. The prefab's **pivot is at the bottom centre of the doorway**, and its **face points along +Z**. The builder turns +Z to face into the corridor.

1. **GameObject → Create Empty**, name it `ProjectDoor`, position (0, 0, 0). **Add Component → Project Door**.
2. Add its children (right-click `ProjectDoor` → **3D Object → Cube** for the first two):

   | Child | Type | Position | Rotation | Scale |
   |---|---|---|---|---|
   | `Frame` | Cube | (0, 1.3, 0.05) | (0, 0, 0) | (1.6, 2.6, 0.1) |
   | `Leaf` | Cube | (0, 1.1, 0.12) | (0, 0, 0) | (1.2, 2.2, 0.05) |
   | `Prefix` | **3D Object → Text - TextMeshPro** | (0, 2.38, 0.11) | (0, **180**, 0) | (1, 1, 1) |
   | `Name` | **3D Object → Text - TextMeshPro** | (0, 1.5, 0.15) | (0, **180**, 0) | (1, 1, 1) |

   **The 180° matters.** TMP text reads correctly when viewed along +Z, from its −Z side. The door's face points +Z towards the viewer, so without the turn the labels would read backwards, facing into the wall.

3. On `Prefix`'s TextMeshPro component: text `HOME`, Font Size **2**, Alignment **centre / middle**, and Rect Transform width **1.4**, height **0.4**. Set its vertex colour to dark grey, so it shows against the frame.
4. On `Name`'s TextMeshPro component: text `Home infrastructure`, Font Size **1.2**, Alignment **centre / middle**, Wrapping **on**, and Rect Transform width **1.0**, height **1.2**. Make it white.
5. Select `ProjectDoor` and fill in the **Project Door** component:
   - **Name Label** → drag in `Name`
   - **Prefix Label** → drag in `Prefix`
   - **Tinted** → add one element and drag in `Leaf`. The frame stays neutral; only the leaf takes the project's colour.
6. Drag `ProjectDoor` from the Hierarchy into `Assets/Purr3D/Prefabs/` (create the folder) to make it a prefab, then delete it from the scene.

## 4. Add the corridor builder

1. **GameObject → Create Empty**, name it `Corridor`, position (0, 0, 0), rotation (0, 0, 0). The corridor runs from here along +Z.
2. **Add Component → Corridor Builder**, and drag the `ProjectDoor` prefab into **Door Prefab**.
3. A loading sign: right-click `Corridor` → **3D Object → Text - TextMeshPro**, and name it `LoadingLabel`. Set position (0, 2, 4), rotation (0, **0**, 0), Font Size 1.5, centre aligned, width 4, height 1. Then drag it into the builder's **Loading Label**. Unlike the door labels it needs no turn, because the player looks at it along +Z, which is the way TMP text reads.
4. With `Corridor` selected, the Scene view shows a gizmo preview: a wireframe of the corridor and 45 coloured door markers. Use it to judge spacing before you press Play. All the defaults (spacing 4 m, width 5 m, height 4 m, first door 6 m in) are on the component.

Leave **Build Shell** on. It generates a floor, walls and end caps sized to however many doors there are. **Build Ceiling** is off by default, because a lid blocks the sun and crowds the third-person camera.

## 5. Press Play

You should see "Loading projects… 12/45" counting up for a couple of seconds. Then the corridor appears with a door per project, alternating left and right. The Console shows:

```
[Purr] sandbox at https://purr.sandbox.nakomis.com/api; API key found
[Purr] corridor: 45 projects in 2.4s: PURR3D, PURR, HOME, …
```

Check the order against Purr: the first door should be the project you touched most recently. The task is done when:

- The doors match sandbox's projects (45 of them, with no archived ones).
- Moving a task in another project (in the sandbox web UI, or through the purr MCP) and pressing Play again brings that project's door to the front.

Then save the scene, and commit the scene, the prefab, `Assets/TextMesh Pro/` and all the `.meta` files.

## If it doesn't work

| You see | Meaning |
|---|---|
| "CorridorBuilder has no door prefab" | Step 4.2: drag the prefab, not the scene object, into **Door Prefab**. |
| "No PurrService in the scene" | The Corridor scene needs its own `Purr` object (guide 02, step 2). |
| "Couldn't reach Purr" and a 401 | The API key is wrong or missing: run `scripts/fetch-api-key.sh`. |
| Doors are there but the labels are invisible or mirrored | Usually the 180° rotation on the door labels (step 3.2), or TMP Essentials weren't imported (step 1). The loading label is the opposite case: it wants 0°. |
| Every door is grey | **Tinted** is empty, or the leaf's material ignores `_BaseColor`. The default URP Lit material honours it. |
| A door says "(offline)" | That project's tasks failed to load. The Console has a `[Purr] PREFIX: …` warning saying why. |
| The camera clips through walls | Raise **Width** on the builder, or add the Cinemachine Deoccluder to `PlayerFollowCamera`. |
