# Fate of the Fallen - Blighted Spore Plant
# Blender 3.6 / 4.x
#
# Creates an original low-poly static harvestable plant for Quest 2.
# No Erenshor/Synty asset geometry is copied or required.
#
# Visual language:
# - thick diseased stems
# - broad wilted leaves
# - swollen purple spore pods
# - pale sickly spore nodules around the pod rims
# - exposed roots gripping the ground
#
# Unity runtime expects the prefab root to be named:
#     BlightedSporePlant
# and a child named:
#     HarvestableSpores
# The runtime hides HarvestableSpores after a successful harvest.

import bpy
import math
import os
from mathutils import Vector

COLLECTION_NAME = "FOTF_BlightedSporePlant"
ROOT_NAME = "BlightedSporePlant"
DELETE_OLD = True
AUTO_EXPORT_FBX = False
EXPORT_FILENAME = "BlightedSporePlant.fbx"


def ensure_object_mode():
    if bpy.context.object is not None and bpy.context.object.mode != 'OBJECT':
        bpy.ops.object.mode_set(mode='OBJECT')


def remove_collection(name):
    coll = bpy.data.collections.get(name)
    if coll is None:
        return
    for obj in list(coll.objects):
        bpy.data.objects.remove(obj, do_unlink=True)
    bpy.data.collections.remove(coll)


def make_collection(name):
    coll = bpy.data.collections.new(name)
    bpy.context.scene.collection.children.link(coll)
    return coll


def material(name, rgba, roughness=0.9, emission=None):
    mat = bpy.data.materials.get(name)
    if mat is None:
        mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    mat.diffuse_color = rgba
    bsdf = mat.node_tree.nodes.get("Principled BSDF") if mat.node_tree else None
    if bsdf:
        if "Base Color" in bsdf.inputs:
            bsdf.inputs["Base Color"].default_value = rgba
        if "Roughness" in bsdf.inputs:
            bsdf.inputs["Roughness"].default_value = roughness
        if emission is not None:
            if "Emission Color" in bsdf.inputs:
                bsdf.inputs["Emission Color"].default_value = emission
                if "Emission Strength" in bsdf.inputs:
                    bsdf.inputs["Emission Strength"].default_value = 0.35
            elif "Emission" in bsdf.inputs:
                bsdf.inputs["Emission"].default_value = emission
    return mat


MAT_STEM = material("FOTF_BlightPlant_Stem", (0.19, 0.25, 0.09, 1.0), 0.95)
MAT_STEM_DARK = material("FOTF_BlightPlant_StemDark", (0.10, 0.13, 0.045, 1.0), 0.95)
MAT_LEAF = material("FOTF_BlightPlant_Leaf", (0.25, 0.31, 0.105, 1.0), 0.95)
MAT_LEAF_DEAD = material("FOTF_BlightPlant_LeafDead", (0.28, 0.20, 0.08, 1.0), 1.0)
MAT_POD = material("FOTF_BlightPlant_Pod", (0.31, 0.12, 0.36, 1.0), 0.80)
MAT_POD_DARK = material("FOTF_BlightPlant_PodDark", (0.13, 0.045, 0.17, 1.0), 0.92)
MAT_SPORE = material(
    "FOTF_BlightPlant_Spore",
    (0.62, 0.72, 0.20, 1.0),
    0.65,
    emission=(0.23, 0.32, 0.035, 1.0)
)
MAT_ROOT = material("FOTF_BlightPlant_Root", (0.16, 0.10, 0.045, 1.0), 1.0)


ensure_object_mode()
if DELETE_OLD:
    remove_collection(COLLECTION_NAME)
COLL = make_collection(COLLECTION_NAME)

root = bpy.data.objects.new(ROOT_NAME, None)
COLL.objects.link(root)
root["FOTF_Type"] = "QuestHarvestable"
root["FOTF_ItemId"] = "1700"
root["FOTF_ItemName"] = "Blighted Spores"
root["FOTF_Quest"] = "Signs of Decay"

harvest_root = bpy.data.objects.new("HarvestableSpores", None)
COLL.objects.link(harvest_root)
harvest_root.parent = root


def parent_to(obj, parent):
    obj.parent = parent
    return obj


def add_material(obj, mat):
    if obj.type == 'MESH':
        obj.data.materials.append(mat)


def add_cone(name, location, radius1, radius2, depth, vertices, mat, parent=root, rotation=(0,0,0)):
    bpy.ops.mesh.primitive_cone_add(
        vertices=vertices,
        radius1=radius1,
        radius2=radius2,
        depth=depth,
        end_fill_type='NGON',
        location=location,
        rotation=rotation,
    )
    obj = bpy.context.object
    obj.name = name
    add_material(obj, mat)
    parent_to(obj, parent)
    return obj


def add_uv_sphere(name, location, scale, segments, rings, mat, parent=root):
    bpy.ops.mesh.primitive_uv_sphere_add(
        segments=segments,
        ring_count=rings,
        location=location,
    )
    obj = bpy.context.object
    obj.name = name
    obj.scale = scale
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    add_material(obj, mat)
    parent_to(obj, parent)
    return obj


def add_ico(name, location, scale, subdivisions, mat, parent=root):
    bpy.ops.mesh.primitive_ico_sphere_add(
        subdivisions=subdivisions,
        radius=1.0,
        location=location,
    )
    obj = bpy.context.object
    obj.name = name
    obj.scale = scale
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    add_material(obj, mat)
    parent_to(obj, parent)
    return obj


def create_leaf(name, center, length, width, angle_deg, tilt_deg, mat):
    # Simple faceted lanceolate leaf: 6 verts, 4 triangles.
    a = math.radians(angle_deg)
    t = math.radians(tilt_deg)
    forward = Vector((math.sin(a), math.cos(a), 0.0))
    side = Vector((math.cos(a), -math.sin(a), 0.0))
    up = Vector((0.0, 0.0, 1.0))

    base = Vector(center)
    tip = base + forward * length * math.cos(t) + up * length * math.sin(t)
    mid = base.lerp(tip, 0.55)
    ridge = up * (0.035 + length * 0.025)

    verts = [
        base - side * width * 0.20,
        mid - side * width * 0.50 + ridge,
        tip,
        mid + side * width * 0.50 + ridge,
        base + side * width * 0.20,
        mid + ridge * 0.20,
    ]
    faces = [
        (0, 1, 5),
        (1, 2, 5),
        (2, 3, 5),
        (3, 4, 5),
    ]
    mesh = bpy.data.meshes.new(name + "_Mesh")
    mesh.from_pydata([tuple(v) for v in verts], [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    COLL.objects.link(obj)
    add_material(obj, mat)
    parent_to(obj, root)
    return obj


def create_root(name, angle_deg, length, radius):
    a = math.radians(angle_deg)
    midpoint = Vector((math.sin(a) * length * 0.45, math.cos(a) * length * 0.45, 0.055))
    rot = (math.radians(82), 0.0, -a)
    return add_cone(
        name,
        midpoint,
        radius,
        radius * 0.25,
        length,
        6,
        MAT_ROOT,
        root,
        rot,
    )


# -----------------------------------------------------------------------------
# ROOT MASS / EXPOSED ROOTS
# -----------------------------------------------------------------------------
add_ico("RootMass", (0, 0, 0.16), (0.42, 0.34, 0.18), 1, MAT_ROOT)
for i, ang in enumerate((5, 62, 118, 181, 238, 304)):
    create_root("Root_%02d" % (i + 1), ang, 0.72 + (i % 2) * 0.10, 0.075)


# -----------------------------------------------------------------------------
# BASE LEAVES
# -----------------------------------------------------------------------------
leaf_specs = [
    (0.02, 0.02, 0.20, 0.85, 0.34, 12, 12, MAT_LEAF),
    (-0.03, 0.01, 0.18, 0.78, 0.29, 76, 18, MAT_LEAF_DEAD),
    (0.00, -0.02, 0.17, 0.92, 0.32, 139, 9, MAT_LEAF),
    (0.03, -0.01, 0.16, 0.74, 0.26, 210, 22, MAT_LEAF_DEAD),
    (-0.02, 0.03, 0.19, 0.88, 0.31, 276, 14, MAT_LEAF),
    (0.01, 0.00, 0.18, 0.68, 0.23, 332, 25, MAT_LEAF),
]
for i, spec in enumerate(leaf_specs):
    x, y, z, ln, wd, ang, tilt, mat = spec
    create_leaf("Leaf_%02d" % (i + 1), (x, y, z), ln, wd, ang, tilt, mat)


# -----------------------------------------------------------------------------
# MAIN STEMS / PODS
# -----------------------------------------------------------------------------
stems = [
    # x, y, baseZ, height, rBase, rTop, leanX, leanY, podScale
    (0.00, 0.00, 0.25, 1.20, 0.105, 0.065,  0.05, -0.03, (0.27, 0.24, 0.22)),
    (-0.24, 0.10, 0.23, 0.88, 0.085, 0.050, -0.06,  0.02, (0.22, 0.19, 0.18)),
    (0.24, 0.12, 0.24, 0.72, 0.077, 0.045,  0.05,  0.05, (0.19, 0.17, 0.16)),
    (0.09, -0.24, 0.22, 0.64, 0.070, 0.042, -0.02, -0.05, (0.17, 0.15, 0.15)),
]

for i, s in enumerate(stems):
    x, y, bz, h, rb, rt, lx, ly, pod_scale = s
    center = (x + lx * 0.5, y + ly * 0.5, bz + h * 0.5)
    # Slight lean by rotating around local X/Y.
    rot_x = math.atan2(ly, max(h, 0.001))
    rot_y = -math.atan2(lx, max(h, 0.001))
    stem_mat = MAT_STEM if i % 2 == 0 else MAT_STEM_DARK
    add_cone(
        "Stem_%02d" % (i + 1),
        center,
        rb,
        rt,
        h,
        7,
        stem_mat,
        root,
        (rot_x, rot_y, 0.0),
    )

    pod_center = (x + lx, y + ly, bz + h + pod_scale[2] * 0.45)
    pod = add_ico(
        "SporePod_%02d" % (i + 1),
        pod_center,
        pod_scale,
        2,
        MAT_POD if i != 3 else MAT_POD_DARK,
        harvest_root,
    )

    # Split-looking darker crown.
    crown_z = pod_center[2] + pod_scale[2] * 0.70
    add_cone(
        "PodCrown_%02d" % (i + 1),
        (pod_center[0], pod_center[1], crown_z),
        pod_scale[0] * 0.38,
        pod_scale[0] * 0.08,
        pod_scale[2] * 0.28,
        6,
        MAT_POD_DARK,
        harvest_root,
    )

    # Spore nodules around the upper rim. Main pod gets more.
    count = 9 if i == 0 else 6
    radius = pod_scale[0] * 0.77
    for j in range(count):
        a = (math.pi * 2.0 * j / count) + i * 0.35
        jitter = 0.92 + (0.12 if (j + i) % 2 == 0 else -0.04)
        pos = (
            pod_center[0] + math.cos(a) * radius,
            pod_center[1] + math.sin(a) * radius,
            pod_center[2] + pod_scale[2] * (0.15 + 0.16 * ((j % 3) / 2.0)),
        )
        rr = pod_scale[0] * 0.12 * jitter
        add_ico(
            "Spore_%02d_%02d" % (i + 1, j + 1),
            pos,
            (rr, rr * 0.88, rr * 0.95),
            1,
            MAT_SPORE,
            harvest_root,
        )


# -----------------------------------------------------------------------------
# SECONDARY LEAVES UP THE MAIN STEM
# -----------------------------------------------------------------------------
create_leaf("StemLeaf_A", (0.00, 0.00, 0.66), 0.48, 0.18, 50, 20, MAT_LEAF)
create_leaf("StemLeaf_B", (0.02, 0.00, 0.82), 0.40, 0.16, 230, 28, MAT_LEAF_DEAD)
create_leaf("StemLeaf_C", (-0.22, 0.10, 0.52), 0.35, 0.14, 320, 26, MAT_LEAF)


# -----------------------------------------------------------------------------
# FINALIZE
# -----------------------------------------------------------------------------
# Flat shading is intentional for the Erenshor-friendly low-poly silhouette.
for obj in COLL.objects:
    if obj.type == 'MESH':
        for poly in obj.data.polygons:
            poly.use_smooth = False

# Ground origin stays at world/local Z=0.
root.location = (0.0, 0.0, 0.0)

# Leave generated collection selected.
bpy.ops.object.select_all(action='DESELECT')
for obj in COLL.objects:
    obj.hide_set(False)
    obj.select_set(True)
bpy.context.view_layer.objects.active = root


def export_fbx():
    if bpy.data.filepath:
        out_dir = os.path.dirname(bpy.data.filepath)
    else:
        out_dir = os.path.expanduser("~/Desktop")

    out_path = os.path.join(out_dir, EXPORT_FILENAME)

    bpy.ops.export_scene.fbx(
        filepath=out_path,
        check_existing=False,
        use_selection=True,
        object_types={'EMPTY', 'MESH'},
        global_scale=1.0,
        apply_unit_scale=True,
        use_space_transform=True,
        bake_space_transform=False,
        axis_forward='-Z',
        axis_up='Y',
        use_mesh_modifiers=True,
        mesh_smooth_type='FACE',
        use_triangles=True,
        bake_anim=False,
        path_mode='AUTO',
        embed_textures=False,
        use_custom_props=True,
    )

    print("[FOTF] Exported:", out_path)
    return out_path


if AUTO_EXPORT_FBX:
    export_fbx()

print("======================================================")
print("FOTF BLIGHTED SPORE PLANT COMPLETE")
print("Collection:", COLLECTION_NAME)
print("Root:", ROOT_NAME)
print("Harvest visual child: HarvestableSpores")
print("Quest item: Blighted Spores [1700]")
print("Static prop: YES")
print("External assets required: NO")
print("Recommended Unity scale: 1,1,1")
print("======================================================")
