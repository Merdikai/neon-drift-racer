import json
import math
import os
import sys

# Safe import with fallback for IDE linter inspection (Pylance / VS Code) outside Blender
try:
    import bpy
    import bmesh
    from mathutils import Euler, Matrix, Vector
except ImportError:
    bpy = None
    bmesh = None
    class _Mock:
        pass
    Euler = Matrix = Vector = _Mock


ROOT_DIR = os.path.dirname(os.path.abspath(__file__))
EXPORT_DIR = os.path.join(ROOT_DIR, "exports")
os.makedirs(EXPORT_DIR, exist_ok=True)


def clear_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def make_material(name, hex_color, metallic=0.1, roughness=0.5, emission_hex=None, emission_strength=0.0):
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


def create_palette_materials():
    return {
        "CarBody": make_material("Mat_CarBody", "#141824", metallic=0.78, roughness=0.26),
        "CarAccent": make_material("Mat_CarAccent", "#1F2842", metallic=0.65, roughness=0.35),
        "GlassCanopy": make_material("Mat_GlassCanopy", "#091022", metallic=0.25, roughness=0.06),
        "NeonCyan": make_material("Mat_NeonCyan", "#00E5FF", metallic=0.0, roughness=0.2, emission_hex="#00E5FF", emission_strength=5.0),
        "NeonMagenta": make_material("Mat_NeonMagenta", "#FF2E93", metallic=0.0, roughness=0.2, emission_hex="#FF2E93", emission_strength=5.0),
        "ScoreGold": make_material("Mat_ScoreGold", "#FFE14D", metallic=0.0, roughness=0.2, emission_hex="#FFE14D", emission_strength=4.5),
        "TireRubber": make_material("Mat_TireRubber", "#0B0D12", metallic=0.0, roughness=0.86),
        "WheelRim": make_material("Mat_WheelRim", "#232C44", metallic=0.88, roughness=0.24),
        "RoadAsphalt": make_material("Mat_RoadAsphalt", "#111626", metallic=0.05, roughness=0.68),
        "TrackBounds": make_material("Mat_TrackBounds", "#1E2747", metallic=0.45, roughness=0.42),
        "BuildingWall": make_material("Mat_BuildingWall", "#0C1222", metallic=0.35, roughness=0.55),
    }


def add_mesh_object(name, bm, mat=None, parent=None, location=(0, 0, 0)):
    mesh = bpy.data.meshes.new(name + "_Mesh")
    bm.normal_update()
    if not bm.loops.layers.uv:
        uv_layer = bm.loops.layers.uv.new("UVMap")
        for face in bm.faces:
            n = face.normal
            ax, ay, az = abs(n.x), abs(n.y), abs(n.z)
            for loop in face.loops:
                co = loop.vert.co
                if az >= ax and az >= ay:
                    loop[uv_layer].uv = (co.x * 0.25, co.y * 0.25)
                elif ax >= ay:
                    loop[uv_layer].uv = (co.y * 0.25, co.z * 0.25)
                else:
                    loop[uv_layer].uv = (co.x * 0.25, co.z * 0.25)
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(obj)
    obj.location = location
    if mat is not None:
        obj.data.materials.append(mat)
    if parent is not None:
        obj.parent = parent
    return obj


def make_box_bm(sx, sy, sz, center=(0, 0, 0), bevel=0.0):
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    bmesh.ops.scale(bm, vec=(sx, sy, sz), verts=bm.verts)
    if bevel > 0.0:
        bmesh.ops.bevel(bm, geom=bm.edges[:], offset=bevel, segments=2, affect='EDGES', clamp_overlap=True)
    bmesh.ops.translate(bm, vec=center, verts=bm.verts)
    return bm


def append_box(bm, sx, sy, sz, center=(0, 0, 0), rot_euler=(0, 0, 0), mat_index=0):
    tmp = bmesh.new()
    bmesh.ops.create_cube(tmp, size=1.0)
    bmesh.ops.scale(tmp, vec=(sx, sy, sz), verts=tmp.verts)
    rot_mat = Euler(rot_euler, 'XYZ').to_matrix().to_4x4()
    trans_mat = Matrix.Translation(center) @ rot_mat
    bmesh.ops.transform(tmp, matrix=trans_mat, verts=tmp.verts)
    for f in tmp.faces:
        f.material_index = mat_index
    me = bpy.data.meshes.new("_tmp")
    tmp.to_mesh(me)
    tmp.free()
    bm.from_mesh(me)
    bpy.data.meshes.remove(me)


def append_tapered_hull(bm, profiles, mat_index=0):
    """Create a lofted symmetric body from longitudinal profiles: [(y, half_w, z_bot, z_top), ...]."""
    rings = []
    for y, hw, zb, zt in profiles:
        # 6-point cross section for sleek sculpted body:
        # bottom-left, mid-left, top-left, top-right, mid-right, bottom-right
        zm = zb + (zt - zb) * 0.52
        hw_top = hw * 0.84
        hw_bot = hw * 0.94
        pts = [
            Vector((-hw_bot, y, zb)),
            Vector((-hw, y, zm)),
            Vector((-hw_top, y, zt)),
            Vector((hw_top, y, zt)),
            Vector((hw, y, zm)),
            Vector((hw_bot, y, zb)),
        ]
        rings.append([bm.verts.new(p) for p in pts])

    for i in range(len(rings) - 1):
        r0 = rings[i]
        r1 = rings[i + 1]
        n = len(r0)
        for j in range(n):
            jn = (j + 1) % n
            f = bm.faces.new((r0[j], r0[jn], r1[jn], r1[j]))
            f.material_index = mat_index

    # Front & back caps
    f_front = bm.faces.new(list(reversed(rings[0])))
    f_front.material_index = mat_index
    f_back = bm.faces.new(rings[-1])
    f_back.material_index = mat_index


def export_fbx(filepath, objects=None):
    bpy.ops.object.select_all(action='DESELECT')
    if objects is not None:
        for o in objects:
            o.select_set(True)
    else:
        for o in bpy.context.scene.collection.objects:
            o.select_set(True)
    # In Blender: -Y forward, +Z up -> Unity +Z forward, +Y up using axis_forward='-Z', axis_up='Y'
    bpy.ops.export_scene.fbx(
        filepath=filepath,
        use_selection=True,
        apply_scale_options='FBX_SCALE_UNITS',
        axis_forward='-Z',
        axis_up='Y',
        bake_space_transform=True,
        object_types={'MESH', 'EMPTY'},
        use_mesh_modifiers=True,
        mesh_smooth_type='FACE',
        path_mode='AUTO',
    )


def build_drift_car():
    """Build cyberpunk drift car facing -Y (Blender front) so Unity imports +Z forward."""
    clear_scene()
    mats = create_palette_materials()

    root = bpy.data.objects.new("NeonDriftCar", None)
    bpy.context.scene.collection.objects.link(root)

    # 1. Main Sculpted Body + Wing + Splitter (multi-material)
    body_bm = bmesh.new()
    # Profile from nose (-2.25) to tail (+2.20)
    chassis_profiles = [
        (-2.24, 0.72, 0.16, 0.34),  # sharp nose
        (-1.85, 0.94, 0.15, 0.56),  # front hood
        (-1.10, 1.02, 0.15, 0.70),  # front fenders
        (-0.35, 0.98, 0.16, 0.72),  # doors
        (0.85,  1.05, 0.16, 0.78),  # flared rear haunches
        (1.85,  0.98, 0.20, 0.80),  # rear quarter
        (2.22,  0.84, 0.26, 0.74),  # tail taper
    ]
    append_tapered_hull(body_bm, chassis_profiles, mat_index=0)  # CarBody

    # Front splitter & side skirts (CarAccent = 1)
    append_box(body_bm, 1.98, 0.48, 0.07, center=(0, -2.08, 0.13), mat_index=1)
    append_box(body_bm, 0.14, 2.35, 0.08, center=(-0.98, 0.05, 0.15), mat_index=1)
    append_box(body_bm, 0.14, 2.35, 0.08, center=(0.98, 0.05, 0.15), mat_index=1)
    # Rear diffuser fins
    for dx in (-0.52, -0.26, 0.0, 0.26, 0.52):
        append_box(body_bm, 0.05, 0.36, 0.22, center=(dx, 2.12, 0.22), mat_index=1)

    # GT Rear Wing Spoiler
    append_box(body_bm, 0.06, 0.28, 0.34, center=(-0.56, 1.96, 0.92), rot_euler=(0.22, 0, 0), mat_index=1)
    append_box(body_bm, 0.06, 0.28, 0.34, center=(0.56, 1.96, 0.92), rot_euler=(0.22, 0, 0), mat_index=1)
    append_box(body_bm, 2.06, 0.38, 0.05, center=(0, 2.05, 1.10), rot_euler=(-0.12, 0, 0), mat_index=0)
    # Wing endplates
    append_box(body_bm, 0.04, 0.44, 0.24, center=(-1.04, 2.05, 1.10), mat_index=1)
    append_box(body_bm, 0.04, 0.44, 0.24, center=(1.04, 2.05, 1.10), mat_index=1)

    # Cabin Canopy (GlassCanopy = 2)
    canopy_profiles = [
        (-0.98, 0.68, 0.66, 0.74),
        (-0.42, 0.74, 0.68, 1.12),
        (0.35,  0.72, 0.68, 1.16),
        (1.15,  0.64, 0.70, 0.90),
        (1.68,  0.54, 0.74, 0.80),
    ]
    append_tapered_hull(body_bm, canopy_profiles, mat_index=2)

    # Neon Trim: Cyan Front DRL + Side Underglow + Wing Tip Strips (NeonCyan = 3)
    append_box(body_bm, 1.52, 0.08, 0.05, center=(0, -2.23, 0.34), mat_index=3)
    append_box(body_bm, 0.26, 0.14, 0.06, center=(-0.72, -2.14, 0.42), rot_euler=(0, 0, -0.25), mat_index=3)
    append_box(body_bm, 0.26, 0.14, 0.06, center=(0.72, -2.14, 0.42), rot_euler=(0, 0, 0.25), mat_index=3)
    append_box(body_bm, 0.05, 2.15, 0.04, center=(-1.02, 0.05, 0.12), mat_index=3)
    append_box(body_bm, 0.05, 2.15, 0.04, center=(1.02, 0.05, 0.12), mat_index=3)

    # Neon Magenta: Full-width Rear Taillight Blade + Thruster Vents (NeonMagenta = 4)
    append_box(body_bm, 1.64, 0.07, 0.07, center=(0, 2.22, 0.62), mat_index=4)
    append_box(body_bm, 0.28, 0.10, 0.10, center=(-0.48, 2.22, 0.34), mat_index=4)
    append_box(body_bm, 0.28, 0.10, 0.10, center=(0.48, 2.22, 0.34), mat_index=4)
    append_box(body_bm, 2.02, 0.05, 0.04, center=(0, 2.23, 1.09), mat_index=4)

    body_obj = add_mesh_object("Body", body_bm, parent=root)
    for key in ("CarBody", "CarAccent", "GlassCanopy", "NeonCyan", "NeonMagenta"):
        body_obj.data.materials.append(mats[key])

    # 2. Four Separate Wheel Meshes (exact hub pivots)
    wheel_radius = 0.36
    wheel_width = 0.28
    # Note: facing -Y -> +X is left side, -X is right side in Blender!
    wheel_positions = {
        "Wheel_FL": (0.94, -1.38, wheel_radius),
        "Wheel_FR": (-0.94, -1.38, wheel_radius),
        "Wheel_RL": (0.98, 1.38, wheel_radius),
        "Wheel_RR": (-0.98, 1.38, wheel_radius),
    }

    for wname, wpos in wheel_positions.items():
        wbm = bmesh.new()
        # Tire cylinder aligned along X axis (rotation Y = 90 deg)
        tire_mat = Matrix.Rotation(math.radians(90), 4, 'Y')
        bmesh.ops.create_cone(
            wbm, cap_ends=True, cap_tris=False, segments=28,
            radius1=wheel_radius, radius2=wheel_radius, depth=wheel_width,
            matrix=tire_mat,
        )
        for f in wbm.faces:
            f.material_index = 0  # TireRubber

        # Glowing Cyan Rim Ring (NeonCyan = 2) on outer lip
        ring_bm = bmesh.new()
        bmesh.ops.create_cone(
            ring_bm, cap_ends=True, cap_tris=False, segments=28,
            radius1=wheel_radius * 0.72, radius2=wheel_radius * 0.72, depth=wheel_width + 0.006,
            matrix=tire_mat,
        )
        for f in ring_bm.faces:
            f.material_index = 2
        me_ring = bpy.data.meshes.new("_ring")
        ring_bm.to_mesh(me_ring)
        ring_bm.free()
        wbm.from_mesh(me_ring)
        bpy.data.meshes.remove(me_ring)

        # Inner alloy hub cylinder (WheelRim = 1), framed inside the cyan ring
        hub_bm = bmesh.new()
        bmesh.ops.create_cone(
            hub_bm, cap_ends=True, cap_tris=False, segments=20,
            radius1=wheel_radius * 0.62, radius2=wheel_radius * 0.62, depth=wheel_width + 0.012,
            matrix=tire_mat,
        )
        for f in hub_bm.faces:
            f.material_index = 1
        me_hub = bpy.data.meshes.new("_hub")
        hub_bm.to_mesh(me_hub)
        hub_bm.free()
        wbm.from_mesh(me_hub)
        bpy.data.meshes.remove(me_hub)

        # 5 Proud Spokes protruding slightly past the hub and cyan lip
        for s in range(5):
            ang = math.radians(s * 72.0)
            append_box(
                wbm, wheel_width + 0.022, 0.06, wheel_radius * 1.34,
                center=(0, 0, 0), rot_euler=(ang, 0, 0), mat_index=1
            )

        wobj = add_mesh_object(wname, wbm, parent=root, location=wpos)
        for key in ("TireRubber", "WheelRim", "NeonCyan"):
            wobj.data.materials.append(mats[key])

    blend_path = os.path.join(ROOT_DIR, "NeonDriftCar.blend")
    fbx_path = os.path.join(EXPORT_DIR, "NeonDriftCar.fbx")
    bpy.ops.wm.save_as_mainfile(filepath=blend_path)
    export_fbx(fbx_path)
    print(f"Exported {fbx_path}")


def eval_track_point(t):
    """Smooth closed periodic drift circuit in Blender coords (X, Y, Z).
    Polar-modulated loop guarantees zero self-intersections with wide drift sweepers & chicanes."""
    a = 2.0 * math.pi * t
    r = 108.0 + 26.0 * math.cos(2.0 * a) + 16.0 * math.sin(3.0 * a) - 10.0 * math.cos(a)
    x = r * math.sin(a)
    y = r * math.cos(a) * 0.86
    z = 3.2 * math.sin(2.0 * a + 0.5) + 1.4 * math.cos(3.0 * a)
    return Vector((x, y, z))


def blender_to_unity_vec(v):
    """Exact coordinate conversion matching axis_forward='-Z', axis_up='Y' for a -Y-forward Blender scene."""
    # With axis_forward='-Z', axis_up='Y':
    # Blender +X -> Unity -X, Blender +Y -> Unity -Z, Blender +Z -> Unity +Y
    return (-v.x, v.z, -v.y)


def build_neon_track():
    clear_scene()
    mats = create_palette_materials()

    num_samples = 180
    road_half_w = 9.0
    curb_w = 0.9
    wall_h = 1.25
    deck_thick = 0.8

    pts = [eval_track_point(i / num_samples) for i in range(num_samples)]
    min_clearance = 1e9
    for i in range(num_samples):
        for j in range(i + 15, num_samples):
            if abs(i - j) > num_samples - 15:
                continue
            d = (pts[i] - pts[j]).length
            if d < min_clearance:
                min_clearance = d
    print(f"Track min non-adjacent centerline clearance: {min_clearance:.2f}m (need > 22m)")
    if min_clearance < 22.0:
        raise RuntimeError(f"Track self-intersects or too narrow clearance: {min_clearance:.2f}m")
    frames = []
    for i in range(num_samples):
        p_prev = pts[(i - 1) % num_samples]
        p_next = pts[(i + 1) % num_samples]
        fwd = (p_next - p_prev).normalized()
        up = Vector((0, 0, 1))
        right = fwd.cross(up).normalized()
        up = right.cross(fwd).normalized()
        frames.append((pts[i], fwd, right, up))

    # Build unified track mesh with material slots:
    # 0: RoadAsphalt, 1: TrackBounds, 2: NeonCyan, 3: NeonMagenta, 4: ScoreGold
    bm = bmesh.new()

    def make_ring(p, r_vec, u_vec):
        # Cross-section vertices (left to right):
        # Outer wall left, outer wall top, curb top, road edge, road center, road edge, curb top, inner wall top, inner wall right
        offsets = [
            (-road_half_w - curb_w - 0.5, -deck_thick),  # 0 outer base
            (-road_half_w - curb_w - 0.5, wall_h),       # 1 outer wall top-out
            (-road_half_w - curb_w, wall_h),             # 2 outer wall top-in
            (-road_half_w - curb_w, 0.16),               # 3 left curb outer
            (-road_half_w, 0.16),                        # 4 left curb inner / glow edge
            (-road_half_w, 0.0),                         # 5 left road edge
            (road_half_w, 0.0),                          # 6 right road edge
            (road_half_w, 0.16),                         # 7 right curb inner / glow edge
            (road_half_w + curb_w, 0.16),                # 8 right curb outer
            (road_half_w + curb_w, wall_h),              # 9 inner wall top-in
            (road_half_w + curb_w + 0.5, wall_h),        # 10 inner wall top-out
            (road_half_w + curb_w + 0.5, -deck_thick),   # 11 inner base
        ]
        return [bm.verts.new(p + r_vec * ox + u_vec * oz) for ox, oz in offsets]

    rings = [make_ring(p, r, u) for p, f, r, u in frames]

    # Segment pairs and material assignment
    seg_mats = [
        (0, 1, 1),   # outer wall side -> TrackBounds
        (1, 2, 1),   # outer wall top -> TrackBounds
        (2, 3, 1),   # outer wall inner -> TrackBounds
        (3, 4, 2),   # left neon curb -> NeonCyan
        (4, 5, 2),   # left curb step -> NeonCyan
        (5, 6, 0),   # main asphalt road -> RoadAsphalt
        (6, 7, 3),   # right curb step -> NeonMagenta
        (7, 8, 3),   # right neon curb -> NeonMagenta
        (8, 9, 1),   # inner wall inner -> TrackBounds
        (9, 10, 1),  # inner wall top -> TrackBounds
        (10, 11, 1), # inner wall side -> TrackBounds
        (11, 0, 1),  # underside -> TrackBounds
    ]

    for i in range(num_samples):
        r0 = rings[i]
        r1 = rings[(i + 1) % num_samples]
        for a, b, m_idx in seg_mats:
            f = bm.faces.new((r0[a], r0[b], r1[b], r1[a]))
            f.material_index = m_idx

    # Add Start/Finish Arch at sample 0
    p0, f0, r0, u0 = frames[0]
    arch_rot = Matrix((r0, f0, u0)).transposed().to_4x4().to_euler()
    # Left & Right pylons + overhead bridge
    for side in (-1, 1):
        pylon_pos = p0 + r0 * (side * (road_half_w + curb_w + 0.6)) + u0 * 3.8
        append_box(bm, 1.1, 1.4, 7.8, center=pylon_pos, rot_euler=arch_rot, mat_index=1)
        strip_pos = p0 + r0 * (side * (road_half_w + 0.35)) + u0 * 3.8
        append_box(bm, 0.25, 1.45, 7.4, center=strip_pos, rot_euler=arch_rot, mat_index=4)
    bridge_pos = p0 + u0 * 7.6
    append_box(bm, (road_half_w + curb_w + 1.4) * 2.0, 1.8, 1.2, center=bridge_pos, rot_euler=arch_rot, mat_index=1)
    banner_pos = p0 + u0 * 7.6 - f0 * 0.92
    append_box(bm, road_half_w * 1.9, 0.12, 0.55, center=banner_pos, rot_euler=arch_rot, mat_index=4)

    track_obj = add_mesh_object("NeonTrack", bm)
    for key in ("RoadAsphalt", "TrackBounds", "NeonCyan", "NeonMagenta", "ScoreGold"):
        track_obj.data.materials.append(mats[key])

    # Export waypoints JSON in Unity world coordinates
    waypoints = []
    total_len = 0.0
    for i in range(num_samples):
        p, f, r, u = frames[i]
        p_next = frames[(i + 1) % num_samples][0]
        total_len += (p_next - p).length
        ux, uy, uz = blender_to_unity_vec(p)
        fx, fy, fz = blender_to_unity_vec(f)
        rx, ry, rz = blender_to_unity_vec(r)
        upx, upy, upz = blender_to_unity_vec(u)
        waypoints.append({
            "index": i,
            "pos": [round(ux, 4), round(uy, 4), round(uz, 4)],
            "forward": [round(fx, 4), round(fy, 4), round(fz, 4)],
            "right": [round(rx, 4), round(ry, 4), round(rz, 4)],
            "up": [round(upx, 4), round(upy, 4), round(upz, 4)],
            "halfWidth": road_half_w,
        })

    wp_path = os.path.join(EXPORT_DIR, "track_waypoints.json")
    with open(wp_path, "w", encoding="utf-8") as fh:
        json.dump({"trackLength": round(total_len, 2), "roadHalfWidth": road_half_w, "waypoints": waypoints}, fh, indent=2)

    blend_path = os.path.join(ROOT_DIR, "NeonTrack.blend")
    fbx_path = os.path.join(EXPORT_DIR, "NeonTrack.fbx")
    bpy.ops.wm.save_as_mainfile(filepath=blend_path)
    export_fbx(fbx_path)
    print(f"Exported {fbx_path} (length ~{total_len:.1f}m) and {wp_path}")


def build_neon_props():
    clear_scene()
    mats = create_palette_materials()

    # 1. BoostPad (5.5m wide x 4.0m long)
    bp_bm = bmesh.new()
    append_box(bp_bm, 5.2, 3.8, 0.10, center=(0, 0, 0.05), mat_index=0)  # TrackBounds
    for cy in (-1.0, 0.0, 1.0):
        append_box(bp_bm, 1.8, 0.35, 0.06, center=(-0.75, cy, 0.11), rot_euler=(0, 0, 0.45), mat_index=2)
        append_box(bp_bm, 1.8, 0.35, 0.06, center=(0.75, cy, 0.11), rot_euler=(0, 0, -0.45), mat_index=2)
    bp_obj = add_mesh_object("BoostPad", bp_bm)
    for key in ("TrackBounds", "NeonCyan", "NeonMagenta"):
        bp_obj.data.materials.append(mats[key])

    # 2. CheckpointGate (spans 18m road)
    cg_bm = bmesh.new()
    for sx in (-9.2, 9.2):
        append_box(cg_bm, 0.55, 0.65, 5.2, center=(sx, 0, 2.6), mat_index=0)
        append_box(cg_bm, 0.16, 0.72, 4.9, center=(sx * 0.95, 0, 2.55), mat_index=1)
    append_box(cg_bm, 19.0, 0.65, 0.5, center=(0, 0, 5.25), mat_index=0)
    append_box(cg_bm, 18.2, 0.72, 0.18, center=(0, 0, 4.98), mat_index=1)
    cg_obj = add_mesh_object("CheckpointGate", cg_bm)
    for key in ("TrackBounds", "NeonCyan"):
        cg_obj.data.materials.append(mats[key])

    # 3. Cyberpunk Skyline Towers (placed in a row for clean FBX export, prefabs split by object)
    towers = [
        ("SkylineTower_A", 14.0, 14.0, 48.0, 1),  # NeonCyan strips
        ("SkylineTower_B", 18.0, 16.0, 68.0, 2),  # NeonMagenta strips
        ("SkylineTower_C", 12.0, 12.0, 36.0, 3),  # ScoreGold strips
    ]
    for idx, (tname, w, d, h, neon_idx) in enumerate(towers):
        tbm = bmesh.new()
        append_box(tbm, w, d, h * 0.65, center=(0, 0, h * 0.325), mat_index=0)
        append_box(tbm, w * 0.74, d * 0.74, h * 0.35, center=(0, 0, h * 0.825), mat_index=0)
        # Vertical neon edge fins
        for sx in (-1, 1):
            for sy in (-1, 1):
                append_box(tbm, 0.35, 0.35, h * 0.62, center=(sx * w * 0.49, sy * d * 0.49, h * 0.32), mat_index=neon_idx)
        # Crown ring
        append_box(tbm, w * 0.78, d * 0.78, 0.55, center=(0, 0, h * 0.98), mat_index=neon_idx)
        tobj = add_mesh_object(tname, tbm)
        for key in ("BuildingWall", "NeonCyan", "NeonMagenta", "ScoreGold"):
            tobj.data.materials.append(mats[key])

    blend_path = os.path.join(ROOT_DIR, "NeonProps.blend")
    fbx_path = os.path.join(EXPORT_DIR, "NeonProps.fbx")
    bpy.ops.wm.save_as_mainfile(filepath=blend_path)
    export_fbx(fbx_path)
    print(f"Exported {fbx_path}")


if __name__ == "__main__":
    build_drift_car()
    build_neon_track()
    build_neon_props()
    print("ALL BLENDER ASSETS BUILT SUCCESSFULLY")
