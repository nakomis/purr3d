# 04 · Swap in Dante (PURR3D-8)

Dante replaces the Starter Assets robot as the player. He's already rigged by Mixamo and sitting at `Assets/Purr3D/Characters/Dante/Dante.fbx`. `CharacterImportSettings` (in `Assets/Purr3D/Editor/`) imports him as a **Humanoid**, which is what lets the robot's walk, run and jump animations play on him unchanged. Allow about 15 minutes.

How he was made: fal.ai produced the head reference and an image-to-3D GLB; `scripts/blender/prep-character.py` scaled him to 1.80 m, put his feet on the origin and cut him to 30k triangles; Mixamo auto-rigged him. The source files are in `ArtSource/Character/Dante/`.

## 1. Check the import

1. Switch to Unity and let it import. You'll see a `Characters/Dante` folder under `Assets/Purr3D/`.
2. Click `Dante.fbx`. In the Inspector's **Rig** tab, **Animation Type** should already say **Humanoid**, because the import script set it.
3. Click **Configure…** (save the scene if asked). The body diagram should be all green. A red bone means Mixamo's skeleton didn't map: tell me which one. Click **Done** to leave.
4. Expand `Dante.fbx` in the Project window. You should see the `Dante` mesh, the `Armature`, a `DanteAvatar` and a material.
5. **Unpack his textures.** They're embedded in the FBX, and until they're extracted he previews grey. Open the **Materials** tab, click **Extract Textures…** and pick the `Assets/Purr3D/Characters/Dante` folder. If Unity offers to fix a normal map, click **Fix Now**. The Inspector's preview at the bottom should then show him in colour.

## 2. Put him inside the player

Do this in the **Corridor** scene.

1. In the Hierarchy, expand **PlayerArmature**. Its children are `PlayerCameraRoot`, `Geometry` (the robot's mesh) and `Skeleton` (the robot's bones).
2. Drag **`Dante.fbx`** from the Project window onto **PlayerArmature** in the Hierarchy, so it becomes a child. Set its Transform to position **(0, 0, 0)** and rotation **(0, 0, 0)**.
3. Select the new **Dante** child. It came with its own **Animator** component: remove it (⋮ → **Remove Component**). Only the Animator on `PlayerArmature` should drive him.
4. Untick **Geometry** and **Skeleton** to hide the robot. Disabling them is enough, and it's easy to undo.
5. Select **PlayerArmature**. On its **Animator** component, set **Avatar** to **DanteAvatar** (the circle picker lists it).
6. Save the scene (⌘S).

These changes are overrides on the Starter Assets prefab inside this scene. Don't **Apply** them to the prefab itself; we'll make a proper Dante prefab once he's settled.

## 3. Press Play

Walk him down the corridor: idle, walk, sprint (Shift or L3) and jump should all play on Dante.

| You see | Meaning |
|---|---|
| He stands in a T-pose and slides about | The Animator's **Avatar** is still the robot's (step 2.5), or the Dante child still has its own Animator (step 2.3). |
| He's half sunk into the floor, or floating | His Transform under `PlayerArmature` isn't (0, 0, 0). |
| Arms twist oddly when he runs | Open the avatar's **Configure…** and use **Pose → Enforce T-Pose**, then **Apply**. |
| He's grey or untextured | The textures weren't extracted (step 1.5). If his material's **Base Map** is still empty afterwards, drag the extracted `texture_diffuse` into it. |
| Long hair stretches when he turns his head | Expected: the hair is part of the head and neck skin. It's for the art pass. |

When he walks, commit the scene and the new `.meta` files, or tell me and I'll do it.

## Later: the art pass

His textures are what the AI made. When you're ready, open `ArtSource/Character/Dante/dante-prep.blend`'s mesh (or `dante-mixamo-rigged.fbx`) in **TextureCraft**, paint MaterialCraft denim, cotton and skin over the top, and export the maps with **OpenGL** normals. Mixamo kept the UVs, so new textures drop straight onto the rigged model.
