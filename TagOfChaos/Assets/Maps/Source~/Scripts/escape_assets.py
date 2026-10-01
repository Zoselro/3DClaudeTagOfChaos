# Escape-mode assets (Plan.md/EscapePlan.md §4.2, §4.4) -> one FBX per prefab unit.
# Run headless:  blender -b --factory-startup --python escape_assets.py -- <scripts_dir> <blend_out> <fbx_dir>
# 1 unit = 1 m, Z-up, front = -Y (Unity +Z). Shared palette/geometry kit: maplib.py + assets.py (M(), G).
# Each FBX keeps its parts as top-level objects so Unity finds them by name directly under the prefab root:
#   ITEM_<ItemId>       one mesh, origin at the centre between both hands (ground view lifts it 0.25 m)
#   CHEST               Body + Lid (Lid origin = back hinge, opens by -X rotation in Unity)
#   SPY_Rocket_<Map>    body meshes + Slot_00/Slot_01 empties (where inserted parts appear)
#   ESC_<Map>           Body + Slot_nn empties (+ ESC_Glow / ESC_Cake_Intact / ESC_Cake_Rocket)
#   WITCH               Robe/Head/Hat/Eyes + SlamArm empty (shoulder pivot) with the arm under it; faces -Y
import bpy, sys, os, math, random
from mathutils import Vector

argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
SCRIPTS = argv[0] if len(argv) > 0 else os.path.dirname(os.path.abspath(__file__))
BLEND_OUT = argv[1] if len(argv) > 1 else os.path.join(os.path.dirname(SCRIPTS), 'Escape', 'TagOfChaos_Escape.blend')
FBX_DIR = argv[2] if len(argv) > 2 else os.path.join(os.path.dirname(os.path.dirname(os.path.dirname(SCRIPTS))),
                                                     '09. Environment', 'Escape', 'Models')

for lib in ('maplib.py', 'assets.py'):
    exec(open(os.path.join(SCRIPTS, lib), encoding='utf-8').read(), globals())

# escape-only colours (ME_*), same material rules as the map palette (M()).
PAL.update({
    'ME_Rubber_Black': ((0.08, 0.08, 0.09), 0), 'ME_Button_Red': ((0.95, 0.10, 0.10), 0.6),
    'ME_Strap_Blue': ((0.25, 0.36, 0.55), 0), 'ME_Battery_Green': ((0.50, 0.85, 0.20), 0.4),
    'ME_Toolbox_Orange': ((0.88, 0.36, 0.12), 0), 'ME_Oil_Choco': ((0.24, 0.11, 0.05), 0),
    'ME_Macaron_Pink': ((1.0, 0.62, 0.76), 0), 'ME_Cream': ((0.99, 0.95, 0.88), 0),
    'ME_Rune_Red': ((1.0, 0.15, 0.20), 1), 'ME_Rune_Pink': ((1.0, 0.35, 0.80), 1),
    'ME_Rune_Blue': ((0.20, 0.45, 1.0), 1), 'ME_Rune_Yellow': ((1.0, 0.90, 0.15), 1),
    'ME_Cell_Red': ((1.0, 0.15, 0.15), 0.6), 'ME_Cell_Orange': ((1.0, 0.45, 0.05), 0.6),
    'ME_Cell_Yellow': ((1.0, 0.90, 0.15), 0.6), 'ME_Cell_Green': ((0.30, 0.95, 0.30), 0.6),
    'ME_Hammer_Pink': ((1.0, 0.40, 0.60), 0), 'ME_Hammer_Yellow': ((1.0, 0.85, 0.25), 0),
    'ME_Stun_Body': ((0.18, 0.22, 0.30), 0), 'ME_Stun_Spark': ((0.30, 0.85, 1.0), 4),
    'ME_Balloon': ((0.30, 0.85, 1.0), 0), 'ME_Chest_Wood': ((0.55, 0.33, 0.18), 0),
    'ME_Chest_Lid': ((0.45, 0.26, 0.14), 0), 'ME_Rocket_Window': ((0.55, 0.95, 1.0), 1.5),
    'ME_Rocket_Flame': ((1.0, 0.55, 0.15), 4), 'ME_Altar_Glow': ((0.85, 0.50, 1.0), 4),
    'ME_Witch_Robe': ((0.18, 0.08, 0.25), 0), 'ME_Witch_Skin': ((0.55, 0.75, 0.45), 0),
    'ME_Witch_Eye': ((1.0, 0.90, 0.20), 6), 'ME_Witch_Hat': ((0.12, 0.05, 0.18), 0),
    'ME_Rail': ((0.40, 0.40, 0.46), 0), 'ME_Cart_Red': ((0.80, 0.15, 0.30), 0),
})

COL = None
UNITS = {}   # fbx name -> [objects]


def reset_scene():
    global COL
    bpy.ops.wm.read_factory_settings(use_empty=True)
    COL = bpy.context.scene.collection


def mesh_obj(unit, name, build, loc=(0, 0, 0), parent=None, rot=(0, 0, 0)):
    g = G()
    build(g)
    ob = bpy.data.objects.new(name, g.mesh('A_' + unit + '_' + name))
    COL.objects.link(ob)
    ob.location = loc
    ob.rotation_euler = rot
    if parent:
        ob.parent = parent
    UNITS.setdefault(unit, []).append(ob)
    return ob


def empty(unit, name, loc=(0, 0, 0), parent=None):
    ob = bpy.data.objects.new(name, None)
    ob.empty_display_size = 0.2
    COL.objects.link(ob)
    ob.location = loc
    if parent:
        ob.parent = parent
    UNITS.setdefault(unit, []).append(ob)
    return ob


def cyl(g, mat, loc, r, h, seg=20, rot=(0, 0, 0), r2=None):
    """Closed cylinder standing on loc (bottom centre), optional taper r2."""
    g.lathe([(0, 0), (r, 0), (r2 if r2 is not None else r, h), (0, h)], mat, seg=seg, loc=loc, rot=rot)


def torus(g, mat, loc, R, r, seg=24, rot=(0, 0, 0)):
    pts = [(R * math.cos(2 * math.pi * k / seg), R * math.sin(2 * math.pi * k / seg), 0) for k in range(seg + 1)]
    before = set(g.bm.verts)   # bmesh reuses freed slots, so new verts are found by set difference, not index
    g.tube(pts, r, mat, seg=10, cap=False)
    g._xf([v for v in g.bm.verts if v not in before], loc, rot)


def disc_gear(g, mat, R, thick, teeth, loc=(0, 0, 0), rot=(0, 0, 0), hole=0.0):
    before = set(g.bm.verts)
    g.lathe([(hole, -thick / 2), (R, -thick / 2), (R, thick / 2), (hole, thick / 2)] if hole > 0 else
            [(0, -thick / 2), (R, -thick / 2), (R, thick / 2), (0, thick / 2)], mat, seg=32)
    for k in range(teeth):
        a = 2 * math.pi * k / teeth
        g.rbox(mat, (math.cos(a) * (R + 0.04), math.sin(a) * (R + 0.04), -thick / 2), (0.1, 0.09, thick), bevel=0.02,
               rotz=a)
    g._xf([v for v in g.bm.verts if v not in before], loc, rot)


# =====================================================================================
# ITEMS (two-handed materials ~0.5 m, origin at the centre)
# =====================================================================================
def item(unit_id, build):
    mesh_obj('ITEM_' + unit_id, 'ITEM_' + unit_id, build)


def b_red_button(g):
    g.rbox('M_Metal_Dark', (0, 0, -0.18), (0.5, 0.5, 0.2), bevel=0.05)
    cyl(g, 'M_Metal_Light', (0, 0, 0.02), 0.2, 0.05)
    g.lathe([(0.17, 0.07), (0.17, 0.12), (0.12, 0.2), (0, 0.22)], 'ME_Button_Red', seg=24)


def b_seatbelt(g):
    pts = [(0.22 * math.cos(a), 0, 0.2 * math.sin(a)) for a in [i * 2 * math.pi / 28 for i in range(29)]]
    g.tube(pts, 0.035, 'ME_Strap_Blue', seg=8, cap=False)
    g.rbox('M_Metal_Light', (0, 0, -0.27), (0.16, 0.08, 0.12), bevel=0.02)
    g.rbox('ME_Button_Red', (0, -0.045, -0.24), (0.06, 0.02, 0.05), bevel=0.01)


def b_battery(g):
    cyl(g, 'ME_Battery_Green', (0, 0, -0.25), 0.16, 0.44, seg=24)
    cyl(g, 'M_Metal_Dark', (0, 0, -0.27), 0.165, 0.06, seg=24)
    cyl(g, 'M_Metal_Light', (0, 0, 0.19), 0.12, 0.04, seg=20)
    cyl(g, 'M_Metal_Light', (0, 0, 0.23), 0.05, 0.05, seg=12)
    g.rbox('M_Candy_White', (0, -0.16, -0.05), (0.04, 0.02, 0.16), bevel=0.005)


def b_toolbox(g):
    g.rbox('ME_Toolbox_Orange', (0, 0, -0.2), (0.6, 0.3, 0.3), bevel=0.04)
    g.rbox('M_Metal_Dark', (0, 0, 0.08), (0.62, 0.32, 0.04), bevel=0.01)
    g.tube([(-0.16, 0, 0.1), (-0.16, 0, 0.22), (0.16, 0, 0.22), (0.16, 0, 0.1)], 0.025, 'M_Metal_Dark', seg=8)
    for x in (-0.2, 0.2):
        g.rbox('M_Metal_Light', (x, -0.155, 0.02), (0.07, 0.02, 0.08), bevel=0.01)


def b_gear(g):
    disc_gear(g, 'M_Metal_Light', 0.24, 0.1, 12, rot=(math.pi / 2, 0, 0), hole=0.07)
    torus(g, 'M_Metal_Dark', (0, 0, 0), 0.08, 0.02, rot=(math.pi / 2, 0, 0))


def b_macaron_wheel(g):
    for z, s in ((-0.07, 1), (0.07, -1)):
        g.lathe([(0, 0), (0.24, 0), (0.25, 0.04 * s), (0.2, 0.09 * s), (0, 0.1 * s)], 'ME_Macaron_Pink', seg=28,
                loc=(0, z, 0), rot=(math.pi / 2 * -s, 0, 0))
    cyl(g, 'ME_Cream', (0, 0.07, 0), 0.23, 0.14, seg=28, rot=(math.pi / 2, 0, 0))
    cyl(g, 'M_Metal_Dark', (0, 0.12, 0), 0.05, 0.24, seg=12, rot=(math.pi / 2, 0, 0))


def b_cookie_wheel(g):
    rnd = random.Random(7)
    cyl(g, 'M_Cookie_Light', (0, 0.06, 0), 0.26, 0.12, seg=28, rot=(math.pi / 2, 0, 0))
    for k in range(9):
        a, r = rnd.uniform(0, 6.28), rnd.uniform(0.05, 0.2)
        g.blob('M_Chocolate_Dark', (r * math.cos(a), -0.065, r * math.sin(a)), 0.03, seg=8)
    cyl(g, 'M_Metal_Dark', (0, 0.1, 0), 0.05, 0.2, seg=12, rot=(math.pi / 2, 0, 0))


def b_chocolate_oil(g):
    g.lathe([(0, -0.26), (0.17, -0.26), (0.2, -0.1), (0.2, 0.1), (0.17, 0.24), (0, 0.24)], 'ME_Oil_Choco', seg=24)
    for z in (-0.16, 0.14):
        torus(g, 'M_Metal_Light', (0, 0, z), 0.205, 0.015)
    cyl(g, 'M_Metal_Dark', (0.07, 0, 0.23), 0.04, 0.06, seg=12)
    g.blob('M_Chocolate_Milk', (-0.05, -0.12, 0.2), 0.06, sz=(1, 0.6, 1.4), seg=10)


def b_rune(mat):
    def b(g):
        g.rbox('M_Rock', (0, 0, -0.25), (0.38, 0.14, 0.5), bevel=0.06)
        # glowing rune glyph on the front (-Y): a diamond + bar
        for (x, z, w, h, rz) in ((0, 0.05, 0.05, 0.28, 0), (0, 0.05, 0.05, 0.2, math.pi / 4), (0, 0.05, 0.05, 0.2, -math.pi / 4)):
            g.rbox(mat, (x, -0.075, z - h / 2), (w, 0.02, h), bevel=0.0, rot=(0, rz, 0))
    return b


def b_candy_cell(mat):
    def b(g):
        g.blob(mat, (0, 0, 0), 0.17, sz=(1.25, 1, 1), seg=20)
        for s in (-1, 1):
            g.lathe([(0.07, 0), (0.12, 0.08), (0.04, 0.12)], 'M_Candy_White', seg=10, loc=(0.18 * s, 0, 0),
                    rot=(0, math.pi / 2 * s, 0))
        torus(g, 'M_Metal_Light', (0, 0, 0), 0.172, 0.018, rot=(0, math.pi / 2, 0))
    return b


# tools (one-handed, held at the grip)
def b_hammer(g):
    cyl(g, 'ME_Hammer_Yellow', (0, 0, -0.22), 0.025, 0.36, seg=10)
    g.lathe([(0, -0.15), (0.09, -0.15), (0.1, -0.1), (0.1, 0.1), (0.09, 0.15), (0, 0.15)], 'ME_Hammer_Pink', seg=20,
            loc=(0, 0, 0.2), rot=(0, math.pi / 2, 0))
    for x in (-0.06, 0.0, 0.06):
        torus(g, 'M_Candy_White', (x, 0, 0.2), 0.101, 0.012, rot=(0, math.pi / 2, 0))


def b_stun_gun(g):
    g.rbox('ME_Stun_Body', (0, 0.02, 0.0), (0.08, 0.3, 0.1), bevel=0.02)
    g.rbox('M_Metal_Dark', (0, 0.1, -0.14), (0.07, 0.08, 0.16), bevel=0.02, rot=(0.25, 0, 0))
    for x in (-0.025, 0.025):
        cyl(g, 'ME_Stun_Spark', (x, -0.13, 0.05), 0.008, 0.05, seg=6, rot=(math.pi / 2, 0, 0))
    g.blob('ME_Stun_Spark', (0, -0.15, 0.05), 0.025, seg=8)


def b_balloon(g):
    g.blob('ME_Balloon', (0, 0, 0.02), 0.13, sz=(1, 1, 1.15), seg=18)
    g.lathe([(0.0, -0.15), (0.03, -0.14), (0.0, -0.12)], 'ME_Balloon', seg=8)


# =====================================================================================
# CHEST (~1.0 x 0.7 x 0.75), Lid hinge at the back top edge
# =====================================================================================
def build_chest():
    u = 'CHEST'

    def body(g):
        g.rbox('ME_Chest_Wood', (0, 0, 0), (1.0, 0.7, 0.58), bevel=0.05)
        for x in (-0.42, 0.42):
            g.rbox('M_Metal_Dark', (x, 0, -0.005), (0.06, 0.72, 0.6), bevel=0.01)
        g.rbox('M_Gold', (0, -0.36, 0.38), (0.12, 0.04, 0.14), bevel=0.02)
    mesh_obj(u, 'Body', body)

    def lid(g):  # extends to the front (-Y) from the hinge at origin
        g.lathe([(0, 0), (0.36, 0), (0.36, 1.04), (0, 1.04)], 'ME_Chest_Lid', seg=16, loc=(-0.52, -0.35, 0),
                rot=(0, math.pi / 2, 0), scale=(0.5, 1, 1))
        g.rbox('ME_Chest_Lid', (0, -0.35, 0), (1.04, 0.72, 0.06), bevel=0.02)
        for x in (-0.42, 0.42):
            g.rbox('M_Metal_Dark', (x, -0.35, 0.0), (0.06, 0.74, 0.2), bevel=0.01)
    mesh_obj(u, 'Lid', lid, loc=(0, 0.35, 0.58))


# =====================================================================================
# SPY ROCKETS (one look per map, ~4.5 m, fins within r 1.4)
# =====================================================================================
def rocket_common(u, body_mat, nose_mat, fin_mat, stripe=None, H=4.2, R=0.75):
    def body(g):
        prof = [(0, 0.35), (R * 0.75, 0.35), (R, 0.9), (R, H * 0.65), (R * 0.75, H * 0.85), (R * 0.3, H * 0.97), (0, H)]
        segmat = (lambda k: stripe if (k // 3) % 2 == 0 else body_mat) if stripe else None
        g.lathe(prof[:5], body_mat, seg=24, segmat=segmat)
        g.lathe(prof[4:], nose_mat, seg=24)
        g.lathe([(0, 0.05), (R * 0.55, 0.05), (R * 0.7, 0.4), (0, 0.4)], 'M_Metal_Dark', seg=20)   # nozzle
        g.lathe([(0, -0.35), (R * 0.45, 0.05), (0, 0.05)], 'ME_Rocket_Flame', seg=16)              # pilot flame
        for k in range(3):
            a = math.pi / 2 + 2 * math.pi * k / 3
            g.prism(fin_mat, (math.cos(a) * (R + 0.25), math.sin(a) * (R + 0.25), 0.2), 0.8, 0.12, 1.3, rotz=a)
        g.blob('ME_Rocket_Window', (0, -R * 0.95, H * 0.62), 0.24, sz=(1, 0.35, 1), seg=16)
        torus(g, 'M_Metal_Light', (0, -R * 0.95, H * 0.62), 0.25, 0.04, rot=(math.pi / 2, 0, 0))
    mesh_obj(u, 'Body', body)
    for i, x in enumerate((-0.42, 0.42)):   # inserted parts sit on the front, under the window
        empty(u, f'Slot_{i:02d}', (x, -R - 0.18, 1.55))


ROCKETS = {
    'CursedCandyCarnival': dict(body_mat='M_Candy_White', nose_mat='M_Neon_Pink', fin_mat='M_Candy_Red', stripe='M_Candy_Red'),
    'HauntedBakery': dict(body_mat='M_Cookie_Light', nose_mat='M_Waffle', fin_mat='M_Brick_Oven'),
    'ChocolateFactory': dict(body_mat='M_Chocolate_Milk', nose_mat='M_Chocolate_Dark', fin_mat='M_Gold', stripe='M_Chocolate_Dark'),
    'GingerbreadVillage': dict(body_mat='M_Cookie_Wall', nose_mat='M_Icing_Pink', fin_mat='M_Gingerbread_Dark', stripe='M_Icing_Pink'),
    'CandyForest': dict(body_mat='M_Candy_Pink', nose_mat='M_Candy_Teal', fin_mat='M_Candy_Purple', stripe='M_Candy_White'),
}


# =====================================================================================
# ESCAPE DEVICES (footprint within r 3.2; Slot_nn = where parts are shown)
# =====================================================================================
def ring_slots(u, n, r, z, start=0):
    for i in range(n):
        a = -math.pi / 2 + 2 * math.pi * i / max(1, n)
        empty(u, f'Slot_{start + i:02d}', (r * math.cos(a), r * math.sin(a), z))


def device_carnival():   # roller-coaster car on a short track
    u = 'ESC_CursedCandyCarnival'

    def body(g):
        # track parts stay below the monster's step (0.15 m) so nobody gets a cookie-only strip between the rails
        for y in (-0.7, 0.7):
            g.rbox('ME_Rail', (0, y, 0.0), (5.6, 0.18, 0.12), bevel=0.03)
        for x in (-2.4, -1.2, 0, 1.2, 2.4):
            g.rbox('M_Wood_Dark', (x, 0, 0), (0.3, 1.8, 0.08), bevel=0.02)
        g.rbox('ME_Cart_Red', (0, 0, 0.35), (3.4, 1.7, 0.9), bevel=0.2)
        for x in (-0.8, 0.6):
            g.rbox('M_Tent_Purple', (x, 0.1, 1.2), (0.9, 1.3, 0.7), bevel=0.15)
        g.rbox('M_Metal_Dark', (-1.75, 0, 0.6), (0.3, 1.5, 1.1), bevel=0.08)   # front control panel
        for x in (-1.3, 1.3):
            for y in (-0.86, 0.86):
                cyl(g, 'M_Metal_Dark', (x, y, 0.32), 0.25, 0.12, rot=(math.pi / 2, 0, 0))
    mesh_obj(u, 'Body', body)
    # 8 slots: button, belt, 3 battery, 3 repair (positions only; Unity shows sockets/parts there)
    slots = [(-1.92, -0.3, 1.75), (-0.8, -0.8, 1.6), (1.6, -0.95, 0.95), (0.8, -0.95, 0.95), (0.0, -0.95, 0.95),
             (1.6, 0.95, 0.95), (0.8, 0.95, 0.95), (0.0, 0.95, 0.95)]
    for i, p in enumerate(slots):
        empty(u, f'Slot_{i:02d}', p)


def device_bakery():     # big dough machine
    u = 'ESC_HauntedBakery'

    def body(g):
        g.rbox('M_Metal_Dark', (0, 0, 0), (3.6, 2.2, 0.3), bevel=0.05)
        g.rbox('M_Metal_Light', (0, 0, 0.3), (3.2, 1.8, 2.2), bevel=0.25)
        g.rbox('M_Brick_Oven', (0, -0.95, 0.6), (1.4, 0.1, 1.0), bevel=0.05)          # machine door
        g.rbox('M_Oven_Fire', (0, -1.0, 0.75), (1.0, 0.04, 0.6), bevel=0.0)
        cyl(g, 'M_Metal_Light', (0.9, 0.3, 2.5), 0.35, 1.2, seg=16)                    # chimney
        cyl(g, 'M_Metal_Dark', (-0.9, 0.3, 2.5), 0.5, 0.25, seg=20)
        disc_gear(g, 'M_Gold', 0.45, 0.12, 10, loc=(-0.9, 0.3, 2.85))
        for x in (-1.2, 1.2):
            g.tube([(x, -0.9, 2.2), (x, -1.2, 2.6), (x, -0.8, 2.9)], 0.08, 'M_Metal_Dark', seg=8)
    mesh_obj(u, 'Body', body)
    slots = [(-1.1, -1.0, 1.9), (-0.9, 0.3, 3.1), (1.7, -0.6, 1.8), (1.7, 0.0, 1.8), (1.7, 0.6, 1.8),
             (-1.7, -0.6, 1.8), (-1.7, 0.0, 1.8), (-1.7, 0.6, 1.8)]
    for i, p in enumerate(slots):
        empty(u, f'Slot_{i:02d}', p)


def device_factory():    # chocolate train: wheels are the wheel slots
    u = 'ESC_ChocolateFactory'

    def body(g):
        for y in (-0.75, 0.75):
            g.rbox('ME_Rail', (0, y, 0.0), (6.0, 0.16, 0.12), bevel=0.03)
        g.rbox('M_Chocolate_Dark', (0.4, 0, 0.55), (4.2, 1.4, 0.4), bevel=0.08)        # chassis
        cyl(g, 'M_Chocolate_Milk', (-0.4, 0, 1.45), 0.75, 2.4, seg=24, rot=(0, math.pi / 2, 0))  # boiler
        g.rbox('M_Chocolate_Milk', (1.6, 0, 0.95), (1.5, 1.5, 1.7), bevel=0.15)       # cab
        g.rbox('M_Chocolate_Dark', (1.6, 0, 2.6), (1.8, 1.7, 0.18), bevel=0.06)
        cyl(g, 'M_Chocolate_Dark', (-1.5, 0, 2.0), 0.28, 0.9, seg=16, r2=0.38)          # chimney
        g.rbox('M_Gold', (-2.0, 0, 0.6), (0.3, 1.5, 0.5), bevel=0.06)                   # cow catcher
    mesh_obj(u, 'Body', body)
    # macaron x2 + cookie x2 (wheels), oil x2 (tanks on top), gear x2 (side gearbox)
    slots = [(-1.2, -0.85, 0.45), (1.2, -0.85, 0.45), (-1.2, 0.85, 0.45), (1.2, 0.85, 0.45),
             (-0.9, 0.0, 2.35), (0.1, 0.0, 2.35), (0.4, -0.85, 1.25), (0.4, 0.85, 1.25)]
    for i, p in enumerate(slots):
        empty(u, f'Slot_{i:02d}', p)


def device_gingerbread():    # rune altar in the ruins
    u = 'ESC_GingerbreadVillage'

    # sloped mound (walkable for the monster too) with a slim rune pillar; cracked edge stones give the ruin look
    def body(g):
        rnd = random.Random(11)
        g.lathe([(0, 0), (2.6, 0), (1.6, 0.45), (0, 0.45)], 'M_Cookie_Stone', seg=8,
                radial=lambda a: 1 + 0.05 * math.sin(a * 5))
        g.lathe([(0, 0.45), (0.65, 0.45), (0.5, 1.15), (0.7, 1.35), (0, 1.4)], 'M_Rock', seg=8)
        for k in range(6):                                                               # flat rubble chips
            a = rnd.uniform(0, 6.28)
            g.rbox('M_Cookie_Stone', (math.cos(a) * 2.2, math.sin(a) * 2.2, 0.0), (0.5, 0.35, 0.1), bevel=0.03, rotz=a)
    mesh_obj(u, 'Body', body)

    def glow(g):
        torus(g, 'ME_Altar_Glow', (0, 0, 1.45), 0.8, 0.08)
        g.lathe([(0.5, 1.4), (0.6, 4.5), (0.0, 5.2)], 'ME_Altar_Glow', seg=16, cap=False)
    mesh_obj(u, 'ESC_Glow', glow)
    empty(u, 'Slot_00', (0, 0, 1.5))


def device_candy():      # giant cake that breaks open into a rocket
    u = 'ESC_CandyForest'

    def base(g):
        cyl(g, 'M_Candy_Teal', (0, 0, 0), 2.6, 0.3, seg=32)
    mesh_obj(u, 'Body', base)

    def cake(g):
        cyl(g, 'M_Sugar_Pink', (0, 0, 0.3), 2.2, 0.9, seg=32)
        cyl(g, 'ME_Cream', (0, 0, 1.2), 2.25, 0.12, seg=32)
        cyl(g, 'M_Candy_Pink', (0, 0, 1.32), 1.6, 0.8, seg=32)
        cyl(g, 'ME_Cream', (0, 0, 2.12), 1.65, 0.12, seg=32)
        for k in range(10):
            a = 2 * math.pi * k / 10
            g.blob('M_Candy_Red', (math.cos(a) * 1.3, math.sin(a) * 1.3, 2.35), 0.18, seg=10)
        cyl(g, 'M_Candy_White', (0, 0, 2.24), 0.08, 0.6, seg=8)
        g.blob('M_Lantern_Glow', (0, 0, 2.95), 0.12, sz=(1, 1, 1.6), seg=8)
    mesh_obj(u, 'ESC_Cake_Intact', cake)

    def cake_rocket(g):
        rnd = random.Random(3)
        for k in range(7):                                                               # shards around the base
            a = 2 * math.pi * k / 7 + rnd.uniform(-0.2, 0.2)
            g.prism('M_Sugar_Pink', (math.cos(a) * 2.0, math.sin(a) * 2.0, 0.3), 1.1, 0.5, rnd.uniform(0.5, 0.9), rotz=a)
        g.lathe([(0, 0.3), (0.9, 0.3), (1.0, 1.0), (1.0, 3.6), (0.7, 4.6), (0, 5.2)], 'M_Candy_White', seg=24,
                segmat=lambda k: 'M_Candy_Pink' if (k // 2) % 2 == 0 else 'M_Candy_White')
        g.blob('ME_Rocket_Window', (0, -0.95, 3.0), 0.3, sz=(1, 0.35, 1), seg=16)
        for k in range(3):
            a = math.pi / 2 + 2 * math.pi * k / 3
            g.prism('M_Candy_Teal', (math.cos(a) * 1.1, math.sin(a) * 1.1, 0.3), 0.9, 0.14, 1.4, rotz=a)
    mesh_obj(u, 'ESC_Cake_Rocket', cake_rocket)
    empty(u, 'Slot_00', (0, -2.35, 0.9))


# =====================================================================================
# WITCH (~90 m, stands outside the map, faces -Y = Unity +Z)
# =====================================================================================
def build_witch(h=90.0):
    u = 'WITCH'

    def robe(g):
        g.lathe([(0, 0), (h * 0.36, 0), (h * 0.3, h * 0.2), (h * 0.2, h * 0.5), (h * 0.14, h * 0.64), (0, h * 0.66)],
                'ME_Witch_Robe', seg=24, radial=lambda a: 1 + 0.06 * math.sin(a * 7))
        torus(g, 'M_Candy_Purple', (0, 0, h * 0.62), h * 0.15, h * 0.02)
    mesh_obj(u, 'Robe', robe)

    def head(g):
        g.blob('ME_Witch_Skin', (0, 0, h * 0.75), h * 0.11, sz=(1, 0.95, 1.1), seg=20)
        g.lathe([(0, 0), (h * 0.025, 0), (0, h * 0.09)], 'ME_Witch_Skin', seg=10, loc=(0, -h * 0.1, h * 0.74),
                rot=(math.pi / 2 + 0.5, 0, 0))                                           # crooked nose
        for s in (-1, 1):                                                                # stringy hair
            g.tube([(s * h * 0.09, h * 0.02, h * 0.82), (s * h * 0.14, h * 0.04, h * 0.7), (s * h * 0.15, h * 0.02, h * 0.58)],
                   h * 0.02, 'M_Pine_Dark', seg=8)
    mesh_obj(u, 'Head', head)

    def hat(g):
        g.lathe([(0, 0), (h * 0.22, 0), (h * 0.23, h * 0.012), (0, h * 0.012)], 'ME_Witch_Hat', seg=28,
                loc=(0, 0, h * 0.83))
        g.tube([(0, 0, h * 0.84), (0, 0, h * 0.95), (h * 0.02, h * 0.03, h * 1.03), (h * 0.07, h * 0.06, h * 1.06)],
               [h * 0.1, h * 0.07, h * 0.035, h * 0.008], 'ME_Witch_Hat', seg=16)
        torus(g, 'M_Gold', (0, 0, h * 0.855), h * 0.098, h * 0.012)
    mesh_obj(u, 'Hat', hat)

    def eyes(g):
        for s in (-1, 1):
            g.blob('ME_Witch_Eye', (s * h * 0.045, -h * 0.095, h * 0.78), h * 0.022, sz=(1, 0.6, 0.8), seg=10)
    mesh_obj(u, 'Eyes', eyes)

    def left_arm(g):
        g.tube([(-h * 0.13, 0, h * 0.6), (-h * 0.22, -h * 0.05, h * 0.42), (-h * 0.2, -h * 0.12, h * 0.32)],
               [h * 0.05, h * 0.04, h * 0.035], 'ME_Witch_Robe', seg=12)
        g.blob('ME_Witch_Skin', (-h * 0.2, -h * 0.14, h * 0.29), h * 0.05, seg=12)
    mesh_obj(u, 'ArmLeft', left_arm)

    # slam arm: pivot at the right shoulder, arm points along -Y (forward) at rest so a -X/+X pitch swings it
    shoulder = (h * 0.2, 0, h * 0.6)
    pivot = empty(u, 'SlamArm', shoulder)

    def right_arm(g):
        g.tube([(0, 0, 0), (0, -h * 0.22, 0), (0, -h * 0.4, 0)], [h * 0.055, h * 0.045, h * 0.04], 'ME_Witch_Robe', seg=12)
        g.blob('ME_Witch_Skin', (0, -h * 0.46, 0), h * 0.09, sz=(1.1, 1, 0.7), seg=14)
        for k in range(4):
            g.tube([((k - 1.5) * h * 0.035, -h * 0.5, 0), ((k - 1.5) * h * 0.04, -h * 0.57, -h * 0.03)],
                   h * 0.014, 'ME_Witch_Skin', seg=6)
    arm = mesh_obj(u, 'Arm', right_arm, parent=pivot)
    arm.location = (0, 0, 0)


# =====================================================================================
# export
# =====================================================================================
def export_unit(unit, path):
    bpy.ops.object.select_all(action='DESELECT')
    for ob in UNITS[unit]:
        ob.select_set(True)
    kw = dict(filepath=path, use_selection=True, object_types={'EMPTY', 'MESH'}, apply_unit_scale=True,
              apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y', bake_space_transform=True,
              use_mesh_modifiers=True, mesh_smooth_type='FACE', use_tspace=False, add_leaf_bones=False,
              bake_anim=False, path_mode='RELATIVE', embed_textures=False, use_custom_props=False)
    bpy.ops.export_scene.fbx(**kw)


def finish_unit(unit):
    """Export the unit, then rename its objects so the next unit can reuse plain names (Body, Slot_00, ...)."""
    export_unit(unit, os.path.join(FBX_DIR, unit + '.fbx'))
    for ob in UNITS[unit]:
        ob.name = unit + '|' + ob.name
    DONE.append(unit)


DONE = []


def build(unit, fn, *args, **kw):
    fn(*args, **kw)
    finish_unit(unit)


def main():
    reset_scene()
    os.makedirs(FBX_DIR, exist_ok=True)
    items = [('RedButton', b_red_button), ('Seatbelt', b_seatbelt), ('Battery', b_battery), ('Toolbox', b_toolbox),
             ('Gear', b_gear), ('MacaronWheel', b_macaron_wheel), ('CookieWheel', b_cookie_wheel),
             ('ChocolateOil', b_chocolate_oil), ('Hammer', b_hammer), ('StunGun', b_stun_gun),
             ('WaterBalloon', b_balloon)]
    for c in ('Red', 'Pink', 'Blue', 'Yellow'):
        items.append(('Rune_' + c, b_rune('ME_Rune_' + c)))
    for c in ('Red', 'Orange', 'Yellow', 'Green'):
        items.append(('CandyCell_' + c, b_candy_cell('ME_Cell_' + c)))
    for iid, fn in items:
        build('ITEM_' + iid, item, iid, fn)
    build('CHEST', build_chest)
    for mp, kw in ROCKETS.items():
        build('SPY_Rocket_' + mp, rocket_common, 'SPY_Rocket_' + mp, **kw)
    build('ESC_CursedCandyCarnival', device_carnival)
    build('ESC_HauntedBakery', device_bakery)
    build('ESC_ChocolateFactory', device_factory)
    build('ESC_GingerbreadVillage', device_gingerbread)
    build('ESC_CandyForest', device_candy)
    build('WITCH', build_witch)

    # lay units out side by side in the .blend for review only (each FBX was exported at its own origin)
    x = 0.0
    for unit, obs in UNITS.items():
        for ob in obs:
            if ob.parent is None:
                ob.location.x += x
        x += 120.0 if unit == 'WITCH' else 8.0
    os.makedirs(os.path.dirname(BLEND_OUT), exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=BLEND_OUT)
    print('ESCAPE_ASSETS_OK', len(DONE), ','.join(DONE))


main()
