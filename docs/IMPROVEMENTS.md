# Improvements

## Block A — English everywhere

## Block B — Windows interface built like PhotoBoost

### §MOD94 MOD94 — Recent files

Keep the last ten paths that OpenAsync loaded and that Save wrote, newest first, in the
settings file (SettingsFile) and not in the registry, so the MSIX package and the
portable build behave alike. Show them on the drop zone under Open…, each with the file
name, the folder and a thumbnail for photos, and in a Recent menu beside the Open
toolbar button. A path that no longer exists is shown greyed with "Remove from list"
rather than being dropped silently. When MOD95 lands, generated photos enter the list
through OpenAsync like any other file. When Windows can do so for the package, the same
paths also feed the taskbar Jump List. A case opens two fixture files, restarts the app,
and finds both listed in order.

### §MOD98 MOD98 — An icon rail with one operation shown at a time

MainWindow.xaml stacks 3D Object, Relief, Cloud 3D, Edit and improve, Export, History
and Quality in one ScrollViewer. Add a narrow vertical rail of icons, about 48 px wide,
on the panel's edge next to the viewport, with one Segoe Fluent icon per card, a
tooltip, an AutomationId (Rail-Object, Rail-Relief, …) and a selected state. The panel
shows only the selected card, so the viewport stays visible and usable, unlike a modal,
which the user weighed and set aside because these settings are tuned while watching the
model. The choice persists in SettingsFile. A card's essentials (its main control and
its Generate button) stay on top, and the rest folds into Advanced, as on 3D Object
today. A dot on an icon means its model still needs a download, so the "One-time
download" note leaves the card. Cloud 3D hides with its rail icon when no key is held.
The rail is keyboard reachable (arrows move, Enter selects). Existing winwright cases
first select the rail icon of the card they drive. A new case checks that pressing each
icon shows that card alone, and that the choice survives a restart.

### §MOD99 MOD99 — The logos of the services the app drives

Ship the marks of Meshy AI, Ideogram and Blender as app resources (SVG converted to XAML
DrawingImage, or PNG at 1x, 2x and 4x), in a light and a dark variant where the brand
provides them. Each logo is taken from the owner's press or brand kit and used under
that owner's guidelines: unmodified, kept to its clear space, and never implying
endorsement. The kit's URL and terms are recorded beside each file, and the logos are
credited under Third-party software. Blender's logo follows the Blender Foundation's
logo usage page. Before shipping, ask Meshy and Ideogram whether their terms allow use
in a Store app, and drop any logo whose answer is no. ICloudProvider gains a Logo next
to DisplayName, so every place that names a service can show its mark: the Ideogram
button on the drop zone, the Cloud 3D card, the status-bar budget chip, the consent
lines and the Blender actions. A local model has no product logo, and none is invented
for one. An architecture test checks that every logo file has its source and terms
recorded.

### §MOD100 MOD100 — Settings with pages, About on its own

AboutPanel in MainWindow.xaml is one ScrollViewer with eleven sections in sequence.
Split it in two. The status-bar button "About and models" becomes a gear, Settings, with
a left navigation as in Windows Settings, and About becomes a small page of its own.
Services: one card per service, Meshy, Ideogram and Blender, with its logo from MOD99, a
state chip (Connected, No key, Key rejected), the key's source from MOD96, this month's
spend as a bar against the budget, and Get a key. The usage detail folds under
"Details". Models: one tile per model, with a state badge (Downloaded, Not downloaded,
Downloading %) and size, grouped by the operation that uses it, with Download or Remove
on each tile and one "Download missing" button (MOD97). Folders and model sources fold
under Advanced. Processing: where inference runs, the GPU found, the provider choice.
About: icon, version, update source, licence, third-party software and what is
downloaded. Text shrinks to one line per item, and the explanations move to tooltips or
an info icon. Existing AutomationIds are kept where the control survives, so the cases
only add a navigation step. Cases press each page and find its key control. MOD96 and
MOD97 land inside these pages, whichever ships first.

## Block C — Existing code wired into the window

## Block D — Model downloads

### §MOD97 MOD97 — Every model About knows of, downloaded or not

FillAbout in MainWindow.About.cs builds both the Models list and the Downloads list from
library.Installed(), so a model that was never fetched, or was removed, is simply
absent. Meanwhile, OnAboutDownloadAll fetches ObjectReconstruction.Models,
ReliefReconstruction.Models(true) and Upscaling.Model without saying which ones are
missing. Build the Downloads list from that same set, together with any model from an
added source, and give each row its state: "Downloaded · 800 MB" with Remove, or "Not
downloaded · 800 MB, used by Make 3D" with Download. Rows flip in place while a download
runs. The Download everything button names what it will fetch and its total size
("Download 2 missing, 847 MB") and disables itself when nothing is missing. The Models
list above keeps describing licences, and also shows a missing model as missing. A case
removes one fixture model, finds its row marked as not downloaded with a Download
button, downloads it, and finds the row back to downloaded.

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

### §MOD92 MOD92 — Store listing in six languages

Asked by the owner on 11/11/2026: a Store page in English, Brazilian Portuguese, German,
French, Spanish and Simplified Chinese. Partner Center's "Manage additional languages"
adds listings in languages the package does not declare; each needs a title, a
description and at least one screenshot.

- `build\msix\store-listing.md` holds one section per language (`en-us`, `pt-br`, `de-de`, `fr-fr`,
  `es-es`, `zh-cn`) in claude-tray's shape: short description, description, features, screenshot
  captions, keywords, copyright. Each caption line is the screenshot's headline, stressed words in
  [brackets], then ` | ` and the line under it; the Store gets the headline without brackets.
- Until Block K translates the interface, every non-English description says the interface is in
  English, so nobody installs expecting otherwise; when K ships, that sentence goes and the window is
  captured in each language.
- `build\store-listing-lib.ps1` reads the sections; `store-shots-vm.ps1` captures the window once and
  composes it per language into `artifacts\store-shots\<language>\`, with a CJK font where the text
  needs one; `build\store-listing-csv.ps1`, after claude-tray's, fills Partner Center's exported
  listing CSV from the file, since its import takes text but refuses images named by file.

Done when the six listings are live on the Store page.

## Block G — Public site for 3D modelling

### §MOD101 Running without NVIDIA is why Modelado is found

Modelado's audience arrived on its own from 03/11/2026, mostly without a referrer; the
site's main named source is chatgpt.com. A test on 12/11/2026 found the question behind
it: asked for a "free offline app photo to 3D model Windows without NVIDIA GPU / AMD /
CPU only", ChatGPT puts Modelado first as the "best fit", for TripoSR and Depth Anything
V2 over DirectML. Asked the same without the GPU clause, it recommends Modly first,
which needs an NVIDIA card with 8 GB or more, and never names Modelado.

So running without NVIDIA is the reason Modelado is found, and the public text hides it.
The same answer marks Modelado "Check build" in its CPU-only column, while Presidium
Shape Studio, which states CPU-only operation in plain words, gets "Yes" and is the one
ChatGPT tells a CPU-only machine to test first. The facts are already measured, but
buried: line 96 of the README, and mid-page on the site.

What changes is where it is said: the first screen of the README, the site's title,
description and hero, the Store listing's first lines, and an llms.txt for the site,
each stating that no NVIDIA GPU is needed, that AMD and Intel graphics run through
DirectML, and that a CPU-only PC works, more slowly, with the measured time. Done when
ChatGPT, asked the CPU-only question again, can answer "Yes" in that column from the
page itself.

## Block H — Cloud services: Meshy AI for 3D, Ideogram for images and textures

### §MOD93 MOD93 — The Ideogram button once its panel is open

OnDropGenerate in MainWindow.Generate.cs only toggles GeneratePanel's visibility, and
the button keeps the same text, "Generate a photo with Ideogram…", in both states. With
the panel open, the button sits above the fields it already showed, and a second press
either collapses them with no visible cue or, as the user saw, appears to do nothing.
Reproduce this first on the guest, because the report says nothing happens, while a
collapse should still be visible. While the panel is open, the button either hides or
becomes an explicit "Hide" or "Cancel" toggle, and the panel takes focus on its first
field ("The object"). A winwright case presses the button, finds the panel and the
prompt field focused, presses the toggle, and finds the panel collapsed and the button
restored.

### §MOD95 MOD95 — Keep every generated photo where the user can find it

ShowCandidatesAsync keeps the candidates in memory only. OpenCandidateAsync writes only
the pressed candidate, to %LOCALAPPDATA%\Modelado\generated, which the MSIX package
virtualises under the package's LocalCache, and nothing in the window names that folder.
Write every candidate as soon as the job returns, carrying its prompt and seed in the
PNG as today, to Pictures\Modelado (the user's own library, visible outside the
package), and name the files after the prompt and the seed. The drop zone gets a
"Generated photos" strip of the most recent ones, which open as if dropped, plus a link
that opens the folder in Explorer. A job that fails to save says where it tried to
write, and the images stay in memory until the user saves or discards them. Cases check
that all four candidates of a fixture job land on disk before any is pressed, and that
the strip lists them.

### §MOD96 MOD96 — Where each cloud key comes from

FillCloudServices in MainWindow.Cloud.cs prints "A key is stored." whenever
CloudKeys.Has is true, then always shows an empty PasswordBox with Save and test. The
stored key is the DPAPI file under %LOCALAPPDATA%\Modelado, written by About or by the
CLI. Nothing reads MESHY_API_KEY or IDEOGRAM_API_KEY, although a user with those
variables set expects them to be used. CloudKeys resolves a key from the environment
variable first (MESHY_API_KEY, IDEOGRAM_API_KEY), then from the stored file, in the app
and in the CLI alike, and reports which source answered. About states the source and the
key's last four characters: "Using IDEOGRAM_API_KEY from the environment (…a1b2)" or
"Stored by Modelado (…a1b2)". When the key comes from the environment, the box and
Remove are hidden, and a line says to change the variable instead. When it is stored,
the box is collapsed behind a "Replace key…" button. Tests cover the order of
precedence, and a case with an environment key finds the source line and no box.

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

### §MOD87 MOD87 — Brazilian Portuguese (pt-BR)

Add Strings.pt-BR.resx with every key from the English file. Terms of the trade (mesh,
GLB, OBJ, STL, texture, prompt) follow the usage of Blender's pt-BR translation, and
product names are never translated. A test checks that the file has the same keys and
the same {n} placeholders as the English one, a check that every language task reuses.
One winwright case launches in pt-BR and confirms that the window title and the main
buttons render without clipping. A native speaker reviews the text before the task
ships.

### §MOD88 MOD88 — German (de)

Add Strings.de.resx, held by the key and placeholder parity test from MOD87. German
strings run 30 to 40 percent longer than English, so buttons, the side panel and the
status bar may need to grow or wrap rather than clip. Fix any width that MOD87 did not
expose in the shared XAML, not in a German-only style. A winwright case launches in
German and checks the main window for clipped text. A native speaker reviews the text
before the task ships.

### §MOD89 MOD89 — French (fr)

Add Strings.fr.resx, held by the key and placeholder parity test from MOD87. French puts
a non-breaking space before colons, question marks and exclamation marks, so strings
that end in those characters carry the space in the resource rather than relying on the
layout. A winwright case launches in French and checks the main window for clipped text.
A native speaker reviews the text before the task ships.

### §MOD90 MOD90 — Spanish (es)

Add Strings.es.resx as neutral Spanish, held by the key and placeholder parity test from
MOD87. Avoid regional terms, so that es-ES and es-MX both fall back to it without a
regional file. A winwright case launches in Spanish and checks the main window for
clipped text. A native speaker reviews the text before the task ships.

### §MOD91 MOD91 — Simplified Chinese (zh-Hans)

Add Strings.zh-Hans.resx, held by the key and placeholder parity test from MOD87. zh-CN
and zh-SG must resolve to it. The font set in Styles.xaml needs a CJK fallback
(Microsoft YaHei UI), or WPF falls back to a glyph set that renders unevenly. Check that
every custom font in the styles has that fallback. Chinese text is short but tall, so
the case checks vertical clipping in rows of fixed height. A native speaker reviews the
text before the task ships.
