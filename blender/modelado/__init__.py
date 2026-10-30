"""Modelado's mesh edits on the selected Blender object (MOD43), and new objects from a photo (MOD44).

A "Modelado" panel in the 3D Viewport's sidebar offers the edits modelado-cli makes: Simplify,
Subdivide, Smooth, Fill holes, Remove loose pieces and Flat base, each with its amount. Each runs
as: the object written to a temporary GLB (its modifiers applied when asked), modelado-cli edit
with --json in a background thread, so Blender never freezes and Esc cancels, then the edited GLB
read back. The result replaces the mesh data of the same object, which keeps its name,
transform, parent, collections and material slots, and is one step of Blender's own undo.

"Generate from photo" turns an image file, or an image already in the .blend, into a 3D Object
(TripoSR) or a Relief (Depth Anything V2) with the window's main options, through modelado-cli
object or relief in the same background thread. The result lands at the 3D cursor, named after
the photo, its longest side the size the panel sets, in a material that shows its colours or its
texture. When the models are not downloaded yet, a dialog first says what the first use fetches,
how large and from where, as the window's consent panel does, and nothing starts without a yes.

With a Meshy AI key stored in the window's About, "3D Object with Meshy AI" sends the photo
through modelado-cli cloud image-to-3d (MOD65): the dialog first shows the window's consent line,
the price and what the month's budget leaves, and the spend lands in the same ledger About reads.
The AI group does the same for the selected object (MOD45): a new texture drawn by Ideogram from
a material's name, and Meshy AI's remesh and retexture, each shown only where About holds the
service's key, each asking first and one step of Blender's undo.

"Edit in Modelado" (MOD46) opens the selected mesh in the Modelado window. There, each "Send to
Blender" puts the version shown on the same object, as one undo step, until the window closes.
"""

import json
import os
import subprocess
import tempfile
import threading

import bpy
from bpy.props import BoolProperty, EnumProperty, FloatProperty, IntProperty, PointerProperty, StringProperty
from mathutils import Matrix

# What each edit is called, how its amount reads, and the arguments modelado-cli takes for it.
EDITS = {
    "SIMPLIFY": ("Simplify", lambda s: ["--simplify", f"{s.simplify_share:.4f}"]),
    "SUBDIVIDE": ("Subdivide", lambda s: ["--subdivide"]),
    "SMOOTH": ("Smooth", lambda s: ["--smooth", str(s.smooth_passes)]),
    "FILL_HOLES": ("Fill holes", lambda s: ["--fill-holes"]),
    "PIECES": ("Remove loose pieces", lambda s: ["--remove-pieces", f"{s.pieces_percent / 100:.4f}"]),
    "FLATTEN": ("Flat base", lambda s: ["--flatten", f"{s.flatten_percent / 100:.4f}"]),
}


def default_cli() -> str:
    """Where the installer puts modelado-cli (MOD28), unless MODELADO_CLI names another."""
    named = os.environ.get("MODELADO_CLI")
    if named:
        return named
    return os.path.join(os.environ.get("LOCALAPPDATA", ""), "Programs", "Modelado", "modelado-cli.exe")


def default_app() -> str:
    """Where the installer puts the Modelado window (MOD28), unless MODELADO_APP names another."""
    named = os.environ.get("MODELADO_APP")
    if named:
        return named
    return os.path.join(os.environ.get("LOCALAPPDATA", ""), "Programs", "Modelado", "Modelado.exe")


def edit_args(kind: str, settings) -> list:
    """modelado-cli's arguments for one edit, at the amounts the panel holds."""
    return EDITS[kind][1](settings)


def triangles(mesh) -> int:
    """The mesh's triangle count, as modelado-cli counts it."""
    return sum(len(polygon.vertices) - 2 for polygon in mesh.polygons)


def export_object(obj, path: str, apply_modifiers: bool) -> None:
    """Writes the object alone to a GLB, in its own space, with nothing else of the scene."""
    for other in bpy.context.view_layer.objects:
        other.select_set(False)
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    # The transform stays on the object: the GLB holds the mesh in its own space, whatever
    # modelado-cli does with a node's transform.
    placed = obj.matrix_world.copy()
    obj.matrix_world = Matrix.Identity(4)
    try:
        bpy.ops.export_scene.gltf(
            filepath=path, export_format="GLB", use_selection=True, export_apply=apply_modifiers,
            export_materials="NONE", export_animations=False, export_skins=False, export_morph=False)
    finally:
        obj.matrix_world = placed


# The JSON on stdout is ASCII (non-ASCII is escaped), but the lines for a person on stderr come in
# the console's code page when redirected ("800 × 800" is byte 0x9E in cp850), so a byte that is
# not UTF-8 is replaced rather than raised in the reader thread.
TEXT = {"text": True, "encoding": "utf-8", "errors": "replace"}


def start_cli(cli: str, source: str, target: str, args: list) -> subprocess.Popen:
    """modelado-cli edit, its result as JSON on stdout and nothing on screen."""
    flags = getattr(subprocess, "CREATE_NO_WINDOW", 0)
    return subprocess.Popen(
        [cli, "edit", source, "-o", target, *args, "--json", "--provider", "cpu"],
        stdout=subprocess.PIPE, stderr=subprocess.PIPE, creationflags=flags, **TEXT)


def read_report(process: subprocess.Popen) -> dict:
    """What modelado-cli said: its JSON, or an error naming why there is none."""
    out, err = process.communicate()
    try:
        report = json.loads(out.strip().splitlines()[-1]) if out.strip() else {}
    except (json.JSONDecodeError, IndexError):
        report = {}
    if report.get("exitCode") != 0:
        raise RuntimeError(report.get("error") or err.strip() or f"modelado-cli exited {process.returncode}")
    return report


def replace_mesh(obj, path: str) -> None:
    """Puts the edited GLB's mesh in place of the object's, keeping everything else about it."""
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=path)
    imported = [o for o in bpy.data.objects if o not in before]
    meshes = [o for o in imported if o.type == "MESH"]
    if not meshes:
        raise RuntimeError("modelado-cli wrote no mesh")
    mesh = meshes[0].data
    # Whatever node transform the GLB carried belongs to the vertices, not to the object.
    mesh.transform(meshes[0].matrix_world)
    for other in imported:
        bpy.data.objects.remove(other, do_unlink=True)
    mesh.materials.clear()
    for slot in obj.material_slots:
        mesh.materials.append(slot.material)
    # The old mesh stays in the file until undo has no use for it: undo puts it back. It gives up
    # its name first, or the new one would be Suzanne.001.
    old = obj.data
    name = old.name
    old.name = f"{name} (before Modelado)"
    obj.data = mesh
    mesh.name = name


GENERATE = {
    "OBJECT": ("3D Object", "object"),
    "RELIEF": ("Relief", "relief"),
    # MOD65: Meshy AI's image to 3D, through modelado-cli cloud and the key stored in the window's About.
    "MESHY": ("3D Object with Meshy AI", "cloud"),
}


def generate_args(kind: str, settings) -> list:
    """modelado-cli's options for one generation, at the values the panel holds."""
    if kind == "OBJECT":
        return ["--resolution", settings.object_resolution]
    if kind == "MESHY":
        return [] if settings.meshy_texture else ["--no-texture"]
    return ["--grid", str(settings.relief_grid), "--depth", f"{settings.relief_depth:.4f}", "--base", f"{settings.relief_base:.4f}"]


def command_for(kind: str, photo: str) -> list:
    """modelado-cli's command and photo: `object <photo>`, or `cloud image-to-3d <photo>` for Meshy."""
    return ["cloud", "image-to-3d", photo] if kind == "MESHY" else [GENERATE[kind][1], photo]


def cloud_api() -> tuple:
    """--cloud-api for modelado-cli when MODELADO_CLOUD_API names another API root (id=url, e.g. a stand-in)."""
    named = os.environ.get("MODELADO_CLOUD_API")
    return ("--cloud-api", named) if named else ()


def key_stored(service: str) -> bool:
    """Whether the window's About holds a key for the service, which is when the panel offers it."""
    return os.path.isfile(os.path.join(os.environ.get("LOCALAPPDATA", ""), "Modelado", "keys", f"{service}.key"))


def meshy_key_stored() -> bool:
    """Whether the window's About holds a Meshy AI key."""
    return key_stored("meshy")


def cloud_estimate(cli: str, photo: str, settings, extra: tuple = ()) -> dict:
    """What sending the photo to Meshy would cost and what the budget leaves, sending nothing.

    `extra` is more arguments for modelado-cli, e.g. --cloud-api for a stand-in service."""
    return run_cli(cli, [*command_for("MESHY", photo), *generate_args("MESHY", settings), "--estimate", *extra])


def run_cli(cli: str, args: list) -> dict:
    """modelado-cli with --json, waited for; its report, or a RuntimeError saying why there is none."""
    flags = getattr(subprocess, "CREATE_NO_WINDOW", 0)
    process = subprocess.Popen(
        [cli, *args, "--json", "--provider", "cpu"],
        stdout=subprocess.PIPE, stderr=subprocess.PIPE, creationflags=flags, **TEXT)
    return read_report(process)


def pending_download(cli: str, what: str, models: str = None) -> dict:
    """What a first use of `what` (object, relief, upscale) would download: download --dry-run's report."""
    return run_cli(cli, ["download", what, "--dry-run", *(["--models", models] if models else [])])


def consent_text(title: str, pending: dict) -> str:
    """The window's consent, in a sentence: what, how large, from where, and that the photo stays here."""
    return (f"{title} needs a one-time download of {pending['pending'] / 1e6:,.0f} MB "
            f"from {pending.get('from') or 'Hugging Face'}: {', '.join(pending.get('licenses', []))}. "
            "Your photo and your model stay on this computer.")


def start_generate(cli: str, kind: str, photo: str, target: str, settings, extra: tuple = ()) -> subprocess.Popen:
    """modelado-cli object, relief or cloud image-to-3d on the photo, its result as JSON on stdout."""
    flags = getattr(subprocess, "CREATE_NO_WINDOW", 0)
    return subprocess.Popen(
        [cli, *command_for(kind, photo), "-o", target, *generate_args(kind, settings), *extra, "--json", "--provider", "cpu"],
        stdout=subprocess.PIPE, stderr=subprocess.PIPE, creationflags=flags, **TEXT)


def photo_path(settings, folder: str) -> str:
    """The photo as a file: the image chosen in the .blend (saved to the folder when packed), else the path."""
    image = settings.image
    if image is not None:
        on_disk = bpy.path.abspath(image.filepath) if image.filepath else ""
        if on_disk and os.path.isfile(on_disk) and not image.packed_file:
            return on_disk
        copy = os.path.join(folder, bpy.path.clean_name(image.name) + ".png")
        image.save(filepath=copy)
        return copy
    return bpy.path.abspath(settings.photo)


def colour_material(name: str):
    """A material whose base colour is the mesh's colour attribute, as TripoSR's vertex colours need."""
    material = bpy.data.materials.new(name)
    material.use_nodes = True
    nodes = material.node_tree.nodes
    shader = nodes.get("Principled BSDF")
    attribute = nodes.new("ShaderNodeVertexColor")
    material.node_tree.links.new(attribute.outputs["Color"], shader.inputs["Base Color"])
    return material


def shows_texture(material) -> bool:
    return material is not None and material.use_nodes and any(n.type == "TEX_IMAGE" and n.image for n in material.node_tree.nodes)


def import_generated(path: str, name: str, size: float, location) -> object:
    """The GLB as one object at the location, standing on it, its longest side `size`, its colours shown."""
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=path)
    imported = [o for o in bpy.data.objects if o not in before]
    meshes = [o for o in imported if o.type == "MESH"]
    if not meshes:
        raise RuntimeError("modelado-cli wrote no mesh")
    obj = meshes[0]
    obj.data.transform(obj.matrix_world)
    for other in imported:
        if other is not obj:
            bpy.data.objects.remove(other, do_unlink=True)
    obj.parent = None
    obj.matrix_world = Matrix.Identity(4)

    # Centred on X and Y, standing on Z = 0, scaled so its longest side is the size asked for.
    xs, ys, zs = zip(*(v.co for v in obj.data.vertices))
    longest = max(max(xs) - min(xs), max(ys) - min(ys), max(zs) - min(zs)) or 1.0
    scale = size / longest
    obj.data.transform(Matrix.Diagonal((scale, scale, scale, 1.0)) @ Matrix.Translation(
        (-(min(xs) + max(xs)) / 2, -(min(ys) + max(ys)) / 2, -min(zs))))
    obj.location = location
    obj.name = name
    obj.data.name = name

    if not any(shows_texture(slot.material) for slot in obj.material_slots) and obj.data.color_attributes:
        obj.data.materials.clear()
        obj.data.materials.append(colour_material(f"{name} colours"))
    return obj


def run_generate(cli: str, kind: str, photo: str, settings, location, extra: tuple = ()) -> tuple:
    """One generation, start to end, on the window's thread; the panel's operator runs the CLI off it."""
    with tempfile.TemporaryDirectory(prefix="modelado-") as folder:
        target = os.path.join(folder, "out.glb")
        report = read_report(start_generate(cli, kind, photo, target, settings, extra))
        name = os.path.splitext(os.path.basename(photo))[0]
        return import_generated(target, name, settings.size, location), report


TEXTURE_ACTIONS = {
    "UPSCALE": ("Upscale texture 4×", lambda s: ["--upscale-texture"]),
    "MATERIAL": ("New texture from an image", lambda s: ["--texture", bpy.path.abspath(s.material_image), "--tiles", str(s.material_tiles)]),
}


def base_colour_image(obj):
    """The image node feeding the first material's Principled BSDF Base Color, and that material; or (None, None)."""
    for slot in obj.material_slots:
        material = slot.material
        if material is None or not material.use_nodes:
            continue
        for node in material.node_tree.nodes:
            if node.type == "BSDF_PRINCIPLED":
                for link in node.inputs["Base Color"].links:
                    if link.from_node.type == "TEX_IMAGE" and link.from_node.image is not None:
                        return link.from_node, material
    return None, None


def export_textured(obj, path: str) -> None:
    """Writes the object alone to a GLB with its material, so its texture and UVs travel too."""
    for other in bpy.context.view_layer.objects:
        other.select_set(False)
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.export_scene.gltf(
        filepath=path, export_format="GLB", use_selection=True, export_apply=False,
        export_materials="EXPORT", export_image_format="AUTO", export_animations=False, export_skins=False, export_morph=False)


def read_texture(path: str, name: str):
    """The texture of the GLB modelado-cli wrote, packed into the .blend under `name`; nothing else is kept."""
    before_objects, before_images = set(bpy.data.objects), set(bpy.data.images)
    bpy.ops.import_scene.gltf(filepath=path)
    images = [i for i in bpy.data.images if i not in before_images]
    for obj in [o for o in bpy.data.objects if o not in before_objects]:
        mesh = obj.data if obj.type == "MESH" else None
        bpy.data.objects.remove(obj, do_unlink=True)
        if mesh is not None and mesh.users == 0:
            bpy.data.meshes.remove(mesh)
    if not images:
        raise RuntimeError("modelado-cli wrote no texture")
    image = images[0]
    image.pack()
    image.name = name
    return image


def run_texture(obj, action: str, cli: str, settings, args: list = None, label: str = None) -> dict:
    """One texture action on the object: its Base Color image is replaced, the rest of its material kept."""
    node, _ = base_colour_image(obj)
    if node is None:
        raise RuntimeError(f"{obj.name} has no image texture on its Base Color")
    with tempfile.TemporaryDirectory(prefix="modelado-") as folder:
        source, target = os.path.join(folder, "in.glb"), os.path.join(folder, "out.glb")
        export_textured(obj, source)
        report = read_report(start_cli(cli, source, target, args or TEXTURE_ACTIONS[action][1](settings)))
        node.image = read_texture(target, f"{node.image.name} ({label or TEXTURE_ACTIONS[action][0]})")
        return report


# MOD45: the cloud half of the AI group, each through modelado-cli cloud with the key the window's
# About stored: what it is called, the service whose key offers it, and whether it needs the
# object's Base Color image.
CLOUD_ACTIONS = {
    "TEXTURE": ("New texture from a prompt", "ideogram", True),
    "REMESH": ("Remesh with Meshy AI", "meshy", False),
    "RETEXTURE": ("Retexture with Meshy AI", "meshy", True),
}


def cloud_args(action: str, source: str, settings) -> list:
    """modelado-cli cloud's command for an action; `source` is the object as a GLB (unused for TEXTURE)."""
    if action == "TEXTURE":
        return ["cloud", "texture", settings.texture_prompt]
    if action == "REMESH":
        return ["cloud", "remesh", source, "--polycount", str(settings.remesh_polycount)]
    # The model's own UVs are kept, so the new image fits the mesh Blender already has.
    return ["cloud", "retexture", source, "--prompt", settings.retexture_prompt, "--keep-uv"]


def export_for(obj, action: str, path: str, settings) -> None:
    if action == "RETEXTURE":
        export_textured(obj, path)
    elif action == "REMESH":
        export_object(obj, path, settings.apply_modifiers)


def estimate_cloud(obj, action: str, cli: str, settings, extra: tuple = ()) -> dict:
    """The window's consent line for the action, its price and what the budget leaves; nothing is sent."""
    with tempfile.TemporaryDirectory(prefix="modelado-") as folder:
        source = os.path.join(folder, "in.glb")
        export_for(obj, action, source, settings)
        return run_cli(cli, [*cloud_args(action, source, settings), "--estimate", *extra])


def run_cloud(obj, action: str, cli: str, settings, extra: tuple = ()) -> dict:
    """One cloud action on the object, after its consent; the result replaces what the action changes."""
    title = CLOUD_ACTIONS[action][0]
    with tempfile.TemporaryDirectory(prefix="modelado-") as folder:
        source = os.path.join(folder, "in.glb")
        if action == "TEXTURE":
            # Ideogram draws the tile; the local material path makes it seamless and lays it on.
            tile = os.path.join(folder, "tile.png")
            report = run_cli(cli, [*cloud_args(action, source, settings), "-o", tile, *extra])
            run_texture(obj, "MATERIAL", cli, settings, ["--texture", tile, "--tiles", str(settings.material_tiles)], f"{title}: {settings.texture_prompt}")
            return report
        target = os.path.join(folder, "out.glb")
        export_for(obj, action, source, settings)
        report = run_cli(cli, [*cloud_args(action, source, settings), "-o", target, *extra])
        if action == "REMESH":
            replace_mesh(obj, target)
        else:
            node, _ = base_colour_image(obj)
            node.image = read_texture(target, f"{node.image.name} ({title})")
        return report


# MOD46: the round trip with the window, over a folder both share (Modelado.Geometry.BlenderSession
# is the window's half). The add-on writes session.json and the object's mesh; each "Send to
# Blender" in the window writes sent-N.glb, then sent.json naming it with its sequence.
def start_session(obj, folder: str, apply_modifiers: bool) -> None:
    """The object written to the session folder, with session.json naming it for the window."""
    export_object(obj, os.path.join(folder, "model.glb"), apply_modifiers)
    with open(os.path.join(folder, "session.json"), "w", encoding="utf-8") as described:
        json.dump({"object": obj.name, "model": "model.glb"}, described)


def read_sent(folder: str):
    """The window's latest send, {"sequence", "model", "label"}, or None before the first."""
    try:
        with open(os.path.join(folder, "sent.json"), encoding="utf-8") as sent:
            return json.load(sent)
    except (OSError, ValueError):
        return None


def apply_send(obj, folder: str, sent: dict) -> None:
    """A send put on the object as MOD43's edits are: its mesh replaced, everything else kept."""
    replace_mesh(obj, os.path.join(folder, os.path.basename(sent["model"])))


def run_edit(obj, args: list, cli: str, apply_modifiers: bool = False) -> dict:
    """One edit, start to end, on the window's thread; the panel's operator runs the CLI off it."""
    with tempfile.TemporaryDirectory(prefix="modelado-") as folder:
        source, target = os.path.join(folder, "in.glb"), os.path.join(folder, "out.glb")
        export_object(obj, source, apply_modifiers)
        report = read_report(start_cli(cli, source, target, args))
        replace_mesh(obj, target)
        return report


class ModeladoSettings(bpy.types.PropertyGroup):
    simplify_share: FloatProperty(name="Keep", description="The share of the triangles Simplify keeps", default=0.25, min=0.02, max=0.9, subtype="FACTOR")
    smooth_passes: IntProperty(name="Passes", description="Taubin smoothing passes", default=3, min=1, max=10)
    pieces_percent: IntProperty(name="Under", description="Loose pieces smaller than this share of the largest are removed", default=5, min=1, max=50, subtype="PERCENTAGE")
    flatten_percent: IntProperty(name="Height", description="The share of the height flattened from the bottom", default=3, min=1, max=20, subtype="PERCENTAGE")
    apply_modifiers: BoolProperty(name="Apply modifiers", description="Edit the mesh as the modifiers show it, and drop them", default=False)
    # MOD44: Generate from photo.
    photo: StringProperty(name="Photo", description="The photo to make a model from", subtype="FILE_PATH", default="")
    image: PointerProperty(name="Image", description="Or an image already in this file", type=bpy.types.Image)
    object_resolution: EnumProperty(name="Resolution", description="Grid points per side TripoSR samples", default="256", items=[
        ("128", "128 · quick draft", ""), ("192", "192", ""), ("256", "256 · default", ""), ("320", "320", ""), ("384", "384 · fine", "")])
    relief_grid: IntProperty(name="Grid", description="Vertices on the relief's longest side", default=256, min=64, max=512)
    relief_depth: FloatProperty(name="Height", description="How far the nearest point stands out, as a share of the width", default=0.25, min=0.05, max=0.6, subtype="FACTOR")
    relief_base: FloatProperty(name="Base", description="A solid base under the relief, as a share of the width; 0 leaves the surface open", default=0.0, min=0.0, max=0.2, subtype="FACTOR")
    size: FloatProperty(name="Size", description="The new object's longest side", default=0.2, min=0.001, subtype="DISTANCE", unit="LENGTH")
    texture_prompt: StringProperty(name="Material", description="The material Ideogram draws as a seamless texture, e.g. weathered oak planks", default="")
    remesh_polycount: IntProperty(name="Triangles", description="The clean mesh's triangle count", default=30000, min=100, max=300000)
    retexture_prompt: StringProperty(name="Look", description="What Meshy AI paints the surface as, e.g. rusted iron", default="")
    meshy_texture: BoolProperty(name="Textured", description="Meshy AI paints a texture too; the bare shape costs less", default=True)
    # MOD45: the AI group's texture actions.
    material_image: StringProperty(name="Material", description="A material image (wood, stone, metal), made to tile and put on the surface", subtype="FILE_PATH", default="")
    material_tiles: IntProperty(name="Tiles", description="How many times the material repeats across the texture", default=2, min=1, max=16)


class ModeladoPreferences(bpy.types.AddonPreferences):
    bl_idname = __package__

    cli: StringProperty(name="modelado-cli", description="modelado-cli.exe, installed with Modelado", subtype="FILE_PATH", default=default_cli())
    app: StringProperty(name="Modelado", description="Modelado.exe, the window Edit in Modelado opens", subtype="FILE_PATH", default=default_app())

    def draw(self, context):
        self.layout.prop(self, "cli")
        self.layout.prop(self, "app")


def cli_path(context) -> str:
    addon = context.preferences.addons.get(__package__)
    return addon.preferences.cli if addon else default_cli()


def app_path(context) -> str:
    addon = context.preferences.addons.get(__package__)
    return addon.preferences.app if addon else default_app()


class MODELADO_OT_edit(bpy.types.Operator):
    """Edit the selected mesh with Modelado"""

    bl_idname = "modelado.edit"
    bl_label = "Modelado edit"
    bl_options = {"REGISTER", "UNDO"}

    kind: EnumProperty(items=[(key, label, "") for key, (label, _) in EDITS.items()])

    _process = None
    _timer = None
    _folder = None

    @classmethod
    def poll(cls, context):
        return context.active_object is not None and context.active_object.type == "MESH" and context.mode == "OBJECT"

    def invoke(self, context, event):
        cli = cli_path(context)
        if not os.path.isfile(cli):
            self.report({"ERROR"}, f"modelado-cli was not found at {cli}; set it in the add-on's preferences")
            return {"CANCELLED"}

        settings = context.scene.modelado
        self._obj = context.active_object
        self._before = triangles(self._obj.data)
        self._folder = tempfile.TemporaryDirectory(prefix="modelado-")
        self._source = os.path.join(self._folder.name, "in.glb")
        self._target = os.path.join(self._folder.name, "out.glb")
        export_object(self._obj, self._source, settings.apply_modifiers)
        self._process = start_cli(cli, self._source, self._target, edit_args(self.kind, settings))
        self._done = threading.Event()
        threading.Thread(target=lambda: (self._process.wait(), self._done.set()), daemon=True).start()
        context.window_manager.progress_begin(0, 1)
        self._timer = context.window_manager.event_timer_add(0.2, window=context.window)
        context.window_manager.modal_handler_add(self)
        context.workspace.status_text_set(f"Modelado: {EDITS[self.kind][0]}… Esc cancels")
        return {"RUNNING_MODAL"}

    def modal(self, context, event):
        if event.type == "ESC":
            self._process.kill()
            self._finish(context)
            self.report({"WARNING"}, f"{EDITS[self.kind][0]} cancelled")
            return {"CANCELLED"}
        if event.type != "TIMER" or not self._done.is_set():
            return {"PASS_THROUGH"}

        try:
            report = read_report(self._process)
            replace_mesh(self._obj, self._target)
        except RuntimeError as error:
            self._finish(context)
            self.report({"ERROR"}, f"{EDITS[self.kind][0]}: {error}")
            return {"CANCELLED"}

        self._finish(context)
        self.report({"INFO"}, f"{EDITS[self.kind][0]}: {self._before:,} → {report.get('triangles', 0):,} triangles in {report.get('seconds', 0):.1f} s")
        return {"FINISHED"}

    def _finish(self, context):
        context.window_manager.event_timer_remove(self._timer)
        context.window_manager.progress_end()
        context.workspace.status_text_set(None)
        self._folder.cleanup()


class MODELADO_OT_generate(bpy.types.Operator):
    """Make a 3D object or a relief from a photo, at the 3D cursor"""

    bl_idname = "modelado.generate"
    bl_label = "Generate from photo"
    bl_options = {"REGISTER", "UNDO"}

    kind: EnumProperty(items=[(key, label, "") for key, (label, _) in GENERATE.items()])

    _process = None
    _timer = None
    _folder = None
    _consent = ""

    @classmethod
    def poll(cls, context):
        return context.mode == "OBJECT"

    def invoke(self, context, event):
        cli = cli_path(context)
        if not os.path.isfile(cli):
            self.report({"ERROR"}, f"modelado-cli was not found at {cli}; set it in the add-on's preferences")
            return {"CANCELLED"}

        settings = context.scene.modelado
        self._folder = tempfile.TemporaryDirectory(prefix="modelado-")
        self._photo = photo_path(settings, self._folder.name)
        if not os.path.isfile(self._photo):
            self._folder.cleanup()
            self.report({"ERROR"}, "Choose a photo, or an image in this file, first")
            return {"CANCELLED"}

        if self.kind == "MESHY":
            # As the window's consent line: what goes, to whom, for how much, what the budget leaves.
            try:
                estimate = cloud_estimate(cli, self._photo, settings, cloud_api())
            except RuntimeError as error:
                self._folder.cleanup()
                self.report({"ERROR"}, f"{GENERATE[self.kind][0]}: {error}")
                return {"CANCELLED"}
            if not estimate.get("keyStored") or estimate.get("blocked"):
                self._folder.cleanup()
                why = estimate["consent"] if estimate.get("blocked") else "no Meshy AI key is stored; add one in Modelado's About"
                self.report({"ERROR"}, f"{GENERATE[self.kind][0]}: {why}")
                return {"CANCELLED"}
            self._consent = estimate["consent"]
            try:
                return context.window_manager.invoke_props_dialog(self, width=480, title="Send the photo to Meshy AI?", confirm_text="Send it")
            except TypeError:  # Blender before 4.1 names neither
                return context.window_manager.invoke_props_dialog(self, width=480)

        try:
            pending = pending_download(cli, GENERATE[self.kind][1])
        except RuntimeError as error:
            self._folder.cleanup()
            self.report({"ERROR"}, f"{GENERATE[self.kind][0]}: {error}")
            return {"CANCELLED"}

        if pending.get("pending", 0) > 0:
            # As the window's consent panel: what, how large, from where, and that the photo stays here.
            self._consent = consent_text(GENERATE[self.kind][0], pending)
            try:
                return context.window_manager.invoke_props_dialog(self, width=480, title="Download the models?", confirm_text="Download and generate")
            except TypeError:  # Blender before 4.1 names neither
                return context.window_manager.invoke_props_dialog(self, width=480)
        return self._start(context)

    def draw(self, context):
        for line in _wrap(self._consent, 70):
            self.layout.label(text=line)

    def execute(self, context):
        return self._start(context)

    def _start(self, context):
        settings = context.scene.modelado
        self._location = context.scene.cursor.location.copy()
        self._target = os.path.join(self._folder.name, "out.glb")
        self._process = start_generate(cli_path(context), self.kind, self._photo, self._target, settings, cloud_api() if self.kind == "MESHY" else ())
        self._done = threading.Event()
        threading.Thread(target=lambda: (self._process.wait(), self._done.set()), daemon=True).start()
        context.window_manager.progress_begin(0, 1)
        self._timer = context.window_manager.event_timer_add(0.2, window=context.window)
        context.window_manager.modal_handler_add(self)
        context.workspace.status_text_set(f"Modelado: {GENERATE[self.kind][0]} from {os.path.basename(self._photo)}… Esc cancels")
        return {"RUNNING_MODAL"}

    def modal(self, context, event):
        if event.type == "ESC":
            self._process.kill()
            self._finish(context)
            self.report({"WARNING"}, f"{GENERATE[self.kind][0]} cancelled")
            return {"CANCELLED"}
        if event.type != "TIMER" or not self._done.is_set():
            return {"PASS_THROUGH"}

        try:
            report = read_report(self._process)
            name = os.path.splitext(os.path.basename(self._photo))[0]
            obj = import_generated(self._target, name, context.scene.modelado.size, self._location)
        except RuntimeError as error:
            self._finish(context)
            self.report({"ERROR"}, f"{GENERATE[self.kind][0]}: {error}")
            return {"CANCELLED"}

        for other in context.view_layer.objects:
            other.select_set(False)
        obj.select_set(True)
        context.view_layer.objects.active = obj
        self._finish(context)
        spent = f" · {report['spent']}" if report.get("spent") else ""
        self.report({"INFO"}, f"{GENERATE[self.kind][0]}: {obj.name}, {report.get('triangles', 0):,} triangles in {report.get('seconds', 0):.1f} s{spent}")
        return {"FINISHED"}

    def _finish(self, context):
        context.window_manager.event_timer_remove(self._timer)
        context.window_manager.progress_end()
        context.workspace.status_text_set(None)
        self._folder.cleanup()


class MODELADO_OT_texture(bpy.types.Operator):
    """Replace the selected object's Base Color texture, keeping the rest of its material"""

    bl_idname = "modelado.texture"
    bl_label = "Modelado texture"
    bl_options = {"REGISTER", "UNDO"}

    action: EnumProperty(items=[(key, label, "") for key, (label, _) in TEXTURE_ACTIONS.items()])

    _consent = ""

    @classmethod
    def poll(cls, context):
        obj = context.active_object
        return obj is not None and obj.type == "MESH" and context.mode == "OBJECT" and base_colour_image(obj)[0] is not None

    def invoke(self, context, event):
        cli = cli_path(context)
        if not os.path.isfile(cli):
            self.report({"ERROR"}, f"modelado-cli was not found at {cli}; set it in the add-on's preferences")
            return {"CANCELLED"}
        if self.action == "MATERIAL" and not os.path.isfile(bpy.path.abspath(context.scene.modelado.material_image)):
            self.report({"ERROR"}, "Choose a material image first")
            return {"CANCELLED"}
        if self.action == "UPSCALE":
            try:
                pending = pending_download(cli, "upscale")
            except RuntimeError as error:
                self.report({"ERROR"}, f"{TEXTURE_ACTIONS[self.action][0]}: {error}")
                return {"CANCELLED"}
            if pending.get("pending", 0) > 0:
                self._consent = consent_text(TEXTURE_ACTIONS[self.action][0], pending)
                return context.window_manager.invoke_props_dialog(self, width=480)
        return self.execute(context)

    def draw(self, context):
        for line in _wrap(self._consent, 70):
            self.layout.label(text=line)

    def execute(self, context):
        # Real-ESRGAN on a 1024 px texture is seconds on the CPU; the status bar says so meanwhile.
        context.workspace.status_text_set(f"Modelado: {TEXTURE_ACTIONS[self.action][0]}…")
        try:
            report = run_texture(context.active_object, self.action, cli_path(context), context.scene.modelado)
        except RuntimeError as error:
            self.report({"ERROR"}, f"{TEXTURE_ACTIONS[self.action][0]}: {error}")
            return {"CANCELLED"}
        finally:
            context.workspace.status_text_set(None)
        self.report({"INFO"}, f"{TEXTURE_ACTIONS[self.action][0]} in {report.get('seconds', 0):.1f} s")
        return {"FINISHED"}


class MODELADO_OT_round_trip(bpy.types.Operator):
    """Open the selected mesh in the Modelado window; each Send to Blender there replaces it here"""

    bl_idname = "modelado.round_trip"
    bl_label = "Edit in Modelado"

    _process = None
    _timer = None
    _folder = None
    _applied = 0

    @classmethod
    def poll(cls, context):
        return context.active_object is not None and context.active_object.type == "MESH" and context.mode == "OBJECT"

    def invoke(self, context, event):
        app = app_path(context)
        if not os.path.isfile(app):
            self.report({"ERROR"}, f"Modelado was not found at {app}; set it in the add-on's preferences")
            return {"CANCELLED"}

        self._name = context.active_object.name
        self._folder = tempfile.TemporaryDirectory(prefix="modelado-session-")
        start_session(context.active_object, self._folder.name, context.scene.modelado.apply_modifiers)
        self._process = subprocess.Popen([app, "--blender-session", self._folder.name])
        self._applied = 0
        self._timer = context.window_manager.event_timer_add(0.5, window=context.window)
        context.window_manager.modal_handler_add(self)
        self.report({"INFO"}, f"{self._name} is open in Modelado; Send to Blender there puts each version here")
        return {"RUNNING_MODAL"}

    def modal(self, context, event):
        # Blender stays usable meanwhile: only the timer is this operator's.
        if event.type != "TIMER":
            return {"PASS_THROUGH"}

        sent = read_sent(self._folder.name)
        obj = bpy.data.objects.get(self._name)
        if sent and sent.get("sequence", 0) > self._applied and obj is not None:
            try:
                apply_send(obj, self._folder.name, sent)
            except RuntimeError as error:
                self.report({"ERROR"}, f"Edit in Modelado: {error}")
            else:
                self._applied = sent["sequence"]
                bpy.ops.ed.undo_push(message=f"Modelado: {sent.get('label', 'edit')}")
                self.report({"INFO"}, f"{obj.name}: version {self._applied} from Modelado ({sent.get('label', 'edit')})")

        if self._process.poll() is None:
            return {"PASS_THROUGH"}

        # The window closed: the session ends, each send already its own undo step.
        context.window_manager.event_timer_remove(self._timer)
        self._folder.cleanup()
        return {"CANCELLED"}


class MODELADO_OT_cloud(bpy.types.Operator):
    """Send the selected object to a cloud service, after saying what goes where and for how much"""

    bl_idname = "modelado.cloud"
    bl_label = "Modelado cloud"
    bl_options = {"REGISTER", "UNDO"}

    action: EnumProperty(items=[(key, label, "") for key, (label, _, _) in CLOUD_ACTIONS.items()])

    _consent = ""

    @classmethod
    def poll(cls, context):
        obj = context.active_object
        return obj is not None and obj.type == "MESH" and context.mode == "OBJECT"

    def invoke(self, context, event):
        cli = cli_path(context)
        title, _, needs_texture = CLOUD_ACTIONS[self.action]
        settings = context.scene.modelado
        if not os.path.isfile(cli):
            self.report({"ERROR"}, f"modelado-cli was not found at {cli}; set it in the add-on's preferences")
            return {"CANCELLED"}
        if needs_texture and base_colour_image(context.active_object)[0] is None:
            self.report({"ERROR"}, f"{title}: the object has no image texture on its Base Color")
            return {"CANCELLED"}
        if (self.action == "TEXTURE" and not settings.texture_prompt.strip()) or (self.action == "RETEXTURE" and not settings.retexture_prompt.strip()):
            self.report({"ERROR"}, f"{title}: say what it should look like first")
            return {"CANCELLED"}
        try:
            estimate = estimate_cloud(context.active_object, self.action, cli, settings, cloud_api())
        except RuntimeError as error:
            self.report({"ERROR"}, f"{title}: {error}")
            return {"CANCELLED"}
        if not estimate.get("keyStored") or estimate.get("blocked"):
            why = estimate["consent"] if estimate.get("blocked") else f"no {estimate.get('serviceName', 'service')} key is stored; add one in Modelado's About"
            self.report({"ERROR"}, f"{title}: {why}")
            return {"CANCELLED"}
        # Nothing is sent until the dialog's button: closing it sends nothing.
        self._consent = estimate["consent"]
        try:
            return context.window_manager.invoke_props_dialog(self, width=480, title=f"{title}?", confirm_text="Send it")
        except TypeError:  # Blender before 4.1 names neither
            return context.window_manager.invoke_props_dialog(self, width=480)

    def draw(self, context):
        for line in _wrap(self._consent, 70):
            self.layout.label(text=line)

    def execute(self, context):
        title = CLOUD_ACTIONS[self.action][0]
        context.workspace.status_text_set(f"Modelado: {title}…")
        try:
            report = run_cloud(context.active_object, self.action, cli_path(context), context.scene.modelado, cloud_api())
        except RuntimeError as error:
            self.report({"ERROR"}, f"{title}: {error}")
            return {"CANCELLED"}
        finally:
            context.workspace.status_text_set(None)
        spent = f" · {report['spent']}" if report.get("spent") else ""
        self.report({"INFO"}, f"{title}{spent}")
        return {"FINISHED"}


def _wrap(text: str, width: int) -> list:
    lines, line = [], ""
    for word in text.split():
        if line and len(line) + 1 + len(word) > width:
            lines.append(line)
            line = word
        else:
            line = f"{line} {word}".strip()
    return lines + ([line] if line else [])


class MODELADO_PT_panel(bpy.types.Panel):
    bl_label = "Modelado"
    bl_space_type = "VIEW_3D"
    bl_region_type = "UI"
    bl_category = "Modelado"

    def draw(self, context):
        layout = self.layout
        settings = context.scene.modelado

        # MOD44: a new object from a photo, whatever is selected.
        box = layout.box()
        box.label(text="Generate from photo")
        box.prop(settings, "photo")
        box.prop(settings, "image")
        row = box.row(align=True)
        row.operator(MODELADO_OT_generate.bl_idname, text="3D Object").kind = "OBJECT"
        row.prop(settings, "object_resolution", text="")
        row = box.row(align=True)
        row.operator(MODELADO_OT_generate.bl_idname, text="Relief").kind = "RELIEF"
        row.prop(settings, "relief_grid")
        row = box.row(align=True)
        row.prop(settings, "relief_depth")
        row.prop(settings, "relief_base")
        if meshy_key_stored():
            row = box.row(align=True)
            row.operator(MODELADO_OT_generate.bl_idname, text=GENERATE["MESHY"][0]).kind = "MESHY"
            row.prop(settings, "meshy_texture")
        box.prop(settings, "size")

        obj = context.active_object
        if obj is None or obj.type != "MESH":
            layout.label(text="Select a mesh object to edit it")
            return

        layout.label(text=f"{obj.name}: {triangles(obj.data):,} triangles")
        layout.operator(MODELADO_OT_round_trip.bl_idname, text="Edit in Modelado")
        for kind, amount in (("SIMPLIFY", "simplify_share"), ("SUBDIVIDE", None), ("SMOOTH", "smooth_passes"),
                             ("FILL_HOLES", None), ("PIECES", "pieces_percent"), ("FLATTEN", "flatten_percent")):
            row = layout.row(align=True)
            row.operator(MODELADO_OT_edit.bl_idname, text=EDITS[kind][0]).kind = kind
            if amount:
                row.prop(settings, amount)
        layout.prop(settings, "apply_modifiers")

        # MOD45: the texture on the Base Color, replaced; the rest of the material stays. The cloud
        # actions show only where the window's About holds the service's key.
        box = layout.box()
        box.label(text="AI")
        if key_stored("meshy"):
            row = box.row(align=True)
            row.operator(MODELADO_OT_cloud.bl_idname, text=CLOUD_ACTIONS["REMESH"][0]).action = "REMESH"
            row.prop(settings, "remesh_polycount")
        if base_colour_image(obj)[0] is None:
            box.label(text="No image texture on its Base Color")
            return
        box.operator(MODELADO_OT_texture.bl_idname, text=TEXTURE_ACTIONS["UPSCALE"][0]).action = "UPSCALE"
        box.prop(settings, "material_image")
        row = box.row(align=True)
        row.operator(MODELADO_OT_texture.bl_idname, text=TEXTURE_ACTIONS["MATERIAL"][0]).action = "MATERIAL"
        row.prop(settings, "material_tiles")
        if key_stored("ideogram"):
            box.prop(settings, "texture_prompt")
            box.operator(MODELADO_OT_cloud.bl_idname, text=CLOUD_ACTIONS["TEXTURE"][0]).action = "TEXTURE"
        if key_stored("meshy"):
            box.prop(settings, "retexture_prompt")
            box.operator(MODELADO_OT_cloud.bl_idname, text=CLOUD_ACTIONS["RETEXTURE"][0]).action = "RETEXTURE"


CLASSES = (ModeladoSettings, ModeladoPreferences, MODELADO_OT_edit, MODELADO_OT_generate, MODELADO_OT_texture, MODELADO_OT_cloud, MODELADO_OT_round_trip, MODELADO_PT_panel)


def register():
    for cls in CLASSES:
        bpy.utils.register_class(cls)
    bpy.types.Scene.modelado = PointerProperty(type=ModeladoSettings)


def unregister():
    del bpy.types.Scene.modelado
    for cls in reversed(CLASSES):
        bpy.utils.unregister_class(cls)
