# Purr3D — A first-person Unreal Engine walk through Purr

## Support

If you find this useful, please consider buying me a coffee:

[![Donate with PayPal](https://www.paypalobjects.com/en_GB/i/btn/btn_donate_SM.gif)](https://www.paypal.com/donate?hosted_button_id=Q3BESC73EWVNN&custom=purr3d)

## Table of Contents

<!-- toc -->

- [What is this?](#what-is-this)
- [Architecture Diagram](#architecture-diagram)
- [Repository Layout](#repository-layout)
- [Getting started](#getting-started)
- [ArtCraft pipeline](#artcraft-pipeline)
- [Architecture Diagrams](#architecture-diagrams)
- [Support](#support)

<!-- tocstop -->

## What is this?

[Purr](https://github.com/nakomis/purr) is a serverless task tracker: projects, tasks, cross-project epics and a priorities board. Purr3D is the same data turned into a place you can walk around:

- **The corridor**: one door per project, labelled with its live name and prefix.
- **The rooms**: each project's board laid out on the floor. There is one zone per state, and each task is a physical object sitting in its state's zone.
- **Jump on a task** to read its ref, title, state and points.
- **Shoot a task** to move it to the next state. It flies across the room to its new zone. If the Purr API refuses the change, the task snaps back.

The project is mostly a way to learn Unreal Engine 5, with an art pipeline built on the [ArtCraft](https://getartcraft.com) suite.

## Architecture Diagram

![Architecture](docs/architecture/purr3d.svg)

## Repository Layout

| Path | What lives there |
|---|---|
| `Purr3D.uproject`, `Source/`, `Config/`, `Content/` | The Unreal Engine 5 project, generated from the First Person C++ template |
| `Source/Purr3D/PurrApi/` | `UPurrApiSubsystem`: the C++ client for the Purr REST API, exposed to Blueprints |
| `ArtSource/` | Working files from the ArtCraft apps (MaterialCraft, TextureCraft, SolidCraft, VectorCraft, PhotoCraft, SoundCraft, FilmCraft), stored in Git LFS |
| `docs/guides/` | Step-by-step guides for the editor-side work |
| `docs/architecture/` | The architecture diagram |

## Getting started

1. Install the Epic Games Launcher, then Unreal Engine 5 and Xcode.
2. Clone, then set up hooks and LFS:

   ```bash
   git clone git@github.com:nakomis/purr3d.git
   cd purr3d
   git config core.hooksPath .githooks
   git lfs pull
   ```

   `.githooks/` holds the Git LFS hooks as well as the diagram/TOC pre-commit hook. Pointing `core.hooksPath` anywhere else would quietly stop LFS files being pushed.

3. Create `Config/PurrSecrets.ini`. It is gitignored:

   ```ini
   [/Script/Purr3D.PurrApiSubsystem]
   ApiKey=<a Purr API key for the purr3d actor>
   ```

   As an alternative, export `PURR_API_KEY` before launching the editor. The base URL is set in `Config/DefaultGame.ini` and points at the sandbox Purr by default.

4. Open `Purr3D.uproject`.

## ArtCraft pipeline

| App | Job |
|---|---|
| MaterialCraft | Tileable PBR surfaces for the corridor, doors and room floors. Each project gets its own colour through a Material Instance parameter. |
| SolidCraft | Door frames, the task object and the blaster |
| TextureCraft | Paints those props with MaterialCraft materials and exports GLB for Unreal |
| VectorCraft | Crosshair, HUD and door-sign graphics |
| PhotoCraft | Decals and grime |
| SoundCraft | Footsteps, doors, the shot, and the sounds for a state change and a rejected change |
| FilmCraft | The walkthrough video |

## Architecture Diagrams

`docs/architecture/purr3d.drawio` is the source for the diagram above.
The SVG is auto-regenerated on commit by the pre-commit hook in `.githooks/pre-commit`.

To activate the hook after cloning:

```bash
git config core.hooksPath .githooks
```

## Support

If you find this useful, please consider buying me a coffee:

[![Donate with PayPal](https://www.paypalobjects.com/en_GB/i/btn/btn_donate_SM.gif)](https://www.paypal.com/donate?hosted_button_id=Q3BESC73EWVNN&custom=purr3d)
