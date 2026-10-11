import bpy
import math
import os
from mathutils import Vector

PREFAB_NAME = 'FOTF_Soulbinders_Grimoire'
EXPORT_FILE = PREFAB_NAME + ".fbx"
AUTO_EXPORT = True


def clear_scene():
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)
    for datablocks in (bpy.data.meshes, bpy.data.curves, bpy.data.materials):
        pass


def material(name, color, metallic=0.0, roughness=0.5, emission=None, emission_strength=0.0):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    bsdf = m.node_tree.nodes.get('Principled BSDF')
    bsdf.inputs['Base Color'].default_value = color
    bsdf.inputs['Metallic'].default_value = metallic
    bsdf.inputs['Roughness'].default_value = roughness
    if emission is not None:
        if 'Emission Color' in bsdf.inputs:
            bsdf.inputs['Emission Color'].default_value = emission
            bsdf.inputs['Emission Strength'].default_value = emission_strength
        elif 'Emission' in bsdf.inputs:
            bsdf.inputs['Emission'].default_value = emission
    return m


def add_cube(name, loc, scale, mat, bevel=0.04):
    bpy.ops.mesh.primitive_cube_add(location=loc)
    o = bpy.context.object
    o.name = name
    o.scale = scale
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if bevel > 0:
        mod = o.modifiers.new('LowPolyBevel', 'BEVEL')
        mod.width = bevel
        mod.segments = 1
        bpy.context.view_layer.objects.active = o
        bpy.ops.object.modifier_apply(modifier=mod.name)
    if mat: o.data.materials.append(mat)
    return o


def add_cylinder(name, loc, radius, depth, mat, vertices=8, rotation=(0,0,0)):
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius, depth=depth, location=loc, rotation=rotation)
    o=bpy.context.object
    o.name=name
    if mat: o.data.materials.append(mat)
    return o


def add_cone(name, loc, r1, r2, depth, mat, vertices=8, rotation=(0,0,0)):
    bpy.ops.mesh.primitive_cone_add(vertices=vertices, radius1=r1, radius2=r2, depth=depth, location=loc, rotation=rotation)
    o=bpy.context.object
    o.name=name
    if mat: o.data.materials.append(mat)
    return o


def add_ico(name, loc, scale, mat, subdivisions=1):
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=subdivisions, radius=1.0, location=loc)
    o=bpy.context.object
    o.name=name
    o.scale=scale
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if mat: o.data.materials.append(mat)
    return o


def add_torus(name, loc, major, minor, mat, rot=(0,0,0)):
    bpy.ops.mesh.primitive_torus_add(major_radius=major, minor_radius=minor, major_segments=12, minor_segments=6, location=loc, rotation=rot)
    o=bpy.context.object
    o.name=name
    if mat: o.data.materials.append(mat)
    return o


def cylinder_between(name, a, b, radius, mat, vertices=8):
    a=Vector(a); b=Vector(b)
    d=b-a
    mid=(a+b)*0.5
    o=add_cylinder(name, mid, radius, d.length, mat, vertices)
    o.rotation_mode='QUATERNION'
    o.rotation_quaternion=d.to_track_quat('Z','Y')
    return o


def add_curve(name, pts, bevel, mat):
    c=bpy.data.curves.new(name+'_Curve','CURVE')
    c.dimensions='3D'
    c.resolution_u=1
    c.bevel_depth=bevel
    c.bevel_resolution=0
    spl=c.splines.new('POLY')
    spl.points.add(len(pts)-1)
    for i,p in enumerate(pts):
        spl.points[i].co=(*p,1.0)
    o=bpy.data.objects.new(name,c)
    bpy.context.collection.objects.link(o)
    if mat: o.data.materials.append(mat)
    return o


def make_root():
    root=bpy.data.objects.new(PREFAB_NAME,None)
    bpy.context.collection.objects.link(root)
    return root


def parent_all(root, objects):
    for o in objects:
        if o is not None and o != root:
            o.parent=root


def _walk_children(obj):
    for child in obj.children:
        yield child
        for nested in _walk_children(child):
            yield nested


def export_fbx(root):
    bpy.ops.object.select_all(action='DESELECT')
    root.select_set(True)
    for o in _walk_children(root):
        o.select_set(True)
    bpy.context.view_layer.objects.active=root
    path=os.path.join(os.path.dirname(bpy.data.filepath) if bpy.data.filepath else os.getcwd(), EXPORT_FILE)
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, add_leaf_bones=False, bake_anim=False, apply_scale_options='FBX_SCALE_ALL')
    print('Exported', path)

clear_scene()
root=make_root()

stone=material('GraveLeather',(0.055,0.045,0.065,1),0.05,0.78)
metal=material('DeathlessMetal',(0.12,0.14,0.16,1),0.75,0.32)
page=material('DeadParchment',(0.45,0.43,0.36,1),0.0,0.9)
void=material('SoulGlow',(0.18,0.04,0.35,1),0.15,0.25,(0.45,0.08,0.8,1),3.0)
parts=[]
parts.append(add_cube('Book_Block',(0,0,0),(0.58,0.10,0.78),page,0.035))
parts.append(add_cube('Front_Cover',(0,-0.135,0),(0.62,0.045,0.82),stone,0.055))
parts.append(add_cube('Back_Cover',(0,0.135,0),(0.62,0.045,0.82),stone,0.055))
parts.append(add_cube('Spine',(-0.64,0,0),(0.07,0.16,0.80),metal,0.03))
for z in (-0.48,0,0.48): parts.append(add_cube('Binding_'+str(z),(-0.66,0,z),(0.06,0.18,0.08),metal,0.02))
parts.append(add_ico('Soul_Gem',(0,-0.205,0.10),(0.19,0.07,0.28),void,1))
parts.append(add_cube('Rune_Bar',(0,-0.205,-0.35),(0.32,0.025,0.035),void,0.01))

parent_all(root, parts)
if AUTO_EXPORT:
    export_fbx(root)
