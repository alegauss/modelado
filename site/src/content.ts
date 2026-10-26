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
      { href: "#studio", label: "Studio" },
      { href: "#privacy", label: "Privacy" },
      { href: "#models", label: "Models" },
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
    hint: "Drag to turn it round, including the back TripoSR inferred.",
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
  // A model the reader turns, in the hero and the Studio.
  viewer: {
    label: "The 3D model. Drag or use the arrow keys to turn it, and the wheel, a pinch or + and − to zoom.",
    turn: "Turn the model",
    loading: "Loading the model…",
    failed: "The model could not be loaded here; the picture shows it from the front.",
    styles: [
      { id: "colours", label: "Colours" },
      { id: "clay", label: "Clay" },
      { id: "wireframe", label: "Wireframe" },
    ],
    stylesLabel: "How the model is drawn",
  },
  // MOD34: each operation on a real input, every result made by modelado-cli with the published
  // models on the VMware guest's CPU. site/README.md says how each was made.
  studio: {
    eyebrow: "The studio",
    title: "Every operation, on a real photo",
    tabsLabel: "Operations",
    object: {
      tab: "3D Object",
      lead: "One photo of one object, on a plain background, becomes the whole object: TripoSR infers the sides and the back the photo never showed.",
      photo: "demo/studio/object-hamburger.png",
      photoAlt: "The photo: a burger with lettuce and tomato on a dark plate.",
      model: "demo/studio/object-hamburger.glb",
      poster: "demo/studio/object-hamburger-poster.png",
      posterAlt: "The 3D model of the burger and its plate, seen from the front and a little to the side.",
      facts: "62,368 triangles · 1.9 MB GLB · grid 320",
      credit: "Photo: TripoSR's examples, MIT licence.",
      creditUrl: "https://github.com/VAST-AI-Research/TripoSR/tree/107cefdc244c39106fa830359024f6a2f1c78871/examples",
    },
    relief: {
      tab: "Relief",
      lead: "Any photo becomes a bas-relief: Depth Anything V2 estimates each pixel's depth, which becomes height, with a solid base under it for printing.",
      choicesLabel: "Which photo",
      items: [
        {
          id: "face",
          label: "A face",
          photo: "demo/studio/relief-face.jpg",
          photoAlt: "The photo: a marble bust of Herodotus, bearded, from the front.",
          model: "demo/studio/relief-face.glb",
          poster: "demo/studio/relief-face-poster.png",
          posterAlt: "The relief of the bust, cut out of its background, seen a little from the side.",
          facts: "56,504 triangles · 2.1 MB GLB · object only",
          credit: "Marble bust of Herodotus, The Metropolitan Museum of Art, CC0.",
          creditUrl: "https://commons.wikimedia.org/wiki/File:Marble_bust_of_Herodotos_MET_DT11742_(cropped).jpg",
        },
        {
          id: "building",
          label: "A building",
          photo: "demo/studio/relief-building.jpg",
          photoAlt: "The photo: the baroque columns of Syracuse Cathedral with the statue of Saint Paul, in evening light.",
          model: "demo/studio/relief-building.glb",
          poster: "demo/studio/relief-building-poster.png",
          posterAlt: "The relief of the cathedral's columns and statue, seen a little from the side.",
          facts: "95,228 triangles · 3.4 MB GLB",
          credit: "Syracuse Cathedral's façade, Julian Lupyan, CC0.",
          creditUrl: "https://commons.wikimedia.org/wiki/File:Statue_of_Saint_Paul_by_Ignazio_Marabitti_and_Syracuse_Cathedral%27s_Baroque_Facade.jpg",
        },
        {
          id: "landscape",
          label: "A landscape",
          photo: "demo/studio/relief-landscape.jpg",
          photoAlt: "The photo: the Grand Canyon from the South Rim, under white clouds.",
          model: "demo/studio/relief-landscape.glb",
          poster: "demo/studio/relief-landscape-poster.png",
          posterAlt: "The relief of the canyon, the rim's cliff standing out in front, seen a little from the side.",
          facts: "110,588 triangles · 4.1 MB GLB",
          credit: "Grand Canyon from the South Rim, Vulturesong, CC0.",
          creditUrl: "https://commons.wikimedia.org/wiki/File:Grand_Canyon_National_Park_-_HCP_-_September_29,_2022_-_052_-_Southern_Rim.jpg",
        },
      ],
    },
    edit: {
      tab: "Edit and refine",
      lead: "The chair as TripoSR leaves it, then with its holes filled, simplified to a fifth and smoothed three passes: lighter for a game or the web, and still the same chair.",
      before: "demo/studio/edit-before.png",
      beforeAlt: "The chair as TripoSR made it, with fine ridges across the seat.",
      beforeLabel: "As made · 132,932 triangles",
      after: "demo/studio/edit-after.png",
      afterAlt: "The same chair after the edits, the seat smooth.",
      afterLabel: "Refined · 26,586 triangles",
    },
    texture: {
      tab: "AI texture",
      lead: "A relief made from a 256 px photo carries a 256 px texture. Real-ESRGAN redraws it four times larger, on this PC, in about seven seconds on the CPU.",
      before: "demo/studio/texture-before.jpg",
      beforeAlt: "A patch of the canyon texture as made, blurred.",
      beforeLabel: "The texture as made · 256 × 192",
      after: "demo/studio/texture-after.jpg",
      afterAlt: "The same patch after Real-ESRGAN, the ridges and shadows sharp.",
      afterLabel: "After Real-ESRGAN · 1024 × 768",
    },
    turnHint: "Turn the model to see its depth.",
  },
  // MOD34: the moves from a photo to a file, privacy, speed, the models and the download.
  journey: {
    eyebrow: "From photo to file",
    title: "Four moves",
    steps: [
      {
        title: "Open a photo",
        body: "PNG, JPEG, WEBP, BMP or GIF, dropped on the window or opened. A model to edit opens the same way: GLB, OBJ, PLY or STL.",
      },
      {
        title: "Generate",
        body: "A whole object with TripoSR, or a relief from the photo's depth. The first time, the window says what that operation downloads and how large it is, and waits for your yes.",
      },
      {
        title: "Refine",
        body: "Simplify, subdivide, smooth, fill holes, remove loose pieces, mirror, turn and flatten a base. Every step is a version in the history, and Undo goes back through them.",
      },
      {
        title: "Export",
        body: "GLB for Windows, Blender and the web, OBJ with its texture, PLY with colours, or STL sized in millimetres for a slicer.",
      },
    ],
  },
  privacy: {
    eyebrow: "Privacy",
    title: "Your photo stays on your PC",
    items: [
      {
        title: "Inference runs here",
        body: "TripoSR, Depth Anything V2, U²-Net and Real-ESRGAN run in ONNX Runtime on this computer, on the GPU with DirectML or on the CPU.",
      },
      {
        title: "Only the models are downloaded",
        body: "Each model is fetched once from Hugging Face and checked against its SHA-256 before it is used. Nothing about your photos goes with it.",
      },
      {
        title: "The cloud is opt-in",
        body: "Meshy AI and Ideogram can be added in About with your own key. Nothing is sent to them until you agree to that one job, with what it costs.",
      },
    ],
  },
  speed: {
    eyebrow: "Speed",
    title: "Measured, not promised",
    lead: "On the CPU alone, 8 cores of an Intel Core i7-14700 with 8 GB of memory, the published models, a 512 px photo:",
    rows: [
      { what: "3D Object, default grid (256)", time: "about 20 s" },
      { what: "3D Object, finer grid (320)", time: "25 to 32 s" },
      { what: "Relief, default grid (256)", time: "about 1 s" },
      { what: "Simplify 150,000 triangles to 45,000", time: "under 1 s" },
    ],
    note: "A GPU with DirectML takes the inference off the CPU; the status bar names the one in use.",
  },
  models: {
    eyebrow: "The models",
    title: "Open models, each under its own licence",
    lead: "What the app downloads, from the list this version carries. Each one is fetched the first time an operation needs it.",
    tasks: {
      "image-to-triplane": "3D Object: reads the photo",
      "triplane-decoder": "3D Object: shapes the mesh",
      depth: "Relief: the depth of each pixel",
      "background-removal": "Cuts the object out of the photo",
      upscale: "Sharpens a photo or a texture",
    } as Record<string, string>,
  },
  download: {
    eyebrow: "Download",
    title: "Modelado for Windows 10 and 11",
    lead: "Free and open source, with no account. Once a day the app asks GitHub whether a newer release exists, sending only its name and version; About turns that off.",
    button: (version: string, size: string) => `Download Modelado ${version} · ${size}`,
    fallback: "Download the latest release",
    sums: "Each release lists its installer's SHA-256 in SHA256SUMS.",
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
