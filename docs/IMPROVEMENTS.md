# Improvements

## Block A — English everywhere

## Block B — Windows interface built like PhotoBoost

## Block C — Existing code wired into the window

## Block D — Model downloads

## Block E — Publishing custom models to Hugging Face

## Block F — Installer, releases and auto update

### §MOD72 MOD72 — Distributing through the Microsoft Store

From the portfolio review of 30/10/2026 (visibility, `estrategia/portfolio.md`). The
installer is not signed, and the README and the release notes both have to warn about
SmartScreen. The decision for all of the author's Windows apps is the Microsoft Store,
which signs the MSIX package itself, instead of buying a certificate. claude-tray tests
the path first (visibility VS58); this line follows what that test learns.

Modelado's own points to check against the Store's rules: about 1.1 GB of models is
downloaded on first use, to a folder the person can choose; the app updates itself by
downloading and running the installer (Block F), which a Store package must not do, so
that path is switched off in the Store build and updates come from the Store; the
`.glb`, `.obj`, `.ply` and `.stl` file associations and `modelado-cli` on the PATH must
survive packaging, since the Blender add-on calls the CLI; and the optional Meshy and
Ideogram keys are stored per user. If one of these rules out MSIX, the outcome is
written down instead.

Done when Modelado is listed in the Store with the add-on still finding its CLI, or the
reason it cannot be is recorded here and in the product sheet.

### §MOD74 MOD74 — The updater stands down inside the package

MOD30 and MOD31 check GitHub for a newer release and, on a click in About, download the
installer, verify it against SHA256SUMS and run it silently. Inside an MSIX package that
is both against the Store's policy (an app must not update itself outside the Store) and
broken: the installer would write a second copy under `%LOCALAPPDATA%\Programs\Modelado`
beside the package.

Copy claude-tray's `src\Tray\PackageIdentity.cs` (T409): `GetCurrentPackageFullName`
answering ERROR_INSUFFICIENT_BUFFER means the process runs with package identity. Under
a package, `ReleaseCheck` never runs, About hides the update row and says updates come
from the Microsoft Store, and `--releases` is ignored. The unpackaged build is
unchanged.

Done when a case on the registered layout shows About without the update offer and no
request reaches the releases URL, and the installed build still offers it.

### §MOD75 MOD75 — File associations declared in the manifest

`build\installer.iss` registers the `Modelado.Model` ProgID, `OpenWithProgids` for
`.glb`, `.obj`, `.ply` and `.stl`, and `Applications\Modelado.exe\SupportedTypes`. A
packaged app's registry writes land in the package's own hive, where Explorer never
looks, so a Store install would never appear in Open with.

Declare one `uap:FileTypeAssociation` (name `modelado.model`, display name "3D model",
the app's logo) with the four types in the manifest from MOD73. Activation passes the
file as the first argument, which `CommandLine` already reads as `--open`. The
installer's registry entries stay for the unpackaged build.

Done when, on the registered layout, Open with on a `.glb` and an `.stl` lists Modelado
and opening one shows it in the viewer.

### §MOD76 MOD76 — Execution aliases, and the add-on finding them

The Blender add-on's default for Edit in Modelado is
`%LOCALAPPDATA%\Programs\Modelado\Modelado.exe` (`blender\modelado\__init__.py`,
`default_app`), the Inno install folder. A Store install has no such file, and its real
folder under `C:\Program Files\WindowsApps` refuses to be started from outside the
package.

Declare `uap5:AppExecutionAlias` entries in the manifest from MOD73: `Modelado.exe` for
the window and `modelado-cli.exe` for the command line, which needs its own
`Application` element since an alias names an application. The add-on's `default_app`
then tries the install folder first and falls back to the alias under
`%LOCALAPPDATA%\Microsoft\WindowsApps`, so both builds work without the person setting
the path. The add-on stays outside the Store package, as it is today.

Done when, on the registered layout, `modelado-cli --version` runs from a fresh shell
and the add-on's Edit in Modelado opens the window with the Blender object, without the
path set by hand.

### §MOD77 MOD77 — Data folders under the package's file system redirection

`ModelLibrary` downloads to `%LOCALAPPDATA%\Modelado\models` unless a folder is chosen,
and `SettingsFile`, `CloudKeys` and `CloudLedger` sit beside it. In a full trust package
Windows redirects new files under AppData into the package's private `LocalCache`.
Inside the process nothing changes, but About's "open folder" starts `explorer.exe`,
which runs outside the package and sees the real, empty path; the sizes and the
remove-all question would name a folder the person cannot find; and the files go away on
uninstall, which the consent text should say.

Under a package (MOD74's PackageIdentity), name and open the redirected path,
`%LOCALAPPDATA%\Packages\Alegauss.Modelado_d7cpypw1jc0bm\LocalCache\Local\Modelado`, and
say the downloads are removed with the app. A folder the person chose stays outside the
redirection and keeps working. Turning redirection off with `unvirtualizedResources` is
the other way, but it is a second restricted capability to justify, so it is kept only
if this one fails.

Done when, on the registered layout, a model downloads, About opens the folder holding
it and reports its size, and the DPAPI keys still decrypt after a restart.

### §MOD79 MOD79 — Store listing and the first submission

Store ID `9PB86WW5NRR2` (MOD73). Port claude-tray's `build\store-listing-lib.ps1`,
`store-listing-csv.ps1`, `store-shots.ps1` and `store-submit.ps1`: the listing text in
English and Portuguese, screenshots taken from the real window with a generated model in
the viewer, hero and box art drawn by `tools\Modelado.Icon`, and the package sent
through the submission API.

The answers Modelado needs of its own:
- privacy policy URL on the site (Block G), covering the local-only photos, the optional Meshy and Ideogram calls with the person's own keys, and the site's consented analytics;
- certification notes: the ONNX models are data downloaded from Hugging Face with pinned SHA-256 digests after consent, not code; `runFullTrust` is needed for ONNX Runtime with DirectML and for starting Blender;
- the Store's current rules for apps that generate content with AI, checked against the cloud text-to-texture feature, with whatever reporting path they require;
- the age rating questionnaire.

Done when the submission passes certification and Modelado is listed, which closes
MOD72.

## Block G — Public site for 3D modelling

## Block H — Cloud services: Meshy AI for 3D, Ideogram for images and textures

## Block I — Blender integration: AI tools for existing Blender models

### §MOD70 MOD70 — Publishing the add-on on extensions.blender.org

From the portfolio review of 30/10/2026 (visibility, `produtos/modelado.md`). People who
use Blender look for add-ons on Blender's extensions platform (extensions.blender.org)
and install them from inside Blender; the Modelado add-on (Block I) exists only inside
this repository and the release, so that audience never meets it. The add-on already has
a `blender_manifest.toml` in the extensions format, which makes the submission mostly a
matter of meeting the platform's rules.

Points to settle before submitting: the add-on drives `modelado-cli`, an external
Windows program, so the listing must say that Modelado has to be installed and that it
works on Windows only, and the reviewers must accept an add-on that depends on it; every
permission it uses is declared (it writes temporary files, and Meshy's cloud jobs leave
the PC through the CLI); the version and the website point at the release and at the
site; and the screenshots show the round trip (Edit in Modelado, Send to Blender). If
the platform refuses add-ons that need an external program, the refusal is recorded and
the add-on stays in the release.

Those points are settled in `blender/LISTING.md`, and the manifest passes `extension
validate` on every gate; only the upload is left. Done when the add-on is submitted to
extensions.blender.org with the listing written here, or the platform's reason for
refusing it is recorded.

## Block J — Local model quality: past TripoSR

### §MOD80 MOD80 — Exporting and measuring TripoSG

MOD71's evaluation (`docs/local-3d-models.md`) found TripoSG (VAST, MIT) the one newer
open image-to-3D model that passes the licence rule with only standard ONNX operators: a
DINOv2 encoder, a rectified-flow transformer and an SDF VAE decoder.

Sized 11/11/2026 from the repository listing: FP32 weights are 7.5 GB (transformer 5.5,
DINOv2 1.2, VAE 0.9), about 3.8 GB in FP16, so 8 GB is plausible but unmeasured. The
transformer is past protobuf's 2 GB limit even in FP16, so its ONNX needs external data
files, which the manifest, downloader and `meta.json` (one file per variant) do not
handle yet: that is the first slice to design.

Then export the way `scripts\export_models.py` exports TripoSR: one ONNX per part,
checked against PyTorch on the model's test image, FP32 and FP16. The sampling loop and
marching cubes stay in C#. Measure on the host's RTX 4060 with DirectML and on the CPU,
on TripoSR's test photos: peak video memory and RAM, time per object, and the mesh
beside TripoSR's. TripoSG makes no colour; the photo projection colours the front.

Done when the measurements and side-by-side are in `docs/local-3d-models.md` with a
verdict: offer TripoSG as a "high quality" 3D Object (its own line), or record why not
and try Step1X-3D's geometry stage.
