# FBX export for Unity with a session-only fix for bake_space_transform on nested empties.
# Blender's ObjectWrapper.fbx_object_matrix mis-handles parent recursion when every level is baked
# (objects at even hierarchy depth get an extra G^-1 rotation and unconverted translation).
# For baked objects the correct FBX local transform is simply G @ M_local @ G^-1.
import io, contextlib
from io_scene_fbx import fbx_utils

UNITY_FBX = r"F:\3DClaudeTagOfChaos\TagOfChaos\Assets\09. Environment\WitchCookieHouse\Witch_Cookie_House.fbx"


def export_fbx(path=UNITY_FBX):
    W_ = fbx_utils.ObjectWrapper
    orig = W_.fbx_object_matrix

    def patched(self, scene_data, rest=False, local_space=False, global_space=False):
        if (self._tag == 'OB' and self.use_bake_space_transform(scene_data)
                and not self.parented_to_armature):
            G = scene_data.settings.global_matrix
            Gi = scene_data.settings.global_matrix_inv
            if local_space:
                M = self.matrix_local
            elif global_space or not self.has_valid_parent(scene_data.objects):
                M = self.matrix_global
            else:
                M = self.matrix_local
            return G @ M @ Gi
        return orig(self, scene_data, rest=rest, local_space=local_space, global_space=global_space)

    lc = bpy.context.view_layer.layer_collection.children[COLL_NAME]
    bpy.context.view_layer.active_layer_collection = lc
    kw = dict(filepath=path, use_active_collection=True,
              object_types={'EMPTY', 'MESH'}, apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL',
              axis_forward='-Z', axis_up='Y', bake_space_transform=True, use_mesh_modifiers=True,
              mesh_smooth_type='FACE', use_tspace=False, add_leaf_bones=False, bake_anim=False,
              path_mode='RELATIVE', embed_textures=False, use_custom_props=False)
    buf = io.StringIO()
    W_.fbx_object_matrix = patched
    try:
        with contextlib.redirect_stdout(buf):
            r = bpy.ops.export_scene.fbx(**kw)
    finally:
        W_.fbx_object_matrix = orig
    return r, buf.getvalue().count("WARNING")


def fbx_models(path=UNITY_FBX):
    """Parse exported FBX -> {name: (parent, T, R, geom_bounds)} for verification."""
    from io_scene_fbx import parse_fbx
    root, _ = parse_fbx.parse(path)
    find = lambda e, n: [c for c in e.elems if c.id == n]
    objs = find(root, b"Objects")[0]
    conns = find(root, b"Connections")[0]
    models = {m.props[0]: m for m in find(objs, b"Model")}
    names = {k: m.props[1].split(b"\x00")[0].decode() for k, m in models.items()}
    geoms = {g.props[0]: g for g in find(objs, b"Geometry")}
    m2g, parent = {}, {}
    for c in find(conns, b"C"):
        a, b = c.props[1], c.props[2]
        if a in geoms and b in models:
            m2g[b] = geoms[a]
        if a in models:
            parent[names[a]] = names.get(b, "ROOT")
    out = {}
    for uid, mdl in models.items():
        T, R = (0.0, 0.0, 0.0), (0.0, 0.0, 0.0)
        for p in find(find(mdl, b"Properties70")[0], b"P"):
            if p.props[0] == b"Lcl Translation":
                T = tuple(p.props[4:7])
            if p.props[0] == b"Lcl Rotation":
                R = tuple(p.props[4:7])
        bb = None
        g = m2g.get(uid)
        if g:
            vv = find(g, b"Vertices")[0].props[0]
            bb = [(min(vv[i::3]), max(vv[i::3])) for i in range(3)]
        out[names[uid]] = (parent.get(names[uid]), T, R, bb)
    return out
