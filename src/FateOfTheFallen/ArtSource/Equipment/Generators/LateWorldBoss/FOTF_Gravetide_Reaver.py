import bpy
import bmesh
import math
import mathutils
import os

# ============================================================
# FOTF - Gravetide Reaver
# One-Handed Ossuary Sword
# v7 - Connected hilt + stronger blade taper
# Stock Blender only - no add-ons required
# ============================================================

CLEAR_SCENE = True
EXPORT_FBX = True
EXPORT_FILENAME = "FOTF_Gravetide_Reaver.fbx"

ROOT_NAME = "FOTF_Gravetide_Reaver"
COLLECTION_NAME = "FOTF_Gravetide_Reaver"

# ------------------------------------------------------------
# HELPERS
# ------------------------------------------------------------

def clear_scene():
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)


def ensure_collection(name):
    col = bpy.data.collections.get(name)
    if col is None:
        col = bpy.data.collections.new(name)
        bpy.context.scene.collection.children.link(col)
    return col


def move_to_collection(obj, col):
    for current_collection in list(obj.users_collection):
        current_collection.objects.unlink(obj)
    col.objects.link(obj)


def make_material(name, rgba, metallic=0.0, roughness=0.5,
                  emission=None, emission_strength=0.0):
    mat = bpy.data.materials.get(name)
    if mat is None:
        mat = bpy.data.materials.new(name=name)

    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")

    bsdf.inputs["Base Color"].default_value = rgba
    bsdf.inputs["Metallic"].default_value = metallic
    bsdf.inputs["Roughness"].default_value = roughness

    if "Emission Color" in bsdf.inputs:
        bsdf.inputs["Emission Color"].default_value = (
            emission if emission else (0, 0, 0, 1)
        )
        bsdf.inputs["Emission Strength"].default_value = emission_strength

    return mat


def assign_material(obj, mat):
    if obj.type != 'MESH':
        return
    obj.data.materials.clear()
    obj.data.materials.append(mat)


def apply_scale(obj):
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.transform_apply(
        location=False,
        rotation=False,
        scale=True
    )
    obj.select_set(False)


def add_bevel(obj, width=0.01, segments=1):
    if obj.type != 'MESH':
        return
    mod = obj.modifiers.new("WornEdges", "BEVEL")
    mod.width = width
    mod.segments = segments


def parent(obj, root):
    obj.parent = root


def cube(name, location, scale, mat, col,
         rotation=(0, 0, 0), bevel=0.0):
    bpy.ops.mesh.primitive_cube_add(
        size=1.0,
        location=location,
        rotation=rotation
    )

    obj = bpy.context.object
    obj.name = name
    obj.scale = scale

    apply_scale(obj)

    if bevel > 0:
        add_bevel(obj, bevel)

    assign_material(obj, mat)
    move_to_collection(obj, col)

    return obj


def cylinder(name, location, radius, depth, mat, col,
             vertices=8, rotation=(0, 0, 0)):
    bpy.ops.mesh.primitive_cylinder_add(
        vertices=vertices,
        radius=radius,
        depth=depth,
        location=location,
        rotation=rotation
    )

    obj = bpy.context.object
    obj.name = name

    assign_material(obj, mat)
    move_to_collection(obj, col)

    return obj


def profile_prism(name, xz_points, depth, mat, col, y_center=0.0):
    half = depth * 0.5

    verts = []

    for x, z in xz_points:
        verts.append((x, y_center - half, z))

    for x, z in xz_points:
        verts.append((x, y_center + half, z))

    n = len(xz_points)

    faces = [
        tuple(range(n)),
        tuple(reversed(range(n, n * 2)))
    ]

    for i in range(n):
        j = (i + 1) % n
        faces.append((i, j, n + j, n + i))

    mesh = bpy.data.meshes.new(name + "_Mesh")
    mesh.from_pydata(verts, [], faces)
    mesh.update()

    obj = bpy.data.objects.new(name, mesh)
    col.objects.link(obj)
    assign_material(obj, mat)

    return obj


def octahedron(name, location, sx, sy, sz, mat, col):
    x, y, z = location

    verts = [
        (x,      y,      z + sz),
        (x + sx, y,      z),
        (x,      y + sy, z),
        (x - sx, y,      z),
        (x,      y - sy, z),
        (x,      y,      z - sz),
    ]

    faces = [
        (0, 1, 2),
        (0, 2, 3),
        (0, 3, 4),
        (0, 4, 1),
        (5, 2, 1),
        (5, 3, 2),
        (5, 4, 3),
        (5, 1, 4),
    ]

    mesh = bpy.data.meshes.new(name + "_Mesh")
    mesh.from_pydata(verts, [], faces)
    mesh.update()

    obj = bpy.data.objects.new(name, mesh)
    col.objects.link(obj)
    assign_material(obj, mat)

    return obj


def bone_bar(name, start, end, radius, mat, col):
    sx, sy, sz = start
    ex, ey, ez = end

    dx = ex - sx
    dy = ey - sy
    dz = ez - sz

    length = math.sqrt(dx*dx + dy*dy + dz*dz)

    mid = (
        (sx + ex) * 0.5,
        (sy + ey) * 0.5,
        (sz + ez) * 0.5
    )

    obj = cylinder(
        name,
        location=mid,
        radius=radius,
        depth=length,
        mat=mat,
        col=col,
        vertices=8
    )

    direction = mathutils.Vector((dx, dy, dz))
    q = mathutils.Vector((0, 0, 1)).rotation_difference(
        direction.normalized()
    )

    obj.rotation_mode = 'QUATERNION'
    obj.rotation_quaternion = q
    obj.rotation_mode = 'XYZ'

    return obj


# ------------------------------------------------------------
# BUILD
# ------------------------------------------------------------


def safe_export_path(filename):
    if bpy.data.filepath:
        base = os.path.dirname(bpy.data.filepath)
    else:
        base = os.path.join(os.path.expanduser("~"), "Documents", "FOTF_Exports")
        os.makedirs(base, exist_ok=True)
    return os.path.join(base, filename)


def recalculate_collection_normals(col):
    """Recalculate outward face normals for every mesh before FBX export."""
    for obj in col.objects:
        if obj is None or obj.type != 'MESH' or obj.data is None:
            continue
        mesh = obj.data
        bm = bmesh.new()
        bm.from_mesh(mesh)
        if bm.faces:
            bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
        bm.to_mesh(mesh)
        bm.free()
        mesh.update()

def build():

    if CLEAR_SCENE:
        clear_scene()

    col = ensure_collection(COLLECTION_NAME)

    # ========================================================
    # MATERIALS
    # ========================================================

    grave_steel = make_material(
        "FOTF_GraveSteel",
        (0.72, 0.75, 0.72, 1),
        metallic=0.58,
        roughness=0.46
    )

    bone = make_material(
        "FOTF_OssuaryBone",
        (0.88, 0.84, 0.72, 1),
        metallic=0.03,
        roughness=0.72
    )

    old_iron = make_material(
        "FOTF_OldGraveIron",
        (0.12, 0.13, 0.12, 1),
        metallic=0.74,
        roughness=0.58
    )

    leather = make_material(
        "FOTF_GraveLeather",
        (0.16, 0.105, 0.065, 1),
        metallic=0.0,
        roughness=0.84
    )

    ghost = make_material(
        "FOTF_GraveGlow",
        (0.66, 0.82, 0.80, 1),
        metallic=0.0,
        roughness=0.25,
        emission=(0.58, 0.86, 0.82, 1),
        emission_strength=1.3
    )

    # ========================================================
    # ROOT
    # ========================================================

    root = bpy.data.objects.new(ROOT_NAME, None)
    col.objects.link(root)

    # ========================================================
    # BLADE
    # More obvious taper:
    # broad at base -> narrower mid -> slim upper blade -> tip
    # ========================================================

    blade_points = [
        (-0.100, 0.405),  # base
        (-0.112, 0.500),  # shoulder
        (-0.096, 0.590),
        (-0.086, 0.900),
        (-0.073, 1.180),
        (-0.058, 1.390),
        (-0.038, 1.520),
        (-0.018, 1.600),

        ( 0.000, 1.660),  # tip

        ( 0.018, 1.600),
        ( 0.038, 1.520),
        ( 0.058, 1.390),
        ( 0.073, 1.180),
        ( 0.086, 0.900),
        ( 0.096, 0.590),
        ( 0.112, 0.500),
        ( 0.100, 0.405),
    ]

    blade = profile_prism(
        "Gravetide_Blade",
        blade_points,
        depth=0.050,
        mat=grave_steel,
        col=col
    )

    add_bevel(blade, 0.008)
    parent(blade, root)

    # ========================================================
    # FUNERARY SPINE
    # Also tapers toward the tip.
    # ========================================================

    spine_points = [
        (-0.024, 0.525),
        (-0.026, 0.900),
        (-0.023, 1.180),
        (-0.018, 1.390),
        (-0.010, 1.535),

        (0.000, 1.595),

        (0.010, 1.535),
        (0.018, 1.390),
        (0.023, 1.180),
        (0.026, 0.900),
        (0.024, 0.525),
    ]

    spine = profile_prism(
        "FunerarySpine",
        spine_points,
        depth=0.060,
        mat=bone,
        col=col,
        y_center=-0.002
    )

    add_bevel(spine, 0.004)
    parent(spine, root)

    # ========================================================
    # TOMBSTONE RICASSO
    # ========================================================

    ricasso_points = [
        (-0.072, 0.395),
        (-0.072, 0.535),
        (-0.052, 0.580),

        (0.000, 0.615),

        (0.052, 0.580),
        (0.072, 0.535),
        (0.072, 0.395),
    ]

    ricasso = profile_prism(
        "Tombstone_Ricasso",
        ricasso_points,
        depth=0.066,
        mat=bone,
        col=col,
        y_center=-0.003
    )

    add_bevel(ricasso, 0.006)
    parent(ricasso, root)

    # ========================================================
    # RUNE
    # ========================================================

    rune_v = cube(
        "GraveRune_V",
        location=(0, -0.038, 0.76),
        scale=(0.008, 0.006, 0.080),
        mat=ghost,
        col=col,
        bevel=0.002
    )

    rune_h = cube(
        "GraveRune_H",
        location=(0, -0.038, 0.79),
        scale=(0.034, 0.006, 0.008),
        mat=ghost,
        col=col,
        bevel=0.002
    )

    parent(rune_v, root)
    parent(rune_h, root)

    # ========================================================
    # SPECTRAL FISSURES
    # ========================================================

    crack_a = cube(
        "SpectralFissure_A",
        location=(-0.038, -0.034, 1.02),
        scale=(0.005, 0.004, 0.065),
        mat=ghost,
        col=col,
        rotation=(0, math.radians(-16), 0)
    )

    crack_b = cube(
        "SpectralFissure_B",
        location=(0.032, -0.034, 1.30),
        scale=(0.004, 0.004, 0.050),
        mat=ghost,
        col=col,
        rotation=(0, math.radians(20), 0)
    )

    parent(crack_a, root)
    parent(crack_b, root)

    # ========================================================
    # CROSSGUARD CORE
    # ========================================================

    guard_core = cube(
        "GuardCore",
        location=(0, 0, 0.455),
        scale=(0.098, 0.036, 0.032),
        mat=old_iron,
        col=col,
        bevel=0.012
    )

    parent(guard_core, root)

    # ========================================================
    # BLADE THROAT
    # Overlaps blade + guard.
    # ========================================================

    blade_throat = cube(
        "BladeThroat",
        location=(0, 0, 0.495),
        scale=(0.052, 0.034, 0.050),
        mat=old_iron,
        col=col,
        bevel=0.006
    )

    parent(blade_throat, root)

    # ========================================================
    # HILT NECK / TANG SOCKET
    # This is the actual gap fix:
    # overlaps upper grip collar and guard core.
    # ========================================================

    hilt_neck = cylinder(
        "HiltNeck",
        location=(0, 0, 0.410),
        radius=0.048,
        depth=0.120,
        mat=old_iron,
        col=col,
        vertices=8
    )

    parent(hilt_neck, root)

    socket_ring = cylinder(
        "GuardSocketRing",
        location=(0, 0, 0.440),
        radius=0.060,
        depth=0.045,
        mat=bone,
        col=col,
        vertices=8
    )

    parent(socket_ring, root)

    # ========================================================
    # OSSUARY CROSSGUARD RIBS
    # ========================================================

    rib_specs = [
        (
            "Rib_L_Inner",
            (-0.055, 0, 0.455),
            (-0.155, 0, 0.405)
        ),
        (
            "Rib_L_Outer",
            (-0.155, 0, 0.405),
            (-0.265, 0, 0.465)
        ),
        (
            "Rib_R_Inner",
            (0.055, 0, 0.455),
            (0.155, 0, 0.405)
        ),
        (
            "Rib_R_Outer",
            (0.155, 0, 0.405),
            (0.265, 0, 0.465)
        ),
    ]

    for name, start, end in rib_specs:
        rib = bone_bar(
            name,
            start,
            end,
            0.020,
            bone,
            col
        )
        parent(rib, root)

    # ========================================================
    # GUARD KNUCKLES / TIPS
    # ========================================================

    guard_details = [
        (
            "GuardKnuckle_L",
            (-0.155, 0, 0.405),
            0.034,
            0.045
        ),
        (
            "GuardKnuckle_R",
            (0.155, 0, 0.405),
            0.034,
            0.045
        ),
        (
            "GuardTip_L",
            (-0.275, 0, 0.475),
            0.028,
            0.050
        ),
        (
            "GuardTip_R",
            (0.275, 0, 0.475),
            0.028,
            0.050
        ),
    ]

    for name, position, sx, sz in guard_details:
        detail = octahedron(
            name,
            position,
            sx,
            0.028,
            sz,
            bone,
            col
        )
        parent(detail, root)

    # ========================================================
    # HANDLE
    # Extended slightly upward so it physically overlaps neck.
    # ========================================================

    handle_core = cylinder(
        "HandleCore",
        location=(0, 0, 0.235),
        radius=0.028,
        depth=0.410,
        mat=old_iron,
        col=col,
        vertices=8
    )

    parent(handle_core, root)

    # ========================================================
    # LEATHER GRIP
    # ========================================================

    grip = cylinder(
        "LeatherGrip",
        location=(0, 0, 0.225),
        radius=0.040,
        depth=0.270,
        mat=leather,
        col=col,
        vertices=8
    )

    parent(grip, root)

    # ========================================================
    # GRIP WRAPS
    # ========================================================

    wrap_positions = [
        0.115,
        0.165,
        0.215,
        0.265,
        0.315
    ]

    for i, z in enumerate(wrap_positions):

        band = cylinder(
            "GripWrap_%02d" % (i + 1),
            location=(0, 0, z),
            radius=0.044,
            depth=0.014,
            mat=(
                bone if i in (0, 4)
                else old_iron
            ),
            col=col,
            vertices=8
        )

        parent(band, root)

    # ========================================================
    # GRIP COLLARS
    # Upper collar now overlaps the hilt neck.
    # ========================================================

    upper_collar = cylinder(
        "UpperGripCollar",
        location=(0, 0, 0.360),
        radius=0.055,
        depth=0.060,
        mat=old_iron,
        col=col,
        vertices=8
    )

    lower_collar = cylinder(
        "LowerGripCollar",
        location=(0, 0, 0.070),
        radius=0.050,
        depth=0.040,
        mat=old_iron,
        col=col,
        vertices=8
    )

    parent(upper_collar, root)
    parent(lower_collar, root)

    # ========================================================
    # RELIQUARY POMMEL
    # ========================================================

    pommel_points = [
        (-0.040, -0.015),
        (-0.055,  0.030),
        (-0.048,  0.080),
        (-0.024,  0.115),

        (0.000, 0.130),

        (0.024,  0.115),
        (0.048,  0.080),
        (0.055,  0.030),
        (0.040, -0.015),
    ]

    pommel = profile_prism(
        "ReliquaryPommel",
        pommel_points,
        depth=0.062,
        mat=bone,
        col=col
    )

    add_bevel(pommel, 0.007)
    parent(pommel, root)

    # ========================================================
    # POMMEL SOUL LIGHT
    # ========================================================

    pommel_gem = octahedron(
        "PommelSoulLight",
        location=(0, -0.036, 0.050),
        sx=0.016,
        sy=0.007,
        sz=0.028,
        mat=ghost,
        col=col
    )

    parent(pommel_gem, root)

    # ========================================================
    # FINAL ORGANIZATION
    # ========================================================

    for obj in col.objects:
        if obj is not root and obj.parent is None:
            obj.parent = root

    bpy.ops.object.select_all(action='DESELECT')

    for obj in col.objects:
        obj.select_set(True)

    bpy.context.view_layer.objects.active = root

    # Unity uses backface culling by default. Recalculate every mesh normal
    # immediately before export so no blade/guard faces arrive inward-wound.
    recalculate_collection_normals(col)

    # ========================================================
    # OPTIONAL FBX EXPORT
    # ========================================================

    if EXPORT_FBX:

        export_path = safe_export_path(EXPORT_FILENAME)

        bpy.ops.export_scene.fbx(
            filepath=export_path,
            use_selection=True,
            object_types={'EMPTY', 'MESH'},
            apply_unit_scale=True,
            bake_space_transform=False,
            mesh_smooth_type='OFF',
            add_leaf_bones=False,
            axis_forward='-Z',
            axis_up='Y'
        )

        print("Exported:", export_path)

    print(
        "Created FOTF Gravetide Reaver v8 - connected hilt, tapered blade, outward normals."
    )


# ------------------------------------------------------------
# RUN
# ------------------------------------------------------------

if __name__ == "__main__":
    build()
