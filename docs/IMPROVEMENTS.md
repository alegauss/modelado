# Improvements

## Block A — English everywhere

## Block B — Windows interface built like PhotoBoost

## Block C — Existing code wired into the window

### §MOD68 Meshopt compression for compact GLBs

Measured shipping MOD66 on the guest: the site's canyon relief
(site/public/demo/studio/relief-landscape.glb, 55,296 vertices, 110,588 triangles) is
2,629,484 bytes as a standard GLB and 1,744,996 compact (KHR_mesh_quantization).
Compact, positions take 8 bytes a vertex (16-bit, padded to 4), normals 4, UVs 4, the
16-bit indices 663 kB and the JPEG texture 195 kB. MOD66's design hoped for under 1.5 MB
and named this step for a relief still above about 1 MB.

Build: EXT_meshopt_compression on the compact writer's buffer views (meshoptimizer's
vertex and index codecs, ported or bound), with the uncompressed data kept as the
fallback buffer only if a reader without the extension must still open the file;
otherwise declared required, behind the Export card's Compact choice as
KHR_mesh_quantization is. The site's viewer needs three.js's MeshoptDecoder set on its
GLTFLoader (three/addons/libs/meshopt_decoder.module.js), and MeshImport needs the
decoder too, so a compact GLB opens again in the window.

Expected from meshoptimizer's own figures on grids like this one: the index stream to
about a fifth and the vertex streams to about half, which puts the canyon near 1 MB.

Done when the canyon relief saves under 1.2 MB with --compact, opens again in the window
and in Blender (whose importer reads EXT_meshopt_compression), and the site's GLBs are
re-saved with it.

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

### §MOD67 A CLI job in About

MOD65 gave modelado-cli `cloud image-to-3d`, which records each job in the ledger the
window's About reads (%LOCALAPPDATA%\Modelado\cloud-ledger.jsonl). Its evidence stops at
that file: tests/Modelado.Cli.Tests/CliJsonTests.cs and the Blender add-on test assert
CloudLedger.Drift on what the CLI wrote, the call About's "1 job(s), estimated 30,
charged 30" line is built from, but no UI case opens the window on a ledger the CLI
wrote. A winwright fixture cannot run a command before the window launches (WW582).

Build, once WW582 ships: a fixture with LOCALAPPDATA staged empty and --cloud-api
pointing Meshy at the fixture server, whose before-launch commands store the case key
with `modelado-cli cloud key meshy` and send cases/fixtures/photo.png with `cloud
image-to-3d`. The case opens About and reads Text#CloudUsage-meshy for "Meshy AI: image
to 3D: 1 job(s), estimated 30, charged 30." and Button#CloudChip for the month's 30
credits.

Done when that case passes in cases/window.cases.json.
