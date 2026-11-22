# 01 · Create the project (PURR3D-1)

This guide creates the Unity project and merges it into the repo around what's already there: `Assets/Purr3D/` once PURR3D-3 lands, plus `scripts/`, `docs/` and `ArtSource/`.

## 1. Install the editor

1. Open **Unity Hub → Installs → Install Editor** and pick the latest **Unity 6 LTS** (the top of the list). This Mac is Intel. Unity 6 supports Intel Macs, and from 6.6 Unity marks that support as deprecated, but it still works.
2. You don't need any extra modules. Mac Build Support is included.
3. The old 2021.3 and 2022.3 editors can go if you want the disk space back (the arrow beside each → Uninstall).

## 2. Create the project somewhere else

The Hub wants an empty folder, and this repo isn't empty.

1. **Projects → New project**. Pick **Universal 3D** (URP).
2. Set the name to **`Purr3D`** and the location to `~/Desktop/unity-scratch`. Leave Unity Cloud and Version Control unticked, because git handles that here.
3. Click **Create project**, wait for it to open, then close the editor.

## 3. Move it into the repo

```bash
cd ~/repos/nakomis/purr3d
rsync -a --exclude Library --exclude Temp --exclude Logs --exclude UserSettings --exclude obj \
  ~/Desktop/unity-scratch/Purr3D/ ./
git status   # expect Assets/, Packages/, ProjectSettings/
```

Excluding `UserSettings` keeps your `PurrSecrets.json` safe. `Library/` is a cache, and Unity rebuilds it on first open.

## 4. Open it and check two settings

1. **Unity Hub → Projects → Add → Add project from disk**, choose `~/repos/nakomis/purr3d`, then open it. The first import takes a few minutes.
2. **Edit → Project Settings → Editor → Asset Serialization: Force Text.** It's the default, but confirm it. Scenes and prefabs must be YAML text for git.
3. **Edit → Project Settings → Editor → Version Control: Visible Meta Files.** Also the default.

## 5. Add the packages Purr3D needs

**Window → Package Manager**:

1. **+ → Install package by name…** → `com.unity.nuget.newtonsoft-json`. The Purr client parses JSON with it.
2. **+ → Install package by name…** → `com.unity.cloud.gltfast`, for the GLB files from TextureCraft. You'll need it in the art pass, so it can wait.
3. **Unity's Starter Assets first-person controller** (free, made by Unity Technologies). On the Asset Store, search "Starter Assets" from publisher **Unity Technologies**. It has been listed as *Starter Assets – FirstPerson* and, more recently, as a combined *Starter Assets: Character Controllers | URP*. Either works. **Add to My Assets**, then import it in Package Manager → My Assets. It gives you a first-person controller with walking, jumping and the input system set up. Accept the prompt to enable the new Input System and restart.

Open the first-person **Playground** scene it installs (under `Assets/StarterAssets/…/Scenes/`) and press **Play**. You should be able to walk around and jump. That's your first playable thing.

## 6. Commit

```bash
git checkout -b purr3d-1-create-project
git add -A
git lfs ls-files | head    # any PNGs/FBX under Assets/ should be listed: that's LFS working
git commit -m "feat: create the Unity 6 URP project with Starter Assets (PURR3D-1)"
```

Commit every `.meta` file alongside its asset. Unity uses them to keep references intact.
