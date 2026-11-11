# Microsoft Store listing — Modelado (MOD79)

What Partner Center asks for when the package is submitted, kept beside the manifest so a
submission and the code it describes are reviewed together. Keep the wording in step with the
README and the site: the pitch and the privacy facts are the same claims. English only, as the
package's resources are (`Resources` in `AppxManifest.xml`) and as the non-goal on a multilingual
interface keeps the app.

The first submission of an app can only be made on the Partner Center page: the submission API
creates later ones by copying the last published. Store ID `9PB86WW5NRR2`.

## Files

| What | Where it comes from |
|---|---|
| Package | `artifacts\msix\Modelado.msix` from `build\build-msix.cmd`, checked with `build\check-msix.ps1 -Layout artifacts\msix\layout -Cases` |
| Screenshots, 1920 x 1080 | `artifacts\store-shots\screenshot-1..4.png` from `build\store-shots-vm.ps1` (after `build\publish.cmd`), taken on the VMware guest |
| Box art 1:1 | `artifacts\store-art\box-1x1-2160x2160.png` |
| Poster art 9:16 | `artifacts\store-art\poster-9x16-1440x2160.png` |
| App tile icons | `artifacts\store-art\tile-300x300.png`, `tile-150x150.png`, `tile-71x71.png` |

The art is drawn by `dotnet run --project tools\Modelado.Icon -c Release -- --store-art artifacts\store-art`.

## Once per submission

**Pricing:** Free. **Markets:** all.

**Category:** Multimedia design.

**Privacy policy URL:** https://alegauss.github.io/modelado/privacy/

**Website:** https://alegauss.github.io/modelado/

**Support contact:** https://github.com/alegauss/modelado/issues

### Notes for certification

Modelado turns a photo into a 3D model with AI models that run locally in ONNX Runtime (on the GPU
through DirectML, or on the CPU). The models are not in the package: the first time an operation
needs one, the window shows what it will download (size, source, licence) and waits for a yes.
They
are data files fetched from huggingface.co/alegauss/modelado-models, each checked against a pinned
SHA-256 before use, not code.

To test: open any photo (the Open button on the toolbar, or drop it on the window), choose Generate
relief, and accept the one-time download of 50 to 100 MB. A relief appears in the 3D viewer in a few seconds; drag to turn it, and use Export to save
it. Any GLB, OBJ, PLY or STL file can also be opened directly, with no download.

The app sends no telemetry and has no account. Meshy AI and Ideogram are optional cloud services
that work only with the user's own API key, entered in About; each job shows what is sent and its
price and waits for consent. Updates come from the Store: the package never checks GitHub for
releases. The execution aliases let the optional Blender add-on and a terminal start `Modelado.exe`
and `modelado-cli.exe`.

### Restricted capability: runFullTrust

> Modelado is a classic Win32 desktop application (.NET 10, WPF) packaged as MSIX, so it runs as
> Windows.FullTrustApplication. It does not elevate, install services or drivers, and runs entirely
> as the signed-in user. It needs full trust for:
> 1. ONNX Runtime with DirectML, which loads native libraries and uses the GPU through DirectX 12 to
>    run the AI models on the user's computer.
> 2. Opening and saving 3D models and photos anywhere the user picks, and the file associations for
>    .glb, .obj, .ply and .stl.
> 3. Starting Blender with the model shown (Open in Blender), and being started by the Blender
>    add-on and from a terminal through the Modelado.exe and modelado-cli.exe execution aliases.
>
> No data is sent to the developer.

### Age rating questionnaire (IARC)

The app has no violence, sexual content, gambling, purchases, user accounts, chat or sharing
between users, and collects no location. The answer to "unrestricted internet access" is no: it
downloads its own models and, with the user's key, calls two cloud services. The photos and models
are the user's own files.

### AI-generated content

The local models reconstruct geometry from the user's own photo. The optional Ideogram texture and
Meshy AI text-to-3D jobs generate content from text the user types, under those services' own
content policies, with the user's own account. Reports about generated content go to the issues
link above. Check this against the Store policy current on the day of submission.

---

## English (en-us)

**Short description**

Turn a photo into a 3D model on your own PC: a whole object or a relief, cleaned up and exported
for printing, games or the web. Nothing is uploaded.

**Description**

Modelado turns photos into 3D models with AI that runs on your own Windows computer, on the GPU when
it has one. Your photos are never uploaded.

One photo of an object becomes a whole coloured model with TripoSR, its sides and back inferred.
Any photo becomes a bas-relief with Depth Anything V2, textured with the photo and ready to print
with a solid base.

Then refine it: simplify, subdivide, smooth, fill holes, remove loose pieces, flatten a base, turn
and mirror, with every step undoable. Real-ESRGAN redraws a texture four times sharper. Open any
GLB, OBJ, PLY or STL to edit it too, and export to GLB, OBJ, PLY or STL sized in millimetres.

Each AI model is downloaded once, after the app says how large it is and where it comes from, and
is checked by SHA-256. A Blender add-on sends objects to Modelado and back. Meshy AI and Ideogram
can be added with your own key, and nothing is sent to them without your yes.

**Product features**

- A whole 3D object from one photo (TripoSR)
- A printable relief from any photo (Depth Anything V2)
- Simplify, subdivide, smooth, fill holes and flatten a base, all undoable
- 4x sharper textures with Real-ESRGAN
- Export to GLB, OBJ, PLY or STL, sized in millimetres for printing
- Runs on your PC, on the GPU through DirectML or on the CPU
- No account and no telemetry; your photos stay on your PC

**Keywords:** 3D model, photo to 3D, 3D printing, relief, GLB, STL, AI

**Copyright and trademark:** © Alegauss. The AI models carry their own licences, listed in About.

**Screenshot captions**

1. Open a photo of an object and make it a whole 3D model
2. A relief from any photo, textured with it
3. A landscape as a relief, ready to export
4. A 3D object made from one photo, ready to export
