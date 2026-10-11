import bpy
import os
import math
import bmesh
from mathutils import Vector, Matrix

# ============================================================
# Fate of the Fallen - Carapace of the Event Horizon V13
# NATIVE-REFERENCE COHESIVE CUIRASS / SYNTY CHEST-SLOT RIG PASS
#
# DESIGN GOAL:
#   Rebuild the Carapace around the construction language of Erenshor's native
#   Synty chest armour: one cohesive cuirass, continuous upper yoke, broad
#   breast band, central keel, finished waist and compact layered pauldrons.
#
# Slot discipline:
#   - torso/bodypiece + shoulders only
#   - NO upper-arm, forearm, wrist or hand armour
#
# Keeps the Event Horizon identity through the black/gunmetal shell, deep void
# purple insets and small magenta/cyan crystal accents rather than through a
# collection of disconnected plates.
#
# Adds:
#   - Unity-safe face pass (outward normals + triangulation) for chest/shoulder plates
#   - native Synty armature import/clone
#   - torso pieces rigidly weighted to native torso bones
#   - pauldrons rigidly weighted to native Shoulder_L / Shoulder_R
#   - native bind-pose fitting using the real Synty shoulder/torso dimensions
#   - V16: compresses ONLY the complete model front-to-back; X/Z unchanged
#   - source Synty body is NEVER exported
# ============================================================

COLLECTION_NAME = "FOTF_Carapace_Event_Horizon_V13_ExactSyntyBodyFit"
EXPORT_FILE_NAME = "FOTF_Carapace_Event_Horizon.fbx"
EXPORT_RIG_NAME = "FOTF_Carapace_Event_Horizon_Rig"
SOURCE_FBX_OVERRIDE = r""
AUTO_EXPORT = True
SHOW_EXPORT_RIG = True

# Exact V5 local design dimensions.
TORSO_BOTTOM = -0.58
TORSO_TOP = 0.58
WAIST_HALF_WIDTH = 0.25
CHEST_HALF_WIDTH = 0.45
WAIST_HALF_DEPTH = 0.145
CHEST_HALF_DEPTH = 0.17
SHOULDER_HALF_WIDTH = 0.47

REQUIRED_BONES = [
    "Root", "Hips", "Spine_01", "Spine_02", "Spine_03", "Neck", "Head",
    "Clavicle_L", "Shoulder_L", "Elbow_L", "Hand_L",
    "Clavicle_R", "Shoulder_R", "Elbow_R", "Hand_R",
]

# ------------------------------------------------------------------
# Source Synty rig discovery
# ------------------------------------------------------------------

def remove_collection(name):
    coll = bpy.data.collections.get(name)
    if coll:
        for obj in list(coll.objects):
            bpy.data.objects.remove(obj, do_unlink=True)
        bpy.data.collections.remove(coll)


def _walk_for_file(root, filename, max_depth=6):
    if not root or not os.path.isdir(root):
        return None
    root = os.path.abspath(root)
    base_depth = root.rstrip(os.sep).count(os.sep)
    try:
        for current, dirs, files in os.walk(root):
            depth = current.rstrip(os.sep).count(os.sep) - base_depth
            if depth >= max_depth:
                dirs[:] = []
            dirs[:] = [d for d in dirs if d.lower() not in {
                '.git', 'library', 'temp', 'obj', 'bin', 'node_modules'
            }]
            for f in files:
                if f.lower() == filename.lower():
                    return os.path.join(current, f)
    except Exception:
        pass
    return None


def resolve_source_fbx():
    candidates = []
    if SOURCE_FBX_OVERRIDE:
        candidates.append(os.path.abspath(os.path.expanduser(SOURCE_FBX_OVERRIDE)))

    blend_dir = os.path.dirname(bpy.data.filepath) if bpy.data.filepath else ""
    cwd = os.getcwd()
    home = os.path.expanduser("~")
    candidates += [
        r"C:\Users\Arcane\source\repos\FateOfTheFallen\Erenshor NPCs\SourceFiles\FBX\Characters.fbx",
        os.path.join(blend_dir, "Characters.fbx") if blend_dir else "",
        os.path.join(blend_dir, "Erenshor NPCs", "SourceFiles", "FBX", "Characters.fbx") if blend_dir else "",
        os.path.join(cwd, "Characters.fbx"),
        os.path.join(cwd, "SourceFiles", "FBX", "Characters.fbx"),
        os.path.join(home, "source", "repos", "FateOfTheFallen", "Erenshor NPCs", "SourceFiles", "FBX", "Characters.fbx"),
    ]
    for path in candidates:
        if path and os.path.isfile(path):
            return os.path.normpath(path)
    for root in (os.path.join(home, "source", "repos"), os.path.join(home, "Downloads")):
        found = _walk_for_file(root, "Characters.fbx")
        if found:
            return found
    raise FileNotFoundError(
        "Could not find Erenshor Characters.fbx. Set SOURCE_FBX_OVERRIDE or place Characters.fbx beside the .blend."
    )


def is_compatible_armature(obj):
    return obj.type == 'ARMATURE' and all(obj.data.bones.get(n) is not None for n in REQUIRED_BONES)


def find_body(objects):
    # Exact native Erenshor/Synty body first.
    for obj in objects:
        if obj.type == 'MESH' and obj.name.startswith("Character_Male_Face_01"):
            return obj

    # Fallback: largest skinned mesh carrying torso/hand groups.
    candidates = []
    for obj in objects:
        if obj.type != 'MESH':
            continue
        names = {g.name for g in obj.vertex_groups}
        if "Spine_02" in names and "Spine_03" in names and "Hand_L" in names and "Hand_R" in names:
            candidates.append(obj)
    if candidates:
        candidates.sort(key=lambda o: len(o.data.polygons), reverse=True)
        return candidates[0]
    return None


def acquire_source():
    scene_objects = list(bpy.context.scene.objects)
    rig = next((o for o in scene_objects if is_compatible_armature(o)), None)
    body = find_body(scene_objects)
    if rig is not None and body is not None:
        print("[Carapace V17.1] Using Synty rig/body already in scene.")
        return rig, body, []

    path = resolve_source_fbx()
    print("[Carapace V17.1] Importing Synty reference:", path)
    before = set(bpy.data.objects.keys())
    bpy.ops.import_scene.fbx(filepath=path)
    imported = [bpy.data.objects[n] for n in bpy.data.objects.keys() if n not in before]
    rig = next((o for o in imported if is_compatible_armature(o)), None)
    body = find_body(imported)
    if rig is None or body is None:
        raise RuntimeError("Characters.fbx imported but compatible Synty rig/body were not found.")
    return rig, body, imported


def clone_export_rig(source_rig, coll):
    # Create a CLEAN armature object from copied armature data. Do not Object.copy()
    # the source rig, because copied object relationships can pull the reference
    # mannequin into the exported FBX in some Blender/FBX versions.
    rig_data = source_rig.data.copy()
    rig_data.name = EXPORT_RIG_NAME + "_Armature"
    rig = bpy.data.objects.new(EXPORT_RIG_NAME, rig_data)
    coll.objects.link(rig)
    rig.matrix_world = source_rig.matrix_world.copy()
    rig.show_in_front = True
    try:
        rig.data.display_type = 'STICK'
    except Exception:
        pass
    rig.hide_viewport = not SHOW_EXPORT_RIG
    return rig

def safe_normal(v, fallback):
    out = v.copy()
    if out.length < 1e-8:
        return fallback.copy()
    out.normalize()
    return out


def bone_head(rig, name):
    b = rig.data.bones.get(name)
    if b is None:
        raise RuntimeError("Missing Synty bone: " + name)
    return b.head_local.copy()


def bone_tail(rig, name):
    b = rig.data.bones.get(name)
    if b is None:
        raise RuntimeError("Missing Synty bone: " + name)
    return b.tail_local.copy()


def character_basis(rig):
    hips = bone_head(rig, "Hips")
    neck = bone_head(rig, "Neck")
    up = safe_normal(neck - hips, Vector((0, 0, 1)))

    # Prefer the arm span for the character's left/right basis.
    sl = bone_head(rig, "Shoulder_L")
    sr = bone_head(rig, "Shoulder_R")
    right = safe_normal(sr - sl, Vector((1, 0, 0)))

    # Forward must be perpendicular to both; choose the sign using the chest
    # bone roll when possible.  The sign only affects which side is front.
    fwd = safe_normal(up.cross(right), Vector((0, -1, 0)))
    spine = rig.data.bones.get("Spine_03")
    if spine is not None:
        zaxis = spine.matrix_local.to_3x3().col[2].normalized()
        if fwd.dot(zaxis) < 0:
            fwd = -fwd
    right = safe_normal(fwd.cross(up), right)
    return right, fwd, up

# ------------------------------------------------------------------
# Collection / materials - EXACT V5 palette
# ------------------------------------------------------------------

remove_collection(COLLECTION_NAME)
COLL = bpy.data.collections.new(COLLECTION_NAME)
bpy.context.scene.collection.children.link(COLL)

source_rig, source_body, imported_source = acquire_source()
EXPORT_RIG = clone_export_rig(source_rig, COLL)


def material(name, base, metallic=0.0, rough=0.5, emission=None, strength=0.0):
    m = bpy.data.materials.get(name)
    if m is None:
        m = bpy.data.materials.new(name)
    m.use_nodes = True
    m.diffuse_color = base
    bsdf = m.node_tree.nodes.get("Principled BSDF")
    if bsdf:
        bsdf.inputs["Base Color"].default_value = base
        bsdf.inputs["Metallic"].default_value = metallic
        bsdf.inputs["Roughness"].default_value = rough
        if emission is not None:
            if "Emission Color" in bsdf.inputs:
                bsdf.inputs["Emission Color"].default_value = emission
                bsdf.inputs["Emission Strength"].default_value = strength
            elif "Emission" in bsdf.inputs:
                bsdf.inputs["Emission"].default_value = emission
    return m

MAT_BASE = material("CEH5_VoidBlack", (0.025, 0.025, 0.035, 1.0), 0.32, 0.46)
MAT_PLATE = material("CEH5_Gunmetal", (0.11, 0.105, 0.13, 1.0), 0.78, 0.28)
MAT_EDGE = material("CEH5_EdgeMetal", (0.20, 0.19, 0.23, 1.0), 0.86, 0.22)
MAT_UNDER = material("CEH5_Underlayer", (0.055, 0.025, 0.070, 1.0), 0.0, 0.82)
MAT_PURPLE = material("CEH5_VoidPurple", (0.18, 0.025, 0.28, 1.0), 0.0, 0.20,
                      (0.74, 0.05, 1.0, 1.0), 3.4)
MAT_CYAN = material("CEH5_VoidCyan", (0.025, 0.24, 0.34, 1.0), 0.0, 0.20,
                    (0.00, 0.90, 1.0, 1.0), 2.8)
MAT_DEEP_PURPLE = material("CEH11_DeepVoidPurple", (0.105, 0.025, 0.165, 1.0), 0.28, 0.34)
MAT_PURPLE_METAL = material("CEH11_VoidPurpleMetal", (0.16, 0.055, 0.22, 1.0), 0.62, 0.28)

CREATED = []


def unity_safe_mesh_pass(obj):
    """Make generated armour geometry Unity-safe by recalculating outward normals,
    triangulating n-gons/quads deterministically, and validating the mesh.
    This fixes front plates rendering oddly due to winding/normal issues after
    the V5 local-space geometry is fitted into the Synty bind pose."""
    if obj is None or obj.type != 'MESH' or obj.data is None:
        return obj
    bm = bmesh.new()
    try:
        bm.from_mesh(obj.data)
        bm.verts.ensure_lookup_table()
        bm.edges.ensure_lookup_table()
        bm.faces.ensure_lookup_table()
        if len(bm.verts) > 0:
            bmesh.ops.remove_doubles(bm, verts=list(bm.verts), dist=1e-6)
        if len(bm.faces) > 0:
            bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
            bmesh.ops.triangulate(bm, faces=list(bm.faces), quad_method='BEAUTY', ngon_method='BEAUTY')
        bm.to_mesh(obj.data)
        obj.data.validate(clean_customdata=False)
        obj.data.update(calc_edges=True, calc_edges_loose=True)
    finally:
        bm.free()
    return obj


def extruded_xz(name, points, y_center, thickness, mat, bevel=0.0):
    front_y = y_center - thickness * 0.5
    back_y = y_center + thickness * 0.5
    n = len(points)
    verts = [(x, front_y, z) for x, z in points] + [(x, back_y, z) for x, z in points]
    faces = [tuple(range(n - 1, -1, -1)), tuple(range(n, n * 2))]
    for i in range(n):
        j = (i + 1) % n
        faces.append((i, j, n + j, n + i))
    mesh = bpy.data.meshes.new(name + "_Mesh")
    mesh.from_pydata(verts, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    COLL.objects.link(obj)
    obj.data.materials.append(mat)
    for p in obj.data.polygons:
        p.use_smooth = False
    if bevel > 0:
        mod = obj.modifiers.new("EdgeBevel", 'BEVEL')
        mod.width = bevel
        mod.segments = 1
        mod.affect = 'EDGES'
    CREATED.append(obj)
    return obj


def mirror_points(points):
    return [(-x, z) for x, z in points]

# ------------------------------------------------------------------
# V11 NATIVE-REFERENCE COHESIVE CUIRASS
#
# Construction reference: the native chestplate supplied in-game.
# Instead of many small plates stitched together, V11 is built from a few
# large overlapping masses that read as one garment:
#   1) continuous torso shell
#   2) upper shoulder-to-shoulder yoke
#   3) broad breast band
#   4) central keel / abdomen guard
#   5) clean lower waist rim
#   6) compact layered pauldrons that overlap the yoke
# ------------------------------------------------------------------

# MAIN CUIRASS SHELL - one continuous body mass, with generous depth so it
# visibly wraps around the Synty torso from the side.
cuirass_shell = [
    (-0.315, 0.555), (-0.185, 0.625), (0.0, 0.645), (0.185, 0.625), (0.315, 0.555),
    (0.385, 0.405), (0.385, 0.120), (0.345, -0.185), (0.285, -0.430),
    (0.205, -0.545), (0.0, -0.585), (-0.205, -0.545), (-0.285, -0.430),
    (-0.345, -0.185), (-0.385, 0.120), (-0.385, 0.405)
]
extruded_xz("CuirassShell", cuirass_shell, 0.000, 0.315, MAT_BASE, 0.015)

# BACK PANEL - a single deliberate rear plate, not a collection of strips.
back_panel = [
    (-0.305, 0.505), (0.305, 0.505), (0.342, 0.180), (0.300, -0.300),
    (0.190, -0.485), (0.0, -0.535), (-0.190, -0.485), (-0.300, -0.300), (-0.342, 0.180)
]
extruded_xz("BackPlate", back_panel, 0.176, 0.045, MAT_PLATE, 0.009)

# UPPER YOKE - this is the visual equivalent of the native white shoulder/chest
# yoke. It is one continuous plate stretching from shoulder root to shoulder
# root, so the pauldrons visually grow out of the chest instead of floating.
upper_yoke = [
    (-0.455, 0.475), (-0.340, 0.565), (-0.180, 0.610), (-0.075, 0.545),
    (0.0, 0.505), (0.075, 0.545), (0.180, 0.610), (0.340, 0.565), (0.455, 0.475),
    (0.420, 0.320), (0.300, 0.270), (0.170, 0.285), (0.0, 0.355),
    (-0.170, 0.285), (-0.300, 0.270), (-0.420, 0.320)
]
extruded_xz("UpperYoke", upper_yoke, -0.178, 0.040, MAT_PLATE, 0.009)

# VOID BREAST BAND - native reference has a broad colour block beneath the
# yoke. Ours is a single deep-purple inset rather than two separate pectorals.
breast_band = [
    (-0.335, 0.305), (-0.190, 0.335), (0.0, 0.300), (0.190, 0.335), (0.335, 0.305),
    (0.320, 0.150), (0.205, 0.105), (0.0, 0.125), (-0.205, 0.105), (-0.320, 0.150)
]
extruded_xz("VoidBreastBand", breast_band, -0.205, 0.030, MAT_DEEP_PURPLE, 0.007)

# CENTRAL KEEL - one long shield-like plate inspired directly by the native
# chest's large white centre plate. This replaces the previous collection of
# sternum/V/lower plates and makes the front read as one intentional cuirass.
central_keel = [
    (-0.115, 0.405), (0.0, 0.465), (0.115, 0.405),
    (0.150, 0.165), (0.125, -0.125), (0.075, -0.355),
    (0.0, -0.485), (-0.075, -0.355), (-0.125, -0.125), (-0.150, 0.165)
]
extruded_xz("CentralKeel", central_keel, -0.232, 0.042, MAT_PLATE, 0.009)

# Thin edge inset down the keel gives Event Horizon definition without making
# the armour look assembled from unrelated pieces.
keel_inset = [
    (-0.045, 0.350), (0.0, 0.385), (0.045, 0.350),
    (0.055, -0.080), (0.028, -0.305), (0.0, -0.390),
    (-0.028, -0.305), (-0.055, -0.080)
]
extruded_xz("KeelInset", keel_inset, -0.258, 0.018, MAT_EDGE, 0.004)

# SIDE ABDOMEN PANELS - broad continuous panels like the native purple side
# sections. They overlap both the shell and central keel, so there are no open
# seams between front components.
left_abdomen = [
    (-0.315, 0.130), (-0.155, 0.115), (-0.135, -0.335), (-0.205, -0.455),
    (-0.292, -0.405), (-0.335, -0.135)
]
extruded_xz("AbdomenPanel_L", left_abdomen, -0.198, 0.028, MAT_PURPLE_METAL, 0.006)
extruded_xz("AbdomenPanel_R", mirror_points(left_abdomen), -0.198, 0.028, MAT_PURPLE_METAL, 0.006)

# LOWER WAIST RIM - clean finished edge similar to the native armour's white
# hem. One continuous band visually terminates the chest slot above trousers.
waist_rim = [
    (-0.285, -0.400), (-0.205, -0.485), (0.0, -0.525), (0.205, -0.485), (0.285, -0.400),
    (0.260, -0.335), (0.175, -0.405), (0.0, -0.445), (-0.175, -0.405), (-0.260, -0.335)
]
extruded_xz("WaistRim", waist_rim, -0.220, 0.028, MAT_EDGE, 0.005)

# Small Event Horizon emblem, kept subordinate to the overall cuirass.
emblem_back = [(-0.082, 0.275), (0.0, 0.350), (0.082, 0.275), (0.0, 0.205)]
extruded_xz("Emblem_Backplate", emblem_back, -0.278, 0.016, MAT_BASE, 0.003)
for name, x, z, sx, sz, mat in [
    ("Emblem_Top", 0.00, 0.315, 0.032, 0.027, MAT_PURPLE),
    ("Emblem_Left", -0.043, 0.268, 0.027, 0.024, MAT_PURPLE),
    ("Emblem_Right", 0.043, 0.268, 0.027, 0.024, MAT_PURPLE),
    ("Emblem_Bottom", 0.00, 0.220, 0.032, 0.027, MAT_PURPLE),
]:
    pts = [(-sx, 0.0), (0.0, sz), (sx, 0.0), (0.0, -sz)]
    pts = [(px + x, pz + z) for px, pz in pts]
    extruded_xz(name, pts, -0.291, 0.009, mat, 0.001)
core = [(-0.010, 0.0), (0.0, 0.014), (0.010, 0.0), (0.0, -0.014)]
core = [(x, z + 0.268) for x, z in core]
extruded_xz("Emblem_Cyan_Core", core, -0.298, 0.006, MAT_CYAN, 0.001)

# ------------------------------------------------------------------
# NATIVE-STYLE PAULDRONS
#
# Based on the supplied native reference:
# - an inner bridge/tongue attached to the chest/yoke
# - a raised trapezoidal main top plate
# - a darker coloured underlayer visible beneath it
# - a short outer drop, but no upper-arm armour
# This creates a readable shoulder pad while staying visually connected to the
# torso and respecting Erenshor's separate arm-equipment regions.
# ------------------------------------------------------------------

def polyhedron(name, verts, faces, mat, bevel=0.0):
    mesh = bpy.data.meshes.new(name + "_Mesh")
    mesh.from_pydata(verts, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    COLL.objects.link(obj)
    obj.data.materials.append(mat)
    for p in obj.data.polygons:
        p.use_smooth = False
    if bevel > 0:
        mod = obj.modifiers.new("EdgeBevel", 'BEVEL')
        mod.width = bevel
        mod.segments = 1
        mod.affect = 'EDGES'
    CREATED.append(obj)
    return obj


def mirror_polyhedron_x(name, verts, faces, mat, bevel=0.0):
    mirrored = [(-x, y, z) for (x, y, z) in verts]
    mirrored_faces = [tuple(reversed(f)) for f in faces]
    return polyhedron(name, mirrored, mirrored_faces, mat, bevel)

# Inner bridge: wider and deeper so the pauldron overlaps the cuirass and
# covers the actual shoulder width instead of reading as a small cap perched
# on top of the torso.
left_bridge = [
    (-0.310, 0.505), (-0.445, 0.555), (-0.550, 0.530),
    (-0.545, 0.430), (-0.415, 0.385), (-0.300, 0.425)
]
extruded_xz("Pauldron_L_Bridge", left_bridge, -0.206, 0.080, MAT_EDGE, 0.006)
extruded_xz("Pauldron_R_Bridge", mirror_points(left_bridge), -0.206, 0.080, MAT_EDGE, 0.006)

# Purple underlayer - broadened so the shoulder shell matches the body width
# from the side and properly covers the top of the shoulder.
left_under_verts = [
    (-0.395, -0.240, 0.540), (-0.520, -0.290, 0.620), (-0.675, -0.280, 0.600),
    (-0.760, -0.210, 0.500), (-0.720, -0.120, 0.380), (-0.535, -0.115, 0.360), (-0.390, -0.145, 0.450),
    (-0.400,  0.070, 0.530), (-0.520,  0.100, 0.600), (-0.665,  0.095, 0.585),
    (-0.740,  0.075, 0.500), (-0.705,  0.055, 0.395), (-0.535,  0.050, 0.370), (-0.395,  0.040, 0.445),
]
left_under_faces = [
    (0,1,2,3,4,5,6), (13,12,11,10,9,8,7),
    (0,7,8,1), (1,8,9,2), (2,9,10,3), (3,10,11,4), (4,11,12,5), (5,12,13,6), (6,13,7,0)
]
polyhedron("Pauldron_L_Under", left_under_verts, left_under_faces, MAT_PURPLE_METAL, 0.008)
mirror_polyhedron_x("Pauldron_R_Under", left_under_verts, left_under_faces, MAT_PURPLE_METAL, 0.008)

# Main shell - thicker and wider so it reads like a true pauldron wrapping the
# shoulder rather than a thin stitched-on plate.
left_main_verts = [
    (-0.390, -0.275, 0.565), (-0.505, -0.330, 0.655), (-0.670, -0.320, 0.635),
    (-0.760, -0.255, 0.535), (-0.720, -0.185, 0.430), (-0.525, -0.180, 0.405), (-0.390, -0.205, 0.485),
    (-0.400, -0.105, 0.555), (-0.510, -0.105, 0.630), (-0.660, -0.100, 0.615),
    (-0.735, -0.090, 0.535), (-0.700, -0.080, 0.450), (-0.525, -0.075, 0.425), (-0.400, -0.080, 0.490),
]
left_main_faces = [
    (0,1,2,3,4,5,6), (13,12,11,10,9,8,7),
    (0,7,8,1), (1,8,9,2), (2,9,10,3), (3,10,11,4), (4,11,12,5), (5,12,13,6), (6,13,7,0)
]
polyhedron("Pauldron_L_Main", left_main_verts, left_main_faces, MAT_PLATE, 0.010)
mirror_polyhedron_x("Pauldron_R_Main", left_main_verts, left_main_faces, MAT_PLATE, 0.010)

# Outer drop plate for depth; kept compact enough not to become upper-arm gear.
left_drop_verts = [
    (-0.645, -0.245, 0.560), (-0.755, -0.220, 0.515), (-0.795, -0.150, 0.430),
    (-0.760, -0.095, 0.360), (-0.685, -0.115, 0.390),
    (-0.645,  0.035, 0.550), (-0.745,  0.045, 0.505), (-0.780,  0.040, 0.430),
    (-0.745,  0.030, 0.375), (-0.680,  0.028, 0.400),
]
left_drop_faces = [
    (0,1,2,3,4), (9,8,7,6,5),
    (0,5,6,1), (1,6,7,2), (2,7,8,3), (3,8,9,4), (4,9,5,0)
]
polyhedron("Pauldron_L_OuterDrop", left_drop_verts, left_drop_faces, MAT_BASE, 0.006)
mirror_polyhedron_x("Pauldron_R_OuterDrop", left_drop_verts, left_drop_faces, MAT_BASE, 0.006)

# Small inset crystal only - moved slightly outward to remain centered on the
# broader shell.
left_gem = [
    (-0.545, 0.582), (-0.568, 0.608), (-0.600, 0.588), (-0.592, 0.550), (-0.560, 0.543)
]
extruded_xz("ShoulderGem_L", left_gem, -0.305, 0.014, MAT_PURPLE, 0.002)
extruded_xz("ShoulderGem_R", mirror_points(left_gem), -0.305, 0.014, MAT_PURPLE, 0.002)

# NO upper-arm / forearm / wrist / hand geometry by design.

# ------------------------------------------------------------------
# Fit the native-reference cuirass to the ACTUAL Synty body surface.
# ------------------------------------------------------------------

def torso_surface_metrics(body, rig, right, fwd, up, hips, neck):
    """Measure the real Synty torso mesh instead of estimating from bones.

    We use vertices influenced by the spine groups and reject the extremities.
    The returned width/depth are therefore the actual body envelope the armour
    must sit over in Unity.
    """
    rig_inv = rig.matrix_world.inverted()
    torso_group_names = {"Spine_01", "Spine_02", "Spine_03", "Clavicle_L", "Clavicle_R"}
    group_name_by_index = {g.index: g.name for g in body.vertex_groups}

    torso_h = max((neck - hips).length, 1e-5)
    mid = (hips + neck) * 0.5
    samples = []

    for v in body.data.vertices:
        influence = 0.0
        for membership in v.groups:
            if group_name_by_index.get(membership.group) in torso_group_names:
                influence = max(influence, membership.weight)
        if influence < 0.12:
            continue

        p = rig_inv @ (body.matrix_world @ v.co)
        vertical = (p - hips).dot(up)
        # Keep the chest/abdomen region and avoid neck/head/pelvis spill.
        if vertical < torso_h * 0.06 or vertical > torso_h * 0.98:
            continue
        samples.append(p)

    if len(samples) < 32:
        # Conservative geometric fallback if this particular Synty body lacks
        # useful imported weights.
        for v in body.data.vertices:
            p = rig_inv @ (body.matrix_world @ v.co)
            vertical = (p - hips).dot(up)
            if torso_h * 0.08 <= vertical <= torso_h * 0.95:
                lateral = abs((p - mid).dot(right))
                if lateral <= torso_h * 0.55:
                    samples.append(p)

    if not samples:
        raise RuntimeError("Unable to measure native Synty torso surface.")

    rel = [p - mid for p in samples]
    xs = [d.dot(right) for d in rel]
    ys = [d.dot(fwd) for d in rel]
    zs = [d.dot(up) for d in rel]

    min_x, max_x = min(xs), max(xs)
    min_y, max_y = min(ys), max(ys)
    min_z, max_z = min(zs), max(zs)

    width = max_x - min_x
    depth = max_y - min_y
    surface_center = (mid +
                      right * ((min_x + max_x) * 0.5) +
                      fwd * ((min_y + max_y) * 0.5) +
                      up * ((min_z + max_z) * 0.5))

    print("[Carapace V17.1] Native torso samples=", len(samples),
          " width=", round(width, 4),
          " depth=", round(depth, 4),
          " height=", round(max_z - min_z, 4))
    return surface_center, max(width * 0.5, 1e-4), max(depth * 0.5, 1e-4), max(max_z - min_z, 1e-4)


right, fwd, up = character_basis(EXPORT_RIG)
hips = bone_head(EXPORT_RIG, "Hips")
neck = bone_head(EXPORT_RIG, "Neck")

torso_center, BODY_HALF_WIDTH, BODY_HALF_DEPTH, BODY_SURFACE_HEIGHT = torso_surface_metrics(
    source_body, source_rig, right, fwd, up, hips, neck
)

# Preserve the proven V14 width and height exactly.  Only the complete armour
# assembly's front/back depth is changed in this version.
X_SCALE = (BODY_HALF_WIDTH * 1.10) / CHEST_HALF_WIDTH
Z_SCALE = (BODY_SURFACE_HEIGHT * 1.03) / (TORSO_TOP - TORSO_BOTTOM)

# Measure the ACTUAL authored Y envelope, including chest plates, back plate,
# shoulder shells and gems. Earlier versions used CHEST_HALF_DEPTH as the Y
# denominator even though several authored front plates extend much farther
# forward than that value. That is why the complete cuirass looked extremely
# chunky from the side.
design_y_values = [v.co.y for obj in CREATED for v in obj.data.vertices]
if not design_y_values:
    raise RuntimeError("Carapace has no generated vertices for depth fitting.")
DESIGN_MIN_Y = min(design_y_values)
DESIGN_MAX_Y = max(design_y_values)
DESIGN_CENTER_Y = (DESIGN_MIN_Y + DESIGN_MAX_Y) * 0.5
DESIGN_HALF_DEPTH = max((DESIGN_MAX_Y - DESIGN_MIN_Y) * 0.5, 1e-5)

# Entire chest piece is only 3% deeper than the measured Synty torso surface.
# X and Z are deliberately untouched; this is FRONT/BACK compression only.
DEPTH_CLEARANCE = 1.03
Y_SCALE = (BODY_HALF_DEPTH * DEPTH_CLEARANCE) / DESIGN_HALF_DEPTH

print("[Carapace V17.1] authored depth=", round(DESIGN_HALF_DEPTH * 2.0, 4),
      " target depth=", round(BODY_HALF_DEPTH * 2.0 * DEPTH_CLEARANCE, 4),
      " Y scale=", round(Y_SCALE, 4))


def transform_design_point(p):
    # Local convention: X = left/right, Z = up, -Y = visual front.
    # Only Y is recentered/compressed. Shoulder width/height stay identical to V14.
    local_y = p.y - DESIGN_CENTER_Y
    return (torso_center +
            right * (p.x * X_SCALE) +
            (-fwd) * (local_y * Y_SCALE) +
            up * (p.z * Z_SCALE))


for obj in CREATED:
    for v in obj.data.vertices:
        v.co = transform_design_point(v.co)
    unity_safe_mesh_pass(obj)
    obj.matrix_world = EXPORT_RIG.matrix_world.copy()


# ------------------------------------------------------------------
# MODULAR BONE-LOCAL EXPORT
# ------------------------------------------------------------------
# The runtime no longer requires SkinnedMeshRenderer.  Instead this FBX exports
# exactly three ordinary mesh parts, each authored in the LOCAL SPACE of the
# native Synty bone it will be parented to at runtime:
#   FOTF_Carapace_Torso       -> Spine_03
#   FOTF_Carapace_Pauldron_L  -> Shoulder_L
#   FOTF_Carapace_Pauldron_R  -> Shoulder_R
# FateEquipmentVisuals parents each part once with local identity.  Unity's
# native skeleton then animates the shoulders automatically.

SHOW_EXPORT_RIG = False


def _apply_non_armature_modifiers(obj):
    if obj is None or obj.type != 'MESH':
        return
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    for mod in list(obj.modifiers):
        if mod.type == 'ARMATURE':
            obj.modifiers.remove(mod)
            continue
        try:
            bpy.ops.object.modifier_apply(modifier=mod.name)
        except Exception as exc:
            print('[Carapace Modular] modifier apply failed', obj.name, mod.name, exc)
    obj.select_set(False)


def _join_objects(objects, name):
    objs=[o for o in objects if o is not None and o.type=='MESH']
    if not objs:
        raise RuntimeError('No objects available for '+name)
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs:
        _apply_non_armature_modifiers(o)
        o.select_set(True)
    bpy.context.view_layer.objects.active=objs[0]
    bpy.ops.object.join()
    joined=bpy.context.view_layer.objects.active
    joined.name=name
    joined.data.name=name+'_Mesh'
    unity_safe_mesh_pass(joined)
    return joined


def _source_height_and_scale():
    rig_inv=source_rig.matrix_world.inverted()
    values=[]
    for v in source_body.data.vertices:
        p=rig_inv @ (source_body.matrix_world @ v.co)
        values.append(p.dot(up))
    if not values:
        return 1.0, 0.0
    height=max(values)-min(values)
    # Erenshor's humanoid is approximately 1.8 Unity units tall.  Baking this
    # conversion into the mesh avoids the old x100 body-asset ambiguity.
    scale=1.8/max(height,1e-6)
    return scale,height


UNITY_UNIT_SCALE, SOURCE_BODY_HEIGHT=_source_height_and_scale()
print('[Carapace Modular] source body height=',round(SOURCE_BODY_HEIGHT,4),
      ' baked Unity scale=',round(UNITY_UNIT_SCALE,6))

# Bridges stay with the torso.  Only the actual shell/drop/gem follows the
# animated shoulder bone.
left_parts=[]
right_parts=[]
torso_parts=[]
for obj in list(CREATED):
    n=obj.name
    if ((n.startswith('Pauldron_L_') and not n.endswith('_Bridge')) or
        n in {'ShoulderGem_L','ShoulderCyan_L'}):
        left_parts.append(obj)
    elif ((n.startswith('Pauldron_R_') and not n.endswith('_Bridge')) or
          n in {'ShoulderGem_R','ShoulderCyan_R'}):
        right_parts.append(obj)
    else:
        torso_parts.append(obj)

TORSO_PART=_join_objects(torso_parts,'FOTF_Carapace_Torso')
LEFT_PART=_join_objects(left_parts,'FOTF_Carapace_Pauldron_L')
RIGHT_PART=_join_objects(right_parts,'FOTF_Carapace_Pauldron_R')
MODULAR_PARTS=[TORSO_PART,LEFT_PART,RIGHT_PART]


def _bake_to_bone_local(obj,bone_name):
    bone=EXPORT_RIG.data.bones.get(bone_name)
    if bone is None:
        raise RuntimeError('Missing Synty bone '+bone_name)
    inv=bone.matrix_local.inverted()
    # Generated vertices are in armature-local bind-pose coordinates.
    for v in obj.data.vertices:
        v.co=(inv @ v.co) * UNITY_UNIT_SCALE
    obj.data.validate(clean_customdata=False)
    obj.data.update(calc_edges=True,calc_edges_loose=True)
    obj.matrix_world=Matrix.Identity(4)
    obj.location=(0.0,0.0,0.0)
    obj.rotation_mode='XYZ'
    obj.rotation_euler=(0.0,0.0,0.0)
    obj.scale=(1.0,1.0,1.0)
    obj['FOTF_TargetBone']=bone_name


_bake_to_bone_local(TORSO_PART,'Spine_03')
_bake_to_bone_local(LEFT_PART,'Shoulder_L')
_bake_to_bone_local(RIGHT_PART,'Shoulder_R')

# Plain Empty root only; no source armature/mannequin is exported.
root_obj=bpy.data.objects.new('FOTF_Carapace_ModularRoot',None)
COLL.objects.link(root_obj)
for obj in MODULAR_PARTS:
    obj.parent=root_obj
    obj.matrix_parent_inverse=Matrix.Identity(4)

# Hide imported references so the Blender result is easy to inspect.
for obj in imported_source:
    try:
        obj.hide_viewport=True
        obj.hide_render=True
    except Exception:
        pass
try:
    source_rig.hide_viewport=True
    source_body.hide_viewport=True
    EXPORT_RIG.hide_viewport=True
except Exception:
    pass


def export_fbx():
    bpy.ops.object.select_all(action='DESELECT')
    root_obj.select_set(True)
    for obj in MODULAR_PARTS:
        obj.select_set(True)
    bpy.context.view_layer.objects.active=root_obj

    base=os.path.dirname(os.path.abspath(bpy.data.filepath)) if bpy.data.filepath else os.path.expanduser('~')
    outdir=os.path.join(base,'FOTF_Exports')
    os.makedirs(outdir,exist_ok=True)
    path=os.path.join(outdir,EXPORT_FILE_NAME)

    bpy.ops.export_scene.fbx(
        filepath=path,
        check_existing=False,
        use_selection=True,
        object_types={'EMPTY','MESH'},
        global_scale=1.0,
        apply_unit_scale=False,
        use_space_transform=True,
        bake_space_transform=False,
        axis_forward='-Z',
        axis_up='Y',
        use_mesh_modifiers=True,
        mesh_smooth_type='FACE',
        use_triangles=True,
        add_leaf_bones=False,
        bake_anim=False,
    )
    print('[Carapace Modular] Exported:',path)
    print('[Carapace Modular] Parts:',[(o.name,o.get('FOTF_TargetBone')) for o in MODULAR_PARTS])


if AUTO_EXPORT:
    export_fbx()
