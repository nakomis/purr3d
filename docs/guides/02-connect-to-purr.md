# 02 · Connect to Purr (PURR3D-3)

Do this once guide 01 is done and this branch's `Assets/Purr3D/` is in the project.

## 1. Check it compiles

When Unity regains focus, it imports `Assets/Purr3D/` and compiles two assemblies: **Purr3D.Core** (engine-free) and **Purr3D.Runtime**. The Console should show no red errors.

If you get `The type or namespace name 'Newtonsoft' could not be found`, the Newtonsoft package from guide 01 step 5 is missing.

Unity creates a `.meta` file for every new file and folder. Commit those along with everything else.

## 2. Add PurrService to the scene

1. Open the Playground scene.
2. **GameObject → Create Empty**, and name it `Purr`.
3. **Add Component → Purr Service**.
4. Make sure `UserSettings/PurrSecrets.json` exists. If it doesn't, run `scripts/fetch-api-key.sh`.

## 3. Press Play

The Console should show:

```
[Purr] sandbox at https://purr.sandbox.nakomis.com/api; API key found
[Purr] 45 projects: ADMIN, ATC, BAPP, BAU, BCON, BENCH, BOOT, …
```

That's live Purr data inside Unity, which is PURR3D-3's finish line. Every later feature builds on these two calls:

```csharp
var projects = await PurrService.Instance.Client.GetProjectsAsync();
var tasks    = await PurrService.Instance.Client.GetTasksAsync("HOME");
```

## If it doesn't work

| Console says | Meaning |
|---|---|
| `API key MISSING` | There's no `UserSettings/PurrSecrets.json`. Run `scripts/fetch-api-key.sh`. |
| `GetProjects failed (401): UNAUTHORISED: unknown API key` | The key in the file isn't one Purr knows. Re-run the script. |
| `GetProjects failed (0): no response` | Network trouble, or the base URL is wrong. Check `baseUrl` in the JSON. |
