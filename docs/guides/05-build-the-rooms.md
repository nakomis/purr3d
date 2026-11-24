# 05 · Build the rooms (PURR3D-5)

The code is done. Walking into a door fades to black, builds that project's room, and puts you inside it. The floor is a board: one labelled zone per state, left to right in Purr's order, with a crate per task. The room then fetches the project again, and any crate whose task changed slides to its new zone. Walk back into the exit behind you to return to the corridor.

Most of the room is built in code (floor, zones, labels, walls, exit and the fade), so the editor half is small: a crate prefab, an almost empty Room scene, and one component. Allow about 20 minutes.

**Before you start:** pull this branch (`purr3d-5-project-rooms`) and let Unity recompile. The Console should show no red errors.

## 1. Make the task crate prefab

Each task is a 0.5 m crate. Its **pivot is the centre of the box**. In the room you look along +Z, so the crate's front, facing you, is its **−Z** side.

1. **GameObject → 3D Object → Cube**, name it `TaskCrate`, position (0, 0, 0), scale **(0.5, 0.5, 0.5)**.
2. **Add Component → Rigidbody**. Leave Mass at 1 and Use Gravity on. Then **Add Component → Task Crate**.
3. Add two labels as children (right-click `TaskCrate` → **3D Object → Text - TextMeshPro**). The crate is scaled 0.5, so these positions are in its local units: 0.51 is just proud of a face.

   | Child | Position | Rotation | Width × height |
   |---|---|---|---|
   | `Front` | (0, 0, **−0.51**) | (0, **0**, 0) | 0.95 × 0.4 |
   | `Top` | (0, **0.51**, 0) | (**90**, 0, 0) | 0.95 × 0.4 |

   Both: text `HOME-42`, centre / middle alignment, wrapping off, white, and **Auto Size** on (Min 0.2, Max 2). Refs vary from `CV-3` to `PURR3D-10`, and auto size fits each one to the face.

   **Why these rotations:** TMP reads correctly when viewed along +Z, and that's the way you face in the room, so `Front` needs no turn. `Top` is tipped 90° about X to lie flat, reading the right way up from the entrance.
4. Select `TaskCrate` and fill in **Task Crate**:
   - **Ref Labels**: add two elements, and drag in `Front` and `Top`.
   - **Tinted**: add one element, and drag in `TaskCrate` itself (its own Mesh Renderer). Crates take the colour of the door you came through.
5. Drag `TaskCrate` into `Assets/Purr3D/Prefabs/`, then delete it from the scene.

## 2. Make the Room scene

1. **File → New Scene → Empty** (not *Basic*: that brings its own camera and light, and the corridor's would be doubled up). Save it as `Assets/Purr3D/Scenes/Room.unity`.
2. **GameObject → Create Empty**, name it `Room`, position (0, 0, 0). **Add Component → Room Builder**, and drag the `TaskCrate` prefab into **Crate Prefab**.
3. Save the scene (⌘S).
4. **File → Build Profiles → Scene List → Add Open Scenes**. It should list `Corridor` at **0** and `Room` at **1**. Order matters: Play always starts from the corridor.

The Room scene is only a container. `RoomTravel` loads it alongside the corridor at startup and hides it until you go through a door. Later, the art pass can dress it up without touching code.

## 3. Hook the corridor up

1. Open the **Corridor** scene.
2. Select `Corridor` and **Add Component → Room Travel**. Its defaults are fine:
   - **Room Scene**: `Room`
   - **Room Origin**: (300, 0, 0), well away from the corridor
   - **Fade Seconds**: 0.25
3. `CorridorBuilder` finds the `RoomTravel` on the same object by itself. Save the scene.

Doors need no changes: each one now makes its own trigger just in front of the leaf.

## 4. Press Play

Walk into a door. Try a small project first, then HOME, which has 540 tasks.

| You see | Meaning |
|---|---|
| A fade, then a floor of coloured zones with crates on them | Working. Zones are grey (backlog), blue (unstarted), amber (started), green (completed) and dull red (cancelled). |
| The Console says `[Purr] HOME room: 540 tasks, no changes` | The refresh worked and nothing had changed since the corridor loaded. |
| Nothing happens at the door | Is there a `RoomTravel` on `Corridor`? The Console says if it's missing. Is the player still tagged **Player**? |
| `no scene called "Room" in the build list` | Step 2.4. |
| Crates fall through the floor, or spring apart | The crate's scale isn't 0.5, or the prefab has a second collider. |
| Labels read backwards | Check the rotations in step 1.3. |
| A purple zone called *Unknown* | Some tasks are in a state the project doesn't list. That's real data, shown rather than hidden. |

Then the real test, the task's **done when**:

1. Count a few zones against the project's board in Purr's sandbox web UI. The zone labels show their totals.
2. While still in Play mode, move one of that project's tasks to another state. Purr's web UI is read-only for tasks, so use the API with the game's own key (the key never appears on screen). Pick a ref from the room, and a state name from its zone labels:

   ```bash
   REF=SINT-3 STATE="In Progress"
   jq -r '"x-api-key: \(.apiKey)"' UserSettings/PurrSecrets.json \
     | curl -s -H @- -H 'content-type: application/json' -X POST \
         "$(jq -r .baseUrl UserSettings/PurrSecrets.json)/tasks/$REF/state" \
         -d "{\"state\": \"$STATE\"}" | jq "{ref, stateId, updatedBy}"
   ```

   Do it in Play mode, after the corridor has loaded: the game fetches every project at startup, so a change made before pressing Play would already be in the cache and the room would report `no changes`.
3. Walk out through the exit behind you (it's door-coloured), then back in. The crate should hop across into its new zone, and the Console shows `1 moved`.

HOME's Done zone stacks its crates six high rather than running 30 m into the distance. The crates are kinematic: solid to stand on, but physics never moves them, only the game's code does. (They started out fully simulated, but the player's CharacterController pushes like an infinitely heavy object, and crates it bumped flew off into the sky. That was fixed in PURR3D-7.)

When it all works, commit `Room.unity`, the `TaskCrate` prefab and the `.meta` files, or tell me and I'll do it.
