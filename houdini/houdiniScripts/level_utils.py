import runpy
runpy.run_path(script_path, init_globals={"hou": hou, "node": node})
import hou

def find_rooms(geo):
    print("find_rooms fonksiyonu çağrıldı.")
    visited = set()
    class_id = 0
    if not geo.findPointAttrib("class"):
        geo.addAttrib(hou.attribType.Point, "class", -1)
    for pt in geo.points():
        if pt.stringAttribValue("tile_type") == "empty" and pt.number() not in visited:
            room_points = []
            queue = [pt]
            visited.add(pt.number())
            while queue:
                current_pt = queue.pop(0)
                room_points.append(current_pt)
                for prim in current_pt.prims():
                    for neighbor in prim.points():
                        if neighbor.number() not in visited and neighbor.stringAttribValue("tile_type") == "empty":
                            visited.add(neighbor.number())
                            queue.append(neighbor)
            for room_pt in room_points:
                room_pt.setAttribValue("class", class_id)
            class_id += 1
    return class_id

import hou

def find_path_between_rooms(geo, start_room, end_room):
    # --- Artık workspace yaratmıyoruz ---
    try:
        # Geometry üzerinde path hesaplama (örnek)
        start_pos = geo.iterPoints()[start_room].position()
        end_pos = geo.iterPoints()[end_room].position()

        path_points = []
        steps = 10
        for i in range(steps + 1):
            t = i / steps
            interp = hou.Vector3(
                start_pos[0] * (1 - t) + end_pos[0] * t,
                start_pos[1] * (1 - t) + end_pos[1] * t,
                start_pos[2] * (1 - t) + end_pos[2] * t
            )
            path_points.append(interp)

        return path_points

    except Exception as e:
        print(f"Pathfinding error: {e}")
        return []
