# 07 · Shoot a task (PURR3D-7)

In a room, a crosshair sits in the middle of the screen. Click, and a bolt flies from Dante's hand to whatever you're aiming at. If it's a crate, its task moves to the next state: the crate hops to its new zone straight away, and the game tells Purr. If Purr refuses, the crate flies back and you hear a buzz. A finished task (completed or cancelled) gives a dull thunk and stays put.

The code is done. Your half is one editor step: adding a **Fire** action to the input actions. Allow about 10 minutes, plus playing with it.

**Before you start:** pull this branch (`purr3d-7-shoot-a-task`) and let Unity recompile. The Console should show no red errors.

## Where the next state comes from

`StateCycle.Next` follows the rules from the start of the project:
- A backlog task jumps to the first state that isn't backlog.
- Anything else steps along to the next state by group, then order.
- Nothing is ever shot into *Cancelled*.
- Done and Cancelled tasks stay where they are.

So shooting a Backlog crate in EYE takes it to *Todo*, then *In Progress*, then *Done*. After that, the thunk.

## 1. Add the Fire action

Starter Assets reads input through the **Input System**. The actions live in an asset, and the PlayerArmature's **Player Input** component turns each action into a message (`OnMove`, `OnJump` and so on) sent to the components beside it. `TaskShooter` has an `OnFire` method waiting, so all it needs is an action called **Fire**.

1. In the Project window, open `Assets/Starter Assets/Runtime/InputSystem/` and double-click **StarterAssets** (the input actions asset). The Input Actions editor opens.
2. In the **Action Maps** column, select **Player**.
3. In the **Actions** column, click **+** and name the new action **Fire** (capital F: it becomes `OnFire`). In its properties, **Action Type** is **Button**.
4. Bind it to the mouse:
   - Select the `<No Binding>` under Fire.
   - In **Path**, click the field, then **Listen**, and click the left mouse button.
   - Or pick **Mouse → Left Button** from the list.
5. Add a gamepad binding (optional):
   - Click the **+** next to Fire, then **Add Binding**.
   - Set **Path** to **Gamepad → Right Trigger**.
6. To match the other actions' control schemes, tick **KeyboardMouse** under **Use in control scheme** for the mouse binding, and **Gamepad** for the trigger.
7. **Save Asset**, at the top of the editor. If **Auto-Save** is ticked, it's already saved.

**Why no wiring:** Player Input's **Behavior** is *Send Messages*. Unity looks for a method called `On` plus the action's name on the components of the same GameObject. `RoomTravel` adds `TaskShooter` to the player when Play starts, so `OnFire` is found by name. If you renamed the action `Shoot`, nothing would happen, because no component has an `OnShoot` method.

## 2. Press Play

Go into a small room (EYE, say). Click in the Game view so it has the mouse, and aim the crosshair at a crate.

| You see / hear | Meaning |
|---|---|
| A white crosshair in the room, but none in the corridor | Working. Shooting is only on in rooms. |
| A zap, a cyan bolt from the hand, and the crate hops to the next zone | Working. The zone counts change too. |
| The Console logs `[Purr] Shot EYE-3: Backlog → Todo` | The shot was sent to Purr. |
| A thunk and a wobble, with no move | The task is Done or Cancelled, so it has nowhere to go. |
| A bolt, but nothing moves | You hit the floor or a wall, not a crate. |
| No bolt at all | Is the action called exactly **Fire**? Did you save the asset? Is the Game view focused? |
| A buzz, the crate flies back, and *"Purr said no to …"* at the top | Purr refused the change. That's the next test. |

Jumping onto a crate you've just shot shows its new state in the HUD.

## 3. The done-when test

**Purr saw it.** Shoot a crate, then check its activity with curl. The command never prints the key. Replace `EYE-3` with the ref you shot:

```bash
REF=EYE-3
jq -r '"x-api-key: \(.apiKey)"' UserSettings/PurrSecrets.json \
  | curl -s -H @- "$(jq -r .baseUrl UserSettings/PurrSecrets.json)/tasks/$REF/activity" \
  | jq '.items[-1] | {at, action, actor, changes}'
```

The newest entry should have `"actor": "purr3d"`.

**A refusal snaps back.**
1. While still in Play mode, select **PurrService** in the Hierarchy. It's under *DontDestroyOnLoad* during Play.
2. Tick **Send Wrong Api Key** under *Testing*. The Console says `Testing: sending a wrong API key`.
3. Shoot a crate. It hops, then flies back, you hear the buzz, and the top of the screen says *"the API key wasn't accepted"*.
4. Untick the box, and shooting works again.

The checkbox resets when Play stops, so you can't leave it on by accident.

Everything happens in **sandbox**, so shoot freely. When you're done, tell me and I'll merge it.
