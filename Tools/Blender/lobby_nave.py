# Genera el interior de una nave (sala de espera) en la escena "Lobby_Nave".
# Estilo: low-poly, colores planos, paredes oscuras con franjas naranjas,
# tiras de luz cálida en el techo, tubos verdes brillantes y barandillas de tubo.
# Uso: Blender > pestaña Scripts > Texto > Abrir este archivo > Ejecutar (Alt+P).
import bpy, bmesh, math
from mathutils import Vector

sc = bpy.data.scenes.get("Lobby_Nave") or bpy.data.scenes.new("Lobby_Nave")
bpy.context.window.scene = sc
col = bpy.data.collections.get("Lobby_Nave")
if not col:
    col = bpy.data.collections.new("Lobby_Nave")
    sc.collection.children.link(col)
for o in list(col.objects):
    bpy.data.objects.remove(o, do_unlink=True)


def mat(name, rgb, emit=0.0, metal=0.0, rough=0.85):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.use_nodes = True
    m.diffuse_color = (*rgb, 1)  # color en vista sólida
    b = next(n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
    b.inputs["Base Color"].default_value = (*rgb, 1)
    b.inputs["Metallic"].default_value = metal
    b.inputs["Roughness"].default_value = rough
    b.inputs["Emission Color"].default_value = (*rgb, 1)
    b.inputs["Emission Strength"].default_value = emit
    return m


M = dict(
    wall=mat("Nave_Pared", (0.035, 0.038, 0.032)),
    floor=mat("Nave_Suelo", (0.2, 0.19, 0.16)),
    ceil=mat("Nave_Techo", (0.09, 0.09, 0.075)),
    orange=mat("Nave_Naranja", (1.0, 0.28, 0.02), emit=0.4),
    warm=mat("Nave_Luz_Calida", (1.0, 0.85, 0.5), emit=8),
    pipe=mat("Nave_Tubo_Luz", (1.0, 0.75, 0.3), emit=2),
    green=mat("Nave_Verde_Brillo", (0.3, 1.0, 0.4), emit=5),
    beige=mat("Nave_Beige", (0.5, 0.47, 0.38)),
    black=mat("Nave_Negro", (0.02, 0.02, 0.02)),
    rail=mat("Nave_Barandilla", (0.06, 0.06, 0.055), metal=0.4, rough=0.5),
    hazard=mat("Nave_Peligro", (0.95, 0.75, 0.05)),
    screen=mat("Nave_Pantalla", (0.15, 0.45, 1.0), emit=3),
    screen2=mat("Nave_Pantalla_Verde", (0.2, 0.9, 0.4), emit=3),
    barrel=mat("Nave_Barril", (0.4, 0.85, 0.08)),
    red=mat("Nave_Rojo", (0.9, 0.05, 0.03), emit=1.5),
    seat=mat("Nave_Asiento", (0.12, 0.12, 0.11)),
)


def fcurves_of(idblock):
    ad = idblock.animation_data
    try:
        return list(ad.action.fcurves)
    except AttributeError:  # acciones por capas (Blender 4.4+)
        from bpy_extras import anim_utils
        cb = anim_utils.action_get_channelbag_for_slot(ad.action, ad.action_slot)
        return list(cb.fcurves) if cb else []


def liquid_mat():
    """Líquido verde con emisión que fluye (ruido 4D animado)."""
    m = bpy.data.materials.get("Nave_Liquido_Brillante") or bpy.data.materials.new("Nave_Liquido_Brillante")
    m.use_nodes = True
    m.diffuse_color = (0.3, 1.0, 0.4, 1)
    nt = m.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled")
    tc = nt.nodes.new("ShaderNodeTexCoord")
    mp = nt.nodes.new("ShaderNodeMapping")
    mp.inputs["Scale"].default_value = (1, 1, 0.4)  # vetas alargadas en vertical
    nz = nt.nodes.new("ShaderNodeTexNoise")
    nz.noise_dimensions = "4D"
    nz.inputs["Scale"].default_value = 4.0
    nz.inputs["Detail"].default_value = 6.0
    ramp = nt.nodes.new("ShaderNodeValToRGB")
    ramp.color_ramp.elements[0].position = 0.35
    ramp.color_ramp.elements[0].color = (0.02, 0.45, 0.1, 1)
    ramp.color_ramp.elements[1].position = 0.7
    ramp.color_ramp.elements[1].color = (0.75, 1.0, 0.55, 1)
    nt.links.new(tc.outputs["Object"], mp.inputs["Vector"])
    nt.links.new(mp.outputs["Vector"], nz.inputs["Vector"])
    nt.links.new(nz.outputs["Fac"], ramp.inputs["Fac"])
    nt.links.new(ramp.outputs["Color"], bsdf.inputs["Base Color"])
    nt.links.new(ramp.outputs["Color"], bsdf.inputs["Emission Color"])
    bsdf.inputs["Emission Strength"].default_value = 7.0
    bsdf.inputs["Roughness"].default_value = 0.1
    nt.links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])
    # Animación continua del flujo
    w = nz.inputs["W"]
    w.default_value = 0.0
    w.keyframe_insert("default_value", frame=1)
    w.default_value = 3.0
    w.keyframe_insert("default_value", frame=250)
    for fc in fcurves_of(nt):
        fc.extrapolation = "LINEAR"
        for kp in fc.keyframe_points:
            kp.interpolation = "LINEAR"
    return m


def glass_mat():
    m = mat("Nave_Vidrio", (0.75, 1.0, 0.85), rough=0.03)
    b = next(n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
    b.inputs["Alpha"].default_value = 0.18
    m.diffuse_color = (0.75, 1.0, 0.85, 0.2)
    try:
        m.surface_render_method = "BLENDED"
    except AttributeError:
        m.blend_method = "BLEND"
    return m


M["liquid"] = liquid_mat()
M["glass"] = glass_mat()
M["bubble"] = mat("Nave_Burbuja", (0.85, 1.0, 0.8), emit=12)
M["barrel_dark"] = mat("Nave_Barril_Oscuro", (0.18, 0.45, 0.04))
M["door"] = mat("Nave_Puerta", (0.4, 0.4, 0.38), metal=0.3, rough=0.6)
M["cushion"] = mat("Nave_Cojin", (0.07, 0.075, 0.085), rough=0.95)
M["metal"] = mat("Nave_Metal", (0.35, 0.35, 0.36), metal=0.85, rough=0.35)
M["leaf"] = mat("Nave_Hoja", (0.06, 0.32, 0.1))
M["leaf2"] = mat("Nave_Hoja_Clara", (0.2, 0.6, 0.15))
M["soil"] = mat("Nave_Tierra", (0.08, 0.05, 0.03))
M["water"] = mat("Nave_Agua", (0.4, 0.7, 1.0), emit=0.6, rough=0.05)
M["red_dome"] = mat("Nave_Rojo_Boton", (1.0, 0.04, 0.02), emit=3, rough=0.2)
M["holo"] = mat("Nave_Holo", (1.0, 0.35, 0.05), emit=4)
M["rust"] = mat("Nave_Oxido", (0.7, 0.2, 0.07))
M["door_dark"] = mat("Nave_Puerta_Oscura", (0.25, 0.25, 0.24), metal=0.3, rough=0.6)
M["window"] = mat("Nave_Ventanilla", (0.1, 0.16, 0.2), emit=0.3, metal=0.6, rough=0.15)
M["yellow_light"] = mat("Nave_Luz_Amarilla", (1.0, 0.8, 0.1), emit=10)
M["mug"] = mat("Nave_Taza", (0.95, 0.6, 0.1))
M["pot"] = mat("Nave_Maceta_Roja", (0.7, 0.18, 0.08))


def obj(name, bm, m, loc, rot=(0, 0, 0), bevel=0.0):
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    o = bpy.data.objects.new(name, me)
    o.location = loc
    o.rotation_euler = rot
    me.materials.append(M[m])
    col.objects.link(o)
    if bevel:
        b = o.modifiers.new("Bisel", "BEVEL")
        b.width = bevel
        b.segments = 2
        b.harden_normals = True
    return o


def box(name, loc, size, m, rot=(0, 0, 0), bevel=0.02):
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1)
    bmesh.ops.scale(bm, vec=size, verts=bm.verts)
    return obj(name, bm, m, loc, rot, bevel)


def cyl(name, loc, r, h, m, r2=None, seg=16, rot=(0, 0, 0)):
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, segments=seg, radius1=r,
                          radius2=r if r2 is None else r2, depth=h)
    return obj(name, bm, m, loc, rot)


def ico(name, loc, r, m):
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=1, radius=r)
    return obj(name, bm, m, loc)


def rounded_slab(name, loc, w, h, r, depth, m, seg=6, all_corners=False):
    """Placa con esquinas superiores redondeadas en el plano XZ, extruida hacia +Y."""
    pts = []
    corners = [(w / 2 - r, h - r, 0), (-w / 2 + r, h - r, 90)]
    if all_corners:
        corners += [(-w / 2 + r, r, 180), (w / 2 - r, r, 270)]
    for cx, cz, a0 in corners:
        for k in range(seg + 1):
            a = math.radians(a0 + 90 * k / seg)
            pts.append((cx + r * math.cos(a), cz + r * math.sin(a)))
    if not all_corners:
        pts += [(-w / 2, 0), (w / 2, 0)]
    bm = bmesh.new()
    vs = [bm.verts.new((x, 0, z)) for x, z in pts]
    f = bm.faces.new(vs)
    ext = bmesh.ops.extrude_face_region(bm, geom=[f])
    moved = [e for e in ext["geom"] if isinstance(e, bmesh.types.BMVert)]
    bmesh.ops.translate(bm, vec=(0, depth, 0), verts=moved)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return obj(name, bm, m, loc)


RX = (0, math.pi / 2, 0)  # cilindro tumbado a lo largo de X
RY = (math.pi / 2, 0, 0)  # cilindro tumbado a lo largo de Y


def railing(name, x, y, length, along_x=True, h=1.0, r=0.05):
    dx, dy = (length / 2, 0) if along_x else (0, length / 2)
    for i, s in enumerate((-1, 1)):
        cyl(f"{name}_Poste_{i}", (x + s * dx, y + s * dy, h / 2), r, h, "rail", seg=8)
    cyl(f"{name}_Barra", (x, y, h), r, length, "rail", seg=8, rot=RX if along_x else RY)


def bar(name, p1, p2, r=0.045, m="rail"):
    a, b = Vector(p1), Vector(p2)
    d = b - a
    rot = d.to_track_quat("Z", "Y").to_euler()
    return cyl(name, (a + b) / 2, r, d.length, m, seg=8, rot=rot)


def rail_line(name, p1, p2, z0, h=0.95, step=1.5):
    a, b = Vector((*p1, z0)), Vector((*p2, z0))
    n = max(1, round((b - a).length / step))
    for i in range(n + 1):
        p = a.lerp(b, i / n)
        bar(f"{name}_Poste_{i}", p, p + Vector((0, 0, h)))
    bar(f"{name}_Barra", a + Vector((0, 0, h)), b + Vector((0, 0, h)))
    bar(f"{name}_Barra_Media", a + Vector((0, 0, h / 2)), b + Vector((0, 0, h / 2)), r=0.03)


W, D, H = 18, 14, 5

# Estructura
box("Suelo", (0, 0, -0.1), (W, D, 0.2), "floor", bevel=0)
box("Techo", (0, 0, H + 0.1), (W, D, 0.2), "ceil", bevel=0)
box("Pared_Sur", (0, -D / 2, H / 2), (W, 0.3, H), "wall", bevel=0)
box("Pared_Oeste", (-W / 2, 0, H / 2), (0.3, D, H), "wall", bevel=0)
box("Pared_Este", (W / 2, 0, H / 2), (0.3, D, H), "wall", bevel=0)
box("Pared_Norte_Bajo", (0, D / 2, 0.75), (W, 0.3, 1.5), "wall", bevel=0)
box("Pared_Norte_Alto", (0, D / 2, 4.25), (W, 0.3, 1.5), "wall", bevel=0)
for x in (-9, -3, 3, 9):
    box(f"Pared_Norte_Pilar_{x}", (x, D / 2, 2.5), (0.8, 0.5, 2.0), "wall")
box("Ventanal_Espacio", (0, D / 2 + 0.12, 2.5), (W, 0.05, 2.0), "black", bevel=0)

# Dobles franjas naranjas en las paredes
for z in (1.0, 1.2):
    box(f"Franja_Sur_{z}", (0, -D / 2 + 0.17, z), (W, 0.04, 0.08), "orange", bevel=0)
    box(f"Franja_Oeste_{z}", (-W / 2 + 0.17, 0, z), (0.04, D, 0.08), "orange", bevel=0)
    box(f"Franja_Este_{z}", (W / 2 - 0.17, 0, z), (0.04, D, 0.08), "orange", bevel=0)
    box(f"Franja_Norte_{z + 0.4}", (0, D / 2 - 0.17, z + 0.3), (W, 0.04, 0.08), "orange", bevel=0)
# Franja naranja en el borde del suelo
for n, (l, s) in {"S": ((0, -D / 2 + 0.4, 0.01), (W - 1, 0.08, 0.02)),
                  "O": ((-W / 2 + 0.4, 0, 0.01), (0.08, D - 1, 0.02)),
                  "E": ((W / 2 - 0.4, 0, 0.01), (0.08, D - 1, 0.02))}.items():
    box(f"Franja_Suelo_{n}", l, s, "orange", bevel=0)

# Tiras de luz cálida en el techo, con abrazaderas negras
for y in (-4.5, 0, 4.5):
    box(f"Tira_Luz_{y}", (0, y, H - 0.05), (W - 1.5, 0.35, 0.08), "warm", bevel=0)
    for x in range(-8, 9, 2):
        box(f"Tira_Luz_{y}_Clip_{x}", (x, y, H - 0.08), (0.15, 0.45, 0.12), "black", bevel=0)
    L = bpy.data.lights.new(f"Luz_Techo_{y}", "AREA")
    L.shape = "RECTANGLE"
    L.size, L.size_y = W - 2, 0.6
    L.energy = 1800
    L.color = (1.0, 0.85, 0.6)
    lo = bpy.data.objects.new(L.name, L)
    lo.location = (0, y, H - 0.15)
    col.objects.link(lo)

# Tubos criogénicos con líquido brillante en la pared oeste, con tubería de luz encima
import random
random.seed(7)
sc.frame_start, sc.frame_end = 1, 240
pipe_x = -W / 2 + 0.5
cyl("Tuberia_Oeste", (pipe_x, 0, 4.2), 0.07, D - 2, "pipe", seg=8, rot=RY)
for i, y in enumerate((-4.5, -1.5, 1.5, 4.5)):
    x = -W / 2 + 1.6
    cyl(f"Tubo_{i}_Base", (x, y, 0.35), 0.95, 0.7, "beige", r2=0.7, seg=24)
    cyl(f"Tubo_{i}_Anillo_Bajo", (x, y, 0.8), 0.75, 0.2, "black", seg=24)
    cyl(f"Tubo_{i}_Collar", (x, y, 0.95), 0.66, 0.12, "beige", seg=24)
    # Líquido + superficie brillante + vidrio exterior
    cyl(f"Tubo_{i}_Liquido", (x, y, 1.82), 0.5, 1.55, "liquid", seg=24)
    cyl(f"Tubo_{i}_Superficie", (x, y, 2.6), 0.5, 0.02, "bubble", seg=24)
    cyl(f"Tubo_{i}_Vidrio", (x, y, 1.88), 0.56, 1.76, "glass", seg=24)
    # Varillas que sujetan el vidrio
    for k in range(3):
        a = k * 2 * math.pi / 3 + math.pi / 2
        box(f"Tubo_{i}_Varilla_{k}", (x + 0.6 * math.cos(a), y + 0.6 * math.sin(a), 1.88),
            (0.06, 0.06, 1.76), "black", rot=(0, 0, a), bevel=0.01)
    cyl(f"Tubo_{i}_Tapa", (x, y, 2.85), 0.7, 0.3, "beige", seg=24)
    cyl(f"Tubo_{i}_Anillo_Alto", (x, y, 3.08), 0.6, 0.16, "black", seg=24)
    cyl(f"Tubo_{i}_Cupula", (x, y, 3.26), 0.6, 0.2, "beige", r2=0.35, seg=24)
    cyl(f"Tubo_{i}_Conducto", (x, y, 3.78), 0.07, 0.84, "pipe", seg=8)
    cyl(f"Tubo_{i}_Codo", ((x + pipe_x) / 2, y, 4.2), 0.07, x - pipe_x, "pipe", seg=8, rot=RX)
    # Burbujas que suben en bucle
    for b in range(8):
        ang, rad = random.uniform(0, 2 * math.pi), random.uniform(0.05, 0.38)
        bx, by = x + rad * math.cos(ang), y + rad * math.sin(ang)
        bo = ico(f"Tubo_{i}_Burbuja_{b}", (bx, by, 1.1), random.uniform(0.025, 0.06), "bubble")
        period = random.randint(50, 90)
        f0 = random.randint(-period, 0)
        bo.keyframe_insert("location", index=2, frame=f0)
        bo.location.z = 2.55
        bo.keyframe_insert("location", index=2, frame=f0 + period)
        for fc in fcurves_of(bo):
            for kp in fc.keyframe_points:
                kp.interpolation = "LINEAR"
            fc.modifiers.new("CYCLES")
    G = bpy.data.lights.new(f"Luz_Tubo_{i}", "POINT")
    G.energy = 250
    G.color = (0.3, 1.0, 0.4)
    G.shadow_soft_size = 0.5
    go = bpy.data.objects.new(G.name, G)
    go.location = (x + 0.9, y, 1.8)
    col.objects.link(go)
    railing(f"Barandilla_Tubo_{i}", x + 1.7, y, 2.0, along_x=False)

# Consola en L con pantallas y franja de peligro (esquina suroeste)
cx, cy = -3.5, -D / 2 + 0.9
box("Consola_A", (cx, cy, 0.5), (3.0, 1.0, 1.0), "seat")
box("Consola_B", (cx + 2.0, cy + 1.0, 0.5), (1.0, 3.0, 1.0), "seat")
box("Consola_Peligro_A", (cx, cy + 0.51, 0.35), (3.0, 0.02, 0.12), "hazard", bevel=0)
box("Consola_Peligro_B", (cx + 1.49, cy + 1.0, 0.35), (0.02, 3.0, 0.12), "hazard", bevel=0)
for i, (dx, m) in enumerate(((-1.0, "screen"), (0.0, "screen2"), (1.0, "screen"))):
    box(f"Consola_Monitor_{i}", (cx + dx, cy - 0.3, 1.45), (0.9, 0.1, 0.7), "black")
    box(f"Consola_Pantalla_{i}", (cx + dx, cy - 0.24, 1.45), (0.75, 0.02, 0.55), m, bevel=0)

# Plataforma elevada con escalera (lado este)
px, PT = W / 2 - 2.5, 1.125  # centro X de la plataforma y altura de su superficie
box("Plataforma", (px, 3.0, PT - 0.125), (4.6, 7.0, 0.25), "beige")
box("Plataforma_Borde", (px, -0.5, PT - 0.125), (4.6, 0.05, 0.12), "orange", bevel=0)
for x in (px - 2.05, px + 2.05):
    for y in (-0.25, 3.0, 6.25):
        box(f"Plataforma_Pata_{x}_{y}", (x, y, (PT - 0.25) / 2), (0.3, 0.3, PT - 0.25), "black")

# Escalera maciza que baja hacia el sur desde el borde de la plataforma
sx, steps, run, rise = px - 1.3, 9, 0.35, PT / 9
for i in range(steps):
    top = PT - (i + 1) * rise
    y = -0.5 - run / 2 - i * run
    box(f"Escalon_{i}", (sx, y, top / 2), (2.0, run, top), "black", bevel=0.01)
    box(f"Escalon_{i}_Luz", (sx, y - run / 2 + 0.02, top + 0.005), (1.8, 0.03, 0.01), "warm", bevel=0)
y_end = -0.5 - steps * run
for i, x in enumerate((sx - 0.95, sx + 0.95)):
    bar(f"Pasamanos_Escalera_{i}", (x, -0.5, PT + 0.9), (x, y_end, 0.9))
    bar(f"Pasamanos_Escalera_{i}_Poste_A", (x, y_end + 0.1, 0), (x, y_end + 0.1, 0.94))
    bar(f"Pasamanos_Escalera_{i}_Poste_B", (x, -0.6, PT), (x, -0.6, PT + 0.9))

# Barandillas en los bordes de la plataforma (con hueco para la escalera)
rail_line("Barandilla_Plat_Sur", (sx + 1.1, -0.45), (px + 2.2, -0.45), PT)
rail_line("Barandilla_Plat_Oeste", (px - 2.25, -0.3), (px - 2.25, 6.3), PT)
rail_line("Barandilla_Plat_Norte", (px - 2.25, 6.3), (px + 2.2, 6.3), PT)

# Pantalla holográfica del mapa sobre la plataforma
box("Mapa_Marco", (W / 2 - 0.2, 3.0, 3.0), (0.1, 3.4, 1.8), "black")
box("Mapa_Pantalla", (W / 2 - 0.26, 3.0, 3.0), (0.02, 3.1, 1.5), "screen", bevel=0)

# Pórtico beige y barriles verdes en la esquina sureste
gx = W / 2 - 1.4
for i, y in enumerate((-4.2, -6.0)):
    for s in (-1, 1):
        box(f"Portico_{i}_Pata_{s}", (gx + s * 0.9, y, 1.5), (0.25, 0.25, 3.0), "beige")
    box(f"Portico_{i}_Viga", (gx, y, 3.0), (2.1, 0.3, 0.3), "beige")
    box(f"Portico_{i}_Remate", (gx, y, 3.2), (2.3, 0.4, 0.12), "black")
box("Portico_Viga_Larga", (gx, -5.1, 3.0), (0.3, 2.1, 0.3), "beige")
for i, (x, y) in enumerate([(gx - 0.4, -5.0), (gx + 0.35, -5.3), (gx - 0.05, -4.6), (gx + 0.3, -5.95)]):
    n = f"Bidon_{i}"
    cyl(n, (x, y, 0.47), 0.36, 0.9, "barrel", seg=24)
    for k, z in enumerate((0.03, 0.91)):
        cyl(f"{n}_Borde_{k}", (x, y, z), 0.375, 0.06, "black", seg=24)
    for k, z in enumerate((0.3, 0.64)):
        cyl(f"{n}_Aro_{k}", (x, y, z), 0.372, 0.05, "barrel_dark", seg=24)
    cyl(f"{n}_Tapa", (x, y, 0.94), 0.33, 0.02, "barrel_dark", seg=24)
    cyl(f"{n}_Tapon_Grande", (x + 0.17, y, 0.965), 0.06, 0.04, "hazard", seg=12)
    cyl(f"{n}_Tapon_Chico", (x - 0.18, y + 0.05, 0.96), 0.035, 0.03, "black", seg=10)
    cyl(f"{n}_Etiqueta", (x, y, 0.47), 0.364, 0.2, "hazard", seg=24)
    # Triángulo de peligro mirando al frente
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, segments=3, radius1=0.085, radius2=0.085, depth=0.01)
    obj(f"{n}_Simbolo", bm, "black", (x, y - 0.368, 0.47), (math.pi / 2, -math.pi / 2, 0))

# Taquillas junto a la puerta sur
for i, x in enumerate((-0.6, 0.0, 0.6)):
    box(f"Taquilla_{i}", (x, -D / 2 + 0.45, 1.0), (0.56, 0.55, 2.0), "seat")
    box(f"Taquilla_{i}_Franja", (x, -D / 2 + 0.73, 1.6), (0.5, 0.02, 0.06), "orange", bevel=0)
    for k in range(3):
        box(f"Taquilla_{i}_Rejilla_{k}", (x, -D / 2 + 0.73, 1.85 - k * 0.08), (0.3, 0.02, 0.03), "black", bevel=0)

# Banco largo bajo el ventanal (cojín, respaldo y patas metálicas)
bx0, by0 = -0.5, D / 2 - 0.6
box("Banco_Ventanal_Asiento", (bx0, by0, 0.48), (5.0, 0.7, 0.14), "cushion", bevel=0.04)
box("Banco_Ventanal_Respaldo", (bx0, by0 + 0.32, 0.85), (5.0, 0.12, 0.6), "cushion", bevel=0.04)
box("Banco_Ventanal_Estructura", (bx0, by0, 0.38), (5.1, 0.75, 0.06), "seat")
box("Banco_Ventanal_Borde", (bx0, by0 - 0.38, 0.38), (5.1, 0.02, 0.04), "orange", bevel=0)
for k, x in enumerate((-2.9, -0.5, 1.9)):
    box(f"Banco_Ventanal_Pata_{k}", (x, by0, 0.18), (0.12, 0.6, 0.36), "metal")

# Plantas en maceteros hexagonales (hojas alargadas + bulbos brillantes)
for i, x in enumerate((-3.6, 2.6)):
    py = D / 2 - 0.65
    cyl(f"Maceta_{i}", (x, py, 0.35), 0.32, 0.7, "seat", r2=0.4, seg=6)
    cyl(f"Maceta_{i}_Franja", (x, py, 0.55), 0.385, 0.06, "orange", seg=6)
    cyl(f"Maceta_{i}_Borde", (x, py, 0.72), 0.42, 0.05, "black", seg=6)
    cyl(f"Maceta_{i}_Tierra", (x, py, 0.7), 0.37, 0.02, "soil", seg=6)
    for k in range(9):
        a = k * 2 * math.pi / 9 + i
        tilt = math.radians(20 + (k % 3) * 12)
        h = 0.7 + (k % 3) * 0.15
        lx = x + math.cos(a) * math.sin(tilt) * h / 2
        ly = py + math.sin(a) * math.sin(tilt) * h / 2
        box(f"Planta_{i}_Hoja_{k}", (lx, ly, 0.7 + math.cos(tilt) * h / 2), (0.14, 0.03, h),
            "leaf" if k % 2 else "leaf2", rot=(0, tilt, a), bevel=0.01)
    for k in range(3):
        a = k * 2 * math.pi / 3
        ico(f"Planta_{i}_Bulbo_{k}", (x + 0.12 * math.cos(a), py + 0.12 * math.sin(a), 1.25 + k * 0.08),
            0.05, "bubble")

# Dispensador de agua
dx, dy = 3.5, D / 2 - 0.6
box("Dispensador", (dx, dy, 0.55), (0.5, 0.45, 1.1), "beige")
box("Dispensador_Panel", (dx, dy - 0.23, 0.8), (0.36, 0.02, 0.3), "seat", bevel=0.005)
for k, (ox, m) in enumerate(((-0.08, "screen"), (0.08, "red"))):
    cyl(f"Dispensador_Grifo_{k}", (dx + ox, dy - 0.27, 0.78), 0.03, 0.08, m, seg=8, rot=RY)
box("Dispensador_Bandeja", (dx, dy - 0.25, 0.6), (0.34, 0.12, 0.03), "black", bevel=0)
cyl("Dispensador_Cuello", (dx, dy, 1.14), 0.07, 0.08, "water", seg=12)
cyl("Dispensador_Garrafa", (dx, dy, 1.4), 0.2, 0.45, "water", seg=16)
cyl("Dispensador_Garrafa_Hombro", (dx, dy, 1.17), 0.2, 0.06, "water", r2=0.08, seg=16)
for k in range(2):
    cyl(f"Vaso_{k}", (dx + 0.35, dy - 0.1 + k * 0.12, 0.05 + k * 0.0), 0.04, 0.1, "beige", r2=0.05, seg=8)

# Cajas de carga con aristas reforzadas
def crate(name, loc, s):
    x, y, z = loc
    box(name, loc, (s * 0.94, s * 0.94, s * 0.94), "beige", bevel=0.01)
    t, h = 0.06, s / 2 - 0.03
    for sx in (-1, 1):
        for sy in (-1, 1):
            box(f"{name}_Arista_V_{sx}{sy}", (x + sx * h, y + sy * h, z), (t, t, s), "black", bevel=0)
        for sz in (-1, 1):
            box(f"{name}_Arista_Y_{sx}{sz}", (x + sx * h, y, z + sz * h), (t, s, t), "black", bevel=0)
            box(f"{name}_Arista_X_{sx}{sz}", (x, y + sx * h, z + sz * h), (s, t, t), "black", bevel=0)
    box(f"{name}_Etiqueta", (x, y - s * 0.47 - 0.005, z + s * 0.1), (s * 0.45, 0.01, s * 0.25), "hazard", bevel=0)
    box(f"{name}_Etiqueta_Franja", (x, y - s * 0.47 - 0.01, z + s * 0.1), (s * 0.45, 0.01, 0.03), "black", bevel=0)
    box(f"{name}_Asa", (x, y - s * 0.47 - 0.02, z - s * 0.2), (s * 0.3, 0.03, 0.04), "metal", bevel=0)


for i, (x, y, z, s_) in enumerate([(-4.75, 5.95, 0.4, 0.8), (-3.95, 6.05, 0.35, 0.7), (-4.55, 5.95, 1.15, 0.7)]):
    crate(f"Caja_{i}", (x, y, z), s_)

# Vigas del techo, tuberías y rejillas de ventilación
for x in (-6, -2, 2, 6):
    box(f"Viga_Techo_{x}", (x, 0, H - 0.12), (0.3, D, 0.25), "black", bevel=0)
cyl("Tuberia_Sur_A", (0, -D / 2 + 0.35, 4.3), 0.09, W - 1, "beige", seg=8, rot=RX)
cyl("Tuberia_Sur_B", (0, -D / 2 + 0.55, 4.5), 0.06, W - 1, "pipe", seg=8, rot=RX)
for i, x in enumerate((-0.3, 6.5)):
    box(f"Rejilla_{i}", (x, -D / 2 + 0.17, 3.2), (1.2, 0.04, 0.6), "black")
    for k in range(4):
        box(f"Rejilla_{i}_Lama_{k}", (x, -D / 2 + 0.2, 3.0 + k * 0.13), (1.1, 0.03, 0.04), "seat", bevel=0)

# Pantalla grande sobre la consola
box("Pantalla_Grande_Marco", (-3.5, -D / 2 + 0.2, 2.6), (3.2, 0.08, 1.2), "black")
box("Pantalla_Grande", (-3.5, -D / 2 + 0.25, 2.6), (3.0, 0.02, 1.0), "screen2", bevel=0)

# Juntas del suelo en cuadrícula
for x in range(-8, 9, 2):
    box(f"Junta_Suelo_X_{x}", (x, 0, 0.003), (0.03, D - 0.6, 0.006), "seat", bevel=0)
for y in range(-6, 7, 2):
    box(f"Junta_Suelo_Y_{y}", (0, y, 0.003), (W - 0.6, 0.03, 0.006), "seat", bevel=0)

# Zona de reunión central: mesa con botón de emergencia y bancos
mx, my = -1.0, 1.0
cyl("Mesa_Pie", (mx, my, 0.35), 0.45, 0.7, "seat", r2=0.3, seg=12)
cyl("Mesa_Pie_Base", (mx, my, 0.04), 0.8, 0.08, "black", seg=12)
cyl("Mesa_Cuerpo", (mx, my, 0.76), 1.85, 0.14, "seat", r2=1.95, seg=32)
cyl("Mesa_Luz", (mx, my, 0.84), 1.96, 0.03, "holo", seg=32)
cyl("Mesa_Tablero", (mx, my, 0.88), 1.9, 0.06, "beige", seg=32)
cyl("Mesa_Anillo_Interior", (mx, my, 0.915), 1.2, 0.01, "seat", seg=32)
cyl("Mesa_Anillo_Interior_Centro", (mx, my, 0.918), 1.15, 0.01, "beige", seg=32)
# Paneles de pantalla incrustados en el tablero
for k in range(6):
    a = k * math.pi / 3 + math.pi / 6
    box(f"Mesa_Pantalla_{k}", (mx + 1.5 * math.cos(a), my + 1.5 * math.sin(a), 0.915), (0.35, 0.5, 0.01),
        "screen", rot=(0, 0, a), bevel=0)
# Objetos sobre la mesa: portátiles, holograma central, tazas, planta y radio
TZ = 0.91  # superficie del tablero


def laptop(name, a, rad=1.25):
    f = (math.cos(a), math.sin(a))
    bx_, by_ = mx + rad * f[0], my + rad * f[1]
    box(f"{name}_Base", (bx_, by_, TZ + 0.0125), (0.36, 0.52, 0.025), "seat", rot=(0, 0, a), bevel=0.005)
    box(f"{name}_Teclado", (bx_ + 0.03 * f[0], by_ + 0.03 * f[1], TZ + 0.027), (0.2, 0.44, 0.005), "black",
        rot=(0, 0, a), bevel=0)
    hx, hy = bx_ - 0.17 * f[0], by_ - 0.17 * f[1]
    t = -0.25
    up = 0.16
    box(f"{name}_Tapa", (hx - math.sin(-t) * up * f[0], hy - math.sin(-t) * up * f[1], TZ + 0.025 + up),
        (0.02, 0.52, 0.32), "seat", rot=(0, t, a), bevel=0.005)
    box(f"{name}_Pantalla", (hx - math.sin(-t) * up * f[0] + 0.012 * f[0],
                             hy - math.sin(-t) * up * f[1] + 0.012 * f[1], TZ + 0.025 + up),
        (0.005, 0.46, 0.27), "screen2", rot=(0, t, a), bevel=0)


for k, a in enumerate((0.0, 2 * math.pi / 3, 4 * math.pi / 3)):
    laptop(f"Portatil_{k}", a)

# Proyector holográfico con un planeta flotando
cyl("Proyector_Base", (mx, my, TZ + 0.04), 0.3, 0.08, "seat", seg=16)
cyl("Proyector_Lente", (mx, my, TZ + 0.1), 0.16, 0.05, "screen", seg=16)
cyl("Proyector_Haz", (mx, my, TZ + 0.42), 0.12, 0.6, "glass", r2=0.35, seg=16)
ico("Holograma_Planeta", (mx, my, TZ + 0.75), 0.2, "screen")
cyl("Holograma_Anillo", (mx, my, TZ + 0.75), 0.33, 0.01, "holo", seg=24, rot=(0.35, 0, 0))
HL = bpy.data.lights.new("Luz_Holograma", "POINT")
HL.energy, HL.color, HL.shadow_soft_size = 60, (0.3, 0.6, 1.0), 0.3
ho = bpy.data.objects.new(HL.name, HL)
ho.location = (mx, my, TZ + 0.8)
col.objects.link(ho)

# Tazas
for k, a in enumerate((1.05, 3.15, 5.25)):
    tx, ty = mx + 1.55 * math.cos(a), my + 1.55 * math.sin(a)
    cyl(f"Taza_{k}", (tx, ty, TZ + 0.06), 0.05, 0.12, "mug", seg=12)
    box(f"Taza_{k}_Asa", (tx + 0.06, ty, TZ + 0.06), (0.03, 0.015, 0.07), "mug", bevel=0.005)

# Planta pequeña en maceta roja
ppx, ppy = mx + 0.9 * math.cos(1.05), my + 0.9 * math.sin(1.05)
cyl("Mesa_Maceta", (ppx, ppy, TZ + 0.09), 0.1, 0.18, "pot", r2=0.13, seg=10)
for k in range(6):
    a = k * math.pi / 3
    box(f"Mesa_Planta_Hoja_{k}", (ppx + 0.07 * math.cos(a), ppy + 0.07 * math.sin(a), TZ + 0.26),
        (0.16, 0.07, 0.01), "leaf2" if k % 2 else "leaf", rot=(0, -0.6, a), bevel=0)

# Radio / dispositivo con antena
rx, ry = mx + 0.85 * math.cos(3.6), my + 0.85 * math.sin(3.6)
box("Radio", (rx, ry, TZ + 0.09), (0.28, 0.16, 0.18), "beige", rot=(0, 0, 3.6 + math.pi / 2))
cyl("Radio_Dial", (rx + 0.06 * math.cos(3.6), ry + 0.06 * math.sin(3.6), TZ + 0.1), 0.04, 0.03, "black",
    seg=10, rot=(math.pi / 2, 0, 3.6 + math.pi / 2))
cyl("Radio_Antena", (rx, ry, TZ + 0.32), 0.008, 0.3, "metal", seg=6, rot=(0.3, 0, 3.6))

# Tabletas apiladas
for k in range(3):
    box(f"Tableta_{k}", (mx + 1.0 * math.cos(5.4), my + 1.0 * math.sin(5.4), TZ + 0.01 + k * 0.022),
        (0.3, 0.22, 0.02), ("seat", "orange", "black")[k], rot=(0, 0, 5.4 + k * 0.15), bevel=0.004)

# Bancos con cojín, respaldo y patas metálicas mirando a la mesa
for i in range(6):
    a = i * math.pi / 3
    c, sn = math.cos(a), math.sin(a)
    x, y = mx + 3.0 * c, my + 3.0 * sn
    r = (0, 0, a)
    box(f"Banco_{i}_Asiento", (x, y, 0.47), (0.6, 1.6, 0.12), "cushion", rot=r, bevel=0.04)
    box(f"Banco_{i}_Estructura", (x, y, 0.38), (0.66, 1.68, 0.06), "seat", rot=r)
    box(f"Banco_{i}_Borde", (x - 0.34 * c, y - 0.34 * sn, 0.38), (0.02, 1.68, 0.04), "orange", rot=r, bevel=0)
    box(f"Banco_{i}_Respaldo", (x + 0.3 * c, y + 0.3 * sn, 0.82), (0.1, 1.6, 0.55), "cushion", rot=r, bevel=0.04)
    box(f"Banco_{i}_Respaldo_Soporte", (x + 0.36 * c, y + 0.36 * sn, 0.62), (0.05, 1.2, 0.5), "seat", rot=r)
    for s_ in (-1, 1):
        ox, oy = -sn * s_ * 0.6, c * s_ * 0.6
        box(f"Banco_{i}_Pata_{s_}", (x + ox, y + oy, 0.18), (0.5, 0.08, 0.36), "metal", rot=r)

# Puertas de escotilla (pared sur): marco redondeado, hoja con ventanilla y panel naranja
wy = -D / 2 + 0.15  # cara interior de la pared
for side, x, kside in (("A", 3.0, 1), ("B", -6.5, -1)):
    n = f"Puerta_{side}"
    box(f"{n}_Panel_Pared", (x, wy + 0.04, 1.9), (3.8, 0.08, 3.8), "rust", bevel=0.01)
    box(f"{n}_Panel_Pared_Remate", (x, wy + 0.06, 3.85), (3.8, 0.12, 0.12), "seat", bevel=0.01)
    rounded_slab(f"{n}_Marco", (x, wy + 0.08, 0), 2.9, 3.3, 0.75, 0.24, "door_dark", seg=8)
    rounded_slab(f"{n}_Junta", (x, wy + 0.32, 0), 2.24, 2.98, 0.55, 0.02, "black", seg=8)
    rounded_slab(f"{n}_Hoja", (x, wy + 0.34, 0.02), 2.1, 2.88, 0.48, 0.06, "door", seg=8)
    # Ventanilla redondeada
    rounded_slab(f"{n}_Ventanilla_Marco", (x, wy + 0.4, 1.95), 1.25, 0.72, 0.18, 0.03, "black", seg=4, all_corners=True)
    rounded_slab(f"{n}_Ventanilla", (x, wy + 0.43, 2.0), 1.1, 0.62, 0.14, 0.01, "window", seg=4, all_corners=True)
    # Paneles inferiores en relieve y barra horizontal
    for s_ in (-1, 1):
        rounded_slab(f"{n}_Relieve_{s_}", (x + s_ * 0.45, wy + 0.4, 0.3), 0.7, 1.05, 0.12, 0.03, "door_dark",
                     seg=4, all_corners=True)
    box(f"{n}_Barra", (x, wy + 0.415, 1.55), (1.9, 0.03, 0.08), "door_dark", bevel=0)
    # Pistones laterales del marco
    for s_ in (-1, 1):
        cyl(f"{n}_Piston_{s_}", (x + s_ * 1.27, wy + 0.36, 1.2), 0.05, 1.6, "metal", seg=10)
    # Teclado de pared con pantalla verde
    kx = x + kside * 1.68
    box(f"{n}_Teclado", (kx, wy + 0.12, 1.45), (0.32, 0.08, 0.55), "seat")
    box(f"{n}_Teclado_Pantalla", (kx, wy + 0.165, 1.58), (0.22, 0.01, 0.16), "screen2", bevel=0)
    for r_ in range(2):
        for c_ in range(3):
            box(f"{n}_Teclado_Boton_{r_}{c_}", (kx - 0.07 + c_ * 0.07, wy + 0.165, 1.36 - r_ * 0.07),
                (0.05, 0.01, 0.05), "orange" if r_ else "hazard", bevel=0)
    # Tubo de luz amarillo vertical al otro lado
    cyl(f"{n}_Tubo_Luz", (x - kside * 1.68, wy + 0.16, 1.9), 0.06, 2.6, "yellow_light", seg=10)
    for z in (0.55, 3.25):
        cyl(f"{n}_Tubo_Luz_Soporte_{z}", (x - kside * 1.68, wy + 0.16, z), 0.09, 0.1, "black", seg=10)
    YL = bpy.data.lights.new(f"{n}_Luz_Amarilla", "POINT")
    YL.energy, YL.color, YL.shadow_soft_size = 120, (1.0, 0.75, 0.2), 0.3
    yo = bpy.data.objects.new(YL.name, YL)
    yo.location = (x - kside * 1.68, wy + 0.6, 1.9)
    col.objects.link(yo)
    # Placa metálica de umbral
    box(f"{n}_Umbral", (x, wy + 0.6, 0.01), (2.4, 0.5, 0.02), "metal", bevel=0)

# Cámara
cd = bpy.data.cameras.new("Cam_Lobby")
cd.lens = 16
cam = bpy.data.objects.new("Cam_Lobby", cd)
cam.location = (-8.0, -6.3, 4.3)
cam.rotation_euler = (math.radians(64), 0, math.radians(-48))
col.objects.link(cam)
sc.camera = cam

# Fondo y luz ambiental tenue
w = sc.world or bpy.data.worlds.new("Espacio")
sc.world = w
w.use_nodes = True
bg = next(n for n in w.node_tree.nodes if n.type == "BACKGROUND")
bg.inputs[0].default_value = (0.02, 0.022, 0.018, 1)
bg.inputs[1].default_value = 1.0

try:
    sc.eevee.use_raytracing = True
except AttributeError:
    pass

# Vista previa de materiales en el visor 3D
for area in bpy.context.window.screen.areas:
    if area.type == "VIEW_3D":
        sh = area.spaces.active.shading
        sh.type = "MATERIAL"

print("Lobby_Nave creado:", len(col.objects), "objetos")
