// Every word on the page, in English only: a Portuguese or multilingual interface is one of
// this roadmap's non-goals, so there is no i18n layer and no second route. What is not a word
// (the base, the links) is written once here too.

export const THEME_KEY = "modelado-theme";
export const BASE = "/modelado/";
export const repoUrl = "https://github.com/alegauss/modelado";
export const releasesUrl = `${repoUrl}/releases/latest`;

/** A file under public/, with the base. */
export const asset = (path: string) => `${BASE}${path}`;

export const words = {
  meta: {
    title: "Modelado: a photo becomes a 3D model, on your PC",
    description:
      "Turn a photo into a 3D object or a relief, then clean it up and export it for printing, games or the web, with AI on your own Windows computer. Nothing is uploaded.",
  },
  nav: {
    links: [
      { href: "#what", label: "What it makes" },
      { href: repoUrl, label: "Source" },
    ],
    cta: "Download",
    themeLabel: (next: string) => `Switch to the ${next} theme`,
  },
  intro: {
    eyebrow: "Modelado · for Windows",
    title: "A photo becomes a 3D model you can turn, print and share.",
    lead:
      "One photo of an object gives a whole model with TripoSR, sides and back included; any photo gives a relief with Depth Anything V2. Smooth, simplify, fill holes and export to GLB, OBJ, PLY or STL. It all runs on your own PC, on the GPU when it has one.",
    download: "Download for Windows 10 and 11",
    source: "See the source",
  },
  // MOD33: one real result, made by modelado-cli object --resolution 320 with the published models
  // and simplified with edit --simplify 0.35; the poster is the same model's --preview render. Of six
  // TripoSR examples made that way, the chair came out closest to its photo.
  hero: {
    photo: {
      src: "demo/chair.png",
      alt: "The photo: a wooden dining chair with a cream upholstered seat and a tufted back, from the front.",
      label: "The photo",
    },
    model: {
      src: "demo/chair.glb",
      poster: "demo/chair-poster.png",
      posterAlt: "The 3D model Modelado made from the photo, seen from the front and a little to the side, its back legs included.",
      label: "The model Modelado made from it",
      facts: "46,526 triangles · 1.4 MB GLB · 3D Object on the CPU",
    },
    arrow: "becomes",
    viewerLabel: "The 3D model. Drag or use the arrow keys to turn it, and the wheel, a pinch or + and − to zoom.",
    hint: "Drag to turn it round, including the back TripoSR inferred.",
    turn: "Turn the model",
    loading: "Loading the model…",
    failed: "The model could not be loaded here; the picture shows it from the front.",
    styles: [
      { id: "colours", label: "Colours" },
      { id: "clay", label: "Clay" },
      { id: "wireframe", label: "Wireframe" },
    ],
    stylesLabel: "How the model is drawn",
    credit: "Photo: the chair from TripoSR's examples, MIT licence.",
    creditUrl: "https://github.com/VAST-AI-Research/TripoSR/tree/107cefdc244c39106fa830359024f6a2f1c78871/examples",
  },
  what: {
    eyebrow: "What it makes",
    title: "Two ways from a photo to a model",
    items: [
      {
        title: "3D Object",
        body: "A single, centred object on a plain background becomes a whole coloured mesh, its unseen sides inferred by TripoSR.",
      },
      {
        title: "Relief",
        body: "Each pixel's depth becomes height: a bas-relief textured with the photo, from faces, landscapes or buildings, with a solid base for printing.",
      },
      {
        title: "Edit and export",
        body: "Turn, mirror, smooth, simplify, subdivide, fill holes and flatten a base, every step undoable, then save it sized for a slicer or a game engine.",
      },
    ],
  },
  footer: {
    links: [
      { href: repoUrl, label: "Source on GitHub" },
      { href: releasesUrl, label: "Releases" },
    ],
    disclaimer:
      "Modelado is free software. The AI models it downloads on first use carry their own licences, which its About panel lists.",
  },
} as const;
