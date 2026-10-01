# Escape-mode cookie escape devices (exec'd by escape_assets.py). EscapeVisualPlan.md §5.
import math, random

# =====================================================================================
# ESCAPE DEVICES (footprint within r 3.2; Slot_nn = where parts are shown)
# =====================================================================================
def ring_slots(u, n, r, z, start=0):
    for i in range(n):
        a = -math.pi / 2 + 2 * math.pi * i / max(1, n)
        empty(u, f'Slot_{start + i:02d}', (r * math.cos(a), r * math.sin(a), z))


def frustum(g, mat, x0, x1, y0, y1, h, slope):
    """Flat deck x0..x1, y0..y1 with all four sides sloped outward by `slope` (walkable ramp edges)."""
    bm = g.bm
    lo = [bm.verts.new(p) for p in ((x0 - slope, y0 - slope, 0), (x1 + slope, y0 - slope, 0),
                                    (x1 + slope, y1 + slope, 0), (x0 - slope, y1 + slope, 0))]
    hi = [bm.verts.new(p) for p in ((x0, y0, h), (x1, y0, h), (x1, y1, h), (x0, y1, h))]
    mi = g.mi(mat)
    for f in [hi, list(reversed(lo))] + [(lo[k], lo[(k + 1) % 4], hi[(k + 1) % 4], hi[k]) for k in range(4)]:
        bm.faces.new(f).material_index = mi


# ---------------- CursedCandyCarnival: roller coaster that runs out of the map (EscapeVisualPlan.md §5.2) ----------------
# Travel direction = -Y (Unity +Z): EscapeMapSetup points it at the nearest map edge. Station rails sit at z 0 (low enough
# to step over), the lift hill and the run beyond the wall are visual only (no colliders). Unity moves the cars along the
# Path_nn empties (rail top, root space). Boarding deck on +X.
COASTER_RAIL = 0.14                       # rail top height = car origin height
COASTER_CARS = (-2.5, 0.1, 2.7)           # car centres (y) at rest, lead car first
COASTER_SEAT_ROWS = (-0.45, 0.55)


def coaster_height(y):
    """Track height (rail base) at y: flat station -> lift hill -> crest -> drop -> run-out."""
    def ease(t):
        t = min(1.0, max(0.0, t))
        return t * t * (3 - 2 * t)
    if y > -9:
        return 0.0
    if y > -37:
        return 14.0 * ease((-9 - y) / 28)
    if y > -55:
        return 14.0 + 0.6 * math.sin(math.pi * (-37 - y) / 18)
    if y > -70:
        return 14.0 - 10.0 * ease((-55 - y) / 15)
    return 4.0


def device_carnival():
    u = 'ESC_CursedCandyCarnival'
    ys = [6.0 - k * 0.5 for k in range(int((6.0 + 125.0) / 0.5) + 1)]           # +6 (behind the tail) .. -125

    def body(g):
        frustum(g, 'ME_Gray_Dark', 1.3, 4.1, -4.4, 4.6, 0.3, 0.9)                 # boarding deck, ramped on all sides
        for k in range(9):
            g.rbox('ME_Gold', (2.7, -4.0 + k, 0.3), (2.6, 0.08, 0.02), bevel=0.0)  # deck planks
        for x in (-0.6, 0.6):                                                     # station rails (low: walk over them)
            g.rbox('ME_Gray_Light', (x, -1.5, 0.0), (0.16, 15.0, COASTER_RAIL), bevel=0.03)
        for k in range(11):
            g.rbox('ME_Cookie_Dark', (0, 5.5 - k * 1.5, 0.0), (1.7, 0.3, 0.07), bevel=0.02)
        for x in (-2.3, 4.6):                                                     # entrance arch over the lead car
            cyl(g, 'ME_Purple_Deep', (x, -4.7, 0), 0.28, 5.0, seg=12)
            g.blob('ME_Gold', (x, -4.7, 5.1), 0.42, seg=12)
        g.rbox('ME_Purple_Deep', (1.15, -4.7, 4.5), (7.4, 0.5, 0.8), bevel=0.2)
        g.rbox('ME_Teal', (1.15, -4.98, 4.65), (5.0, 0.08, 0.5), bevel=0.04)      # sign board
        for k in range(3):                                                        # three candy stars on the sign
            star_prism(g, 'ME_Orange_Warm', (-0.35 + k * 1.5, -5.05, 4.9), 0.32, 0.14, 0.08)
    mesh_obj(u, 'Body', body)

    def bulbs_off(g):                                                             # unlit sockets (always visible)
        for p in BULBS:
            g.blob('ME_Gray_Light', p, 0.15, seg=8)
    BULBS = [(-2.3 + 6.9 * k / 9, -4.98, 5.3) for k in range(10)] + \
            [(x, -4.98, z) for z in (1.5, 2.6, 3.7) for x in (-2.3, 4.6)]
    mesh_obj(u, 'Bulbs_Off', bulbs_off)
    for i, p in enumerate(BULBS):                                                 # lit bulbs, switched on in order by Unity
        mesh_obj(u, f'Bulb_{i:02d}', lambda g: g.blob('ME_Glow_Orange', (0, 0, 0), 0.2, seg=10), loc=p)

    def track(g):                                                                 # lift hill + run-out (visual only)
        pts = [(0, y, coaster_height(y)) for y in ys if y <= -8.5]
        for x in (-0.6, 0.6):
            g.tube([(x, p[1], p[2] + COASTER_RAIL - 0.06) for p in pts[::2]], 0.08, 'ME_Gray_Light', seg=6, cap=False)
        g.tube([(0, p[1], p[2] - 0.2) for p in pts[::2]], 0.16, 'ME_Purple_Deep', seg=8, cap=False)
        for y in [p for p in ys if p <= -9][::3]:
            z = coaster_height(y)
            g.rbox('ME_Cookie_Dark', (0, y, z - 0.07), (1.6, 0.25, 0.07), bevel=0.02)
        for y in range(-12, -125, -6):                                            # support columns with a T head
            z = coaster_height(y)
            if z < 1.2:
                continue
            cyl(g, 'ME_Purple_Deep', (0, y, -3.0), 0.22, z + 2.7, seg=10)
            g.rbox('ME_Purple_Deep', (0, y, z - 0.45), (1.6, 0.35, 0.25), bevel=0.06)
    mesh_obj(u, 'Track', track)

    for k in range(0, len(ys), 6):                                                # path for the cars (rail top)
        empty(u, f'Path_{k // 6:02d}', (0, ys[k], coaster_height(ys[k]) + COASTER_RAIL))

    slot = 0
    for c, cy in enumerate(COASTER_CARS):
        lead = c == 0
        car = None

        def car_body(g, lead=lead):
            g.rbox('ME_Gray_Dark', (0, 0, 0.0), (1.5, 2.1, 0.3), bevel=0.08)               # chassis
            for x in (-0.68, 0.68):
                for y in (-0.7, 0.7):
                    cyl(g, 'ME_Gray_Light', (x, y, 0.2), 0.2, 0.16, seg=12, rot=(0, math.pi / 2, 0))
            g.rbox('ME_Purple_Deep', (0, 0, 0.3), (1.9, 2.4, 0.85), bevel=0.28)            # tub
            g.rbox('ME_Orange_Warm', (0, 0, 0.78), (1.96, 2.46, 0.16), bevel=0.06)        # stripe
            for row in COASTER_SEAT_ROWS:
                for x in (-0.42, 0.42):
                    g.rbox('ME_Orange_Warm', (x, row, 1.1), (0.68, 0.55, 0.18), bevel=0.07)   # cushion
                    g.rbox('ME_Cookie_Gold', (x, row + 0.3, 1.1), (0.68, 0.16, 0.75), bevel=0.07)  # backrest
            if lead:                                                                      # round nose + dashboard
                g.blob('ME_Purple_Deep', (0, -1.25, 0.75), 0.75, sz=(1.25, 0.55, 0.7), seg=16)
                for x in (-0.55, 0.55):
                    g.blob('ME_Glow_Teal', (x, -1.6, 0.85), 0.17, seg=10)
                g.rbox('ME_Gray_Dark', (0, -0.98, 1.1), (1.5, 0.22, 0.45), bevel=0.08)
                g.blob('ME_Gold', (0, -1.55, 1.2), 0.22, sz=(1, 0.6, 1), seg=10)          # gold emblem
            else:
                g.blob('ME_Gold', (0, -1.25, 0.6), 0.14, seg=8)                           # coupling
        car = mesh_obj(u, f'Car_{c}', car_body, loc=(0, cy, COASTER_RAIL))

        def lap_bar(g):     # origin = hinge on top of the backrests; Unity raises it until the train leaves
            g.tube([(-0.85, 0, 0), (-0.85, -0.5, -0.3), (0.85, -0.5, -0.3), (0.85, 0, 0)], 0.06, 'ME_Teal', seg=8)
        for r, row in enumerate(COASTER_SEAT_ROWS):
            mesh_obj(u, f'LapBar_{r}', lap_bar, loc=(0, row + 0.3, 1.85), parent=car)
            for s, x in enumerate((-0.42, 0.42)):
                empty(u, f'Seat_{c * 4 + r * 2 + s:02d}', (x, row, 1.28), parent=car)
        if lead:                                                                          # button on the dash, belt on the side
            empty(u, f'Slot_{slot:02d}', (0.45, -0.98, 1.72), parent=car); slot += 1
            empty(u, f'Slot_{slot:02d}', (1.15, -0.55, 0.9), parent=car); slot += 1
    for c in range(3):                                                                    # one battery bay per car (deck side)
        empty(u, f'Slot_{slot:02d}', (1.15, 0.45, 0.6), parent=bpy.data.objects[f'Car_{c}']); slot += 1
    for y in (-6.0, -7.3, -8.6):                                                          # broken rail spots to repair
        empty(u, f'Slot_{slot:02d}', (0.0, y, 0.15)); slot += 1

    def broken(g):                                                                        # cracked ties + warning cones
        for k, y in enumerate((-6.0, -7.3, -8.6)):
            g.rbox('ME_Orange_Warm', (1.05, y, 0.0), (0.3, 0.3, 0.05), bevel=0.02)
            cyl(g, 'ME_Orange_Warm', (1.05, y, 0.05), 0.12, 0.55, seg=10, r2=0.02)
    mesh_obj(u, 'Repair_Marks', broken)
    empty(u, 'Board', (2.7, 0.1, 0.3))


# ---------------- HauntedBakery: the magic oven landmark becomes the escape machine (EscapeVisualPlan.md §5.3) ----------------
# Built in the oven's own frame (build_hauntedbakery.py _oven: stone base 46 x 24 x 3 m, front face y -12, mouth centre
# (0, -9.8, 10), round door hinge plane y -11). Unity places the root on HAU_Landmark_MagicOven. The map door rolls aside
# (Unity +X = here -X), so the machine sits on the other half of the base ledge (+X) and the ramp climbs to the mouth.
OVEN_FRONT = -12.0
OVEN_LEDGE = 3.0
OVEN_RAMP = 12.0                      # ramp run (rise OVEN_LEDGE): 1:4 keeps the checker monster capsule off the slope


def device_bakery():
    u = 'ESC_HauntedBakery'

    def ramp(g):     # 5 m wide, 3 m rise over 12 m (1:4), then a 1.6 m flat landing in front of the door
        bm = g.bm
        y0, y1, y2 = OVEN_FRONT - OVEN_RAMP - 1.6, OVEN_FRONT - 1.6, OVEN_FRONT
        prof = [(y0, 0), (y2, 0), (y2, OVEN_LEDGE), (y1, OVEN_LEDGE)]                   # side profile (y, z)
        L = [bm.verts.new((-2.5, y, z)) for y, z in prof]
        R = [bm.verts.new((2.5, y, z)) for y, z in prof]
        mi = g.mi('ME_Cookie_Gold')
        bm.faces.new(list(reversed(L))).material_index = mi
        bm.faces.new(R).material_index = mi
        for k in range(4):
            bm.faces.new((L[k], L[(k + 1) % 4], R[(k + 1) % 4], R[k])).material_index = mi
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])

    def treads(g):   # chocolate strips lying on the slope + gold landing mat (decor only: keeps the walkable slope smooth)
        y0 = OVEN_FRONT - OVEN_RAMP - 1.6
        tilt = math.atan2(OVEN_LEDGE, OVEN_RAMP)
        for k in range(1, 9):
            t = k / 9
            g.rbox('ME_Cookie_Dark', (0, y0 + OVEN_RAMP * t, OVEN_LEDGE * t - 0.02), (4.6, 0.25, 0.04), bevel=0.0, rot=(tilt, 0, 0))
        g.rbox('ME_Gold', (0, OVEN_FRONT - 0.8, OVEN_LEDGE - 0.01), (4.6, 1.2, 0.03), bevel=0.0)
    mesh_obj(u, 'Ramp_Treads', treads)

    mesh_obj(u, 'Ramp', ramp)

    def body(g):
        # machine housing on the +X half of the ledge (blocks the narrow ledge so no cookie-only strip is left)
        g.rbox('ME_Purple_Deep', (12.75, -10.85, OVEN_LEDGE), (20.5, 2.9, 5.5), bevel=0.45)
        g.rbox('ME_Gold', (12.75, -12.33, OVEN_LEDGE + 5.0), (20.6, 0.12, 0.35), bevel=0.05)       # gold trim
        g.rbox('ME_Gray_Dark', (-12.75, -9.9, OVEN_LEDGE), (20.5, 1.0, 5.0), bevel=0.2)            # door track (-X half)
        for x, z in ((8.0, 6.0), (12.2, 5.1)):                                                     # gear axles
            cyl(g, 'ME_Gray_Dark', (x, -12.2, z), 0.45, 0.5, seg=14, rot=(math.pi / 2, 0, 0))
        cyl(g, 'ME_Gray_Light', (18.0, -10.8, OVEN_LEDGE + 5.5), 1.0, 2.2, seg=18)                 # piston cylinder
        torus(g, 'ME_Gold', (18.0, -10.8, OVEN_LEDGE + 7.6), 1.0, 0.14, seg=18)
        cyl(g, 'ME_Gray_Dark', (5.0, -10.8, OVEN_LEDGE + 5.5), 0.75, 4.2, seg=16, r2=0.6)          # machine chimney
        g.lathe([(0.6, 0), (0.95, 0.4), (0.9, 0.7), (0, 0.7)], 'ME_Purple_Deep', seg=16, loc=(5.0, -10.8, OVEN_LEDGE + 9.7))
        g.lathe([(0, -0.15), (1.0, -0.15), (1.0, 0.15), (0, 0.15)], 'ME_Cream', seg=24, loc=(20.5, -12.35, 6.5),
                rot=(math.pi / 2, 0, 0))                                                           # gauge face
        torus(g, 'ME_Gold', (20.5, -12.5, 6.5), 1.0, 0.12, seg=24, rot=(math.pi / 2, 0, 0))
        # floor-level parts on the stone base front: button console, gear seat, battery bays, leaky pipe
        g.rbox('ME_Gray_Dark', (4.3, -12.35, 0.0), (1.5, 0.7, 1.9), bevel=0.15)
        g.rbox('ME_Gray_Dark', (6.6, -12.2, 0.9), (1.0, 0.4, 1.0), bevel=0.1)
        for x in (9.0, 11.0, 13.0):
            g.rbox('ME_Gray_Dark', (x, -12.2, 0.6), (1.2, 0.45, 1.5), bevel=0.12)
            g.rbox('ME_Teal', (x, -12.45, 0.65), (0.9, 0.1, 1.2), bevel=0.05)
        g.tube([(14.8, -12.45, 2.4), (23.0, -12.45, 2.4)], 0.32, 'ME_Gray_Light', seg=12)
        for x in (16.0, 18.5, 21.0):
            torus(g, 'ME_Orange_Warm', (x, -12.45, 2.4), 0.36, 0.1, seg=14, rot=(0, math.pi / 2, 0))
    mesh_obj(u, 'Body', body)

    def gear(R, teeth):
        def b(g):
            disc_gear(g, 'ME_Gold', R, 0.5, teeth, rot=(math.pi / 2, 0, 0), tooth=(0.55, 0.5))
            g.blob('ME_Glow_Teal', (0, -0.3, 0), R * 0.28, sz=(1, 0.5, 1), seg=12)
        return b
    mesh_obj(u, 'Gear_A', gear(2.0, 12), loc=(8.0, -12.55, 6.0))
    mesh_obj(u, 'Gear_B', gear(1.35, 8), loc=(12.2, -12.55, 5.1))
    mesh_obj(u, 'Piston_Rod', lambda g: (cyl(g, 'ME_Gray_Light', (0, 0, -1.5), 0.45, 3.0, seg=12),
                                         g.blob('ME_Red_Button', (0, 0, 1.6), 0.7, sz=(1, 1, 0.6), seg=14)),
             loc=(18.0, -10.8, OVEN_LEDGE + 7.6))
    mesh_obj(u, 'Gauge_Needle', lambda g: g.rbox('ME_Red_Button', (0, 0, -0.08), (0.12, 0.08, 0.85), bevel=0.03),
             loc=(20.5, -12.55, 6.5))

    def oven_light(g):    # white glow that fills the mouth once the door is open
        g.lathe([(0, 0.0), (7.4, 0.0), (6.8, 0.25), (0, 0.4)], 'ME_Glow_White', seg=40, rot=(math.pi / 2, 0, 0))
    mesh_obj(u, 'Oven_Light', oven_light, loc=(0, -10.05, 10.0))
    empty(u, 'Smoke', (5.0, -10.8, OVEN_LEDGE + 10.6))
    empty(u, 'Mouth', (0, -10.4, 7.5))
    empty(u, 'Board', (0, OVEN_FRONT - 0.9, OVEN_LEDGE))
    slots = [(4.3, -12.75, 2.05), (6.6, -12.5, 1.4), (9.0, -12.55, 1.25), (11.0, -12.55, 1.25), (13.0, -12.55, 1.25),
             (16.0, -12.85, 2.4), (18.5, -12.85, 2.4), (21.0, -12.85, 2.4)]
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


# ---------------- CandyForest: giant cake that breaks open into a candy rocket (EscapeVisualPlan.md §5.1) ----------------
CAKE_TIERS = [  # (radius, z bottom, height, sponge, shard count)
    (7.4, 0.35, 4.0, 'ME_Pink', 6),
    (5.3, 4.35, 3.2, 'ME_Cream', 4),
    (3.3, 7.55, 2.5, 'ME_Pink', 2),
]


def sector(g, mat, top_mat, R, z0, z1, a0, a1, seg=8):
    """Solid cake wedge (angles in radians) from the axis out to R, z0..z1; top face uses top_mat."""
    bm = g.bm
    ring = lambda z, r: [bm.verts.new((r * math.cos(a0 + (a1 - a0) * i / seg), r * math.sin(a0 + (a1 - a0) * i / seg), z))
                         for i in range(seg + 1)]
    c0, c1 = bm.verts.new((0, 0, z0)), bm.verts.new((0, 0, z1))
    lo, hi = ring(z0, R), ring(z1, R)
    m_side, m_top = g.mi(mat), g.mi(top_mat)
    for i in range(seg):
        f = bm.faces.new((lo[i], lo[i + 1], hi[i + 1], hi[i])); f.material_index = m_side          # outer skin
        f = bm.faces.new((c1, hi[i], hi[i + 1])); f.material_index = m_top                       # top
        f = bm.faces.new((c0, lo[i + 1], lo[i])); f.material_index = m_side                      # bottom
    f = bm.faces.new((c0, lo[0], hi[0], c1)); f.material_index = m_side                          # cut faces
    f = bm.faces.new((c0, c1, hi[-1], lo[-1])); f.material_index = m_side


def device_candy():
    u = 'ESC_CandyForest'

    def body(g):   # wide candy plate with a gentle slope (walkable), cream rim, 5 glass cell pedestals at the front
        g.lathe([(0, 0), (9.6, 0), (9.2, 0.15), (8.4, 0.35), (0, 0.35)], 'ME_Teal', seg=48)
        torus(g, 'ME_Cream', (0, 0, 0.32), 8.2, 0.12, seg=48)
        for k in range(5):
            x, y, _ = side(230 + k * 20, 8.9, 0)
            cyl(g, 'ME_Cookie_Gold', (x, y, 0.2), 0.45, 0.55, seg=12)
            torus(g, 'ME_Purple_Deep', (x, y, 0.72), 0.42, 0.06, seg=16)
    mesh_obj(u, 'Body', body)

    def intact(g):
        rnd = random.Random(4)
        for R, z0, h, sponge, _ in CAKE_TIERS:
            cyl(g, sponge, (0, 0, z0), R, h, seg=40)
            cyl(g, 'ME_Cream', (0, 0, z0 + h - 0.05), R + 0.12, 0.35, seg=40)                       # icing band
            for k in range(int(R * 3)):                                                            # cream drips
                a = 2 * math.pi * k / int(R * 3) + rnd.uniform(-0.05, 0.05)
                g.blob('ME_Cream', (math.cos(a) * (R + 0.08), math.sin(a) * (R + 0.08), z0 + h - 0.35 - rnd.uniform(0, 0.5)),
                       0.32, sz=(1, 1, 1.8), seg=8)
            for k in range(int(R * 1.6)):                                                          # strawberries on the rim
                a = 2 * math.pi * (k + 0.5) / int(R * 1.6)
                g.blob('ME_Red_Button', (math.cos(a) * (R - 0.6), math.sin(a) * (R - 0.6), z0 + h + 0.35), 0.45, seg=10)
        for k in range(3):                                                                         # candles on top
            x, y, _ = side(90 + k * 120, 1.4, 0)
            cyl(g, 'ME_Cream', (x, y, 10.05), 0.28, 1.6, seg=10)
            g.blob('ME_Glow_Orange', (x, y, 11.9), 0.3, sz=(1, 1, 1.6), seg=10)
    mesh_obj(u, 'Cake_Intact', intact)

    def cracks(g):    # glowing cracks that show while the cake shakes before it bursts
        rnd = random.Random(9)
        for R, z0, h, _, n in CAKE_TIERS:
            for k in range(n):
                a = 2 * math.pi * k / n
                pts = [(math.cos(a + rnd.uniform(-0.1, 0.1)) * (R + 0.06), math.sin(a + rnd.uniform(-0.1, 0.1)) * (R + 0.06),
                        z0 + h * t) for t in (0.0, 0.3, 0.55, 0.8, 1.0)]
                g.tube(pts, 0.12, 'ME_Glow_Orange', seg=6)
    mesh_obj(u, 'Cake_Cracks', cracks)

    idx = 0
    for R, z0, h, sponge, n in CAKE_TIERS:          # shards: wedges of each tier, origin at the wedge centre
        for k in range(n):
            a0, a1 = 2 * math.pi * k / n, 2 * math.pi * (k + 1) / n
            am = (a0 + a1) / 2
            cx, cy, cz = math.cos(am) * R * 0.55, math.sin(am) * R * 0.55, z0 + h / 2
            idx += 1
            mesh_obj(u, f'Cake_Shard_{idx:02d}',
                     lambda g, R=R, z0=z0, h=h, s=sponge, a0=a0, a1=a1, cx=cx, cy=cy, cz=cz:
                     placed(g, lambda gg: sector(gg, s, 'ME_Cream', R, z0, z0 + h, a0, a1), loc=(-cx, -cy, -cz)),
                     loc=(cx, cy, cz))

    rocket = empty(u, 'Cake_Rocket', (0, 0, 0))     # Unity lowers this under the plate and raises it after the burst

    def rocket_body(g):
        g.lathe([(0, 0.35), (1.6, 0.35), (2.3, 1.6), (2.4, 5.5), (2.0, 7.6), (1.2, 9.4), (0.4, 10.4), (0, 10.6)],
                'ME_Pink', seg=32, segmat=lambda k: 'ME_Cream' if (k // 4) % 2 == 0 else 'ME_Pink')
        g.blob('ME_Teal', (0, 0, 10.6), 0.45, seg=12)
        g.lathe([(0, 0.0), (1.2, 0.0), (1.6, 0.6), (0, 0.6)], 'ME_Gray_Dark', seg=24)                     # nozzle
        for a in (90, 210, 330):
            fin(g, 'ME_Teal', a, [(0, 0.3), (2.0, -0.1), (2.2, 0.6), (1.2, 2.4), (0.1, 4.2)], thick=0.35, r0=2.1)
        g.blob('ME_Glow_Teal', side(270, 2.25, 7.0), 0.75, sz=(1, 0.35, 1), seg=16)                     # window
        torus(g, 'ME_Purple_Deep', side(270, 2.28, 7.0), 0.8, 0.12, rot=(math.pi / 2, 0, 0))
        g.rbox('ME_Purple_Deep', (0, -2.35, 0.9), (2.0, 0.2, 0.18), bevel=0.05)                         # hatch frame
        g.rbox('ME_Purple_Deep', (0, -2.35, 3.6), (2.0, 0.2, 0.18), bevel=0.05)
    mesh_obj(u, 'RocketBody', rocket_body, parent=rocket)

    def hatch(g):
        g.rbox('ME_Cream', (0.8, 0, 0), (1.6, 0.2, 2.6), bevel=0.12)
        g.blob('ME_Red_Button', (1.35, -0.12, 1.2), 0.12, seg=8)
    mesh_obj(u, 'Hatch', hatch, loc=(-0.8, -2.42, 1.0), parent=rocket)

    def flame(g):
        g.lathe([(0, -3.4), (1.1, -0.9), (1.05, 0.0), (0, 0.0)], 'ME_Glow_Orange', seg=16)
        g.lathe([(0, -2.2), (0.6, -0.6), (0.55, 0.02), (0, 0.02)], 'ME_Glow_White', seg=12)
    mesh_obj(u, 'Flame', flame, loc=(0, 0, 0.05), parent=rocket)

    for k in range(5):                                # cell sockets (counter mode: Slot_00 .. Slot_nn)
        empty(u, f'Slot_{k:02d}', side(230 + k * 20, 8.9, 1.15))
    empty(u, 'Board', (0, -5.2, 0.35))


BUILDS.append(('ESC_CursedCandyCarnival', device_carnival, (), {}))
BUILDS.append(('ESC_HauntedBakery', device_bakery, (), {}))
BUILDS.append(('ESC_ChocolateFactory', device_factory, (), {}))
BUILDS.append(('ESC_GingerbreadVillage', device_gingerbread, (), {}))
BUILDS.append(('ESC_CandyForest', device_candy, (), {}))
