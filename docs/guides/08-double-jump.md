# 08 · Double jump (PURR3D-11)

Press jump again in mid-air and Dante jumps again, once, like in plenty of PlayStation games. Then you add a Mixamo front flip, so the second jump is a somersault.

This chapter is mostly about **taking ownership of starter code**. Starter Assets gave us a working third-person controller, but it's vendor code: reimporting the package would quietly replace it. So Purr3D now has its own copy, `PlayerController`, and that's where the double jump lives. The second half is your first **Animator state machine** edit. Allow about 40 minutes.

## How the double jump works

Starter Assets' jump (in `JumpAndGravity`) sets the upward speed needed to reach `JumpHeight`, `√(JumpHeight × −2 × Gravity)`, but only when you're grounded. In the air, it throws your jump presses away.

`PlayerController` adds three things:
- **A counter**, `AirJumps` (default 1), which refills every time you land.
- **The air jump itself.** A fresh press in mid-air spends one, and *sets* the upward speed for `AirJumpHeight`. It doesn't add to your current speed, so a double jump at the top of a jump goes as high as one made while falling.
- **The takeoff uses up the press.** Otherwise the Space you're still holding would count as "pressed" on the first frame in the air, and spend the air jump straight away. As a bonus, holding Space no longer bunny-hops.

The Input System only sends `OnJump` on a new press, so you have to let go and press again, just like on a PlayStation.

## 1. Swap in our player controller

1. Pull `main` and let Unity recompile. The Console should show no red errors.
2. Open the **Corridor** scene and select **PlayerArmature**.
3. Choose **Tools → Purr3D → Swap In Our Player Controller**.

   This replaces *Third Person Controller* with *Player Controller* and copies every setting across: speeds, Ground Layers, the Cinemachine camera target, and the footstep sounds. The two scripts use the same field names, so the menu item copies the settings with a JSON round trip. It's one Undo step, if you want to see it again.
4. Check the Inspector: **Player Controller** should be there, with a new **Purr3D: double jump** section (Air Jumps 1, Air Jump Height 1.2). There should be no *Third Person Controller*.
5. Save the scene (⌘S).

**Press Play** and double-jump. Dante's jump animation restarts for the second jump. That works, but it's dull. Next, the flip.

## 2. Get a front flip from Mixamo

1. Go to [mixamo.com](https://www.mixamo.com) and sign in (it uses an Adobe account). Upload Dante's FBX as the character, as you did when rigging him, so the preview is him.
2. Search **Animations** for **flip** and pick a front flip you like.
   - If it has an **In Place** option, tick it, so the flip doesn't travel forwards. The game moves Dante; the clip should only move his body.
   - Shorter is better. A double jump lasts about a second, and we'll speed the clip up anyway.
3. **Download**:
   - **Format:** FBX for Unity (.fbx)
   - **Skin:** **Without Skin**. We only want the motion; the mesh is already Dante.
   - **Frames per second:** 30
   - **Keyframe Reduction:** none
4. Create the folder `Assets/Purr3D/Animations/`, and drop the file in as `FrontFlip.fbx`.

### What happens on import

`AnimationImportSettings` (in `Assets/Purr3D/Editor/`) sets anything imported into that folder up the same way every time. It's worth seeing in the Inspector what it did for you:

- **Rig tab:** *Humanoid*, **Avatar Definition:** *Copy From Other Avatar*, Source: *DanteAvatar*. The clip was recorded on Mixamo's skeleton; going through Dante's Humanoid avatar retargets it onto him.
- **Animation tab:** *Loop Time* is off, and all three **Root Transform** rows (Rotation, Position Y and Position XZ) are set to **Bake Into Pose**. The CharacterController owns where Dante is; the clip only moves him relative to that. Without this, the flip would try to drag him forwards, or down through the floor.
- **Materials tab:** *None*. There's no skin, so there's nothing to import.

Click **▶** in the preview at the bottom of the Inspector to watch it on Dante.

**Don't put clips in `Assets/Purr3D/Characters/`.** That folder's rules (`CharacterImportSettings`) switch *Import Animation* off, because a character's own FBX only carries a T-pose.

## 3. Our own Animator Controller

The Animator Controller, the state machine that picks which clip plays, is also Starter Assets' vendor code. Take a copy first:

1. In `Assets/Starter Assets/Runtime/ThirdPersonController/Character/Animations/`, select **StarterAssetsThirdPerson** (the controller) and press **⌘D**.
2. Move the copy into `Assets/Purr3D/Animations/` and rename it **PlayerAnimator**.
3. Select **PlayerArmature**. In its **Animator** component, drag **PlayerAnimator** into **Controller**.
4. Double-click **PlayerAnimator** to open the **Animator** window.

You'll see Starter Assets' states: *Idle Walk Run Blend*, *JumpStart*, *InAir* and *JumpLand*. The arrows between them are transitions, driven by the parameters on the left (*Jump*, *Grounded*, *FreeFall*, …) that `PlayerController` sets every frame.

### Add the flip

1. **Parameters** tab → **+** → **Trigger**, named **AirJump**. Spell it exactly like that: `PlayerController` looks it up by name. Without it, the code falls back to restarting *JumpStart*.
2. Drag **FrontFlip** from the Project window into the graph to make a state, and rename it **AirJump**. In the Inspector, set its **Speed** to about **1.5**. Mixamo flips last over a second, which is longer than a double jump.
3. **Any State → AirJump.** Right-click *Any State* → **Make Transition**, then click *AirJump*. Select the arrow and set:
   - **Has Exit Time:** off (start the flip the moment you press)
   - **Transition Duration:** 0.05
   - **Can Transition To Self:** off
   - **Conditions:** **+** → *AirJump*
4. **AirJump → InAir** (the flip finished, and he's still falling): **Make Transition** from *AirJump* to *InAir*.
   - **Has Exit Time:** on
   - **Exit Time:** 0.9
   - **Transition Duration:** 0.15
   - no conditions
5. **AirJump → JumpLand** (he lands mid-flip): **Make Transition** from *AirJump* to *JumpLand*.
   - **Has Exit Time:** off
   - **Transition Duration:** 0.1
   - **Conditions:** *Grounded* is *true*
6. Save the project (⌘S). Animator edits are assets, and are saved with the project, not the scene.

## 4. Press Play

| You see | Meaning |
|---|---|
| Jump, then jump again in mid-air: a front flip and a second lift | Working |
| A third press does nothing until you land | Working: `AirJumps` is 1 |
| Holding Space jumps once and doesn't keep hopping | Working: the takeoff used up the press |
| Walk off a crate, then press jump while falling | Working: you get the air jump, as in most games |
| The flip plays but Dante drifts forwards or sinks | Check the clip's **Root Transform** rows are all *Bake Into Pose* (section 2) |
| No flip, just the jump animation again | The **AirJump** trigger is missing or misspelt, or PlayerArmature's Animator still points at Starter Assets' controller |
| The flip's cut short, or he finishes it standing | Change the AirJump state's **Speed**, or the exit time to *InAir* |

Then climb HOME's Done stacks a layer or two at a time, which a single jump couldn't manage.

**To tune it:** on *Player Controller*, **Air Jumps** 2 gives you a triple jump, **Air Jump Height** sets how high each one goes, and 0 turns them off.

When it works, commit the scene, `PlayerAnimator.controller`, `FrontFlip.fbx` and their `.meta` files, or tell me and I'll do it.
