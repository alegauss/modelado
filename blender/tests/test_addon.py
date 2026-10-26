"""MOD43's done-when, headless: blender --background --factory-startup --python-exit-code 1
--python blender/tests/test_addon.py -- <modelado-cli.exe>

Simplify to 25% on a twice-subdivided Suzanne keeps the object's name, transform, parent and
material, lands at the triangles modelado-cli reported, keeps the mesh where it was, and one
undo step puts the original mesh back.
"""

import os
import sys

import bpy
from mathutils import Vector

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
import modelado  # noqa: E402  (the add-on's folder, loaded as a module beside Blender's own)

cli = sys.argv[sys.argv.index("--") + 1]
failures = []


def check(what, holds):
    print(("ok   " if holds else "FAIL ") + what)
    if not holds:
        failures.append(what)


def bounds(obj):
    corners = [obj.matrix_world @ v.co for v in obj.data.vertices]
    return Vector(min(c[i] for c in corners) for i in range(3)), Vector(max(c[i] for c in corners) for i in range(3))


bpy.ops.wm.read_factory_settings(use_empty=True)
modelado.register()

bpy.ops.object.empty_add(location=(0, 0, 1))
parent = bpy.context.active_object
bpy.ops.mesh.primitive_monkey_add(location=(1, 2, 0.5), rotation=(0.3, 0, 0.7))
suzanne = bpy.context.active_object
suzanne.name = "Suzanne"
suzanne.scale = (1.5, 1.5, 1.5)
suzanne.parent = parent
wood = bpy.data.materials.new("Wood")
suzanne.data.materials.append(wood)
subsurf = suzanne.modifiers.new("Subdivision", "SUBSURF")
subsurf.levels = 2
bpy.ops.object.modifier_apply(modifier=subsurf.name)
bpy.context.view_layer.update()

before_triangles = modelado.triangles(suzanne.data)
before_matrix = suzanne.matrix_world.copy()
before_mesh = suzanne.data.name
before_low, before_high = bounds(suzanne)
bpy.ops.ed.undo_push(message="before Simplify")

report = modelado.run_edit(suzanne, modelado.edit_args("SIMPLIFY", bpy.context.scene.modelado), cli)
bpy.ops.ed.undo_push(message="Simplify")
bpy.context.view_layer.update()

after = bpy.data.objects.get("Suzanne")
after_triangles = modelado.triangles(after.data)
after_low, after_high = bounds(after)
size = (before_high - before_low).length
print(f"{before_triangles} -> {after_triangles} triangles (modelado-cli: {report.get('triangles')})")

check("one object named Suzanne, nothing imported left over", after is not None and len([o for o in bpy.data.objects if o.type == "MESH"]) == 1)
check("its parent kept", after.parent == parent)
check("its transform kept", all(abs(a - b) < 1e-5 for ra, rb in zip(after.matrix_world, before_matrix) for a, b in zip(ra, rb)))
check("its material kept", [s.material for s in after.material_slots] == [wood])
check("its mesh name kept", after.data.name == before_mesh)
check("the triangles modelado-cli reported", after_triangles == report.get("triangles"))
check("about a quarter of the triangles", abs(after_triangles - before_triangles * 0.25) <= before_triangles * 0.02)
check("the mesh where it was", (after_low - before_low).length < size * 0.05 and (after_high - before_high).length < size * 0.05)

bpy.ops.ed.undo()
restored = bpy.data.objects.get("Suzanne")
check("undo puts the original mesh back", restored is not None and modelado.triangles(restored.data) == before_triangles)

# MOD44: Generate from photo. With no models in a folder, a first use says what it would fetch,
# which is what the add-on's consent dialog shows before anything starts.
import tempfile  # noqa: E402

with tempfile.TemporaryDirectory(prefix="modelado-empty-") as empty:
    pending = modelado.pending_download(cli, "RELIEF", models=empty)
check("with no models, a relief's first use names what it downloads", pending.get("pending", 0) > 50_000_000 and pending.get("models") == ["depth-anything-v2-small"])
check("and its licence and source", pending.get("licenses") == ["depth-anything-v2-small: Apache-2.0"] and pending.get("from") == "huggingface.co")

robot = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "docs", "robot.png")
settings = bpy.context.scene.modelado
settings.size = 0.2
settings.object_resolution = "128"


def longest(obj):
    low, high = bounds(obj)
    return max(high - low)


for kind in ("RELIEF", "OBJECT"):
    if modelado.pending_download(cli, kind).get("pending", 0) > 0:
        # The real models are needed, as for every test that runs them: none are downloaded here.
        print(f"skipped {kind}: its models are not in this machine's models folder")
        continue
    bpy.context.scene.cursor.location = (3, -1, 0.5)
    made, report = modelado.run_generate(cli, kind, robot, settings, bpy.context.scene.cursor.location.copy())
    bpy.context.view_layer.update()
    low, high = bounds(made)
    print(f"{kind}: {made.name}, {report.get('triangles')} triangles, longest side {longest(made):.4f}")
    check(f"{kind}: a new object named after the photo", made.name.startswith("robot") and made.type == "MESH")
    check(f"{kind}: its longest side the size asked for", abs(longest(made) - 0.2) < 0.002)
    check(f"{kind}: standing on the 3D cursor", abs(low.z - 0.5) < 0.002 and abs((low.x + high.x) / 2 - 3) < 0.002 and abs((low.y + high.y) / 2 + 1) < 0.002)
    shows = [s.material for s in made.material_slots if s.material]
    check(f"{kind}: a material that shows its colours", any(modelado.shows_texture(m) or any(n.type == "VERTEX_COLOR" for n in m.node_tree.nodes) for m in shows))

print(f"{len(failures)} failure(s)")
sys.exit(1 if failures else 0)
