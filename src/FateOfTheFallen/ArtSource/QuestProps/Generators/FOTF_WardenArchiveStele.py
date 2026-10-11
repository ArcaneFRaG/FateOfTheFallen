import bpy
import math
import os

# ============================================================
# Fate of the Fallen - Warden Archive Stele
# Quest 12: Names That Were Buried
#
# Low-poly authored version of the procedural in-game fallback.
# Intended Unity prefab / AssetBundle name:
#     WardenArchiveStele
# Bundle:
#     fotf_questprops
#
# Visual language:
#   - ancient dark warding stone
#   - four inset prison seals
#   - one fractured ward line through the centre
#   - deliberately damaged/chiselled inscription bands
#   - restrained Blight / Void / Death / Infernal colour accents
# ============================================================

ROOT_NAME = "WardenArchiveStele"
COLLECTION_NAME = "FOTF_WardenArchiveStele"
EXPORT_FILE_NAME = "WardenArchiveStele.fbx"
AUTO_EXPORT = True


def remove_collection(name):
    coll = bpy.data.collections.get(name)
    if coll:
        for obj in list(coll.objects):
            bpy.data.objects.remove(obj, do_unlink=True)
        bpy.data.collections.remove(coll)


def make_material(name, color, emission=None):
    mat = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    mat.diffuse_color = (*color, 1.0)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    if bsdf:
        bsdf.inputs["Base Color"].default_value = (*color, 1.0)
        bsdf.inputs["Roughness"].default_value = 0.78
        if emission:
            if "Emission Color" in bsdf.inputs:
                bsdf.inputs["Emission Color"].default_value = (*emission, 1.0)
                bsdf.inputs["Emission Strength"].default_value = 0.7
            elif "Emission" in bsdf.inputs:
                bsdf.inputs["Emission"].default_value = (*emission, 1.0)
    return mat


def bevel(obj, amount=0.035, segments=1):
    mod = obj.modifiers.new("LowPolyBevel", "BEVEL")
    mod.width = amount
    mod.segments = segments
    mod.affect = 'EDGES'
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.modifier_apply(modifier=mod.name)
    obj.select_set(False)


def cube(name, loc, scale, mat, rot=(0, 0, 0), bevel_amount=0.025):
    bpy.ops.mesh.primitive_cube_add(location=loc, rotation=rot)
    obj = bpy.context.object
    obj.name = name
    obj.scale = (scale[0] / 2.0, scale[1] / 2.0, scale[2] / 2.0)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if bevel_amount > 0:
        bevel(obj, bevel_amount, 1)
    obj.data.materials.append(mat)
    return obj


def cylinder(name, loc, radius, depth, mat, rot=(math.radians(90), 0, 0), vertices=12):
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius, depth=depth,
                                       location=loc, rotation=rot)
    obj = bpy.context.object
    obj.name = name
    bevel(obj, 0.018, 1)
    obj.data.materials.append(mat)
    return obj


def parent_all(root, objects):
    for obj in objects:
        obj.parent = root


def create_sigil(objects, name, x, z, accent, dark):
    # Blender uses Z-up; front is -Y for the authored prefab.
    objects.append(cylinder(name + "_Backing", (x, -0.205, z), 0.34, 0.11, dark))
    objects.append(cylinder(name + "_Glyph", (x, -0.270, z), 0.235, 0.07, accent))
    objects.append(cube(name + "_StrokeA", (x, -0.315, z), (0.31, 0.045, 0.045), dark,
                        rot=(0, math.radians(45), 0), bevel_amount=0.012))
    objects.append(cube(name + "_StrokeB", (x, -0.317, z), (0.31, 0.045, 0.045), dark,
                        rot=(0, math.radians(-45), 0), bevel_amount=0.012))


def build():
    remove_collection(COLLECTION_NAME)
    coll = bpy.data.collections.new(COLLECTION_NAME)
    bpy.context.scene.collection.children.link(coll)

    # Move newly created objects into our collection.
    old_layer_collection = bpy.context.view_layer.active_layer_collection
    layer_coll = bpy.context.view_layer.layer_collection.children.get(COLLECTION_NAME)
    if layer_coll:
        bpy.context.view_layer.active_layer_collection = layer_coll

    stone = make_material("FOTF_Stele_Stone", (0.30, 0.29, 0.27))
    dark = make_material("FOTF_Stele_DarkStone", (0.16, 0.15, 0.14))
    engraving = make_material("FOTF_Stele_Engraving", (0.075, 0.07, 0.065))
    blight = make_material("FOTF_Stele_Blight", (0.36, 0.58, 0.13), (0.06, 0.12, 0.015))
    void = make_material("FOTF_Stele_Void", (0.34, 0.13, 0.55), (0.06, 0.015, 0.12))
    death = make_material("FOTF_Stele_Death", (0.34, 0.55, 0.58), (0.02, 0.08, 0.09))
    infernal = make_material("FOTF_Stele_Infernal", (0.70, 0.20, 0.06), (0.16, 0.025, 0.005))
    crack = make_material("FOTF_Stele_WardCrack", (0.72, 0.69, 0.52), (0.12, 0.10, 0.045))

    root = bpy.data.objects.new(ROOT_NAME, None)
    coll.objects.link(root)

    objects = []

    # Base/plinth.
    objects += [
        cube("Plinth_Lower", (0, 0, 0.10), (2.20, 1.05, 0.20), dark),
        cube("Plinth_Middle", (0, 0, 0.26), (1.92, 0.86, 0.16), stone),
        cube("Plinth_Upper", (0, 0, 0.40), (1.72, 0.70, 0.12), dark),
    ]

    # Main slab and frame.
    objects += [
        cube("ArchiveSlab", (0, 0, 1.78), (1.56, 0.34, 2.64), stone, bevel_amount=0.045),
        cube("Frame_Left", (-0.86, 0, 1.79), (0.16, 0.46, 2.72), dark,
             rot=(0, 0, math.radians(-1.5))),
        cube("Frame_Right", (0.86, 0, 1.79), (0.16, 0.46, 2.72), dark,
             rot=(0, 0, math.radians(1.5))),
        cube("Frame_Top", (0, 0, 3.10), (1.90, 0.48, 0.22), dark,
             rot=(0, 0, math.radians(-1.0))),
        cube("Crown_Left", (-0.43, 0, 3.25), (0.75, 0.42, 0.18), stone,
             rot=(0, 0, math.radians(-6.0))),
        cube("Crown_Right", (0.46, 0, 3.22), (0.70, 0.40, 0.16), stone,
             rot=(0, 0, math.radians(8.0))),
    ]

    # Four Fallen/prison seals.
    create_sigil(objects, "Seal_Blight", -0.43, 2.28, blight, engraving)
    create_sigil(objects, "Seal_Void", 0.43, 2.28, void, engraving)
    create_sigil(objects, "Seal_Death", -0.43, 1.43, death, engraving)
    create_sigil(objects, "Seal_Infernal", 0.43, 1.43, infernal, engraving)

    # Chiselled inscription remnants.
    objects += [
        cube("InscriptionBand_Upper", (0, -0.205, 2.77), (1.22, 0.05, 0.09), engraving, bevel_amount=0.008),
        cube("InscriptionBand_Lower", (0, -0.205, 0.90), (1.18, 0.05, 0.09), engraving, bevel_amount=0.008),
    ]

    # Fractured ward line.
    objects += [
        cube("WardCrack_Upper", (-0.05, -0.245, 2.52), (0.045, 0.045, 0.78), crack,
             rot=(0, 0, math.radians(-8)), bevel_amount=0.006),
        cube("WardCrack_Middle", (0.04, -0.245, 1.82), (0.045, 0.045, 0.68), crack,
             rot=(0, 0, math.radians(9)), bevel_amount=0.006),
        cube("WardCrack_Lower", (-0.02, -0.245, 1.16), (0.045, 0.045, 0.62), crack,
             rot=(0, 0, math.radians(-5)), bevel_amount=0.006),
    ]

    parent_all(root, objects)

    # Apply flat shading for the low-poly Erenshor/Synty-adjacent look.
    for obj in objects:
        if obj.type == 'MESH':
            for poly in obj.data.polygons:
                poly.use_smooth = False

    bpy.context.view_layer.active_layer_collection = old_layer_collection
    return root, objects


def export_fbx(root, objects):
    for obj in bpy.context.selected_objects:
        obj.select_set(False)
    root.select_set(True)
    for obj in objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = root

    blend_dir = os.path.dirname(bpy.data.filepath) if bpy.data.filepath else os.getcwd()
    out_path = os.path.join(blend_dir, EXPORT_FILE_NAME)

    bpy.ops.export_scene.fbx(
        filepath=out_path,
        use_selection=True,
        apply_scale_options='FBX_SCALE_ALL',
        bake_space_transform=False,
        object_types={'EMPTY', 'MESH'},
        add_leaf_bones=False,
        mesh_smooth_type='FACE',
        path_mode='AUTO',
        axis_forward='-Z',
        axis_up='Y',
    )
    print("[FOTF] Exported Warden Archive Stele:", out_path)


root, objects = build()
if AUTO_EXPORT:
    export_fbx(root, objects)

print("[FOTF] Warden Archive Stele ready. Unity prefab name must be:", ROOT_NAME)
