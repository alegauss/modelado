"""MOD43's done-when, headless: blender --background --factory-startup --python-exit-code 1
--python blender/tests/test_addon.py -- <modelado-cli.exe>

Simplify to 25% on a twice-subdivided Suzanne keeps the object's name, transform, parent and
material, lands at the triangles modelado-cli reported, keeps the mesh where it was, and one
undo step puts the original mesh back.
"""

import os
import sys

import bpy
from mathutils import Matrix, Vector

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

# MOD46: the round trip's Blender half. The window's half (Modelado.Geometry.BlenderSession) is
# played here by modelado-cli and the same sent.json it writes: the object goes to the session
# folder, gets three edits (Subdivide; then Mirror and a turn about X), is sent back twice, and
# both sends undo in order. A tall box, so the turn shows in its bounds.
import json  # noqa: E402
import subprocess  # noqa: E402

bpy.ops.mesh.primitive_cube_add(location=(0, 6, 0))
box = bpy.context.active_object
box.name = "Box"
box.data.transform(Matrix.Diagonal((1, 1, 3, 1)))
box.data.materials.append(bpy.data.materials.new("Box paint"))
bpy.context.view_layer.update()


def extents(obj):
    low, high = bounds(obj)
    return tuple(round(v, 3) for v in (high - low))


with tempfile.TemporaryDirectory(prefix="modelado-session-") as session:
    modelado.start_session(box, session, False)
    with open(os.path.join(session, "session.json"), encoding="utf-8") as described:
        check("ROUND TRIP: the session names the object and its mesh", json.load(described) == {"object": "Box", "model": "model.glb"} and os.path.isfile(os.path.join(session, "model.glb")))

    def window_sends(sequence, source, edits, label):
        subprocess.run([cli, "edit", os.path.join(session, source), "-o", os.path.join(session, f"sent-{sequence}.glb"), *edits, "--provider", "cpu"], check=True, capture_output=True)
        with open(os.path.join(session, "sent.json"), "w", encoding="utf-8") as sent:
            json.dump({"sequence": sequence, "model": f"sent-{sequence}.glb", "label": label}, sent)

    original = (modelado.triangles(box.data), extents(box))
    bpy.ops.ed.undo_push(message="before Modelado")
    window_sends(1, "model.glb", ["--subdivide"], "Subdivide")
    modelado.apply_send(bpy.data.objects["Box"], session, modelado.read_sent(session))
    bpy.ops.ed.undo_push(message="Modelado: Subdivide")
    first = (modelado.triangles(bpy.data.objects["Box"].data), extents(bpy.data.objects["Box"]))
    window_sends(2, "sent-1.glb", ["--mirror", "--rotate", "x:90"], "Turn 90° about X")
    modelado.apply_send(bpy.data.objects["Box"], session, modelado.read_sent(session))
    bpy.ops.ed.undo_push(message="Modelado: Turn 90° about X")
    second = (modelado.triangles(bpy.data.objects["Box"].data), extents(bpy.data.objects["Box"]))
    print(f"ROUND TRIP: original {original}, first send {first}, second send {second}")
    check("ROUND TRIP: the first send is the subdivided box, still tall", first[0] == original[0] * 4 and first[1][2] > first[1][1])
    check("ROUND TRIP: the second send is turned about X", second[0] == first[0] and second[1] == (first[1][0], first[1][2], first[1][1]))
    check("ROUND TRIP: the object, its place and material kept", bpy.data.objects["Box"].location.y == 6 and [s.material.name for s in bpy.data.objects["Box"].material_slots] == ["Box paint"])
    bpy.ops.ed.undo()
    check("ROUND TRIP: undo goes back to the first send", (modelado.triangles(bpy.data.objects["Box"].data), extents(bpy.data.objects["Box"])) == first)
    bpy.ops.ed.undo()
    check("ROUND TRIP: and again to the box before Modelado", (modelado.triangles(bpy.data.objects["Box"].data), extents(bpy.data.objects["Box"])) == original)

# Undo reloads the scene: a reference taken before it no longer reaches the settings.
settings = bpy.context.scene.modelado

# MOD66: a compact GLB (KHR_mesh_quantization) opens in Blender as the standard one does: the same
# vertices, where the node's scale and offset put them.
import subprocess  # noqa: E402

canyon = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "site", "public", "demo", "studio", "relief-landscape.glb")
with tempfile.TemporaryDirectory(prefix="modelado-compact-") as folder:
    opened = {}
    for kind in ("standard", "compact"):
        path = os.path.join(folder, f"{kind}.glb")
        written = subprocess.run([cli, "edit", canyon, "-o", path, "--json", "--provider", "cpu", *(["--compact"] if kind == "compact" else [])], capture_output=True, **modelado.TEXT)
        before = set(bpy.data.objects)
        bpy.ops.import_scene.gltf(filepath=path)
        made = [o for o in bpy.data.objects if o not in before and o.type == "MESH"]
        bpy.context.view_layer.update()
        opened[kind] = (len(made[0].data.vertices), *bounds(made[0])) if written.returncode == 0 and made else None
        for obj in [o for o in bpy.data.objects if o not in before]:
            bpy.data.objects.remove(obj, do_unlink=True)
    print(f"COMPACT: standard {opened['standard']}, compact {opened['compact']}, {os.path.getsize(os.path.join(folder, 'compact.glb')):,} bytes")
    check("COMPACT: Blender opens it with the standard file's vertices",
          opened["standard"] is not None and opened["compact"] is not None and opened["compact"][0] == opened["standard"][0])
    check("COMPACT: in the same place, within a quantization step",
          opened["compact"] is not None and all((opened["compact"][k] - opened["standard"][k]).length < 1e-3 for k in (1, 2)))

# MOD65: Meshy AI's image to 3D through modelado-cli cloud, against a stand-in of Meshy's API that
# answers as the UI cases' fixture server does (tests/Modelado.Cases/FixtureServer.cs): the key
# msy_case_key, and every job the cube for 30 credits. LOCALAPPDATA is this run's own, so the key
# store, settings.json and the ledger the window's About reads are too.
import http.server  # noqa: E402
import json  # noqa: E402
import subprocess  # noqa: E402
import threading  # noqa: E402

FIXTURES = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "cases", "fixtures")
# What /files/ serves: the cube for image to 3D, the subdivided cube for a remesh, a model with
# textures for a retexture, and the photo fixture for an Ideogram drawing.
FILES = {
    "cube.glb": os.path.join(FIXTURES, "download", "cube.glb"),
    "remeshed.glb": os.path.join(FIXTURES, "download", "remeshed.glb"),
    "two-textures.glb": os.path.join(FIXTURES, "two-textures.glb"),
    "candidate.png": os.path.join(FIXTURES, "download", "candidate.png"),
}
# MOD45: Meshy's remesh and retexture, as MESHY_JOBS[path] = (job id, credits, file).
MESHY_JOBS = {
    "/meshy/openapi/v1/image-to-3d": ("case-image", 30, "cube.glb"),
    "/meshy/openapi/v1/remesh": ("case-remesh", 5, "remeshed.glb"),
    "/meshy/openapi/v1/retexture": ("case-retexture", 10, "two-textures.glb"),
}


class Meshy(http.server.BaseHTTPRequestHandler):
    """Meshy AI under /meshy/ (key msy_case_key) and, for MOD45, Ideogram under /ideogram/ (key case-ideogram-key)."""

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
        if self.path.startswith("/files/") and self.path[7:] in FILES:
            with open(FILES[self.path[7:]], "rb") as served:
                return self.answer(200, served.read(), "application/octet-stream")
        if self.path.startswith("/ideogram/"):
            if self.headers.get("Api-Key") != "case-ideogram-key":
                return self.answer(401, {"message": "Access denied."})
            if self.path == "/ideogram/v2/generations/case-generation":
                return self.answer(200, {"generation_id": "case-generation", "status": "completed", "usage_cost_usd_micros": 30000,
                                         "data": [{"prompt": "oak", "seed": 7, "url": f"{root}/files/candidate.png", "is_image_safe": True}]})
            return self.answer(404, {"message": "Not found"})
        if self.headers.get("Authorization") != "Bearer msy_case_key":
            return self.answer(401, {"message": "Invalid API key"})
        if self.path == "/meshy/openapi/v1/balance":
            return self.answer(200, {"balance": 470})
        for path, (job, credits, file) in MESHY_JOBS.items():
            if self.path == f"{path}/{job}":
                sent.append("polled")
                return self.answer(200, {"id": job, "status": "SUCCEEDED", "progress": 100, "consumed_credits": credits, "model_urls": {"glb": f"{root}/files/{file}"}})
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
        if self.path.startswith("/ideogram/"):
            if self.headers.get("Api-Key") != "case-ideogram-key":
                return self.answer(401, {"message": "Access denied."})
            if self.path.endswith("?dry_run=true"):
                return self.answer(200, {"object": "price_quote", "usd_micros": 30000, "qualifier": "exact"})
            sent.append(self.path)
            return self.answer(200, {"generation_id": "case-generation", "seed": 7})
        if self.headers.get("Authorization") != "Bearer msy_case_key":
            return self.answer(401, {"message": "Invalid API key"})
        sent.append(self.path)
        job = MESHY_JOBS.get(self.path)
        return self.answer(200, {"result": job[0]}) if job else self.answer(404, {"message": "Not found"})


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

        # MOD45: the AI group's cloud actions on the textured plane, Ideogram's then Meshy's.
        root = f"http://127.0.0.1:{server.server_port}"
        ideogram = ("--cloud-api", f"ideogram={root}/ideogram/")
        check("TEXTURE: not offered before an Ideogram key is stored", not modelado.key_stored("ideogram"))
        stored = subprocess.run([cli, "cloud", "key", "ideogram", *ideogram, "--json"], input="case-ideogram-key\n", capture_output=True, **modelado.TEXT)
        check("TEXTURE: offered once one is", stored.returncode == 0 and modelado.key_stored("ideogram"))

        plane = bpy.data.objects["Panel"]
        for other in bpy.context.view_layer.objects:
            other.select_set(False)
        plane.select_set(True)
        bpy.context.view_layer.objects.active = plane
        settings.texture_prompt = "weathered oak planks"
        before_image = modelado.base_colour_image(plane)[0].image.name

        # What the operator's invoke does before its dialog: priced from the local table, nothing
        # sent. Only its execute, the dialog's Send it, runs the job. (Headless Blender has no
        # dialog to decline: invoke_props_dialog runs execute at once there.)
        posted = len(sent)
        estimate = modelado.estimate_cloud(plane, "TEXTURE", cli, settings, ideogram)
        check("TEXTURE: the price shown first, with nothing sent", estimate.get("consent") == "This description goes to Ideogram for about $0.03." and len(sent) == posted)
        check("TEXTURE: the image untouched until it is sent", modelado.base_colour_image(plane)[0].image.name == before_image)
        bpy.ops.ed.undo_push(message="before Ideogram")
        modelado.run_cloud(plane, "TEXTURE", cli, settings, ideogram)
        bpy.ops.ed.undo_push(message="Ideogram texture")
        node, material = modelado.base_colour_image(bpy.data.objects["Panel"])
        check("TEXTURE: the Base Color takes Ideogram's tile, laid on", "New texture from a prompt" in node.image.name and node.image.packed_file is not None)
        check("TEXTURE: the rest of the material is kept", material.name == "Paint" and abs(material.node_tree.nodes["Principled BSDF"].inputs["Roughness"].default_value - 0.33) < 1e-6)
        bpy.ops.ed.undo()
        check("TEXTURE: undo puts the old image back", modelado.base_colour_image(bpy.data.objects["Panel"])[0].image.name == before_image)

        # Undo reloads the scene: a reference taken before it no longer reaches the settings.
        plane = bpy.data.objects["Panel"]
        settings = bpy.context.scene.modelado
        settings.remesh_polycount = 5000
        estimate = modelado.estimate_cloud(plane, "REMESH", cli, settings, stand_in)
        check("REMESH: the price shown first", estimate.get("consent", "").endswith("goes to Meshy AI for about 5 credits."))
        report = modelado.run_cloud(plane, "REMESH", cli, settings, stand_in)
        plane = bpy.data.objects["Panel"]
        check("REMESH: the mesh is Meshy's, the object and material kept", modelado.triangles(plane.data) == report.get("triangles") and [s.material.name for s in plane.material_slots] == ["Paint"])

        settings.retexture_prompt = "rusted iron"
        report = modelado.run_cloud(plane, "RETEXTURE", cli, settings, stand_in)
        node, material = modelado.base_colour_image(bpy.data.objects["Panel"])
        check("RETEXTURE: the Base Color takes Meshy's texture, the material kept", "Retexture with Meshy AI" in node.image.name and material.name == "Paint")

        with open(os.path.join(local, "Modelado", "cloud-ledger.jsonl"), encoding="utf-8") as ledger:
            entries = [(e["Provider"], e["Action"]) for e in map(json.loads, filter(str.strip, ledger))]
        check("AI: every job in the ledger About reads", entries == [("meshy", "ImageTo3D"), ("ideogram", "TextToImage"), ("meshy", "Remesh"), ("meshy", "Retexture")])
    finally:
        server.shutdown()
        if kept_local is None:
            del os.environ["LOCALAPPDATA"]
        else:
            os.environ["LOCALAPPDATA"] = kept_local

print(f"{len(failures)} failure(s)")
sys.exit(1 if failures else 0)
