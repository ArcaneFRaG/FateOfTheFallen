import bpy
import math
import os

# ============================================================
# FOTF - Infernal Bastion
# V5 - Surface-Mounted Rebuild
# Blender 4.x / 5.x - stock primitives only
#
# Fixes over V4:
#   - Separates MAIN BODY front depth from FRONT PLATE depth.
#   - Outer rim pieces are seated into the main shield body.
#   - Internal ribs/fractures are seated into the front plate.
#   - No front accent should have an air gap behind it.
#   - Replaces the solid "core frame plate" with an open frame.
#   - Restores a clearly visible low-poly glowing ember gem.
#   - Rear grip mounts are calculated to physically bridge the
#     shield back to the grip instead of visually floating.
#   - Keeps corrected outward normals and explicit Grip marker.
#
# Orientation:
#   FRONT / decorated face = negative local Y
#   BACK / grip side       = positive local Y
# ============================================================

PREFAB_NAME = "FOTF_Infernal_Bastion"
EXPORT_FILE = PREFAB_NAME + ".fbx"
AUTO_EXPORT = False

# ------------------------------------------------------------
# Authoring planes
# ------------------------------------------------------------

BODY_CENTER_Y = 0.0
BODY_HALF_DEPTH = 0.052
BODY_FRONT_Y = BODY_CENTER_Y - BODY_HALF_DEPTH
BODY_BACK_Y = BODY_CENTER_Y + BODY_HALF_DEPTH

FRONT_PLATE_CENTER_Y = -0.060
FRONT_PLATE_HALF_DEPTH = 0.012
PLATE_FRONT_Y = FRONT_PLATE_CENTER_Y - FRONT_PLATE_HALF_DEPTH
PLATE_BACK_Y = FRONT_PLATE_CENTER_Y + FRONT_PLATE_HALF_DEPTH

# Surface overlap: details should actually intersect their substrate.
SURFACE_OVERLAP = 0.004

# ------------------------------------------------------------
# Helpers
# ------------------------------------------------------------

def clear_scene():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)


def make_material(name, base_color, roughness=0.75, metallic=0.0,
                  emission_color=None, emission_strength=0.0):
    mat = bpy.data.materials.get(name)
    if mat is None:
        mat = bpy.data.materials.new(name)

    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")

    bsdf.inputs["Base Color"].default_value = (*base_color, 1.0)
    bsdf.inputs["Roughness"].default_value = roughness
    bsdf.inputs["Metallic"].default_value = metallic

    if emission_color is not None:
        if "Emission Color" in bsdf.inputs:
            bsdf.inputs["Emission Color"].default_value = (*emission_color, 1.0)
        elif "Emission" in bsdf.inputs:
            bsdf.inputs["Emission"].default_value = (*emission_color, 1.0)

        if "Emission Strength" in bsdf.inputs:
            bsdf.inputs["Emission Strength"].default_value = emission_strength

    return mat


def shade_flat(obj):
    if obj and obj.type == "MESH":
        for poly in obj.data.polygons:
            poly.use_smooth = False


def apply_scale(obj):
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    obj.select_set(False)


def recalculate_outside(obj):
    if obj is None or obj.type != "MESH":
        return

    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj

    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")

    try:
        bpy.ops.mesh.normals_make_consistent(inside=False)
    except Exception:
        pass

    bpy.ops.object.mode_set(mode="OBJECT")
    obj.select_set(False)


def front_mounted_center(substrate_front_y, half_depth, overlap=SURFACE_OVERLAP):
    """
    Places a detail on a negative-Y-facing surface.

    Back face of the detail penetrates the substrate by `overlap`,
    eliminating visible air gaps.
    """
    return substrate_front_y - half_depth + overlap


def create_profile_prism(name, profile, half_thickness, material, y_center=0.0):
    """
    Extrude an X/Z silhouette through Y.

    Front cap faces -Y.
    Rear cap faces +Y.
    """
    count = len(profile)

    front = [(x, y_center - half_thickness, z) for x, z in profile]
    back = [(x, y_center + half_thickness, z) for x, z in profile]

    vertices = front + back
    faces = []

    # Front cap -> -Y.
    for i in range(1, count - 1):
        faces.append((0, i + 1, i))

    # Rear cap -> +Y.
    for i in range(1, count - 1):
        faces.append((count, count + i, count + i + 1))

    # Side walls.
    for i in range(count):
        j = (i + 1) % count
        faces.append((i, count + i, count + j, j))

    mesh = bpy.data.meshes.new(name + "_Mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.update()

    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    obj.data.materials.append(material)

    shade_flat(obj)
    recalculate_outside(obj)
    return obj


def add_cube(name, loc, scale, material, bevel=0.0, rotation=(0.0, 0.0, 0.0)):
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=loc, rotation=rotation)
    obj = bpy.context.object
    obj.name = name
    obj.scale = scale
    apply_scale(obj)

    if bevel > 0.0:
        mod = obj.modifiers.new("LowPolyBevel", "BEVEL")
        mod.width = bevel
        mod.segments = 1
        bpy.context.view_layer.objects.active = obj
        bpy.ops.object.modifier_apply(modifier=mod.name)

    obj.data.materials.append(material)
    shade_flat(obj)
    recalculate_outside(obj)
    return obj


def add_cylinder(name, loc, radius, depth, material, vertices=8,
                 rotation=(0.0, 0.0, 0.0), scale=(1.0, 1.0, 1.0)):
    bpy.ops.mesh.primitive_cylinder_add(
        vertices=vertices,
        radius=radius,
        depth=depth,
        location=loc,
        rotation=rotation
    )
    obj = bpy.context.object
    obj.name = name
    obj.scale = scale
    apply_scale(obj)
    obj.data.materials.append(material)
    shade_flat(obj)
    recalculate_outside(obj)
    return obj


def add_ico(name, loc, radius, material, subdivisions=1, scale=(1.0, 1.0, 1.0)):
    bpy.ops.mesh.primitive_ico_sphere_add(
        subdivisions=subdivisions,
        radius=radius,
        location=loc
    )
    obj = bpy.context.object
    obj.name = name
    obj.scale = scale
    apply_scale(obj)
    obj.data.materials.append(material)
    shade_flat(obj)
    recalculate_outside(obj)
    return obj


def bar_between(name, start, end, width, half_depth, material, y_center):
    x1, z1 = start
    x2, z2 = end

    dx = x2 - x1
    dz = z2 - z1
    length = math.sqrt(dx * dx + dz * dz)

    mid = ((x1 + x2) * 0.5, y_center, (z1 + z2) * 0.5)
    angle = math.atan2(dz, dx)

    return add_cube(
        name,
        mid,
        (length * 0.5, half_depth, width * 0.5),
        material,
        bevel=min(0.0045, width * 0.18),
        rotation=(0.0, -angle, 0.0)
    )


def make_root():
    bpy.ops.object.empty_add(type="PLAIN_AXES", location=(0.0, 0.0, 0.0))
    root = bpy.context.object
    root.name = PREFAB_NAME
    return root


def make_marker(name, location):
    bpy.ops.object.empty_add(type="PLAIN_AXES", location=location)
    marker = bpy.context.object
    marker.name = name
    marker.empty_display_size = 0.05
    return marker


def parent_all(root, objects):
    for obj in objects:
        if obj is not None and obj != root:
            obj.parent = root


def walk_children(obj):
    for child in obj.children:
        yield child
        yield from walk_children(child)


def final_mesh_cleanup(root):
    for obj in walk_children(root):
        if obj.type == "MESH":
            recalculate_outside(obj)


def resolve_export_directory():
    if bpy.data.filepath:
        return os.path.dirname(bpy.data.filepath)

    home = os.path.expanduser("~")
    docs = os.path.join(home, "Documents")

    if os.path.isdir(docs):
        export_dir = os.path.join(docs, "FOTF_Exports")
        os.makedirs(export_dir, exist_ok=True)
        return export_dir

    return home


def export_fbx(root):
    final_mesh_cleanup(root)

    bpy.ops.object.select_all(action="DESELECT")
    root.select_set(True)

    for obj in walk_children(root):
        obj.select_set(True)

    bpy.context.view_layer.objects.active = root

    path = os.path.join(resolve_export_directory(), EXPORT_FILE)

    bpy.ops.export_scene.fbx(
        filepath=path,
        use_selection=True,
        add_leaf_bones=False,
        bake_anim=False,
        apply_scale_options="FBX_SCALE_ALL"
    )

    print("Exported:", path)


# ------------------------------------------------------------
# Materials
# ------------------------------------------------------------

clear_scene()

black_iron = make_material(
    "FOTF_Mat_InfernalBastion_BlackIron",
    (0.022, 0.018, 0.017),
    roughness=0.55,
    metallic=0.86
)

scorched_steel = make_material(
    "FOTF_Mat_InfernalBastion_ScorchedSteel",
    (0.115, 0.070, 0.046),
    roughness=0.48,
    metallic=0.74
)

brass = make_material(
    "FOTF_Mat_InfernalBastion_Brass",
    (0.30, 0.135, 0.037),
    roughness=0.38,
    metallic=0.78
)

edge_steel = make_material(
    "FOTF_Mat_InfernalBastion_EdgeSteel",
    (0.25, 0.14, 0.070),
    roughness=0.34,
    metallic=0.80
)

leather = make_material(
    "FOTF_Mat_InfernalBastion_Leather",
    (0.060, 0.020, 0.012),
    roughness=0.95,
    metallic=0.0
)

fracture_dark = make_material(
    "FOTF_Mat_InfernalBastion_FractureDark",
    (0.018, 0.004, 0.001),
    roughness=0.85,
    metallic=0.0
)

ember = make_material(
    "FOTF_Mat_InfernalBastion_Ember",
    (0.95, 0.07, 0.005),
    roughness=0.15,
    metallic=0.0,
    emission_color=(1.0, 0.10, 0.005),
    emission_strength=5.0
)

ember_hot = make_material(
    "FOTF_Mat_InfernalBastion_EmberHot",
    (1.0, 0.42, 0.018),
    roughness=0.08,
    metallic=0.0,
    emission_color=(1.0, 0.55, 0.025),
    emission_strength=8.0
)

parts = []

# ------------------------------------------------------------
# Main body
# ------------------------------------------------------------

outer = [
    (-0.34,  0.62),
    (-0.22,  0.70),
    ( 0.00,  0.74),
    ( 0.22,  0.70),
    ( 0.34,  0.62),
    ( 0.39,  0.30),
    ( 0.36, -0.08),
    ( 0.27, -0.38),
    ( 0.00, -0.70),
    (-0.27, -0.38),
    (-0.36, -0.08),
    (-0.39,  0.30),
]

body = create_profile_prism(
    "Shield_MainBody",
    outer,
    half_thickness=BODY_HALF_DEPTH,
    material=black_iron,
    y_center=BODY_CENTER_Y
)
parts.append(body)

front = [
    (-0.29,  0.56),
    (-0.19,  0.63),
    ( 0.00,  0.67),
    ( 0.19,  0.63),
    ( 0.29,  0.56),
    ( 0.33,  0.28),
    ( 0.30, -0.05),
    ( 0.22, -0.31),
    ( 0.00, -0.59),
    (-0.22, -0.31),
    (-0.30, -0.05),
    (-0.33,  0.28),
]

front_plate = create_profile_prism(
    "Shield_FrontPlate",
    front,
    half_thickness=FRONT_PLATE_HALF_DEPTH,
    material=scorched_steel,
    y_center=FRONT_PLATE_CENTER_Y
)
parts.append(front_plate)

# ------------------------------------------------------------
# Outer rim - seats on MAIN BODY, not front plate
# ------------------------------------------------------------

RIM_HALF_DEPTH = 0.006
RIM_Y = front_mounted_center(BODY_FRONT_Y, RIM_HALF_DEPTH)

for i in range(len(outer)):
    a = outer[i]
    b = outer[(i + 1) % len(outer)]
    parts.append(
        bar_between(
            f"Rim_{i:02d}",
            a,
            b,
            width=0.038,
            half_depth=RIM_HALF_DEPTH,
            material=brass,
            y_center=RIM_Y
        )
    )

# ------------------------------------------------------------
# Central furnace - seats on FRONT PLATE
# ------------------------------------------------------------

SPINE_HALF_DEPTH = 0.006
SPINE_Y = front_mounted_center(PLATE_FRONT_Y, SPINE_HALF_DEPTH)

spine = add_cube(
    "Furnace_Spine",
    (0.0, SPINE_Y, 0.005),
    (0.040, SPINE_HALF_DEPTH, 0.455),
    black_iron,
    bevel=0.010
)
parts.append(spine)

# Crown sits on plate.
CROWN_HALF_DEPTH = 0.008
CROWN_Y = front_mounted_center(PLATE_FRONT_Y, CROWN_HALF_DEPTH)

crown = create_profile_prism(
    "Furnace_Crown",
    [
        (-0.115, -0.040),
        (-0.082,  0.095),
        ( 0.000,  0.188),
        ( 0.082,  0.095),
        ( 0.115, -0.040),
        ( 0.050, -0.110),
        (-0.050, -0.110),
    ],
    CROWN_HALF_DEPTH,
    edge_steel,
    y_center=CROWN_Y
)
crown.location.z = 0.34
parts.append(crown)

# ------------------------------------------------------------
# Open ember-core frame
# ------------------------------------------------------------
# V4 used a solid polygon as the "frame", which could occlude
# the gem. V5 uses separate frame bars around an open center.
# ------------------------------------------------------------

FRAME_HALF_DEPTH = 0.006
FRAME_Y = front_mounted_center(PLATE_FRONT_Y, FRAME_HALF_DEPTH)

core_frame_points = [
    (-0.100,  0.000),
    (-0.075,  0.105),
    ( 0.000,  0.150),
    ( 0.075,  0.105),
    ( 0.100,  0.000),
    ( 0.070, -0.105),
    ( 0.000, -0.145),
    (-0.070, -0.105),
]

core_z = 0.075

for i in range(len(core_frame_points)):
    a = core_frame_points[i]
    b = core_frame_points[(i + 1) % len(core_frame_points)]

    a_shift = (a[0], a[1] + core_z)
    b_shift = (b[0], b[1] + core_z)

    parts.append(
        bar_between(
            f"EmberCore_Frame_{i:02d}",
            a_shift,
            b_shift,
            width=0.027,
            half_depth=FRAME_HALF_DEPTH,
            material=brass,
            y_center=FRAME_Y
        )
    )

# ------------------------------------------------------------
# Visible faceted glowing gem
# ------------------------------------------------------------
# Positioned farther forward than the frame and large enough to
# remain clearly visible in Solid/Material Preview and Unity.
# ------------------------------------------------------------

gem = add_ico(
    "Furnace_EmberGem",
    (0.0, -0.087, core_z),
    radius=0.105,
    material=ember_hot,
    subdivisions=1,
    scale=(0.68, 0.24, 1.12)
)
parts.append(gem)

# Small dark cradle immediately behind gem.
cradle = add_ico(
    "Furnace_EmberGem_Cradle",
    (0.0, -0.077, core_z),
    radius=0.118,
    material=black_iron,
    subdivisions=1,
    scale=(0.78, 0.16, 1.18)
)
parts.append(cradle)

# Ensure gem draws farther forward than cradle.
gem.location.y = -0.091

# ------------------------------------------------------------
# Internal reinforcement ribs - front plate surface
# ------------------------------------------------------------

RIB_HALF_DEPTH = 0.0045
RIB_Y = front_mounted_center(PLATE_FRONT_Y, RIB_HALF_DEPTH)

rib_specs = [
    ((0.00, 0.34), ( 0.25, 0.49)),
    ((0.00, 0.34), (-0.25, 0.49)),
    ((0.00, 0.04), ( 0.27, 0.17)),
    ((0.00, 0.04), (-0.27, 0.17)),
    ((0.00,-0.21), ( 0.20,-0.34)),
    ((0.00,-0.21), (-0.20,-0.34)),
]

for i, (a, b) in enumerate(rib_specs):
    parts.append(
        bar_between(
            f"Reinforcement_{i:02d}",
            a,
            b,
            width=0.022,
            half_depth=RIB_HALF_DEPTH,
            material=black_iron,
            y_center=RIB_Y
        )
    )

# ------------------------------------------------------------
# Top horns - seated primarily on main body
# ------------------------------------------------------------

HORN_HALF_DEPTH = 0.009
HORN_Y = front_mounted_center(BODY_FRONT_Y, HORN_HALF_DEPTH)

for side in (-1, 1):
    horn = create_profile_prism(
        "Horn_Left" if side < 0 else "Horn_Right",
        [
            (-0.035,-0.035),
            (-0.020, 0.040),
            ( 0.015, 0.120),
            ( 0.045, 0.025),
            ( 0.030,-0.040),
        ],
        HORN_HALF_DEPTH,
        black_iron,
        y_center=HORN_Y
    )
    horn.location.x = 0.29 * side
    horn.location.z = 0.63
    horn.rotation_euler[1] = math.radians(16 * side)
    parts.append(horn)

# ------------------------------------------------------------
# Ember fracture network - embedded in front plate
# ------------------------------------------------------------

fractures = [
    [(0.00, 0.08), (-0.08, 0.18), (-0.17, 0.28), (-0.24, 0.39)],
    [(0.01, 0.04), ( 0.10, 0.13), ( 0.18, 0.18), ( 0.27, 0.24)],
    [(-0.01,-0.05), (-0.08,-0.15), (-0.15,-0.25), (-0.20,-0.35)],
    [(0.01,-0.10), ( 0.09,-0.18), ( 0.15,-0.28), ( 0.19,-0.38)],
]

DARK_HALF_DEPTH = 0.0025
HOT_HALF_DEPTH = 0.0014

DARK_Y = front_mounted_center(PLATE_FRONT_Y, DARK_HALF_DEPTH, overlap=0.003)
HOT_Y = DARK_Y - DARK_HALF_DEPTH - HOT_HALF_DEPTH + 0.0015

for fi, pts in enumerate(fractures):
    for si in range(len(pts) - 1):
        a = pts[si]
        b = pts[si + 1]

        parts.append(
            bar_between(
                f"Fracture_{fi:02d}_{si:02d}_Dark",
                a,
                b,
                width=0.012,
                half_depth=DARK_HALF_DEPTH,
                material=fracture_dark,
                y_center=DARK_Y
            )
        )

        # Slightly shorter glowing inset.
        x1, z1 = a
        x2, z2 = b
        mx = (x1 + x2) * 0.5
        mz = (z1 + z2) * 0.5

        shrink = 0.90
        ga = (
            mx + (x1 - mx) * shrink,
            mz + (z1 - mz) * shrink
        )
        gb = (
            mx + (x2 - mx) * shrink,
            mz + (z2 - mz) * shrink
        )

        parts.append(
            bar_between(
                f"Fracture_{fi:02d}_{si:02d}_Hot",
                ga,
                gb,
                width=0.0046,
                half_depth=HOT_HALF_DEPTH,
                material=ember,
                y_center=HOT_Y
            )
        )

# ------------------------------------------------------------
# Rear grip hardware
# ------------------------------------------------------------
# The grip remains intentionally spaced away from the shield so
# a hand can fit around it. The mounting bridges now calculate
# directly between the shield back and the grip.
# ------------------------------------------------------------

HANDLE_RADIUS = 0.030
HANDLE_CENTER_Y = 0.098
HANDLE_NEAR_Y = HANDLE_CENTER_Y - HANDLE_RADIUS

MOUNT_FRONT_Y = BODY_BACK_Y - 0.004
MOUNT_BACK_Y = HANDLE_NEAR_Y + 0.006
MOUNT_CENTER_Y = (MOUNT_FRONT_Y + MOUNT_BACK_Y) * 0.5
MOUNT_HALF_DEPTH = (MOUNT_BACK_Y - MOUNT_FRONT_Y) * 0.5

upper_mount = add_cube(
    "Grip_Mount_Upper",
    (0.0, MOUNT_CENTER_Y, 0.115),
    (0.082, MOUNT_HALF_DEPTH, 0.032),
    brass,
    bevel=0.008
)
lower_mount = add_cube(
    "Grip_Mount_Lower",
    (0.0, MOUNT_CENTER_Y, -0.115),
    (0.082, MOUNT_HALF_DEPTH, 0.032),
    brass,
    bevel=0.008
)
parts.extend([upper_mount, lower_mount])

handle = add_cylinder(
    "Grip_Handle",
    (0.0, HANDLE_CENTER_Y, 0.0),
    radius=HANDLE_RADIUS,
    depth=0.23,
    material=leather,
    vertices=8
)
parts.append(handle)

for z in (-0.115, 0.115):
    parts.append(
        add_cylinder(
            "Grip_Cap_Lower" if z < 0 else "Grip_Cap_Upper",
            (0.0, HANDLE_CENTER_Y, z),
            radius=0.038,
            depth=0.026,
            material=brass,
            vertices=8
        )
    )

# Forearm strap - visibly attached to body via short side blocks.
STRAP_CENTER_Y = 0.082

strap = add_cube(
    "Arm_Strap",
    (0.0, STRAP_CENTER_Y, 0.30),
    (0.16, 0.022, 0.025),
    leather,
    bevel=0.006
)
parts.append(strap)

for side in (-1, 1):
    bridge = add_cube(
        "Arm_Strap_Bridge_Left" if side < 0 else "Arm_Strap_Bridge_Right",
        (0.115 * side, 0.067, 0.30),
        (0.028, 0.018, 0.030),
        brass,
        bevel=0.005
    )
    parts.append(bridge)

# ------------------------------------------------------------
# Root + markers
# ------------------------------------------------------------

root = make_root()
parent_all(root, parts)

grip_marker = make_marker("Grip", (0.0, HANDLE_CENTER_Y, 0.0))
grip_marker.parent = root

front_marker = make_marker("Front", (0.0, -0.16, 0.0))
front_marker.parent = root

final_mesh_cleanup(root)

bpy.ops.object.select_all(action="DESELECT")
root.select_set(True)
bpy.context.view_layer.objects.active = root

if AUTO_EXPORT:
    export_fbx(root)

print("")
print("FOTF Infernal Bastion V5 generated.")
print("")
print("Surface planes:")
print("  body front :", BODY_FRONT_Y)
print("  plate front:", PLATE_FRONT_Y)
print("")
print("V5 fixes:")
print("  outer rim mounted to main-body surface")
print("  internal ribs mounted to front-plate surface")
print("  fracture channels physically overlap front plate")
print("  solid core cover removed")
print("  visible faceted Furnace_EmberGem restored")
print("  rear grip brackets physically bridge shield and handle")
print("  all meshes receive outward-normal cleanup")
print("  Grip marker retained for runtime hand alignment")
print("")
print("Orientation:")
print("  decorated face = -Y")
print("  grip side      = +Y")
print("")
print("Export:")
print("  AUTO_EXPORT =", AUTO_EXPORT)
print("  Saved .blend -> export beside .blend")
print("  Unsaved .blend -> ~/Documents/FOTF_Exports")
print("")
