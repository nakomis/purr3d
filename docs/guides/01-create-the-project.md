# 01 · Create the project (PURR3D-1)

The repo already has the Purr API client in `Source/Purr3D/PurrApi/` and its tests in `Source/Purr3D/Tests/`. What it doesn't have yet is the project those files belong to. This guide generates the project from the First Person C++ template and merges it in around the existing code.

## 1. Install

1. Install the **Epic Games Launcher** from epicgames.com and sign in.
2. Go to **Unreal Engine → Library → +** and install the latest **5.x**. It's about 40 GB, so leave it running.
3. Open Xcode once and accept the licence. Unreal compiles C++ with Xcode's toolchain.

## 2. Generate the template somewhere else

Don't generate straight into the repo. The launcher wants an empty folder, and macOS's case-insensitive file system treats `Purr3D` and `purr3d` as the same folder.

1. Launch UE 5.x, then **Games → First Person**.
2. Choose **C++** (not Blueprint), set Starter Content **off**, and leave the target platform as Desktop.
3. Set the location to `~/Desktop/ue-scratch` and the name to **`Purr3D`**. The name becomes the module name, and the code in this repo assumes `Purr3D` (`PURR3D_API`, `/Script/Purr3D.…`).
4. Click **Create**. It compiles and opens the editor. Close the editor.

## 3. Move it into the repo

```bash
cd ~/repos/nakomis/purr3d
rsync -a --exclude Binaries --exclude Intermediate --exclude Saved --exclude DerivedDataCache \
  ~/Desktop/ue-scratch/Purr3D/ ./
git status   # expect Purr3D.uproject, Config/*.ini, Content/**, Source/*.Target.cs, Source/Purr3D/*
```

`rsync` adds the template's files next to `PurrApi/` and `Tests/` and doesn't touch them.

## 4. Wire in the Purr client

**`Source/Purr3D/Purr3D.Build.cs`**: add the HTTP and JSON modules to the public dependencies:

```csharp
PublicDependencyModuleNames.AddRange(new string[] {
    "Core", "CoreUObject", "Engine", "InputCore", "EnhancedInput",
    "HTTP", "Json"
});
```

Leave whatever else the template lists in place. Only `"HTTP", "Json"` are new.

**`Config/DefaultGame.ini`**: append:

```ini
[/Script/Purr3D.PurrApiSubsystem]
BaseUrl=https://purr.sandbox.nakomis.com/api
```

**The API key**: run `scripts/fetch-api-key.sh`. It writes the gitignored `Config/PurrSecrets.ini` from SSM, using the sandbox key by default.

## 5. Build and test

1. Right-click `Purr3D.uproject` → **Services → Generate Xcode Project**, or run `"$UE_ROOT/Engine/Build/BatchFiles/Mac/GenerateProjectFiles.sh" -project="$PWD/Purr3D.uproject" -game`.
2. Open `Purr3D.uproject`. When it asks to rebuild the module, say yes. If the build fails, open the `.xcworkspace` and build the **Purr3DEditor** scheme to see the errors properly.
3. Run the specs: **Tools → Session Frontend → Automation**, filter on `Purr3D`, tick and run. All should be green. The headless command is in `CLAUDE.md`.
4. Check the Output Log for `Purr API at https://purr.sandbox.nakomis.com/api; API key found`.

## 6. Commit

```bash
git checkout -b purr3d-1-generate-project
git add -A
git lfs ls-files | head     # .uasset/.umap should be listed: that's LFS working
git commit -m "feat: generate the project from the First Person C++ template (PURR3D-1)"
```

## What you just learned

- **Module**: a unit of C++ code with its own `Build.cs`. `Purr3D` is the game's primary module. `PURR3D_API` is the export macro that lets other modules, and the tests, see its classes.
- **UHT (Unreal Header Tool)**: reads `UCLASS`, `USTRUCT`, `UPROPERTY` and `UFUNCTION`, then writes the `*.generated.h` files. That's what makes C++ types visible to Blueprints.
- **GameInstanceSubsystem**: a singleton tied to the game instance. Unreal creates it automatically, and any Blueprint can reach it through **Get PurrApiSubsystem**, so there's nothing to place in a level.
- **Config hierarchy**: `UCLASS(Config = Game)` plus `UPROPERTY(Config)` means `BaseUrl` is read from `DefaultGame.ini`, in the section named after the class's script path.

Add anything that surprised you to `docs/learning-log.md`.
