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

## Block C — Existing code wired into the window

## Block D — Model downloads

- ✅ **MOD47** **The download tests fail to construct when the loopback server's first random port is already taken** — The download test server opens a fresh listener on each port attempt, so a busy port means another try instead of a red suite.

## Block E — Publishing custom models to Hugging Face

## Block F — Installer, releases and auto update

## Block G — Public site for 3D modelling

## Block H — Cloud services: Meshy AI for 3D, Ideogram for images and textures

## Block I — Blender integration: AI tools for existing Blender models

