# Improvements

## Block A — English everywhere

## Block B — Windows interface built like PhotoBoost

## Block C — Existing code wired into the window

### §MOD68 Meshopt compression for compact GLBs

Measured shipping MOD66 on the guest: the site's canyon relief
(site/public/demo/studio/relief-landscape.glb, 55,296 vertices, 110,588 triangles) is
2,629,484 bytes as a standard GLB and 1,744,996 compact (KHR_mesh_quantization).
Compact, positions take 8 bytes a vertex (16-bit, padded to 4), normals 4, UVs 4, the
16-bit indices 663 kB and the JPEG texture 195 kB.

Checked when MOD68 was first claimed (2026-10-26): Blender 4.5's glTF importer has no
EXT_meshopt_compression decoder (its add-on carries a Draco extension and nothing for
meshopt), so a compact GLB that required meshopt would stop opening in Blender, which
the Export card's Compact choice promises. Nor can the guest's gate read meshopt output:
it has no Node or browser for three.js's MeshoptDecoder. A codec port checked only
against itself could break every site model with the tests green.

Build, once a reader is in the gate: either meshopt for the site alone, packed at the
site's build (gltfpack -cc in site.yml, which has Node) with three.js's MeshoptDecoder
set on the viewer's GLTFLoader and a browser check of the Studio; or the app's writer
with meshopt kept optional (EXT_meshopt_compression with the fallback buffer, so Blender
still reads the quantized data). Expected: the canyon near 1 MB, the Studio's 5.6 MB
near 3.5.

Done when a browser loads every Studio model from the packed files in a check that runs
on the guest or in CI, and the Studio's download is under 4 MB.

## Block D — Model downloads

## Block E — Publishing custom models to Hugging Face

## Block F — Installer, releases and auto update

## Block G — Public site for 3D modelling

## Block H — Cloud services: Meshy AI for 3D, Ideogram for images and textures

## Block I — Blender integration: AI tools for existing Blender models

### §MOD46 Round trip between Blender and the window

"Edit in Modelado" in the add-on exports the object to a session folder and launches
Modelado.exe --blender-session <folder>, which opens it with its name in the caption. In
that mode the toolbar gains "Send to Blender". It writes the current version to the
session folder and signals the add-on, which polls the folder with a Blender timer and
replaces the object's mesh data as MOD43 does: transform, materials and collections
kept, and one Blender undo step.

Several sends are fine, each a new undo step in Blender. Closing the window ends the
session. In the other direction, the standalone window's Export card gains "Open in
Blender", which finds Blender (registry, then the user's path preference) and starts it
importing the model.

Done when an object goes to the window, gets three edits, is sent back twice, and both
sends undo in Blender in order.
