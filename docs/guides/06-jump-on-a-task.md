# 06 · Jump on a task (PURR3D-6)

Jump onto a crate, and a panel at the bottom of the screen reads it out: the task's ref, title, state and points. Step off, and it fades away.

There's no editor work this time. The panel is built in code, like the fade, and the bit that notices which crate you're on is added to the player when Play starts. The art pass (PURR3D-8) will restyle the panel. Allow about 5 minutes.

**Before you start:** pull this branch (`purr3d-6-jump-on-task`) and let Unity recompile. The Console should show no red errors.

## How it works

Each frame, `TaskStandReader` (on the player) sweeps a small sphere a few centimetres down from Dante's feet. If the first thing it touches belongs to a `TaskCrate`, that's the crate you're standing on. One sweep is cheaper than a trigger on every crate (HOME has 540), and on a stack it finds the crate you're actually on, not the ones underneath.

You have to stop touching a crate for 0.15 s before it counts as stepping off. Without that, a bump or a hop to the next crate would make the panel flicker.

The crates are 0.5 m tall and Dante's step offset is 0.25 m, so you can't walk onto one. You have to jump (**Space**).

## Press Play

Go into any room and jump onto a crate.

| You see | Meaning |
|---|---|
| A dark panel at the bottom: the ref in large type, a coloured strip down the left, the title, a state chip and the points | Working. The strip is the door's colour, and the chip matches the colour of the zone you're standing in. |
| The details change as you hop from crate to crate | Working: it reads whichever crate is underfoot. |
| On HOME's Done stacks, the panel shows the top crate | Working: the sweep stops at the first thing it hits. |
| The panel fades when you jump down onto the floor | Working. It also hides mid-jump, because you're not standing on anything. |
| `No estimate` | The task has no points in Purr. |
| `Unknown state`, with a purple chip | The task's state isn't one the project lists. Its crate is in the purple *Unknown* zone too. |
| Nothing appears | Check the Console for errors, and check that `PlayerArmature` is still tagged **Player**. While playing, `PlayerArmature` should have gained a **Task Stand Reader** component. |

**The done-when test:** jump onto three or four crates in a small room (EYE or SCOPE, say), and check each against the task in Purr's sandbox web UI: same title, state and points. Then step off and check that the panel goes.

When it works, tell me and I'll merge it.
