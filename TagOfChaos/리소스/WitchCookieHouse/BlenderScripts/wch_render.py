# render helper: exec after wch_lib
import os
RENDER_DIR = r"F:\3DClaudeTagOfChaos\TagOfChaos\리소스\WitchCookieHouse\Renders"


def hide_collision(hide=True):
    for o in coll().objects:
        if o.name.startswith("COL_"):
            o.hide_render = True
            o.hide_viewport = hide


def aim(cam, loc, target):
    cam.location = Vector(loc)
    d = Vector(target) - cam.location
    cam.rotation_euler = d.to_track_quat('-Z', 'Y').to_euler()


def render_view(name, loc, target, lens=35, ortho=None, res=(1280, 960), samples=32):
    sc = bpy.context.scene
    cam = bpy.data.objects["PV_Camera"]
    sc.camera = cam
    if ortho:
        cam.data.type = 'ORTHO'
        cam.data.ortho_scale = ortho
    else:
        cam.data.type = 'PERSP'
        cam.data.lens = lens
    cam.data.clip_end = 500
    aim(cam, loc, target)
    sc.render.resolution_x, sc.render.resolution_y = res
    sc.render.resolution_percentage = 100
    try:
        sc.eevee.taa_render_samples = samples
    except Exception:
        pass
    sc.render.image_settings.file_format = 'PNG'
    path = os.path.join(RENDER_DIR, name + ".png")
    sc.render.filepath = path
    bpy.ops.render.render(write_still=True)
    return path
