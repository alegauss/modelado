# Improvements

## Block A — English everywhere

## Block B — Windows interface built like PhotoBoost

## Block C — Existing code wired into the window

## Block D — Model downloads

## Block E — Publishing custom models to Hugging Face

## Block F — Installer, releases and auto update

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

Checked 11/11/2026 on the host (MOD78's cases-packaged): About names the LocalCache
folder and says downloads go with the app, and a key the CLI alias stored is decrypted
to send a job the window then counts. Left, for a desk with no Modelado data of its own
(the host has the person's models and keys, which a download through the real
redirection would sit beside): Done when, on the registered layout, a model downloads,
About opens the folder holding it and reports its size, and the DPAPI keys still decrypt
after a restart.

### §MOD81 MOD81 — Captioned Store screenshots

Seen on the live listing (11/11/2026): at the Store's thumbnail size the four
screenshots read as one light window with the Export card, the model small in the middle
and no word saying what each shows. claude-tray's listing (T412) composes each capture
under its caption at 1920 x 1080 on a branded backdrop; Modelado follows it, with what a
3D app needs of its own.

- `tools\Modelado.Icon --store-shot <capture> <out> <headline> [<photo>]` draws the backdrop in the
  icon's blues, the brand mark (icon and name) in the top-left corner, the caption as the headline,
  the window below it with rounded corners and a shadow, and, where a photo is given, that photo as a
  card beside the window, so the shot says "this photo became this model".
- `build\store-shots.ps1` captures the window smaller (about 1440 x 900), so the interface is not
  shrunk below legibility, turns each model to a three-quarter view with a drag in the viewport, and
  brings the card the caption is about into view through UI Automation: the 3D Object card, the
  Relief card, Edit and improve with History after a Simplify and a Smooth, and Export with STL in
  millimetres.
- The captions in `build\msix\store-listing.md` become one per card shown, and the raw captures stay
  beside the composed ones.

Done when `artifacts\store-shots` holds the composed screenshots and they are in a
submission.

### §MOD82 MOD82 — Super hero art for the listing

The Store shows a listing's trailers only when the listing also has the 16:9 Super hero
art (3840 x 2160), and Modelado's `--store-art` draws the box, the poster and the tiles
but not that one. claude-tray draws it without text (`hero-16x9-3840x2160.png`), because
the Store sets the title over it. Modelado's should show what the app makes: a generated
model on the icon's blue, not the icon alone, rendered from a demo model through the
same renderer `modelado-cli --preview` uses, so it is redrawn when the renderer changes.
Keep the left third and the bottom quarter quiet, where the Store lays its title and
buttons.

Done when `--store-art` writes `hero-16x9-3840x2160.png` and it is in a submission.

### §MOD84 MOD84 — A trailer for the listing

A 3D modeller's listing shows still pictures of models that can be turned, and the Store
takes up to 15 trailers (MP4 or MOV, 1920 x 1080, under 2 GB, about 60 seconds
recommended, each with a 1920 x 1080 PNG thumbnail), shown at the top of the listing
when the Super hero art is there (MOD82).

The trailer, 30 to 45 seconds, in the screenshots' look (MOD81): a photo opened and Generate 3D
object pressed, the model turning, the View modes switched, a relief turning, a Refine step with its
History, then Export. Made so it can be remade when the interface changes:
- the turning shots are frames drawn by the app's own renderer, the camera stepped a fixed angle per
  frame, not a hand-held screen recording;
- the steps with clicks are captured from the window on the VMware guest, as the screenshots are;
- each part carries its caption in the screenshots' headline style, and ffmpeg joins the frames into
  an H.264 MP4 at 1920 x 1080, 30 fps.

Done when the trailer and its thumbnail are in a submission and play on the listing.

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

## Block K — Translated interface

### §MOD85 MOD85 — English resource file as the base language

Move every visible string in Modelado.App (MainWindow.xaml, Styles.xaml, the
MainWindow.*.cs partials, DownloadText and the dialogs) into Properties/Strings.resx as
the neutral English culture. XAML reads the strings through a markup extension or
x:Static, and code reads them through the generated Strings class. Format strings take
placeholders ({0}) rather than concatenation, because word order changes between
languages. Only the app's interface is in scope. The CLI, the Blender add-on, the site,
the Store listing, log text and exception messages stay English. An architecture test
fails when a XAML Text, Content, Header, ToolTip or Title attribute holds a literal
instead of a resource reference, so a new string cannot skip the file. Because the
English text is unchanged, the winwright cases keep passing without edits.

### §MOD86 MOD86 — Interface language follows Windows, with a setting

App.xaml.cs pins both culture and UI culture to en-US (MOD49, held by CultureTests).
Split the two. CurrentCulture stays invariant-safe for numbers written to files, the
formats MeshExport and the cloud requests depend on. CurrentUICulture takes the language
chosen in settings, which defaults to the Windows display language when a satellite
assembly exists for it, and to English otherwise. The supported list is en, pt-BR, de,
fr, es and zh-Hans. A language change asks for a restart rather than re-binding the live
window. CultureTests is rewritten to hold the split. The winwright cases pin English
through a command-line switch or environment variable, so they keep asserting on English
text whatever the guest's display language. One case changes the setting and checks that
the choice persists. The MSIX manifest declares the shipped languages in Resources so
the Store lists them.
