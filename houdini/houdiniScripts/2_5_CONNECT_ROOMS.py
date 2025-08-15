import hou
from collections import deque

node = hou.pwd()
geo = node.geometry()

# --- 1. Odaları class attribute ile grupla ---
def find_rooms_by_class(geometry, class_attr_name="class"):
    rooms = {}
    for pt in geometry.points():
        room_id = pt.intAttribValue(class_attr_name)
        if room_id >= 0:
            rooms.setdefault(room_id, []).append(pt)
    return rooms

# --- 2. Komşu noktaları bul (4 yönlü) ---
def get_neighbors(point, geo):
    neighbors = []
    pos = point.position()
    x, y, z = pos[0], pos[1], pos[2]
    offsets = [(1,0,0), (-1,0,0), (0,0,1), (0,0,-1)]
    for ox, oy, oz in offsets:
        nx, ny, nz = x+ox, y+oy, z+oz
        for npt in geo.points():
            np = npt.position()
            if abs(np[0]-nx)<0.1 and abs(np[1]-ny)<0.1 and abs(np[2]-nz)<0.1:
                neighbors.append(npt)
                break
    return neighbors

# --- 3. İki oda arasında yol aç ---
def connect_two_rooms(geo, room_a_pts, room_b_pts):
    def closest_point_pair(pts1, pts2):
        min_dist = float("inf")
        pair = (None,None)
        for p1 in pts1:
            for p2 in pts2:
                dist = (p1.position() - p2.position()).length()
                if dist < min_dist:
                    min_dist = dist
                    pair = (p1, p2)
        return pair

    start_pt, end_pt = closest_point_pair(room_a_pts, room_b_pts)
    queue = deque([start_pt])
    came_from = {start_pt: None}

    while queue:
        current = queue.popleft()
        if current == end_pt:
            break
        for neighbor in get_neighbors(current, geo):
            if neighbor not in came_from:
                came_from[neighbor] = current
                queue.append(neighbor)

    if end_pt not in came_from:
        print("Yol bulunamadı!")
        return

    # Yol üzerindeki duvarları aç
    path = []
    cur = end_pt
    while cur is not None:
        path.append(cur)
        cur = came_from[cur]
    path.reverse()

    for pt in path:
        if pt.stringAttribValue("tile_type") == "wall":
            pt.setAttribValue("tile_type", "empty")

# --- 4. Ana işlem ---
rooms = find_rooms_by_class(geo, "class")
room_ids = sorted(rooms.keys())

for i in range(len(room_ids)-1):
    connect_two_rooms(geo, rooms[room_ids[i]], rooms[room_ids[i+1]])

# --- 5. neighbours attribute oluştur ---
if not geo.findPointAttrib("neighbours"):
    geo.addArrayAttrib(hou.attribType.Point, "neighbours", hou.attribData.Int, 4)

for pt in geo.points():
    neighbors = get_neighbors(pt, geo)
    neighbor_ids = [n.number() for n in neighbors]
    # Eğer komşu sayısı 4’ten az ise kalanları -1 ile doldur
    while len(neighbor_ids) < 4:
        neighbor_ids.append(-1)
    pt.setAttribValue("neighbours", neighbor_ids)


print(f"{len(room_ids)} oda birbirine bağlandı ve neighbours eklendi.")
