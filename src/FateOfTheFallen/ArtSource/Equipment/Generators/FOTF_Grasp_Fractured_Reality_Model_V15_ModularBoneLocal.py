import bpy
import bmesh
import os
from mathutils import Vector, Matrix

# ============================================================
# Fate of the Fallen - Grasp of Fractured Reality V12.3
# CLEAN EXPORT + EXACT SURFACE FIT
#
# Core change:
#   Do NOT invent glove/finger geometry from bone positions.
#   Instead, extract the actual hand/finger surface from the native
#   Character_Male_Face_01 Synty mesh, preserve its native bone weights,
#   inflate it slightly into an armour shell, and add FOTF dorsal plating.
#
# Result:
#   - exact native Synty hand/finger silhouette
#   - exact native finger flexing/animation
#   - HAND ONLY: no cuff, wrist, forearm, vambrace or elbow geometry
# ============================================================

COLLECTION_NAME = "FOTF_Grasp_Fractured_Reality_V12_CleanExportSurfaceFit"
EXPORT_FILE_NAME = "FOTF_Grasp_Fractured_Reality.fbx"
EXPORT_RIG_NAME = "FOTF_Grasp_Fractured_Reality_Rig"
SOURCE_FBX_OVERRIDE = r""
AUTO_EXPORT = True
SHOW_EXPORT_RIG = False
SHOW_SOURCE_GUIDE = False

# Shell tuning. This is relative to the native hand bone length so it scales
# with the imported Synty character instead of assuming arbitrary metres.
HAND_REGION_MIN_WEIGHT = 0.32
WRIST_CUTBACK = 0.02       # fraction of hand-bone length behind the wrist
SHELL_INFLATE = 0.045      # fraction of hand-bone length
DORSAL_LIFT = 0.060        # fraction of hand-bone length

REQUIRED_BONES = [
    "Root", "Hips", "Spine_01", "Spine_02", "Spine_03", "Neck", "Head",
    "Clavicle_L", "Shoulder_L", "Elbow_L", "Hand_L",
    "Clavicle_R", "Shoulder_R", "Elbow_R", "Hand_R",
]

# ------------------------------------------------------------
# Source discovery
# ------------------------------------------------------------

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
    for p in candidates:
        if p and os.path.isfile(p):
            return os.path.normpath(p)
    for root in (os.path.join(home, "source", "repos"), os.path.join(home, "Downloads")):
        found = _walk_for_file(root, "Characters.fbx")
        if found:
            return found
    raise FileNotFoundError("Could not find Characters.fbx. Set SOURCE_FBX_OVERRIDE or place it beside the .blend.")


def is_compatible_armature(obj):
    return obj.type == 'ARMATURE' and all(obj.data.bones.get(n) is not None for n in REQUIRED_BONES)


def find_body(objects):
    # Prefer the exact Erenshor/Synty body name.
    for obj in objects:
        if obj.type == 'MESH' and obj.name.startswith("Character_Male_Face_01"):
            return obj

    # Fallback: choose the largest mesh that carries both native hand groups.
    candidates = []
    for obj in objects:
        if obj.type != 'MESH':
            continue
        names = {g.name for g in obj.vertex_groups}
        if "Hand_L" in names and "Hand_R" in names:
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
        print("[Grasp V11] Using Synty source already in the scene.")
        return rig, body, []

    path = resolve_source_fbx()
    print("[Grasp V11] Importing:", path)
    before = set(bpy.data.objects.keys())
    bpy.ops.import_scene.fbx(filepath=path)
    imported = [bpy.data.objects[n] for n in bpy.data.objects.keys() if n not in before]
    rig = next((o for o in imported if is_compatible_armature(o)), None)
    body = find_body(imported)
    if rig is None or body is None:
        raise RuntimeError("Characters.fbx imported but compatible Synty rig/body were not found.")
    return rig, body, imported


def clone_export_rig(source_rig, coll):
    rig = source_rig.copy()
    rig.data = source_rig.data.copy()
    rig.name = EXPORT_RIG_NAME
    rig.data.name = EXPORT_RIG_NAME + "_Armature"
    coll.objects.link(rig)
    rig.matrix_world = source_rig.matrix_world.copy()
    if rig.animation_data:
        rig.animation_data_clear()
    rig.show_in_front = True
    try:
        rig.data.display_type = 'STICK'
    except Exception:
        pass
    rig.hide_viewport = not SHOW_EXPORT_RIG
    return rig


def safe_normal(v, fallback):
    out = Vector(v)
    if out.length < 1e-8:
        return Vector(fallback)
    out.normalize()
    return out


def clean_children(bone):
    ignored = ("ik", "weapon", "prop", "socket", "helper", "twist", "end", "nub", "attach")
    return [c for c in bone.children if not any(t in c.name.lower() for t in ignored)]


def descendants(parent, max_depth=5):
    out = []
    q = [(c, 1) for c in clean_children(parent)]
    while q:
        b, d = q.pop(0)
        if d > max_depth:
            continue
        out.append((b, d))
        for c in clean_children(b):
            q.append((c, d + 1))
    return out


def first_named_root(hand, include, exclude=()):
    candidates = clean_children(hand) + [b for b, d in descendants(hand, 2) if d <= 2]
    seen = set()
    for b in candidates:
        if b.name in seen:
            continue
        seen.add(b.name)
        n = b.name.lower()
        if all(x in n for x in include) and not any(x in n for x in exclude):
            return b
    return None


def follow_chain(root, max_count):
    chain = [root]
    cur = root
    while len(chain) < max_count:
        kids = clean_children(cur)
        if not kids:
            break
        kids.sort(key=lambda b: b.length, reverse=True)
        cur = kids[0]
        chain.append(cur)
    return chain


def resolve_digit_groups(rig, hand_name):
    hand = rig.data.bones.get(hand_name)
    if hand is None:
        raise RuntimeError("Missing hand bone " + hand_name)

    thumb_root = first_named_root(hand, ("thumb",))
    index_root = first_named_root(hand, ("indexfinger",))
    finger_root = first_named_root(hand, ("finger",), ("index", "thumb"))

    if thumb_root is None or index_root is None or finger_root is None:
        names = [b.name for b, _ in descendants(hand, 4)]
        raise RuntimeError(
            "Could not resolve Synty digit groups under %s. Descendants: %s" %
            (hand_name, ", ".join(names))
        )

    return hand, follow_chain(thumb_root, 3), follow_chain(index_root, 4), follow_chain(finger_root, 4)

# ------------------------------------------------------------
# Scene / materials
# ------------------------------------------------------------

remove_collection(COLLECTION_NAME)
COLL = bpy.data.collections.new(COLLECTION_NAME)
bpy.context.scene.collection.children.link(COLL)

SOURCE_RIG, SOURCE_BODY, IMPORTED_SOURCE = acquire_source()
EXPORT_RIG = clone_export_rig(SOURCE_RIG, COLL)


def material(name, rgba, metallic=0.0, rough=0.5, emission=None, strength=0.0):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.use_nodes = True
    m.diffuse_color = rgba
    bsdf = m.node_tree.nodes.get("Principled BSDF")
    if bsdf:
        bsdf.inputs["Base Color"].default_value = rgba
        bsdf.inputs["Metallic"].default_value = metallic
        bsdf.inputs["Roughness"].default_value = rough
        if emission is not None:
            if "Emission Color" in bsdf.inputs:
                bsdf.inputs["Emission Color"].default_value = emission
                bsdf.inputs["Emission Strength"].default_value = strength
            elif "Emission" in bsdf.inputs:
                bsdf.inputs["Emission"].default_value = emission
    return m

MAT_GLOVE = material("GFR11_VoidGlove", (0.035, 0.035, 0.050, 1.0), 0.58, 0.34)
MAT_PLATE = material("GFR11_Gunmetal", (0.14, 0.14, 0.18, 1.0), 0.80, 0.25)
MAT_EDGE = material("GFR11_Edge", (0.24, 0.23, 0.29, 1.0), 0.86, 0.22)
MAT_PURPLE = material("GFR11_VoidPurple", (0.22, 0.025, 0.36, 1.0), 0.0, 0.20,
                      (0.82, 0.06, 1.0, 1.0), 3.5)
MAT_CYAN = material("GFR11_FractureCyan", (0.02, 0.22, 0.32, 1.0), 0.0, 0.20,
                    (0.00, 0.95, 1.0, 1.0), 2.8)

CREATED = []

LEFT_HAND, LEFT_THUMB, LEFT_INDEX, LEFT_FINGER = resolve_digit_groups(EXPORT_RIG, "Hand_L")
RIGHT_HAND, RIGHT_THUMB, RIGHT_INDEX, RIGHT_FINGER = resolve_digit_groups(EXPORT_RIG, "Hand_R")

# ------------------------------------------------------------
# Native hand-surface extraction
# ------------------------------------------------------------

def group_index_map(obj):
    return {g.index: g.name for g in obj.vertex_groups}


def relevant_group_names(hand, thumb, index, finger):
    return {hand.name} | {b.name for b in thumb} | {b.name for b in index} | {b.name for b in finger}


def native_vertex_weights(body, vi, allowed_names, index_to_name):
    out = []
    total = 0.0
    for membership in body.data.vertices[vi].groups:
        name = index_to_name.get(membership.group)
        if name in allowed_names:
            out.append((name, membership.weight))
            total += membership.weight
    return total, out


def hand_axis_data(hand, index, finger):
    wrist = hand.head_local.copy()
    digit_center = (index[0].head_local + finger[0].head_local) * 0.5
    axis = safe_normal(digit_center - wrist, hand.tail_local - hand.head_local)
    return wrist, axis


# ============================================================
# MODULAR BONE-LOCAL HAND EXPORT
# ============================================================
# This version deliberately exports ordinary MeshRenderer-ready pieces rather
# than a skinned mesh.  Every piece is generated from the ACTUAL Synty hand
# surface, then converted into the local coordinate system of the exact bone
# that will own it in game. FateEquipmentVisuals parents the object to that
# live bone once with local identity; animation is then completely native.

SHOW_EXPORT_RIG = False
BOUNDARY_OVERLAP_RATIO = 0.72
MIN_FACE_BONE_WEIGHT = 0.045


def source_body_height_and_scale():
    rig_inv = SOURCE_RIG.matrix_world.inverted()
    hips = SOURCE_RIG.data.bones.get('Hips')
    head = SOURCE_RIG.data.bones.get('Head')
    if hips is not None and head is not None:
        # Use the full mesh envelope because head.tail alone excludes feet.
        vals=[]
        up=safe_normal(head.head_local-hips.head_local, Vector((0,0,1)))
        for v in SOURCE_BODY.data.vertices:
            p=rig_inv @ (SOURCE_BODY.matrix_world @ v.co)
            vals.append(p.dot(up))
        if vals:
            h=max(vals)-min(vals)
            return 1.8/max(h,1e-6),h
    return 1.0,0.0


UNITY_UNIT_SCALE, SOURCE_HEIGHT = source_body_height_and_scale()
print('[Grasp Modular] source body height=',round(SOURCE_HEIGHT,4),
      ' baked Unity scale=',round(UNITY_UNIT_SCALE,6))


def group_index_map(obj):
    return {g.index:g.name for g in obj.vertex_groups}


GROUP_NAME_BY_INDEX=group_index_map(SOURCE_BODY)
BODY_TO_RIG=EXPORT_RIG.matrix_world.inverted() @ SOURCE_BODY.matrix_world


def side_targets(hand, thumb, index, finger, side_code):
    targets=[]
    targets.append(('FOTF_Grasp_Hand_'+side_code, hand))
    for i,b in enumerate(thumb):
        targets.append(('FOTF_Grasp_Thumb_'+side_code+'_%02d'%(i+1), b))
    for i,b in enumerate(index):
        targets.append(('FOTF_Grasp_Index_'+side_code+'_%02d'%(i+1), b))
    for i,b in enumerate(finger):
        targets.append(('FOTF_Grasp_Finger_'+side_code+'_%02d'%(i+1), b))
    return targets


LEFT_TARGETS=side_targets(LEFT_HAND,LEFT_THUMB,LEFT_INDEX,LEFT_FINGER,'L')
RIGHT_TARGETS=side_targets(RIGHT_HAND,RIGHT_THUMB,RIGHT_INDEX,RIGHT_FINGER,'R')

if len(LEFT_THUMB)!=3 or len(LEFT_INDEX)!=4 or len(LEFT_FINGER)!=4:
    raise RuntimeError('Left Synty hand chains changed: Thumb=%d Index=%d Finger=%d' %
                       (len(LEFT_THUMB),len(LEFT_INDEX),len(LEFT_FINGER)))
if len(RIGHT_THUMB)!=3 or len(RIGHT_INDEX)!=4 or len(RIGHT_FINGER)!=4:
    raise RuntimeError('Right Synty hand chains changed: Thumb=%d Index=%d Finger=%d' %
                       (len(RIGHT_THUMB),len(RIGHT_INDEX),len(RIGHT_FINGER)))


def vertex_weight_for_name(vertex, target_name):
    value=0.0
    for membership in vertex.groups:
        if GROUP_NAME_BY_INDEX.get(membership.group)==target_name:
            value=max(value,membership.weight)
    return value


def relevant_total(vertex, target_names):
    total=0.0
    for membership in vertex.groups:
        if GROUP_NAME_BY_INDEX.get(membership.group) in target_names:
            total+=membership.weight
    return total


def hand_region_vertex(vertex, hand, targets):
    target_names={b.name for _,b in targets}
    if relevant_total(vertex,target_names)<HAND_REGION_MIN_WEIGHT:
        return False
    # Cut away the forearm/wrist. The region begins exactly at the native hand
    # joint and extends toward the native digit roots.
    index_bone=targets[4][1]  # first index segment after hand + 3 thumb pieces
    finger_bone=targets[8][1] # first broad-finger segment after 4 index pieces
    wrist=hand.head_local.copy()
    digit_center=(index_bone.head_local+finger_bone.head_local)*0.5
    axis=safe_normal(digit_center-wrist, hand.tail_local-hand.head_local)
    p=BODY_TO_RIG @ vertex.co
    return (p-wrist).dot(axis) >= -(max(hand.length,1e-4)*WRIST_CUTBACK)


def face_weight(poly,bone_name):
    if len(poly.vertices)==0:
        return 0.0
    return sum(vertex_weight_for_name(SOURCE_BODY.data.vertices[i],bone_name)
               for i in poly.vertices)/float(len(poly.vertices))


def build_piece(part_name,target_bone,hand,targets,material_for_piece):
    target_names={b.name for _,b in targets}
    candidate_faces=[]

    for poly in SOURCE_BODY.data.polygons:
        if len(poly.vertices)<3:
            continue
        # Reject faces outside the hand/fingers or crossing into the forearm.
        if not all(hand_region_vertex(SOURCE_BODY.data.vertices[i],hand,targets)
                   for i in poly.vertices):
            continue

        weights={b.name:face_weight(poly,b.name) for _,b in targets}
        max_weight=max(weights.values()) if weights else 0.0
        this_weight=weights.get(target_bone.name,0.0)

        # Dominant faces belong to this piece. Near joints, also copy faces that
        # carry a strong secondary influence so neighbouring rigid plates overlap
        # slightly instead of opening animation gaps.
        if this_weight >= MIN_FACE_BONE_WEIGHT and (
            this_weight >= max_weight-1e-6 or
            (max_weight>0.0 and this_weight >= max_weight*BOUNDARY_OVERLAP_RATIO)):
            candidate_faces.append(poly)

    if not candidate_faces:
        # Some Synty segments are weighted very softly at their ends. Fall back
        # to any hand-region face carrying a meaningful amount of this bone.
        for poly in SOURCE_BODY.data.polygons:
            if len(poly.vertices)<3:
                continue
            if not all(hand_region_vertex(SOURCE_BODY.data.vertices[i],hand,targets)
                       for i in poly.vertices):
                continue
            if face_weight(poly,target_bone.name) >= 0.015:
                candidate_faces.append(poly)

    if not candidate_faces:
        raise RuntimeError('No Synty hand surface faces resolved for '+part_name+
                           ' target bone '+target_bone.name)

    used=sorted({vi for poly in candidate_faces for vi in poly.vertices})
    remap={old:new for new,old in enumerate(used)}
    verts=[BODY_TO_RIG @ SOURCE_BODY.data.vertices[vi].co for vi in used]
    faces=[tuple(remap[vi] for vi in poly.vertices) for poly in candidate_faces]

    mesh=bpy.data.meshes.new(part_name+'_Mesh')
    mesh.from_pydata([tuple(v) for v in verts],[],faces)
    mesh.validate(clean_customdata=False)
    mesh.update(calc_edges=True,calc_edges_loose=True)

    obj=bpy.data.objects.new(part_name,mesh)
    COLL.objects.link(obj)
    obj.data.materials.append(material_for_piece)

    # Inflate in source/bind space so this is an armour shell sitting above the
    # native hand rather than a duplicate skin surface.
    bm=bmesh.new()
    try:
        bm.from_mesh(mesh)
        bm.normal_update()
        amount=max(hand.length,1e-4)*SHELL_INFLATE
        for bv in bm.verts:
            bv.co += bv.normal*amount
        if bm.faces:
            bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
            bmesh.ops.triangulate(bm,faces=list(bm.faces),quad_method='BEAUTY',ngon_method='BEAUTY')
        bm.to_mesh(mesh)
        mesh.validate(clean_customdata=False)
        mesh.update(calc_edges=True,calc_edges_loose=True)
    finally:
        bm.free()

    for p in mesh.polygons:
        p.use_smooth=False

    # Convert actual native hand geometry into this exact bone's local space.
    inv=target_bone.matrix_local.inverted()
    for v in mesh.vertices:
        v.co=(inv @ v.co)*UNITY_UNIT_SCALE
    mesh.update(calc_edges=True,calc_edges_loose=True)

    obj.matrix_world=Matrix.Identity(4)
    obj.location=(0.0,0.0,0.0)
    obj.rotation_mode='XYZ'
    obj.rotation_euler=(0.0,0.0,0.0)
    obj.scale=(1.0,1.0,1.0)
    obj['FOTF_TargetBone']=target_bone.name
    CREATED.append(obj)
    return obj


# Build actual glove surfaces. The hand/palm is gunmetal; articulated digit
# pieces are void-black so the result still reads as Grasp rather than exposed
# native skin.
for part_name,bone in LEFT_TARGETS:
    mat=MAT_PLATE if part_name=='FOTF_Grasp_Hand_L' else MAT_GLOVE
    build_piece(part_name,bone,LEFT_HAND,LEFT_TARGETS,mat)
for part_name,bone in RIGHT_TARGETS:
    mat=MAT_PLATE if part_name=='FOTF_Grasp_Hand_R' else MAT_GLOVE
    build_piece(part_name,bone,RIGHT_HAND,RIGHT_TARGETS,mat)

# Plain asset root only. No armature/body reference is exported.
root_obj=bpy.data.objects.new('FOTF_Grasp_ModularRoot',None)
COLL.objects.link(root_obj)
for obj in CREATED:
    obj.parent=root_obj
    obj.matrix_parent_inverse=Matrix.Identity(4)

for obj in IMPORTED_SOURCE:
    try:
        obj.hide_viewport=True
        obj.hide_render=True
    except Exception:
        pass
try:
    SOURCE_RIG.hide_viewport=True
    SOURCE_BODY.hide_viewport=True
    EXPORT_RIG.hide_viewport=True
except Exception:
    pass

try:
    for area in bpy.context.screen.areas:
        if area.type=='VIEW_3D':
            area.spaces.active.shading.type='MATERIAL'
except Exception:
    pass


def export_fbx():
    bpy.ops.object.select_all(action='DESELECT')
    root_obj.select_set(True)
    for obj in CREATED:
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
    print('[Grasp Modular] Exported:',path)
    print('[Grasp Modular] Parts=',len(CREATED))
    print('[Grasp Modular] Expected runtime names=',[o.name for o in CREATED])


if AUTO_EXPORT:
    export_fbx()
