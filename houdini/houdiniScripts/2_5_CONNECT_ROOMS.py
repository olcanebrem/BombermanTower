import hou
from collections import deque

node = hou.pwd()
geo = node.geometry()

# Öncelikle odaları class attribute'una göre gruplayalım (class point attrib olsun)
def find_rooms_by_class(geometry, class_attr_name="class"):
    rooms = {}
    for pt in geometry.points():
        room_id = pt.intAttribValue(class_attr_name)
        if room_id >= 0:
            rooms.setdefault(room_id, []).append(pt)
    return rooms

# Tile grid'de komşu noktaları (4 yönlü) bul
def get_neighbors(point, geo):
    neighbors = []
    pos = point.position()
    x, y, z = pos[0], pos[1], pos[2]
    offsets = [(1,0,0), (-1,0,0), (0,0,1), (0,0,-1)]  # X,Z ekseninde 4 komşu

    # Kaba komşu arama için küçük optimize: point lookup için dict yapabiliriz
    # Ama basitçe brute force deneyelim
    for ox, oy, oz in offsets:
        nx, ny, nz = x+ox, y+oy, z+oz
        # Yaklaşık pozisyon karşılaştırması (toleranslı)
        for npt in geo.points():
            np = npt.position()
            if abs(np[0]-nx)<0.1 and abs(np[2]-nz)<0.1 and abs(np[1]-ny)<0.1:
                neighbors.append(npt)
                break
    return neighbors

# İki oda arasında yol bul (BFS) ve yol üzerindeki duvarları kır (tile_type="wall" ise "empty" yap)
def connect_two_rooms(geo, room_a_pts, room_b_pts):
    # Oda noktalarının merkezlerini ortalama hesapla
    def avg_pos(points):
        x = sum(p.position()[0] for p in points) / len(points)
        y = sum(p.position()[1] for p in points) / len(points)
        z = sum(p.position()[2] for p in points) / len(points)
        return hou.Vector3(x,y,z)
    center_a = avg_pos(room_a_pts)
    center_b = avg_pos(room_b_pts)

    # En yakın oda noktası çiftini bul (oda merkezlerine en yakın noktalar)
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

    # BFS ile yol ara
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

    # Yol yoksa çık
    if end_pt not in came_from:
        print("Yol bulunamadı!")
        return

    # Yol üzerindeki tile_type "wall" ise "empty" yap (yolu aç)
    path = []
    cur = end_pt
    while cur is not None:
        path.append(cur)
        cur = came_from[cur]
    path.reverse()

    for pt in path:
        tile_type = pt.stringAttribValue("tile_type")
        if tile_type == "wall":
            pt.setAttribValue("tile_type", "empty")

# Ana script başlıyor

# Önce odaları bul
rooms = find_rooms_by_class(geo, "class")
room_ids = sorted(rooms.keys())

# Sıralı olarak odaları birbirine bağla
for i in range(len(room_ids)-1):
    connect_two_rooms(geo, rooms[room_ids[i]], rooms[room_ids[i+1]])

print(f"{len(room_ids)} oda birbirine bağlandı.")
