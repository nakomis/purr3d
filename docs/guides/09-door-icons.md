# 09 · Door icons (PURR3D-13)

Each door now shows its project's icon at the top of the leaf, above the name, just as Purr's web UI shows it beside the name. A project with no icon gets a placeholder: the first letter of its prefix on a darker tile in the door's colour.

There's no editor work. The icon tile is made in code, and the name label on the door prefab has moved down a little to make room. Allow 5 minutes.

**Before you start:** pull this branch (`purr3d-13-door-icons`) and let Unity recompile.

## Where the icons come from

Purr's `GET /projects` gives each project an `icon`, a path such as `/icons/HOME.png`. Icons are plain public files on Purr's site:

- They're served from the site root, not under `/api`, so `https://purr.sandbox.nakomis.com/icons/HOME.png`.
- They need no API key.
- Each is a 128 × 128 PNG of about 19 KB.

`IconUrl` (in Core, with tests) turns the path into that URL. `ProjectIcons` (on `PurrService`) downloads them four at a time with `UnityWebRequestTexture`, and keeps each texture for the session.

The corridor never waits for icons:
1. Every door appears straight away with its placeholder.
2. Each icon swaps in as it arrives.

A missing or broken icon just leaves the placeholder, with one warning in the Console.

## Press Play

| You see | Meaning |
|---|---|
| A moment of lettered tiles, then icons popping in | Working. On sandbox, 33 of the 45 projects have an icon. |
| A letter on a dark tile, for example BCON, CLOUD, TALK | Working: that project has no icon in Purr. |
| `[Purr] icon https://…: …` in the Console | That icon couldn't be fetched. The door keeps its placeholder. |
| Icons mirrored, or no tile at all | Tell me. The tile faces out into the corridor, so it's never seen from behind. |

Compare a few doors with Purr's sandbox web UI: same picture, same project.

**To move or resize the tile:** select the door prefab. On **Project Door**, the *Icon* section has **Icon Centre** and **Icon Size**. To design your own tile, perhaps a framed plaque in the art pass, put a Renderer in the prefab and assign it to **Icon Renderer**, and a text label to **Icon Letter**. The code then uses yours instead of making them.

**Before a player build:** the tile's material is found with `Shader.Find("Universal Render Pipeline/Unlit")`, which works in the editor. A build strips shaders that nothing references. Add that shader under **Project Settings → Graphics → Always Included Shaders**, or assign your own tile with a material.
