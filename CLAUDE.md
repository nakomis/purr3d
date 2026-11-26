# Purr3D

A third-person Unity 6 walk through Purr's live data: one long corridor of project doors (most recently active first), and rooms where tasks are objects you can jump on and shoot. Shooting a task moves it to the next state.

## History

The project began on Unreal Engine 5. It moved to Unity on 2026-11-22 because UE 5.8 dropped rendering on Intel Macs, and Martin's machine is a 2019 Intel MacBook Pro (i9-9980HK, Radeon Pro 5500M, 32 GB). The Unreal C++ is kept for reference on the unmerged branch `purr3d-3-api-subsystem` (PR #3, closed). **Check hardware before recommending tools:** this Mac is x86_64.

## Purpose

A fun side project. Martin has used Unity before, so the aim is building the thing and enjoying it, with the ArtCraft art pipeline as the other half. He still does the editor work: scenes, prefabs and the art pass. Claude writes C#, tests and guides.

## Stack

- **Unity 6.6 (`6000.6.10f1`)** with the Universal Render Pipeline, on macOS (Intel). Unity marks Intel as deprecated from 6.6, but it's still supported.
- **`Assets/Purr3D/Core/`** is engine-free C# (`Purr3D.Core.asmdef`, `noEngineReferences: true`): Purr's types, JSON parsing with Newtonsoft, `StateCycle`, `PurrClient`, which talks HTTP only through the `IPurrTransport` port, the corridor logic (`CorridorLoader`, `CorridorOrder`, `ProjectHue`), and the room logic (`RoomLayout` puts each task in a slot on the board; `RoomDiff` says what changed between fetches). Keep `UnityEngine` out of it, so `dotnet test` can exercise it without Unity.
- **`Assets/Purr3D/Runtime/`** is the Unity side: `UnityWebRequestTransport`, settings loading, the `PurrService` MonoBehaviour that scenes use (it loads and caches every project's tasks at startup), the `CorridorBuilder` / `ProjectDoor` components, and the rooms. There is one `Room` scene, loaded additively at startup and reused for every door: `RoomTravel` (on the Corridor object) fades, turns the room to face the way you walked in, teleports the player (CharacterController off, then `CinemachineCore.OnTargetObjectWarped`), and refreshes the project. `RoomBuilder` builds the floor, zones and walls in code, and reconciles `TaskCrate`s by ref. Doors and the exit make their own `PlayerTrigger`s. `RoomTravel` also adds a `TaskStandReader` to the player at runtime (a short sphere-cast down each frame, with a 0.15 s grace before stepping off counts), which drives the code-built `TaskHud`. The panel's text comes from Core's `TaskCard`, and its state chip shares `StateColours` with the floor zones.
- **Shooting (`TaskShooter`)** is added to the player at runtime and enabled only in rooms. It reaches `OnFire` through PlayerInput's *Send Messages*, from the **Fire** action in `StarterAssets.inputactions`. A ray from the camera picks the target, and `ShotBolt` is only the visual. On arrival, the task is moved optimistically: `PurrTask.WithState` goes into `CorridorEntry.ReplaceTask`, then `RoomBuilder.Apply`, then `PurrService.SetStateAsync`. A `PurrApiException` puts the old task back, so the crate flies home. The sounds are synthesised placeholders (`ShotSounds`) until SoundCraft. `PurrService`'s Inspector has a **Send Wrong Api Key** testing checkbox, which spoils the key live in Play mode.
- **The player controller is ours (PURR3D-11).** `Runtime/PlayerController.cs` is a copy of Starter Assets' `ThirdPersonController`, because a package reimport would quietly undo edits to the vendor copy. Our changes are marked `Purr3D:`. They are the double jump (`AirJumps`, `AirJumpHeight`, the takeoff using up the press) and an optional `AirJump` Animator trigger for a flip. **Tools → Purr3D → Swap In Our Player Controller** (`Editor/PlayerControllerSwap.cs`) swaps it onto PlayerArmature, copying the settings property by property with `SerializedObject`. A JSON round trip doesn't work across classes, because `EditorJsonUtility` wraps the data under the class name. Clips go in `Assets/Purr3D/Animations/`, where `AnimationImportSettings` imports them as Humanoid using Dante's avatar, with no loop and the root baked into the pose. Never put clips under `Characters/`, which switches animation import off.
- **Door icons (PURR3D-13).** `Project.icon` is a path such as `/icons/HOME.png`, served from Purr's site root, not `/api`, and it needs no key. Core's `IconUrl.Resolve` makes the URL. `ProjectIcons` (owned by `PurrService`, as `PurrService.Instance.Icons`) downloads them four at a time and caches them by URL. `ProjectDoor` makes its own unlit quad tile and placeholder letter unless the prefab assigns `iconRenderer` / `iconLetter`. `CorridorBuilder` asks for each icon after laying the doors out, and never waits for them. The tile finds its shader with `Shader.Find`, so a player build needs URP/Unlit in Always Included Shaders.
- **sed gotcha:** `&` in a `sed` replacement means "the matched text", so `&lt;br&gt;` in a drawio label gets mangled. Edit drawio labels with a literal replace (Python `str.replace`) instead.
- **C# 9 only** in `Core` and `Runtime`, because that's Unity's ceiling. No file-scoped namespaces, `required`, raw string literals or `init`-only setters in Core. The test project pins `LangVersion` 9 to enforce it.
- Newtonsoft comes from `com.unity.nuget.newtonsoft-json` in Unity, and from NuGet `Newtonsoft.Json` in the test project.
- Art comes from the ArtCraft suite (see README). Working files go in `ArtSource/<App>/`, which is stored in LFS.
- **Surfaces (PURR3D-15):** `ArtSource/MaterialCraft/Purr3DSurfaces.sbs` holds one graph per surface. `scripts/export-surfaces.py` exports them with `materialcraft-cli` into `Assets/Purr3D/Surfaces/Textures/` and packs metallic + smoothness (1 − roughness, in alpha) for URP. `SurfaceImportSettings` sets the imports. The builders make blocks with `TiledBox` (UVs in metres), not scaled cubes, so textures don't stretch.
- **MaterialCraft 0.4** has no noise nodes and no templates beyond empty/PBR: noise is hand-built value noise in pixel processors. Its FX-Map scatter with `rand` put every iteration in one spot, and upscaling a small random image gave flat grey, so don't use those for noise. The MCP is `materialcraft-cli mcp --bridge 7878` against the app started with `--control 7878`; in bridge mode it can't export or save to paths, so export with the CLI from the saved `.sbs`.

## The Purr API

- `scripts/fetch-api-key.sh [--player] [sandbox|prod]` writes `UserSettings/PurrSecrets.json` (`environment`, `baseUrl`, `apiKey`; format in Core's `PurrSecretsFile`). It's per-user and gitignored, so never commit it. The default is sandbox. `--player` also writes the built app's copy, `~/Library/Application Support/Nakomis/Purr3D/PurrSecrets.json`.
- **No key, or a refused one (PURR3D-14):** the game shows its code-built **Connect to Purr** screen (`ApiKeyScreen`, made by `PurrService`). The screen checks the key with `GET /me`, then saves it via `PurrSettings.Save` (`chmod 600`). F10 reopens it in the corridor. `PurrService.UseKey` reconfigures the client and reloads the corridor.
- **Builds:** code-built things use `Purr3DMaterials` (`Resources/Purr3D/Shell.mat`, `Unlit.mat`), never `Shader.Find` or a primitive's default material, because both turn magenta in a player build. Signing: `scripts/sign-and-notarise.sh` with `macos/Purr3D.entitlements` (Mono needs allow-jit, unsigned executable memory, and disable library validation). Guide 10.
- Auth: the `x-api-key` header carries a key issued to the **`purr3d`** actor. Changes made by the game show up as `purr3d` in task activity.
- The full route list is in the Purr repo: `~/repos/nakomis/purr/docs/api.md`. The routes Purr3D uses:
  - `GET /projects`: doors
  - `GET /projects/{prefix}/tasks?all=true&limit=100` (paginated by `cursor`): room contents, fetched for every project at startup, six at a time
  - `POST /tasks/{ref}/state` with body `{"state": "<id>"}`: shooting
- **State cycling (`StateCycle.Next`):** leaving any backlog state goes to the first non-backlog state. Otherwise the task steps by (group, order). Nothing ever moves into `cancelled`, and completed or cancelled tasks stay put. Real projects need these rules: `Triage` is a backlog state sorted after Done, and BCON lists `Published` (completed) first. The API's states array is not in `order`.

- **Corridor order:** the newest task `updatedAt` per project, not the project's own `updatedAt` (most projects still carry the 14 Sep 2026 migration date). If startup gets slow, the fallback is adding `lastActivityAt` to Purr's `GET /projects`.
- Newtonsoft's `JObject.Parse` turns ISO date strings into `Date` tokens. Read dates with `PurrJson`'s `Date` helper, not as strings.

## Git

- `.githooks/` holds the Git LFS hooks plus the drawio→SVG/TOC pre-commit hook. After cloning, run `git config core.hooksPath .githooks`.
- Unity YAML (scenes, prefabs, assets, `.meta`) is stored as text, so Asset Serialization must stay on **Force Text**. Binary assets under `Assets/` and everything in `ArtSource/` go to LFS. Always commit `.meta` files with their assets.
- Feature branches and PRs as usual. Branch and PR names carry the Purr task ref (`PURR3D-n`).

## Testing

- `dotnet test tests/Purr3D.Core.Tests/Purr3D.Core.Tests.csproj` runs the Core NUnit tests. `global.json` pins the .NET 8 SDK, because newer SDKs change `dotnet test` behaviour. The .NET 8 SDK comes from asdf: `asdf set dotnet 8.0.424` in this repo. CI runs the same command on every PR.
- `Runtime` can be compile-checked without opening the editor. Build a throwaway `netstandard2.1` / C# 9 project that includes `Assets/Purr3D/**/*.cs` and references `UnityEngine.CoreModule.dll` and `UnityEngine.UnityWebRequestModule.dll` from `/Applications/Unity/Hub/Editor/6000.6.10f1/PlaybackEngines/MacStandaloneSupport/Variations/mono/Managed/`.
- The Runtime compile check also needs `UnityEngine.PhysicsModule.dll` and `UnityEngine.TextRenderingModule.dll` from the same folder, plus `Unity.TextMeshPro.dll` and `UnityEngine.UI.dll` from the main checkout's `Library/ScriptAssemblies/`. Add `<NoWarn>0649</NoWarn>`, because Unity fills `[SerializeField]` fields that C# thinks are never assigned.
- Editor-side behaviour gets checked in Play mode. There's no Unity in CI.

## Architecture diagrams

Source: `docs/architecture/purr3d.drawio`. The SVG is auto-regenerated on commit by `.githooks/pre-commit`.
