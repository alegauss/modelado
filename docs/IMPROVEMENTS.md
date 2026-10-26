# Improvements

## Block A — English everywhere

## Block B — Windows interface built like PhotoBoost

## Block C — Existing code wired into the window

### §MOD59 Material colours on import

The user opened a model file and it showed without its colours. MeshImport reads three
sources of colour: COLOR_0 or PLY's red/green/blue as vertex colours, glTF's
baseColorTexture, and OBJ's map_Kd. It ignores the rest.

- GLB: a material's baseColorFactor, the flat colour Blender, SketchUp and most CAD exporters write for an untextured material, is never read. The factor that multiplies a texture is dropped too. Only the first primitive's texture is kept (`texture ??=`), so a model with several textured materials maps every primitive's UVs onto that one image.
- OBJ: the .mtl's Kd is never read, and usemtl is ignored, so only the first map_Kd applies to every face.
- PLY: diffuse_red, diffuse_green and diffuse_blue are not recognised.

The fix bakes what a single Mesh cannot hold into what it can. A primitive or usemtl
group with a flat colour gets that colour as its vertex colours, times COLOR_0 where
present. Where several textures exist, they are packed into one atlas with each group's
UVs remapped into its tile; at most four textures, else they are baked to vertex colours
with a notice. Done when a GLB with three plain-coloured materials, a two-texture GLB
and an OBJ with Kd colours each open coloured, read through MeshImport tests and one
window case.

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

### §MOD40 Ideogram: textures from a prompt

The Edit and refine card's "With AI" group gains "New texture with Ideogram…", shown
only with a key and only for a model with UVs (reliefs, OBJ and GLB opened with
textures). It takes a prompt wrapped in a template for a flat, evenly lit, seamless
square texture with no perspective. The result replaces the model's texture as a new
version, so Undo returns to the old one, and Real-ESRGAN can then be applied to it as
today.

A model with no UVs (TripoSR's vertex colours) gets a box or triplanar projection in
Modelado.Geometry, which a test checks for seams. For tiling, Ideogram's result is made
seamless locally (offset and blend at the edges) when the service's own output tiles
badly.

Done when a relief gets a stone texture from a prompt, the seam test passes, and Undo
restores the photo texture.

## Block I — Blender integration: AI tools for existing Blender models

### §MOD44 Generate from a photo into Blender

The add-on's panel gains "Generate from photo". It picks an image file, or an image
already loaded in the .blend, and offers 3D Object (TripoSR, local), Relief (Depth
Anything, local) or, with a key, Meshy image to 3D. It takes the same main options as
the window (resolution, thickness, relief height and base).

The result is imported at the 3D cursor as a new object named after the photo, scaled to
a size the user sets in the panel (default 0.2 m on the longest side), with its vertex
colours or texture in a new material. Models download on first use through the CLI's
download, with progress in Blender's status bar, after the same consent the window asks
for.

Done when the robot photo becomes an object at the cursor at 0.2 m with its colours
visible in Material Preview, from a Blender with no models downloaded yet.

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
