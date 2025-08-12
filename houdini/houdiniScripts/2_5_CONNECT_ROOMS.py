import hou
from collections import defaultdict

node = hou.pwd()
geo = node.geometry()

# --- 1. class attribute kontrolü ---
class_attrib = geo.findPointAttrib("class")
if not class_attrib:
    raise ValueError("Point attribute 'class' bulunamadı. Önce carve rooms scriptini çalıştırmalısınız.")

# --- 2. Odaları grupla ---
rooms = defaultdict(list)
for pt in geo.points():
    cid = pt.attribValue("class")
    if cid != -1:  # -1 oda dışı demek
        rooms[cid].append(pt)

if len(rooms) < 2:
    raise ValueError("Bağlanacak en az iki oda bulunamadı.")

print(f"{len(rooms)} oda bulundu. Bağlantı oluşturuluyor...")

# --- 3. Odaların merkez noktalarını bul ---
room_centers = {}
for cid, pts in rooms.items():
    avg_x = sum(p.position()[0] for p in pts) / len(pts)
    avg_z = sum(p.position()[2] for p in pts) / len(pts)
    room_centers[cid] = (avg_x, avg_z)

# --- 4. Odaları birbirine bağla (basit MST yaklaşımı) ---
connected = set()
edges = []

# En yakın odaları sırayla bağla
unconnected_rooms = list(room_centers.keys())
connected.add(unconnected_rooms.pop(0))

while unconnected_rooms:
    shortest_dist = None
    closest_pair = None
    for r1 in connected:
        for r2 in unconnected_rooms:
            dx = room_centers[r1][0] - room_centers[r2][0]
            dz = room_centers[r1][1] - room_centers[r2][1]
            dist = dx * dx + dz * dz
            if shortest_dist is None or dist < shortest_dist:
                shortest_dist = dist
                closest_pair = (r1, r2)

    if closest_pair:
        r1, r2 = closest_pair
        edges.append((r1, r2))
        connected.add(r2)
        unconnected_rooms.remove(r2)

# --- 5. Yolları işaretle ---
for r1, r2 in edges:
    x1, z1 = room_centers[r1]
    x2, z2 = room_centers[r2]

    # Basit L şeklinde koridor
    for pt in geo.points():
        px, py, pz = pt.position()
        if (min(x1, x2) <= px <= max(x1, x2) and abs(pz - z1) < 0.5) or \
           (min(z1, z2) <= pz <= max(z1, z2) and abs(px - x2) < 0.5):
            if pt.attribValue("class") == -1:  # sadece boş alan olmayanlara
                pt.setAttribValue("tile_type", "empty")

print("Oda bağlantıları tamamlandı.")
