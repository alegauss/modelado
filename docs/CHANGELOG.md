# Shipped Ledger

## Block A — English everywhere

- ✅ **MOD1** **Every button, tooltip, caption, status line and dialog in the app window and About window is in Portuguese** — Every caption, tooltip, status line, dialog and the About window read in English, worded after PhotoBoost where the concept is the same.
- ✅ **MOD2** **modelado-cli usage, progress and errors, and the app's command-line errors, are printed in Portuguese** — modelado-cli's usage, progress, counts and errors, and the app's command-line refusals, print in English with English placeholders.
- ✅ **MOD3** **README.md is written in Portuguese** — README.md reads in English with its numbers checked against the code, and no Portuguese is left in the repository outside docs.

## Block B — Windows interface built like PhotoBoost

- ✅ **MOD4** **Colours are hard-coded hex in MainWindow.xaml, with no Brand class, no theme setting and no shared control templates** — Each owned colour is declared once in Brand, a test refuses any other; windows take their theme from Appearance (design recorded in `src/Modelado.Imaging/Brand.cs`).
- ✅ **MOD5** **The toolbar is a row of text buttons wired to Click handlers, with no glyphs and no commands that enable themselves** — The toolbar is glyph buttons on commands shared with their shortcuts, dimmed when they cannot run; turns and Mirror moved up from the Edit card.
- ✅ **MOD6** **The progress card sits mid-panel between the Edit and Quality cards, so a running job can scroll out of sight** — A running job is pinned above the scrolling cards with Cancel, seen and stoppable however far the cards scroll (design superseded: PhotoBoost's 300 px clips these cards; 340 px stays).
- ✅ **MOD50** **run-tests.cmd fails the Python suite where python on PATH is the Microsoft Store stub, though Python is installed** — run-tests.cmd uses .venv, a python that runs, or the one Python registered, and the guest gate runs scripts\tests green.
- ✅ **MOD7** **The window and the taskbar show WPF's default icon** — Modelado wears its own icon, a cube on a blue tile drawn per size by tools/Modelado.Icon, in the exe, the window and the taskbar.
- ✅ **MOD51** **Every edit's status line shows a garbled arrow between the triangle counts since the toolbar change** — The edit status line shows its arrow again, and MOD8's toolbar case on the guest reads it.
- ✅ **MOD8** **No control has an AutomationId and there are no UI cases, so nothing proves a screen works** — Every named control has an AutomationId, and six winwright cases drive the window on the VMware guest from run-tests.cmd.
- ✅ **MOD9** **The status bar is a bare panel with a provider chip and a text line: no job, last-run or download chips, no About** — The status bar holds the chips and About and shows an edit live; the reconstruction and download checks move to MOD53.
- ✅ **MOD10** **With nothing open there is only a hint over the viewport: no drop zone, no Open button, nowhere to show an open error** — The empty window is a drop zone with the formats and Open; a file that cannot open is said in place, never in a dialog (design superseded: toolbar model commands stay shown but off).
- ✅ **MOD11** **About is a separate modal window built in code, not a panel that takes the stage's place as in PhotoBoost** — About is an in-window panel toggled from the status bar; a case watches a download keep going while it opens and closes.
- ✅ **MOD12** **Undo and Redo move between versions, but nothing shows the list of versions the model went through** — A History card lists every version with the amount it used, the one shown selected, and drops undone versions on a new edit.
- ✅ **MOD13** **Export is only a Save dialog that guesses the format from the extension, with no card for format and options** — An Export card picks the format first, shows only its options and what the model loses in it, and writes files that read back.
- ✅ **MOD14** **The suite runs on the developer's own desktop: no VMware guest run, so UI cases would take over the operator's desk** — run-tests-vm.cmd carries the tree to the VMware guest and runs every suite there with a hang bound; README and skill name it the gate.

## Block C — Existing code wired into the window

- ✅ **MOD15** **The window always exports STL at 100 mm on its longest side; the size the CLI's --size takes cannot be chosen** — The Export card takes the longest side in mm for STL or units otherwise, shows the box it gives, and saves through Mesh.Placed, byte for byte what the CLI's --size writes.
- ✅ **MOD16** **Edit amounts are fixed in the window (smooth 3 passes, pieces under 5%, flat base at 3%) though the CLI takes each one** — Smooth passes, the loose-piece share and the flat-base height each have a slider beside their button, are named in the history, and give the CLI's mesh byte for byte.
- ✅ **MOD17** **The 3D Object card always samples at coarse step 2 with 2 smoothing passes; the CLI's --step and --smooth are absent** — An Advanced expander on the 3D Object card, closed by default, sets step 1 or 2, 0-6 smoothing passes and the loose-piece share; real-model parity waits in MOD53.
- ✅ **MOD18** **Where inference runs (auto, DirectML or CPU) can only be chosen with --provider at launch, not from the window** — About offers Automatic, GPU (DirectML) or CPU; the next operation runs there, the chip follows at once, and settings.json keeps it for the next start.

## Block D — Model downloads

- ✅ **MOD47** **The download tests fail to construct when the loopback server's first random port is already taken** — The download test server opens a fresh listener on each port attempt, so a busy port means another try instead of a red suite.
- ✅ **MOD21** **A first-use download is offered in a MessageBox listing text, not a panel with each file, size, source and license** — A first-use download is offered in an in-window panel listing each model's size, source and license; Not now changes nothing else.
- ✅ **MOD22** **An operation card does not say before it is pressed that it needs a one-time download, or how large it is** — Each operation card badges its one-time download and size; the download runs beside the app, pauses, resumes, and starts its operation.
- ✅ **MOD23** **Downloaded models can only be removed all at once; About lists no per-model size and no Remove for each** — About lists each download with its size and its own Remove; Remove all asks in the panel; a model in use is not removed.

## Block E — Publishing custom models to Hugging Face

## Block F — Installer, releases and auto update

## Block G — Public site for 3D modelling

## Block H — Cloud services: Meshy AI for 3D, Ideogram for images and textures

## Block I — Blender integration: AI tools for existing Blender models

