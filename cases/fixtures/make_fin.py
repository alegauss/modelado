"""MOD146: writes fin.stl, a 20 x 20 x 5 mm block with a fin 10 mm long, 0.5 mm thin and 10 mm tall
standing on it, for the thickness check's case. At 20 mm (the model's own size) and a 1.2 mm
minimum, the fin's eight corners are thin and the block's are not.

    python cases/fixtures/make_fin.py
"""

from pathlib import Path

FACES = [0, 2, 1, 0, 3, 2, 4, 5, 6, 4, 6, 7, 0, 1, 5, 0, 5, 4, 1, 2, 6, 1, 6, 5, 2, 3, 7, 2, 7, 6, 3, 0, 4, 3, 4, 7]


def box(low, high):
    (x0, y0, z0), (x1, y1, z1) = low, high
    p = [(x0, y0, z0), (x1, y0, z0), (x1, y1, z0), (x0, y1, z0), (x0, y0, z1), (x1, y0, z1), (x1, y1, z1), (x0, y1, z1)]
    return [(p[FACES[i]], p[FACES[i + 1]], p[FACES[i + 2]]) for i in range(0, len(FACES), 3)]


def main() -> None:
    triangles = box((0, 0, 0), (20, 20, 5)) + box((5, 9.75, 5), (15, 10.25, 15))
    lines = ["solid fin"]
    for a, b, c in triangles:
        lines += ["  facet normal 0 0 0", "    outer loop"]
        lines += [f"      vertex {v[0]:g} {v[1]:g} {v[2]:g}" for v in (a, b, c)]
        lines += ["    endloop", "  endfacet"]
    lines.append("endsolid fin")
    Path(__file__).with_name("fin.stl").write_text("\n".join(lines) + "\n", encoding="ascii")


if __name__ == "__main__":
    main()
