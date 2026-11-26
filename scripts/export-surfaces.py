#!/usr/bin/env python3
"""Exports the surface materials from MaterialCraft into Unity (PURR3D-15).

    scripts/export-surfaces.py [path/to/materialcraft-cli]

1. Runs `materialcraft-cli export` on ArtSource/MaterialCraft/Purr3DSurfaces.sbs, one graph at a time,
   into a temporary folder.
2. Copies the maps URP's Lit shader reads straight into Assets/Purr3D/Surfaces/Textures/:
   <graph>_basecolor.png, <graph>_normal.png and <graph>_ambientocclusion.png.
3. Packs <graph>_metallic.png and <graph>_roughness.png into <graph>_metallicsmoothness.png:
   metallic in red, smoothness (1 - roughness) in alpha. URP wants smoothness, not roughness,
   and reads both from one texture.

Run it again after changing anything in MaterialCraft (and saving), then let Unity reimport.
Needs Pillow and numpy.
"""
import os
import shutil
import subprocess
import sys
import tempfile
import xml.etree.ElementTree as ET

import numpy as np
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SBS = os.path.join(ROOT, "ArtSource", "MaterialCraft", "Purr3DSurfaces.sbs")
OUT = os.path.join(ROOT, "Assets", "Purr3D", "Surfaces", "Textures")
DEFAULT_CLI = os.path.expanduser(
    "~/.local/share/materialcraft-cli/materialcraft-cli-0.4.1-macos-universal/materialcraft-cli")
COPIED = ("basecolor", "normal", "ambientocclusion")


def graph_names(sbs):
    """The material graphs in the package, in file order (the CLI's --graph takes an index)."""
    return [graph.find("identifier").get("v") for graph in ET.parse(sbs).getroot().iter("graph")]


def main():
    cli = sys.argv[1] if len(sys.argv) > 1 else DEFAULT_CLI
    os.makedirs(OUT, exist_ok=True)
    env = dict(os.environ, MATERIALCRAFT_NO_PREFS="1")
    names = graph_names(SBS)
    with tempfile.TemporaryDirectory() as tmp:
        for index, name in enumerate(names):
            subprocess.run([cli, "export", SBS, tmp, "--graph", str(index), "--format", "png", "--depth", "8"],
                           check=True, env=env, stdout=subprocess.DEVNULL)
            for map_name in COPIED:
                shutil.copyfile(os.path.join(tmp, f"{name}_{map_name}.png"), os.path.join(OUT, f"{name}_{map_name}.png"))

            metallic = np.asarray(Image.open(os.path.join(tmp, f"{name}_metallic.png")).convert("L"))
            roughness = np.asarray(Image.open(os.path.join(tmp, f"{name}_roughness.png")).convert("L"))
            zero = np.zeros_like(metallic)
            packed = np.dstack([metallic, zero, zero, 255 - roughness])
            Image.fromarray(packed, "RGBA").save(os.path.join(OUT, f"{name}_metallicsmoothness.png"))
            print(f"{name}: basecolor, normal, ambientocclusion, metallicsmoothness")


if __name__ == "__main__":
    main()
