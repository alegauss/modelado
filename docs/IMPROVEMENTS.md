# Improvements

## Block A — English everywhere

## Block B — Windows interface built like PhotoBoost

## Block C — Existing code wired into the window

### §MOD62 Simplify on a relief with a base

Seen making the site's Studio (MOD34) on the guest's CPU: `modelado-cli edit --simplify
0.35` on a relief made with `--grid 256 --base 0.03` took 260 s (196,604 to 68,810
triangles, the bust), 102 s (the cathedral) and 75 s (the canyon), where the same edit
on TripoSR's chair took 0.9 s for 150,000 to 45,000. The window's Simplify card runs the
same MeshEdit.Simplify, so a person simplifying a relief waits minutes with nothing to
say why.

The lead, from reading MeshEdit.Simplify: every collapse walks the ring of the vertex it
keeps (`Ring`, `around[a].RemoveAll`) and enqueues an edge to each neighbour, so its
cost grows with that vertex's valence. A relief with a base is a closed solid; if the
base or the walls are triangulated as a fan from a few vertices, those vertices carry
thousands of neighbours, and each collapse next to them costs thousands. Measure before
changing anything: the valence histogram of a relief with and without `--base`, and the
time with and without it.

Build: whatever the measurement names, for example triangulate the base as a
grid-aligned strip instead of a fan in Relief.Build, or skip re-enqueueing a neighbour
whose edge is already queued at the current version. Done when simplifying a 256-grid
relief with a base to a third takes seconds, the same order as a TripoSR mesh of its
size, with a test in Modelado.Geometry.Tests that bounds it.

### §MOD63 JPEG textures in GLB

Seen making the site's Studio (MOD34): reliefs from 768 px photos, at `--grid 192`,
saved as GLB weigh 2.1 MB (the bust), 3.4 MB (the cathedral) and 4.1 MB (the canyon).
Their geometry is a few hundred kilobytes; the rest is the photo, embedded in the GLB as
a lossless PNG. A 768 px JPEG of the same photo is 90 to 200 kB. The web, game engines
and the site's own viewer all read JPEG textures in glTF (image/jpeg is one of the two
types the spec allows), so a relief or a TripoSR result with a projected photo is
several times heavier than it needs to be wherever it is shared.

Build: MeshExport's GLB writer chooses the texture's encoding: JPEG (quality about 90)
for a texture with no transparency, PNG where the alpha is used (a cut-out relief keeps
its transparent background). The Export card can say which it wrote. OBJ keeps its .png,
which slicers and MeshLab read everywhere.

Done when the canyon relief above saves under 1 MB as GLB, opens again in the window
with the same look, and a cut-out relief keeps its transparency.

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
