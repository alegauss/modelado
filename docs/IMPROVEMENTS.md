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

## Block G — Public site for 3D modelling

### §MOD69 MOD69 — A README that leads to the site and the release

From the portfolio review of 30/10/2026 (visibility, `produtos/modelado.md` and
`estrategia/portfolio.md`). Two releases with installers exist, and a site at
alegauss.github.io/modelado, but the README reaches neither: after the opening table it
goes straight to `dotnet build` and `run.cmd`, so a visitor from GitHub concludes the
app has to be compiled. It has no link to the site, and it calls the sibling app
PhotoBoost, linking `Japode/photo-boost`, a name and a repository that product left on
26/10/2026 (it is now Revelei, at `alegauss/revelei`).

The README opens on the person the author confirmed it is for: someone who wants to work
with 3D using AI and save time, rather than one niche (printing, Blender or games).
Right below, a download line points to the latest release's
`Modelado-Setup-<version>.exe` (per user, no admin, the SmartScreen note) and to the
site. Building from source moves to a section for contributors. The sibling mention
becomes Revelei, with its link and the shared line "AI on your PC, nothing leaves it",
matching what Revelei's own pages say about Modelado.

Done when the README's first screen names its reader and links the release and the site,
the build steps come after, and no text names PhotoBoost or `Japode/photo-boost` as the
sibling.

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

Done when the add-on is submitted to extensions.blender.org with the listing written
here, or the platform's reason for refusing it is recorded.

## Block J — Local model quality: past TripoSR

### §MOD71 MOD71 — Evaluating a local model newer than TripoSR

From the portfolio review of 30/10/2026 (visibility, `produtos/modelado.md`). TripoSR
dates from 2024, and the open image-to-3D models published since (Hunyuan3D 2.x, TRELLIS
and their successors) produce clearly better shape and texture, including the back of an
object that TripoSR has to invent. Cloud services (Meshy, Tripo AI) and Microsoft's free
Copilot 3D set the bar a visitor compares against; local quality is the weakest point of
the product.

This is an evaluation, not a migration. For each candidate, record: licence (commercial
use must be allowed, as every model Modelado ships today allows it), whether an ONNX
export runs on ONNX Runtime with DirectML, video memory and RAM needed at a usable
resolution, time per object on an 8 GB GPU and on the CPU, and the result on the same
test photos TripoSR is measured on. The outcome is a short table and a verdict: add one
as a "high quality" option, replace TripoSR, or keep it and look again later.

Done when the table and the verdict are written in this section, and any follow-up is
filed as its own line.
