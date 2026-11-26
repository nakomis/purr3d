# 11 · Surfaces from MaterialCraft (PURR3D-15)

Replace the grey boxes with real materials: painted wall panels, polished concrete, ceiling tiles, a rubber room floor and brushed-steel doors. They're made procedurally in MaterialCraft, from maths rather than photos, so they tile perfectly and stay editable.

Allow 30 minutes, plus as long as you like playing with colours.

## What a PBR material is

A modern material isn't one picture. It's a set of maps, each one answering a different question for every pixel:

| Map | The question | Here |
|---|---|---|
| **Base colour** | What colour is it, with no lighting at all? | Panel paint, concrete, steel |
| **Normal** | Which way does the surface face? | Fakes grooves and bumps without extra geometry |
| **Metallic** | Is it metal (1) or not (0)? | Steel and the ceiling are metal; paint, concrete and rubber aren't |
| **Roughness** | How blurry are its reflections? | Polished concrete is low; rubber is high |
| **Ambient occlusion** | How much light reaches into its crevices? | Darkens the seams |
| **Height** | How far does it stick out? | Only used here to make the normal map |

Two conversions catch everyone:
- **Unity's URP wants smoothness, not roughness.** Smoothness is 1 − roughness, and URP reads it from the *alpha* of the metallic map. `scripts/export-surfaces.py` does that packing.
- **Normal maps come in two flavours.** OpenGL style (green up) or DirectX style (green down). Unity uses OpenGL. The *Normal* node in MaterialCraft has an **OpenGL normals** tick box: keep it on.

## MaterialCraft 0.4: building from primitives

This version of MaterialCraft has no noise generators or ready-made materials. Everything is built from about 25 primitive nodes, the most useful being the **pixel processor**: a tiny program that runs once for every pixel, given its position.

- **Panel seams:** multiply the position by the number of panels, keep the fractional part (where am I within this panel?), and measure the distance to the nearest edge. Near an edge is a groove; further in is the panel. A short ramp in between is the bevel.
- **Per-panel colour:** the same maths, but keep the *whole-number* part (which panel am I in?) and turn it into a random number with a hash: `fract(sin(dot(cell, (12.9898, 78.233))) × 43758.5453)`. Every pixel in a panel gets the same number. It's the seed idea from the box contents: random-looking, but the same every time.
- **Noise:** put a hashed random number at each corner of a grid and blend smoothly between them (*value noise*). Wrap the grid round at the edges, and it tiles. Stack copies at 4, 16, 64 and 256 cells across and you have **fBm** (fractal noise): clouds, mottling, grain and speckle. That's polished concrete. Stretch the grid (2 × 96 cells) and the same noise becomes brushed-metal streaks.

The file is `ArtSource/MaterialCraft/Purr3DSurfaces.sbs`, with one graph per surface: `corridor_wall`, `corridor_floor`, `corridor_ceiling`, `door_face` and `room_floor`.

**Driving it from Claude:** MaterialCraft has an MCP server in its command-line tool (`materialcraft-cli`, a separate download from the app's GitHub releases). Start the app with `open -a MaterialCraft --args --control 7878`, register `materialcraft-cli mcp --bridge 7878`, and an agent can add nodes in your open window while you watch.

## 1. Change a colour

1. Open `Purr3DSurfaces.sbs` in MaterialCraft and click **corridor_wall** in the Explorer.
2. Click **Panel paint** (top row, second node). In **Properties → Specific parameters → Gradient**, click a stop under the bar and change its colour. Each panel takes a random point along this gradient.
3. **File → Save.**

## 2. Export to Unity

```bash
scripts/export-surfaces.py
```

It exports every graph with `materialcraft-cli`, copies the base colour, normal and AO maps into `Assets/Purr3D/Surfaces/Textures/`, and packs metallic + smoothness. Switch back to Unity and it reimports.

`SurfaceImportSettings` (an editor script) sets the imports, so there's nothing to click: `*_normal` becomes a normal map, the data maps are linear (not sRGB), and everything repeats with anisotropic filtering.

## 3. In the game

The five URP Lit materials are in `Assets/Purr3D/Resources/Purr3D/Surfaces/`. The builders use them unless you drag another material into their **Surfaces** slots:

- **Corridor:** concrete floor, panelled walls, and the ceiling tiles if **Build Ceiling** is on.
- **Rooms:** rubber floor, panelled walls, and a brushed-steel exit tinted in the project's colour.
- **Doors:** the door leaf in `ProjectDoor.prefab` is brushed steel, tinted per project as before.

**Why the walls don't stretch:** a scaled cube stretches one copy of the texture across each face, so a 40 m wall would be one 40 m panel. The builders now make their blocks with `TiledBox`, whose UVs are measured in metres: one copy of the texture every **Tile Metres** (2 by default). Change it on the builder to make panels bigger or smaller.

## Done when

- Press Play: the corridor has concrete and panels, not grey, and the panels are the same size along the whole wall.
- Walk into a room: rubber floor, and the exit looks like steel in the project's colour.
- The grooves catch the light as you walk past. If they look flat or lit from the wrong side, check the normal map's import (Texture Type: Normal map).
- A build (guide 10) looks the same as Play mode.
