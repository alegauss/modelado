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

print(f"{len(failures)} failure(s)")
sys.exit(1 if failures else 0)
