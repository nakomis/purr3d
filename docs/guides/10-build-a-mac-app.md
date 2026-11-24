# 10 · Build a Mac app (PURR3D-14)

Turn the project into a standalone `Purr3D.app`: one you can double-click, with no Unity open. Then, optionally, sign and notarise it with your Apple Developer ID, to see what Apple's checks actually do.

Allow 20 minutes for the build, plus 15 for signing if you do it.

**Before you start:** pull `main` once this chapter's PR is merged, and let Unity recompile.

## What changed so a build works

The first build (before this chapter) showed three things that work in the editor but not in a build.

| In the build | Why | Fix |
|---|---|---|
| `401 UNAUTHORISED`, no doors | The game looked for its key in `UserSettings/`, next to the project. In a build that path is inside `Purr3D.app`. | A build keeps its key in `~/Library/Application Support/Nakomis/Purr3D/`, and asks for one if it isn't there (below). |
| Magenta walls and floors | Shapes made in code with `CreatePrimitive` get URP's default material in the editor, but Unity's *built-in* one in a build, and URP can't draw that. | Two real material assets, `Resources/Purr3D/Shell.mat` and `Unlit.mat`. |
| Magenta icon tiles and shot bolts | They found their shader by name with `Shader.Find`. A build strips every shader that no asset references. | They use `Unlit.mat` too. Anything in a `Resources` folder always goes into a build, with its shader. |

**The rule of thumb:** if a material comes from an asset, it survives a build. Anything that relies on the editor's defaults, or on finding a shader by name, doesn't.

The player settings are set too: Company Name **Nakomis** and bundle id **`com.nakomis.purr3d`**. Together with the product name, they decide where the key and the logs live.

## The "Connect to Purr" screen

If there's no key, or Purr refuses the one it has, the game shows **Connect to Purr** instead of an empty corridor:

- **Sandbox / Production:** which Purr to talk to.
- **The key field:** paste the `purr3d` key. It's masked; **Show** reveals it.
- **Connect:** asks Purr `GET /me` first. If Purr answers, *"Connected as purr3d."* appears, the key is saved and the corridor loads. If not, the error shows inline and nothing is saved.
- **Forget saved key:** deletes the saved file.
- **F10**, in the corridor: opens the screen again, to switch environment or swap the key.

The key is saved readable only by you (`chmod 600`):
- In the editor: `UserSettings/PurrSecrets.json`.
- In a build: `~/Library/Application Support/Nakomis/Purr3D/PurrSecrets.json`.

It's never logged, and never inside the `.app`. (The macOS Keychain would be better still, but Unity can only reach it through a native plugin. That's for the plugin chapter.)

**Try it in the editor:** rename `UserSettings/PurrSecrets.json` for a moment and press Play. Then put it back, or paste the key in.

**Shortcut:** `scripts/fetch-api-key.sh --player` writes the build's key file as well as the editor's, so a build skips the screen.

## 1. Build

1. **File → Build Profiles**, select **macOS**. Unity calls the platform *Mac*. Check the Scene List: `Corridor` at 0, `Room` at 1.
2. Settings:
   - **Architecture: Intel 64-bit.** This Mac is Intel. *Intel 64-bit + Apple silicon* makes a universal app that also runs natively on newer Macs, but takes twice as long to build.
   - **Development Build: on**, for now. It adds logging and lets the Profiler connect.
   - **Scripting backend** (under Player Settings → Other Settings): **Mono**. It builds quickly. IL2CPP converts the C# to C++ first: slower to build, faster to run.
3. **Build**, and choose a `Builds` folder at the project root. It's gitignored. Out comes `Builds/Purr3D.app`.
4. Open it. If macOS objects that it can't check the app, **right-click → Open → Open**. You won't need to after signing.

You should see **Connect to Purr** (unless you ran `--player`). Paste the sandbox key, Connect, and the corridor loads with grey walls, real icons and cyan bolts. Quit and reopen, and it remembers the key.

**Logs** from the built app: `~/Library/Logs/Nakomis/Purr3D/Player.log`. Every `[Purr]` line ends up there.

## 2. Sign and notarise (optional)

There are three separate things, which are easy to mix up:

- **Code signing** puts a seal on the app. Every file is hashed, and the hashes are signed with your **Developer ID Application** certificate. Change one byte and the seal breaks. The certificate chains up to Apple, so a Mac knows the app came from your account, unchanged.
- **Notarisation** is Apple scanning it. You upload the signed app; an automated service (not a person) checks it for malware and for a proper signature, and issues a **ticket**. **Stapling** attaches the ticket to the app, so it can be checked offline.
- **Gatekeeper** is the check macOS runs when you first open an app, if it was downloaded (*quarantined*). Signed and notarised: it opens quietly. Unsigned: you get the "can't be checked" warning. Apps you built yourself aren't quarantined, which is why your own builds may open anyway.

### One-off setup

```bash
# Your signing identity (from the Developer ID Application certificate in your keychain):
security find-identity -v -p codesigning

# Store notarisation credentials in the keychain. It asks for an app-specific password,
# which you make at appleid.apple.com under Sign-In and Security.
xcrun notarytool store-credentials purr3d --apple-id <your Apple ID> --team-id <TEAMID>
```

If `find-identity` shows no *Developer ID Application* line, make one in Xcode: **Settings → Accounts → Manage Certificates → + → Developer ID Application**.

### Each build

```bash
scripts/sign-and-notarise.sh "Developer ID Application: <Your Name> (<TEAMID>)" purr3d
```

It runs these four steps, and prints each as it goes:
1. **`codesign --options runtime --entitlements macos/Purr3D.entitlements`.** `--options runtime` turns on the **hardened runtime**, which notarisation requires. The hardened runtime normally forbids writing new machine code into memory while running, but that's exactly what Mono does with C#. So `Purr3D.entitlements` grants `allow-jit` and `allow-unsigned-executable-memory`, plus `disable-library-validation` for Unity's own plugins. Without them, the app notarises perfectly and then crashes on launch.
2. **`notarytool submit --wait`:** zips and uploads the app, then waits for Apple's verdict, usually a few minutes.
3. **`stapler staple`:** attaches the ticket.
4. **`spctl --assess`:** asks Gatekeeper. It should say *accepted, source=Notarized Developer ID*.

### Watch the seal break

```bash
echo "tamper" >> Builds/Purr3D.app/Contents/Resources/Data/boot.config
codesign --verify --deep --strict Builds/Purr3D.app   # "a sealed resource is missing or invalid"
```

Rebuild, or sign again, to fix it.

## Done when

- A fresh build with no saved key shows **Connect to Purr**, rejects a bad key inline, and accepts the sandbox key.
- It loads the corridor with grey walls, real icons and cyan bolts, and remembers the key next time.
- Optionally: `spctl --assess` says *Notarized Developer ID*.
