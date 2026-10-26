# Improvements

## Block A — English everywhere

## Block B — Windows interface built like PhotoBoost

## Block C — Existing code wired into the window

### §MOD66 Lighter GLB geometry

Measured shipping MOD63: the site's canyon relief
(`site/public/demo/studio/relief-landscape.glb`, 768 px photo, `--grid 192`) went from
4.1 MB to 3.3 MB once its texture became a 195 kB JPEG. MOD63's design had guessed the
geometry at a few hundred kilobytes; it is 3.1 MB. The relief has 55,296 vertices and
110,588 triangles, and MeshExport.WriteGlb writes them at full width: float positions
and normals (663 kB each), float UVs (442 kB) and 32-bit indices (1.33 MB).

Build, in the order of what each saves for what it risks:

1. Indices as UNSIGNED_SHORT (5123) when the mesh has fewer than 65,536 vertices. Core glTF,
   every reader takes it; the canyon's indices halve, about 660 kB.
2. KHR_mesh_quantization: positions as normalized int16 with the node's scale and offset
   restoring the bounds, normals as int8, UVs as uint16. three.js (the site), Blender's
   importer and glTF validators read it; check Windows 3D Viewer before making it the default,
   and keep a float writer behind an Export card choice if it does not.
3. EXT_meshopt_compression only if 1 and 2 leave a relief above about 1 MB.

MeshImport must read whatever MeshExport writes (16-bit indices, the quantized
accessors), so a saved GLB opens again in the window. Done when the canyon relief saves
under 1.5 MB as GLB with its look unchanged in the window and on the site, and the
site's Studio GLBs are re-saved.

## Block D — Model downloads

## Block E — Publishing custom models to Hugging Face

## Block F — Installer, releases and auto update

## Block G — Public site for 3D modelling

### §MOD64 A GPU row in the speed table

MOD34's speed section quotes only CPU timings, measured on the VMware guest (8 cores of
an Intel Core i7-14700, 8 GB): 3D Object about 20 s at grid 256 and 25 to 32 s at 320,
Relief about 1 s, simplify 150,000 to 45,000 triangles under 1 s. Its design asked for a
named GPU beside them, and the guest has no GPU for DirectML, so that row could not be
measured there.

Build: on a Windows machine with a DirectML GPU, run the same commands with --provider
dml on the same photos (site/public/demo/chair.png for 3D Object at --resolution 256 and
320, site/public/demo/studio/relief-face.jpg for Relief), three times each, and note the
GPU's name as the status bar shows it (MOD52's label, which modelado-cli prints on its
first line). Add the rows to words.speed in site/src/content.ts, with the GPU named in
the lead, and drop the note that only promises it.

Done when the speed table names a GPU and its timings come from those runs.

## Block H — Cloud services: Meshy AI for 3D, Ideogram for images and textures

## Block I — Blender integration: AI tools for existing Blender models

### §MOD45 AI actions in the Blender add-on

The add-on's panel gains an "AI" group for the selected object. It has Upscale texture
4× (Real-ESRGAN, local); New texture from prompt (Ideogram); and Remesh and Retexture
(Meshy). The cloud actions show only when the app has the key: the CLI reads the same
DPAPI store as the window, so the add-on never handles a key itself.

Each cloud action shows the same consent line and cost estimate as the window (MOD42's
estimate, via modelado-cli estimate --json), and asks before anything is sent. Its spend
goes into the same ledger, so the usage monitor counts it.

The CLI gains the subcommands these need (upscale-texture already exists; texture
--prompt, meshy remesh, meshy retexture). Textures come back as images packed into the
.blend and wired into the object's existing Principled BSDF Base Color, replacing only
that input, so the rest of the material is kept.

Done when, on the guest, a textured model in Blender gets an upscaled texture and an
Ideogram texture, each undoable, and the cloud action is refused when declined, with no
request made.

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

### §MOD65 Meshy AI from Blender, through the CLI

MOD44 gave the Blender add-on "Generate from photo" with the two local operations, 3D
Object (TripoSR) and Relief (Depth Anything V2), through modelado-cli object and relief.
Its design also named Meshy AI's image to 3D, with a key, and that was left out: the
add-on drives only modelado-cli, and the CLI has no cloud command. The key lives in the
window's DPAPI store (MOD36), the price table and the budget in Modelado.Cloud (MOD42,
MOD55), and the consent line in the window.

Build: a `modelado-cli cloud image-to-3d <photo> -o <out.glb> --service meshy` command
over Modelado.Cloud's CloudJobs, reading the same key store and ledger as the window,
with `--estimate --json` answering the price and the budget left before anything is
sent, and the job followed to the end (or left at the service and said, as the window
does on close). The add-on then offers "3D Object with Meshy AI" when that estimate
answers, shows the price and what is sent in the same dialog MOD44's consent uses, and
imports the result as it imports TripoSR's.

MOD45 (texture and Meshy remesh or retexture from Blender) wants the same CLI door, so
building this one first gives both.

Done when the robot photo becomes a Meshy model at the 3D cursor from Blender with a
stand-in service (the fixture server's Meshy), the price shown before anything is sent,
and the spend appearing in the window's About.
