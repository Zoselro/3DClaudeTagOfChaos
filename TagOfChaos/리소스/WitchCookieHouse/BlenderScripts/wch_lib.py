# Witch Cookie House - shared helpers (exec'd at the start of every Blender MCP call)
import bpy, bmesh, math, random
from mathutils import Vector, Matrix, Euler

COLL_NAME = "WitchCookieHouse"
PREVIEW_COLL = "Preview_NotExported"

# ---- dimensions (metres, Blender Z-up; front = -Y) ----
W = 7.5          # outer half-size of walls (15 x 15 m body)
T = 0.5          # wall thickness
H = 7.5          # eave wall height
RIDGE = 15.0     # roof underside at ridge (45 deg pitch)
OVH = 1.3        # roof overhang
RX = W + OVH     # roof half-extent (8.8)
RT = 0.85        # roof vertical thickness
DOOR_HW = 0.75   # door opening half width (1.5 m)
DOOR_H = 3.0     # door opening height (arched top)
DOOR_SPRING = 2.25  # arch spring line (rect part height)
GAP = 0.03
LEAF_T = 0.15
SIDES = {"Front": 0, "Right": 90, "Back": 180, "Left": 270}  # rotation about Z from Front(-Y)


def coll():
    c = bpy.data.collections.get(COLL_NAME)
    if c is None:
        c = bpy.data.collections.new(COLL_NAME)
        bpy.context.scene.collection.children.link(c)
    return c


def preview_coll():
    c = bpy.data.collections.get(PREVIEW_COLL)
    if c is None:
        c = bpy.data.collections.new(PREVIEW_COLL)
        bpy.context.scene.collection.children.link(c)
    return c


def principled(m):
    return next(n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED")


def set_in(node, names, value):
    for nm in names:
        if nm in node.inputs:
            node.inputs[nm].default_value = value
            return True
    return False


def mat(name):
    m = bpy.data.materials.get(name)
    if m is None:
        raise KeyError("material missing: " + name)
    return m


def empty(name, parent=None, loc=(0, 0, 0)):
    o = bpy.data.objects.get(name)
    if o is None:
        o = bpy.data.objects.new(name, None)
        o.empty_display_size = 0.5
        coll().objects.link(o)
    o.location = loc
    o.parent = parent
    return o


def remove_obj(name):
    o = bpy.data.objects.get(name)
    if o is not None:
        me = o.data if o.type == "MESH" else None
        bpy.data.objects.remove(o, do_unlink=True)
        if me is not None and me.users == 0:
            bpy.data.meshes.remove(me)


def remove_tree(name):
    o = bpy.data.objects.get(name)
    if o is None:
        return
    for ch in list(o.children_recursive):
        remove_obj(ch.name)
    remove_obj(name)


def obj_from_bm(name, bm, mats, parent=None, smooth=False):
    remove_obj(name)
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    for m in mats:
        me.materials.append(mat(m) if isinstance(m, str) else m)
    if smooth:
        for p in me.polygons:
            p.use_smooth = True
    o = bpy.data.objects.new(name, me)
    coll().objects.link(o)
    o.parent = parent
    return o


def apply_mods(o):
    """Bake the modifier stack into the mesh without bpy.ops."""
    dg = bpy.context.evaluated_depsgraph_get()
    oe = o.evaluated_get(dg)
    me = bpy.data.meshes.new_from_object(oe)
    old = o.data
    mats = [s.material for s in o.material_slots]
    o.modifiers.clear()
    o.data = me
    me.name = o.name
    if old.users == 0:
        bpy.data.meshes.remove(old)
    return o


def bevel(o, width, segs=2, angle=40, apply=True):
    md = o.modifiers.new("Bevel", "BEVEL")
    md.width = width
    md.segments = segs
    md.limit_method = "ANGLE"
    md.angle_limit = math.radians(angle)
    md.harden_normals = False
    if apply:
        apply_mods(o)
    return o


def boolean_cut(o, cutters, apply=True):
    for c in cutters:
        md = o.modifiers.new("Bool_" + c.name, "BOOLEAN")
        md.operation = "DIFFERENCE"
        md.solver = "EXACT"
        md.object = c
    if apply:
        apply_mods(o)
        for c in cutters:
            remove_obj(c.name)
    return o


# ---- bmesh primitive builders (all return bmesh with geometry already placed) ----
def bm_box(bm, size, loc=(0, 0, 0), mat_index=0, rot=None):
    r = bmesh.ops.create_cube(bm, size=1.0)
    verts = r["verts"]
    M = Matrix.Translation(loc)
    if rot is not None:
        M = M @ rot.to_matrix().to_4x4()
    M = M @ Matrix.Diagonal((size[0], size[1], size[2], 1))
    bmesh.ops.transform(bm, matrix=M, verts=verts)
    for f in {f for v in verts for f in v.link_faces}:
        f.material_index = mat_index
    return verts


def bm_sphere(bm, radius, loc=(0, 0, 0), scale=(1, 1, 1), segs=12, rings=8, mat_index=0, rot=None):
    r = bmesh.ops.create_uvsphere(bm, u_segments=segs, v_segments=rings, radius=radius)
    verts = r["verts"]
    M = Matrix.Translation(loc)
    if rot is not None:
        M = M @ rot.to_matrix().to_4x4()
    M = M @ Matrix.Diagonal((scale[0], scale[1], scale[2], 1))
    bmesh.ops.transform(bm, matrix=M, verts=verts)
    for f in {f for v in verts for f in v.link_faces}:
        f.material_index = mat_index
        f.smooth = True
    return verts


def bm_cyl(bm, r1, r2, depth, loc=(0, 0, 0), segs=12, mat_index=0, rot=None, smooth=True):
    r = bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=segs,
                              radius1=r1, radius2=r2, depth=depth)
    verts = r["verts"]
    M = Matrix.Translation(loc)
    if rot is not None:
        M = M @ rot.to_matrix().to_4x4()
    bmesh.ops.transform(bm, matrix=M, verts=verts)
    for f in {f for v in verts for f in v.link_faces}:
        f.material_index = mat_index
        f.smooth = smooth and len(f.verts) == 4
    return verts


def bm_torus(bm, R, r, loc=(0, 0, 0), rot=None, seg=24, rseg=8, mat_index=0, scale=(1, 1, 1)):
    vs = []
    grid = []
    for i in range(seg):
        a = 2 * math.pi * i / seg
        row = []
        for j in range(rseg):
            b = 2 * math.pi * j / rseg
            x = (R + r * math.cos(b)) * math.cos(a)
            y = (R + r * math.cos(b)) * math.sin(a)
            z = r * math.sin(b)
            row.append(bm.verts.new((x * scale[0], y * scale[1], z * scale[2])))
        grid.append(row)
    for i in range(seg):
        for j in range(rseg):
            f = bm.faces.new((grid[i][j], grid[(i + 1) % seg][j], grid[(i + 1) % seg][(j + 1) % rseg], grid[i][(j + 1) % rseg]))
            f.material_index = mat_index
            f.smooth = True
    verts = [v for row in grid for v in row]
    M = Matrix.Translation(loc)
    if rot is not None:
        M = M @ rot.to_matrix().to_4x4()
    bmesh.ops.transform(bm, matrix=M, verts=verts)
    return verts


def bm_tube(bm, pts, radius, segs=8, mat_index=0, radii=None, cap=True):
    """Tube along a polyline of Vector points."""
    rings = []
    n = len(pts)
    for i, p in enumerate(pts):
        if i == 0:
            t = (pts[1] - pts[0]).normalized()
        elif i == n - 1:
            t = (pts[-1] - pts[-2]).normalized()
        else:
            t = (pts[i + 1] - pts[i - 1]).normalized()
        up = Vector((0, 0, 1)) if abs(t.z) < 0.9 else Vector((1, 0, 0))
        a = t.cross(up).normalized()
        b = t.cross(a).normalized()
        rr = radii[i] if radii else radius
        ring = []
        for k in range(segs):
            ang = 2 * math.pi * k / segs
            ring.append(bm.verts.new(p + (a * math.cos(ang) + b * math.sin(ang)) * rr))
        rings.append(ring)
    for i in range(n - 1):
        for k in range(segs):
            f = bm.faces.new((rings[i][k], rings[i][(k + 1) % segs], rings[i + 1][(k + 1) % segs], rings[i + 1][k]))
            f.material_index = mat_index
            f.smooth = True
    if cap:
        for ring, rev in ((rings[0], True), (rings[-1], False)):
            f = bm.faces.new(list(reversed(ring)) if rev else ring)
            f.material_index = mat_index
    return [v for r in rings for v in r]


def bm_prism(bm, poly2d, y0, y1, mat_index=0, plane="XZ"):
    """Extrude a 2D polygon (x,z) between y0..y1 (plane XZ)."""
    bot = [bm.verts.new((x, y0, z)) for x, z in poly2d]
    top = [bm.verts.new((x, y1, z)) for x, z in poly2d]
    n = len(poly2d)
    fs = [bm.faces.new(bot), bm.faces.new(list(reversed(top)))]
    for i in range(n):
        fs.append(bm.faces.new((bot[i], bot[(i + 1) % n], top[(i + 1) % n], top[i])))
    for f in fs:
        f.material_index = mat_index
    bmesh.ops.recalc_face_normals(bm, faces=fs)
    return bot + top


def bm_gem(bm, r, h, loc, mat_index=0, sides=6):
    """Faceted crystal (bi-pyramid with a belt)."""
    top = bm.verts.new((loc[0], loc[1], loc[2] + h * 0.6))
    bot = bm.verts.new((loc[0], loc[1], loc[2] - h * 0.4))
    ring = [bm.verts.new((loc[0] + r * math.cos(2 * math.pi * i / sides), loc[1] + r * math.sin(2 * math.pi * i / sides), loc[2])) for i in range(sides)]
    for i in range(sides):
        a, b = ring[i], ring[(i + 1) % sides]
        f1 = bm.faces.new((a, b, top)); f1.material_index = mat_index
        f2 = bm.faces.new((b, a, bot)); f2.material_index = mat_index
    return ring + [top, bot]


def rotate_bm_z(bm, deg, verts=None):
    verts = verts if verts is not None else bm.verts
    bmesh.ops.rotate(bm, verts=list(verts), cent=(0, 0, 0), matrix=Matrix.Rotation(math.radians(deg), 3, "Z"))


def rotz(v, deg):
    return Matrix.Rotation(math.radians(deg), 3, "Z") @ Vector(v)


def set_origin(o, world_point):
    """Move object origin to world_point, keeping geometry in place."""
    wp = Vector(world_point)
    mw = o.matrix_world.copy()
    local = mw.inverted() @ wp
    o.data.transform(Matrix.Translation(-local))
    o.matrix_world = mw @ Matrix.Translation(local)


def shade(o, angle=40):
    me = o.data
    me.shade_smooth()
    me.set_sharp_from_angle(angle=math.radians(angle))


def inst(proto, name, loc, rot_z_deg=0.0, scale=1.0, parent=None, rot=None):
    """Linked duplicate of a prototype mesh (shared mesh data -> reusable asset)."""
    remove_obj(name)
    me = bpy.data.meshes[proto]
    o = bpy.data.objects.new(name, me)
    coll().objects.link(o)
    o.location = loc
    o.rotation_euler = rot if rot is not None else Euler((0, 0, math.radians(rot_z_deg)))
    o.scale = (scale, scale, scale) if not isinstance(scale, (tuple, list)) else scale
    o.parent = parent
    return o


def proto_mesh(name, bm, mats, smooth_angle=None):
    old = bpy.data.meshes.get(name)
    me = bpy.data.meshes.new(name) if old is None else old
    if old is not None:
        me.clear_geometry(); me.materials.clear()
    bm.to_mesh(me); bm.free()
    for m in mats:
        me.materials.append(mat(m))
    if smooth_angle:
        me.shade_smooth(); me.set_sharp_from_angle(angle=math.radians(smooth_angle))
    me.use_fake_user = True
    return me


def uv_smart(o):
    vl = bpy.context.view_layer
    for x in vl.objects:
        x.select_set(False)
    vl.objects.active = o
    o.select_set(True)
    with bpy.context.temp_override(active_object=o, object=o, selected_objects=[o], selected_editable_objects=[o]):
        bpy.ops.object.mode_set(mode='EDIT')
        bpy.ops.mesh.select_all(action='SELECT')
        bpy.ops.uv.smart_project(angle_limit=math.radians(66), island_margin=0.01, scale_to_bounds=False)
        bpy.ops.object.mode_set(mode='OBJECT')
    o.select_set(False)


def twisted_stripe_cyl(bm, r, h, segs=12, rings=20, turns=2.0, pairs=2, mats=(0, 1), z0=0.0):
    """Candy-cane cylinder: vertex rings are twisted so stripe borders follow edges (low poly, smooth stripes)."""
    grid = []
    for i in range(rings + 1):
        z = z0 + h * i / rings
        tw = 2 * math.pi * turns * i / rings
        grid.append([bm.verts.new((r * math.cos(2 * math.pi * j / segs + tw), r * math.sin(2 * math.pi * j / segs + tw), z)) for j in range(segs)])
    per = segs // (2 * pairs)
    for i in range(rings):
        for j in range(segs):
            f = bm.faces.new((grid[i][j], grid[i][(j + 1) % segs], grid[i + 1][(j + 1) % segs], grid[i + 1][j]))
            f.material_index = mats[(j // per) % 2]
            f.smooth = True
    for ring, rev in ((grid[0], True), (grid[-1], False)):
        f = bm.faces.new(list(reversed(ring)) if rev else ring)
        f.material_index = mats[0]


def parent_keep(o, parent):
    mw = o.matrix_world.copy()
    o.parent = parent
    o.matrix_parent_inverse = Matrix.Identity(4)
    o.matrix_world = mw
