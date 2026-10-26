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
    pending = modelado.pending_download(cli, "relief", models=empty)
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
    if modelado.pending_download(cli, modelado.GENERATE[kind][1]).get("pending", 0) > 0:
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

# MOD45: the AI group's texture actions on a textured plane. Only the Base Color's image changes;
# the rest of the material stays, and undo puts the old image back.
bpy.ops.mesh.primitive_plane_add(location=(-3, 0, 0))
plane = bpy.context.active_object
plane.name = "Panel"
pixels = bpy.data.images.new("Panel colours", 64, 64)
pixels.pixels = [v for y in range(64) for x in range(64) for v in (x / 63, y / 63, 0.5, 1.0)]
pixels.pack()
paint = bpy.data.materials.new("Paint")
paint.use_nodes = True
shader = paint.node_tree.nodes["Principled BSDF"]
shader.inputs["Roughness"].default_value = 0.33
texture = paint.node_tree.nodes.new("ShaderNodeTexImage")
texture.image = pixels
paint.node_tree.links.new(texture.outputs["Color"], shader.inputs["Base Color"])
plane.data.materials.append(paint)
check("the plane's Base Color image is found", modelado.base_colour_image(plane)[0] == texture)

material_image = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "cases", "fixtures", "photo.png")
settings.material_image = material_image
settings.material_tiles = 2
bpy.ops.ed.undo_push(message="before New texture")
meshes_before = len([o for o in bpy.data.objects if o.type == "MESH"])
modelado.run_texture(plane, "MATERIAL", cli, settings)
bpy.ops.ed.undo_push(message="New texture")
node, material = modelado.base_colour_image(bpy.data.objects["Panel"])
check("MATERIAL: the Base Color takes a new image, two tiles of the 64 px material across", node.image != pixels and node.image.size[0] == 128)
check("MATERIAL: the rest of the material is kept", material.name == "Paint" and abs(material.node_tree.nodes["Principled BSDF"].inputs["Roughness"].default_value - 0.33) < 1e-6)
check("MATERIAL: the image is packed into the .blend", node.image.packed_file is not None)
check("MATERIAL: nothing imported is left over", len([o for o in bpy.data.objects if o.type == "MESH"]) == meshes_before)
bpy.ops.ed.undo()
check("MATERIAL: undo puts the old image back", modelado.base_colour_image(bpy.data.objects["Panel"])[0].image.name == "Panel colours")

if modelado.pending_download(cli, "upscale").get("pending", 0) > 0:
    print("skipped UPSCALE: Real-ESRGAN is not in this machine's models folder")
else:
    plane = bpy.data.objects["Panel"]
    modelado.run_texture(plane, "UPSCALE", cli, settings)
    node, material = modelado.base_colour_image(plane)
    check("UPSCALE: the Base Color image is four times as wide", node.image.size[0] == 256)
    check("UPSCALE: the rest of the material is kept", abs(material.node_tree.nodes["Principled BSDF"].inputs["Roughness"].default_value - 0.33) < 1e-6)

# MOD65: Meshy AI's image to 3D through modelado-cli cloud, against a stand-in of Meshy's API that
# answers as the UI cases' fixture server does (tests/Modelado.Cases/FixtureServer.cs): the key
# msy_case_key, and every job the cube for 30 credits. LOCALAPPDATA is this run's own, so the key
# store, settings.json and the ledger the window's About reads are too.
import http.server  # noqa: E402
import json  # noqa: E402
import subprocess  # noqa: E402
import threading  # noqa: E402

CUBE = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "cases", "fixtures", "download", "cube.glb")


class Meshy(http.server.BaseHTTPRequestHandler):
    def log_message(self, *args):
        pass

    def answer(self, status, body, kind="application/json"):
        data = body if isinstance(body, bytes) else json.dumps(body).encode()
        self.send_response(status)
        self.send_header("Content-Type", kind)
        self.send_header("Content-Length", str(len(data)))
        self.end_headers()
        self.wfile.write(data)

    def do_GET(self):
        root = f"http://127.0.0.1:{self.server.server_port}"
        if self.path == "/files/cube.glb":
            with open(CUBE, "rb") as glb:
                return self.answer(200, glb.read(), "model/gltf-binary")
        if self.headers.get("Authorization") != "Bearer msy_case_key":
            return self.answer(401, {"message": "Invalid API key"})
        if self.path == "/meshy/openapi/v1/balance":
            return self.answer(200, {"balance": 470})
        if self.path == "/meshy/openapi/v1/image-to-3d/case-image":
            sent.append("polled")
            return self.answer(200, {"id": "case-image", "status": "SUCCEEDED", "progress": 100, "consumed_credits": 30, "model_urls": {"glb": f"{root}/files/cube.glb"}})
        return self.answer(404, {"message": "Not found"})

    def do_POST(self):
        # The whole body is read before answering, or closing on it resets the upload: .NET sends
        # a JSON body chunked, with no Content-Length.
        if self.headers.get("Transfer-Encoding", "").lower() == "chunked":
            while True:
                size = int(self.rfile.readline().split(b";")[0].strip() or b"0", 16)
                self.rfile.read(size + 2)
                if size == 0:
                    break
        else:
            self.rfile.read(int(self.headers.get("Content-Length", 0)))
        if self.headers.get("Authorization") != "Bearer msy_case_key":
            return self.answer(401, {"message": "Invalid API key"})
        sent.append(self.path)
        return self.answer(200, {"result": "case-image"} if self.path == "/meshy/openapi/v1/image-to-3d" else {"message": "Not found"})


sent = []
server = http.server.ThreadingHTTPServer(("127.0.0.1", 0), Meshy)
threading.Thread(target=server.serve_forever, daemon=True).start()
stand_in = ("--cloud-api", f"meshy=http://127.0.0.1:{server.server_port}/meshy/")
kept_local = os.environ.get("LOCALAPPDATA")
with tempfile.TemporaryDirectory(prefix="modelado-local-") as local:
    os.environ["LOCALAPPDATA"] = local
    try:
        check("MESHY: not offered before a key is stored", not modelado.meshy_key_stored())
        stored = subprocess.run([cli, "cloud", "key", "meshy", *stand_in, "--json"], input="msy_case_key\n", capture_output=True, **modelado.TEXT)
        check("MESHY: offered once a key is stored", stored.returncode == 0 and modelado.meshy_key_stored())

        estimate = modelado.cloud_estimate(cli, robot, settings, stand_in)
        print(f"MESHY: {estimate.get('consent')}")
        check("MESHY: the price shown before anything is sent", "goes to Meshy AI for about 30 credits." in estimate.get("consent", "") and not sent)

        bpy.context.scene.cursor.location = (-2, 3, 0.25)
        made, report = modelado.run_generate(cli, "MESHY", robot, settings, bpy.context.scene.cursor.location.copy(), stand_in)
        bpy.context.view_layer.update()
        low, high = bounds(made)
        check("MESHY: the photo was sent once, then followed", sent[:1] == ["/meshy/openapi/v1/image-to-3d"] and "polled" in sent)
        check("MESHY: a new object named after the photo", made.name.startswith("robot") and made.type == "MESH")
        check("MESHY: its longest side the size asked for", abs(longest(made) - 0.2) < 0.002)
        check("MESHY: standing on the 3D cursor", abs(low.z - 0.25) < 0.002 and abs((low.x + high.x) / 2 + 2) < 0.002 and abs((low.y + high.y) / 2 - 3) < 0.002)
        check("MESHY: what it cost, as reported", report.get("spent") == "30 credits")
        with open(os.path.join(local, "Modelado", "cloud-ledger.jsonl"), encoding="utf-8") as ledger:
            entries = [json.loads(line) for line in ledger if line.strip()]
        check("MESHY: the spend in the ledger the window's About reads", [(e["Provider"], e["Action"], e["Amount"], e["Estimated"]) for e in entries] == [("meshy", "ImageTo3D", 30, 30)])
    finally:
        server.shutdown()
        if kept_local is None:
            del os.environ["LOCALAPPDATA"]
        else:
            os.environ["LOCALAPPDATA"] = kept_local

print(f"{len(failures)} failure(s)")
sys.exit(1 if failures else 0)
