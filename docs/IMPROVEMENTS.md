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

## Block D — Model downloads

## Block E — Publishing custom models to Hugging Face

## Block F — Installer, releases and auto update

## Block G — Public site for 3D modelling

### §MOD34 Studio, journey, details and download

Model: PhotoBoost's Studio.tsx, Journey.tsx and Sections.tsx. The Studio is a dark stage
with a tab per operation, each showing a real input and its orbitable result: 3D Object
(TripoSR), Relief (Depth Anything V2: a face, a building, a landscape), Edit and refine
(before and after simplify, smooth and fill holes, with triangle counts), and AI texture
(Real-ESRGAN, before and after). The Journey is the four moves: drop a photo, generate,
refine, export (GLB, OBJ, PLY, STL, and print-ready STL in millimetres).

Then privacy (inference on this PC, only models are downloaded, the opt-in cloud
services of block H named as the one exception), speed (measured timings on a named GPU
and on CPU), and the models and their licenses.

The download button points at the latest GitHub release's installer (MOD29), read at
build time, with its size and version. Every number the page quotes comes from a real
run, written once in content.ts.

Done when every tab shows a real result, and the download link resolves to the current
release.

## Block H — Cloud services: Meshy AI for 3D, Ideogram for images and textures

### §MOD39 Ideogram: a reference image from a prompt

IdeogramProvider is the second ICloudProvider, with the TextToImage capability, over
Ideogram's REST API and the user's key (check its current API docs when built). The drop
zone gains "Generate a photo with Ideogram…", shown only with a key. It takes a prompt
and wraps it in a template that asks for what reconstruction needs: one whole object,
centred, three-quarter view, even light, plain light background, no text. The user sees
the template and can edit it.

It offers Ideogram's aspect ratio, style and seed, and up to four candidates side by
side. The chosen one opens as if it had been dropped, so 3D Object, Relief and Meshy all
take it unchanged. The image is saved beside the session with its prompt and seed in the
PNG's text chunk, so the result can be traced and redone.

Done when a prompt gives four candidates, one becomes a TripoSR model, and its PNG
carries the prompt.

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
