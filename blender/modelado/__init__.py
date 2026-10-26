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


def start_cli(cli: str, source: str, target: str, args: list) -> subprocess.Popen:
    """modelado-cli edit, its result as JSON on stdout and nothing on screen."""
    flags = getattr(subprocess, "CREATE_NO_WINDOW", 0)
    return subprocess.Popen(
        [cli, "edit", source, "-o", target, *args, "--json", "--provider", "cpu"],
        stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True, encoding="utf-8", creationflags=flags)


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
}


def generate_args(kind: str, settings) -> list:
    """modelado-cli's options for one generation, at the values the panel holds."""
    if kind == "OBJECT":
        return ["--resolution", settings.object_resolution]
    return ["--grid", str(settings.relief_grid), "--depth", f"{settings.relief_depth:.4f}", "--base", f"{settings.relief_base:.4f}"]


def run_cli(cli: str, args: list) -> dict:
    """modelado-cli with --json, waited for; its report, or a RuntimeError saying why there is none."""
    flags = getattr(subprocess, "CREATE_NO_WINDOW", 0)
    process = subprocess.Popen(
        [cli, *args, "--json", "--provider", "cpu"],
        stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True, encoding="utf-8", creationflags=flags)
    return read_report(process)


def pending_download(cli: str, kind: str, models: str = None) -> dict:
    """What generating would download first: download --dry-run's report (pending bytes, models, licenses, from)."""
    return run_cli(cli, ["download", GENERATE[kind][1], "--dry-run", *(["--models", models] if models else [])])


def start_generate(cli: str, kind: str, photo: str, target: str, settings) -> subprocess.Popen:
    """modelado-cli object or relief on the photo, its result as JSON on stdout."""
    flags = getattr(subprocess, "CREATE_NO_WINDOW", 0)
    return subprocess.Popen(
        [cli, GENERATE[kind][1], photo, "-o", target, *generate_args(kind, settings), "--json", "--provider", "cpu"],
        stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True, encoding="utf-8", creationflags=flags)


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


def run_generate(cli: str, kind: str, photo: str, settings, location) -> tuple:
    """One generation, start to end, on the window's thread; the panel's operator runs the CLI off it."""
    with tempfile.TemporaryDirectory(prefix="modelado-") as folder:
        target = os.path.join(folder, "out.glb")
        report = read_report(start_generate(cli, kind, photo, target, settings))
        name = os.path.splitext(os.path.basename(photo))[0]
        return import_generated(target, name, settings.size, location), report


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


class ModeladoPreferences(bpy.types.AddonPreferences):
    bl_idname = __package__

    cli: StringProperty(name="modelado-cli", description="modelado-cli.exe, installed with Modelado", subtype="FILE_PATH", default=default_cli())

    def draw(self, context):
        self.layout.prop(self, "cli")


def cli_path(context) -> str:
    addon = context.preferences.addons.get(__package__)
    return addon.preferences.cli if addon else default_cli()


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

        try:
            pending = pending_download(cli, self.kind)
        except RuntimeError as error:
            self._folder.cleanup()
            self.report({"ERROR"}, f"{GENERATE[self.kind][0]}: {error}")
            return {"CANCELLED"}

        if pending.get("pending", 0) > 0:
            # As the window's consent panel: what, how large, from where, and that the photo stays here.
            self._consent = (f"{GENERATE[self.kind][0]} needs a one-time download of {pending['pending'] / 1e6:,.0f} MB "
                             f"from {pending.get('from') or 'Hugging Face'}: {', '.join(pending.get('licenses', []))}. "
                             "Your photo stays on this computer.")
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
        self._process = start_generate(cli_path(context), self.kind, self._photo, self._target, settings)
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
        self.report({"INFO"}, f"{GENERATE[self.kind][0]}: {obj.name}, {report.get('triangles', 0):,} triangles in {report.get('seconds', 0):.1f} s")
        return {"FINISHED"}

    def _finish(self, context):
        context.window_manager.event_timer_remove(self._timer)
        context.window_manager.progress_end()
        context.workspace.status_text_set(None)
        self._folder.cleanup()


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
        box.prop(settings, "size")

        obj = context.active_object
        if obj is None or obj.type != "MESH":
            layout.label(text="Select a mesh object to edit it")
            return

        layout.label(text=f"{obj.name}: {triangles(obj.data):,} triangles")
        for kind, amount in (("SIMPLIFY", "simplify_share"), ("SUBDIVIDE", None), ("SMOOTH", "smooth_passes"),
                             ("FILL_HOLES", None), ("PIECES", "pieces_percent"), ("FLATTEN", "flatten_percent")):
            row = layout.row(align=True)
            row.operator(MODELADO_OT_edit.bl_idname, text=EDITS[kind][0]).kind = kind
            if amount:
                row.prop(settings, amount)
        layout.prop(settings, "apply_modifiers")


CLASSES = (ModeladoSettings, ModeladoPreferences, MODELADO_OT_edit, MODELADO_OT_generate, MODELADO_PT_panel)


def register():
    for cls in CLASSES:
        bpy.utils.register_class(cls)
    bpy.types.Scene.modelado = PointerProperty(type=ModeladoSettings)


def unregister():
    del bpy.types.Scene.modelado
    for cls in reversed(CLASSES):
        bpy.utils.unregister_class(cls)
