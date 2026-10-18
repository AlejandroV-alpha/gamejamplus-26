# Escena "Nave_Danada": la nave j_CentralShip cruza un campo de asteroides y recibe 4 impactos.
# Cada impacto: asteroide que choca, destello, chispas, fragmentos, sacudida de nave y cámara,
# y daño que se queda (quemadura, grieta incandescente, fuego y humo). En el 3.er impacto
# se desprende una placa del techo y desde el 1.º parpadea una alarma roja.
#
# Uso: Blender > pestaña Scripts > Texto > Abrir este archivo > Ejecutar (Alt+P).
# Crea la escena "Nave_Danada" (240 fotogramas a 30 fps) sin tocar las demás escenas.
import bpy, bmesh, math, random
from mathutils import Vector

SHIP_BLEND = r"E:\gamejamplus-26\Assets\06j_Models\Nave_Puerta_Animada_Unity\Nave_Puerta_Animada\Nave_Puerta_Animada.blend"
SHIP_ROOT = "Nave_Exploradora"
SCENE = "Nave_Danada"
END = 240
IMPACTS = [70, 105, 140, 175]  # fotogramas de cada choque
random.seed(11)

# ---------------------------------------------------------------- escena limpia
old = bpy.data.scenes.get(SCENE)
if old:
    for o in list(old.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    bpy.data.scenes.remove(old)
sc = bpy.data.scenes.new(SCENE)
if bpy.context.window:
    bpy.context.window.scene = sc
vl = sc.view_layers[0]
sc.frame_start, sc.frame_end = 1, END
sc.render.fps = 30
for engine in ("BLENDER_EEVEE", "BLENDER_EEVEE_NEXT"):
    try:
        sc.render.engine = engine
        break
    except TypeError:
        pass
col = bpy.data.collections.new(SCENE + "_Efectos")
sc.collection.children.link(col)


# ---------------------------------------------------------------- utilidades
def fcurves_of(idblock):
    ad = idblock.animation_data
    if not ad or not ad.action:
        return []
    try:
        return list(ad.action.fcurves)
    except AttributeError:  # acciones por capas (Blender 4.4+)
        from bpy_extras import anim_utils
        cb = anim_utils.action_get_channelbag_for_slot(ad.action, ad.action_slot)
        return list(cb.fcurves) if cb else []


def linear(idblock):
    for fc in fcurves_of(idblock):
        for kp in fc.keyframe_points:
            kp.interpolation = "LINEAR"


def mat(name, rgb, emit=0.0, rough=0.8, alpha=1.0):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.use_nodes = True
    m.diffuse_color = (*rgb, alpha)
    b = next(n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
    b.inputs["Base Color"].default_value = (*rgb, 1)
    b.inputs["Roughness"].default_value = rough
    b.inputs["Emission Color"].default_value = (*rgb, 1)
    b.inputs["Emission Strength"].default_value = emit
    b.inputs["Alpha"].default_value = alpha
    if alpha < 1:
        try:
            m.surface_render_method = "BLENDED"
        except AttributeError:
            m.blend_method = "BLEND"
    return m


M = dict(
    rock=mat("Ast_Roca", (0.22, 0.2, 0.18), rough=0.95),
    rock2=mat("Ast_Roca_Clara", (0.35, 0.31, 0.27), rough=0.95),
    flash=mat("Ast_Destello", (1.0, 0.85, 0.6), emit=40),
    spark=mat("Ast_Chispa", (1.0, 0.7, 0.2), emit=25),
    fire=mat("Ast_Fuego", (1.0, 0.35, 0.05), emit=18),
    fire2=mat("Ast_Fuego_Nucleo", (1.0, 0.8, 0.2), emit=25),
    scorch=mat("Ast_Quemadura", (0.02, 0.018, 0.015), rough=1.0),
    crack=mat("Ast_Grieta", (1.0, 0.3, 0.03), emit=10),
    smoke=mat("Ast_Humo", (0.12, 0.12, 0.12), rough=1.0, alpha=0.55),
    debris=mat("Ast_Chatarra", (0.3, 0.3, 0.28), rough=0.6),
)


def new_obj(name, bm, m, loc=(0, 0, 0), parent=None):
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    o = bpy.data.objects.new(name, me)
    me.materials.append(M[m])
    col.objects.link(o)
    if parent:
        o.parent = parent
    o.location = loc
    return o


def ico(name, r, m, loc=(0, 0, 0), parent=None, sub=1):
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=sub, radius=r)
    return new_obj(name, bm, m, loc, parent)


def cone(name, r, h, m, loc=(0, 0, 0), parent=None):
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, segments=8, radius1=r, radius2=0, depth=h)
    bmesh.ops.translate(bm, vec=(0, 0, h / 2), verts=bm.verts)  # base en el origen
    return new_obj(name, bm, m, loc, parent)


def rock(name, r, loc=(0, 0, 0)):
    o = ico(name, r, random.choice(("rock", "rock2")), loc, sub=2)
    tex = bpy.data.textures.get("Ast_Ruido") or bpy.data.textures.new("Ast_Ruido", "CLOUDS")
    tex.noise_scale = 0.6
    d = o.modifiers.new("Irregular", "DISPLACE")
    d.texture = tex
    d.strength = r * 0.6
    o.scale = (random.uniform(0.8, 1.3), random.uniform(0.7, 1.1), random.uniform(0.6, 1.0))
    return o


def show_from(o, f_on, f_off=None):
    """Oculto antes de f_on (y desde f_off si se indica)."""
    for f, hidden in ((1, True), (f_on, False)) + (((f_off, True),) if f_off else ()):
        o.hide_viewport = o.hide_render = hidden
        o.keyframe_insert("hide_viewport", frame=f)
        o.keyframe_insert("hide_render", frame=f)


def noise(o, path, strength, scale=2.0, start=1, end=END):
    for fc in fcurves_of(o):
        if fc.data_path == path:
            n = fc.modifiers.new("NOISE")
            n.strength, n.scale, n.phase = strength, scale, random.uniform(0, 100)
            n.use_restricted_range, n.frame_start, n.frame_end = True, start, end


def add_light(name, kind, energy, color, loc, parent=None):
    L = bpy.data.lights.new(name, kind)
    L.energy, L.color = energy, color
    o = bpy.data.objects.new(name, L)
    col.objects.link(o)
    o.parent = parent
    o.location = loc
    return o


# ---------------------------------------------------------------- traer la nave
with bpy.data.libraries.load(SHIP_BLEND, link=False) as (src, dst):
    dst.objects = list(src.objects)
loaded = [o for o in dst.objects if o]
root = next(o for o in loaded if o.parent is None and o.name.split(".")[0] == SHIP_ROOT)


def top(o):
    while o.parent:
        o = o.parent
    return o


ship_objs = [o for o in loaded if top(o) == root]
for o in loaded:
    if o not in ship_objs:
        bpy.data.objects.remove(o, do_unlink=True)
ship_col = bpy.data.collections.new(SCENE + "_Nave")
sc.collection.children.link(ship_col)
for o in ship_objs:
    ship_col.objects.link(o)
    # La puerta se queda cerrada: fija el primer fotograma de su animación y quita la acción.
    for fc in fcurves_of(o):
        if fc.keyframe_points:
            try:
                getattr(o, fc.data_path)[fc.array_index] = fc.keyframe_points[0].co[1]
            except (AttributeError, TypeError, IndexError):
                pass
    if o.animation_data:
        o.animation_data_clear()

root.location = (0, 0, 0)
root.rotation_euler = (0, 0, 0)
vl.update()

# Ejes de la nave: el parabrisas marca el frente
wind = next((o for o in ship_objs if o.name.split(".")[0] == "Parabrisas"), None)
fwd = Vector((0, 1 if wind is None or wind.matrix_world.translation.y >= 0 else -1, 0))
up = Vector((0, 0, 1))
side = fwd.cross(up)
pts = [o.matrix_world @ Vector(c) for o in ship_objs if o.type == "MESH" for c in o.bound_box]
center = sum(pts, Vector()) / len(pts)

# ---------------------------------------------------------------- puntos de impacto (rayos contra el casco)
depsgraph = vl.depsgraph
dirs = [up + side * 0.3 + fwd * 0.4, side + up * 0.2, up - side * 0.4 - fwd * 0.3, -side + fwd * 0.2 + up * 0.1]
hits = []  # (posición local, normal local)
inv = root.matrix_world.inverted()
for d in dirs:
    d = d.normalized()
    ok, loc, nor, *_ = sc.ray_cast(depsgraph, center + d * 25, -d)
    if not ok:
        loc, nor = center + d * 1.2, d
    hits.append((inv @ loc, (inv.to_3x3() @ nor).normalized()))

# ---------------------------------------------------------------- animación de la nave: deriva, balanceo y sacudidas
root.keyframe_insert("location", frame=1)
root.keyframe_insert("rotation_euler", frame=1)
roll = 0.0
for i, F in enumerate(IMPACTS):
    _, n = hits[i]
    jolt = -(root.matrix_world.to_3x3() @ n)
    root.location = (0, 0, 0)
    root.rotation_euler = (0, math.radians(roll), 0)
    root.keyframe_insert("location", frame=F - 1)
    root.keyframe_insert("rotation_euler", frame=F - 1)
    root.location = jolt * 0.45
    root.rotation_euler = (math.radians(random.uniform(-7, 7)), math.radians(roll + random.choice((-8, 8))), math.radians(random.uniform(-5, 5)))
    root.keyframe_insert("location", frame=F + 2)
    root.keyframe_insert("rotation_euler", frame=F + 2)
    root.location = -jolt * 0.12
    root.keyframe_insert("location", frame=F + 6)
    roll += random.choice((-4, 4))
    root.location = (0, 0, -0.1 * (i + 1))
    root.rotation_euler = (0, math.radians(roll), 0)
    root.keyframe_insert("location", frame=F + 14)
    root.keyframe_insert("rotation_euler", frame=F + 14)
root.location = (0, 0, -0.6)
root.rotation_euler = (math.radians(4), math.radians(roll * 1.5), math.radians(6))
root.keyframe_insert("location", frame=END)
root.keyframe_insert("rotation_euler", frame=END)
noise(root, "location", 0.06, 25)        # vaivén suave del vuelo
noise(root, "rotation_euler", 0.015, 30)

# ---------------------------------------------------------------- campo de asteroides de fondo (en bucle)
for k in range(32):
    while True:
        x, z = random.uniform(-28, 28), random.uniform(-14, 14)
        if abs(x) > 6 or abs(z) > 5:
            break
    lat = side * x + up * z
    o = rock(f"Asteroide_Fondo_{k}", random.uniform(0.3, 2.2))
    dur = random.randint(110, 240)
    s0 = random.randint(-dur, 0)
    o.location = center + fwd * 90 + lat
    o.rotation_euler = (0, 0, 0)
    o.keyframe_insert("location", frame=s0)
    o.keyframe_insert("rotation_euler", frame=s0)
    o.location = center - fwd * 70 + lat
    o.rotation_euler = [random.uniform(-6, 6) for _ in range(3)]
    o.keyframe_insert("location", frame=s0 + dur)
    o.keyframe_insert("rotation_euler", frame=s0 + dur)
    linear(o)
    for fc in fcurves_of(o):
        fc.modifiers.new("CYCLES")

# ---------------------------------------------------------------- impactos
for i, F in enumerate(IMPACTS):
    lp, ln = hits[i]
    sc.frame_set(F)
    wp = root.matrix_world @ lp
    wn = (root.matrix_world.to_3x3() @ ln).normalized()
    align = Vector((0, 0, 1)).rotation_difference(ln).to_euler()

    # Asteroide que choca
    a = rock(f"Asteroide_Impacto_{i}", random.uniform(0.35, 0.55))
    a.location = wp + wn * 28 + fwd * 18 + side * random.uniform(-4, 4)
    a.keyframe_insert("location", frame=F - 26)
    a.location = wp
    a.keyframe_insert("location", frame=F)
    a.rotation_euler = (0, 0, 0)
    a.keyframe_insert("rotation_euler", frame=F - 26)
    a.rotation_euler = (5, 3, 4)
    a.keyframe_insert("rotation_euler", frame=F)
    linear(a)
    show_from(a, F - 26, F + 1)

    # Destello + luz del golpe
    fl = ico(f"Impacto_{i}_Destello", 1.0, "flash", lp, root, sub=2)
    for f, s in ((F, 0.05), (F + 2, 0.9), (F + 9, 0.0)):
        fl.scale = (s, s, s)
        fl.keyframe_insert("scale", frame=f)
    show_from(fl, F, F + 10)
    lg = add_light(f"Impacto_{i}_Luz", "POINT", 0, (1.0, 0.6, 0.3), lp + ln * 0.6, root)
    for f, e in ((F - 1, 0), (F + 1, 9000), (F + 5, 2500), (F + 14, 0)):
        lg.data.energy = e
        lg.data.keyframe_insert("energy", frame=f)

    # Chispas y fragmentos que salen despedidos
    for k in range(14):
        d = (wn + Vector([random.uniform(-0.9, 0.9) for _ in range(3)])).normalized()
        is_spark = k < 8
        o = ico(f"Impacto_{i}_{'Chispa' if is_spark else 'Fragmento'}_{k}",
                0.04 if is_spark else random.uniform(0.08, 0.18), "spark" if is_spark else "debris", wp, sub=1)
        life = random.randint(10, 18) if is_spark else random.randint(30, 50)
        o.keyframe_insert("location", frame=F)
        o.keyframe_insert("rotation_euler", frame=F)
        o.location = wp + d * random.uniform(2.5, 6.0) - fwd * random.uniform(0, 4)
        o.rotation_euler = [random.uniform(-9, 9) for _ in range(3)]
        o.keyframe_insert("location", frame=F + life)
        o.keyframe_insert("rotation_euler", frame=F + life)
        o.keyframe_insert("scale", frame=F + life - 4)
        o.scale = (0, 0, 0)
        o.keyframe_insert("scale", frame=F + life)
        show_from(o, F, F + life + 1)

    # Daño permanente pegado al casco
    sc_ = ico(f"Dano_{i}_Quemadura", 1.0, "scorch", lp, root, sub=2)
    sc_.rotation_euler = align
    sc_.scale = (0.42, 0.42, 0.03)
    cr = ico(f"Dano_{i}_Grieta", 1.0, "crack", lp + ln * 0.02, root, sub=1)
    cr.rotation_euler = align
    cr.scale = (0.32, 0.18, 0.05)
    cr.keyframe_insert("scale", frame=F)
    noise(cr, "scale", 0.04, 4, F, END)
    for o in (sc_, cr):
        show_from(o, F)

    # Fuego (conos parpadeantes)
    for k, (m, r, h) in enumerate((("fire", 0.35, 1.4), ("fire", 0.25, 1.1), ("fire2", 0.15, 0.8))):
        off = Vector((random.uniform(-0.12, 0.12), random.uniform(-0.12, 0.12), 0))
        fo = cone(f"Dano_{i}_Fuego_{k}", r, h, m, lp + (align.to_matrix() @ off), root)
        fo.rotation_euler = align
        fo.keyframe_insert("scale", frame=F)
        noise(fo, "scale", 0.35, 2.5, F, END)
        show_from(fo, F)
    show_from(add_light(f"Dano_{i}_Luz_Fuego", "POINT", 350, (1.0, 0.45, 0.1), lp + ln * 0.5, root), F)

    # Humo en bucle que sube y se queda atrás
    for k in range(6):
        sm = ico(f"Dano_{i}_Humo_{k}", 0.5, "smoke", lp, root, sub=2)
        s0 = F + k * 7
        sm.scale = (0.2, 0.2, 0.2)
        sm.keyframe_insert("location", frame=s0)
        sm.keyframe_insert("scale", frame=s0)
        sm.location = lp + ln * 2.2 + (root.matrix_world.inverted().to_3x3() @ (-fwd * 3.5 + up * 0.8))
        sm.scale = (1.3, 1.3, 1.3)
        sm.keyframe_insert("location", frame=s0 + 42)
        sm.keyframe_insert("scale", frame=s0 + 42)
        linear(sm)
        for fc in fcurves_of(sm):
            if fc.data_path in ("location", "scale"):
                c = fc.modifiers.new("CYCLES")
                c.mode_before = "NONE"
        show_from(sm, s0)

# ---------------------------------------------------------------- placa del techo que se desprende (3.er impacto)
F3 = IMPACTS[2]
lp3, ln3 = hits[2]
plates = [o for o in ship_objs if o.type == "MESH" and o.name.startswith("Armadura_techo") and o.parent == root]
if plates:
    plate = min(plates, key=lambda o: (o.location - lp3).length)
    plate.keyframe_insert("location", frame=F3)
    plate.keyframe_insert("rotation_euler", frame=F3)
    back = root.matrix_world.inverted().to_3x3() @ (-fwd)
    plate.location = plate.location + ln3 * 7 + back * 12
    plate.rotation_euler = (plate.rotation_euler.x + 7, plate.rotation_euler.y + 4, plate.rotation_euler.z + 3)
    plate.keyframe_insert("location", frame=END)
    plate.keyframe_insert("rotation_euler", frame=END)
    linear(plate)

# ---------------------------------------------------------------- alarma roja parpadeante (desde el 1.er impacto)
top_z = max(p.z for p in pts)
alarm = add_light("Alarma_Roja", "POINT", 0, (1.0, 0.05, 0.02), inv @ (center + up * (top_z - center.z + 0.6)), root)
for f, e in ((1, 0), (8, 500), (15, 0)):
    alarm.data.energy = e
    alarm.data.keyframe_insert("energy", frame=f)
for fc in fcurves_of(alarm.data):
    fc.modifiers.new("CYCLES")
show_from(alarm, IMPACTS[0])

# ---------------------------------------------------------------- luces generales
sun = add_light("Sol", "SUN", 3.0, (1.0, 0.95, 0.88), center + up * 20 + side * 10)
sun.rotation_euler = (math.radians(50), math.radians(20), math.radians(30))
rim = add_light("Luz_Contorno_Azul", "AREA", 4000, (0.4, 0.6, 1.0), center - fwd * 12 + up * 4 - side * 6)
rim.data.size = 8
rim_c = rim.constraints.new("TRACK_TO")
rim_c.target = root

# ---------------------------------------------------------------- cámara con sacudidas
rig = bpy.data.objects.new("Camara_Rig", None)
col.objects.link(rig)
rig.location = center
cam_data = bpy.data.cameras.new("Camara_Asteroides")
cam_data.lens = 32
cam = bpy.data.objects.new("Camara_Asteroides", cam_data)
col.objects.link(cam)
cam.parent = rig
cam.location = side * 13 + up * 3.5 + fwd * 7
tt = cam.constraints.new("TRACK_TO")
tt.target = root
sc.camera = cam
rig.keyframe_insert("location", frame=1)
for F in IMPACTS:
    rig.location = center
    rig.keyframe_insert("location", frame=F)
    for k, f in enumerate(range(F + 1, F + 13, 2)):
        amp = 0.35 * (1 - k / 6)
        rig.location = center + Vector([random.uniform(-amp, amp) for _ in range(3)])
        rig.keyframe_insert("location", frame=f)
    rig.location = center
    rig.keyframe_insert("location", frame=F + 14)
# Acercamiento lento de la cámara durante toda la toma
cam.keyframe_insert("location", frame=1)
cam.location = side * 11 + up * 3 + fwd * 4
cam.keyframe_insert("location", frame=END)

# ---------------------------------------------------------------- fondo estrellado
w = bpy.data.worlds.new(SCENE + "_Espacio")
sc.world = w
w.use_nodes = True
nt = w.node_tree
bg = next(n for n in nt.nodes if n.type == "BACKGROUND")
nz = nt.nodes.new("ShaderNodeTexNoise")
nz.inputs["Scale"].default_value = 400.0
nz.inputs["Detail"].default_value = 0.0
ramp = nt.nodes.new("ShaderNodeValToRGB")
ramp.color_ramp.elements[0].position = 0.83
ramp.color_ramp.elements[0].color = (0.002, 0.002, 0.008, 1)
ramp.color_ramp.elements[1].position = 0.85
ramp.color_ramp.elements[1].color = (1, 1, 1, 1)
nt.links.new(nz.outputs["Fac"], ramp.inputs["Fac"])
nt.links.new(ramp.outputs["Color"], bg.inputs["Color"])
bg.inputs["Strength"].default_value = 1.5

sc.frame_set(1)
for area in (bpy.context.window.screen.areas if bpy.context.window else []):
    if area.type == "VIEW_3D":
        area.spaces.active.shading.type = "MATERIAL"
print(f"{SCENE} creada: {len(col.objects)} efectos, impactos en {IMPACTS}")
