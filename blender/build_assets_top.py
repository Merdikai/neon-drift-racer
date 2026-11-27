import json
import math
import os
import sys

# Provide graceful fallback for IDE linters (VSCode/Pylance) when opened outside Blender's python runtime
try:
    import bpy
    import bmesh
    from mathutils import Euler, Matrix, Vector
except ImportError:
    # Running outside Blender, stub objects to silence IDE / linter errors
    bpy = None
    bmesh = None
    Euler = None
    Matrix = None
    Vector = None


ROOT_DIR = os.path.dirname(os.path.abspath(__file__))
EXPORT_DIR = os.path.join(ROOT_DIR, "exports")
os.makedirs(EXPORT_DIR, exist_ok=True)


def clear_scene():
    if bpy is not None:
        bpy.ops.wm.read_factory_settings(use_empty=True)


def make_material(name, hex_color, metallic=0.1, roughness=0.5, emission_hex=None, emission_strength=0.0):
    if bpy is None:
        return None
    mat = bpy.data.materials.get(name)
    if mat is not None:
        return mat
    mat = bpy.data.materials.new(name=name)
    if mat.node_tree is None:
        mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    r, g, b = hex_to_linear_rgb(hex_color)
    bsdf.inputs["Base Color"].default_value = (r, g, b, 1.0)
    bsdf.inputs["Metallic"].default_value = metallic
    bsdf.inputs["Roughness"].default_value = roughness
    if emission_hex and emission_strength > 0.0:
        er, eg, eb = hex_to_linear_rgb(emission_hex)
        if "Emission Color" in bsdf.inputs:
            bsdf.inputs["Emission Color"].default_value = (er, eg, eb, 1.0)
        if "Emission Strength" in bsdf.inputs:
            bsdf.inputs["Emission Strength"].default_value = emission_strength
    return mat


def hex_to_linear_rgb(hex_str):
    h = hex_str.lstrip("#")
    srgb = [int(h[i:i + 2], 16) / 255.0 for i in (0, 2, 4)]
    def s2l(c):
        return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4
    return (s2l(srgb[0]), s2l(srgb[1]), s2l(srgb[2]))
