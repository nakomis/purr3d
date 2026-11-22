# Purr3D

A first-person Unreal Engine 5 walk through Purr's live data: projects are doors, and tasks are objects you can jump on and shoot. Shooting a task moves it to the next state.

## Purpose

This project is for **learning Unreal**, not shipping a product. Martin does the editor work himself: Blueprints, levels and the art pass. Claude writes C++ and guides, and explains the Unreal concepts as they come up. Don't hand Martin a finished Blueprint recipe when he'd learn more from understanding why it works. `docs/learning-log.md` records what each concept turned out to mean.

## Stack

- Unreal Engine **5.8** (`UE_ROOT="/Users/Shared/Epic Games/UE_5.8"`), First Person C++ template, macOS on Apple Silicon. C++ builds through Xcode.
- `Source/Purr3D/PurrApi/`: `UPurrApiSubsystem` (a GameInstanceSubsystem) wraps the Purr REST API using `HttpModule` and `Json`, and hands `USTRUCT`s to Blueprints through dynamic multicast delegates. Gameplay logic lives in Blueprints.
- Art comes from the ArtCraft suite (see README). Working files go in `ArtSource/<App>/`, which is stored in LFS.

## The Purr API

- Base URL in `Config/DefaultGame.ini`. The default is sandbox: `https://purr.sandbox.nakomis.com/api`. Prod is `https://purr.nakomis.com/api`.
- Auth: the `x-api-key` header carries a key issued to the **`purr3d`** actor. Changes made by the game show up as `purr3d` in task activity. The key lives in the gitignored `Config/PurrSecrets.ini`, or in the `PURR_API_KEY` environment variable. Never commit it.
- The full route list is in the Purr repo: `~/repos/nakomis/purr/docs/api.md`. The routes Purr3D uses:
  - `GET /projects`: doors
  - `GET /projects/{prefix}/tasks?all=true` (paginated by `cursor`): room contents
  - `POST /tasks/{ref}/state` with body `{"state": "<id or name>"}`: shooting
- State cycling: the next state by `order`, stopping at the first state in the `completed` group. Shooting never moves a task into `cancelled`. This logic is pure and unit-tested in `PurrStateCycle`.

## Git

- `.githooks/` holds the Git LFS hooks plus the drawio→SVG/TOC pre-commit hook. After cloning, run `git config core.hooksPath .githooks`.
- `*.uasset`, `*.umap`, `Content/**` binaries and `ArtSource/**` are stored in Git LFS. Keep Starter Content out, because GitHub's LFS quota is small.
- Feature branches and PRs as usual. Branch and PR names carry the Plane ref (`PURR3D-n`).

## Testing

- C++ unit tests are Automation Specs in `Source/Purr3D/Tests/`. Run them headless:

  ```bash
  "$UE_ROOT/Engine/Binaries/Mac/UnrealEditor.app/Contents/MacOS/UnrealEditor" \
    "$PWD/Purr3D.uproject" -ExecCmds="Automation RunTests Purr3D; Quit" \
    -unattended -nullrhi -nosplash -log
  ```

- There's no CI: GitHub-hosted runners can't usefully build Unreal, and nothing is deployed.

## Architecture diagrams

Source: `docs/architecture/purr3d.drawio`. The SVG is auto-regenerated on commit by `.githooks/pre-commit`.
